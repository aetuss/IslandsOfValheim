using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // iov_info lists how many islets of each tier the world has and the closest ones; iov_goto (cheat)
    // teleports to the closest islet nobody has been to yet, optionally of one tier.
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    static class Commands
    {
        static void Postfix()
        {
            new Terminal.ConsoleCommand("iov_info",
                "- IslandsOfValheim: show how many islets of each tier this world has and where the closest ones are",
                args =>
                {
                    if (!OnServer(args.Context)) return;
                    Vector3 from = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero;
                    List<ZoneSystem.LocationInstance> islets = Islets(null);
                    if (islets.Count == 0)
                    {
                        args.Context.AddString("This world has no islets. An existing world only gets them after `genloc` (devcommands), and only in ocean nobody has visited yet.");
                        return;
                    }
                    foreach (IsletTier tier in IsletTier.All)
                    {
                        int count = 0, placed = 0;
                        foreach (ZoneSystem.LocationInstance instance in islets)
                            if (Is(instance, tier)) { count++; if (instance.m_placed) placed++; }
                        string where = tier.Side == IsletTier.Half.North ? ", north half" : tier.Side == IsletTier.Half.South ? ", south half" : "";
                        args.Context.AddString(tier.Name + ": " + count + " islets (" + placed + " already generated" + where + ")");
                    }
                    if (WorldSize.Scale != 1f)
                        args.Context.AddString("World radius " + WorldSize.Radius.ToString("0", CultureInfo.InvariantCulture) + " m (Expand World Size): the tier rings are " +
                            WorldSize.Scale.ToString("0.##", CultureInfo.InvariantCulture) + " times as far out as in vanilla.");
                    islets.Sort((a, b) => Distance(a, from).CompareTo(Distance(b, from)));
                    args.Context.AddString("Closest:");
                    for (int i = 0; i < islets.Count && i < 5; i++)
                    {
                        Vector3 p = islets[i].m_position;
                        args.Context.AddString("  " + TierName(islets[i]) + ", " + Distance(islets[i], from).ToString("0", CultureInfo.InvariantCulture) + " m away at (" +
                            p.x.ToString("0", CultureInfo.InvariantCulture) + ", " + p.z.ToString("0", CultureInfo.InvariantCulture) + ")" +
                            (islets[i].m_placed ? ", generated" : ""));
                    }
                    List<ZoneSystem.LocationInstance> harbours = Harbours();
                    if (harbours.Count > 0)
                    {
                        ZoneSystem.LocationInstance harbour = harbours[0];
                        foreach (ZoneSystem.LocationInstance instance in harbours) if (instance.m_placed) harbour = instance;
                        args.Context.AddString("Sea trader: " + (harbour.m_placed ? "his harbour is at (" + harbour.m_position.x.ToString("0", CultureInfo.InvariantCulture) + ", " +
                            harbour.m_position.z.ToString("0", CultureInfo.InvariantCulture) + ")" : harbours.Count + " possible harbours, nobody has found one yet"));
                    }
                    args.Context.AddString("Teleport to the closest new one: iov_goto [tier] (needs devcommands)");
                });

            new Terminal.ConsoleCommand("iov_placeislets",
                "- IslandsOfValheim: adds the islets this world is missing now (host or single player, needs devcommands); it also runs once per world on its own",
                args =>
                {
                    if (!OnServer(args.Context)) return;
                    args.Context.AddString("Looking for missing islets... (the result shows here and in the log)");
                    ExistingWorlds.RunNow(args.Context);
                }, true);

            new Terminal.ConsoleCommand("iov_light",
                "- IslandsOfValheim: shows the light from the end of Sævar's tale a few metres in front of you (needs devcommands)",
                args =>
                {
                    if (Player.m_localPlayer == null) return;
                    LightBeam.Over(Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 5f, 15f);
                }, true);

            new Terminal.ConsoleCommand("iov_goto",
                "[tier] - IslandsOfValheim: teleport to the closest islet that hasn't been generated yet (of that tier: " + TierNames(", ") + "), or to the sea trader's harbour (trader)",
                args =>
                {
                    if (!OnServer(args.Context) || Player.m_localPlayer == null) return;
                    IsletTier wanted = null;
                    if (args.Length > 1 && string.Equals(args[1], "trader", System.StringComparison.OrdinalIgnoreCase))
                    {
                        GotoHarbour(args.Context);
                        return;
                    }
                    if (args.Length > 1 && string.Equals(args[1], "terraintest", System.StringComparison.OrdinalIgnoreCase))
                    {
                        TerrainIsletTest.Goto(args.Context); // research prototype
                        return;
                    }
                    // Work in progress: a design's bare islet (ground, rocks, dungeon), unturned, to lay out its
                    // draft on with the dev tool (iov_goto draft_meadows_2).
                    foreach (IsletBuilder.LargeKind kind in IsletBuilder.LargeKinds)
                        if (args.Length > 1 && string.Equals(args[1], "draft_" + LargeArg(kind).Substring(6), System.StringComparison.OrdinalIgnoreCase))
                        {
                            TerrainIsletTest.Goto(args.Context, kind.BaseName, kind.Height, 140f, Mathf.Max(900f, kind.Tier.MinDistance), 0f);
                            return;
                        }
                    string story = args.Length > 1 ? StoryLocation(args[1]) : null;
                    if (story != null)
                    {
                        GotoUnique(args.Context, story, "Sævar's tale");
                        return;
                    }
                    if (args.Length > 1)
                    {
                        foreach (IsletTier tier in IsletTier.All)
                            if (string.Equals(tier.Name, args[1], System.StringComparison.OrdinalIgnoreCase)) wanted = tier;
                        if (wanted == null)
                        {
                            args.Context.AddString("Unknown tier " + args[1] + ". Use one of: " + TierNames(", ") + ".");
                            return;
                        }
                    }
                    Vector3 from = Player.m_localPlayer.transform.position;
                    bool found = false;
                    ZoneSystem.LocationInstance best = new ZoneSystem.LocationInstance();
                    foreach (ZoneSystem.LocationInstance instance in Islets(wanted))
                    {
                        if (instance.m_placed) continue;
                        if (!found || Distance(instance, from) < Distance(best, from)) best = instance;
                        found = true;
                    }
                    if (!found)
                    {
                        args.Context.AddString("No islet left that hasn't been generated yet.");
                        return;
                    }
                    Vector3 target = new Vector3(best.m_position.x, ZoneSystem.instance.m_waterLevel + IsletBuilder.SmallPlateau + 3f, best.m_position.z);
                    Player.m_localPlayer.TeleportTo(target, Player.m_localPlayer.transform.rotation, true);
                    args.Context.AddString("Teleporting " + Distance(best, from).ToString("0", CultureInfo.InvariantCulture) + " m to a new " + TierName(best) + " islet.");
                }, true, optionsFetcher: () =>
                {
                    List<string> names = new List<string>();
                    foreach (IsletTier tier in IsletTier.All) names.Add(tier.Name);
                    names.Add("trader");
                    for (int i = 1; i <= Story.Islets.Length; i++) names.Add("story" + i);
                    foreach (IsletBuilder.LargeKind kind in IsletBuilder.LargeKinds) names.Add(LargeArg(kind));
                    foreach (IsletBuilder.LargeKind kind in IsletBuilder.LargeKinds) names.Add("draft_" + LargeArg(kind).Substring(6));
                    return names;
                });
        }

        // The sea trader's harbour: the one that was generated, or else the closest of the candidates (going
        // there generates it, and the others vanish).
        static string StoryLocation(string arg)
        {
            for (int i = 1; i <= Story.Islets.Length; i++)
                if (string.Equals(arg, "story" + i, System.StringComparison.OrdinalIgnoreCase)) return Story.Islets[i - 1];
            foreach (IsletBuilder.LargeKind kind in IsletBuilder.LargeKinds)
                if (string.Equals(arg, LargeArg(kind), System.StringComparison.OrdinalIgnoreCase)) return kind.LandName;
            return null;
        }

        // large_meadows for a land's first design, large_meadows_2 for its second...
        static string LargeArg(IsletBuilder.LargeKind kind)
        {
            return "large_" + kind.Tier.Name.ToLowerInvariant() + (kind.Design > 1 ? "_" + kind.Design : "");
        }

        static void GotoHarbour(Terminal context)
        {
            GotoUnique(context, IsletBuilder.HarbourName, "the sea trader's harbour");
        }

        // A unique location: the one that was generated, or else the closest of its candidates.
        static void GotoUnique(Terminal context, string location, string what)
        {
            List<ZoneSystem.LocationInstance> harbours = Unique(location);
            if (harbours.Count == 0)
            {
                context.AddString("This world has no " + what + " location (a new world, or genloc, places one; SeaTraderIslets 0 turns it off).");
                return;
            }
            Vector3 from = Player.m_localPlayer.transform.position;
            harbours.Sort((a, b) => Distance(a, from).CompareTo(Distance(b, from)));
            ZoneSystem.LocationInstance best = harbours[0];
            foreach (ZoneSystem.LocationInstance instance in harbours) if (instance.m_placed) best = instance;
            IsletBuilder.LargeKind large = IsletBuilder.LargeKindOf(location);
            float plateau = large != null ? large.Height : IsletBuilder.SmallPlateau;
            Vector3 target = new Vector3(best.m_position.x, ZoneSystem.instance.m_waterLevel + plateau + 3f, best.m_position.z);
            Player.m_localPlayer.TeleportTo(target, Player.m_localPlayer.transform.rotation, true);
            context.AddString("Teleporting " + Distance(best, from).ToString("0", CultureInfo.InvariantCulture) + " m to " + what + " (" + location + ").");
        }

        internal static List<ZoneSystem.LocationInstance> Harbours()
        {
            return Unique(IsletBuilder.HarbourName);
        }

        static List<ZoneSystem.LocationInstance> Unique(string location)
        {
            // A large islet: either kind of ground (old ones on our mesh, new ones of the game's terrain).
            IsletBuilder.LargeKind large = IsletBuilder.LargeKindOf(location);
            List<ZoneSystem.LocationInstance> result = new List<ZoneSystem.LocationInstance>();
            foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.GetLocationList())
            {
                if (instance.m_location == null) continue;
                string name = instance.m_location.m_prefabName;
                if (name == location || (large != null && name == large.LocationName && large.Design == 1 && location == large.LandName)) result.Add(instance);
            }
            return result;
        }

        static bool OnServer(Terminal context)
        {
            if (ZoneSystem.instance != null && ZNet.instance != null && ZNet.instance.IsServer()) return true;
            context.AddString("Island positions are only known on the server (single player or the host).");
            return false;
        }

        // All islet location instances, or only those of one tier.
        internal static List<ZoneSystem.LocationInstance> Islets(IsletTier only)
        {
            List<ZoneSystem.LocationInstance> result = new List<ZoneSystem.LocationInstance>();
            foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.GetLocationList())
            {
                if (instance.m_location == null) continue;
                foreach (IsletTier tier in IsletTier.All)
                    if ((only == null || only == tier) && Is(instance, tier)) result.Add(instance);
            }
            return result;
        }

        static string TierNames(string separator)
        {
            List<string> names = new List<string>();
            foreach (IsletTier tier in IsletTier.All) names.Add(tier.Name);
            return string.Join(separator, names.ToArray());
        }

        static string TierName(ZoneSystem.LocationInstance instance)
        {
            foreach (IsletTier tier in IsletTier.All)
                if (Is(instance, tier)) return tier.Name;
            return "?";
        }

        internal static bool Is(ZoneSystem.LocationInstance instance, IsletTier tier)
        {
            return instance.m_location.m_prefabName.StartsWith(tier.LocationPrefix);
        }

        internal static float Distance(ZoneSystem.LocationInstance instance, Vector3 from)
        {
            Vector3 p = instance.m_position;
            return new Vector2(p.x - from.x, p.z - from.z).magnitude;
        }
    }
}
