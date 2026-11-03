using System.Collections.Generic;
using HarmonyLib;
using SoftReferenceableAssets;
using UnityEngine;

namespace IslandsOfValheim
{
    // Vanilla places a location anywhere in its distance ring; the Deep North and Ashlands islets share the
    // outer ring and must each stay in their half of the world (IsletTier.Allows). While it looks for a spot,
    // the game asks whether a similar location is too close; for a spot in the wrong half the answer here is
    // yes, so it tries another one.
    [HarmonyPatch(typeof(ZoneSystem), "HaveLocationInRange")]
    static class IsletPlacement
    {
        static readonly Dictionary<AssetID, IsletTier> sided = new Dictionary<AssetID, IsletTier>();
        static ZoneSystem mappedFor;

        static void Postfix(ZoneSystem __instance, AssetID assetID, string group, Vector3 p, bool maxGroup, ref bool __result)
        {
            if (__result || maxGroup || group != IsletBuilder.Group) return;
            if (mappedFor != __instance) Map(__instance);
            IsletTier tier;
            if (sided.TryGetValue(assetID, out tier) && !tier.Allows(p)) __result = true;
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
                    if (tier.Side != IsletTier.Half.Any && location.m_prefabName.StartsWith(tier.LocationPrefix))
                        sided[location.m_prefab.m_assetID] = tier;
            }
            Plugin.Debug("Islet kinds that keep to one half of the world: " + sided.Count + " locations");
        }
    }
}
