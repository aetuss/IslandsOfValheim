using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // Many of the game's things drop onto the land under them now and then (StaticPhysics: pickables,
    // stones, a structure's loose bits). They look for the game's terrain, which under an islet of our ground
    // mesh is the sea floor, so they would fall through the islet. One standing on our ground looks for solid
    // ground instead, as some of the game's own things do, and finds the islet. (The islets' plants and nests
    // are copies without it; the game's structures on the new small islets bring theirs along.)
    [HarmonyPatch(typeof(StaticPhysics), "CheckFall")]
    static class IsletPhysics
    {
        static readonly HashSet<int> seen = new HashSet<int>();
        static int mask;

        // (a new world: those looked at before are gone; and never more than so many kept in a long game)
        internal static void Reset() { seen.Clear(); }

        static void Prefix(StaticPhysics __instance)
        {
            if (__instance.m_checkSolids || seen.Contains(__instance.GetInstanceID())) return;
            if (seen.Count > 50000) seen.Clear();
            if (mask == 0) mask = LayerMask.GetMask("static_solid", "terrain"); // terrain: on the land, it's settled at once
            bool any = false;
            foreach (RaycastHit hit in Physics.RaycastAll(__instance.transform.position + Vector3.up * 0.5f, Vector3.down, 40f, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null) continue;
                any = true;
                if (hit.collider.name != "IoV_Ground") continue;
                __instance.m_checkSolids = true;
                break;
            }
            // Nothing under it yet (the islet still loading): it's looked at again next time.
            if (any) seen.Add(__instance.GetInstanceID());
        }
    }
}
