using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Builds an islet location for one tier at runtime from vanilla assets, so the mod needs no asset bundle.
    //
    // The ground is a generated mesh painted with Valheim's terrain material. The location snaps to the
    // water surface (y = 0 here is sea level), so it works in deep water; the mesh's skirt reaches 30 m
    // down, and since players can't dive, nobody sees that it doesn't touch the sea floor. Rocks and the
    // wreck are static copies of vanilla prefabs with their scripts removed. Ruin pieces, plants,
    // pickables, spawners and the chest are networked objects that behave like their vanilla selves.
    static partial class IsletBuilder
    {
        internal const float SmallPlateau = 3f;
        // The plateau's height above the sea for the islet being built: SmallPlateau for most, more for a
        // large islet (set while it's built, then back).
        internal static float PlateauHeight = SmallPlateau;

        // Per variant (see Register): the variants of a tier differ in size, outline and features.
        static float plateauRadius, shoreRadius, maxRadius, seed;
        static float stretchX, stretchZ;  // an oval islet
        static float cliffDeg;            // the high, steep side; the jetty, wreck and ridge structure are placed relative to it
        static float coveDeg = float.NaN; // a bay cut into the shore
        static float hillDeg = float.NaN; // a second hump on the slope
        const float Bottom = -30f;
        const int SmallSegments = 72;
        static int segments = SmallSegments; // around each ring of the ground mesh (more for a large islet)

        const float RingStep = 1f;
        // Floor tiles stick out of the ground a little: a piece only gets support where it touches the
        // ground mesh's surface, and one entirely below it touches nothing.
        static float FloorTop { get { return PlateauHeight + 0.25f; } }

        // Where the Rock ground style takes its material from.
        static readonly string[] bigRocks = { "rock4_coast", "rock1_mountain", "Rock_7", "rock3_mountain", "Rock_3" };

        internal const int Variants = 4;
        internal const string Group = "IoV_Islets";

        static void SetVariant(int variant)
        {
            seed = tier.Seed + variant * 53f;
            float size = 1f;
            stretchX = stretchZ = 1f;
            coveDeg = hillDeg = float.NaN;
            switch (variant)
            {
                case 0: cliffDeg = 206f; break;
                case 1: size = 1.1f; stretchX = 1.3f; stretchZ = 0.82f; cliffDeg = 20f; break;  // long and narrow
                case 2: size = 1.05f; cliffDeg = 300f; coveDeg = cliffDeg + 140f; break;        // with a bay
                default: size = 0.95f; stretchX = 0.85f; stretchZ = 1.2f; cliffDeg = 110f; hillDeg = cliffDeg + 170f; break; // two humps
            }
            plateauRadius = 9.5f * size;
            shoreRadius = 20f * size;
            maxRadius = shoreRadius * Mathf.Max(stretchX, stretchZ) + 26f;
        }

        static IsletTier tier;
        static string locationName;
        static GameObject root;
        static Transform parent; // where new parts go: the location, or an optional group in it
        static int built;
        static bool quietProps; // decoration that should lie still: its particle effects off too (see KetillsIslet)
        static int missing;

        // Variants 0-3 are the small islets up to 1.9, with our own ruins: kept known to the game (quantity 0),
        // so those already built in a world come back as they are. Every new one is 4-7, the same four shapes
        // with the game's own structures on them (PlaceTemplates).
        internal static void Register(IsletTier islet, int variant)
        {
            RegisterSmall(islet, variant, islet.LocationName(variant), 0);
        }

        // Work in progress (not for release): a small islet with only design n of its land, unturned, to look
        // at each design (iov_goto small_meadows_2).
        internal static void RegisterSmallTests()
        {
            foreach (IsletTier islet in IsletTier.All)
                for (int n = 1; ReadDraft(SmallDraftId(islet, n)) != null; n++)
                    RegisterSmall(islet, Variants + (n - 1) % Variants, SmallTestName(islet, n), n);
        }

        internal static string SmallTestName(IsletTier islet, int design) { return "IoVTest_Small_" + islet.Name + "_" + design; }
        static string SmallDraftId(IsletTier islet, int design) { return "Small_" + islet.Name + "_" + design; }

        // The new kind of islet for an old one: a small islet's variant n becomes n + 4, the old hidden islet
        // the final arena; null for anything else.
        internal static string TemplatedName(string prefabName)
        {
            if (prefabName == Story.HiddenIslet) return Story.Arena;
            foreach (IsletTier islet in IsletTier.All)
                for (int v = 0; v < Variants; v++)
                    if (prefabName == islet.LocationName(v)) return islet.LocationName(v + Variants);
            return null;
        }

        // onlyDesign: a test islet with that one design (quantity 0, unturned); 0 for the real ones.
        static void RegisterSmall(IsletTier islet, int variant, string name, int onlyDesign)
        {
            tier = islet;
            locationName = name;
            bool templated = variant >= Variants;
            SetVariant(variant % Variants);
            root = ZoneManager.Instance.CreateLocationContainer(locationName);
            parent = root.transform;
            built = 0;
            missing = 0;

            Material rockMaterial = BuildGround();
            PlaceRocks();
            if (templated) PlaceTemplates(onlyDesign);
            else
            {
                PlaceRuins();
                if (tier.DvergrOutpost) PlaceDvergrWard();
            }
            if (tier.Raiders != null) PlaceRaiders();
            PlaceHints();
            PlaceBeaconTower();
            PlaceFishingGround();
            // Jetty and wreck: one of two spots on the shore, or none.
            OneOf(new[] { 0.35f, 0.35f, 0.3f }, () => PlaceDock(cliffDeg + 229f), () => PlaceDock(cliffDeg + 140f), () => { });
            OneOf(new[] { 0.35f, 0.3f, 0.35f }, () => PlaceWreck(cliffDeg + 194f), () => PlaceWreck(cliffDeg + 60f), () => { });
            PlaceHoard();
            PlacePlants();
            PlacePickables();
            PlaceOutsideGuard();
            if (templated)
            {
                // The game's structures come with their own levelling and paint, made for the land: under our
                // ground mesh they would only reshape the sea floor.
                int stray = 0;
                foreach (TerrainModifier m in root.GetComponentsInChildren<TerrainModifier>(true)) if (m != null) { Object.DestroyImmediate(m); stray++; }
                foreach (TerrainOp op in root.GetComponentsInChildren<TerrainOp>(true)) if (op != null) { Object.DestroyImmediate(op.gameObject); stray++; }
                if (stray > 0) Plugin.Debug(locationName + ": removed " + stray + " terrain modifiers");
            }
            Plugin.Debug(locationName + " built: " + built + " parts, " + missing + " prefabs not found. Ground material: " + Describe(rockMaterial));

            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.BiomeArea = Heightmap.BiomeArea.Median; // whole zone is ocean: no islets glued to a coast
            // The tier's count is shared between its four shapes; only the new kind is placed anew.
            int shape = variant % Variants;
            config.Quantity = !templated || onlyDesign > 0 ? 0 : tier.Count.Value / Variants + (shape < tier.Count.Value % Variants ? 1 : 0);
            config.Priotized = true;
            config.SnapToWater = true;
            config.RandomRotation = onlyDesign <= 0;
            config.ClearArea = false;
            config.ExteriorRadius = 30f;
            config.MinAltitude = -1000f;
            config.MaxAltitude = -6f; // at least 6 m of water: open sea, not a beach
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 1000f; // the sea floor under it doesn't matter
            config.MinDistance = tier.MinDistance;
            config.MaxDistance = tier.MaxDistance;
            config.Group = Group;
            config.MinDistanceFromSimilar = 300f;
            // Vanilla damages a location's building pieces at random when it spawns, so the ruins look old;
            // an outpost someone lives in is kept up.
            root.AddComponent<Location>().m_applyRandomDamage = tier.Ruined;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + locationName + " (quantity " + config.Quantity + ")");
        }

        // ---- ground ----

        static float Noise(float x, float z)
        {
            return Mathf.PerlinNoise(x + seed, z + seed * 0.7f) * 2f - 1f;
        }

        static float Shore(float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float oval = 1f / Mathf.Sqrt(c * c / (stretchX * stretchX) + s * s / (stretchZ * stretchZ));
            float cove = 1f;
            if (!float.IsNaN(coveDeg))
            {
                float k = Mathf.Max(0f, Mathf.Cos(angle - coveDeg * Mathf.Deg2Rad));
                cove = 1f - 0.3f * Mathf.Pow(k, 10f);
            }
            float r = shoreRadius * oval * cove * (1f + 0.16f * Noise(c * 1.3f + 5f, s * 1.3f + 5f) + 0.08f * Noise(c * 3f + 11f, s * 3f + 11f));
            return Mathf.Max(r, Plateau(angle) + 3f); // always some slope between the plateau and the sea
        }

        static float Plateau(float angle)
        {
            return plateauRadius * (1f + 0.12f * Noise(Mathf.Cos(angle) * 2f + 21f, Mathf.Sin(angle) * 2f + 21f));
        }

        // 1 on the cliff side, falling to 0 a quarter turn away.
        static float Cliff(float angle)
        {
            float c = Mathf.Max(0f, Mathf.Cos(angle - cliffDeg * Mathf.Deg2Rad));
            return c * c * c;
        }

        // Height of the ground above sea level at a local position.
        internal static float Height(float x, float z)
        {
            if (heightOverride != null) return heightOverride(x, z);
            float h = NaturalHeight(x, z);
            return cuts.Count > 0 && !ignoreCuts ? ApplyCuts(x, z, h) : h;
        }

        static bool ignoreCuts; // while a design is laid out at the middle before being moved (Village)

        // An islet with a shape of its own (the final arena): its height and its shore, by direction, while
        // it's built; null for every other islet.
        static System.Func<float, float, float> heightOverride;
        static System.Func<float, float> shoreOverride;

        static float NaturalHeight(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float angle = Mathf.Atan2(z, x);
            float shore = Shore(angle);
            float plateau = Plateau(angle);
            float fine = Noise(x * 0.5f + 1f, z * 0.5f + 2f);
            if (r <= plateau) return PlateauHeight + fine * 0.1f;

            float rough = Noise(x * 0.09f + 3f, z * 0.09f + 7f) + 0.5f * Noise(x * 0.2f + 13f, z * 0.2f + 17f) + 0.25f * Noise(x * 0.45f + 31f, z * 0.45f + 37f);
            float detail = Noise(x * 0.9f + 41f, z * 0.9f + 43f) * 0.18f;
            if (r <= shore)
            {
                float t = (r - plateau) / (shore - plateau);
                float cliff = Cliff(angle);
                // The cliff side rises into a ridge past the plateau, stays high, then drops steeply into the sea.
                float tc = Mathf.Lerp(t, t * t * t, cliff);
                float smooth = tc * tc * (3f - 2f * tc);
                float ridge = cliff * 2.4f * 4f * t * (1f - t);
                float edge = Mathf.Clamp01(t * 4f); // no step where the plateau ends
                return PlateauHeight * (1f - smooth) + ridge + Hump(x, z, plateau, shore) * Mathf.Sin(Mathf.PI * t)
                    + (rough * 1.6f * Mathf.Sin(Mathf.PI * t) + fine * 0.35f + detail) * edge;
            }
            float d = r - shore;
            if (d <= 7f) return -d * 0.7f + rough * 0.8f * (1f - d / 7f) + detail;
            return Mathf.Max(Bottom, -4.9f - (d - 7f) * 4f);
        }

        // The second hump of a two-humped islet: a rounded rise on the slope, fading out before the shore.
        static float Hump(float x, float z, float plateau, float shore)
        {
            if (float.IsNaN(hillDeg)) return 0f;
            Vector2 centre = Polar(hillDeg, (plateau + shore) * 0.5f);
            float d2 = (x - centre.x) * (x - centre.x) + (z - centre.y) * (z - centre.y);
            return 3.2f * Mathf.Exp(-d2 / (2f * 3.5f * 3.5f));
        }

        static Vector3 Normal(float x, float z)
        {
            const float e = 0.35f;
            float dx = Height(x + e, z) - Height(x - e, z);
            float dz = Height(x, z + e) - Height(x, z - e);
            return new Vector3(-dx, 2f * e, -dz).normalized;
        }

        static Material BuildGround()
        {
            int rings = Mathf.RoundToInt(maxRadius / RingStep);
            // Rings of Segments + 1 vertices: the last repeats the first, so the texture can wrap around
            // without a seam. Normals come from the height function, so the surface is shaded smoothly.
            int perRing = segments + 1;
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int seg = 0; seg < perRing; seg++)
            {
                verts.Add(new Vector3(0f, Height(0f, 0f), 0f));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2((float)seg / segments, 0f));
            }
            for (int ring = 1; ring <= rings; ring++)
            {
                for (int seg = 0; seg < perRing; seg++)
                {
                    // Jitter the grid a little so it doesn't show as rings.
                    int s = seg % segments;
                    float jitter = cuts.Count > 0 ? 0f : 0.3f; // not where the ground is cut steeply: it would fold over
                    float a = (seg + jitter * Noise(ring * 1.7f + 0.5f, s * 1.3f + 0.5f)) * Mathf.PI * 2f / segments;
                    float r = (ring + jitter * Noise(s * 2.1f + 7.5f, ring * 0.9f + 3.5f)) * RingStep;
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    float y = Height(x, z);
                    verts.Add(new Vector3(x, y, z));
                    normals.Add(Normal(x, z));
                    // The terrain shader projects its textures in world space; uv only reads the paint mask.
                    uvs.Add(new Vector2(x / (2f * maxRadius) + 0.5f, z / (2f * maxRadius) + 0.5f));
                }
            }
            // Vertex colour is the biome blend the terrain shader paints with.
            Color32 biome = Heightmap.GetBiomeColor(tier.Biome);
            List<Color32> colors = new List<Color32>(verts.Count);
            for (int i = 0; i < verts.Count; i++) colors.Add(biome);
            for (int ring = 0; ring < rings; ring++)
            {
                int inner = ring * perRing, outer = (ring + 1) * perRing;
                for (int seg = 0; seg < segments; seg++)
                {
                    tris.Add(inner + seg); tris.Add(inner + seg + 1); tris.Add(outer + seg);
                    tris.Add(inner + seg + 1); tris.Add(outer + seg + 1); tris.Add(outer + seg);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "IoV_IsletGround";
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            GameObject ground = new GameObject("IoV_Ground");
            ground.layer = LayerMask.NameToLayer("static_solid");
            ground.transform.SetParent(root.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = ground.AddComponent<MeshRenderer>();
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Valheim's own terrain material: grass on flat ground, sand by the water, rock on steep slopes,
            // like any other shore. Its paint mask is left blank (nothing dug, cultivated or paved).
            Material material = null;
            Heightmap terrain = ZoneSystem.instance != null && ZoneSystem.instance.m_zonePrefab != null
                ? ZoneSystem.instance.m_zonePrefab.GetComponentInChildren<Heightmap>(true) : null;
            if (terrain != null && terrain.m_material != null && Plugin.GroundStyle.Value == Plugin.GroundTerrain)
            {
                material = new Material(terrain.m_material);
                material.name = "IoV_IsletTerrain";
                Texture2D mask = new Texture2D(2, 2);
                mask.name = "IoV_IsletPaintMask";
                mask.wrapMode = TextureWrapMode.Clamp;
                // Alpha is lava where the ground is painted as Ashlands, and the Mistlands' grass instead of
                // their moss, so it stays 0 there.
                Color nothing = new Color(0f, 0f, 0f, tier.Biome == Heightmap.Biome.AshLands || tier.Biome == Heightmap.Biome.Mistlands ? 0f : 1f);
                mask.SetPixels(new Color[] { nothing, nothing, nothing, nothing });
                mask.Apply();
                material.SetTexture("_ClearedMaskTex", mask);
                // Real terrain sets this per renderer while the game runs (Heightmap.ApplySettings); left at 0,
                // the shader hides the islet at any distance.
                material.SetFloat("_LodHideDistance", 10000f);
                LogProperties(material);
                renderer.sharedMaterial = material;
                Plugin.Debug("Ground uses the terrain material " + terrain.m_material.name);
                return material;
            }
            if (Plugin.GroundStyle.Value == Plugin.GroundTerrain) Plugin.Log.LogWarning("Terrain material not found, the islet ground falls back to a rock material.");
            foreach (string name in bigRocks)
            {
                GameObject prefab = PrefabManager.Instance.GetPrefab(name);
                if (prefab == null) continue;
                MeshRenderer source = prefab.GetComponentInChildren<MeshRenderer>(true);
                if (source != null && source.sharedMaterial != null)
                {
                    material = source.sharedMaterial;
                    Plugin.Debug("Ground material taken from " + name);
                    break;
                }
            }
            if (material != null) renderer.sharedMaterial = material;
            // The rock shader maps its texture by uv: project from above, shifted along the slope so steep
            // faces aren't stretched. Values stay small; large ones streak in the shader.
            for (int i = 0; i < verts.Count; i++)
            {
                float lift = Mathf.Clamp(verts[i].y, -6f, PlateauHeight + 3f);
                uvs[i] = new Vector2(verts[i].x + normals[i].x * lift, verts[i].z + normals[i].z * lift) / 8f;
            }
            mesh.SetUVs(0, uvs);
            return material;
        }

        // Lowest ground under a circle, so things on a slope don't hang in the air on the downhill side.
        static float GroundMin(float x, float z, float radius)
        {
            float min = Height(x, z);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                min = Mathf.Min(min, Height(x + Mathf.Cos(a) * radius, z + Mathf.Sin(a) * radius));
            }
            return min;
        }

        static Vector2 Polar(float degrees, float radius)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        static float ShoreAt(float degrees)
        {
            if (shoreOverride != null) return shoreOverride(degrees);
            return Shore(degrees * Mathf.Deg2Rad);
        }

        static float PlateauAt(float degrees)
        {
            return Plateau(degrees * Mathf.Deg2Rad);
        }

        // ---- static copies of vanilla prefabs ----

        static GameObject Find(string[] candidates)
        {
            foreach (string name in candidates)
            {
                GameObject prefab = PrefabManager.Instance.GetPrefab(name);
                if (prefab != null) return prefab;
            }
            missing++;
            Plugin.Debug("None of these prefabs exist: " + string.Join(", ", candidates));
            return null;
        }

        // On the ground at (x, z): size > 0 scales the largest horizontal extent to that many metres,
        // and the copy is sunk by sink times its height below the lowest ground under it.
        static GameObject OnGround(string[] candidates, Vector2 at, float rotY, float size, float sink)
        {
            return OnGround(candidates, at, rotY, size, sink, true);
        }

        // strip false keeps the vanilla object as it is (networked: trees can be chopped).
        static GameObject OnGround(string[] candidates, Vector2 at, float rotY, float size, float sink, bool strip)
        {
            GameObject go = Copy(candidates, new Vector3(0f, rotY, 0f), size, strip);
            if (go == null) return null;
            Bounds b = LocalBounds(go);
            float footprint = 0.4f * Mathf.Max(b.size.x, b.size.z);
            float y = GroundMin(at.x, at.y, footprint) - sink * b.size.y;
            Move(go, b, new Vector3(at.x, y, at.y), false);
            return go;
        }

        // A real building piece (networked, can be broken for its materials like vanilla ruins), with its
        // bottom (or top, for floor tiles) at pos.
        static GameObject At(string name, Vector3 pos, Vector3 euler, bool alignTop)
        {
            GameObject go = Copy(new string[] { name }, euler, 0f, false);
            if (go == null) return null;
            Bounds b = SolidBounds(go);
            if (!alignTop)
            {
                // A piece meant to stand on the ground must touch it, or it has no support and collapses.
                // Just above the ground means "on the ground": sink it in, deep enough that it still
                // touches where the ground is a little higher or lower (ruins are turned after building).
                float ground = GroundMin(pos.x, pos.z, 0.35f * Mathf.Max(b.size.x, b.size.z));
                if (pos.y > ground - 0.1f && pos.y < ground + 0.6f) pos.y = ground - 0.3f;
            }
            Move(go, b, pos, alignTop);
            return go;
        }

        // strip: remove all scripts, leaving a static mesh and collider.
        static GameObject Copy(string[] candidates, Vector3 euler, float size, bool strip)
        {
            GameObject prefab = Find(candidates);
            if (prefab == null) return null;
            if (!strip) prefab = PlantOrSelf(prefab.name);
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localPosition = Vector3.zero;
            if (strip)
            {
                // An item as decoration: without the shine the game puts on things to pick up, so it doesn't look like loot.
                bool item = go.GetComponent<ItemDrop>() != null;
                Strip(go);
                foreach (ParticleSystem shine in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!item && !quietProps) continue;
                    Plugin.Debug("  " + go.name + ": particles " + shine.name + " turned off");
                    if (shine.gameObject != go) { shine.gameObject.SetActive(false); continue; }
                    // On the item itself (a weapon's glint): it would start again when the islet is built, so it
                    // goes quiet for good, emitting nothing and drawing nothing.
                    ParticleSystem.MainModule main = shine.main;
                    main.playOnAwake = false;
                    ParticleSystem.EmissionModule emission = shine.emission;
                    emission.enabled = false;
                    ParticleSystemRenderer drawn = shine.GetComponent<ParticleSystemRenderer>();
                    if (drawn != null) drawn.enabled = false;
                }
            }

            if (size > 0f)
            {
                Bounds b = LocalBounds(go);
                float extent = Mathf.Max(b.size.x, b.size.z);
                if (extent > 0.01f) go.transform.localScale = Vector3.one * (size / extent);
            }
            built++;
            return go;
        }

        static void Move(GameObject go, Bounds b, Vector3 pos, bool alignTop)
        {
            float y = alignTop ? pos.y - b.max.y : pos.y - b.min.y;
            go.transform.localPosition = new Vector3(pos.x - b.center.x, y, pos.z - b.center.z);
            Plugin.Debug("  " + go.name + " size " + b.size.ToString("F1") + " at " + pos.ToString("F1"));
        }

        static void Strip(GameObject go)
        {
            // Several passes: a script that another one requires can only go after that one.
            for (int pass = 0; pass < 4; pass++)
            {
                MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
                if (scripts.Length == 0) break;
                for (int i = scripts.Length - 1; i >= 0; i--) Object.DestroyImmediate(scripts[i]);
            }
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
        }

        // Bounds in the location's space of the meshes that would be visible (Renderer.bounds is empty
        // while the location container is inactive).
        static Bounds LocalBounds(GameObject go)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !ActiveBelow(filter.transform, go.transform)) continue;
                Bounds mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = root.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        // Bounds of a piece's colliders (what support is worked out from) in the location's space; the
        // mesh can be bumpier than its collider, so stacking by the mesh would leave a gap. Falls back to
        // the mesh bounds for a piece without colliders.
        static Bounds SolidBounds(GameObject go)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger || !ActiveBelow(collider.transform, go.transform)) continue;
                Bounds local;
                BoxCollider box = collider as BoxCollider;
                MeshCollider mesh = collider as MeshCollider;
                if (box != null) local = new Bounds(box.center, box.size);
                else if (mesh != null && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
                else continue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = root.transform.InverseTransformPoint(collider.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return any ? result : LocalBounds(go);
        }

        static bool ActiveBelow(Transform t, Transform top)
        {
            for (; t != null && t != top; t = t.parent)
                if (!t.gameObject.activeSelf) return false;
            return true;
        }

        // ---- layout ----

        static void PlaceRocks()
        {
            // Big rocks along the shore give the islet its outline; they leave room for the jetty and wreck spots.
            float[] angles = { 159f, 209f, 249f, 304f, 354f, 29f, 84f, 124f };
            float[] sizes = { 7f, 4.5f, 6f, 5f, 9f, 7.5f, 6f, 4f };
            for (int i = 0; i < angles.Length; i++)
            {
                float deg = cliffDeg + angles[i];
                OnGround(tier.BigRocks, Polar(deg, ShoreAt(deg) - 2f), deg * 3f, sizes[i], 0.4f);
            }
            // Outcrops on the slopes, half buried.
            float[] slope = { 120f, 185f, 215f, 265f, 20f };
            float[] slopeSizes = { 4f, 5f, 4.5f, 3.5f, 3f };
            for (int i = 0; i < slope.Length; i++)
            {
                float r = (PlateauAt(slope[i]) + ShoreAt(slope[i])) * 0.5f;
                OnGround(tier.BigRocks, Polar(slope[i], r), slope[i] * 2f, slopeSizes[i], 0.5f);
            }
            // Loose stones, a different few on every islet.
            float[] small = { 40f, 75f, 170f, 250f, 310f, 345f };
            for (int i = 0; i < small.Length; i++)
            {
                int k = i;
                Optional(0.6f, () => OnGround(tier.SmallRocks, Polar(small[k], ShoreAt(small[k]) - 4f - k % 3), small[k], 1.2f + (k % 3) * 0.6f, 0.3f));
            }
            // Where the sea freezes: ice washed up on the shore.
            if (tier.ShoreIce == null) return;
            float[] ice = { 95f, 200f, 280f, 330f };
            for (int i = 0; i < ice.Length; i++)
            {
                int k = i;
                Optional(0.7f, () => OnGround(tier.ShoreIce, Polar(cliffDeg + ice[k], ShoreAt(cliffDeg + ice[k]) - 0.5f), ice[k] * 4f, 2f + (k % 2) * 1.2f, 0.35f));
            }
        }

        // An old wooden jetty on the low side: one plank section on land, one over the water, and the
        // poles of a third that's gone. Boats can moor next to it.
        static void PlaceDock(float deg)
        {
            float s = ShoreAt(deg);
            Vector2 u = Polar(deg, 1f), side = new Vector2(-u.y, u.x);
            float yaw = 90f - deg;
            Vector2 land = u * (s - 2f);
            float top = Mathf.Max(0.9f, GroundMin(land.x, land.y, 1f) + 0.1f);
            for (int i = 0; i < 2; i++)
            {
                Vector2 p = u * (s - 1f + i * 2f);
                At(tier.JettyFloor, new Vector3(p.x, top, p.y), new Vector3(0f, yaw, 0f), true);
            }
            float[] along = { s + 0.1f, s + 1.9f, s + 3.9f };
            for (int i = 0; i < along.Length; i++)
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector2 p = u * along[i] + side * (0.9f * k);
                    float poleTop = i == 2 ? top + 0.6f - k * 0.25f : top;
                    float surface = GroundMin(p.x, p.y, 0.2f);
                    float ground = surface - 0.3f;
                    int count = Mathf.Max(1, Mathf.CeilToInt((poleTop - ground) / 2f));
                    for (int n = 0; n < count; n++)
                    {
                        // Poles stack down from the top, each placed by its top so the stack has no gaps
                        // (placing by the bottom would sink one that ends near the ground, away from
                        // the pole above). A pole well under the sea floor touches nothing and is left out;
                        // the one above it reaches through the floor instead.
                        float poleEnd = poleTop - 2f * (count - 1 - n);
                        if (poleEnd < surface - 0.25f) continue;
                        At(tier.JettyPole, new Vector3(p.x, poleEnd, p.y), new Vector3(0f, yaw, 0f), true);
                    }
                }
        }

        // A wrecked ship on the shore around deg: bow, stern and something in between.
        static void PlaceWreck(float deg)
        {
            OnGround(new[] { tier.Wreck[0] }, Polar(deg - 8f, ShoreAt(deg - 8f) - 3f), deg + 88f, 0f, 0.25f);
            OnGround(new[] { tier.Wreck[1] }, Polar(deg + 8f, ShoreAt(deg + 8f) - 1.5f), deg + 128f, 0f, 0.3f);
            OnGround(new[] { tier.Wreck[2] }, Polar(deg, ShoreAt(deg) - 2.5f), deg + 108f, 0f, 0.2f);
        }

        // On one islet in eight: a second, richer chest hidden among rocks on the slope, with coins spilled around it.
        static void PlaceHoard()
        {
            Optional(0.125f, () =>
            {
                float deg = cliffDeg + 95f;
                float r = (PlateauAt(deg) + ShoreAt(deg)) * 0.5f;
                Vector2 c = Polar(deg, r);
                OnGround(tier.BigRocks, Polar(deg - 9f, r + 0.5f), deg * 2f, 4.5f, 0.45f);
                OnGround(tier.BigRocks, Polar(deg + 9f, r - 0.5f), deg * 3f, 3.5f, 0.45f);
                PlaceNetworked(tier.HoardName, new Vector3(c.x, GroundMin(c.x, c.y, 0.6f), c.y), 90f - deg);
                Prop(new[] { "treasure_pile" }, Polar(deg + 3f, r + 1.3f), 0f, 0.1f);
                Prop(new[] { "treasure_stack" }, Polar(deg - 3f, r - 1.2f), 40f, 0.05f);
            });
            // A runestone on some islets.
            Optional(0.3f, () => PlaceNetworked(new[] { tier.RuneStone }, Polar(cliffDeg + 250f, PlateauAt(cliffDeg + 250f) + 1.5f), cliffDeg, 0f));
        }

        static void PlacePlants()
        {
            float[] angles = { 60f, 110f, 170f, 235f, 280f, 345f };
            for (int i = 0; i < angles.Length; i++)
            {
                int k = i;
                Optional(0.7f, () => OnGround(tier.Bushes, Polar(angles[k], PlateauAt(angles[k]) + 1.5f + k % 2), angles[k] * 5f, 0f, 0.15f, false));
            }
            Optional(0.8f, () => OnGround(tier.Trees, Polar(320f, PlateauAt(320f) + 0.5f), 45f, 0f, 0.05f, false));
            Optional(0.6f, () => OnGround(tier.Trees, Polar(30f, PlateauAt(30f) + 2f), 160f, 0f, 0.05f, false));
            Optional(0.7f, () => OnGround(tier.SmallTrees, Polar(140f, PlateauAt(140f) + 1f), 200f, 0f, 0.05f, false));
        }

        // Vanilla pickables: they respawn on their own like anywhere else.
        static void PlacePickables()
        {
            Optional(0.8f, () => PlaceNetworked(tier.Berries, Polar(80f, PlateauAt(80f) + 1f), 30f, 0f));
            Optional(0.8f, () => PlaceNetworked(tier.Berries, Polar(255f, PlateauAt(255f) + 2f), 200f, 0f));
            Optional(0.6f, () => PlaceNetworked(tier.Berries, Polar(170f, PlateauAt(170f) + 1.5f), 90f, 0f));
            Optional(0.7f, () => PlaceNetworked(tier.Mushrooms, Polar(130f, PlateauAt(130f) + 0.5f), 0f, 0f));
            Optional(0.7f, () => PlaceNetworked(tier.Mushrooms, Polar(300f, PlateauAt(300f) + 1.5f), 0f, 0f));
            float[] shore = { 15f, 105f, 160f, 245f, 315f };
            for (int i = 0; i < shore.Length; i++)
            {
                int k = i;
                Optional(0.75f, () => PlaceNetworked(new[] { k % 2 == 0 ? "Pickable_Flint" : "Pickable_Stone" }, Polar(shore[k], ShoreAt(shore[k]) - 3f), shore[k], 0f));
            }
            Optional(0.7f, () => PlaceNetworked(new[] { "Pickable_Branch" }, Polar(25f, ShoreAt(25f) - 4f), 80f, 0f));
            Optional(0.7f, () => PlaceNetworked(new[] { "Pickable_Branch" }, Polar(185f, PlateauAt(185f) + 2f), 10f, 0f));
        }

        // The chest and the three guards in the main ruin. Called by each ruin design, inside its group,
        // so they come and go with it.
        static void CourtyardGuardsAndChest(float floor, Vector3 chest, float chestRotY)
        {
            float y = floor + 0.2f;
            string[] guards = tier.Guards;
            PlaceNetworked(IsletTier.SpawnerName(guards[0]), new Vector3(1.5f, y, 0.5f), 0f);
            PlaceNetworked(IsletTier.SpawnerName(guards[1]), new Vector3(-1f, y, -1.5f), 0f);
            PlaceNetworked(IsletTier.SpawnerName(guards[2]), new Vector3(0f, y, 1.5f), 0f);
            PlaceNetworked(tier.ChestName, chest, chestRotY);
        }

        // The fourth guard keeps watch near the wreck side; the fifth is with the ridge structure.
        static void PlaceOutsideGuard()
        {
            float deg = cliffDeg + 194f;
            PlaceNetworked(new[] { IsletTier.SpawnerName(tier.Guards[3]) }, Polar(deg, ShoreAt(deg) - 6f), 0f, 0.3f);
        }

        static void RidgeGuard()
        {
            float deg = cliffDeg + 19f;
            PlaceNetworked(new[] { IsletTier.SpawnerName(tier.Guards[4]) }, Polar(deg, PlateauAt(deg) - 1f), 0f, 0.3f);
        }

        // ---- variety: vanilla's own per-instance randomness for location parts ----

        // Parts built in build are there on some islets and not on others (RandomSpawn).
        static void Optional(float chance, System.Action build)
        {
            GameObject group = new GameObject("IoV_Optional");
            group.transform.SetParent(parent, false);
            group.AddComponent<RandomSpawn>().m_chanceToSpawn = chance * 100f;
            Transform outer = parent;
            parent = group.transform;
            build();
            parent = outer;
        }

        // Exactly one of the designs is there on each islet (RandomObject).
        static void OneOf(params System.Action[] designs)
        {
            float[] weights = new float[designs.Length];
            for (int i = 0; i < weights.Length; i++) weights[i] = 1f;
            OneOf(weights, designs);
        }

        static void OneOf(float[] weights, params System.Action[] designs)
        {
            GameObject holder = new GameObject("IoV_OneOf");
            holder.transform.SetParent(parent, false);
            RandomObject random = holder.AddComponent<RandomObject>();
            random.m_objects = new List<RandomObject.ObjectEntry>();
            Transform outer = parent;
            for (int i = 0; i < designs.Length; i++)
            {
                GameObject group = new GameObject("IoV_Design");
                group.transform.SetParent(holder.transform, false);
                RandomObject.ObjectEntry entry = new RandomObject.ObjectEntry();
                entry.m_object = group;
                entry.m_weight = weights[i];
                random.m_objects.Add(entry);
                parent = group.transform;
                designs[i]();
            }
            parent = outer;
        }

        // The first of the candidates that exists, on the ground at (x, z).
        static void PlaceNetworked(string[] candidates, Vector2 at, float rotY, float lift)
        {
            string name = candidates[0];
            foreach (string candidate in candidates)
                if (PlantOrSelf(candidate) != null) { name = candidate; break; }
            PlaceNetworked(name, new Vector3(at.x, Height(at.x, at.y) + lift, at.y), rotY);
        }

        // The islet's own copy of a vanilla plant if there is one (see IsletPrefabs), else the prefab itself.
        static GameObject PlantOrSelf(string name)
        {
            GameObject plant = PrefabManager.Instance.GetPrefab(IsletPrefabs.PlantName(name));
            return plant != null ? plant : PrefabManager.Instance.GetPrefab(name);
        }

        static void PlaceNetworked(string name, Vector3 pos, float rotY)
        {
            GameObject prefab = PlantOrSelf(name);
            if (prefab == null)
            {
                missing++;
                Plugin.Debug("Prefab " + name + " missing");
                return;
            }
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            built++;
            Plugin.Debug("  " + name + " at " + pos.ToString("F1"));
        }

        static void LogProperties(Material m)
        {
            if (!Plugin.DebugLog.Value || m.shader == null) return;
            List<string> props = new List<string>();
            for (int i = 0; i < m.shader.GetPropertyCount(); i++)
            {
                string name = m.shader.GetPropertyName(i);
                UnityEngine.Rendering.ShaderPropertyType type = m.shader.GetPropertyType(i);
                if (type == UnityEngine.Rendering.ShaderPropertyType.Float || type == UnityEngine.Rendering.ShaderPropertyType.Range)
                    props.Add(name + "=" + m.GetFloat(name));
                else props.Add(name + "(" + type + ")");
            }
            Plugin.Debug("Terrain shader " + m.shader.name + " keywords [" + string.Join(" ", m.shaderKeywords) + "] properties: " + string.Join(", ", props.ToArray()));
        }

        static string Describe(Material m)
        {
            if (m == null) return "none";
            return m.name + " (shader " + (m.shader != null ? m.shader.name : "?") + ")";
        }
    }
}