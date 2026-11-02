using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // With DebugLog on: which islet piece broke, where, and whether it had any support left.
    // A piece that falls apart by itself had no support (it touched nothing that carries it).
    [HarmonyPatch(typeof(WearNTear), "Destroy")]
    static class PieceBreakLog
    {
        static void Prefix(WearNTear __instance, HitData hitData, float ___m_support)
        {
            if (!Plugin.DebugLog.Value || __instance == null) return;
            Vector3 pos = __instance.transform.position;
            Location location = Location.GetLocation(pos, false);
            if (location == null || !location.gameObject.name.StartsWith("IoV_")) return;
            Vector3 local = location.transform.InverseTransformPoint(pos);
            Plugin.Debug("Islet piece broke: " + Utils.GetPrefabName(__instance.gameObject) + " at " + local.ToString("F1") +
                " in " + location.gameObject.name + ", support " + ___m_support.ToString("F0") +
                (hitData == null ? " (no hit: fell apart or was removed)" : " (hit)"));
        }
    }
}
