using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The tale of the Brim Folk, told by Sævar one step at a time. Progress belongs to the world, not to one
    // player: each step is a global key, so whoever brings the news, everyone hears the rest. The steps go
    // in order:
    //   1. Sævar gives a torn map to the old Brim harbour (Meadows waters). Lighting its beacon finishes it;
    //      back at Sævar: the first page of the tale and the next map.
    //   2. Ketill's islet (Swamp waters): his crew lies dead and Ketill walks as an Elite Draugr, the first
    //      fragment about his neck.
    //   3. Sigrún's islet (Plains waters): she threw the second fragment into the sea and a fish swallowed it;
    //      it comes up with a fish caught around her islet.
    //   4. Ormr's islet (Mistlands waters): his diary on stones, the third fragment locked in his chest.
    //   5. A dvergr outpost (Mistlands waters): they keep the fourth fragment, and taking it angers them.
    //   6. Hafdís's shore (Ashlands waters): where the sea's guardian was once a sailor. The fifth fragment
    //      lies in the ashes; taking it wakes her crew.
    //   7. The hidden islet (Deep North waters): Grímr, then Ormr, then Hafdís herself, who offers you her
    //      place before she fights. Her fragment, the last, brought to Sævar: the light takes him, and
    //      leaves the pieces as one small trinket. His chest stays and keeps trading.
    // Each fragment brought to Sævar: a page of the tale and new goods; then the map for the next step.
    // While anyone carries a fragment the sea turns against them: storms, and serpents that come looking.
    static class Story
    {
        internal const string OldHarbour = "IoV_Story_OldHarbour";
        internal const string KetillIslet = "IoV_Story_Ketill";
        internal const string SigrunIslet = "IoV_Story_Sigrun";
        internal const string OrmrIslet = "IoV_Story_Ormr";
        internal const string DvergrHold = "IoV_Story_Dvergr";
        internal const string AshIslet = "IoV_Story_Ashes";
        internal const string HiddenIslet = "IoV_Story_Hidden";
        internal static readonly string[] Islets = { OldHarbour, KetillIslet, SigrunIslet, OrmrIslet, DvergrHold, AshIslet, HiddenIslet }; // iov_goto story1..7

        internal const string Map1 = "IoV_StoryMap1";
        internal const string Map2 = "IoV_StoryMap2";
        internal const string Map3 = "IoV_StoryMap3";
        internal const string Map4 = "IoV_StoryMap4";
        internal const string Map5 = "IoV_StoryMap5";
        internal const string Map6 = "IoV_StoryMap6";
        internal const string Map7 = "IoV_StoryMap7";
        internal const string Piece1 = "IoV_BrimPiece1";
        internal const string Piece2 = "IoV_BrimPiece2";
        internal const string Piece3 = "IoV_BrimPiece3";
        internal const string Piece4 = "IoV_BrimPiece4";
        internal const string Piece5 = "IoV_BrimPiece5";
        internal const string Piece6 = "IoV_BrimPiece6";
        internal const string BrimHeart = "IoV_BrimHeart";        // the reward: what the light leaves of them
        internal const string AshPiece = "IoV_Story_AshPiece";    // the fifth fragment in the ashes, to pick up
        internal const string Grimr = "IoV_Grimr";
        internal const string OrmrBoss = "IoV_Ormr";
        internal const string Hafdis = "IoV_Hafdis";
        internal const string FinalStand = "IoV_Story_FinalStand"; // runs the three fights on the hidden islet
        internal const string BrimOil = "IoV_BrimOil";
        internal const string StormGlass = "IoV_StormGlass";
        internal const string GullFeather = "IoV_GullFeather";
        internal const string Harpoon = "IoV_BrimHarpoon";
        internal const string SerpentCharm = "IoV_SerpentCharm";
        internal const string AshTar = "IoV_AshTar";
        internal const string Ketill = "IoV_Ketill";
        internal const string KetillSpawner = "IoV_Spawner_IoV_Ketill";
        internal const string OrmrChest = "IoV_Story_OrmrChest";
        internal const string OrmrGuard = "IoV_Spawner_OrmrGuard";
        internal const string RunePiece = "IoV_Story_RunePiece";     // the dvergr's fragment on its stand, to pick up
        internal const string SigrunWaters = "IoV_Story_SigrunWaters"; // marks the water where the fish has it

        // The world's progress (global keys, not text keys).
        internal const string HarbourLit = "iovstory_harbour_lit";
        internal const string FirstTold = "iovstory_first_told";
        internal const string KetillDone = "iovstory_ketill_done";
        internal const string SigrunDone = "iovstory_sigrun_done";
        internal const string OrmrDone = "iovstory_ormr_done";
        internal const string DvergrDone = "iovstory_dvergr_done";
        internal const string AshesDone = "iovstory_ashes_done";
        internal const string GrimrDown = "iovstory_grimr_down";
        internal const string OrmrDown = "iovstory_ormr_down";
        internal const string HafdisDown = "iovstory_hafdis_down";
        internal const string FinalDone = "iovstory_final_done"; // Sævar is gone; his chest trades

        const string RequestRpc = "IoV_StoryMapRequest";
        const string AnswerRpc = "IoV_StoryMapAnswer";

        // The steps after the first: the key that finishes it, the fragment to bring, the map that leads to
        // it, Sævar's lines (+ give, wait, done) and the page of the tale he tells.
        class Step
        {
            internal string Done, Piece, Map, Lines, Lore;
            internal bool Last; // bringing this one ends the tale
        }

        static readonly Step[] Steps =
        {
            new Step { Done = KetillDone, Piece = Piece1, Map = Map2, Lines = "iov_story_q2_", Lore = "iov_lore_ketill" },
            new Step { Done = SigrunDone, Piece = Piece2, Map = Map3, Lines = "iov_story_q3_", Lore = "iov_lore_sigrun" },
            new Step { Done = OrmrDone, Piece = Piece3, Map = Map4, Lines = "iov_story_q4_", Lore = "iov_lore_ormr" },
            new Step { Done = DvergrDone, Piece = Piece4, Map = Map5, Lines = "iov_story_q5_", Lore = "iov_lore_dvergr" },
            new Step { Done = AshesDone, Piece = Piece5, Map = Map6, Lines = "iov_story_q6_", Lore = "iov_lore_hafdis" },
            new Step { Done = FinalDone, Piece = Piece6, Map = Map7, Lines = "iov_story_q7_", Lore = "iov_lore_light", Last = true },
        };

        // Each map: its item, the location it leads to, the pin it leaves and its item's text.
        static readonly string[][] Maps =
        {
            new[] { Map1, OldHarbour, "$iov_storypin1", "iov_storymap1" },
            new[] { Map2, KetillIslet, "$iov_storypin2", "iov_storymap2" },
            new[] { Map3, SigrunIslet, "$iov_storypin3", "iov_storymap3" },
            new[] { Map4, OrmrIslet, "$iov_storypin4", "iov_storymap4" },
            new[] { Map5, DvergrHold, "$iov_storypin5", "iov_storymap5" },
            new[] { Map6, AshIslet, "$iov_storypin6", "iov_storymap6" },
            new[] { Map7, HiddenIslet, "$iov_storypin7", "iov_storymap7" },
        };

        // Lore stones on the story islets: prefab, label, text.
        internal static readonly string[][] Stones =
        {
            new[] { "IoV_StoryStone_Harbour", "RuneStone_Meadows", "iov_stone_harbour" },
            new[] { "IoV_StoryStone_Ketill1", "RuneStone_Swamps", "iov_stone_ketill1" },
            new[] { "IoV_StoryStone_Ketill2", "RuneStone_Swamps", "iov_stone_ketill2" },
            new[] { "IoV_StoryStone_Ketill3", "RuneStone_Swamps", "iov_stone_ketill3" },
            new[] { "IoV_StoryStone_Sigrun1", "RuneStone_Plains", "iov_stone_sigrun1" },
            new[] { "IoV_StoryStone_Sigrun2", "RuneStone_Plains", "iov_stone_sigrun2" },
            new[] { "IoV_StoryStone_Sigrun3", "RuneStone_Plains", "iov_stone_sigrun3" },
            new[] { "IoV_StoryStone_Ormr1", "RuneStone_Mistlands", "iov_stone_ormr1" },
            new[] { "IoV_StoryStone_Ormr2", "RuneStone_Mistlands", "iov_stone_ormr2" },
            new[] { "IoV_StoryStone_Ormr3", "RuneStone_Mistlands", "iov_stone_ormr3" },
            new[] { "IoV_StoryStone_Hafdis1", "RuneStone_Ashlands", "iov_stone_hafdis1" },
            new[] { "IoV_StoryStone_Hafdis2", "RuneStone_Ashlands", "iov_stone_hafdis2" },
            new[] { "IoV_StoryStone_Hafdis3", "RuneStone_Ashlands", "iov_stone_hafdis3" },
            new[] { "IoV_StoryStone_Hidden", "RuneStone_DeepNorth", "iov_stone_hidden" },
        };

        // The three on the hidden islet, in the order they come: prefab, vanilla body, name, level, base
        // health (the level multiplies it), the key their fall sets, and their lines (+ spawn, death).
        internal class Keeper
        {
            internal string Prefab, Body, Name, Down, Lines;
            internal int Level;
            internal float Health;
            internal Character.Faction Faction; // its vanilla side, which Hafdís turns back to once struck
        }

        internal static readonly Keeper[] Keepers =
        {
            new Keeper { Prefab = Grimr, Body = "JotunWarrior", Name = "$iov_grimr", Level = 2, Health = 1500f, Down = GrimrDown, Lines = "iov_grimr_" },
            new Keeper { Prefab = OrmrBoss, Body = "Fenring_Cultist", Name = "$iov_ormr", Level = 3, Health = 1200f, Down = OrmrDown, Lines = "iov_ormr_" },
            new Keeper { Prefab = Hafdis, Body = "FallenValkyrie", Name = "$iov_hafdis", Level = 2, Health = 2500f, Down = HafdisDown, Lines = "iov_hafdis_" },
        };

        static int calmEffect, wardEffect, heartEffect, tarEffect;

        // The ash-sea tar: no burning-sea damage to the ship the local player is aboard while it lasts.
        [HarmonyPatch(typeof(Ship), "TakeAshlandsDamage")]
        static class AshTarPatch
        {
            static bool Prefix(Ship __instance)
            {
                Player player = Player.m_localPlayer;
                return player == null || tarEffect == 0 || Ship.GetLocalShip() != __instance || !player.GetSEMan().HaveStatusEffect(tarEffect);
            }
        }

        internal static void Register()
        {
            Recolor.Shift parchment = Recolor.ToHue(0.09f, 0.3f, 1.35f);
            foreach (string[] map in Maps)
            {
                CustomItem item = SeafarerGear.Simple(map[0], new[] { "LeatherScraps", "TrollHide" }, map[3], 1, 0.1f, parchment);
                if (item != null) item.ItemDrop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
            }
            Fragment(Piece1, "iov_piece1", Recolor.ToHue(0.13f, 0.75f, 1.25f));
            Fragment(Piece2, "iov_piece2", Recolor.ToHue(0.12f, 0.3f, 1.4f));   // salt-white
            Fragment(Piece3, "iov_piece3", Recolor.ToHue(0.75f, 0.35f, 0.55f)); // gone dark
            Fragment(Piece4, "iov_piece4", Recolor.ToHue(0.5f, 0.55f, 1.1f));   // the dvergr's blue runes
            Fragment(Piece5, "iov_piece5", Recolor.ToHue(0.03f, 0.8f, 0.9f));   // ember-red
            Fragment(Piece6, "iov_piece6", Recolor.ToHue(0.58f, 0.15f, 1.3f));  // cold, almost white
            CustomItem oil = SeafarerGear.Simple(BrimOil, new[] { "Honey" }, "iov_brimoil", 10, 0.5f, Recolor.ToHue(0.07f, 0.8f, 0.7f));
            if (oil != null)
            {
                ItemDrop.ItemData.SharedData shared = oil.ItemDrop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
                shared.m_food = shared.m_foodStamina = shared.m_foodEitr = shared.m_foodRegen = 0f;
                shared.m_consumeStatusEffect = null;
            }
            SE_Stats calm = SeafarerGear.Effect("IoV_SE_StormGlass", "iov_se_calm", false);
            calm.m_ttl = 600f;
            Consumable(StormGlass, new[] { "Crystal" }, "iov_stormglass", 0.3f, Recolor.ToHue(0.55f, 0.5f, 1.1f), calm);
            calmEffect = calm.NameHash();

            // Sigrún's: the wind behind the sail, the way Moder's power does it.
            SE_Stats tailwind = SeafarerGear.Effect("IoV_SE_Tailwind", "iov_se_tailwind", false);
            tailwind.m_ttl = 300f;
            tailwind.m_attributes = StatusEffect.StatusAttribute.SailingPower;
            Consumable(GullFeather, new[] { "Feathers" }, "iov_gullfeather", 0.1f, Recolor.Pale(0.2f, 1.25f), tailwind);
            // Ormr's crew hunted serpents.
            CustomItem harpoon = SeafarerGear.Clone(Harpoon, new[] { "SpearChitin" }, "iov_harpoon", Recolor.ToHue(0.08f, 0.45f, 0.85f));
            if (harpoon != null)
            {
                ItemDrop.ItemData.SharedData shared = harpoon.ItemDrop.m_itemData.m_shared;
                shared.m_damages.m_pierce *= 1.3f;
                shared.m_damagesPerLevel = new HitData.DamageTypes();
                shared.m_maxQuality = 1;       // bought, not crafted: nothing to upgrade it at
                shared.m_useDurability = false; // and nothing to repair it at
            }
            // From what the dvergr knew: serpents bite softer, and the angry sea sends none.
            SE_Stats ward = SeafarerGear.Effect("IoV_SE_SerpentWard", "iov_se_serpentward", false);
            ward.m_ttl = 600f;
            Consumable(SerpentCharm, new[] { "SerpentScale" }, "iov_serpentcharm", 0.2f, Recolor.ToHue(0.45f, 0.5f, 0.9f), ward);
            wardEffect = ward.NameHash();
            // What Hafdís's crew tarred their hulls with: the burning sea doesn't hurt the ship you're on.
            SE_Stats tar = SeafarerGear.Effect("IoV_SE_AshTar", "iov_se_ashtar", false);
            tar.m_ttl = 600f;
            Consumable(AshTar, new[] { "Tar" }, "iov_ashtar", 1f, Recolor.ToHue(0.04f, 0.7f, 0.55f), tar);
            tarEffect = tar.NameHash();

            // The end of the tale: the fragments made one, clean. The sea knows whoever wears it.
            SE_Stats heart = SeafarerGear.Effect("IoV_SE_BrimHeart", "iov_se_brimheart", false);
            heart.m_swimSpeedModifier = 0.3f;
            heart.m_swimStaminaUseModifier = -0.3f;
            CustomItem trinket = SeafarerGear.Clone(BrimHeart, new[] { "TrinketChitinSwim", "TrinketSilverResist" }, "iov_brimheart", Recolor.ToHue(0.13f, 0.7f, 1.3f));
            if (trinket != null)
            {
                ItemDrop.ItemData.SharedData shared = trinket.ItemDrop.m_itemData.m_shared;
                shared.m_equipStatusEffect = heart;
                shared.m_maxQuality = 1;
                shared.m_teleportable = true;
                heart.m_icon = trinket.ItemDrop.m_itemData.GetIcon();
            }
            heartEffect = heart.NameHash();

            RegisterKetill();
            RegisterOrmrChest();
            IsletPrefabs.AddSpawner("SeekerBrute", OrmrGuard, 2, 2, 0f, 0f, true, 30f);
            RegisterStoryPickable(RunePiece, Piece4, 40f, "Pickable_DvergrLantern");
            RegisterStoryPickable(AshPiece, Piece5, 0f, "Pickable_DvergrLantern");
            foreach (Keeper keeper in Keepers) RegisterKeeper(keeper);
            RegisterMarker(FinalStand).AddComponent<FinalStand>();
            RegisterMarker(SigrunWaters).AddComponent<SigrunWatersMarker>();
            foreach (string[] stone in Stones) RegisterStone(stone[0], stone[1], stone[2]);
        }

        // A fragment: it has to cross the sea, and the sea knows (no teleporting).
        static void Fragment(string name, string token, Recolor.Shift shift)
        {
            CustomItem piece = SeafarerGear.Simple(name, new[] { "Thunderstone", "AmberPearl" }, token, 1, 0.5f, shift);
            if (piece == null) return;
            ItemDrop.ItemData.SharedData shared = piece.ItemDrop.m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
            shared.m_teleportable = false;
        }

        // Something used up for a status effect, with the item's icon on it.
        static void Consumable(string name, string[] bases, string token, float weight, Recolor.Shift shift, SE_Stats effect)
        {
            CustomItem item = SeafarerGear.Simple(name, bases, token, 10, weight, shift);
            if (item == null) return;
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            shared.m_food = shared.m_foodStamina = shared.m_foodEitr = shared.m_foodRegen = 0f;
            shared.m_consumeStatusEffect = effect;
            effect.m_icon = item.ItemDrop.m_itemData.GetIcon();
        }

        // Ketill: an Elite Draugr with his name, a boss's bar over him, and the first fragment; his looks and
        // his fight are his own (KetillBoss).
        static void RegisterKetill()
        {
            if (PrefabManager.Instance.GetPrefab("Draugr_Elite") == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(Ketill, "Draugr_Elite");
            Humanoid body = go.GetComponent<Humanoid>();
            if (body != null)
            {
                body.m_name = "$iov_ketill";
                body.m_boss = true;
                body.m_bossEvent = "";
            }
            KetillBoss.Setup(go);
            CharacterDrop drops = go.GetComponent<CharacterDrop>();
            GameObject fragment = PrefabManager.Instance.GetPrefab(Piece1);
            if (drops != null && fragment != null)
            {
                CharacterDrop.Drop drop = new CharacterDrop.Drop();
                drop.m_prefab = fragment;
                drop.m_amountMin = drop.m_amountMax = 1;
                drop.m_chance = 1f;
                drop.m_levelMultiplier = false;
                drops.m_drops.Add(drop);
            }
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
            IsletPrefabs.AddSpawner(Ketill, KetillSpawner, 2, 2, 0f, 0f, true, 30f);
        }

        // Ormr's own chest: the fragment he hid from his crew, and what was left of his share.
        static void RegisterOrmrChest()
        {
            GameObject fragment = PrefabManager.Instance.GetPrefab(Piece3);
            GameObject coins = PrefabManager.Instance.GetPrefab("Coins");
            string baseName = PrefabManager.Instance.GetPrefab("TreasureChest_dvergrtower") != null ? "TreasureChest_dvergrtower" : "TreasureChest_meadows";
            if (fragment == null || PrefabManager.Instance.GetPrefab(baseName) == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(OrmrChest, baseName);
            Container container = go.GetComponent<Container>();
            container.m_name = "$iov_ormrchest";
            container.m_checkGuardStone = false;
            DropTable table = new DropTable();
            table.m_dropChance = 1f;
            table.m_oneOfEach = true;
            table.m_drops = new List<DropTable.DropData>();
            table.m_drops.Add(new DropTable.DropData { m_item = fragment, m_stackMin = 1, m_stackMax = 1, m_weight = 1f });
            if (coins != null) table.m_drops.Add(new DropTable.DropData { m_item = coins, m_stackMin = 80, m_stackMax = 140, m_weight = 1f });
            table.m_dropMin = table.m_dropMax = table.m_drops.Count; // all of it
            container.m_defaultItems = table;
            Piece piece = go.GetComponent<Piece>();
            if (piece != null) piece.m_canBeRemoved = false;
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // A fragment lying where it was left, to pick up: the fragment item itself with its loose-item parts
        // taken off, so it looks and glints the same; once taken it is gone for good. aggravate: like the
        // dvergr's steins and lanterns, taking it angers those around (the dvergr's fragment).
        static void RegisterStoryPickable(string name, string piece, float aggravate, string effectsFrom)
        {
            GameObject item = PrefabManager.Instance.GetPrefab(piece);
            if (item == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(name, piece);
            foreach (Floating floating in go.GetComponentsInChildren<Floating>(true)) Object.DestroyImmediate(floating);
            foreach (ItemDrop drop in go.GetComponentsInChildren<ItemDrop>(true)) Object.DestroyImmediate(drop);
            foreach (ZSyncTransform sync in go.GetComponentsInChildren<ZSyncTransform>(true)) Object.DestroyImmediate(sync);
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            ZNetView nview = go.GetComponent<ZNetView>();
            if (nview != null) nview.m_persistent = true;
            Pickable pickable = go.AddComponent<Pickable>();
            pickable.m_itemPrefab = item;
            pickable.m_amount = 1;
            pickable.m_dontScale = true;
            pickable.m_respawnTimeMinutes = 0f;
            pickable.m_aggravateRange = aggravate;
            pickable.m_defaultEnabled = true;
            pickable.m_useInteractAnimation = true; // you reach down for it
            GameObject source = PrefabManager.Instance.GetPrefab(effectsFrom);
            Pickable vanilla = source != null ? source.GetComponent<Pickable>() : null;
            if (vanilla != null) pickable.m_pickEffector = vanilla.m_pickEffector;
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // One of the three keepers: a vanilla creature with a name, a boss's bar, its own health and no
        // spawner of its own (FinalStand brings them). Hafdís doesn't attack first: she is Dvergr-like,
        // left in peace until someone strikes her; she has the last fragment.
        static void RegisterKeeper(Keeper keeper)
        {
            if (PrefabManager.Instance.GetPrefab(keeper.Body) == null)
            {
                Plugin.Log.LogWarning(keeper.Body + " not found: " + keeper.Prefab + " is left out, and the hidden islet's fights skip it.");
                return;
            }
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(keeper.Prefab, keeper.Body);
            Humanoid humanoid = go.GetComponent<Humanoid>();
            Character body = go.GetComponent<Character>();
            if (body == null) return;
            body.m_name = keeper.Name;
            body.m_boss = true;
            body.m_bossEvent = "";
            body.m_health = keeper.Health;
            keeper.Faction = body.m_faction;
            BaseAI ai = go.GetComponent<BaseAI>();
            if (ai != null) { ai.m_spawnMessage = ""; ai.m_deathMessage = ""; }
            if (keeper.Prefab == Hafdis)
            {
                // Nobody's enemy until struck (FinalStand turns her back), and she keeps low over her hall,
                // in reach, rather than circling high the way a Fallen Valkyrie does.
                body.m_faction = Character.Faction.Dverger;
                if (ai != null)
                {
                    ai.m_aggravatable = true;
                    Plugin.Debug("Hafdís flies at " + ai.m_flyAltitudeMin + "-" + ai.m_flyAltitudeMax + " m in vanilla");
                    ai.m_flyAltitudeMin = Mathf.Min(ai.m_flyAltitudeMin, 3f);
                    ai.m_flyAltitudeMax = Mathf.Min(ai.m_flyAltitudeMax, 7f);
                }
                CharacterDrop drops = go.GetComponent<CharacterDrop>();
                GameObject fragment = PrefabManager.Instance.GetPrefab(Piece6);
                if (drops != null && fragment != null)
                {
                    CharacterDrop.Drop drop = new CharacterDrop.Drop();
                    drop.m_prefab = fragment;
                    drop.m_amountMin = drop.m_amountMax = 1;
                    drop.m_chance = 1f;
                    drop.m_levelMultiplier = false;
                    drops.m_drops.Add(drop);
                }
            }
            if (keeper.Prefab == Grimr) GrimrBoss.Setup(go);
            if (keeper.Prefab == OrmrBoss) OrmrBossFight.Setup(go);
            Plugin.Debug("Keeper " + keeper.Prefab + " made from " + keeper.Body + (humanoid != null ? " (humanoid)" : ""));
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // An invisible networked marker.
        static GameObject RegisterMarker(string name)
        {
            GameObject go = PrefabManager.Instance.CreateEmptyPrefab(name, true);
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<ZNetView>().m_persistent = true;
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
            return go;
        }

        // A lore stone with a text of our own, standing on the islet (no physics dropping it to the sea floor).
        static void RegisterStone(string name, string vanilla, string token)
        {
            if (PrefabManager.Instance.GetPrefab(vanilla) == null) vanilla = "RuneStone_Meadows";
            if (PrefabManager.Instance.GetPrefab(vanilla) == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(name, vanilla);
            foreach (StaticPhysics physics in go.GetComponentsInChildren<StaticPhysics>(true)) Object.DestroyImmediate(physics);
            RuneStone stone = go.GetComponentInChildren<RuneStone>(true);
            if (stone != null)
            {
                if (stone.m_randomTexts != null) stone.m_randomTexts.Clear();
                stone.m_label = "$" + token + "_label";
                stone.m_topic = "$" + token + "_label";
                stone.m_text = "$" + token;
                stone.m_locationName = "";
            }
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // ---- progress ----

        internal static bool Key(string key)
        {
            return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key);
        }

        static void Set(string key)
        {
            if (ZoneSystem.instance != null && !ZoneSystem.instance.GetGlobalKey(key)) ZoneSystem.instance.SetGlobalKey(key);
            Plugin.Debug("Story: " + key);
        }

        static bool Has(Player player, string prefab)
        {
            return Find(player, prefab) != null;
        }

        static ItemDrop.ItemData Find(Player player, string prefab)
        {
            GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (item == null) return null;
            return player.GetInventory().GetItem(item.GetComponent<ItemDrop>().m_itemData.m_shared.m_name, -1, false);
        }

        // Into the inventory, or at the player's feet if it's full; the item's name for the message.
        static string Hand(Player player, string prefab)
        {
            GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (item == null) return null;
            Inventory inventory = player.GetInventory();
            if (inventory.CanAddItem(item, 1)) inventory.AddItem(item, 1);
            else Object.Instantiate(item, player.transform.position + Vector3.up, Quaternion.identity);
            return item.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
        }

        static void Give(Player player, string prefab)
        {
            string name = Hand(player, prefab);
            if (name != null) player.Message(MessageHud.MessageType.Center, Texts.Get("iov_story_received", name), 0, null, false);
        }

        // The pages of the tale this world has reached, in the player's compendium.
        static void LearnPages(Player player)
        {
            if (Key(FirstTold)) player.AddKnownText("$iov_lore_brim", "$iov_lore_brim_text");
            foreach (Step step in Steps)
                if (Key(step.Done)) player.AddKnownText("$" + step.Lore, "$" + step.Lore + "_text");
        }

        static void Tell(Player player, string label, string text)
        {
            player.AddKnownText(label, text);
            if (TextViewer.instance != null) TextViewer.instance.ShowText(TextViewer.Style.Rune, label, text, true); // the game says it's in the compendium
        }

        // Talking to Sævar: the next step of the tale if there is one to take, else nothing (the store opens).
        internal static bool Talk(Player player, SeaTraderNpc npc)
        {
            LearnPages(player);
            if (Key(FinalDone)) return false; // only his chest is left
            if (!Key(FirstTold))
            {
                if (Key(HarbourLit))
                {
                    npc.Say("$iov_story_q1_done");
                    Set(FirstTold);
                    Tell(player, "$iov_lore_brim", "$iov_lore_brim_text");
                    Give(player, Map2);
                    return true;
                }
                if (!Has(player, Map1))
                {
                    npc.Say("$iov_story_q1_give");
                    Give(player, Map1);
                    return true;
                }
                return false;
            }
            foreach (Step step in Steps)
            {
                if (Key(step.Done)) continue;
                ItemDrop.ItemData piece = Find(player, step.Piece);
                if (piece != null)
                {
                    player.GetInventory().RemoveOneItem(piece);
                    npc.Say("$" + step.Lines + "done");
                    Set(step.Done);
                    if (step.Last)
                    {
                        // The light comes for him, as it once came for her, after his last words; the page
                        // and what the light leaves of the fragments come once he's gone.
                        player.AddKnownText("$" + step.Lore, "$" + step.Lore + "_text");
                        npc.Hold();
                        Plugin.Instance.StartCoroutine(Ending(player, npc, "$" + step.Lore));
                        Plugin.Debug("Story: the tale is told");
                    }
                    else Tell(player, "$" + step.Lore, "$" + step.Lore + "_text");
                    return true;
                }
                if (!Has(player, step.Map))
                {
                    npc.Say("$" + step.Lines + "give");
                    Give(player, step.Map);
                    return true;
                }
                return false; // one step at a time
            }
            return false;
        }

        // The end, in its own time: his last words, then the light comes down slowly, he goes in it, and
        // the page and the trinket follow. Runs on the plugin, so it finishes even if the harbour unloads.
        static System.Collections.IEnumerator Ending(Player player, SeaTraderNpc npc, string lore)
        {
            yield return new WaitForSeconds(3f);
            if (npc != null) LightBeam.Over(npc.transform.position, 15f);
            yield return new WaitForSeconds(9f);
            if (npc != null) npc.Leave(true);
            yield return new WaitForSeconds(3f);
            if (player == null) yield break;
            Give(player, BrimHeart);
            if (TextViewer.instance != null) TextViewer.instance.ShowText(TextViewer.Style.Rune, lore, lore + "_text", true);
        }

        // What he says as the store opens, while a step is waiting; his last words for now once all are done.
        internal static string Reminder()
        {
            if (!Key(FirstTold)) return "$iov_story_q1_wait";
            foreach (Step step in Steps)
                if (!Key(step.Done)) return "$" + step.Lines + "wait";
            return "$iov_story_later";
        }

        // ---- the old harbour's beacon ----

        internal static bool InOldHarbour(Vector3 point)
        {
            return InLocation(point, OldHarbour);
        }

        static bool InLocation(Vector3 point, string location)
        {
            Location here = Location.GetLocation(point, false);
            return here != null && Utils.GetPrefabName(here.gameObject) == location;
        }

        internal static void HarbourBeaconLit(Vector3 beacon)
        {
            if (Key(HarbourLit)) return;
            Set(HarbourLit);
            Player player = Player.m_localPlayer;
            if (player != null && Vector3.Distance(player.transform.position, beacon) < 60f)
                player.Message(MessageHud.MessageType.TopLeft, "$iov_story_lit", 0, null, false);
        }

        // ---- Sigrún's fish ----

        const float FishChance = 0.35f; // for each fish caught around her islet,
        const int FishAtMost = 3;       // and at the latest with this one
        static int fishCaught;

        // A fish from the ground around Sigrún's islet: one of them swallowed her fragment long ago. Called
        // as the fish is landed; the fisher keeps the fish too.
        internal static void FromFish(Fish fish, Humanoid fisher)
        {
            Player player = fisher as Player;
            if (player == null || player != Player.m_localPlayer) return;
            bool there = SigrunWatersMarker.Near(fish.transform.position) || InLocation(player.transform.position, SigrunIslet) ||
                InLocation(fish.transform.position, SigrunIslet);
            if (!there) return;
            if (Key(SigrunDone) || Has(player, Piece2))
            {
                Plugin.Debug("Story: a fish from Sigrún's waters, but her fragment " + (Key(SigrunDone) ? "is already with Sævar" : "is already in your bag"));
                return;
            }
            fishCaught++;
            Plugin.Debug("Story: fish " + fishCaught + " from Sigrún's waters");
            if (fishCaught < FishAtMost && Random.value >= FishChance) return;
            fishCaught = 0;
            string name = Hand(player, Piece2);
            if (name != null) player.Message(MessageHud.MessageType.Center, Texts.Get("iov_story_swallowed", name), 0, null, false);
            Plugin.Debug("Story: the fish had Sigrún's fragment");
        }

        // ---- torn maps ----

        static float waitingSince = -100f;

        internal static bool IsMap(ItemDrop.ItemData item)
        {
            return MapOf(item) != null;
        }

        static string[] MapOf(ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab == null) return null;
            foreach (string[] map in Maps)
                if (item.m_dropPrefab.name == map[0]) return map;
            return null;
        }

        [HarmonyPatch(typeof(Humanoid), "UseItem")]
        static class UseMapPatch
        {
            static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
            {
                string[] map = MapOf(item);
                if (map == null) return true;
                if (__instance == Player.m_localPlayer && Time.time - waitingSince > 3f && ZRoutedRpc.instance != null)
                {
                    waitingSince = Time.time;
                    ZPackage request = new ZPackage();
                    request.Write(map[0]);
                    request.Write(__instance.transform.position);
                    ZRoutedRpc.instance.InvokeRoutedRPC(SeaChart.ServerPeer(), RequestRpc, request);
                }
                return false;
            }
        }

        // Server: where the map leads. The location that was built if there is one, else the candidate
        // closest to the reader (going there builds it, and the other candidates vanish). The pin is near it,
        // not on it: an old chart shows the way, not the spot.
        static void OnRequest(long sender, ZPackage request)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
            string mapName = request.ReadString();
            Vector3 from = request.ReadVector3();
            string[] map = null;
            foreach (string[] m in Maps) if (m[0] == mapName) map = m;
            if (map == null) return;
            bool found = false;
            ZoneSystem.LocationInstance best = new ZoneSystem.LocationInstance();
            foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.GetLocationList())
            {
                if (instance.m_location == null || instance.m_location.m_prefabName != map[1]) continue;
                if (!found || (instance.m_placed && !best.m_placed) ||
                    (instance.m_placed == best.m_placed && Commands.Distance(instance, from) < Commands.Distance(best, from))) best = instance;
                found = true;
            }
            ZPackage answer = new ZPackage();
            answer.Write(mapName);
            answer.Write(found);
            if (found)
            {
                float angle = (map[1].GetStableHashCode() & 0xffff) / 65535f * Mathf.PI * 2f;
                answer.Write(best.m_position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 70f);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, answer);
        }

        static void OnAnswer(long sender, ZPackage answer)
        {
            waitingSince = -100f;
            string mapName = answer.ReadString();
            Player player = Player.m_localPlayer;
            if (player == null || Minimap.instance == null) return;
            if (!answer.ReadBool())
            {
                player.Message(MessageHud.MessageType.Center, "$iov_story_nomap", 0, null, false);
                return;
            }
            Vector3 where = answer.ReadVector3();
            string pin = "";
            foreach (string[] m in Maps) if (m[0] == mapName) pin = m[2];
            if (!SeaChart.HasPin(where)) Minimap.instance.DiscoverLocation(where, Minimap.PinType.Icon3, pin, false);
            player.Message(MessageHud.MessageType.Center, "$iov_story_mapped", 0, null, false);
            if (InventoryGui.IsVisible()) InventoryGui.instance.Hide();
            Plugin.Instance.StartCoroutine(ShowLater(where));
        }

        static System.Collections.IEnumerator ShowLater(Vector3 where)
        {
            yield return new WaitForSeconds(0.3f);
            if (Minimap.instance != null && !InventoryGui.IsVisible()) Minimap.instance.ShowPointOnMap(where);
        }

        [HarmonyPatch(typeof(Game), "Start")]
        static class RpcPatch
        {
            static void Postfix()
            {
                ZRoutedRpc.instance.Register<ZPackage>(RequestRpc, OnRequest);
                ZRoutedRpc.instance.Register<ZPackage>(AnswerRpc, OnAnswer);
            }
        }

        // ---- the restless sea ----

        internal static bool CarriesPiece(Player player)
        {
            foreach (Step step in Steps)
                if (Has(player, step.Piece)) return true;
            return false;
        }

        internal static bool Calmed(Player player)
        {
            return calmEffect != 0 && player.GetSEMan().HaveStatusEffect(calmEffect);
        }

        internal static bool SerpentWarded(Player player)
        {
            SEMan effects = player.GetSEMan();
            return (wardEffect != 0 && effects.HaveStatusEffect(wardEffect)) || (heartEffect != 0 && effects.HaveStatusEffect(heartEffect));
        }

        // ---- Hafdís's shore: taking the fragment from the ashes wakes her crew ----

        static readonly string[] Crew = { "Charred_Melee", "Charred_Melee", "Charred_Archer", "Charred_Mage" };

        static readonly System.Reflection.FieldInfo pickedField = AccessTools.Field(typeof(Pickable), "m_picked");

        // Interact's result doesn't say whether it was picked (it's whether to play the animation), so the
        // prefix notes whether there was still something to take.
        [HarmonyPatch(typeof(Pickable), "Interact")]
        static class AshPiecePatch
        {
            static void Prefix(Pickable __instance, bool repeat, out bool __state)
            {
                __state = !repeat && Utils.GetPrefabName(__instance.gameObject) == AshPiece && pickedField != null && !(bool)pickedField.GetValue(__instance);
            }

            static void Postfix(Pickable __instance, Humanoid character, bool __state)
            {
                if (!__state || character != Player.m_localPlayer || ZNetScene.instance == null) return;
                Vector3 at = __instance.transform.position;
                for (int i = 0; i < Crew.Length; i++)
                {
                    GameObject prefab = ZNetScene.instance.GetPrefab(Crew[i]);
                    if (prefab == null) continue;
                    float angle = (i * 90f + 45f) * Mathf.Deg2Rad;
                    Vector3 p = at + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 5f + Vector3.up * 1.5f;
                    GameObject go = Object.Instantiate(prefab, p, Quaternion.LookRotation(at - p));
                    Character c = go.GetComponent<Character>();
                    if (c != null) c.SetLevel(2);
                }
                character.Message(MessageHud.MessageType.Center, "$iov_story_ashes", 0, null, false);
                Plugin.Debug("Story: Hafdís's crew wakes");
            }
        }

        // ---- the hidden islet: a keeper falls ----

        internal static Keeper KeeperOf(Character character)
        {
            if (character == null) return null;
            string name = Utils.GetPrefabName(character.gameObject);
            foreach (Keeper keeper in Keepers)
                if (keeper.Prefab == name) return keeper;
            return null;
        }

        // The next keeper still standing, or null when all three have fallen.
        internal static Keeper NextKeeper()
        {
            foreach (Keeper keeper in Keepers)
                if (!Key(keeper.Down) && PrefabManager.Instance.GetPrefab(keeper.Prefab) != null) return keeper;
            return null;
        }

        [HarmonyPatch(typeof(Character), "OnDeath")]
        static class KeeperDeathPatch
        {
            static void Prefix(Character __instance)
            {
                Keeper keeper = KeeperOf(__instance);
                if (keeper == null) return;
                Set(keeper.Down);
                global::IslandsOfValheim.FinalStand.Fallen(); // the class, not the prefab name
                Player player = Player.m_localPlayer;
                if (player != null && Vector3.Distance(player.transform.position, __instance.transform.position) < 60f)
                    player.Message(MessageHud.MessageType.Center, Texts.Get("iov_story_says", keeper.Name, "$" + keeper.Lines + "death"), 0, null, false);
            }
        }

        // The serpent charm: serpents' bites on its wearer count half. On the player's own client, where the
        // damage is applied.
        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        static class SerpentWardPatch
        {
            static void Prefix(Character __instance, HitData hit)
            {
                Player player = __instance as Player;
                if (player == null || player != Player.m_localPlayer || hit == null || !SerpentWarded(player)) return;
                Character attacker = hit.GetAttacker();
                if (attacker != null && attacker.m_name == "$enemy_serpent") hit.ApplyModifier(0.5f);
            }
        }

        // The keepers' blows don't throw the player off their small islet into the sea (Hafdís's spin).
        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        static class KeeperPushPatch
        {
            static void Prefix(Character __instance, HitData hit)
            {
                if (hit == null || __instance != Player.m_localPlayer || KeeperOf(hit.GetAttacker()) == null) return;
                hit.m_pushForce *= 0.25f;
            }
        }
    }

    // The hidden islet's keepers, one after the other: Grímr, Ormr, Hafdís. The client that owns the marker
    // brings the next one while a player is near and none is standing (each fall is a world key, so it
    // carries on where it left off). Every client shows their words over them: a line as each comes, and
    // Hafdís's offer while she hasn't been struck.
    class FinalStand : MonoBehaviour
    {
        const float Near = 35f;    // a player this close wakes the next keeper
        const float Search = 300f; // a keeper this far off still counts as standing (Hafdís flies)
        const float After = 8f;    // seconds between one falling and the next coming
        static float lastFall = -100f;

        ZNetView nview;
        bool playerWasNear;
        Character current;
        int spoken;
        float nextLine;

        void Awake()
        {
            nview = GetComponent<ZNetView>();
            if (nview == null || nview.GetZDO() == null) return;
            InvokeRepeating("Tick", 10f, 2f); // gives a standing keeper time to load with the zone
        }

        internal static void Fallen() { lastFall = Time.time; }

        void Tick()
        {
            if (ZNetScene.instance == null || !nview.IsValid()) return;
            Story.Keeper keeper = Story.NextKeeper();
            Character standing = keeper != null ? Find(keeper) : null;
            bool near = Player.IsPlayerInRange(transform.position, Near);
            if (keeper != null && standing == null && !coming && nview.IsOwner() && near && playerWasNear && Time.time - lastFall > After)
                StartCoroutine(Come(keeper));
            playerWasNear = near;
            Speak(keeper, standing);
            if (keeper != null && standing != null && keeper.Prefab == Story.Hafdis) Awaken(keeper, standing);
        }

        // Hafdís struck: she's an enemy like any other from now on, and comes for whoever is near. Her side
        // is a plain field, so every client turns it; the owner of her AI sets it hunting.
        static void Awaken(Story.Keeper keeper, Character hafdis)
        {
            if (hafdis.m_faction == keeper.Faction) return;
            BaseAI ai = hafdis.GetBaseAI();
            bool struck = hafdis.GetHealth() < hafdis.GetMaxHealth() - 0.5f || (ai != null && ai.IsAggravated());
            if (!struck) return;
            hafdis.m_faction = keeper.Faction;
            ZNetView view = hafdis.GetComponent<ZNetView>();
            if (ai != null && view != null && view.IsValid() && view.IsOwner()) ai.SetHuntPlayer(true);
            Plugin.Debug("Hidden islet: Hafdís fights");
        }

        Character Find(Story.Keeper keeper)
        {
            foreach (Character c in Character.GetAllCharacters())
                if (c != null && !c.IsDead() && Utils.GetPrefabName(c.gameObject) == keeper.Prefab &&
                    Vector3.Distance(c.transform.position, transform.position) < Search) return c;
            return null;
        }

        bool coming;

        // A keeper doesn't just appear: a mist gathers at the back of the hall first, and it steps out of it.
        System.Collections.IEnumerator Come(Story.Keeper keeper)
        {
            coming = true;
            GameObject prefab = ZNetScene.instance.GetPrefab(keeper.Prefab);
            Vector3 at = transform.position + transform.rotation * new Vector3(0f, 0.5f, 1.5f);
            Effect("vfx_prespawn", at);
            Effect("fx_frostcloud", at);
            yield return new WaitForSeconds(2.5f);
            coming = false;
            if (prefab == null || !nview.IsValid()) yield break;
            Player closest = Player.GetClosestPlayer(at, Near * 2f);
            Vector3 to = closest != null ? closest.transform.position - at : -transform.forward;
            to.y = 0f;
            GameObject go = Instantiate(prefab, at, to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to) : transform.rotation);
            Character c = go.GetComponent<Character>();
            if (c != null) c.SetLevel(keeper.Level);
            // Grímr and Ormr know why you came: they turn on you at once, rather than idling about first.
            BaseAI ai = go.GetComponent<BaseAI>();
            if (ai != null && keeper.Prefab != Story.Hafdis) ai.Alert();
            Effect("vfx_spawn_large", at);
            Plugin.Debug("Hidden islet: " + keeper.Prefab + " comes");
        }

        static void Effect(string name, Vector3 at)
        {
            GameObject fx = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (fx != null) Instantiate(fx, at, Quaternion.identity);
        }

        const float ReachHeight = 3f;  // Hafdís stays at most this far above her hall's floor,
        const float ReachRadius = 9f;  // and this close to its middle: within a sword's reach, over the islet

        // On the client that runs the keeper (what it does there, everyone sees): Hafdís is kept low over
        // her hall, so she can be fought without wings; and while she speaks her offer, she looks at you.
        // (Done after everything else each frame, so neither vanilla's flying nor other mods' AI tweaks,
        // like hovering after an attack, carry her out of reach.)
        void LateUpdate()
        {
            if (current == null || current.IsDead()) return;
            ZNetView view = current.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            Story.Keeper keeper = Story.KeeperOf(current);
            if (keeper == null || keeper.Prefab != Story.Hafdis) return;
            KeepInReach(current);
            Player player = Player.m_localPlayer;
            if (player == null || current.m_faction == keeper.Faction) return; // struck: she fights as she will
            Vector3 to = player.transform.position - current.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f || to.sqrMagnitude > 30f * 30f) return;
            Quaternion want = Quaternion.LookRotation(to.normalized);
            current.transform.rotation = Quaternion.RotateTowards(current.transform.rotation, want, 240f * Time.deltaTime);
            BaseAI ai = current.GetBaseAI();
            if (ai != null) ai.LookTowards(to.normalized);
        }

        void KeepInReach(Character c)
        {
            Vector3 centre = transform.position;
            Vector3 p = c.transform.position;
            Vector3 want = p;
            float top = centre.y + ReachHeight;
            if (p.y > top) want.y = Mathf.MoveTowards(p.y, top, 10f * Time.deltaTime);
            Vector3 flat = new Vector3(p.x - centre.x, 0f, p.z - centre.z);
            if (flat.magnitude > ReachRadius)
            {
                Vector3 edge = centre + flat.normalized * ReachRadius;
                want.x = Mathf.MoveTowards(p.x, edge.x, 10f * Time.deltaTime);
                want.z = Mathf.MoveTowards(p.z, edge.z, 10f * Time.deltaTime);
            }
            if (want == p) return;
            c.transform.position = want;
            Rigidbody body = c.GetComponent<Rigidbody>();
            if (body == null) return;
            body.position = want;
            Vector3 v = body.linearVelocity;
            if (want.y < p.y && v.y > 0f) v.y = 0f;
            body.linearVelocity = v;
        }

        void Speak(Story.Keeper keeper, Character standing)
        {
            Player player = Player.m_localPlayer;
            if (keeper == null || standing == null || player == null || Chat.instance == null) return;
            if (standing != current) { current = standing; spoken = 0; nextLine = 0f; }
            if (Time.time < nextLine || Vector3.Distance(player.transform.position, standing.transform.position) > 25f) return;
            string line = null;
            if (spoken == 0) line = "$" + keeper.Lines + "spawn";
            else if (keeper.Prefab == Story.Hafdis && spoken < 3 && !Struck(standing)) line = "$" + keeper.Lines + "offer" + (spoken + 1);
            if (line == null) return;
            Chat.instance.SetNpcText(standing.gameObject, Vector3.up * (standing.GetHeight() + 0.6f), 40f, 7f, "", line, false);
            spoken++;
            nextLine = Time.time + 8f;
        }

        static bool Struck(Character c)
        {
            BaseAI ai = c.GetBaseAI();
            return ai != null && ai.IsAggravated();
        }
    }

    // The water around Sigrún's islet, where the fish with her fragment swims: loaded markers, to tell
    // whether a fish was caught there.
    class SigrunWatersMarker : MonoBehaviour
    {
        const float Radius = 70f;
        static readonly List<SigrunWatersMarker> loaded = new List<SigrunWatersMarker>();

        void OnEnable() { loaded.Add(this); }
        void OnDisable() { loaded.Remove(this); }

        internal static bool Near(Vector3 point)
        {
            foreach (SigrunWatersMarker marker in loaded)
                if (marker != null && Utils.DistanceXZ(marker.transform.position, point) < Radius) return true;
            return false;
        }
    }

    // While the local player carries a fragment (and hasn't calmed the water with a storm glass): a storm
    // over them wherever they go, and now and then a serpent at sea (not while a serpent charm is on them).
    // Each client does this for its own player; the serpents are ordinary networked creatures.
    //
    // The game blends light and fog from one weather to the next, but rain starts and stops at once, so the
    // storm comes in steps (light rain, rain, thunderstorm) and leaves the same way.
    class RestlessSea : MonoBehaviour
    {
        const float SerpentEvery = 120f, SerpentChance = 0.5f, SerpentDistance = 45f;
        const float StepSeconds = 20f;  // between one step of the weather and the next
        const float Blend = 12f;        // how long each step takes to blend in
        static readonly string[] Steps = { "", "LightRain", "Rain", "ThunderStorm" };

        bool active;
        int level, wanted;
        float nextStep, nextSerpent;
        float savedTransition = -1f;

        void Start()
        {
            InvokeRepeating("Tick", 2f, 2f);
        }

        void Tick()
        {
            Player player = Player.m_localPlayer;
            bool carrying = player != null && !player.IsDead() && Story.CarriesPiece(player) && !Story.Calmed(player);
            if (carrying != active)
            {
                active = carrying;
                wanted = active ? Steps.Length - 1 : 0;
                nextStep = 0f;
                if (player != null) player.Message(MessageHud.MessageType.Center, active ? "$iov_sea_restless" : "$iov_sea_calm", 0, null, false);
                nextSerpent = Time.time + SerpentEvery * 0.5f;
                Plugin.Debug("Restless sea " + (active ? "on" : "off"));
            }
            if (level != wanted && Time.time >= nextStep)
            {
                level += wanted > level ? 1 : -1;
                while (level > 0 && level < Steps.Length - 1 && !Exists(Steps[level])) level += wanted > level ? 1 : -1;
                Weather(level == 0 ? "" : Steps[level]);
                nextStep = Time.time + StepSeconds;
            }
            else if (level == wanted && savedTransition >= 0f && Time.time >= nextStep && EnvMan.instance != null)
            {
                EnvMan.instance.m_transitionDuration = savedTransition; // back to the game's own once the change is done
                savedTransition = -1f;
            }
            if (!active || Time.time < nextSerpent) return;
            nextSerpent = Time.time + SerpentEvery;
            if (Random.value < SerpentChance && !Story.SerpentWarded(player)) SendSerpent(player);
        }

        static bool Exists(string environment)
        {
            if (EnvMan.instance == null) return false;
            foreach (EnvSetup env in EnvMan.instance.m_environments)
                if (env != null && env.m_name == environment) return true;
            return false;
        }

        void Weather(string environment)
        {
            EnvMan env = EnvMan.instance;
            if (env == null) return;
            if (savedTransition < 0f) savedTransition = env.m_transitionDuration;
            env.m_transitionDuration = Mathf.Max(savedTransition, Blend);
            env.SetForceEnvironment(environment);
            Plugin.Debug("Restless sea weather: " + (environment == "" ? "the world's own" : environment));
        }

        static void SendSerpent(Player player)
        {
            if (ZoneSystem.instance == null || ZNetScene.instance == null || WorldGenerator.instance == null) return;
            if (Ship.GetLocalShip() == null && !player.IsSwimming()) return;
            Vector3 at = player.transform.position;
            if (WorldGenerator.instance.GetBiome(at) != Heightmap.Biome.Ocean) return;
            foreach (Character c in Character.GetAllCharacters())
                if (c.m_name == "$enemy_serpent" && Vector3.Distance(c.transform.position, at) < 120f) return; // one is enough
            GameObject serpent = ZNetScene.instance.GetPrefab("Serpent");
            if (serpent == null) return;
            float water = ZoneSystem.instance.m_waterLevel;
            for (int i = 0; i < 8; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 p = at + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SerpentDistance;
                if (WorldGenerator.instance.GetBiome(p) != Heightmap.Biome.Ocean || ZoneSystem.instance.GetGroundHeight(p) > water - 12f) continue;
                Object.Instantiate(serpent, new Vector3(p.x, water - 1f, p.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                Plugin.Debug("Restless sea: a serpent at " + p.ToString("F0"));
                return;
            }
        }
    }
}
