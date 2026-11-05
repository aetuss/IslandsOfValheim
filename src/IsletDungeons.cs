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
            new[] { "MorkBorg", "TheDarkestHole", "TheHole01" },
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
            BeginStory(name, kind, 0, 2000f + name.GetHashCode() % 500);
            DungeonInterior interior = AddDungeon(source, Vector2.zero, 0f, name);
            PlaceDock(cliffDeg + 229f);
            PlacePlants();
            Location location = root.AddComponent<Location>();
            location.m_applyRandomDamage = false;
            if (interior != null) interior.CopyTo(location);

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

        // What a vanilla dungeon location says about its interior, for the islet's own Location.
        internal class DungeonInterior
        {
            internal bool HasInterior, UseCustomTransform, NoBuild;
            internal float Radius, ExteriorRadius;
            internal float Footprint; // how far its entrance's parts reach from its middle, on the islet
            internal float DoorDeg = float.NaN; // which way its way in opens, in the islet's degrees (as Polar)
            internal string Environment;
            internal Transform InteriorTransform;
            internal DungeonGenerator Generator;
            internal GameObject InteriorPrefab;

            internal void CopyTo(Location location)
            {
                location.m_exteriorRadius = Mathf.Max(location.m_exteriorRadius, ExteriorRadius);
                location.m_hasInterior = HasInterior;
                location.m_interiorRadius = Radius;
                location.m_interiorEnvironment = Environment;
                location.m_interiorTransform = InteriorTransform;
                location.m_useCustomInteriorTransform = UseCustomTransform;
                location.m_generator = Generator;
                location.m_interiorPrefab = InteriorPrefab;
            }
        }

        // A vanilla dungeon location's parts (entrance, interior with its generator, the teleports between
        // them) moved onto the islet being built, its middle at (at.x, at.y) on the plateau, turned rotY.
        // Its terrain changes are left out (here they'd dig into or raise the sea floor). Null if the vanilla
        // location can't be had.
        static DungeonInterior AddDungeon(string source, Vector2 at, float rotY, string tag)
        {
            CustomLocation clone = ZoneManager.Instance.CreateClonedLocation(tag + "_Source", source);
            if (clone == null || clone.Prefab == null) { Plugin.Log.LogWarning(tag + ": couldn't clone the vanilla location " + source); return null; }
            GameObject vanilla = clone.Prefab;
            Location vanillaLocation = vanilla.GetComponent<Location>();
            Plugin.Debug(tag + ": dungeon from " + source + ":\n" + DescribeTree(vanilla.transform, 0, 3));

            GameObject group = new GameObject("IoV_Dungeon");
            group.transform.SetParent(parent, false);
            group.transform.localPosition = new Vector3(at.x, PlateauHeight - 0.05f, at.y);
            group.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            List<Transform> parts = new List<Transform>();
            foreach (Transform child in vanilla.transform) parts.Add(child);
            foreach (Transform part in parts) part.SetParent(group.transform, false);
            int removed = 0;
            foreach (TerrainModifier modifier in group.GetComponentsInChildren<TerrainModifier>(true)) { Object.DestroyImmediate(modifier.gameObject); removed++; }
            foreach (TerrainOp op in group.GetComponentsInChildren<TerrainOp>(true)) { Object.DestroyImmediate(op.gameObject); removed++; }
            // An infested mine's way in is at the bottom of a pit the terrain is dug out for; the islet can't
            // be dug, and the pit would reach below the sea. Its steps and shaft go, and the gateway comes up
            // to the fort's floor.
            if (source.StartsWith("Mistlands_DvergrTown"))
            {
                foreach (Transform t in group.GetComponentsInChildren<Transform>(true))
                    if (t.parent != null && t.parent.name == "fort" && (t.name.StartsWith("stair") || t.name == "mine"))
                    {
                        t.gameObject.SetActive(false);
                        Plugin.Debug("  " + tag + ": " + t.name + " (below ground) left out");
                    }
                Transform gateway = group.transform.Find("Gateway");
                if (gateway != null)
                {
                    gateway.localPosition = new Vector3(gateway.localPosition.x, 0.1f, gateway.localPosition.z);
                    // The dark doorway (the gateway's cube, 3 m behind it) framed by a black marble arch on
                    // two columns, like the dvergr's own gates.
                    Vector3 door = root.transform.InverseTransformPoint(gateway.TransformPoint(new Vector3(0f, 0f, -3f)));
                    Vector3 side = Quaternion.Euler(0f, rotY, 0f) * gateway.localRotation * Vector3.right;
                    Vector3 turn = new Vector3(0f, rotY + gateway.localEulerAngles.y, 0f);
                    float g = PlateauHeight - 0.05f;
                    Vector3 left = door - side * 1.3f, right = door + side * 1.3f;
                    float l = Top(At("blackmarble_column_1", new Vector3(left.x, g, left.z), turn, false), g + 3f);
                    float r = Top(At("blackmarble_column_1", new Vector3(right.x, g, right.z), turn, false), g + 3f);
                    At("blackmarble_arch", new Vector3(door.x, Mathf.Min(l, r), door.z), turn, true);
                }
            }
            // Spawners meant for terrain at the location's own height would sit inside the islet's ground.
            foreach (CreatureSpawner spawner in group.GetComponentsInChildren<CreatureSpawner>(true))
            {
                Vector3 p = root.transform.InverseTransformPoint(spawner.transform.position);
                if (p.y > 1000f || p.y >= PlateauHeight + 0.2f) continue; // the interior's are far up
                spawner.transform.position = root.transform.TransformPoint(new Vector3(p.x, PlateauHeight + 0.3f, p.z));
                Plugin.Debug("  " + tag + ": spawner " + spawner.name + " raised to the ground");
            }
            DungeonGenerator generator = group.GetComponentInChildren<DungeonGenerator>(true);
            Plugin.Debug(tag + ": " + parts.Count + " parts moved, " + removed + " terrain changes left out, generator " +
                (generator != null ? generator.name + " at " + generator.transform.localPosition.ToString("F0") : "none"));

            DungeonInterior interior = new DungeonInterior();
            // How far the entrance reaches (its meshes, not the interior far up), so nothing else is built on it.
            foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !ActiveBelow(filter.transform, group.transform)) continue;
                Bounds mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = group.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (p.y > 500f) continue;
                    interior.Footprint = Mathf.Max(interior.Footprint, new Vector2(p.x, p.z).magnitude);
                }
            }
            // The way in: the teleport outside (not the one in the interior far up) faces where you come out.
            foreach (Teleport door in group.GetComponentsInChildren<Teleport>(true))
            {
                Vector3 p = root.transform.InverseTransformPoint(door.transform.position);
                if (p.y > 500f) continue;
                Vector3 forward = root.transform.InverseTransformDirection(door.transform.forward);
                interior.DoorDeg = Mathf.Atan2(forward.z, forward.x) * Mathf.Rad2Deg;
            }
            Plugin.Debug(tag + ": the entrance reaches " + interior.Footprint.ToString("F1") + " m from its middle, its way in faces " +
                (float.IsNaN(interior.DoorDeg) ? "?" : interior.DoorDeg.ToString("0")) + " degrees");
            if (vanillaLocation != null)
            {
                interior.ExteriorRadius = vanillaLocation.m_exteriorRadius;
                interior.HasInterior = vanillaLocation.m_hasInterior;
                interior.Radius = vanillaLocation.m_interiorRadius;
                interior.Environment = vanillaLocation.m_interiorEnvironment;
                interior.InteriorTransform = vanillaLocation.m_interiorTransform;
                interior.UseCustomTransform = vanillaLocation.m_useCustomInteriorTransform;
                interior.Generator = vanillaLocation.m_generator != null ? vanillaLocation.m_generator : generator;
                interior.InteriorPrefab = vanillaLocation.m_interiorPrefab;
                interior.NoBuild = vanillaLocation.m_noBuild;
            }
            built++;
            // The emptied clone is known to Jötunn: it goes from there too, or Jötunn tries to load it.
            ZoneManager.Instance.DestroyCustomLocation(clone.Name);
            if (vanilla != null) Object.DestroyImmediate(vanilla);
            return interior;
        }
        // The game puts whoever comes out of a dungeon a metre below the exit's gateway: on terrain that's
        // fine, the ground pushes them up, but an islet's ground is a thin surface and they end up under
        // it. On our islets the point is lifted onto whatever ground is just above it.
        [HarmonyLib.HarmonyPatch(typeof(Teleport), "GetTeleportPoint")]
        static class ExitOnGroundPatch
        {
            static void Postfix(ref Vector3 __result)
            {
                if (__result.y > 1000f) return; // into an interior
                Location location = Location.GetLocation(__result, false);
                if (location == null || !Utils.GetPrefabName(location.gameObject).StartsWith("IoV_")) return;
                // Only the islet's own ground counts: an arch or roof over the doorway must not catch it.
                int mask = LayerMask.GetMask("static_solid");
                foreach (RaycastHit hit in Physics.RaycastAll(__result + Vector3.up * 3f, Vector3.down, 6f, mask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider == null || hit.collider.name != "IoV_Ground" || hit.point.y <= __result.y) continue;
                    Plugin.Debug("Dungeon exit lifted " + (hit.point.y + 0.1f - __result.y).ToString("F1") + " m onto the islet's ground");
                    __result.y = hit.point.y + 0.1f;
                    break;
                }
            }
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
