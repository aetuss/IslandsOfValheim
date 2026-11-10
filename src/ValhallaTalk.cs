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
        internal const int Bragi = 0, Saevar = 1, Odin = 2, Guard = 3, HighSeatGuard = 4, RingMaster = 5, Captain = 6;
        internal string captain = ""; // which of the tale's captains (TaleCaptains), for Captain

        // Hliðskjálf's seat (world), and when its guards last spoke up about it
        internal static Vector3 highSeat;
        static float warnedAt = -100f;
        static readonly string[] GuardTalk = { "$iov_guard_talk1", "$iov_guard_talk2" };

        internal int who;
        internal float height = 2.2f; // over its feet (lower for one sitting)
        bool greeted;
        float nextGreetLook;

        static bool Welcomed { get { return Story.Key(WelcomedKey); } }

        static readonly string[] BragiTalk = { "$iov_bragi_talk1", "$iov_bragi_talk2", "$iov_bragi_talk3" };

        string Name
        {
            get
            {
                if (who == Captain) return TaleCaptains.Of(captain).Name;
                return who == Bragi ? "$iov_bragi" : who == Saevar ? SeaTrader.NameToken : who == Odin ? "$iov_odin" : who == RingMaster ? "$iov_ringmaster" : "$iov_guard";
            }
        }

        public string GetHoverText()
        {
            if (who == Odin) return Localization.instance.Localize(Name);
            if (who == RingMaster)
                return Localization.instance.Localize(Name + (ValhallaDuel.Active ? "" : "\n[<color=yellow><b>$KEY_Use</b></color>] $iov_duel_challenge"));
            return Localization.instance.Localize(Name + "\n[<color=yellow><b>$KEY_Use</b></color>] $raven_interact");
        }

        public string GetHoverName() { return Localization.instance.Localize(Name); }

        public float GetHoverOffset() { return 0f; }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player player = user as Player;
            if (hold || player == null || player != Player.m_localPlayer || who == Odin) return false;
            if (who == Captain)
            {
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
                Say(Story.Key(ValhallaDuel.WonKey) ? "$iov_ringmaster_again" : "$iov_ringmaster_start");
                ValhallaDuel.Current.Begin();
                return true;
            }
            if (who == Guard || who == HighSeatGuard)
            {
                Say(GuardTalk[Random.Range(0, GuardTalk.Length)]);
                return true;
            }
            if (who == Bragi)
            {
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

        internal void Say(string line)
        {
            if (Chat.instance == null) return;
            Chat.instance.SetNpcText(gameObject, Vector3.up * height, 35f, 9f, "", line, false); // (the ring's master is heard across his ring)
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
