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
        internal const int Bragi = 0, Saevar = 1, Odin = 2, Guard = 3, HighSeatGuard = 4, RingMaster = 5;

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

        string Name { get { return who == Bragi ? "$iov_bragi" : who == Saevar ? SeaTrader.NameToken : who == Odin ? "$iov_odin" : who == RingMaster ? "$iov_ringmaster" : "$iov_guard"; } }

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
                Say(Welcomed ? BragiTalk[Random.Range(0, BragiTalk.Length)] : "$iov_bragi_greet");
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
            Say("$iov_valsaevar_ring");
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }

        // Bragi greets whoever first comes near the throne (once a visit), if they haven't been welcomed yet.
        void Update()
        {
            if (who == HighSeatGuard) { Watch(); return; }
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
            foreach (ValhallaSpeaker s in FindObjectsOfType<ValhallaSpeaker>())
            {
                if (s.who != HighSeatGuard) continue;
                float d = Vector3.Distance(s.transform.position, player.transform.position);
                if (d < best) { best = d; nearest = s; }
            }
            if (nearest != this) return;
            warnedAt = Time.time;
            Say("$iov_guard_odin");
        }

        void Say(string line)
        {
            if (Chat.instance == null) return;
            Chat.instance.SetNpcText(gameObject, Vector3.up * height, 20f, 9f, "", line, false);
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
