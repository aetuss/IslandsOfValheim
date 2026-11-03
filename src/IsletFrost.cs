using UnityEngine;

namespace IslandsOfValheim
{
    // Deep North islets: what the frost took. The stone of the frost caves' builders, ice grown over it,
    // and the dead frozen where they stood.
    static partial class IsletBuilder
    {
        static readonly string[] iceShards = { "IceShard_01", "IceShard_02", "IceShard_03", "IceShard_04", "IceShard_05", "IceShard_06", "ice_rock1" };

        // A walled stone keep around a slab floor, ice breaking through its walls.
        static void FrostKeep()
        {
            for (int ix = -2; ix <= 2; ix += 2)
                for (int iz = -2; iz <= 2; iz += 2)
                    if (!(ix == -2 && iz == -2)) At("MountainKit_int_floor_2x2", new Vector3(ix, FloorTop, iz), Vector3.zero, true);
            float b = FloorTop - 0.05f;
            At("MountainKit_int_wall_4x4", new Vector3(0f, b, 3.4f), Vector3.zero, false);
            At("MountainKit_int_wall_4x2", new Vector3(-3.4f, b, 1f), new Vector3(0f, 90f, 0f), false);
            At("MountainKit_int_wall_2x4", new Vector3(3.4f, b, 2f), new Vector3(0f, 90f, 0f), false);
            // The gate: a wall on each side of the way in, the rest of the south wall lies in the snow.
            At("MountainKit_int_wall_2x4", new Vector3(-2f, b, -3.4f), Vector3.zero, false);
            At("MountainKit_int_wall_4x2", new Vector3(4.6f, PlateauHeight - 0.2f, -2.5f), new Vector3(0f, 30f, 85f), false);
            // Ice grew through the walls, and two of those who held the keep froze inside.
            Ice(new Vector2(4.8f, 0.6f), 20f, 2.8f);
            Ice(new Vector2(-4.6f, -3.2f), 140f, 2.4f);
            Ice(new Vector2(-4.4f, 3.9f), 250f, 2.2f);
            PropAtMost("FrozenSkeleton_Pose1", new Vector2(1.8f, -0.6f), 200f, 0.05f, 2.2f);
            PropAtMost("FrozenGD", new Vector2(-1.8f, -1.6f), 40f, 0.05f, 2f);
            // Someone still keeps a light at the gate.
            Prop(new[] { "deepnorth_lantern_standing", "dvergrprops_lantern_standing" }, new Vector2(1.6f, -4.4f), 0f, 0.05f);
            CourtyardGuardsAndChest(FloorTop, new Vector3(-2f, FloorTop, 1.4f), 90f);
        }

        // A hunters' camp the frost took: a timber lean-to, a fire long gone cold, its people frozen where they sat.
        static void FrozenCamp()
        {
            float g = PlateauHeight - 0.05f;
            float back = Mathf.Min(Top(At("woodwall", new Vector3(-1f, g, 3f), Vector3.zero, false), g + 2f),
                Top(At("woodwall", new Vector3(1f, g, 3f), Vector3.zero, false), g + 2f));
            At("woodwall", new Vector3(-2f, g, 2f), new Vector3(0f, 90f, 0f), false);
            At("woodwall", new Vector3(2f, g, 2f), new Vector3(0f, 90f, 0f), false);
            // The roof gave way under the snow: one half still rests on the back wall, the other came down.
            At("wood_roof", new Vector3(0f, back, 2f), new Vector3(0f, 180f, 0f), false);
            At("wood_roof", new Vector3(0.4f, g, 0.8f), new Vector3(12f, 15f, 0f), false);
            Prop(new[] { "fire_pit" }, new Vector2(0f, -1.6f), 0f, 0.05f);
            Prop(new[] { "wood_frost_stack", "wood_stack" }, new Vector2(-3.4f, 2.6f), 90f, 0.05f);
            PropAtMost("prop_bed02", new Vector2(-0.8f, 2f), 90f, 0.05f, 2.4f);
            PropAtMost("FrozenSkeleton_Pose2", new Vector2(1.4f, -2.4f), 300f, 0.05f, 2f);
            PropAtMost("FrozenSkeleton_Pose1", new Vector2(-1.5f, -2.2f), 60f, 0.05f, 2.2f);
            Ice(new Vector2(4.2f, 2.8f), 70f, 2.6f);
            Ice(new Vector2(-4.5f, -2.5f), 190f, 2.2f);
            Ice(new Vector2(3.6f, -4.2f), 310f, 2f);
            CourtyardGuardsAndChest(PlateauHeight, new Vector3(0.9f, PlateauHeight, 2.1f), 180f);
        }

        // Graves on the cliff ridge, ice standing over them.
        static void FrozenGraves()
        {
            Vector2 c; Quaternion turn; float b;
            RidgeSpot(2.5f, out c, out turn, out b);
            float[,] spots = { { -1.4f, 0f }, { 0f, 0.3f }, { 1.4f, -0.1f } };
            for (int i = 0; i < spots.GetLength(0); i++)
            {
                Vector3 off = turn * new Vector3(spots[i, 0], 0f, spots[i, 1]);
                Prop(new[] { "MountainGraveStone01", "marker01" }, new Vector2(c.x + off.x, c.y + off.z), turn.eulerAngles.y + i * 9f, 0.15f);
            }
            Vector3 dead = turn * new Vector3(0.6f, 0f, -1.4f);
            PropAtMost("FrozenSkeleton_Pose2", new Vector2(c.x + dead.x, c.y + dead.z), turn.eulerAngles.y + 180f, 0.05f, 2f);
            Vector3 ice = turn * new Vector3(-1.8f, 0f, 1.2f);
            Ice(new Vector2(c.x + ice.x, c.y + ice.z), turn.eulerAngles.y, 2.4f);
        }

        // An ice shard, at most size metres across; which one depends on where it is.
        static void Ice(Vector2 at, float rotY, float size)
        {
            int pick = Mathf.Abs(Mathf.RoundToInt(at.x * 7f + at.y * 13f)) % iceShards.Length;
            PropAtMost(iceShards[pick], at, rotY, 0.2f, size);
        }
    }
}
