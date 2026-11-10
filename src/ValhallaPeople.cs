using System.Collections;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Valhalla's people, on each client while its player is there (made when you arrive, gone when you leave):
    // nothing of them is saved or sent. Every client works out the same people in the same places, and those
    // who walk go by the world's clock, so everyone sees them alike.
    //
    // - the einherjar: at the hall's tables, at the yard's dummies, in the duel ring and round it;
    // - Sævar, at peace, by the dais;
    // - Bragi, Valhalla's skald, on the hall's throne; Odin on Hliðskjálf, his back to the hall, looking out;
    //   once the tale's last task is done (the world's key DoneKey), Odin sits in the hall and Bragi stands by;
    // - a few who walk: up and down the way, round the ring, across the yard, from the hall to Hliðskjálf;
    // - the Brim Folk's ghosts, pale and walked through (as the tasks bring them; in development builds a
    //   few already, to see them).
    //
    // The living are solid (a body on the "character" layer); a walker waits while you stand in its way.
    class ValhallaPeople : MonoBehaviour
    {
        internal const string DoneKey = "iovvalhalladone"; // (the game keeps its world keys in lower case)
        internal static int devAfter = -1;   // iov_people: 1 after the tale's end, 0 before, -1 as the world is
        internal static int devCrowd;        // iov_people crowd N: so many more standing about, for the frame rate

        const float HallEnd = 40f;
        static readonly Vector2 Ring = new Vector2(40f, 6f), Yard = new Vector2(40f, -18.5f), Feast = new Vector2(-42f, 8f);

        readonly List<GameObject> made = new List<GameObject>();
        readonly List<Walker> walkers = new List<Walker>();
        bool here;
        Coroutine making;
        static int mask;

        internal static ValhallaPeople Current;

        void Awake() { Current = this; }

        internal void Show(bool on)
        {
            if (on == here) return;
            here = on;
            if (on) making = StartCoroutine(Make());
            else Clear();
        }

        internal void Remake()
        {
            bool was = here;
            Clear();
            here = false;
            if (was) Show(true);
        }

        void Clear()
        {
            if (making != null) StopCoroutine(making);
            making = null;
            foreach (GameObject go in made) if (go != null) Destroy(go);
            made.Clear();
            walkers.Clear();
            actors.Clear();
            everyone.Clear();
        }

        void OnDestroy() { Clear(); if (Current == this) Current = null; }

        bool After()
        {
            if (devAfter >= 0) return devAfter == 1;
            return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(DoneKey);
        }

        // ---- who is where ----

        IEnumerator Make()
        {
            yield return null; // the rock's own things first
            int count = 0;
            bool after = After();

            // The two thrones.
            Transform hallThrone = Find("piece_throne01", t => Mathf.Abs(t.localPosition.x) < 9f);
            Transform highSeat = Find("piece_blackmarble_throne", t => true);
            if (after)
            {
                Speaks(Seat(Odin(), hallThrone, 0f), ValhallaSpeaker.Odin, 1.7f);
                Speaks(Stand(Bragi(), new Vector3(-2f, 0f, HallEnd - 6.2f), Vector3.back, 3f), ValhallaSpeaker.Bragi, 2.2f);
            }
            else
            {
                Speaks(Seat(Bragi(), hallThrone, 0f), ValhallaSpeaker.Bragi, 1.7f);
                Speaks(Seat(Odin(), highSeat, 0f), ValhallaSpeaker.Odin, 1.7f);
            }
            // Sævar, waiting at his own people's table (the Brim Folk's, under the golden birches to the west):
            // at its head, between the two rows, looking out towards the way you come by.
            Speaks(Stand(Saevar(), new Vector3(Feast.x, 0f, Feast.y - 10f), new Vector3(1f, 0f, -0.6f), 3f), ValhallaSpeaker.Saevar, 2.2f);
            yield return null;

            // The feast: on the hall's benches, here one, there two, some places empty; every third with a
            // tankard in hand. Now and then each takes a bite.
            int b = 0, n = 0;
            foreach (Transform bench in Children("piece_bench01"))
            {
                Vector3 p = bench.localPosition;
                if (Mathf.Abs(p.x) > 9f || p.z < 3f || p.z > HallEnd) continue; // the hall's only (the Brim Folk's are theirs)
                foreach (float along in new[] { -0.55f, 0.55f })
                {
                    if (Pick(b * 2 + (along > 0f ? 1 : 0), 17) > 0.42f) continue;
                    Act(Seat(Warrior(n, false), bench, along, n % 3 == 0 ? "Tankard" : ""), Feaster, n * 3.7f);
                    n++;
                    if (++count % 3 == 0) yield return null;
                }
                b++;
            }

            // The yard: two at the dummies, weapon and shield in hand, swinging at them.
            Act(Stand(Warrior(n++, true), new Vector3(Yard.x - 2f, 0f, Yard.y + 2.2f), Vector3.forward, 4f, "SwordIron", "ShieldBanded"), Trainer, 0f);
            Act(Stand(Warrior(n++, true), new Vector3(Yard.x + 6f, 0f, Yard.y + 2.2f), Vector3.forward, 4f, "AxeBronze", "ShieldWood"), Trainer, 2.3f);
            // The ring: two fighting in it (by turns one swings, the other takes it on his shield), and three
            // watching from its edge, cheering now and then.
            // (the square shield, held, lay flat like a tray, and with none his guard looked like a bare arm:
            // a round one, as the trainers' stand right)
            SpiritHuman one = Stand(Warrior(n++, true), new Vector3(Ring.x - 1.3f, 0f, Ring.y), Vector3.right, 4f, "SwordBronze", "ShieldBanded");
            SpiritHuman other = Stand(Warrior(n++, true), new Vector3(Ring.x + 1.3f, 0f, Ring.y), Vector3.left, 4f, "MaceIron", "ShieldSilver");
            if (one != null && other != null) actors.Add(new Actor { who = one, partner = other, kind = Duel });
            foreach (float deg in new[] { 205f, 240f, 300f })
            {
                float a = deg * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Ring.x + Mathf.Cos(a) * 12.6f, 0f, Ring.y + Mathf.Sin(a) * 12.6f);
                Act(Stand(Warrior(n++, deg != 240f), at, new Vector3(Ring.x, 0f, Ring.y) - at, 4f, Watching[(int)(deg / 10f) % Watching.Length], "ShieldWood"), Watcher, deg * 0.05f);
            }
            yield return null;

            // Those who walk.
            Walk(Warrior(n++, true), Solid, 1.3f, 4f, false, new Vector2(0.9f, -57f), new Vector2(0.9f, -3f));
            Walk(Warrior(n++, false), Solid, 1.2f, 0f, true, Circle(Ring, 14.5f, 16, 0f));
            Walk(Warrior(n++, true), Solid, 1.25f, 5f, false, new Vector2(Yard.x - 8f, Yard.y + 5.8f), new Vector2(Yard.x + 8f, Yard.y + 5.8f));
            Walk(Warrior(n++, false), Solid, 1.2f, 6f, false, new Vector2(-1.5f, 43.5f), new Vector2(-1.2f, 51f));
            yield return null;

            // The guard: Einherjar in gold-red flametal with polearms, alike as a guard is. Two at the gate, two
            // at the hall's door, four round Hliðskjálf's foot looking out (they speak up if you come too close
            // to the Allfather), and two who walk their rounds: round the hall, round the high seat.
            float high = IsletBuilder.LookoutZ, gate = IsletBuilder.ValhallaGateZ;
            int g = 0;
            foreach (float x in new[] { -3.6f, 3.6f })
                Speaks(Stand(Guard(g++), new Vector3(x, 0f, gate + 2.5f), Vector3.forward, 4f, GuardWeapon), ValhallaSpeaker.Guard, 2.3f);
            foreach (float x in new[] { -5.2f, 5.2f })
                Speaks(Stand(Guard(g++), new Vector3(x, 0f, 2.2f), Vector3.back, 3f, GuardWeapon), ValhallaSpeaker.Guard, 2.3f);
            foreach (float x in new[] { -8.2f, 8.2f })
                foreach (float z in new[] { -8.2f, 8.2f })
                    Speaks(Stand(Guard(g++), new Vector3(x, 0f, high + z), new Vector3(x, 0f, z), 4f, GuardWeapon), ValhallaSpeaker.HighSeatGuard, 2.3f);
            walkerHolds = GuardWeapon;
            Speaks(Walk(Guard(g++), Solid, 1.1f, 0f, true, new Vector2(11.5f, -1.5f), new Vector2(11.5f, 44f), new Vector2(-11.5f, 44f), new Vector2(-11.5f, -1.5f)), ValhallaSpeaker.Guard, 2.3f);
            walkerHolds = GuardWeapon;
            Speaks(Walk(Guard(g++), Solid, 1.0f, 0f, true, Circle(new Vector2(0f, high), 10.5f, 20, 0.3f)), ValhallaSpeaker.HighSeatGuard, 2.3f);
            ValhallaSpeaker.highSeat = transform.TransformPoint(new Vector3(0f, 2.4f, high));
            yield return null;

#if IOV_DEV
            // The Brim Folk's ghosts: round their own feast, slowly, outside its tables and inside its torches
            // (a circle cut across the tables' ends: they walked over them). (In the tale they come as the tasks
            // bring them.)
            for (int i = 0; i < 3; i++)
                Walk(BrimFolk(i), SpiritHuman.GhostSoft, 0.7f, 3f, true, Around(Feast, 6.8f, 11.2f, 1.5f));
#endif

            // iov_people crowd N: so many more, standing in rows west of the way, to see what they cost.
            for (int i = 0; i < devCrowd; i++)
            {
                Stand(Warrior(100 + i, i % 2 == 0), new Vector3(-14f - (i % 6) * 1.5f, 0f, -40f + (i / 6) * 1.5f), Vector3.right, 4f);
                if (i % 4 == 3) yield return null;
            }
            Plugin.Debug("Valhalla: " + made.Count + " people (" + walkers.Count + " walking), " + (after ? "after" : "before") + " the tale's end");
            making = null;
        }

        const int Solid = SpiritHuman.Solid;

        // ---- looks ----

        static readonly string[][] Gear =
        {
            new[] { "HelmetIron", "ArmorIronChest", "ArmorIronLegs", "CapeWolf" },
            new[] { "HelmetBronze", "ArmorBronzeChest", "ArmorBronzeLegs", "CapeDeerHide" },
            new[] { "HelmetPadded", "ArmorPaddedCuirass", "ArmorPaddedGreaves", "CapeLox" },
            new[] { "HelmetDrake", "ArmorWolfChest", "ArmorWolfLegs", "CapeWolf" },
            new[] { "HelmetBerserker", "ArmorBerserkerChest", "ArmorBerserkerLegs", "" },
            new[] { "HelmetCarapace", "ArmorCarapaceChest", "ArmorCarapaceLegs", "CapeFeather" },
            new[] { "HelmetFlametal", "ArmorFlametalChest", "ArmorFlametalLegs", "CapeAsh" },
            new[] { "HelmetFenring", "ArmorFenringChest", "ArmorFenringLegs", "" },
            new[] { "HelmetRoot", "ArmorRootChest", "ArmorRootLegs", "" },
            new[] { "HelmetTrollLeather", "ArmorTrollLeatherChest", "ArmorTrollLeatherLegs", "CapeTrollHide" },
            new[] { "HelmetLeather", "ArmorLeatherChest", "ArmorLeatherLegs", "CapeLinen" },
        };
        static readonly string[] MenHair = { "Hair1", "Hair2", "Hair3", "Hair5", "Hair6", "Hair7", "Hair9", "Hair10" };
        static readonly string[] WomenHair = { "Hair12", "Hair14", "Hair16", "Hair18", "Hair20" };
        static readonly string[] Beards = { "Beard1", "Beard2", "Beard3", "Beard5", "Beard6", "Beard7", "Beard9", "Beard10" };
        static readonly Vector3[] HairColours =
        {
            new Vector3(0.85f, 0.7f, 0.45f), new Vector3(0.65f, 0.3f, 0.15f), new Vector3(0.4f, 0.27f, 0.17f),
            new Vector3(0.12f, 0.1f, 0.09f), new Vector3(0.6f, 0.45f, 0.3f), new Vector3(0.62f, 0.6f, 0.58f),
        };

        // An einherjar: one of the gear sets, a man or (every fourth) a woman, the helmet off at table.
        static SpiritHuman.Look Warrior(int i, bool helmet)
        {
            string[] set = Gear[(i * 7 + 3) % Gear.Length];
            bool woman = i % 4 == 2;
            return new SpiritHuman.Look
            {
                Model = woman ? 1 : 0,
                Hair = woman ? WomenHair[i % WomenHair.Length] : MenHair[(i * 3) % MenHair.Length],
                Beard = woman ? "" : Beards[(i * 5 + 1) % Beards.Length],
                HairColor = HairColours[(i * 5 + 2) % HairColours.Length],
                SkinColor = Vector3.one * (0.86f + 0.14f * Pick(i, 3)),
                Helmet = helmet ? set[0] : "",
                Chest = set[1], Legs = set[2], Shoulder = set[3],
            };
        }

        // A guard: all in the same gold-red flametal, a polearm in hand; their faces their own.
        static readonly string[] GuardGear = { "HelmetFlametal", "ArmorFlametalChest", "ArmorFlametalLegs", "CapeAsh" };
        const string GuardWeapon = "AtgeirBlackmetal";

        static SpiritHuman.Look Guard(int i)
        {
            return new SpiritHuman.Look
            {
                Hair = MenHair[(i * 5 + 1) % MenHair.Length],
                Beard = Beards[(i * 3 + 2) % Beards.Length],
                HairColor = HairColours[(i * 7 + 3) % HairColours.Length],
                SkinColor = Vector3.one * (0.86f + 0.14f * Pick(i, 41)),
                Helmet = GuardGear[0], Chest = GuardGear[1], Legs = GuardGear[2], Shoulder = GuardGear[3],
            };
        }

        static SpiritHuman.Look Odin()
        {
            return new SpiritHuman.Look { Hair = "Hair8", Beard = "Beard11", HairColor = new Vector3(0.72f, 0.72f, 0.74f), Helmet = "HelmetOdin", Chest = "ArmorMageChest", Legs = "ArmorMageLegs", Shoulder = "CapeOdin" };
        }

        static SpiritHuman.Look Bragi()
        {
            return new SpiritHuman.Look { Hair = "Hair4", Beard = "Beard12", HairColor = new Vector3(0.86f, 0.8f, 0.68f), Chest = "ArmorTunic3", Legs = "ArmorLeatherLegs", Shoulder = "CapeLinen" };
        }

        static SpiritHuman.Look Saevar()
        {
            return new SpiritHuman.Look { Hair = "Hair3", Beard = "Beard5", HairColor = new Vector3(0.45f, 0.33f, 0.22f), Helmet = SeafarerGear.Hat, Chest = SeafarerGear.Vest, Legs = SeafarerGear.Trousers, Shoulder = SeafarerGear.Cape };
        }

        static SpiritHuman.Look BrimFolk(int i)
        {
            bool woman = i % 2 == 1;
            return new SpiritHuman.Look
            {
                Model = woman ? 1 : 0,
                Hair = woman ? WomenHair[(i + 1) % WomenHair.Length] : MenHair[(i + 2) % MenHair.Length],
                Beard = woman ? "" : Beards[i % Beards.Length],
                HairColor = HairColours[(i + 1) % HairColours.Length],
                Chest = SeafarerGear.Vest, Legs = SeafarerGear.Trousers,
            };
        }

        // ---- placing ----

        // A seat's own place (a bench's, a throne's: the game's Chair, read off the piece's prefab, since the
        // rock's copies have had their workings taken out), slid along the seat for a second sitter.
        SpiritHuman Seat(SpiritHuman.Look look, Transform piece, float along, string right = "")
        {
            if (piece == null) return null;
            GameObject prefab = PrefabManager.Instance.GetPrefab(Clean(piece.name));
            Chair chair = prefab != null ? prefab.GetComponentInChildren<Chair>(true) : null;
            if (chair == null || chair.m_attachPoint == null) { Plugin.Debug("Valhalla: " + piece.name + " has no seat"); return null; }
            Vector3 local = prefab.transform.InverseTransformPoint(chair.m_attachPoint.position);
            Quaternion turn = Quaternion.Inverse(prefab.transform.rotation) * chair.m_attachPoint.rotation;
            Vector3 at = piece.TransformPoint(local);
            if (along != 0f)
            {
                // along the seat's length: the longer of its sideways sizes
                Bounds size = Size(piece);
                Vector3 axis = size.size.x >= size.size.z ? Vector3.right : Vector3.forward;
                at += axis * along;
            }
            return Add(SpiritHuman.MakeLocal(look, SpiritHuman.Sit, chair.m_attachAnimation, Solid, transform, at, piece.rotation * turn, right));
        }

        SpiritHuman Stand(SpiritHuman.Look look, Vector3 local, Vector3 facing, float from, string right = "", string left = "")
        {
            local.y = Ground(local.x, local.z, from);
            facing.y = 0f;
            Quaternion turn = transform.rotation * Quaternion.LookRotation(facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward);
            return Add(SpiritHuman.MakeLocal(look, SpiritHuman.Stand, "", Solid, transform, transform.TransformPoint(local), turn, right, left));
        }

        // One you can talk to (hover, use): ValhallaTalk.
        static void Speaks(SpiritHuman person, int who, float height)
        {
            if (person == null) return;
            ValhallaSpeaker speaker = person.gameObject.AddComponent<ValhallaSpeaker>();
            speaker.who = who;
            speaker.height = height;
        }

        SpiritHuman Add(SpiritHuman person)
        {
            if (person != null) { made.Add(person.gameObject); everyone.Add(person); }
            return person;
        }

        // ---- what they do (by the world's clock, so everyone sees the same swing at the same moment) ----

        const int Feaster = 0, Trainer = 1, Duel = 2, Watcher = 3;
        static readonly string[] Watching = { "AxeIron", "SwordIron", "MaceBronze", "AxeBronze" };

        class Actor
        {
            internal SpiritHuman who, partner;
            internal int kind;
            internal float offset;
            internal long last = long.MinValue;
        }

        readonly List<Actor> actors = new List<Actor>();
        readonly List<SpiritHuman> everyone = new List<SpiritHuman>();

        void Act(SpiritHuman who, int kind, float offset)
        {
            if (who != null) actors.Add(new Actor { who = who, kind = kind, offset = offset });
        }

        void Perform(double now)
        {
            foreach (Actor a in actors)
            {
                if (a.who == null) continue;
                double t = now + a.offset;
                switch (a.kind)
                {
                    case Feaster:
                    {
                        // a bite every quarter minute or so
                        long slot = (long)(t / 15.0);
                        if (slot != a.last) { if (a.last != long.MinValue) a.who.Eat(); a.last = slot; }
                        break;
                    }
                    case Trainer:
                    {
                        // three swings in a row at the dummy, then a breath
                        long slot = (long)(t / 0.9);
                        if (slot != a.last) { int k = (int)(slot % 6); if (k < 3) a.who.Strike(k, null); a.last = slot; }
                        break;
                    }
                    case Duel:
                    {
                        // 3.5 s each: one swings three times, the other holds up his shield; then the other way
                        long turnOf = (long)(t / 3.5);
                        bool firstSwings = turnOf % 2 == 0;
                        SpiritHuman swinger = firstSwings ? a.who : a.partner, guard = firstSwings ? a.partner : a.who;
                        if (guard == null || swinger == null) break;
                        guard.blocking = true;
                        swinger.blocking = false;
                        long slot = (long)(t / 1.0);
                        if (slot != a.last) { int k = (int)(slot - turnOf * 3.5); if (k >= 0 && k < 3) swinger.Strike(k, guard); a.last = slot; }
                        break;
                    }
                    case Watcher:
                        // They only watch, weapon in hand. (The player's emotes, cheering and shouting, looked
                        // like bobbing up and down on them: two tries, both dropped.)
                        break;
                }
            }
        }

        // How much of each is worked out, by how far it is from the camera.
        float nextDetail;

        void Details()
        {
            if (Time.time < nextDetail) return;
            nextDetail = Time.time + 0.25f;
            Camera cam = Utils.GetMainCamera();
            if (cam == null) return;
            Vector3 eye = cam.transform.position;
            foreach (SpiritHuman p in everyone) if (p != null) p.Detail(Vector3.Distance(eye, p.transform.position));
        }

        // ---- walking ----

        class Walker
        {
            internal SpiritHuman who;
            internal Vector2[] points;  // in the rock's floor space; a loop (an out-and-back is made into one)
            internal float[] lengths;
            internal float speed, rest, cycle;
            internal double clock;
            internal Rigidbody body;
            internal Quaternion facing;
        }

        string walkerHolds = ""; // what the next walker made carries in his right hand

        SpiritHuman Walk(SpiritHuman.Look look, int ghost, float speed, float rest, bool loop, params Vector2[] route)
        {
            if (route.Length < 2) return null;
            List<Vector2> points = new List<Vector2>(route);
            if (!loop) for (int i = route.Length - 2; i > 0; i--) points.Add(route[i]); // and back
            Walker w = new Walker { points = points.ToArray(), speed = speed, rest = rest };
            w.lengths = new float[w.points.Length];
            for (int i = 0; i < w.points.Length; i++)
            {
                w.lengths[i] = Vector2.Distance(w.points[i], w.points[(i + 1) % w.points.Length]);
                w.cycle += w.lengths[i] / speed + (loop ? 0f : RestAt(w, i));
            }
            if (loop) w.rest = 0f;
            // everyone starts from the world's clock, each walker a little apart
            double now = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.time;
            w.clock = now + walkers.Count * 37.0;
            Vector3 start;
            Vector2 dir;
            Where(w, out start, out dir);
            w.facing = dir != Vector2.zero ? Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)) : Quaternion.identity;
            w.who = SpiritHuman.MakeLocal(look, SpiritHuman.Stand, "", ghost, transform, transform.TransformPoint(start), transform.rotation * w.facing, walkerHolds);
            walkerHolds = "";
            if (w.who == null) return null;
            w.who.keepMoving = true;
            everyone.Add(w.who);
            if (ghost == Solid)
            {
                w.body = w.who.gameObject.AddComponent<Rigidbody>();
                w.body.isKinematic = true; // moved by us, still something you bump into
            }
            made.Add(w.who.gameObject);
            walkers.Add(w);
            return w.who;
        }

        // Out-and-back walkers rest at both ends (the route's first point and its turning point).
        static float RestAt(Walker w, int i) { return (i == 0 || i == w.points.Length / 2) ? w.rest : 0f; }

        // Where a walker is on its route at its clock (the rock's floor space, on the ground), and which way
        // it heads (zero while it rests).
        void Where(Walker w, out Vector3 at, out Vector2 heading)
        {
            float t = (float)(w.clock % w.cycle);
            for (int i = 0; i < w.points.Length; i++)
            {
                float rest = w.rest > 0f ? RestAt(w, i) : 0f;
                Vector2 a = w.points[i], b = w.points[(i + 1) % w.points.Length];
                if (t < rest) { at = Lift(a); heading = Vector2.zero; return; }
                t -= rest;
                float walk = w.lengths[i] / w.speed;
                if (t < walk)
                {
                    Vector2 p = Vector2.Lerp(a, b, t / walk);
                    at = Lift(p);
                    heading = (b - a).normalized;
                    return;
                }
                t -= walk;
            }
            at = Lift(w.points[0]);
            heading = Vector2.zero;
        }

        // On the ground or the paving under a walker: looked for from a metre over the meadow, so never on a
        // table or a bench it passes.
        Vector3 Lift(Vector2 p) { return new Vector3(p.x, Ground(p.x, p.y, IsletBuilder.SkyGround(p.x, p.y) + 1f), p.y); }

        void Update()
        {
            if (made.Count == 0) return;
            Details();
            Perform(ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.time);
            if (walkers.Count == 0) return;
            Player player = Player.m_localPlayer;
            Vector3 you = player != null ? transform.InverseTransformPoint(player.transform.position) : new Vector3(9999f, 0f, 0f);
            float dt = Time.deltaTime;
            foreach (Walker w in walkers)
            {
                if (w.who == null) continue;
                Vector3 at;
                Vector2 heading;
                Where(w, out at, out heading);
                // the living wait while you stand in their way; ghosts pass through
                bool blocked = false;
                if (w.body != null && heading != Vector2.zero)
                {
                    Vector3 ahead = at + new Vector3(heading.x, 0f, heading.y) * 0.8f;
                    blocked = new Vector2(you.x - ahead.x, you.z - ahead.z).sqrMagnitude < 0.9f * 0.9f && Mathf.Abs(you.y - at.y) < 2f;
                }
                if (blocked) { w.who.walking = 0f; continue; }
                w.clock += dt;
                Where(w, out at, out heading);
                if (heading != Vector2.zero) w.facing = Quaternion.Slerp(w.facing, Quaternion.LookRotation(new Vector3(heading.x, 0f, heading.y)), dt * 4f);
                w.who.walking = heading != Vector2.zero ? w.speed : 0f;
                Vector3 world = transform.TransformPoint(at);
                Quaternion turn = transform.rotation * w.facing;
                if (w.body != null) { w.body.MovePosition(world); w.body.MoveRotation(turn); }
                else w.who.transform.SetPositionAndRotation(world, turn);
            }
        }

        // ---- the rock ----

        // The ground (or floor, or step) under a place, in the rock's floor space: the first thing solid below
        // from height from (under the hall's roof, over the bushes).
        float Ground(float x, float z, float from)
        {
            if (mask == 0) mask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain");
            Vector3 top = transform.TransformPoint(new Vector3(x, from, z));
            RaycastHit hit;
            if (Physics.Raycast(top, -transform.up, out hit, from + 30f, mask, QueryTriggerInteraction.Ignore))
                return transform.InverseTransformPoint(hit.point).y;
            return IsletBuilder.SkyGround(x, z);
        }

        // A loop round a rectangle (half sizes hx, hz), its corners cut by cut.
        static Vector2[] Around(Vector2 c, float hx, float hz, float cut)
        {
            return new[]
            {
                c + new Vector2(hx, hz - cut), c + new Vector2(hx - cut, hz), c + new Vector2(-hx + cut, hz), c + new Vector2(-hx, hz - cut),
                c + new Vector2(-hx, -hz + cut), c + new Vector2(-hx + cut, -hz), c + new Vector2(hx - cut, -hz), c + new Vector2(hx, -hz + cut),
            };
        }

        static Vector2[] Circle(Vector2 centre, float radius, int steps, float start)
        {
            Vector2[] points = new Vector2[steps];
            for (int i = 0; i < steps; i++)
            {
                float a = start + i * Mathf.PI * 2f / steps;
                points[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            return points;
        }

        IEnumerable<Transform> Children(string name)
        {
            foreach (Transform t in transform) if (Clean(t.name) == name) yield return t;
        }

        Transform Find(string name, System.Func<Transform, bool> where)
        {
            foreach (Transform t in Children(name)) if (where(t)) return t;
            return null;
        }

        static string Clean(string name)
        {
            int cut = name.IndexOf('(');
            return (cut >= 0 ? name.Substring(0, cut) : name).Trim();
        }

        static Bounds Size(Transform t)
        {
            Bounds b = new Bounds(t.position, Vector3.zero);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            return b;
        }

        // A steady pseudo-random 0..1 for an index (the same on every client).
        static float Pick(int i, int salt)
        {
            uint h = (uint)(i * 2654435761L + salt * 40503L);
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 1023) / 1023f;
        }
    }
}
