using System.Collections.Generic;
using UnityEngine;

namespace IslandsOfValheim
{
    // The Brim village (Valhalla arc, T4): the Brim Folk's old home on a large islet of the game's terrain, in
    // sight of Sævar's harbour. It is there from the start, ruined; donations at its altar restore it in
    // stages (SpiritsIslet):
    //   0 ruined:   broken walls, no roofs, cold fires, rubble and weeds, a wreck on the shore, the watchtower a
    //               stump;
    //   1 cleared:  the walls whole again (weathered), the hall roofed, the first fires lit, wood and stone
    //               piled to build with, the tower's frame up;
    //   2 rebuilt:  every house roofed, new and furnished for its trade, the hall's feast laid, the field sown,
    //               the tower roofed and its beacon lit, a ship at the jetty, torches along the paths, and the
    //               altar a great fire; and the player's house, with its real beds, chests and fires.
    //   (3, the people's return, comes with T5.)
    // Each stage's look is its own group in the location, shown by SpiritStages from the world's keys; what
    // every stage shares (floors, corner posts, paths, the dock, trees) stands outside them.
    // The houses differ (the user found 1.10.89's alike and made by a machine): each its own size, walls (stone
    // footings, log walls, planks), roof (dark shingles or thatch), and a little off square to the others.
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
        internal static readonly List<HomePiece> DoorPieces = new List<HomePiece>();
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
        static float planScale = 1f;
        static readonly Dictionary<string, Vector2> pieceSize = new Dictionary<string, Vector2>(); // width, height

        enum Trade { Hall, Home, Smith, Weaver, Fisher, Brewer }

        const string Planks = "woodwall", Stone = "stone_wall_2x1", Logs = "wood_wall_log";
        const string DarkRoof = "darkwood_roof_45", DarkCap = "darkwood_roof_top_45", Thatch = "wood_roof_45", ThatchCap = "wood_roof_top_45";

        // How a building is made: its size, its rows of wall from the ground up, its roof, its floor, how far off
        // facing the square it's turned.
        class Style
        {
            internal Trade Trade;
            internal float Length, Width;
            internal string[] Rows;
            internal string Shingle = DarkRoof, Cap = DarkCap, Floor = "wood_floor";
            internal float Turn;
        }

        static void BuildVillage(LargeKind kind)
        {
            villageRandom = new System.Random(7341);
            frames.Clear();
            HomePieces.Clear();
            DoorPieces.Clear();
            pieceSize.Clear();
            Transform top = parent;
            groupAll = SpGroup(top, "IoV_SpiritAll", true);
            for (int s = 0; s < 3; s++) stageGroups[s] = SpGroup(top, SpiritStages.GroupName(s), s == 0);
            GameObject stages = new GameObject("IoV_SpiritStages");
            stages.transform.SetParent(top, false);
            stages.AddComponent<SpiritStages>();

            // How far the village reaches: its plan is drawn for a plateau of 33 m, scaled to this one's.
            float p = kind.Plateau;
            kind.NoBuildRadius = p + 4f; // the village is the people's (the yard excepted: SpiritYard)
            planScale = Mathf.Clamp(p / 33f, 0.8f, 1.2f);
            Plugin.Debug("Brim village: plateau " + p.ToString("F1") + " m, shore " + kind.Shore.ToString("F1") + " m, plan scaled " + planScale.ToString("F2"));

            // The square with the altar in the middle; the hall north of it; the player's house east, beside the
            // hall, its yard beyond; four houses round the rest; the watchtower behind the hall's west end; the
            // memorial of the drowned to the north-east; the field to the west.
            Vector2 hall = At(0f, 20f), home = At(21f, 6f);
            Style hallStyle = new Style { Trade = Trade.Hall, Length = 22f, Width = 14f, Rows = new[] { Stone, Planks, Planks } };
            Style homeStyle = new Style { Trade = Trade.Home, Length = 12f, Width = 8f, Rows = new[] { Stone, Planks, Planks }, Turn = -6f };
            Vector2[] houses = { At(-22f, 8f), At(-19f, -15f), At(-3f, -24f), At(13f, -22f) };
            Style[] styles =
            {
                new Style { Trade = Trade.Smith, Length = 12f, Width = 8f, Rows = new[] { Stone, Stone, Planks }, Floor = "stone_floor_2x2", Turn = 8f },
                new Style { Trade = Trade.Weaver, Length = 10f, Width = 8f, Rows = new[] { Logs, Logs }, Shingle = Thatch, Cap = ThatchCap, Turn = -11f },
                new Style { Trade = Trade.Fisher, Length = 10f, Width = 6f, Rows = new[] { Planks, Planks }, Shingle = Thatch, Cap = ThatchCap, Turn = 7f },
                new Style { Trade = Trade.Brewer, Length = 14f, Width = 8f, Rows = new[] { Stone, Planks, Planks }, Turn = -9f },
            };
            Vector2 tower = At(-15f, 28f), yard = At(24f, -9f), field = At(-29f, -4f), memorial = At(18f, 23f);

            Building(hall, hallStyle);
            Building(home, homeStyle);
            for (int i = 0; i < houses.Length; i++) Building(houses[i], styles[i]);
            float altarTop = Square();
            Tower(tower);
            SpiritYardAt = new Vector3(yard.x, Height(yard.x, yard.y), yard.y);
            SpYard(yard);
            Field(field);
            Memorial(memorial);
            Commons();
            float dock = cliffDeg + 180f;
            parent = groupAll;
            PlaceDock(dock);
            Trees(kind);
            Slopes(kind);
            // the paths: from the square to every door, the tower, the yard, the field, the memorial and the
            // jetty; torches along them once rebuilt
            List<Vector2> ends = new List<Vector2>();
            ends.Add(Door(hall, hallStyle));
            ends.Add(Door(home, homeStyle));
            for (int i = 0; i < houses.Length; i++) ends.Add(Door(houses[i], styles[i]));
            ends.Add(tower);
            ends.Add(yard);
            ends.Add(field);
            ends.Add(memorial);
            ends.Add(Polar(dock, PlateauAt(dock)));
            foreach (Vector2 e in ends) Path(Vector2.zero, e, 8.5f);
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

        // A building faces the square (its door, its own -z, towards the middle), turned a little off as its
        // style has it.
        static float Facing(Vector2 at, float turn) { return Mathf.Atan2(at.x, at.y) * Mathf.Rad2Deg + turn; }

        // Just outside a building's door.
        static Vector2 Door(Vector2 at, Style style)
        {
            Vector3 d = Quaternion.Euler(0f, Facing(at, style.Turn), 0f) * new Vector3(0f, 0f, -style.Length * 0.5f - 1.5f);
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

        // A building piece's width and height, measured once (2 x 2 m for a plank wall, 2 x 1 for stone...).
        static Vector2 Size(string name)
        {
            Vector2 size;
            if (pieceSize.TryGetValue(name, out size)) return size;
            Transform keep = parent;
            parent = groupAll;
            GameObject probe = SpProp(new[] { name }, Vector3.zero, 0f, false);
            parent = keep;
            size = new Vector2(2f, 2f);
            if (probe != null)
            {
                Bounds b = LocalBounds(probe);
                size = new Vector2(Mathf.Max(0.5f, Mathf.Round(Mathf.Max(b.size.x, b.size.z) * 2f) / 2f), Mathf.Max(0.5f, Mathf.Round(b.size.y * 2f) / 2f));
                Object.DestroyImmediate(probe);
                built--;
            }
            pieceSize[name] = size;
            Plugin.Debug("Brim village: " + name + " is " + size.x + " x " + size.y + " m");
            return size;
        }

        // The first that the game has (a wall kind may be missing: plank walls then).
        static string Have(string name, string otherwise) { return Find(new[] { name }) != null ? name : otherwise; }

        // ---- a building ----

        static void Building(Vector2 at, Style style)
        {
            float yaw = Facing(at, style.Turn);
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            float length = style.Length, width = style.Width;
            float floor = float.MinValue;
            foreach (Vector3 c in new[] { new Vector3(-width, 0f, -length), new Vector3(width, 0f, -length), new Vector3(-width, 0f, length), new Vector3(width, 0f, length), Vector3.zero })
            {
                Vector3 w = turn * (c * 0.5f);
                floor = Mathf.Max(floor, Height(at.x + w.x, at.y + w.z));
            }
            floor += 0.05f; // on the ground
            Vector3 pose = new Vector3(at.x, floor, at.y);
            string[] rows = new string[style.Rows.Length];
            float wallTop = 0f;
            for (int r = 0; r < rows.Length; r++) { rows[r] = Have(style.Rows[r], Planks); wallTop += Size(rows[r]).y; }

            parent = Frame(groupAll, pose, yaw);
            Floor(length, width, style.Floor);
            Corners(length, width, wallTop);
            // the doorway: a wooden door's (2 m, where a wall piece would be), or the hall's great gate's (its own
            // size, in the middle)
            GameObject gate = style.Trade == Trade.Hall ? Find(new[] { "darkwood_gate" }) : null;
            if (gate != null)
            {
                Vector3 g = SpiritsIslet.OwnSize(gate);
                doorAt = 0f;
                doorWide = Mathf.Max(g.x, g.z) + 0.1f;
                doorHigh = Mathf.Min(g.y + 0.05f, wallTop - 0.3f);
                Plugin.Debug("Brim village: the hall's gate is " + g.ToString("F2"));
            }
            else
            {
                doorAt = DoorX(width, Size(rows[0]).x);
                doorWide = Size(rows[0]).x;
                doorHigh = 2f;
            }
            if (style.Trade == Trade.Home) HomePlan(length, width);
            for (int stage = 0; stage < 3; stage++)
            {
                parent = Frame(stageGroups[stage], pose, yaw);
                Walls(length, width, rows, stage);
                if (stage == 2 || (stage == 1 && style.Trade == Trade.Hall))
                {
                    Gables(length, width, wallTop, stage == 2 ? 0 : 1);
                    SpRoof(Have(style.Shingle, DarkRoof), Have(style.Cap, DarkCap), wallTop, width * 0.5f + 0.15f, -length * 0.5f, length * 0.5f);
                }
                Inside(style.Trade, stage, length, width);
                Outside(style.Trade, stage, length, width);
            }
            if (style.Trade == Trade.Home) homePose = new FramePose { At = pose, Yaw = yaw };
            // its door, in the islet's space (the altar puts the real ones in: SpiritsIslet.BuildDoors)
            Vector3 door = pose + turn * new Vector3(doorAt, 0f, -length * 0.5f);
            DoorPieces.Add(new HomePiece { Prefab = new[] { gate != null ? SpiritsIslet.GateName : SpiritsIslet.DoorName, SpiritsIslet.DoorName }, At = door, Yaw = yaw });
        }

        static FramePose homePose;
        // the doorway of the building being built: its middle's x, its width and height (braziers either side)
        static float doorAt, doorWide = 2f, doorHigh = 2f;

        static void Floor(float length, float width, string kind)
        {
            string name = Have(kind, "wood_floor");
            float t = Size(name).x;
            for (float x = -width * 0.5f + t * 0.5f; x < width * 0.5f; x += t)
                for (float z = -length * 0.5f + t * 0.5f; z < length * 0.5f; z += t)
                    SpProp(new[] { name }, new Vector3(x, 0f, z), 0f, true, 1);
        }

        static void Corners(float length, float width, float height)
        {
            foreach (float x in new[] { -width * 0.5f, width * 0.5f })
                foreach (float z in new[] { -length * 0.5f, length * 0.5f })
                    Pole(new Vector3(x, 0f, z), height + 0.3f, 0);
        }

        // A dark pole as tall as wanted, its foot at foot.
        static GameObject Pole(Vector3 foot, float height, int wear)
        {
            GameObject pole = SpProp(new[] { "darkwood_pole4", "wood_pole4" }, foot, 0f, false, wear);
            if (pole == null) return null;
            Bounds b = LocalBounds(pole);
            pole.transform.localScale = Vector3.Scale(pole.transform.localScale, new Vector3(1f, height / Mathf.Max(0.1f, b.size.y), 1f));
            pole.transform.localPosition = Vector3.zero;
            Move(pole, LocalBounds(pole), foot, false);
            return pole;
        }

        // A wall turned so its rough side (its frame of beams, the game's wall's own -z) faces in.
        static float Inward(float x, float z, bool side) { return side ? (x > 0f ? 90f : -90f) : (z < 0f ? 180f : 0f); }

        // Walls all round, row on row (each row its own kind), but for the door (2 m high, 2 wide); worn as the
        // stage is (broken, weathered, new). A ruin's are gappy, its upper rows most.
        static void Walls(float length, float width, string[] rows, int stage)
        {
            int wear = stage == 0 ? 2 : stage == 1 ? 1 : 0;
            float y = 0f;
            float openL = doorAt - doorWide * 0.5f, openR = doorAt + doorWide * 0.5f;
            for (int r = 0; r < rows.Length; r++)
            {
                Vector2 size = Size(rows[r]);
                double gone = stage == 0 ? (r == 0 ? 0.1 : r == rows.Length - 1 ? 0.55 : 0.3) : 0.0;
                foreach (float x in new[] { -width * 0.5f, width * 0.5f })
                    for (float z = -length * 0.5f + size.x * 0.5f; z < length * 0.5f; z += size.x)
                        if (villageRandom.NextDouble() >= gone) SpProp(new[] { rows[r] }, new Vector3(x, y, z), Inward(x, z, true), false, wear);
                foreach (float z in new[] { -length * 0.5f, length * 0.5f })
                    for (float x = -width * 0.5f + size.x * 0.5f; x < width * 0.5f; x += size.x)
                    {
                        float l = x - size.x * 0.5f, rr = x + size.x * 0.5f;
                        bool door = z < 0f && y < doorHigh - 0.05f && rr > openL + 0.01f && l < openR - 0.01f;
                        if (!door)
                        {
                            if (villageRandom.NextDouble() >= gone) SpProp(new[] { rows[r] }, new Vector3(x, y, z), Inward(x, z, false), false, wear);
                            continue;
                        }
                        // a piece the doorway cuts through: what's left of it either side, and above the doorway
                        if (openL - l > 0.05f) Board(rows[r], size, (l + openL) * 0.5f, y, openL - l, size.y, z, wear);
                        if (rr - openR > 0.05f) Board(rows[r], size, (openR + rr) * 0.5f, y, rr - openR, size.y, z, wear);
                        float top = y + size.y;
                        if (top > doorHigh + 0.05f)
                        {
                            float a = Mathf.Max(l, openL), b = Mathf.Min(rr, openR);
                            Board(rows[r], size, (a + b) * 0.5f, doorHigh, b - a, top - doorHigh, z, wear);
                        }
                    }
                y += size.y;
            }
        }

        // Part of a wall piece: one stretched to the width and height wanted, its foot at y.
        static void Board(string row, Vector2 size, float x, float y, float w, float h, float z, int wear)
        {
            Vector3 at = new Vector3(x, y, z);
            GameObject go = SpProp(new[] { row }, at, Inward(x, z, false), false, wear);
            if (go == null) return;
            go.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
            go.transform.localPosition = Vector3.zero;
            Move(go, LocalBounds(go), at, false);
        }

        // Where a door goes in the front wall: the wall piece nearest the middle (1.10.90 left two out where the
        // pieces met in the middle: a 4 m gap).
        static float DoorX(float width, float piece)
        {
            float best = 0f, nearest = float.MaxValue;
            for (float x = -width * 0.5f + piece * 0.5f; x < width * 0.5f; x += piece)
                if (Mathf.Abs(x) < nearest - 0.01f) { nearest = Mathf.Abs(x); best = x; }
            return best;
        }

        // The ends above the walls, closed up to the 45 degree roof: narrow boards, each as high as the roof over
        // its outer edge (1.10.89's whole wall pieces stopped a step short, and left the triangles open).
        static void Gables(float length, float width, float wallTop, int wear)
        {
            float half = width * 0.5f + 0.15f, board = 0.5f;
            Vector2 plank = Size(Planks);
            foreach (float z in new[] { -length * 0.5f, length * 0.5f })
                for (float x = -half + board * 0.5f; x < half; x += board)
                {
                    float h = half - (Mathf.Abs(x) + board * 0.5f) + 0.1f;
                    if (h < 0.15f) continue;
                    GameObject go = SpProp(new[] { Planks }, new Vector3(x, wallTop, z), Inward(x, z, false), false, wear);
                    if (go == null) continue;
                    go.transform.localScale = new Vector3(board / plank.x, h / plank.y, 1f);
                    go.transform.localPosition = Vector3.zero;
                    Move(go, LocalBounds(go), new Vector3(x, wallTop, z), false);
                }
        }

        // The roof: shingles at 45 degrees from the walls' tops up to the ridge cap, by the pieces' own snap
        // points (as Valhalla's hall), of whichever kind.
        static void SpRoof(string shingle, string capName, float beamTop, float beamOuter, float start, float end)
        {
            GameObject prefab = Find(new[] { shingle });
            GameObject capPrefab = Find(new[] { capName });
            if (prefab == null) return;
            Vector3 down = Downhill(prefab);
            float yawL = down != Vector3.zero ? YawFor(down, Vector3.left) : 90f;
            float yawR = down != Vector3.zero ? YawFor(down, Vector3.right) : -90f;
            List<Vector3> snaps = SnapPoints(prefab);
            if (snaps.Count < 4) { Plugin.Log.LogWarning("Brim village: " + shingle + " has " + snaps.Count + " snap points; no roof"); return; }
            float slope = 1f, along = 2f;
            foreach (float side in new[] { -1f, 1f })
            {
                Quaternion turn = Quaternion.Euler(0f, side < 0f ? yawL : yawR, 0f);
                Vector3 lower, upper;
                float span;
                Edges(snaps, turn, out lower, out upper, out span);
                Vector3 step = upper - lower;
                float run = Mathf.Abs(step.x), rise = step.y;
                if (run < 0.2f || rise < 0.2f || span < 0.2f) return;
                along = span;
                slope = rise / run;
                int rowsOf = Mathf.Max(1, Mathf.RoundToInt(beamOuter / run));
                for (int k = 0; k < rowsOf; k++)
                    for (float z = start - 1f + along * 0.5f; z < end + 1f; z += along)
                    {
                        Vector3 edge = new Vector3(side * beamOuter, beamTop, z) + new Vector3(-side * run, rise, 0f) * k;
                        SkyPivot(shingle, edge - turn * Mid(snaps, true), turn);
                    }
            }
            List<Vector3> capSnaps = capPrefab != null ? SnapPoints(capPrefab) : new List<Vector3>();
            if (capSnaps.Count < 2) return;
            Quaternion capTurn = Quaternion.Euler(0f, yawL, 0f);
            Vector3 capLow = Mid(capSnaps, true);
            float capHalf = 0f;
            foreach (Vector3 s in capSnaps) capHalf = Mathf.Max(capHalf, Mathf.Abs((capTurn * s).x - (capTurn * capLow).x));
            float capY = beamTop + (beamOuter - capHalf) * slope;
            for (float z = start - 1f + along * 0.5f; z < end + 1f; z += along)
                SkyPivot(capName, new Vector3(0f, capY, z) - capTurn * capLow, capTurn);
        }

        // ---- inside and round each building ----

        static void Inside(Trade trade, int stage, float length, float width)
        {
            float back = length * 0.5f, side = width * 0.5f;
            float wallIn = side - 0.12f, backIn = back - 0.12f; // the walls' inner faces
            if (stage == 0)
            {
                Rubble(length, width, trade == Trade.Hall ? 6 : 3);
                if (trade == Trade.Hall) SpProp(new[] { "hearth" }, new Vector3(0f, 0f, -back + 7f), 0f, false, -1, false, true);
                return;
            }
            if (trade == Trade.Hall)
            {
                if (stage == 1) { SpProp(new[] { "hearth" }, new Vector3(0f, 0f, -back + 7f), 0f, false, -1, true); return; }
                // two fires down the middle, a long table either side well clear of them, benches both sides
                foreach (float z in new[] { -back + 7f, back - 9f })
                    SpProp(new[] { "hearth" }, new Vector3(0f, 0f, z), 0f, false, -1, true);
                foreach (float s in new[] { -1f, 1f })
                    for (float z = -back + 4f; z < back - 5f; z += 4.5f)
                    {
                        float x = s * (side - 2.6f);
                        SpAlongZ(SpProp(new[] { "piece_table_oak", "piece_table" }, new Vector3(x, 0f, z), 0f, false), new Vector3(x, 0f, z));
                        foreach (float off in new[] { -1.15f, 1.15f })
                            SpSeat(new[] { "piece_bench01" }, new Vector3(x + off, 0f, z), new Vector3(-Mathf.Sign(off), 0f, 0f));
                    }
                // the high seat on a stone dais at the far end, blue fires either side, a fur before it
                float daisZ = back - 2f;
                for (float x = -2f; x <= 2.01f; x += 2f)
                    SpProp(new[] { "stone_floor_2x2" }, new Vector3(x, 0.35f, daisZ), 0f, true);
                SpSeat(new[] { "piece_throne01", "piece_chair02" }, new Vector3(0f, 0.35f, daisZ), Vector3.back);
                foreach (float x in new[] { -2.4f, 2.4f })
                    SpProp(new[] { "piece_brazierfloor02", "piece_brazierfloor01" }, new Vector3(x, 0.35f, daisZ), 0f, false, -1, true);
                SpProp(new[] { "rug_fur", "rug_wolf", "rug_deer" }, new Vector3(0f, 0.02f, daisZ - 2.8f), 0f, false);
                Doorway(length);
                return;
            }
            if (stage == 1) { if (trade != Trade.Home) Piles(length, width); return; }
            // Rebuilt: each house furnished for its trade, against its walls, its middle left free; a bed along
            // the back wall's corner, a torch by the door.
            switch (trade)
            {
                case Trade.Home:
                    {
                        GameObject t = SpProp(new[] { "piece_table" }, new Vector3(-wallIn + 1f, 0f, -0.5f), 0f, false);
                        SpAlongZ(t, new Vector3(-wallIn + 1f, 0f, -0.5f));
                        SpAgainst(t, Vector3.left, -wallIn);
                        SpSeat(new[] { "piece_bench01" }, new Vector3(-wallIn + 2.2f, 0f, -0.5f), Vector3.left);
                        SpProp(new[] { "rug_deer", "rug_fur" }, new Vector3(0f, 0.02f, 1f), 0f, false);
                        break;
                    }
                case Trade.Smith:
                    SpAgainst(SpAgainst(SpProp(new[] { "forge" }, new Vector3(-side + 1.5f, 0f, back - 1.5f), 180f, false), Vector3.forward, backIn), Vector3.left, -wallIn);
                    SpAgainst(SpProp(new[] { "forge_ext1", "forge_ext2" }, new Vector3(-side + 1f, 0f, 0f), 90f, false), Vector3.left, -wallIn);
                    SpAgainst(SpProp(new[] { "piece_workbench" }, new Vector3(side - 1f, 0f, -1.5f), -90f, false), Vector3.right, wallIn);
                    break;
                case Trade.Weaver:
                    SpAgainst(SpAgainst(SpProp(new[] { "piece_spinningwheel" }, new Vector3(-side + 1.2f, 0f, back - 1.5f), 90f, false), Vector3.left, -wallIn), Vector3.forward, backIn);
                    SpAgainst(SpProp(new[] { "piece_chest_wood" }, new Vector3(-side + 0.8f, 0f, 0f), 90f, false), Vector3.left, -wallIn);
                    SpAgainst(SpProp(new[] { "piece_chest_wood" }, new Vector3(-side + 0.8f, 0f, -1.4f), 90f, false), Vector3.left, -wallIn);
                    break;
                case Trade.Fisher:
                    SpAgainst(SpProp(new[] { "piece_cookingstation" }, new Vector3(-side + 1f, 0f, 0f), 90f, false), Vector3.left, -wallIn);
                    {
                        GameObject t = SpProp(new[] { "piece_table" }, new Vector3(-side + 1f, 0f, back - 1.6f), 0f, false);
                        SpAlongZ(t, new Vector3(-side + 1f, 0f, back - 1.6f));
                        SpAgainst(SpAgainst(t, Vector3.left, -wallIn), Vector3.forward, backIn);
                    }
                    SpAgainst(SpProp(new[] { "piece_chest_wood" }, new Vector3(side - 0.8f, 0f, -1.2f), -90f, false), Vector3.right, wallIn);
                    break;
                case Trade.Brewer:
                    SpAgainst(SpAgainst(SpProp(new[] { "fermenter" }, new Vector3(-side + 1.2f, 0f, back - 1.5f), 180f, false), Vector3.left, -wallIn), Vector3.forward, backIn);
                    SpAgainst(SpProp(new[] { "fermenter" }, new Vector3(-side + 1.2f, 0f, back - 3.6f), 180f, false), Vector3.left, -wallIn);
                    SpAgainst(SpProp(new[] { "piece_cauldron" }, new Vector3(-side + 1.2f, 0f, -1f), 90f, false), Vector3.left, -wallIn);
                    SpAgainst(SpProp(new[] { "barrell", "piece_chest_wood" }, new Vector3(side - 0.9f, 0f, -2.5f), 0f, false), Vector3.right, wallIn);
                    break;
            }
            if (trade != Trade.Home)
            {
                Vector3 bed = new Vector3(side - 1.2f, 0f, back - 1.6f);
                GameObject b = SpProp(new[] { "bed", "piece_bed02" }, bed, 0f, false);
                SpAlongZ(b, bed);
                SpAgainst(SpAgainst(b, Vector3.right, wallIn), Vector3.forward, backIn);
                SpProp(new[] { "piece_groundtorch_wood", "piece_groundtorch" }, new Vector3(side - 0.6f, 0f, -back + 0.8f), 0f, false, -1, true);
            }
            Doorway(length);
        }

        // Round the outside of a house: its trade's things by its walls once it's rebuilt (a smith's wood, a
        // fisher's racks, a brewer's barrels), wood piled by it while it's being rebuilt.
        static void Outside(Trade trade, int stage, float length, float width)
        {
            if (trade == Trade.Hall || trade == Trade.Home || stage == 0) return;
            float side = width * 0.5f + 0.9f;
            if (stage == 1) { SpProp(new[] { "wood_stack" }, new Vector3(side + 0.3f, 0f, 1f), 0f, false); return; }
            switch (trade)
            {
                case Trade.Smith:
                    SpProp(new[] { "wood_stack" }, new Vector3(side, 0f, 1.5f), 0f, false);
                    SpProp(new[] { "wood_core_stack", "wood_stack" }, new Vector3(side, 0f, -1f), 0f, false);
                    SpProp(new[] { "piece_chopping_block", "Beech_log" }, new Vector3(side + 1.6f, 0f, 0.2f), 30f, false);
                    break;
                case Trade.Weaver:
                    SpProp(new[] { "piece_beehive" }, new Vector3(-side - 0.3f, 0f, 1.5f), 90f, false);
                    SpProp(new[] { "piece_beehive" }, new Vector3(-side - 0.3f, 0f, -0.5f), 90f, false);
                    break;
                case Trade.Fisher:
                    SpProp(new[] { "piece_cookingstation" }, new Vector3(side + 0.4f, 0f, 1f), 90f, false);
                    SpProp(new[] { "barrell" }, new Vector3(side, 0f, -1.5f), 0f, false);
                    break;
                case Trade.Brewer:
                    foreach (float z in new[] { -2f, -0.8f, 0.4f })
                        SpProp(new[] { "barrell" }, new Vector3(side - 0.2f, 0f, z), z * 40f, false);
                    break;
            }
        }

        // Where the real pieces of the player's house go, in the house's own space (turned into the islet's
        // once it stands), each up against its wall by its own size: two beds in the back corners, two chests
        // by the right wall, a fire either side of the door.
        static void HomePlan(float length, float width)
        {
            float backIn = length * 0.5f - 0.12f, wallIn = width * 0.5f - 0.12f;
            homeLocal.Clear();
            Vector3 bed = HomeSize("bed"), chest = HomeSize("piece_chest");
            float bedW = Mathf.Min(bed.x, bed.z), bedL = Mathf.Max(bed.x, bed.z);
            foreach (float s in new[] { -1f, 1f })
                homeLocal.Add(new HomePiece { Prefab = new[] { "bed" }, At = new Vector3(s * (wallIn - bedW * 0.5f - 0.05f), 0f, backIn - bedL * 0.5f - 0.05f), AlongZ = true });
            float chestDepth = chest.z > 0.05f ? chest.z : 0.8f;
            foreach (float z in new[] { 0.6f, -1.2f })
                homeLocal.Add(new HomePiece { Prefab = new[] { "piece_chest", "piece_chest_wood" }, At = new Vector3(wallIn - chestDepth * 0.5f - 0.05f, 0f, z), Yaw = -90f });
            foreach (float s in new[] { -1f, 1f })
                homeLocal.Add(new HomePiece { Prefab = new[] { "piece_brazierfloor01" }, At = new Vector3(s * (wallIn - 0.7f), 0f, -length * 0.5f + 1.3f) });
        }

        static Vector3 HomeSize(string name)
        {
            GameObject prefab = Find(new[] { name });
            return prefab != null ? SpiritsIslet.OwnSize(prefab) : new Vector3(1.4f, 1f, 2.4f);
        }

        static readonly List<HomePiece> homeLocal = new List<HomePiece>();

        static void Doorway(float length)
        {
            foreach (float s in new[] { -1f, 1f })
                SpProp(new[] { "piece_brazierfloor01" }, new Vector3(doorAt + s * (doorWide * 0.5f + 0.9f), 0f, -length * 0.5f - 0.9f), 0f, false, -1, true);
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

        // ---- the square, the tower, the yard, the field, the memorial, the commons, the shore ----

        // The altar's platform of stone in the middle (gives back its top), the square paved round it, fires at
        // its corners (cold in the ruin), benches round it once rebuilt, and then the altar a great fire.
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
                GameObject fire = SpProp(new[] { "bonfire", "fire_pit" }, new Vector3(0f, top, 0f), 0f, false, -1, true);
                if (fire != null) { fire.transform.localScale *= 1.4f; fire.transform.localPosition = Vector3.zero; Move(fire, LocalBounds(fire), new Vector3(0f, top, 0f), false); }
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

        // The watchtower: four tall posts braced at half height, a platform with a low wall round it, a roof on
        // corner posts over it and its beacon. A stump in the ruin; its frame and platform once cleared; roofed
        // and lit once rebuilt.
        static void Tower(Vector2 at)
        {
            float ground = Height(at.x, at.y);
            const float half = 1.9f, deck = 8f;
            for (int stage = 0; stage < 3; stage++)
            {
                parent = stageGroups[stage];
                int wear = stage == 0 ? 2 : stage == 1 ? 1 : 0;
                foreach (float x in new[] { -half, half })
                    foreach (float z in new[] { -half, half })
                    {
                        if (stage == 0 && x > 0f && z > 0f) continue; // one post fallen
                        Pole(new Vector3(at.x + x, ground, at.y + z), stage == 0 ? 3.2f : deck + 0.1f, wear);
                    }
                if (stage == 0)
                {
                    SpProp(new[] { "darkwood_pole4", "wood_pole4" }, new Vector3(at.x + 3.2f, ground, at.y + 1.6f), 0f, false, 2); // the fallen post
                    continue;
                }
                // braces round the posts at half height and under the deck
                foreach (float y in new[] { deck * 0.5f, deck - 0.3f })
                {
                    Beam(new Vector3(at.x - half, ground + y, at.y - half), new Vector3(at.x + half, ground + y, at.y - half), wear);
                    Beam(new Vector3(at.x - half, ground + y, at.y + half), new Vector3(at.x + half, ground + y, at.y + half), wear);
                    Beam(new Vector3(at.x - half, ground + y, at.y - half), new Vector3(at.x - half, ground + y, at.y + half), wear);
                    Beam(new Vector3(at.x + half, ground + y, at.y - half), new Vector3(at.x + half, ground + y, at.y + half), wear);
                }
                float floorTop = ground + deck + 0.2f;
                foreach (float x in new[] { -1f, 1f })
                    foreach (float z in new[] { -1f, 1f })
                        SpProp(new[] { "wood_floor" }, new Vector3(at.x + x, floorTop, at.y + z), 0f, true, wear);
                // the low wall round the platform
                Vector2 rail = Size(Planks);
                for (float t = -1f; t <= 1.01f; t += 2f)
                {
                    foreach (Vector3 p in new[] { new Vector3(t, 0f, -2f), new Vector3(t, 0f, 2f) })
                        Rail(new Vector3(at.x + p.x, floorTop, at.y + p.z), p.z < 0f ? 180f : 0f, rail, wear);
                    foreach (Vector3 p in new[] { new Vector3(-2f, 0f, t), new Vector3(2f, 0f, t) })
                        Rail(new Vector3(at.x + p.x, floorTop, at.y + p.z), p.x > 0f ? 90f : -90f, rail, wear);
                }
                if (stage < 2) continue;
                // the roof on corner posts, and the beacon under it
                foreach (float x in new[] { -half, half })
                    foreach (float z in new[] { -half, half })
                        Pole(new Vector3(at.x + x, floorTop, at.y + z), 2.6f, 0);
                Transform keep = parent;
                GameObject roofFrame = new GameObject("IoV_SpiritTowerRoof");
                roofFrame.transform.SetParent(keep, false);
                parent = roofFrame.transform;
                SpRoof(DarkRoof, DarkCap, floorTop + 2.6f, half + 0.3f, -half - 0.2f, half + 0.2f);
                roofFrame.transform.localPosition = new Vector3(at.x, 0f, at.y);
                parent = keep;
                SpProp(new[] { "piece_brazierfloor01" }, new Vector3(at.x, floorTop, at.y), 0f, false, -1, true);
            }
        }

        // A plank wall half as high (a railing), its foot at foot.
        static void Rail(Vector3 foot, float yaw, Vector2 plank, int wear)
        {
            GameObject go = SpProp(new[] { Planks }, foot, yaw, false, wear);
            if (go == null) return;
            go.transform.localScale = new Vector3(1f, 0.5f, 1f);
            go.transform.localPosition = Vector3.zero;
            Move(go, LocalBounds(go), foot, false);
        }

        // A dark beam from one point to another (level), stretched to fit.
        static void Beam(Vector3 from, Vector3 to, int wear)
        {
            Vector3 mid = (from + to) * 0.5f;
            Vector3 d = to - from;
            d.y = 0f;
            GameObject go = SpProp(new[] { "darkwood_beam", "wood_beam" }, mid, 0f, false, wear);
            if (go == null) return;
            Bounds b = LocalBounds(go);
            bool alongX = b.size.x >= b.size.z;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + (alongX ? -90f : 0f);
            float own = alongX ? b.size.x : b.size.z;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 scale = Vector3.one;
            if (alongX) scale.x = d.magnitude / Mathf.Max(0.1f, own); else scale.z = d.magnitude / Mathf.Max(0.1f, own);
            go.transform.localScale = scale;
            go.transform.localPosition = Vector3.zero;
            Bounds nb = LocalBounds(go);
            Move(go, nb, new Vector3(mid.x, mid.y - nb.size.y * 0.5f, mid.z), false);
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

        // The village's field: ploughed ground (always), weeds in the ruin, sown once rebuilt, a scarecrow of
        // posts in it.
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
            // a low fence of posts and rails round it once cleared
            foreach (int stage in new[] { 1, 2 })
            {
                parent = stageGroups[stage];
                Vector3[] c =
                {
                    new Vector3(at.x - 4.5f, 0f, at.y - 3.5f), new Vector3(at.x + 4.5f, 0f, at.y - 3.5f),
                    new Vector3(at.x + 4.5f, 0f, at.y + 3.5f), new Vector3(at.x - 4.5f, 0f, at.y + 3.5f),
                };
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = c[i], b = c[(i + 1) % 4];
                    a.y = Height(a.x, a.z); b.y = Height(b.x, b.z);
                    Pole(a, 1.2f, stage == 1 ? 1 : 0);
                    if (i == 3) continue; // its gate side, open to the path
                    Beam(new Vector3(a.x, Mathf.Max(a.y, b.y) + 0.9f, a.z), new Vector3(b.x, Mathf.Max(a.y, b.y) + 0.9f, b.z), stage == 1 ? 1 : 0);
                }
            }
        }

        // The memorial of the drowned: a ring of standing stones round a cold blue fire, lit once the village is
        // cleared (the souls Rán held), looking out to sea.
        static void Memorial(Vector2 at)
        {
            float ground = Height(at.x, at.y);
            parent = groupAll;
            SpPaint(at.x, at.y, 4.5f, TerrainModifier.PaintType.Paved);
            for (int i = 0; i < 7; i++)
            {
                float a = i * (360f / 7f);
                Vector3 q = new Vector3(at.x, 0f, at.y) + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 3.4f);
                q.y = Height(q.x, q.z) - 0.2f;
                GameObject stone = SpProp(new[] { "stone_pillar", "stone_wall_1x1" }, q, a, false);
                if (stone == null) continue;
                Bounds b = LocalBounds(stone);
                stone.transform.localScale = Vector3.Scale(stone.transform.localScale, new Vector3(1f, (2.2f + (i % 3) * 0.4f) / Mathf.Max(0.1f, b.size.y), 1f));
                stone.transform.localPosition = Vector3.zero;
                Move(stone, LocalBounds(stone), q, false);
            }
            for (int stage = 0; stage < 3; stage++)
            {
                parent = stageGroups[stage];
                SpProp(new[] { "piece_brazierfloor02", "piece_brazierfloor01" }, new Vector3(at.x, ground, at.y), 0f, false, -1, stage > 0, stage == 0);
            }
        }

        // The middle ring between the square and the houses: a well, an outdoor fire with log seats, a drying
        // place for hides and fish, a cart; torches along the paths once rebuilt.
        static void Commons()
        {
            Vector2 well = At(10f, -9f), fire = At(-11f, 11f), drying = At(-11f, -9f), cart = At(10f, 12f), logs = At(-12f, 17f);
            // the well: a ring of stone, two posts and a beam over it
            parent = groupAll;
            float wg = Height(well.x, well.y);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                Vector3 q = new Vector3(well.x, wg - 0.2f, well.y) + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 1.1f);
                SpProp(new[] { "stone_wall_1x1", "stone_wall_2x1" }, q, a, false);
            }
            foreach (int stage in new[] { 1, 2 })
            {
                parent = stageGroups[stage];
                foreach (float s in new[] { -1f, 1f })
                    Pole(new Vector3(well.x + s * 1.4f, wg, well.y), 2.4f, stage == 1 ? 1 : 0);
                Beam(new Vector3(well.x - 1.5f, wg + 2.3f, well.y), new Vector3(well.x + 1.5f, wg + 2.3f, well.y), stage == 1 ? 1 : 0);
            }
            // the outdoor fire with logs to sit on (the village's evenings; cold in the ruin)
            for (int stage = 0; stage < 3; stage++)
            {
                parent = stageGroups[stage];
                Vector3 f = new Vector3(fire.x, Height(fire.x, fire.y), fire.y);
                SpProp(new[] { "fire_pit" }, f, 0f, false, -1, stage > 0, stage == 0);
                if (stage == 0) continue;
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 120f + 30f;
                    Vector3 q = f + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 2.6f);
                    q.y = Height(q.x, q.z);
                    SpProp(new[] { "Beech_log_half", "Beech_log", "FirTree_log" }, q, a + 90f, false);
                }
            }
            // drying racks for hides and fish
            foreach (int stage in new[] { 1, 2 })
            {
                parent = stageGroups[stage];
                for (int i = 0; i < 2; i++)
                {
                    Vector3 q = new Vector3(drying.x + i * 2.2f, 0f, drying.y);
                    q.y = Height(q.x, q.z);
                    SpProp(new[] { "piece_cookingstation" }, q, 0f, false);
                }
                SpProp(new[] { "Cart" }, new Vector3(cart.x, Height(cart.x, cart.y), cart.y), 30f, false);
                SpProp(new[] { "wood_stack" }, new Vector3(logs.x, Height(logs.x, logs.y), logs.y), 0f, false);
            }
        }

        // The jetty's side: a wreck on the shore in the ruin, a ship moored alongside once rebuilt.
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
        }

        // Trees round the village, on its slopes, never on the plateau where the houses stand.
        static void Trees(LargeKind kind)
        {
            string[] trees = { "Beech1", "Birch1", "Oak1" };
            for (int i = 0; i < 30; i++)
            {
                float a = (float)villageRandom.NextDouble() * Mathf.PI * 2f;
                float r = kind.Plateau + 5f + (float)villageRandom.NextDouble() * Mathf.Max(2f, kind.Shore - kind.Plateau - 13f);
                Vector2 q = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
                if (Height(q.x, q.y) < 1.5f) continue;
                OnGround(new[] { trees[i % trees.Length] }, q, a * Mathf.Rad2Deg, 0f, 0.05f, true);
            }
        }

        // Between the trees on the slopes: boulders and berry bushes.
        static void Slopes(LargeKind kind)
        {
            for (int i = 0; i < 22; i++)
            {
                float a = (float)villageRandom.NextDouble() * Mathf.PI * 2f;
                float r = kind.Plateau + 2f + (float)villageRandom.NextDouble() * Mathf.Max(2f, kind.Shore - kind.Plateau - 8f);
                Vector2 q = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
                if (Height(q.x, q.y) < 1f) continue;
                if (i % 2 == 0) OnGround(new[] { "Rock_3", "rock1" }, q, a * 90f, 1.8f + (i % 3) * 0.6f, 0.35f, true);
                else OnGround(new[] { "RaspberryBush", "BlueberryBush", "Bush01" }, q, a * 90f, 0f, 0.05f, true);
            }
        }

        // A worn dirt path from one point to another (from `from` out of a circle of `skip` round it); a torch
        // halfway along once rebuilt.
        static void Path(Vector2 from, Vector2 to, float skip)
        {
            Vector2 d = to - from;
            float length = d.magnitude;
            for (float t = skip; t < length; t += 2f)
            {
                Vector2 q = from + d * (t / length);
                SpPaint(q.x, q.y, 1.3f, TerrainModifier.PaintType.Dirt);
            }
            if (length < skip + 6f) return;
            Transform keep = parent;
            parent = stageGroups[2];
            Vector2 side = new Vector2(-d.y, d.x).normalized * 1.8f;
            Vector2 m = from + d * ((skip + length) * 0.5f / length) + side;
            SpProp(new[] { "piece_groundtorch_wood", "piece_groundtorch" }, new Vector3(m.x, Height(m.x, m.y), m.y), 0f, false, -1, true);
            parent = keep;
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

        // Moved along out (+x, -x or +z of its frame) until its side that way touches a wall's inner face at
        // plane (1.10.89 left things standing a little off the walls).
        static GameObject SpAgainst(GameObject go, Vector3 out_, float plane)
        {
            if (go == null) return null;
            // (plane is the wall's coordinate on that axis, so along out it lies at |plane|)
            Bounds b = LocalBounds(go);
            float face = Vector3.Dot(b.center, out_) + Vector3.Dot(b.extents, new Vector3(Mathf.Abs(out_.x), Mathf.Abs(out_.y), Mathf.Abs(out_.z)));
            go.transform.localPosition += out_ * (Mathf.Abs(plane) - 0.03f - face);
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
