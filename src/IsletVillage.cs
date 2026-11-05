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
            // A beam still across the back, the roof fallen in, and a wall lying outside.
            At("wood_beam", new Vector3(0f, back, 3.6f), Vector3.zero, false);
            At("wood_roof", new Vector3(0.6f, floor, 0.8f), new Vector3(12f, 20f, 0f), false);
            At("wood_roof", new Vector3(-0.8f, floor, -1.6f), new Vector3(-9f, 190f, 4f), false);
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
