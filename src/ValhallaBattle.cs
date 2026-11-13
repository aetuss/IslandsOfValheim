using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // The einherjar's daily battle (Gylfaginning: every day they arm themselves, go out and fight and fell one
    // another; at the evening meal all rise whole and drink together). On the meadow west of the yard
    // (IsletBuilder.Battlefield), from Valhalla's dawn to its sunset, four pairs fight for real: the ring's own
    // fighters (the player's body with a creature's mind, EinherjarFighter), each with his one foe, moving about
    // the field and trading real blows. One whose health runs out dies as anyone does (the body falls), and a
    // while later rises where he began, in a soft gold glow, and goes back at his foe. At sunset they fade away
    // (to the feast in the hall); at dawn they come back. Their old master stands at the field's north edge.
    //
    // (2.0.11-2.0.13 were the town's people acting it out by the clock, one swinging, one guarding, a loser
    // kneeling: the user asked for a real fight, as the ring's, moving about, dying and coming back.)
    //
    // They are networked creatures, made (and unmade at sunset) by one game: of the players near the field, the
    // one who joined first (the lowest user id), so two never make the same man; any other sees that one's.
    // They touch nobody but their own foe, and nothing but their foe touches them (BattleRules).
    partial class ValhallaPeople
    {
        class Slot
        {
            internal int index;               // pair index * 2 + side
            internal SpiritHuman.Look look;
            internal string right, left;
            internal Vector3 home;            // where he starts and rises, in the rock's space
            internal float missingSince = -1f;
        }

        readonly List<Slot> battle = new List<Slot>();
        bool battleOn;
        float nextBattleTick;

        static readonly string[][] BattleArms =
        {
            new[] { "SwordIron", "ShieldBanded", "AxeBronze", "ShieldWood" },
            new[] { "MaceIron", "ShieldSilver", "SwordBronze", "ShieldBanded" },
            new[] { "AxeIron", "ShieldWood", "SwordSilver", "ShieldSilver" },
            new[] { "MaceBronze", "ShieldBanded", "SwordIron", "ShieldWood" },
        };

        const float RiseAfter = 10f;     // dead this long, he rises again
        const float FieldReach = 11f;    // past this from the field's middle, he gives up his chase and comes back
        const float BattleHealth = 250f;

        // Fighting from Valhalla's dawn to its sunset (the sun's place: 0.25 sunrise, 0.745 the sunset begins).
        internal static bool BattleTime()
        {
            float t;
            string weather, part;
            ValhallaWeather.Now(out t, out weather, out part);
            return t >= 0.25f && t < 0.745f;
        }

        // The pairs' places and looks, and their master; n: the next einherjar's face. Gives back the next.
        int Battlefield(int n)
        {
            Vector2 c = IsletBuilder.Battlefield;
            Vector2[] spots = { new Vector2(-5f, -3f), new Vector2(5f, -3.5f), new Vector2(-4.5f, 3.5f), new Vector2(5.5f, 3f) };
            for (int i = 0; i < spots.Length; i++)
            {
                Vector2 p = c + spots[i];
                string[] arms = BattleArms[i % BattleArms.Length];
                for (int side = 0; side < 2; side++)
                {
                    Vector3 home = new Vector3(p.x + (side == 0 ? -1.5f : 1.5f), 0f, p.y);
                    home.y = Ground(home.x, home.z, 4f);
                    battle.Add(new Slot { index = i * 2 + side, look = Warrior(n++, true), right = arms[side * 2], left = arms[side * 2 + 1], home = home });
                }
            }
            battleOn = BattleTime();
            nextBattleTick = 0f;
            // (his spear on his back, as a player's put away: held, the game's spear stance carried it level at
            // his hip, odd on one standing and watching)
            SpiritHuman master = Stand(BattleMaster(), new Vector3(c.x, 0f, c.y + 8.5f), Vector3.back, 4f);
            VisEquipment gear = master != null ? master.GetComponent<VisEquipment>() : null;
            if (gear != null) gear.SetRightBackItem("SpearElderbark".GetStableHashCode(), 1);
            Speaks(master, ValhallaSpeaker.BattleMaster, 2.3f);
            Plugin.Debug("Valhalla: the daily battle, 4 pairs at " + c + (battleOn ? ", fighting" : ", gone to the feast"));
            return n;
        }

        // The elder of the einherjar, at the field's edge: grey, in bronze, a cape of deer hide.
        static SpiritHuman.Look BattleMaster()
        {
            return new SpiritHuman.Look
            {
                Hair = "Hair2", Beard = "Beard6", HairColor = new Vector3(0.7f, 0.69f, 0.66f), SkinColor = Vector3.one * 0.9f,
                Chest = "ArmorBronzeChest", Legs = "ArmorBronzeLegs", Shoulder = "CapeDeerHide",
            };
        }

        Vector3 FieldWorld()
        {
            Vector2 c = IsletBuilder.Battlefield;
            return transform.TransformPoint(new Vector3(c.x, IsletBuilder.SkyGround(c.x, c.y), c.y));
        }

        // This game makes and unmakes them: of the players near the field, the one who joined first.
        bool BattleRunner()
        {
            Player me = Player.m_localPlayer;
            if (me == null || ZNetScene.instance == null) return false;
            Vector3 field = FieldWorld();
            if (Vector3.Distance(me.transform.position, field) > 120f) return false;
            long mine = me.GetZDOID().UserID;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null || p == me) continue;
                if (Vector3.Distance(p.transform.position, field) > 120f) continue;
                if (p.GetZDOID().UserID < mine) return false;
            }
            return true;
        }

        void Battles(double now)
        {
            if (battle.Count == 0) return;
            bool day = BattleTime();
            if (day != battleOn)
            {
                battleOn = day;
                bool seen = Near(IsletBuilder.Battlefield, 80f);
                if (!day) foreach (EinherjarFighter f in new List<EinherjarFighter>(EinherjarFighter.Battle.Values)) StartCoroutine(Leave(f, seen));
                foreach (Slot s in battle) s.missingSince = -1000f; // (at dawn, all at once)
                Plugin.Debug("Valhalla: the daily battle " + (day ? "begins" : "ends: to the feast"));
            }
            if (Time.time < nextBattleTick) return;
            nextBattleTick = Time.time + 0.5f;
            if (!BattleRunner()) return;
            // who's on the field, by his place (and any second one for a place, from two games at once: gone)
            EinherjarFighter[] by = new EinherjarFighter[battle.Count];
            foreach (EinherjarFighter f in new List<EinherjarFighter>(EinherjarFighter.Battle.Values))
            {
                if (f == null || f.leaving) continue;
                int i = f.Slot;
                Character body = f.GetComponent<Character>();
                if (i < 0 || i >= by.Length || body == null || body.IsDead()) continue;
                if (!battleOn || by[i] != null) { Unmake(f); continue; }
                by[i] = f;
                Leash(f, body);
            }
            if (!battleOn) return;
            foreach (Slot s in battle)
            {
                if (by[s.index] != null) { s.missingSince = -1f; continue; }
                if (s.missingSince == -1f) { s.missingSince = Time.time; continue; }
                if (Time.time - s.missingSince < RiseAfter) continue;
                s.missingSince = -1f;
                Rise(s);
            }
        }

        // Too far off chasing his foe: he lets him go and comes back to his place (his foe comes too, and on it goes).
        void Leash(EinherjarFighter f, Character body)
        {
            ZNetView nview = f.GetComponent<ZNetView>();
            if (nview == null || !nview.IsOwner()) return;
            Vector3 at = transform.InverseTransformPoint(body.transform.position);
            Vector2 c = IsletBuilder.Battlefield;
            if (new Vector2(at.x - c.x, at.z - c.y).magnitude < FieldReach) return;
            MonsterAI ai = body.GetComponent<MonsterAI>();
            if (ai == null) return;
            Traverse t = Traverse.Create(ai);
            t.Field("m_targetCreature").SetValue(null);
            t.Method("SetAlerted", new[] { typeof(bool) }).GetValue(false);
        }

        // One back on the field: made where he began, dressed as he was; the glow comes on every game that sees
        // him come (EinherjarFighter.Rising).
        void Rise(Slot s)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(EinherjarFighter.BattlePrefab) : null;
            Humanoid template = prefab != null ? prefab.GetComponent<Humanoid>() : null;
            if (template == null || ObjectDB.instance == null) { Plugin.Log.LogWarning("Valhalla: no " + EinherjarFighter.BattlePrefab); return; }
            List<GameObject> items = new List<GameObject>();
            foreach (string name in new[] { s.right, s.left, s.look.Helmet, s.look.Chest, s.look.Legs, s.look.Shoulder })
            {
                GameObject item = string.IsNullOrEmpty(name) ? null : ObjectDB.instance.GetItemPrefab(name);
                if (item != null) items.Add(item);
            }
            template.m_defaultItems = items.ToArray();
            Traverse.Create(template).Field("m_hairItem").SetValue(s.look.Hair ?? "");
            Traverse.Create(template).Field("m_beardItem").SetValue(s.look.Beard ?? "");
            Vector3 at = transform.TransformPoint(s.home);
            Vector3 face = transform.rotation * (s.index % 2 == 0 ? Vector3.right : Vector3.left);
            GameObject go = Instantiate(prefab, at + Vector3.up * 0.3f, Quaternion.LookRotation(face));
            template.m_defaultItems = new GameObject[0];
            Character body = go.GetComponent<Character>();
            body.SetMaxHealth(BattleHealth);
            body.SetHealth(BattleHealth);
            EinherjarFighter me = go.GetComponent<EinherjarFighter>();
            if (me != null)
            {
                me.modelWanted = s.look.Model;
                me.hairWanted = s.look.Hair ?? "";
                me.beardWanted = s.look.Beard ?? "";
                me.Place(s.index);
                me.QuickerSoon(1.2f, 1.6f);
            }
            VisEquipment vis = go.GetComponent<VisEquipment>();
            if (vis != null)
            {
                vis.SetModel(s.look.Model);
                vis.SetHairColor(s.look.HairColor);
                vis.SetSkinColor(s.look.SkinColor);
            }
            MonsterAI ai = go.GetComponent<MonsterAI>();
            if (ai != null) Traverse.Create(ai).Method("SetPatrolPoint", new[] { typeof(Vector3) }).GetValue(at);
            Plugin.Debug("Valhalla: battle place " + s.index + " rises (" + s.right + ", " + s.left + ")");
        }

        void Unmake(EinherjarFighter f)
        {
            if (f == null) return;
            ZNetView nview = f.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || ZNetScene.instance == null) return;
            if (!nview.IsOwner()) nview.ClaimOwnership();
            ZNetScene.instance.Destroy(f.gameObject);
        }

        bool Near(Vector2 place, float within)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return false;
            Vector3 you = transform.InverseTransformPoint(player.transform.position);
            return new Vector2(you.x - place.x, you.z - place.y).magnitude < within;
        }

        // Sunset: each fades away where he stands (a pale copy of him in his place, on every game that sees it,
        // and that fading out; the man himself unmade by the game that made him); far off, just gone.
        IEnumerator Leave(EinherjarFighter f, bool seen)
        {
            if (f == null) yield break;
            f.leaving = true;
            int i = f.Slot;
            Slot s = i >= 0 && i < battle.Count ? battle.Find(x => x.index == i) : null;
            SpiritHuman ghost = null;
            if (seen && s != null)
            {
                ghost = SpiritHuman.MakeLocal(s.look, SpiritHuman.Stand, "", SpiritHuman.GhostSoft, transform, f.transform.position, f.transform.rotation, s.right, s.left);
                if (ghost != null) made.Add(ghost.gameObject);
            }
            // (a ghost is only seen once it's dressed, a second on: the man stays until then)
            if (ghost != null) yield return new WaitForSeconds(1.2f);
            if (f != null && BattleRunner()) Unmake(f);
            if (ghost == null) yield break;
            List<Material> mats = new List<Material>();
            List<Color> from = new List<Color>();
            foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.HasProperty("_Color") && m.renderQueue >= 3000) { mats.Add(m); from.Add(m.color); }
            }
            const float Takes = 3f;
            for (float t = 0f; t < Takes; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Takes);
                for (int m = 0; m < mats.Count; m++)
                    if (mats[m] != null) { Color c = from[m]; c.a *= 1f - k; mats[m].color = c; }
                if (ghost != null) foreach (Light l in ghost.GetComponentsInChildren<Light>(true)) l.intensity *= 0.97f;
                yield return null;
            }
            if (ghost != null) { made.Remove(ghost.gameObject); everyone.Remove(ghost); Destroy(ghost.gameObject); }
        }

        // A soft gold glow where one rises: a light that swells and fades (no beam: those are the Allfather's),
        // at its brightest GlowRise seconds after it starts.
        internal const float GlowRise = 1.3f;

        internal void RiseGlow(Vector3 at) { StartCoroutine(Glow(at)); }

        IEnumerator Glow(Vector3 at)
        {
            GameObject go = new GameObject("IoV_RiseGlow");
            go.transform.SetParent(transform, false); // (gone with Valhalla's people, if they go first)
            go.transform.position = at + Vector3.up * 0.9f;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.45f);
            light.range = 4.5f;
            light.shadows = LightShadows.None;
            const float Takes = GlowRise * 2f;
            for (float t = 0f; t < Takes; t += Time.deltaTime)
            {
                if (light == null) yield break;
                light.intensity = Mathf.Sin(Mathf.Clamp01(t / Takes) * Mathf.PI) * 2.6f;
                yield return null;
            }
            Destroy(go);
        }
    }

    // The battle's rules: each fights his own foe only (the other of his pair) and harms nobody else; nothing
    // but his foe harms him (not you, not a fall); a blow takes about the same off him whatever the weapon and
    // armour (so a fight lasts about as long each time); no bar over their heads; and they really die (the
    // ring's own never do: NoDeathInTheRing) and fall as the game's bodies fall, looking as they did.
    static class BattleRules
    {
        const float Blow = 22f; // health a landed blow takes, about

        [HarmonyPatch(typeof(BaseAI), "IsEnemy", new[] { typeof(Character), typeof(Character) })]
        static class Foes
        {
            static bool Prefix(Character __0, Character __1, ref bool __result)
            {
                if (EinherjarFighter.Battle.Count == 0 || __0 == null || __1 == null) return true;
                EinherjarFighter a, b;
                bool inA = EinherjarFighter.Battle.TryGetValue(__0, out a), inB = EinherjarFighter.Battle.TryGetValue(__1, out b);
                if (!inA && !inB) return true;
                __result = inA && inB && a != null && b != null && !a.leaving && !b.leaving &&
                    a.Slot >= 0 && b.Slot >= 0 && a.Slot / 2 == b.Slot / 2 && a.Slot != b.Slot;
                return false;
            }
        }

        [HarmonyPatch(typeof(Character), "Damage")]
        static class Blows
        {
            static bool Prefix(Character __instance, HitData __0)
            {
                if (EinherjarFighter.Battle.Count == 0 || __0 == null || !EinherjarFighter.Battle.ContainsKey(__instance)) return true;
                Character by = __0.GetAttacker();
                if (by == null || !EinherjarFighter.Battle.ContainsKey(by)) return false;
                float total = __0.GetTotalDamage();
                if (total <= 0f) return true;
                // what it must be before his armour so about Blow is left after it (the game: under twice the
                // armour, damage² / (4 armour); over it, damage - armour)
                float want = Blow * Random.Range(0.75f, 1.25f);
                float armour = Mathf.Max(0f, __instance.GetBodyArmor());
                float raw = armour <= want ? want + armour : Mathf.Sqrt(4f * want * armour);
                __0.m_damage.Modify(raw / total);
                return true;
            }
        }

        // The body that falls: as he looked (the game dresses it, but leaves its build and colours the default).
        [HarmonyPatch(typeof(Humanoid), "OnRagdollCreated")]
        static class Fallen
        {
            static void Postfix(Humanoid __instance, Ragdoll __0)
            {
                if (__0 == null || !EinherjarFighter.Battle.ContainsKey(__instance)) return;
                VisEquipment mine = __instance.GetComponent<VisEquipment>(), its = __0.GetComponent<VisEquipment>();
                if (mine == null || its == null) return;
                Traverse t = Traverse.Create(mine);
                its.SetModel(mine.GetModelIndex());
                its.SetHairColor(t.Field("m_hairColor").GetValue<Vector3>());
                its.SetSkinColor(t.Field("m_skinColor").GetValue<Vector3>());
                Plugin.Debug("Valhalla: battle place " + EinherjarFighter.Battle[__instance].Slot + " falls");
            }
        }
    }
}
