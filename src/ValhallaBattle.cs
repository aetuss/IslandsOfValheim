using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IslandsOfValheim
{
    // The einherjar's daily battle (Gylfaginning: every day they arm themselves, go out and fight and fell one
    // another; at the evening meal all rise whole and drink together). On the meadow south-west of the yard
    // (IsletBuilder.Battlefield), four pairs fight from Valhalla's dawn to its sunset, by the world's clock so
    // every player sees the same: by turns one swings and the other takes it on his shield, as the ring's pair
    // do; now and then one of a pair is beaten and down on his knee a while, his foe over him, then rises in a
    // soft gold glow and fights on. At sunset they fade away (to the feast in the hall); at dawn they come back.
    // Their old master stands at the field's north edge: talk to him.
    partial class ValhallaPeople
    {
        class Fighter
        {
            internal SpiritHuman one, other;
            internal SpiritHuman.Look lookOne, lookOther;
            internal Vector3 atOne, atOther;
            internal string[] arms;           // right, left of one; right, left of the other
            internal float period, offset;    // its round (fight, one falls, he rises), by the world's clock
            internal long last = long.MinValue;
            internal bool down, glowing;
            internal SpiritHuman fallen;
        }

        readonly List<Fighter> battle = new List<Fighter>();
        bool battleOn;

        static readonly string[][] BattleArms =
        {
            new[] { "SwordIron", "ShieldBanded", "AxeBronze", "ShieldWood" },
            new[] { "MaceIron", "ShieldSilver", "SwordBronze", "ShieldBanded" },
            new[] { "AxeIron", "ShieldWood", "SwordSilver", "ShieldSilver" },
            new[] { "MaceBronze", "ShieldBanded", "SwordIron", "ShieldWood" },
        };

        // Fighting from Valhalla's dawn to its sunset (the sun's place: 0.25 sunrise, 0.745 the sunset begins).
        internal static bool BattleTime()
        {
            float t;
            string weather, part;
            ValhallaWeather.Now(out t, out weather, out part);
            return t >= 0.25f && t < 0.745f;
        }

        // The pairs and their master; n: the next einherjar's face. Gives back the next.
        int Battlefield(int n)
        {
            Vector2 c = IsletBuilder.Battlefield;
            Vector2[] spots = { new Vector2(-5f, -3f), new Vector2(5f, -3.5f), new Vector2(-4.5f, 3.5f), new Vector2(5.5f, 3f) };
            for (int i = 0; i < spots.Length; i++)
            {
                Vector2 p = c + spots[i];
                Fighter f = new Fighter
                {
                    lookOne = Warrior(n++, true), lookOther = Warrior(n++, true),
                    atOne = new Vector3(p.x - 1.3f, 0f, p.y), atOther = new Vector3(p.x + 1.3f, 0f, p.y),
                    arms = BattleArms[i % BattleArms.Length],
                    period = 80f + i * 9f, offset = i * 23f,
                };
                battle.Add(f);
            }
            battleOn = BattleTime();
            if (battleOn) foreach (Fighter f in battle) Arm(f);
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

        void Arm(Fighter f)
        {
            f.one = Stand(f.lookOne, f.atOne, Vector3.right, 4f, f.arms[0], f.arms[1]);
            f.other = Stand(f.lookOther, f.atOther, Vector3.left, 4f, f.arms[2], f.arms[3]);
            f.down = false;
            f.fallen = null;
            f.last = long.MinValue;
        }

        void Battles(double now)
        {
            if (battle.Count == 0) return;
            bool day = BattleTime();
            if (day != battleOn)
            {
                battleOn = day;
                bool seen = Near(IsletBuilder.Battlefield, 80f);
                foreach (Fighter f in battle) StartCoroutine(day ? Arrive(f, seen) : Leave(f, seen));
                Plugin.Debug("Valhalla: the daily battle " + (day ? "begins" : "ends: to the feast"));
            }
            if (!battleOn) return;
            foreach (Fighter f in battle)
            {
                if (f.one == null || f.other == null) continue;
                double clock = now + f.offset;
                double t = clock % f.period;
                long round = (long)(clock / f.period);
                // each round: fighting, then one is beaten (each round the other one) and goes down on his knee
                // as the ring's yielded do, stays down a while, and rises: the gold glow swells first and he
                // stands at its brightest. (2.0.11 laid him on his back, hands behind his head, the game's
                // "rest": the user found it a day at the beach; and the glow came only as he stood, out of step.)
                SpiritHuman loser = round % 2 == 0 ? f.one : f.other, winner = loser == f.one ? f.other : f.one;
                const double DownAt = 48.0, UpAt = 68.0;
                bool lying = t >= DownAt && t < UpAt;
                if (lying && !f.down)
                {
                    f.down = true;
                    f.glowing = false;
                    f.fallen = loser;
                    loser.blocking = winner.blocking = false;
                    loser.Trigger("emote_kneel");
                }
                if (lying && f.down && !f.glowing && t >= UpAt - GlowRise && f.fallen != null)
                {
                    f.glowing = true;
                    StartCoroutine(Glow(f.fallen.transform.position));
                }
                else if (!lying && f.down)
                {
                    f.down = false;
                    if (f.fallen != null) f.fallen.Trigger("emote_stop");
                    f.fallen = null;
                }
                if (f.down) continue;
                // A real exchange: by turns (3.5 s) one attacks with three swings and the other takes them on his
                // shield. In the round's last 14 s its loser gives way: his foe attacks two turns of three, the
                // last two of those swings get through (he reels from each), and just before he goes down comes
                // one last blow. (2.0.11-2.0.12: they only took turns, one swinging and one guarding, to the end:
                // the user asked how anyone lost that.)
                bool late = t >= DownAt - 14.0, last = t >= DownAt - 1.0;
                long turnOf = (long)(t / 3.5);
                bool winnerAttacks = last || (late ? turnOf % 3 != 2 : (turnOf + round) % 2 == 0);
                SpiritHuman swinger = winnerAttacks ? winner : loser, guard = winnerAttacks ? loser : winner;
                int k = (int)(t - turnOf * 3.5);
                bool through = last || (late && winnerAttacks && k >= 1);
                guard.blocking = !through;
                swinger.blocking = false;
                long slot = (long)(clock / 1.0);
                if (slot != f.last)
                {
                    f.last = slot;
                    if (last) { winner.Strike(0, null); StartCoroutine(Reel(loser, 0.4f)); }
                    else if (k >= 0 && k < 3)
                    {
                        if (through) { swinger.Strike(k, null); StartCoroutine(Reel(guard, 0.4f)); }
                        else swinger.Strike(k, guard);
                    }
                }
            }
        }

        // Struck: he reels from the blow as it lands (the game's stagger).
        static IEnumerator Reel(SpiritHuman who, float after)
        {
            yield return new WaitForSeconds(after);
            if (who != null) who.Trigger("stagger");
        }

        bool Near(Vector2 place, float within)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return false;
            Vector3 you = transform.InverseTransformPoint(player.transform.position);
            return new Vector2(you.x - place.x, you.z - place.y).magnitude < within;
        }

        // Sunset: each fades away where he stands (a pale copy of him in his place, and that fading out); far off,
        // just gone.
        IEnumerator Leave(Fighter f, bool seen)
        {
            SpiritHuman[] two = { f.one, f.other };
            SpiritHuman.Look[] looks = { f.lookOne, f.lookOther };
            f.one = f.other = null;
            f.down = false;
            f.fallen = null;
            List<SpiritHuman> pale = new List<SpiritHuman>();
            for (int i = 0; i < 2; i++)
            {
                if (two[i] == null) continue;
                if (seen)
                {
                    SpiritHuman ghost = SpiritHuman.MakeLocal(looks[i], SpiritHuman.Stand, "", SpiritHuman.GhostSoft, transform,
                        two[i].transform.position, two[i].transform.rotation, f.arms[i * 2], f.arms[i * 2 + 1]);
                    if (ghost != null) { pale.Add(ghost); made.Add(ghost.gameObject); }
                }
            }
            // (a ghost is only seen once it's dressed, a second on: the man stays until then)
            if (pale.Count > 0) yield return new WaitForSeconds(1.2f);
            foreach (SpiritHuman man in two) Gone(man);
            if (pale.Count == 0) yield break;
            List<Material> mats = new List<Material>();
            List<Color> from = new List<Color>();
            foreach (SpiritHuman g in pale)
                foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || r is ParticleSystemRenderer) continue;
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && m.HasProperty("_Color") && m.renderQueue >= 3000) { mats.Add(m); from.Add(m.color); }
                }
            const float Takes = 3f;
            for (float t = 0f; t < Takes; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Takes);
                for (int i = 0; i < mats.Count; i++)
                    if (mats[i] != null) { Color c = from[i]; c.a *= 1f - k; mats[i].color = c; }
                foreach (SpiritHuman g in pale) if (g != null) foreach (Light l in g.GetComponentsInChildren<Light>(true)) l.intensity *= 0.97f;
                yield return null;
            }
            foreach (SpiritHuman g in pale) Gone(g);
        }

        // Dawn: back on the field, each with the gold glow of one who rises (unseen far off).
        IEnumerator Arrive(Fighter f, bool seen)
        {
            Arm(f);
            if (seen && f.one != null) StartCoroutine(Glow(f.one.transform.position));
            if (seen && f.other != null) StartCoroutine(Glow(f.other.transform.position));
            yield break;
        }

        void Gone(SpiritHuman person)
        {
            if (person == null) return;
            made.Remove(person.gameObject);
            everyone.Remove(person);
            foreach (Actor a in actors) if (a.who == person) a.who = null;
            Destroy(person.gameObject);
        }

        // A soft gold glow where one rises: a light that swells and fades (no beam: those are the Allfather's),
        // at its brightest GlowRise seconds after it starts.
        const float GlowRise = 1.3f;

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
}
