using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace IslandsOfValheim
{
    // The kinds of islet. Each ring of the world (band) has its own: the further from the centre, the
    // tougher the guards and the better the loot, roughly following the biome order. A ring can hold two
    // kinds, side by side (Mistlands: Seeker ruins and dvergr outposts) or split between the north and the
    // south of the world (the outer ring: Deep North and Ashlands). Everything that differs between kinds
    // is here; the layout itself is the same (IsletBuilder).
    class IsletTier
    {
        internal enum Half { Any, North, South }

        internal string Name;
        internal int Band;                     // which ring: kinds in the same ring count as equally tough
        internal Half Side = Half.Any;
        internal string Note = "";             // for the config description
        internal Heightmap.Biome Biome;        // how the ground is painted
        internal float MinDistance, MaxDistance;
        internal int DefaultCount;
        internal float Seed;                   // shifts the noise, so each tier's islet has its own shape
        internal bool Ruined = true;           // vanilla's random damage to the pieces, so they look old
        internal bool DvergrOutpost;           // dvergr live here: see DvergrOutposts
        internal string[] Raiders;             // on some islets these attack it as the player arrives

        // Five guard slots: three in the courtyard, one by the wreck, one by the tower.
        internal string[] Guards;
        internal int MinLevel = 1, MaxLevel = 2;
        internal float LevelUpChance = 15f;

        internal string[] ChestBases;
        internal int DropMin = 3, DropMax = 5;
        internal List<Loot> Loot = new List<Loot>();

        internal IsletBuilder.RuinStyle Style;
        internal string Nest;          // vanilla creature nest used instead of a ruin on some islets; null: none
        internal int NestCount = 1;
        internal string[] NestProps;   // static clutter around the nest
        internal string RuneStone;
        internal string JettyFloor = "wood_floor", JettyPole = "wood_pole2";
        internal string[] Wreck = KarveWreck;
        internal string[] BigRocks = { "rock4_coast", "rock1_mountain", "Rock_7", "rock3_mountain", "Rock_3" };
        internal string[] SmallRocks = { "Rock_3", "rock4_coast", "rock1_mountain" };
        internal string[] ShoreIce;    // ice on the shore, where the sea freezes

        // The old beacon tower some islets have (IsletBeacons): a base block and a pillar of the tier's
        // stone with a brazier on top, what lighting it costs (any one of the options) and who comes for it.
        internal string BeaconBase = "stone_floor_2x2", BeaconPillar = "stone_pillar", BeaconRubble = "stone_wall_1x1";
        internal string BeaconBrazier = "piece_brazierfloor01";
        internal List<Cost[]> BeaconFuel = new List<Cost[]>();
        internal List<WaveGroup> BeaconWave = new List<WaveGroup>();

        static readonly string[] KarveWreck = { "shipwreck_karve_bow", "shipwreck_karve_stern", "shipwreck_karve_bottomboards" };
        static readonly string[] LongshipWreck = { "shipwreck_vikingship_front", "shipwreck_vikingship_rear", "shipwreck_vikingship_mast1" };

        // Candidate lists: the first prefab that exists is used.
        internal string[] Trees, SmallTrees, Bushes, Berries, Mushrooms;

        internal ConfigEntry<int> Count;

        internal string LocationName(int variant) { return LocationPrefix + (variant + 1); }
        internal string LocationPrefix { get { return "IoV_Islet_" + Name; } }
        internal string ChestName { get { return "IoV_TreasureChest_" + Name; } }
        internal string HoardName { get { return "IoV_TreasureHoard_" + Name; } }
        internal static string SpawnerName(string creature) { return "IoV_Spawner_" + creature; }

        IsletTier Drop(string item, int min, int max, float weight)
        {
            Loot.Add(new Loot { Item = item, Min = min, Max = max, Weight = weight });
            return this;
        }

        // One way to pay for lighting the beacon.
        IsletTier Fuel(params object[] itemsAndAmounts)
        {
            Cost[] option = new Cost[itemsAndAmounts.Length / 2];
            for (int i = 0; i < option.Length; i++)
                option[i] = new Cost { Item = (string)itemsAndAmounts[i * 2], Amount = (int)itemsAndAmounts[i * 2 + 1] };
            BeaconFuel.Add(option);
            return this;
        }

        // fromSky: they fly in (or drop) instead of climbing out of the sea.
        IsletTier Wave(string creature, int count, bool fromSky)
        {
            BeaconWave.Add(new WaveGroup { Creature = creature, Count = count, FromSky = fromSky });
            return this;
        }

        internal static readonly List<IsletTier> All = new List<IsletTier>();

        static IsletTier()
        {
            All.Add(new IsletTier
            {
                Name = "Meadows", Band = 0, Biome = Heightmap.Biome.Meadows, MinDistance = 300f, MaxDistance = 2200f, DefaultCount = 20, Seed = 0f,
                Style = IsletBuilder.RuinStyle.StoneHall, Nest = "Spawner_GreydwarfNest", NestProps = new[] { "root07", "root08" }, RuneStone = "RuneStone_Meadows",
                Guards = new[] { "Greydwarf", "Greydwarf", "Greydwarf_Shaman", "Greydwarf", "Greydwarf" },
                ChestBases = new[] { "TreasureChest_meadows" },
                Trees = new[] { "FirTree_small_dead", "FirTree_small", "Pinetree_01" },
                SmallTrees = new[] { "Beech_small1", "Beech_small2", "Bush01" },
                Bushes = new[] { "Bush01", "Bush02_en", "shrub_2" },
                Berries = new[] { "RaspberryBush" },
                Mushrooms = new[] { "Pickable_Mushroom" },
            }.Drop("Coins", 20, 45, 3f).Drop("Amber", 1, 3, 2f).Drop("AmberPearl", 1, 2, 1.5f).Drop("Ruby", 1, 2, 0.8f)
             .Drop("ArrowFire", 10, 20, 1f).Drop("Resin", 5, 10, 1f).Drop("Feathers", 4, 8, 1f)
             .Fuel("Wood", 10, "Resin", 5).Wave("Neck", 4, false));

            All.Add(new IsletTier
            {
                Name = "Swamp", Band = 1, Biome = Heightmap.Biome.Swamp, MinDistance = 2200f, MaxDistance = 4000f, DefaultCount = 25, Seed = 137f,
                Style = IsletBuilder.RuinStyle.StiltHut, Nest = "BonePileSpawner_swamp", NestProps = new[] { "bone_stack", "crypt_skeleton_laying" }, RuneStone = "RuneStone_Swamps",
                Guards = new[] { "Draugr", "Draugr", "Draugr_Ranged", "Draugr", "Draugr_Elite" },
                MaxLevel = 2, LevelUpChance = 10f,
                ChestBases = new[] { "TreasureChest_swamp", "TreasureChest_meadows" },
                Trees = new[] { "SwampTree1", "SwampTree2", "FirTree_small_dead" },
                SmallTrees = new[] { "SwampTree2", "FirTree_small_dead" },
                Bushes = new[] { "Bush02_en", "shrub_2", "Bush01" },
                Berries = new[] { "BlueberryBush", "RaspberryBush" },
                Mushrooms = new[] { "Pickable_Mushroom" },
            }.Drop("Coins", 30, 60, 3f).Drop("IronScrap", 3, 6, 2f).Drop("AmberPearl", 1, 3, 1.5f).Drop("Ruby", 1, 2, 1f)
             .Drop("Chain", 1, 1, 0.8f).Drop("ArrowIron", 10, 20, 1f).Drop("WitheredBone", 1, 2, 1f)
             .Fuel("SurtlingCore", 1, "Coal", 5).Wave("Draugr", 3, false).Wave("Draugr_Ranged", 1, false));

            All.Add(new IsletTier
            {
                Name = "Plains", Band = 2, Biome = Heightmap.Biome.Plains, MinDistance = 4000f, MaxDistance = 5800f, DefaultCount = 25, Seed = 271f,
                Style = IsletBuilder.RuinStyle.FulingCamp, Wreck = LongshipWreck, RuneStone = "RuneStone_Plains",
                Guards = new[] { "Goblin", "Goblin", "GoblinShaman", "GoblinArcher", "Goblin" },
                LevelUpChance = 10f,
                ChestBases = new[] { "TreasureChest_plains_stone", "TreasureChest_heath", "TreasureChest_meadows" },
                Trees = new[] { "Birch1", "Birch2", "Birch1_aut", "FirTree_small_dead" },
                SmallTrees = new[] { "Birch2", "Birch1", "Bush01_heath" },
                Bushes = new[] { "Bush01_heath", "shrub_2", "Bush01" },
                Berries = new[] { "CloudberryBush", "RaspberryBush" },
                Mushrooms = new[] { "Pickable_Barley_Wild", "Pickable_Flax_Wild", "Pickable_Mushroom" },
            }.Drop("Coins", 60, 120, 3f).Drop("BlackMetalScrap", 3, 6, 2f).Drop("SilverNecklace", 1, 1, 1.2f).Drop("Ruby", 1, 3, 1.2f)
             .Drop("AmberPearl", 2, 3, 1f).Drop("ArrowNeedle", 10, 20, 1f).Drop("ArrowObsidian", 10, 20, 1f)
             .Fuel("SurtlingCore", 2, "Coal", 10).Wave("Deathsquito", 2, true).Wave("Goblin", 2, false));

            All.Add(new IsletTier
            {
                Name = "Mistlands", Band = 3, BeaconBase = "blackmarble_2x2x1", BeaconPillar = "blackmarble_column_1", BeaconRubble = "blackmarble_2x1x1", BeaconBrazier = "piece_brazierfloor02",
                Biome = Heightmap.Biome.Mistlands, MinDistance = 5800f, MaxDistance = 7500f, DefaultCount = 25, Seed = 409f,
                Style = IsletBuilder.RuinStyle.DvergrRuin, Wreck = LongshipWreck, Nest = "SeekerEgg", NestCount = 4, NestProps = new[] { "blackmarble_pile", "dvergrprops_crate" }, RuneStone = "RuneStone_Mistlands",
                Guards = new[] { "Seeker", "Seeker", "SeekerBrute", "Seeker", "Seeker" },
                MaxLevel = 2, LevelUpChance = 10f,
                ChestBases = new[] { "TreasureChest_dvergrtower", "TreasureChest_meadows" },
                Trees = new[] { "YggaShoot1", "YggaShoot2", "YggaShoot3", "FirTree_small_dead" },
                SmallTrees = new[] { "YggaShoot_small1", "YggaShoot2", "Bush01" },
                Bushes = new[] { "shrub_2", "Bush02_en", "Bush01" },
                Berries = new[] { "Pickable_Mushroom_JotunPuffs", "Pickable_Mushroom_Magecap" },
                Mushrooms = new[] { "Pickable_Mushroom_Magecap", "Pickable_Mushroom" },
            }.Drop("Coins", 100, 180, 3f).Drop("Softtissue", 3, 6, 2f).Drop("BlackMarble", 4, 8, 1.5f).Drop("BlackCore", 1, 1, 0.6f)
             .Drop("SilverNecklace", 1, 1, 1f).Drop("Ruby", 2, 3, 1f).Drop("ArrowCarapace", 10, 20, 1f)
             .Fuel("BlackCore", 1).Fuel("SurtlingCore", 3).Wave("Tick", 2, true).Wave("Seeker", 2, false));

            // Dvergr live on these, in the same ring as the Seeker islets: leave them in peace, or raid them.
            All.Add(new IsletTier
            {
                Name = "Dvergr", Band = 3, BeaconBase = "blackmarble_2x2x1", BeaconPillar = "blackmarble_column_1", BeaconRubble = "blackmarble_2x1x1", BeaconBrazier = "piece_brazierfloor02",
                Biome = Heightmap.Biome.Mistlands, MinDistance = 5800f, MaxDistance = 7500f, DefaultCount = 8, Seed = 463f,
                Note = " Dvergr outposts share the Mistlands ring with the Mistlands islets.",
                Style = IsletBuilder.RuinStyle.DvergrCamp, Wreck = LongshipWreck, RuneStone = "RuneStone_Mistlands",
                Ruined = false, DvergrOutpost = true, Raiders = new[] { "Seeker", "Seeker", "SeekerBrute" },
                Guards = new[] { "Dverger", "DvergerMageFire", "DvergerMageSupport", "Dverger", "DvergerMageIce" },
                MaxLevel = 2, LevelUpChance = 10f, DropMin = 4, DropMax = 6,
                ChestBases = new[] { "TreasureChest_dvergrtower", "TreasureChest_meadows" },
                Trees = new[] { "YggaShoot1", "YggaShoot2", "YggaShoot3", "FirTree_small_dead" },
                SmallTrees = new[] { "YggaShoot_small1", "YggaShoot2", "Bush01" },
                Bushes = new[] { "shrub_2", "Bush02_en", "Bush01" },
                Berries = new[] { "Pickable_Mushroom_JotunPuffs", "Pickable_Mushroom_Magecap" },
                Mushrooms = new[] { "Pickable_Mushroom_Magecap", "Pickable_Mushroom" },
            }.Drop("Coins", 150, 250, 3f).Drop("BlackCore", 1, 2, 1.2f).Drop("DvergrNeedle", 1, 1, 0.8f).Drop("Lantern", 1, 1, 0.5f)
             .Drop("Softtissue", 4, 8, 1.5f).Drop("Iron", 4, 8, 1.2f).Drop("Copper", 6, 10, 1f).Drop("YggdrasilWood", 6, 10, 1f).Drop("Ruby", 2, 3, 1f)
             .Fuel("Lantern", 1).Fuel("BlackCore", 2).Wave("Seeker", 3, false).Wave("SeekerBrute", 1, false));

            // The outer ring is split like the world itself: Ashlands to the south, Deep North to the north.
            All.Add(new IsletTier
            {
                Name = "Ashlands", Band = 4, BeaconBase = "Piece_grausten_floor_2x2", BeaconPillar = "Piece_grausten_pillarbase_medium", BeaconRubble = "Piece_grausten_wall_1x2",
                Side = Half.South, Biome = Heightmap.Biome.AshLands, MinDistance = 7500f, MaxDistance = 10000f, DefaultCount = 15, Seed = 557f,
                Note = " Only in the southern half of the world (the Deep North islets take the north).",
                Style = IsletBuilder.RuinStyle.GraustenFort, Wreck = LongshipWreck, Nest = "GraveStone_CharredTwitcherNest", NestCount = 2, NestProps = new[] { "grausten_pile", "ashland_pot2_red" }, RuneStone = "RuneStone_Ashlands",
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
             .Drop("ArrowCharred", 10, 20, 1f).Drop("CharredBone", 3, 6, 1f)
             .Fuel("MoltenCore", 1).Fuel("SurtlingCore", 5).Wave("Volture", 2, true).Wave("Charred_Melee", 2, false));

            All.Add(new IsletTier
            {
                Name = "DeepNorth", Band = 4, BeaconBrazier = "piece_brazierfloor02",
                Side = Half.North, Biome = Heightmap.Biome.DeepNorth, MinDistance = 7500f, MaxDistance = 10000f, DefaultCount = 15, Seed = 613f,
                Note = " Only in the northern half of the world (the Ashlands islets take the south).",
                Style = IsletBuilder.RuinStyle.FrostKeep, Wreck = LongshipWreck, RuneStone = "RuneStone_DeepNorth",
                Guards = new[] { "Skeleton_DeepNorth", "Ulv", "Fenring_Cultist", "Ulv", "Fenring" },
                MaxLevel = 2, LevelUpChance = 15f,
                ChestBases = new[] { "TreasureChest_deepnorth_village", "TreasureChest_mountains", "TreasureChest_meadows" },
                BigRocks = new[] { "Rock_4_deepnorth", "rock4_coast" },
                SmallRocks = new[] { "Rock_3_deepnorth", "Rock_3" },
                ShoreIce = new[] { "ice_rock1", "ice1" },
                Trees = new[] { "Pinetree_Snow", "SnowFirTree_small", "FirTree_small_dead" },
                SmallTrees = new[] { "SnowFirTree_small", "Pinetree_Snow_dead", "FirTree_small_dead" },
                Bushes = new[] { "Bush01_deepnorth", "shrub_2", "Bush01" },
                Berries = new[] { "LingonberryBush", "BlueberryBush" },
                Mushrooms = new[] { "Pickable_Branch_Snow", "Pickable_Stone" },
            }.Drop("Coins", 180, 300, 3f).Drop("Frostwood", 6, 12, 1.5f).Drop("Ice", 4, 8, 1.5f).Drop("FrostCore", 1, 1, 0.6f)
             .Drop("Crystal", 2, 4, 1f).Drop("Silver", 3, 6, 1f).Drop("FreezeGland", 2, 4, 1f).Drop("SpiceDeepNorth", 1, 2, 0.8f)
             .Drop("ArrowFrost", 10, 20, 1f)
             .Fuel("Frostwood", 10, "SurtlingCore", 2).Wave("Ulv", 3, false).Wave("Fenring_Cultist", 1, false));
        }

        // Whether a kind may be placed at this point. The game puts the Deep North beyond 12 km from a point
        // 4 km south of the centre, and the Ashlands beyond 12 km from a point 4 km north of it
        // (WorldGenerator.IsDeepnorth / IsAshlands), so the line between the two halves runs east-west through
        // the centre. Expand World Size moves both the same way, so the line stays where it is.
        internal bool Allows(Vector3 point)
        {
            if (Side == Half.North) return point.z > 0f;
            if (Side == Half.South) return point.z <= 0f;
            return true;
        }

        internal static void BindConfig(ConfigFile config)
        {
            foreach (IsletTier tier in All)
                tier.Count = config.Bind("1 - World generation", tier.Name + "Islets", tier.DefaultCount,
                    new ConfigDescription("How many " + tier.Name + "-level islets the game tries to place, " +
                        (int)tier.MinDistance + "-" + (int)tier.MaxDistance + " m from the world centre (in a vanilla-sized world; " +
                        "Expand World Size moves the rings with its world radius)." + tier.Note + " 0 turns them off. " +
                        "Only used when a world's locations are generated (a new world, or the genloc command). Needs a restart.",
                        new AcceptableValueRange<int>(0, 200)));
        }

        // The ring a point this far from the world centre is in, for this world's size.
        internal static int BandAt(float distance)
        {
            float scale = WorldSize.Scale;
            foreach (IsletTier tier in All)
                if (distance < tier.MaxDistance * scale) return tier.Band;
            return All[All.Count - 1].Band;
        }

        // All the vanilla objects any tier places that may drop themselves onto the terrain (see IsletPrefabs).
        internal static IEnumerable<string> AllPlants()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (IsletTier tier in All)
            {
                foreach (string[] list in new[] { tier.Trees, tier.SmallTrees, tier.Bushes, tier.Berries, tier.Mushrooms })
                    foreach (string name in list)
                        if (seen.Add(name)) yield return name;
                if (tier.Nest != null && seen.Add(tier.Nest)) yield return tier.Nest;
                if (tier.RuneStone != null && seen.Add(tier.RuneStone)) yield return tier.RuneStone;
            }
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

    class Cost
    {
        internal string Item;
        internal int Amount;
    }

    class WaveGroup
    {
        internal string Creature;
        internal int Count;
        internal bool FromSky;
    }
}
