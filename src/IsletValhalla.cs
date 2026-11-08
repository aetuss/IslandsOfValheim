using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Valhalla (the Valhalla arc, work in progress): a wide rock floating over a sea of clouds, high above the
    // world, in an eternal golden dusk.
    //
    // It's a location like any other, out at sea: its root sits on the water, and everything of Valhalla hangs
    // SkyHeight metres above it (IoV_SkyFloor), as the game's dungeons hang their insides over theirs. Not
    // above 3000 m, where the game counts a place as indoors (no sun shadows, no outdoor sky). The ground is
    // our own mesh, like the small islets'; the world below is hidden by the clouds and the fog.
    //
    // The sky, light, fog and sounds are its own weather (IoV_Valhalla), and the sun holds still at dusk, on
    // each client for its own player while they are there (ValhallaSky). Whoever steps off the edge is caught
    // before the fall and set back on the rock.
    static partial class IsletBuilder
    {
        internal const string ValhallaBase = "IoVTest_Base_Valhalla"; // the bare rock, to lay out its draft on
        internal const float SkyHeight = 2000f;   // the rock's floor, above the sea
        internal const string SkyFloorName = "IoV_SkyFloor"; // the dev tool measures a sky draft's heights from it
        internal const string SkyGroundName = "IoV_ValhallaGround";
        internal const float ValhallaEdge = 82f;  // the rock's edge, on average
        const float RockDepth = 150f;             // from its meadow down to the tip of its underside
        const int RockSegments = 240, TopRings = 70, SideRings = 34;

        internal static void RegisterValhallaBase()
        {
            foreach (IsletTier t in IsletTier.All) if (t.Biome == Heightmap.Biome.Meadows) { tier = t; break; }
            locationName = ValhallaBase;
            root = ZoneManager.Instance.CreateLocationContainer(ValhallaBase);
            GameObject sky = new GameObject(SkyFloorName);
            sky.transform.SetParent(root.transform, false);
            sky.transform.localPosition = new Vector3(0f, SkyHeight, 0f);
            parent = sky.transform;
            built = 0;
            missing = 0;
            seed = 911f;

            BuildFloatingRock(sky.transform);
            ValhallaClouds.Build(sky.transform);
            ValhallaClouds.Motes(sky.transform);
            sky.AddComponent<ValhallaSky>();

            Location location = root.AddComponent<Location>();
            location.m_applyRandomDamage = false;
            location.m_noBuild = false; // the bare rock is for laying out: pieces built here with the hammer go into the draft
            location.m_exteriorRadius = ValhallaEdge + 20f;

            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.Quantity = 0; // placed by iov_goto draft_valhalla (later: when the way to it opens)
            config.SnapToWater = true;
            config.ClearArea = false;
            config.RandomRotation = false;
            config.ExteriorRadius = ValhallaEdge + 20f;
            config.Group = ValhallaBase;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + ValhallaBase + " (Valhalla's bare rock, " + SkyHeight + " m up)");
        }

        // ---- the rock ----

        // Its outline: wandering, so it doesn't read as a circle.
        static float RockEdge(float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return ValhallaEdge * (1f + 0.11f * Noise(c * 1.4f + 3f, s * 1.4f + 3f) + 0.05f * Noise(c * 3.5f + 9f, s * 3.5f + 9f));
        }

        // Its meadow: rolling gently, a metre or two.
        internal static float RockMeadow(float x, float z)
        {
            return 1.1f * Noise(x * 0.018f + 5f, z * 0.018f + 5f) + 0.45f * Noise(x * 0.05f + 15f, z * 0.05f + 15f)
                + 0.08f * Noise(x * 0.4f + 25f, z * 0.4f + 25f);
        }

        // One mesh, two parts: the meadow on top (the game's terrain look: grass, and rock where it's steep),
        // and the rock underneath, which narrows, ragged, to a point far down in the clouds, as a rock torn
        // out of the ground would. The rim rounds over a little lip before the rock falls away.
        static void BuildFloatingRock(Transform sky)
        {
            int perRing = RockSegments + 1; // the last repeats the first, so the textures wrap without a seam
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color32> colors = new List<Color32>();
            List<int> top = new List<int>(), under = new List<int>();
            Color32 grass = Heightmap.GetBiomeColor(Heightmap.Biome.Meadows);
            float span = ValhallaEdge * 2.6f;

            // The meadow: rings out from the middle, each following the outline.
            for (int ring = 0; ring <= TopRings; ring++)
            {
                float t = (float)ring / TopRings;
                for (int seg = 0; seg < perRing; seg++)
                {
                    float a = seg * Mathf.PI * 2f / RockSegments;
                    float r = t * RockEdge(a);
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    float lip = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.9f) / 0.1f));
                    verts.Add(new Vector3(x, RockMeadow(x, z) - lip * 1.6f, z));
                    uvs.Add(new Vector2(x / span + 0.5f, z / span + 0.5f));
                    colors.Add(grass);
                }
            }
            // The underside: on from the rim, down and in, ragged, to the tip.
            int sideStart = verts.Count;
            for (int ring = 1; ring <= SideRings; ring++)
            {
                float d = (float)ring / SideRings;
                for (int seg = 0; seg < perRing; seg++)
                {
                    float a = seg * Mathf.PI * 2f / RockSegments;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    float edge = RockEdge(a);
                    // a little overhang under the lip, then narrowing faster and faster
                    float bulge = 1f + 0.05f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(d * 5f));
                    float narrow = Mathf.Pow(1f - d, 0.75f);
                    float jag = 1f + 0.16f * Noise(c * 2.2f + d * 3f + 51f, s * 2.2f + d * 3f + 51f) * Mathf.Clamp01(d * 4f)
                        + 0.07f * Noise(c * 7f + d * 9f + 71f, s * 7f + d * 9f + 71f);
                    float r = ring == SideRings ? 0f : edge * bulge * narrow * jag;
                    float rimY = RockMeadow(Mathf.Cos(a) * edge, Mathf.Sin(a) * edge) - 1.6f;
                    float y = rimY - 2f - Mathf.Pow(d, 1.25f) * RockDepth + 6f * Noise(c * 3f + d * 5f + 91f, s * 3f + d * 5f + 91f) * d;
                    verts.Add(new Vector3(c * r, y, s * r));
                    // the rock's picture: around by distance, down by height
                    uvs.Add(new Vector2(a * edge / 9f, y / 9f));
                    colors.Add(new Color32(255, 255, 255, 255));
                }
            }
            for (int ring = 0; ring < TopRings; ring++)
            {
                int inner = ring * perRing, outer = (ring + 1) * perRing;
                for (int seg = 0; seg < RockSegments; seg++)
                {
                    top.Add(inner + seg); top.Add(inner + seg + 1); top.Add(outer + seg);
                    top.Add(inner + seg + 1); top.Add(outer + seg + 1); top.Add(outer + seg);
                }
            }
            // the rim ring of the meadow joins the first ring of the underside
            for (int ring = 0; ring < SideRings; ring++)
            {
                int upper = ring == 0 ? TopRings * perRing : sideStart + (ring - 1) * perRing, lower = sideStart + ring * perRing;
                for (int seg = 0; seg < RockSegments; seg++)
                {
                    under.Add(upper + seg); under.Add(upper + seg + 1); under.Add(lower + seg);
                    under.Add(upper + seg + 1); under.Add(lower + seg + 1); under.Add(lower + seg);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = SkyGroundName;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(under, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            GameObject ground = new GameObject(SkyGroundName);
            ground.layer = LayerMask.NameToLayer("static_solid");
            ground.transform.SetParent(sky, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = ground.AddComponent<MeshRenderer>();
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            renderer.sharedMaterials = new[] { MeadowMaterial(), UndersideMaterial() };
            Plugin.Debug("Valhalla: the rock has " + verts.Count + " points; materials " + renderer.sharedMaterials[0].name + ", " + renderer.sharedMaterials[1].name);
        }

        // The game's terrain material, as the small islets' ground has it (BuildGround).
        static Material MeadowMaterial()
        {
            Heightmap terrain = ZoneSystem.instance != null && ZoneSystem.instance.m_zonePrefab != null
                ? ZoneSystem.instance.m_zonePrefab.GetComponentInChildren<Heightmap>(true) : null;
            if (terrain == null || terrain.m_material == null) return UndersideMaterial();
            Material material = new Material(terrain.m_material);
            material.name = "IoV_ValhallaMeadow";
            Texture2D mask = new Texture2D(2, 2);
            mask.wrapMode = TextureWrapMode.Clamp;
            Color nothing = new Color(0f, 0f, 0f, 1f);
            mask.SetPixels(new Color[] { nothing, nothing, nothing, nothing });
            mask.Apply();
            material.SetTexture("_ClearedMaskTex", mask);
            material.SetFloat("_LodHideDistance", 10000f);
            return material;
        }

        // A big rock's own material, for the underside.
        static Material UndersideMaterial()
        {
            foreach (string name in new[] { "rock1_mountain", "rock4_coast", "Rock_7", "rock3_mountain", "Rock_3" })
            {
                GameObject prefab = PrefabManager.Instance.GetPrefab(name);
                MeshRenderer source = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>(true) : null;
                if (source != null && source.sharedMaterial != null) return source.sharedMaterial;
            }
            return new Material(Shader.Find("Standard"));
        }
    }

    // The sea of clouds under the rock (three layers, the lowest thick enough to hide the world far below)
    // and the little lights drifting over it. Built once into the location; drawn see-through with the
    // shader the game draws see-through (Sprites/Default).
    static class ValhallaClouds
    {
        internal static readonly string[] Layers = { "IoV_CloudDeck", "IoV_CloudMid", "IoV_CloudWisps" };

        internal static void Build(Transform sky)
        {
            Shader shader = Find("Sprites/Default");
            if (shader == null) { Plugin.Log.LogWarning("Valhalla: no see-through shader for the clouds"); return; }
            Texture2D puffs = CloudTexture(256, 4f, 0.42f, 7.3f);
            Texture2D wisps = CloudTexture(256, 6f, 0.55f, 19.1f);
            // depth below the floor, how thick, tint, metres per tile of the picture
            Layer(sky, Layers[0], -24f, 0.97f, new Color(0.78f, 0.64f, 0.74f), 300f, puffs, shader, 2980, 0.62f, true);
            Layer(sky, Layers[1], -17f, 0.8f, new Color(1f, 0.85f, 0.78f), 190f, puffs, shader, 2981, 0.0f, false);
            Layer(sky, Layers[2], -9f, 0.38f, new Color(1f, 0.93f, 0.86f), 110f, wisps, shader, 2982, 0.0f, false);
        }

        // floor: the least the layer covers anywhere (the deck is never quite clear, so no hole shows the world).
        // depth: the deck also gives the game's fog its distance (the game's invisible shader, drawn just before
        // it); without, the fog takes the world 2 km below as what's seen there and washes the clouds out white.
        static void Layer(Transform sky, string name, float y, float alpha, Color tint, float tile, Texture2D texture, Shader shader, int queue, float floor, bool depth)
        {
            const float Radius = 2600f, Solid = 900f;
            const int Rings = 40, Segments = 72;
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();
            verts.Add(Vector3.zero); uvs.Add(Vector2.zero); colors.Add(new Color(tint.r, tint.g, tint.b, alpha));
            for (int ring = 1; ring <= Rings; ring++)
            {
                float f = (float)ring / Rings;
                float r = Radius * f * f; // close rings near the middle, where it's seen up close
                float fade = r <= Solid ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (r - Solid) / (Radius - Solid));
                for (int seg = 0; seg < Segments; seg++)
                {
                    float a = seg * Mathf.PI * 2f / Segments;
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    verts.Add(new Vector3(x, 0f, z));
                    uvs.Add(new Vector2(x / tile, z / tile));
                    colors.Add(new Color(tint.r, tint.g, tint.b, alpha * fade));
                }
            }
            for (int seg = 0; seg < Segments; seg++)
                { tris.Add(0); tris.Add(1 + (seg + 1) % Segments); tris.Add(1 + seg); }
            for (int ring = 1; ring < Rings; ring++)
            {
                int inner = 1 + (ring - 1) * Segments, outer = 1 + ring * Segments;
                for (int seg = 0; seg < Segments; seg++)
                {
                    int next = (seg + 1) % Segments;
                    tris.Add(inner + seg); tris.Add(inner + next); tris.Add(outer + seg);
                    tris.Add(inner + next); tris.Add(outer + next); tris.Add(outer + seg);
                }
            }
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            GameObject go = new GameObject(name);
            go.transform.SetParent(sky, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            Material m = new Material(shader);
            m.name = name;
            m.mainTexture = floor > 0f ? Floored(texture, floor) : texture;
            m.renderQueue = queue;
            renderer.sharedMaterial = m;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Shader invisible = depth ? Find("Unlit/Invis") : null;
            if (invisible == null) return;
            // Both faces, whichever way that shader culls.
            List<int> both = new List<int>(tris);
            for (int i = 0; i < tris.Count; i += 3) { both.Add(tris[i]); both.Add(tris[i + 2]); both.Add(tris[i + 1]); }
            Mesh twinMesh = new Mesh();
            twinMesh.name = name + "_Depth";
            twinMesh.SetVertices(verts);
            twinMesh.SetTriangles(both, 0);
            twinMesh.RecalculateBounds();
            GameObject twin = new GameObject(name + "_Depth");
            twin.transform.SetParent(go.transform, false);
            twin.AddComponent<MeshFilter>().sharedMesh = twinMesh;
            MeshRenderer twinRenderer = twin.AddComponent<MeshRenderer>();
            Material dm = new Material(invisible);
            dm.renderQueue = queue - 1;
            twinRenderer.sharedMaterial = dm;
            twinRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            twinRenderer.receiveShadows = false;
        }

        static Texture2D Floored(Texture2D source, float floor)
        {
            Color[] px = source.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i].a = Mathf.Lerp(floor, 1f, px[i].a);
            Texture2D t = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Repeat;
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        // Soft cloud puffs that tile without a seam: noise blended with itself shifted by a tile, both ways.
        static Texture2D CloudTexture(int size, float scale, float cover, float seed)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.name = "IoV_Clouds";
            t.wrapMode = TextureWrapMode.Repeat;
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float n = Seamless(u, v, scale, seed);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((n - (1f - cover)) / 0.35f));
                    float shade = 0.82f + 0.18f * n; // brighter where thicker
                    px[y * size + x] = new Color(shade, shade, shade, a);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static float Seamless(float u, float v, float scale, float seed)
        {
            float a = Fbm(u, v, scale, seed), b = Fbm(u - 1f, v, scale, seed);
            float c = Fbm(u, v - 1f, scale, seed), d = Fbm(u - 1f, v - 1f, scale, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static float Fbm(float u, float v, float scale, float seed)
        {
            float sum = 0f, amp = 0.55f, f = scale;
            for (int i = 0; i < 4; i++)
            {
                sum += amp * Mathf.PerlinNoise(u * f + seed + i * 13.7f, v * f + seed * 0.7f + i * 7.1f);
                amp *= 0.5f;
                f *= 2f;
            }
            return Mathf.Clamp01(sum / 1.03f);
        }

        // Golden motes drifting up slowly over the meadow.
        internal static void Motes(Transform sky)
        {
            Shader shader = Find("Legacy Shaders/Particles/Additive");
            bool additive = shader != null;
            if (shader == null) shader = Find("Sprites/Default");
            if (shader == null) return;
            GameObject go = new GameObject("IoV_ValhallaMotes");
            go.transform.SetParent(sky, false);
            go.transform.localPosition = new Vector3(0f, 2f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.5f, 0.9f), new Color(1f, 0.95f, 0.75f, 0.7f));
            main.maxParticles = 260;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = true;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 18f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(150f, 6f, 150f);
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            Material m = new Material(shader);
            m.mainTexture = Dot();
            if (additive) m.SetColor("_TintColor", new Color(0.5f, 0.45f, 0.3f, 0.5f));
            renderer.sharedMaterial = m;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Texture2D Dot()
        {
            const int size = 32;
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        // Shader.Find misses some of what the game has loaded; this doesn't.
        internal static Shader Find(string name)
        {
            foreach (Shader s in Resources.FindObjectsOfTypeAll<Shader>())
                if (s != null && s.name == name && s.isSupported) return s;
            return null;
        }
    }

    // Valhalla's own weather: an eternal dusk, golden light, a soft violet haze, a calm wind, no rain.
    [HarmonyPatch(typeof(EnvMan), "Awake")]
    static class ValhallaWeather
    {
        internal const string Name = "IoV_Valhalla";
        internal const float Dusk = 0.70f; // where the sun holds there (0.5 is noon, 0.75 sunset)

        static void Postfix(EnvMan __instance)
        {
            EnvSetup clear = null;
            foreach (EnvSetup e in __instance.m_environments) if (e != null && e.m_name == "Clear") { clear = e; break; }
            if (clear == null) { Plugin.Log.LogWarning("Valhalla: the game's Clear weather wasn't found"); return; }
            EnvSetup env = clear.Clone();
            env.m_name = Name;
            env.m_default = false;
            env.m_isWet = env.m_isFreezing = env.m_isFreezingAtNight = env.m_isCold = env.m_isColdAtNight = env.m_alwaysDark = false;
            Color ambient = new Color(0.55f, 0.48f, 0.56f);
            env.m_ambColorDay = env.m_ambColorNight = ambient;
            Color haze = new Color(0.8f, 0.64f, 0.68f);
            env.m_fogColorMorning = env.m_fogColorDay = env.m_fogColorEvening = env.m_fogColorNight = haze;
            Color sunHaze = new Color(1f, 0.8f, 0.56f);
            env.m_fogColorSunMorning = env.m_fogColorSunDay = env.m_fogColorSunEvening = env.m_fogColorSunNight = sunHaze;
            env.m_fogDensityMorning = env.m_fogDensityDay = env.m_fogDensityEvening = env.m_fogDensityNight = 0.0025f;
            Color sun = new Color(1f, 0.82f, 0.6f);
            env.m_sunColorMorning = env.m_sunColorDay = env.m_sunColorEvening = env.m_sunColorNight = sun;
            env.m_lightIntensityDay = env.m_lightIntensityNight = 1.15f;
            env.m_windMin = 0.05f;
            env.m_windMax = 0.22f;
            env.m_cloudOpacityMorning = env.m_cloudOpacityDay = env.m_cloudOpacityEvening = env.m_cloudOpacityNight = 0.45f;
            env.m_auroraIntensityMorning = env.m_auroraIntensityDay = env.m_auroraIntensityEvening = env.m_auroraIntensityNight = 0f;
            env.m_psystems = new GameObject[0];
            env.m_envObject = null;
            env.m_rainCloudAlpha = 0f;
            env.m_musicMorning = env.m_musicDay = env.m_musicEvening = env.m_musicNight = "morning";
            __instance.AppendEnvironment(env);
            Plugin.Debug("Valhalla: weather " + Name + " added (from Clear)");
        }
    }

    // On each client, while its own player is at Valhalla: the weather and the held dusk, the meadow's birds
    // instead of the sea's sounds, the clouds drifting, and a catch for whoever steps off the edge.
    class ValhallaSky : MonoBehaviour
    {
        const float Reach = 600f;      // across, from the rock's middle
        const float Below = 300f;      // how far under the floor still counts as there
        const float FallLimit = 45f;   // under the floor: caught and set back
        bool inside, heldTime, previousDebugTime;
        float previousTime;
        float nextCheck;
        readonly List<Transform> clouds = new List<Transform>();
        static string meadowAmbients;

        void Start()
        {
            foreach (string name in ValhallaClouds.Layers)
            {
                Transform t = transform.Find(name);
                if (t != null) clouds.Add(t);
            }
            if (!measured)
            {
                measured = true;
                foreach (string line in MeasureTree(transform.position, 400f)) Plugin.Log.LogInfo("Valhalla: " + line);
            }
        }

        static bool measured;

        // The World-tree in the game's sky is real, and huge: where its branches are, by its own parts (the
        // shader they're drawn with), and how low and high they come within reach metres of a spot, to keep
        // the rock clear of them (while it's being made: from the test's log, and iov_tree).
        internal static List<string> MeasureTree(Vector3 at, float reach)
        {
            List<string> lines = new List<string>();
            int parts = 0;
            float low = float.MaxValue, high = float.MinValue;
            int points = 0;
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
            {
                bool tree = r.name.IndexOf("ygg", System.StringComparison.OrdinalIgnoreCase) >= 0;
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.shader != null && m.shader.name.IndexOf("Yggdrasil", System.StringComparison.OrdinalIgnoreCase) >= 0) tree = true;
                if (!tree) continue;
                parts++;
                Bounds b = r.bounds;
                string path = r.name;
                for (Transform p = r.transform.parent; p != null && path.Length < 120; p = p.parent) path = p.name + "/" + path;
                MeshFilter filter = r.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : r is SkinnedMeshRenderer ? ((SkinnedMeshRenderer)r).sharedMesh : null;
                bool readable = mesh != null && mesh.isReadable;
                lines.Add("tree part " + path + ": from " + b.min.ToString("F0") + " to " + b.max.ToString("F0") + (mesh != null ? ", " + mesh.vertexCount + " points" + (readable ? "" : " (not readable)") : ""));
                if (!readable) continue;
                Matrix4x4 world = r.transform.localToWorldMatrix;
                foreach (Vector3 v in mesh.vertices)
                {
                    Vector3 w = world.MultiplyPoint3x4(v);
                    if (Utils.DistanceXZ(w, at) > reach) continue;
                    points++;
                    low = Mathf.Min(low, w.y);
                    high = Mathf.Max(high, w.y);
                }
            }
            lines.Add(parts == 0 ? "no part of the World-tree found" :
                points == 0 ? "no point of the World-tree within " + reach + " m of " + at.ToString("F0") + " (that its parts let us read)" :
                "the World-tree within " + reach + " m of " + at.ToString("F0") + ": " + points + " points, lowest " + low.ToString("F0") + " m, highest " + high.ToString("F0") + " m (world height; the sea is at 30)");
            return lines;
        }

        void Update()
        {
            // The layers drift apart, slowly, each its own way.
            for (int i = 0; i < clouds.Count; i++)
                clouds[i].Rotate(0f, (i % 2 == 0 ? 0.08f : -0.12f) * (1f + i * 0.4f) * Time.deltaTime, 0f, Space.Self);

            Player player = Player.m_localPlayer;
            if (player == null) return;
            Vector3 p = player.transform.position, floor = transform.position;
            bool here = Utils.DistanceXZ(p, floor) < Reach && p.y > floor.y - Below;
            if (here != inside) { inside = here; Apply(here); }
            if (!inside) return;
            if (p.y < floor.y - FallLimit && !player.IsTeleporting()) Catch(player);
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 1f;
                Apply(true); // again now and then: other things may set the weather meanwhile
            }
        }

        void Apply(bool on)
        {
            EnvMan env = EnvMan.instance;
            if (env == null) return;
            if (on)
            {
                Ambients();
                env.SetForceEnvironment(ValhallaWeather.Name);
                if (!heldTime)
                {
                    heldTime = true;
                    previousDebugTime = env.m_debugTimeOfDay;
                    previousTime = env.m_debugTime;
                }
                env.m_debugTimeOfDay = true;
                env.m_debugTime = ValhallaWeather.Dusk;
            }
            else
            {
                env.SetForceEnvironment("");
                if (heldTime)
                {
                    env.m_debugTimeOfDay = previousDebugTime;
                    env.m_debugTime = previousTime;
                    heldTime = false;
                }
            }
            Plugin.Debug(on ? "Valhalla: here (its weather and dusk)" : "Valhalla: left");
        }

        // The meadow's random sounds (birds by day) instead of whatever the sea far below would play.
        static void Ambients()
        {
            if (meadowAmbients != null || AudioMan.instance == null || EnvMan.instance == null) return;
            meadowAmbients = "";
            foreach (AudioMan.BiomeAmbients a in AudioMan.instance.m_randomAmbients)
                if (a != null && a.m_biome == Heightmap.Biome.Meadows) { meadowAmbients = a.m_name; break; }
            foreach (EnvSetup e in EnvMan.instance.m_environments)
                if (e != null && e.m_name == ValhallaWeather.Name) e.m_ambientList = meadowAmbients;
            Plugin.Debug("Valhalla: random sounds from " + (meadowAmbients == "" ? "nothing" : meadowAmbients));
        }

        // Stepped off the edge: back onto the rock, near where Odin's light sets you down, unhurt.
        void Catch(Player player)
        {
            Vector3 back = transform.position + new Vector3(0f, 3f, 0f);
            player.TeleportTo(back, player.transform.rotation, false);
            Traverse.Create(player).Field("m_maxAirAltitude").SetValue(back.y);
            Plugin.Debug("Valhalla: caught a fall, set back on the rock");
        }

        void OnDestroy()
        {
            if (inside) Apply(false);
        }
    }
}
