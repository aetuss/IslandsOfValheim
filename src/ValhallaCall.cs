using System.Collections.Generic;
using UnityEngine;

namespace IslandsOfValheim
{
    // Odin's call, after Sævar's tale ends (he walked into Odin's light; Story.FinalDone):
    // - the next morning (by the world's days: the day after the one the end was first seen on, from dawn),
    //   the stone gate at his harbour wakes (the world's key ValhallaGate.OpenKey);
    // - Hugin, the game's own raven, comes to each player with the call (once per character: the game keeps
    //   which of his words you've heard);
    // - and over the harbour a pillar of light stands, for each player till they first go through: seen
    //   from about 1.5 km, faint at first, clearer the closer they sail. It's this client's own, not the
    //   harbour's (a zone's things only load near you), set over the harbour's middle, and over its gate once
    //   that's loaded.
    // Each client works it out for itself from the world's keys; the first to see a step due sets its key.
    class ValhallaCall : MonoBehaviour
    {
        internal const string CallKey = "iovvalhallacall";          // Hugin's words (the player's seen list)
        internal const string EnteredKey = "iovvalhallaentered";    // first time through the gate (the same)
        const string DayKey = "iovcallday";                          // + the day the end was first seen on
        internal static bool devNow;                                 // iov_call now: due at once, tale or not

        float next;
        bool told;
        CallLight beam;

        // The game's raven-style text sits a little low over the middle of the view; ours goes up a little while
        // it shows, and back when it's closed (others that use it keep theirs).
        static RectTransform raised;
        static Vector2 raisedFrom;
        const float RaiseBy = 60f;

        static void Raise()
        {
            if (TextViewer.instance == null || TextViewer.instance.m_ravenRoot == null) return;
            RectTransform panel = TextViewer.instance.m_ravenRoot.GetComponent<RectTransform>();
            if (panel == null || raised != null) return;
            raised = panel;
            raisedFrom = panel.anchoredPosition;
            panel.anchoredPosition = raisedFrom + new Vector2(0f, RaiseBy);
        }

        void Update()
        {
            if (raised != null && (TextViewer.instance == null || !TextViewer.instance.IsVisible()))
            {
                raised.anchoredPosition = raisedFrom;
                raised = null;
            }
            if (Time.time < next) return;
            next = Time.time + 1f;
            ZoneSystem zones = ZoneSystem.instance;
            Player player = Player.m_localPlayer;
            EnvMan env = EnvMan.instance;
            if (zones == null || player == null || env == null) { Light(false, player); return; }
            bool open = zones.GetGlobalKey(ValhallaGate.OpenKey);
            if (!open && (devNow || Story.Key(Story.FinalDone)))
            {
                int day = devNow ? int.MinValue : EndDay(zones, env);
                if (devNow || (env.GetDay() > day && env.GetDayFraction() >= 0.25f))
                {
                    zones.SetGlobalKey(ValhallaGate.OpenKey);
                    open = true;
                    Plugin.Log.LogInfo("Odin's call: the harbour's gate wakes");
                }
            }
            if (!open) { Light(false, player); return; }
            if (!told && !player.HaveSeenTutorial(CallKey))
            {
                told = true;
                if (Raven.m_tutorialsEnabled)
                {
                    Raven.AddTempText(CallKey, "$iov_call_topic", "$iov_call_text", "", false);
                    Plugin.Debug("Odin's call: Hugin has it for you");
                }
                else if (TextViewer.instance != null)
                {
                    // Hugin's tips turned off in the settings: he doesn't come at all, so his words come the way
                    // he'd show them, without him
                    TextViewer.instance.ShowText(TextViewer.Style.Raven, "$iov_call_topic", "$iov_call_text", false);
                    Raise();
                    player.SetSeenTutorial(CallKey);
                    Plugin.Debug("Odin's call: shown without Hugin (his tips are off)");
                }
            }
            Light(!player.HaveSeenTutorial(EnteredKey), player);
        }

        // The day the tale's end was first seen on, by anyone (set now if no one has yet).
        static int EndDay(ZoneSystem zones, EnvMan env)
        {
            int found = int.MaxValue;
            foreach (string key in zones.GetGlobalKeys())
            {
                if (!key.StartsWith(DayKey)) continue;
                int day;
                if (int.TryParse(key.Substring(DayKey.Length).Trim(), out day)) found = Mathf.Min(found, day);
            }
            if (found != int.MaxValue) return found;
            int today = env.GetDay();
            zones.SetGlobalKey(DayKey + today);
            Plugin.Log.LogInfo("Odin's call: the tale's end seen on day " + today + "; the call comes the next morning");
            return today;
        }

        void Light(bool on, Player player)
        {
            if (!on) { if (beam != null) { Destroy(beam.gameObject); beam = null; } return; }
            if (beam == null) beam = CallLight.Make();
            // over the harbour's gate if it's here, else over the harbour's middle
            foreach (ValhallaGate gate in ValhallaGate.All)
                if (gate != null && !gate.valhallaSide) { beam.transform.position = gate.transform.position; return; }
            foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.GetLocationList())
                if (instance.m_location != null && instance.m_location.m_prefabName == IsletBuilder.HarbourName)
                {
                    beam.transform.position = new Vector3(instance.m_position.x, ZoneSystem.instance.m_waterLevel + IsletBuilder.SmallPlateau, instance.m_position.z);
                    return;
                }
        }

        // iov_call reset (development): as if the tale had just ended (the gate dark, Hugin's words and the
        // light not yet seen).
        internal static void Reset()
        {
            devNow = false;
            ZoneSystem zones = ZoneSystem.instance;
            if (zones != null)
            {
                zones.RemoveGlobalKey(ValhallaGate.OpenKey);
                zones.RemoveGlobalKey(ValhallaSpeaker.WelcomedKey);
                foreach (string key in new List<string>(zones.GetGlobalKeys()))
                    if (key.StartsWith(DayKey)) zones.RemoveGlobalKey(key);
            }
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                HashSet<string> seen = HarmonyLib.Traverse.Create(player).Field("m_shownTutorials").GetValue<HashSet<string>>();
                if (seen != null) { seen.Remove(CallKey); seen.Remove(EnteredKey); seen.Remove(ValhallaSky.FoundKey); }
            }
            foreach (ValhallaCall call in Object.FindObjectsOfType<ValhallaCall>()) call.told = false;
        }
    }

    // A tall pillar of soft gold light (the light Sævar left in, standing), on this client: seen from far
    // off only faintly, stronger as you come; it gently breathes.
    class CallLight : MonoBehaviour
    {
        const float Height = 260f, Width = 7f, Seen = 1500f, Full = 300f;
        Material material;
        Light glow;

        internal static CallLight Make()
        {
            Shader shader = null;
            foreach (string name in new[] { "Sprites/Default", "Legacy Shaders/Particles/Additive", "Unlit/Transparent" })
                if ((shader = Shader.Find(name)) != null) break;
            GameObject root = new GameObject("IoV_CallLight");
            CallLight light = root.AddComponent<CallLight>();
            if (shader != null)
            {
                light.material = new Material(shader);
                light.material.mainTexture = LightBeam.Soft();
                for (int i = 0; i < 4; i++)
                {
                    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.Destroy(quad.GetComponent<Collider>());
                    quad.transform.SetParent(root.transform, false);
                    bool core = i >= 2;
                    quad.transform.localPosition = new Vector3(0f, Height * 0.5f, 0f);
                    quad.transform.localRotation = Quaternion.Euler(0f, (i % 2) * 90f + (core ? 45f : 0f), 0f);
                    quad.transform.localScale = new Vector3(core ? Width * 0.45f : Width, Height, 1f);
                    Renderer renderer = quad.GetComponent<Renderer>();
                    renderer.sharedMaterial = light.material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
            GameObject lamp = new GameObject("IoV_CallLightGlow");
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = Vector3.up * 4f;
            light.glow = lamp.AddComponent<Light>();
            light.glow.type = LightType.Point;
            light.glow.color = new Color(1f, 0.88f, 0.62f);
            light.glow.range = 18f;
            light.glow.shadows = LightShadows.None;
            Plugin.Debug("Odin's call: the light over the harbour" + (shader != null ? " (" + shader.name + ")" : " (no see-through shader: glow only)"));
            return light;
        }

        void Update()
        {
            Camera cam = Utils.GetMainCamera();
            if (cam == null) return;
            Vector3 eye = cam.transform.position, foot = transform.position;
            float d = Vector2.Distance(new Vector2(eye.x, eye.z), new Vector2(foot.x, foot.z));
            float k = 1f - Mathf.SmoothStep(0f, 1f, (d - Full) / (Seen - Full));
            float breath = 0.85f + 0.15f * Mathf.Sin(Time.time * 0.8f);
            if (material != null) material.color = new Color(1f, 0.9f, 0.68f, 0.6f * k * breath);
            if (glow != null) glow.intensity = 2.2f * k * breath;
            // facing the eye, the quads would show edge-on now and then: turn the pillar with the camera
            Vector3 to = eye - foot;
            to.y = 0f;
            if (to.sqrMagnitude > 1f) transform.rotation = Quaternion.LookRotation(to.normalized);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
