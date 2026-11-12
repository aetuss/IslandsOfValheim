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
        internal const string HafdisPrefab = "IoV_HafdisDuelist"; // T5: Hafdís as she was, a shield-maiden
        internal const string AllyPrefab = "IoV_StarkadrAlly";    // the champion's oath: at your side (ChampionOath)

        internal static void Register()
        {
            Make(PrefabName, false, "$iov_einherjar");
            Make(ChampionPrefab, true, "$iov_starkadr");
            Make(HafdisPrefab, true, "$iov_hafdis");
            Make(AllyPrefab, false, "$iov_starkadr");
            // on your side: the players' own (so he goes for whatever is your foe, and never for you or your
            // tamed beasts: BaseAI.IsEnemy), hunting no player
            GameObject ally = PrefabManager.Instance.GetPrefab(AllyPrefab);
            if (ally != null)
            {
                ally.GetComponent<Humanoid>().m_faction = Character.Faction.Players;
                MonsterAI ai = ally.GetComponent<MonsterAI>();
                ai.m_enableHuntPlayer = false;
                ai.m_viewAngle = 360f;
                ai.m_viewRange = 30f;
                ai.m_alertRange = 30f;
                ai.m_maxChaseDistance = 30f;
                ai.m_avoidWater = false;
                ai.m_minAttackInterval = 1.2f;
                ally.GetComponent<EinherjarFighter>().ally = true;
            }
        }

        // the champion come to your side (ChampionOath): falls back when beaten, with a word of his own
        // (public: the prefab's copies keep it)
        public bool ally;

        static void Make(string name, bool champion, string display)
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
            body.m_name = display;
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
            // (not circling while their next blow comes ready: up close that was running on the spot, round
            // and round; they stand their ground, facing you, instead)
            ai.m_circulateWhileCharging = false;
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
            // (Hafdís always a woman, with her own hair: the game puts a body's model and colours on only if it's
            // a player's (VisEquipment.UpdateVisuals), and picks a creature's at random; 1.10.104-105's came as the
            // body's default, a bald man)
            if (vis != null && name == HafdisPrefab) { vis.m_randomModelIndex = false; vis.m_isPlayer = true; }
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
            Looks();
            Invoke("Looks", 0.5f); // (and again, after the game's own setting up of a creature's looks)
        }

        // The body and hair asked for, set on it as the game sets a player's (a creature's are otherwise its
        // own pick: a random body, hair by chance).
        internal int modelWanted = -1;
        internal string hairWanted = "", beardWanted = "";

        void Looks()
        {
            VisEquipment vis = GetComponent<VisEquipment>();
            ZNetView nview = GetComponent<ZNetView>();
            if (vis == null || modelWanted < 0 || nview == null || !nview.IsOwner()) return;
            vis.SetModel(modelWanted);
            vis.SetHairItem(hairWanted.Length > 0 ? hairWanted.GetStableHashCode() : 0);
            vis.SetBeardItem(beardWanted.Length > 0 ? beardWanted.GetStableHashCode() : 0);
        }

        // Hafdís's blows come quicker (the user: she waited too long between them). A creature waits between
        // its blows its own while (MonsterAI's) and each weapon's own while for creatures (the item's, made for
        // the player's hand: long); hers, on her own copies of her weapons' settings (shared with every such
        // weapon in the world otherwise).
        static readonly System.Reflection.MethodInfo copyOf = typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // (and the first bout's one, a little: the user found it waiting long between blows, 1.10.120; once its
        // weapons are in its hands, a moment after it's made: the game gives a creature its gear as it starts)
        float soonLeast, soonMost;

        internal void QuickerSoon(float least, float most)
        {
            soonLeast = least;
            soonMost = most;
            if (ai != null) ai.m_minAttackInterval = least;
            Invoke("QuickerNow", 0.8f);
        }

        void QuickerNow() { Quicker(soonLeast, soonMost); }

        internal void Quicker(float least = 0.4f, float most = 0.7f)
        {
            if (ai != null) ai.m_minAttackInterval = least;
            Humanoid me = body as Humanoid;
            if (me == null || copyOf == null) return;
            foreach (ItemDrop.ItemData item in me.GetInventory().GetAllItems())
            {
                if (item == null || !item.IsWeapon()) continue;
                ItemDrop.ItemData.SharedData own = (ItemDrop.ItemData.SharedData)copyOf.Invoke(item.m_shared, null);
                Plugin.Debug("Duel: " + body.m_name + "'s " + own.m_name + ": " + own.m_aiAttackInterval.ToString("F1") + " s between blows, now " + Mathf.Min(own.m_aiAttackInterval, most).ToString("F1"));
                own.m_aiAttackInterval = Mathf.Min(own.m_aiAttackInterval, most);
                item.m_shared = own;
            }
        }

        // Hafdís (T5) waiting in the ring for you: still, facing you; then the bout.
        internal bool waiting;

        void Wait()
        {
            if (ai != null) ai.enabled = false;
            body.SetMoveDir(Vector3.zero);
            Player player = Player.m_localPlayer;
            if (player == null) return;
            Vector3 face = player.transform.position - transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), Time.deltaTime * 3f);
        }

        internal void Fight()
        {
            waiting = false;
            if (ai != null) ai.enabled = true;
            nextEcho = Time.time + 4f;
            if (hafdis) Quicker();
            // her bar across the top of the view only now (1.10.105 showed it from when she came into the ring,
            // the player still far off): a boss's, made afresh (one made while she waited would be the plain kind)
            body.m_boss = true;
            ClearBar();
        }

        // Her bar as the game keeps it, taken away (to be made again as it should be now, or not at all).
        void ClearBar()
        {
            EnemyHud hud = EnemyHud.instance;
            System.Collections.IDictionary huds = hud != null ? Traverse.Create(hud).Field("m_huds").GetValue() as System.Collections.IDictionary : null;
            if (huds == null || !huds.Contains(body)) return;
            GameObject gui = Traverse.Create(huds[body]).Field("m_gui").GetValue<GameObject>();
            if (gui != null) Destroy(gui);
            huds.Remove(body);
        }

        // Beaten (its health run out): it doesn't die, it kneels, says so, and before long is gone.
        internal void Yield(bool kneel)
        {
            if (yielded) return;
            yielded = true;
            yieldedAt = Time.time;
            Plugin.Debug("Duel: " + body.m_name + " yields" + (kneel ? ", kneeling" : "") + " (mid-blow: " + body.InAttack() + ", winding up a slam: " + (slamAt > 0f || shockAt > 0f) + ")");
            if (slamGlow != null) Destroy(slamGlow.gameObject); // (beaten mid-blow)
            if (ring != null) Destroy(ring.gameObject);
            slamAt = shockAt = -1f;
            if (ai != null)
            {
                ai.m_viewRange = ai.m_hearRange = ai.m_alertRange = 0f;
                ai.m_enableHuntPlayer = false;
                ai.m_randomMoveRange = 0f;
                Traverse.Create(ai).Field("m_targetCreature").SetValue(null);
                Traverse.Create(ai).Method("SetAlerted", new[] { typeof(bool) }).GetValue(false);
                // and its mind put to rest: still awake, it kept walking at you, kneeling, wherever you went (it
                // still knew where you'd last been); stilled as Starkaðr is for his slam
                ai.enabled = false;
            }
            if (echoGlow != null) Destroy(echoGlow.gameObject);
            if (hafdis) body.m_boss = false;
            ClearBar(); // (the bout's over: no bar over a farewell; NoBarOnceYielded keeps it gone)
            if (kneel)
            {
                // (Hafdís lowers her spear; and anyone with a two-handed weapon lays it down: with one in his hands
                // the champion never knelt, beaten mid-blow or not (1.10.118, 1.10.121, the log showing him idle
                // the second time); 1.10.120, his weapon taken, knelt him, but twice, and he knelt and rose)
                Unset("emote_challenge"); // (and the champion's slam wind-up pose let go)
                Humanoid me = body as Humanoid;
                ItemDrop.ItemData held = me != null ? me.GetCurrentWeapon() : null;
                bool twoHanded = held != null && (held.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || held.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft);
                bool laid = false;
                if (held != null && (hafdis || twoHanded) && held.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield && me.GetInventory().ContainsItem(held))
                {
                    me.UnequipItem(held, false);
                    laid = true;
                }
                // The kneel, once: after the blow it was beaten in is over, and after its weapon is put away
                // (Update).
                kneelPending = true;
                kneelAt = Time.time + (laid ? 0.45f : 0f);
                if (Chat.instance != null) Chat.instance.SetNpcText(gameObject, Vector3.up * 2.2f, 20f, hafdis ? 7f : 4f, "", hafdis ? "$iov_t5_yield" : ally ? "$iov_oath_fallen" : "$iov_duel_yield", false);
            }
        }

        // ---- Hafdís (T5): she yields at a fifth of her health; from half, now and then, the echo of what she was
        // in the tale: a gold light at her back, a mark where you stand, and a few of her old feathers coming
        // down on it (fewer and lighter than the Fallen Valkyrie's) ----

        internal bool hafdis;
        internal bool ceremony; // yielded, she stays for her farewell (ValhallaPeople.Ceremony)
        internal float echoPierce = 30f;
        const float EchoEvery = 14f, EchoWarning = 1.6f, EchoHold = 0.45f, EchoHeight = 8f, EchoSpread = 0.6f;
        const int EchoFeathers = 4;
        float nextEcho = -1f;
        bool echoed;
        Light echoGlow;
        static readonly int wet = "Wet".GetStableHashCode();

        void Hafdis()
        {
            Player player = Player.m_localPlayer;
            ZNetView nview = GetComponent<ZNetView>();
            if (player == null || nview == null || !nview.IsOwner()) return;
            if (body.GetHealth() < body.GetMaxHealth() * 0.2f) { Yield(true); return; }
            if (body.GetHealth() > body.GetMaxHealth() * 0.5f || Time.time < nextEcho) return;
            if (Vector3.Distance(player.transform.position, transform.position) > 25f) return;
            nextEcho = Time.time + EchoEvery;
            if (!echoed && Chat.instance != null) Chat.instance.SetNpcText(gameObject, Vector3.up * 2.2f, 30f, 4f, "", "$iov_t5_echo", false);
            echoed = true;
            StartCoroutine(Echo(player, nview));
        }

        System.Collections.IEnumerator Echo(Player player, ZNetView nview)
        {
            GameObject light = new GameObject("IoV_HafdisEcho");
            light.transform.SetParent(transform, false);
            light.transform.localPosition = new Vector3(0f, 1.5f, -0.5f);
            echoGlow = light.AddComponent<Light>();
            echoGlow.type = LightType.Point;
            echoGlow.color = HafdisBoss.Glow;
            echoGlow.range = 6f;
            echoGlow.intensity = 3f;
            echoGlow.shadows = LightShadows.None;
            // (the user: on you, not where you were) a gold light on the ground that follows you while she
            // calls them, then holds still a breath before they come down, there, close together
            BossKit.Effect("sfx_fallenvalkyrie_attack_spit_charge", transform.position);
            GameObject mark = new GameObject("IoV_HafdisEchoMark");
            Light shine = mark.AddComponent<Light>();
            shine.type = LightType.Point;
            shine.color = HafdisBoss.Glow;
            shine.range = 3.5f;
            shine.intensity = 4f;
            shine.shadows = LightShadows.None;
            Vector3 at = player.transform.position;
            for (float t = 0f; t < EchoWarning; t += Time.deltaTime)
            {
                if (player == null || yielded) break;
                if (t < EchoWarning - EchoHold)
                {
                    Vector3 p = player.transform.position;
                    at = BossKit.Ground(p) ?? p;
                }
                mark.transform.position = at + Vector3.up * 0.4f;
                shine.intensity = 2f + 4f * t / EchoWarning;
                yield return null;
            }
            BossKit.Effect("vfx_prespawn", at);
            Destroy(mark);
            if (echoGlow != null) Destroy(echoGlow.gameObject);
            GameObject feather = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("fallenvalkyrie_feather_projectile") : null;
            if (yielded || nview == null || !nview.IsValid() || !nview.IsOwner() || feather == null) yield break;
            for (int i = 0; i < EchoFeathers; i++)
            {
                Vector2 off = Random.insideUnitCircle * EchoSpread;
                Vector3 from = at + new Vector3(off.x, EchoHeight, off.y);
                Vector3 velocity = new Vector3(Random.Range(-0.5f, 0.5f), -28f, Random.Range(-0.5f, 0.5f));
                GameObject go = Instantiate(feather, from, Quaternion.LookRotation(velocity));
                IProjectile projectile = go.GetComponent<IProjectile>();
                if (projectile != null)
                {
                    HitData hit = new HitData();
                    hit.m_damage.m_pierce = echoPierce;
                    hit.m_pushForce = 10f;
                    hit.m_statusEffectHash = wet;
                    hit.SetAttacker(body);
                    projectile.Setup(body, velocity, -1f, hit, null, null);
                }
                yield return new WaitForSeconds(0.14f);
            }
            Plugin.Debug("T5: Hafdís's feathers (" + EchoFeathers + ", " + echoPierce.ToString("F0") + " pierce)");
        }

        bool goingHome;

        // ---- the champion's own blow: he raises his weapon and glows gold, calls out, and brings it down on
        // the ground, a shock round him that throws back and hurts whoever's still close (step away, or block) ----

        internal float slamDamage;        // set for the champion: how hard his slam hits (by the world's tier)
        const float SlamWindUp = 1.4f, SlamReach = 5f, SlamLands = 0.6f;
        float nextSlam = -1f, slamAt = -1f, shockAt = -1f;
        Light slamGlow;
        Vector3 slamCentre;
        SlamRing ring;

        void Champion()
        {
            Player player = Player.m_localPlayer;
            ZNetView nview = GetComponent<ZNetView>();
            if (player == null || nview == null || !nview.IsOwner()) return;
            if (nextSlam < 0f) nextSlam = Time.time + 9f;
            if (shockAt > 0f)
            {
                // his own heavy blow on its way down (his weapon's second attack, as the game swings it): the
                // shock where it lands
                body.SetMoveDir(Vector3.zero);
                if (Time.time >= shockAt) Slam(player);
                return;
            }
            if (slamAt > 0f)
            {
                // winding up: still, glowing brighter; then the blow
                body.SetMoveDir(Vector3.zero);
                if (slamGlow != null) slamGlow.intensity = Mathf.Lerp(0.5f, 7f, 1f - (slamAt - Time.time) / SlamWindUp);
                if (Time.time >= slamAt)
                {
                    slamAt = -1f;
                    Trigger("emote_stop");
                    Humanoid me = body as Humanoid;
                    bool swinging = me != null && (me.StartAttack(player, true) || me.StartAttack(player, false));
                    if (!swinging) Trigger("swing_sledge");
                    shockAt = Time.time + SlamLands;
                }
                return;
            }
            if (Time.time < nextSlam || Vector3.Distance(player.transform.position, transform.position) > 7f) return;
            // the wind-up
            slamAt = Time.time + SlamWindUp;
            if (ai != null) ai.enabled = false;
            Vector3 face = player.transform.position - transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
            // where it'll land, and how far it reaches: a ring of gold on the ground, brightening (out of it,
            // and you're safe)
            slamCentre = transform.position + transform.forward * 1.2f;
            ring = SlamRing.Make(slamCentre, SlamReach, SlamWindUp + SlamLands);
            Emote("emote_challenge");
            GameObject light = new GameObject("IoV_SlamGlow");
            light.transform.SetParent(transform, false);
            light.transform.localPosition = new Vector3(0f, 1.8f, 0.4f);
            slamGlow = light.AddComponent<Light>();
            slamGlow.type = LightType.Point;
            slamGlow.color = new Color(1f, 0.8f, 0.4f);
            slamGlow.range = 7f;
            slamGlow.intensity = 0.5f;
            slamGlow.shadows = LightShadows.None;
            if (Chat.instance != null) Chat.instance.SetNpcText(gameObject, Vector3.up * 2.4f, 30f, 2.5f, "", "$iov_starkadr_slam", false);
            EffectList start = SledgeEffects(true);
            if (start != null) start.Create(transform.position + Vector3.up, transform.rotation);
        }

        void Slam(Player player)
        {
            slamAt = shockAt = -1f;
            nextSlam = Time.time + Random.Range(13f, 18f);
            if (slamGlow != null) Destroy(slamGlow.gameObject);
            if (ai != null && !yielded) ai.enabled = true;
            Vector3 centre = slamCentre;
            EffectList hit = SledgeEffects(false);
            if (hit != null) hit.Create(centre, Quaternion.identity);
            // the ring bursts out from where it struck, and the ground shakes
            if (ring != null) ring.Burst();
            ring = null;
            if (GameCamera.instance != null) GameCamera.instance.AddShake(centre, 20f, 2.5f, false);
            Vector3 away = player.transform.position - centre;
            away.y = 0f;
            if (away.magnitude > SlamReach) return;
            HitData blow = new HitData();
            blow.m_damage.m_blunt = slamDamage > 0f ? slamDamage : 60f;
            blow.m_point = player.transform.position + Vector3.up;
            blow.m_dir = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
            blow.m_pushForce = 120f;
            blow.m_staggerMultiplier = 2f;
            blow.SetAttacker(body);
            player.Damage(blow);
        }

        // The game's own sledge, its swing (start) or its blow on the ground (the shock and its sound).
        static EffectList SledgeEffects(bool start)
        {
            foreach (string name in new[] { "SledgeIron", "SledgeStagbreaker", "SledgeDemolisher" })
            {
                GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(name) : null;
                Attack a = item != null ? item.GetComponent<ItemDrop>().m_itemData.m_shared.m_attack : null;
                if (a == null) continue;
                EffectList e = start ? a.m_startEffect : (a.m_hitTerrainEffect != null && a.m_hitTerrainEffect.HasEffects() ? a.m_hitTerrainEffect : a.m_triggerEffect);
                if (e != null && e.HasEffects()) return e;
            }
            return null;
        }

        void Trigger(string name)
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == name && p.type == AnimatorControllerParameterType.Trigger) { animator.SetTrigger(name); return; }
        }

        // Kneeling a while, then taken back by the light that brought it (the same soft column, coming on round it;
        // at its brightest it's gone).
        void Update()
        {
            if (!yielded && body != null && slamDamage > 0f) { Champion(); return; }
            if (!yielded && body != null && waiting) { Wait(); return; }
            if (!yielded && body != null && hafdis) { Hafdis(); return; }
            if (!yielded || body == null) return;
            body.SetMoveDir(Vector3.zero);
            if (kneelPending && Time.time >= kneelAt && (!body.InAttack() || Time.time > yieldedAt + 1.5f))
            {
                kneelPending = false;
                Emote("emote_kneel");
                // held there: nothing to push it about while it kneels (1.10.124: the champion knelt, and got up
                // as the user moved off; a body nudged walks, and walking ends an emote)
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null) { rb.velocity = Vector3.zero; rb.constraints = RigidbodyConstraints.FreezeAll; } // (not kinematic: the game sets a body's velocity every step)
                kneltAt = Time.time;
            }
            KneelWatch();
            // (Hafdís kneels longer: her words first; through her farewell, if there's one: the ceremony takes her)
            if (ceremony) return;
            float stays = hafdis ? 9f : 3.5f;
            if (!goingHome && Time.time > yieldedAt + stays)
            {
                goingHome = true;
                LightBeam.Over(transform.position, 3f, 1.8f, 0.9f, 2f, true);
            }
            ZNetView nview = GetComponent<ZNetView>();
            if (Time.time > yieldedAt + stays + 1f && nview != null && nview.IsOwner()) ZNetScene.instance.Destroy(gameObject);
        }

        bool kneelPending;
        float kneelAt, kneltAt = -1f;
        int kneelState, lastState;

        // (for the log: should it still get up, what it got up into, and its speed then)
        void KneelWatch()
        {
            if (kneltAt < 0f) return;
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            int state = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            if (Time.time - kneltAt < 1.2f) { kneelState = lastState = state; return; }
            if (state == lastState) return;
            lastState = state;
            if (state != kneelState)
            {
                MonsterAI mind = GetComponent<MonsterAI>();
                Player you = Player.m_localPlayer;
                Plugin.Debug("Duel: " + body.m_name + ", kneeling, changed pose (state " + state + " from " + kneelState + "; walking " + animator.GetFloat("forward_speed").ToString("F2") +
                    ", on the ground " + animator.GetBool("onGround") + ", in an attack " + body.InAttack() +
                    ", mind " + (mind != null && mind.enabled ? "awake" : "still") + ", " + (Time.time - kneltAt).ToString("F1") + " s after kneeling; the player " +
                    (you != null ? Vector3.Distance(you.transform.position, transform.position).ToString("F1") + " m off, moving " + you.GetVelocity().magnitude.ToString("F1") : "?") + ")");
            }
        }

        // A pose held by a switch (as the champion's challenge), let go.
        void Unset(string name)
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == name && p.type == AnimatorControllerParameterType.Bool) { animator.SetBool(name, false); return; }
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

        // One who has yielded shows no bar and no "searching" mark over its head (1.10.107: Hafdís, kneeling for
        // her farewell, still had both).
        [HarmonyPatch(typeof(EnemyHud), "TestShow")]
        static class NoBarOnceYielded
        {
            static bool Prefix(Character __0, ref bool __result)
            {
                EinherjarFighter f = __0 != null ? __0.GetComponent<EinherjarFighter>() : null;
                if (f == null || !f.yielded) return true;
                __result = false;
                return false;
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

    // Starkaðr's slam made plain: a ring on the ground as far as it reaches, gold and brightening as he winds
    // up (turning fiery as it comes), then, as it lands, a wave bursting out from the middle past the ring's
    // edge and fading. Only on this client (it's the duelling player who needs to see it).
    class SlamRing : MonoBehaviour
    {
        const int Points = 72;
        const float BurstTime = 0.4f;
        LineRenderer line;
        Vector3 centre;
        float radius, born, windup, burstAt = -1f;
        static Material material;

        internal static SlamRing Make(Vector3 centre, float radius, float windup)
        {
            GameObject go = new GameObject("IoV_SlamRing");
            SlamRing ring = go.AddComponent<SlamRing>();
            ring.centre = centre;
            ring.radius = radius;
            ring.windup = windup;
            ring.born = Time.time;
            ring.line = go.AddComponent<LineRenderer>();
            ring.line.useWorldSpace = true;
            ring.line.loop = true;
            ring.line.positionCount = Points;
            ring.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.line.receiveShadows = false;
            if (material == null)
            {
                Shader shader = null;
                foreach (string name in new[] { "Sprites/Default", "Legacy Shaders/Particles/Additive", "Unlit/Transparent" })
                    if ((shader = Shader.Find(name)) != null) break;
                if (shader != null) material = new Material(shader);
            }
            if (material != null) ring.line.sharedMaterial = material;
            ring.Draw(radius, 0.12f, new Color(1f, 0.8f, 0.3f, 0.3f));
            return ring;
        }

        internal void Burst() { burstAt = Time.time; }

        // A circle on the ground: each point dropped onto whatever's under it.
        void Draw(float r, float width, Color colour)
        {
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = colour;
            for (int i = 0; i < Points; i++)
            {
                float a = i * Mathf.PI * 2f / Points;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                RaycastHit hit;
                if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out hit, 5f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain"), QueryTriggerInteraction.Ignore))
                    p.y = hit.point.y;
                line.SetPosition(i, p + Vector3.up * 0.06f);
            }
        }

        void Update()
        {
            if (burstAt < 0f)
            {
                // winding up: brighter and thicker, pulsing, gold going to fire
                float k = Mathf.Clamp01((Time.time - born) / windup);
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * (6f + 14f * k));
                Color c = Color.Lerp(new Color(1f, 0.82f, 0.3f), new Color(1f, 0.35f, 0.1f), k);
                c.a = Mathf.Lerp(0.3f, 0.95f, k) * pulse;
                Draw(radius, Mathf.Lerp(0.1f, 0.22f, k), c);
                if (Time.time - born > windup + 1f) Destroy(gameObject); // (never struck: gone all the same)
                return;
            }
            float t = (Time.time - burstAt) / BurstTime;
            if (t >= 1f) { Destroy(gameObject); return; }
            Color wave = new Color(1f, 0.75f, 0.35f, 1f - t);
            Draw(Mathf.Lerp(0.6f, radius * 1.15f, t), Mathf.Lerp(0.5f, 0.15f, t), wave);
        }
    }

    // The bouts, on the duelling player's game (on Valhalla's rock, alongside its sky).
    class ValhallaDuel : MonoBehaviour
    {
        internal const string WonKey = "iovvalhalladuelwon"; // the world's key: the einherjar have accepted you
        internal const string HafdisWonKey = "iovt5duelwon";  // T5: Hafdís has yielded in the ring
        internal static ValhallaDuel Current;
        internal static bool Active { get { return Current != null && Current.round > 0; } }
        internal static bool HafdisBout { get { return Active && Current.hafdisBout; } }

        // What's playing while a bout is on (ValhallaSky.Music): the first two, then the champion's; Hafdís's
        // a sorrowful one of its own (the dragon queen's, high in the mountains).
        internal static readonly string[] BoutMusic = { "boss_eikthyr" }, ChampionMusic = { "boss_frozenking" }, HafdisMusic = { "boss_moder", "boss_bonemass" };
        internal static string[] Music { get { return !Active || Current.waitingForYou ? null : Current.hafdisBout ? HafdisMusic : Current.round >= 3 ? ChampionMusic : BoutMusic; } }

        bool hafdisBout;      // T5: this bout is Hafdís's
        bool waitingForYou;   // her bout is set, and waits for you to come into the ring
        bool calledIn;        // the ring's master has called you to it
        internal static readonly Vector3 HafdisHair = new Vector3(1f, 0.9f, 0.62f);

        // T5: Hafdís, asked at the rock's edge, will meet you in the ring: the bout begins once you're in it.
        internal void BeginHafdis()
        {
            if (Active) return;
            if (ValhallaPeople.Current != null) ValhallaPeople.Current.RingCleared(true);
            hafdisBout = true;
            waitingForYou = true;
            calledIn = false;
            round = 1;
            boutAt = Time.time + 6f; // (she leaves the rock's edge first)
            lastHealth = -1f;
            Message("$iov_t5_wait");
            Plugin.Debug("T5: Hafdís waits in the ring");
        }

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
            ValhallaSpeaker.Shout(ValhallaSpeaker.RingMaster, "$iov_ringmaster_won");
            Story.Set(WonKey); // (and with it the champion's oath, told a little after: ValhallaGifts)
            if (ValhallaPeople.Current != null) ValhallaPeople.Current.CaptainsArrive(); // (T2: the captains come to Sævar's table)
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
            // (her bout not won: back at the rock's edge, to be asked again; those who watched go)
            if (hafdisBout && ValhallaPeople.Current != null && !Story.Key(HafdisWonKey)) { ValhallaPeople.Current.HafdisAway(false); ValhallaPeople.Current.Witnesses(false); }
            hafdisBout = waitingForYou = false;
            round = 0;
            endAt = -1f;
            outsideSince = -1f;
        }

        // She has yielded: her farewell (ValhallaPeople.Ceremony: those who watched come to her, a word each,
        // her last, the light), which ends it (Stop); and with it the tale (the world's keys first, so a
        // farewell missed, leaving Valhalla in the middle of it, still leaves the tale done).
        void HafdisWon(Character her)
        {
            Message("$iov_t5_won");
            Story.Set(HafdisWonKey);
            Story.Set(ValhallaPeople.DoneKey);
            EinherjarFighter f = her != null ? her.GetComponent<EinherjarFighter>() : null;
            if (ValhallaPeople.Current != null && f != null)
            {
                f.ceremony = true;
                endAt = Time.time + 300f; // (the ceremony ends it before then)
                ValhallaPeople.Current.Ceremony(her);
            }
            else endAt = Time.time + 11f; // (she kneels, says her last, and the light takes her)
            Plugin.Debug("T5: Hafdís yielded");
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
            Vector3 centre = RingCentre;
            // Hafdís's: she comes into the ring a little after you've asked her (once she has had her say and
            // gone in the light), and waits there for you, still; the ring's master calls you in as you come
            // near; the bout begins as you step in between its columns. (1.10.104 waited for you to come within
            // 9 m of its middle before she came at all: standing at its edge, she never did.)
            if (waitingForYou)
            {
                if (pending.Count > 0)
                {
                    if (Time.time >= pendingAt) { foreach (System.Action make in pending) make(); pending.Clear(); }
                    return;
                }
                if (fighters.Count == 0 && Time.time >= boutAt) { Spawn(player, centre); return; }
                float off = Utils.DistanceXZ(player.transform.position, centre);
                if (!calledIn && fighters.Count > 0 && off < 32f)
                {
                    calledIn = true;
                    ValhallaSpeaker.Shout(ValhallaSpeaker.RingMaster, "$iov_t5_ringmaster");
                }
                if (off > RingRadius + 2f || fighters.Count == 0) return;
                waitingForYou = false;
                foreach (Character c in fighters)
                {
                    EinherjarFighter f = c != null ? c.GetComponent<EinherjarFighter>() : null;
                    if (f != null) f.Fight();
                    EyesOn(c, player);
                }
                if (ValhallaPeople.Current != null) ValhallaPeople.Current.Witnesses(true); // (Sævar and the captains come to watch)
                Message("$iov_t5_bout");
                Plugin.Debug("T5: Hafdís's bout begins");
                return;
            }
            // out of the ring for a moment: you've left it
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
            Character her = fighters.Count > 0 ? fighters[0] : null;
            fighters.Clear();
            if (hafdisBout) { HafdisWon(her); return; }
            if (round >= 3) { Win(); return; }
            player.Heal(player.GetMaxHealth() * 0.35f, true);
            Next(round + 1);
        }

        // ---- the fighters ----

        // The strongest boss this world has beaten: 0 none, 1 Eikthyr ... 7 Fader.
        internal static int Tier()
        {
            if (ZoneSystem.instance == null) return 0;
            string[] keys = { "defeated_eikthyr", "defeated_gdking", "defeated_bonemass", "defeated_dragon", "defeated_goblinking", "defeated_queen", "defeated_fader" };
            int tier = 0;
            for (int i = 0; i < keys.Length; i++) if (ZoneSystem.instance.GetGlobalKey(keys[i])) tier = i + 1;
            return tier;
        }

        // By tier (0..7): armour, cape, and each kind of weapon (the first the game has, of a few).
        internal static readonly string[][] Armour =
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
        internal static readonly string[][] Great =
        {
            new[] { "Battleaxe" }, new[] { "Battleaxe" }, new[] { "Battleaxe" }, new[] { "Battleaxe" }, new[] { "BattleaxeCrystal", "Battleaxe" },
            new[] { "BattleaxeCrystal", "Battleaxe" }, new[] { "THSwordKrom", "BattleaxeCrystal" }, new[] { "THSwordSlayer", "THSwordKrom", "BattleaxeCrystal" },
        };
        internal static readonly float[] Health = { 120f, 180f, 260f, 400f, 600f, 850f, 1200f, 1700f };
        // Hafdís's: spears, and round shields (the sea serpent's scales once the world has them)
        static readonly string[][] Spears =
        {
            new[] { "SpearFlint" }, new[] { "SpearBronze", "SpearFlint" }, new[] { "SpearBronze" }, new[] { "SpearElderbark", "SpearBronze" },
            new[] { "SpearWolfFang", "SpearElderbark" }, new[] { "SpearWolfFang" }, new[] { "SpearCarapace", "SpearWolfFang" }, new[] { "SpearSplitner", "SpearCarapace", "SpearWolfFang" },
        };
        static readonly string[][] RoundShields =
        {
            new[] { "ShieldWood" }, new[] { "ShieldBronzeBuckler", "ShieldWood" }, new[] { "ShieldBronzeBuckler" }, new[] { "ShieldBanded" },
            new[] { "ShieldSerpentscale", "ShieldSilver" }, new[] { "ShieldSerpentscale" }, new[] { "ShieldSerpentscale" }, new[] { "ShieldSerpentscale", "ShieldFlametal" },
        };

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
            if (hafdisBout)
            {
                // Hafdís as she was before the curse: a shield-maiden, her hair loose (no helmet), in the
                // sea-dark chitin of the deep with a cape of feathers, a spear and a round shield; as strong as
                // the champion
                Vector3 at = centre + away * In;
                Later(at, () =>
                {
                    Character her = Fighter(EinherjarFighter.HafdisPrefab, at, player, tier, Health[tier] * 2.2f, 1, Pick(Spears, tier), Pick(RoundShields, tier), "Hair20", "", "CapeFeather",
                        new[] { "", "ArmorCarapaceChest", "ArmorCarapaceLegs" });
                    EinherjarFighter f = her != null ? her.GetComponent<EinherjarFighter>() : null;
                    if (f != null) { f.hafdis = true; f.echoPierce = 25f + 12f * tier; f.waiting = waitingForYou; }
                    if (her != null && waitingForYou) her.m_boss = false; // (no bar over the view till the bout: Fight)
                    VisEquipment vis = her != null ? her.GetComponent<VisEquipment>() : null;
                    if (vis != null) vis.SetHairColor(HafdisHair);
                });
            }
            else if (round == 1)
            {
                Vector3 at = centre + away * In;
                Later(at, () =>
                {
                    Character one = Fighter(PrefabName(false), at, player, tier, Health[tier], 0, Pick(Swords, tier), Pick(Shields, tier), "Hair5", "Beard3", Capes[tier]);
                    EinherjarFighter f = one != null ? one.GetComponent<EinherjarFighter>() : null;
                    if (f != null) f.QuickerSoon(1f, 1.2f); // (alone against you: not so long between his blows)
                });
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
                Later(at, () =>
                {
                    Character champion = Fighter(PrefabName(true), at, player, Mathf.Max(tier, 6), Health[tier] * 2.5f, 0, Pick(Great, tier), "", "Hair8", "Beard10", "CapeWolf");
                    EinherjarFighter f = champion != null ? champion.GetComponent<EinherjarFighter>() : null;
                    if (f != null) f.slamDamage = 25f + 18f * tier;
                });
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

        internal static string Pick(string[][] table, int tier)
        {
            foreach (string name in table[Mathf.Clamp(tier, 0, table.Length - 1)])
                if (ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(name) != null) return name;
            return "";
        }

        // One fighter: dressed (its gear given as a creature's default items, set on the prefab just before it's
        // made, which is when the game reads them), its health, and its eyes on you.
        Character Fighter(string prefabName, Vector3 at, Player player, int armourTier, float health, int model, string right, string left, string hair, string beard, string cape, string[] armour = null)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            Humanoid template = prefab != null ? prefab.GetComponent<Humanoid>() : null;
            if (template == null) { Plugin.Log.LogWarning("Duel: no " + prefabName); return null; }
            List<GameObject> items = new List<GameObject>();
            if (armour == null) armour = Armour[Mathf.Clamp(armourTier, 0, Armour.Length - 1)];
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
            EinherjarFighter me = go.GetComponent<EinherjarFighter>();
            if (me != null) { me.modelWanted = model; me.hairWanted = hair ?? ""; me.beardWanted = beard ?? ""; }
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
            return body;
        }
    }
}
