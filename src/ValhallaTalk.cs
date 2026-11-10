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
        internal const int Bragi = 0, Saevar = 1, Odin = 2;

        internal int who;
        internal float height = 2.2f; // over its feet (lower for one sitting)
        bool greeted;
        float nextGreetLook;

        static bool Welcomed { get { return Story.Key(WelcomedKey); } }

        static readonly string[] BragiTalk = { "$iov_bragi_talk1", "$iov_bragi_talk2", "$iov_bragi_talk3" };

        string Name { get { return who == Bragi ? "$iov_bragi" : who == Saevar ? SeaTrader.NameToken : "$iov_odin"; } }

        public string GetHoverText()
        {
            if (who == Odin) return Localization.instance.Localize(Name);
            return Localization.instance.Localize(Name + "\n[<color=yellow><b>$KEY_Use</b></color>] $raven_interact");
        }

        public string GetHoverName() { return Localization.instance.Localize(Name); }

        public float GetHoverOffset() { return 0f; }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player player = user as Player;
            if (hold || player == null || player != Player.m_localPlayer || who == Odin) return false;
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
            if (who != Bragi || greeted || Time.time < nextGreetLook) return;
            nextGreetLook = Time.time + 0.5f;
            Player player = Player.m_localPlayer;
            if (player == null || Welcomed) return;
            if (Vector3.Distance(player.transform.position, transform.position) > 8f) return;
            greeted = true;
            Say("$iov_bragi_greet");
        }

        void Say(string line)
        {
            if (Chat.instance == null) return;
            Chat.instance.SetNpcText(gameObject, Vector3.up * height, 20f, 9f, "", line, false);
        }
    }
}
