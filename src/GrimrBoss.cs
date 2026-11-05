using UnityEngine;

namespace IslandsOfValheim
{
    // Grímr, the son, first of the hidden islet's keepers: grown up guarding the treasure for his mother,
    // cold as the north. A Jotun warrior in frost colours with a pale blue light on his chest.
    //   0. full: now and then a breath of frost warns of it, and his blow brings ice up from the ground
    //      around his feet (the Fenring's frost nova, his size): it hurts, and Grímr's frost stiffens you
    //      for a few seconds;
    //   1. three quarters: a frost shield (the staff of protection's), which takes the hurt until it
    //      breaks or runs out;
    //   2. half: rage: a burst of frost, a cold mist about him from then on; faster, harder blows, each of
    //      which stiffens you, and the ice comes more often;
    //   3. a quarter: the shield once more.
    static class GrimrBoss
    {
        internal const string Nova = "IoV_GrimrNova";
        internal const string Chill = "IoV_SE_GrimrChill";
        internal const float Scale = 1.1f;
        internal static readonly Color Glow = new Color(0.6f, 0.85f, 1f);
        internal static SE_Stats chill;

        internal static void Setup(GameObject go)
        {
            go.transform.localScale = go.transform.localScale * Scale;
            Character body = go.GetComponent<Character>();
            if (body != null) body.m_staggerDamageFactor *= 0.5f;
            BossKit.Tint(go, BossKit.Cast(new Color(0.55f, 0.72f, 0.9f), 0.4f, 1.05f), 0.35f, "Grímr");
            BossKit.AddGlow(go, Glow, 4f, 1.4f, "Grímr");
            go.AddComponent<GrimrFight>();

            // Grímr's frost: slower for a few seconds, the game's frost on you while it lasts. (The game's
            // own chill from frost damage is over in a moment.)
            chill = SeafarerGear.Effect(Chill, "iov_se_grimrchill", false);
            chill.m_ttl = 5f;
            chill.m_speedModifier = -0.35f;
            GameObject frost = Jotunn.Managers.PrefabManager.Instance.GetPrefab("vfx_Frost");
            if (frost != null)
            {
                EffectList.EffectData look = new EffectList.EffectData();
                look.m_prefab = frost;
                look.m_enabled = true;
                look.m_attach = true;
                chill.m_startEffects = new EffectList();
                chill.m_startEffects.m_effectPrefabs = new[] { look };
            }

            HitData.DamageTypes damage = new HitData.DamageTypes();
            damage.m_frost = 45f;
            damage.m_blunt = 20f;
            BossKit.RegisterHurt(Nova, "FenringIceNova_aoe", damage, 0f, 30f);
            GameObject nova = Jotunn.Managers.PrefabManager.Instance.GetPrefab(Nova);
            if (nova != null)
                foreach (Aoe aoe in nova.GetComponentsInChildren<Aoe>(true)) aoe.m_statusEffect = Chill;
        }

        // The chill's icon: the game's own frost, once the game's status effects are there.
        internal static void IconChill()
        {
            if (chill == null || chill.m_icon != null || ObjectDB.instance == null) return;
            StatusEffect frost = ObjectDB.instance.GetStatusEffect("Frost".GetStableHashCode());
            if (frost != null) chill.m_icon = frost.m_icon;
        }

        static SE_Shield shield;

        // The staff of protection's shield, made his: it takes a set amount of hurt, for a few seconds.
        internal static SE_Shield Shield()
        {
            if (shield != null) return shield;
            GameObject staff = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("staff_shield_aoe") : null;
            Aoe aoe = staff != null ? staff.GetComponentInChildren<Aoe>(true) : null;
            SE_Shield vanilla = aoe != null && ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(aoe.m_statusEffect.GetStableHashCode()) as SE_Shield : null;
            if (vanilla == null) { Plugin.Log.LogWarning("Grímr: the staff of protection's shield wasn't found; he fights without it."); return null; }
            shield = Object.Instantiate(vanilla);
            shield.name = "IoV_SE_GrimrShield";
            shield.m_ttl = 8f;
            shield.m_ttlPerItemLevel = 0;
            shield.m_absorbDamage = 400f;
            shield.m_absorbDamagePerSkillLevel = 0f;
            shield.m_absorbDamageWorldLevel = 0f;
            Plugin.Debug("Grímr's shield from " + vanilla.name);
            return shield;
        }
    }

    class GrimrFight : BossFight
    {
        const float NovaEvery = 18f, NovaEveryRaging = 10f, NovaReach = 5f;
        const float RageHarder = 1.2f, RageFrost = 20f;
        const int ShieldStage = 1, RageStage = 2, ShieldAgainStage = 3;
        static readonly float[] stages = { 0.75f, 0.5f, 0.25f };

        bool raging, novaPending;
        float nextNova;
        GameObject aura;

        protected override string StageKey { get { return "IoV_GrimrStage"; } }
        protected override float[] Thresholds { get { return stages; } }

        protected override string LineFor(int stage)
        {
            if (stage == ShieldStage) return "$iov_grimr_stage_1";
            if (stage == RageStage) return "$iov_grimr_stage_3";
            return null;
        }

        protected override void Awake()
        {
            base.Awake();
            nextNova = Time.time + 8f;
            GrimrBoss.IconChill();
        }

        protected override void Everywhere(int stage)
        {
            bool rage = stage >= RageStage;
            Shine(rage, 1.4f, 4f);
            if (rage && !raging)
            {
                raging = true;
                Pace(1.3f);
                aura = BossKit.LocalEffect("vfx_Frost", transform); // a cold mist about him, on every client
            }
        }

        void OnDestroy()
        {
            if (aura != null) Destroy(aura);
        }

        protected override void Begin(int stage)
        {
            if (stage == RageStage)
            {
                Vector3 at = BossKit.Ground(transform.position) ?? transform.position;
                BossKit.Effect("fx_shield_start_frost", transform.position + Vector3.up);
                BossKit.Effect("fx_fenring_icenova", at);
                BossKit.Spawn(GrimrBoss.Nova, at, body);
                nextNova = Time.time + NovaEveryRaging;
                Plugin.Debug("Grímr rages");
                return;
            }
            if (stage != ShieldStage && stage != ShieldAgainStage) return;
            SE_Shield se = GrimrBoss.Shield();
            if (se == null) return;
            body.GetSEMan().AddStatusEffect(se, true, 0, 0f, (short)0);
            BossKit.Effect("sfx_dverger_ice_aoe_start", transform.position);
            Plugin.Debug("Grímr raises his shield");
        }

        // ---- the frost nova, with one of his blows ----

        internal override void AttackStarted()
        {
            if (!Owner || Time.time < nextNova) return;
            Character target = Target(30f);
            if (target == null || Vector3.Distance(target.transform.position, transform.position) > NovaReach) return;
            novaPending = true;
            nextNova = Time.time + (Stage >= RageStage ? NovaEveryRaging : NovaEvery);
            BossKit.Effect("sfx_dverger_ice_aoe_start", transform.position);
            BossKit.Effect("fx_fenring_frost", transform.position + Vector3.up * 0.3f);
        }

        internal override void AttackLanded()
        {
            if (!novaPending) return;
            novaPending = false;
            if (!Owner) return;
            Vector3 at = BossKit.Ground(transform.position) ?? transform.position;
            BossKit.Effect("fx_fenring_icenova", at);
            BossKit.Spawn(GrimrBoss.Nova, at, body);
            Plugin.Debug("Grímr: frost nova");
        }

        // ---- in his rage every blow is harder and stiffens you ----

        internal override void Dealing(Character target, HitData hit)
        {
            if (Stage < RageStage || hit.m_damage.GetTotalPhysicalDamage() <= 0f) return;
            hit.m_damage.Modify(RageHarder);
            hit.m_damage.m_frost += RageFrost;
            hit.m_statusEffectHash = GrimrBoss.Chill.GetStableHashCode();
        }
    }
}
