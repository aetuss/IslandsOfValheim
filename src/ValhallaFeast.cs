using HarmonyLib;
using Jotunn.Entities;
using UnityEngine;

namespace IslandsOfValheim
{
    // Valhalla's feast: Andhrímnir, the einherjar's cook, at Eldhrímnir west of the hall's door, gives each
    // player a plate of Sæhrímnir (the boar cooked every day, whole again every night) once a game day: a food
    // of its own, good for how far the world has come (the strongest boss beaten), never as good as the best
    // food the player could cook for it, as it's free.
    static class ValhallaFeast
    {
        internal const string Meat = "IoV_SaehrimnirMeat";
        const string DayKey = "IoV_FeastDay"; // (the player's own: the game day of their last plate)
        static ItemDrop.ItemData.SharedData shared;

        internal static void Register()
        {
            CustomItem item = SeafarerGear.Food(Meat, new[] { "CookedMeat" }, "iov_saehrimnir", 35f, 15f, 1800f, 2f, Recolor.ToHue(0.09f, 0.6f, 1.2f));
            if (item == null) return;
            shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_maxStackSize = 5;
        }

        // By the strongest boss this world has beaten (0 none ... 7 Fader): health 35 to 88, stamina 15 to 29,
        // healing 2 to 5 a tick, for half an hour.
        internal static void Fit()
        {
            if (shared == null) return;
            int tier = ValhallaDuel.Tier();
            shared.m_food = 35f + tier * 7.5f;
            shared.m_foodStamina = 15f + tier * 2f;
            shared.m_foodRegen = 2f + tier / 2;
            shared.m_foodBurnTime = 1800f;
        }

        internal static void Serve(Player player, ValhallaSpeaker cook)
        {
            if (ValhallaGifts.UsedToday(player, DayKey)) { cook.Say("$iov_cook_later"); return; }
            Fit();
            ValhallaGifts.Use(player, DayKey);
            Story.Give(player, Meat);
            cook.Say("$iov_cook_give");
            Plugin.Debug("Valhalla: a plate of Sæhrímnir (tier " + ValhallaDuel.Tier() + ": " + shared.m_food + " health, " + shared.m_foodStamina + " stamina)");
        }

        // As the world stands now: when you come into it, and before a plate is eaten.
        [HarmonyPatch(typeof(Player), "OnSpawned")]
        static class FitOnSpawn
        {
            static void Postfix() { Fit(); }
        }

        [HarmonyPatch(typeof(Player), "CanEat")]
        static class FitBeforeEating
        {
            static void Prefix(ItemDrop.ItemData item)
            {
                if (item != null && item.m_shared == shared) Fit();
            }
        }
    }
}
