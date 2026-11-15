#if IOV_DEV
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandsOfValheim
{
    // (development) iov_perf: what costs the frame rate where you stand. Counts what's drawn, lit and moving within
    // 60 m of the camera; then switches each kind of thing off in turn for a few seconds (people drawn, people's
    // animation, capes, people's shadows, lights, fire and smoke, every shadow, Valhalla's people's own script),
    // measures the frame rate with and without it, and puts it back as it was. Stand still, don't touch anything:
    // about 2 minutes.
    // The table goes to the console and the log ("iov_perf:").
    static class ValhallaPerf
    {
        const float Near = 60f, Settle = 1.2f, Measure = 1.5f;
        static bool running;

        internal static void Run(Terminal context)
        {
            if (running) { context.AddString("iov_perf is measuring already."); return; }
            if (Plugin.Instance == null || Utils.GetMainCamera() == null) return;
            context.AddString("iov_perf: measuring for about 2 minutes. Stand still and don't touch anything; the result comes here and in the log.");
            Plugin.Instance.StartCoroutine(Test(context));
        }

        class Step
        {
            internal string name;
            internal System.Action off, on;
        }

        static IEnumerator Test(Terminal context)
        {
            running = true;
            Camera cam = Utils.GetMainCamera();
            Vector3 eye = cam.transform.position;
            List<string> lines = new List<string>();

            // ---- what's here ----
            List<Renderer> renderers = new List<Renderer>();
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
                if (r != null && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy && Vector3.Distance(eye, r.bounds.center) < Near) renderers.Add(r);
            int skinned = 0, casting = 0, visible = 0;
            foreach (Renderer r in renderers)
            {
                if (r is SkinnedMeshRenderer) skinned++;
                if (r.shadowCastingMode != ShadowCastingMode.Off) casting++;
                if (r.isVisible) visible++;
            }
            List<Light> lights = new List<Light>();
            int shadowed = 0;
            foreach (Light l in Object.FindObjectsOfType<Light>())
                if (l != null && l.enabled && l.type != LightType.Directional && l.gameObject.activeInHierarchy && Vector3.Distance(eye, l.transform.position) < Near + l.range)
                {
                    lights.Add(l);
                    if (l.shadows != LightShadows.None) shadowed++;
                }
            List<ParticleSystem> particles = new List<ParticleSystem>();
            foreach (ParticleSystem p in Object.FindObjectsOfType<ParticleSystem>())
                if (p != null && p.isPlaying && Vector3.Distance(eye, p.transform.position) < Near) particles.Add(p);
            List<Cloth> capes = new List<Cloth>();
            foreach (Cloth c in Object.FindObjectsOfType<Cloth>()) if (c != null && c.enabled && Vector3.Distance(eye, c.transform.position) < Near) capes.Add(c);
            // The game's newer capes (MagicaCloth, on what VisEquipment puts on) aren't Unity's Cloth.
            List<Behaviour> magica = new List<Behaviour>();
            System.Type magicaType = MagicaType();
            if (magicaType != null)
                foreach (Object o in Object.FindObjectsOfType(magicaType))
                {
                    Behaviour b = o as Behaviour;
                    if (b != null && b.enabled && Vector3.Distance(eye, b.transform.position) < Near) magica.Add(b);
                }
            List<SpiritHuman> people = new List<SpiritHuman>();
            foreach (SpiritHuman h in Object.FindObjectsOfType<SpiritHuman>()) if (h != null && Vector3.Distance(eye, h.transform.position) < Near) people.Add(h);
            List<VisEquipment> settled = new List<VisEquipment>(); // dressed once and left be (SpiritHuman.Settle)
            foreach (SpiritHuman h in people) { VisEquipment v = h.GetComponent<VisEquipment>(); if (v != null && !v.enabled) settled.Add(v); }
            List<Animator> animators = new List<Animator>(), peopleAnimators = new List<Animator>();
            foreach (Animator a in Object.FindObjectsOfType<Animator>())
                if (a != null && a.enabled && Vector3.Distance(eye, a.transform.position) < Near)
                {
                    animators.Add(a);
                    if (a.GetComponentInParent<SpiritHuman>() != null) peopleAnimators.Add(a);
                }
            List<Renderer> peopleParts = new List<Renderer>();
            foreach (SpiritHuman h in people)
                foreach (Renderer r in h.GetComponentsInChildren<Renderer>(true))
                    if (r != null && !r.forceRenderingOff) peopleParts.Add(r);
            int characters = 0;
            foreach (Character c in Character.GetAllCharacters()) if (c != null && Vector3.Distance(eye, c.transform.position) < Near) characters++;

            lines.Add("iov_perf: within " + Near + " m of the camera at " + eye.ToString("F0") + ":");
            lines.Add("iov_perf:   renderers " + renderers.Count + " (" + visible + " in view, " + skinned + " skinned, " + casting + " casting shadows)");
            lines.Add("iov_perf:   lights " + lights.Count + " (" + shadowed + " with shadows); fire/smoke systems playing " + particles.Count);
            lines.Add("iov_perf:   people " + people.Count + " (" + peopleParts.Count + " parts drawn, " + peopleAnimators.Count + " animated); capes moving " + capes.Count + " (+" + magica.Count + " MagicaCloth on, those past 14 m resting" + (magicaType == null ? ": type not found" : "") + "); dressed once " + settled.Count +
                "; animators " + animators.Count + "; game characters " + characters);
            lines.Add("iov_perf:   heaviest by what's drawn: " + Heaviest(renderers));

            // ---- each off in turn ----
            List<Step> steps = new List<Step>();
            steps.Add(Toggle("people not drawn", peopleParts, r => r.forceRenderingOff, (r, v) => r.forceRenderingOff = v, true));
            steps.Add(Toggle("people not animated", peopleAnimators, a => a.enabled, (a, v) => a.enabled = v, false));
            steps.Add(Toggle("capes still", capes, c => c.enabled, (c, v) => c.enabled = v, false));
            steps.Add(Toggle("MagicaCloth capes still", magica, b => b.enabled, (b, v) => b.enabled = v, false));
            // (these two put back what was saved before: the frame rate should drop)
            Step dressing = Toggle("people dressed every frame again, as before", settled, v => v.enabled, (v, on) => v.enabled = on, true);
            System.Action dressOff = dressing.off, dressOn = dressing.on;
            dressing.off = () => { SpiritHuman.KeepDressing = true; dressOff(); };
            dressing.on = () => { SpiritHuman.KeepDressing = false; dressOn(); };
            steps.Add(dressing);
            steps.Add(new Step { name = "every grass patch out again, as before", off = () => ValhallaSky.AllPatches = true, on = () => ValhallaSky.AllPatches = false });
            List<Renderer> peopleCasting = peopleParts.FindAll(r => r.shadowCastingMode != ShadowCastingMode.Off);
            steps.Add(Toggle("people cast no shadow", peopleCasting, r => r.shadowCastingMode, (r, v) => r.shadowCastingMode = v, ShadowCastingMode.Off));
            steps.Add(Toggle("lights off", lights, l => l.enabled, (l, v) => l.enabled = v, false));
            List<Renderer> particleDraw = new List<Renderer>();
            foreach (ParticleSystem p in particles) { Renderer r = p.GetComponent<Renderer>(); if (r != null && !r.forceRenderingOff) particleDraw.Add(r); }
            steps.Add(Toggle("fire and smoke not drawn", particleDraw, r => r.forceRenderingOff, (r, v) => r.forceRenderingOff = v, true));
            List<Renderer> allCasting = renderers.FindAll(r => r.shadowCastingMode != ShadowCastingMode.Off);
            steps.Add(Toggle("nothing casts a shadow", allCasting, r => r.shadowCastingMode, (r, v) => r.shadowCastingMode = v, ShadowCastingMode.Off));
            ValhallaPeople hall = ValhallaPeople.Current;
            if (hall != null) steps.Add(new Step { name = "Valhalla people's script stopped", off = () => hall.enabled = false, on = () => { if (hall != null) hall.enabled = true; } });

            // Each step measured as it is, without, without, as it is (one after another, so the frame rate's own
            // slow drift, about 13 fps over the old 40 s run, falls equally on both sides and cancels out).
            yield return new WaitForSecondsRealtime(Settle);
            float baseline = 0f;
            yield return Fps(v => baseline = v);
            lines.Add("iov_perf: frame rate as it is: " + baseline.ToString("F0"));
            foreach (Step s in steps)
            {
                float a1 = 0f, b1 = 0f, b2 = 0f, a2 = 0f;
                yield return Fps(v => a1 = v);
                try { s.off(); } catch (System.Exception e) { Plugin.Log.LogWarning("iov_perf: " + s.name + ": " + e.Message); }
                yield return new WaitForSecondsRealtime(Settle);
                yield return Fps(v => b1 = v);
                yield return Fps(v => b2 = v);
                try { s.on(); } catch (System.Exception e) { Plugin.Log.LogWarning("iov_perf: " + s.name + " back: " + e.Message); }
                yield return new WaitForSecondsRealtime(Settle);
                yield return Fps(v => a2 = v);
                float with = (a1 + a2) * 0.5f, without = (b1 + b2) * 0.5f, change = without - with;
                lines.Add("iov_perf:   " + s.name + ": " + with.ToString("F0") + " -> " + without.ToString("F0") + " (" + (change >= 0f ? "+" : "") + change.ToString("F0") + ")");
            }
            float after = 0f;
            yield return Fps(v => after = v);
            lines.Add("iov_perf: frame rate with all back: " + after.ToString("F0"));
            foreach (string line in lines) { Plugin.Log.LogInfo(line); if (context != null) context.AddString(line.Substring("iov_perf: ".Length)); }
            running = false;
        }

        static System.Type MagicaType()
        {
            foreach (System.Reflection.Assembly a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!a.GetName().Name.Contains("Magica")) continue;
                System.Type[] types;
                try { types = a.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
                foreach (System.Type t in types) if (t != null && t.Name == "MagicaCloth" && typeof(Behaviour).IsAssignableFrom(t)) return t;
            }
            return null;
        }

        // Each thing's setting kept, the new one put in its place; then each one's own put back.
        static Step Toggle<T, V>(string name, List<T> things, System.Func<T, V> get, System.Action<T, V> set, V off) where T : Object
        {
            Dictionary<T, V> was = new Dictionary<T, V>();
            return new Step
            {
                name = name + " (" + things.Count + ")",
                off = () => { was.Clear(); foreach (T t in things) if (t != null) { was[t] = get(t); set(t, off); } },
                on = () => { foreach (KeyValuePair<T, V> kv in was) if (kv.Key != null) set(kv.Key, kv.Value); },
            };
        }

        static IEnumerator Fps(System.Action<float> result)
        {
            int frames = 0;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < Measure) { frames++; yield return null; }
            result(frames / Mathf.Max(0.001f, Time.realtimeSinceStartup - start));
        }

        // What's drawn the most, by the name of the thing it's part of (its top under the scene, or the prefab).
        static string Heaviest(List<Renderer> renderers)
        {
            Dictionary<string, int> count = new Dictionary<string, int>();
            foreach (Renderer r in renderers)
            {
                Transform t = r.transform;
                while (t.parent != null && t.parent.parent != null && t.parent.parent.parent != null && t.GetComponent<ZNetView>() == null && t.GetComponent<SpiritHuman>() == null) t = t.parent;
                string n = t.name.Replace("(Clone)", "").Trim();
                int c;
                count.TryGetValue(n, out c);
                count[n] = c + 1;
            }
            List<KeyValuePair<string, int>> sorted = new List<KeyValuePair<string, int>>(count);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < sorted.Count && i < 12; i++) s.Append(i > 0 ? ", " : "").Append(sorted[i].Key).Append(" ").Append(sorted[i].Value);
            return s.ToString();
        }
    }
}
#endif
