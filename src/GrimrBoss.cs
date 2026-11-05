using UnityEngine;

namespace IslandsOfValheim
{
    // Grímr, the son, first of the hidden islet's keepers: grown up guarding the treasure for his mother,
    // cold as the north. A Jotun warrior in frost colours with a pale blue light on his chest.
    //   0. full: now and then his blow brings ice up around his feet (the Fenring's frost nova, his size):
    //      it hurts and chills;
    //   1. two thirds: a frost shield (the staff of protection's), which takes the hurt until it breaks
    //      or runs out;
    //   2. under half: the shield once more;
    //   3. one third: rage: faster, and some of his blows freeze.
    static class GrimrBoss
    {
        internal const string Nova = "IoV_GrimrNova";
        internal const float Scale = 1.1f;
        internal static readonly Color Glow = new Color(0.6f, 0.85f, 1f);

        internal static void Setup(GameObject go)
        {
            go.transform.localScale = go.transform.localScale * Scale;
            Character body = go.GetComponent<Character>();
            if (body != null) body.m_staggerDamageFactor *= 0.5f;
            BossKit.Tint(go, BossKit.Cast(new Color(0.55f, 0.72f, 0.9f), 0.4f, 1.05f), 0.35f, "Grímr");
            BossKit.AddGlow(go, Glow, 4f, 1.4f, "Grímr");
            go.AddComponent<GrimrFight>();
            HitData.DamageTypes damage = new HitData.DamageTypes();
            damage.m_frost = 45f;
            damage.m_blunt = 20f;
            BossKit.RegisterHurt(Nova, "FenringIceNova_aoe", damage, 0f, 30f);
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
        const float NovaEvery = 18f, NovaEveryRaging = 12f, NovaReach = 5f;
        const float FrostChance = 0.3f, FrostAmount = 25f;
        static readonly float[] stages = { 2f / 3f, 0.45f, 1f / 3f };

        bool raging, novaPending;
        float nextNova;

        protected override string StageKey { get { return "IoV_GrimrStage"; } }
        protected override float[] Thresholds { get { return stages; } }

        protected override string LineFor(int stage)
        {
            if (stage == 1) return "$iov_grimr_stage_1";
            if (stage == 3) return "$iov_grimr_stage_3";
            return null;
        }

        protected override void Awake()
        {
            base.Awake();
            nextNova = Time.time + 8f;
        }

        protected override void Everywhere(int stage)
        {
            bool rage = stage >= 3;
            Shine(rage, 1.4f, 4f);
            if (rage && !raging)
            {
                raging = true;
                Pace(1.2f);
            }
        }

        protected override void Begin(int stage)
        {
            if (stage != 1 && stage != 2) return;
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
            nextNova = Time.time + (Stage >= 3 ? NovaEveryRaging : NovaEvery);
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

        // ---- in his rage, some blows freeze ----

        internal override void Dealing(Character target, HitData hit)
        {
            if (Stage < 3 || hit.m_damage.GetTotalPhysicalDamage() <= 0f || Random.value >= FrostChance) return;
            hit.m_damage.m_frost += FrostAmount;
        }
    }
}
