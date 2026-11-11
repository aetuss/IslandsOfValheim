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
    static class HafgufaLooks
    {
        static readonly Color Abyss = new Color(0.012f, 0.025f, 0.05f);
        static readonly Color Deep = new Color(0.04f, 0.13f, 0.17f);
        static readonly Color Pale = new Color(0.55f, 0.72f, 0.76f);

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
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            // The game shifts a creature's hue by its level (Hafgufa is level 2): not ours, its colours are set.
            foreach (LevelEffects level in go.GetComponentsInChildren<LevelEffects>(true))
                if (level.m_levelSetups != null)
                    foreach (LevelEffects.LevelSetup setup in level.m_levelSetups)
                    {
                        Plugin.Debug("Hafgufa: the game's level look (hue " + setup.m_hue + ", saturation " + setup.m_saturation + ", value " + setup.m_value + ", glow " + setup.m_setEmissiveColor + ") left out");
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
                    if (!done.TryGetValue(skin, out painted)) done[skin] = painted = Paint(skin);
                    Material copy = new Material(source);
                    copy.name = source.name + "_Hafgufa";
                    copy.SetTexture("_MainTex", painted[0]);
                    if (copy.HasProperty("_Glossiness")) copy.SetFloat("_Glossiness", Mathf.Max(copy.GetFloat("_Glossiness"), 0.45f));
                    List<string> emission = new List<string>();
                    for (int p = 0; p < copy.shader.GetPropertyCount(); p++)
                        if (copy.shader.GetPropertyName(p).IndexOf("Emission", System.StringComparison.OrdinalIgnoreCase) >= 0) emission.Add(copy.shader.GetPropertyName(p));
                    bool lit = copy.HasProperty("_EmissionMap") && copy.HasProperty("_EmissionColor");
                    if (lit)
                    {
                        copy.SetTexture("_EmissionMap", painted[1]);
                        copy.EnableKeyword("_EMISSION");
                        copy.SetColor("_EmissionColor", Color.white * HafgufaGlow.Low);
                        glows = true;
                    }
                    Plugin.Debug("Hafgufa's skin " + source.name + " (" + copy.shader.name + ", " + skin.width + "x" + skin.height + "): two tones" +
                        (lit ? ", its pale scales glowing" : ", no glow (its shader has " + (emission.Count > 0 ? string.Join(", ", emission.ToArray()) : "no emission") + ")"));
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
            if (glows) go.AddComponent<HafgufaGlow>();
        }

        // Its skin repainted (0) and where it glows (1), from the light and shade of the game's own: the darkest
        // part of its scales abyss-black, the middle a deep sea-blue, the lightest drowned-pale; the palest glow.
        // Which is light or dark is measured on this texture itself (its own spread), so the two tones show
        // whatever the game's colours are.
        static Texture2D[] Paint(Texture source)
        {
            Texture2D main = Recolor.Copy(source, new Rect(0f, 0f, source.width, source.height), true);
            Color[] pixels = main.GetPixels();
            List<float> sample = new List<float>();
            for (int i = 0; i < pixels.Length; i += 7) sample.Add(Light(pixels[i]));
            sample.Sort();
            float lo = sample[(int)(sample.Count * 0.3f)], hi = sample[(int)(sample.Count * 0.92f)];
            if (hi - lo < 0.05f) hi = lo + 0.05f;
            Color[] glow = new Color[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Light(pixels[i]) - lo) / (hi - lo)));
                Color c = t < 0.6f ? Color.Lerp(Abyss, Deep, t / 0.6f) : Color.Lerp(Deep, Pale, (t - 0.6f) / 0.4f);
                c.a = pixels[i].a;
                pixels[i] = c;
                float g = Mathf.Clamp01((t - 0.72f) / 0.28f);
                Color e = RanNet.SoulColour * (g * g);
                e.a = 1f;
                glow[i] = e;
            }
            main.SetPixels(pixels);
            main.name = source.name + "_Hafgufa";
            main.wrapMode = source.wrapMode;
            main.filterMode = source.filterMode;
            main.anisoLevel = source.anisoLevel;
            main.Apply(true, true);
            Texture2D lit = new Texture2D(main.width, main.height, TextureFormat.RGBA32, true);
            lit.SetPixels(glow);
            lit.name = source.name + "_HafgufaGlow";
            lit.wrapMode = source.wrapMode;
            lit.filterMode = source.filterMode;
            lit.Apply(true, true);
            Plugin.Debug("Hafgufa's skin painted: its scales' light " + lo.ToString("F2") + " to " + hi.ToString("F2"));
            return new[] { main, lit };
        }

        static float Light(Color c) { return c.r * 0.3f + c.g * 0.59f + c.b * 0.11f; }
    }

    // The souls' glow in its pale scales, slowly waxing and waning (each client its own). Set on its materials as
    // they are when it's in the world (the game makes its own copy of them for its level).
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
                        if (m != null && m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") != null && m.GetTexture("_EmissionMap").name.EndsWith("_HafgufaGlow"))
                            lit.Add(m);
                }
            }
            float k = Mathf.Lerp(Low, High, 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / Period));
            foreach (Material m in lit) if (m != null) m.SetColor("_EmissionColor", Color.white * k);
        }
    }
}
