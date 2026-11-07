using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Work in progress (not for release): writes to the log every vanilla location the game has (its land,
    // reach, how many) and, for a few, how big they really are and what they're made of, part by part: so a
    // design can be laid out on their true sizes instead of guesses ("IoV measure" lines).
    static partial class IsletBuilder
    {
        // (Round 1, logged 2026-11-08: the boss altars, the stone henges, ShipSetting01, the statues. Round 2: the
        // Deep North's own, with the parts of their big parts.)
        static readonly string[] measured =
        {
            "DN_Bossroom", "DN_gammeltrollFrac01", "DN_gammeltrollFrac02", "ShipSetting02", "ShipSetting03", "IcePond1", "MorkBorg",
            "TheHole01", "HotSpring3", "Runestone_DeepNorth", "MountainWell1", "AncientUpgradeStation", "LumberCamp",
        };

        internal static void MeasureVanilla()
        {
            if (ZoneSystem.instance == null) return;
            List<string> names = new List<string>();
            foreach (ZoneSystem.ZoneLocation l in ZoneSystem.instance.m_locations)
            {
                if (l == null || l.m_prefabName == null || l.m_prefabName.StartsWith("IoV")) continue;
                names.Add(l.m_prefabName + " [" + l.m_biome + ", reach " + l.m_exteriorRadius.ToString("0") + ", " + (l.m_unique ? "unique" : "x" + l.m_quantity) + "]");
            }
            names.Sort();
            Plugin.Log.LogInfo("IoV measure: " + names.Count + " vanilla locations: " + string.Join("; ", names.ToArray()));

            GameObject savedRoot = root;
            foreach (string name in measured)
            {
                if (FindLocation(new[] { name }) == null) { Plugin.Log.LogInfo("IoV measure: " + name + " - no such location"); continue; }
                CustomLocation clone = null;
                try
                {
                    clone = ZoneManager.Instance.CreateClonedLocation("IoVMeasure_" + name, name);
                    if (clone == null || clone.Prefab == null) continue;
                    root = clone.Prefab;
                    Bounds all = LocalBounds(clone.Prefab);
                    Plugin.Log.LogInfo("IoV measure: " + name + " spans x " + all.min.x.ToString("F1") + ".." + all.max.x.ToString("F1") + ", z " + all.min.z.ToString("F1") + ".." + all.max.z.ToString("F1") +
                        ", y " + all.min.y.ToString("F1") + ".." + all.max.y.ToString("F1") + "; " + clone.Prefab.transform.childCount + " parts");
                    int shown = 0;
                    foreach (Transform child in clone.Prefab.transform)
                    {
                        if (shown++ >= 60) { Plugin.Log.LogInfo("IoV measure:   ..."); break; }
                        Bounds b = LocalBounds(child.gameObject);
                        bool mesh = b.size.sqrMagnitude > 0.0001f;
                        Vector3 p = child.localPosition;
                        Plugin.Log.LogInfo("IoV measure:   " + child.name + " at (" + p.x.ToString("F1") + ", " + p.y.ToString("F1") + ", " + p.z.ToString("F1") + ") turned " + child.localEulerAngles.y.ToString("0") +
                            (mesh ? ", spans x " + b.min.x.ToString("F1") + ".." + b.max.x.ToString("F1") + " z " + b.min.z.ToString("F1") + ".." + b.max.z.ToString("F1") + " up to y " + b.max.y.ToString("F1") : ", no mesh") +
                            Kinds(child.gameObject));
                        // A big part made of many: its own parts too, one level down.
                        if (!mesh || Mathf.Max(b.size.x, b.size.z) < 12f || child.childCount < 2) continue;
                        int inner = 0;
                        foreach (Transform part in child)
                        {
                            if (inner++ >= 45) { Plugin.Log.LogInfo("IoV measure:       ..."); break; }
                            Bounds pb = LocalBounds(part.gameObject);
                            Vector3 q = root.transform.InverseTransformPoint(part.position);
                            Plugin.Log.LogInfo("IoV measure:       " + part.name + " at (" + q.x.ToString("F1") + ", " + q.y.ToString("F1") + ", " + q.z.ToString("F1") + ")" +
                                (pb.size.sqrMagnitude > 0.0001f ? " spans x " + pb.min.x.ToString("F1") + ".." + pb.max.x.ToString("F1") + " z " + pb.min.z.ToString("F1") + ".." + pb.max.z.ToString("F1") + " up to y " + pb.max.y.ToString("F1") : " no mesh") +
                                Kinds(part.gameObject));
                        }
                    }
                }
                catch (System.Exception e) { Plugin.Log.LogWarning("IoV measure: " + name + " failed: " + e.Message); }
                finally
                {
                    root = savedRoot;
                    if (clone != null)
                    {
                        ZoneManager.Instance.DestroyCustomLocation(clone.Name);
                        if (clone.Prefab != null) Object.DestroyImmediate(clone.Prefab);
                    }
                }
            }
        }

        // What a part is, where it matters for a design: what the game does with it.
        static string Kinds(GameObject go)
        {
            List<string> kinds = new List<string>();
            if (go.GetComponentInChildren<OfferingBowl>(true) != null) kinds.Add("altar");
            if (go.GetComponentInChildren<Vegvisir>(true) != null) kinds.Add("vegvisir");
            if (go.GetComponentInChildren<CreatureSpawner>(true) != null) kinds.Add("spawner");
            if (go.GetComponentInChildren<SpawnArea>(true) != null) kinds.Add("nest");
            if (go.GetComponentInChildren<Container>(true) != null) kinds.Add("chest");
            if (go.GetComponentInChildren<TerrainModifier>(true) != null) kinds.Add("terrain");
            if (go.GetComponentInChildren<WearNTear>(true) != null) kinds.Add("piece");
            if (go.GetComponentInChildren<Fireplace>(true) != null) kinds.Add("fire");
            if (go.GetComponentInChildren<RandomSpawn>(true) != null) kinds.Add("sometimes");
            if (go.GetComponentInChildren<ParticleSystem>(true) != null) kinds.Add("particles");
            if (go.GetComponentInChildren<Light>(true) != null) kinds.Add("light");
            return kinds.Count > 0 ? " [" + string.Join(", ", kinds.ToArray()) + "]" : "";
        }
    }
}
