using System.Collections.Generic;
using System.Text;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // A vanilla dungeon on an islet: its location is cloned (entrance, interior with its DungeonGenerator,
    // the teleports between them), its parts are moved onto our islet's plateau, and the location's own
    // interior settings are copied onto ours. The game then builds the interior the vanilla way when the
    // islet is placed (ZoneSystem.SpawnLocation generates any DungeonGenerator among a location's parts).
    //
    // For now (1.7.0 in progress) this is a feasibility test: one test islet per dungeon close to the world
    // centre, reached with iov_goto dungeon1..4.
    static partial class IsletBuilder
    {
        internal static readonly string[] DungeonTests = { "IoV_DungeonTest_Crypt", "IoV_DungeonTest_Sunken", "IoV_DungeonTest_Mine", "IoV_DungeonTest_DeepNorth" };

        // Which vanilla location for each test (the first that exists; or one whose name has the hint), on
        // which tier's ground.
        static readonly string[][] DungeonSources =
        {
            new[] { "Crypt4", "Crypt3", "Crypt2" },
            new[] { "SunkenCrypt4", "SunkenCrypt3", "SunkenCrypt2", "SunkenCrypt1" },
            new[] { "Mistlands_DvergrTownEntrance1", "Mistlands_DvergrTownEntrance2" },
            new[] { "*Morkhalla", "*MorkHalla", "*DeepNorth_Dungeon", "*Morgen" },
        };
        static readonly int[] DungeonTiers = { 0, 1, 3, 6 };

        internal static void RegisterDungeonTests()
        {
            LogVanillaLocations();
            for (int i = 0; i < DungeonTests.Length; i++)
            {
                string source = FindLocation(DungeonSources[i]);
                if (source == null) { Plugin.Log.LogWarning("Dungeon test " + DungeonTests[i] + ": none of " + string.Join(", ", DungeonSources[i]) + " found"); continue; }
                try { DungeonIslet(DungeonTests[i], source, IsletTier.All[DungeonTiers[i]]); }
                catch (System.Exception e) { Plugin.Log.LogError("Dungeon test " + DungeonTests[i] + " from " + source + " failed: " + e); }
            }
        }

        // Every vanilla location name, once, so the right dungeon names can be found (debug log only).
        static void LogVanillaLocations()
        {
            if (!Plugin.DebugLog.Value || ZoneSystem.instance == null) return;
            StringBuilder sb = new StringBuilder("Vanilla locations:");
            foreach (ZoneSystem.ZoneLocation location in ZoneSystem.instance.m_locations)
                if (location != null && !string.IsNullOrEmpty(location.m_prefabName)) sb.Append(' ').Append(location.m_prefabName);
            Plugin.Log.LogInfo(sb.ToString());
        }

        static string FindLocation(string[] candidates)
        {
            if (ZoneSystem.instance == null) return null;
            foreach (string candidate in candidates)
                foreach (ZoneSystem.ZoneLocation location in ZoneSystem.instance.m_locations)
                {
                    if (location == null || string.IsNullOrEmpty(location.m_prefabName)) continue;
                    bool hint = candidate.StartsWith("*");
                    string name = hint ? candidate.Substring(1) : candidate;
                    if (hint ? location.m_prefabName.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0 : location.m_prefabName == name)
                        return location.m_prefabName;
                }
            return null;
        }

        static void DungeonIslet(string name, string source, IsletTier kind)
        {
            CustomLocation clone = ZoneManager.Instance.CreateClonedLocation(name + "_Source", source);
            if (clone == null || clone.Prefab == null) { Plugin.Log.LogWarning("Dungeon test " + name + ": couldn't clone " + source); return; }
            GameObject vanilla = clone.Prefab;
            Location vanillaLocation = vanilla.GetComponent<Location>();
            Plugin.Debug("Dungeon test " + name + " from " + source + ":\n" + DescribeTree(vanilla.transform, 0, 3));

            BeginStory(name, kind, 0, 2000f + name.GetHashCode() % 500);
            // The vanilla parts, raised onto the plateau. Terrain changes are left out: here they'd dig into
            // or raise the sea floor under the islet.
            List<Transform> parts = new List<Transform>();
            foreach (Transform child in vanilla.transform) parts.Add(child);
            foreach (Transform part in parts)
            {
                part.SetParent(root.transform, false);
                part.localPosition += new Vector3(0f, PlateauHeight - 0.05f, 0f);
            }
            int removed = 0;
            foreach (TerrainModifier modifier in root.GetComponentsInChildren<TerrainModifier>(true)) { Object.DestroyImmediate(modifier.gameObject); removed++; }
            foreach (TerrainOp op in root.GetComponentsInChildren<TerrainOp>(true)) { Object.DestroyImmediate(op.gameObject); removed++; }
            DungeonGenerator generator = root.GetComponentInChildren<DungeonGenerator>(true);
            Plugin.Debug("Dungeon test " + name + ": " + parts.Count + " parts moved, " + removed + " terrain changes left out, generator " +
                (generator != null ? generator.name + " at " + generator.transform.localPosition.ToString("F0") : "none"));

            PlaceDock(cliffDeg + 229f);
            PlacePlants();

            Location location = root.AddComponent<Location>();
            location.m_applyRandomDamage = false;
            if (vanillaLocation != null)
            {
                location.m_exteriorRadius = Mathf.Max(vanillaLocation.m_exteriorRadius, 30f);
                location.m_hasInterior = vanillaLocation.m_hasInterior;
                location.m_interiorRadius = vanillaLocation.m_interiorRadius;
                location.m_interiorEnvironment = vanillaLocation.m_interiorEnvironment;
                location.m_interiorTransform = vanillaLocation.m_interiorTransform;
                location.m_useCustomInteriorTransform = vanillaLocation.m_useCustomInteriorTransform;
                location.m_generator = vanillaLocation.m_generator != null ? vanillaLocation.m_generator : generator;
                location.m_interiorPrefab = vanillaLocation.m_interiorPrefab;
                location.m_noBuild = vanillaLocation.m_noBuild;
                Plugin.Debug("Dungeon test " + name + ": interior " + location.m_hasInterior + ", radius " + location.m_interiorRadius + ", environment " + location.m_interiorEnvironment);
            }
            Object.DestroyImmediate(vanilla);

            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.BiomeArea = Heightmap.BiomeArea.Median;
            config.Quantity = 2;
            config.Priotized = true;
            config.SnapToWater = true;
            config.RandomRotation = true;
            config.ClearArea = false;
            config.ExteriorRadius = 30f;
            config.MinAltitude = -1000f;
            config.MaxAltitude = -6f;
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 1000f;
            config.MinDistance = 300f;
            config.MaxDistance = 2500f;
            config.Group = name;
            config.MinDistanceFromSimilar = 500f;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered dungeon test " + name + " from " + source);
        }

        static string DescribeTree(Transform t, int depth, int maxDepth)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(' ', depth * 2).Append(t.name).Append(" @").Append(t.localPosition.ToString("F0")).Append(':');
            foreach (Component c in t.GetComponents<Component>())
                if (c != null && !(c is Transform)) sb.Append(' ').Append(c.GetType().Name);
            sb.Append('\n');
            if (depth < maxDepth) foreach (Transform child in t) sb.Append(DescribeTree(child, depth + 1, maxDepth));
            return sb.ToString();
        }
    }
}
