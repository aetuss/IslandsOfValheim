using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // What the sea trader sells, besides the sea vegvisir shard: the Seafarer set (a travel set, not a
    // combat one: each piece helps at sea, the full set more), meads and long-lasting voyage food, a lure
    // for the islet fishing grounds and a kit to patch up a ship at sea. All are vanilla items cloned and
    // recoloured, with stats of their own; none can be crafted, only bought.
    static class SeafarerGear
    {
        internal const string Lure = "IoV_AncientLure";
        internal const string Cape = "IoV_SeafarerCape";
        internal const string Hat = "IoV_CaptainHat";
        internal const string Vest = "IoV_SeafarerVest";
        internal const string Trousers = "IoV_DeckhandTrousers";
        internal const string Caulk = "IoV_CaulkingKit";
        internal const string StormMead = "IoV_StormMead";
        internal const string RansBlessing = "IoV_RansBlessing";
        internal const string SaltedFish = "IoV_SaltedFish";
        internal const string Hardtack = "IoV_Hardtack";

        const string SetName = "IoV_Seafarer";
        const string CaulkToken = "$iov_caulk";
        const float LureBite = 0.85f;      // the chance a ground's fish takes the lure each time it tests it
        const float LureStarChance = 0.6f; // the chance a fish that took it is one star better

        // Status effects, looked up by name hash while the game runs.
        static int hatEffect, vestEffect, trousersEffect, capeEffect, setEffect, stormEffect, ranEffect;
        static float wetTime = -1f;

        internal static readonly List<GameObject> Items = new List<GameObject>();

        internal static void Register()
        {
            SE_Stats hat = Effect("IoV_SE_CaptainHat", "iov_se_hat", true);
            if (hat != null) { hat.m_skillLevel = Skills.SkillType.Fishing; hat.m_skillLevelModifier = 10f; }
            SE_Stats vest = Effect("IoV_SE_SeafarerVest", "iov_se_vest", true);
            if (vest != null) vest.m_swimStaminaUseModifier = -0.25f;
            SE_Stats trousers = Effect("IoV_SE_DeckhandTrousers", "iov_se_trousers", true); // Wet wears off faster: see WeatherPatch
            SE_Stats cape = Effect("IoV_SE_SeafarerCape", "iov_se_cape", true);          // no Cold: see WeatherPatch
            SE_Stats set = Effect("IoV_SE_ChildOfTheSea", "iov_se_set");           // never Wet: see WeatherPatch
            if (set != null) set.m_swimSpeedModifier = 0.25f;

            // Each piece as its text says: a captain's broad hat with a gull's feather, a vest of navy and
            // sailcloth, trousers of tarred canvas, a cape of oiled wool.
            // (The gull's feather on the hat was tried and dropped: the game's feathers are a bundle, and
            // on a hat they read as a dark blot. A captain's gold band instead.)
            Armor(Hat, new[] { "HelmetFishingHat", "HelmetHat4", "HelmetHat10", "HelmetLeather" }, "iov_hat", 6f, 1f, hat, NavyAndGold);
            Armor(Vest, new[] { "ArmorLeatherChest" }, "iov_vest", 10f, 5f, vest, NavyAndSailcloth);
            Armor(Trousers, new[] { "ArmorLeatherLegs" }, "iov_trousers", 10f, 5f, trousers, Tarred);
            // The deer hide cape takes our colour; the linen cape's red isn't in the textures we can change.
            Armor(Cape, new[] { "CapeDeerHide", "CapeLinen" }, "iov_cape", 1f, 1f, cape, OiledWool);
            Gloss(Trousers, 0.65f); // tar shines
            Gloss(Cape, 0.4f);      // so does oiled wool, a little
            foreach (GameObject piece in Items)
            {
                ItemDrop.ItemData.SharedData shared = piece.GetComponent<ItemDrop>().m_itemData.m_shared;
                shared.m_setName = SetName;
                shared.m_setSize = 4;
                shared.m_setStatusEffect = set;
            }
            if (set != null) set.m_icon = Icons.Waves;

            SE_Stats storm = Effect("IoV_SE_StormMead", "iov_se_storm");  // no Wet, no Cold: see WeatherPatch
            if (storm != null) storm.m_ttl = 600f;
            SE_Stats ran = Effect("IoV_SE_RansBlessing", "iov_se_ran");
            if (ran != null)
            {
                ran.m_ttl = 600f; ran.m_swimSpeedModifier = 0.4f; ran.m_swimStaminaUseModifier = -0.4f;
                // Drunk, the sea answers: a splash of water about you (a bait's splash, your size, and its sound).
                ran.m_startEffects = Effects("fx_float_hitwater", "sfx_land_water");
            }
            Mead(StormMead, new[] { "MeadFrostResist" }, "iov_stormmead", storm, Recolor.ToHue(0.6f, 0.45f, 0.75f));
            Mead(RansBlessing, new[] { "MeadStaminaLingering", "MeadTasty" }, "iov_ransblessing", ran, Recolor.ToHue(0.48f, 0.6f, 0.9f));

            // Mid-strength voyage food: weaker than a good cooked meal, but it lasts.
            Food(SaltedFish, new[] { "FishCooked" }, "iov_saltedfish", 25f, 50f, 2400f, 2f, Recolor.Pale(0.45f, 1.1f));
            Food(Hardtack, new[] { "Bread" }, "iov_hardtack", 35f, 35f, 3600f, 2f, Recolor.ToHue(0.1f, 0.28f, 1.2f)); // dry and pale, baked hard

            Simple(Lure, new[] { "FishingBaitOcean", "FishingBait" }, "iov_lure", 50, 0.1f, Recolor.ToHue(0.11f, 0.7f, 1.1f));
            CustomItem caulk = Simple(Caulk, new[] { "Tar" }, "iov_caulk", 10, 2f, Recolor.Lift(0.08f, 0.55f, 0.35f));
            if (caulk != null)
            {
                caulk.ItemDrop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
                // Tar, oakum and a mallet: the game's hammer, stained with tar (it still works as the kit).
                Reskin(caulk.ItemPrefab, caulk.ItemDrop.m_itemData.m_shared, "Hammer", BossKit.Cast(new Color(0.16f, 0.11f, 0.08f), 0.5f, 0.9f));
            }
            Glow(Lure, new Color(1f, 0.72f, 0.3f), 1.9f, 1.5f, 1f); // old amber, carved: it catches the light

            hatEffect = Hash(hat); vestEffect = Hash(vest); trousersEffect = Hash(trousers); capeEffect = Hash(cape);
            setEffect = Hash(set); stormEffect = Hash(storm); ranEffect = Hash(ran);
        }

        static int Hash(StatusEffect effect) { return effect != null ? effect.NameHash() : 0; }

        // The vest: its darker parts navy, its lighter ones the off-white of sailcloth.
        static Color NavyAndSailcloth(Color c)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            if (v < 0.06f) return c;
            Color result = v > 0.5f ? Color.HSVToRGB(0.11f, 0.12f, Mathf.Clamp01(v * 1.15f)) : Color.HSVToRGB(0.61f, 0.55f, Mathf.Clamp01(v * 0.85f));
            result.a = c.a;
            return result;
        }

        // Effects that play once where something happens (not following anyone).
        static EffectList Effects(params string[] names)
        {
            List<EffectList.EffectData> list = new List<EffectList.EffectData>();
            foreach (string n in names)
            {
                GameObject prefab = PrefabManager.Instance.GetPrefab(n);
                if (prefab == null) continue;
                EffectList.EffectData e = new EffectList.EffectData();
                e.m_prefab = prefab;
                e.m_enabled = true;
                list.Add(e);
            }
            EffectList effects = new EffectList();
            effects.m_effectPrefabs = list.ToArray();
            return effects;
        }

        // A gull's white: the shading kept, the colour all but gone.
        internal static Color GullWhite(Color c)
        {
            float light = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            Color result = Color.Lerp(new Color(0.93f, 0.93f, 0.9f), new Color(0.58f, 0.6f, 0.62f), 1f - Mathf.Clamp01(light * 1.6f));
            result.a = c.a;
            return result;
        }

        // A small light of an item's own where it lies (a glass that calms the sea, oil for a beacon, tar
        // from a burning sea), steady or slowly breathing.
        internal static void Glow(string name, Color color, float range, float intensity, float breathe)
        {
            Glow(name, color, range, intensity, breathe, 0.15f);
        }

        internal static void Glow(string name, Color color, float range, float intensity, float breathe, float height)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            if (prefab == null) return;
            GameObject glow = new GameObject("IoV_ItemGlow");
            glow.transform.SetParent(prefab.transform, false);
            glow.transform.localPosition = Vector3.up * height;
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            // The game draws only its few brightest lights near you in full; a small dim one like this was
            // dropped among the others (the oil's and the tar's never showed), so this one always is.
            light.renderMode = LightRenderMode.ForcePixel;
            ItemGlow pulse = glow.AddComponent<ItemGlow>();
            pulse.speed = breathe;
            pulse.strength = intensity;
            // And the thing itself gives off a little of that colour (seen even where the light isn't drawn).
            int lit = 0;
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer) continue;
                    foreach (Material m in renderer.sharedMaterials)
                    {
                        if (m == null || !m.HasProperty("_EmissionColor")) continue;
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", color * 0.35f);
                        lit++;
                    }
                }
            Plugin.Debug(name + ": a light of its own (" + range + " m), and " + lit + " of its materials glow");
        }

        // The hat: navy, its lighter parts (the band) a captain's dull gold.
        static Color NavyAndGold(Color c)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            if (v < 0.06f) return c;
            Color result = v > 0.55f ? Color.HSVToRGB(0.12f, 0.6f, Mathf.Clamp01(v * 0.9f)) : Color.HSVToRGB(0.62f, 0.6f, Mathf.Clamp01(v * 0.7f));
            result.a = c.a;
            return result;
        }

        // The trousers: tarred canvas, near black with a little brown in it, the weave still showing.
        static Color Tarred(Color c)
        {
            float light = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            Color result = new Color(0.075f, 0.062f, 0.05f) + new Color(0.18f, 0.15f, 0.12f) * light;
            result.a = c.a;
            return result;
        }

        // The cape: deep sea-green wool whatever colour the cloth was (the linen cape's is near white),
        // its weave and folds kept.
        static Color OiledWool(Color c)
        {
            float light = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            Color result = Color.Lerp(c, new Color(0.13f, 0.34f, 0.3f) * (0.45f + light * 1.1f), 0.85f);
            result.a = c.a;
            return result;
        }

        // A shine on a piece's materials, worn and lying about (tar, oiled wool).
        static void Gloss(string name, float gloss)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null) return;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in renderer.sharedMaterials)
                    if (m != null && m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", Mathf.Max(m.GetFloat("_Glossiness"), gloss));
            Material body = drop.m_itemData.m_shared.m_armorMaterial;
            if (body != null && body.HasProperty("_Glossiness")) body.SetFloat("_Glossiness", Mathf.Max(body.GetFloat("_Glossiness"), gloss));
        }

        // A piece made to look like another of the game's (its models and icons) while keeping what it
        // does: the trinkets' adrenaline, effects and stats stay the base's.
        internal static void Reskin(GameObject item, ItemDrop.ItemData.SharedData shared, string lookFrom, Recolor.Shift shift)
        {
            GameObject source = PrefabManager.Instance.GetPrefab(lookFrom);
            ItemDrop sourceDrop = source != null ? source.GetComponent<ItemDrop>() : null;
            if (sourceDrop == null) { Plugin.Debug(item.name + ": " + lookFrom + " not found, keeps its own look"); return; }
            List<GameObject> old = new List<GameObject>();
            foreach (Transform child in item.transform) old.Add(child.gameObject);
            foreach (Transform child in source.transform)
            {
                GameObject copy = Object.Instantiate(child.gameObject, item.transform, false);
                copy.name = child.name;
            }
            foreach (GameObject o in old) Object.DestroyImmediate(o);
            Sprite[] icons = sourceDrop.m_itemData.m_shared.m_icons;
            shared.m_icons = icons != null ? (Sprite[])icons.Clone() : shared.m_icons;
            Recolor.Item(item, shared, shift);
            Plugin.Debug(item.name + " looks like " + lookFrom);
        }

        static SE_Stats Effect(string name, string token)
        {
            return Effect(name, token, false);
        }

        internal static SE_Stats Effect(string name, string token, bool hidden)
        {
            SE_Stats effect = ScriptableObject.CreateInstance<SE_Stats>();
            effect.name = name;
            effect.m_name = "$" + token;
            effect.m_tooltip = "$" + token + "_tooltip";
            effect.m_hidden = hidden; // a piece's own effect is listed on the item, not at the top of the screen
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect, false));
            return effect;
        }

        // The first base item that exists, cloned under name with the token's name and description.
        internal static CustomItem Clone(string name, string[] bases, string token, Recolor.Shift shift)
        {
            foreach (string baseName in bases)
            {
                if (PrefabManager.Instance.GetPrefab(baseName) == null) continue;
                ItemConfig config = new ItemConfig();
                config.Name = "$" + token;
                config.Description = "$" + token + "_desc";
                CustomItem item = new CustomItem(name, baseName, config);
                if (item.ItemDrop == null) continue;
                ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
                shared.m_value = 0; // not something to sell on: the trader's goods aren't valuables
                shared.m_teleportable = true;
                shared.m_questItem = false;
                Recolor.Item(item.ItemPrefab, shared, shift);
                ItemManager.Instance.AddItem(item);
                Items.Add(item.ItemPrefab);
                Plugin.Debug("Trader item " + name + " made from " + baseName);
                return item;
            }
            Plugin.Log.LogWarning("None of " + string.Join(", ", bases) + " exist; the trader won't sell " + name + ".");
            return null;
        }

        static void Armor(string name, string[] bases, string token, float armor, float weight, StatusEffect effect, Recolor.Shift shift)
        {
            CustomItem item = Clone(name, bases, token, shift);
            if (item == null) return;
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_armor = armor;
            shared.m_armorPerLevel = 0f;
            shared.m_maxQuality = 1;      // bought, not crafted: nothing to upgrade it at
            shared.m_useDurability = false; // and nothing to repair it at
            shared.m_weight = weight;
            shared.m_movementModifier = 0f;
            shared.m_damageModifiers = new List<HitData.DamageModPair>();
            shared.m_equipStatusEffect = effect;
            if (effect != null) effect.m_icon = item.ItemDrop.m_itemData.GetIcon();
        }

        static void Mead(string name, string[] bases, string token, StatusEffect effect, Recolor.Shift shift)
        {
            CustomItem item = Clone(name, bases, token, shift);
            if (item == null) return;
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_consumeStatusEffect = effect;
            shared.m_maxStackSize = 10;
            if (effect != null) effect.m_icon = item.ItemDrop.m_itemData.GetIcon();
        }

        static void Food(string name, string[] bases, string token, float health, float stamina, float seconds, float regen, Recolor.Shift shift)
        {
            CustomItem item = Clone(name, bases, token, shift);
            if (item == null) return;
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_food = health;
            shared.m_foodStamina = stamina;
            shared.m_foodEitr = 0f;
            shared.m_foodBurnTime = seconds;
            shared.m_foodRegen = regen;
            shared.m_consumeStatusEffect = null;
            shared.m_maxStackSize = 20;
        }

        internal static CustomItem Simple(string name, string[] bases, string token, int stack, float weight, Recolor.Shift shift)
        {
            CustomItem item = Clone(name, bases, token, shift);
            if (item == null) return null;
            item.ItemDrop.m_itemData.m_shared.m_maxStackSize = stack;
            item.ItemDrop.m_itemData.m_shared.m_weight = weight;
            item.ItemDrop.m_itemData.m_shared.m_attackForce = 0f; // vanilla bait carries a knockback value the tooltip would show
            return item;
        }

        // ---- the effects the game has no field for ----

        static float WetTime
        {
            get
            {
                if (wetTime < 0f && ObjectDB.instance != null)
                {
                    StatusEffect wet = ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectWet);
                    if (wet != null) wetTime = wet.m_ttl;
                }
                return wetTime;
            }
        }

        // Rain, spray and swimming make you Wet, nights and rain make you Cold: the full set and the Storm
        // Mead keep you dry, the cape and the mead keep the Cold off (not Freezing, which needs real frost
        // protection), and the trousers make Wet wear off twice as fast.
        [HarmonyPatch(typeof(SEMan), "AddStatusEffect", new[] { typeof(int), typeof(bool), typeof(int), typeof(float), typeof(short) })]
        static class WeatherPatch
        {
            static bool Prefix(SEMan __instance, int nameHash, ref StatusEffect __result)
            {
                bool blocked = false;
                if (nameHash == SEMan.s_statusEffectWet) blocked = Has(__instance, setEffect) || Has(__instance, stormEffect);
                else if (nameHash == SEMan.s_statusEffectCold) blocked = Has(__instance, capeEffect) || Has(__instance, stormEffect);
                if (!blocked) return true;
                if (__instance.HaveStatusEffect(nameHash)) __instance.RemoveStatusEffect(nameHash, true);
                __result = null;
                return false;
            }

            static void Postfix(SEMan __instance, int nameHash, StatusEffect __result)
            {
                if (nameHash != SEMan.s_statusEffectWet || __result == null || WetTime <= 0f) return;
                __result.m_ttl = Has(__instance, trousersEffect) ? WetTime * 0.5f : WetTime;
            }

            static bool Has(SEMan seman, int effect)
            {
                return effect != 0 && seman.HaveStatusEffect(effect);
            }
        }

        // The Ancient Lure: on an islet's fishing ground every fish takes it readily, and one that does is
        // often a star better. Anywhere else no fish wants it.
        [HarmonyPatch(typeof(Fish), "TestBate")]
        static class LurePatch
        {
            static bool Prefix(Fish __instance, FishingFloat ff, ref bool __result)
            {
                if (ff == null || ff.GetBait() != Lure) return true;
                __result = false;
                ZNetView nview = __instance.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) return false;
                ZDO zdo = nview.GetZDO();
                int ground = zdo.GetInt(IsletFishing.GroundKey, 0);
                if (ground <= 0 || ground > IsletTier.All.Count || Random.value >= LureBite) return false;
                __result = true;
                if (nview.IsOwner() && !zdo.GetBool(LuredKey, false))
                {
                    zdo.Set(LuredKey, true);
                    ItemDrop drop = __instance.GetComponent<ItemDrop>();
                    IsletTier tier = IsletTier.All[ground - 1];
                    if (drop != null && Random.value < LureStarChance)
                    {
                        int max = Mathf.Min(tier.FishMaxLevel + 1, Mathf.Max(1, drop.m_itemData.m_shared.m_maxQuality));
                        if (drop.m_itemData.m_quality < max) drop.SetQuality(drop.m_itemData.m_quality + 1);
                    }
                    Plugin.Debug("Lure taken by " + __instance.name + " (level " + (drop != null ? drop.m_itemData.m_quality : 0) + ")");
                }
                return false;
            }
        }

        const string LuredKey = "IoV_Lured";
        const float LureRange = 35f; // how far an islet ground's fish come for the lure (vanilla's float range is a few metres)

        // A ground's fish are spread all around its islet, a few at a time, so few would ever be near the
        // float: the lure draws them from much further off. A fish that isn't already after a float looks
        // for the closest lure in the water within LureRange.
        [HarmonyPatch(typeof(Fish), "FindFloat")]
        static class LureRangePatch
        {
            static void Postfix(Fish __instance, ref FishingFloat __result)
            {
                if (__result != null) return;
                ZNetView nview = __instance.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid() || nview.GetZDO().GetInt(IsletFishing.GroundKey, 0) <= 0) return;
                float best = LureRange;
                foreach (FishingFloat ff in FishingFloat.GetAllInstances())
                {
                    if (ff == null || !ff.IsInWater() || ff.GetCatch() != null || ff.GetBait() != Lure) continue;
                    float distance = Vector3.Distance(ff.transform.position, __instance.transform.position);
                    if (distance < best) { best = distance; __result = ff; }
                }
                if (__result != null && best > 8f) Plugin.Debug("Lure: " + __instance.name + " comes for it from " + best.ToString("0") + " m");
            }
        }

        // The game lists a knockback for anything used as ammo, bait included, even when it's 0.
        [HarmonyPatch(typeof(ItemDrop.ItemData), "GetTooltip", new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
        static class LureTooltipPatch
        {
            static void Postfix(ItemDrop.ItemData item, ref string __result)
            {
                if (item == null || item.m_shared == null || item.m_shared.m_name != "$iov_lure" || __result == null) return;
                __result = System.Text.RegularExpressions.Regex.Replace(__result, @"\n?\$item_knockback: <color=orange>[^<]*</color>", "");
            }
        }

        // The Caulking Kit: used on board (right-click in the inventory, or a hotbar key), it repairs the ship
        // you're on. Vanilla needs a workbench nearby to repair a ship; this works at sea.
        [HarmonyPatch(typeof(Humanoid), "UseItem")]
        static class CaulkPatch
        {
            static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
            {
                if (item == null || item.m_shared == null || item.m_shared.m_name != CaulkToken) return true;
                Player player = __instance as Player;
                if (player != null && player == Player.m_localPlayer) UseCaulk(player, item);
                return false;
            }
        }

        static void UseCaulk(Player player, ItemDrop.ItemData kit)
        {
            Ship ship = Ship.GetLocalShip();
            WearNTear hull = ship != null ? ship.GetComponent<WearNTear>() : null;
            if (hull == null)
            {
                player.Message(MessageHud.MessageType.Center, "$iov_caulk_noship", 0, null, false);
                return;
            }
            if (hull.GetHealthPercentage() >= 0.999f)
            {
                player.Message(MessageHud.MessageType.Center, "$iov_caulk_fine", 0, null, false);
                return;
            }
            if (!hull.Repair()) return; // just repaired a moment ago
            player.GetInventory().RemoveOneItem(kit);
            player.Message(MessageHud.MessageType.Center, "$iov_caulk_done", 0, null, false);
            Plugin.Debug("Caulking kit used on " + ship.name);
        }
    }

    // An item's own light, breathing slowly (SeafarerGear.Glow).
    class ItemGlow : MonoBehaviour
    {
        // Serialized, so copies of the prefab keep them (Unity copies only serialized fields).
        [SerializeField] internal float speed = 0f;
        [SerializeField] internal float strength = 1f;
        Light light;
        float offset;
        static int told;

        void Awake()
        {
            light = GetComponent<Light>();
            offset = Random.Range(0f, 10f);
        }

        void Start()
        {
            if (light == null || told >= 6) return;
            told++;
            Plugin.Debug("Item light on " + (transform.parent != null ? transform.parent.name : name) + ": " + (light.enabled && gameObject.activeInHierarchy ? "on" : "OFF") +
                ", " + light.intensity + " at " + light.range + " m, " + light.renderMode + ", culling " + light.cullingMask);
        }

        void Update()
        {
            if (light == null) return;
            light.intensity = speed > 0f ? strength * (0.55f + 0.45f * Mathf.Sin((Time.time + offset) * speed)) : strength;
        }
    }

    // Recolouring vanilla textures at startup: a readable copy is made on the GPU (the game's textures
    // can't be read directly), shifted pixel by pixel, and used on cloned materials and icons. Without a
    // graphics device (a dedicated server) nothing is recoloured, which only matters to how things look.
    static class Recolor
    {
        internal delegate Color Shift(Color c);

        static readonly Dictionary<Texture, Texture2D> textures = new Dictionary<Texture, Texture2D>();
        static readonly string[] textureNames = { "_MainTex", "_ChestTex", "_LegsTex" };

        static bool CanRender
        {
            get { return SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null; }
        }

        // Coloured parts take the hue, keep their shading (saturation and value scaled); greys, metal and
        // near-black stay as they are.
        internal static Shift ToHue(float hue, float saturation, float value)
        {
            return c =>
            {
                float h, s, v;
                Color.RGBToHSV(c, out h, out s, out v);
                if (s < 0.12f || v < 0.06f) return c;
                Color result = Color.HSVToRGB(hue, Mathf.Clamp01(Mathf.Lerp(s, saturation, 0.6f)), Mathf.Clamp01(v * value));
                result.a = c.a;
                return result;
            };
        }

        // Less colour, a little lighter: salted, dried.
        internal static Shift Pale(float saturation, float value)
        {
            return c =>
            {
                float h, s, v;
                Color.RGBToHSV(c, out h, out s, out v);
                Color result = Color.HSVToRGB(h, s * saturation, Mathf.Clamp01(v * value));
                result.a = c.a;
                return result;
            };
        }

        // For dark items: lift the darks into a colour, so a black blob becomes a brown one.
        internal static Shift Lift(float hue, float saturation, float lift)
        {
            return c =>
            {
                float h, s, v;
                Color.RGBToHSV(c, out h, out s, out v);
                Color result = Color.HSVToRGB(hue, saturation, Mathf.Clamp01(v + lift * (1f - v) * 0.8f));
                result.a = c.a;
                return result;
            };
        }

        // An item's look: its meshes (in the world and worn), the armour texture on the body, its icons.
        internal static void Item(GameObject prefab, ItemDrop.ItemData.SharedData shared, Shift shift)
        {
            if (!CanRender || shift == null) return;
            try
            {
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer) continue;
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = Material(materials[i], shift);
                    renderer.sharedMaterials = materials;
                }
                if (shared.m_armorMaterial != null) shared.m_armorMaterial = Material(shared.m_armorMaterial, shift);
                if (shared.m_icons != null)
                    for (int i = 0; i < shared.m_icons.Length; i++) shared.m_icons[i] = Icon(shared.m_icons[i], shift);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Could not recolour " + prefab.name + ": " + e.Message);
            }
        }

        internal static Material Material(Material source, Shift shift)
        {
            if (source == null) return null;
            Material copy = new Material(source);
            copy.name = source.name + "_IoV";
            foreach (string property in textureNames)
            {
                if (!copy.HasProperty(property)) continue;
                Texture texture = copy.GetTexture(property);
                if (texture != null) copy.SetTexture(property, Texture(texture, shift));
            }
            return copy;
        }

        static Texture2D Texture(Texture source, Shift shift)
        {
            Texture2D done;
            if (textures.TryGetValue(source, out done)) return done;
            Texture2D copy = Copy(source, new Rect(0f, 0f, source.width, source.height), true);
            Apply(copy, shift);
            copy.name = source.name + "_IoV";
            copy.wrapMode = source.wrapMode;
            copy.filterMode = source.filterMode;
            copy.anisoLevel = source.anisoLevel;
            copy.Apply(true, true); // mipmaps, then the CPU copy is no longer needed
            textures[source] = copy;
            return copy;
        }

        internal static Sprite Icon(Sprite source, Shift shift)
        {
            if (source == null) return null;
            Rect rect = source.textureRect;
            Texture2D copy = Copy(source.texture, rect, false);
            Apply(copy, shift);
            copy.Apply(false, true);
            Sprite sprite = Sprite.Create(copy, new Rect(0f, 0f, copy.width, copy.height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            sprite.name = source.name + "_IoV";
            return sprite;
        }

        static void Apply(Texture2D texture, Shift shift)
        {
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = shift(pixels[i]);
            texture.SetPixels(pixels);
        }

        // A readable copy of part of a texture (rect in pixels).
        static Texture2D Copy(Texture source, Rect rect, bool mipmaps)
        {
            int w = Mathf.Max(1, Mathf.RoundToInt(rect.width)), h = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            RenderTexture target = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Vector2 scale = new Vector2(rect.width / source.width, rect.height / source.height);
            Vector2 offset = new Vector2(rect.x / source.width, rect.y / source.height);
            Graphics.Blit(source, target, scale, offset);
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D copy = new Texture2D(w, h, TextureFormat.RGBA32, mipmaps);
            copy.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            return copy;
        }
    }
}
