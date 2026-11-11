using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The Valhalla arc's third task (T3): Rán's net. Most of the Brim Folk drowned cursed, and Rán holds them in
    // her net, far out in the Deep North waters where their fleet went down. Once the captains are all free (T2),
    // Sævar gives you a chart of the place. Out there, among the masts of the sunken fleet, Hafgufa rises: Rán's
    // beast, a great dark serpent, kept by her net while its three glowing knots float on the water. Break the
    // knots from your boat, then the beast can be hurt. Half dead, it wraps the net round your hull (your ship
    // slows) and the drowned rise as pale ghosts. When it dies the net tears and the souls go up as light.
    // The beast can hurt your ship, but never sink it: near the graveyard no ship falls below a fifth of its hull.
    //
    // Everything is the world's: the done key. The client that owns the graveyard's marker (GraveyardStand) brings
    // the beast and the knots; every client shows the souls, the mist, the words.
    static class RanNet
    {
        internal const string Graveyard = "IoV_Story_Graveyard";
        internal const string Stand = "IoV_Story_GraveyardStand";
        internal const string Map = "IoV_StoryMapGraveyard";
        internal const string Hafgufa = "IoV_Hafgufa";
        internal const string Buoy = "IoV_RanBuoy";
        internal const string Drowned = "IoV_DrownedSoul";
        internal const string SoulBeam = "IoV_SoulBeam";
        internal const string EndBeam = "IoV_HafgufaEndBeam";
        internal const string LastLight = "IoV_HafgufaLastLight";

        // The world's keys (lower case; none starts with iov_, which the build reads as a text's key).
        internal const string DoneKey = "iovt3done";
        // The player's (seen): Sævar told them of the net.
        const string ToldKey = "iovt3told";

        internal const float Reach = 75f;       // how near the graveyard's middle a player brings the beast
        internal const float Area = 180f;       // the graveyard's waters: mist, the ship kept afloat

        internal static bool Begun { get { return TaleCaptains.AllFreed; } }
        internal static bool Done { get { return Story.Key(DoneKey); } }

        internal static void Register()
        {
            RegisterHafgufa();
            RegisterBuoy();
            RegisterDrowned();
            BossKit.RegisterBeam(SoulBeam, 2.5f, 0.7f, 1.2f); // a drowned soul comes up out of the sea in it
            BossKit.RegisterBeam(EndBeam, 4f, 3.5f, 1.8f);    // where Hafgufa falls (8 s was too long)
            BossKit.RegisterLight(LastLight, SoulColour, 30f, 5f, 8f);
            GameObject stand = PrefabManager.Instance.CreateEmptyPrefab(Stand, true);
            Object.DestroyImmediate(stand.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(stand.GetComponent<MeshFilter>());
            Object.DestroyImmediate(stand.GetComponent<Collider>());
            stand.GetComponent<ZNetView>().m_persistent = true;
            stand.AddComponent<GraveyardStand>();
            PrefabManager.Instance.AddPrefab(new CustomPrefab(stand, false));
        }

        // How many times the game's serpent (about 30 m; 3.5 was too big to fight from a ship: user, 2026-11-11).
        const float Size = 2.5f;

        // Hafgufa: the game's serpent, two and a half times its size, in the colours of the deep, its eyes alight.
        // Its bites reach as far as it is big (a serpent's own reach, from so big a body, never got to a ship), and
        // it never swims off to lick its wounds.
        static void RegisterHafgufa()
        {
            if (PrefabManager.Instance.GetPrefab("Serpent") == null) { Plugin.Log.LogWarning("No Serpent: Hafgufa is left out."); return; }
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(Hafgufa, "Serpent");
            go.transform.localScale = go.transform.localScale * Size;
            Character body = go.GetComponent<Character>();
            if (body != null)
            {
                body.m_name = "$iov_hafgufa";
                body.m_boss = true;
                body.m_bossEvent = "";
                body.m_health = 3000f;
                body.m_swimSpeed *= 1.3f;
                // never staggered: arrows from a ship stopped it for 2-3 s each time
                body.m_staggerDamageFactor = 0f;
            }
            Humanoid humanoid = go.GetComponent<Humanoid>();
            if (humanoid != null && humanoid.m_defaultItems != null)
            {
                GameObject[] items = (GameObject[])humanoid.m_defaultItems.Clone();
                for (int i = 0; i < items.Length; i++) items[i] = LongerBite(items[i]);
                humanoid.m_defaultItems = items;
            }
            BaseAI ai = go.GetComponent<BaseAI>();
            if (ai != null)
            {
                ai.m_spawnMessage = "";
                ai.m_deathMessage = "";
                Field(ai, "m_viewRange", 90f); // (it lay off at the graveyard's edge, never seeing you come)
            }
            MonsterAI monster = go.GetComponent<MonsterAI>();
            if (monster != null)
            {
                Field(monster, "m_alertRange", 90f);
                Field(monster, "m_fleeIfHurtWhenTargetCantBeReached", false);
                Field(monster, "m_fleeIfNotAlerted", false);
                Field(monster, "m_fleeIfLowHealth", 0f);
                Field(monster, "m_fleeTimeSinceHurt", 0f);
                Field(monster, "m_maxChaseDistance", 0f); // (0: it chases as long as it can see you)
            }
            CharacterDrop drops = go.GetComponent<CharacterDrop>();
            if (drops != null) drops.m_drops.Clear(); // (what it leaves comes later, with the arc's rewards)
            BossKit.Tint(go, BossKit.Cast(new Color(0.05f, 0.12f, 0.16f), 0.75f, 0.75f), 0.4f, "Hafgufa");
            Eyes(go);
            try { HafgufaLooks.Dress(go); }
            catch (System.Exception e) { Plugin.Log.LogWarning("Hafgufa's looks: " + e); }
            go.AddComponent<HafgufaFight>();
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
            Plugin.Debug("Hafgufa made from Serpent");
        }

        // One of the serpent's bites, cloned, reaching as far as Hafgufa is big; it starts one from a little nearer
        // than that (from as far as it reaches, it bit at the air). Its roar (a long reach of its own) keeps its reach.
        const float StartBite = 2f;

        static GameObject LongerBite(GameObject item)
        {
            if (item == null || item.GetComponent<ItemDrop>() == null) return item;
            if (item.GetComponent<ItemDrop>().m_itemData.m_shared.m_aiAttackRange > 10f) return SlowerTaunt(item);
            string name = "IoV_Hafgufa_" + item.name;
            GameObject clone = PrefabManager.Instance.GetPrefab(name);
            if (clone == null)
            {
                clone = PrefabManager.Instance.CreateClonedPrefab(name, item);
                ItemDrop.ItemData.SharedData shared = clone.GetComponent<ItemDrop>().m_itemData.m_shared;
                string before = shared.m_aiAttackRange.ToString("F1") + "/" + (shared.m_attack != null ? shared.m_attack.m_attackRange.ToString("F1") : "-");
                shared.m_aiAttackRange *= StartBite;
                shared.m_aiAttackRangeMin *= StartBite;
                if (shared.m_attack != null)
                {
                    shared.m_attack.m_attackRange *= Size;
                    shared.m_attack.m_attackHeight *= Size;
                    shared.m_attack.m_attackOffset *= Size;
                    shared.m_attack.m_attackRayWidth *= Size;
                }
                ItemManager.Instance.AddItem(new CustomItem(clone, false));
                Plugin.Debug("Hafgufa's " + item.name + ": reach " + before + " -> " + shared.m_aiAttackRange.ToString("F1") + "/" + (shared.m_attack != null ? shared.m_attack.m_attackRange.ToString("F1") : "-"));
            }
            return clone;
        }

        // Its roar (the serpent's taunt, used from far off): it stops still for it, which looked like a stagger each
        // time an arrow hit (it roared back at whoever shot it from out of its bite's reach). Now and then only.
        static GameObject SlowerTaunt(GameObject item)
        {
            string name = "IoV_Hafgufa_" + item.name;
            GameObject clone = PrefabManager.Instance.GetPrefab(name);
            if (clone != null) return clone;
            clone = PrefabManager.Instance.CreateClonedPrefab(name, item);
            ItemDrop.ItemData.SharedData shared = clone.GetComponent<ItemDrop>().m_itemData.m_shared;
            float before = shared.m_aiAttackInterval;
            shared.m_aiAttackInterval = Mathf.Max(shared.m_aiAttackInterval, 35f);
            shared.m_aiPrioritized = false;
            ItemManager.Instance.AddItem(new CustomItem(clone, false));
            Plugin.Debug("Hafgufa's " + item.name + ": once every " + before.ToString("F0") + " s -> " + shared.m_aiAttackInterval.ToString("F0") + " s at most");
            return clone;
        }

        internal static void Field(object on, string name, object value)
        {
            System.Reflection.FieldInfo f = AccessTools.Field(on.GetType(), name);
            if (f == null) { Plugin.Debug("Hafgufa: no " + name); return; }
            Plugin.Debug("Hafgufa: " + name + " " + f.GetValue(on) + " -> " + value);
            f.SetValue(on, value);
        }

        // A cold light at its head (the game's glow child, so its fight can make it pulse).
        static void Eyes(GameObject go)
        {
            Transform head = null;
            List<string> bones = new List<string>();
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Renderer>() != null) continue;
                bones.Add(t.name);
                if (head == null && t.name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0) head = t;
            }
            Plugin.Debug("Hafgufa's bones: " + string.Join(", ", bones.ToArray()));
            GameObject glow = new GameObject(BossKit.GlowChild);
            glow.transform.SetParent(head != null ? head : go.transform, false);
            glow.transform.localPosition = head != null ? Vector3.zero : new Vector3(0f, 1f, 2f);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = SoulColour;
            light.range = 8f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            Plugin.Debug("Hafgufa's eyes on " + (head != null ? head.name : "its root"));
        }

        internal static readonly Color SoulColour = new Color(0.55f, 0.92f, 1f);

        // A knot of Rán's net: a dark float with a cold light in it, on the water, broken by blows like a barrel.
        static void RegisterBuoy()
        {
            string from = PrefabManager.Instance.GetPrefab("dvergrprops_barrel") != null ? "dvergrprops_barrel" : "barrell";
            if (PrefabManager.Instance.GetPrefab(from) == null) { Plugin.Log.LogWarning("No barrel: Rán's knots are left out."); return; }
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(Buoy, from);
            go.transform.localScale = go.transform.localScale * 1.6f;
            foreach (DropOnDestroyed drop in go.GetComponentsInChildren<DropOnDestroyed>(true)) Object.DestroyImmediate(drop);
            foreach (Floating f in go.GetComponentsInChildren<Floating>(true)) Object.DestroyImmediate(f);
            foreach (ZSyncTransform sync in go.GetComponentsInChildren<ZSyncTransform>(true)) Object.DestroyImmediate(sync);
            foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            Destructible destructible = go.GetComponent<Destructible>();
            if (destructible != null)
            {
                destructible.m_health = 300f;
                destructible.m_minToolTier = 0;
                destructible.m_spawnWhenDestroyed = null;
            }
            ZNetView nview = go.GetComponent<ZNetView>();
            if (nview != null) nview.m_persistent = true;
            HoverText hover = go.AddComponent<HoverText>();
            hover.m_text = "$iov_ranbuoy";
            BossKit.Tint(go, BossKit.Cast(new Color(0.08f, 0.2f, 0.24f), 0.7f, 0.8f), 0.3f, "Rán's knot");
            GameObject glow = new GameObject("IoV_KnotGlow");
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = Vector3.up * 1.2f;
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = SoulColour;
            light.range = 9f;
            light.intensity = 2.6f;
            light.shadows = LightShadows.None;
            go.AddComponent<RanKnot>();
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // The drowned: the game's wraith, pale as sea foam, leaving nothing.
        static void RegisterDrowned()
        {
            if (PrefabManager.Instance.GetPrefab("Wraith") == null) { Plugin.Log.LogWarning("No Wraith: the drowned are left out."); return; }
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(Drowned, "Wraith");
            Character body = go.GetComponent<Character>();
            if (body != null)
            {
                body.m_name = "$iov_drowned";
                body.m_faction = Character.Faction.SeaMonsters; // Rán's, as Hafgufa is (as the dead, they fought it)
            }
            CharacterDrop drops = go.GetComponent<CharacterDrop>();
            if (drops != null) drops.m_drops.Clear();
            MonsterAI ai = go.GetComponent<MonsterAI>();
            if (ai != null) Field(ai, "m_despawnInDay", false); // (by day as well as by night; the setter needs a live network view)
            BossKit.Tint(go, BossKit.Cast(new Color(0.7f, 0.92f, 1f), 0.6f, 1.15f), 0f, "the drowned");
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // ---- Sævar ----

        // What Sævar says once the captains are all free: the net (a page, and the chart) the first time; then a
        // reminder (and the chart again if it's been lost); once the net is torn, his thanks.
        internal static string SaevarLine(Player player)
        {
            if (Done) return "$iov_valsaevar_t3done";
            if (!player.HaveSeenTutorial(ToldKey))
            {
                player.SetSeenTutorial(ToldKey);
                Story.Tell(player, "$iov_t3_page", "$iov_t3_page_text");
                Story.Give(player, Map);
                return "$iov_valsaevar_t3";
            }
            player.AddKnownText("$iov_t3_page", "$iov_t3_page_text");
            if (!Story.Has(player, Map)) Story.Give(player, Map);
            return "$iov_valsaevar_t3remind";
        }

        // ---- the ship is never lost here ----

        // Near the graveyard a ship's hull never falls below a fifth: the beast can break it, not sink it.
        [HarmonyPatch(typeof(WearNTear), "ApplyDamage")]
        static class KeepAfloatPatch
        {
            static void Prefix(WearNTear __instance, ref float damage)
            {
                if (damage <= 0f || __instance.GetComponent<Ship>() == null || !GraveyardStand.Near(__instance.transform.position, Area)) return;
                ZNetView view = __instance.GetComponent<ZNetView>();
                if (view == null || !view.IsValid()) return;
                float health = view.GetZDO().GetFloat(ZDOVars.s_health, __instance.m_health);
                float floor = __instance.m_health * 0.2f;
                float most = Mathf.Max(0f, health - floor);
                if (damage > most)
                {
                    damage = most;
                    Plugin.Debug("T3: the ship kept afloat at " + Mathf.RoundToInt(Mathf.Max(health - most, 0f)) + " of " + Mathf.RoundToInt(__instance.m_health));
                }
            }
        }

        // ---- nothing pushes Hafgufa back ----

        // Every arrow pushed it (a serpent's light body, however big it's drawn), and while pushed it couldn't swim:
        // it stopped for a moment at each hit. (The log showed it wasn't its roar: it bit, it didn't roar.)
        [HarmonyPatch(typeof(Character), "ApplyPushback", new[] { typeof(Vector3), typeof(float) })]
        static class NoPushbackPatch
        {
            static bool Prefix(Character __instance)
            {
                return __instance == null || __instance.GetComponent<HafgufaFight>() == null;
            }
        }

        // ---- a knot breaks in three or four blows, whatever they are ----

        // (By the barrel's own health and resistances an arrow did too little; now each blow takes a share of it.)
        [HarmonyPatch(typeof(Destructible), "Damage")]
        static class KnotPatch
        {
            static void Prefix(Destructible __instance, HitData hit)
            {
                if (__instance == null || hit == null || __instance.GetComponent<RanKnot>() == null) return;
                float share = Random.Range(0.26f, 0.36f) * __instance.m_health;
                hit.m_damage = new HitData.DamageTypes();
                hit.m_damage.m_damage = share;
                hit.m_toolTier = (short)100;
            }
        }

        // ---- while the net holds, the beast can't be hurt ----

        static float shieldSaidAt = -100f;

        [HarmonyPatch(typeof(Character), "Damage")]
        static class NetShieldPatch
        {
            static bool Prefix(Character __instance, HitData hit)
            {
                if (__instance == null || __instance.GetComponent<HafgufaFight>() == null) return true;
                if (!RanKnot.AnyNear(__instance.transform.position, 250f)) return true;
                Character attacker = hit != null ? hit.GetAttacker() : null;
                if (attacker == Player.m_localPlayer && Time.time - shieldSaidAt > 6f)
                {
                    shieldSaidAt = Time.time;
                    RanVoice.SayIfQuiet(Player.m_localPlayer, "$iov_ran_shielded", 4f);
                }
                return false;
            }
        }

        // ---- development ----

        internal static void Dev(string what, Terminal context)
        {
            Player player = Player.m_localPlayer;
            switch (what)
            {
                case "done":
                    Story.Set(DoneKey);
                    break;
                case "reset":
                    if (ZoneSystem.instance != null) ZoneSystem.instance.RemoveGlobalKey(DoneKey);
                    if (player != null) RemoveSeen(player, ToldKey);
                    GraveyardStand.ClearAll();
                    GraveyardStand.resetPending = true; // (a graveyard not loaded now forgets too, when it next loads)
                    break;
                case "start":
                    if (GraveyardStand.Current == null) { context.AddString("Not near the graveyard (iov_goto graveyard)."); return; }
                    GraveyardStand.Current.Rise(true);
                    break;
                case "map":
                    if (player != null) Story.Give(player, Map);
                    break;
                case "boat":
                    GameObject ship = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("VikingShip") : null;
                    if (ship != null && player != null)
                    {
                        Vector3 at = player.transform.position + player.transform.forward * 8f;
                        at.y = ZoneSystem.instance.m_waterLevel;
                        Object.Instantiate(ship, at, Quaternion.LookRotation(player.transform.forward));
                    }
                    break;
                default:
                    context.AddString("iov_t3 start | reset | done | map | boat");
                    return;
            }
            context.AddString("T3 " + what + ": " + (Done ? "the net is torn" : Begun ? "the net holds (T2 done)" : "not begun (T2 not done)"));
        }

        static void RemoveSeen(Player player, string key)
        {
            System.Reflection.FieldInfo field = AccessTools.Field(typeof(Player), "m_shownTutorials");
            HashSet<string> seen = field != null ? field.GetValue(player) as HashSet<string> : null;
            if (seen != null) seen.Remove(key);
        }
    }

    // A knot of the net, kept in a list of those loaded (so the beast's shield and the stand can count them).
    class RanKnot : MonoBehaviour
    {
        static readonly List<RanKnot> loaded = new List<RanKnot>();

        void OnEnable() { loaded.Add(this); }
        void OnDisable() { loaded.Remove(this); }

        internal static bool AnyNear(Vector3 point, float range)
        {
            foreach (RanKnot k in loaded)
                if (k != null && Utils.DistanceXZ(k.transform.position, point) < range) return true;
            return false;
        }

        internal static int CountNear(Vector3 point, float range)
        {
            int n = 0;
            foreach (RanKnot k in loaded)
                if (k != null && Utils.DistanceXZ(k.transform.position, point) < range) n++;
            return n;
        }

        internal static List<RanKnot> Near(Vector3 point, float range)
        {
            List<RanKnot> result = new List<RanKnot>();
            foreach (RanKnot k in loaded)
                if (k != null && Utils.DistanceXZ(k.transform.position, point) < range) result.Add(k);
            return result;
        }
    }

    // Hafgufa's fight: one stage, at half its health, when Rán's net takes hold of your ship (GraveyardStand does
    // that) and a storm comes over; its eyes burn brighter. Its bites land harder than a serpent's.
    class HafgufaFight : BossFight
    {
        static readonly float[] stages = { 0.5f };
        protected override string StageKey { get { return "IoV_HafgufaStage"; } }
        protected override float[] Thresholds { get { return stages; } }
        protected override string LineFor(int stage) { return stage == 1 ? "$iov_ran_net" : null; }
        internal int CurrentStage { get { return Stage; } }

        protected override void Everywhere(int stage)
        {
            Shine(stage >= 1, 3f, 8f);
            Storm(stage >= 1 && LocalPlayerWithin(RanNet.Area));
        }

        internal override float HurtScale(HitData hit) { return 1.8f; }

        // (logged, to see what makes it stop: its roar, or something else)
        internal override void AttackStarted()
        {
            Humanoid h = body as Humanoid;
            ItemDrop.ItemData weapon = h != null ? h.GetCurrentWeapon() : null;
            Plugin.Debug("T3: Hafgufa attacks with " + (weapon != null && weapon.m_dropPrefab != null ? weapon.m_dropPrefab.name : weapon != null ? weapon.m_shared.m_name : "?"));
        }

        // Its death (on the client that ran it): the net is torn, for the world.
        // Its end, seen by everyone: a burst of the sea where it falls, a great pale light going up through it, and
        // the cold light of its eyes fading slowly over the water.
        protected override string LastLight { get { return RanNet.LastLight; } }

        internal override void Fallen()
        {
            base.Fallen();
            Story.Set(RanNet.DoneKey);
            Vector3 at = transform.position;
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : at.y;
            Vector3 surface = new Vector3(at.x, water, at.z);
            BossKit.Effect("vfx_serpent_death", surface);
            for (int i = 0; i < 4; i++)
                BossKit.Effect("vfx_WaterImpact_VikingShip", surface + transform.forward * (i * 6f - 6f));
            GameObject beam = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(RanNet.EndBeam) : null;
            if (beam != null) Instantiate(beam, surface, Quaternion.identity);
            // and heard: the souls let go (the frost king's spirits' release), a deep note under it
            BossKit.Effect("sfx_frozenking_spirit_death", surface);
            BossKit.Effect("sfx_deepnorth_gate_activate", surface);
            Plugin.Debug("T3: Hafgufa falls; the net is torn");
        }
    }

    // The graveyard's marker, at its middle on the water. Every client: the souls' cold lights under the water and
    // their wisps rising, a mist over the place, the words when the net breaks and when it's torn, the net's hold on
    // your own ship. The client that owns it: Hafgufa and the knots, and the drowned in the second stage.
    class GraveyardStand : MonoBehaviour
    {
        static readonly List<GraveyardStand> loaded = new List<GraveyardStand>();
        internal static bool resetPending; // (development: iov_t3 reset, for a graveyard not loaded at the time)
        internal static GraveyardStand Current
        {
            get
            {
                Player p = Player.m_localPlayer;
                foreach (GraveyardStand s in loaded)
                    if (s != null && p != null && Utils.DistanceXZ(s.transform.position, p.transform.position) < RanNet.Area) return s;
                return null;
            }
        }

        internal static bool Near(Vector3 point, float range)
        {
            foreach (GraveyardStand s in loaded)
                if (s != null && Utils.DistanceXZ(s.transform.position, point) < range) return true;
            return false;
        }

        const float Search = 220f;      // the beast this far from the middle still counts as here
        const float KnotRadius = 26f;   // the knots float this far out from the middle

        ZNetView nview;
        GameObject souls;
        bool rising, wasDone, sawKnots, mist;
        float nextWaiting, nextDrowned;
        int lastStage = -1;

        void Awake()
        {
            nview = GetComponent<ZNetView>();
            if (nview == null || nview.GetZDO() == null) return;
            loaded.Add(this);
            if (resetPending && nview.IsOwner())
            {
                resetPending = false;
                nview.GetZDO().Set(DoneHere, false);
                nview.GetZDO().Set(BeastHere, ZDOID.None);
            }
            wasDone = RanNet.Done;
            MakeSouls();
            InvokeRepeating("Tick", 5f, 1f);
        }

        void OnDestroy()
        {
            loaded.Remove(this);
            Mist(false);
        }

        Vector3 Middle { get { return new Vector3(transform.position.x, ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : transform.position.y, transform.position.z); } }

        // The graveyard keeps its own word that the net is torn, beside the world's key (which once went missing
        // in a test, and a new Hafgufa rose): either one is enough, and the key is set again from it.
        const string DoneHere = "IoV_T3Done";
        const string BeastHere = "IoV_T3Beast"; // the one Hafgufa that rose here (its ZDO), so no second rises

        bool Done
        {
            get
            {
                bool here = nview != null && nview.IsValid() && nview.GetZDO().GetBool(DoneHere, false);
                if (RanNet.Done)
                {
                    if (!here && nview != null && nview.IsValid() && nview.IsOwner()) nview.GetZDO().Set(DoneHere, true);
                    return true;
                }
                if (here)
                {
                    Plugin.Log.LogWarning("T3: the world's key was gone, but the graveyard knows the net is torn; set again");
                    Story.Set(RanNet.DoneKey);
                }
                return here;
            }
        }

        void Tick()
        {
            if (ZNetScene.instance == null || nview == null || !nview.IsValid()) return;
            Player local = Player.m_localPlayer;
            bool done = Done;
            Character beast = Find();
            HafgufaFight fight = beast != null ? beast.GetComponent<HafgufaFight>() : null;
            int knots = RanKnot.CountNear(Middle, Search);
            bool here = local != null && Utils.DistanceXZ(local.transform.position, Middle) < RanNet.Area;

            // the end, seen by everyone here: the souls go up as light
            if (done && !wasDone)
            {
                wasDone = true;
                if (here) StartCoroutine(Freed());
            }
            if (souls != null && done && !rising) souls.SetActive(false);

            Mist(here && !done && (fight == null || fight.CurrentStage < 1));

            // the knots all broken while the beast lives: Rán's word, once
            if (beast != null && knots > 0) sawKnots = true;
            if (beast != null && knots == 0 && sawKnots)
            {
                sawKnots = false;
                if (here) RanVoice.Say(local, "$iov_ran_knotsbroken", 5f);
            }
            int stage = fight != null ? fight.CurrentStage : -1;
            // (not as it dies: its last blow takes it past half and to nothing at once)
            if (stage >= 1 && lastStage == 0 && here && !done && beast.GetHealth() > 0f)
                RanVoice.Say(local, "$iov_ran_hull", 5f);
            lastStage = stage;

            if (done) return;
            KeepNear(beast);
            if (!RanNet.Begun)
            {
                // the captains aren't free yet: only a heaviness on the water
                if (here && Utils.DistanceXZ(local.transform.position, Middle) < RanNet.Reach && Time.time >= nextWaiting)
                {
                    nextWaiting = Time.time + 90f;
                    RanVoice.Say(local, "$iov_t3_waiting", 5f);
                }
                return;
            }
            if (!nview.IsOwner()) return;
            if (beast == null)
            {
                if (rising) return;
                foreach (Player player in Player.GetAllPlayers())
                    if (player != null && Utils.DistanceXZ(player.transform.position, Middle) < RanNet.Reach)
                    {
                        // the one that rose before is still in the world, out of sight: it's called back, not another
                        if (CallBack()) return;
                        Rise(false);
                        return;
                    }
                return;
            }
            // the second stage: the drowned come up round whoever is near, two at a time, never more than three
            // about at once (counted over the whole graveyard: those off fighting elsewhere count too)
            if (stage >= 1 && Time.time >= nextDrowned)
            {
                nextDrowned = Time.time + 25f;
                Player near = Player.GetClosestPlayer(beast.transform.position, 120f);
                int about = CountDrowned(Middle, Search);
                if (near != null && about < 3) StartCoroutine(DrownedRise(near.transform.position, Mathf.Min(2, 3 - about)));
            }
        }

        // Hafgufa strays no farther than this from the middle: past it, on the game that runs it, it's taken back
        // under the water to the graveyard's edge (unseen: it's deep and far from whoever it chased).
        const float Leash = 110f;

        void KeepNear(Character beast)
        {
            if (beast == null || beast.IsDead()) return;
            ZNetView view = beast.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            Vector3 middle = Middle, p = beast.transform.position;
            Vector3 out_ = new Vector3(p.x - middle.x, 0f, p.z - middle.z);
            if (out_.magnitude < Leash) return;
            Vector3 back = middle + out_.normalized * 40f;
            back.y = middle.y - 6f;
            beast.transform.position = back;
            Rigidbody body = beast.GetComponent<Rigidbody>();
            if (body != null) { body.position = back; body.velocity = Vector3.zero; }
            Plugin.Debug("T3: Hafgufa strayed " + Mathf.RoundToInt(out_.magnitude) + " m; back to the graveyard");
        }

        // The Hafgufa that rose here, if it's still in the world but not loaded (it strayed out of reach): moved back
        // under the graveyard, where it comes into the world again. True if there is one.
        bool CallBack()
        {
            ZDOID id = nview.GetZDO().GetZDOID(BeastHere);
            if (id == ZDOID.None || ZDOMan.instance == null) return false;
            ZDO zdo = ZDOMan.instance.GetZDO(id);
            if (zdo == null) return false;
            Vector3 back = Middle + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * 40f;
            back.y = Middle.y - 6f;
            if (Utils.DistanceXZ(zdo.GetPosition(), Middle) > 60f)
            {
                zdo.SetOwner(ZDOMan.GetSessionID());
                zdo.SetPosition(back);
                Plugin.Debug("T3: the Hafgufa that rose before is called back");
            }
            return true;
        }

        Character Find()
        {
            foreach (Character c in Character.GetAllCharacters())
                if (c != null && !c.IsDead() && Utils.GetPrefabName(c.gameObject) == RanNet.Hafgufa &&
                    Utils.DistanceXZ(c.transform.position, Middle) < Search) return c;
            return null;
        }

        static int CountDrowned(Vector3 at, float range)
        {
            int n = 0;
            foreach (Character c in Character.GetAllCharacters())
                if (c != null && !c.IsDead() && Utils.GetPrefabName(c.gameObject) == RanNet.Drowned && Utils.DistanceXZ(c.transform.position, at) < range) n++;
            return n;
        }

        // How many of the drowned have come up in this fight: the first is the strongest (two stars), the second has
        // one, the rest none (the user's wish).
        int soulsCome;

        // The knots come up first (if they aren't there already), then the beast, from the deep, at the far side
        // of the graveyard from whoever came. (force: development, even before the captains are free.)
        internal void Rise(bool force)
        {
            if (rising || nview == null || !nview.IsValid()) return;
            if (!force && (Done || !RanNet.Begun)) return;
            if (!nview.IsOwner()) nview.ClaimOwnership();
            StartCoroutine(Rising());
        }

        System.Collections.IEnumerator Rising()
        {
            rising = true;
            soulsCome = 0;
            Vector3 middle = Middle;
            Player near = Player.GetClosestPlayer(middle, RanNet.Area);
            foreach (Player p in Player.GetAllPlayers())
                if (p != null && Utils.DistanceXZ(p.transform.position, middle) < RanNet.Area)
                    RanVoice.Say(p, "$iov_ran_rise", 5f);
            if (RanKnot.CountNear(middle, Search) == 0)
            {
                GameObject knot = ZNetScene.instance.GetPrefab(RanNet.Buoy);
                float start = Random.Range(0f, 360f);
                for (int i = 0; i < 3 && knot != null; i++)
                {
                    float a = (start + i * 120f) * Mathf.Deg2Rad;
                    Vector3 at = middle + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * KnotRadius;
                    at.y = middle.y - 0.5f;
                    BossKit.Effect("vfx_WaterImpact_Karve", at);
                    Instantiate(knot, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    yield return new WaitForSeconds(0.8f);
                }
            }
            yield return new WaitForSeconds(3f);
            GameObject prefab = ZNetScene.instance.GetPrefab(RanNet.Hafgufa);
            if (prefab != null && Find() == null)
            {
                // in front of whoever came, between them and the middle (from the far side it lay off out of sight)
                Vector3 at = middle;
                if (near != null)
                {
                    Vector3 toward = middle - near.transform.position;
                    toward.y = 0f;
                    at = near.transform.position + (toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.forward) * Mathf.Min(30f, toward.magnitude + 10f);
                }
                at.y = middle.y - 3f;
                BossKit.Effect("vfx_spawn_large", at + Vector3.up * 3f);
                Vector3 look = (near != null ? near.transform.position : middle) - at;
                look.y = 0f;
                GameObject go = Instantiate(prefab, at, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
                Character c = go.GetComponent<Character>();
                if (c != null) c.SetLevel(2);
                BaseAI ai = go.GetComponent<BaseAI>();
                if (ai != null) ai.Alert();
                MonsterAI mind = go.GetComponent<MonsterAI>();
                if (mind != null && near != null) RanNet.Field(mind, "m_targetCreature", near); // it comes for whoever came
                ZNetView view = go.GetComponent<ZNetView>();
                if (view != null && view.GetZDO() != null && nview.IsValid()) nview.GetZDO().Set(BeastHere, view.GetZDO().m_uid);
                Plugin.Debug("T3: Hafgufa rises (the world's key: " + RanNet.Done + ", the captains free: " + RanNet.Begun + ")");
            }
            rising = false;
            sawKnots = true;
        }

        // Each comes up out of the sea in a pale light (seen by everyone), a moment before it's there.
        System.Collections.IEnumerator DrownedRise(Vector3 around, int count)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(RanNet.Drowned);
            if (prefab == null) yield break;
            float water = Middle.y;
            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 at = around + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(10f, 16f);
                at.y = water;
                GameObject beam = ZNetScene.instance.GetPrefab(RanNet.SoulBeam);
                if (beam != null) Instantiate(beam, at, Quaternion.identity);
                BossKit.Effect("vfx_WaterImpact_Karve", at);
                yield return new WaitForSeconds(1.4f);
                BossKit.Effect("vfx_ghost_spawn", at + Vector3.up);
                GameObject go = Instantiate(prefab, at + Vector3.up * 1.2f, Quaternion.identity);
                Character c = go.GetComponent<Character>();
                if (c != null) c.SetLevel(soulsCome == 0 ? 3 : soulsCome == 1 ? 2 : 1);
                soulsCome++;
                yield return new WaitForSeconds(1.5f);
                BaseAI ai = go.GetComponent<BaseAI>();
                if (ai != null) ai.Alert();
            }
            Plugin.Debug("T3: the drowned rise");
        }

        // The net torn: the souls under the water rise as light, one after another, and Rán lets them go.
        System.Collections.IEnumerator Freed()
        {
            rising = true;
            Player local = Player.m_localPlayer;
            // the net torn, then Rán's last words while the souls are still going up, held 8 s (the user's wish), one
            // after the other, never over each other (RanVoice)
            RanVoice.Say(local, "$iov_ran_torn", 5f);
            RanVoice.Say(local, "$iov_ran_free", 8f);
            Vector3 middle = Middle;
            for (int i = 0; i < 7; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 at = middle + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(8f, 50f);
                LightBeam.Over(at, 5f);
                yield return new WaitForSeconds(1.1f);
            }
            yield return new WaitForSeconds(4f);
            if (souls != null) souls.SetActive(false);
            rising = false;
        }

        // ---- the souls under the water (this client's own) ----

        void MakeSouls()
        {
            souls = new GameObject("IoV_GraveyardSouls");
            souls.transform.SetParent(transform, false);
            souls.transform.position = Middle;
            System.Random random = new System.Random(Mathf.RoundToInt(transform.position.x * 7f + transform.position.z * 13f));
            for (int i = 0; i < 9; i++)
            {
                double a = random.NextDouble() * Mathf.PI * 2.0, r = 6.0 + random.NextDouble() * 48.0;
                GameObject l = new GameObject("IoV_Soul");
                l.transform.SetParent(souls.transform, false);
                l.transform.localPosition = new Vector3((float)(System.Math.Cos(a) * r), -1.2f, (float)(System.Math.Sin(a) * r));
                Light light = l.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = RanNet.SoulColour;
                light.range = 9f;
                light.intensity = 1.6f;
                light.shadows = LightShadows.None;
            }
            Wisps(souls.transform);
            souls.SetActive(!RanNet.Done);
        }

        // Pale wisps rising slowly off the water across the graveyard.
        static void Wisps(Transform at)
        {
            Shader shader = ValhallaClouds.Find("Legacy Shaders/Particles/Additive");
            bool additive = shader != null;
            if (shader == null) shader = ValhallaClouds.Find("Sprites/Default");
            if (shader == null) return;
            GameObject go = new GameObject("IoV_SoulWisps");
            go.transform.SetParent(at, false);
            go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.95f, 1f, 0.8f), new Color(0.85f, 1f, 1f, 0.6f));
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = true;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 16f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 55f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            Material m = new Material(shader);
            m.mainTexture = ValhallaClouds.Dot();
            if (additive) m.SetColor("_TintColor", new Color(0.3f, 0.5f, 0.55f, 0.5f));
            renderer.sharedMaterial = m;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // A mist over the graveyard while the net holds (this client's weather; only ever undoes its own: lifting it
        // when the second stage's storm has already come cleared the storm too, the moment it came).
        void Mist(bool want)
        {
            EnvMan env = EnvMan.instance;
            if (env == null || want == mist) return;
            mist = want;
            if (want) env.SetForceEnvironment("Misty");
            else if (Forced(env) == "Misty") env.SetForceEnvironment("");
            Plugin.Debug("T3: mist " + (want ? "over the graveyard" : "lifts") + " (weather now: " + Forced(env) + ")");
        }

        static readonly System.Reflection.FieldInfo forceEnv = HarmonyLib.AccessTools.Field(typeof(EnvMan), "m_forceEnv");

        static string Forced(EnvMan env)
        {
            return forceEnv != null ? (forceEnv.GetValue(env) as string ?? "") : "";
        }

        // ---- the net round your ship (on the client that steers it) ----

        void FixedUpdate()
        {
            if (lastStage < 1 || RanNet.Done) return;
            Ship ship = Ship.GetLocalShip();
            if (ship == null || Utils.DistanceXZ(ship.transform.position, Middle) > RanNet.Area) return;
            ZNetView view = ship.GetComponent<ZNetView>();
            Rigidbody body = ship.GetComponent<Rigidbody>();
            if (view == null || !view.IsValid() || !view.IsOwner() || body == null) return;
            Vector3 v = body.velocity;
            float keep = Mathf.Clamp01(1f - 0.9f * Time.fixedDeltaTime);
            body.velocity = new Vector3(v.x * keep, v.y, v.z * keep);
        }

        // (development) the beast, the knots and the drowned gone, the souls back
        internal static void ClearAll()
        {
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null) continue;
                string name = Utils.GetPrefabName(c.gameObject);
                if (name == RanNet.Hafgufa || name == RanNet.Drowned) Despawn(c.gameObject);
            }
            foreach (GraveyardStand s in loaded)
            {
                if (s == null) continue;
                foreach (RanKnot k in RanKnot.Near(s.Middle, Search)) Despawn(k.gameObject);
                s.wasDone = false;
                s.lastStage = -1;
                if (s.nview != null && s.nview.IsValid())
                {
                    s.nview.ClaimOwnership();
                    s.nview.GetZDO().Set(DoneHere, false);
                    s.nview.GetZDO().Set(BeastHere, ZDOID.None);
                }
                if (s.souls != null) s.souls.SetActive(true);
            }
        }

        static void Despawn(GameObject go)
        {
            ZNetView view = go.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return;
            view.ClaimOwnership();
            ZNetScene.instance.Destroy(go);
        }
    }
}
