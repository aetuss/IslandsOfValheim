using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The sea trader: the last of the seafaring people whose ruins are on the islets, keeping a harbour on
    // one islet in the Swamp or Plains waters. He sells for coins and buys nothing. His body is one of the
    // Deep North's shadows (the ghosts of the fallen) with its scripts taken out, so it just stands there;
    // if a game version doesn't have them, Haldor recoloured. Trading is the vanilla store with his goods.
    static class SeaTrader
    {
        internal const string PrefabName = "IoV_SeaTrader";
        internal const string NameToken = "$iov_trader_name";
        static readonly string[] Bodies = { "ShadowPerson", "Haldor" };

        internal static GameObject Prefab;
        internal static string Body = "";

        // Bought, with the global key that unlocks them (vanilla's boss keys, like Haldor's goods).
        static readonly object[] Goods =
        {
            SeaChart.PrefabName, 1, 150, "",
            SeafarerGear.Lure, 10, 40, "",
            SeafarerGear.Cape, 1, 250, "",
            SeafarerGear.Caulk, 3, 150, "defeated_bonemass",
            SeafarerGear.Hat, 1, 350, "defeated_bonemass",
            SeafarerGear.SaltedFish, 5, 45, "defeated_bonemass",
            SeafarerGear.Vest, 1, 650, "defeated_dragon",
            SeafarerGear.Trousers, 1, 550, "defeated_dragon",
            SeafarerGear.StormMead, 3, 120, "defeated_dragon",
            SeafarerGear.Hardtack, 5, 70, "defeated_dragon",
            SeafarerGear.RansBlessing, 2, 200, "defeated_goblinking",
            Story.BrimOil, 1, 60, Story.FirstTold,       // the tale's own goods, as it goes on
            Story.StormGlass, 1, 120, Story.KetillDone,
        };

        static List<string> Lines(string prefix, int count)
        {
            List<string> lines = new List<string>();
            for (int i = 1; i <= count; i++) lines.Add("$iov_trader_" + prefix + i);
            return lines;
        }

        internal static void Register()
        {
            foreach (string body in Bodies)
            {
                GameObject source = PrefabManager.Instance.GetPrefab(body);
                if (source == null || source.GetComponentInChildren<Renderer>(true) == null) continue;
                Plugin.Debug("Sea trader body " + body + ":\n" + Describe(source.transform, 0));
                GameObject go = PrefabManager.Instance.CreateClonedPrefab(PrefabName, source);
                StripToBody(go);
                if (body == "Haldor") Haldorless(go);
                Setup(go);
                PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
                Prefab = go;
                Body = body;
                Plugin.Log.LogInfo("Sea trader made from " + body + " (" + go.GetComponentsInChildren<Renderer>(true).Length + " renderers, animator "
                    + (go.GetComponentInChildren<Animator>(true) != null) + ")");
                return;
            }
            Plugin.Log.LogWarning("No body found for the sea trader; harbour islets will have none.");
        }

        // Everything that makes it a creature goes: it doesn't move, fight, drop loot or talk about how it died.
        // Its looks stay: meshes, animator (left standing in its idle), lights, particles. A shadow's body is a
        // see-through ghost, so it isn't dressed in armour.
        static void StripToBody(GameObject go)
        {
            for (int pass = 0; pass < 5; pass++)
            {
                bool any = false;
                MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = scripts.Length - 1; i >= 0; i--)
                {
                    if (scripts[i] is ZNetView || scripts[i] is VisEquipment) continue;
                    Object.DestroyImmediate(scripts[i]);
                    any = true;
                }
                if (!any) break;
            }
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            foreach (Animator animator in go.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false; // stays where he's put
            ZNetView nview = go.GetComponent<ZNetView>();
            nview.m_persistent = true;
        }

        // The last resort: Haldor in sea colours.
        static void Haldorless(GameObject go)
        {
            Recolor.Shift sea = Recolor.ToHue(0.58f, 0.45f, 0.85f);
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = Recolor.Material(materials[i], sea);
                renderer.sharedMaterials = materials;
            }
        }

        static void Setup(GameObject go)
        {
            // His own component first: the hud and the interact key take the first one they find.
            go.AddComponent<SeaTraderNpc>();
            Trader trader = go.AddComponent<Trader>();
            trader.enabled = false; // the vanilla one drives Haldor's animations; SeaTraderNpc does the talking
            trader.m_name = NameToken;
            trader.m_standRange = 15f;
            trader.m_greetRange = 6f;
            trader.m_byeRange = 9f;
            trader.m_hideDialogDelay = 5f;
            trader.m_randomTalkInterval = 40f;
            trader.m_dialogHeight = 2.3f;
            trader.m_randomTalk = Lines("talk", 7);
            trader.m_randomGreets = Lines("greet", 3);
            trader.m_randomGoodbye = Lines("bye", 3);
            trader.m_randomStartTrade = Lines("trade", 3);
            trader.m_randomBuy = Lines("buy", 3);
            trader.m_randomSell = Lines("buy", 3);
            trader.m_items = new List<Trader.TradeItem>();
            for (int i = 0; i < Goods.Length; i += 4)
            {
                GameObject item = PrefabManager.Instance.GetPrefab((string)Goods[i]);
                ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
                if (drop == null) continue;
                Trader.TradeItem trade = new Trader.TradeItem();
                trade.m_prefab = drop;
                trade.m_stack = (int)Goods[i + 1];
                trade.m_price = (int)Goods[i + 2];
                trade.m_requiredGlobalKey = (string)Goods[i + 3];
                trader.m_items.Add(trade);
            }
            Plugin.Debug("Sea trader sells " + trader.m_items.Count + " goods");
        }

        internal static bool IsOurs(Trader trader)
        {
            return trader != null && trader.m_name == NameToken;
        }

        static string Describe(Transform t, int depth)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(' ', depth * 2).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (off)").Append(':');
            foreach (Component c in t.GetComponents<Component>())
                if (c != null && !(c is Transform)) sb.Append(' ').Append(c.GetType().Name);
            Renderer r = t.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null)
                sb.Append(" [").Append(r.sharedMaterial.name).Append(" / ").Append(r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "?").Append(']');
            sb.Append('\n');
            if (depth < 6) foreach (Transform child in t) sb.Append(Describe(child, depth + 1));
            return sb.ToString();
        }

        // ---- the store ----

        // Says a line over his head, like the vanilla traders, without their animations.
        [HarmonyPatch(typeof(Trader), "Say", new[] { typeof(string), typeof(string) })]
        static class SayPatch
        {
            static bool Prefix(Trader __instance, string text)
            {
                if (!IsOurs(__instance)) return true;
                if (Chat.instance != null)
                    Chat.instance.SetNpcText(__instance.gameObject, Vector3.up * __instance.m_dialogHeight, 20f, __instance.m_hideDialogDelay, "", text, false);
                return false;
            }
        }

        static readonly System.Reflection.FieldInfo storeTrader = AccessTools.Field(typeof(StoreGui), "m_trader");

        // He doesn't buy anything.
        [HarmonyPatch(typeof(StoreGui), "Show")]
        static class ShowPatch
        {
            static void Postfix(StoreGui __instance, Trader trader)
            {
                if (__instance.m_sellButton != null) __instance.m_sellButton.gameObject.SetActive(!IsOurs(trader));
            }
        }

        [HarmonyPatch(typeof(StoreGui), "GetSellableItem")]
        static class SellPatch
        {
            static bool Prefix(StoreGui __instance, ref ItemDrop.ItemData __result)
            {
                if (!IsOurs(storeTrader.GetValue(__instance) as Trader)) return true;
                __result = null;
                return false;
            }
        }

        // ---- the map icon: a white anchor, like the vanilla traders' icons ----

        [HarmonyPatch(typeof(Minimap), "Start")]
        static class IconPatch
        {
            static void Postfix(Minimap __instance)
            {
                Minimap.LocationSpriteData data = new Minimap.LocationSpriteData();
                data.m_name = IsletBuilder.HarbourName;
                data.m_icon = Icons.Anchor;
                __instance.m_locationIcons.Add(data);
            }
        }
    }

    // The trader's own behaviour: greets you, says goodbye, talks now and then, turns to face you, opens
    // the store. Each client runs it for itself; nothing here is shared over the network.
    class SeaTraderNpc : MonoBehaviour, Hoverable, Interactable
    {
        Trader trader;
        bool greeted;
        float nextTalk;

        void Awake()
        {
            trader = GetComponent<Trader>();
            nextTalk = Time.time + Random.Range(20f, 40f);
            InvokeRepeating("Tick", Random.Range(0f, 0.5f), 0.5f);
        }

        void Tick()
        {
            Player player = Player.m_localPlayer;
            if (trader == null || player == null) return;
            float distance = Vector3.Distance(player.transform.position, transform.position);
            if (!greeted && distance < trader.m_greetRange)
            {
                greeted = true;
                Say(trader.m_randomGreets);
                nextTalk = Time.time + trader.m_randomTalkInterval;
            }
            else if (greeted && distance > trader.m_byeRange)
            {
                greeted = false;
                Say(trader.m_randomGoodbye);
            }
            else if (greeted && Time.time > nextTalk && !StoreGui.IsVisible())
            {
                Say(trader.m_randomTalk);
                nextTalk = Time.time + trader.m_randomTalkInterval * Random.Range(0.8f, 1.3f);
            }
        }

        // Turns towards a player standing close by.
        void Update()
        {
            Player player = Player.m_localPlayer;
            if (player == null || trader == null) return;
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > trader.m_standRange * trader.m_standRange || to.sqrMagnitude < 0.01f) return;
            Quaternion want = Quaternion.LookRotation(to.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 90f * Time.deltaTime);
        }

        void Say(List<string> lines)
        {
            if (lines == null || lines.Count == 0 || Chat.instance == null) return;
            Chat.instance.SetNpcText(gameObject, Vector3.up * trader.m_dialogHeight, 20f, trader.m_hideDialogDelay, "", lines[Random.Range(0, lines.Count)], false);
        }

        // One line over his head.
        internal void Say(string line)
        {
            if (Chat.instance == null || trader == null) return;
            Chat.instance.SetNpcText(gameObject, Vector3.up * trader.m_dialogHeight, 20f, trader.m_hideDialogDelay + 3f, "", line, false);
            nextTalk = Time.time + trader.m_randomTalkInterval;
        }

        // The next step of the tale first (a map to give, news to hear, a fragment to take), else the store.
        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || trader == null || StoreGui.instance == null) return false;
            Player player = user as Player;
            if (player != null && player == Player.m_localPlayer && Story.Talk(player, this)) return true;
            StoreGui.instance.Show(trader);
            if (Random.value < 0.5f) Say(Story.Reminder());
            else Say(trader.m_randomStartTrade);
            return false;
        }

        // Handing him a fragment from the hotbar works too.
        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            Player player = user as Player;
            return player != null && player == Player.m_localPlayer && Story.Talk(player, this);
        }

        public string GetHoverText()
        {
            return Localization.instance.Localize(SeaTrader.NameToken + "\n[<color=yellow><b>$KEY_Use</b></color>] $raven_interact");
        }

        public string GetHoverName() { return Localization.instance.Localize(SeaTrader.NameToken); }

        public float GetHoverOffset() { return 0f; }
    }
}
