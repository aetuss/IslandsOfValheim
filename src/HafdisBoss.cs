using System.Collections;
using UnityEngine;

namespace IslandsOfValheim
{
    // Hafdís, the sea's guardian, last of the hidden islet's keepers. She offers you her place first
    // (FinalStand); struck, she fights, a Fallen Valkyrie in pale gold and white with a sea light about her.
    //   0. full: feather rain: her own feathers come down where you stand, a mist marking the spot first;
    //   1. two thirds: the sea pulls: a warning cry, she drags you to her and spins;
    //   2. one third: she calls the storm: the sky darkens, and the rain and the pull come oftener and
    //      harder.
    // Nothing of this while she still makes her offer (her side is the dvergr's until struck).
    static class HafdisBoss
    {
        internal const float Scale = 1f;
        internal static readonly Color Glow = new Color(1f, 0.9f, 0.62f);

        internal static void Setup(GameObject go)
        {
            Character body = go.GetComponent<Character>();
            if (body != null) body.m_staggerDamageFactor *= 0.5f;
            BossKit.Tint(go, BossKit.Cast(new Color(0.88f, 0.84f, 0.72f), 0.35f, 1.1f), 0.3f, "Hafdís");
            BossKit.AddGlow(go, Glow, 5f, 1.6f, "Hafdís");
            go.AddComponent<HafdisFight>();
        }
    }

    class HafdisFight : BossFight
    {
        const string PullsKey = "IoV_HafdisPulls", PullWhoKey = "IoV_HafdisPullWho";
        const float RainEvery = 14f, RainEveryStorm = 9f, RainReach = 25f, RainWarning = 1.4f, RainHeight = 14f, RainSpread = 1.6f;
        const int Feathers = 4, FeathersStorm = 7;
        const float FeatherPierce = 38f;
        const float PullEvery = 16f, PullEveryStorm = 10f, PullReach = 18f, PullWarning = 1.2f, PullForce = 70f, PullForceStorm = 95f;
        const int PullStage = 1, StormStage = 2;
        static readonly float[] stages = { 2f / 3f, 1f / 3f };

        Story.Keeper keeper;
        float nextRain, nextPull;
        int seenPulls = -1;
        bool busy;

        protected override string StageKey { get { return "IoV_HafdisStage"; } }
        protected override float[] Thresholds { get { return stages; } }

        protected override string LineFor(int stage)
        {
            if (stage == PullStage) return "$iov_hafdis_stage_1";
            if (stage == StormStage) return "$iov_hafdis_stage_2";
            return null;
        }

        protected override void Awake()
        {
            base.Awake();
            keeper = Story.KeeperOf(body);
            nextRain = Time.time + 6f;
            nextPull = Time.time + 8f;
        }

        // Fighting, not offering: struck, she's back on her own side.
        bool Fighting
        {
            get { return keeper != null && body.m_faction == keeper.Faction; }
        }

        protected override void Everywhere(int stage)
        {
            Shine(stage >= StormStage, 1.6f, 5f);
            Storm(stage >= StormStage && !body.IsDead() && LocalPlayerWithin(60f));
            Pulled();
        }

        protected override void Run(int stage)
        {
            if (!Fighting || busy) return;
            Character target = Target(RainReach);
            if (target == null) return;
            float distance = Vector3.Distance(target.transform.position, transform.position);
            if (stage >= PullStage && Time.time >= nextPull && distance < PullReach && distance > 3f && target is Player)
            {
                nextPull = Time.time + (stage >= StormStage ? PullEveryStorm : PullEvery);
                StartCoroutine(Pull((Player)target));
            }
            else if (Time.time >= nextRain && distance < RainReach)
            {
                nextRain = Time.time + (stage >= StormStage ? RainEveryStorm : RainEvery);
                StartCoroutine(Rain(target.transform.position, stage >= StormStage ? FeathersStorm : Feathers));
            }
        }

        // ---- feather rain ----

        IEnumerator Rain(Vector3 where, int count)
        {
            busy = true;
            Vector3 at = BossKit.Ground(where) ?? where;
            BossKit.Effect("vfx_prespawn", at);
            BossKit.Effect("sfx_fallenvalkyrie_attack_spit_charge", transform.position);
            yield return new WaitForSeconds(RainWarning);
            busy = false;
            GameObject feather = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("fallenvalkyrie_feather_projectile") : null;
            if (!Owner || feather == null) yield break;
            for (int i = 0; i < count; i++)
            {
                Vector2 off = Random.insideUnitCircle * RainSpread;
                Vector3 from = at + new Vector3(off.x, RainHeight, off.y);
                Vector3 velocity = new Vector3(Random.Range(-0.5f, 0.5f), -28f, Random.Range(-0.5f, 0.5f));
                GameObject go = Instantiate(feather, from, Quaternion.LookRotation(velocity));
                IProjectile projectile = go.GetComponent<IProjectile>();
                if (projectile != null)
                {
                    HitData hit = new HitData();
                    hit.m_damage.m_pierce = FeatherPierce;
                    hit.m_pushForce = 10f;
                    hit.SetAttacker(body);
                    projectile.Setup(body, velocity, -1f, hit, null, null);
                }
                yield return new WaitForSeconds(0.12f);
            }
            Plugin.Debug("Hafdís: feather rain (" + count + ")");
        }

        // ---- the sea pulls you to her, and she spins ----

        IEnumerator Pull(Player target)
        {
            busy = true;
            BossKit.Effect("sfx_fallenvalkyrie_attack_spin_charge", transform.position);
            BossKit.Effect("fx_float_hitwater", target.transform.position); // the sea's grip on you
            yield return new WaitForSeconds(PullWarning);
            if (!Owner || target == null || target.IsDead()) { busy = false; yield break; }
            ZDO zdo = nview.GetZDO();
            zdo.Set(PullWhoKey, target.GetPlayerID());
            zdo.Set(PullsKey, zdo.GetInt(PullsKey, 0) + 1); // the player's own client pulls them (Pulled)
            yield return new WaitForSeconds(0.5f);
            Spin(target);
            busy = false;
            Plugin.Debug("Hafdís pulls " + target.GetPlayerName());
        }

        // On every client: if this pull is for the local player, the sea drags them towards her.
        void Pulled()
        {
            ZDO zdo = nview.GetZDO();
            int pulls = zdo.GetInt(PullsKey, 0);
            if (seenPulls < 0) { seenPulls = pulls; return; }
            if (pulls == seenPulls) return;
            seenPulls = pulls;
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || zdo.GetLong(PullWhoKey, 0L) != player.GetPlayerID()) return;
            Vector3 to = transform.position - player.transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 2.5f) return;
            float force = (Stage >= StormStage ? PullForceStorm : PullForce) * Mathf.Clamp01((distance - 2f) / 10f + 0.3f);
            player.ApplyPushback((to / distance + Vector3.up * 0.15f).normalized, force);
        }

        // Her spin, now that you're close.
        void Spin(Character target)
        {
            Humanoid self = body as Humanoid;
            if (self == null) return;
            foreach (ItemDrop.ItemData item in self.GetInventory().GetAllItems())
            {
                string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
                if (prefab.IndexOf("spin", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                self.EquipItem(item, false);
                self.StartAttack(target, false);
                return;
            }
        }
    }
}
