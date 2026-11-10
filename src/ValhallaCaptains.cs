using UnityEngine;

namespace IslandsOfValheim
{
    // The Valhalla arc's second task (T2): the captains' regrets. Once the einherjar have taken you in (T1),
    // the four captains of the tale sit at the Brim Folk's table beside Sævar, pale ghosts held back by what
    // each left undone on the islet where they fell. Do it, come back and tell them, and each one is freed: no
    // longer a ghost, a man (or woman) at the table.
    //
    // - Ketill (the Swamp islet): he never raised the stone for Hrafn. Raise it.
    // - Sigrún (the Plains islet): in her madness she threw her sister's necklace into the sea. Fish it up.
    // - Ormr (the Mistlands islet of his crew): his last diary stone holds what he never told his son. Read it,
    //   and carry the words to Grímr in Valhalla: that frees Ormr.
    // - Grímr (the Deep North arena): the one thing his mother ever made him, a small carved toy, left there.
    //
    // Everything is the world's (world keys): found (the deed done on the islet) and freed (told in Valhalla).
    static class TaleCaptains
    {
        internal const string Ketill = "ketill", Sigrun = "sigrun", Ormr = "ormr", Grimr = "grimr";
        internal static readonly string[] All = { Ketill, Sigrun, Ormr, Grimr };

        // The world's keys (lower case, as the game keeps them; none starts with iov_, which the build reads as
        // a text's key).
        internal static string FoundKey(string id)
        {
            switch (id)
            {
                case Ketill: return "iovt2ketillfound";
                case Sigrun: return "iovt2sigrunfound";
                case Ormr: return "iovt2ormrfound";
                default: return "iovt2grimrfound";
            }
        }

        internal static string FreedKey(string id)
        {
            switch (id)
            {
                case Ketill: return "iovt2ketillfreed";
                case Sigrun: return "iovt2sigrunfreed";
                case Ormr: return "iovt2ormrfreed";
                default: return "iovt2grimrfreed";
            }
        }

        internal static bool Found(string id) { return Story.Key(FoundKey(id)); }
        internal static bool Freed(string id) { return Story.Key(FreedKey(id)); }

        internal static bool AllFreed
        {
            get { foreach (string id in All) if (!Freed(id)) return false; return true; }
        }

        // Has the task begun (the duels won: the captains are at the table)?
        internal static bool Begun { get { return Story.Key(ValhallaDuel.WonKey); } }

        // What each says (each its own text, so the build can check they're all there).
        internal class Lines { internal string Name, Wish, Page, PageText, Remind, Free, Thanks; }

        internal static Lines Of(string id)
        {
            switch (id)
            {
                case Ketill: return new Lines { Name = "$iov_ketill", Wish = "$iov_t2_ketill_wish", Page = "$iov_t2_ketill_page", PageText = "$iov_t2_ketill_page_text", Remind = "$iov_t2_ketill_remind", Free = "$iov_t2_ketill_free", Thanks = "$iov_t2_ketill_thanks" };
                case Sigrun: return new Lines { Name = "$iov_sigrun", Wish = "$iov_t2_sigrun_wish", Page = "$iov_t2_sigrun_page", PageText = "$iov_t2_sigrun_page_text", Remind = "$iov_t2_sigrun_remind", Free = "$iov_t2_sigrun_free", Thanks = "$iov_t2_sigrun_thanks" };
                case Ormr: return new Lines { Name = "$iov_ormr", Wish = "$iov_t2_ormr_wish", Page = "$iov_t2_ormr_page", PageText = "$iov_t2_ormr_page_text", Remind = "$iov_t2_ormr_remind", Free = "$iov_t2_ormr_free", Thanks = "$iov_t2_ormr_thanks" };
                default: return new Lines { Name = "$iov_grimr", Wish = "$iov_t2_grimr_wish", Page = "$iov_t2_grimr_page", PageText = "$iov_t2_grimr_page_text", Remind = "$iov_t2_grimr_remind", Free = "$iov_t2_grimr_free", Thanks = "$iov_t2_grimr_thanks" };
            }
        }

        // Talking to a captain at the table: his wish (and its page) the first time, after that a reminder; once
        // it's done, he's freed; freed, his thanks. Ormr, his words found, sends you to his son; Grímr, hearing
        // them, frees his father (before his own wish). Gives back the line to say, and whom to free (or null).
        internal static string Talk(Player player, string id, out string free)
        {
            free = null;
            Lines lines = Of(id);
            if (id == Grimr && Found(Ormr) && !Freed(Ormr))
            {
                free = Ormr;
                return "$iov_t2_grimr_hears";
            }
            if (Freed(id)) return lines.Thanks;
            if (Found(id))
            {
                if (id == Ormr) return "$iov_t2_ormr_tellhim";
                free = id;
                return lines.Free;
            }
            string told = "iovt2told" + id; // (this player's: heard his wish)
            if (!player.HaveSeenTutorial(told))
            {
                player.SetSeenTutorial(told);
                Story.Tell(player, lines.Page, lines.PageText);
                return lines.Wish;
            }
            player.AddKnownText(lines.Page, lines.PageText); // (a page lost is found again)
            return lines.Remind;
        }

        // Freed: the world's key, and (if they're in sight) the one at the table turns from a ghost into a man.
        // (line: what the freed one is saying as it happens, said on once he's whole)
        internal static void Free(string id, string line = "")
        {
            Story.Set(FreedKey(id));
            if (ValhallaPeople.Current != null) ValhallaPeople.Current.FreeCaptain(id, line);
            Plugin.Debug("T2: " + id + " freed" + (AllFreed ? "; all four" : ""));
        }

        // (development) iov_t2: found/free one or all, or reset them all
        internal static void Dev(string what, string id, Terminal context)
        {
            foreach (string one in All)
            {
                if (id != "all" && id != one) continue;
                if (what == "found") Story.Set(FoundKey(one));
                else if (what == "free") { Story.Set(FoundKey(one)); Free(one); }
                else if (what == "reset" && ZoneSystem.instance != null)
                {
                    ZoneSystem.instance.RemoveGlobalKey(FoundKey(one));
                    ZoneSystem.instance.RemoveGlobalKey(FreedKey(one));
                }
            }
            if (what == "reset" && ValhallaPeople.Current != null) ValhallaPeople.Current.Remake();
            context.AddString("T2 " + what + " " + id + ": " + string.Join(", ", System.Array.ConvertAll(All, one => one + (Freed(one) ? " freed" : Found(one) ? " found" : " -"))));
        }
    }
}
