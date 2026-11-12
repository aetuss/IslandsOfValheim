using System.Collections.Generic;
using UnityEngine;

namespace IslandsOfValheim
{
    // Where one of the Brim Folk's spirits is once they've come home (the tale's end, T5): put there by the
    // village's builder (and by the harbour's, for Sævar at his old fire), read by BrimSouls. Its fields are
    // public, so the location's copy keeps them.
    class SoulSpot : MonoBehaviour
    {
        internal const int Sit = 0, Feast = 1, Stand = 2, Work = 3, Walk = 4;
        internal const int Folk = 0, Elder = 1, Saevar = 2;

        public int kind, who, index;
        public float along;          // Sit, Feast: along the seat (the spot's parent)
        public string holds = "";    // in the right hand
        public string chest = "";    // dressed otherwise than the folk's vest (the smith's leather)
        public bool talks;
        public string[] lines;       // what this one says, by turns (else the folk's own, at random)
        public float every;          // Work: a swing each so many seconds, four in a row, then a breath
        public Vector3[] route;      // Walk: in BrimSouls' space, there and back (or round, loop)
        public bool loop;
        public float speed = 1f, rest = 4f;
    }

    // The Brim Folk at home: once the tale is done (the world's key ValhallaPeople.DoneKey), on each client while
    // its player is near, their spirits, pale as at their feast in Valhalla, where SoulSpots put them: in the
    // rebuilt village (the hall's benches and high seat, at their trades, on the square, the pier, the lighthouse,
    // the field, by the memorial, walking its lanes), and Sævar by his old fire at the harbour. Nothing of them
    // is saved or sent; every client makes the same, and those who walk go by the world's clock.
    class BrimSouls : MonoBehaviour
    {
        public bool village; // the Brim village's (only once it stands again), or the harbour's
        internal static int devHome = -1; // (development) 1 home, 0 not, -1 as the world is
        internal const string ArrivedKey = "iovsoulshome"; // (the player's own: told they've come home)

        const float Near = 90f, Far = 110f;

        readonly List<GameObject> made = new List<GameObject>();
        readonly List<SpiritHuman> everyone = new List<SpiritHuman>();
        readonly List<Doing> doings = new List<Doing>();
        readonly List<Walker> walkers = new List<Walker>();
        bool here;
        static int terrain;

        class Doing { internal SpiritHuman who; internal int kind; internal float every, offset; internal long last = long.MinValue; }

        class Walker
        {
            internal SpiritHuman who;
            internal Vector3[] points;
            internal float[] lengths;
            internal float speed, rest, cycle;
            internal bool loop;
            internal double clock;
            internal Quaternion facing;
        }

        static bool Home { get { return devHome >= 0 ? devHome == 1 : Story.Key(ValhallaPeople.DoneKey); } }

        void OnEnable() { InvokeRepeating("Check", Random.Range(0.3f, 1.2f), 2f); }

        void OnDisable() { CancelInvoke("Check"); Clear(); }

        void Check()
        {
            Player player = Player.m_localPlayer;
            bool want = player != null && Home && (!village || SpiritsIslet.Stage >= 2);
            float d = player != null ? Utils.DistanceXZ(player.transform.position, transform.position) : float.MaxValue;
            if (!here && want && d < Near) Make(player);
            else if (here && (!want || d > Far)) Clear();
        }

        void Make(Player player)
        {
            here = true;
            foreach (SoulSpot spot in transform.parent.GetComponentsInChildren<SoulSpot>(true))
            {
                SpiritHuman person = Make(spot);
                if (person == null) continue;
                made.Add(person.gameObject);
                everyone.Add(person);
            }
            Plugin.Debug((village ? "Brim village: " : "Harbour: ") + made.Count + " of the Brim Folk at home (" + walkers.Count + " walking)");
            // the first time you come to the village after the tale's end, a word of it
            if (village && made.Count > 0 && !player.HaveSeenTutorial(ArrivedKey))
            {
                player.SetSeenTutorial(ArrivedKey);
                player.Message(MessageHud.MessageType.Center, "$iov_souls_home");
                // and what their homecoming gives you (OdinBlessing): the hearth of your house, Sævar's prices
                player.Message(MessageHud.MessageType.TopLeft, "$iov_hearth_given");
                player.Message(MessageHud.MessageType.TopLeft, "$iov_friend_given");
            }
        }

        void Clear()
        {
            foreach (GameObject go in made) if (go != null) Destroy(go);
            made.Clear();
            everyone.Clear();
            doings.Clear();
            walkers.Clear();
            here = false;
        }

        SpiritHuman Make(SoulSpot spot)
        {
            SpiritHuman.Look look = spot.who == SoulSpot.Saevar ? ValhallaPeople.Saevar() : spot.who == SoulSpot.Elder ? Elder() : ValhallaPeople.BrimFolk(spot.index);
            if (spot.chest.Length > 0) look.Chest = spot.chest;
            const int ghost = SpiritHuman.GhostSoft;
            SpiritHuman person = null;
            switch (spot.kind)
            {
                case SoulSpot.Sit:
                case SoulSpot.Feast:
                    person = SpiritHuman.SitOn(look, spot.transform.parent, spot.along, ghost, transform, spot.holds);
                    break;
                case SoulSpot.Stand:
                case SoulSpot.Work:
                    person = SpiritHuman.MakeLocal(look, SpiritHuman.Stand, "", ghost, transform, spot.transform.position, spot.transform.rotation, spot.holds);
                    break;
                case SoulSpot.Walk:
                    person = Walk(look, spot);
                    break;
            }
            if (person == null) return null;
            if (spot.kind == SoulSpot.Feast) doings.Add(new Doing { who = person, kind = SoulSpot.Feast, offset = spot.index * 4.3f + 2f });
            if (spot.kind == SoulSpot.Work) doings.Add(new Doing { who = person, kind = SoulSpot.Work, every = Mathf.Max(0.8f, spot.every), offset = spot.index * 1.7f });
            if (spot.talks)
            {
                // a body to point at and talk to (a ghost has none of its own)
                bool seated = spot.kind == SoulSpot.Sit || spot.kind == SoulSpot.Feast;
                CapsuleCollider body = person.gameObject.AddComponent<CapsuleCollider>();
                body.center = seated ? new Vector3(0f, 0.45f, 0.1f) : new Vector3(0f, 0.95f, 0f);
                body.height = seated ? 1.3f : 1.9f;
                body.radius = 0.33f;
                person.gameObject.layer = LayerMask.NameToLayer("character");
                ValhallaSpeaker speaker = person.gameObject.AddComponent<ValhallaSpeaker>();
                speaker.who = spot.who == SoulSpot.Saevar ? (village ? ValhallaSpeaker.SaevarVillage : ValhallaSpeaker.SaevarHarbour)
                    : spot.who == SoulSpot.Elder ? ValhallaSpeaker.BrimElder : ValhallaSpeaker.BrimSoul;
                speaker.height = seated ? 1.7f : 2.2f;
                if (spot.lines != null && spot.lines.Length > 0) speaker.lines = spot.lines;
            }
            return person;
        }

        // The village's elder, on the hall's high seat: grey, in a linen cape over the folk's vest.
        static SpiritHuman.Look Elder()
        {
            return new SpiritHuman.Look
            {
                Hair = "Hair6", Beard = "Beard10", HairColor = new Vector3(0.74f, 0.73f, 0.72f),
                Chest = SeafarerGear.Vest, Legs = SeafarerGear.Trousers, Shoulder = "CapeLinen",
            };
        }

        // ---- walking (by the world's clock, there and back, resting at both ends; or round) ----

        SpiritHuman Walk(SpiritHuman.Look look, SoulSpot spot)
        {
            if (spot.route == null || spot.route.Length < 2) return null;
            List<Vector3> points = new List<Vector3>(spot.route);
            if (!spot.loop) for (int i = spot.route.Length - 2; i > 0; i--) points.Add(spot.route[i]); // and back
            Walker w = new Walker { points = points.ToArray(), speed = Mathf.Max(0.3f, spot.speed), rest = spot.loop ? 0f : spot.rest, loop = spot.loop };
            w.lengths = new float[w.points.Length];
            for (int i = 0; i < w.points.Length; i++)
            {
                Vector3 a = w.points[i], b = w.points[(i + 1) % w.points.Length];
                w.lengths[i] = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                w.cycle += w.lengths[i] / w.speed + RestAt(w, i);
            }
            if (w.cycle <= 0f) return null;
            w.clock = Now() + spot.index * 37.0;
            Vector3 at;
            Vector3 heading;
            Where(w, out at, out heading);
            w.facing = heading != Vector3.zero ? Quaternion.LookRotation(heading) : Quaternion.identity;
            w.who = SpiritHuman.MakeLocal(look, SpiritHuman.Stand, "", SpiritHuman.GhostSoft, transform, transform.TransformPoint(at), transform.rotation * w.facing, spot.holds);
            if (w.who == null) return null;
            w.who.keepMoving = true;
            walkers.Add(w);
            return w.who;
        }

        static double Now() { return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.time; }

        static float RestAt(Walker w, int i) { return w.loop ? 0f : (i == 0 || i == w.points.Length / 2) ? w.rest : 0f; }

        // Where a walker is at its clock (in this space, on the ground), and which way it heads (zero: resting).
        void Where(Walker w, out Vector3 at, out Vector3 heading)
        {
            float t = (float)(w.clock % w.cycle);
            for (int i = 0; i < w.points.Length; i++)
            {
                Vector3 a = w.points[i], b = w.points[(i + 1) % w.points.Length];
                float rest = RestAt(w, i);
                if (t < rest) { at = Ground(a); heading = Vector3.zero; return; }
                t -= rest;
                float walk = w.lengths[i] / w.speed;
                if (t < walk)
                {
                    at = Ground(Vector3.Lerp(a, b, t / walk));
                    heading = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
                    return;
                }
                t -= walk;
            }
            at = Ground(w.points[0]);
            heading = Vector3.zero;
        }

        // The ground under a point of a lane (the islet's own terrain only: never a roof, a bench or a tree).
        Vector3 Ground(Vector3 p)
        {
            if (terrain == 0) terrain = LayerMask.GetMask("terrain");
            RaycastHit hit;
            if (Physics.Raycast(transform.TransformPoint(p + Vector3.up * 6f), -transform.up, out hit, 20f, terrain, QueryTriggerInteraction.Ignore))
                p.y = transform.InverseTransformPoint(hit.point).y;
            return p;
        }

        // ---- every frame ----

        float nextDetail;

        void Update()
        {
            if (made.Count == 0) return;
            double now = Now();
            Perform(now);
            float dt = Time.deltaTime;
            foreach (Walker w in walkers)
            {
                if (w.who == null) continue;
                w.clock += dt;
                Vector3 at, heading;
                Where(w, out at, out heading);
                if (heading != Vector3.zero) w.facing = Quaternion.Slerp(w.facing, Quaternion.LookRotation(heading), dt * 4f);
                w.who.walking = heading != Vector3.zero ? w.speed : 0f;
                w.who.transform.SetPositionAndRotation(transform.TransformPoint(at), transform.rotation * w.facing);
            }
            if (Time.time < nextDetail) return;
            nextDetail = Time.time + 0.25f;
            Camera cam = Utils.GetMainCamera();
            if (cam == null) return;
            Vector3 eye = cam.transform.position;
            foreach (SpiritHuman p in everyone) if (p != null) p.Detail(Vector3.Distance(eye, p.transform.position));
        }

        // A bite now and then at table; at work, four swings in a row and a breath.
        void Perform(double now)
        {
            foreach (Doing d in doings)
            {
                if (d.who == null) continue;
                double t = now + d.offset;
                if (d.kind == SoulSpot.Feast)
                {
                    long slot = (long)(t / 15.0);
                    if (slot == d.last) continue;
                    if (d.last != long.MinValue)
                    {
                        d.who.Eat();
                        if (slot % 23 == 11) d.who.Burp();
                    }
                    d.last = slot;
                }
                else
                {
                    long slot = (long)(t / d.every);
                    if (slot == d.last) continue;
                    int k = (int)(slot % 6);
                    if (k < 4) d.who.Strike(k, null);
                    d.last = slot;
                }
            }
        }
    }
}
