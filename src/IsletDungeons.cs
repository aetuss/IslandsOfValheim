using System.Collections.Generic;
using System.Text;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // A vanilla dungeon on a large islet: its location is cloned (entrance, interior with its
    // DungeonGenerator, the teleports between them), its parts are moved onto the islet's plateau, and the
    // location's own interior settings are copied onto the islet's. The game then builds the interior the
    // vanilla way when the islet is placed (ZoneSystem.SpawnLocation generates any DungeonGenerator among a
    // location's parts).
    static partial class IsletBuilder
    {
        // The first of the vanilla locations that exists.
        static string FindLocation(string[] candidates)
        {
            if (ZoneSystem.instance == null) return null;
            foreach (string candidate in candidates)
                foreach (ZoneSystem.ZoneLocation location in ZoneSystem.instance.m_locations)
                    if (location != null && location.m_prefabName == candidate) return candidate;
            return null;
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

        // ---- the ground a dungeon's entrance needs ----

        // A vanilla location levels the terrain around its parts (an infested mine's way in is at the bottom
        // of a dug pit, Mørkhalla's gate down a shaft). The islet's ground can't be dug once it's built, so
        // for those the levelling is read first and the ground is shaped the same way (Height).
        class TerrainCut
        {
            internal float X, Z, Y, Level, Smooth, Yaw;
            internal bool Square;
            internal int Order;
        }

        static readonly List<TerrainCut> cuts = new List<TerrainCut>();
        static float dungeonShift; // how far the dungeon is raised so its own ground is the plateau (ReadCuts)

        // How far out from the middle the shaped ground reaches.
        static float CutsReach()
        {
            float reach = 0f;
            foreach (TerrainCut cut in cuts) reach = Mathf.Max(reach, Mathf.Sqrt(cut.X * cut.X + cut.Z * cut.Z) + cut.Smooth);
            return reach;
        }

        // The levelling of a vanilla location as it will stand at (at.x, at.y) on the plateau, turned rotY.
        static void ReadCuts(string source, Vector2 at, float rotY, string tag)
        {
            cuts.Clear();
            CustomLocation clone = ZoneManager.Instance.CreateClonedLocation(tag + "_Terrain", source);
            if (clone == null || clone.Prefab == null) return;
            Transform top = clone.Prefab.transform;
            Quaternion turn = Quaternion.Euler(0f, rotY, 0f);
            Vector3 origin = new Vector3(at.x, PlateauHeight - 0.05f, at.y);
            float lowest = float.MaxValue;
            foreach (TerrainModifier modifier in clone.Prefab.GetComponentsInChildren<TerrainModifier>(true))
            {
                if (!modifier.m_level) continue;
                Vector3 p = origin + turn * top.InverseTransformPoint(modifier.transform.position);
                TerrainCut cut = new TerrainCut();
                cut.X = p.x;
                cut.Z = p.z;
                cut.Y = p.y + modifier.m_levelOffset;
                cut.Level = modifier.m_levelRadius;
                cut.Smooth = modifier.m_smooth ? Mathf.Max(modifier.m_smoothRadius, modifier.m_levelRadius + 0.5f) : modifier.m_levelRadius + 0.5f;
                cut.Square = modifier.m_square;
                cut.Yaw = (Quaternion.Inverse(top.rotation) * modifier.transform.rotation).eulerAngles.y + rotY;
                cut.Order = modifier.m_sortOrder;
                cuts.Add(cut);
                lowest = Mathf.Min(lowest, cut.Y);
                Plugin.Debug("  " + tag + ": ground levelled to " + cut.Y.ToString("F1") + " within " + cut.Level.ToString("F1") + " m (blending out to " + cut.Smooth.ToString("F1") + ")" + (cut.Square ? ", square" : "") + " at (" + cut.X.ToString("F1") + ", " + cut.Z.ToString("F1") + ")");
            }
            cuts.Sort((a, b) => a.Order.CompareTo(b.Order));
            // A deep square shaft (Mørkhalla's) is dug a little wider than the walls built down it; terrain
            // closes round them, but the islet's ground would leave a gap all the way down. The shaft is cut
            // to the walls instead.
            foreach (TerrainCut cut in cuts)
            {
                if (!cut.Square || PlateauHeight - cut.Y < 3f) continue;
                float a = -cut.Yaw * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a), reach = 0f;
                foreach (MeshFilter filter in clone.Prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    Bounds mb = filter.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector3 p = origin + turn * top.InverseTransformPoint(filter.transform.TransformPoint(corner));
                        if (p.y > 500f || p.y > PlateauHeight + 1f || p.y < cut.Y - 1f) continue; // only what's down the shaft
                        float dx = p.x - cut.X, dz = p.z - cut.Z;
                        float d = Mathf.Max(Mathf.Abs(dx * c - dz * s), Mathf.Abs(dx * s + dz * c));
                        if (d <= cut.Level + 1f) reach = Mathf.Max(reach, d);
                    }
                }
                if (reach > 1f && reach < cut.Level)
                {
                    Plugin.Debug("  " + tag + ": shaft cut to its walls, " + reach.ToString("F1") + " m instead of " + cut.Level.ToString("F1"));
                    cut.Level = reach;
                    cut.Smooth = reach + 0.3f;
                }
            }
            // A broad, shallow levelling is the ground the location stands on (the mine's fort sits a metre
            // and a half below its origin): the dungeon is raised or lowered so that ground is the plateau,
            // and that levelling is no longer needed. Without it the plateau around would slope down under
            // the islet's other buildings.
            dungeonShift = 0f;
            TerrainCut floor = null;
            foreach (TerrainCut cut in cuts)
                if (cut.Level >= 7f && Mathf.Abs(cut.Y - PlateauHeight) < 3.5f && (floor == null || cut.Level > floor.Level)) floor = cut;
            if (floor != null)
            {
                dungeonShift = PlateauHeight - floor.Y;
                foreach (TerrainCut cut in cuts) cut.Y += dungeonShift;
                cuts.RemoveAll(c => Mathf.Abs(c.Y - PlateauHeight) < 0.4f);
                lowest += dungeonShift;
                Plugin.Debug("  " + tag + ": raised " + dungeonShift.ToString("F1") + " m so it stands on the plateau; " + cuts.Count + " levellings left");
            }
            if (cuts.Count > 0 && lowest < 0.5f) Plugin.Log.LogWarning(tag + ": its ground goes down to " + lowest.ToString("F1") + " m, under the sea; the plateau should be higher.");
            ZoneManager.Instance.DestroyCustomLocation(clone.Name);
        }

        // The ground with every cut applied in turn: levelled to its height inside its radius, blending back
        // to the ground as it was out to its smoothing radius.
        static float ApplyCuts(float x, float z, float h)
        {
            foreach (TerrainCut cut in cuts)
            {
                float dx = x - cut.X, dz = z - cut.Z, d;
                if (cut.Square)
                {
                    float a = -cut.Yaw * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
                    d = Mathf.Max(Mathf.Abs(dx * c - dz * s), Mathf.Abs(dx * s + dz * c));
                }
                else d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d <= cut.Level) h = cut.Y;
                else if (d < cut.Smooth)
                {
                    float t = (d - cut.Level) / (cut.Smooth - cut.Level);
                    h = Mathf.Lerp(cut.Y, h, t * t * (3f - 2f * t));
                }
            }
            return h;
        }

        // A vanilla dungeon location's parts (entrance, interior with its generator, the teleports between
        // them) moved onto the islet being built, its middle at (at.x, at.y) on the plateau, turned rotY.
        // Its terrain changes are left out (here they'd dig into or raise the sea floor). Null if the vanilla
        // location can't be had. shaped: the ground was shaped for it (ReadCuts), so its parts stay as they are.
        static DungeonInterior AddDungeon(string source, Vector2 at, float rotY, string tag)
        {
            return AddDungeon(source, at, rotY, tag, false);
        }

        static DungeonInterior AddDungeon(string source, Vector2 at, float rotY, string tag, bool shaped)
        {
            CustomLocation clone = ZoneManager.Instance.CreateClonedLocation(tag + "_Source", source);
            if (clone == null || clone.Prefab == null) { Plugin.Log.LogWarning(tag + ": couldn't clone the vanilla location " + source); return null; }
            GameObject vanilla = clone.Prefab;
            Location vanillaLocation = vanilla.GetComponent<Location>();
            Plugin.Debug(tag + ": dungeon from " + source + ":\n" + DescribeTree(vanilla.transform, 0, 3));

            GameObject group = new GameObject("IoV_Dungeon");
            group.transform.SetParent(parent, false);
            group.transform.localPosition = new Vector3(at.x, PlateauHeight - 0.05f + (shaped ? dungeonShift : 0f), at.y);
            group.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            List<Transform> parts = new List<Transform>();
            foreach (Transform child in vanilla.transform) parts.Add(child);
            foreach (Transform part in parts) part.SetParent(group.transform, false);
            int removed = 0;
            foreach (TerrainModifier modifier in group.GetComponentsInChildren<TerrainModifier>(true)) { Object.DestroyImmediate(modifier.gameObject); removed++; }
            foreach (TerrainOp op in group.GetComponentsInChildren<TerrainOp>(true)) { Object.DestroyImmediate(op.gameObject); removed++; }
            // Spawners meant for terrain the islet's ground doesn't follow would sit inside it.
            foreach (CreatureSpawner spawner in group.GetComponentsInChildren<CreatureSpawner>(true))
            {
                Vector3 p = root.transform.InverseTransformPoint(spawner.transform.position);
                if (p.y > 1000f) continue; // the interior's are far up
                float ground = Height(p.x, p.z);
                if (p.y >= ground + 0.2f) continue;
                spawner.transform.position = root.transform.TransformPoint(new Vector3(p.x, ground + 0.3f, p.z));
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
