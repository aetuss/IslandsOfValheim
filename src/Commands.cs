using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // iov_info lists how many islets the world has and the closest ones. To go to one, use the vanilla
    // command `findtp IoV_Islet_Meadows` (needs devcommands).
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    static class Commands
    {
        static void Postfix()
        {
            new Terminal.ConsoleCommand("iov_info",
                "- IslandsOfValheim: show how many islets this world has and where the closest ones are",
                args =>
                {
                    if (ZoneSystem.instance == null || ZNet.instance == null || !ZNet.instance.IsServer())
                    {
                        args.Context.AddString("Island positions are only known on the server (single player or the host).");
                        return;
                    }
                    Vector3 from = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero;
                    List<ZoneSystem.LocationInstance> islets = new List<ZoneSystem.LocationInstance>();
                    foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.GetLocationList())
                        if (instance.m_location != null && instance.m_location.m_prefabName == IsletBuilder.LocationName)
                            islets.Add(instance);
                    islets.Sort((a, b) => Distance(a, from).CompareTo(Distance(b, from)));

                    int placed = 0;
                    foreach (ZoneSystem.LocationInstance instance in islets) if (instance.m_placed) placed++;
                    args.Context.AddString("This world has " + islets.Count + " islets (" + placed + " already generated).");
                    if (islets.Count == 0)
                        args.Context.AddString("An existing world only gets islets after `genloc` (devcommands), and only in ocean nobody has visited yet.");
                    for (int i = 0; i < islets.Count && i < 5; i++)
                    {
                        Vector3 p = islets[i].m_position;
                        args.Context.AddString("  " + Distance(islets[i], from).ToString("0", CultureInfo.InvariantCulture) + " m away at (" +
                            p.x.ToString("0", CultureInfo.InvariantCulture) + ", " + p.z.ToString("0", CultureInfo.InvariantCulture) + ")" +
                            (islets[i].m_placed ? ", generated" : ""));
                    }
                    if (islets.Count > 0) args.Context.AddString("Teleport to the closest new one: iov_goto (needs devcommands)");
                });

            new Terminal.ConsoleCommand("iov_goto",
                "- IslandsOfValheim: teleport to the closest islet that hasn't been generated yet, so you see the current version of it",
                args =>
                {
                    if (ZoneSystem.instance == null || ZNet.instance == null || !ZNet.instance.IsServer() || Player.m_localPlayer == null)
                    {
                        args.Context.AddString("Only works in single player or as the host.");
                        return;
                    }
                    Vector3 from = Player.m_localPlayer.transform.position;
                    bool found = false;
                    ZoneSystem.LocationInstance best = new ZoneSystem.LocationInstance();
                    foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.GetLocationList())
                    {
                        if (instance.m_placed || instance.m_location == null || instance.m_location.m_prefabName != IsletBuilder.LocationName) continue;
                        if (!found || Distance(instance, from) < Distance(best, from)) best = instance;
                        found = true;
                    }
                    if (!found)
                    {
                        args.Context.AddString("No islet left that hasn't been generated yet.");
                        return;
                    }
                    Vector3 target = new Vector3(best.m_position.x, ZoneSystem.instance.m_waterLevel + IsletBuilder.PlateauHeight + 3f, best.m_position.z);
                    Player.m_localPlayer.TeleportTo(target, Player.m_localPlayer.transform.rotation, true);
                    args.Context.AddString("Teleporting " + Distance(best, from).ToString("0", CultureInfo.InvariantCulture) + " m to a new islet.");
                }, true);
        }

        static float Distance(ZoneSystem.LocationInstance instance, Vector3 from)
        {
            Vector3 p = instance.m_position;
            return new Vector2(p.x - from.x, p.z - from.z).magnitude;
        }
    }
}
