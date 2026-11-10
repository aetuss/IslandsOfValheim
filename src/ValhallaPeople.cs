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
            moves.Clear();
            voices.Clear();
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
            foreach (float x in new[] { -8.2f, 8.2f })
                foreach (float z in new[] { -8.2f, 8.2f })
                    Speaks(Stand(Guard(g++), new Vector3(x, 0f, high + z), new Vector3(x, 0f, z), 4f, GuardWeapon), ValhallaSpeaker.HighSeatGuard, 2.3f);
            walkerHolds = GuardWeapon;
            Speaks(Walk(Guard(g++), Solid, 1.1f, 0f, true, new Vector2(11.5f, -1.5f), new Vector2(11.5f, 44f), new Vector2(-11.5f, 44f), new Vector2(-11.5f, -1.5f)), ValhallaSpeaker.Guard, 2.3f);
            walkerHolds = GuardWeapon;
            Speaks(Walk(Guard(g++), Solid, 1.0f, 0f, true, Circle(new Vector2(0f, high), 10.5f, 20, 0.3f)), ValhallaSpeaker.HighSeatGuard, 2.3f);
            ValhallaSpeaker.highSeat = transform.TransformPoint(new Vector3(0f, 2.4f, high));
            yield return null;

            // The Allfather's beasts (each where no one walks or keeps watch):
            // - Geri and Freki, his wolves, lying at his feet: on Hliðskjálf's third tier, either side of the
            //   steps on the side he looks out on, heads to the steps; a touch of gold in their grey;
            // - Eikþyrnir, the hart of Valhalla (the game's Eikthyr is him), wandering the meadow east of the hall,
            //   slowly, stopping a while at either end: between the guard's round (x 11.5) and the ring's walker;
            // - Gullinkambi, the golden cock that wakes the einherjar, on the hall's ridge over its doors.
            foreach (float side in new[] { -1f, 1f })
                Beast("Wolf", new Vector3(side * 2.7f, 0f, high + 3.25f), new Vector3(-side, 0f, 0f), 6f, 1f, new Color(1f, 0.9f, 0.68f), "sleeping");
            BeastWalk(Beast("Eikthyr", new Vector3(18f, 0f, 36f), new Vector3(0.3f, 0f, -1f), 4f, 1f, Color.white, ""), 0.9f, 14f,
                new Vector2(18f, 36f), new Vector2(21.5f, 26f), new Vector2(18.5f, 15f));
            if (Beast("Hen", new Vector3(0f, 0f, 4.6f), Vector3.back, 40f, 1.3f, new Color(1f, 0.76f, 0.22f), "") == null)
                Beast("Chicken", new Vector3(0f, 0f, 4.6f), Vector3.back, 40f, 1.6f, new Color(1f, 0.76f, 0.22f), "");
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

        class Voice { internal Transform who; internal EffectList sound; internal float every, next; }
        readonly List<Voice> voices = new List<Voice>();

        // Each beast's voice every so often (a while apart, never on the dot), heard only near it.
        void Voices()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            foreach (Voice v in voices)
            {
                if (v.who == null || Time.time < v.next) continue;
                v.next = Time.time + v.every * Random.Range(0.7f, 1.6f);
                if (Vector3.Distance(player.transform.position, v.who.position) > 30f) continue;
                v.sound.Create(v.who.position + Vector3.up, v.who.rotation);
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
            // its own voice, now and then (what the game's creature makes idling), kept before its mind goes
            BaseAI mind = go.GetComponent<BaseAI>();
            if (mind != null && mind.m_idleSound != null && mind.m_idleSound.m_effectPrefabs != null && mind.m_idleSound.m_effectPrefabs.Length > 0)
                voices.Add(new Voice { who = go.transform, sound = mind.m_idleSound, every = Mathf.Max(8f, mind.m_idleSoundInterval * 2f), next = Time.time + Random.Range(3f, 15f) });
            else Plugin.Debug("Valhalla: " + prefabName + " has no voice of its own");
            for (int pass = 0; pass < 6; pass++)
            {
                MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
                if (scripts.Length == 0) break;
                for (int i = scripts.Length - 1; i >= 0; i--) DestroyImmediate(scripts[i]);
            }
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(body);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) if (c.isTrigger) DestroyImmediate(c);
            foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
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
