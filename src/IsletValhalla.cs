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
            UnderRocks();
            keepSounds = true;
            try { BuildValhallaLayout(); }
            finally { keepSounds = false; }
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
        // The rock's noise, on its own fixed seed: the shared one (seed) is set by whatever islet was built last,
        // and the rock's shape is asked for again while the game runs (its grass, its ground).
        static float VNoise(float x, float z) { return Mathf.PerlinNoise(x + 911f, z + 911f * 0.7f) * 2f - 1f; }

        internal static float RockEdgeAt(float x, float z) { return RockEdge(Mathf.Atan2(z, x)); }

        static float RockEdge(float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return ValhallaEdge * (1f + 0.11f * VNoise(c * 1.4f + 3f, s * 1.4f + 3f) + 0.05f * VNoise(c * 3.5f + 9f, s * 3.5f + 9f));
        }

        // Its meadow: rolling gently, a metre or two.
        internal static float RockMeadow(float x, float z)
        {
            float rolling = 1.1f * VNoise(x * 0.018f + 5f, z * 0.018f + 5f) + 0.45f * VNoise(x * 0.05f + 15f, z * 0.05f + 15f)
                + 0.08f * VNoise(x * 0.4f + 25f, z * 0.4f + 25f);
            return rolling * (1f - ValhallaPads(x, z)); // level where the hall, the way and the ring stand
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
            Color32 golden = Heightmap.GetBiomeColor(Heightmap.Biome.Plains);
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
                    colors.Add(MeadowColour(x, z, grass, golden));
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
                    // tucked in under the lip at once, so the rim's rocks (not this) are what's seen from the side
                    float bulge = 0.92f;
                    float narrow = Mathf.Pow(1f - d, 0.75f);
                    float jag = 1f + 0.16f * Noise(c * 2.2f + d * 3f + 51f, s * 2.2f + d * 3f + 51f) * Mathf.Clamp01(d * 4f)
                        + 0.07f * Noise(c * 7f + d * 9f + 71f, s * 7f + d * 9f + 71f);
                    float r = ring == SideRings ? 0f : edge * bulge * narrow * jag;
                    float rimY = RockMeadow(Mathf.Cos(a) * edge, Mathf.Sin(a) * edge) - 1.6f;
                    float y = rimY - 2f - Mathf.Pow(d, 1.25f) * RockDepth + 6f * Noise(c * 3f + d * 5f + 91f, s * 3f + d * 5f + 91f) * d;
                    verts.Add(new Vector3(c * r, y, s * r));
                    // the rock's picture: around by distance, down by height
                    uvs.Add(new Vector2(a * edge / 24f, y / 24f));
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

        // The underside is clad in the game's own big rocks, turned upside down and hung in rings under the rim,
        // bigger and lower towards the middle: a ragged mass, as if torn out of a mountain. (Our underside
        // mesh stays inside them, so no gap shows.)
        static readonly string[] UnderRockNames = { "rock1_mountain", "rock3_mountain", "Rock_7", "rock4_coast" };

        static void UnderRocks()
        {
            int placed = 0;
            // ring: how many, how far in (part of the edge), how big, how deep their tops hang
            float[][] rings =
            {
                new[] { 46f, 0.95f, 10.5f, -1.1f }, // right under the lip, sticking out: the rock's face from the side, no bare band
                new[] { 22f, 0.75f, 22f, 12f },
                new[] { 12f, 0.5f, 32f, 30f },
                new[] { 5f, 0.2f, 42f, 58f },
            };
            for (int k = 0; k < rings.Length; k++)
            {
                int count = (int)rings[k][0];
                for (int i = 0; i < count; i++)
                {
                    float deg = (i + 0.5f * (k % 2)) * 360f / count + 9f * Noise(i * 1.3f + k * 7f, k * 2.1f + 3f);
                    float a = deg * Mathf.Deg2Rad;
                    float r = RockEdge(a) * rings[k][1] * (1f + 0.06f * Noise(i * 2.7f + 1f, k * 3.3f + 2f));
                    float size = rings[k][2] * (1f + 0.25f * Noise(i * 1.9f + k * 5f + 4f, 0.5f + k));
                    float rimY = RockMeadow(Mathf.Cos(a) * r, Mathf.Sin(a) * r) - 1.6f;
                    float topY = rimY - rings[k][3] - 2f;
                    string name = UnderRockNames[(i + k) % UnderRockNames.Length];
                    // upside down, each turned its own way, leaning a little outward
                    Vector3 euler = new Vector3(180f + 12f * Noise(i * 3.1f, k + 9f), deg * 2.3f + i * 47f, 8f * Noise(k + 4f, i * 2.3f));
                    GameObject go = Copy(new[] { name }, euler, size, true);
                    if (go == null) continue;
                    Bounds b = LocalBounds(go);
                    b.center -= new Vector3(0f, SkyHeight, 0f); // LocalBounds is the location's space; these hang under the sky floor
                    Move(go, b, new Vector3(Mathf.Cos(a) * r, topY, Mathf.Sin(a) * r), true);
                    placed++;
                }
            }
            Plugin.Debug("Valhalla: " + placed + " rocks under the rock");
        }

        // The game's terrain material, as the small islets' ground has it (BuildGround).
        static Material MeadowMaterial()
        {
            Heightmap terrain = ZoneSystem.instance != null && ZoneSystem.instance.m_zonePrefab != null
                ? ZoneSystem.instance.m_zonePrefab.GetComponentInChildren<Heightmap>(true) : null;
            if (terrain == null || terrain.m_material == null) return UndersideMaterial();
            Material material = new Material(terrain.m_material);
            material.name = "IoV_ValhallaMeadow";
            material.SetTexture("_ClearedMaskTex", ValhallaPaint(ValhallaEdge * 2.6f, 512));
            material.SetFloat("_LodHideDistance", 10000f);
            return material;
        }

        // The meadow isn't one green: drifts of the plains' golden grass run through it (the terrain shader paints
        // by these colours).
        static Color32 MeadowColour(float x, float z, Color32 grass, Color32 golden)
        {
            return Color32.Lerp(grass, golden, GoldenAt(x, z) * 0.85f);
        }

        // The meadow's paint (what the hoe and the pickaxe leave on the game's terrain): paved stone under the
        // way, the landing and round the hall and the high seat; trodden earth in the duel ring, round the
        // feast and along the memorial path. Red is earth, blue is paving; alpha stays 1 (plain ground).
        static Texture2D ValhallaPaint(float span, int size)
        {
            Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
            mask.name = "IoV_ValhallaPaint";
            mask.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    float x = ((i + 0.5f) / size - 0.5f) * span, z = ((j + 0.5f) / size - 0.5f) * span;
                    Vector2 paint = PaintAt(x, z);
                    px[j * size + i] = new Color(paint.x, 0f, paint.y, 1f);
                }
            mask.SetPixels(px);
            mask.Apply();
            return mask;
        }

        // How much the ground at (x, z) is trodden earth (x) and paved (y).
        internal static Vector2 PaintAt(float x, float z)
        {
            if (memorialPoints == null)
            {
                memorialPoints = new List<Vector2>();
                for (int i = 0; i <= 40; i++) memorialPoints.Add(MemorialAt(i / 40f));
            }
            float paved = Mathf.Max(Soft(Box(x, z, 0f, (ValhallaArrival.z + HallStart) * 0.5f, 2.6f, (HallStart - ValhallaArrival.z) * 0.5f + 1f)),
                Mathf.Max(Soft(Box(x, z, 0f, ValhallaArrival.z, 5.5f, 4.5f)), Soft(Box(x, z, 0f, (HallStart + HallEnd) * 0.5f, HallHalfWidth + 2f, (HallEnd - HallStart) * 0.5f + 2f))));
            paved = Mathf.Max(paved, Soft(new Vector2(x, z - LookoutZ).magnitude - 6.5f));
            float earth = Soft(new Vector2(x - DuelRing.x, z - DuelRing.y).magnitude - (DuelRadius - 0.5f)) * 0.9f;
            earth = Mathf.Max(earth, Soft(new Vector2(x - Feast.x, z - Feast.y).magnitude - 9f) * 0.7f);
            float near = float.MaxValue;
            foreach (Vector2 p in memorialPoints) near = Mathf.Min(near, (new Vector2(x, z) - p).sqrMagnitude);
            earth = Mathf.Max(earth, Soft(Mathf.Sqrt(near) - 1.4f) * 0.75f);
            return new Vector2(earth, paved);
        }

        static List<Vector2> memorialPoints;

        // The height of the rock's ground at (x, z), as its mesh has it: the meadow, and the lip rounding down
        // at the rim.
        internal static float SkyGround(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float t = r / Mathf.Max(1f, RockEdge(Mathf.Atan2(z, x)));
            float lip = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.9f) / 0.1f));
            return RockMeadow(x, z) - lip * 1.6f;
        }

        // True where golden plains grass drifts through the meadow (as MeadowColour paints it).
        internal static float GoldenAt(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * 0.025f + 71f, z * 0.025f + 13f) * 0.7f + Mathf.PerlinNoise(x * 0.08f + 5f, z * 0.08f + 91f) * 0.3f;
            return Mathf.SmoothStep(0f, 1f, (n - 0.45f) / 0.25f);
        }

        // 1 inside (distance under 0), fading to 0 a metre out.
        static float Soft(float distance) { return 1f - Mathf.SmoothStep(0f, 1f, distance); }

        // How far outside a box a point is (negative inside).
        static float Box(float x, float z, float cx, float cz, float hx, float hz)
        {
            float dx = Mathf.Abs(x - cx) - hx, dz = Mathf.Abs(z - cz) - hz;
            return Mathf.Max(dx, dz);
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
            Layer(sky, Layers[0], -72f, 0.97f, new Color(0.78f, 0.64f, 0.74f), 300f, puffs, shader, 2980, 0.62f, true);
            Layer(sky, Layers[1], -56f, 0.8f, new Color(1f, 0.85f, 0.78f), 190f, puffs, shader, 2981, 0.0f, false);
            Layer(sky, Layers[2], -38f, 0.38f, new Color(1f, 0.93f, 0.86f), 110f, wisps, shader, 2982, 0.0f, false);
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
                    float shade = 0.5f + 0.5f * Mathf.Pow(n, 1.4f); // bright tops, shadowed hollows
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

        internal static Texture2D Dot()
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

    // Valhalla's own weather: never rain or storm, a calm wind, and a slow day of its own. A long golden dusk,
    // a violet night under the northern lights, a rosy dawn, a short golden day, and round again (about 34
    // minutes in all), every change soft. Some days are clear, some hazy: a warm mist, the clouds thicker,
    // the northern lights fainter through it. Everyone there sees the same, by the world's own clock.
    [HarmonyPatch(typeof(EnvMan), "Awake")]
    static class ValhallaWeather
    {
        internal const string Name = "IoV_Valhalla";
        internal const string Hazy = "IoV_Valhalla_Haze";

        // The day: the sun's place (0.5 noon, 0.75 sunset, 1 midnight, 1.25 sunrise) at each turn, and how
        // long (seconds) the way to the next takes.
        static readonly float[] Turns = { 0.70f, 0.745f, 0.90f, 1.08f, 1.25f, 1.30f, 1.70f };
        static readonly float[] Takes = { 420f, 240f, 480f, 240f, 360f, 300f };
        internal static readonly string[] TurnNames = { "golden dusk", "sunset", "night, northern lights", "before dawn", "rosy dawn", "the day" };

        // Set by iov_sky (development): a fixed time of day and weather to look at; -1 / null: the day runs.
        internal static float heldTime = -1f;
        internal static string heldWeather;

        static void Postfix(EnvMan __instance)
        {
            EnvSetup clear = null, north = null;
            foreach (EnvSetup e in __instance.m_environments)
            {
                if (e == null) continue;
                if (e.m_name == "Clear") clear = e;
                if (e.m_name == "Twilight_Clear") north = e;
            }
            if (clear == null) { Plugin.Log.LogWarning("Valhalla: the game's Clear weather wasn't found"); return; }
            __instance.AppendEnvironment(Make(clear, north, Name, false));
            __instance.AppendEnvironment(Make(clear, north, Hazy, true));
            Plugin.Debug("Valhalla: weathers " + Name + " and " + Hazy + " added (from Clear" + (north != null ? "; the northern lights' colours from Twilight_Clear, whose lights are " +
                north.m_auroraIntensityNight.ToString("F2") + " at night, " + north.m_auroraIntensityEvening.ToString("F2") + " at dusk" : "; Twilight_Clear not found, the northern lights' colours are its own") + ")");
        }

        static EnvSetup Make(EnvSetup clear, EnvSetup north, string name, bool hazy)
        {
            EnvSetup env = clear.Clone();
            env.m_name = name;
            env.m_default = false;
            env.m_isWet = env.m_isFreezing = env.m_isFreezingAtNight = env.m_isCold = env.m_isColdAtNight = env.m_alwaysDark = false;
            env.m_snowBuildup = 0f;
            // the light on things, by day and by night
            env.m_ambColorDay = hazy ? new Color(0.6f, 0.53f, 0.55f) : new Color(0.55f, 0.48f, 0.56f);
            env.m_ambColorNight = hazy ? new Color(0.3f, 0.29f, 0.4f) : new Color(0.26f, 0.28f, 0.44f);
            // the sun (the moon at night)
            env.m_sunColorMorning = new Color(1f, 0.72f, 0.66f);  // rose
            env.m_sunColorDay = new Color(1f, 0.9f, 0.72f);       // warm gold
            env.m_sunColorEvening = new Color(1f, 0.82f, 0.6f);   // amber
            env.m_sunColorNight = new Color(0.55f, 0.62f, 0.9f);  // pale blue moon
            env.m_lightIntensityDay = hazy ? 0.95f : 1.15f;
            env.m_lightIntensityNight = hazy ? 0.5f : 0.6f;
            // the haze, and its glow toward the sun
            env.m_fogColorMorning = new Color(0.86f, 0.66f, 0.72f);
            env.m_fogColorDay = new Color(0.82f, 0.72f, 0.66f);
            env.m_fogColorEvening = new Color(0.8f, 0.64f, 0.68f);
            env.m_fogColorNight = new Color(0.17f, 0.18f, 0.32f);
            env.m_fogColorSunMorning = new Color(1f, 0.72f, 0.66f);
            env.m_fogColorSunDay = new Color(1f, 0.88f, 0.7f);
            env.m_fogColorSunEvening = new Color(1f, 0.8f, 0.56f);
            env.m_fogColorSunNight = new Color(0.32f, 0.38f, 0.6f);
            // thin, so the far clouds and the branch still show (thick fog washes the cloud deck white)
            env.m_fogDensityMorning = env.m_fogDensityEvening = hazy ? 0.0021f : 0.0012f;
            env.m_fogDensityDay = hazy ? 0.0018f : 0.0011f;
            env.m_fogDensityNight = hazy ? 0.0016f : 0.001f;
            env.m_windMin = 0.05f;
            env.m_windMax = hazy ? 0.15f : 0.22f;
            env.m_cloudOpacityMorning = env.m_cloudOpacityDay = env.m_cloudOpacityEvening = env.m_cloudOpacityNight = hazy ? 0.7f : 0.45f;
            // the northern lights: the game's own, in its sky, as in the far north
            if (north != null && north.m_auroraColors != null) env.m_auroraColors = north.m_auroraColors;
            else env.m_auroraColors = Lights();
            env.m_auroraIntensityNight = hazy ? 0.55f : 1f;
            env.m_auroraIntensityEvening = hazy ? 0.1f : 0.25f;
            env.m_auroraIntensityMorning = hazy ? 0.05f : 0.15f;
            env.m_auroraIntensityDay = 0f;
            env.m_psystems = new GameObject[0];
            env.m_envObject = null;
            env.m_rainCloudAlpha = 0f;
            // no music of its own: ValhallaSky.Music plays it, by the time of day and the place
            env.m_musicMorning = env.m_musicDay = env.m_musicEvening = env.m_musicNight = "";
            return env;
        }

        // Northern lights' colours of our own, if the far north's aren't there: green into teal into violet.
        static Gradient Lights()
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.2f, 1f, 0.55f), 0f), new GradientColorKey(new Color(0.2f, 0.85f, 0.9f), 0.5f), new GradientColorKey(new Color(0.65f, 0.35f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        internal static bool Mine(string name) { return name == Name || name == Hazy; }

        // Where Valhalla's day is now: the sun's place (0..1) and which weather, and (for iov_sky) which part.
        internal static void Now(out float time, out string weather, out string part)
        {
            double now = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.time;
            float total = 0f;
            foreach (float s in Takes) total += s;
            long round = (long)System.Math.Floor(now / total);
            float into = (float)(now - round * (double)total);
            float at = Turns[Turns.Length - 1];
            part = TurnNames[TurnNames.Length - 1];
            for (int i = 0; i < Takes.Length; i++)
            {
                if (into < Takes[i]) { at = Mathf.Lerp(Turns[i], Turns[i + 1], into / Takes[i]); part = TurnNames[i]; break; }
                into -= Takes[i];
            }
            // a day runs noon to noon, so the weather only changes in the bright middle of the day
            long day = round + (at >= 1.5f ? 1 : 0);
            uint h = (uint)(day * 2654435761L);
            bool haze = ((h >> 13) % 100) < 40;
            time = at - Mathf.Floor(at);
            weather = haze ? Hazy : Name;
            if (heldTime >= 0f) { time = heldTime; part = "held by iov_sky"; }
            if (heldWeather != null) weather = heldWeather;
        }
    }
    // On each client, while its own player is at Valhalla: the weather and the held dusk, the meadow's birds
    // instead of the sea's sounds, the World-tree's branch overhead, the clouds drifting, and a catch for
    // whoever steps off the edge.
    //
    // The World-tree in the game's sky is a real branch, and huge, but it goes along with the player (the
    // game's FollowPlayer), so from the ground it always hangs in the sky. Up here it would cut through the
    // rock, and walk along with you. So while you're here, the game's own is hidden and a still copy of it
    // takes its place, fixed to the rock and lifted until its nearest parts arch high overhead.
    class ValhallaSky : MonoBehaviour
    {
        const float Reach = 600f;      // across, from the rock's middle
        const float Below = 300f;      // how far under the floor still counts as there
        const float FallLimit = 75f;   // under the floor (in the cloud deck): caught and set back
        const float CanopyClearance = 200f; // the branch's lowest part in sight, above the rock's floor
        const float CanopyNear = 1000f;     // how far "in sight" reaches
        bool inside, heldTime, previousDebugTime;
        float previousTime, previousTransition, arrived;
        string lastPart, lastWeather;
        float nextCheck;
        readonly List<Transform> clouds = new List<Transform>();
        GameObject tree, canopy;
        ValhallaPeople people;
        static string meadowAmbients;

        void Start()
        {
            foreach (string name in ValhallaClouds.Layers)
            {
                Transform t = transform.Find(name);
                if (t != null) clouds.Add(t);
            }
            Grass();
            people = gameObject.AddComponent<ValhallaPeople>();
            if (Plugin.DebugLog.Value)
            {
                Light[] lights = GetComponentsInChildren<Light>(true);
                int lod = 0, shadowed = 0;
                foreach (Light l in lights) { if (l.GetComponent<LightLod>() != null) lod++; if (l.shadows != LightShadows.None) shadowed++; }
                Plugin.Debug("Valhalla: " + lights.Length + " lights, " + lod + " dimmed with distance by the game, " + shadowed + " casting shadows");
            }
        }

        // ---- grass ----

        // The game only grows its grass and wild flowers on its own terrain (ClutterSystem), never on a mesh
        // like the rock's. So the rock gets them the same way, with the game's own grass: for each kind the
        // meadows have (and, where golden drifts run, the plains'), patches of instanced blades and flowers as
        // thick as the game sets them, in the same patchy spread, off the paved and trodden ground and the
        // level places where things stand. Each client grows its own; nothing of it is saved.
        void Grass()
        {
            ClutterSystem system = ClutterSystem.instance;
            if (system == null || system.m_clutter == null) return;
            float patch = Mathf.Max(4f, system.m_grassPatchSize);
            int kinds = 0, blades = 0;
            foreach (ClutterSystem.Clutter c in system.m_clutter)
            {
                if (c == null || !c.m_enabled || !c.m_instanced || c.m_prefab == null || !c.m_onUncleared) continue;
                bool meadow = (c.m_biome & Heightmap.Biome.Meadows) != 0, plains = (c.m_biome & Heightmap.Biome.Plains) != 0;
                if (!meadow && !plains) continue;
                if (c.m_prefab.GetComponent<InstanceRenderer>() == null) continue;
                kinds++;
                int perPatch = Mathf.Max(1, Mathf.RoundToInt(c.m_amount * Mathf.Max(0.25f, system.m_amountScale)));
                Random.State saved = Random.state;
                Random.InitState(c.m_name.GetStableHashCode());
                for (float px = -96f; px < 96f; px += patch)
                    for (float pz = -96f; pz < 96f; pz += patch)
                    {
                        Vector3 centre = transform.TransformPoint(new Vector3(px + patch * 0.5f, 0f, pz + patch * 0.5f));
                        InstanceRenderer drawn = null;
                        for (int i = 0; i < perPatch; i++)
                        {
                            float x = Random.Range(px, px + patch), z = Random.Range(pz, pz + patch);
                            float yaw = Random.Range(0f, 360f), size = Random.Range(c.m_scaleMin, c.m_scaleMax);
                            if (!Grows(c, x, z, meadow, plains)) continue;
                            if (drawn == null)
                            {
                                GameObject go = Object.Instantiate(c.m_prefab, centre, Quaternion.identity, transform);
                                drawn = go.GetComponent<InstanceRenderer>();
                                if (drawn.m_lodMaxDistance > system.m_distance - patch * 0.5f) drawn.m_lodMaxDistance = system.m_distance - patch * 0.5f;
                            }
                            drawn.AddInstance(transform.TransformPoint(new Vector3(x, IsletBuilder.SkyGround(x, z), z)), Quaternion.Euler(0f, yaw, 0f), size);
                            blades++;
                        }
                    }
                Random.state = saved;
            }
            Plugin.Debug("Valhalla: " + blades + " blades and flowers of " + kinds + " of the game's grass kinds on the rock");
        }

        // Whether a kind of grass grows at (x, z) on the rock: on it and off its edge, not where it's paved,
        // trodden or levelled for something to stand, in its biome (golden drifts are the plains'), and in the
        // game's own patchy spread for it.
        static bool Grows(ClutterSystem.Clutter c, float x, float z, bool meadow, bool plains)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            if (r > IsletBuilder.RockEdgeAt(x, z) - 1.5f) return false;
            Vector2 paint = IsletBuilder.PaintAt(x, z);
            if (paint.x > 0.3f || paint.y > 0.2f || IsletBuilder.ValhallaPads(x, z) > 0.6f) return false;
            bool golden = IsletBuilder.GoldenAt(x, z) > 0.5f;
            if (golden ? !plains : !meadow) return false;
            if (c.m_fractalScale > 0f)
            {
                float f = Utils.Fbm(new Vector3(x, 0f, z) * 0.01f * c.m_fractalScale + new Vector3(c.m_fractalOffset, 0f, 0f), 3, 1.6f, 0.7f);
                if (f < c.m_fractalTresholdMin || f > c.m_fractalTresholdMax) return false;
            }
            return true;
        }

        // ---- the World-tree ----

        static GameObject FindTree()
        {
            foreach (MeshFilter f in Object.FindObjectsOfType<MeshFilter>())
                if (f != null && f.transform.parent != null && f.transform.parent.name == "YggdrasilBranch") return f.transform.parent.gameObject;
            return null;
        }

        // The game's own is only made unseen (its renderers off), so it keeps following the player and is
        // where it should be when it's shown again, and when it's copied.
        void Canopy(bool on)
        {
            if (on)
            {
                if (tree == null) tree = FindTree();
                if (tree == null) return;
                foreach (Renderer r in tree.GetComponentsInChildren<Renderer>(true))
                    if (r.enabled) { r.enabled = false; hiddenTree.Add(r); }
                if (canopy != null) canopy.SetActive(true);
            }
            else
            {
                foreach (Renderer r in hiddenTree) if (r != null) r.enabled = true;
                hiddenTree.Clear();
                if (canopy != null) canopy.SetActive(false);
            }
        }

        readonly List<Renderer> hiddenTree = new List<Renderer>();
        float onRockSince = -1f;

        // Copied once the player has stood on the rock a moment: right after a teleport the game's branch
        // hasn't caught up with them yet.
        void WatchForCanopy(Player player)
        {
            if (canopy != null || tree == null) return;
            Vector3 p = player.transform.position;
            bool onRock = !player.IsTeleporting() && Utils.DistanceXZ(p, transform.position) < 120f && p.y > transform.position.y - 20f;
            if (!onRock) { onRockSince = -1f; return; }
            if (onRockSince < 0f) onRockSince = Time.time;
            if (Time.time - onRockSince > 2f) MakeCanopy();
        }

        void MakeCanopy()
        {
            Camera cam = Utils.GetMainCamera();
            Vector3 eye = cam != null ? cam.transform.position : transform.position;
            Vector3 offset = tree.transform.position - eye; // where it hangs from where we see it from
            canopy = Object.Instantiate(tree);
            canopy.name = "IoV_WorldTreeCanopy";
            foreach (FollowPlayer follow in canopy.GetComponentsInChildren<FollowPlayer>(true)) Object.Destroy(follow);
            foreach (Renderer r in canopy.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // no hard shadow of it across the meadow
            }
            canopy.transform.localScale = tree.transform.lossyScale;
            canopy.transform.rotation = tree.transform.rotation;
            canopy.transform.position = new Vector3(transform.position.x + offset.x, tree.transform.position.y, transform.position.z + offset.z);
            // Lifted until nothing of it comes nearer the rock's floor than the clearance, anywhere in sight.
            float low = Lowest(canopy, transform.position, CanopyNear);
            if (!float.IsNaN(low)) canopy.transform.position += Vector3.up * (transform.position.y + CanopyClearance - low);
            canopy.transform.SetParent(transform, true);
            float overhead = Lowest(canopy, transform.position, 150f);
            Plugin.Debug("Valhalla: the World-tree's branch held over the rock" + (float.IsNaN(low) ? " (none of it near)" : ", lifted " +
                (transform.position.y + CanopyClearance - low).ToString("F0") + " m") +
                (float.IsNaN(overhead) ? "; none of it right overhead" : "; right overhead it's " + (overhead - transform.position.y).ToString("F0") + " m up"));
        }

        // The lowest point of a thing's meshes within reach metres of a spot (NaN: none there, or not readable).
        static float Lowest(GameObject thing, Vector3 at, float reach)
        {
            float low = float.NaN;
            foreach (MeshFilter f in thing.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || !f.sharedMesh.isReadable) continue;
                Matrix4x4 world = f.transform.localToWorldMatrix;
                foreach (Vector3 v in f.sharedMesh.vertices)
                {
                    Vector3 w = world.MultiplyPoint3x4(v);
                    if (Utils.DistanceXZ(w, at) > reach) continue;
                    if (float.IsNaN(low) || w.y < low) low = w.y;
                }
            }
            return low;
        }

        // Where the game's own branch is over a spot (iov_tree, while the place is being made).
        internal static List<string> MeasureTree(Vector3 at, float reach)
        {
            List<string> lines = new List<string>();
            GameObject t = FindTree();
            if (t == null) { lines.Add("the World-tree isn't in sight (hidden while at Valhalla)"); return lines; }
            float low = Lowest(t, at, reach);
            lines.Add("the World-tree's branch at " + t.transform.position.ToString("F0") + ", its lowest within " + reach + " m of " + at.ToString("F0") + ": " +
                (float.IsNaN(low) ? "none" : low.ToString("F0") + " m (world height; the sea is at 30)"));
            return lines;
        }

        // ---- being here ----

        void Update()
        {
            // The layers drift apart, slowly, each its own way.
            for (int i = 0; i < clouds.Count; i++)
                clouds[i].Rotate(0f, (i % 2 == 0 ? 0.08f : -0.12f) * (1f + i * 0.4f) * Time.deltaTime, 0f, Space.Self);

            Player player = Player.m_localPlayer;
            if (player == null) return;
            Vector3 p = player.transform.position, floor = transform.position;
            bool here = Utils.DistanceXZ(p, floor) < Reach && p.y > floor.y - Below;
            if (here != inside)
            {
                inside = here;
                Here = here;
                arrivedAt = Time.time;
                Apply(here);
                Canopy(here);
                if (people != null) people.Show(here);
                Plugin.Debug(here ? "Valhalla: here (its weather, dusk and branch)" : "Valhalla: left");
            }
            if (!inside) return;
            Clock();
            Music(transform.InverseTransformPoint(p));
            Found(player);
            WatchForCanopy(player);
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
                if (!heldTime)
                {
                    heldTime = true;
                    previousDebugTime = env.m_debugTimeOfDay;
                    previousTime = env.m_debugTime;
                    previousTransition = env.m_transitionDuration;
                    arrived = Time.time;
                }
                Clock();
            }
            else
            {
                MusicOff();
                env.SetForceEnvironment("");
                if (heldTime)
                {
                    env.m_debugTimeOfDay = previousDebugTime;
                    env.m_debugTime = previousTime;
                    env.m_transitionDuration = previousTransition;
                    heldTime = false;
                }
            }
        }

        // Valhalla's own day, every frame (the sun moves smoothly) and its weather. On arrival the sea's weather
        // gives way at the game's usual pace (no rain carried in); after that one Valhalla weather melts
        // into the other over a minute.
        void Clock()
        {
            EnvMan env = EnvMan.instance;
            if (env == null || !heldTime) return;
            float time;
            string weather, part;
            ValhallaWeather.Now(out time, out weather, out part);
            if (Time.time > arrived + 15f) env.m_transitionDuration = 60f;
            env.SetForceEnvironment(weather);
            env.m_debugTimeOfDay = true;
            env.m_debugTime = time;
            if (part != lastPart || weather != lastWeather)
            {
                Plugin.Debug("Valhalla: " + part + " (" + time.ToString("F3") + "), " + weather);
                lastPart = part;
                lastWeather = weather;
            }
        }

        // ---- a land of its own ----
        //
        // Valhalla is named as a land is: on the minimap's corner (it would say the sea below) and, the first
        // time this character comes, in the middle of the view as the game names a land found.
        internal const string FoundKey = "iovvalhallafound"; // the player's seen list
        internal static bool Here;   // the local player is in Valhalla
        float arrivedAt;

        void Found(Player player)
        {
            if (Time.time < arrivedAt + 3f || player.IsTeleporting() || player.HaveSeenTutorial(FoundKey)) return;
            if (MessageHud.instance == null) return;
            player.SetSeenTutorial(FoundKey);
            MessageHud.instance.ShowBiomeFoundMsg("$iov_valhalla", true);
        }

        [HarmonyPatch(typeof(Minimap), "UpdateBiome")]
        static class MinimapName
        {
            static bool named;
            static readonly System.Reflection.FieldInfo shown = AccessTools.Field(typeof(Minimap), "m_biome");

            static void Postfix(Minimap __instance)
            {
                if (Here)
                {
                    string name = Localization.instance != null ? Localization.instance.Localize("$iov_valhalla") : "Valhalla";
                    if (__instance.m_biomeNameSmall != null && __instance.m_biomeNameSmall.text != name) __instance.m_biomeNameSmall.text = name;
                    if (__instance.m_biomeNameLarge != null && __instance.m_biomeNameLarge.text != name) __instance.m_biomeNameLarge.text = name;
                    named = true;
                }
                else if (named)
                {
                    // forget which land it last named, so it names the one below again
                    named = false;
                    if (shown != null)
                        shown.SetValue(__instance, shown.FieldType.IsValueType ? System.Activator.CreateInstance(shown.FieldType) : null);
                }
            }
        }

        // ---- music ----
        //
        // All of it the game's own pieces, played through its music player (as a location's, like Haldor's),
        // so the player's music volume and settings hold. Each time of day and each place has a few pieces
        // that take turns, one after another; going from one to another, the old one fades out and the new
        // fades in slowly. At dawn and at dusk the game's own short call for it comes first.
        static readonly string[] DayMusic = { "meadows", "home", "location_haldor" };
        static readonly string[] DuskMusic = { "plains", "home" };
        static readonly string[] NightMusic = { "deepnorth", "mistlands" };
        static readonly string[] HallMusic = { "credits", "location_deepnorth_timberhallvillage" };
        static readonly string[] FeastMusic = { "location_deepnorth_memorialsite", "location_ashlands_ruins" };
        const float Fade = 6f; // seconds each way between pieces
        static readonly Vector2 FeastAt = new Vector2(-42f, 8f);

        string[] playlist;            // what's being taken turns at now
        string playing;               // the piece asked for
        float playingUntil, nextMusic;
        string lastTimeOfDay;
        readonly Dictionary<string[], int> turn = new Dictionary<string[], int>();
        float savedFade = -1f;
        readonly Dictionary<MusicMan.NamedMusic, float> savedFadeIn = new Dictionary<MusicMan.NamedMusic, float>();

        void Music(Vector3 local)
        {
            if (Time.time < nextMusic) return;
            nextMusic = Time.time + 1f;
            MusicMan music = MusicMan.instance;
            if (music == null || EnvMan.instance == null) return;
            SlowFades(music, true);

            // the place first (a few metres' grace on the way out, so its edge doesn't flicker)
            float grace = playlist == HallMusic || playlist == FeastMusic ? 3f : 0f;
            string[] want = null;
            if (Mathf.Abs(local.x) < 8.5f + grace && local.z > 3.5f - grace && local.z < 40.5f + grace && local.y < 12f) want = HallMusic;
            else if (new Vector2(local.x - FeastAt.x, local.z - FeastAt.y).magnitude < 18f + grace) want = FeastMusic;

            // else the time of day (the sun's place in Valhalla's day: see ValhallaWeather.Turns)
            float t = EnvMan.instance.m_debugTime;
            string timeOfDay = t >= 0.30f && t < 0.70f ? "day" : t >= 0.70f && t < 0.90f ? "dusk" : t >= 0.25f && t < 0.30f ? "dawn" : "night";
            string call = null;
            if (lastTimeOfDay != null && timeOfDay != lastTimeOfDay)
            {
                if (timeOfDay == "dawn") call = "morning";
                if (timeOfDay == "dusk") call = "evening";
            }
            lastTimeOfDay = timeOfDay;
            if (want == null) want = timeOfDay == "night" ? NightMusic : timeOfDay == "dusk" ? DuskMusic : DayMusic;

            if (call != null && want != HallMusic && want != FeastMusic) Play(call, null);           // dawn's or dusk's call, once
            else if (want != playlist && !(playlist == null && playing != null && Time.time < playingUntil)) Play(Next(want), want);
            else if (Time.time >= playingUntil) Play(Next(want), want);                                     // this one's done: the next
            music.LocationMusic(playing);
        }

        string Next(string[] list)
        {
            int i;
            turn.TryGetValue(list, out i);
            turn[list] = i + 1;
            return list[i % list.Length];
        }

        // Starts a piece; the next is asked for a fade before it ends.
        void Play(string name, string[] list)
        {
            if (name == playing && list == playlist) return;
            playing = name;
            playlist = list;
            float length = 120f;
            MusicMan.NamedMusic piece = Piece(name);
            if (piece != null && piece.m_clips != null)
            {
                float shortest = float.MaxValue;
                foreach (AudioClip c in piece.m_clips) if (c != null) shortest = Mathf.Min(shortest, c.length);
                if (shortest < float.MaxValue) length = shortest;
            }
            playingUntil = Time.time + Mathf.Max(20f, length - Fade);
            Plugin.Debug("Valhalla: music " + name + " (" + length.ToString("F0") + " s)");
        }

        static MusicMan.NamedMusic Piece(string name)
        {
            if (MusicMan.instance == null) return null;
            foreach (MusicMan.NamedMusic m in MusicMan.instance.m_music) if (m != null && m.m_name == name) return m;
            return null;
        }

        // Slow fades while here (the game's are quick), put back as they were on leaving.
        void SlowFades(MusicMan music, bool on)
        {
            Traverse fade = Traverse.Create(music).Field("m_musicFadeTime");
            if (on && savedFade < 0f)
            {
                savedFade = fade.GetValue<float>();
                fade.SetValue(Mathf.Max(savedFade, Fade));
                foreach (string[] list in new[] { DayMusic, DuskMusic, NightMusic, HallMusic, FeastMusic, new[] { "morning", "evening" } })
                    foreach (string name in list)
                    {
                        MusicMan.NamedMusic piece = Piece(name);
                        if (piece == null || savedFadeIn.ContainsKey(piece)) continue;
                        savedFadeIn[piece] = piece.m_fadeInTime;
                        piece.m_fadeInTime = Mathf.Max(piece.m_fadeInTime, Fade);
                    }
            }
            else if (!on && savedFade >= 0f)
            {
                fade.SetValue(savedFade);
                foreach (KeyValuePair<MusicMan.NamedMusic, float> kv in savedFadeIn) if (kv.Key != null) kv.Key.m_fadeInTime = kv.Value;
                savedFadeIn.Clear();
                savedFade = -1f;
            }
        }

        void MusicOff()
        {
            MusicMan music = MusicMan.instance;
            if (music != null)
            {
                if (playing != null) music.LocationMusic(null);
                SlowFades(music, false);
            }
            playing = null;
            playlist = null;
            lastTimeOfDay = null;
        }

        // The meadow's random sounds (birds by day) instead of whatever the sea far below would play.
        static void Ambients()
        {
            if (meadowAmbients != null || AudioMan.instance == null || EnvMan.instance == null) return;
            meadowAmbients = "";
            List<string> names = new List<string>();
            foreach (AudioMan.BiomeAmbients a in AudioMan.instance.m_randomAmbients)
            {
                if (a == null) continue;
                names.Add(a.m_name + " (" + a.m_biome + ")");
                bool meadow = (a.m_biome & Heightmap.Biome.Meadows) != 0 || a.m_name.IndexOf("meadow", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (meadow && meadowAmbients == "") meadowAmbients = a.m_name;
            }
            foreach (EnvSetup e in EnvMan.instance.m_environments)
                if (e != null && ValhallaWeather.Mine(e.m_name)) e.m_ambientList = meadowAmbients;
            Plugin.Debug("Valhalla: random sounds from " + (meadowAmbients == "" ? "nothing" : meadowAmbients) + "; the game has " + string.Join(", ", names.ToArray()));
        }

        // Stepped off the edge: back onto the rock, near where Odin's light sets you down, unhurt.
        void Catch(Player player)
        {
            Vector3 back = transform.TransformPoint(IsletBuilder.ValhallaArrival + new Vector3(0f, 3f, 0f));
            player.TeleportTo(back, player.transform.rotation, false);
            Traverse.Create(player).Field("m_maxAirAltitude").SetValue(back.y);
            Plugin.Debug("Valhalla: caught a fall, set back on the rock");
        }

        void OnDestroy()
        {
            if (!inside) return;
            Here = false;
            // (when the game itself is shutting down, logging out, its weather may already be half gone)
            try { Apply(false); Canopy(false); }
            catch (System.Exception e) { Plugin.Debug("Valhalla: weather not put back on the way out (" + e.GetType().Name + ")"); }
        }
    }
}
