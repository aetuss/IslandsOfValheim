using UnityEngine;

namespace IslandsOfValheim
{
    // Those in Valhalla you can talk to (hover shows their name; use to talk), and what they say. As with
    // Sævar at his harbour: a line over their head, and a longer telling as a page for the compendium.
    //
    // The welcome: Bragi greets you as you come near the hall's throne and sends you to Sævar; Sævar tells you
    // what still holds his people (a page), and sends you to the ring: the world's key WelcomedKey. Odin is
    // only named; he doesn't turn to you yet.
    class ValhallaSpeaker : MonoBehaviour, Hoverable, Interactable
    {
        internal const string WelcomedKey = "iovvalhallawelcomed";
        internal const int Bragi = 0, Saevar = 1, Odin = 2, Guard = 3, HighSeatGuard = 4, RingMaster = 5, Captain = 6, Hafdis = 7;
        // the Brim Folk at home, once the tale is done (BrimSouls): one of them, their elder, and Sævar's spirit at
        // his harbour's old fire and on his village's pier
        internal const int BrimSoul = 8, BrimElder = 9, SaevarHarbour = 10, SaevarVillage = 11;
        // Andhrímnir, the einherjar's cook (his plate of Sæhrímnir: ValhallaFeast)
        internal const int Cook = 12;
        static readonly string[] CookTalk = { "$iov_cook_talk1", "$iov_cook_talk2", "$iov_cook_talk3" };
        static readonly string[] SoulTalk = { "$iov_brimsoul_talk1", "$iov_brimsoul_talk2", "$iov_brimsoul_talk3", "$iov_brimsoul_talk4", "$iov_brimsoul_talk5" };
        static readonly string[] ElderTalk = { "$iov_brimelder_talk1", "$iov_brimelder_talk2", "$iov_brimelder_talk3", "$iov_brimelder_home", "$iov_brimelder_talk4", "$iov_brimelder_talk5", "$iov_brimelder_talk6" };
        static readonly string[] HarbourTalk = { "$iov_saevar_harbour1", "$iov_saevar_harbour3", "$iov_saevar_harbour2", "$iov_saevar_harbour4" };
        static readonly string[] VillageTalk = { "$iov_saevar_village1", "$iov_saevar_village3", "$iov_saevar_village2", "$iov_saevar_home", "$iov_saevar_village4" };
        // The Brim Folk at home now and then say a word of the hour, the weather or how you look (Aside), and
        // very rarely something odd.
        static readonly string[] NightTalk = { "$iov_ctx_night1", "$iov_ctx_night2" };
        static readonly string[] StormTalk = { "$iov_ctx_storm1", "$iov_ctx_storm2" };
        static readonly string[] RareTalk = { "$iov_rare1", "$iov_rare2", "$iov_rare3" };
        static int saevarTurn; // (his lines by turns, not at random: both heard in two talks)
        internal string[] lines; // one of the folk's own (else the folk's at random)
        int ownTurn;
        internal const string HafdisMetKey = "iovt5met"; // (the player's own: Hafdís has told them who she is)
        internal string captain = ""; // which of the tale's captains (TaleCaptains), for Captain

        // Hliðskjálf's seat (world), and when its guards last spoke up about it
        internal static Vector3 highSeat;
        static float warnedAt = -100f;
        static readonly string[] GuardTalk = { "$iov_guard_talk1", "$iov_guard_talk2", "$iov_guard_talk3", "$iov_guard_talk4", "$iov_guard_talk5" };
        static readonly string[] RingAgain = { "$iov_ringmaster_again", "$iov_ringmaster_again2", "$iov_ringmaster_again3" };
        // The freed captains at the table now and then talk among themselves: the first says his part, the
        // other answers.
        static readonly string[][] Banter =
        {
            new[] { TaleCaptains.Ormr, "$iov_banter1_a", TaleCaptains.Grimr, "$iov_banter1_b" },
            new[] { TaleCaptains.Grimr, "$iov_banter2_a", TaleCaptains.Ormr, "$iov_banter2_b" },
            new[] { TaleCaptains.Ketill, "$iov_banter3_a", TaleCaptains.Sigrun, "$iov_banter3_b" },
            new[] { TaleCaptains.Sigrun, "$iov_banter4_a", TaleCaptains.Ketill, "$iov_banter4_b" },
        };

        internal int who;
        internal float height = 2.2f; // over its feet (lower for one sitting)
        bool greeted;
        float nextGreetLook;

        static bool Welcomed { get { return Story.Key(WelcomedKey); } }

        static readonly string[] BragiTalk = { "$iov_bragi_talk1", "$iov_bragi_talk2", "$iov_bragi_talk3", "$iov_bragi_talk4", "$iov_bragi_talk5", "$iov_bragi_talk6" };

        string Name
        {
            get
            {
                if (who == Captain) return TaleCaptains.Of(captain).Name;
                if (who == Hafdis) return "$iov_hafdis";
                if (who == BrimSoul) return "$iov_brimsoul";
                if (who == BrimElder) return "$iov_brimelder";
                if (who == Cook) return "$iov_cook";
                if (who == SaevarHarbour || who == SaevarVillage) return SeaTrader.NameToken;
                return who == Bragi ? "$iov_bragi" : who == Saevar ? SeaTrader.NameToken : who == Odin ? "$iov_odin" : who == RingMaster ? "$iov_ringmaster" : "$iov_guard";
            }
        }

        // The tale done (Hafdís at peace): the Allfather sits in his hall, and speaks.
        static bool TaleDone { get { return Story.Key(ValhallaPeople.DoneKey); } }
        internal const string OdinSpokeKey = "iovodinspoke"; // (the player's own: his first words heard)
        static readonly string[] OdinTalk = { "$iov_odin_talk1", "$iov_odin_talk2", "$iov_odin_talk3", "$iov_odin_talk4", "$iov_odin_talk5", "$iov_odin_talk6" };

        public string GetHoverText()
        {
            if (who == Odin && !TaleDone) return Localization.instance.Localize(Name);
            // Bragi, once you've been welcomed: your saga too; Andhrímnir: a plate (the alt use key, as the game's
            // item stands use it)
            if ((who == Bragi && Welcomed) || who == Cook)
                return Localization.instance.Localize(Name + "\n[<color=yellow><b>$KEY_Use</b></color>] $raven_interact\n[<color=yellow><b>" +
                    (ZInput.IsGamepadActive() ? "$KEY_AltKeys" : "$KEY_AltPlace") + " + $KEY_Use</b></color>] " + (who == Cook ? "$iov_cook_ask" : "$iov_saga_ask"));
            if (who == RingMaster)
                return Localization.instance.Localize(Name + (ValhallaDuel.Active ? "" : "\n[<color=yellow><b>$KEY_Use</b></color>] $iov_duel_challenge"));
            return Localization.instance.Localize(Name + "\n[<color=yellow><b>$KEY_Use</b></color>] $raven_interact");
        }

        public string GetHoverName() { return Localization.instance.Localize(Name); }

        public float GetHoverOffset() { return 0f; }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player player = user as Player;
            if (hold || player == null || player != Player.m_localPlayer || (who == Odin && !TaleDone)) return false;
            if (who == Odin)
            {
                // his first words: what you've done, and (lightly) that the sea forgets nothing; then now and then
                // a word of welcome
                if (!player.HaveSeenTutorial(OdinSpokeKey)) { player.SetSeenTutorial(OdinSpokeKey); Say("$iov_odin_done"); }
                else if (!OdinBlessing.Has(player)) OdinBlessing.Give(player, this); // (then, his blessing)
                else Say(Random.value < 0.3f ? "$iov_odin_done" : OdinTalk[Random.Range(0, OdinTalk.Length)]);
                return true;
            }
            if (who == Cook)
            {
                if (alt) ValhallaFeast.Serve(player, this);
                else Say(CookTalk[Random.Range(0, CookTalk.Length)]);
                return true;
            }
            if (who == Captain)
            {
                if (TaleCaptains.Freed(captain) && Random.value < 0.35f && Banters()) return true;
                string free;
                string line = TaleCaptains.Talk(player, captain, out free);
                // (the ghost says it; freed himself, he goes on saying it once he's whole)
                Say(line);
                if (free != null) TaleCaptains.Free(free, free == captain ? line : "");
                return true;
            }
            if (who == RingMaster)
            {
                // Sævar sends you here; then the ring is yours whenever you like (again after you've won)
                if (ValhallaDuel.Active || ValhallaDuel.Current == null) return false;
                if (!Welcomed) { Say("$iov_ringmaster_wait"); return true; }
                Say(Story.Key(ValhallaDuel.WonKey) ? RingAgain[Random.Range(0, RingAgain.Length)] : "$iov_ringmaster_start");
                ValhallaDuel.Current.Begin();
                return true;
            }
            if (who == Hafdis)
            {
                // T5: alone until the village stands again; then who she is (a page), and then the ring
                if (ValhallaDuel.Active || ValhallaDuel.Current == null) return false;
                if (SpiritsIslet.Stage < 2) { Say("$iov_t5_alone"); return true; }
                if (!player.HaveSeenTutorial(HafdisMetKey))
                {
                    player.SetSeenTutorial(HafdisMetKey);
                    Story.Tell(player, "$iov_t5_page", "$iov_t5_page_text");
                    Say("$iov_t5_meet");
                    return true;
                }
                player.AddKnownText("$iov_t5_page", "$iov_t5_page_text");
                Say("$iov_t5_challenge");
                ValhallaDuel.Current.BeginHafdis();
                if (ValhallaPeople.Current != null) ValhallaPeople.Current.HafdisAway(true, 5f); // (her words read first)
                return true;
            }
            if (who == BrimSoul || who == BrimElder || who == SaevarHarbour || who == SaevarVillage)
            {
                string aside = Aside(player);
                if (aside != null) { Say(aside); return true; }
            }
            if (who == BrimSoul)
            {
                // each their own few lines, of their own place and trade (the hall, the square, the pier, the
                // forge, the loom ...), by turns
                if (lines != null && lines.Length > 0) Say(lines[ownTurn++ % lines.Length]);
                else Say(SoulTalk[Random.Range(0, SoulTalk.Length)]);
                return true;
            }
            if (who == BrimElder) { Say(ElderTalk[Random.Range(0, ElderTalk.Length)]); return true; }
            if (who == SaevarHarbour || who == SaevarVillage)
            {
                string[] lines = who == SaevarHarbour ? HarbourTalk : VillageTalk;
                Say(lines[saevarTurn++ % lines.Length]);
                return true;
            }
            if (who == Guard || who == HighSeatGuard)
            {
                Say(GuardTalk[Random.Range(0, GuardTalk.Length)]);
                return true;
            }
            if (who == Bragi)
            {
                // (not a word between the lines of a saga he's singing)
                if (BragiSaga.Singing) return true;
                // the alt use key: your saga (BragiSaga), once you've been welcomed
                if (alt && Welcomed)
                {
                    BragiSaga.Sing(this, player);
                    greeted = true;
                    return true;
                }
                // (once the einherjar have taken you in, now and then he sings of it)
                bool won = Story.Key(ValhallaDuel.WonKey);
                Say(!Welcomed ? "$iov_bragi_greet" : won && Random.value < 0.5f ? "$iov_bragi_won" : BragiTalk[Random.Range(0, BragiTalk.Length)]);
                greeted = true;
                return true;
            }
            // Sævar
            if (!Welcomed)
            {
                Say("$iov_valsaevar_welcome");
                Story.Tell(player, "$iov_lore_scattered", "$iov_lore_scattered_text");
                Story.Set(WelcomedKey);
                Plugin.Debug("Valhalla: welcomed; the einherjar wait at the ring");
                return true;
            }
            player.AddKnownText("$iov_lore_scattered", "$iov_lore_scattered_text"); // (a page lost is found again)
            // to the ring; once you've won there, to his captains at the table (T2); all of them freed, out to Rán's
            // net (T3: a page and a chart)
            Say(!Story.Key(ValhallaDuel.WonKey) ? "$iov_valsaevar_ring" : TaleCaptains.AllFreed ? RanNet.SaevarLine(player) : "$iov_valsaevar_won");
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }

        // Now and then (about one talk in three, when something fits) a word of the night, the morning, the
        // weather, or of you soaked or hurt; and about one talk in fifty, something odd. Else null.
        static string Aside(Player player)
        {
            if (Random.value < 0.02f) return RareTalk[Random.Range(0, RareTalk.Length)];
            if (Random.value >= 0.3f) return null;
            System.Collections.Generic.List<string> fits = new System.Collections.Generic.List<string>();
            EnvMan env = EnvMan.instance;
            if (env != null && EnvMan.IsNight()) fits.AddRange(NightTalk);
            else if (env != null && EnvMan.IsDaylight() && env.GetDayFraction() < 0.36f) fits.Add("$iov_ctx_morning");
            if (env != null && EnvMan.IsWet()) fits.AddRange(StormTalk);
            if (player.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectWet)) fits.Add("$iov_ctx_wet");
            if (player.GetHealthPercentage() < 0.3f) fits.Add("$iov_ctx_hurt");
            return fits.Count > 0 ? fits[Random.Range(0, fits.Count)] : null;
        }

        // A freed captain and a freed one of the other two talking at the table (only if both are here and freed).
        bool Banters()
        {
            System.Collections.Generic.List<string[]> mine = new System.Collections.Generic.List<string[]>();
            foreach (string[] pair in Banter)
                if ((pair[0] == captain || pair[2] == captain) && TaleCaptains.Freed(pair[0]) && TaleCaptains.Freed(pair[2])) mine.Add(pair);
            if (mine.Count == 0) return false;
            string[] chosen = mine[Random.Range(0, mine.Count)];
            ValhallaSpeaker first = CaptainSpeaker(chosen[0]), second = CaptainSpeaker(chosen[2]);
            if (first == null || second == null) return false;
            first.Say(chosen[1], 6f);
            second.StartCoroutine(SayLater(second, chosen[3], 3.5f));
            return true;
        }

        static ValhallaSpeaker CaptainSpeaker(string id)
        {
            foreach (ValhallaSpeaker s in all)
                if (s != null && s.who == Captain && s.captain == id) return s;
            return null;
        }

        static System.Collections.IEnumerator SayLater(ValhallaSpeaker who, string line, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (who != null) who.Say(line, 7f);
        }

        // Bragi greets whoever first comes near the throne (once a visit), if they haven't been welcomed yet.
        void Update()
        {
            if (who == HighSeatGuard) { Watch(); return; }
            if (who == RingMaster) { CallOver(); return; }
            if (who != Bragi || greeted || Time.time < nextGreetLook) return;
            nextGreetLook = Time.time + 0.5f;
            Player player = Player.m_localPlayer;
            if (player == null || Welcomed) return;
            if (Vector3.Distance(player.transform.position, transform.position) > 8f) return;
            greeted = true;
            Say("$iov_bragi_greet");
        }

        // No wall keeps you from the Allfather's seat; his guard does. Climb close to it and the nearest of them
        // tells you he's not to be disturbed (now and then, not on every step).
        void Watch()
        {
            if (Time.time < nextGreetLook) return;
            nextGreetLook = Time.time + 0.5f;
            Player player = Player.m_localPlayer;
            if (player == null || Time.time - warnedAt < 25f || highSeat == Vector3.zero) return;
            if (Vector3.Distance(player.transform.position, highSeat) > 5.5f) return;
            ValhallaSpeaker nearest = null;
            float best = float.MaxValue;
            foreach (ValhallaSpeaker s in all)
            {
                if (s == null || s.who != HighSeatGuard) continue;
                float d = Vector3.Distance(s.transform.position, player.transform.position);
                if (d < best) { best = d; nearest = s; }
            }
            if (nearest != this) return;
            warnedAt = Time.time;
            Say("$iov_guard_odin");
        }

        // Sent by Sævar and come to the ring, not yet tested: the ring's master calls you over to him (as you
        // come near, and again now and then while you're about).
        float calledAt = -100f;

        void CallOver()
        {
            if (Time.time < nextGreetLook) return;
            nextGreetLook = Time.time + 0.5f;
            Player player = Player.m_localPlayer;
            if (player == null || !Welcomed || ValhallaDuel.Active || Story.Key(ValhallaDuel.WonKey) || Time.time - calledAt < 45f) return;
            if (Vector3.Distance(player.transform.position, transform.position) > 18f) return;
            calledAt = Time.time;
            Say("$iov_ringmaster_call");
        }

        // A line from whichever of them is the one (the ring's master, as the bouts end).
        internal static void Shout(int one, string line)
        {
            foreach (ValhallaSpeaker s in all)
                if (s != null && s.who == one) { s.Say(line); return; }
        }

        // Everyone who speaks, kept as they come and go (looking them up across the whole scene, as the
        // high seat's guards did all at once when you came close, made the game catch for a moment).
        static readonly System.Collections.Generic.List<ValhallaSpeaker> all = new System.Collections.Generic.List<ValhallaSpeaker>();

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() { all.Remove(this); }

        internal void Say(string line, float seconds = 9f)
        {
            if (Chat.instance == null) return;
            Chat.instance.SetNpcText(gameObject, Vector3.up * height, 35f, seconds, "", line, false); // (the ring's master is heard across his ring)
        }
    }

    // A rune stone of Valhalla's memorial path that can be read, as the game's own rune stones are.
    // (Its field is public so the location's copy keeps it.)
    class ValhallaRune : MonoBehaviour, Hoverable, Interactable
    {
        public string text;

        public string GetHoverText() { return Localization.instance.Localize("$iov_runestone\n[<color=yellow><b>$KEY_Use</b></color>] $piece_rune_read"); }
        public string GetHoverName() { return Localization.instance.Localize("$iov_runestone"); }
        public float GetHoverOffset() { return 0f; }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || user != Player.m_localPlayer || TextViewer.instance == null || string.IsNullOrEmpty(text)) return false;
            TextViewer.instance.ShowText(TextViewer.Style.Rune, "$iov_runestone", text, true);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
}
