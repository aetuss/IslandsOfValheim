using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The Valhalla tale's rewards (the user's, 2026-11-12), each the player's own, once a game day:
    // - the champion's oath (the ring's champion beaten: the world's key ValhallaDuel.WonKey): hard pressed (under
    //   a quarter of your health, a foe near), Starkaðr comes down in Valhalla's light at your side and fights
    //   your foes for a minute, or till none are left near, then goes back in it (beaten, he kneels and goes);
    // - the drowned's debt (Rán's net torn: RanNet.Done): your ship breaking up under you (under a quarter of her
    //   hull), the souls you freed rise from the sea round her and hold her together, mending her to half.
    // When each was last called is kept with the player (their custom data); while it rests till the next day,
    // an icon at the top of the screen counts it down. On the local player's client.
    class ValhallaGifts : MonoBehaviour
    {
        internal const string OathDay = "IoV_OathDay", DebtDay = "IoV_DebtDay";
        internal const string OathEffect = "IoV_SE_OathRests", DebtEffect = "IoV_SE_DebtRests";
        internal const string MendRpc = "IoV_DrownedMend";
        const float Pressed = 0.25f, OathStays = 60f, FoeNear = 20f;

        static int oathHash, debtHash;
        internal static ValhallaGifts Instance;

        internal static void Register()
        {
            oathHash = Rests(OathEffect, "iov_oath", new[] { "Battleaxe", "SwordIron" });
            debtHash = Rests(DebtEffect, "iov_debt", new[] { "TrophyDraugr", "TrophySerpent" });
        }

        // An icon that only counts down (no stats): the gift resting till the next day.
        static int Rests(string name, string token, string[] icons)
        {
            SE_Stats effect = SeafarerGear.Effect(name, token, false);
            foreach (string icon in icons)
            {
                GameObject item = PrefabManager.Instance.GetPrefab(icon);
                ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
                if (drop == null || drop.m_itemData.m_shared.m_icons == null || drop.m_itemData.m_shared.m_icons.Length == 0) continue;
                effect.m_icon = drop.m_itemData.m_shared.m_icons[0];
                break;
            }
            return name.GetStableHashCode();
        }

        void Awake() { Instance = this; }

        float next;

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 0.25f;
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || EnvMan.instance == null || ZNet.instance == null) return;
            Resting(player, OathDay, oathHash, Story.Key(ValhallaDuel.WonKey));
            Resting(player, DebtDay, debtHash, RanNet.Done);
            Oath(player);
            Debt(player);
        }

        // ---- once a day ----

        static int Today() { return EnvMan.instance.GetDay(); }

        static bool UsedToday(Player player, string key)
        {
            string day;
            return player.m_customData.TryGetValue(key, out day) && day == Today().ToString();
        }

        static void Use(Player player, string key) { player.m_customData[key] = Today().ToString(); }

        // Its icon while it rests, counting down to the next day (again after a reload: the icon isn't kept).
        static void Resting(Player player, string key, int hash, bool have)
        {
            SEMan effects = player.GetSEMan();
            if (effects == null || hash == 0) return;
            bool rests = have && UsedToday(player, key);
            if (!rests) { if (effects.HaveStatusEffect(hash)) effects.RemoveStatusEffect(hash, true); return; }
            if (effects.HaveStatusEffect(hash)) return;
            StatusEffect se = effects.AddStatusEffect(hash, true, 0, 0f, (short)0);
            if (se == null) return;
            double length = EnvMan.instance.m_dayLengthSec;
            se.m_ttl = Mathf.Max(1f, (float)((Today() + 1) * length - ZNet.instance.GetTimeSeconds()));
        }

        // (development: iov_gifts ready) both given back for today
        internal static void Ready(Player player)
        {
            player.m_customData.Remove(OathDay);
            player.m_customData.Remove(DebtDay);
        }

        // ---- the champion's oath ----

        void Oath(Player player)
        {
            if (!Story.Key(ValhallaDuel.WonKey) || ValhallaDuel.Active || UsedToday(player, OathDay)) return;
            if (player.GetHealth() > player.GetMaxHealth() * Pressed) return;
            Character foe = Foe(player, player.transform.position, FoeNear);
            if (foe == null) return;
            Use(player, OathDay);
            StartCoroutine(Comes(player, foe));
        }

        // The nearest of your foes (as the game's creatures tell them), alive, within reach of a place.
        internal static Character Foe(Player player, Vector3 near, float reach)
        {
            Character best = null;
            float closest = reach;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == player || c.IsDead() || !BaseAI.IsEnemy(player, c)) continue;
                float d = Vector3.Distance(c.transform.position, near);
                if (d < closest) { closest = d; best = c; }
            }
            return best;
        }

        // He comes down in the light beside you, between you and your foe, a little to the side.
        internal IEnumerator Comes(Player player, Character foe)
        {
            Vector3 towards = foe != null ? foe.transform.position - player.transform.position : player.transform.forward;
            towards.y = 0f;
            towards = towards.sqrMagnitude > 0.01f ? towards.normalized : Vector3.forward;
            Vector3 p = player.transform.position + towards * 1.8f + Vector3.Cross(Vector3.up, towards) * 1.4f;
            Vector3 at = BossKit.Ground(p + Vector3.up * 2f) ?? player.transform.position;
            LightBeam.Over(at, 3.4f, 1.8f, 0.9f, 2.2f, true);
            yield return new WaitForSeconds(1f);
            if (player == null) yield break;
            Character him = Starkadr(at, player, foe);
            if (him == null) yield break;
            ChampionAlly ally = him.gameObject.AddComponent<ChampionAlly>();
            ally.player = player;
            ally.until = Time.time + OathStays;
            if (Chat.instance != null) Chat.instance.SetNpcText(him.gameObject, Vector3.up * 2.4f, 30f, 4f, "", "$iov_oath_arrive", false);
            Plugin.Debug("Oath: Starkaðr at the player's side, " + him.GetMaxHealth().ToString("F0") + " health" + (foe != null ? ", against " + foe.m_name : ""));
        }

        // As he stands in the ring (his great weapon, the champion's war-gear and wolf cape, his hair and beard), as
        // strong as the world's tier, on your side.
        static Character Starkadr(Vector3 at, Player player, Character foe)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(EinherjarFighter.AllyPrefab) : null;
            Humanoid template = prefab != null ? prefab.GetComponent<Humanoid>() : null;
            if (template == null || ObjectDB.instance == null) { Plugin.Log.LogWarning("Oath: no " + EinherjarFighter.AllyPrefab); return null; }
            int tier = ValhallaDuel.Tier();
            string[] armour = ValhallaDuel.Armour[Mathf.Clamp(Mathf.Max(tier, 6), 0, ValhallaDuel.Armour.Length - 1)];
            List<GameObject> items = new List<GameObject>();
            foreach (string name in new[] { ValhallaDuel.Pick(ValhallaDuel.Great, tier), armour[0], armour[1], armour[2], "CapeWolf" })
            {
                GameObject item = string.IsNullOrEmpty(name) ? null : ObjectDB.instance.GetItemPrefab(name);
                if (item != null) items.Add(item);
            }
            template.m_defaultItems = items.ToArray();
            Traverse.Create(template).Field("m_hairItem").SetValue("Hair8");
            Traverse.Create(template).Field("m_beardItem").SetValue("Beard10");
            Vector3 look = (foe != null ? foe.transform.position : player.transform.position + player.transform.forward * 5f) - at;
            look.y = 0f;
            GameObject go = Instantiate(prefab, at + Vector3.up * 0.3f, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
            template.m_defaultItems = new GameObject[0];
            Character body = go.GetComponent<Character>();
            float health = ValhallaDuel.Health[Mathf.Clamp(tier, 0, ValhallaDuel.Health.Length - 1)] * 1.5f;
            body.SetMaxHealth(health);
            body.SetHealth(health);
            EinherjarFighter me = go.GetComponent<EinherjarFighter>();
            if (me != null) { me.modelWanted = 0; me.hairWanted = "Hair8"; me.beardWanted = "Beard10"; }
            VisEquipment vis = go.GetComponent<VisEquipment>();
            if (vis != null)
            {
                vis.SetModel(0);
                vis.SetHairColor(new Vector3(0.4f, 0.27f, 0.17f));
                vis.SetSkinColor(Vector3.one * 0.95f);
            }
            MonsterAI ai = go.GetComponent<MonsterAI>();
            if (ai != null) ai.SetFollowTarget(player.gameObject);
            ChampionAlly.Target(body, foe);
            return body;
        }

        // ---- the drowned's debt ----

        bool mending;

        void Debt(Player player)
        {
            if (!RanNet.Done || mending || UsedToday(player, DebtDay)) return;
            Ship ship = Ship.GetLocalShip();
            if (ship == null) return;
            WearNTear hull = ship.GetComponent<WearNTear>();
            ZNetView nview = ship.GetComponent<ZNetView>();
            if (hull == null || nview == null || !nview.IsValid()) return;
            float health = nview.GetZDO().GetFloat(ZDOVars.s_health, hull.m_health);
            if (health <= 0f || health > hull.m_health * Pressed) return;
            Use(player, DebtDay);
            StartCoroutine(Mend(player, ship, nview));
        }

        // Pale, out of the sea along both her sides, their hands on her; she's held and mended (her owner does it:
        // MendRpc), and they sink back.
        internal IEnumerator Mend(Player player, Ship ship, ZNetView nview)
        {
            mending = true;
            player.Message(MessageHud.MessageType.Center, "$iov_debt_rise");
            Bounds b = ShipBounds(ship);
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : ship.transform.position.y;
            List<SpiritHuman> souls = new List<SpiritHuman>();
            List<float> depth = new List<float>();
            int n = 0;
            foreach (float side in new[] { -1f, 1f })
                foreach (float along in new[] { -0.3f, 0.3f })
                {
                    Vector3 local = new Vector3(side * (b.extents.x + 0.35f), 0f, b.center.z + along * b.size.z);
                    Vector3 at = ship.transform.TransformPoint(local);
                    at.y = water - 1.8f;
                    Vector3 face = ship.transform.position - at;
                    face.y = 0f;
                    SpiritHuman soul = SpiritHuman.MakeLocal(ValhallaPeople.BrimFolk(n++), SpiritHuman.Stand, "", SpiritHuman.GhostSoft, null, at,
                        Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : Vector3.forward));
                    if (soul == null) continue;
                    souls.Add(soul);
                    depth.Add(local.x);
                    BossKit.Effect("vfx_WaterImpact_Karve", new Vector3(at.x, water, at.z));
                }
            // up to the waist out of the water, keeping with her as she drifts
            for (float t = 0f; t < 4.5f; t += Time.deltaTime)
            {
                if (ship == null) break;
                float rise = Mathf.Clamp01(t / 1.4f);
                for (int i = 0; i < souls.Count; i++)
                {
                    if (souls[i] == null) continue;
                    Vector3 p = souls[i].transform.position;
                    p.y = water - 1.8f + 1.0f * rise - (t > 3.6f ? (t - 3.6f) * 1.6f : 0f);
                    souls[i].transform.position = p;
                    souls[i].Detail(Vector3.Distance(Utils.GetMainCamera() != null ? Utils.GetMainCamera().transform.position : p, p));
                }
                if (t >= 1.5f && t - Time.deltaTime < 1.5f && nview != null && nview.IsValid())
                {
                    nview.InvokeRPC(MendRpc);
                    BossKit.Effect("vfx_HealthUpgrade", ship.transform.position);
                    Plugin.Debug("Debt: the drowned hold the ship");
                }
                yield return null;
            }
            foreach (SpiritHuman soul in souls) if (soul != null) Destroy(soul.gameObject);
            mending = false;
        }

        // A ship's size in her own space (her meshes measured there).
        static Bounds ShipBounds(Ship ship)
        {
            Bounds b = new Bounds(Vector3.zero, new Vector3(2f, 1f, 6f));
            bool any = false;
            Matrix4x4 into = ship.transform.worldToLocalMatrix;
            foreach (MeshRenderer r in ship.GetComponentsInChildren<MeshRenderer>())
            {
                MeshFilter m = r.GetComponent<MeshFilter>();
                if (m == null || m.sharedMesh == null) continue;
                Bounds mb = m.sharedMesh.bounds;
                Matrix4x4 to = into * m.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = to.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                    if (!any) { b = new Bounds(c, Vector3.zero); any = true; }
                    else b.Encapsulate(c);
                }
            }
            // (a ship's mast and sail spread wider than her hull: no wider than her length allows)
            b.extents = new Vector3(Mathf.Min(b.extents.x, Mathf.Max(1f, b.extents.z * 0.35f)), b.extents.y, b.extents.z);
            return b;
        }

        // Every client's ships answer it; her owner mends her to half (no lower than she is) and tells everyone.
        [HarmonyPatch(typeof(Ship), "Awake")]
        static class MendRpcPatch
        {
            static void Postfix(Ship __instance)
            {
                ZNetView nview = __instance.GetComponent<ZNetView>();
                WearNTear hull = __instance.GetComponent<WearNTear>();
                if (nview == null || hull == null || nview.GetZDO() == null) return;
                nview.Register(MendRpc, (long sender) =>
                {
                    if (!nview.IsValid() || !nview.IsOwner()) return;
                    float now = nview.GetZDO().GetFloat(ZDOVars.s_health, hull.m_health);
                    float want = Mathf.Max(now, hull.m_health * 0.5f);
                    nview.GetZDO().Set(ZDOVars.s_health, want);
                    nview.InvokeRPC(ZNetView.Everybody, "RPC_HealthChanged", want);
                });
            }
        }
    }

    // Starkaðr at your side: he follows you and goes for your foes; after his minute, or once none are left near
    // you for a while, he goes back in the light. (Beaten, he kneels and goes: EinherjarFighter.)
    class ChampionAlly : MonoBehaviour
    {
        internal Player player;
        internal float until;
        float since, lastFoe, nextLook;
        bool leaving;
        Character body;
        EinherjarFighter fighter;

        void Start()
        {
            body = GetComponent<Character>();
            fighter = GetComponent<EinherjarFighter>();
            since = lastFoe = Time.time;
        }

        void Update()
        {
            ZNetView nview = GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner() || leaving) return;
            if (fighter != null && fighter.yielded) { enabled = false; return; }
            if (player == null || player.IsDead() || Vector3.Distance(player.transform.position, transform.position) > 60f) { Leave(""); return; }
            if (Time.time >= nextLook)
            {
                nextLook = Time.time + 1f;
                Character foe = ValhallaGifts.Foe(player, player.transform.position, 25f);
                if (foe != null)
                {
                    lastFoe = Time.time;
                    MonsterAI ai = GetComponent<MonsterAI>();
                    Character now = ai != null ? Traverse.Create(ai).Field("m_targetCreature").GetValue<Character>() : null;
                    if (now == null || now.IsDead()) Target(body, foe);
                }
            }
            if (Time.time >= until) { Leave("$iov_oath_leave"); return; }
            if (Time.time - since > 8f && Time.time - lastFoe > 6f) Leave("$iov_oath_leave");
        }

        // Its foe: roused, and knowing where it is.
        internal static void Target(Character body, Character foe)
        {
            MonsterAI ai = body != null ? body.GetComponent<MonsterAI>() : null;
            if (ai == null || foe == null) return;
            Traverse t = Traverse.Create(ai);
            t.Field("m_targetCreature").SetValue(foe);
            t.Field("m_lastKnownTargetPos").SetValue(foe.transform.position);
            t.Method("SetAlerted", new[] { typeof(bool) }).GetValue(true);
        }

        void Leave(string line)
        {
            leaving = true;
            if (line.Length > 0 && Chat.instance != null) Chat.instance.SetNpcText(gameObject, Vector3.up * 2.4f, 30f, 4f, "", line, false);
            StartCoroutine(Going());
        }

        IEnumerator Going()
        {
            MonsterAI ai = GetComponent<MonsterAI>();
            if (ai != null) ai.enabled = false;
            if (body != null) body.SetMoveDir(Vector3.zero);
            yield return new WaitForSeconds(1.5f);
            LightBeam.Over(transform.position, 3f, 1.8f, 0.9f, 2f, true);
            yield return new WaitForSeconds(1f);
            ZNetView nview = GetComponent<ZNetView>();
            if (nview != null && nview.IsValid() && nview.IsOwner() && ZNetScene.instance != null) ZNetScene.instance.Destroy(gameObject);
        }
    }
}
