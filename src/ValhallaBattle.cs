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
    // the field and trading real blows. One whose health runs out dies as anyone does (the body falls); a while
    // later a soft gold glow comes up over his body, and at its brightest the body is gone and he stands there
    // whole and goes back at his foe.
    //
    // At sunset the fighting stops: the fallen rise, and their old master leads them all off the field in a file
    // up the track to the hall, and in, to the feast. At dawn he leads them back out to the field, each to his
    // place, and it starts again. (2.0.14 had them, and only them, fade where they stood: the user didn't like
    // it, and the master stood waiting on an empty field all night.)
    //
    // The fighters are networked creatures, made by one game: of the players near the field, the one who joined
    // first (the lowest user id), so two never make the same man; any other sees that one's. The file to and
    // from the hall is each game's own (the town's people, walking: the same on every game, near enough).
    // They touch nobody but their own foe, and nothing but their foe touches them (BattleRules).
    partial class ValhallaPeople
    {
        class Slot
        {
            internal int index;               // pair index * 2 + side
            internal SpiritHuman.Look look;
            internal string right, left;
            internal Vector3 home;            // where he starts, in the rock's space
            internal float missingSince = -1f;
            internal float dueAt = -1f;       // coming out of the hall at dawn: on the field (quietly) then
        }

        readonly List<Slot> battle = new List<Slot>();
        bool battleOn;
        float nextBattleTick, nextBattleDetail;
        SpiritHuman elder;

        static readonly string[][] BattleArms =
        {
            new[] { "SwordIron", "ShieldBanded", "AxeBronze", "ShieldWood" },
            new[] { "MaceIron", "ShieldSilver", "SwordBronze", "ShieldBanded" },
            new[] { "AxeIron", "ShieldWood", "SwordSilver", "ShieldSilver" },
            new[] { "MaceBronze", "ShieldBanded", "SwordIron", "ShieldWood" },
        };

        internal const float RiseAfter = 10f; // dead this long, he rises again
        const float FieldReach = 12f;         // a pair past this from the field's middle stop and come back
        const float BattleHealth = 250f;
        const float FileSpeed = 1.6f, FileGap = 1.2f; // walking to and from the hall: how fast, how far apart

        // The track to the hall: from the master's place at the field's north edge, north-west across the meadow
        // (kept clear of trees: IsletBuilder.ValhallaPads), to the foot of the hall's front steps, and in.
        static Vector2 ElderAt { get { return IsletBuilder.Battlefield + new Vector2(0f, 8.5f); } }
        static readonly Vector2[] Track = { new Vector2(9f, -4f), new Vector2(0f, 0.5f), new Vector2(0f, 10f) };

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
            elder = battleOn ? Elder(ElderAt, Vector3.back) : null; // (at night he's at the feast)
            Plugin.Debug("Valhalla: the daily battle, 4 pairs at " + c + (battleOn ? ", fighting" : ", gone to the feast"));
            return n;
        }

        // The elder of the einherjar: grey, in bronze, a cape of deer hide; his spear on his back, as a player's
        // put away (held, the game's spear stance carried it level at his hip, odd on one standing and watching).
        SpiritHuman Elder(Vector2 at, Vector3 facing)
        {
            SpiritHuman man = Stand(new SpiritHuman.Look
            {
                Hair = "Hair2", Beard = "Beard6", HairColor = new Vector3(0.7f, 0.69f, 0.66f), SkinColor = Vector3.one * 0.9f,
                Chest = "ArmorBronzeChest", Legs = "ArmorBronzeLegs", Shoulder = "CapeDeerHide",
            }, new Vector3(at.x, 0f, at.y), facing, 4f);
            VisEquipment gear = man != null ? man.GetComponent<VisEquipment>() : null;
            if (gear != null) gear.SetRightBackItem("SpearElderbark".GetStableHashCode(), 1);
            Speaks(man, ValhallaSpeaker.BattleMaster, 2.3f);
            return man;
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

        EinherjarFighter FighterOf(int slot)
        {
            foreach (EinherjarFighter f in EinherjarFighter.Battle.Values)
                if (f != null && !f.gone && f.Slot == slot) return f;
            return null;
        }

        // A fallen one's body, still lying there (any game's: it carries his place).
        static Ragdoll CorpseOf(int slot)
        {
            foreach (Ragdoll r in Object.FindObjectsByType<Ragdoll>(FindObjectsSortMode.None))
            {
                ZNetView nview = r != null ? r.GetComponent<ZNetView>() : null;
                if (nview != null && nview.IsValid() && nview.GetZDO().GetInt(EinherjarFighter.SlotKey, 0) - 1 == slot) return r;
            }
            return null;
        }

        void Battles(double now)
        {
            if (battle.Count == 0) return;
            BattleDetail();
            bool day = BattleTime();
            if (day != battleOn)
            {
                battleOn = day;
                bool seen = Near(IsletBuilder.Battlefield, 80f);
                if (day) Dawn(seen);
                else Sunset(seen);
                Plugin.Debug("Valhalla: the daily battle " + (day ? "begins" : "ends: to the feast") + (seen ? ", seen" : ""));
            }
            if (Time.time < nextBattleTick) return;
            nextBattleTick = Time.time + 0.5f;
            if (!BattleRunner()) return;
            // who's on the field, by his place (and any second one for a place, from two games at once: gone)
            EinherjarFighter[] by = new EinherjarFighter[battle.Count];
            foreach (EinherjarFighter f in new List<EinherjarFighter>(EinherjarFighter.Battle.Values))
            {
                if (f == null) continue;
                int i = f.Slot;
                Character body = f.GetComponent<Character>();
                if (i < 0 || i >= by.Length || body == null || body.IsDead()) continue;
                if (!battleOn || f.gone || by[i] != null) { Unmake(f.gameObject); continue; }
                by[i] = f;
            }
            if (!battleOn) return;
            for (int p = 0; p + 1 < by.Length; p += 2) Leash(by[p], by[p + 1]);
            foreach (Slot s in battle)
            {
                if (by[s.index] != null) { s.missingSince = -1f; s.dueAt = -1f; continue; }
                if (s.dueAt > 0f)
                {
                    // (coming out of the hall: on the field as he gets to his place)
                    if (Time.time < s.dueAt) continue;
                    s.dueAt = -1f;
                    Rise(s, transform.TransformPoint(s.home), true);
                    continue;
                }
                if (s.missingSince == -1f) { s.missingSince = Time.time; continue; }
                if (Time.time - s.missingSince < RiseAfter) continue;
                s.missingSince = -1f;
                // up where he fell (his body gone as he's there), or where he began
                Ragdoll body = CorpseOf(s.index);
                Vector3 at = body != null ? OnGround(body.GetAverageBodyPosition()) : transform.TransformPoint(s.home);
                Rise(s, at, false);
                if (body != null) StartCoroutine(Later(EinherjarFighter.GlowRise, () => Unmake(body != null ? body.gameObject : null)));
            }
        }

        // A pair too far off, chasing each other: both let go and come back towards their places (no foes to each
        // other a few seconds, so they don't turn straight round again: 2.0.14 let one go and he was at once
        // after his foe again, round and round at the field's edge; the user saw them flounder), then on it goes.
        void Leash(EinherjarFighter a, EinherjarFighter b)
        {
            if (a == null || b == null) return;
            if (Inside(a) && Inside(b)) return;
            foreach (EinherjarFighter f in new[] { a, b })
            {
                f.returningUntil = Time.time + 6f;
                ZNetView nview = f.GetComponent<ZNetView>();
                MonsterAI ai = f.GetComponent<MonsterAI>();
                if (nview == null || !nview.IsOwner() || ai == null) continue;
                Traverse t = Traverse.Create(ai);
                t.Field("m_targetCreature").SetValue(null);
                t.Method("SetAlerted", new[] { typeof(bool) }).GetValue(false);
            }
            Plugin.Debug("Valhalla: battle pair " + a.Slot / 2 + " too far off the field, coming back");
        }

        bool Inside(EinherjarFighter f)
        {
            Vector3 at = transform.InverseTransformPoint(f.transform.position);
            Vector2 c = IsletBuilder.Battlefield;
            return new Vector2(at.x - c.x, at.z - c.y).magnitude < FieldReach;
        }

        Vector3 OnGround(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            local.y = Ground(local.x, local.z, local.y + 2f);
            return transform.TransformPoint(local);
        }

        static IEnumerator Later(float after, System.Action then)
        {
            yield return new WaitForSeconds(after);
            then();
        }

        // One on the field: made where asked, dressed as he is (BattleRules.Dressed: kept with him, so he's still
        // dressed when he comes back into view; 2.0.14's came back bare and bald); in the gold glow if he rises,
        // without it if he walked there.
        void Rise(Slot s, Vector3 at, bool quiet)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(EinherjarFighter.BattlePrefab) : null;
            if (prefab == null) { Plugin.Log.LogWarning("Valhalla: no " + EinherjarFighter.BattlePrefab); return; }
            Vector3 face = transform.rotation * (s.index % 2 == 0 ? Vector3.right : Vector3.left);
            GameObject go = Instantiate(prefab, at + Vector3.up * 0.3f, Quaternion.LookRotation(face));
            Character body = go.GetComponent<Character>();
            body.SetMaxHealth(BattleHealth);
            body.SetHealth(BattleHealth);
            EinherjarFighter me = go.GetComponent<EinherjarFighter>();
            if (me != null)
            {
                string gear = string.Join(",", new[] { s.right, s.left, s.look.Helmet, s.look.Chest, s.look.Legs, s.look.Shoulder });
                me.Place(s.index, gear, s.look.Hair ?? "", s.look.Beard ?? "", s.look.Model, quiet);
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
            if (ai != null) Traverse.Create(ai).Method("SetPatrolPoint", new[] { typeof(Vector3) }).GetValue(transform.TransformPoint(s.home));
            Plugin.Debug("Valhalla: battle place " + s.index + (quiet ? " on the field" : " rises") + " (" + s.right + ", " + s.left + ")");
        }

        void Unmake(GameObject go)
        {
            ZNetView nview = go != null ? go.GetComponent<ZNetView>() : null;
            if (nview == null || !nview.IsValid() || ZNetScene.instance == null) return;
            if (!nview.IsOwner()) nview.ClaimOwnership();
            ZNetScene.instance.Destroy(go);
        }

        bool Near(Vector2 place, float within)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return false;
            Vector3 you = transform.InverseTransformPoint(player.transform.position);
            return new Vector2(you.x - place.x, you.z - place.y).magnitude < within;
        }

        // ---- to the feast and back ----

        // Sunset: in each one's place one of the town's people just like him (the fighter himself unseen at once,
        // and unmade by the game that made him); the fallen up in the gold glow where they lie; and all in a file
        // behind the elder up to the hall and in. Far off: just gone.
        void Sunset(bool seen)
        {
            if (!seen)
            {
                foreach (EinherjarFighter f in new List<EinherjarFighter>(EinherjarFighter.Battle.Values)) if (f != null) f.Vanish();
                Gone(elder);
                elder = null;
                if (BattleRunner()) foreach (Slot s in battle) { Ragdoll r = CorpseOf(s.index); if (r != null) Unmake(r.gameObject); }
                return;
            }
            // each one's way: from where he is to the elder's place, then the track; the elder first, and each
            // after him in the order he'd get to the elder's place, a little apart (his start held back to keep it)
            List<Walking> file = new List<Walking>();
            if (elder != null) file.Add(new Walking { man = elder, path = Path(ElderAt, true), inside = true });
            elder = null;
            foreach (Slot s in battle)
            {
                Walking w = new Walking { look = s.look, right = s.right, left = s.left, inside = true };
                EinherjarFighter f = FighterOf(s.index);
                Ragdoll body = f == null ? CorpseOf(s.index) : null;
                Vector3 world = f != null ? f.transform.position : body != null ? OnGround(body.GetAverageBodyPosition()) : transform.TransformPoint(s.home);
                Vector3 local = transform.InverseTransformPoint(world);
                Vector2 from = new Vector2(local.x, local.z);
                w.path = new List<Vector2> { from };
                w.path.AddRange(Path(ElderAt, true));
                Vector2 dir = ElderAt - from;
                w.startTurn = f != null ? f.transform.rotation : transform.rotation * Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? new Vector3(dir.x, 0f, dir.y) : Vector3.forward);
                if (f != null) f.Vanish(); // (unseen at once on every game; unmade by the one that made him)
                else
                {
                    // the fallen: up first (the glow; at its brightest his body's gone and he's there)
                    w.appearAfter = EinherjarFighter.GlowRise;
                    RiseGlow(world);
                    if (body != null && BattleRunner()) StartCoroutine(Later(EinherjarFighter.GlowRise, () => Unmake(body != null ? body.gameObject : null)));
                }
                w.reach = w.appearAfter + Vector2.Distance(from, ElderAt) / FileSpeed;
                file.Add(w);
            }
            foreach (EinherjarFighter f in new List<EinherjarFighter>(EinherjarFighter.Battle.Values)) if (f != null) f.Vanish();
            file.Sort((x, y) => x.reach.CompareTo(y.reach)); // (the elder: 0, first)
            float last = -FileGap;
            foreach (Walking w in file)
            {
                float arrives = Mathf.Max(w.reach, last + FileGap);
                last = arrives;
                w.wait = w.appearAfter + (arrives - w.reach);
                StartCoroutine(Walk(w));
            }
        }

        // Dawn: out of the hall in a file behind the elder, down the track and each to his place (the elder to his,
        // at the field's edge), where the fighter takes over from the walker. Far off: they're just there.
        void Dawn(bool seen)
        {
            if (!seen)
            {
                if (elder == null) elder = Elder(ElderAt, Vector3.back);
                foreach (Slot s in battle) { s.missingSince = -1000f; s.dueAt = -1f; } // (all at once, in the glow)
                return;
            }
            List<Vector2> out_ = Path(ElderAt, false);
            Vector2 first = out_[1] - out_[0];
            Quaternion leaving = transform.rotation * Quaternion.LookRotation(new Vector3(first.x, 0f, first.y));
            StartCoroutine(Walk(new Walking { elder = true, path = out_, appearAfter = 0.5f, wait = 0.5f, startTurn = leaving, endTurn = transform.rotation * Quaternion.LookRotation(Vector3.back) }));
            float length = Length(out_);
            int k = 1;
            foreach (Slot s in battle)
            {
                List<Vector2> path = new List<Vector2>(out_);
                path.Add(new Vector2(s.home.x, s.home.z));
                float wait = 0.5f + k++ * FileGap;
                Walking w = new Walking
                {
                    look = s.look, right = s.right, left = s.left, path = path, appearAfter = wait, wait = wait, slot = s.index, startTurn = leaving,
                    endTurn = transform.rotation * Quaternion.LookRotation(s.index % 2 == 0 ? Vector3.right : Vector3.left),
                };
                s.missingSince = -1f;
                s.dueAt = Time.time + wait + Length(path) / FileSpeed;
                StartCoroutine(Walk(w));
            }
            Plugin.Debug("Valhalla: out of the hall to the field, " + (length / FileSpeed).ToString("F0") + " s to the elder's place");
        }

        // The way between the elder's place and the hall's middle (in: there to the hall; out: the hall to there).
        static List<Vector2> Path(Vector2 elderAt, bool inward)
        {
            List<Vector2> path = new List<Vector2> { elderAt };
            path.AddRange(Track);
            if (!inward) path.Reverse();
            return path;
        }

        static float Length(List<Vector2> path)
        {
            float l = 0f;
            for (int i = 1; i < path.Count; i++) l += Vector2.Distance(path[i - 1], path[i]);
            return l;
        }

        class Walking
        {
            internal SpiritHuman man;            // one already standing (the elder going in), or made here
            internal SpiritHuman.Look look;
            internal string right = "", left = "";
            internal bool elder, inside;         // the elder (coming out: made as him); going into the hall (then gone)
            internal int slot = -1;              // coming out: whose place he takes
            internal Quaternion startTurn = Quaternion.identity, endTurn = Quaternion.identity;
            internal List<Vector2> path;
            internal float appearAfter;          // made (where he starts) this long from now
            internal float wait;                 // and he sets off this long from now
            internal float reach;                // (the file's order: when he'd get to the elder's place)
        }

        IEnumerator Walk(Walking w)
        {
            List<Vector2> path = w.path;
            if (w.man == null)
            {
                if (w.appearAfter > 0f) yield return new WaitForSeconds(w.appearAfter);
                Vector3 facing = w.startTurn * Vector3.forward;
                w.man = w.elder ? Elder(path[0], Quaternion.Inverse(transform.rotation) * facing) : Made(w, path[0], w.startTurn);
            }
            SpiritHuman man = w.man;
            if (man == null) yield break;
            for (float t = w.appearAfter; t < w.wait; t += Time.deltaTime) { if (man == null) yield break; man.walking = 0f; yield return null; }
            man.keepMoving = true;
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 a = path[i - 1], b = path[i];
                float length = Vector2.Distance(a, b);
                if (length < 0.05f) continue;
                Vector2 dir = (b - a) / length;
                Quaternion heading = transform.rotation * Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
                for (float d = 0f; d < length; d += FileSpeed * Time.deltaTime)
                {
                    if (man == null) yield break;
                    man.walking = FileSpeed;
                    man.transform.SetPositionAndRotation(transform.TransformPoint(Lift(Vector2.Lerp(a, b, d / length))),
                        Quaternion.RotateTowards(man.transform.rotation, heading, 360f * Time.deltaTime));
                    yield return null;
                }
            }
            if (man == null) yield break;
            man.walking = 0f;
            man.keepMoving = false;
            if (w.inside) { Gone(man); yield break; }
            man.transform.rotation = w.endTurn;
            if (w.elder)
            {
                if (elder != null && elder != man) Gone(elder);
                elder = man;
                yield break;
            }
            // at his place: facing his foe till the fighter's there (made by whichever game makes them), then gone
            for (float t = 0f; t < 20f && FighterOf(w.slot) == null; t += 0.25f) yield return new WaitForSeconds(0.25f);
            Gone(man);
        }

        SpiritHuman Made(Walking w, Vector2 at, Quaternion turn)
        {
            SpiritHuman man = SpiritHuman.MakeLocal(w.look, SpiritHuman.Stand, "", Solid, transform, transform.TransformPoint(Lift(at)), turn, w.right, w.left);
            return Add(man);
        }

        void Gone(SpiritHuman person)
        {
            if (person == null) return;
            made.Remove(person.gameObject);
            everyone.Remove(person);
            foreach (Actor a in actors) if (a.who == person) a.who = null;
            Destroy(person.gameObject);
        }

        // ---- what they cost ----

        // The fighters' capes (cloth, worked out every frame) still when far from the camera, as the town's
        // people's are (SpiritHuman.Detail).
        void BattleDetail()
        {
            if (Time.time < nextBattleDetail || EinherjarFighter.Battle.Count == 0) return;
            nextBattleDetail = Time.time + 1f;
            Camera cam = Utils.GetMainCamera();
            if (cam == null) return;
            foreach (EinherjarFighter f in EinherjarFighter.Battle.Values)
            {
                if (f == null) continue;
                bool near = Vector3.Distance(cam.transform.position, f.transform.position) < 30f;
                foreach (Cloth c in f.GetComponentsInChildren<Cloth>(true)) if (c != null && c.enabled != near) c.enabled = near;
            }
        }

        // A soft gold glow where one rises: a light that swells and fades (no beam: those are the Allfather's),
        // at its brightest GlowRise seconds after it starts.
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
            float takes = EinherjarFighter.GlowRise * 2f;
            for (float t = 0f; t < takes; t += Time.deltaTime)
            {
                if (light == null) yield break;
                light.intensity = Mathf.Sin(Mathf.Clamp01(t / takes) * Mathf.PI) * 2.6f;
                yield return null;
            }
            Destroy(go);
        }
    }

    // The battle's rules: each fights his own foe only (the other of his pair) and harms nobody else; nothing
    // but his foe harms him (not you, not a fall); a blow takes about the same off him whatever the weapon and
    // armour (so a fight lasts about as long each time); no bar over their heads; dressed from what's kept with
    // him; and they really die (the ring's own never do: NoDeathInTheRing) and fall as the game's bodies fall,
    // looking as they did, the body lying there till he rises.
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
                __result = inA && inB && a != null && b != null && !a.gone && !b.gone &&
                    Time.time >= a.returningUntil && Time.time >= b.returningUntil &&
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

        // His gear, hair and beard, from what's kept with him, just as the game gives a creature its gear (each
        // time he's made on a game: when he first comes, and whenever he comes back into view).
        [HarmonyPatch(typeof(Humanoid), "GiveDefaultItems")]
        static class Dressed
        {
            static void Prefix(Humanoid __instance)
            {
                EinherjarFighter f;
                if (EinherjarFighter.Battle.Count > 0 && EinherjarFighter.Battle.TryGetValue(__instance, out f) && f != null) f.Dress(__instance);
            }
        }

        // The body that falls: as he looked (the game dresses it, but leaves its build and colours the default),
        // marked with his place, and lying there till he rises (or, if the game that rises him is gone, a while).
        [HarmonyPatch(typeof(Humanoid), "OnRagdollCreated")]
        static class Fallen
        {
            static void Postfix(Humanoid __instance, Ragdoll __0)
            {
                EinherjarFighter f;
                if (__0 == null || !EinherjarFighter.Battle.TryGetValue(__instance, out f) || f == null) return;
                VisEquipment mine = __instance.GetComponent<VisEquipment>(), its = __0.GetComponent<VisEquipment>();
                if (mine != null && its != null)
                {
                    Traverse t = Traverse.Create(mine);
                    its.SetModel(mine.GetModelIndex());
                    its.SetHairColor(t.Field("m_hairColor").GetValue<Vector3>());
                    its.SetSkinColor(t.Field("m_skinColor").GetValue<Vector3>());
                }
                ZNetView nview = __0.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid()) nview.GetZDO().Set(EinherjarFighter.SlotKey, f.Slot + 1);
                __0.CancelInvoke("DestroyNow");
                __0.InvokeRepeating("DestroyNow", ValhallaPeople.RiseAfter + 40f, 1f);
                Plugin.Debug("Valhalla: battle place " + f.Slot + " falls");
            }
        }
    }
}
