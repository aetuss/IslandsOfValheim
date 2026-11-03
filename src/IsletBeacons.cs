using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // An old beacon tower on some islets: a stone pillar with a cold brazier on top. Pay the tier's fuel
    // and it burns for good, for everyone (the lit state is in the brazier's ZDO). Its light shows every
    // islet within RevealRadius: they get a pin on the map, since the map is drawn from the vanilla
    // terrain and shows islets as open sea, and the fog around the beacon clears. Every player who comes
    // to a lit beacon gets the same. Lighting it also draws one small wave of the tier's creatures, once.
    static partial class IsletBuilder
    {
        // Up by the cliff, beside the ridge structure, on about one islet in three.
        static void PlaceBeaconTower()
        {
            Optional(0.33f, () =>
            {
                float deg = cliffDeg - 28f;
                float r = PlateauAt(deg) + 2.5f;
                Vector2 at = Polar(deg, r);
                Vector3 turn = new Vector3(0f, 90f - deg, 0f);
                float g = GroundMin(at.x, at.y, 1f) - 0.3f;
                float top = Top(At(tier.BeaconBase, new Vector3(at.x, g, at.y), turn, false), g + 1f);
                top = Top(At(tier.BeaconPillar, new Vector3(at.x, top, at.y), turn, false), top + 2f);
                At(IsletBeacons.PrefabName(tier), new Vector3(at.x, top, at.y), turn, false);
                // What's left of the rest of the tower.
                Vector2 a = Polar(deg + 8f, r + 2.2f);
                At(tier.BeaconRubble, new Vector3(a.x, Height(a.x, a.y) - 0.15f, a.y), new Vector3(10f, deg * 2f, 80f), false);
                Vector2 b = Polar(deg - 9f, r - 1.6f);
                At(tier.BeaconRubble, new Vector3(b.x, Height(b.x, b.y) - 0.1f, b.y), new Vector3(0f, deg, 0f), false);
            });
        }
    }

    static class IsletBeacons
    {
        internal const string LightRpc = "IoV_BeaconLight";
        const string RequestRpc = "IoV_BeaconRequest";
        const string AnswerRpc = "IoV_BeaconAnswer";
        internal const string LitKey = "IoV_BeaconLit";
        internal const string FlamesName = "IoV_Flames";
        const float RevealRadius = 700f;     // islets this close to a lit beacon get a pin
        const float OwnIsletRadius = 60f;    // the beacon's own islet
        const float FogRadius = 300f;
        internal const float VisitRadius = 30f;

        static readonly System.Reflection.MethodInfo explore = AccessTools.Method(typeof(Minimap), "Explore", new[] { typeof(Vector3), typeof(float) });

        internal static string PrefabName(IsletTier tier) { return "IoV_Beacon_" + tier.Name; }

        internal static IsletTier TierOf(string prefabName)
        {
            foreach (IsletTier tier in IsletTier.All)
                if (PrefabName(tier) == prefabName) return tier;
            return null;
        }

        // One brazier per tier, cloned from a vanilla one: its flames stay, its fuel logic goes.
        internal static void Register()
        {
            foreach (IsletTier tier in IsletTier.All)
            {
                string source = PrefabManager.Instance.GetPrefab(tier.BeaconBrazier) != null ? tier.BeaconBrazier : "piece_brazierfloor01";
                if (PrefabManager.Instance.GetPrefab(source) == null)
                {
                    Plugin.Log.LogWarning("No vanilla brazier found, " + tier.Name + " islets get no beacons.");
                    continue;
                }
                GameObject go = PrefabManager.Instance.CreateClonedPrefab(PrefabName(tier), source);
                Fireplace fire = go.GetComponent<Fireplace>();
                if (fire != null)
                {
                    foreach (GameObject state in new[] { fire.m_enabledObjectLow, fire.m_enabledObjectHigh, fire.m_halfObject, fire.m_emptyObject })
                        if (state != null && state != fire.m_enabledObject) state.SetActive(false);
                    GameObject flames = fire.m_enabledObject;
                    if (flames != null)
                    {
                        flames.name = FlamesName;
                        flames.SetActive(false);
                        AddGlow(flames);
                    }
                    if (fire.m_playerBaseObject != null) Object.DestroyImmediate(fire.m_playerBaseObject);
                    Object.DestroyImmediate(fire);
                }
                // A burning brazier counts as a player's base, where creatures don't spawn: not this one,
                // or the islet's guards would stay away.
                foreach (EffectArea area in go.GetComponentsInChildren<EffectArea>(true))
                    area.m_type &= ~(EffectArea.Type.PlayerBase | EffectArea.Type.NoMonsters);
                // Nothing to break or take apart with a hammer.
                foreach (WearNTear piece in go.GetComponentsInChildren<WearNTear>(true)) Object.DestroyImmediate(piece);
                foreach (Piece piece in go.GetComponentsInChildren<Piece>(true)) Object.DestroyImmediate(piece);
                go.GetComponent<ZNetView>().m_persistent = true;
                go.AddComponent<IsletBeacon>();
                PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
                Plugin.Debug("Beacon " + go.name + " made from " + source + (fire != null && fire.m_enabledObject != null ? "" : " (no flames found)"));
            }
        }

        // A light strong enough to carry over the water, in the colour of the brazier's own.
        static void AddGlow(GameObject flames)
        {
            Light own = flames.GetComponentInChildren<Light>(true);
            GameObject glow = new GameObject("IoV_BeaconGlow");
            glow.transform.SetParent(flames.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 30f;
            light.intensity = 2.5f;
            light.color = own != null ? own.color : new Color(1f, 0.65f, 0.35f);
        }

        // ---- fuel ----

        internal static string ItemName(string prefab)
        {
            GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
            return drop != null ? drop.m_itemData.m_shared.m_name : prefab;
        }

        // The first way of paying the player can afford, or null.
        internal static Cost[] Affordable(IsletTier tier, Inventory inventory)
        {
            foreach (Cost[] option in tier.BeaconFuel)
            {
                bool enough = true;
                foreach (Cost cost in option)
                    if (inventory.CountItems(ItemName(cost.Item), -1, true) < cost.Amount) enough = false;
                if (enough) return option;
            }
            return null;
        }

        // "10 $item_wood, 5 $item_resin or ..." (the hud translates the tokens).
        internal static string CostText(IsletTier tier)
        {
            List<string> options = new List<string>();
            foreach (Cost[] option in tier.BeaconFuel)
            {
                List<string> parts = new List<string>();
                foreach (Cost cost in option) parts.Add(cost.Amount + " " + ItemName(cost.Item));
                options.Add(string.Join(", ", parts.ToArray()));
            }
            return string.Join(" $iov_beacon_or ", options.ToArray());
        }

        // ---- the wave ----

        // On the beacon's owner, once: the tier's creatures climb out of the sea onto the shore 15-25 m away,
        // or come down from the sky, and go for the player.
        internal static void SpawnWave(IsletTier tier, Vector3 beacon)
        {
            Location location = Location.GetLocation(beacon, false);
            List<Vector3> shore = ShorePoints(location != null ? location.transform.position : beacon, beacon);
            int spawned = 0;
            foreach (WaveGroup group in tier.BeaconWave)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(group.Creature);
                if (prefab == null) continue;
                for (int i = 0; i < group.Count; i++)
                {
                    Vector3 pos;
                    if (group.FromSky || shore.Count == 0)
                    {
                        Vector2 off = Random.insideUnitCircle.normalized * Random.Range(15f, 22f);
                        pos = beacon + new Vector3(off.x, group.FromSky ? 12f : 0f, off.y);
                    }
                    else pos = shore[Random.Range(0, shore.Count)];
                    Spawn(prefab, pos, beacon, tier, group.FromSky);
                    spawned++;
                }
            }
            Plugin.Debug("Beacon lit at " + beacon.ToString("F0") + ": " + spawned + " creatures came, " + shore.Count + " shore spots");
        }

        // Spots at the waterline around the islet, preferring those 15-25 m from the beacon.
        static List<Vector3> ShorePoints(Vector3 centre, Vector3 beacon)
        {
            float water = ZoneSystem.instance.m_waterLevel;
            int mask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece");
            List<Vector3> near = new List<Vector3>(), any = new List<Vector3>();
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (float r = 10f; r < 36f; r += 1f)
                {
                    Vector3 from = centre + dir * r;
                    from.y = water + 30f;
                    RaycastHit hit;
                    if (!Physics.Raycast(from, Vector3.down, out hit, 80f, mask)) break;
                    if (hit.point.y > water + 1.2f) continue; // still up on the islet
                    if (hit.point.y < water - 1.5f) break;    // past the shore, under water
                    Vector3 p = hit.point + Vector3.up * 0.2f;
                    float d = Utils.DistanceXZ(p, beacon);
                    if (d >= 15f && d <= 25f) near.Add(p);
                    else any.Add(p);
                    break;
                }
            }
            return near.Count > 0 ? near : any;
        }

        static void Spawn(GameObject prefab, Vector3 pos, Vector3 beacon, IsletTier tier, bool fromSky)
        {
            Vector3 look = new Vector3(beacon.x - pos.x, 0f, beacon.z - pos.z);
            GameObject go = Object.Instantiate(prefab, pos, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
            Character character = go.GetComponent<Character>();
            if (character != null)
            {
                int level = tier.MinLevel + (Random.Range(0f, 100f) < tier.LevelUpChance ? 1 : 0);
                character.SetLevel(Mathf.Min(level, tier.MaxLevel));
            }
            // Alerted, so they look for whoever lit it; not set to hunt players, which would keep them alert
            // for good (they stay on the islet afterwards, like its guards).
            BaseAI ai = go.GetComponent<BaseAI>();
            if (ai != null) ai.Alert();
            Effect(fromSky ? "vfx_spawn" : "fx_WaterImpact_Big", pos);
            Effect("sfx_spawn", pos);
        }

        static void Effect(string name, Vector3 pos)
        {
            GameObject fx = ZNetScene.instance.GetPrefab(name);
            if (fx != null) Object.Instantiate(fx, pos, Quaternion.identity);
        }

        // ---- the reward ----

        // Client: ask the server which islets are around this beacon.
        internal static void RequestReward(Vector3 beacon)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage request = new ZPackage();
            request.Write(beacon);
            ZRoutedRpc.instance.InvokeRoutedRPC(SeaChart.ServerPeer(), RequestRpc, request);
        }

        // Server: every islet (visited or not) within RevealRadius, other than the beacon's own.
        static void OnRequest(long sender, ZPackage request)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
            Vector3 beacon = request.ReadVector3();
            List<Vector3> near = new List<Vector3>();
            foreach (ZoneSystem.LocationInstance instance in Commands.Islets(null))
            {
                float d = Commands.Distance(instance, beacon);
                if (d > OwnIsletRadius && d < RevealRadius) near.Add(instance.m_position);
            }
            ZPackage answer = new ZPackage();
            answer.Write(beacon);
            answer.Write(near.Count);
            foreach (Vector3 p in near) answer.Write(p);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, answer);
        }

        // Client: pin the islets not on the map yet and clear the fog around the beacon.
        static void OnAnswer(long sender, ZPackage answer)
        {
            Player player = Player.m_localPlayer;
            if (player == null || Minimap.instance == null) return;
            Vector3 beacon = answer.ReadVector3();
            int count = answer.ReadInt();
            int added = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 islet = answer.ReadVector3();
                if (SeaChart.HasPin(islet)) continue;
                Minimap.instance.DiscoverLocation(islet, Minimap.PinType.Icon3, SeaChart.PinName, false);
                added++;
            }
            explore.Invoke(Minimap.instance, new object[] { beacon, FogRadius });
            if (added > 0) Plugin.Instance.StartCoroutine(Say("$iov_beacon_reward " + added, flaredAt + MessageTime - Time.time));
            Plugin.Debug("Beacon at " + beacon.ToString("F0") + ": " + count + " islets around it, " + added + " new pins");
        }

        // Lighting it says one thing and its light another, right after: the second waits until the first
        // could be read. Both also stay in the list at the top left for a while.
        const float MessageTime = 5f;
        static float flaredAt = -100f;

        internal static void Flared(Player player)
        {
            flaredAt = Time.time;
            player.Message(MessageHud.MessageType.Center, "$iov_beacon_flare", 0, null, false);
            player.Message(MessageHud.MessageType.TopLeft, "$iov_beacon_flare", 0, null, false);
        }

        static System.Collections.IEnumerator Say(string message, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Player player = Player.m_localPlayer;
            if (player == null) yield break;
            player.Message(MessageHud.MessageType.Center, message, 0, null, false);
            player.Message(MessageHud.MessageType.TopLeft, message, 0, null, false);
        }

        internal static void AddTranslations(CustomLocalization localization)
        {
            string english = "English", turkish = "Turkish";
            localization.AddTranslation(ref english, new Dictionary<string, string>
            {
                { "iov_beacon", "Old beacon" },
                { "iov_beacon_light", "Light it" },
                { "iov_beacon_or", "or" },
                { "iov_beacon_lit", "It burns. Its light shows the islets around it." },
                { "iov_beacon_need", "To light the beacon you need:" },
                { "iov_beacon_flare", "The beacon flares up... and something out there saw it." },
                { "iov_beacon_reward", "The beacon's light shows the islets nearby. Marked on your map:" },
            });
            localization.AddTranslation(ref turkish, new Dictionary<string, string>
            {
                { "iov_beacon", "Eski işaret kulesi" },
                { "iov_beacon_light", "Yak" },
                { "iov_beacon_or", "ya da" },
                { "iov_beacon_lit", "Yanıyor. Işığı çevresindeki adacıkları gösteriyor." },
                { "iov_beacon_need", "İşaret ateşini yakmak için gerekenler:" },
                { "iov_beacon_flare", "İşaret ateşi parladı... ama dışarıda bir şeyler de gördü." },
                { "iov_beacon_reward", "İşaret ateşinin ışığı yakındaki adacıkları gösteriyor. Haritanda işaretlenen:" },
            });
        }

        [HarmonyPatch(typeof(Game), "Start")]
        static class RpcPatch
        {
            static void Postfix()
            {
                ZRoutedRpc.instance.Register<ZPackage>(RequestRpc, OnRequest);
                ZRoutedRpc.instance.Register<ZPackage>(AnswerRpc, OnAnswer);
                IsletBeacon.Rewarded.Clear();
            }
        }
    }

    // The brazier on top of a beacon tower.
    class IsletBeacon : MonoBehaviour, Hoverable, Interactable
    {
        // Beacons whose reward this player already asked for in this session.
        internal static readonly HashSet<ZDOID> Rewarded = new HashSet<ZDOID>();

        ZNetView nview;
        IsletTier tier;
        GameObject flames;
        float paidAt = -100f;

        void Awake()
        {
            nview = GetComponent<ZNetView>();
            if (nview == null || nview.GetZDO() == null) return;
            tier = IsletBeacons.TierOf(Utils.GetPrefabName(gameObject));
            Transform found = Find(transform, IsletBeacons.FlamesName);
            if (found != null) flames = found.gameObject;
            nview.Register(IsletBeacons.LightRpc, RPC_Light);
            InvokeRepeating("Check", 0.5f, 1f);
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform child in t)
            {
                Transform found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }

        bool Lit
        {
            get { return nview != null && nview.IsValid() && nview.GetZDO().GetBool(IsletBeacons.LitKey, false); }
        }

        // Flames follow the shared state; a player close to a lit beacon gets its reward once per session.
        void Check()
        {
            if (nview == null || !nview.IsValid()) return;
            bool lit = Lit;
            if (flames != null && flames.activeSelf != lit) flames.SetActive(lit);
            Player player = Player.m_localPlayer;
            if (!lit || player == null || Utils.DistanceXZ(player.transform.position, transform.position) > IsletBeacons.VisitRadius) return;
            if (Rewarded.Add(nview.GetZDO().m_uid)) IsletBeacons.RequestReward(transform.position);
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || tier == null || Lit || Time.time - paidAt < 3f) return false;
            Player player = user as Player;
            if (player == null || player != Player.m_localPlayer) return false;
            Cost[] option = IsletBeacons.Affordable(tier, player.GetInventory());
            if (option == null)
            {
                player.Message(MessageHud.MessageType.Center, "$iov_beacon_need " + IsletBeacons.CostText(tier), 0, null, false);
                player.Message(MessageHud.MessageType.TopLeft, "$iov_beacon_need " + IsletBeacons.CostText(tier), 0, null, false);
                return true;
            }
            foreach (Cost cost in option) player.GetInventory().RemoveItem(IsletBeacons.ItemName(cost.Item), cost.Amount, -1, true);
            paidAt = Time.time;
            IsletBeacons.Flared(player);
            nview.InvokeRPC(IsletBeacons.LightRpc);
            return true;
        }

        // On the owner: light it for good, and the wave comes, only the first time.
        void RPC_Light(long sender)
        {
            if (!nview.IsOwner() || Lit) return;
            nview.GetZDO().Set(IsletBeacons.LitKey, true);
            IsletBeacons.SpawnWave(tier, transform.position);
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }

        // The hud shows hover text as it is: it has to be translated here.
        public string GetHoverText()
        {
            if (tier == null) return "";
            if (Lit) return Localization.instance.Localize("$iov_beacon\n$iov_beacon_lit");
            return Localization.instance.Localize("$iov_beacon\n[<color=yellow><b>$KEY_Use</b></color>] $iov_beacon_light\n" + IsletBeacons.CostText(tier));
        }

        public string GetHoverName() { return Localization.instance.Localize("$iov_beacon"); }

        public float GetHoverOffset() { return 0f; }
    }
}
