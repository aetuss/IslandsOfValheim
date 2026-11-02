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
        internal const float PlateauHeight = 3f;

        const float PlateauRadius = 9.5f;
        const float ShoreRadius = 20f;
        const float CliffAngle = 206f * Mathf.Deg2Rad; // the high, steep side
        const float Bottom = -30f;
        const int Segments = 72;
        const float MaxRadius = 46f;
        const float RingStep = 1f;
        const float FloorTop = PlateauHeight + 0.1f;

        static readonly string[] bigRocks = { "rock4_coast", "rock1_mountain", "Rock_7", "rock3_mountain", "Rock_3" };
        static readonly string[] smallRocks = { "Rock_3", "rock4_coast", "rock1_mountain" };

        static IsletTier tier;
        static GameObject root;
        static int built;
        static int missing;

        internal static void Register(IsletTier islet)
        {
            tier = islet;
            root = ZoneManager.Instance.CreateLocationContainer(tier.LocationName);
            built = 0;
            missing = 0;

            Material rockMaterial = BuildGround();
            PlaceRocks();
            PlaceRuins();
            PlaceDock();
            PlaceWreck();
            PlacePlants();
            PlacePickables();
            PlaceSpawnersAndChest();
            Plugin.Debug(tier.LocationName + " built: " + built + " parts, " + missing + " prefabs not found. Ground material: " + Describe(rockMaterial));

            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.BiomeArea = Heightmap.BiomeArea.Median; // whole zone is ocean: no islets glued to a coast
            config.Quantity = tier.Count.Value;
            config.Priotized = true;
            config.SnapToWater = true;
            config.RandomRotation = true;
            config.ClearArea = false;
            config.ExteriorRadius = 30f;
            config.MinAltitude = -1000f;
            config.MaxAltitude = -6f; // at least 6 m of water: open sea, not a beach
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 1000f; // the sea floor under it doesn't matter
            config.MinDistance = tier.MinDistance;
            config.MaxDistance = tier.MaxDistance;
            config.Group = "IoV_Islets";
            config.MinDistanceFromSimilar = 300f;
            // Vanilla damages a location's building pieces at random when it spawns, so the ruins look old.
            root.AddComponent<Location>().m_applyRandomDamage = true;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + tier.LocationName + " (quantity " + config.Quantity + ")");
        }

        // ---- ground ----

        static float Noise(float x, float z)
        {
            return Mathf.PerlinNoise(x + tier.Seed, z + tier.Seed * 0.7f) * 2f - 1f;
        }

        static float Shore(float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return ShoreRadius * (1f + 0.16f * Noise(c * 1.3f + 5f, s * 1.3f + 5f) + 0.08f * Noise(c * 3f + 11f, s * 3f + 11f));
        }

        static float Plateau(float angle)
        {
            return PlateauRadius * (1f + 0.12f * Noise(Mathf.Cos(angle) * 2f + 21f, Mathf.Sin(angle) * 2f + 21f));
        }

        // 1 on the cliff side, falling to 0 a quarter turn away.
        static float Cliff(float angle)
        {
            float c = Mathf.Max(0f, Mathf.Cos(angle - CliffAngle));
            return c * c * c;
        }

        // Height of the ground above sea level at a local position.
        internal static float Height(float x, float z)
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
                return PlateauHeight * (1f - smooth) + ridge + (rough * 1.6f * Mathf.Sin(Mathf.PI * t) + fine * 0.35f + detail) * edge;
            }
            float d = r - shore;
            if (d <= 7f) return -d * 0.7f + rough * 0.8f * (1f - d / 7f) + detail;
            return Mathf.Max(Bottom, -4.9f - (d - 7f) * 4f);
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
            int rings = Mathf.RoundToInt(MaxRadius / RingStep);
            // Rings of Segments + 1 vertices: the last repeats the first, so the texture can wrap around
            // without a seam. Normals come from the height function, so the surface is shaded smoothly.
            int perRing = Segments + 1;
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int seg = 0; seg < perRing; seg++)
            {
                verts.Add(new Vector3(0f, Height(0f, 0f), 0f));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2((float)seg / Segments, 0f));
            }
            for (int ring = 1; ring <= rings; ring++)
            {
                for (int seg = 0; seg < perRing; seg++)
                {
                    // Jitter the grid a little so it doesn't show as rings.
                    int s = seg % Segments;
                    float a = (seg + 0.3f * Noise(ring * 1.7f + 0.5f, s * 1.3f + 0.5f)) * Mathf.PI * 2f / Segments;
                    float r = (ring + 0.3f * Noise(s * 2.1f + 7.5f, ring * 0.9f + 3.5f)) * RingStep;
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    float y = Height(x, z);
                    verts.Add(new Vector3(x, y, z));
                    normals.Add(Normal(x, z));
                    // The terrain shader projects its textures in world space; uv only reads the paint mask.
                    uvs.Add(new Vector2(x / (2f * MaxRadius) + 0.5f, z / (2f * MaxRadius) + 0.5f));
                }
            }
            // Vertex colour is the biome blend the terrain shader paints with.
            Color32 biome = Heightmap.GetBiomeColor(tier.Biome);
            List<Color32> colors = new List<Color32>(verts.Count);
            for (int i = 0; i < verts.Count; i++) colors.Add(biome);
            for (int ring = 0; ring < rings; ring++)
            {
                int inner = ring * perRing, outer = (ring + 1) * perRing;
                for (int seg = 0; seg < Segments; seg++)
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
                // Alpha is lava where the ground is painted as Ashlands, so it stays 0 there.
                Color nothing = new Color(0f, 0f, 0f, tier.Biome == Heightmap.Biome.AshLands ? 0f : 1f);
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
            Move(go, LocalBounds(go), pos, alignTop);
            return go;
        }

        // strip: remove all scripts, leaving a static mesh and collider.
        static GameObject Copy(string[] candidates, Vector3 euler, float size, bool strip)
        {
            GameObject prefab = Find(candidates);
            if (prefab == null) return null;
            if (!strip) prefab = PlantOrSelf(prefab.name);
            GameObject go = Object.Instantiate(prefab, root.transform, false);
            go.name = prefab.name;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localPosition = Vector3.zero;
            if (strip) Strip(go);

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

        static bool ActiveBelow(Transform t, Transform top)
        {
            for (; t != null && t != top; t = t.parent)
                if (!t.gameObject.activeSelf) return false;
            return true;
        }

        // ---- layout ----

        static void PlaceRocks()
        {
            // Big rocks along the shore give the islet its outline.
            float[] angles = { 5f, 55f, 95f, 150f, 200f, 235f, 290f, 330f };
            float[] sizes = { 7f, 4.5f, 6f, 5f, 9f, 7.5f, 6f, 4f };
            for (int i = 0; i < angles.Length; i++)
                OnGround(bigRocks, Polar(angles[i], ShoreAt(angles[i]) - 2f), angles[i] * 3f, sizes[i], 0.4f);
            // Outcrops on the slopes, half buried.
            float[] slope = { 120f, 185f, 215f, 265f, 20f };
            float[] slopeSizes = { 4f, 5f, 4.5f, 3.5f, 3f };
            for (int i = 0; i < slope.Length; i++)
            {
                float r = (PlateauAt(slope[i]) + ShoreAt(slope[i])) * 0.5f;
                OnGround(bigRocks, Polar(slope[i], r), slope[i] * 2f, slopeSizes[i], 0.5f);
            }
            // Loose stones.
            float[] small = { 40f, 75f, 170f, 250f, 310f, 345f };
            for (int i = 0; i < small.Length; i++)
                OnGround(smallRocks, Polar(small[i], ShoreAt(small[i]) - 4f - i % 3), small[i], 1.2f + (i % 3) * 0.6f, 0.3f);
        }

        // An old wooden jetty on the low side: one plank section on land, one over the water, and the
        // poles of a third that's gone. Boats can moor next to it.
        static void PlaceDock()
        {
            const float deg = 75f;
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
                    float ground = GroundMin(p.x, p.y, 0.2f) - 0.3f;
                    int count = Mathf.Max(1, Mathf.CeilToInt((poleTop - ground) / 2f));
                    for (int n = 0; n < count; n++)
                        At(tier.JettyPole, new Vector3(p.x, poleTop - 2f * (count - n), p.y), new Vector3(0f, yaw, 0f), false);
                }
        }

        // A wrecked ship on the low side of the islet: bow, stern and something in between.
        static void PlaceWreck()
        {
            OnGround(new[] { tier.Wreck[0] }, Polar(32f, ShoreAt(32f) - 3f), 120f, 0f, 0.25f);
            OnGround(new[] { tier.Wreck[1] }, Polar(48f, ShoreAt(48f) - 1.5f), 160f, 0f, 0.3f);
            OnGround(new[] { tier.Wreck[2] }, Polar(40f, ShoreAt(40f) - 2.5f), 140f, 0f, 0.2f);
        }

        static void PlacePlants()
        {
            float[] angles = { 60f, 110f, 170f, 235f, 280f, 345f };
            for (int i = 0; i < angles.Length; i++)
                OnGround(tier.Bushes, Polar(angles[i], PlateauAt(angles[i]) + 1.5f + i % 2), angles[i] * 5f, 0f, 0.15f, false);
            OnGround(tier.Trees, Polar(320f, PlateauAt(320f) + 0.5f), 45f, 0f, 0.05f, false);
            OnGround(tier.SmallTrees, Polar(140f, PlateauAt(140f) + 1f), 200f, 0f, 0.05f, false);
        }

        // Vanilla pickables: they respawn on their own like anywhere else.
        static void PlacePickables()
        {
            PlaceNetworked(tier.Berries, Polar(80f, PlateauAt(80f) + 1f), 30f, 0f);
            PlaceNetworked(tier.Berries, Polar(255f, PlateauAt(255f) + 2f), 200f, 0f);
            PlaceNetworked(tier.Mushrooms, Polar(130f, PlateauAt(130f) + 0.5f), 0f, 0f);
            PlaceNetworked(tier.Mushrooms, Polar(300f, PlateauAt(300f) + 1.5f), 0f, 0f);
            float[] shore = { 15f, 105f, 160f, 245f, 315f };
            for (int i = 0; i < shore.Length; i++)
                PlaceNetworked(new[] { i % 2 == 0 ? "Pickable_Flint" : "Pickable_Stone" }, Polar(shore[i], ShoreAt(shore[i]) - 3f), shore[i], 0f);
            PlaceNetworked(new[] { "Pickable_Branch" }, Polar(25f, ShoreAt(25f) - 4f), 80f, 0f);
            PlaceNetworked(new[] { "Pickable_Branch" }, Polar(185f, PlateauAt(185f) + 2f), 10f, 0f);
        }

        static void PlaceSpawnersAndChest()
        {
            float y = ruinFloor + 0.2f;
            string[] guards = tier.Guards;
            PlaceNetworked(IsletTier.SpawnerName(guards[0]), new Vector3(1.5f, y, 0.5f), 0f);
            PlaceNetworked(IsletTier.SpawnerName(guards[1]), new Vector3(-1f, y, -1.5f), 0f);
            PlaceNetworked(IsletTier.SpawnerName(guards[2]), new Vector3(0f, y, 1.5f), 0f);
            PlaceNetworked(new[] { IsletTier.SpawnerName(guards[3]) }, Polar(40f, ShoreAt(40f) - 6f), 0f, 0.3f);
            PlaceNetworked(new[] { IsletTier.SpawnerName(guards[4]) }, Polar(225f, PlateauAt(225f) - 1f), 0f, 0.3f);
            PlaceNetworked(tier.ChestName, chestPos, chestRotY);
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
            GameObject go = Object.Instantiate(prefab, root.transform, false);
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