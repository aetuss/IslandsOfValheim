using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using HarmonyLib;

namespace IslandsOfValheim
{
    // Every text a player sees comes from the translation files in Translations/<Language>/IslandsOfValheim.json
    // next to the mod (Jötunn loads them; a missing key falls back to English). Texts with something filled
    // in use one pattern key with {0}, {1}... so each language can put the parts where its grammar wants them.
    //
    // Players can change any text, or add a language, without an update wiping it: files in
    // BepInEx/config/IslandsOfValheim/Translations/<Language>/*.json win over the mod's own.
    static class Texts
    {
        internal static string OverrideFolder
        {
            get { return Path.Combine(Path.Combine(Paths.ConfigPath, Plugin.ModName), "Translations"); }
        }

        // The translated text of one of this mod's keys, with {0}, {1}... replaced by args (each translated too,
        // so an arg can be a $token such as an item name).
        internal static string Get(string key, params string[] args)
        {
            Localization localization = Localization.instance;
            string text = localization.Localize("$" + key);
            for (int i = 0; i < args.Length; i++)
                text = text.Replace("{" + i + "}", localization.Localize(args[i]));
            return text;
        }

        // "10 Wood": an amount of an item.
        internal static string Amount(int amount, string itemToken)
        {
            return Get("iov_amount", amount.ToString(), itemToken);
        }

        static string appliedLanguage;
        static readonly System.Reflection.MethodInfo addWord = AccessTools.Method(typeof(Localization), "AddWord");

        // After the game and Jötunn have set up a language, put the player's own texts for it on top.
        static void ApplyOverrides(Localization localization, string language)
        {
            if (localization == null || string.IsNullOrEmpty(language)) return;
            string folder = Path.Combine(OverrideFolder, language);
            if (!Directory.Exists(folder)) return;
            int count = 0;
            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                try
                {
                    foreach (KeyValuePair<string, string> pair in ParseFlatJson(File.ReadAllText(file, Encoding.UTF8)))
                    {
                        addWord.Invoke(localization, new object[] { pair.Key.TrimStart('$'), pair.Value });
                        count++;
                    }
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning("Could not read the translation file " + file + ": " + e.Message);
                }
            }
            if (appliedLanguage != language) Plugin.Log.LogInfo("Applied " + count + " translated texts from " + folder);
            appliedLanguage = language;
        }

        [HarmonyPatch(typeof(Localization), "SetupLanguage")]
        static class SetupLanguagePatch
        {
            [HarmonyPriority(Priority.Last)]
            static void Postfix(Localization __instance, string language)
            {
                ApplyOverrides(__instance, language);
            }
        }

        // Jötunn adds the mods' translations here too, so the overrides go on again after it.
        [HarmonyPatch(typeof(FejdStartup), "SetupGui")]
        static class SetupGuiPatch
        {
            [HarmonyPriority(Priority.Last)]
            static void Postfix()
            {
                if (Localization.instance != null) ApplyOverrides(Localization.instance, Localization.instance.GetSelectedLanguage());
            }
        }

        // A JSON object of strings, {"key": "text", ...}: all a translation file holds.
        internal static Dictionary<string, string> ParseFlatJson(string json)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            int i = 0;
            Skip(json, ref i);
            Expect(json, ref i, '{');
            Skip(json, ref i);
            if (i < json.Length && json[i] == '}') return result;
            while (true)
            {
                Skip(json, ref i);
                string key = ReadString(json, ref i);
                Skip(json, ref i);
                Expect(json, ref i, ':');
                Skip(json, ref i);
                result[key] = ReadString(json, ref i);
                Skip(json, ref i);
                if (i < json.Length && json[i] == ',') { i++; continue; }
                Expect(json, ref i, '}');
                return result;
            }
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '﻿')) i++;
        }

        static void Expect(string s, ref int i, char c)
        {
            if (i >= s.Length || s[i] != c) throw new System.FormatException("expected '" + c + "' at position " + i);
            i++;
        }

        static string ReadString(string s, ref int i)
        {
            Expect(s, ref i, '"');
            StringBuilder sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'u': sb.Append((char)System.Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            Expect(s, ref i, '"');
            return sb.ToString();
        }
    }
}
