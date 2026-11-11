using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // The music of the fight with Hafgufa (the user asked for one or two pieces that fit): the game's own, played
    // the way it plays a raid's music (above a place's, the sea's and the weather's): while the net holds, the
    // Seeker Queen's deep, uneasy piece; from the second stage, when the storm comes, a harder one. Each the first
    // of its list the game has. When the fight is over (or you've sailed off) the game's own music comes back as
    // after a raid. Each player's own.
    static class RanMusic
    {
        internal static readonly string[] Beast = { "boss_queen", "boss_bonemass", "boss_eikthyr" };
        internal static readonly string[] Storm = { "boss_fader", "boss_moder", "boss_goblinking" };

        static string[] wanted;
        static string playing;

        internal static void Want(string[] list)
        {
            if (list == wanted) return;
            wanted = list;
            playing = list != null ? First(list) : null;
            Plugin.Debug("T3: music " + (playing ?? "the game's own"));
        }

        static string First(string[] list)
        {
            MusicMan music = MusicMan.instance;
            if (music == null || music.m_music == null) return list[0];
            foreach (string name in list)
                foreach (MusicMan.NamedMusic m in music.m_music)
                    if (m != null && m.m_name == name && m.m_clips != null && m.m_clips.Length > 0) return name;
            Plugin.Debug("T3: none of " + string.Join(", ", list) + " is in the game");
            return null;
        }

        // (a raid's music, as the game asks for it; ours while a raid isn't on)
        [HarmonyPatch(typeof(RandEventSystem), "GetMusicOverride")]
        static class OverridePatch
        {
            static void Postfix(ref string __result)
            {
                if (string.IsNullOrEmpty(__result) && playing != null) __result = playing;
            }
        }
    }
}
