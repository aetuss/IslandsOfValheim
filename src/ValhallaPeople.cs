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
    // - a few who walk: up and down the way, round the ring, across the yard;
    // - the guard: at the landing, the hall's door and round Hliðskjálf, two of them on their rounds;
    // - the Brim Folk's ghosts, pale and walked through (as the tasks bring them; in development builds a
    //   few already, to see them).
    //
    // The living are solid (a body on the "character" layer); a walker waits while you stand in its way.
    partial class ValhallaPeople : MonoBehaviour
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
            StopAllCoroutines(); // (the making, a farewell, those going in the light)
            making = null;
            // (a farewell left in the middle: she goes with it)
            if (farewellOf != null && farewellOf.GetComponent<ZNetView>() != null && farewellOf.GetComponent<ZNetView>().IsOwner() && ZNetScene.instance != null)
                ZNetScene.instance.Destroy(farewellOf.gameObject);
            farewellOf = null;
            witnesses.Clear();
            byTheTale.Clear();
            foreach (GameObject go in made) if (go != null) Destroy(go);
            made.Clear();
            walkers.Clear();
            actors.Clear();
            everyone.Clear();
            moves.Clear();
            voices.Clear();
            captains.Clear();
            captainSeats.Clear();
            edgeHafdis = null;
            battle.Clear();
            ringPair = null;
            ringCleared = false;
            ringHomeKnown[0] = ringHomeKnown[1] = false;
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
            Thrones(after);
            // Sævar, waiting at his own people's table (the Brim Folk's, under the golden birches to the west):
            // at its head, between the two rows, looking out towards the way you come by.
            Speaks(Stand(Saevar(), new Vector3(Feast.x, 0f, Feast.y - 10f), new Vector3(1f, 0f, -0.6f), 3f), ValhallaSpeaker.Saevar, 2.2f);
            CaptainsAtTable();
            // Andhrímnir, the einherjar's cook, at Eldhrímnir west of the hall's door, a knife in hand, looking over
            // his pot towards the hall (a plate of Sæhrímnir for each of you once a day: ValhallaFeast)
            Vector2 kitchen = IsletBuilder.Kitchen;
            Speaks(Stand(Cook(), new Vector3(kitchen.x - 1.7f, 0f, kitchen.y), Vector3.right, 3f, "KnifeFlint"), ValhallaSpeaker.Cook, 2.2f);
            yield return null;
            count += SoulsAtTheFeast();
            HafdisAtTheEdge();
            HafdisSpear();
            yield return null;

            // The feast: on the hall's benches, here one, there two, some places empty; every third with a
            // tankard in hand. Now and then each takes a bite.
            int b = 0, n = 0;
            Transform spareBench = null;
            float spareAlong = 0f, spareOff = float.MaxValue;
            foreach (Transform bench in Children("piece_bench01"))
            {
                Vector3 p = bench.localPosition;
                if (Mathf.Abs(p.x) > 9f || p.z < 3f || p.z > HallEnd) continue; // the hall's only (the Brim Folk's are theirs)
                foreach (float along in new[] { -0.55f, 0.55f })
                {
                    if (Pick(b * 2 + (along > 0f ? 1 : 0), 17) > 0.42f)
                    {
                        // an empty place: the one nearest the hall's middle is kept for one more (below)
                        float off = Mathf.Abs(p.z - HallEnd * 0.5f);
                        if (off < spareOff) { spareOff = off; spareBench = bench; spareAlong = along; }
                        continue;
                    }
                    Act(Seat(Warrior(n, false), bench, along, n % 3 == 0 ? "Tankard" : ""), Feaster, n * 3.7f);
                    n++;
                    if (++count % 3 == 0) yield return null;
                }
                b++;
            }
            // (one who walked to and fro in the meadow west of the hall sits down with the rest now)
            if (spareBench != null)
            {
                Act(Seat(Warrior(n, false), spareBench, spareAlong, "Tankard"), Feaster, n * 3.7f);
                n++;
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
            if (one != null && other != null) { ringPair = new Actor { who = one, partner = other, kind = Duel }; actors.Add(ringPair); }
            // One watching in every gap between the ring's columns (the two gaps the sparring pair walk out to
            // are kept for them), a little behind the blue fires.
            int w = 0;
            foreach (float deg in new[] { 18f, 54f, 90f, 162f, 198f, 234f, 270f, 306f })
            {
                float a = deg * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Ring.x + Mathf.Cos(a) * 13f, 0f, Ring.y + Mathf.Sin(a) * 13f);
                SpiritHuman watcher = Stand(Warrior(n++, w % 3 != 1), at, new Vector3(Ring.x, 0f, Ring.y) - at, 4f,
                    Watching[w % Watching.Length], WatchingShields[(w * 3) % WatchingShields.Length]);
                Act(watcher, Watcher, deg * 0.05f);
                w++;
                if (count++ % 3 == 2) yield return null;
            }
            // and the ring's master, on his dais on the hall's side, over it all: talk to him to challenge the
            // einherjar
            Vector2 m = IsletBuilder.RingMasterAt;
            Speaks(Stand(RingMaster(), new Vector3(m.x, 0f, m.y), new Vector3(Ring.x - m.x, 0f, Ring.y - m.y), 4f, "Battleaxe"), ValhallaSpeaker.RingMaster, 2.7f);
            yield return null;
            // The daily battle on the meadow south-west of the yard (ValhallaBattle)
            n = Battlefield(n);
            yield return null;

            // Those who walk.
            Walk(Warrior(n++, true), Solid, 1.3f, 4f, false, new Vector2(0.9f, -57f), new Vector2(0.9f, -3f));
            Walk(Warrior(n++, false), Solid, 1.2f, 0f, true, Circle(Ring, 14.5f, 16, 0f));
            Walk(Warrior(n++, true), Solid, 1.25f, 5f, false, new Vector2(Yard.x - 8f, Yard.y + 5.8f), new Vector2(Yard.x + 8f, Yard.y + 5.8f));
            yield return null;

            // The guard: Einherjar in gold-red flametal with polearms, alike as a guard is. Four on the landing's
            // side slabs facing the way, two at the hall's door, four round Hliðskjálf's foot looking out (they
            // speak up if you come too close to the Allfather), and two who walk their rounds: round the hall,
            // round the high seat.
            float high = IsletBuilder.LookoutZ, land = IsletBuilder.ValhallaArrival.z;
            int g = 0;
            foreach (float z in new[] { land - 2f, land + 2f })
                foreach (float x in new[] { -3.2f, 3.2f })
                    Speaks(Stand(Guard(g++), new Vector3(x, 0f, z), new Vector3(-x, 0f, 0f), 4f, GuardWeapon), ValhallaSpeaker.Guard, 2.3f);
            foreach (float x in new[] { -5.2f, 5.2f })
                Speaks(Stand(Guard(g++), new Vector3(x, 0f, 2.2f), Vector3.back, 3f, GuardWeapon), ValhallaSpeaker.Guard, 2.3f);
            walkerHolds = GuardWeapon;
            Speaks(Walk(Guard(g++), Solid, 1.1f, 0f, true, new Vector2(11.5f, -1.5f), new Vector2(11.5f, 44f), new Vector2(-11.5f, 44f), new Vector2(-11.5f, -1.5f)), ValhallaSpeaker.Guard, 2.3f);
            SeatsGuard(after);
            ValhallaSpeaker.highSeat = transform.TransformPoint(new Vector3(0f, 2.4f, high));
            yield return null;

            // The Allfather's beasts (each where no one walks or keeps watch):
            // - Geri and Freki, his wolves, lying at his feet: on Hliðskjálf's third tier, either side of the
            //   steps on the side he looks out on, heads to the steps; a touch of gold in their grey;
            // - Eikþyrnir, the hart of Valhalla (the game's Eikthyr is him), wandering the meadow east of the hall,
            //   slowly, stopping a while at either end: between the guard's round (x 11.5) and the ring's walker;
            // - Gullinkambi, the golden cock that wakes the einherjar, on the hall's ridge over its doors.
            // (and once he sits in the hall, there with him: on its dais either side of his throne, facing down
            // the hall)
            Wolves(after);
            Vector2[] hart = IsletBuilder.HartRoute;
            BeastWalk(Beast("Eikthyr", new Vector3(hart[0].x, 0f, hart[0].y), new Vector3(0.3f, 0f, -1f), 4f, 1f, Color.white, ""), 0.9f, 14f, hart);
            if (Beast("Hen", new Vector3(0f, 0f, 4.6f), Vector3.back, 40f, 1.3f, new Color(1f, 0.76f, 0.22f), "") == null)
                Beast("Chicken", new Vector3(0f, 0f, 4.6f), Vector3.back, 40f, 1.6f, new Color(1f, 0.76f, 0.22f), "");
            yield return null;

            // (1.10.69-128's development builds had three of the Brim Folk's ghosts walking round their feast,
            // to see them by: gone, the user had them out; the tale brings its own)

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

        // ---- what changes when the tale is done (the Allfather comes to his hall) ----

        // Those, kept apart: at the tale's end only they go and come again in their new places (1.10.107-111
        // made the whole of Valhalla's people again: everyone vanished and came back at once, as if new-come).
        readonly List<GameObject> byTheTale = new List<GameObject>();

        T Tale<T>(T thing) where T : Component { if (thing != null) byTheTale.Add(thing.gameObject); return thing; }
        GameObject Tale(GameObject thing) { if (thing != null) byTheTale.Add(thing); return thing; }

        Transform HallThrone { get { return Find("piece_throne01", t => Mathf.Abs(t.localPosition.x) < 9f); } }

        // Before: Bragi on the hall's throne, Odin on Hliðskjálf, his back to the hall. After: Odin on the hall's
        // throne, Bragi standing by.
        void Thrones(bool after)
        {
            Transform hallThrone = HallThrone, highSeat = Find("piece_blackmarble_throne", t => true);
            if (after)
            {
                Speaks(Tale(Seat(Odin(), hallThrone, 0f)), ValhallaSpeaker.Odin, 1.7f);
                Speaks(Tale(Stand(Bragi(), new Vector3(-2f, 0f, HallEnd - 6.2f), Vector3.back, 3f)), ValhallaSpeaker.Bragi, 2.2f);
            }
            else
            {
                Speaks(Tale(Seat(Bragi(), hallThrone, 0f)), ValhallaSpeaker.Bragi, 1.7f);
                Speaks(Tale(Seat(Odin(), highSeat, 0f)), ValhallaSpeaker.Odin, 1.7f);
            }
        }

        // Before: four round Hliðskjálf's foot looking out (they speak up if you come too close to the Allfather)
        // and one walking round it. After: two at the hall's dais's front corners, facing down the hall (1.10.107
        // left them guarding the empty seat, warning you off it).
        void SeatsGuard(bool after)
        {
            float high = IsletBuilder.LookoutZ;
            int g = 20;
            if (!after)
            {
                foreach (float x in new[] { -8.2f, 8.2f })
                    foreach (float z in new[] { -8.2f, 8.2f })
                        Speaks(Tale(Stand(Guard(g++), new Vector3(x, 0f, high + z), new Vector3(x, 0f, z), 4f, GuardWeapon)), ValhallaSpeaker.HighSeatGuard, 2.3f);
                walkerHolds = GuardWeapon;
                Speaks(Tale(Walk(Guard(g++), Solid, 1.0f, 0f, true, Circle(new Vector2(0f, high), 10.5f, 20, 0.3f))), ValhallaSpeaker.HighSeatGuard, 2.3f);
            }
            else
                foreach (float x in new[] { -4.1f, 4.1f })
                    Speaks(Tale(Stand(Guard(g++), new Vector3(x, 0f, HallEnd - 5.4f), Vector3.back, 4f, GuardWeapon)), ValhallaSpeaker.Guard, 2.3f);
        }

        // Geri and Freki: on Hliðskjálf's third tier, or on the hall's dais either side of its throne.
        void Wolves(bool after)
        {
            float high = IsletBuilder.LookoutZ;
            Transform hallThrone = HallThrone;
            foreach (float side in new[] { -1f, 1f })
            {
                if (after && hallThrone != null)
                    Tale(Beast("Wolf", new Vector3(hallThrone.localPosition.x + side * 1.45f, 0f, hallThrone.localPosition.z - 0.2f), Vector3.back, 4f, 1f, new Color(1f, 0.9f, 0.68f), "sleeping"));
                else
                    Tale(Beast("Wolf", new Vector3(side * 2.7f, 0f, high + 3.25f), new Vector3(-side, 0f, 0f), 6f, 1f, new Color(1f, 0.9f, 0.68f), "sleeping"));
            }
        }

        // The tale's end, while you're here: those it moves go, in the light, and come again where they now
        // belong, the Allfather to his hall in a column of light; everyone else as they were.
        internal void TaleEnds()
        {
            foreach (GameObject go in byTheTale)
            {
                if (go == null) continue;
                SpiritHuman person = go.GetComponent<SpiritHuman>();
                if (person != null)
                {
                    everyone.Remove(person);
                    walkers.RemoveAll(w => w.who == person);
                    actors.RemoveAll(a => a.who == person);
                }
                else walkers.RemoveAll(w => w.beast == go.transform);
                voices.RemoveAll(v => v.who == go.transform);
                made.Remove(go);
                Destroy(go);
            }
            byTheTale.Clear();
            Thrones(true);
            SeatsGuard(true);
            Wolves(true);
            Transform hallThrone = HallThrone;
            if (hallThrone != null) LightBeam.Over(hallThrone.position, 5f, 1.8f, 1.2f, 2.6f, true);
            Plugin.Debug("Valhalla: the tale's end: Odin to his hall");
        }

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

        // Andhrímnir: a big red beard, plain working clothes.
        static SpiritHuman.Look Cook()
        {
            return new SpiritHuman.Look { Hair = "Hair5", Beard = "Beard7", HairColor = new Vector3(0.55f, 0.3f, 0.16f), SkinColor = Vector3.one * 0.95f, Chest = "ArmorRagsChest", Legs = "ArmorRagsLegs" };
        }

        static SpiritHuman.Look Bragi()
        {
            return new SpiritHuman.Look { Hair = "Hair4", Beard = "Beard12", HairColor = new Vector3(0.86f, 0.8f, 0.68f), Chest = "ArmorTunic3", Legs = "ArmorLeatherLegs", Shoulder = "CapeLinen" };
        }

        internal static SpiritHuman.Look Saevar()
        {
            return new SpiritHuman.Look { Hair = "Hair3", Beard = "Beard5", HairColor = new Vector3(0.45f, 0.33f, 0.22f), Helmet = SeafarerGear.Hat, Chest = SeafarerGear.Vest, Legs = SeafarerGear.Trousers, Shoulder = SeafarerGear.Cape };
        }

        internal static SpiritHuman.Look BrimFolk(int i)
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
        SpiritHuman Seat(SpiritHuman.Look look, Transform piece, float along, string right = "", int ghost = SpiritHuman.Solid)
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
            return Add(SpiritHuman.MakeLocal(look, SpiritHuman.Sit, chair.m_attachAnimation, ghost, transform, at, piece.rotation * turn, right));
        }

        // ---- the Brim Folk's feast ----

        // Once Rán's net is torn (T3), the souls it held, set free, have come to their people's feast: pale, at
        // the Brim Folk's tables under the golden birches, here one and there two, eating and drinking (the
        // user found the tables empty all the tale long). Not the places kept for the captains. (In Valhalla
        // still once their village stands again: spirits, they are at home in both.)
        int SoulsAtTheFeast()
        {
            if (!RanNet.Done) return 0;
            HashSet<Transform> kept = new HashSet<Transform>();
            foreach (KeyValuePair<Transform, float> seat in captainSeats.Values) kept.Add(seat.Key);
            int n = 0, b = 0;
            foreach (Transform bench in Children("piece_bench01"))
            {
                Vector3 p = bench.localPosition;
                if (Mathf.Abs(p.x - Feast.x) > 9f || Mathf.Abs(p.z - Feast.y) > 10f) continue; // (the Brim Folk's benches only)
                b++;
                foreach (float along in new[] { -0.55f, 0.55f })
                {
                    if (kept.Contains(bench)) continue;
                    if (Pick(b * 2 + (along > 0f ? 1 : 0), 59) > 0.72f) continue; // (a few places empty)
                    SpiritHuman soul = Seat(BrimFolk(n), bench, along, n % 3 == 1 ? "Tankard" : "", SpiritHuman.GhostSoft);
                    Act(soul, Feaster, n * 4.3f + 2f);
                    n++;
                }
            }
            Plugin.Debug("Valhalla: " + n + " freed souls at the Brim Folk's feast");
            return n;
        }

        // ---- Hafdís (T5) ----

        // Alone at the rock's north-western edge, away from the hall, the Brim Folk's table and the Allfather's
        // seat, looking out over the clouds (until she has yielded in the ring): a shield-maiden, her hair loose,
        // in the sea-dark chitin of the deep and a cape of feathers, spear and shield in hand.
        SpiritHuman edgeHafdis;

        void HafdisAtTheEdge()
        {
            if (Story.Key(ValhallaDuel.HafdisWonKey)) return;
            Vector2 spot = IsletBuilder.HafdisEdge;
            Vector3 at = new Vector3(spot.x, 0f, spot.y);
            SpiritHuman.Look look = new SpiritHuman.Look
            {
                Model = 1, Hair = "Hair20", HairColor = ValhallaDuel.HafdisHair,
                Chest = "ArmorCarapaceChest", Legs = "ArmorCarapaceLegs", Shoulder = "CapeFeather",
            };
            edgeHafdis = Stand(look, at, new Vector3(spot.x, 0f, spot.y), 4f, "SpearElderbark", "ShieldSerpentscale");
            Speaks(edgeHafdis, ValhallaSpeaker.Hafdis, 2.2f);
            Plugin.Debug("Valhalla: Hafdís at the rock's edge, at " + spot.ToString("F1"));
        }

        // Gone to the ring (away), in a column of light, once she has had her say (after: the time it's read
        // in; 1.10.104 took her at once, and her words went with her); back at the edge if the bout wasn't won.
        internal void HafdisAway(bool away, float after = 0f)
        {
            if (edgeHafdis == null) return;
            StartCoroutine(Going(edgeHafdis.gameObject, away, after));
        }

        static IEnumerator Going(GameObject go, bool away, float after)
        {
            if (after > 0f) yield return new WaitForSeconds(after);
            if (go == null || away == !go.activeSelf) yield break;
            LightBeam.Over(go.transform.position, 3f, 1.8f, 0.9f, 2f, true);
            yield return new WaitForSeconds(1f);
            if (go != null) go.SetActive(!away);
        }

        // ---- her bout, and her farewell (T5) ----

        // Those whose curse it was watch her bout: the four captains and Sævar, come in the light to stand in a
        // row inside the ring, just in front of its west column (on the hall's side; where the user pointed),
        // facing its middle. (1.10.107-109 brought them among the columns themselves, 10 m out: some came
        // with a column in front of them, and one stood in one.)
        const float WitnessAngle = 180f, WitnessRow = 8.6f, WitnessApart = 1.15f;
        readonly List<SpiritHuman> witnesses = new List<SpiritHuman>();
        static readonly string[] WitnessOrder = { TaleCaptains.Ketill, TaleCaptains.Sigrun, TaleCaptains.Ormr, TaleCaptains.Grimr, "" };
        static readonly string[] WitnessLines = { "$iov_t5_ketill", "$iov_t5_sigrun", "$iov_t5_ormr", "$iov_t5_grimr", "$iov_t5_saevar" };

        internal void Witnesses(bool come)
        {
            if (!come)
            {
                foreach (SpiritHuman w in witnesses)
                    if (w != null) StartCoroutine(Going(w.gameObject, true, 0f));
                witnesses.Clear();
                return;
            }
            if (witnesses.Count > 0 || made.Count == 0) return;
            float w0 = WitnessAngle * Mathf.Deg2Rad;
            Vector2 outward = new Vector2(Mathf.Cos(w0), Mathf.Sin(w0)), along = new Vector2(-outward.y, outward.x);
            for (int i = 0; i < WitnessOrder.Length; i++)
            {
                Vector2 p = Ring + outward * WitnessRow + along * (i - (WitnessOrder.Length - 1) * 0.5f) * WitnessApart;
                Vector3 at = new Vector3(p.x, 0f, p.y);
                string id = WitnessOrder[i];
                SpiritHuman w = Stand(id.Length > 0 ? CaptainLook(id) : Saevar(), at, new Vector3(Ring.x - at.x, 0f, Ring.y - at.z), 4f);
                if (w == null) continue;
                Speaks(w, id.Length > 0 ? ValhallaSpeaker.Captain : ValhallaSpeaker.Saevar, 2.2f);
                ValhallaSpeaker speaker = w.GetComponent<ValhallaSpeaker>();
                if (speaker != null) speaker.captain = id;
                LightBeam.Over(w.transform.position, 3.2f, 1.6f, 0.6f, 2.2f, true);
                witnesses.Add(w);
            }
            Plugin.Debug("T5: " + witnesses.Count + " come to watch Hafdís's bout");
        }

        // Her farewell, once she has yielded: those who watched come to her where she kneels, in a half ring
        // between her and the ring's middle, and each says a word; then she says her last and the light takes
        // her. Then they go, the Allfather rises from Hliðskjálf, and Valhalla is as it is after the tale (Odin
        // in the hall: Remake), her spear left lying where she knelt.
        Character farewellOf;

        internal void Ceremony(Character her)
        {
            farewellOf = her;
            StartCoroutine(Farewell(her));
        }

        IEnumerator Farewell(Character her)
        {
            yield return new WaitForSeconds(3f);
            if (her == null) yield break;
            // Their places: a half ring round her on their own side of her (so none walks through her), each
            // given the place on his own side of the others (sorted round her alike: no two paths cross), and
            // they set off one after another. (1.10.107 sent each to the place of his turn: paths crossed, and
            // some went through her.)
            Vector3 at = transform.InverseTransformPoint(her.transform.position);
            Vector2 mine = new Vector2(at.x, at.z), theirs = Vector2.zero;
            List<SpiritHuman> going = new List<SpiritHuman>();
            foreach (SpiritHuman w in witnesses)
                if (w != null) { going.Add(w); Vector3 p = transform.InverseTransformPoint(w.transform.position); theirs += new Vector2(p.x, p.z); }
            if (going.Count > 0)
            {
                Vector2 toward = (theirs / going.Count - mine).normalized;
                if (toward == Vector2.zero) toward = Vector2.left;
                // round her, from their side's left to its right
                System.Func<Vector2, float> round = p => Vector2.SignedAngle(toward, p - mine);
                going.Sort((a, b) =>
                {
                    Vector3 pa = transform.InverseTransformPoint(a.transform.position), pb = transform.InverseTransformPoint(b.transform.position);
                    return round(new Vector2(pa.x, pa.z)).CompareTo(round(new Vector2(pb.x, pb.z)));
                });
                float span = going.Count > 1 ? 120f / (going.Count - 1) : 0f;
                for (int i = 0; i < going.Count; i++)
                {
                    float off = (going.Count > 1 ? -60f + i * span : 0f) * Mathf.Deg2Rad;
                    Vector2 d = new Vector2(toward.x * Mathf.Cos(off) - toward.y * Mathf.Sin(off), toward.x * Mathf.Sin(off) + toward.y * Mathf.Cos(off));
                    Vector3 to = new Vector3(mine.x + d.x * 2.8f, 0f, mine.y + d.y * 2.8f);
                    moves.Add(new Move { who = going[i], to = to, end = transform.rotation * Quaternion.LookRotation(new Vector3(-d.x, 0f, -d.y)), back = false });
                    yield return new WaitForSeconds(0.4f);
                }
            }
            yield return new WaitForSeconds(5f);
            for (int i = 0; i < witnesses.Count && i < WitnessLines.Length; i++)
            {
                ValhallaSpeaker speaker = witnesses[i] != null ? witnesses[i].GetComponent<ValhallaSpeaker>() : null;
                if (speaker == null) continue;
                // (one at a time: each gone before the next is said; 1.10.107-110 left each up 9 s, and next to
                // each other two or three stood over their heads at once)
                speaker.Say(WitnessLines[i], 4.6f);
                yield return new WaitForSeconds(4.8f);
            }
            if (her != null && Chat.instance != null) Chat.instance.SetNpcText(her.gameObject, Vector3.up * 2.2f, 30f, 7f, "", "$iov_t5_farewell", false);
            yield return new WaitForSeconds(6f);
            if (her != null)
            {
                LightBeam.Over(her.transform.position, 5f, 1.8f, 1.2f, 2.6f, true);
                BossKit.Effect("vfx_odin_despawn", her.transform.position);
            }
            yield return new WaitForSeconds(1.3f);
            if (her != null && her.GetComponent<ZNetView>() != null && her.GetComponent<ZNetView>().IsOwner()) ZNetScene.instance.Destroy(her.gameObject);
            farewellOf = null;
            yield return new WaitForSeconds(3f);
            Witnesses(false);
            yield return new WaitForSeconds(2.5f);
            if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "$iov_t5_odin_rises");
            BossKit.Effect("vfx_odin_despawn", ValhallaSpeaker.highSeat);
            yield return new WaitForSeconds(2f);
            if (ValhallaDuel.Current != null) ValhallaDuel.Current.Stop();
            Plugin.Debug("T5: Hafdís's farewell done; Odin to the hall");
            TaleEnds();
            HafdisSpear();
        }

        // Her spear, planted upright where she came into the ring (its north side, between where you fight and
        // its columns), its point to the sky and a faint gold light at it: her memorial. (1.10.107 laid it on
        // the ground, a thin dark line the user could hardly make out.)
        void HafdisSpear()
        {
            if (!Story.Key(ValhallaDuel.HafdisWonKey)) return;
            GameObject item = PrefabManager.Instance.GetPrefab("SpearElderbark");
            Transform look = item != null ? item.transform.Find("attach") : null;
            if (look == null) return;
            GameObject spear = Instantiate(look.gameObject, transform, false);
            spear.name = "IoV_HafdisSpear";
            spear.SetActive(true);
            foreach (MonoBehaviour s in spear.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(s);
            foreach (Collider c in spear.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (Rigidbody r in spear.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(r);
            spear.transform.localScale = look.lossyScale;
            Vector3 at = new Vector3(Ring.x, 0f, Ring.y + 10.7f);
            spear.transform.localPosition = at;
            spear.transform.localRotation = Quaternion.identity;
            // its long side, and its point: the end of it nearer where it's held (its own middle, the hand).
            // (1.10.109 took the further end for the point, and the user's photos showed it standing point down:
            // for this spear the further end is its shaft's foot.)
            Bounds own = OwnBounds(spear);
            Vector3 size = own.size;
            Vector3 axis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
            float far = Vector3.Dot(own.max, axis), near = Vector3.Dot(own.min, axis);
            Vector3 point = Mathf.Abs(far) >= Mathf.Abs(near) ? -axis : axis;
            spear.transform.localRotation = Quaternion.FromToRotation(point, Vector3.up);
            // stood in the ground, a hand of it sunk
            Bounds b = WorldBounds(spear);
            float ground = Ground(at.x, at.z, 4f);
            Vector3 bottom = transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
            spear.transform.localPosition += new Vector3(at.x - bottom.x, ground - 0.35f - bottom.y, at.z - bottom.z);
            // solid, as a spear stood in the ground is (its copy has no body of its own: the user walked through
            // it, 1.10.127): a thin upright shaft from the ground to its point
            Bounds stood = WorldBounds(spear);
            GameObject shaft = new GameObject("IoV_HafdisSpearShaft");
            shaft.transform.SetParent(transform, false);
            shaft.transform.position = stood.center;
            shaft.transform.rotation = Quaternion.identity;
            shaft.layer = LayerMask.NameToLayer("static_solid");
            CapsuleCollider body = shaft.AddComponent<CapsuleCollider>();
            body.direction = 1; // upright
            body.radius = 0.12f;
            body.height = stood.size.y;
            made.Add(shaft);
            GameObject glow = new GameObject("IoV_HafdisSpearGlow");
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(at.x, ground + b.size.y - 0.3f, at.z);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = HafdisBoss.Glow;
            light.range = 3f;
            light.intensity = 0.9f;
            light.shadows = LightShadows.None;
            made.Add(spear);
            made.Add(glow);
            Plugin.Debug("Valhalla: Hafdís's spear planted, " + b.size.y.ToString("F2") + " m high (its point along its own " + point + ")");
        }

        // A thing's mesh bounds in its own space (its pivot at the middle).
        static Bounds OwnBounds(GameObject go)
        {
            Bounds result = new Bounds();
            bool any = false;
            foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                Bounds m = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = m.center + Vector3.Scale(m.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = go.transform.InverseTransformPoint(f.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        static Bounds WorldBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // ---- the tale's captains at the Brim Folk's table (T2: TaleCaptains) ----

        readonly Dictionary<string, SpiritHuman> captains = new Dictionary<string, SpiritHuman>();
        readonly Dictionary<string, KeyValuePair<Transform, float>> captainSeats = new Dictionary<string, KeyValuePair<Transform, float>>();

        // On the two inner benches of the table nearest Sævar, two to a bench: Ketill and Sigrún, Ormr and his
        // son Grímr. Ghosts until freed (you can still talk to them: a body to point at, nothing to bump into but
        // that).
        void CaptainsAtTable()
        {
            if (!TaleCaptains.Begun) return;
            List<Transform> benches = new List<Transform>();
            foreach (Transform bench in Children("piece_bench01"))
            {
                Vector3 p = bench.localPosition;
                if (Mathf.Abs(p.x - Feast.x) < 3.5f && Mathf.Abs(p.z - (Feast.y - 6f)) < 1.5f) benches.Add(bench);
            }
            benches.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));
            if (benches.Count < 2) { Plugin.Debug("Valhalla: no benches for the captains (" + benches.Count + ")"); return; }
            captainSeats[TaleCaptains.Ketill] = new KeyValuePair<Transform, float>(benches[0], -0.55f);
            captainSeats[TaleCaptains.Sigrun] = new KeyValuePair<Transform, float>(benches[0], 0.55f);
            captainSeats[TaleCaptains.Ormr] = new KeyValuePair<Transform, float>(benches[1], -0.55f);
            captainSeats[TaleCaptains.Grimr] = new KeyValuePair<Transform, float>(benches[1], 0.55f);
            foreach (string id in TaleCaptains.All) Captain(id);
        }

        // The duels just won, here: they come to the table now, each in a column of light.
        internal void CaptainsArrive()
        {
            if (captains.Count > 0 || made.Count == 0) return;
            CaptainsAtTable();
            foreach (SpiritHuman c in captains.Values) if (c != null) LightBeam.Over(c.transform.position, 3.4f, 1.6f, 0.4f, 2.4f, true);
        }

        void Captain(string id)
        {
            KeyValuePair<Transform, float> seat;
            if (!captainSeats.TryGetValue(id, out seat)) return;
            bool free = TaleCaptains.Freed(id);
            SpiritHuman person = Seat(CaptainLook(id), seat.Key, seat.Value, "", free ? Solid : SpiritHuman.GhostSoft);
            if (person == null) return;
            if (!free)
            {
                CapsuleCollider body = person.gameObject.AddComponent<CapsuleCollider>();
                body.center = new Vector3(0f, 0.45f, 0.1f);
                body.height = 1.3f;
                body.radius = 0.33f;
                person.gameObject.layer = LayerMask.NameToLayer("character");
            }
            Speaks(person, ValhallaSpeaker.Captain, 1.7f);
            ValhallaSpeaker speaker = person.GetComponent<ValhallaSpeaker>();
            if (speaker != null) speaker.captain = id;
            captains[id] = person;
        }

        readonly HashSet<string> becoming = new HashSet<string>();

        // Freed while you watch, slowly: a column of light comes down on the ghost, it grows bright and nearly
        // whole in it, and at its brightest the man sits in its place; then the light goes. (line: what the
        // ghost was saying as it went, said on by the man)
        internal void FreeCaptain(string id, string line = "")
        {
            SpiritHuman ghost;
            if (!captains.TryGetValue(id, out ghost) || ghost == null || becoming.Contains(id)) return;
            StartCoroutine(Becoming(id, ghost, line));
        }

        IEnumerator Becoming(string id, SpiritHuman ghost, string line)
        {
            becoming.Add(id);
            yield return new WaitForSeconds(0.9f); // (a breath: the words first)
            if (ghost == null) { becoming.Remove(id); yield break; }
            Vector3 at = ghost.transform.position;
            LightBeam.Over(at, 5.2f, 1.6f, 1.5f, 2.6f, true);
            // its see-through colours, to brighten (not the depth twins' invisible ones)
            List<Material> pale = new List<Material>();
            List<Color> from = new List<Color>();
            foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.HasProperty("_Color") && m.renderQueue >= 3000 && m.shader != null && m.shader.name == "Sprites/Default")
                    {
                        pale.Add(m);
                        from.Add(m.color);
                    }
            }
            Color bright = new Color(1f, 0.96f, 0.86f, 0.93f);
            const float Takes = 2.3f;
            for (float t = 0f; t < Takes; t += Time.deltaTime)
            {
                if (ghost == null) break;
                float k = Mathf.SmoothStep(0f, 1f, t / Takes);
                for (int i = 0; i < pale.Count; i++) if (pale[i] != null) pale[i].color = Color.Lerp(from[i], bright, k);
                yield return null;
            }
            BossKit.Effect("fx_summon_spirit_spawn", at + Vector3.up * 0.6f);
            if (ghost != null)
            {
                made.Remove(ghost.gameObject);
                everyone.Remove(ghost);
                Destroy(ghost.gameObject);
            }
            captains.Remove(id);
            Captain(id);
            becoming.Remove(id);
            SpiritHuman man;
            if (line.Length > 0 && captains.TryGetValue(id, out man) && man != null)
            {
                ValhallaSpeaker speaker = man.GetComponent<ValhallaSpeaker>();
                if (speaker != null) speaker.Say(line);
            }
        }

        static SpiritHuman.Look CaptainLook(string id)
        {
            switch (id)
            {
                case TaleCaptains.Ketill:
                    return new SpiritHuman.Look { Hair = "Hair2", Beard = "Beard9", HairColor = new Vector3(0.35f, 0.24f, 0.15f), Chest = SeafarerGear.Vest, Legs = SeafarerGear.Trousers, Shoulder = SeafarerGear.Cape };
                case TaleCaptains.Sigrun:
                    return new SpiritHuman.Look { Model = 1, Hair = "Hair16", HairColor = new Vector3(0.85f, 0.7f, 0.45f), Chest = SeafarerGear.Vest, Legs = SeafarerGear.Trousers, Shoulder = "CapeLinen" };
                case TaleCaptains.Ormr:
                    return new SpiritHuman.Look { Hair = "Hair6", Beard = "Beard12", HairColor = new Vector3(0.55f, 0.53f, 0.5f), Helmet = SeafarerGear.Hat, Chest = SeafarerGear.Vest, Legs = SeafarerGear.Trousers, Shoulder = "CapeWolf" };
                default:
                    return new SpiritHuman.Look { Hair = "Hair1", Beard = "Beard2", HairColor = new Vector3(0.3f, 0.22f, 0.15f), Chest = "ArmorWolfChest", Legs = "ArmorWolfLegs", Shoulder = "CapeWolf" };
            }
        }

        SpiritHuman Stand(SpiritHuman.Look look, Vector3 local, Vector3 facing, float from, string right = "", string left = "")
        {
            local.y = Ground(local.x, local.z, from);
            facing.y = 0f;
            Quaternion turn = transform.rotation * Quaternion.LookRotation(facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward);
            return Add(SpiritHuman.MakeLocal(look, SpiritHuman.Stand, "", Solid, transform, transform.TransformPoint(local), turn, right, left));
        }

        class Voice { internal Transform who; internal EffectList sound; internal float every, next; internal string name; }
        readonly List<Voice> voices = new List<Voice>();

        static bool Has(EffectList e) { return e != null && e.m_effectPrefabs != null && e.m_effectPrefabs.Length > 0; }

        // Each beast's voice every so often (a while apart, never on the dot), heard only near it.
        void Voices()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            foreach (Voice v in voices)
            {
                if (v.who == null || Time.time < v.next) continue;
                v.next = Time.time + v.every * Random.Range(0.7f, 1.6f);
                if (Vector3.Distance(player.transform.position, v.who.position) > 35f) continue;
                v.sound.Create(v.who.position + Vector3.up, v.who.rotation);
                Plugin.Debug("Valhalla: " + v.name + " calls");
            }
        }

        // A beast that walks a route of its own, as the walkers do (its legs: the animator's forward_speed).
        void BeastWalk(GameObject beast, float speed, float rest, params Vector2[] route)
        {
            if (beast == null) return;
            Walker w = Route(speed, rest, false, route);
            if (w == null) return;
            w.beast = beast.transform;
            w.legs = beast.GetComponentInChildren<Animator>(true);
            w.body = beast.AddComponent<Rigidbody>();
            w.body.isKinematic = true;
            walkers.Add(w);
        }

        // A beast of the game's, as a still copy on this client: made under a switched-off holder (so none of
        // its workings ever wake: not its mind, not its network side), all its scripts taken out, then put in
        // place; its own animator keeps it breathing and looking about. Solid. Tinted if asked (gold).
        GameObject beastHolder;

        GameObject Beast(string prefabName, Vector3 local, Vector3 facing, float from, float scale, Color tint, string pose)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (prefab == null) { Plugin.Debug("Valhalla: no " + prefabName + " for a beast"); return null; }
            if (beastHolder == null)
            {
                beastHolder = new GameObject("IoV_BeastHolder");
                beastHolder.SetActive(false);
                beastHolder.transform.SetParent(transform, false);
            }
            GameObject go = Instantiate(prefab, beastHolder.transform, false);
            go.name = prefabName + "_local";
            // its own voice, now and then, kept before its mind goes: what the game's creature makes idling, or
            // (a wolf has none) the call it gives when it spots you: a wolf's howl, now and then, longer apart
            // (A wolf has no idle sound: its howl is one of its idle animations, whose sound the animation itself
            // calls up (the game's AnimationEffect, kept below with the sound scripts).)
            BaseAI mind = go.GetComponent<BaseAI>();
            if (Has(mind != null ? mind.m_idleSound : null))
                voices.Add(new Voice { who = go.transform, sound = mind.m_idleSound, every = Mathf.Max(12f, mind.m_idleSoundInterval * 2f), next = Time.time + Random.Range(3f, 12f), name = prefabName });
            List<string> sounds = new List<string>();
            foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true))
                sounds.Add(a.name + (a.clip != null ? " (" + a.clip.name + ")" : ""));
            for (int pass = 0; pass < 6; pass++)
            {
                bool any = false;
                MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = scripts.Length - 1; i >= 0; i--)
                {
                    if (scripts[i] is ZSFX || scripts[i] is AnimationEffect) continue; // (sounds its animations call up)
                    DestroyImmediate(scripts[i]);
                    any = true;
                }
                if (!any) break;
            }
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(body);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) if (c.isTrigger) DestroyImmediate(c);
            Plugin.Debug("Valhalla: " + prefabName + "'s own sounds: " + (sounds.Count > 0 ? string.Join(", ", sounds.ToArray()) : "none"));
            local.y = Ground(local.x, local.z, from);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.LookRotation(facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward);
            go.transform.localScale = prefab.transform.localScale * scale;
            if (tint != Color.white)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor("_Color", tint);
                    block.SetColor("_EmissionColor", tint * 0.12f);
                    r.SetPropertyBlock(block);
                }
            }
            Animator animator = go.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                List<string> bools = new List<string>();
                foreach (AnimatorControllerParameter p in animator.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Bool) bools.Add(p.name);
                    if (p.name == pose && pose.Length > 0 && p.type == AnimatorControllerParameterType.Bool) animator.SetBool(pose, true);
                }
                Plugin.Debug("Valhalla: " + prefabName + " placed (" + (pose.Length > 0 ? pose + (bools.Contains(pose) ? "" : " not found") : "idle") + "; its animator's switches: " + string.Join(", ", bools.ToArray()) + ")");
            }
            made.Add(go);
            return go;
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
        static readonly string[] Watching = { "AxeIron", "SwordIron", "MaceBronze", "AxeBronze", "SwordSilver", "MaceIron" };
        static readonly string[] WatchingShields = { "ShieldWood", "ShieldBanded", "ShieldBronzeBuckler", "ShieldSilver", "ShieldWood", "ShieldBlackmetal" };

        // The ring's master: an old champion, in a berserker's war-gear and a wolf cape, a great axe in hand.
        static SpiritHuman.Look RingMaster()
        {
            return new SpiritHuman.Look
            {
                Hair = "Hair6", Beard = "Beard10", HairColor = new Vector3(0.62f, 0.6f, 0.58f), SkinColor = Vector3.one * 0.9f,
                Helmet = "HelmetBerserker", Chest = "ArmorBerserkerChest", Legs = "ArmorBerserkerLegs", Shoulder = "CapeWolf",
            };
        }

        class Actor
        {
            internal SpiritHuman who, partner;
            internal int kind;
            internal float offset;
            internal long last = long.MinValue;
        }

        readonly List<Actor> actors = new List<Actor>();
        readonly List<SpiritHuman> everyone = new List<SpiritHuman>();

        // ---- the ring, while you duel in it ----

        Actor ringPair;                       // the two who spar in the ring
        readonly Vector3[] ringHome = new Vector3[2];
        readonly Quaternion[] ringTurn = new Quaternion[2];
        readonly bool[] ringHomeKnown = new bool[2];
        bool ringCleared;

        // The two sparring walk out to the ring's edge (through gaps between its columns, clear of its fires)
        // and watch with the rest; afterwards they walk back and take up their bout again.
        internal void RingCleared(bool clear)
        {
            if (ringPair == null || clear == ringCleared) return;
            ringCleared = clear;
            SpiritHuman[] two = { ringPair.who, ringPair.partner };
            float[] degrees = { 126f, 342f };
            ringPair.kind = Watcher; // (no more swings, walking out or in)
            int going = 0;
            for (int i = 0; i < 2; i++)
            {
                if (two[i] == null) continue;
                two[i].blocking = false;
                Vector3 to;
                Quaternion end;
                // (their own places in the ring, noted once only: a bout begun again while they were still on
                // their way back took wherever they were then for home, and they sparred there, at the edge)
                moves.RemoveAll(o => o.who == two[i]);
                if (!ringHomeKnown[i])
                {
                    ringHome[i] = transform.InverseTransformPoint(two[i].transform.position);
                    ringTurn[i] = two[i].transform.rotation;
                    ringHomeKnown[i] = true;
                }
                if (clear)
                {
                    float a = degrees[i] * Mathf.Deg2Rad;
                    to = new Vector3(Ring.x + Mathf.Cos(a) * 12.6f, 0f, Ring.y + Mathf.Sin(a) * 12.6f);
                    end = transform.rotation * Quaternion.LookRotation(new Vector3(Ring.x - to.x, 0f, Ring.y - to.z).normalized);
                }
                else
                {
                    to = ringHome[i];
                    end = ringTurn[i];
                }
                going++;
                moves.Add(new Move { who = two[i], to = to, end = end, back = !clear });
            }
            if (!clear && going == 0) ringPair.kind = Duel;
        }

        class Move { internal SpiritHuman who; internal Vector3 to; internal Quaternion end; internal bool back; }
        readonly List<Move> moves = new List<Move>();
        const float StrollSpeed = 1.5f;

        void Strolls(float dt)
        {
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                Move m = moves[i];
                if (m.who == null) { moves.RemoveAt(i); continue; }
                Vector3 at = transform.InverseTransformPoint(m.who.transform.position);
                Vector3 d = m.to - at;
                d.y = 0f;
                float left = d.magnitude;
                if (left < 0.05f)
                {
                    m.who.walking = 0f;
                    m.who.transform.rotation = m.end;
                    moves.RemoveAt(i);
                    // both back in the ring: their bout goes on
                    if (m.back && ringPair != null && !moves.Exists(o => o.back)) ringPair.kind = Duel;
                    continue;
                }
                Vector3 step = at + d / left * Mathf.Min(left, StrollSpeed * dt);
                step.y = Ground(step.x, step.z, 4f);
                m.who.transform.position = transform.TransformPoint(step);
                m.who.transform.rotation = Quaternion.Slerp(m.who.transform.rotation, transform.rotation * Quaternion.LookRotation(d / left), dt * 6f);
                m.who.walking = StrollSpeed;
            }
        }

        // A blow landed in the ring: some of those watching knock weapon on shield.
        internal void Cheer()
        {
            Vector3 ring = transform.TransformPoint(new Vector3(Ring.x, 0f, Ring.y));
            foreach (Actor a in actors)
            {
                // about half of them, each in his own time, once to three times
                if (a.kind != Watcher || a.who == null) continue;
                if (Random.value < 0.5f && Utils.DistanceXZ(a.who.transform.position, ring) < 16f) a.who.Clang(Random.Range(0.05f, 0.7f), Random.Range(1, 4));
                if (a.partner != null && Random.value < 0.5f && Utils.DistanceXZ(a.partner.transform.position, ring) < 16f) a.partner.Clang(Random.Range(0.05f, 0.7f), Random.Range(1, 4));
            }
        }

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
                        if (slot != a.last)
                        {
                            if (a.last != long.MinValue)
                            {
                                a.who.Eat();
                                if (slot % 23 == 11) a.who.Burp(); // each now and then (every six minutes or so)
                            }
                            a.last = slot;
                        }
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
            internal Transform beast;   // or a beast walks it (no person)
            internal Animator legs;     // the beast's
        }

        static void Pace(Walker w, float speed)
        {
            if (w.who != null) { w.who.walking = speed; return; }
            if (w.legs != null) w.legs.SetFloat("forward_speed", speed);
        }

        string walkerHolds = ""; // what the next walker made carries in his right hand

        // A route walked by the world's clock: out and back (resting at both ends) or round a loop.
        Walker Route(float speed, float rest, bool loop, Vector2[] route)
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
            return w;
        }

        SpiritHuman Walk(SpiritHuman.Look look, int ghost, float speed, float rest, bool loop, params Vector2[] route)
        {
            Walker w = Route(speed, rest, loop, route);
            if (w == null) return null;
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
            Battles(ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.time);
            Strolls(Time.deltaTime);
            Voices();
            if (walkers.Count == 0) return;
            Player player = Player.m_localPlayer;
            Vector3 you = player != null ? transform.InverseTransformPoint(player.transform.position) : new Vector3(9999f, 0f, 0f);
            float dt = Time.deltaTime;
            foreach (Walker w in walkers)
            {
                if (w.who == null && w.beast == null) continue;
                Vector3 at;
                Vector2 heading;
                Where(w, out at, out heading);
                // the living wait while you stand in their way; ghosts pass through
                bool blocked = false;
                if (w.body != null && heading != Vector2.zero)
                {
                    float reach = w.beast != null ? 2.6f : 0.8f, room = w.beast != null ? 1.8f : 0.9f; // (a hart is bigger)
                    Vector3 ahead = at + new Vector3(heading.x, 0f, heading.y) * reach;
                    blocked = new Vector2(you.x - ahead.x, you.z - ahead.z).sqrMagnitude < room * room && Mathf.Abs(you.y - at.y) < 2.5f;
                }
                if (blocked) { Pace(w, 0f); continue; }
                w.clock += dt;
                Where(w, out at, out heading);
                if (heading != Vector2.zero) w.facing = Quaternion.Slerp(w.facing, Quaternion.LookRotation(new Vector3(heading.x, 0f, heading.y)), dt * (w.beast != null ? 1.5f : 4f));
                Pace(w, heading != Vector2.zero ? w.speed : 0f);
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
