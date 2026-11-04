using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The islets of Sævar's tale (see Story). Each is a unique location like his harbour: the game picks a
    // few possible spots, the first one somebody comes near is built and the others vanish, and his torn
    // maps point to whichever that is. Built from the same parts as the ordinary islets of their waters.
    static partial class IsletBuilder
    {
        const int StorySpots = 3;

        internal static void RegisterStoryIslets()
        {
            OldBrimHarbour();
            KetillsIslet();
            SigrunsIslet();
            OrmrsIslet();
            DvergrHold();
        }

        static void BeginStory(string name, IsletTier kind, int variant, float seedShift)
        {
            tier = kind;
            locationName = name;
            SetVariant(variant);
            seed += seedShift;
            root = ZoneManager.Instance.CreateLocationContainer(locationName);
            parent = root.transform;
            built = 0;
            missing = 0;
            BuildGround();
            PlaceRocks();
        }

        static void EndStory(float minDistance, float maxDistance)
        {
            EndStory(minDistance, maxDistance, true);
        }

        // ruined: vanilla's random damage to the pieces, for a place long abandoned.
        static void EndStory(float minDistance, float maxDistance, bool ruined)
        {
            Plugin.Debug(locationName + " built: " + built + " parts, " + missing + " prefabs not found");
            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.BiomeArea = Heightmap.BiomeArea.Median;
            config.Quantity = StorySpots;
            config.Priotized = true;
            config.Unique = true;
            config.SnapToWater = true;
            config.RandomRotation = true;
            config.ClearArea = false;
            config.ExteriorRadius = 30f;
            config.MinAltitude = -1000f;
            config.MaxAltitude = -6f;
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 1000f;
            config.MinDistance = minDistance;
            config.MaxDistance = maxDistance;
            config.Group = locationName;
            config.MinDistanceFromSimilar = 600f;
            root.AddComponent<Location>().m_applyRandomDamage = ruined;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + locationName + " (" + StorySpots + " possible spots, one is kept)");
        }

        // The first harbour of the Brim Folk, in the Meadows waters: their stone hall and lookout, a broken
        // jetty, a wreck, and the beacon they lit so no crew would be lost, cold for a lifetime. Lighting it
        // is the first step of the tale. A stone by it tells what it was.
        static void OldBrimHarbour()
        {
            BeginStory(Story.OldHarbour, IsletTier.All[0], 2, 400f);
            StoneHall();
            StoneTower();
            RidgeGuard();
            float deg = cliffDeg - 28f;
            Vector2 at = Polar(deg, PlateauAt(deg) + 2.5f);
            Vector3 turn = new Vector3(0f, 90f - deg, 0f);
            float g = GroundMin(at.x, at.y, 1f) - 0.3f;
            float top = Top(At(tier.BeaconBase, new Vector3(at.x, g, at.y), turn, false), g + 1f);
            top = Top(At(tier.BeaconPillar, new Vector3(at.x, top, at.y), turn, false), top + 2f);
            At(IsletBeacons.PrefabName(tier), new Vector3(at.x, top, at.y), turn, false);
            float stone = cliffDeg - 52f;
            PlaceNetworked(new[] { "IoV_StoryStone_Harbour" }, Polar(stone, PlateauAt(stone) + 1.2f), 90f - stone, 0f); // its face towards the plateau
            PlaceDock(cliffDeg + 229f);
            PlaceWreck(cliffDeg + 194f);
            PlaceHints();
            PlacePlants();
            PlacePickables();
            PlaceOutsideGuard();
            EndStory(800f, IsletTier.All[0].MaxDistance);
        }

        // Where Ketill's crew turned on each other, in the Swamp waters: their wrecked ship, their camp gone
        // cold, their weapons where they fell, the stones they raised for each other, and the dead still on
        // their feet. Ketill walks among them, the first fragment about his neck.
        static void KetillsIslet()
        {
            BeginStory(Story.KetillIslet, IsletTier.All[1], 0, 600f);
            float floor = PlateauHeight;
            quietProps = true; // a dead camp: no glints or embers on its things
            Prop(new[] { "fire_pit" }, new Vector2(0.5f, -0.8f), 0f, 0.05f);
            Prop(new[] { "barrell_static", "barrell" }, new Vector2(-2.8f, 1.6f), 70f, 0.05f);
            Prop(new[] { "CargoCrate", "dvergrprops_crate" }, new Vector2(2.6f, 2.4f), 25f, 0.05f);
            Prop(new[] { "crypt_skeleton_laying" }, new Vector2(-1.6f, -2.6f), 40f, 0.05f);
            Prop(new[] { "crypt_skeleton_laying" }, new Vector2(2.2f, -1.9f), 210f, 0.05f);
            Prop(new[] { "bone_stack" }, new Vector2(-3.4f, -0.6f), 0f, 0.1f);
            // Their weapons, where they dropped them.
            Lying(new[] { "SwordBronze", "SwordIron" }, new Vector2(-0.9f, -2.2f), 30f, 1.4f);
            Lying(new[] { "AxeBronze", "AxeIron" }, new Vector2(1.6f, -1.2f), 140f, 1.2f);
            Lying(new[] { "SpearBronze", "SpearFlint" }, new Vector2(-2.4f, 0.4f), 80f, 2f);
            quietProps = false;
            // The stones they raised for each other, in a row towards the cliff.
            for (int i = 0; i < 3; i++)
            {
                float deg = cliffDeg - 40f + i * 40f;
                PlaceNetworked(new[] { "IoV_StoryStone_Ketill" + (i + 1) }, Polar(deg, PlateauAt(deg) - 0.8f), 90f - deg, 0f); // faces towards the camp
            }
            CourtyardGuardsAndChest(floor, new Vector3(-0.6f, floor, 3.4f), 180f);
            PlaceNetworked(Story.KetillSpawner, new Vector3(0f, floor + 0.2f, -1.6f), 0f);
            Graves();
            RidgeGuard();
            PlaceWreck(cliffDeg + 194f);
            PlaceDock(cliffDeg + 140f);
            PlaceHints();
            PlacePlants();
            PlacePickables();
            PlaceOutsideGuard();
            EndStory(IsletTier.All[1].MinDistance + 400f, IsletTier.All[1].MaxDistance);
        }

        // A row of the tale's stones along the plateau's edge towards the cliff, each facing the middle.
        static void StoryStones(string prefix, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float deg = cliffDeg - 40f + i * 40f;
                PlaceNetworked(new[] { prefix + (i + 1) }, Polar(deg, PlateauAt(deg) - 0.8f), 90f - deg, 0f);
            }
        }

        // Where Sigrún's crew died, in the Plains waters: Fulings have made a camp of it since, and her
        // crew's stones stand outside their palisade. The fish around it still have her fragment; there is a
        // fishing ground, and the old jetty to cast from.
        static void SigrunsIslet()
        {
            BeginStory(Story.SigrunIslet, IsletTier.All[2], 1, 800f);
            FulingCamp();
            FulingLookout();
            RidgeGuard();
            StoryStones("IoV_StoryStone_Sigrun", 3);
            quietProps = true; // her crew, where they fell
            float deg = cliffDeg + 205f;
            Prop(new[] { "crypt_skeleton_laying" }, Polar(deg, PlateauAt(deg) - 1.2f), deg, 0.05f);
            Lying(new[] { "SwordIron", "SwordBronze" }, Polar(deg + 9f, PlateauAt(deg + 9f) - 0.6f), deg + 60f, 1.4f);
            Prop(new[] { "crypt_skeleton_laying" }, Polar(deg - 150f, PlateauAt(deg - 150f) - 1.4f), deg + 100f, 0.05f);
            Lying(new[] { "AxeIron", "AxeBronze" }, Polar(deg - 141f, PlateauAt(deg - 141f) - 0.7f), deg + 20f, 1.2f);
            quietProps = false;
            PlaceNetworked(IsletFishing.PrefabName(tier), new Vector3(0f, PlateauHeight, 0f), 0f);
            PlaceNetworked(Story.SigrunWaters, new Vector3(0f, PlateauHeight, 0f), 0f);
            PlaceDock(cliffDeg + 229f);
            PlaceWreck(cliffDeg + 194f);
            PlaceHints();
            PlacePlants();
            PlacePickables();
            PlaceOutsideGuard();
            EndStory(IsletTier.All[2].MinDistance + 300f, IsletTier.All[2].MaxDistance);
        }

        // Where Ormr's crew died, in the Mistlands waters: the old marble ruin they camped in, his diary on
        // three stones, their bones, and his own chest with the fragment he hid, a Seeker Soldier by it.
        static void OrmrsIslet()
        {
            BeginStory(Story.OrmrIslet, IsletTier.All[3], 3, 1000f);
            DvergrRuin();
            PlaceNetworked(Story.OrmrChest, new Vector3(1.8f, FloorTop, -1.6f), 300f);
            PlaceNetworked(Story.OrmrGuard, new Vector3(2.4f, FloorTop + 0.2f, 0.2f), 0f);
            StoryStones("IoV_StoryStone_Ormr", 3);
            quietProps = true;
            Prop(new[] { "crypt_skeleton_laying" }, new Vector2(5.8f, 2.4f), 200f, 0.05f);
            Prop(new[] { "crypt_skeleton_laying" }, new Vector2(-5.2f, 4.2f), 30f, 0.05f);
            Lying(new[] { "SwordSilver", "SwordIron" }, new Vector2(-3.2f, -5.6f), 110f, 1.4f);
            Lying(new[] { "MaceSilver", "MaceIron" }, new Vector2(3.6f, -5.4f), 20f, 1.2f);
            quietProps = false;
            DvergrColumn();
            RidgeGuard();
            PlaceWreck(cliffDeg + 194f);
            PlaceDock(cliffDeg + 140f);
            PlaceHints();
            PlacePlants();
            PlacePickables();
            PlaceOutsideGuard();
            EndStory(IsletTier.All[3].MinDistance + 300f, IsletTier.All[3].MaxDistance);
        }

        // A dvergr outpost in the Mistlands waters, lived in like the others, where they keep the fragment
        // that washed up on their shore on a crate by their beds. It's theirs: taking it is stealing.
        static void DvergrHold()
        {
            BeginStory(Story.DvergrHold, IsletTier.All[4], 0, 1200f);
            DvergrCamp();
            PlaceDvergrWard();
            float g = PlateauHeight - 0.05f;
            GameObject crate = At("dvergrprops_crate", new Vector3(-0.6f, g, -3.6f), new Vector3(0f, 8f, 0f), false);
            if (crate != null)
            {
                Bounds b = SolidBounds(crate);
                PlaceNetworked(Story.RunePiece, new Vector3(b.center.x, b.max.y + 0.08f, b.center.z), 40f);
            }
            DvergrWatch();
            RidgeGuard();
            PlaceWreck(cliffDeg + 60f);
            PlaceDock(cliffDeg + 229f);
            PlaceHints();
            PlacePlants();
            PlacePickables();
            PlaceOutsideGuard();
            EndStory(IsletTier.All[4].MinDistance + 300f, IsletTier.All[4].MaxDistance, false);
        }
    }
}
