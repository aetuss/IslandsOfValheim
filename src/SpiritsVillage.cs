using System.Collections.Generic;
using UnityEngine;

namespace IslandsOfValheim
{
    // The Brim village (Valhalla arc, T4): the Brim Folk's old home on a large islet of the game's terrain, in
    // sight of Sævar's harbour. It is there from the start, ruined; donations at its altar restore it in
    // stages (SpiritsIslet):
    //   0 ruined:   broken walls, no roofs, cold fires, rubble and weeds;
    //   1 cleared:  the walls whole again (weathered), the hall roofed, the first fires lit;
    //   2 rebuilt:  every house roofed and new, braziers and banners, the hall's feast laid, the tower's beacon
    //               lit; and the player's house, beside the hall, with its real bed, chests and fire.
    //   (3, the people's return, comes with T5.)
    // Each stage's look is its own group in the location, shown by SpiritStages from the world's keys; what
    // every stage shares (floors, corner posts, the dock, trees) stands outside them. First layout in code, to be
    // corrected together from photos (as Valhalla's was).
    static partial class IsletBuilder
    {
        internal const string SpiritsName = "IoV_Story_Spirits";
        internal const float SpiritsHeight = 4.5f;
        internal static readonly LargeKind SpiritsKind = new LargeKind
        {
            Tier = IsletTier.All[0], Variant = 1, Height = SpiritsHeight, Scale = 3.4f, Layout = Layout.Village,
            Peaceful = true, Guardian = new string[0], Life = new Life[0], Custom = BuildVillage,
        };

        // Where the altar stands (it is networked: it finds the islet's middle from itself), and the real pieces
        // of the player's house, put there by the altar when the village is rebuilt; both in the islet's space.
        internal static Vector3 SpiritAltarAt;
        internal struct HomePiece { internal string[] Prefab; internal Vector3 At; internal float Yaw; }
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
        static float wallWidth = 2f, wallHeight = 2f, tile = 2f;

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

            // How far the village may reach: its plan is drawn for a plateau of 28 m, scaled to this one's.
            float p = kind.Plateau;
            kind.NoBuildRadius = p + 4f; // the village is the people's (the yard excepted: SpiritYard)
            float s0 = Mathf.Clamp(p / 28f, 0.75f, 1.2f);
            Plugin.Debug("Brim village: plateau " + p.ToString("F1") + " m, shore " + kind.Shore.ToString("F1") + " m, plan scaled " + s0.ToString("F2"));
            System.Func<float, float, Vector2> at = (x, z) => new Vector2(x * s0, z * s0);

            // The hall north of the square, its door to it; the player's house east of the square, beside the
            // hall; four houses round the rest; the old watchtower behind them; the yard by the player's house.
            Building(at(0f, 14f), 18f, 10f, 3, Hall, true);
            Building(at(14f, 4f), 10f, 8f, 2, PlayerHouse, false, true);
            Building(at(-14f, 7f), 8f, 6f, 2, House);
            Building(at(-15f, -8f), 8f, 6f, 2, House);
            Building(at(-6f, -16f), 8f, 6f, 2, House);
            Building(at(6f, -17f), 8f, 6f, 2, House);
            Square();
            Tower(at(-9f, 21f));
            Vector2 yard = at(17f, -6f);
            SpiritYardAt = new Vector3(yard.x, Height(yard.x, yard.y), yard.y);
            SpYard(yard);
            parent = groupAll;
            PlaceDock(cliffDeg + 180f);
            Trees(kind);

            // Every building's frames to where it stands (built square to the islet, so measured bounds held).
            foreach (FramePose f in frames)
            {
                f.Frame.localPosition = f.At;
                f.Frame.localRotation = Quaternion.Euler(0f, f.Yaw, 0f);
            }
            SpiritHomeToIslet();
            parent = top;
            SpiritAltarAt =new Vector3(0f, Height(0f, 1.5f * s0), 1.5f * s0);
            PlaceNetworked(SpiritsIslet.AltarName, SpiritAltarAt, 0f);
            Plugin.Debug("Brim village: " + frames.Count + " frames, " + HomePieces.Count + " pieces of the player's house, altar at " + SpiritAltarAt.ToString("F1"));
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

        // L along its own z (its door at -z, towards the square), W across; rows of walls high. inside(stage)
        // fills it for each stage (stage -1: what every stage shares), its frame the parent.
        static void Building(Vector2 at, float length, float width, int rows, System.Action<int, float, float> inside, bool hall = false, bool home = false)
        {
            float yaw = Mathf.Round(Mathf.Atan2(at.x, at.y) * Mathf.Rad2Deg / 5f) * 5f;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            float floor = float.MinValue;
            foreach (Vector3 c in new[] { new Vector3(-width, 0f, -length), new Vector3(width, 0f, -length), new Vector3(-width, 0f, length), new Vector3(width, 0f, length), Vector3.zero })
            {
                Vector3 w = turn * (c * 0.5f);
                floor = Mathf.Max(floor, Height(at.x + w.x, at.y + w.z));
            }
            floor += 0.25f;
            Vector3 pose = new Vector3(at.x, floor, at.y);

            parent = Frame(groupAll, pose, yaw);
            Floor(length, width);
            Corners(length, width, rows * wallHeight);
            inside(-1, length, width);
            for (int stage = 0; stage < 3; stage++)
            {
                parent = Frame(stageGroups[stage], pose, yaw);
                Walls(length, width, rows, stage);
                if (stage == 2 || (stage == 1 && hall))
                {
                    Gables(length, width, rows);
                    Roof(rows * wallHeight, width * 0.5f + 0.15f, -length * 0.5f, length * 0.5f);
                }
                inside(stage, length, width);
            }
            if (home) homePose = new FramePose { At = pose, Yaw = yaw };
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
                        if (villageRandom.NextDouble() >= gone) SpProp(new[] { "woodwall" }, new Vector3(x, y, z), 90f, false, wear);
                foreach (float z in new[] { -length * 0.5f, length * 0.5f })
                    for (float x = -width * 0.5f + wallWidth * 0.5f; x < width * 0.5f; x += wallWidth)
                    {
                        bool door = z < 0f && Mathf.Abs(x) < wallWidth * 0.6f && (r == 0 || (r == 1 && rows > 2));
                        if (!door && villageRandom.NextDouble() >= gone) SpProp(new[] { "woodwall" }, new Vector3(x, y, z), 0f, false, wear);
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
                        SpProp(new[] { "woodwall" }, new Vector3(x, y, z), 0f, false, 0);
            }
        }

        // ---- what's inside ----

        static void Hall(int stage, float length, float width)
        {
            float back = length * 0.5f;
            switch (stage)
            {
                case 0:
                    SpProp(new[] { "fire_pit" }, new Vector3(0f, 0f, -back + 5f), 0f, false, -1, false, true);
                    Rubble(length, width, 4);
                    break;
                case 1:
                    SpProp(new[] { "hearth", "fire_pit" }, new Vector3(0f, 0f, -back + 5f), 0f, false, -1, true);
                    break;
                case 2:
                    foreach (float z in new[] { -back + 5f, back - 6f })
                        SpProp(new[] { "hearth", "fire_pit" }, new Vector3(0f, 0f, z), 0f, false, -1, true);
                    foreach (float side in new[] { -1f, 1f })
                        for (float z = -back + 3.5f; z < back - 3f; z += 4.5f)
                        {
                            float x = side * (width * 0.5f - 2f);
                            SpAlongZ(SpProp(new[] { "piece_table_oak", "piece_table" }, new Vector3(x, 0f, z), 0f, false), new Vector3(x, 0f, z));
                            foreach (float off in new[] { -1.15f, 1.15f })
                                SpAlongZ(SpProp(new[] { "piece_bench01" }, new Vector3(x + off, 0f, z), 0f, false), new Vector3(x + off, 0f, z));
                        }
                    SpProp(new[] { "piece_throne01", "piece_chair02" }, new Vector3(0f, 0f, back - 1.6f), 180f, false);
                    foreach (float x in new[] { -2.5f, 2.5f })
                        SpProp(new[] { "piece_banner01", "piece_banner02" }, new Vector3(x, 2.8f, back - 0.35f), 180f, false);
                    Doorway(length);
                    break;
            }
        }

        static void House(int stage, float length, float width)
        {
            switch (stage)
            {
                case 0:
                    SpProp(new[] { "fire_pit" }, new Vector3(0f, 0f, 0.5f), 0f, false, -1, false, true);
                    Rubble(length, width, 2);
                    break;
                case 2:
                    SpProp(new[] { "fire_pit" }, new Vector3(0f, 0f, 0.5f), 0f, false, -1, true);
                    SpProp(new[] { "piece_table" }, new Vector3(-width * 0.5f + 1.2f, 0f, length * 0.5f - 1.6f), 90f, false);
                    SpProp(new[] { "piece_bench01" }, new Vector3(-width * 0.5f + 2.4f, 0f, length * 0.5f - 1.6f), 90f, false);
                    SpProp(new[] { "piece_bed02", "bed" }, new Vector3(width * 0.5f - 1.2f, 0f, length * 0.5f - 1.5f), 180f, false);
                    Doorway(length);
                    break;
            }
        }

        // The player's: the same as a house, but its bed, chests and fire are the game's own, real and working,
        // put there by the altar once the village is rebuilt (SpiritsIslet); here only what's still.
        static void PlayerHouse(int stage, float length, float width)
        {
            switch (stage)
            {
                case -1:
                    // (where the real pieces go, in the house's own space; turned into the islet's when it stands)
                    homeLocal.Clear();
                    homeLocal.Add(new HomePiece { Prefab = new[] { "bed" }, At = new Vector3(-width * 0.5f + 1.3f, 0f, length * 0.5f - 1.4f), Yaw = 0f });
                    homeLocal.Add(new HomePiece { Prefab = new[] { "bed" }, At = new Vector3(width * 0.5f - 1.3f, 0f, length * 0.5f - 1.4f), Yaw = 0f });
                    homeLocal.Add(new HomePiece { Prefab = new[] { "piece_chest", "piece_chest_wood" }, At = new Vector3(-width * 0.5f + 0.7f, 0f, 0f), Yaw = 90f });
                    homeLocal.Add(new HomePiece { Prefab = new[] { "piece_chest", "piece_chest_wood" }, At = new Vector3(width * 0.5f - 0.7f, 0f, 0f), Yaw = -90f });
                    homeLocal.Add(new HomePiece { Prefab = new[] { "piece_brazierfloor01" }, At = new Vector3(0f, 0f, length * 0.5f - 1.6f), Yaw = 0f });
                    homeLocal.Add(new HomePiece { Prefab = new[] { "piece_brazierfloor01" }, At = new Vector3(0f, 0f, -length * 0.5f + 2f), Yaw = 0f });
                    break;
                case 0:
                    Rubble(length, width, 3);
                    break;
                case 2:
                    SpProp(new[] { "piece_table" }, new Vector3(0f, 0f, 0.8f), 90f, false);
                    SpProp(new[] { "piece_banner01", "piece_banner02" }, new Vector3(0f, 2.6f, length * 0.5f - 0.35f), 180f, false);
                    Doorway(length);
                    break;
            }
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

        static void Sized(GameObject go, float size, Vector3 at)
        {
            if (go == null) return;
            Bounds b = LocalBounds(go);
            float extent = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (extent > 0.01f) go.transform.localScale *= size / extent;
            go.transform.localPosition = Vector3.zero;
            Move(go, LocalBounds(go), at, false);
        }

        // ---- the square, the tower, the yard, the trees ----

        static void Square()
        {
            // a fire in the middle: cold in the ruin, then lit, a ring of benches round it once rebuilt
            Vector3 fire = new Vector3(0f, Height(0f, -5f), -5f);
            parent = stageGroups[0];
            SpProp(new[] { "bonfire", "fire_pit" }, fire, 0f, false, -1, false, true);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 15f;
                Vector3 p = fire + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 6f);
                p.y = Height(p.x, p.z);
                SpProp(new[] { "Bush01", "Bush02_en" }, p, a, false);
            }
            foreach (int stage in new[] { 1, 2 })
            {
                parent = stageGroups[stage];
                SpProp(new[] { "bonfire", "fire_pit" }, fire, 0f, false, -1, true);
                if (stage < 2) continue;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f + 30f;
                    Vector3 p = fire + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 4.2f);
                    p.y = Height(p.x, p.z);
                    SpProp(new[] { "piece_bench01" }, p, a + 90f, false);
                }
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f + 22.5f;
                    Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 10f);
                    p.y = Height(p.x, p.z);
                    SpProp(new[] { "piece_groundtorch_wood", "piece_groundtorch" }, p, 0f, false, -1, true);
                }
            }
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

        // The player's yard: open ground, only its corner posts (lanterns once rebuilt).
        static void SpYard(Vector2 at)
        {
            for (int stage = 1; stage < 3; stage++)
            {
                parent = stageGroups[stage];
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 90f + 45f;
                    Vector3 p = new Vector3(at.x, 0f, at.y) + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, SpiritYardRadius);
                    p.y = Height(p.x, p.z);
                    SpProp(stage == 2 ? new[] { "piece_groundtorch_wood", "piece_groundtorch" } : new[] { "wood_pole2", "wood_pole" }, p, 0f, false, -1, stage == 2);
                }
            }
            GameObject marker = new GameObject("IoV_SpiritYard");
            marker.transform.SetParent(groupAll, false);
            marker.transform.localPosition = SpiritYardAt;
            marker.AddComponent<SpiritYard>();
        }

        // Trees round the village, on its slopes, never on the plateau where the houses stand.
        static void Trees(LargeKind kind)
        {
            string[] trees = { "Beech1", "Birch1", "Oak1" };
            for (int i = 0; i < 26; i++)
            {
                float a = (float)villageRandom.NextDouble() * Mathf.PI * 2f;
                float r = kind.Plateau + 4f + (float)villageRandom.NextDouble() * Mathf.Max(2f, kind.Shore - kind.Plateau - 12f);
                Vector2 p = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
                if (Height(p.x, p.y) < 1.5f) continue;
                OnGround(new[] { trees[i % trees.Length] }, p, a * Mathf.Rad2Deg, 0f, 0.05f, true);
            }
        }

        // When the frames have been turned into place: the real pieces of the player's house, in the islet's space.
        internal static void SpiritHomeToIslet()
        {
            HomePieces.Clear();
            Quaternion turn = Quaternion.Euler(0f, homePose.Yaw, 0f);
            foreach (HomePiece h in homeLocal)
                HomePieces.Add(new HomePiece { Prefab = h.Prefab, At = homePose.At + turn * h.At, Yaw = homePose.Yaw + h.Yaw });
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
