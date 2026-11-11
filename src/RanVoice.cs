using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // The lines in the middle of the screen during Rán's net (T3), one after the other: none comes over another
    // that's still being read (the user's wish), each held as long as it's given. The game shows a middle line
    // for about 4 s (it fades it out over 4 s from the moment it comes); a longer one is held fully there and
    // faded out over its last 2 s. Other players' lines go to them as the game sends them.
    static class RanVoice
    {
        struct Line { internal string Text; internal float Seconds; }

        static readonly Queue<Line> waiting = new Queue<Line>();
        static bool speaking;

        internal static void Say(Player player, string text, float seconds)
        {
            if (player == null) return;
            if (player != Player.m_localPlayer) { player.Message(MessageHud.MessageType.Center, text, 0, null, false); return; }
            waiting.Enqueue(new Line { Text = text, Seconds = seconds });
            if (!speaking && Plugin.Instance != null) Plugin.Instance.StartCoroutine(Speak());
        }

        // Only if nothing is being said (a reminder that comes again and again).
        internal static void SayIfQuiet(Player player, string text, float seconds)
        {
            if (speaking || waiting.Count > 0) return;
            Say(player, text, seconds);
        }

        static IEnumerator Speak()
        {
            speaking = true;
            while (waiting.Count > 0)
            {
                Line line = waiting.Dequeue();
                Player player = Player.m_localPlayer;
                if (player != null) Show(player, line.Text, line.Seconds);
                yield return new WaitForSeconds(line.Seconds + 0.3f);
            }
            speaking = false;
        }

        static readonly System.Reflection.FieldInfo fades = AccessTools.Field(typeof(MessageHud), "_crossFadeTextBuffer");
        static readonly System.Reflection.FieldInfo centreText = AccessTools.Field(typeof(MessageHud), "m_messageCenterText");

        static void Show(Player player, string text, float seconds)
        {
            player.Message(MessageHud.MessageType.Center, text, 0, null, false);
            if (seconds <= 4.5f || MessageHud.instance == null || fades == null) return;
            // the fade-out the game just queued (its last): taken away, so the line stays; faded later, ourselves
            IList queued = fades.GetValue(MessageHud.instance) as IList;
            if (queued == null || queued.Count == 0) return;
            queued.RemoveAt(queued.Count - 1);
            string shown = Localization.instance != null ? Localization.instance.Localize(text) : text;
            Plugin.Instance.StartCoroutine(FadeLater(shown, seconds - 2f));
        }

        static IEnumerator FadeLater(string shown, float after)
        {
            yield return new WaitForSeconds(after);
            if (MessageHud.instance == null || centreText == null) yield break;
            Component label = centreText.GetValue(MessageHud.instance) as Component;
            if (label == null) yield break;
            System.Reflection.PropertyInfo textOf = label.GetType().GetProperty("text");
            string now = textOf != null ? textOf.GetValue(label, null) as string : null;
            if (now != shown) yield break; // (another line is there now: it fades as the game fades it)
            System.Reflection.MethodInfo fade = label.GetType().GetMethod("CrossFadeAlpha", new[] { typeof(float), typeof(float), typeof(bool) });
            if (fade != null) fade.Invoke(label, new object[] { 0f, 2f, false });
        }
    }
}
