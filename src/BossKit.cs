using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // What the tale's keepers share as bosses of their own (Ketill came first and has his own, KetillBoss):
    // their looks made of the game's own (a colour cast, a glow on the chest) and a fight in stages by
    // health. The client that runs a keeper decides (stages, abilities); the stage is in its ZDO, so every
    // client shows the words and the looks that go with it.
    static class BossKit
    {
        internal const string GlowChild = "IoV_BossGlow";

        // Every texture of its body pulled towards a colour (Recolor's way: a copy on the GPU), and a
        // little gloss where asked. Nothing on a dedicated server.
        internal static void Tint(GameObject go, Recolor.Shift shift, float gloss, string who)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            try
            {
                foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer) continue;
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material copy = Recolor.Material(materials[i], shift);
                        if (copy == null) continue;
                        if (gloss > 0f && copy.HasProperty("_Glossiness")) copy.SetFloat("_Glossiness", Mathf.Max(copy.GetFloat("_Glossiness"), gloss));
                        materials[i] = copy;
                    }
                    renderer.sharedMaterials = materials;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Could not give " + who + " their look: " + e.Message);
            }
        }

        // A colour cast: every colour pulled part of the way towards one, keeping its light and shade.
        internal static Recolor.Shift Cast(Color towards, float amount, float brightness)
        {
            return c =>
            {
                float light = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                Color target = towards * (0.5f + light * 1.2f);
                Color result = Color.Lerp(c, target, amount) * brightness;
                result.a = c.a;
                return result;
            };
        }

        // A light on the chest that moves with the body.
        internal static void AddGlow(GameObject go, Color color, float range, float intensity, string who)
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
                CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
                glow.transform.SetParent(go.transform, false);
                glow.transform.localPosition = new Vector3(0f, capsule != null ? capsule.height * 0.65f : 1.4f, 0.25f);
            }
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            Plugin.Debug(who + "'s glow on " + (chest != null ? chest.name : "their root"));
        }

        internal static Transform Bone(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        internal static void Effect(string name, Vector3 at)
        {
            GameObject fx = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (fx != null) Object.Instantiate(fx, at, Quaternion.identity);
        }

        // A vanilla effect shown only here, on this client, following parent (its networking left out, so
        // it doesn't linger in the world after the boss is gone).
        internal static GameObject LocalEffect(string name, Transform parent)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (prefab == null) return null;
            ZNetView.m_forceDisableInit = true;
            GameObject go;
            try { go = Object.Instantiate(prefab, parent.position, parent.rotation, parent); }
            finally { ZNetView.m_forceDisableInit = false; }
            foreach (ZSyncTransform sync in go.GetComponentsInChildren<ZSyncTransform>(true)) Object.DestroyImmediate(sync);
            foreach (ZNetView view in go.GetComponentsInChildren<ZNetView>(true)) Object.DestroyImmediate(view);
            foreach (TimedDestruction timed in go.GetComponentsInChildren<TimedDestruction>(true)) Object.DestroyImmediate(timed);
            return go;
        }

        // A thing of ours that hurts, set up as the boss's own: it spares the boss and its side.
        internal static void Spawn(string name, Vector3 at, Character owner)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (prefab == null) return;
            GameObject go = Object.Instantiate(prefab, at, Quaternion.identity);
            foreach (Aoe aoe in go.GetComponentsInChildren<Aoe>(true))
                aoe.Setup(owner, Vector3.zero, -1f, null, null, null);
        }

        // A clone of a vanilla area hurt with nothing of its own to see, dealing what we say.
        internal static void RegisterHurt(string name, string baseName, HitData.DamageTypes damage, float radius, float push)
        {
            if (PrefabManager.Instance.GetPrefab(name) != null || PrefabManager.Instance.GetPrefab(baseName) == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(name, baseName);
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.enabled = false;
                if (ps.gameObject != go) ps.gameObject.SetActive(false);
            }
            foreach (Light light in go.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (Aoe aoe in go.GetComponentsInChildren<Aoe>(true))
            {
                if (radius > 0f)
                {
                    Plugin.Debug(name + " from " + baseName + ": its reach was " + aoe.m_radius + " m, now " + radius + " m");
                    aoe.m_radius = radius;
                }
                aoe.m_damage = damage;
                aoe.m_damagePerLevel = new HitData.DamageTypes();
                if (push > 0f) aoe.m_attackForce = push;
                aoe.m_hitFriendly = false;
                aoe.m_hitOwner = false;
                aoe.m_useAttackSettings = false;
            }
            PrefabManager.Instance.AddPrefab(new Jotunn.Entities.CustomPrefab(go, false));
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

        static BossFight FightOf(Character character)
        {
            return character != null ? character.GetComponent<BossFight>() : null;
        }

        static readonly System.Reflection.FieldInfo attackCharacter = AccessTools.Field(typeof(Attack), "m_character");

        [HarmonyPatch(typeof(Attack), "Start")]
        static class AttackStartPatch
        {
            static void Postfix(Humanoid character, bool __result)
            {
                if (!__result) return;
                BossFight fight = FightOf(character);
                if (fight != null) fight.AttackStarted();
            }
        }

        [HarmonyPatch(typeof(Attack), "OnAttackTrigger")]
        static class AttackTriggerPatch
        {
            static void Postfix(Attack __instance)
            {
                if (attackCharacter == null) return;
                BossFight fight = FightOf(attackCharacter.GetValue(__instance) as Character);
                if (fight != null) fight.AttackLanded();
            }
        }

        // Our status effects' names sit a little higher over their icons, clear of them (a long name like
        // "Grímr's Frost" otherwise touches the icon). The list's rows are reused, so others are put back.
        static readonly System.Reflection.FieldInfo hudRows = AccessTools.Field(typeof(Hud), "m_statusEffects");
        static readonly System.Type textType = AccessTools.TypeByName("TMPro.TMP_Text");
        static readonly System.Collections.Generic.Dictionary<int, Vector2> namePlaces = new System.Collections.Generic.Dictionary<int, Vector2>();
        const float NameRaise = 5f;

        [HarmonyPatch(typeof(Hud), "UpdateStatusEffects")]
        static class StatusNamePatch
        {
            static void Postfix(Hud __instance, System.Collections.Generic.List<StatusEffect> statusEffects)
            {
                if (hudRows == null || textType == null || statusEffects == null) return;
                System.Collections.Generic.List<RectTransform> rows = hudRows.GetValue(__instance) as System.Collections.Generic.List<RectTransform>;
                if (rows == null) return;
                for (int i = 0; i < rows.Count && i < statusEffects.Count; i++)
                {
                    Component text = rows[i] != null ? rows[i].GetComponentInChildren(textType) : null;
                    RectTransform place = text != null ? text.transform as RectTransform : null;
                    if (place == null) continue;
                    int id = place.GetInstanceID();
                    Vector2 home;
                    if (!namePlaces.TryGetValue(id, out home)) { home = place.anchoredPosition; namePlaces[id] = home; }
                    bool ours = statusEffects[i] != null && statusEffects[i].name.StartsWith("IoV_SE_");
                    place.anchoredPosition = ours ? home + new Vector2(0f, NameRaise) : home;
                }
            }
        }

        // A keeper's blow being dealt (on the client that runs it): its own touch (frost, a drain...).
        [HarmonyPatch(typeof(Character), "Damage")]
        static class DealPatch
        {
            static void Prefix(Character __instance, HitData hit)
            {
                if (hit == null) return;
                BossFight fight = FightOf(hit.GetAttacker());
                if (fight != null) fight.Dealing(__instance, hit);
            }
        }
    }

    // A keeper's fight in stages by health. Each keeper says what its stages are and what they do.
    abstract class BossFight : MonoBehaviour
    {
        protected Character body;
        protected ZNetView nview;
        protected Light glow;
        protected float walkSpeed, runSpeed;
        int seenStage = -1;
        float nextStageAllowed;
        protected virtual float StageGap { get { return 10f; } }

        protected abstract string StageKey { get; }
        protected abstract float[] Thresholds { get; } // health left at which stage 1, 2... begin
        protected virtual string LineFor(int stage) { return null; }

        protected virtual void Awake()
        {
            body = GetComponent<Character>();
            nview = GetComponent<ZNetView>();
            Transform t = BossKit.Bone(transform, BossKit.GlowChild);
            glow = t != null ? t.GetComponent<Light>() : null;
            if (body != null) { walkSpeed = body.m_speed; runSpeed = body.m_runSpeed; }
        }

        protected int Stage
        {
            get { return nview != null && nview.IsValid() ? nview.GetZDO().GetInt(StageKey, 0) : 0; }
        }

        void Update()
        {
            if (body == null || nview == null || !nview.IsValid()) return;
            int stage = Stage;
            if (seenStage < 0) seenStage = stage; // came into view mid-fight: no words for what's past
            else if (stage != seenStage)
            {
                seenStage = stage;
                string line = LineFor(stage);
                if (line != null) Say(line);
            }
            Everywhere(stage);
            if (!nview.IsOwner() || body.IsDead()) return;
            float[] thresholds = Thresholds;
            if (stage < thresholds.Length && body.GetHealthPercentage() <= thresholds[stage] && Time.time >= nextStageAllowed)
            {
                stage++;
                nextStageAllowed = Time.time + StageGap; // a heavy blow doesn't run two stages into one
                nview.GetZDO().Set(StageKey, stage);
                Plugin.Debug(name + ": stage " + stage + " at " + Mathf.RoundToInt(body.GetHealthPercentage() * 100f) + "% health");
                Begin(stage);
            }
            Run(stage);
        }

        // On every client, every frame: the looks of the stage.
        protected virtual void Everywhere(int stage) { }
        // On the client that runs it: a stage begins.
        protected virtual void Begin(int stage) { }
        // On the client that runs it, every frame.
        protected virtual void Run(int stage) { }
        // On the client that runs it: one of its attacks starts, or lands.
        internal virtual void AttackStarted() { }
        internal virtual void AttackLanded() { }
        // On the client that runs it: one of its blows is being dealt to target.
        internal virtual void Dealing(Character target, HitData hit) { }

        protected bool Owner
        {
            get { return nview != null && nview.IsValid() && nview.IsOwner() && body != null && !body.IsDead(); }
        }

        protected bool Engaged()
        {
            BaseAI ai = body.GetBaseAI();
            return ai != null && (ai.IsAlerted() || ai.GetTargetCreature() != null);
        }

        protected Character Target(float range)
        {
            BaseAI ai = body.GetBaseAI();
            Character target = ai != null ? ai.GetTargetCreature() : null;
            if (target != null && !target.IsDead()) return target;
            return Engaged() ? Player.GetClosestPlayer(transform.position, range) : null;
        }

        protected bool LocalPlayerWithin(float range)
        {
            Player player = Player.m_localPlayer;
            return player != null && Vector3.Distance(player.transform.position, transform.position) < range;
        }

        protected void Say(string line)
        {
            if (Chat.instance == null || !LocalPlayerWithin(40f)) return;
            Chat.instance.SetNpcText(gameObject, Vector3.up * (body.GetHeight() + 0.6f), 40f, 7f, "", line, false);
        }

        // Faster on its feet (every client sets it: whoever runs it moves it).
        protected void Pace(float factor)
        {
            body.m_speed = walkSpeed * factor;
            body.m_runSpeed = runSpeed * factor;
        }

        // The glow: a slow pulse, or a quicker, brighter one.
        protected void Shine(bool fierce, float baseIntensity, float baseRange)
        {
            if (glow == null) return;
            float pulse = Mathf.Sin(Time.time * (fierce ? 6f : 2f)) * 0.5f + 0.5f;
            glow.intensity = fierce ? baseIntensity * 1.8f + pulse * baseIntensity : baseIntensity * 0.8f + pulse * baseIntensity * 0.4f;
            glow.range = fierce ? baseRange * 1.6f : baseRange;
        }
    }
}
