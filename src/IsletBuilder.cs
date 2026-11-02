using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Builds the islet location at runtime from vanilla assets, so the mod needs no asset bundle.
    //
    // The ground is a generated rock mound mesh. The location snaps to the water surface (y = 0 here
    // is sea level), so it works in deep water; the mound's skirt reaches 30 m down, and since players
    // can't dive, nobody sees that it doesn't touch the sea floor. Rocks, ruin pieces and plants are
    // static copies of vanilla prefabs with all their scripts removed: they render and collide for
    // everyone but aren't networked, can't be broken and don't drop anything.
    static class IsletBuilder
    {
        internal const string LocationName = "IoV_Islet_Meadows";

        const float PlateauHeight = 2.6f;
        const float PlateauRadius = 7.5f;
        const float ShoreRadius = 15f;
        const float Bottom = -30f;
        const int Segments = 72;
        const float MaxRadius = 34f;
        const float RingStep = 1f;

        static readonly string[] bigRocks = { "rock4_coast", "rock1_mountain", "Rock_7", "rock3_mountain", "Rock_3" };
        static readonly string[] smallRocks = { "Rock_3", "rock4_coast", "rock1_mountain" };
        static readonly string[] bushes = { "Bush01", "Bush02_en", "shrub_2", "Bush01_heath" };
        static readonly string[] trees = { "FirTree_small_dead", "FirTree_small", "Pinetree_01", "Beech_small1" };

        static GameObject root;
        static int built;
        static int missing;

        internal static void Register()
        {
            root = ZoneManager.Instance.CreateLocationContainer(LocationName);
            built = 0;
            missing = 0;

            Material rockMaterial = BuildGround();
            PlaceRocks();
            PlaceRuin();
            PlacePlants();
            PlaceSpawnersAndChest();
            Plugin.Debug("Islet built: " + built + " parts, " + missing + " prefabs not found. Ground material: " + Describe(rockMaterial));

            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.BiomeArea = Heightmap.BiomeArea.Median; // whole zone is ocean: no islets glued to a coast
            config.Quantity = Plugin.IsletCount.Value;
            config.Priotized = true;
            config.SnapToWater = true;
            config.RandomRotation = true;
            config.ClearArea = false;
            config.ExteriorRadius = 24f;
            config.MinAltitude = -1000f;
            config.MaxAltitude = -6f; // at least 6 m of water: open sea, not a beach
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 1000f; // the sea floor under it doesn't matter
            config.MinDistance = 300f;
            config.MaxDistance = 4500f; // Meadows / Black Forest tier: the inner ring of the world
            config.Group = "IoV_Islets";
            config.MinDistanceFromSimilar = 300f;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + LocationName + " (quantity " + config.Quantity + ")");
        }

        // ---- ground ----

        static float Noise(float x, float z)
        {
            return Mathf.PerlinNoise(x, z) * 2f - 1f;
        }

        static float Shore(float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return ShoreRadius * (1f + 0.16f * Noise(c * 1.3f + 5f, s * 1.3f + 5f) + 0.08f * Noise(c * 3f + 11f, s * 3f + 11f));
        }

        // Height of the ground above sea level at a local position.
        internal static float Height(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float shore = Shore(Mathf.Atan2(z, x));
            float bump = Noise(x * 0.15f + 3f, z * 0.15f + 7f);
            float fine = Noise(x * 0.5f + 1f, z * 0.5f + 2f);
            if (r <= PlateauRadius) return PlateauHeight + fine * 0.12f;
            if (r <= shore)
            {
                float t = (r - PlateauRadius) / (shore - PlateauRadius);
                float smooth = t * t * (3f - 2f * t);
                return PlateauHeight * (1f - smooth) + bump * 0.9f * Mathf.Sin(Mathf.PI * t) + fine * 0.2f;
            }
            float d = r - shore;
            if (d <= 6f) return -d * 0.7f + bump * 0.4f * (1f - d / 6f);
            return Mathf.Max(Bottom, -4.2f - (d - 6f) * 4f);
        }

        static Material BuildGround()
        {
            int rings = Mathf.RoundToInt(MaxRadius / RingStep);
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            verts.Add(new Vector3(0f, Height(0f, 0f), 0f));
            for (int ring = 1; ring <= rings; ring++)
            {
                float r = ring * RingStep;
                for (int seg = 0; seg < Segments; seg++)
                {
                    float a = seg * Mathf.PI * 2f / Segments;
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    verts.Add(new Vector3(x, Height(x, z), z));
                }
            }
            foreach (Vector3 v in verts) uvs.Add(new Vector2((v.x + v.y * 0.5f) / 5f, (v.z + v.y * 0.5f) / 5f));

            for (int seg = 0; seg < Segments; seg++)
            {
                int next = (seg + 1) % Segments;
                tris.Add(0); tris.Add(1 + next); tris.Add(1 + seg);
            }
            for (int ring = 1; ring < rings; ring++)
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
            mesh.name = "IoV_IsletGround";
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            GameObject ground = new GameObject("IoV_Ground");
            ground.layer = LayerMask.NameToLayer("static_solid");
            ground.transform.SetParent(root.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = ground.AddComponent<MeshRenderer>();
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;

            Material material = null;
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
            return material;
        }

        // ---- static copies of vanilla prefabs ----

        static GameObject Place(string[] candidates, Vector3 pos, Vector3 euler, float size, float sink)
        {
            foreach (string name in candidates)
            {
                GameObject prefab = PrefabManager.Instance.GetPrefab(name);
                if (prefab == null) continue;
                return Place(prefab, pos, euler, size, sink);
            }
            missing++;
            Plugin.Debug("None of these prefabs exist: " + string.Join(", ", candidates));
            return null;
        }

        static GameObject Place(string name, Vector3 pos, Vector3 euler)
        {
            return Place(new string[] { name }, pos, euler, 0f, 0f);
        }

        // Copies a prefab as plain meshes and colliders. size > 0 scales its largest horizontal extent to
        // that many metres. The copy's bottom is put at pos.y, sunk by sink times its height.
        static GameObject Place(GameObject prefab, Vector3 pos, Vector3 euler, float size, float sink)
        {
            GameObject go = Object.Instantiate(prefab, root.transform, false);
            go.name = prefab.name;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localPosition = Vector3.zero;

            WearNTear wear = go.GetComponent<WearNTear>();
            if (wear != null && wear.m_worn != null && wear.m_new != null && wear.m_worn != wear.m_new)
            {
                wear.m_new.SetActive(false);
                wear.m_worn.SetActive(true);
            }
            Strip(go);

            Bounds b = LocalBounds(go);
            if (size > 0f && b.size.x > 0.01f)
            {
                float scale = size / Mathf.Max(b.size.x, b.size.z);
                go.transform.localScale = Vector3.one * scale;
                b = LocalBounds(go);
            }
            go.transform.localPosition = new Vector3(pos.x, pos.y - b.min.y - sink * b.size.y, pos.z) - new Vector3(b.center.x, 0f, b.center.z);
            built++;
            Plugin.Debug("  " + prefab.name + " size " + b.size.ToString("F1"));
            return go;
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

        static Vector3 OnGround(float x, float z)
        {
            return new Vector3(x, Height(x, z), z);
        }

        static Vector3 Polar(float degrees, float radius)
        {
            float a = degrees * Mathf.Deg2Rad;
            return OnGround(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        // ---- layout ----

        static void PlaceRocks()
        {
            // Big rocks on the shore give the islet its outline; one is half on the plateau as a back wall.
            float[] angles = { 15f, 95f, 205f, 290f };
            float[] sizes = { 7f, 5.5f, 8f, 6f };
            for (int i = 0; i < angles.Length; i++)
            {
                float a = angles[i] * Mathf.Deg2Rad;
                float r = Shore(a) - 1.5f;
                Place(bigRocks, Polar(angles[i], r), new Vector3(0f, angles[i] * 3f, 0f), sizes[i], 0.35f);
            }
            Place(bigRocks, Polar(140f, 9f), new Vector3(0f, 60f, 0f), 5f, 0.3f);
            Place(smallRocks, Polar(250f, 11f), new Vector3(0f, 10f, 0f), 2f, 0.3f);
            Place(smallRocks, Polar(330f, 12.5f), new Vector3(0f, 200f, 0f), 2.5f, 0.3f);
            Place(smallRocks, Polar(60f, 13f), new Vector3(0f, 120f, 0f), 1.6f, 0.3f);
        }

        static void PlaceRuin()
        {
            float floor = PlateauHeight - 0.15f;
            // A 6x6 m stone courtyard with two tiles missing.
            for (int ix = -1; ix <= 1; ix++)
                for (int iz = -1; iz <= 1; iz++)
                {
                    if ((ix == 1 && iz == 1) || (ix == -1 && iz == 0)) continue;
                    Place("stone_floor_2x2", new Vector3(ix * 2f, floor, iz * 2f), Vector3.zero);
                }
            // Broken walls on the north and west side.
            Place("stone_wall_4x2", new Vector3(-1f, floor, 3f), Vector3.zero);
            Place("stone_wall_2x1", new Vector3(2f, floor, 3f), Vector3.zero);
            Place("stone_wall_2x1", new Vector3(-3f, floor, 1f), new Vector3(0f, 90f, 0f));
            Place("stone_wall_1x1", new Vector3(-3f, floor, -1.5f), new Vector3(0f, 90f, 0f));
            // An arch as the way in from the south, pillars on the corners, one fallen over.
            Place("stone_arch", new Vector3(0f, floor, -3f), Vector3.zero);
            Place("stone_pillar", new Vector3(3f, floor, -3f), Vector3.zero);
            Place("stone_pillar", new Vector3(-3f, floor, -3f), Vector3.zero);
            Place("stone_pillar", new Vector3(3.5f, floor, 0.5f), new Vector3(0f, 25f, 90f));
        }

        static void PlacePlants()
        {
            Place(bushes, Polar(60f, 9.5f), new Vector3(0f, 30f, 0f), 0f, 0.1f);
            Place(bushes, Polar(235f, 10f), new Vector3(0f, 200f, 0f), 0f, 0.1f);
            Place(bushes, Polar(170f, 11f), new Vector3(0f, 90f, 0f), 0f, 0.1f);
            Place(trees, Polar(320f, 9f), new Vector3(0f, 45f, 0f), 0f, 0.05f);
        }

        static void PlaceSpawnersAndChest()
        {
            float y = PlateauHeight + 0.3f;
            PlaceNetworked(IsletPrefabs.GreydwarfSpawner, new Vector3(1.5f, y, 0.5f), 0f);
            PlaceNetworked(IsletPrefabs.GreydwarfSpawner, new Vector3(-1f, y, -1.5f), 0f);
            Vector3 outside = Polar(300f, 10f);
            PlaceNetworked(IsletPrefabs.GreydwarfSpawner, outside + Vector3.up * 0.3f, 0f);
            PlaceNetworked(IsletPrefabs.ShamanSpawner, new Vector3(0f, y, 1.5f), 0f);
            PlaceNetworked(IsletPrefabs.Chest, new Vector3(-1.8f, PlateauHeight, 2.1f), 180f);
        }

        static void PlaceNetworked(string name, Vector3 pos, float rotY)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            if (prefab == null)
            {
                missing++;
                Plugin.Debug("Own prefab " + name + " missing");
                return;
            }
            GameObject go = Object.Instantiate(prefab, root.transform, false);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            built++;
        }

        static string Describe(Material m)
        {
            if (m == null) return "none";
            return m.name + " (shader " + (m.shader != null ? m.shader.name : "?") + ")";
        }
    }
}
