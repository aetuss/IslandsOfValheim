using System.Collections.Generic;
using BepInEx.Configuration;

namespace IslandsOfValheim
{
    // One kind of islet per ring of the world: the further from the centre, the tougher the guards and
    // the better the loot, roughly following the biome order. Everything that differs between tiers is
    // here; the layout itself is the same (IsletBuilder).
    class IsletTier
    {
        internal string Name;
        internal Heightmap.Biome Biome;        // how the ground is painted
        internal float MinDistance, MaxDistance;
        internal int DefaultCount;
        internal float Seed;                   // shifts the noise, so each tier's islet has its own shape

        // Five guard slots: three in the courtyard, one by the wreck, one by the tower.
        internal string[] Guards;
        internal int MinLevel = 1, MaxLevel = 2;
        internal float LevelUpChance = 15f;

        internal string[] ChestBases;
        internal int DropMin = 3, DropMax = 5;
        internal List<Loot> Loot = new List<Loot>();

        internal IsletBuilder.RuinStyle Style;
        internal string JettyFloor = "wood_floor", JettyPole = "wood_pole2";
        internal string[] Wreck = KarveWreck;

        static readonly string[] KarveWreck = { "shipwreck_karve_bow", "shipwreck_karve_stern", "shipwreck_karve_bottomboards" };
        static readonly string[] LongshipWreck = { "shipwreck_vikingship_front", "shipwreck_vikingship_rear", "shipwreck_vikingship_mast1" };

        // Candidate lists: the first prefab that exists is used.
        internal string[] Trees, SmallTrees, Bushes, Berries, Mushrooms;

        internal ConfigEntry<int> Count;

        internal string LocationName { get { return "IoV_Islet_" + Name; } }
        internal string ChestName { get { return "IoV_TreasureChest_" + Name; } }
        internal static string SpawnerName(string creature) { return "IoV_Spawner_" + creature; }

        IsletTier Drop(string item, int min, int max, float weight)
        {
            Loot.Add(new Loot { Item = item, Min = min, Max = max, Weight = weight });
            return this;
        }

        internal static readonly List<IsletTier> All = new List<IsletTier>();

        static IsletTier()
        {
            All.Add(new IsletTier
            {
                Name = "Meadows", Biome = Heightmap.Biome.Meadows, MinDistance = 300f, MaxDistance = 2200f, DefaultCount = 20, Seed = 0f,
                Style = IsletBuilder.RuinStyle.StoneHall,
                Guards = new[] { "Greydwarf", "Greydwarf", "Greydwarf_Shaman", "Greydwarf", "Greydwarf" },
                ChestBases = new[] { "TreasureChest_meadows" },
                Trees = new[] { "FirTree_small_dead", "FirTree_small", "Pinetree_01" },
                SmallTrees = new[] { "Beech_small1", "Beech_small2", "Bush01" },
                Bushes = new[] { "Bush01", "Bush02_en", "shrub_2" },
                Berries = new[] { "RaspberryBush" },
                Mushrooms = new[] { "Pickable_Mushroom" },
            }.Drop("Coins", 20, 45, 3f).Drop("Amber", 1, 3, 2f).Drop("AmberPearl", 1, 2, 1.5f).Drop("Ruby", 1, 2, 0.8f)
             .Drop("ArrowFire", 10, 20, 1f).Drop("Resin", 5, 10, 1f).Drop("Feathers", 4, 8, 1f));

            All.Add(new IsletTier
            {
                Name = "Swamp", Biome = Heightmap.Biome.Swamp, MinDistance = 2200f, MaxDistance = 4000f, DefaultCount = 25, Seed = 137f,
                Style = IsletBuilder.RuinStyle.StiltHut,
                Guards = new[] { "Draugr", "Draugr", "Draugr_Ranged", "Draugr", "Draugr_Elite" },
                MaxLevel = 2, LevelUpChance = 10f,
                ChestBases = new[] { "TreasureChest_swamp", "TreasureChest_meadows" },
                Trees = new[] { "SwampTree1", "SwampTree2", "FirTree_small_dead" },
                SmallTrees = new[] { "SwampTree2", "FirTree_small_dead" },
                Bushes = new[] { "Bush02_en", "shrub_2", "Bush01" },
                Berries = new[] { "BlueberryBush", "RaspberryBush" },
                Mushrooms = new[] { "Pickable_Mushroom" },
            }.Drop("Coins", 30, 60, 3f).Drop("IronScrap", 3, 6, 2f).Drop("AmberPearl", 1, 3, 1.5f).Drop("Ruby", 1, 2, 1f)
             .Drop("Chain", 1, 1, 0.8f).Drop("ArrowIron", 10, 20, 1f).Drop("WitheredBone", 1, 2, 1f));

            All.Add(new IsletTier
            {
                Name = "Plains", Biome = Heightmap.Biome.Plains, MinDistance = 4000f, MaxDistance = 5800f, DefaultCount = 25, Seed = 271f,
                Style = IsletBuilder.RuinStyle.FulingCamp, Wreck = LongshipWreck,
                Guards = new[] { "Goblin", "Goblin", "GoblinShaman", "GoblinArcher", "Goblin" },
                LevelUpChance = 10f,
                ChestBases = new[] { "TreasureChest_plains_stone", "TreasureChest_heath", "TreasureChest_meadows" },
                Trees = new[] { "Birch1", "Birch2", "Birch1_aut", "FirTree_small_dead" },
                SmallTrees = new[] { "Birch2", "Birch1", "Bush01_heath" },
                Bushes = new[] { "Bush01_heath", "shrub_2", "Bush01" },
                Berries = new[] { "CloudberryBush", "RaspberryBush" },
                Mushrooms = new[] { "Pickable_Barley_Wild", "Pickable_Flax_Wild", "Pickable_Mushroom" },
            }.Drop("Coins", 60, 120, 3f).Drop("BlackMetalScrap", 3, 6, 2f).Drop("SilverNecklace", 1, 1, 1.2f).Drop("Ruby", 1, 3, 1.2f)
             .Drop("AmberPearl", 2, 3, 1f).Drop("ArrowNeedle", 10, 20, 1f).Drop("ArrowObsidian", 10, 20, 1f));

            All.Add(new IsletTier
            {
                Name = "Mistlands", Biome = Heightmap.Biome.Mistlands, MinDistance = 5800f, MaxDistance = 7500f, DefaultCount = 25, Seed = 409f,
                Style = IsletBuilder.RuinStyle.DvergrRuin, Wreck = LongshipWreck,
                Guards = new[] { "Seeker", "Seeker", "SeekerBrute", "Seeker", "Seeker" },
                MaxLevel = 2, LevelUpChance = 10f,
                ChestBases = new[] { "TreasureChest_dvergrtower", "TreasureChest_meadows" },
                Trees = new[] { "YggaShoot1", "YggaShoot2", "YggaShoot3", "FirTree_small_dead" },
                SmallTrees = new[] { "YggaShoot_small1", "YggaShoot2", "Bush01" },
                Bushes = new[] { "shrub_2", "Bush02_en", "Bush01" },
                Berries = new[] { "Pickable_Mushroom_JotunPuffs", "Pickable_Mushroom_Magecap" },
                Mushrooms = new[] { "Pickable_Mushroom_Magecap", "Pickable_Mushroom" },
            }.Drop("Coins", 100, 180, 3f).Drop("Softtissue", 3, 6, 2f).Drop("BlackMarble", 4, 8, 1.5f).Drop("BlackCore", 1, 1, 0.6f)
             .Drop("SilverNecklace", 1, 1, 1f).Drop("Ruby", 2, 3, 1f).Drop("ArrowCarapace", 10, 20, 1f));

            All.Add(new IsletTier
            {
                Name = "Ashlands", Biome = Heightmap.Biome.AshLands, MinDistance = 7500f, MaxDistance = 10000f, DefaultCount = 20, Seed = 557f,
                Style = IsletBuilder.RuinStyle.GraustenFort, Wreck = LongshipWreck,
                JettyFloor = "ashwood_floor_2x2", JettyPole = "ashwood_pole_2m",
                Guards = new[] { "Charred_Melee", "Charred_Melee", "Charred_Mage", "Charred_Archer", "Charred_Melee" },
                MaxLevel = 2, LevelUpChance = 10f,
                ChestBases = new[] { "TreasureChest_charredfortress", "TreasureChest_meadows" },
                Trees = new[] { "AshlandsTree3", "AshlandsTree4", "AshlandsTree1", "FirTree_small_dead" },
                SmallTrees = new[] { "AshlandsTree5", "AshlandsTree6", "AshlandsBush1" },
                Bushes = new[] { "AshlandsBush1", "AshlandsBush2", "shrub_2" },
                Berries = new[] { "Pickable_SmokePuff", "Pickable_Fiddlehead" },
                Mushrooms = new[] { "Pickable_Fiddlehead", "Pickable_SmokePuff" },
            }.Drop("Coins", 150, 260, 3f).Drop("FlametalOreNew", 2, 4, 1.5f).Drop("Grausten", 6, 12, 1.5f).Drop("MoltenCore", 1, 1, 0.6f)
             .Drop("GemstoneRed", 1, 1, 0.5f).Drop("GemstoneGreen", 1, 1, 0.5f).Drop("GemstoneBlue", 1, 1, 0.5f)
             .Drop("ArrowCharred", 10, 20, 1f).Drop("CharredBone", 3, 6, 1f));
        }

        internal static void BindConfig(ConfigFile config)
        {
            foreach (IsletTier tier in All)
                tier.Count = config.Bind("1 - World generation", tier.Name + "Islets", tier.DefaultCount,
                    new ConfigDescription("How many " + tier.Name + "-level islets the game tries to place, " +
                        (int)tier.MinDistance + "-" + (int)tier.MaxDistance + " m from the world centre. 0 turns them off. " +
                        "Only used when a world's locations are generated (a new world, or the genloc command). Needs a restart.",
                        new AcceptableValueRange<int>(0, 200)));
        }

        // All the vanilla plants any tier may use.
        internal static IEnumerable<string> AllPlants()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (IsletTier tier in All)
                foreach (string[] list in new[] { tier.Trees, tier.SmallTrees, tier.Bushes, tier.Berries, tier.Mushrooms })
                    foreach (string name in list)
                        if (seen.Add(name)) yield return name;
            foreach (string name in new[] { "Pickable_Flint", "Pickable_Stone", "Pickable_Branch" })
                if (seen.Add(name)) yield return name;
        }
    }

    class Loot
    {
        internal string Item;
        internal int Min, Max;
        internal float Weight;
    }
}
