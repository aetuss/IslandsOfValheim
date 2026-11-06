using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Ketill as a boss of his own, made of the game's own parts: an Elite Draugr grown bigger, dark and wet
    // as one who lay long in the sea, with the fragment glowing sea-green on his chest. His fight goes in
    // three stages by his health:
    //   0. full: now and then a heavy blow, his blade strikes the ground and throws back whoever is near;
    //   1. two thirds: he calls his dead crew up out of the ground (once);
    //   2. one third: the fragment's curse wakes, he glows and moves faster, the sky darkens and lightning
    //      falls where you stand (a mist marks the spot first).
    // The client that runs him does the fight; the stage is in his ZDO, so every client shows the words,
    // the glow and the storm. When he falls the fragment's light goes out slowly where he lay.
    static class KetillBoss
    {
        internal const string Tide = "IoV_KetillTide";           // the blow of his tide slam
        internal const string Bolt = "IoV_KetillBolt";           // the curse's lightning
        internal const string LastLight = "IoV_KetillLastLight"; // the fragment's glow going out where he fell
        internal const string GlowChild = "IoV_KetillGlow";
        internal const float Scale = 1.3f;
        internal const float Health = 700f; // before his star: about twice that in the fight
        internal static readonly Color Glow = new Color(0.35f, 1f, 0.72f);

        // Drowned: every colour pulled towards a dark, wet sea-green, keeping its light and shade.
        static Color Drowned(Color c)
        {
            float light = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            Color sea = new Color(0.2f, 0.36f, 0.3f) * (0.55f + light * 1.2f);
            Color result = Color.Lerp(c, sea, 0.55f) * 0.85f;
            result.a = c.a;
            return result;
        }

        internal static void Setup(GameObject go)
        {
            go.transform.localScale = go.transform.localScale * Scale;
            Humanoid body = go.GetComponent<Humanoid>();
            if (body != null)
            {
                body.m_health = Health;
                body.m_staggerDamageFactor *= 0.5f; // a boss: not so easily knocked off his feet
            }
            Look(go);
            AddGlow(go);
            Cape(go, body);
            go.AddComponent<KetillFight>();
            RegisterTide();
            RegisterBolt();
            RegisterLastLight();
        }

        static void Look(GameObject go)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return; // a dedicated server: no looks
            try
            {
                Recolor.Shift drowned = Drowned;
                foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer) continue;
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material copy = Recolor.Material(materials[i], drowned);
                        if (copy == null) continue;
                        if (copy.HasProperty("_Glossiness")) copy.SetFloat("_Glossiness", Mathf.Max(copy.GetFloat("_Glossiness"), 0.55f)); // wet
                        materials[i] = copy;
                    }
                    renderer.sharedMaterials = materials;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Could not give Ketill his drowned look: " + e.Message);
            }
        }

        // The fragment about his neck: a light on his chest that moves with him.
        static void AddGlow(GameObject go)
        {
            Transform chest = Bone(go.transform, "Spine2") ?? Bone(go.transform, "Spine1") ?? Bone(go.transform, "Spine");
            GameObject glow = new GameObject(GlowChild);
            if (chest != null)
            {
                glow.transform.SetParent(chest, false);
                glow.transform.localPosition = Vector3.zero;
            }
            else
            {
                glow.transform.SetParent(go.transform, false);
                glow.transform.localPosition = new Vector3(0f, 1.4f, 0.25f);
            }
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Glow;
            light.range = 3.5f;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;
            Plugin.Debug("Ketill's glow on " + (chest != null ? chest.name : "his root"));
        }

        internal static Transform Bone(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // A Brim captain's cape on him, if his body can wear one the way a player does (the same skeleton);
        // otherwise he goes without.
        static void Cape(GameObject go, Humanoid body)
        {
            GameObject cape = PrefabManager.Instance.GetPrefab(SeafarerGear.Cape);
            VisEquipment vis = go.GetComponent<VisEquipment>();
            if (cape == null || body == null || vis == null || vis.m_bodyModel == null) { Plugin.Debug("Ketill: no cape (nothing to wear it on)"); return; }
            ItemDrop item = cape.GetComponent<ItemDrop>();
            Transform attach = item != null ? cape.transform.Find("attach_skin") : null;
            SkinnedMeshRenderer skin = attach != null ? attach.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (skin == null || skin.bones.Length != vis.m_bodyModel.bones.Length)
            {
                Plugin.Debug("Ketill: no cape (his skeleton isn't a player's: " + (skin != null ? skin.bones.Length + " vs " + vis.m_bodyModel.bones.Length : "no cape skin") + ")");
                return;
            }
            List<GameObject> items = new List<GameObject>(body.m_defaultItems ?? new GameObject[0]);
            items.Add(cape);
            body.m_defaultItems = items.ToArray();
            Plugin.Debug("Ketill wears a Brim cape");
        }

        // The heavy blow's hurt: the troll's ground slam (only the hurt, nothing to see; a sword-sized hit
        // is shown on its own when it lands), Ketill's own and as strong as a Swamp boss's should be.
        static void RegisterTide()
        {
            if (PrefabManager.Instance.GetPrefab(Tide) != null) return;
            string baseName = PrefabManager.Instance.GetPrefab("troll_groundslam_aoe") != null ? "troll_groundslam_aoe" : "bonemass_aoe";
            if (PrefabManager.Instance.GetPrefab(baseName) == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(Tide, baseName);
            NoLooks(go);
            Aoe aoe = go.GetComponentInChildren<Aoe>(true);
            if (aoe != null)
            {
                float radius = 5f;
                if (aoe.m_radius > 0.5f && aoe.m_useCollider == null) go.transform.localScale = go.transform.localScale * (radius / aoe.m_radius);
                Plugin.Debug("Ketill's tide from " + baseName + ": its reach was " + aoe.m_radius + " m, now " + radius + " m");
                aoe.m_radius = radius;
                HitData.DamageTypes damage = new HitData.DamageTypes();
                damage.m_blunt = 32f;
                aoe.m_damage = damage;
                aoe.m_attackForce = Mathf.Max(aoe.m_attackForce, 40f); // it throws you back a little
                aoe.m_damagePerLevel = new HitData.DamageTypes();
                aoe.m_hitFriendly = false;
                aoe.m_hitOwner = false;
                aoe.m_useAttackSettings = false;
            }
            PrefabManager.Instance.AddPrefab(new Jotunn.Entities.CustomPrefab(go, false));
        }

        // Only the hurt: whatever the base shows (particles, lights) is turned off.
        static void NoLooks(GameObject go)
        {
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.enabled = false;
                if (ps.gameObject != go) ps.gameObject.SetActive(false);
            }
            foreach (Light light in go.GetComponentsInChildren<Light>(true)) light.enabled = false;
        }

        // The curse's lightning: the game's lightning strike, set to hurt as much as a Swamp boss should.
        static void RegisterBolt()
        {
            if (PrefabManager.Instance.GetPrefab(Bolt) != null || PrefabManager.Instance.GetPrefab("lightningAOE") == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(Bolt, "lightningAOE");
            foreach (Aoe aoe in go.GetComponentsInChildren<Aoe>(true))
            {
                HitData.DamageTypes damage = new HitData.DamageTypes();
                damage.m_lightning = 40f;
                aoe.m_damage = damage;
                aoe.m_damagePerLevel = new HitData.DamageTypes();
                aoe.m_hitFriendly = false;
                aoe.m_hitOwner = false;
                aoe.m_useAttackSettings = false;
            }
            PrefabManager.Instance.AddPrefab(new Jotunn.Entities.CustomPrefab(go, false));
        }

        // Where he fell: the fragment's light, going out over a few seconds.
        static void RegisterLastLight()
        {
            if (PrefabManager.Instance.GetPrefab(LastLight) != null) return;
            GameObject go = PrefabManager.Instance.CreateEmptyPrefab(LastLight, true);
            // CreateEmptyPrefab makes a cube; only the light should show.
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Glow;
            light.range = 6f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;
            go.AddComponent<FadingLight>();
            TimedDestruction timed = go.AddComponent<TimedDestruction>();
            timed.m_timeout = 6f;
            timed.m_triggerOnAwake = true;
            PrefabManager.Instance.AddPrefab(new Jotunn.Entities.CustomPrefab(go, false));
        }

        internal static void Effect(string name, Vector3 at)
        {
            GameObject fx = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (fx != null) Object.Instantiate(fx, at, Quaternion.identity);
        }

        // A thing of ours that hurts (the tide, a bolt), set up as Ketill's: it spares him and his crew.
        internal static void Spawn(string name, Vector3 at, Character owner)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (prefab == null) return;
            GameObject go = Object.Instantiate(prefab, at, Quaternion.identity);
            foreach (Aoe aoe in go.GetComponentsInChildren<Aoe>(true))
                aoe.Setup(owner, Vector3.zero, -1f, null, null, null);
        }

        static readonly int groundMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece");

        // The ground under a point (islet ground is a mesh, not terrain), or null if there's only water.
        internal static Vector3? Ground(Vector3 p)
        {
            RaycastHit hit;
            if (!Physics.Raycast(p + Vector3.up * 8f, Vector3.down, out hit, 20f, groundMask)) return null;
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            if (hit.point.y < water + 0.2f) return null;
            return hit.point;
        }

        static KetillFight FightOf(Humanoid character)
        {
            return character != null ? character.GetComponent<KetillFight>() : null;
        }

        static readonly System.Reflection.FieldInfo attackCharacter = AccessTools.Field(typeof(Attack), "m_character");

        // A blow begins: if his slam is ready, this one is it. A rumble and a mist at his feet give it away.
        [HarmonyPatch(typeof(Attack), "Start")]
        static class SlamStartPatch
        {
            static void Postfix(Humanoid character, bool __result)
            {
                if (!__result) return;
                KetillFight fight = FightOf(character);
                if (fight != null) fight.AttackStarted();
            }
        }

        // ...and lands: the poisoned water spreads from where he stands.
        [HarmonyPatch(typeof(Attack), "OnAttackTrigger")]
        static class SlamTriggerPatch
        {
            static void Postfix(Attack __instance)
            {
                if (attackCharacter == null) return;
                KetillFight fight = FightOf(attackCharacter.GetValue(__instance) as Humanoid);
                if (fight != null) fight.AttackLanded();
            }
        }

        // His blade lay long in the swamp: now and then a cut of his carries poison too (only the game's
        // own poisoning shows, nothing of ours). On the client that runs him, where his blows are dealt.
        const float PoisonChance = 0.3f, PoisonAmount = 20f;

        [HarmonyPatch(typeof(Character), "Damage")]
        static class PoisonBladePatch
        {
            static void Prefix(HitData hit)
            {
                if (hit == null || hit.m_damage.m_slash <= 0f) return; // his sword, not the heavy blow or the lightning
                Character attacker = hit.GetAttacker();
                if (attacker == null || Utils.GetPrefabName(attacker.gameObject) != Story.Ketill || Random.value >= PoisonChance) return;
                hit.m_damage.m_poison += PoisonAmount;
            }
        }

        // He falls: his last words, and the fragment's light going out where he lay.
        [HarmonyPatch(typeof(Character), "OnDeath")]
        static class DeathPatch
        {
            static void Prefix(Character __instance)
            {
                if (__instance == null || Utils.GetPrefabName(__instance.gameObject) != Story.Ketill) return;
                Transform glow = Bone(__instance.transform, GlowChild);
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(LastLight) : null;
                Vector3 chest = glow != null ? glow.position : __instance.transform.position + Vector3.up;
                if (prefab != null) Object.Instantiate(prefab, chest, Quaternion.identity);
                Effect("vfx_ghost_death", chest); // what was left of him goes free
                Player player = Player.m_localPlayer;
                if (player != null && Vector3.Distance(player.transform.position, __instance.transform.position) < 60f)
                    player.Message(MessageHud.MessageType.Center, Texts.Get("iov_story_says", "$iov_ketill", "$iov_ketill_death"), 0, null, false);
                Plugin.Debug("Ketill falls");
            }
        }
    }

    // Ketill's fight, on his body. The client that runs him decides; every client shows.
    class KetillFight : MonoBehaviour
    {
        const string StageKey = "IoV_KetillStage";
        const float CrewAt = 2f / 3f, CurseAt = 1f / 3f;
        const float SlamEvery = 22f, SlamEveryCursed = 15f, SlamReach = 5f;
        const float BoltEveryMin = 8f, BoltEveryMax = 12f, BoltWarning = 1.8f, BoltReach = 30f;
        const float SlamAhead = 1.6f; // where his blade meets the ground, in front of him
        const float StormReach = 60f;
        static readonly string[] Crew = { "Draugr", "Draugr", "Draugr_Ranged" };
        static readonly int[] CrewLevel = { 1, 2, 1 };

        Character body;
        ZNetView nview;
        Light glow;
        int seenStage = -1;
        bool introduced, cursedPace, slamming, forcedStorm;
        float nextSlam, nextBolt, nextWet;
        float walkSpeed, runSpeed;

        void Awake()
        {
            body = GetComponent<Character>();
            nview = GetComponent<ZNetView>();
            Transform t = KetillBoss.Bone(transform, KetillBoss.GlowChild);
            glow = t != null ? t.GetComponent<Light>() : null;
            if (body != null) { walkSpeed = body.m_speed; runSpeed = body.m_runSpeed; }
            nextSlam = Time.time + 6f;
            nextBolt = Time.time + 3f;
        }

        int Stage
        {
            get { return nview != null && nview.IsValid() ? nview.GetZDO().GetInt(StageKey, 0) : 0; }
        }

        void Update()
        {
            if (body == null || nview == null || !nview.IsValid()) return;
            int stage = Stage;
            Shine(stage);
            if (seenStage < 0) seenStage = stage; // came into view mid-fight: no words for what's past
            else if (stage != seenStage)
            {
                seenStage = stage;
                Say("$iov_ketill_stage_" + stage);
            }
            if (!introduced && Engaged() && LocalPlayerWithin(25f))
            {
                introduced = true;
                if (stage == 0) Say("$iov_ketill_intro");
            }
            Storm(stage >= 2 && !body.IsDead() && LocalPlayerWithin(StormReach));
            if (stage >= 2 && !cursedPace)
            {
                cursedPace = true;
                body.m_speed = walkSpeed * 1.2f;
                body.m_runSpeed = runSpeed * 1.2f;
            }
            if (!nview.IsOwner() || body.IsDead()) return;
            Run(stage);
        }

        void OnDestroy()
        {
            Storm(false);
        }

        // The fragment's glow: a slow pulse, brighter and quicker once the curse is awake.
        void Shine(int stage)
        {
            if (glow == null) return;
            bool cursed = stage >= 2;
            float pulse = Mathf.Sin(Time.time * (cursed ? 6f : 2f)) * 0.5f + 0.5f;
            glow.intensity = cursed ? 2.2f + pulse * 1.3f : 1.1f + pulse * 0.5f;
            glow.range = cursed ? 6f : 3.5f;
        }

        void Run(int stage)
        {
            if (Time.time >= nextWet)
            {
                nextWet = Time.time + 20f;
                SEMan effects = body.GetSEMan();
                int wet = "Wet".GetStableHashCode();
                if (effects != null && !effects.HaveStatusEffect(wet)) effects.AddStatusEffect(wet, true, 0, 0f, (short)0);
            }
            float health = body.GetHealthPercentage();
            if (stage == 0 && health <= CrewAt)
            {
                SetStage(1);
                StartCoroutine(RaiseCrew());
            }
            else if (stage == 1 && health <= CurseAt)
            {
                SetStage(2);
                nextBolt = Time.time + 2.5f;
            }
            if (stage >= 2 && Time.time >= nextBolt)
            {
                nextBolt = Time.time + Random.Range(BoltEveryMin, BoltEveryMax);
                Character target = Target();
                if (target != null && Vector3.Distance(target.transform.position, transform.position) < BoltReach)
                    StartCoroutine(Strike(target.transform.position));
            }
        }

        void SetStage(int stage)
        {
            nview.GetZDO().Set(StageKey, stage);
            Plugin.Debug("Ketill: stage " + stage + " at " + Mathf.RoundToInt(body.GetHealthPercentage() * 100f) + "% health");
        }

        bool Engaged()
        {
            BaseAI ai = body.GetBaseAI();
            return ai != null && (ai.IsAlerted() || ai.GetTargetCreature() != null);
        }

        Character Target()
        {
            BaseAI ai = body.GetBaseAI();
            Character target = ai != null ? ai.GetTargetCreature() : null;
            if (target != null && !target.IsDead()) return target;
            return Engaged() ? Player.GetClosestPlayer(transform.position, BoltReach) : null;
        }

        // ---- the tide slam (on the client that runs him) ----

        internal void AttackStarted()
        {
            if (nview == null || !nview.IsValid() || !nview.IsOwner() || body.IsDead() || Time.time < nextSlam) return;
            Character target = Target();
            if (target == null || Vector3.Distance(target.transform.position, transform.position) > SlamReach) return;
            slamming = true;
            nextSlam = Time.time + (Stage >= 2 ? SlamEveryCursed : SlamEvery);
            KetillBoss.Effect("sfx_sledge_swing", transform.position);
            KetillBoss.Effect("vfx_prespawn", SlamPoint());
        }

        // The blow lands: the ground breaks where his blade hits it, and the poisoned water wells up there.
        internal void AttackLanded()
        {
            if (!slamming) return;
            slamming = false;
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
            Vector3 at = SlamPoint();
            // A sword's blow, not a troll's: the iron sledge's hit on the ground, his size.
            KetillBoss.Effect("vfx_sledge_iron_hit", at);
            KetillBoss.Effect("sfx_sledge_iron_hit", at);
            KetillBoss.Spawn(KetillBoss.Tide, at, body);
            Plugin.Debug("Ketill: tide slam");
        }

        Vector3 SlamPoint()
        {
            Vector3 ahead = transform.position + transform.forward * SlamAhead * KetillBoss.Scale;
            Vector3? ground = KetillBoss.Ground(ahead);
            return ground ?? transform.position;
        }

        // ---- his crew (once, at two thirds) ----

        System.Collections.IEnumerator RaiseCrew()
        {
            List<Vector3> spots = new List<Vector3>();
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < Crew.Length; i++)
            {
                float deg = start + i * 360f / Crew.Length;
                Vector3? spot = null;
                foreach (float r in new[] { 5f, 3.5f, 2.5f })
                {
                    Vector3 p = transform.position + Quaternion.Euler(0f, deg, 0f) * Vector3.forward * r;
                    spot = KetillBoss.Ground(p);
                    if (spot != null) break;
                }
                if (spot == null) continue; // only sea there
                spots.Add(spot.Value);
                KetillBoss.Effect("vfx_prespawn", spot.Value);
            }
            yield return new WaitForSeconds(2.2f);
            if (ZNetScene.instance == null || nview == null || !nview.IsValid()) yield break;
            for (int i = 0; i < spots.Count; i++)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(Crew[i]);
                if (prefab == null) continue;
                KetillBoss.Effect("vfx_DraugrSpawn", spots[i]);
                Vector3 look = transform.position - spots[i];
                look.y = 0f;
                GameObject go = Instantiate(prefab, spots[i] + Vector3.up * 0.2f, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
                Character c = go.GetComponent<Character>();
                if (c != null) c.SetLevel(CrewLevel[i]);
                BaseAI ai = go.GetComponent<BaseAI>();
                if (ai != null) ai.Alert();
            }
            Plugin.Debug("Ketill: " + spots.Count + " of his crew rise");
        }

        // ---- the curse's lightning (from a third down) ----

        System.Collections.IEnumerator Strike(Vector3 where)
        {
            Vector3 at = KetillBoss.Ground(where) ?? where;
            KetillBoss.Effect("vfx_prespawn", at);
            KetillBoss.Effect("sfx_StaffLightning_charge", at);
            yield return new WaitForSeconds(BoltWarning);
            if (nview == null || !nview.IsValid() || body == null || body.IsDead()) yield break;
            KetillBoss.Spawn(KetillBoss.Bolt, at, body);
        }

        // The sky over the fight once the curse is awake, on each client near it. Only ever undoes its own
        // storm, and leaves the weather alone while the restless sea has it (the player carries a fragment).
        void Storm(bool want)
        {
            EnvMan env = EnvMan.instance;
            Player player = Player.m_localPlayer;
            if (env == null || want == forcedStorm) return;
            if (want && player != null && Story.CarriesPiece(player)) return;
            forcedStorm = want;
            if (!want && player != null && Story.CarriesPiece(player)) return;
            env.SetForceEnvironment(want ? "ThunderStorm" : "");
            Plugin.Debug("Ketill's storm " + (want ? "comes" : "goes"));
        }

        bool LocalPlayerWithin(float range)
        {
            Player player = Player.m_localPlayer;
            return player != null && Vector3.Distance(player.transform.position, transform.position) < range;
        }

        void Say(string line)
        {
            if (Chat.instance == null || !LocalPlayerWithin(40f)) return;
            Chat.instance.SetNpcText(gameObject, Vector3.up * (body.GetHeight() + 0.6f), 40f, 7f, "", line, false);
        }
    }

    // A light that goes out over a few seconds (the fragment's, where Ketill fell).
    class FadingLight : MonoBehaviour
    {
        const float Seconds = 4f;
        Light light;
        float start, from;

        void Awake()
        {
            light = GetComponent<Light>();
            start = Time.time;
            from = light != null ? light.intensity : 0f;
        }

        void Update()
        {
            if (light == null) return;
            float t = Mathf.Clamp01((Time.time - start) / Seconds);
            light.intensity = from * (1f - t) * (1f - t);
        }
    }
}
