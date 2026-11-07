using System.Collections.Generic;
using HarmonyLib;
using SoftReferenceableAssets;
using UnityEngine;

namespace IslandsOfValheim
{
    // Vanilla places a location anywhere in its distance ring; the Deep North and Ashlands islets share the
    // outer ring and must each stay in their half of the world (IsletTier.Allows). While it looks for a spot,
    // the game asks whether a similar location is too close; for a spot in the wrong half the answer here is
    // yes, so it tries another one. The same goes for the tale's Ashlands and Deep North islets.
    [HarmonyPatch(typeof(ZoneSystem), "HaveLocationInRange")]
    static class IsletPlacement
    {
        static readonly Dictionary<AssetID, IsletTier> sided = new Dictionary<AssetID, IsletTier>();
        static ZoneSystem mappedFor;

        static void Postfix(ZoneSystem __instance, AssetID assetID, string group, Vector3 p, bool maxGroup, ref bool __result)
        {
            if (__result || maxGroup || group == null || !group.StartsWith("IoV_")) return;
            // The tale's two outer islets: each unique location is its own group.
            if (group == Story.AshIslet && p.z > 0f) { __result = true; return; }
            if (group == Story.HiddenIslet && p.z <= 0f) { __result = true; return; }
            if (group == IsletBuilder.Group || group == "IoV_Large")
            {
                if (mappedFor != __instance) Map(__instance);
                IsletTier tier;
                if (sided.TryGetValue(assetID, out tier) && !tier.Allows(p)) { __result = true; return; }
            }
            // Each kind only keeps its distance from its own group; ours also keep apart from one another,
            // or a small islet could come up on a large one's shore.
            if (NearOurs(__instance, p, group == "IoV_Large")) __result = true;
        }

        const float Apart = 130f, ApartLarge = 230f;

        static bool NearOurs(ZoneSystem zones, Vector3 p, bool large)
        {
            Vector2s zone = ZoneSystem.GetZone(p);
            for (int dx = -4; dx <= 4; dx++)
                for (int dy = -4; dy <= 4; dy++)
                {
                    ZoneSystem.LocationInstance other;
                    if (!zones.m_locationInstances.TryGetValue(new Vector2s(zone.x + dx, zone.y + dy), out other) || other.m_location == null) continue;
                    string name = other.m_location.m_prefabName;
                    if (name == null || !name.StartsWith("IoV_")) continue;
                    float keep = large || name.StartsWith(IsletBuilder.LargePrefix) ? ApartLarge : Apart;
                    if (new Vector2(other.m_position.x - p.x, other.m_position.z - p.z).sqrMagnitude < keep * keep) return true;
                }
            return false;
        }

        // Which of the registered locations belong to a kind that keeps to one half.
        static void Map(ZoneSystem zones)
        {
            mappedFor = zones;
            sided.Clear();
            foreach (ZoneSystem.ZoneLocation location in zones.m_locations)
            {
                if (location == null || location.m_prefabName == null) continue;
                foreach (IsletTier tier in IsletTier.All)
                    if (tier.Side != IsletTier.Half.Any && (location.m_prefabName.StartsWith(tier.LocationPrefix) || IsletBuilder.IsLargeOf(location.m_prefabName, tier)))
                        sided[location.m_prefab.m_assetID] = tier;
            }
            Plugin.Debug("Islet kinds that keep to one half of the world: " + sided.Count + " locations");
        }
    }
}
