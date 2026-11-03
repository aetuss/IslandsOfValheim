using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The sea vegvisir shard: found in islet chests, used from the inventory, it marks the closest islet
    // nobody has visited yet on the map, so one islet leads to the next. Only the server knows where the
    // islets are, so using it asks the server over an RPC and the answer places the pin.
    static class SeaChart
    {
        internal const string PrefabName = "IoV_SeaChart";
        const string Token = "$iov_seachart";
        internal const string PinName = "$iov_pin_islet";
        const string RequestRpc = "IoV_SeaChartRequest";
        const string AnswerRpc = "IoV_SeaChartAnswer";
        const int Candidates = 8;      // the server sends a few, the client skips islets it already has a pin for
        const float PinnedRadius = 60f;

        internal static GameObject Prefab;
        static float waitingSince = -100f;

        static readonly System.Reflection.MethodInfo serverPeerId = AccessTools.Method(typeof(ZRoutedRpc), "GetServerPeerID");
        static readonly System.Reflection.MethodInfo closestPin = AccessTools.Method(typeof(Minimap), "GetClosestPin", new[] { typeof(Vector3), typeof(float), typeof(bool) });

        internal static void Register()
        {
            ItemConfig config = new ItemConfig();
            config.Name = Token;
            config.Description = Token + "_desc";
            config.Weight = 0.2f;
            config.StackSize = 10;
            CustomItem item = new CustomItem(PrefabName, "VegvisirShard_Bonemass", config);
            if (item.ItemDrop == null)
            {
                Plugin.Log.LogWarning("Could not make the sea vegvisir shard; island chests won't have any.");
                return;
            }
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
            shared.m_teleportable = true;
            ItemManager.Instance.AddItem(item);
            Prefab = item.ItemPrefab;
        }

        internal static void AddTranslations()
        {
            CustomLocalization localization = LocalizationManager.Instance.GetLocalization();
            string english = "English", turkish = "Turkish"; // Jötunn takes the language by reference
            localization.AddTranslation(ref english, new Dictionary<string, string>
            {
                { "iov_seachart", "Sea vegvisir shard" },
                { "iov_seachart_desc", "A shard of an old vegvisir, worn smooth by the sea. Use it to mark an islet nobody has visited yet on your map." },
                { "iov_pin_islet", "Islet" },
                { "iov_seachart_found", "The shard points across the sea: an islet is marked on your map." },
                { "iov_seachart_none", "The shard stays silent: there is no unvisited islet left." },
            });
            localization.AddTranslation(ref turkish, new Dictionary<string, string>
            {
                { "iov_seachart", "Deniz vegvisiri parçası" },
                { "iov_seachart_desc", "Denizin aşındırdığı eski bir vegvisir parçası. Kullandığında henüz kimsenin gitmediği bir adacığı haritanda işaretler." },
                { "iov_pin_islet", "Adacık" },
                { "iov_seachart_found", "Parça denizin ötesini gösteriyor: haritanda bir adacık işaretlendi." },
                { "iov_seachart_none", "Parça sessiz: gidilmemiş adacık kalmadı." },
            });
            IsletBeacons.AddTranslations(localization);
        }

        internal static long ServerPeer()
        {
            return (long)serverPeerId.Invoke(ZRoutedRpc.instance, null);
        }

        // Whether the map already has a pin at this islet (from a shard, a beacon or the player).
        internal static bool HasPin(Vector3 islet)
        {
            return closestPin.Invoke(Minimap.instance, new object[] { islet, PinnedRadius, false }) != null;
        }

        internal static void RegisterRpcs()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RequestRpc, OnRequest);
            ZRoutedRpc.instance.Register<ZPackage>(AnswerRpc, OnAnswer);
        }

        internal static bool IsShard(ItemDrop.ItemData item)
        {
            return item != null && item.m_shared != null && item.m_shared.m_name == Token;
        }

        // Client: ask the server for islets near the player. The shard is only used up once one is pinned.
        static void Use()
        {
            if (Time.time - waitingSince < 5f || ZRoutedRpc.instance == null) return;
            waitingSince = Time.time;
            ZPackage request = new ZPackage();
            request.Write(Player.m_localPlayer.transform.position);
            ZRoutedRpc.instance.InvokeRoutedRPC(ServerPeer(), RequestRpc, request);
        }

        // Server: the closest islets nobody has been to yet, of the tier of these waters or a tougher one
        // (any unvisited islet if none of those is left), closest first.
        static void OnRequest(long sender, ZPackage request)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
            Vector3 from = request.ReadVector3();
            int here = IsletTier.BandAt(new Vector2(from.x, from.z).magnitude);
            List<ZoneSystem.LocationInstance> unvisited = new List<ZoneSystem.LocationInstance>();
            List<ZoneSystem.LocationInstance> further = new List<ZoneSystem.LocationInstance>();
            foreach (ZoneSystem.LocationInstance instance in Commands.Islets(null))
            {
                if (instance.m_placed) continue;
                unvisited.Add(instance);
                if (Band(instance) >= here) further.Add(instance);
            }
            List<ZoneSystem.LocationInstance> pick = further.Count > 0 ? further : unvisited;
            pick.Sort((a, b) => Commands.Distance(a, from).CompareTo(Commands.Distance(b, from)));

            ZPackage answer = new ZPackage();
            int count = Mathf.Min(Candidates, pick.Count);
            answer.Write(count);
            for (int i = 0; i < count; i++) answer.Write(pick[i].m_position);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, answer);
            Plugin.Debug("Sea chart: sent " + count + " islets to " + sender);
        }

        static System.Collections.IEnumerator ShowLater(Vector3 islet)
        {
            yield return new WaitForSeconds(0.3f);
            if (Minimap.instance != null && !InventoryGui.IsVisible()) Minimap.instance.ShowPointOnMap(islet);
        }

        static int Band(ZoneSystem.LocationInstance instance)
        {
            foreach (IsletTier tier in IsletTier.All)
                if (Commands.Is(instance, tier)) return tier.Band;
            return 0;
        }

        // Client: pin the first islet that isn't on the map yet and use up one shard.
        static void OnAnswer(long sender, ZPackage answer)
        {
            waitingSince = -100f;
            Player player = Player.m_localPlayer;
            if (player == null || Minimap.instance == null) return;
            int count = answer.ReadInt();
            for (int i = 0; i < count; i++)
            {
                Vector3 islet = answer.ReadVector3();
                if (HasPin(islet)) continue;
                ItemDrop.ItemData shard = player.GetInventory().GetItem(Token, -1, false);
                if (shard == null) return;
                player.GetInventory().RemoveOneItem(shard);
                Minimap.instance.DiscoverLocation(islet, Minimap.PinType.Icon3, PinName, false);
                player.Message(MessageHud.MessageType.Center, "$iov_seachart_found", 0, null, false);
                // The shard is used from the inventory: opening the map on top of it locks the game's input,
                // so close the inventory first and open the map a moment later.
                if (InventoryGui.IsVisible()) InventoryGui.instance.Hide();
                Plugin.Instance.StartCoroutine(ShowLater(islet));
                return;
            }
            player.Message(MessageHud.MessageType.Center, "$iov_seachart_none", 0, null, false);
        }

        // Right-click in the inventory (or a hotbar key) uses the shard instead of the vanilla "can't use".
        [HarmonyPatch(typeof(Humanoid), "UseItem")]
        static class UsePatch
        {
            static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
            {
                if (!IsShard(item)) return true;
                if (__instance == Player.m_localPlayer) Use();
                return false;
            }
        }

        [HarmonyPatch(typeof(Game), "Start")]
        static class RpcPatch
        {
            static void Postfix()
            {
                RegisterRpcs();
            }
        }
    }
}
