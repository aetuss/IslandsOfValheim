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
    // instead of our mesh. The game levels the ground under a location's TerrainModifiers whenever it builds
    // a zone's heightmap, before the players' own edits (TerrainComp) go on top, and with no ±8 m limit:
    // that limit is only for the players' edits, relative to the ground built this way. So a location of
    // nothing but a few stacked level+smooth discs lifts the sea floor into an island, the same on every
    // client (each builds it from the same location), and the hoe, the pickaxe and the cultivator work on it
    // within ±8 m of it, as on any land.
    //
    // The idea of using levelling locations to get past the ±8 m limit is from Shuiei's Valheim World
    // Editor (MIT, github.com/Shuiei/valheim-world-editor), which saves big edits as invisible "ground
    // disc" locations; no code of it is used here.
    //
    // The test islet isn't placed by world generation (quantity 0, and its name doesn't start with "IoV_",
    // so the existing-world pass leaves it alone): `iov_goto terraintest` (devcommands) places one in deep
    // water near the world's centre the first time, and takes you there.
    static class TerrainIsletTest
    {
        internal const string Name = "IoVTest_TerrainIslet";

        // The discs, widest and lowest first (the game applies them in this order, m_sortOrder): a beach
        // just over the water, a shoulder, a top; each smooths out further than it levels, which makes the
        // slopes. Heights are over the water, which is where the location stands.
        struct Disc
        {
            internal float Height, Level, Smooth, Power;
            internal Disc(float height, float level, float smooth, float power) { Height = height; Level = level; Smooth = smooth; Power = power; }
        }

        static readonly Disc[] Discs =
        {
            new Disc(0.6f, 24f, 36f, 3f),
            new Disc(2.2f, 16f, 23f, 3f),
            new Disc(3.6f, 9f, 14f, 3f),
        };

        static readonly float Top = 3.6f;

        internal static void Register()
        {
            GameObject root = ZoneManager.Instance.CreateLocationContainer(Name);
            for (int i = 0; i < Discs.Length; i++)
            {
                Disc d = Discs[i];
                GameObject disc = new GameObject("IoVTest_GroundDisc" + i);
                disc.transform.SetParent(root.transform, false);
                disc.transform.localPosition = new Vector3(0f, d.Height, 0f);
                TerrainModifier modifier = disc.AddComponent<TerrainModifier>();
                modifier.m_sortOrder = i;
                modifier.m_level = true;
                modifier.m_levelRadius = d.Level;
                modifier.m_levelOffset = 0f;
                modifier.m_square = false;
                modifier.m_smooth = true;
                modifier.m_smoothRadius = d.Smooth;
                modifier.m_smoothPower = d.Power;
                modifier.m_paintCleared = false; // no dirt: the ground keeps what the terrain shader gives it
                modifier.m_playerModifiction = false;
                modifier.m_useTerrainCompiler = false;
            }
            Location location = root.AddComponent<Location>();
            location.m_applyRandomDamage = false;
            location.m_noBuild = false;

            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.Quantity = 0; // never placed by world generation
            config.SnapToWater = true;
            config.ClearArea = false;
            config.ExteriorRadius = 40f;
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
            ZoneSystem zones = ZoneSystem.instance;
            Player player = Player.m_localPlayer;
            if (zones == null || player == null || WorldGenerator.instance == null) return;
            Vector3 spot = Vector3.zero;
            bool have = false;
            foreach (ZoneSystem.LocationInstance instance in zones.GetLocationList())
                if (instance.m_location != null && instance.m_location.m_prefabName == Name) { spot = instance.m_position; have = true; break; }
            if (!have)
            {
                ZoneSystem.ZoneLocation location = null;
                foreach (ZoneSystem.ZoneLocation l in zones.m_locations)
                    if (l != null && l.m_prefabName == Name) { location = l; break; }
                if (location == null || registerLocation == null || spawnLocation == null) { context.AddString("The terrain islet test isn't registered."); return; }
                if (!FindSpot(out spot)) { context.AddString("No open, deep water found near the centre of the world."); return; }
                spot.y = zones.m_waterLevel;
                Vector2s zone = ZoneSystem.GetZone(spot);
                HashSet<Vector2s> generated = generatedZones != null ? generatedZones.GetValue(zones) as HashSet<Vector2s> : null;
                bool built = generated != null && generated.Contains(zone);
                registerLocation.Invoke(zones, new object[] { location, spot, built });
                if (built)
                    spawnLocation.Invoke(zones, new object[] { location, 1234, spot, Quaternion.identity, ZoneSystem.SpawnMode.Full, new List<GameObject>(), false });
                Plugin.Log.LogInfo("Terrain islet test placed at " + spot.ToString("F0") + (built ? " (zone already there: built now)" : " (built when the zone is)"));
            }
            Vector3 target = new Vector3(spot.x, zones.m_waterLevel + Top + 4f, spot.z);
            player.TeleportTo(target, player.transform.rotation, true);
            context.AddString("Teleporting to the terrain islet test at " + spot.x.ToString("0", CultureInfo.InvariantCulture) + ", " + spot.z.ToString("0", CultureInfo.InvariantCulture) + ".");
        }

        // The open sea closest to the world's centre: a spot in the Ocean biome at least 10 m deep, with deep
        // ocean all round it for the islet's whole spread, searched outward ring by ring.
        static bool FindSpot(out Vector3 spot)
        {
            float water = ZoneSystem.instance.m_waterLevel;
            for (float r = 300f; r < 6000f; r += 64f)
            {
                int steps = Mathf.Max(16, Mathf.RoundToInt(r * 2f * Mathf.PI / 64f));
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (!DeepOcean(p, water, 10f)) continue;
                    bool open = true;
                    for (int k = 0; k < 12 && open; k++)
                    {
                        float b = k * Mathf.PI * 2f / 12f;
                        open = DeepOcean(p + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * 60f, water, 4f);
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
    }
}
