using UnityEngine;

namespace IslandsOfValheim
{
    // What was built on the islet, from that biome's own vanilla pieces: a main ruin on the plateau (with the
    // chest and three guards) and on most islets a smaller structure on the cliff ridge. Each islet gets one
    // of the tier's two ruin designs, turned one of two ways, or on some tiers a creature nest instead.
    // Building pieces are networked (they can be broken for their materials); props are static decor.
    static partial class IsletBuilder
    {
        internal enum RuinStyle { StoneHall, StiltHut, FulingCamp, DvergrRuin, GraustenFort }

        static void PlaceRuins()
        {
            switch (tier.Style)
            {
                case RuinStyle.StoneHall: MainRuin(StoneHall, FishingCamp); Ridge(StoneTower); break;
                case RuinStyle.StiltHut: MainRuin(StiltHut, SunkenShrine); Ridge(Graves); break;
                case RuinStyle.FulingCamp: MainRuin(FulingCamp, FulingRuin); Ridge(FulingLookout); break;
                case RuinStyle.DvergrRuin: MainRuin(DvergrRuin, DvergrOutpost); Ridge(DvergrColumn); break;
                case RuinStyle.GraustenFort: MainRuin(GraustenFort, AshlandsArches); Ridge(GraustenPillar); break;
            }
        }

        // Each design turned two ways; a nest instead on about one islet in four, where the tier has one.
        static void MainRuin(System.Action a, System.Action b)
        {
            if (tier.Nest == null)
                OneOf(Turned(a, 0f), Turned(a, 180f), Turned(b, 90f), Turned(b, 270f));
            else
                OneOf(new[] { 0.19f, 0.19f, 0.19f, 0.19f, 0.24f }, Turned(a, 0f), Turned(a, 180f), Turned(b, 90f), Turned(b, 270f), NestSite);
        }

        // Builds the design unturned (the layout code works in plateau coordinates), then turns its whole
        // group around the plateau's centre. The plateau is flat, so everything still sits on the ground.
        static System.Action Turned(System.Action design, float yaw)
        {
            return () => { design(); parent.localRotation = Quaternion.Euler(0f, yaw, 0f); };
        }

        // A creature nest (or a clutch of them) in a ring of rocks: break it before it sends more.
        static void NestSite()
        {
            Vector2[] spots;
            switch (tier.NestCount)
            {
                case 1: spots = new[] { Vector2.zero }; break;
                case 2: spots = new[] { new Vector2(-1.6f, 0.3f), new Vector2(1.7f, -0.4f) }; break;
                default: spots = new[] { new Vector2(-1.4f, 1.2f), new Vector2(1.5f, 1f), new Vector2(-1.2f, -1.5f), new Vector2(1.3f, -1.3f) }; break;
            }
            for (int i = 0; i < spots.Length; i++)
                PlaceNetworked(new[] { tier.Nest }, spots[i], i * 77f, 0f);
            float[] rocks = { 30f, 140f, 250f, 320f };
            for (int i = 0; i < rocks.Length; i++)
                OnGround(bigRocks, Polar(rocks[i], 4.8f), rocks[i] * 2f, 2f + (i % 2) * 0.8f, 0.4f);
            // The vanilla skull props are giants' skulls; small clutter of the tier's own fits better.
            PropAtMost(tier.NestProps[0], Polar(95f, 3.6f), 30f, 0.15f, 2.4f);
            PropAtMost(tier.NestProps[1], Polar(290f, 3.2f), 200f, 0.05f, 1.6f);
            Vector2 chest = Polar(200f, 3.2f);
            CourtyardGuardsAndChest(PlateauHeight, new Vector3(chest.x, PlateauHeight, chest.y), 20f);
        }

        // The ridge structure and its guard, on three islets out of four.
        static void Ridge(System.Action build)
        {
            Optional(0.75f, () => { build(); RidgeGuard(); });
        }

        // ---- Meadows: a stone hall and a lookout tower ----

        static void StoneHall()
        {
            // An 8x8 m stone courtyard with some tiles missing; the tiles are 1 m thick, so they sink into the ground.
            for (int ix = -3; ix <= 3; ix += 2)
                for (int iz = -3; iz <= 3; iz += 2)
                {
                    if ((ix == 3 && iz == 3) || (ix == -3 && iz == -1) || (ix == 1 && iz == -3) || (ix == 3 && iz == -1)) continue;
                    At("stone_floor_2x2", new Vector3(ix, FloorTop, iz), Vector3.zero, true);
                }
            // Tiles that slid off, tilted.
            At("stone_floor_2x2", new Vector3(6f, PlateauHeight + 0.25f, 3.5f), new Vector3(8f, 30f, -6f), true);
            At("stone_floor_2x2", new Vector3(-5.5f, PlateauHeight + 0.2f, -4.5f), new Vector3(-5f, 70f, 7f), true);

            float b = FloorTop - 0.05f;
            // North wall: two storeys, crumbling from the east end.
            At("stone_wall_4x2", new Vector3(-2f, b, 4f), Vector3.zero, false);
            At("stone_wall_4x2", new Vector3(2f, b, 4f), Vector3.zero, false);
            At("stone_wall_4x2", new Vector3(-2f, b + 2f, 4f), Vector3.zero, false);
            At("stone_wall_2x1", new Vector3(1f, b + 2f, 4f), Vector3.zero, false);
            At("stone_wall_1x1", new Vector3(-3.5f, b + 4f, 4f), Vector3.zero, false);
            // West wall: lower and broken in the middle.
            At("stone_wall_4x2", new Vector3(-4f, b, 2f), new Vector3(0f, 90f, 0f), false);
            At("stone_wall_2x1", new Vector3(-4f, b + 2f, 3f), new Vector3(0f, 90f, 0f), false);
            At("stone_wall_2x1", new Vector3(-4f, b, -1f), new Vector3(0f, 90f, 0f), false);
            At("stone_wall_1x1", new Vector3(-4f, b, -3.5f), new Vector3(0f, 90f, 0f), false);
            // East side: only pillars are left standing, one fell over.
            At("stone_pillar", new Vector3(4f, b, 4f), Vector3.zero, false);
            At("stone_pillar", new Vector3(4f, b + 2f, 4f), Vector3.zero, false);
            At("stone_pillar", new Vector3(4f, b, 0f), Vector3.zero, false);
            At("stone_pillar", new Vector3(5.6f, PlateauHeight - 0.1f, -1.5f), new Vector3(0f, 25f, 90f), false);
            // South gate: two tall pillars with an arch on top.
            At("stone_pillar", new Vector3(-1.5f, b, -4f), Vector3.zero, false);
            At("stone_pillar", new Vector3(-1.5f, b + 2f, -4f), Vector3.zero, false);
            At("stone_pillar", new Vector3(1.5f, b, -4f), Vector3.zero, false);
            At("stone_pillar", new Vector3(1.5f, b + 2f, -4f), Vector3.zero, false);
            At("stone_arch", new Vector3(0f, b + 4f, -4f), Vector3.zero, true);
            // Fallen blocks.
            At("stone_wall_1x1", new Vector3(-6f, PlateauHeight - 0.15f, 1f), new Vector3(10f, 35f, 80f), false);
            At("stone_wall_1x1", new Vector3(2.5f, PlateauHeight - 0.15f, 6.2f), new Vector3(-8f, 60f, 5f), false);

            CourtyardGuardsAndChest(FloorTop, new Vector3(-2f, FloorTop, 2.8f), 180f); // chest against the north wall
        }

        // A ruined lookout tower on the cliff ridge, its door facing the courtyard.
        static void StoneTower()
        {
            Vector2 c; Quaternion turn; float b;
            RidgeSpot(2.8f, out c, out turn, out b);
            // Local frame: +z points out to sea, -z (the door) back towards the courtyard.
            Local("stone_wall_4x2", c, turn, 0f, 1.5f, b, 0f);          // sea side
            Local("stone_wall_4x2", c, turn, -1.5f, 0f, b, 90f);        // left
            Local("stone_wall_4x2", c, turn, 1.5f, 0f, b, 90f);         // right
            Local("stone_wall_1x1", c, turn, -1.5f, -1.5f, b, 0f);      // door frame
            Local("stone_wall_1x1", c, turn, -1.5f, -1.5f, b + 1f, 0f);
            Local("stone_wall_1x1", c, turn, 1.5f, -1.5f, b, 0f);
            // Second storey, mostly gone.
            Local("stone_wall_4x2", c, turn, 0f, 1.5f, b + 2f, 0f);
            Local("stone_wall_4x2", c, turn, -1.5f, 0f, b + 2f, 90f);
            Local("stone_wall_2x1", c, turn, 1.5f, 1f, b + 2f, 90f);
            Local("stone_wall_1x1", c, turn, -0.5f, 1.5f, b + 4f, 0f);
            // Its wooden floor came down.
            LyingBeams("wood_beam", "wood_pole2");
        }

        // ---- Swamp: a hut on stilts, half fallen in, and a few graves ----

        static void StiltHut()
        {
            const float stilts = 1f; // low enough to jump onto
            float floor = PlateauHeight + stilts;
            // 4x6 m plank floor.
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -2; iz <= 2; iz += 2)
                    At("wood_floor", new Vector3(ix, floor, iz), Vector3.zero, true);
            // Stilts down into the mud.
            for (int ix = -2; ix <= 2; ix += 4)
                for (int iz = -3; iz <= 3; iz += 2)
                {
                    float ground = GroundMin(ix, iz, 0.2f) - 0.5f;
                    At("wood_pole2", new Vector3(ix, Mathf.Min(ground, floor - 2f), iz), Vector3.zero, false);
                }
            // Walls: whole on the north, gaps on the other sides, the south one has the doorway.
            At("woodwall", new Vector3(-1f, floor, 3f), Vector3.zero, false);
            At("woodwall", new Vector3(1f, floor, 3f), Vector3.zero, false);
            At("woodwall", new Vector3(-2f, floor, 1f), new Vector3(0f, 90f, 0f), false);
            At("woodwall", new Vector3(-2f, floor, -1f), new Vector3(0f, 90f, 0f), false);
            At("woodwall", new Vector3(2f, floor, 1f), new Vector3(0f, 90f, 0f), false);
            At("woodwall", new Vector3(-1f, floor, -3f), Vector3.zero, false);
            At("wood_wall_half", new Vector3(2f, floor, -2f), new Vector3(0f, 90f, 0f), false);
            // The roof fell in: one slope lies on the floor, one in the mud, a beam across the doorway.
            At("wood_roof", new Vector3(0.5f, floor, -0.5f), new Vector3(10f, 20f, 0f), false);
            At("wood_roof", new Vector3(4f, PlateauHeight - 0.1f, -1f), new Vector3(15f, 80f, 20f), false);
            At("wood_beam", new Vector3(1f, floor, -2.5f), new Vector3(0f, 30f, 0f), false);
            // A rotten fence and bones around it.
            At("wood_fence", new Vector3(-4.5f, PlateauHeight - 0.05f, -2f), new Vector3(0f, 80f, 0f), false);
            At("wood_fence", new Vector3(-4f, PlateauHeight - 0.05f, -5f), new Vector3(0f, 30f, 8f), false);
            Prop(new[] { "skull_pile" }, new Vector2(3.5f, 4.5f), 40f, 0.1f);
            Prop(new[] { "bone_stack" }, new Vector2(-4f, 3.5f), 110f, 0.1f);
            Prop(new[] { "crypt_skeleton_laying" }, new Vector2(3f, -4.5f), 200f, 0.05f);

            CourtyardGuardsAndChest(floor, new Vector3(-1f, floor, 2.2f), 180f);
        }

        static void Graves()
        {
            Vector2 c; Quaternion turn; float b;
            RidgeSpot(2.5f, out c, out turn, out b);
            float[,] spots = { { -1.5f, 0f }, { 0f, 0.3f }, { 1.5f, -0.2f }, { 0.7f, 1.6f } };
            for (int i = 0; i < spots.GetLength(0); i++)
            {
                Vector3 off = turn * new Vector3(spots[i, 0], 0f, spots[i, 1]);
                Prop(new[] { i % 2 == 0 ? "marker01" : "marker02", "marker01" }, new Vector2(c.x + off.x, c.y + off.z), turn.eulerAngles.y + i * 7f, 0.15f);
            }
            Prop(new[] { "crypt_skeleton_laying" }, c, turn.eulerAngles.y + 90f, 0.05f);
        }

        // ---- Plains: a Fuling camp behind a palisade, with a lookout of totems ----

        static void FulingCamp()
        {
            // Palisade ring with a gap to the south and a few fallen sections.
            for (int i = 0; i < 16; i++)
            {
                float deg = i * 22.5f;
                if (deg == 270f || i == 3 || i == 10) continue;
                Vector2 p = Polar(deg, 6.5f);
                At("goblin_fence", new Vector3(p.x, Height(p.x, p.y) - 0.2f, p.y), new Vector3(0f, -(deg + 90f), 0f), false);
            }
            // A hut against the north side.
            float b = PlateauHeight - 0.05f;
            At("goblin_woodwall_2m", new Vector3(-1f, b, 3.5f), Vector3.zero, false);
            At("goblin_woodwall_2m", new Vector3(1f, b, 3.5f), Vector3.zero, false);
            At("goblin_woodwall_2m_ribs", new Vector3(-2f, b, 2.5f), new Vector3(0f, 90f, 0f), false);
            At("goblin_woodwall_2m", new Vector3(2f, b, 2.5f), new Vector3(0f, 90f, 0f), false);
            At("goblin_pole", new Vector3(-2f, b, 3.5f), Vector3.zero, false);
            At("goblin_pole", new Vector3(2f, b, 3.5f), Vector3.zero, false);
            At("goblin_roof_45d", new Vector3(0f, b + 2f, 3f), Vector3.zero, false);
            At("goblin_roof_45d", new Vector3(0f, b + 2f, 2f), new Vector3(0f, 180f, 0f), false);
            // The camp: totem by the gate, banner, beds, rubbish and a fire pit.
            At("goblin_totempole", new Vector3(2.5f, b, -5.2f), new Vector3(0f, 160f, 0f), false);
            At("goblin_banner", new Vector3(-2.5f, b, -5.5f), new Vector3(0f, 200f, 0f), false);
            Prop(new[] { "goblin_strawpile" }, new Vector2(-3.5f, 0.5f), 30f, 0.1f);
            Prop(new[] { "goblin_strawpile" }, new Vector2(3.8f, -0.5f), 300f, 0.1f);
            Prop(new[] { "goblin_trashpile" }, new Vector2(4f, 3.5f), 60f, 0.1f);
            Prop(new[] { "fire_pit" }, new Vector2(0f, -1f), 0f, 0.05f);

            CourtyardGuardsAndChest(PlateauHeight, new Vector3(-0.8f, PlateauHeight, 2.4f), 180f);
        }

        static void FulingLookout()
        {
            Vector2 c; Quaternion turn; float b;
            RidgeSpot(2f, out c, out turn, out b);
            Local("goblin_totempole", c, turn, -1f, 0f, b + 0.2f, 0f);
            Local("goblin_totempole", c, turn, 1.2f, 0.4f, b + 0.2f, 40f);
            Local("goblin_banner", c, turn, 0f, 1f, b + 0.2f, 0f);
            Prop(new[] { "goblin_trashpile" }, c, 0f, 0.1f);
        }

        // ---- Mistlands: a ring of black marble columns, and one tall column ----

        static void DvergrRuin()
        {
            // Floor of black marble slabs, some missing.
            for (int ix = -2; ix <= 2; ix += 2)
                for (int iz = -2; iz <= 2; iz += 2)
                {
                    if ((ix == 2 && iz == 2) || (ix == -2 && iz == -2)) continue;
                    At("blackmarble_floor", new Vector3(ix, FloorTop, iz), Vector3.zero, true);
                }
            float b = FloorTop - 0.05f;
            // Six columns around it, two still carry an arch (the way in), one has fallen.
            float[] angles = { 30f, 90f, 150f, 210f, 255f, 285f, 330f };
            float gateTop = b + 4f;
            for (int i = 0; i < angles.Length; i++)
            {
                Vector2 p = Polar(angles[i], 4.2f);
                if (angles[i] == 150f)
                {
                    At("blackmarble_column_1", new Vector3(p.x * 1.4f, PlateauHeight - 0.1f, p.y * 1.4f), new Vector3(0f, 20f, 90f), false);
                    continue;
                }
                float top = Top(At("blackmarble_column_1", new Vector3(p.x, b, p.y), Vector3.zero, false), b + 2f);
                if (angles[i] == 90f || angles[i] == 255f || angles[i] == 285f)
                    top = Top(At("blackmarble_column_2", new Vector3(p.x, top, p.y), Vector3.zero, false), top + 2f);
                if (angles[i] == 255f) gateTop = top;
            }
            // The arch spans the two gate columns (255 and 285 degrees), its top level with theirs.
            Vector2 gate = Polar(270f, 4.2f * Mathf.Cos(15f * Mathf.Deg2Rad));
            At("blackmarble_arch", new Vector3(gate.x, gateTop, gate.y), Vector3.zero, true);
            // A low wall of blocks on the north side, and the dvergr's leftovers.
            float wall = Top(At("blackmarble_2x2x1", new Vector3(-1f, b, 3.6f), Vector3.zero, false), b + 1f);
            At("blackmarble_2x2x1", new Vector3(1f, b, 3.6f), Vector3.zero, false);
            At("blackmarble_2x1x1", new Vector3(-1f, wall, 3.6f), Vector3.zero, false);
            Prop(new[] { "blackmarble_head_big01", "blackmarble_head01" }, new Vector2(5.5f, -3f), 120f, 0.25f);
            Prop(new[] { "dvergrprops_crate" }, new Vector2(2.5f, 2.2f), 15f, 0.05f);
            Prop(new[] { "dvergrprops_barrel" }, new Vector2(-2.8f, 1.5f), 0f, 0.05f);
            Prop(new[] { "blackmarble_pile" }, new Vector2(-5f, -2.5f), 70f, 0.2f);

            CourtyardGuardsAndChest(FloorTop, new Vector3(0.2f, FloorTop, 1.6f), 180f);
        }


        static void DvergrColumn()
        {
            Vector2 c; Quaternion turn; float b;
            RidgeSpot(1.5f, out c, out turn, out b);
            float top = Top(Local("blackmarble_base_1", c, turn, 0f, 0f, b, 0f), b + 1f);
            top = Top(Local("blackmarble_column_3", c, turn, 0f, 0f, top, 0f), top + 3f);
            Local("blackmarble_tip", c, turn, 0f, 0f, top, 0f);
            Prop(new[] { "blackmarble_pile" }, c + new Vector2(1.5f, 1f), 30f, 0.2f);
        }

        // ---- Ashlands: a corner of a grausten fortress and a broken pillar ----

        static void GraustenFort()
        {
            At("Ashlands_Ruins_Floor_6x6_broken1", new Vector3(0f, FloorTop, 0f), Vector3.zero, true);
            float b = FloorTop - 0.05f;
            At("Ashlands_Ruins_Wall_Broken3_4x6", new Vector3(-1f, b, 3.5f), Vector3.zero, false);
            At("Ashlands_Ruins_Wall_Window_4x6_broken2", new Vector3(-3.5f, b, 0.5f), new Vector3(0f, 90f, 0f), false);
            // A pillar whose top broke off and lies next to it.
            At("Ashlands_Pillar4", new Vector3(3.5f, b, 3.5f), Vector3.zero, false);
            Prop(new[] { "Ashlands_Pillar4_tip_broken1" }, new Vector2(5.5f, 2f), 70f, 0.2f);
            At("Ashlands_Arch2_Broken1", new Vector3(0.5f, b, -3.5f), Vector3.zero, false);
            // Rubble.
            At("Piece_grausten_wall_2x2", new Vector3(5f, PlateauHeight - 0.15f, -1f), new Vector3(0f, 30f, 85f), false);
            At("Piece_grausten_wall_1x2", new Vector3(-5.5f, PlateauHeight - 0.15f, -3.5f), new Vector3(80f, 60f, 0f), false);
            Prop(new[] { "grausten_pile" }, new Vector2(4.5f, -4f), 0f, 0.2f);
            Prop(new[] { "ashland_pot1_red" }, new Vector2(-2f, 2f), 0f, 0.02f);
            Prop(new[] { "ashland_pot2_green" }, new Vector2(2.4f, 2.4f), 40f, 0.02f);

            CourtyardGuardsAndChest(FloorTop, new Vector3(-1.5f, FloorTop, 2.2f), 180f);
        }

        static void GraustenPillar()
        {
            Vector2 c; Quaternion turn; float b;
            RidgeSpot(2f, out c, out turn, out b);
            // Only the base still stands; the top stone lies at its foot.
            Local("Ashlands_Fortress_Wall_Pillar_base", c, turn, 0f, 0f, b, 0f);
            Vector3 off = turn * new Vector3(2f, 0f, -1.5f);
            Prop(new[] { "Ashlands_Fortress_Wall_PillarTopStone", "Ashlands_Ruins_TopStone" }, new Vector2(c.x + off.x, c.y + off.z), 40f, 0.2f);
            LyingBeams("Piece_grausten_pillarbeam_small", "Piece_grausten_pillarbeam_medium");
        }

        // ---- second designs ----

        // Meadows: an abandoned fishing camp, a plank shack with drying racks and a cold fire pit.
        static void FishingCamp()
        {
            float floor = PlateauHeight + 0.05f;
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = 1; iz <= 3; iz += 2)
                    At("wood_floor", new Vector3(ix, floor, iz + 0.5f), Vector3.zero, true);
            At("woodwall", new Vector3(-1f, floor, 4.5f), Vector3.zero, false);
            At("woodwall", new Vector3(1f, floor, 4.5f), Vector3.zero, false);
            At("woodwall", new Vector3(-2f, floor, 3.5f), new Vector3(0f, 90f, 0f), false);
            At("woodwall", new Vector3(-2f, floor, 1.5f), new Vector3(0f, 90f, 0f), false);
            At("woodwall", new Vector3(2f, floor, 3.5f), new Vector3(0f, 90f, 0f), false);
            At("woodwall", new Vector3(1f, floor, 0.5f), Vector3.zero, false); // doorway on the left
            At("wood_roof", new Vector3(0.3f, floor, 2.2f), new Vector3(8f, 15f, 0f), false); // fell in
            // Two drying racks: poles with a beam across.
            for (int i = 0; i < 2; i++)
            {
                float x = -4.5f + i * 1.2f, z = -1.5f - i * 1.5f;
                float top = Top(At("wood_pole2", new Vector3(x, PlateauHeight, z - 1f), Vector3.zero, false), PlateauHeight + 1.9f);
                At("wood_pole2", new Vector3(x, PlateauHeight, z + 1f), Vector3.zero, false);
                At("wood_beam", new Vector3(x, top - 0.25f, z), new Vector3(0f, 90f, 0f), false);
            }
            Prop(new[] { "fire_pit" }, new Vector2(2.5f, -2f), 0f, 0.05f);
            Prop(new[] { "barrell_static", "barrell" }, new Vector2(3f, 1.5f), 20f, 0.05f);
            Prop(new[] { "barrell_static", "barrell" }, new Vector2(3.6f, 0.6f), 80f, 0.05f);
            Prop(new[] { "wood_stack" }, new Vector2(-3.2f, 3f), 90f, 0.05f);
            CourtyardGuardsAndChest(floor, new Vector3(0.6f, floor, 3.6f), 180f);
        }

        // Swamp: the stone mouth of a sunken crypt, half swallowed by the ground.
        static void SunkenShrine()
        {
            for (int ix = -2; ix <= 2; ix += 2)
                for (int iz = -2; iz <= 2; iz += 2)
                    if (!(ix == 2 && iz == -2)) At("stone_floor_2x2", new Vector3(ix, FloorTop, iz), Vector3.zero, true);
            float b = FloorTop - 0.05f;
            At("SunkenKit_int_wall_2x4", new Vector3(-1f, b, 3.4f), Vector3.zero, false);
            At("SunkenKit_int_wall_2x4", new Vector3(1f, b, 3.4f), Vector3.zero, false);
            At("SunkenKit_int_wall_1x4", new Vector3(-3.4f, b, 1.5f), new Vector3(0f, 90f, 0f), false);
            At("SunkenKit_int_wall_1x2", new Vector3(3.4f, b, 2f), new Vector3(0f, 90f, 0f), false);
            At("SunkenKit_int_arch", new Vector3(0f, b, -3.4f), Vector3.zero, false);
            Prop(new[] { "sunken_crypt_gate" }, new Vector2(0f, 2.7f), 0f, 0.1f);
            Prop(new[] { "crypt_skeleton_laying" }, new Vector2(2f, -1f), 60f, 0.05f);
            Prop(new[] { "bone_stack" }, new Vector2(-2.5f, -2.5f), 10f, 0.1f);
            Prop(new[] { "skull_pile" }, new Vector2(4.5f, -3f), 140f, 0.1f);
            CourtyardGuardsAndChest(FloorTop, new Vector3(-2f, FloorTop, 1.2f), 90f);
        }

        // Plains: Fulings moved into an old stone ruin and fenced it with sharpened stakes.
        static void FulingRuin()
        {
            float b = PlateauHeight - 0.05f;
            At("stone_wall_4x2", new Vector3(-2f, b, 4f), Vector3.zero, false);
            At("stone_wall_2x1", new Vector3(1f, b, 4f), Vector3.zero, false);
            At("stone_wall_4x2", new Vector3(-4f, b, 2f), new Vector3(0f, 90f, 0f), false);
            At("stone_wall_2x1", new Vector3(-4f, b + 2f, 2.5f), new Vector3(0f, 90f, 0f), false);
            At("stone_pillar", new Vector3(3.5f, b, 3.5f), Vector3.zero, false);
            At("goblin_woodwall_2m", new Vector3(3f, b, 4f), Vector3.zero, false);
            At("goblin_pole", new Vector3(-4f, b, -2f), Vector3.zero, false);
            At("goblin_banner", new Vector3(-1f, b, -5f), new Vector3(0f, 180f, 0f), false);
            float[] stakes = { 200f, 240f, 300f, 340f, 20f, 60f };
            foreach (float deg in stakes)
            {
                Vector2 p = Polar(deg, 6.2f);
                At("piece_sharpstakes", new Vector3(p.x, Height(p.x, p.y) - 0.1f, p.y), new Vector3(0f, 90f - deg, 0f), false);
            }
            Prop(new[] { "goblin_strawpile" }, new Vector2(-2.5f, 2.2f), 10f, 0.1f);
            Prop(new[] { "goblin_strawpile" }, new Vector2(2.5f, 1.8f), 200f, 0.1f);
            Prop(new[] { "goblin_trashpile" }, new Vector2(4f, -2.5f), 0f, 0.1f);
            Prop(new[] { "fire_pit" }, new Vector2(0.5f, -1.5f), 0f, 0.05f);
            Prop(new[] { "goblin_stepladder" }, new Vector2(-1f, 3.2f), 0f, 0.05f);
            CourtyardGuardsAndChest(PlateauHeight, new Vector3(0.5f, PlateauHeight, 3.1f), 180f);
        }

        // Mistlands: a dvergr outpost, plank walls and stake fences around a black marble floor, crates left behind.
        static void DvergrOutpost()
        {
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -1; iz <= 1; iz += 2)
                    At("blackmarble_floor", new Vector3(ix, FloorTop, iz), Vector3.zero, true);
            float b = PlateauHeight - 0.05f;
            At("dvergrprops_wood_wall", new Vector3(-1.2f, b, 3.2f), Vector3.zero, false);
            At("dvergrprops_wood_wall", new Vector3(1.4f, b, 3.2f), Vector3.zero, false);
            At("dvergrprops_wood_wall", new Vector3(-3.2f, b, 0.8f), new Vector3(0f, 90f, 0f), false);
            float[] stakes = { 240f, 300f, 340f, 20f };
            foreach (float deg in stakes)
            {
                Vector2 p = Polar(deg, 5.5f);
                At("dvergrprops_wood_stakewall", new Vector3(p.x, Height(p.x, p.y) - 0.1f, p.y), new Vector3(0f, -(deg + 90f), 0f), false);
            }
            Prop(new[] { "dvergrprops_crate" }, new Vector2(2.6f, 2f), 15f, 0.05f);
            Prop(new[] { "dvergrprops_crate_long" }, new Vector2(-2.4f, -2.6f), 80f, 0.05f);
            Prop(new[] { "dvergrprops_barrel" }, new Vector2(-2.6f, 2.2f), 0f, 0.05f);
            Prop(new[] { "dvergrprops_barrel" }, new Vector2(3.2f, -1.2f), 0f, 0.05f);
            Prop(new[] { "dvergrprops_lantern_standing" }, new Vector2(2f, -3f), 0f, 0.05f);
            Prop(new[] { "dvergrprops_table" }, new Vector2(0.3f, -2.6f), 10f, 0.05f);
            CourtyardGuardsAndChest(FloorTop, new Vector3(0f, FloorTop, 2f), 180f);
        }

        // Ashlands: arches and broken twisted pillars around a cracked floor.
        static void AshlandsArches()
        {
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -1; iz <= 1; iz += 2)
                    At(ix == 1 && iz == 1 ? "Ashlands_Ruins_Floor_3x3_broken1" : "Ashlands_Ruins_Floor_3x3", new Vector3(ix * 1.5f, FloorTop, iz * 1.5f), Vector3.zero, true);
            At("Ashlands_Arch1", new Vector3(0f, PlateauHeight, 4f), Vector3.zero, false);
            At("Ashlands_Arch2_Broken2", new Vector3(-4f, PlateauHeight, 0f), new Vector3(0f, 90f, 0f), false);
            At("Ashlands_Ruins_twist_PillarBase", new Vector3(4f, PlateauHeight, 3f), Vector3.zero, false);
            At("Ashlands_Ruins_twist_PillarBaseSmall", new Vector3(4.2f, PlateauHeight, -2.5f), Vector3.zero, false);
            Prop(new[] { "Ashlands_Ruins_TopStone" }, new Vector2(-2.5f, -4.5f), 30f, 0.2f);
            Prop(new[] { "grausten_pile" }, new Vector2(5.5f, 0.5f), 0f, 0.2f);
            Prop(new[] { "ashland_pot3_red" }, new Vector2(-2f, 2.4f), 0f, 0.02f);
            Prop(new[] { "ashland_pot1_green" }, new Vector2(2.6f, -2.8f), 0f, 0.02f);
            CourtyardGuardsAndChest(FloorTop, new Vector3(-1f, FloorTop, 1.6f), 180f);
        }

        // ---- helpers ----

        // A spot on the cliff ridge facing the plateau: centre, rotation (+z out to sea) and base height
        // (lowest ground under radius, a little sunk).

        static void RidgeSpot(float radius, out Vector2 centre, out Quaternion turn, out float bottom)
        {
            centre = Polar(cliffDeg, PlateauAt(cliffDeg) + 3f);
            turn = Quaternion.Euler(0f, 90f - cliffDeg, 0f);
            bottom = GroundMin(centre.x, centre.y, radius) - 0.3f;
        }

        // A piece at (lx, lz) in a frame turned by turn around centre.
        static GameObject Local(string name, Vector2 centre, Quaternion turn, float lx, float lz, float y, float rotY)
        {
            Vector3 off = turn * new Vector3(lx, 0f, lz);
            return At(name, new Vector3(centre.x + off.x, y, centre.y + off.z), new Vector3(0f, turn.eulerAngles.y + rotY, 0f), false);
        }

        // Top of a placed piece, to stack the next one on it; fallback if the piece doesn't exist.
        static float Top(GameObject piece, float fallback)
        {
            return piece != null ? LocalBounds(piece).max.y : fallback;
        }

        // Debris below the ridge structure.
        static void LyingBeams(string beam, string pole)
        {
            float r = PlateauAt(cliffDeg) + 3f;
            Vector2 q = Polar(cliffDeg - 9f, r - 3.5f);
            At(beam, new Vector3(q.x, Height(q.x, q.y) - 0.05f, q.y), new Vector3(0f, 70f, 0f), false);
            Vector2 w = Polar(cliffDeg + 8f, r - 3.2f);
            At(pole, new Vector3(w.x, Height(w.x, w.y) - 0.05f, w.y), new Vector3(0f, 150f, 85f), false);
            Vector2 v = Polar(cliffDeg + 2f, r - 4.5f);
            At(beam, new Vector3(v.x, Height(v.x, v.y) - 0.05f, v.y), new Vector3(4f, 10f, -3f), false);
        }

        // Static decoration on the ground (not networked, can't be broken).
        static void Prop(string[] candidates, Vector2 at, float rotY, float sink)
        {
            OnGround(candidates, at, rotY, 0f, sink, true);
        }

        // Like Prop, but shrunk to at most maxSize metres across if the vanilla prop is bigger.
        static void PropAtMost(string name, Vector2 at, float rotY, float sink, float maxSize)
        {
            GameObject go = Copy(new[] { name }, new Vector3(0f, rotY, 0f), 0f, true);
            if (go == null) return;
            Bounds b = LocalBounds(go);
            float extent = Mathf.Max(b.size.x, b.size.z);
            if (extent > maxSize)
            {
                go.transform.localScale = Vector3.one * (maxSize / extent);
                b = LocalBounds(go);
            }
            float y = GroundMin(at.x, at.y, 0.4f * Mathf.Max(b.size.x, b.size.z)) - sink * b.size.y;
            Move(go, b, new Vector3(at.x, y, at.y), false);
        }
    }
}
