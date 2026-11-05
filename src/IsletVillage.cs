using UnityEngine;

namespace IslandsOfValheim
{
    // What's left of the people who lived on a large islet: houses fallen in, a well or a graveyard, the
    // ashes of their fire and a broken watchtower. Each is laid out around (0, 0) with its front (doorway)
    // towards -z, and built inside a Shifted group that puts it in its place on the plateau.
    static partial class IsletBuilder
    {
        // A 6 x 8 m plank house: a floor with boards missing, the back wall whole, the side walls broken,
        // a doorway at the front, its roof come down inside and a wall fallen outside. On stilts in the Swamp.
        static void RuinedHouse(bool stilts)
        {
            float floor = stilts ? PlateauHeight + 1f : PlateauHeight + 0.1f;
            for (int ix = -2; ix <= 2; ix += 2)
                for (int iz = -3; iz <= 3; iz += 2)
                {
                    if ((ix == 2 && iz == 3) || (ix == -2 && iz == -3)) continue;
                    At("wood_floor", new Vector3(ix, floor, iz), Vector3.zero, true);
                }
            if (stilts)
                for (int ix = -3; ix <= 3; ix += 6)
                    for (int iz = -4; iz <= 4; iz += 4)
                    {
                        float ground = GroundMin(ix, iz, 0.2f) - 0.5f;
                        At("wood_pole2", new Vector3(ix, Mathf.Min(ground, floor - 2f), iz), Vector3.zero, false);
                    }
            float b = floor - 0.05f;
            // Back wall whole, one board on top still standing.
            float back = b + 2f;
            for (int ix = -2; ix <= 2; ix += 2) back = Mathf.Min(back, Top(At("woodwall", new Vector3(ix, b, 4f), Vector3.zero, false), b + 2f));
            At("wood_wall_half", new Vector3(-2f, back, 4f), Vector3.zero, false);
            // Side walls with gaps.
            At("woodwall", new Vector3(-3f, b, 3f), new Vector3(0f, 90f, 0f), false);
            At("woodwall", new Vector3(-3f, b, -1f), new Vector3(0f, 90f, 0f), false);
            At("woodwall", new Vector3(-3f, b, -3f), new Vector3(0f, 90f, 0f), false);
            At("woodwall", new Vector3(3f, b, 1f), new Vector3(0f, 90f, 0f), false);
            At("wood_wall_half", new Vector3(3f, b, -3f), new Vector3(0f, 90f, 0f), false);
            // Front: a doorway in the middle.
            At("woodwall", new Vector3(-2f, b, -4f), Vector3.zero, false);
            At("wood_wall_half", new Vector3(2f, b, -4f), Vector3.zero, false);
            // A beam still across the back, the roof long gone (its beams lie about), and a wall lying outside.
            At("wood_beam", new Vector3(0f, back, 3.6f), Vector3.zero, false);
            At("wood_beam", new Vector3(0.8f, floor, 0.6f), new Vector3(0f, 35f, 0f), false);
            At("wood_beam", new Vector3(-1.2f, floor, -1.4f), new Vector3(0f, 100f, 0f), false);
            At("woodwall", new Vector3(5.2f, PlateauHeight - 0.1f, 2.6f), new Vector3(0f, 80f, 84f), false);
            At("wood_beam", new Vector3(-4.6f, PlateauHeight - 0.05f, -2f), new Vector3(0f, 25f, 0f), false);
            if (!stilts) Prop(new[] { "barrell_static", "barrell" }, new Vector2(-1.8f, 2.6f), 30f, 0.05f);
            Prop(new[] { "wood_stack" }, new Vector2(4.4f, -1.2f), 90f, 0.05f);
        }

        // A stone well with its winch frame, a bucket's barrel beside it.
        static void Well()
        {
            float g = PlateauHeight - 0.05f;
            At("stone_wall_1x1", new Vector3(0f, g, 0.75f), Vector3.zero, false);
            At("stone_wall_1x1", new Vector3(0f, g, -0.75f), Vector3.zero, false);
            At("stone_wall_1x1", new Vector3(0.75f, g, 0f), new Vector3(0f, 90f, 0f), false);
            At("stone_wall_1x1", new Vector3(-0.75f, g, 0f), new Vector3(0f, 90f, 0f), false);
            float left = Top(At("wood_pole2", new Vector3(-1.4f, g, 0f), Vector3.zero, false), g + 2f);
            float right = Top(At("wood_pole2", new Vector3(1.4f, g, 0f), Vector3.zero, false), g + 2f);
            Across("wood_beam", new Vector3(0f, Mathf.Min(left, right) - 0.2f, 0f));
            Prop(new[] { "barrell_static", "barrell" }, new Vector2(1.6f, -1.4f), 0f, 0.05f);
        }

        // The ashes of their fire with the logs they sat on, and what they left lying about.
        static void Campfire()
        {
            Prop(new[] { "fire_pit" }, Vector2.zero, 0f, 0.05f);
            At("wood_beam", new Vector3(0f, PlateauHeight - 0.05f, 1.8f), new Vector3(0f, 90f, 0f), false);
            At("wood_beam", new Vector3(1.8f, PlateauHeight - 0.05f, -0.4f), new Vector3(0f, 10f, 0f), false);
            At("wood_beam", new Vector3(-1.7f, PlateauHeight - 0.05f, -0.6f), new Vector3(0f, 160f, 0f), false);
            Prop(new[] { "barrell_static", "barrell" }, new Vector2(-2.4f, 1.6f), 40f, 0.05f);
            Prop(new[] { "wood_stack" }, new Vector2(2.4f, 1.8f), 30f, 0.05f);
        }

        // Their graves in two rows, a fence round them fallen in places.
        static void Graveyard()
        {
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 3; i++)
                    Prop(new[] { (i + row) % 2 == 0 ? "marker01" : "marker02", "marker01" }, new Vector2(-1.6f + i * 1.6f, -0.9f + row * 2f), i * 9f - 6f, 0.15f);
            Prop(new[] { "crypt_skeleton_laying" }, new Vector2(2.6f, -2.2f), 120f, 0.05f);
            float g = PlateauHeight - 0.05f;
            At("wood_fence", new Vector3(0f, g, 2.6f), Vector3.zero, false);
            At("wood_fence", new Vector3(-3.2f, g, 0.2f), new Vector3(0f, 90f, 0f), false);
            At("wood_fence", new Vector3(3.2f, g, 0.8f), new Vector3(0f, 90f, 6f), false);
        }

        // ---- the Plains: a Fuling stronghold ----

        // A palisade 13 m out with a gate between two totems and a few breaches, three huts inside facing
        // the middle, a great fire with a totem by it, their beds, rubbish and banners, and the Fulings.
        static void FulingStronghold()
        {
            float g = PlateauHeight - 0.05f;
            const float Wall = 13f;
            for (int i = 0; i < 30; i++)
            {
                float deg = i * 12f;
                if (Mathf.Abs(Mathf.DeltaAngle(deg, 270f)) < 10f || i % 11 == 5) continue; // the gate, and breaches
                Vector2 p = Polar(deg, Wall);
                At("goblin_fence", new Vector3(p.x, Height(p.x, p.y) - 0.2f, p.y), new Vector3(0f, -(deg + 90f), 0f), false);
            }
            Vector2 gl = Polar(258f, Wall + 0.6f), gr = Polar(282f, Wall + 0.6f);
            At("goblin_totempole", new Vector3(gl.x, g, gl.y), new Vector3(0f, 180f, 0f), false);
            At("goblin_totempole", new Vector3(gr.x, g, gr.y), new Vector3(0f, 180f, 0f), false);
            Vector2 banner = Polar(270f, Wall - 1.5f);
            At("goblin_banner", new Vector3(banner.x - 2.4f, g, banner.y), new Vector3(0f, 180f, 0f), false);
            At("goblin_banner", new Vector3(banner.x + 2.4f, g, banner.y), new Vector3(0f, 180f, 0f), false);
            // Their huts, round the inside of the wall.
            Village(40f, 8.5f, FulingHut);
            Village(150f, 8.5f, FulingHut);
            Village(210f, 8.5f, FulingHut);
            // The great fire in the middle.
            Prop(new[] { "fire_pit" }, Vector2.zero, 0f, 0.05f);
            At("goblin_totempole", new Vector3(0f, g, 2.6f), Vector3.zero, false);
            Prop(new[] { "goblin_strawpile" }, new Vector2(-6f, -3f), 30f, 0.1f);
            Prop(new[] { "goblin_strawpile" }, new Vector2(5.5f, -4f), 200f, 0.1f);
            Prop(new[] { "goblin_trashpile" }, new Vector2(-9f, 4.5f), 60f, 0.1f);
            Prop(new[] { "goblin_trashpile" }, new Vector2(9.5f, -1f), 140f, 0.1f);
            // Who holds it, and what they've piled up.
            float y = PlateauHeight + 0.2f;
            PlaceNetworked(IsletTier.SpawnerName("Goblin"), new Vector3(3f, y, -2f), 0f);
            PlaceNetworked(IsletTier.SpawnerName("Goblin"), new Vector3(-3f, y, -1f), 0f);
            PlaceNetworked(IsletTier.SpawnerName("GoblinArcher"), new Vector3(0f, y, -8f), 0f);
            PlaceNetworked(IsletTier.SpawnerName("GoblinShaman"), new Vector3(-1.5f, y, 3.5f), 0f);
            PlaceNetworked(IsletTier.SpawnerName("Goblin"), new Vector3(6f, y, 6f), 0f);
            PlaceNetworked(tier.ChestName, new Vector3(1.4f, PlateauHeight, 4f), 180f);
        }

        // A Fuling hut: two walls of rough logs at the back and sides, poles, a steep roof, straw inside.
        static void FulingHut()
        {
            float b = PlateauHeight - 0.05f;
            float back = Mathf.Min(Top(At("goblin_woodwall_2m", new Vector3(-1f, b, 1f), Vector3.zero, false), b + 2f),
                Top(At("goblin_woodwall_2m", new Vector3(1f, b, 1f), Vector3.zero, false), b + 2f));
            At("goblin_woodwall_2m_ribs", new Vector3(-2f, b, 0f), new Vector3(0f, 90f, 0f), false);
            At("goblin_woodwall_2m", new Vector3(2f, b, 0f), new Vector3(0f, 90f, 0f), false);
            At("goblin_pole", new Vector3(-2f, b, 1f), Vector3.zero, false);
            At("goblin_pole", new Vector3(2f, b, 1f), Vector3.zero, false);
            At("goblin_roof_45d", new Vector3(0f, back, 0.5f), Vector3.zero, false);
            At("goblin_roof_45d", new Vector3(0f, back, -0.5f), new Vector3(0f, 180f, 0f), false);
            Prop(new[] { "goblin_strawpile" }, new Vector2(0f, 0.2f), 0f, 0.1f);
        }

        // ---- the Ashlands: a charred fortress ----

        // Tall broken grausten walls round a 20 m square, a gate in the front, a corner tower at each
        // corner, the ruined keep in the middle (with its guards and chest), and the charred dead at the
        // gate and on the walls.
        static void CharredFortress()
        {
            float g = PlateauHeight - 0.05f;
            const float Half = 10f;
            string[] walls = { "Ashlands_Ruins_Wall_Broken3_4x6", "Ashlands_Ruins_Wall_Window_4x6_broken2" };
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90f;
                for (int k = -2; k <= 2; k++)
                {
                    if (side == 0 && k == 0) continue; // the gate
                    Vector3 along = Quaternion.Euler(0f, yaw, 0f) * new Vector3(k * 4f, 0f, -Half);
                    if ((side * 5 + k + 10) % 7 == 3)
                    {
                        // A stretch that fell: its blocks lie in the ash.
                        At("Piece_grausten_wall_2x2", new Vector3(along.x * 1.12f, PlateauHeight - 0.15f, along.z * 1.12f), new Vector3(0f, yaw + 20f, 85f), false);
                        continue;
                    }
                    At(walls[(side + k + 4) % 2], new Vector3(along.x, g, along.z), new Vector3(0f, yaw, 0f), false);
                }
            }
            for (int cx = -1; cx <= 1; cx += 2)
                for (int cz = -1; cz <= 1; cz += 2)
                {
                    float x = cx * (Half + 0.5f), z = cz * (Half + 0.5f);
                    float top = Top(At("Ashlands_Fortress_Wall_Pillar_base", new Vector3(x, g, z), Vector3.zero, false), g + 2f);
                    if (cx > 0 || cz > 0) top = Top(At("Ashlands_Fortress_Wall_Pillar", new Vector3(x, top, z), Vector3.zero, false), top + 4f);
                    if (cx > 0 && cz > 0) At("Ashlands_Fortress_Wall_PillarTop", new Vector3(x, top, z), Vector3.zero, false);
                }
            GraustenFort(); // the keep, with its guards and chest
            float y = PlateauHeight + 0.2f;
            PlaceNetworked(IsletTier.SpawnerName("Charred_Melee"), new Vector3(-2.5f, y, -Half + 2f), 0f);
            PlaceNetworked(IsletTier.SpawnerName("Charred_Archer"), new Vector3(Half - 2f, y, Half - 2f), 0f);
            Prop(new[] { "grausten_pile" }, new Vector2(-7f, -6f), 0f, 0.2f);
            Prop(new[] { "grausten_pile" }, new Vector2(7.5f, 5f), 80f, 0.2f);
            Prop(new[] { "ashland_pot1_red" }, new Vector2(6f, -7f), 0f, 0.02f);
            Prop(new[] { "ashland_pot2_green" }, new Vector2(-6.5f, 7f), 30f, 0.02f);
            Prop(new[] { "ashland_pot3_red" }, new Vector2(7f, -6.2f), 60f, 0.02f);
        }

        // A spire of black marble, the dvergr's own landmark: a base, two tall columns and the tip.
        static void MarbleSpire()
        {
            float g = PlateauHeight - 0.05f;
            float top = Top(At("blackmarble_base_1", new Vector3(0f, g, 0f), Vector3.zero, false), g + 1f);
            top = Top(At("blackmarble_column_3", new Vector3(0f, top, 0f), Vector3.zero, false), top + 3f);
            top = Top(At("blackmarble_column_3", new Vector3(0f, top, 0f), Vector3.zero, false), top + 3f);
            At("blackmarble_tip", new Vector3(0f, top, 0f), Vector3.zero, false);
            Prop(new[] { "blackmarble_pile" }, new Vector2(1.6f, -1.2f), 30f, 0.2f);
            Prop(new[] { "blackmarble_head01", "blackmarble_pile" }, new Vector2(-1.8f, 1f), 140f, 0.2f);
        }

        // A stone watchtower four storeys high, broken down to one corner at the top, rubble at its foot.
        static void WatchTower()
        {
            float g = PlateauHeight - 0.05f;
            // Ground storey: a doorway at the front.
            float back = Top(At("stone_wall_4x2", new Vector3(0f, g, 2f), Vector3.zero, false), g + 2f);
            float left = Top(At("stone_wall_4x2", new Vector3(-2f, g, 0f), new Vector3(0f, 90f, 0f), false), g + 2f);
            float right = Top(At("stone_wall_4x2", new Vector3(2f, g, 0f), new Vector3(0f, 90f, 0f), false), g + 2f);
            float door = Top(At("stone_wall_1x1", new Vector3(-1.5f, g, -2f), Vector3.zero, false), g + 1f);
            At("stone_wall_1x1", new Vector3(1.5f, g, -2f), Vector3.zero, false);
            At("stone_wall_1x1", new Vector3(-1.5f, door, -2f), Vector3.zero, false);
            // Second storey: three sides.
            back = Top(At("stone_wall_4x2", new Vector3(0f, back, 2f), Vector3.zero, false), back + 2f);
            left = Top(At("stone_wall_4x2", new Vector3(-2f, left, 0f), new Vector3(0f, 90f, 0f), false), left + 2f);
            right = Top(At("stone_wall_4x2", new Vector3(2f, right, 0f), new Vector3(0f, 90f, 0f), false), right + 2f);
            // Third: the back and the left.
            back = Top(At("stone_wall_4x2", new Vector3(0f, back, 2f), Vector3.zero, false), back + 2f);
            left = Top(At("stone_wall_2x1", new Vector3(-2f, left, 1f), new Vector3(0f, 90f, 0f), false), left + 1f);
            At("stone_wall_2x1", new Vector3(2f, right, 1f), new Vector3(0f, 90f, 0f), false);
            // Top: one corner left standing, a pillar on it.
            float corner = Top(At("stone_wall_2x1", new Vector3(-1f, back, 2f), Vector3.zero, false), back + 1f);
            At("stone_pillar", new Vector3(-1.6f, corner, 2f), Vector3.zero, false);
            // Rubble.
            At("stone_wall_1x1", new Vector3(3.4f, PlateauHeight - 0.15f, -1.6f), new Vector3(12f, 40f, 80f), false);
            At("stone_wall_2x1", new Vector3(-3.6f, PlateauHeight - 0.15f, -2.4f), new Vector3(80f, 70f, 0f), false);
            Prop(new[] { "stone_wall_1x1" }, new Vector2(0.6f, -3.6f), 30f, 0.2f);
        }
    }
}
