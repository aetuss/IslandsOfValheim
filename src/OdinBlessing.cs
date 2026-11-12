using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // The Valhalla tale's last rewards (the user's, 2026-11-12):
    // - the Allfather's blessing: Odin, in his hall once the tale is done, grants it when you speak with him a
    //   second time (the first, his words of what you've done): every skill rises a tenth quicker, and death
    //   takes a quarter less of them. Each player's own, kept with them; written in their compendium (no icon);
    //   given in the light of the game's own forsaken powers;
    // - the Brim hearth: rested in your house in the Brim village, the rest lasts half as long again;
    // - the Brim Folk's friend: Sævar's goods (his chest) a fifth cheaper.
    // The last two once the Brim Folk are home (ValhallaPeople.DoneKey), for everyone.
    static class OdinBlessing
    {
        internal const string Key = "iovodinblessed"; // (the player's own: blessed)
        const float Learning = 1.1f, Loss = 0.75f, HearthRest = 1.5f, Price = 0.8f;

        internal static bool Has(Player player) { return player != null && player.HaveSeenTutorial(Key); }

        static bool Home { get { return Story.Key(ValhallaPeople.DoneKey); } }

        // His words, the light about you, and the page.
        internal static void Give(Player player, ValhallaSpeaker odin)
        {
            player.SetSeenTutorial(Key);
            odin.Say("$iov_odin_bless", 12f);
            player.AddKnownText("$iov_blessing_page", "$iov_blessing_page_text");
            player.Message(MessageHud.MessageType.Center, "$iov_blessing_given");
            if (ObjectDB.instance != null)
                foreach (string power in new[] { "GP_Eikthyr", "GP_TheElder", "GP_Bonemass", "GP_Moder" })
                {
                    StatusEffect gp = ObjectDB.instance.GetStatusEffect(power.GetStableHashCode());
                    if (gp == null || gp.m_startEffects == null || !gp.m_startEffects.HasEffects()) continue;
                    gp.m_startEffects.Create(player.transform.position, player.transform.rotation, player.transform);
                    break;
                }
            Plugin.Debug("Odin's blessing given");
        }

        static readonly System.Reflection.FieldInfo skillsPlayer = AccessTools.Field(typeof(Skills), "m_player");

        static bool Blessed(Skills skills) { return skillsPlayer != null && Has(skillsPlayer.GetValue(skills) as Player); }

        [HarmonyPatch(typeof(Skills), "RaiseSkill")]
        static class Learn
        {
            static void Prefix(Skills __instance, ref float factor) { if (factor > 0f && Blessed(__instance)) factor *= Learning; }
        }

        [HarmonyPatch(typeof(Skills), "LowerAllSkills")]
        static class Keep
        {
            static void Prefix(Skills __instance, ref float factor) { if (factor > 0f && Blessed(__instance)) factor *= Loss; }
        }

        // ---- the Brim hearth ----

        static readonly System.Reflection.FieldInfo seTime = AccessTools.Field(typeof(StatusEffect), "m_time");

        [HarmonyPatch(typeof(SE_Rested), "UpdateTTL")]
        static class Hearth
        {
            static void Postfix(SE_Rested __instance)
            {
                Player player = __instance.m_character as Player;
                if (player == null || !Home || seTime == null || !SpiritsIslet.InBrimHome(player.transform.position)) return;
                float want = (__instance.m_baseTTL + (player.GetComfortLevel() - 1) * __instance.m_TTLPerComfortLevel) * HearthRest;
                float left = __instance.m_ttl - (float)seTime.GetValue(__instance);
                if (want <= left) return;
                __instance.m_ttl = want;
                seTime.SetValue(__instance, 0f);
            }
        }

        // ---- the Brim Folk's friend ----

        [HarmonyPatch(typeof(Trader), "GetAvailableItems")]
        static class Friend
        {
            static void Postfix(Trader __instance, ref System.Collections.Generic.List<Trader.TradeItem> __result)
            {
                if (__result == null || !SeaTrader.IsOurs(__instance) || !Home) return;
                System.Collections.Generic.List<Trader.TradeItem> cheaper = new System.Collections.Generic.List<Trader.TradeItem>();
                foreach (Trader.TradeItem item in __result)
                {
                    if (item == null) continue;
                    // (the same copy each time: the store finds the one you'd picked among them by itself)
                    Trader.TradeItem copy;
                    if (!copies.TryGetValue(item, out copy))
                    {
                        copy = new Trader.TradeItem { m_prefab = item.m_prefab, m_stack = item.m_stack, m_requiredGlobalKey = item.m_requiredGlobalKey };
                        copies[item] = copy;
                    }
                    copy.m_price = Mathf.Max(1, Mathf.RoundToInt(item.m_price * Price));
                    cheaper.Add(copy);
                }
                __result = cheaper;
            }

            static readonly System.Collections.Generic.Dictionary<Trader.TradeItem, Trader.TradeItem> copies = new System.Collections.Generic.Dictionary<Trader.TradeItem, Trader.TradeItem>();
        }
    }
}
