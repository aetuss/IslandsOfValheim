using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // The einherjar's bouts in the ring (Gylfaginning: every day they arm themselves, go out and fight; the
    // ring's rule: nobody dies in it, the beaten kneels and yields). By turns two of those watching from the gaps
    // between its columns (the gaps with no fire) walk in and fight a real bout: the ring's own fighters (the
    // player's body with a creature's mind, EinherjarFighter), moving about, trading blows and taking them on
    // their shields, until one is beaten and kneels; then both walk back to their places and watch, and before
    // long two more come. When you challenge the ring (ValhallaDuel), any bout stops and they make way.
    //
    // (2.0.11-2.0.15 were a daily battle of four pairs on the meadow west of the yard: the user moved the
    // fighting into the ring, one bout at a time.)
    //
    // A bout goes by the world's clock (one every BoutEvery seconds, the pair by its number), so every game sees
    // the same two walk in. The fighters themselves are networked creatures made by one game (of the players
    // near the ring, the one who joined first), so two never make the same man; the walking in and out is each
    // game's own (the watchers themselves, walking). They touch nobody but each other, and nothing else touches
    // them (BattleRules).
    partial class ValhallaPeople
    {
        class RingSeat
        {
            internal int index;
            internal SpiritHuman who;         // the watcher himself (he walks in, and back)
            internal SpiritHuman.Look look;
            internal string right, left;
            internal Vector2 at, spot;        // his place at the ring's edge; where he stands in the ring
            internal Quaternion seatTurn;
            internal Coroutine walk;
            internal bool there;              // got where he was walking
        }

        readonly List<RingSeat> seats = new List<RingSeat>();
        readonly RingSeat[] pair = new RingSeat[2];
        bool ringCleared;
        long bout = -1, boutDone = long.MinValue;
        int stage;                            // 0 none, 1 on (walking in, fighting), 2 walking back
        long spawnedFor = -1;
        bool ringBusy;                        // someone's own bout (yours, or another player's) in the ring
        float nextBoutTick, nextBattleDetail;

        const double BoutEvery = 100.0;       // a bout begins every so often (if the ring's free)
        const double InAt = 8.0;              // they're in the ring (walked in): the fighters come
        const double ForceAt = 84.0;          // still fighting: the worse off yields
        const double KneelFor = 5.0;          // the beaten kneels this long before they walk back
        const float RingWalk = 1.6f, BoutHealth = 250f, RingReach = 8.5f;

        static Vector3 RingWorld(Transform t) { return t.TransformPoint(new Vector3(Ring.x, IsletBuilder.SkyGround(Ring.x, Ring.y), Ring.y)); }

        // One of the watchers who may be called into a bout.
        void BoutSeat(SpiritHuman who, SpiritHuman.Look look, string right, string left, Vector2 at)
        {
            if (who == null) return;
            Vector2 toMiddle = (Ring - at).normalized;
            seats.Add(new RingSeat
            {
                index = seats.Count, who = who, look = look, right = right, left = left, at = at,
                spot = Ring - toMiddle * 1.6f, seatTurn = who.transform.rotation,
            });
        }

        void ClearBouts()
        {
            seats.Clear();
            pair[0] = pair[1] = null;
            ringCleared = false;
            bout = -1;
            boutDone = long.MinValue;
            stage = 0;
            spawnedFor = -1;
        }

        // You (ValhallaDuel) challenge the ring, or are done: a bout going stops and they walk back; none begins
        // till you're done.
        internal void RingCleared(bool clear)
        {
            ringCleared = clear;
            if (clear && stage == 1) EndBout("the ring is called for");
        }

        // This game makes and unmakes the fighters: of the players near the ring, the one who joined first.
        bool BattleRunner()
        {
            Player me = Player.m_localPlayer;
            if (me == null || ZNetScene.instance == null) return false;
            Vector3 ring = RingWorld(transform);
            if (Vector3.Distance(me.transform.position, ring) > 120f) return false;
            long mine = me.GetZDOID().UserID;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null || p == me) continue;
                if (Vector3.Distance(p.transform.position, ring) > 120f) continue;
                if (p.GetZDOID().UserID < mine) return false;
            }
            return true;
        }

        EinherjarFighter FighterOf(int seat, long of)
        {
            foreach (EinherjarFighter f in EinherjarFighter.Battle.Values)
                if (f != null && !f.gone && f.Slot == seat && f.Bout == of) return f;
            return null;
        }

        void Battles(double now)
        {
            if (seats.Count < 2) return;
            BattleDetail();
            long b = (long)(now / BoutEvery);
            double phase = now - b * BoutEvery;
            if (Time.time >= nextBoutTick)
            {
                nextBoutTick = Time.time + 0.5f;
                ringBusy = RingBusy();
                if (BattleRunner()) Run(b, phase);
            }
            bool blocked = ValhallaDuel.Active || ringBusy; // (a bout of yours, or another player's)
            switch (stage)
            {
                case 0:
                    if (blocked || b == boutDone) break;
                    if (phase < 3.0) StartBout(b);
                    else if (FighterOf(PairOf(b, 0).index, b) != null || FighterOf(PairOf(b, 1).index, b) != null) StartBout(b, true); // (come by mid-bout)
                    else boutDone = b;
                    break;
                case 1:
                    if (blocked) { EndBout("the ring is called for"); break; }
                    // the watcher unseen while the fighter he's become is there
                    bool any = false;
                    double over = -1.0;
                    foreach (RingSeat s in pair)
                    {
                        EinherjarFighter f = FighterOf(s.index, bout);
                        if (f != null) { any = true; if (s.who != null) s.who.Unseen = true; over = System.Math.Max(over, f.OverAt); }
                    }
                    if (b != bout) { EndBout("its while is up"); break; }
                    if (over > 0.0 && now >= over + KneelFor) { EndBout("one has yielded"); break; }
                    if (phase >= ForceAt + KneelFor + 2.0) { EndBout("its while is up"); break; }
                    if (!any && phase >= InAt + 15.0 && spawnedFor != bout && !BattleRunner()) { EndBout("no fighters came"); break; }
                    break;
                case 2:
                    if (pair[0] != null && pair[1] != null && pair[0].there && pair[1].there) { stage = 0; boutDone = bout; }
                    break;
            }
        }

        static RingSeat Of(List<RingSeat> list, long b, int which)
        {
            int k = (int)(b % list.Count);
            return list[which == 0 ? k : (k + 2) % list.Count];
        }

        RingSeat PairOf(long b, int which) { return Of(seats, b, which); }

        // Two walk in from their places to theirs in the ring, facing each other.
        void StartBout(long b, bool joining = false)
        {
            bout = b;
            stage = 1;
            pair[0] = PairOf(b, 0);
            pair[1] = PairOf(b, 1);
            for (int i = 0; i < 2; i++)
            {
                RingSeat s = pair[i], foe = pair[1 - i];
                Vector2 face = foe.spot - s.spot;
                Quaternion end = transform.rotation * Quaternion.LookRotation(new Vector3(face.x, 0f, face.y));
                if (joining) { Place(s, s.spot, end); s.there = true; continue; }
                Go(s, s.spot, end);
            }
            Plugin.Debug("Valhalla: a bout in the ring, " + pair[0].index + " and " + pair[1].index + (joining ? " (going on)" : ""));
        }

        // The bout's over (or stopped): each walker back where his fighter stands (the fighter unseen at once on
        // this game, and unmade by the game that made him), and back to his place.
        void EndBout(string why)
        {
            stage = 2;
            foreach (RingSeat s in pair)
            {
                if (s == null) continue;
                EinherjarFighter f = FighterOf(s.index, bout);
                if (s.who == null) { s.there = true; continue; }
                if (f != null)
                {
                    Vector3 at = transform.InverseTransformPoint(f.transform.position);
                    f.Vanish();
                    s.who.Unseen = false;
                    Place(s, new Vector2(at.x, at.z), f.transform.rotation);
                }
                else s.who.Unseen = false;
                Go(s, s.at, s.seatTurn);
            }
            if (why == "one has yielded") Cheer();
            Plugin.Debug("Valhalla: the bout's over (" + why + ")");
        }

        void Place(RingSeat s, Vector2 at, Quaternion turn)
        {
            if (s.who == null) return;
            if (s.walk != null) StopCoroutine(s.walk);
            s.walk = null;
            s.who.walking = 0f;
            s.who.transform.SetPositionAndRotation(transform.TransformPoint(Lift(at)), turn);
        }

        void Go(RingSeat s, Vector2 to, Quaternion end)
        {
            if (s.who == null) { s.there = true; return; }
            if (s.walk != null) StopCoroutine(s.walk);
            s.there = false;
            s.walk = StartCoroutine(Walking(s, to, end));
        }

        IEnumerator Walking(RingSeat s, Vector2 to, Quaternion end)
        {
            SpiritHuman man = s.who;
            man.keepMoving = true;
            while (man != null)
            {
                Vector3 local = transform.InverseTransformPoint(man.transform.position);
                Vector2 d = to - new Vector2(local.x, local.z);
                float left = d.magnitude;
                if (left < 0.05f) break;
                Vector2 step = new Vector2(local.x, local.z) + d / left * Mathf.Min(left, RingWalk * Time.deltaTime);
                Quaternion heading = transform.rotation * Quaternion.LookRotation(new Vector3(d.x, 0f, d.y));
                man.walking = RingWalk;
                man.transform.SetPositionAndRotation(transform.TransformPoint(Lift(step)), Quaternion.RotateTowards(man.transform.rotation, heading, 360f * Time.deltaTime));
                yield return null;
            }
            if (man == null) { s.there = true; yield break; }
            man.walking = 0f;
            man.keepMoving = false;
            man.transform.rotation = end;
            s.there = true;
            s.walk = null;
        }

        // Someone's own bout in the ring (yours, or another player's): the ring's own fighters in it.
        bool RingBusy()
        {
            Vector3 ring = RingWorld(transform);
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead()) continue;
                EinherjarFighter f = c.GetComponent<EinherjarFighter>();
                if (f != null && !f.battle && Utils.DistanceXZ(c.transform.position, ring) < 16f) return true;
            }
            return false;
        }

        // The game that makes them: the fighters of the bout on (made as the two get to their places), none of
        // any other; kept in the ring; and if it's gone on too long, the worse off yields.
        void Run(long b, double phase)
        {
            List<EinherjarFighter> theirs = new List<EinherjarFighter>();
            foreach (EinherjarFighter f in new List<EinherjarFighter>(EinherjarFighter.Battle.Values))
            {
                if (f == null) continue;
                if (stage != 1 || f.gone || f.Bout != bout) { Unmake(f.gameObject); continue; }
                theirs.Add(f);
            }
            if (stage != 1) return;
            if (spawnedFor != bout && b == bout && phase >= InAt)
            {
                spawnedFor = bout;
                if (theirs.Count == 0) foreach (RingSeat s in pair) Fighter(s);
                return;
            }
            if (theirs.Count != 2) return;
            bool over = theirs[0].yielded || theirs[1].yielded;
            if (!over && b == bout && phase >= ForceAt)
            {
                Character one = theirs[0].GetComponent<Character>(), other = theirs[1].GetComponent<Character>();
                EinherjarFighter worse = one != null && other != null && one.GetHealth() > other.GetHealth() ? theirs[1] : theirs[0];
                worse.Yield(true);
                Plugin.Debug("Valhalla: the bout's gone on long, " + worse.Slot + " yields");
            }
        }

        // One of the two in the ring: made where he stands, dressed as he is (kept with him: BattleRules.Dressed),
        // as strong as the other.
        void Fighter(RingSeat s)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(EinherjarFighter.BattlePrefab) : null;
            if (prefab == null) { Plugin.Log.LogWarning("Valhalla: no " + EinherjarFighter.BattlePrefab); return; }
            Vector3 at = transform.TransformPoint(Lift(s.spot));
            RingSeat foe = pair[0] == s ? pair[1] : pair[0];
            Vector2 face = foe.spot - s.spot;
            GameObject go = Instantiate(prefab, at + Vector3.up * 0.3f, transform.rotation * Quaternion.LookRotation(new Vector3(face.x, 0f, face.y)));
            Character body = go.GetComponent<Character>();
            body.SetMaxHealth(BoutHealth);
            body.SetHealth(BoutHealth);
            EinherjarFighter me = go.GetComponent<EinherjarFighter>();
            if (me != null)
            {
                string gear = string.Join(",", new[] { s.right, s.left, s.look.Helmet, s.look.Chest, s.look.Legs, s.look.Shoulder });
                me.Place(s.index, bout, gear, s.look.Hair ?? "", s.look.Beard ?? "", s.look.Model);
                me.centre = RingWorld(transform);
            }
            VisEquipment vis = go.GetComponent<VisEquipment>();
            if (vis != null)
            {
                vis.SetModel(s.look.Model);
                vis.SetHairColor(s.look.HairColor);
                vis.SetSkinColor(s.look.SkinColor);
            }
            MonsterAI ai = go.GetComponent<MonsterAI>();
            if (ai != null) Traverse.Create(ai).Method("SetPatrolPoint", new[] { typeof(Vector3) }).GetValue(RingWorld(transform));
            Plugin.Debug("Valhalla: in the ring, " + s.index + " (" + s.right + ", " + s.left + ")");
        }

        void Unmake(GameObject go)
        {
            ZNetView nview = go != null ? go.GetComponent<ZNetView>() : null;
            if (nview == null || !nview.IsValid() || ZNetScene.instance == null) return;
            if (!nview.IsOwner()) nview.ClaimOwnership();
            ZNetScene.instance.Destroy(go);
        }

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
    }

    // The bout's rules: the two fight each other only and harm nobody else; nothing but the other harms either
    // (not you, not a fall); a blow takes about the same off whatever the weapon and armour (so a bout lasts
    // about as long each time), and barely moves him (the weapons' push is made for a player's hand: it threw
    // them metres, and they came back oddly); no bar over their heads; dressed from what's kept with them.
    static class BattleRules
    {
        const float Blow = 26f;    // health a landed blow takes, about
        const float Push = 0.15f;  // of a blow's push

        [HarmonyPatch(typeof(BaseAI), "IsEnemy", new[] { typeof(Character), typeof(Character) })]
        static class Foes
        {
            static bool Prefix(Character __0, Character __1, ref bool __result)
            {
                if (EinherjarFighter.Battle.Count == 0 || __0 == null || __1 == null) return true;
                EinherjarFighter a, b;
                bool inA = EinherjarFighter.Battle.TryGetValue(__0, out a), inB = EinherjarFighter.Battle.TryGetValue(__1, out b);
                if (!inA && !inB) return true;
                __result = inA && inB && a != null && b != null && a.Foes(b);
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
                __0.m_pushForce *= Push;
                float total = __0.GetTotalDamage();
                if (total <= 0f) return true;
                // what it must be before his armour so about Blow is left after it (the game: under twice the
                // armour, damage² / (4 armour); over it, damage - armour)
                float want = Blow * Random.Range(0.75f, 1.25f);
                float armour = Mathf.Max(0f, __instance.GetBodyArmor());
                float raw = armour <= want ? want + armour : Mathf.Sqrt(4f * want * armour);
                __0.m_damage.Modify(raw / total);
                if (Random.value < 0.35f && ValhallaPeople.Current != null) ValhallaPeople.Current.Cheer();
                return true;
            }
        }

        // Facing his foe whichever way he steps (as a player with his shield up does), so he sidesteps round him and
        // backs off, not turning his back to walk (EinherjarFighter.Spar).
        [HarmonyPatch(typeof(Character), "AlwaysRotateCamera")]
        static class Facing
        {
            static void Postfix(Character __instance, ref bool __result)
            {
                if (!__result && EinherjarFighter.Battle.Count > 0 && EinherjarFighter.Battle.ContainsKey(__instance)) __result = true;
            }
        }

        // His gear, hair and beard, from what's kept with him, just as the game gives a creature its own (each
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
    }
}
