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
            shared.m_food = Mathf.Round(35f + tier * 7.5f);
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

    // Sæhrímnir on its spit (IsletBuilder.RoastingBoar): once Valhalla stands, what's drawn of the boar is
    // measured as the game draws it (its bones' doing) and moved so its middle is on the spit's bar. Logged.
    // (2.0.3's was drawn 50 m below the spit, so it wasn't seen. 2.0.4-2.0.6 also switched on what of it was
    // off, 4 of its 6 parts: likely the game's other looks of a boar, drawn over each other, dark. Now only
    // what the game itself shows.) (Its field public: the location's copy keeps it.)
    class RoastOnSpit : MonoBehaviour
    {
        public Vector3 bar; // the spit's bar, in this holder's space

        void Start()
        {
            Transform boar = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (boar == null) return;
            bool any = false;
            Bounds drawn = new Bounds();
            int shown = 0;
            foreach (Renderer r in boar.GetComponentsInChildren<Renderer>(false))
            {
                if (r is ParticleSystemRenderer || !r.enabled) continue;
                shown++;
                if (!any) { drawn = r.bounds; any = true; }
                else drawn.Encapsulate(r.bounds);
            }
            if (!any) { Plugin.Debug("Valhalla: Sæhrímnir has nothing to draw"); return; }
            Vector3 want = transform.TransformPoint(bar);
            Plugin.Debug("Valhalla: Sæhrímnir drawn at " + drawn.center.ToString("F2") + ", " + drawn.size.ToString("F2") + " m (" + shown + " parts); the bar at " + want.ToString("F2"));
            boar.position += want - drawn.center;
        }
    }
}
