using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The Valhalla arc's first task (T1): prove yourself in the einherjar's ring. Three bouts, harder each
    // time: one against one (sword and shield); two at once (an axe, and a shield-maiden with a polearm); and
    // the champion, Starkaðr. Nobody dies: an einherjar beaten kneels and yields, and you, beaten, are spared.
    //
    // The fighters are the player's own body with a creature's mind (the game's Humanoid and MonsterAI), in
    // gear as good as the strongest boss this world has beaten. They are made by the duelling player's game and
    // vanish when the bout is done (not saved).
    class EinherjarFighter : MonoBehaviour
    {
        internal const string PrefabName = "IoV_Einherjar", ChampionPrefab = "IoV_EinherjarChampion";

        internal static void Register()
        {
            Make(PrefabName, false);
            Make(ChampionPrefab, true);
        }

        static void Make(string name, bool champion)
        {
            GameObject source = PrefabManager.Instance.GetPrefab("Player");
            if (source == null) { Plugin.Log.LogWarning("Duel: no Player prefab"); return; }
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(name, source);
            Player player = go.GetComponent<Player>();
            Humanoid body = go.AddComponent<Humanoid>();
            // everything the player's body was set up with (a Player is a Humanoid): copied over as it is
            for (System.Type t = typeof(Humanoid); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (System.Reflection.FieldInfo f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                    if (!f.IsLiteral && !f.IsInitOnly) f.SetValue(body, f.GetValue(player));
            // then whatever makes it a player goes (its controls, skills, talking, the player itself)
            for (int pass = 0; pass < 6; pass++)
            {
                bool any = false;
                MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = scripts.Length - 1; i >= 0; i--)
                {
                    MonoBehaviour s = scripts[i];
                    if (s == body || s is ZNetView || s is ZSyncTransform || s is ZSyncAnimation || s is VisEquipment ||
                        s is FootStep || s is CharacterAnimEvent || s is LightLod) continue;
                    Object.DestroyImmediate(s);
                    any = true;
                }
                if (!any) break;
            }
            body.m_name = champion ? "$iov_starkadr" : "$iov_einherjar";
            body.m_faction = Character.Faction.Boss;
            body.m_boss = champion; // the champion's bar across the top of the view, as a boss's
            body.m_health = 200f;
            body.m_defaultItems = new GameObject[0];
            body.m_randomWeapon = new GameObject[0];
            body.m_randomArmor = new GameObject[0];
            body.m_randomShield = new GameObject[0];

            MonsterAI ai = go.AddComponent<MonsterAI>();
            ai.m_viewRange = 60f;
            ai.m_viewAngle = 360f; // (in the ring they know where you are, whichever way they face)
            ai.m_hearRange = 60f;
            ai.m_alertRange = 40f;
            ai.m_pathAgentType = Pathfinding.AgentType.Humanoid;
            ai.m_moveMinAngle = 10f;
            ai.m_smoothMovement = true;
            ai.m_jumpInterval = 0f;
            ai.m_randomMoveRange = 3f;
            ai.m_randomMoveInterval = 5f;
            ai.m_avoidFire = false;
            ai.m_afraidOfFire = false;
            ai.m_avoidWater = true;
            ai.m_enableHuntPlayer = true;
            ai.m_attackPlayerObjects = false;
            ai.m_circulateWhileCharging = true;
            ai.m_minAttackInterval = champion ? 1.2f : 1.8f;
            // They stand and fight: no circling off, and none of the running away a creature does when hurt
            // and (to its pathfinding) you can't be reached; on the rock up in the sky it often thinks so, and
            // they kept turning their backs mid-fight and running off before coming back.
            ai.m_circleTargetInterval = 0f;
            ai.m_circleTargetDuration = 0f;
            ai.m_circleTargetDistance = 0f;
            ai.m_maxChaseDistance = 40f;
            ai.m_fleeIfLowHealth = 0f;
            ai.m_fleeIfHurtWhenTargetCantBeReached = false;
            ai.m_fleeIfNotAlerted = false;
            ai.m_fleeInLava = false;
            ai.m_fleeUnreachableSinceAttacking = 9999f;
            ai.m_fleeUnreachableSinceHurt = 9999f;
            ai.m_fleeTimeSinceHurt = 0f;
            ai.m_alertedEffects = new EffectList();
            ai.m_idleSound = new EffectList();
            ai.m_wakeupEffects = new EffectList();
            ai.m_sleepEffects = new EffectList();
            ai.m_consumeItems = new List<ItemDrop>();
            ai.m_spawnMessage = ai.m_deathMessage = ai.m_alertedMessage = "";

            ZNetView nview = go.GetComponent<ZNetView>();
            nview.m_persistent = false; // a bout's, not the world's
            VisEquipment vis = go.GetComponent<VisEquipment>();
            if (vis != null) vis.m_isPlayer = false;
            go.AddComponent<EinherjarFighter>();
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
            Plugin.Log.LogInfo("Duel: " + name + " made from the player's body");
        }

        internal bool yielded;
        float yieldedAt;
        Character body;
        MonsterAI ai;

        void Awake()
        {
            body = GetComponent<Character>();
            ai = GetComponent<MonsterAI>();
        }

        // The player's body starts out waking up, sitting on the ground (the player's own script lets it get
        // up; this one has no such script): up at once.
        void Start()
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.SetBool("wakeup", false);
        }

        // Beaten (its health run out): it doesn't die, it kneels, says so, and before long is gone.
        internal void Yield(bool kneel)
        {
            if (yielded) return;
            yielded = true;
            yieldedAt = Time.time;
            if (ai != null)
            {
                ai.m_viewRange = ai.m_hearRange = ai.m_alertRange = 0f;
                ai.m_enableHuntPlayer = false;
                ai.m_randomMoveRange = 0f;
                Traverse.Create(ai).Field("m_targetCreature").SetValue(null);
                Traverse.Create(ai).Method("SetAlerted", new[] { typeof(bool) }).GetValue(false);
            }
            if (kneel)
            {
                Emote("emote_kneel");
                if (Chat.instance != null) Chat.instance.SetNpcText(gameObject, Vector3.up * 2.2f, 20f, 4f, "", "$iov_duel_yield", false);
            }
        }

        bool goingHome;

        // Kneeling a while, then taken back by the light that brought it (the same soft column, coming on round it;
        // at its brightest it's gone).
        void Update()
        {
            if (!yielded || body == null) return;
            body.SetMoveDir(Vector3.zero);
            if (!goingHome && Time.time > yieldedAt + 3.5f)
            {
                goingHome = true;
                LightBeam.Over(transform.position, 3f, 1.8f, 0.9f, 2f, true);
            }
            ZNetView nview = GetComponent<ZNetView>();
            if (Time.time > yieldedAt + 4.5f && nview != null && nview.IsOwner()) ZNetScene.instance.Destroy(gameObject);
        }

        void Emote(string name)
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name != name) continue;
                if (p.type == AnimatorControllerParameterType.Trigger) animator.SetTrigger(name);
                else if (p.type == AnimatorControllerParameterType.Bool) animator.SetBool(name, true);
                return;
            }
        }

        // Neither side dies in the ring: a fighter whose health runs out yields; the duelling player, beaten,
        // is spared (the bout is lost).
        [HarmonyPatch(typeof(Character), "CheckDeath")]
        static class NoDeathInTheRing
        {
            static bool Prefix(Character __instance)
            {
                if (__instance.IsDead() || __instance.GetHealth() > 0f) return true;
                EinherjarFighter fighter = __instance.GetComponent<EinherjarFighter>();
                if (fighter != null)
                {
                    __instance.SetHealth(1f);
                    fighter.Yield(true);
                    return false;
                }
                if (__instance == Player.m_localPlayer && ValhallaDuel.Active)
                {
                    __instance.SetHealth(__instance.GetMaxHealth() * 0.25f);
                    ValhallaDuel.Current.Lose("$iov_duel_lost");
                    return false;
                }
                return true;
            }
        }
    }

    // The bouts, on the duelling player's game (on Valhalla's rock, alongside its sky).
    class ValhallaDuel : MonoBehaviour
    {
        internal const string WonKey = "iovvalhalladuelwon"; // the world's key: the einherjar have accepted you
        internal static ValhallaDuel Current;
        internal static bool Active { get { return Current != null && Current.round > 0; } }

        // What's playing while a bout is on (ValhallaSky.Music): the first two, then the champion's.
        internal static readonly string[] BoutMusic = { "boss_eikthyr" }, ChampionMusic = { "boss_frozenking" };
        internal static string[] Music { get { return !Active ? null : Current.round >= 3 ? ChampionMusic : BoutMusic; } }

        static readonly Vector2 Ring = new Vector2(40f, 6f);
        const float RingRadius = 10f;

        int round;                 // 0: none; 1..3 a bout (or the pause before it)
        float boutAt;              // when the fighters of this round come in
        float endAt = -1f;         // when it's all over (after the last words)
        float outsideSince = -1f;
        float lastHealth = -1f, nextCheer;
        readonly List<Character> fighters = new List<Character>();

        void Awake() { Current = this; }
        void OnDestroy() { Stop(); if (Current == this) Current = null; }

        Vector3 RingCentre
        {
            get { return transform.TransformPoint(new Vector3(Ring.x, IsletBuilder.SkyGround(Ring.x, Ring.y), Ring.y)); }
        }

        // ---- beginning, and the end ----

        internal void Begin(int from = 1)
        {
            if (Active) return;
            if (ValhallaPeople.Current != null) ValhallaPeople.Current.RingCleared(true);
            Next(Mathf.Clamp(from, 1, 3));
            Plugin.Debug("Duel: begun, at the tier of " + Tier() + " bosses beaten");
        }

        void Next(int r)
        {
            round = r;
            boutAt = Time.time + (r == 1 ? 5f : 6f); // (the first: while the two sparring walk out of the ring)
            lastHealth = -1f;
            Message(r == 1 ? "$iov_duel_round1" : r == 2 ? "$iov_duel_round2" : "$iov_duel_round3");
        }

        void Win()
        {
            Message("$iov_duel_won");
            Story.Set(WonKey);
            endAt = Time.time + 5f;
            Plugin.Debug("Duel: won");
        }

        internal void Lose(string why)
        {
            if (!Active || endAt > 0f) return;
            Message(why);
            foreach (Character c in fighters)
            {
                if (c == null) continue;
                EinherjarFighter f = c.GetComponent<EinherjarFighter>();
                if (f != null) f.Yield(false);
            }
            endAt = Time.time + 4f;
            Plugin.Debug("Duel: lost (" + why + ")");
        }

        // All over, at once (leaving Valhalla, or iov_duel stop).
        internal void Stop()
        {
            foreach (Character c in fighters)
                if (c != null && c.GetComponent<ZNetView>() != null && c.GetComponent<ZNetView>().IsOwner() && ZNetScene.instance != null)
                    ZNetScene.instance.Destroy(c.gameObject);
            fighters.Clear();
            pending.Clear();
            if (round > 0 && ValhallaPeople.Current != null) ValhallaPeople.Current.RingCleared(false);
            round = 0;
            endAt = -1f;
            outsideSince = -1f;
        }

        static void Message(string text)
        {
            if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text);
        }

        // ---- while it's on ----

        void Update()
        {
            if (!Active) return;
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead()) { Stop(); return; }
            if (endAt > 0f)
            {
                if (Time.time >= endAt) Stop();
                return;
            }
            // out of the ring for a moment: you've left it
            Vector3 centre = RingCentre;
            bool outside = Utils.DistanceXZ(player.transform.position, centre) > RingRadius + 2.5f;
            if (!outside) outsideSince = -1f;
            else if (outsideSince < 0f) outsideSince = Time.time;
            else if (Time.time - outsideSince > 2f) { Lose("$iov_duel_fled"); return; }
            // nearly beaten: spared before the last blow
            if (player.GetHealth() < player.GetMaxHealth() * 0.1f) { Lose("$iov_duel_lost"); return; }

            if (pending.Count > 0)
            {
                // in the light that brings them: they step out of it at its brightest
                if (Time.time >= pendingAt) { foreach (System.Action make in pending) make(); pending.Clear(); }
                return;
            }
            if (fighters.Count == 0)
            {
                if (Time.time >= boutAt) Spawn(player, centre);
                return;
            }
            // the crowd round the ring knocks weapon on shield when the einherjar take a blow
            float health = 0f;
            bool standing = false;
            bool remind = Time.time >= nextRemind;
            if (remind) nextRemind = Time.time + 0.5f;
            foreach (Character c in fighters)
            {
                if (c == null) continue;
                health += c.GetHealth();
                EinherjarFighter f = c.GetComponent<EinherjarFighter>();
                if (f != null && !f.yielded)
                {
                    standing = true;
                    // eyes on you all the bout: the first one in (you still on your way into the ring) stood with
                    // its back to you, turning now and then, looking for you where it last knew you (nowhere)
                    if (remind) EyesOn(c, player);
                }
            }
            if (lastHealth >= 0f && health < lastHealth - 0.5f && Time.time >= nextCheer && ValhallaPeople.Current != null)
            {
                ValhallaPeople.Current.Cheer();
                nextCheer = Time.time + 1.2f;
            }
            lastHealth = health;
            if (standing) return;
            // every one of them yielded: the next bout (a breath first, some health back), or the end
            fighters.Clear();
            if (round >= 3) { Win(); return; }
            player.Heal(player.GetMaxHealth() * 0.35f, true);
            Next(round + 1);
        }

        // ---- the fighters ----

        // The strongest boss this world has beaten: 0 none, 1 Eikthyr ... 7 Fader.
        static int Tier()
        {
            if (ZoneSystem.instance == null) return 0;
            string[] keys = { "defeated_eikthyr", "defeated_gdking", "defeated_bonemass", "defeated_dragon", "defeated_goblinking", "defeated_queen", "defeated_fader" };
            int tier = 0;
            for (int i = 0; i < keys.Length; i++) if (ZoneSystem.instance.GetGlobalKey(keys[i])) tier = i + 1;
            return tier;
        }

        // By tier (0..7): armour, cape, and each kind of weapon (the first the game has, of a few).
        static readonly string[][] Armour =
        {
            new[] { "HelmetLeather", "ArmorLeatherChest", "ArmorLeatherLegs" },
            new[] { "HelmetTrollLeather", "ArmorTrollLeatherChest", "ArmorTrollLeatherLegs" },
            new[] { "HelmetBronze", "ArmorBronzeChest", "ArmorBronzeLegs" },
            new[] { "HelmetIron", "ArmorIronChest", "ArmorIronLegs" },
            new[] { "HelmetDrake", "ArmorWolfChest", "ArmorWolfLegs" },
            new[] { "HelmetPadded", "ArmorPaddedCuirass", "ArmorPaddedGreaves" },
            new[] { "HelmetCarapace", "ArmorCarapaceChest", "ArmorCarapaceLegs" },
            new[] { "HelmetFlametal", "ArmorFlametalChest", "ArmorFlametalLegs" },
        };
        static readonly string[] Capes = { "CapeDeerHide", "CapeDeerHide", "CapeTrollHide", "CapeTrollHide", "CapeWolf", "CapeLox", "CapeFeather", "CapeAsh" };
        static readonly string[][] Swords =
        {
            new[] { "SwordBronze" }, new[] { "SwordBronze" }, new[] { "SwordBronze" }, new[] { "SwordIron" }, new[] { "SwordSilver" },
            new[] { "SwordBlackmetal" }, new[] { "SwordMistwalker", "SwordBlackmetal" }, new[] { "SwordNiedhogg", "SwordMistwalker", "SwordBlackmetal" },
        };
        static readonly string[][] Shields =
        {
            new[] { "ShieldWood" }, new[] { "ShieldBronzeBuckler", "ShieldWood" }, new[] { "ShieldBronzeBuckler" }, new[] { "ShieldBanded" }, new[] { "ShieldSilver" },
            new[] { "ShieldBlackmetal" }, new[] { "ShieldCarapace", "ShieldBlackmetal" }, new[] { "ShieldFlametal", "ShieldCarapace", "ShieldBlackmetal" },
        };
        static readonly string[][] Axes =
        {
            new[] { "AxeFlint" }, new[] { "AxeBronze" }, new[] { "AxeBronze" }, new[] { "AxeIron" }, new[] { "AxeIron" },
            new[] { "AxeBlackMetal", "AxeIron" }, new[] { "AxeJotunBane", "AxeBlackMetal" }, new[] { "AxeBerzerkr", "AxeJotunBane", "AxeBlackMetal" },
        };
        static readonly string[][] Polearms =
        {
            new[] { "AtgeirBronze" }, new[] { "AtgeirBronze" }, new[] { "AtgeirBronze" }, new[] { "AtgeirIron" }, new[] { "AtgeirIron" },
            new[] { "AtgeirBlackmetal" }, new[] { "AtgeirHimminAfl", "AtgeirBlackmetal" }, new[] { "AtgeirHimminAfl", "AtgeirBlackmetal" },
        };
        static readonly string[][] Great =
        {
            new[] { "Battleaxe" }, new[] { "Battleaxe" }, new[] { "Battleaxe" }, new[] { "Battleaxe" }, new[] { "BattleaxeCrystal", "Battleaxe" },
            new[] { "BattleaxeCrystal", "Battleaxe" }, new[] { "THSwordKrom", "BattleaxeCrystal" }, new[] { "THSwordSlayer", "THSwordKrom", "BattleaxeCrystal" },
        };
        static readonly float[] Health = { 120f, 180f, 260f, 400f, 600f, 850f, 1200f, 1700f };

        void Spawn(Player player, Vector3 centre)
        {
            int tier = Tier();
            // always the same way in: the ring's north side, by the gap with the blue fire (a little in from the
            // columns), unless you stand on it
            const float In = 7.5f;
            Vector3 away = transform.TransformDirection(Vector3.forward);
            away.y = 0f;
            away.Normalize();
            if (Utils.DistanceXZ(player.transform.position, centre + away * In) < 2.5f) away = -away;
            if (round == 1)
            {
                Vector3 at = centre + away * In;
                Later(at, () => Fighter(PrefabName(false), at, player, tier, Health[tier], 0, Pick(Swords, tier), Pick(Shields, tier), "Hair5", "Beard3", Capes[tier]));
            }
            else if (round == 2)
            {
                Vector3 side = Vector3.Cross(Vector3.up, away);
                Vector3 one = centre + away * In + side * 2.2f, other = centre + away * In - side * 2.2f;
                Later(one, () => Fighter(PrefabName(false), one, player, tier, Health[tier] * 0.7f, 0, Pick(Axes, tier), Pick(Shields, tier), "Hair9", "Beard7", Capes[tier]));
                Later(other, () => Fighter(PrefabName(false), other, player, tier, Health[tier] * 0.7f, 1, Pick(Polearms, tier), "", "Hair14", "", Capes[tier]));
            }
            else
            {
                Vector3 at = centre + away * In;
                Later(at, () => Fighter(PrefabName(true), at, player, Mathf.Max(tier, 6), Health[tier] * 2.5f, 0, Pick(Great, tier), "", "Hair8", "Beard10", "CapeWolf"));
            }
            Plugin.Debug("Duel: bout " + round + ", " + pending.Count + " coming into the ring (tier " + tier + ")");
        }

        // They come in Valhalla's own light (a soft column, as the one that took Sævar), and step out of it.
        readonly List<System.Action> pending = new List<System.Action>();
        float pendingAt;

        void Later(Vector3 at, System.Action make)
        {
            LightBeam.Over(at, 3.4f, 1.8f, 0.9f, 2.2f, true);
            pending.Add(make);
            pendingAt = Time.time + 1f;
        }

        float nextRemind;

        // Its foe is you, it's roused, and it knows where you are.
        static void EyesOn(Character fighter, Player player)
        {
            MonsterAI ai = fighter != null ? fighter.GetComponent<MonsterAI>() : null;
            if (ai == null || player == null) return;
            Traverse t = Traverse.Create(ai);
            t.Field("m_targetCreature").SetValue(player);
            t.Field("m_lastKnownTargetPos").SetValue(player.transform.position);
            t.Method("SetAlerted", new[] { typeof(bool) }).GetValue(true);
        }

        static string PrefabName(bool champion) { return champion ? EinherjarFighter.ChampionPrefab : EinherjarFighter.PrefabName; }

        static string Pick(string[][] table, int tier)
        {
            foreach (string name in table[Mathf.Clamp(tier, 0, table.Length - 1)])
                if (ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(name) != null) return name;
            return "";
        }

        // One fighter: dressed (its gear given as a creature's default items, set on the prefab just before it's
        // made, which is when the game reads them), its health, and its eyes on you.
        void Fighter(string prefabName, Vector3 at, Player player, int armourTier, float health, int model, string right, string left, string hair, string beard, string cape)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            Humanoid template = prefab != null ? prefab.GetComponent<Humanoid>() : null;
            if (template == null) { Plugin.Log.LogWarning("Duel: no " + prefabName); return; }
            List<GameObject> items = new List<GameObject>();
            string[] armour = Armour[Mathf.Clamp(armourTier, 0, Armour.Length - 1)];
            foreach (string name in new[] { right, left, armour[0], armour[1], armour[2], cape })
            {
                GameObject item = string.IsNullOrEmpty(name) || ObjectDB.instance == null ? null : ObjectDB.instance.GetItemPrefab(name);
                if (item != null) items.Add(item);
            }
            template.m_defaultItems = items.ToArray();
            Traverse.Create(template).Field("m_hairItem").SetValue(hair ?? "");
            Traverse.Create(template).Field("m_beardItem").SetValue(beard ?? "");
            Vector3 look = player.transform.position - at;
            look.y = 0f;
            GameObject go = Instantiate(prefab, at + Vector3.up * 0.3f, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
            template.m_defaultItems = new GameObject[0];
            Character body = go.GetComponent<Character>();
            body.SetMaxHealth(health);
            body.SetHealth(health);
            VisEquipment vis = go.GetComponent<VisEquipment>();
            if (vis != null)
            {
                vis.SetModel(model);
                vis.SetHairColor(model == 1 ? new Vector3(0.85f, 0.7f, 0.45f) : new Vector3(0.4f, 0.27f, 0.17f));
                vis.SetSkinColor(Vector3.one * 0.95f);
            }
            EyesOn(body, player);
            fighters.Add(body);
            Plugin.Debug("Duel: " + prefabName + " with " + right + (left != "" ? " and " + left : "") + ", " + health.ToString("F0") + " health");
        }
    }
}
