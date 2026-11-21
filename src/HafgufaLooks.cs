using System.Collections.Generic;
using UnityEngine;

namespace IslandsOfValheim
{
    // What makes the game's serpent Rán's beast, beyond its size and strength. The user asked for it to look
    // unlike a common serpent, through its colours alone:
    // - its own skin repainted in two tones, from the light and shade of the game's scales: an abyss-black body,
    //   its lightest scales a drowned, bone-pale grey-blue (a cast towards one colour, as before 1.10.87, kept the
    //   serpent's look);
    // - the palest of those scales glowing with the drowned souls' cold light, slowly waxing and waning, as if
    //   the souls Rán holds shone through its skin;
    // - a few souls' lights caught in its body, hung on its tail bones.
    // (1.10.79-1.10.85 also tried rocks, weed, net ropes and eyes on it. Every way left some of them floating off
    // it, so 1.10.86 dropped them. Colours can't float off.)
    //
    // The same two-tone repaint is the tale's keepers' look too (BossKit.TwoTone), each in its own colours.
    static class HafgufaLooks
    {
        static readonly Color Abyss = new Color(0.012f, 0.025f, 0.05f);
        static readonly Color Deep = new Color(0.04f, 0.13f, 0.17f);
        static readonly Color Pale = new Color(0.55f, 0.72f, 0.76f);

        // A body's two tones: its darkest parts `dark`, the middle `mid`, the lightest `pale`, and the palest of
        // all glowing `glow`; at least this gloss.
        internal class Palette
        {
            internal string who;
            internal Color dark, mid, pale, glow;
            internal float gloss;
            internal bool keepOwnGlow; // a part that glows in the game's own (eyes) keeps its glow
            // How the light and shade are read: the share of the texture called darkest (low) and lightest (high),
            // eased (smooth) or straight; and whether its palest parts glow. Hafgufa's big, smooth scales took
            // the strong way. The keepers' small textures (64-256 px, drawn sharp, pixel by pixel) came out in
            // blocks and specks that way (2.1.5): theirs is read over nearly its whole spread, straight, with
            // no glowing parts, so the game's own shading carries over in the new colours.
            internal float low = 0.3f, high = 0.92f;
            internal bool smooth = true, glowParts = true;
            // Lightness kept as the game's (×brightness), only the colours from the palette: even read straight,
            // a keeper's narrow shading stretched to the palette's dark-to-pale still came out in blocks (2.1.6);
            // kept, its pixels differ only as much as the game's own do.
            internal bool keepLight;
            internal float brightness = 1f, saturation = 1f; // (keepLight only)
        }

        internal const string GlowSuffix = "_IoVGlow"; // the glow pictures' name ends so (HafgufaGlow finds them by it)

        internal static void Dress(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == "Tail3" || t.name == "Tail6" || t.name == "Tail9")
                {
                    GameObject l = new GameObject("IoV_HafgufaSoul");
                    l.transform.SetParent(t, false);
                    Light light = l.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = RanNet.SoulColour;
                    light.range = 4f;
                    light.intensity = 1.3f;
                    light.shadows = LightShadows.None;
                }
        }

        // Its skin in two tones, and the glow (on the prefab, once). Nothing on a dedicated server.
        internal static void Colour(GameObject go)
        {
            Repaint(go, new Palette { who = "Hafgufa", dark = Abyss, mid = Deep, pale = Pale, glow = RanNet.SoulColour, gloss = 0.45f });
        }

        internal static void Repaint(GameObject go, Palette p)
        {
            fallsAs.Add(go.name); // (even on a dedicated server: a death it runs still marks the ragdoll)
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Dictionary<string, Material> copies = new Dictionary<string, Material>();
            // The game shifts a creature's hue by its level (Hafgufa is level 2): not ours, its colours are set.
            foreach (LevelEffects level in go.GetComponentsInChildren<LevelEffects>(true))
                if (level.m_levelSetups != null)
                    foreach (LevelEffects.LevelSetup setup in level.m_levelSetups)
                    {
                        Plugin.Debug(p.who + ": the game's level look (hue " + setup.m_hue + ", saturation " + setup.m_saturation + ", value " + setup.m_value + ", glow " + setup.m_setEmissiveColor + ") left out");
                        setup.m_hue = 0f;
                        setup.m_saturation = 0f;
                        setup.m_value = 0f;
                        setup.m_setEmissiveColor = false;
                    }
            Dictionary<Texture, Texture2D[]> done = new Dictionary<Texture, Texture2D[]>();
            bool glows = false;
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || !source.HasProperty("_MainTex") || source.GetTexture("_MainTex") == null) continue;
                    Texture skin = source.GetTexture("_MainTex");
                    Texture2D[] painted;
                    if (!done.TryGetValue(skin, out painted)) done[skin] = painted = Paint(skin, p);
                    Material copy = new Material(source);
                    copy.name = source.name + "_" + p.who;
                    copy.SetTexture("_MainTex", painted[0]);
                    if (copy.HasProperty("_Glossiness")) copy.SetFloat("_Glossiness", Mathf.Max(copy.GetFloat("_Glossiness"), p.gloss));
                    List<string> emission = new List<string>();
                    for (int k = 0; k < copy.shader.GetPropertyCount(); k++)
                        if (copy.shader.GetPropertyName(k).IndexOf("Emission", System.StringComparison.OrdinalIgnoreCase) >= 0) emission.Add(copy.shader.GetPropertyName(k));
                    bool lit = p.glowParts && copy.HasProperty("_EmissionMap") && copy.HasProperty("_EmissionColor");
                    bool own = lit && p.keepOwnGlow && source.GetTexture("_EmissionMap") != null;
                    if (lit && !own)
                    {
                        copy.SetTexture("_EmissionMap", painted[1]);
                        copy.EnableKeyword("_EMISSION");
                        copy.SetColor("_EmissionColor", Color.white * HafgufaGlow.Low);
                        glows = true;
                    }
                    Plugin.Debug(p.who + "'s skin " + source.name + " (" + copy.shader.name + ", " + skin.width + "x" + skin.height + "): two tones" +
                        (own ? ", its own glow kept" : lit ? ", its palest parts glowing" : ", no glow (its shader has " + (emission.Count > 0 ? string.Join(", ", emission.ToArray()) : "no emission") + ")"));
                    materials[i] = copy;
                    if (!copies.ContainsKey(source.name)) copies[source.name] = copy;
                }
                renderer.sharedMaterials = materials;
            }
            if (glows && go.GetComponent<HafgufaGlow>() == null) go.AddComponent<HafgufaGlow>();
            copiesOf[go.name] = copies;
            if (glows) glowing.Add(go.name);
        }

        // ---- the body when it falls ----
        //
        // The game's ragdoll is the vanilla creature's, in the vanilla colours: the repainted body turned back
        // into the plain creature as it fell. The client the death runs on marks the ragdoll with whose it is
        // (in its ZDO, so every client knows), and each client puts that body's repainted materials in place of
        // the same vanilla ones on it (anything else on it is left as it is).
        static readonly HashSet<string> fallsAs = new HashSet<string>();
        static readonly Dictionary<string, Dictionary<string, Material>> copiesOf = new Dictionary<string, Dictionary<string, Material>>();
        static readonly HashSet<string> glowing = new HashSet<string>();
        const string FellKey = "IoV_FellAs";

        [HarmonyLib.HarmonyPatch(typeof(Character), "OnRagdollCreated")]
        static class FallCharacter { static void Postfix(Character __instance, Ragdoll ragdoll) { Fell(__instance, ragdoll); } }

        [HarmonyLib.HarmonyPatch(typeof(Humanoid), "OnRagdollCreated")]
        static class FallHumanoid { static void Postfix(Humanoid __instance, Ragdoll ragdoll) { Fell(__instance, ragdoll); } }

        static void Fell(Character body, Ragdoll ragdoll)
        {
            if (body == null || ragdoll == null || fallsAs.Count == 0) return;
            string name = Utils.GetPrefabName(body.gameObject);
            if (!fallsAs.Contains(name)) return;
            ZNetView nview = ragdoll.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid()) nview.GetZDO().Set(FellKey, name);
            Dress(ragdoll, name);
        }

        [HarmonyLib.HarmonyPatch(typeof(Ragdoll), "Start")]
        static class FallSeen
        {
            static void Postfix(Ragdoll __instance)
            {
                if (copiesOf.Count == 0) return;
                ZNetView nview = __instance.GetComponent<ZNetView>();
                string name = nview != null && nview.IsValid() ? nview.GetZDO().GetString(FellKey, "") : "";
                if (name.Length > 0) Dress(__instance, name);
            }
        }

        static void Dress(Ragdoll ragdoll, string name)
        {
            Dictionary<string, Material> copies;
            if (!copiesOf.TryGetValue(name, out copies) || copies.Count == 0) return; // (a dedicated server: nothing to show)
            int dressed = 0, parts = 0;
            foreach (Renderer r in ragdoll.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                parts++;
                Material[] materials = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    Material copy;
                    if (copies.TryGetValue(materials[i].name.Replace(" (Instance)", ""), out copy)) { materials[i] = copy; changed = true; }
                }
                if (!changed) continue;
                r.sharedMaterials = materials;
                dressed++;
            }
            if (glowing.Contains(name) && ragdoll.GetComponent<HafgufaGlow>() == null) ragdoll.gameObject.AddComponent<HafgufaGlow>();
            Plugin.Debug(name + " fell: " + dressed + " of its ragdoll's " + parts + " parts in its own colours");
        }

        // Its skin repainted (0) and where it glows (1), from the light and shade of the game's own: the darkest
        // part (Hafgufa's scales) abyss-black, the middle a deep sea-blue, the lightest drowned-pale; the palest
        // glow. Which is light or dark is measured on this texture itself (its own spread), so the two tones show
        // whatever the game's colours are.
        static Texture2D[] Paint(Texture source, Palette p)
        {
            Texture2D main = Recolor.Copy(source, new Rect(0f, 0f, source.width, source.height), true);
            Color[] pixels = main.GetPixels();
            List<float> sample = new List<float>();
            for (int i = 0; i < pixels.Length; i += 7) sample.Add(Light(pixels[i]));
            sample.Sort();
            int last = sample.Count - 1;
            float lo = sample[Mathf.Min(last, (int)(sample.Count * p.low))], hi = sample[Mathf.Min(last, (int)(sample.Count * p.high))];
            if (hi - lo < 0.05f) hi = lo + 0.05f;
            Color[] glow = new Color[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                float light = Light(pixels[i]);
                float u = Mathf.Clamp01((light - lo) / (hi - lo));
                float t = p.smooth ? Mathf.SmoothStep(0f, 1f, u) : u;
                Color c = t < 0.6f ? Color.Lerp(p.dark, p.mid, t / 0.6f) : Color.Lerp(p.mid, p.pale, (t - 0.6f) / 0.4f);
                if (p.keepLight)
                {
                    // its colour from the palette, its lightness the game's own (×brightness)
                    float have = Light(c);
                    if (have > 0.0001f) c *= light * p.brightness / have;
                    // muted: the game's pixel-by-pixel shading shows as blocks in a strong colour (2.1.7: a
                    // bright green Ketill, a vivid purple Ormr), not in a soft one
                    float grey = light * p.brightness;
                    c = Color.Lerp(new Color(grey, grey, grey), c, p.saturation);
                    c.r = Mathf.Min(1f, c.r); c.g = Mathf.Min(1f, c.g); c.b = Mathf.Min(1f, c.b);
                }
                c.a = pixels[i].a;
                pixels[i] = c;
                float g = Mathf.Clamp01((t - 0.72f) / 0.28f);
                Color e = p.glow * (g * g);
                e.a = 1f;
                glow[i] = e;
            }
            main.SetPixels(pixels);
            main.name = source.name + "_" + p.who;
            main.wrapMode = source.wrapMode;
            main.filterMode = source.filterMode;
            main.anisoLevel = source.anisoLevel;
            main.Apply(true, true);
            Texture2D lit = new Texture2D(main.width, main.height, TextureFormat.RGBA32, true);
            lit.SetPixels(glow);
            lit.name = source.name + GlowSuffix;
            lit.wrapMode = source.wrapMode;
            lit.filterMode = source.filterMode;
            lit.Apply(true, true);
            Plugin.Debug(p.who + "'s skin painted: its light " + lo.ToString("F2") + " to " + hi.ToString("F2") + " (drawn " + source.filterMode + ")");
            return new[] { main, lit };
        }

        static float Light(Color c) { return c.r * 0.3f + c.g * 0.59f + c.b * 0.11f; }
    }

    // The souls' glow in its pale scales, slowly waxing and waning (each client its own). Set on its materials as
    // they are when it's in the world (the game makes its own copy of them for its level). (The keepers' glow too.)
    class HafgufaGlow : MonoBehaviour
    {
        internal const float Low = 0.35f, High = 0.95f, Period = 5f;
        readonly List<Material> lit = new List<Material>();
        float refresh;

        void Update()
        {
            refresh -= Time.deltaTime;
            if (refresh <= 0f)
            {
                refresh = 2f;
                lit.Clear();
                foreach (Renderer r in GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer) continue;
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") != null && m.GetTexture("_EmissionMap").name.EndsWith(HafgufaLooks.GlowSuffix))
                            lit.Add(m);
                }
            }
            float k = Mathf.Lerp(Low, High, 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / Period));
            foreach (Material m in lit) if (m != null) m.SetColor("_EmissionColor", Color.white * k);
        }
    }
}
