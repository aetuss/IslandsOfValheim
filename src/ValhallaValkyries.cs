using System.Collections.Generic;
using UnityEngine;

namespace IslandsOfValheim
{
    // The valkyries (Grímnismál: they bear the ale to the einherjar; Gylfaginning: the mead of Heiðrún, the goat
    // that feeds on Læraðr's leaves). In the hall, three go up and down between the tables with a cup, stop by
    // one at the table and fill his, and he drinks; every few they go back to the barrels by the door to fill
    // theirs. Two of the old shape, winged (the one that carries a newcomer into the world at the game's
    // start), circle high over the hall. All by the world's clock, so every game sees the same.
    partial class ValhallaPeople
    {
        class Server
        {
            internal int index;
            internal SpiritHuman who;
            internal int side;             // -1 west, 1 east (the outer one: by turns)
            internal bool outer;           // the aisle between the outer benches and the wall, or the inner one
            internal float offset;
            internal double round = ServeRound;  // her round's length (the outer one's goes round the hall: longer)
            internal long builtFor = long.MinValue, served = -1;
            internal readonly List<Vector2> points = new List<Vector2>();
            internal readonly List<float> pauses = new List<float>();
            internal readonly List<Vector2> faces = new List<Vector2>();
            internal readonly List<SpiritHuman> serves = new List<SpiritHuman>();
        }

        class Flier { internal Transform body; internal float radius, height, speed, phase; }

        readonly List<Server> servers = new List<Server>();
        readonly List<Flier> fliers = new List<Flier>();
        readonly List<KeyValuePair<Vector2, SpiritHuman>> drinkers = new List<KeyValuePair<Vector2, SpiritHuman>>();

        const double ServeRound = 60.0;  // one round: fill her cup at the barrels, three served, back again
        const double OuterRound = 85.0;  // the outer one's: up one side, across behind the last tables, down the other
        const float ServeWalk = 1.3f, ServePause = 2.6f;
        static readonly Vector2 HallMiddle = new Vector2(0f, 22f);

        static readonly string[] ValkyrieHair = { "Hair11", "Hair13", "Hair6" };

        static SpiritHuman.Look Valkyrie(int i)
        {
            Vector3[] hair = { new Vector3(0.88f, 0.76f, 0.5f), new Vector3(0.93f, 0.88f, 0.72f), new Vector3(0.75f, 0.55f, 0.3f) };
            return new SpiritHuman.Look
            {
                // long hair, loose or braided (the game's Long Braid, Twin Braids, Long and Loose: 2.0.19's bun, curls
                // and gathered braids looked like men to the user)
                Model = 1, Hair = ValkyrieHair[i % ValkyrieHair.Length], HairColor = hair[i % hair.Length], SkinColor = Vector3.one * 0.97f,
                // (a white linen cape: 2.0.19's feather one, the Fallen Valkyrie's, came black and to the floor)
                // (in bronze: 2.0.19's wolf armour glittered green)
                Chest = "ArmorBronzeChest", Legs = "ArmorBronzeLegs", Shoulder = "CapeLinen",
            };
        }

        // After the hall's feasters are seated: the three in the hall, and the two in the sky.
        void Valkyries()
        {
            // those at the tables, by where they sit (the benches either side of each table)
            drinkers.Clear();
            foreach (Actor a in actors)
            {
                if (a.kind != Feaster || a.who == null) continue;
                Vector3 p = transform.InverseTransformPoint(a.who.transform.position);
                if (p.z < 8f || p.z > 34f || Mathf.Abs(p.x) > 7f) continue; // (the hall's)
                drinkers.Add(new KeyValuePair<Vector2, SpiritHuman>(new Vector2(p.x, p.z), a.who));
            }
            servers.Clear();
            for (int i = 0; i < 3; i++)
            {
                Server s = new Server { index = i, side = i == 0 ? -1 : 1, outer = i == 2, offset = i * 20f, round = i == 2 ? OuterRound : ServeRound };
                Build(s, Round(s, Now()));
                Vector2 start = s.points[0];
                s.who = Stand(Valkyrie(i), new Vector3(start.x, 0f, start.y), new Vector3(s.faces[0].x, 0f, s.faces[0].y), 4f, "Tankard");
                if (s.who == null) continue;
                s.who.keepMoving = true;
                Speaks(s.who, ValhallaSpeaker.Valkyrie, 2.2f);
                servers.Add(s);
            }
            // high over the hall, the old winged shape, round and round
            fliers.Clear();
            float ground = IsletBuilder.SkyGround(HallMiddle.x, HallMiddle.y);
            for (int i = 0; i < 2; i++)
            {
                GameObject go = Beast("Valkyrie", new Vector3(HallMiddle.x, 0f, HallMiddle.y), Vector3.forward, 4f, 1f, Color.white, "");
                if (go == null) break;
                fliers.Add(new Flier { body = go.transform, radius = i == 0 ? 34f : 46f, height = ground + (i == 0 ? 26f : 32f), speed = 7f, phase = i * 2.4f });
            }
            Plugin.Debug("Valhalla: " + servers.Count + " valkyries serving (" + drinkers.Count + " at the tables), " + fliers.Count + " in the sky");
        }

        static double Now() { return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.time; }

        static long Round(Server s, double now) { return (long)System.Math.Floor((now + s.offset) / s.round); }

        // One round of hers: from the barrels into her aisle, by up to three (a different few each round) in turn
        // up the hall, back down the aisle, and to the barrels again; waiting at the barrels whatever's left of
        // the round.
        void Build(Server s, long round)
        {
            s.builtFor = round;
            s.points.Clear(); s.pauses.Clear(); s.faces.Clear(); s.serves.Clear();
            int side = s.outer ? (round % 2 == 0 ? -1 : 1) : s.side;
            float aisle = side * (s.outer ? 7f : 2f);
            Vector2 barrels = s.outer ? new Vector2(side * 5.6f, 7.6f) : new Vector2(side * 5.3f, 6.2f);
            Vector2 entry = new Vector2(aisle, s.outer ? 10f : 8f);
            // those she can reach from her aisle: on the bench next to it
            List<KeyValuePair<Vector2, SpiritHuman>> near = drinkers.FindAll(d =>
                Mathf.Sign(d.Key.x) == side && Mathf.Abs(Mathf.Abs(d.Key.x) - (s.outer ? 5.8f : 3f)) < 0.7f);
            System.Random pick = new System.Random((int)(round * 7919 + s.index * 104729));
            List<KeyValuePair<Vector2, SpiritHuman>> chosen = new List<KeyValuePair<Vector2, SpiritHuman>>();
            while (chosen.Count < 3 && near.Count > 0)
            {
                int k = pick.Next(near.Count);
                chosen.Add(near[k]);
                near.RemoveAt(k);
            }
            chosen.Sort((a, b) => a.Key.y.CompareTo(b.Key.y));
            while (true)
            {
                s.points.Clear(); s.pauses.Clear(); s.faces.Clear(); s.serves.Clear();
                Add(s, barrels, 0f, new Vector2(side, 0f), null);
                Add(s, entry, 0f, Vector2.zero, null);
                foreach (KeyValuePair<Vector2, SpiritHuman> d in chosen)
                    Add(s, new Vector2(aisle, d.Key.y), ServePause, new Vector2(Mathf.Sign(d.Key.x - aisle), 0f), d.Value);
                if (s.outer)
                {
                    // (the other side's barrels are her next round's start: up her side, across behind the last
                    // tables, down the other; 2.0.19 went back to her own side's and the next round began at the
                    // other's: the user saw her jump across the hall)
                    Add(s, new Vector2(aisle, 33f), 0f, Vector2.zero, null);
                    Add(s, new Vector2(-aisle, 33f), 0f, Vector2.zero, null);
                    Add(s, new Vector2(-aisle, entry.y), 0f, Vector2.zero, null);
                    Add(s, new Vector2(-barrels.x, barrels.y), 0f, new Vector2(-side, 0f), null);
                }
                else
                {
                    Add(s, entry, 0f, Vector2.zero, null);
                    Add(s, barrels, 0f, new Vector2(side, 0f), null);
                }
                float busy = 0f;
                for (int i = 0; i < s.points.Count; i++)
                {
                    busy += s.pauses[i];
                    if (i + 1 < s.points.Count) busy += Vector2.Distance(s.points[i], s.points[i + 1]) / ServeWalk;
                }
                float left = (float)s.round - busy;
                if (left >= 2f || chosen.Count == 0) { s.pauses[0] = Mathf.Max(0f, left); break; }
                chosen.RemoveAt(chosen.Count - 1);
            }
        }

        static void Add(Server s, Vector2 at, float pause, Vector2 face, SpiritHuman serve)
        {
            s.points.Add(at); s.pauses.Add(pause); s.faces.Add(face); s.serves.Add(serve);
        }

        void ValkyriesMove(double now)
        {
            float dt = Time.deltaTime;
            foreach (Server s in servers)
            {
                if (s.who == null) continue;
                long round = Round(s, now);
                if (round != s.builtFor) Build(s, round);
                float u = (float)(now + s.offset - round * s.round);
                Vector2 at = s.points[0], heading = Vector2.zero, face = s.faces[0];
                for (int i = 0; i < s.points.Count; i++)
                {
                    at = s.points[i];
                    face = s.faces[i];
                    if (u < s.pauses[i])
                    {
                        // she's by one of them: half way through, he has his cup filled, and drinks
                        if (s.serves[i] != null && u > s.pauses[i] * 0.5f && s.served != round * 16 + i) { s.served = round * 16 + i; s.serves[i].Eat(); }
                        break;
                    }
                    u -= s.pauses[i];
                    if (i + 1 >= s.points.Count) break;
                    float walk = Vector2.Distance(s.points[i], s.points[i + 1]) / ServeWalk;
                    if (u < walk)
                    {
                        at = Vector2.Lerp(s.points[i], s.points[i + 1], walk > 0f ? u / walk : 1f);
                        heading = (s.points[i + 1] - s.points[i]).normalized;
                        break;
                    }
                    u -= walk;
                }
                Vector2 look = heading != Vector2.zero ? heading : face;
                Quaternion turn = look != Vector2.zero ? transform.rotation * Quaternion.LookRotation(new Vector3(look.x, 0f, look.y)) : s.who.transform.rotation;
                s.who.walking = heading != Vector2.zero ? ServeWalk : 0f;
                s.who.transform.SetPositionAndRotation(transform.TransformPoint(Lift(at)), Quaternion.RotateTowards(s.who.transform.rotation, turn, 300f * dt));
            }
            foreach (Flier f in fliers)
            {
                if (f.body == null) continue;
                float a = (float)(now * f.speed / f.radius % (Mathf.PI * 2.0)) + f.phase;
                Vector3 at = new Vector3(HallMiddle.x + Mathf.Cos(a) * f.radius, f.height + Mathf.Sin((float)(now * 0.3) + f.phase) * 1.5f, HallMiddle.y + Mathf.Sin(a) * f.radius);
                Vector3 along = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                f.body.localPosition = at;
                f.body.localRotation = Quaternion.LookRotation(along) * Quaternion.Euler(0f, 0f, 12f); // (leaning into the turn)
            }
        }
    }
}
