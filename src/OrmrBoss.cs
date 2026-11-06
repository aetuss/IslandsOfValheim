using System.Collections;
using UnityEngine;

namespace IslandsOfValheim
{
    // Ormr, the father, second of the hidden islet's keepers: the one who held out longest against the
    // curse, half of him given to it now, with his dead crew's voices in his head. A Fenring cultist in dark
    // colours with the third fragment's darkened violet glowing on his chest.
    //   0. full: now and then he goes into a mist and comes out of another one behind you;
    //   1. two thirds: the shades of his crew come for you (wraiths);
    //   2. one third: the curse feeds him: part of what he deals you heals him.
    // (The class isn't called OrmrBoss: that's Story's name for his prefab.)
    static class OrmrBossFight
    {
        internal const float Scale = 1.05f;
        internal static readonly Color Glow = new Color(0.62f, 0.32f, 1f);

        internal const string Shade = "IoV_OrmrShade";
        internal const string LastLight = "IoV_OrmrLastLight";

        // When he falls the voices stop: his shades go, one after the other.
        internal static IEnumerator Silence(Vector3 where)
        {
            yield return new WaitForSeconds(0.8f);
            foreach (Character c in new System.Collections.Generic.List<Character>(Character.GetAllCharacters()))
            {
                if (c == null || c.IsDead() || Utils.GetPrefabName(c.gameObject) != Shade || Vector3.Distance(c.transform.position, where) > 80f) continue;
                BossKit.LetGo(c); // it dies its own way
                yield return new WaitForSeconds(0.4f);
            }
        }

        internal static void Setup(GameObject go)
        {
            go.transform.localScale = go.transform.localScale * Scale;
            Character body = go.GetComponent<Character>();
            BossKit.Steady(body, "Ormr");
            BossKit.Tint(go, BossKit.Cast(new Color(0.26f, 0.2f, 0.32f), 0.45f, 0.85f), 0f, "Ormr");
            BossKit.AddGlow(go, Glow, 3.5f, 1.3f, "Ormr");
            go.AddComponent<OrmrFight>();
            if (body != null) RegisterShade(body.m_faction);
            BossKit.RegisterLight(LastLight, Glow, 6f, 3f, 6f);
        }

        // His crew's shades: wraiths on his side (a wraith of its own would go for him too), that don't
        // fade with the daylight as wraiths do.
        static void RegisterShade(Character.Faction side)
        {
            if (Jotunn.Managers.PrefabManager.Instance.GetPrefab(Shade) != null || Jotunn.Managers.PrefabManager.Instance.GetPrefab("Wraith") == null) return;
            GameObject go = Jotunn.Managers.PrefabManager.Instance.CreateClonedPrefab(Shade, "Wraith");
            Character c = go.GetComponent<Character>();
            if (c != null) c.m_faction = side;
            MonsterAI ai = go.GetComponent<MonsterAI>();
            System.Reflection.FieldInfo inDay = HarmonyLib.AccessTools.Field(typeof(MonsterAI), "m_despawnInDay");
            if (ai != null && inDay != null) inDay.SetValue(ai, false);
            CharacterDrop drops = go.GetComponent<CharacterDrop>();
            if (drops != null) drops.m_drops.Clear(); // shades leave nothing behind
            Jotunn.Managers.PrefabManager.Instance.AddPrefab(new Jotunn.Entities.CustomPrefab(go, false));
            Plugin.Debug("Ormr's shades are on the " + side + " side");
        }

        // Going into the mist, and coming out of it: the Jotun witch's own vanishing, his size.
        internal static void Mist(Vector3 at, bool coming)
        {
            BossKit.Effect("fx_JotunWitch_Dodge", at + Vector3.up);
            BossKit.Effect("sfx_jotunwitch_dodge", at);
            BossKit.Effect(coming ? "vfx_ghost_spawn" : "vfx_prespawn", at);
        }
    }

    class OrmrFight : BossFight
    {
        const string HiddenKey = "IoV_OrmrHidden";
        const float VanishEvery = 16f, VanishEveryFed = 12f, VanishReach = 15f, VanishFor = 1.2f, Behind = 2.5f;
        const float DrainShare = 0.35f, DrainMax = 60f;
        const int ShadeStage = 1, FedStage = 2;
        static readonly float[] stages = { 2f / 3f, 1f / 3f };
        const int Shades = 2, ShadeLevel = 2; // one star: two were too much on top of him

        Renderer[] looks;
        bool shown = true, vanishing;
        float nextVanish;

        protected override string StageKey { get { return "IoV_OrmrStage"; } }
        protected override float[] Thresholds { get { return stages; } }

        protected override string LineFor(int stage)
        {
            if (stage == ShadeStage) return "$iov_ormr_stage_1";
            if (stage == FedStage) return "$iov_ormr_stage_2";
            return null;
        }

        protected override void Awake()
        {
            base.Awake();
            looks = GetComponentsInChildren<Renderer>(true);
            nextVanish = Time.time + 10f;
        }

        // In the mist he isn't there: his body and his glow hidden on every client.
        protected override void Everywhere(int stage)
        {
            Shine(stage >= FedStage, 1.3f, 3.5f);
            bool hidden = nview.GetZDO().GetBool(HiddenKey, false);
            if (hidden == !shown) return;
            shown = !hidden;
            foreach (Renderer r in looks) if (r != null) r.enabled = shown;
            if (glow != null) glow.enabled = shown;
        }

        protected override void Begin(int stage)
        {
            if (stage == ShadeStage) StartCoroutine(CallShades());
            if (stage == FedStage)
            {
                BossKit.Effect("fx_summon_spirit_spawn", transform.position);
                Plugin.Debug("Ormr: the curse feeds him");
            }
        }

        protected override void Run(int stage)
        {
            if (!vanishing && nview.GetZDO().GetBool(HiddenKey, false)) nview.GetZDO().Set(HiddenKey, false); // a vanish cut short (it changed hands)
            if (vanishing || Time.time < nextVanish) return;
            Character target = Target(30f);
            if (target == null || Vector3.Distance(target.transform.position, transform.position) > VanishReach) return;
            nextVanish = Time.time + (stage >= FedStage ? VanishEveryFed : VanishEvery);
            StartCoroutine(Vanish(target));
        }

        // ---- into the mist, and out of it behind you ----

        IEnumerator Vanish(Character target)
        {
            vanishing = true;
            OrmrBossFight.Mist(transform.position, false);
            nview.GetZDO().Set(HiddenKey, true);
            yield return new WaitForSeconds(VanishFor);
            if (!Owner || target == null || target.IsDead())
            {
                if (nview != null && nview.IsValid() && nview.IsOwner()) nview.GetZDO().Set(HiddenKey, false);
                vanishing = false;
                yield break;
            }
            Vector3 back = target.transform.position - target.transform.forward * Behind;
            Vector3? spot = BossKit.Ground(back) ?? BossKit.Ground(target.transform.position + target.transform.right * Behind) ?? BossKit.Ground(target.transform.position - target.transform.right * Behind);
            if (spot != null)
            {
                Vector3 to = target.transform.position - spot.Value;
                to.y = 0f;
                transform.position = spot.Value + Vector3.up * 0.1f;
                if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null) { rb.position = transform.position; rb.linearVelocity = Vector3.zero; }
                OrmrBossFight.Mist(spot.Value, true);
                Plugin.Debug("Ormr comes out of the mist behind " + target.m_name);
            }
            nview.GetZDO().Set(HiddenKey, false);
            vanishing = false;
        }

        // ---- his crew's shades (once, at two thirds) ----

        IEnumerator CallShades()
        {
            Vector3[] spots = new Vector3[Shades];
            int count = 0;
            for (int i = 0; i < Shades; i++)
            {
                Vector3 p = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 4f;
                Vector3? ground = BossKit.Ground(p);
                spots[count++] = (ground ?? transform.position) + Vector3.up * 1.2f;
                BossKit.Effect("fx_summon_spirit_spawn", spots[count - 1]);
            }
            yield return new WaitForSeconds(1.5f);
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(OrmrBossFight.Shade) : null;
            if (prefab == null || nview == null || !nview.IsValid()) yield break;
            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(prefab, spots[i], Quaternion.identity);
                Character c = go.GetComponent<Character>();
                if (c != null) c.SetLevel(ShadeLevel);
                BaseAI ai = go.GetComponent<BaseAI>();
                if (ai != null) ai.Alert();
            }
            Plugin.Debug("Ormr: " + count + " shades of his crew");
        }

        // ---- his end: his violet light goes out, and so do his shades ----

        protected override string LastLight { get { return OrmrBossFight.LastLight; } }

        internal override void Fallen()
        {
            base.Fallen();
            Vector3 chest = glow != null ? glow.transform.position : transform.position + Vector3.up;
            BossKit.Effect("vfx_wraith_death", chest); // the curse's half of him goes first
            BossKit.Effect("sfx_wraith_death", chest);
            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(OrmrBossFight.Silence(transform.position)); // he's gone before it ends
        }

        // ---- fed by the curse: part of what he deals heals him ----

        internal override void Dealing(Character target, HitData hit)
        {
            if (Stage < FedStage || target == null || target == body) return;
            float dealt = hit.GetTotalDamage();
            if (dealt <= 0f) return;
            body.Heal(Mathf.Min(dealt * DrainShare, DrainMax), true);
        }
    }
}
