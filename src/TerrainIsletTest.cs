using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // RESEARCH PROTOTYPE (branch iov-terrain-test, not for release): an islet of the game's own terrain
    // instead of our mesh, so the hoe, the pickaxe and the cultivator work on it.
    //
    // Round 1 used the game's own TerrainModifier discs (level + smooth) in a location: the game builds the
    // ground under a location's modifiers each time it makes a zone's heightmap, with no ±8 m limit (that
    // limit is only for the players' edits, relative to the ground built this way). It worked (hoe,
    // building, boats), but the game's smoothing falls off steeply at the rim, so the islet stood on a
    // stepped wall. (The idea of levelling locations past the 8 m limit is from Shuiei's Valheim World
    // Editor, MIT, github.com/Shuiei/valheim-world-editor; none of its code is used.)
    //
    // Round 2 (this): the islet's own ground shape, written into each heightmap the islet touches before
    // the game's modifiers and the players' edits go on (Heightmap.ApplyModifiers prefix): a gently rolling
    // top, a beach that runs down into the water, a shallow shelf and a slope to the sea floor, with an
    // uneven coast. Every client builds it the same from the same location (the mod is on every client
    // anyway). It never digs: only where it's higher than the sea floor. And plants on it grow: the islet
    // stays Ocean to the game, where no crop grows, so a plant standing on one counts Ocean as its own.
    //
    // The test islet isn't placed by world generation (quantity 0, and its name doesn't start with "IoV_",
    // so the existing-world pass leaves it alone): `iov_goto terraintest` (devcommands) places one in deep
    // water near the world's centre the first time, and takes you there.
    static class TerrainIsletTest
    {
        internal const string Name = "IoVTest_TerrainIslet";
        internal const float Top = 3.2f;

        internal static void Register()
        {
            GameObject root = ZoneManager.Instance.CreateLocationContainer(Name);
            GameObject ground = new GameObject("IoVTest_IsletGround");
            ground.transform.SetParent(root.transform, false);
            IsletGround shape = ground.AddComponent<IsletGround>();
            shape.radius = 30f;
            shape.top = Top;
            shape.beach = 9f;
            shape.seed = 17.3f;
            Location location = root.AddComponent<Location>();
            location.m_applyRandomDamage = false;
            location.m_noBuild = false;

            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.Quantity = 0; // never placed by world generation
            config.SnapToWater = true;
            config.ClearArea = false;
            config.ExteriorRadius = 45f;
            config.Group = Name;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered research location " + Name + " (real-terrain islet test)");
        }

        static readonly System.Reflection.MethodInfo registerLocation = AccessTools.Method(typeof(ZoneSystem), "RegisterLocation");
        static readonly System.Reflection.MethodInfo spawnLocation = AccessTools.Method(typeof(ZoneSystem), "SpawnLocation");
        static readonly System.Reflection.FieldInfo generatedZones = AccessTools.Field(typeof(ZoneSystem), "m_generatedZones");

        // Takes you to the test islet, placing it first if the world has none.
        internal static void Goto(Terminal context)
        {
            Goto(context, Name, Top, 70f, 300f);
        }

        // The same for any test location: placed in open water (clear for reach metres round) at least
        // from metres out from the world's centre, the first time.
        internal static void Goto(Terminal context, string name, float top, float reach, float from)
        {
            ZoneSystem zones = ZoneSystem.instance;
            Player player = Player.m_localPlayer;
            if (zones == null || player == null || WorldGenerator.instance == null) return;
            Vector3 spot = Vector3.zero;
            bool have = false;
            foreach (ZoneSystem.LocationInstance instance in zones.GetLocationList())
                if (instance.m_location != null && instance.m_location.m_prefabName == name) { spot = instance.m_position; have = true; break; }
            if (!have)
            {
                ZoneSystem.ZoneLocation location = null;
                foreach (ZoneSystem.ZoneLocation l in zones.m_locations)
                    if (l != null && l.m_prefabName == name) { location = l; break; }
                if (location == null || registerLocation == null || spawnLocation == null) { context.AddString("The test location " + name + " isn't registered."); return; }
                if (!FindSpot(out spot, reach, from)) { context.AddString("No open, deep water found near the centre of the world."); return; }
                spot.y = zones.m_waterLevel;
                Vector2s zone = ZoneSystem.GetZone(spot);
                HashSet<Vector2s> generated = generatedZones != null ? generatedZones.GetValue(zones) as HashSet<Vector2s> : null;
                bool built = generated != null && generated.Contains(zone);
                registerLocation.Invoke(zones, new object[] { location, spot, built });
                if (built)
                    // Turned, as world generation turns locations, so the ground is seen to turn with it.
                    spawnLocation.Invoke(zones, new object[] { location, 1234, spot, Quaternion.Euler(0f, 137f, 0f), ZoneSystem.SpawnMode.Full, new List<GameObject>(), false });
                Plugin.Log.LogInfo("Test location " + name + " placed at " + spot.ToString("F0") + (built ? " (zone already there: built now)" : " (built when the zone is)"));
            }
            Vector3 target = new Vector3(spot.x, zones.m_waterLevel + top + 4f, spot.z);
            player.TeleportTo(target, player.transform.rotation, true);
            context.AddString("Teleporting to the test location " + name + " at " + spot.x.ToString("0", CultureInfo.InvariantCulture) + ", " + spot.z.ToString("0", CultureInfo.InvariantCulture) + ".");
        }

        // The open sea closest to the world's centre: a spot in the Ocean biome at least 10 m deep, with deep
        // ocean all round it for the islet's whole spread, searched outward ring by ring. (A world that
        // already had the round-1 test keeps that spot: the location's new ground replaces the old.)
        static bool FindSpot(out Vector3 spot, float reach, float from)
        {
            float water = ZoneSystem.instance.m_waterLevel;
            // Well away from the other test locations already placed, and from any location at all.
            List<Vector3> taken = new List<Vector3>();
            foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.GetLocationList()) taken.Add(instance.m_position);
            for (float r = from; r < 10000f; r += 64f)
            {
                int steps = Mathf.Max(16, Mathf.RoundToInt(r * 2f * Mathf.PI / 64f));
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (!DeepOcean(p, water, 10f)) continue;
                    bool clear = true;
                    foreach (Vector3 t in taken)
                        if (Utils.DistanceXZ(t, p) < reach + 60f) { clear = false; break; }
                    if (!clear) continue;
                    bool open = true;
                    for (int k = 0; k < 12 && open; k++)
                    {
                        float b = k * Mathf.PI * 2f / 12f;
                        open = DeepOcean(p + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * reach, water, 4f);
                    }
                    if (!open) continue;
                    spot = p;
                    return true;
                }
            }
            spot = Vector3.zero;
            return false;
        }

        static bool DeepOcean(Vector3 p, float water, float depth)
        {
            WorldGenerator gen = WorldGenerator.instance;
            if (gen.GetBiome(p) != Heightmap.Biome.Ocean) return false;
            return gen.GetHeight(p.x, p.z) < water - depth;
        }

        // ---- the islet's ground, written into the heightmaps ----

        [HarmonyPatch(typeof(Heightmap), "ApplyModifiers")]
        static class GroundPatch
        {
            static void Prefix(Heightmap __instance)
            {
                if (IsletGround.Loaded.Count == 0) return;
                foreach (IsletGround ground in IsletGround.Loaded)
                    if (ground != null) ground.Apply(__instance);
            }
        }

        // A plant on an islet grows as on land: the islet is Ocean to the game, where no crop grows.
        [HarmonyPatch(typeof(Plant), "UpdateHealth")]
        static class PlantPatch
        {
            static void Prefix(Plant __instance)
            {
                if ((__instance.m_biome & Heightmap.Biome.Ocean) != 0 || !IsletGround.Covers(__instance.transform.position)) return;
                __instance.m_biome |= Heightmap.Biome.Ocean;
            }
        }
    }

    // An islet's ground shape (on its location; every client has it while the zone is loaded). Its numbers
    // are serialized, so the location's copies keep them.
    class IsletGround : MonoBehaviour
    {
        [SerializeField] internal float radius = 30f; // where the coast is, on average
        [SerializeField] internal float top = 3f;     // how high its top is over the water
        [SerializeField] internal float beach = 8f;   // how far in from the coast the top begins
        [SerializeField] internal float seed = 0f;    // the coast's and the top's unevenness

        const float Shelf = 7f;   // shallow water off the beach
        const float Slope = 22f;  // from the shelf down to the sea floor
        const float ShelfDepth = 2.5f;

        internal static readonly List<IsletGround> Loaded = new List<IsletGround>();
        static readonly List<Heightmap> near = new List<Heightmap>();

        float Reach { get { return radius * 1.2f + Shelf + Slope; } }

        void OnEnable()
        {
            Loaded.Add(this);
            Poke();
        }

        void OnDisable()
        {
            Loaded.Remove(this);
            Poke();
        }

        void Poke()
        {
            near.Clear();
            Heightmap.FindHeightmap(transform.position, Reach + 4f, near);
            foreach (Heightmap h in near) if (h != null) h.Poke(0, false);
        }

        internal static bool Covers(Vector3 p)
        {
            foreach (IsletGround g in Loaded)
                if (g != null && Utils.DistanceXZ(g.transform.position, p) < g.radius * 1.2f) return true;
            return false;
        }

        // The coast's distance from the middle in a direction: the average, give or take a little, so the
        // islet isn't a circle.
        float Coast(float angle)
        {
            float n = Mathf.PerlinNoise(Mathf.Cos(angle) * 1.3f + seed, Mathf.Sin(angle) * 1.3f + seed * 0.7f) - 0.5f;
            float m = Mathf.PerlinNoise(Mathf.Cos(angle) * 3.1f + seed * 1.9f, Mathf.Sin(angle) * 3.1f - seed) - 0.5f;
            return radius * (1f + n * 0.45f + m * 0.15f);
        }

        // How high the islet's ground is at a point (over the world's zero), and how much it counts there:
        // fully on the islet, the beach and the shallows, fading out down the slope to the sea floor (so it
        // meets the sea floor wherever that is, with no step). Zero weight outside it.
        float Height(float x, float z, float water, out float weight)
        {
            weight = 0f;
            Vector3 c = transform.position;
            float dx = x - c.x, dz = z - c.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d > Reach) return 0f;
            float coast = Coast(Mathf.Atan2(dz, dx));
            weight = 1f;
            float rolling = (Mathf.PerlinNoise(x * 0.06f + seed, z * 0.06f - seed) - 0.5f) * 1.2f; // a gently uneven top
            if (d <= coast - beach) return water + top + rolling * Mathf.Clamp01((coast - beach - d) / 6f);
            if (d <= coast) // the beach: from the top down to just over the water
                return Mathf.Lerp(water + 0.25f, water + top, Smooth((coast - d) / beach));
            if (d <= coast + Shelf) // the shallows
                return Mathf.Lerp(water - ShelfDepth, water + 0.25f, Smooth((coast + Shelf - d) / Shelf));
            weight = Smooth((coast + Shelf + Slope - d) / Slope); // down to the sea floor
            return water - ShelfDepth;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // Raises this heightmap's ground to the islet's where the islet's is higher (never digs).
        internal void Apply(Heightmap map)
        {
            Vector3 origin = map.transform.position;
            int width = map.m_width;
            float scale = map.m_scale;
            float half = width * scale * 0.5f;
            Vector3 c = transform.position;
            float reach = Reach;
            if (Mathf.Abs(c.x - origin.x) > half + reach || Mathf.Abs(c.z - origin.z) > half + reach) return;
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            int raised = 0;
            for (int y = 0; y <= width; y++)
            {
                float wz = origin.z + (y - width / 2) * scale;
                for (int x = 0; x <= width; x++)
                {
                    float wx = origin.x + (x - width / 2) * scale;
                    float weight;
                    float want = Height(wx, wz, water, out weight);
                    if (weight <= 0f) continue;
                    float have = map.GetHeight(x, y);
                    if (want <= have) continue;
                    map.SetHeight(x, y, Mathf.Lerp(have, want, weight));
                    raised++;
                }
            }
            if (raised > 0) Plugin.Debug("Islet ground: raised " + raised + " points of the heightmap at " + origin.ToString("F0"));
        }
    }
}
