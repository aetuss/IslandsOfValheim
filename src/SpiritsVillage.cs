using System.Collections.Generic;
using UnityEngine;

namespace IslandsOfValheim
{
    // The Brim village (Valhalla arc, T4): the Brim Folk's old home on a large islet of the game's terrain, in
    // sight of Sævar's harbour. It is there from the start, ruined; donations at its altar restore it in
    // stages (SpiritsIslet):
    //   0 ruined:   broken walls, no roofs, cold fires, rubble and weeds, a wreck on the shore;
    //   1 cleared:  the walls whole again (weathered), the hall roofed, the first fires lit, wood and stone
    //               piled to build with;
    //   2 rebuilt:  every house roofed, new and furnished for its trade (smith, weaver, fisher, brewer), the
    //               hall's feast laid, the field sown, the tower's beacon lit, a ship at the jetty; and the
    //               player's house beside the hall, with its real beds, chests and fires.
    //   (3, the people's return, comes with T5.)
    // Each stage's look is its own group in the location, shown by SpiritStages from the world's keys; what
    // every stage shares (floors, corner posts, paths, the dock, trees) stands outside them.
    // 1.10.89, from the user's photos of 1.10.88: walls turned rough side in; the houses bigger, apart and each
    // furnished for its own trade, without the campfires; floors on the ground; the altar on its own paved
    // platform in the middle of the square; seats facing the right way; paths, a field, wood piles, a ship
    // and a wreck so the islet isn't empty round the houses; the islet itself bigger.
    static partial class IsletBuilder
    {
        internal const string SpiritsName = "IoV_Story_Spirits";
        internal const float SpiritsHeight = 4.5f;
        internal static readonly LargeKind SpiritsKind = new LargeKind
        {
            Tier = IsletTier.All[0], Variant = 1, Height = SpiritsHeight, Scale = 4.4f, Layout = Layout.Village,
            Peaceful = true, Guardian = new string[0], Life = new Life[0], Custom = BuildVillage,
        };

        // Where the altar stands (it is networked: it finds the islet's middle from itself), and the real pieces
        // of the player's house, put there by the altar when the village is rebuilt; both in the islet's space.
        internal static Vector3 SpiritAltarAt;
        internal struct HomePiece { internal string[] Prefab; internal Vector3 At; internal float Yaw; internal bool AlongZ; }
        internal static readonly List<HomePiece> HomePieces = new List<HomePiece>();
        // The player's yard (building allowed there), in the islet's space.
        internal static Vector3 SpiritYardAt;
        internal const float SpiritYardRadius = 6f;

        internal static void RegisterSpiritsIslet()
        {
            BuildLarge(SpiritsKind, SpiritsName, true, 0, false); // (placed by SpiritsIslet, near the harbour)
        }

        static Transform groupAll;
        static readonly Transform[] stageGroups = new Transform[3];
        struct FramePose { internal Transform Frame; internal Vector3 At; internal float Yaw; }
        static readonly List<FramePose> frames = new List<FramePose>();
        static System.Random villageRandom;
        static float wallWidth = 2f, wallHeight = 2f, tile = 2f, planScale = 1f;

        enum Trade { Hall, Home, Smith, Weaver, Fisher, Brewer }

        static void BuildVillage(LargeKind kind)
        {
            villageRandom = new System.Random(7341);
            frames.Clear();
            HomePieces.Clear();
            Transform top = parent;
            groupAll = SpGroup(top, "IoV_SpiritAll", true);
            for (int s = 0; s < 3; s++) stageGroups[s] = SpGroup(top, SpiritStages.GroupName(s), s == 0);
            GameObject stages = new GameObject("IoV_SpiritStages");
            stages.transform.SetParent(top, false);
            stages.AddComponent<SpiritStages>();
            MeasurePieces();

            // How far the village reaches: its plan is drawn for a plateau of 33 m, scaled to this one's.
            float p = kind.Plateau;
            kind.NoBuildRadius = p + 4f; // the village is the people's (the yard excepted: SpiritYard)
            planScale = Mathf.Clamp(p / 33f, 0.8f, 1.2f);
            Plugin.Debug("Brim village: plateau " + p.ToString("F1") + " m, shore " + kind.Shore.ToString("F1") + " m, plan scaled " + planScale.ToString("F2"));

            // The square with the altar in the middle; the hall north of it; the player's house east, beside the
            // hall, its yard beyond; four houses round the rest, well apart; the old watchtower behind the hall;
            // the field to the west.
            Vector2 hall = At(0f, 21f), home = At(21f, 6f);
            Vector2[] houses = { At(-22f, 8f), At(-19f, -15f), At(-3f, -24f), At(13f, -22f) };
            Trade[] trades = { Trade.Smith, Trade.Weaver, Trade.Fisher, Trade.Brewer };
            Vector2 tower = At(-14f, 29f), yard = At(24f, -9f), field = At(-29f, -4f);

            Building(hall, 20f, 10f, 3, Trade.Hall);
            Building(home, 12f, 8f, 2, Trade.Home);
            for (int i = 0; i < houses.Length; i++) Building(houses[i], 12f, 8f, 2, trades[i]);
            float altarTop = Square();
            Tower(tower);
            SpiritYardAt = new Vector3(yard.x, Height(yard.x, yard.y), yard.y);
            SpYard(yard);
            Field(field);
            float dock = cliffDeg + 180f;
            parent = groupAll;
            PlaceDock(dock);
            Trees(kind);
            // the paths: from the square to every door, the tower, the yard, the field and the jetty
            List<Vector2> ends = new List<Vector2>();
            foreach (Vector2 b in new[] { hall, home }) ends.Add(Door(b, b == hall ? 20f : 12f));
            foreach (Vector2 b in houses) ends.Add(Door(b, 12f));
            ends.Add(tower);
            ends.Add(yard);
            ends.Add(field);
            ends.Add(Polar(dock, PlateauAt(dock)));
            foreach (Vector2 e in ends) Path(Vector2.zero, e, 7f);
            SpShore(dock);

            // Every building's frames to where it stands (built square to the islet, so measured bounds held).
            foreach (FramePose f in frames)
            {
                f.Frame.localPosition = f.At;
                f.Frame.localRotation = Quaternion.Euler(0f, f.Yaw, 0f);
            }
            SpiritHomeToIslet();
            parent = top;
            SpiritAltarAt = new Vector3(0f, altarTop, 0f);
            PlaceNetworked(SpiritsIslet.AltarName, SpiritAltarAt, 0f);
            Plugin.Debug("Brim village: " + frames.Count + " frames, " + HomePieces.Count + " pieces of the player's house, altar at " + SpiritAltarAt.ToString("F1"));
        }

        static Vector2 At(float x, float z) { return new Vector2(x * planScale, z * planScale); }

        // A building faces the square: the yaw that turns its door (its own -z) towards the middle.
        static float FacingSquare(Vector2 at) { return Mathf.Round(Mathf.Atan2(at.x, at.y) * Mathf.Rad2Deg / 5f) * 5f; }

        // Just outside a building's door.
        static Vector2 Door(Vector2 at, float length)
        {
            Vector3 d = Quaternion.Euler(0f, FacingSquare(at), 0f) * new Vector3(0f, 0f, -length * 0.5f - 1.5f);
            return at + new Vector2(d.x, d.z);
        }

        static Transform SpGroup(Transform under, string name, bool active)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(under, false);
            go.SetActive(active);
            return go.transform;
        }

        static Transform Frame(Transform group, Vector3 at, float yaw)
        {
            GameObject go = new GameObject("IoV_SpiritFrame");
            go.transform.SetParent(group, false);
            frames.Add(new FramePose { Frame = go.transform, At = at, Yaw = yaw });
            return go.transform;
        }

        // The size of a wall and a floor tile, measured (2 m each, unless the game changed them).
        static void MeasurePieces()
        {
            parent = groupAll;
            GameObject wall = SpProp(new[] { "woodwall" }, Vector3.zero, 0f, false);
            if (wall != null)
            {
                Bounds b = LocalBounds(wall);
                wallWidth = Mathf.Max(1f, Mathf.Round(Mathf.Max(b.size.x, b.size.z) * 2f) / 2f);
                wallHeight = Mathf.Max(1f, Mathf.Round(b.size.y * 2f) / 2f);
                Object.DestroyImmediate(wall);
                built--;
            }
            GameObject floor = SpProp(new[] { "wood_floor" }, Vector3.zero, 0f, true);
            if (floor != null)
            {
                Bounds b = LocalBounds(floor);
                tile = Mathf.Max(1f, Mathf.Round(Mathf.Max(b.size.x, b.size.z) * 2f) / 2f);
                Object.DestroyImmediate(floor);
                built--;
            }
            Plugin.Debug("Brim village: walls " + wallWidth + " x " + wallHeight + " m, floor tiles " + tile + " m");
        }

        // ---- a building ----

        // length along its own z (its door at -z, towards the square), width across; rows of walls high.
        static void Building(Vector2 at, float length, float width, int rows, Trade trade)
        {
            float yaw = FacingSquare(at);
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            float floor = float.MinValue;
            foreach (Vector3 c in new[] { new Vector3(-width, 0f, -length), new Vector3(width, 0f, -length), new Vector3(-width, 0f, length), new Vector3(width, 0f, length), Vector3.zero })
            {
                Vector3 w = turn * (c * 0.5f);
                floor = Mathf.Max(floor, Height(at.x + w.x, at.y + w.z));
            }
            floor += 0.05f; // (on the ground: 1.10.88's stood a hand above it)
            Vector3 pose = new Vector3(at.x, floor, at.y);

            parent = Frame(groupAll, pose, yaw);
            Floor(length, width);
            Corners(length, width, rows * wallHeight);
            if (trade == Trade.Home) HomePlan(length, width);
            for (int stage = 0; stage < 3; stage++)
            {
                parent = Frame(stageGroups[stage], pose, yaw);
                Walls(length, width, rows, stage);
                if (stage == 2 || (stage == 1 && trade == Trade.Hall))
                {
                    Gables(length, width, rows);
                    Roof(rows * wallHeight, width * 0.5f + 0.15f, -length * 0.5f, length * 0.5f);
                }
                Inside(trade, stage, length, width);
            }
            if (trade == Trade.Home) homePose = new FramePose { At = pose, Yaw = yaw };
        }

        static FramePose homePose;

        static void Floor(float length, float width)
        {
            for (float x = -width * 0.5f + tile * 0.5f; x < width * 0.5f; x += tile)
                for (float z = -length * 0.5f + tile * 0.5f; z < length * 0.5f; z += tile)
                    SpProp(new[] { "wood_floor" }, new Vector3(x, 0f, z), 0f, true, 1);
        }

        static void Corners(float length, float width, float height)
        {
            foreach (float x in new[] { -width * 0.5f, width * 0.5f })
                foreach (float z in new[] { -length * 0.5f, length * 0.5f })
                {
                    Vector3 foot = new Vector3(x, 0f, z);
                    GameObject pole = SpProp(new[] { "darkwood_pole4", "wood_pole4" }, foot, 0f, false);
                    if (pole == null) continue;
                    Bounds b = LocalBounds(pole);
                    pole.transform.localScale *= (height + 0.3f) / Mathf.Max(0.1f, b.size.y);
                    pole.transform.localPosition = Vector3.zero;
                    Move(pole, LocalBounds(pole), foot, false);
                }
        }

        // A wall turned so its rough side (its frame of beams, the game's wall's own -z) faces in.
        static float Inward(float x, float z, bool side) { return side ? (x > 0f ? 90f : -90f) : (z < 0f ? 180f : 0f); }

        // Walls all round, but for the door; worn as the stage is (broken, weathered, new). A ruin's are gappy,
        // its top rows most.
        static void Walls(float length, float width, int rows, int stage)
        {
            int wear = stage == 0 ? 2 : stage == 1 ? 1 : 0;
            for (int r = 0; r < rows; r++)
            {
                float y = r * wallHeight;
                double gone = stage == 0 ? (r == 0 ? 0.12 : r == rows - 1 ? 0.55 : 0.3) : 0.0;
                foreach (float x in new[] { -width * 0.5f, width * 0.5f })
                    for (float z = -length * 0.5f + wallWidth * 0.5f; z < length * 0.5f; z += wallWidth)
                        if (villageRandom.NextDouble() >= gone) SpProp(new[] { "woodwall" }, new Vector3(x, y, z), Inward(x, z, true), false, wear);
                foreach (float z in new[] { -length * 0.5f, length * 0.5f })
                    for (float x = -width * 0.5f + wallWidth * 0.5f; x < width * 0.5f; x += wallWidth)
                    {
                        bool door = z < 0f && Mathf.Abs(x) < wallWidth * 0.6f && (r == 0 || (r == 1 && rows > 2));
                        if (!door && villageRandom.NextDouble() >= gone) SpProp(new[] { "woodwall" }, new Vector3(x, y, z), Inward(x, z, false), false, wear);
                    }
            }
        }

        // The ends above the walls, in steps under the 45 degree roof.
        static void Gables(float length, float width, int rows)
        {
            float half = width * 0.5f + 0.15f;
            for (int k = 0; ; k++)
            {
                float reach = half - k * wallHeight - wallHeight - 0.4f; // how far out a piece's outer edge may go
                if (reach < wallWidth * 0.5f) break;
                float y = rows * wallHeight + k * wallHeight;
                foreach (float z in new[] { -length * 0.5f, length * 0.5f })
                    for (float x = -Mathf.Floor(reach / wallWidth - 0.5f) * wallWidth; x <= reach - wallWidth * 0.5f + 0.01f; x += wallWidth)
                        SpProp(new[] { "woodwall" }, new Vector3(x, y, z), Inward(x, z, false), false, 0);
            }
        }

        // ---- what's inside ----

        static void Inside(Trade trade, int stage, float length, float width)
        {
            float back = length * 0.5f, side = width * 0.5f;
            if (stage == 0) { Rubble(length, width, trade == Trade.Hall ? 5 : 3); if (trade == Trade.Hall) SpProp(new[] { "hearth" }, new Vector3(0f, 0f, -back + 6f), 0f, false, -1, false, true); return; }
            if (trade == Trade.Hall)
            {
                if (stage == 1) { SpProp(new[] { "hearth" }, new Vector3(0f, 0f, -back + 6f), 0f, false, -1, true); return; }
                foreach (float z in new[] { -back + 6f, back - 7f })
                    SpProp(new[] { "hearth" }, new Vector3(0f, 0f, z), 0f, false, -1, true);
                foreach (float s in new[] { -1f, 1f })
                    for (float z = -back + 4f; z < back - 4f; z += 4.5f)
                    {
                        float x = s * (side - 2f);
                        SpAlongZ(SpProp(new[] { "piece_table_oak", "piece_table" }, new Vector3(x, 0f, z), 0f, false), new Vector3(x, 0f, z));
                        foreach (float off in new[] { -1.15f, 1.15f })
                            SpSeat(new[] { "piece_bench01" }, new Vector3(x + off, 0f, z), new Vector3(-Mathf.Sign(off), 0f, 0f));
                    }
                SpSeat(new[] { "piece_throne01", "piece_chair02" }, new Vector3(0f, 0f, back - 1.6f), Vector3.back);
                foreach (float x in new[] { -2.5f, 2.5f })
                    SpProp(new[] { "piece_banner01", "piece_banner02" }, new Vector3(x, 2.8f, back - 0.35f), 180f, false);
                Doorway(length);
                return;
            }
            if (stage == 1) { if (trade != Trade.Home) Piles(length, width); return; }
            // Rebuilt: each house furnished for its trade, along its walls, its middle left free; a bed by the
            // back wall, a torch in a corner.
            switch (trade)
            {
                case Trade.Home:
                    SpAlongZ(SpProp(new[] { "piece_table" }, new Vector3(-side + 1.6f, 0f, -1f), 0f, false), new Vector3(-side + 1.6f, 0f, -1f));
                    SpSeat(new[] { "piece_bench01" }, new Vector3(-side + 2.9f, 0f, -1f), Vector3.left);
                    SpProp(new[] { "piece_banner01", "piece_banner02" }, new Vector3(0f, 2.6f, back - 0.35f), 180f, false);
                    break;
                case Trade.Smith:
                    SpProp(new[] { "forge" }, new Vector3(-side + 1.5f, 0f, back - 1.6f), 180f, false);
                    SpProp(new[] { "forge_ext1", "forge_ext2" }, new Vector3(-side + 1.2f, 0f, 0.5f), 90f, false);
                    SpProp(new[] { "piece_workbench" }, new Vector3(side - 1.3f, 0f, 0f), -90f, false);
                    break;
                case Trade.Weaver:
                    SpProp(new[] { "piece_spinningwheel" }, new Vector3(-side + 1.4f, 0f, back - 2f), 90f, false);
                    SpProp(new[] { "piece_chest_wood" }, new Vector3(-side + 0.8f, 0f, 0f), 90f, false);
                    SpProp(new[] { "piece_chest_wood" }, new Vector3(-side + 0.8f, 0f, -1.5f), 90f, false);
                    break;
                case Trade.Fisher:
                    SpProp(new[] { "piece_cookingstation" }, new Vector3(-side + 1.4f, 0f, 0f), 90f, false);
                    SpAlongZ(SpProp(new[] { "piece_table" }, new Vector3(-side + 1.5f, 0f, back - 2f), 0f, false), new Vector3(-side + 1.5f, 0f, back - 2f));
                    SpProp(new[] { "piece_chest_wood" }, new Vector3(side - 0.8f, 0f, -1f), -90f, false);
                    break;
                case Trade.Brewer:
                    SpProp(new[] { "fermenter" }, new Vector3(-side + 1.4f, 0f, back - 1.6f), 180f, false);
                    SpProp(new[] { "piece_cauldron" }, new Vector3(-side + 1.4f, 0f, 0f), 90f, false);
                    SpProp(new[] { "barrell", "piece_chest_wood" }, new Vector3(side - 0.9f, 0f, -1.2f), 0f, false);
                    break;
            }
            if (trade != Trade.Home)
            {
                Vector3 bed = new Vector3(side - 1.2f, 0f, back - 1.6f);
                SpAlongZ(SpProp(new[] { "bed", "piece_bed02" }, bed, 0f, false), bed);
                SpProp(new[] { "piece_groundtorch_wood", "piece_groundtorch" }, new Vector3(side - 0.6f, 0f, -back + 0.8f), 0f, false, -1, true);
            }
            Doorway(length);
        }

        // Where the real pieces of the player's house go, in the house's own space (turned into the islet's
        // once it stands): two beds by the back wall, a chest by each side wall, a fire either side of the door.
        static void HomePlan(float length, float width)
        {
            float back = length * 0.5f, side = width * 0.5f;
            homeLocal.Clear();
            homeLocal.Add(new HomePiece { Prefab = new[] { "bed" }, At = new Vector3(-side + 1.2f, 0f, back - 1.5f), AlongZ = true });
            homeLocal.Add(new HomePiece { Prefab = new[] { "bed" }, At = new Vector3(side - 1.2f, 0f, back - 1.5f), AlongZ = true });
            homeLocal.Add(new HomePiece { Prefab = new[] { "piece_chest", "piece_chest_wood" }, At = new Vector3(side - 0.7f, 0f, 0.5f), Yaw = -90f });
            homeLocal.Add(new HomePiece { Prefab = new[] { "piece_chest", "piece_chest_wood" }, At = new Vector3(side - 0.7f, 0f, -1.2f), Yaw = -90f });
            homeLocal.Add(new HomePiece { Prefab = new[] { "piece_brazierfloor01" }, At = new Vector3(-side + 1f, 0f, -back + 1.3f) });
            homeLocal.Add(new HomePiece { Prefab = new[] { "piece_brazierfloor01" }, At = new Vector3(side - 1f, 0f, -back + 1.3f) });
        }

        static readonly List<HomePiece> homeLocal = new List<HomePiece>();

        static void Doorway(float length)
        {
            foreach (float x in new[] { -1.9f, 1.9f })
                SpProp(new[] { "piece_brazierfloor01" }, new Vector3(x, 0f, -length * 0.5f - 0.9f), 0f, false, -1, true);
        }

        // Stones and old logs lying about a ruin, and weeds through its floor.
        static void Rubble(float length, float width, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float x = (float)(villageRandom.NextDouble() - 0.5) * (width - 2f);
                float z = (float)(villageRandom.NextDouble() - 0.5) * (length - 2f);
                float yaw = (float)villageRandom.NextDouble() * 360f;
                switch (i % 3)
                {
                    case 0: Sized(SpProp(new[] { "Rock_3", "rock1" }, new Vector3(x, -0.3f, z), yaw, false), 1.1f, new Vector3(x, -0.3f, z)); break;
                    case 1: SpProp(new[] { "Beech_log", "FirTree_log" }, new Vector3(x, 0f, z), yaw, false); break;
                    default: SpProp(new[] { "Bush01", "Bush02_en" }, new Vector3(x, 0f, z), yaw, false); break;
                }
            }
        }

        // While the village is being rebuilt: wood and stone piled inside the houses.
        static void Piles(float length, float width)
        {
            SpProp(new[] { "wood_stack" }, new Vector3(-width * 0.5f + 1.2f, 0f, length * 0.5f - 1.5f), 90f, false);
            SpProp(new[] { "stone_pile" }, new Vector3(width * 0.5f - 1.3f, 0f, 0f), 0f, false);
        }

        static void Sized(GameObject go, float size, Vector3 at)
        {
            if (go == null) return;
            Bounds b = LocalBounds(go);
            float extent = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (extent > 0.01f) go.transform.localScale *= size / extent;
            go.transform.localPosition = Vector3.zero;
            Move(go, LocalBounds(go), at, false);
        }

        // ---- the square, the tower, the yard, the field, the shore, the trees ----

        // The altar's platform of stone in the middle (gives back its top), the square paved round it, fires at
        // its corners (cold in the ruin), and once rebuilt benches round it.
        static float Square()
        {
            parent = groupAll;
            SpPaint(0f, 0f, 8f, TerrainModifier.PaintType.Paved);
            float ground = GroundMin(0f, 0f, 3.5f);
            float top = ground + 0.3f;
            for (float x = -2f; x <= 2.01f; x += 2f)
                for (float z = -2f; z <= 2.01f; z += 2f)
                    SpProp(new[] { "stone_floor_2x2" }, new Vector3(x, top, z), 0f, true);
            for (int stage = 0; stage < 3; stage++)
            {
                parent = stageGroups[stage];
                foreach (float x in new[] { -2.6f, 2.6f })
                    foreach (float z in new[] { -2.6f, 2.6f })
                        SpProp(new[] { "piece_brazierfloor01" }, new Vector3(x, top, z), 0f, false, -1, stage > 0, stage == 0);
                if (stage == 0)
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f + 20f;
                        Vector3 q = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 6f);
                        q.y = Height(q.x, q.z);
                        SpProp(new[] { "Bush01", "Bush02_en" }, q, a, false);
                    }
                if (stage < 2) continue;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f + 22.5f;
                    Vector3 q = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 5.5f);
                    q.y = Height(q.x, q.z);
                    SpSeat(new[] { "piece_bench01" }, q, -q.normalized);
                }
            }
            return top;
        }

        // The old watchtower: a stump in the ruin, its frame standing again once cleared, its beacon lit once
        // rebuilt.
        static void Tower(Vector2 at)
        {
            float ground = Height(at.x, at.y);
            for (int stage = 0; stage < 3; stage++)
            {
                parent = stageGroups[stage];
                float top = stage == 0 ? 3f : 8f;
                foreach (float x in new[] { -1f, 1f })
                    foreach (float z in new[] { -1f, 1f })
                    {
                        if (stage == 0 && x > 0f && z > 0f) continue; // one post fallen
                        for (float y = 0f; y < top - 0.1f; y += 4f)
                            SpProp(new[] { "darkwood_pole4", "wood_pole4" }, new Vector3(at.x + x, ground + y, at.y + z), 0f, false, stage == 0 ? 2 : stage == 1 ? 1 : 0);
                    }
                if (stage == 0) continue;
                SpProp(new[] { "wood_floor" }, new Vector3(at.x, ground + top + 0.2f, at.y), 0f, true, stage == 1 ? 1 : 0);
                if (stage == 2)
                    SpProp(new[] { "piece_brazierfloor01" }, new Vector3(at.x, ground + top + 0.2f, at.y), 0f, false, -1, true);
            }
        }

        // The player's yard: open ground, only its corner posts (torches once rebuilt).
        static void SpYard(Vector2 at)
        {
            for (int stage = 1; stage < 3; stage++)
            {
                parent = stageGroups[stage];
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 90f + 45f;
                    Vector3 q = new Vector3(at.x, 0f, at.y) + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, SpiritYardRadius);
                    q.y = Height(q.x, q.z);
                    SpProp(stage == 2 ? new[] { "piece_groundtorch_wood", "piece_groundtorch" } : new[] { "wood_pole2", "wood_pole" }, q, 0f, false, -1, stage == 2);
                }
            }
            GameObject marker = new GameObject("IoV_SpiritYard");
            marker.transform.SetParent(groupAll, false);
            marker.transform.localPosition = SpiritYardAt;
            marker.AddComponent<SpiritYard>();
        }

        // The village's field: ploughed ground (always), weeds in the ruin, sown once rebuilt.
        static void Field(Vector2 at)
        {
            parent = groupAll;
            for (float dx = -3f; dx <= 3.01f; dx += 2f)
                for (float dz = -2f; dz <= 2.01f; dz += 2f)
                    SpPaint(at.x + dx, at.y + dz, 1.6f, TerrainModifier.PaintType.Cultivate);
            string[][] crops = { new[] { "Pickable_Barley", "Pickable_Carrot" }, new[] { "Pickable_Flax", "Pickable_Turnip" }, new[] { "Pickable_Carrot" }, new[] { "Pickable_Turnip" } };
            for (float dx = -3.5f; dx <= 3.51f; dx += 1f)
                for (float dz = -2.5f; dz <= 2.51f; dz += 1f)
                {
                    Vector3 q = new Vector3(at.x + dx, Height(at.x + dx, at.y + dz), at.y + dz);
                    int row = Mathf.RoundToInt(dz + 2.5f);
                    parent = stageGroups[2];
                    SpProp(crops[row % crops.Length], q, (float)villageRandom.NextDouble() * 360f, false);
                    if (villageRandom.NextDouble() < 0.25)
                    {
                        parent = stageGroups[0];
                        SpProp(new[] { "Bush01", "Bush02_en" }, q, (float)villageRandom.NextDouble() * 360f, false);
                    }
                }
        }

        // The jetty's side: a wreck on the shore in the ruin, a ship moored alongside once rebuilt; a cart and
        // wood piled by the hall once cleared.
        static void SpShore(float dock)
        {
            parent = stageGroups[0];
            PlaceWreck(dock + 40f);
            float s = ShoreAt(dock + 14f);
            Vector2 u = Polar(dock + 14f, 1f);
            Vector2 ship = u * (s + 6f);
            parent = stageGroups[2];
            GameObject boat = SpProp(new[] { "VikingShip", "Karve" }, new Vector3(ship.x, -1.3f, ship.y), 180f - (dock + 14f), false);
            if (boat == null) Plugin.Debug("Brim village: no ship to moor");
            foreach (int stage in new[] { 1, 2 })
            {
                parent = stageGroups[stage];
                Vector2 c = At(8f, 13f);
                SpProp(new[] { "Cart" }, new Vector3(c.x, Height(c.x, c.y), c.y), 30f, false);
                Vector2 w = At(-8f, 13f);
                SpProp(new[] { "wood_stack" }, new Vector3(w.x, Height(w.x, w.y), w.y), 0f, false);
            }
        }

        // Trees round the village, on its slopes, never on the plateau where the houses stand.
        static void Trees(LargeKind kind)
        {
            string[] trees = { "Beech1", "Birch1", "Oak1" };
            for (int i = 0; i < 40; i++)
            {
                float a = (float)villageRandom.NextDouble() * Mathf.PI * 2f;
                float r = kind.Plateau + 4f + (float)villageRandom.NextDouble() * Mathf.Max(2f, kind.Shore - kind.Plateau - 12f);
                Vector2 q = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
                if (Height(q.x, q.y) < 1.5f) continue;
                OnGround(new[] { trees[i % trees.Length] }, q, a * Mathf.Rad2Deg, 0f, 0.05f, true);
            }
        }

        // A worn dirt path from one point to another (from `from` out of a circle of `skip` round it).
        static void Path(Vector2 from, Vector2 to, float skip)
        {
            Vector2 d = to - from;
            float length = d.magnitude;
            for (float t = skip; t < length; t += 2f)
            {
                Vector2 q = from + d * (t / length);
                SpPaint(q.x, q.y, 1.3f, TerrainModifier.PaintType.Dirt);
            }
        }

        // The game's own ground paint (nothing reshaped), kept on the islet's terrain (DraftGround).
        static void SpPaint(float x, float z, float radius, TerrainModifier.PaintType paint)
        {
            GameObject go = new GameObject("IoV_Paint");
            go.transform.SetParent(groupAll, false);
            go.transform.localPosition = new Vector3(x, Height(x, z), z);
            TerrainModifier modifier = go.AddComponent<TerrainModifier>();
            modifier.m_level = false;
            modifier.m_smooth = false;
            modifier.m_paintCleared = true;
            modifier.m_paintHeightCheck = false;
            modifier.m_paintType = paint;
            modifier.m_paintRadius = radius;
            built++;
        }

        // When the frames have been turned into place: the real pieces of the player's house, in the islet's space.
        internal static void SpiritHomeToIslet()
        {
            HomePieces.Clear();
            Quaternion turn = Quaternion.Euler(0f, homePose.Yaw, 0f);
            foreach (HomePiece h in homeLocal)
                HomePieces.Add(new HomePiece { Prefab = h.Prefab, At = homePose.At + turn * h.At, Yaw = homePose.Yaw + h.Yaw, AlongZ = h.AlongZ });
        }

        // ---- pieces ----

        // A still copy of the first of these the game has, its bottom (or top) at `at` in its frame, turned yaw;
        // wear: the stage's look of a building piece (0 new, 1 weathered, 2 broken; -1 as it comes); a fire lit,
        // or cold.
        static GameObject SpProp(string[] names, Vector3 at, float yaw, bool byTop, int wear = -1, bool lit = false, bool cold = false)
        {
            GameObject prefab = Find(names);
            if (prefab == null) { missing++; Plugin.Debug("Brim village: none of " + string.Join(", ", names)); return null; }
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (wear >= 0) Wear(go, wear);
            if (lit) LightFires(go);
            else if (cold) Cold(go);
            Strip(go);
            go.transform.localPosition = Vector3.zero;
            Move(go, LocalBounds(go), at, byTop);
            built++;
            return go;
        }

        // A seat turned so its sitter looks along look (by the game's own seat on it).
        static GameObject SpSeat(string[] names, Vector3 at, Vector3 look)
        {
            Vector3 own = SeatFacing(Find(names));
            if (own == Vector3.zero) Plugin.Debug("Brim village: " + names[0] + " has no seat to face by");
            return SpProp(names, at, YawFor(own, look), false);
        }

        static void Wear(GameObject go, int wear)
        {
            WearNTear w = go.GetComponent<WearNTear>();
            if (w == null) return;
            GameObject want = wear == 2 ? (w.m_broken ?? w.m_worn ?? w.m_new) : wear == 1 ? (w.m_worn ?? w.m_new) : w.m_new;
            if (want == null) return;
            foreach (GameObject look in new[] { w.m_new, w.m_worn, w.m_broken })
                if (look != null && look != want) look.SetActive(false);
            want.SetActive(true);
        }

        static void Cold(GameObject go)
        {
            foreach (Fireplace fire in go.GetComponentsInChildren<Fireplace>(true))
            {
                foreach (GameObject state in new[] { fire.m_enabledObject, fire.m_enabledObjectLow, fire.m_enabledObjectHigh, fire.m_halfObject })
                    if (state != null && state != fire.m_emptyObject) state.SetActive(false);
                if (fire.m_emptyObject != null) fire.m_emptyObject.SetActive(true);
            }
        }

        // Turned to lie along its frame's z if it came out lying across it, and put back in place.
        static void SpAlongZ(GameObject go, Vector3 at)
        {
            if (go == null) return;
            Bounds b = LocalBounds(go);
            if (b.size.x <= b.size.z) return;
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * go.transform.localRotation;
            go.transform.localPosition = Vector3.zero;
            Move(go, LocalBounds(go), at, false);
        }
    }
}
