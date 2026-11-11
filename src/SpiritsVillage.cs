using System.Collections.Generic;
using UnityEngine;

namespace IslandsOfValheim
{
    // The Brim village (Valhalla arc, T4): the Brim Folk's old home on a large islet of the game's terrain, in
    // sight of Sævar's harbour. It is there from the start, ruined; donations at its altar restore it in
    // stages (SpiritsIslet):
    //   0 ruined:   broken walls, no roofs, cold fires, rubble and weeds, a wreck on the shore, the lighthouse a
    //               broken stump;
    //   1 cleared:  the walls whole again (weathered), the hall roofed, the first fires lit, wood and stone
    //               piled to build with, the lighthouse standing;
    //   2 rebuilt:  every house roofed, new and furnished for its trade, the hall's feast laid, the field sown,
    //               the lighthouse lit, a ship at the jetty, torches along the paths, the altar a great fire;
    //               and the player's house, with its real beds, chests and fires.
    //   (3, the people's return, comes with T5.)
    // Each stage's look is its own group in the location, shown by SpiritStages from the world's keys; what
    // every stage shares (floors, footings, corner posts, paths, the dock, trees) stands outside them.
    // The plan (1.10.93, the user: orderly and symmetric, but not as if made by a machine): an axis north to
    // south, the hall at its head and the fisher's house at its foot; two houses either side, facing across the
    // square in pairs; the yard and the lighthouse on the north corners, the memorial and the field on the
    // south ones. Each house its own size, footing, roof and lights, none quite square to the others.
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
        // of the player's house and the doors, put there by the altar; all in the islet's space.
        internal static Vector3 SpiritAltarAt;
        internal struct HomePiece { internal string[] Prefab; internal Vector3 At; internal float Yaw; internal bool AlongZ; internal float Scale; }
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
        static readonly Dictionary<string, Vector3> pieceSize = new Dictionary<string, Vector3>(); // width, height, thickness

        enum Trade { Hall, Home, Smith, Weaver, Fisher, Brewer }

        const string Planks = "woodwall", Stone = "stone_wall_2x1";
        const string DarkRoof = "darkwood_roof_45", DarkCap = "darkwood_roof_top_45", Thatch = "wood_roof_45", ThatchCap = "wood_roof_top_45";
        const float DoorScale = 1.25f; // the houses' doors, a quarter taller than the game's
        const float Thin = 0.26f;      // a stone wall as thick as a plank one (1.10.92's stuck out at the corners)

        // How a building is made: its size, its rows of wall from the ground up, its roof, its floor, its yaw
        // (its door, its own -z, towards the square) and the light by its door.
        class Style
        {
            internal Trade Trade;
            internal float Length, Width, Yaw;
            internal string[] Rows;
            internal string Shingle = DarkRoof, Cap = DarkCap, Floor = "wood_floor";
            internal string[] Light;
        }

        static void BuildVillage(LargeKind kind)
        {
            villageRandom = new System.Random(7341);
            frames.Clear();
            HomePieces.Clear();
            DoorPieces.Clear();
            pieceSize.Clear();
            occupied.Clear();
            takenCircles.Clear();
            pathPoints.Clear();
            Transform top = parent;
            groupAll = SpGroup(top, "IoV_SpiritAll", true);
            for (int s = 0; s < 3; s++) stageGroups[s] = SpGroup(top, SpiritStages.GroupName(s), s == 0);
            GameObject stages = new GameObject("IoV_SpiritStages");
            stages.transform.SetParent(top, false);
            stages.AddComponent<SpiritStages>();

            float p = kind.Plateau;
            kind.NoBuildRadius = p + 4f; // the village is the people's (the yard excepted: SpiritYard)
            planScale = Mathf.Clamp(p / 33f, 0.8f, 1.2f);
            Plugin.Debug("Brim village: plateau " + p.ToString("F1") + " m, shore " + kind.Shore.ToString("F1") + " m, plan scaled " + planScale.ToString("F2"));

            // the buildings: (where, how)
            Vector2 hall = At(0f, 23f);
            Vector2[] places = { At(-19f, 9f), At(-19.5f, -9f), At(0f, -23f), At(19.5f, -9f), At(19f, 9f) };
            Style hallStyle = new Style { Trade = Trade.Hall, Length = 22f, Width = 14f, Yaw = 0f, Rows = new[] { Stone, Planks, Planks }, Light = new[] { "MountainKit_brazier", "piece_brazierfloor02" } };
            Style[] styles =
            {
                new Style { Trade = Trade.Smith, Length = 12f, Width = 8f, Yaw = -90f + 2f, Rows = new[] { Stone, Stone, Planks }, Floor = "stone_floor_2x2", Light = new[] { "piece_groundtorch", "piece_groundtorch_wood" } },
                new Style { Trade = Trade.Weaver, Length = 10f, Width = 8f, Yaw = -90f - 3f, Rows = new[] { Planks, Planks }, Shingle = Thatch, Cap = ThatchCap, Light = new[] { "piece_groundtorch_wood" } },
                new Style { Trade = Trade.Fisher, Length = 10f, Width = 6f, Yaw = 180f + 2f, Rows = new[] { Planks, Planks }, Shingle = Thatch, Cap = ThatchCap, Light = new[] { "piece_groundtorch_blue", "piece_groundtorch_wood" } },
                new Style { Trade = Trade.Brewer, Length = 14f, Width = 8f, Yaw = 90f + 3f, Rows = new[] { Stone, Planks, Planks }, Light = new[] { "CastleKit_groundtorch", "piece_groundtorch" } },
                new Style { Trade = Trade.Home, Length = 12f, Width = 8f, Yaw = 90f - 2f, Rows = new[] { Stone, Planks, Planks }, Light = new[] { "piece_brazierfloor02", "piece_brazierfloor01" } },
            };
            Vector2 lighthouse = At(-21f, 23f), yard = At(21f, 23f), memorial = At(17f, -22f), field = At(-17f, -22f);

            Building(hall, hallStyle);
            for (int i = 0; i < places.Length; i++) Building(places[i], styles[i]);
            float altarTop = Square();
            Lighthouse(lighthouse);
            SpiritYardAt = new Vector3(yard.x, Height(yard.x, yard.y), yard.y);
            SpYard(yard);
            Field(field);
            Memorial(memorial);
            Commons();
            Taken(Vector2.zero, 9f);
            Taken(lighthouse, 5f);
            Taken(yard, SpiritYardRadius + 1f);
            Taken(memorial, 5f);
            occupied.Add(new Footprint { At = field, Yaw = 0f, Half = new Vector2(5.5f, 4.5f) });
            float dock = cliffDeg + 180f;
            parent = groupAll;
            PlaceDock(dock);
            Trees(kind);
            Slopes(kind);
            // the paths: from the square to every door, and to the edges of the lighthouse, the yard, the field,
            // the memorial and the jetty; torches along them once rebuilt
            List<Vector2> ends = new List<Vector2>();
            ends.Add(Door(hall, hallStyle));
            for (int i = 0; i < places.Length; i++) ends.Add(Door(places[i], styles[i]));
            ends.Add(lighthouse - lighthouse.normalized * 4.5f);
            ends.Add(yard - yard.normalized * SpiritYardRadius);
            ends.Add(field - field.normalized * 4.5f);
            ends.Add(memorial - memorial.normalized * 4.5f);
            ends.Add(Polar(dock, PlateauAt(dock)));
            for (int i = 0; i < ends.Count; i++) Path(Vector2.zero, ends[i], 8.5f, i);
            SpShore(dock);
            SpMeadow(kind);

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
            Plugin.Debug("Brim village: " + frames.Count + " frames, " + HomePieces.Count + " pieces of the player's house, " + DoorPieces.Count + " doors, altar at " + SpiritAltarAt.ToString("F1"));
        }

        static Vector2 At(float x, float z) { return new Vector2(x * planScale, z * planScale); }

        // Just outside a building's door.
        static Vector2 Door(Vector2 at, Style style)
        {
            Vector3 d = Quaternion.Euler(0f, style.Yaw, 0f) * new Vector3(0f, 0f, -style.Length * 0.5f - 1.5f);
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

        // A building piece's width, height and thickness, measured once.
        static Vector3 Size(string name)
        {
            Vector3 size;
            if (pieceSize.TryGetValue(name, out size)) return size;
            Transform keep = parent;
            parent = groupAll;
            GameObject probe = SpProp(new[] { name }, Vector3.zero, 0f, false);
            parent = keep;
            size = new Vector3(2f, 2f, 0.2f);
            if (probe != null)
            {
                Bounds b = LocalBounds(probe);
                size = new Vector3(Mathf.Max(0.5f, Mathf.Round(Mathf.Max(b.size.x, b.size.z) * 2f) / 2f), Mathf.Max(0.5f, Mathf.Round(b.size.y * 2f) / 2f), Mathf.Min(b.size.x, b.size.z));
                Object.DestroyImmediate(probe);
                built--;
            }
            pieceSize[name] = size;
            Plugin.Debug("Brim village: " + name + " is " + size.x + " x " + size.y + " m, " + size.z.ToString("F2") + " thick");
            return size;
        }

        static string Have(string name, string otherwise) { return Find(new[] { name }) != null ? name : otherwise; }

        // ---- a building ----

        static void Building(Vector2 at, Style style)
        {
            float yaw = style.Yaw;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            float length = style.Length, width = style.Width;
            float floor = float.MinValue;
            // the highest ground anywhere under it, a metre apart (1.10.93 looked at its corners only, and a hump
            // in the middle came up through a floor)
            for (float u = -width * 0.5f; u <= width * 0.5f + 0.01f; u += 1f)
                for (float v = -length * 0.5f; v <= length * 0.5f + 0.01f; v += 1f)
                {
                    Vector3 w = turn * new Vector3(u, 0f, v);
                    floor = Mathf.Max(floor, Height(at.x + w.x, at.y + w.z));
                }
            floor += 0.08f; // on the ground (where it's lower, a footing of stone: Footing)
            occupied.Add(new Footprint { At = at, Yaw = yaw, Half = new Vector2(width * 0.5f + 1.5f, length * 0.5f + 1.5f) });
            Vector3 pose = new Vector3(at.x, floor, at.y);
            string[] rows = new string[style.Rows.Length];
            float wallTop = 0f;
            for (int r = 0; r < rows.Length; r++) { rows[r] = Have(style.Rows[r], Planks); wallTop += Size(rows[r]).y; }

            // the doorway: the hall's great dark gate, the game's own (1.10.92/93 made a copy of it that the game
            // never had, and a wooden door stood in for it), or a wooden door a quarter taller than the game's
            GameObject gate = style.Trade == Trade.Hall ? Find(new[] { "darkwood_gate" }) : null;
            GameObject wood = Find(new[] { "wood_door" });
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
                Vector3 d = wood != null ? SpiritsIslet.OwnSize(wood) * DoorScale : new Vector3(1.6f, 2.5f, 0.2f);
                doorAt = DoorX(width, Size(rows[0]).x);
                doorWide = Mathf.Max(Size(rows[0]).x, Mathf.Max(d.x, d.z) + 0.15f);
                doorHigh = Mathf.Min(d.y + 0.05f, wallTop - 0.4f);
            }

            parent = Frame(groupAll, pose, yaw);
            Floor(length, width, style.Floor);
            Corners(length, width, wallTop);
            Footing(length, width, pose, turn);
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
                if (stage > 0) Doorway(length, style.Light, stage == 2);
            }
            if (style.Trade == Trade.Home) homePose = new FramePose { At = pose, Yaw = yaw };
            // its door(s), in the islet's space (the altar puts the real ones in: SpiritsIslet.BuildDoors)
            if (gate != null)
                DoorPieces.Add(new HomePiece { Prefab = new[] { "darkwood_gate", "wood_door" }, At = pose + turn * new Vector3(0f, 0f, -length * 0.5f), Yaw = yaw, Scale = 1f });
            else
                DoorPieces.Add(new HomePiece { Prefab = new[] { "wood_door" }, At = pose + turn * new Vector3(doorAt, 0f, -length * 0.5f), Yaw = yaw, Scale = DoorScale });
        }

        static FramePose homePose;
        // the doorway of the building being built: its middle's x, its width and height
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

        // Where the ground falls away under a building (the hall's back reaches the plateau's edge), stone down
        // to it under the walls, so nothing stands on air.
        static void Footing(float length, float width, Vector3 pose, Quaternion turn)
        {
            Vector3 stone = Size(Stone);
            List<Vector3> spots = new List<Vector3>();
            for (float z = -length * 0.5f + stone.x * 0.5f; z < length * 0.5f; z += stone.x) { spots.Add(new Vector3(-width * 0.5f, 0f, z)); spots.Add(new Vector3(width * 0.5f, 0f, z)); }
            for (float x = -width * 0.5f + stone.x * 0.5f; x < width * 0.5f; x += stone.x) { spots.Add(new Vector3(x, 0f, -length * 0.5f)); spots.Add(new Vector3(x, 0f, length * 0.5f)); }
            foreach (Vector3 s in spots)
            {
                Vector3 w = pose + turn * s;
                float gap = pose.y - Height(w.x, w.z);
                if (gap < 0.12f) continue;
                bool side = Mathf.Abs(Mathf.Abs(s.x) - width * 0.5f) < 0.01f;
                for (float y = -stone.y; y > -gap - stone.y; y -= stone.y)
                    Wall(Stone, stone, new Vector3(s.x, y, s.z), Inward(s.x, s.z, side), 0, stone.x, stone.y);
            }
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

        // A wall piece (or part of one: w by h), its foot at `at`, no thicker than a plank wall.
        static GameObject Wall(string name, Vector3 size, Vector3 at, float yaw, int wear, float w, float h)
        {
            GameObject go = SpProp(new[] { name }, at, yaw, false, wear);
            if (go == null) return null;
            float thick = size.z > Thin + 0.04f ? Thin / size.z : 1f;
            if (Mathf.Abs(w - size.x) < 0.01f && Mathf.Abs(h - size.y) < 0.01f && thick >= 1f) return go;
            go.transform.localScale = new Vector3(w / size.x, h / size.y, thick);
            go.transform.localPosition = Vector3.zero;
            Move(go, LocalBounds(go), at, false);
            return go;
        }

        // Walls all round, row on row (each row its own kind), but for the doorway; worn as the stage is
        // (broken, weathered, new). A ruin's are gappy, its upper rows most.
        static void Walls(float length, float width, string[] rows, int stage)
        {
            int wear = stage == 0 ? 2 : stage == 1 ? 1 : 0;
            float y = 0f;
            float openL = doorAt - doorWide * 0.5f, openR = doorAt + doorWide * 0.5f;
            for (int r = 0; r < rows.Length; r++)
            {
                Vector3 size = Size(rows[r]);
                double gone = stage == 0 ? (r == 0 ? 0.1 : r == rows.Length - 1 ? 0.55 : 0.3) : 0.0;
                foreach (float x in new[] { -width * 0.5f, width * 0.5f })
                    for (float z = -length * 0.5f + size.x * 0.5f; z < length * 0.5f; z += size.x)
                        if (villageRandom.NextDouble() >= gone) Wall(rows[r], size, new Vector3(x, y, z), Inward(x, z, true), wear, size.x, size.y);
                foreach (float z in new[] { -length * 0.5f, length * 0.5f })
                    for (float x = -width * 0.5f + size.x * 0.5f; x < width * 0.5f; x += size.x)
                    {
                        float l = x - size.x * 0.5f, rr = x + size.x * 0.5f;
                        bool door = z < 0f && y < doorHigh - 0.05f && rr > openL + 0.01f && l < openR - 0.01f;
                        if (!door)
                        {
                            if (villageRandom.NextDouble() >= gone) Wall(rows[r], size, new Vector3(x, y, z), Inward(x, z, false), wear, size.x, size.y);
                            continue;
                        }
                        // a piece the doorway cuts through: what's left of it either side, and above the doorway
                        if (openL - l > 0.05f) Wall(rows[r], size, new Vector3((l + openL) * 0.5f, y, z), Inward(x, z, false), wear, openL - l, size.y);
                        if (rr - openR > 0.05f) Wall(rows[r], size, new Vector3((openR + rr) * 0.5f, y, z), Inward(x, z, false), wear, rr - openR, size.y);
                        float top = y + size.y;
                        if (top > doorHigh + 0.05f)
                        {
                            float a = Mathf.Max(l, openL), b = Mathf.Min(rr, openR);
                            Wall(rows[r], size, new Vector3((a + b) * 0.5f, doorHigh, z), Inward(x, z, false), wear, b - a, top - doorHigh);
                        }
                    }
                y += size.y;
            }
        }

        // Where a door goes in the front wall: over the wall piece nearest the middle.
        static float DoorX(float width, float piece)
        {
            float best = 0f, nearest = float.MaxValue;
            for (float x = -width * 0.5f + piece * 0.5f; x < width * 0.5f; x += piece)
                if (Mathf.Abs(x) < nearest - 0.01f) { nearest = Mathf.Abs(x); best = x; }
            return best;
        }

        // The ends above the walls, closed up to the 45 degree roof: narrow boards, each as high as the roof over
        // its outer edge.
        static void Gables(float length, float width, float wallTop, int wear)
        {
            float half = width * 0.5f + 0.15f, board = 0.5f;
            Vector3 plank = Size(Planks);
            foreach (float z in new[] { -length * 0.5f, length * 0.5f })
                for (float x = -half + board * 0.5f; x < half; x += board)
                {
                    float h = half - (Mathf.Abs(x) + board * 0.5f) + 0.1f;
                    if (h < 0.15f) continue;
                    Wall(Planks, plank, new Vector3(x, wallTop, z), Inward(x, z, false), wear, board, h);
                }
        }

        // The roof: shingles at 45 degrees from the walls' tops up to the ridge cap, by the pieces' own snap
        // points, of whichever kind.
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
            float slope = 1f, along = 2f, first = start, last = end;
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
                // as many as cover it and a metre over each end, centred on it (1.10.93 started from one end, and
                // the lighthouse's small roof hung off to one side)
                int n = Mathf.Max(1, Mathf.CeilToInt((end - start + 2f) / along - 0.01f));
                first = (start + end) * 0.5f - (n - 1) * along * 0.5f;
                last = first + (n - 0.5f) * along;
                int rowsOf = Mathf.Max(1, Mathf.RoundToInt(beamOuter / run));
                for (int k = 0; k < rowsOf; k++)
                    for (float z = first; z < last; z += along)
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
            for (float z = first; z < last; z += along)
                SkyPivot(capName, new Vector3(0f, capY, z) - capTurn * capLow, capTurn);
        }

        // ---- inside and round each building ----

        // What stands against a wall, from the back towards the door, each by its own size with room between.
        struct Thing { internal string[] Names; internal float Yaw; internal bool Table; }

        static Thing T(float yaw, params string[] names) { return new Thing { Names = names, Yaw = yaw }; }
        static Thing Table(params string[] names) { return new Thing { Names = names, Table = true }; }

        static void Inside(Trade trade, int stage, float length, float width)
        {
            float back = length * 0.5f, side = width * 0.5f;
            if (stage == 0)
            {
                Rubble(length, width, trade == Trade.Hall ? 6 : 3);
                if (trade == Trade.Hall) SpProp(new[] { "hearth" }, new Vector3(0f, 0f, -back + 7f), 0f, false, -1, false, true);
                return;
            }
            if (trade == Trade.Hall)
            {
                if (stage == 1) { SpSpit(SpProp(new[] { "hearth" }, new Vector3(0f, 0f, -back + 7f), 0f, false, -1, true), 0); return; }
                // two fires down the middle, meat roasting over them; a long table either side well clear of
                // them, benches both sides, the feast laid
                int k = 0;
                foreach (float z in new[] { -back + 7f, back - 9f })
                    SpSpit(SpProp(new[] { "hearth" }, new Vector3(0f, 0f, z), 0f, false, -1, true), k++);
                foreach (float s in new[] { -1f, 1f })
                    for (float z = -back + 4f; z < back - 5f; z += 4.5f)
                    {
                        float x = s * (side - 2.6f);
                        GameObject t = SpProp(new[] { "piece_table_oak", "piece_table" }, new Vector3(x, 0f, z), 0f, false);
                        SpAlongZ(t, new Vector3(x, 0f, z));
                        SpLaid(t, k++);
                        foreach (float off in new[] { -1.15f, 1.15f })
                            SpSeat(new[] { "piece_bench01" }, new Vector3(x + off, 0f, z), new Vector3(-Mathf.Sign(off), 0f, 0f));
                    }
                // the high seat on a stone dais at the far end, blue fires either side, furs before it
                float daisZ = back - 2f;
                for (float x = -2f; x <= 2.01f; x += 2f)
                    SpProp(new[] { "stone_floor_2x2" }, new Vector3(x, 0.35f, daisZ), 0f, true);
                SpSeat(new[] { "piece_throne01", "piece_chair02" }, new Vector3(0f, 0.35f, daisZ), Vector3.back);
                foreach (float x in new[] { -2.4f, 2.4f })
                    SpProp(new[] { "piece_brazierfloor02", "piece_brazierfloor01" }, new Vector3(x, 0.35f, daisZ), 0f, false, -1, true);
                SpProp(new[] { "rug_fur", "rug_wolf", "rug_deer" }, new Vector3(0f, 0.02f, daisZ - 2.8f), 0f, false);
                SpProp(new[] { "rug_wolf", "rug_fur" }, new Vector3(0f, 0.02f, -back + 3f), 90f, false);
                return;
            }
            if (stage == 1) { if (trade != Trade.Home) Piles(length, width); return; }
            // Rebuilt: each house furnished for its trade along its walls (a bed in a back corner), its middle
            // free, a rug there, a torch by the door.
            Thing[] left, right;
            switch (trade)
            {
                case Trade.Smith:
                    left = new[] { T(90f, "forge"), T(90f, "forge_ext1", "forge_ext2") };
                    right = new[] { T(-90f, "piece_workbench"), T(-90f, "piece_chest_wood") };
                    break;
                case Trade.Weaver:
                    left = new[] { T(90f, "piece_spinningwheel"), T(90f, "piece_chest_wood"), T(90f, "piece_chest_wood") };
                    right = new[] { T(-90f, "piece_chest_wood") };
                    break;
                case Trade.Fisher:
                    left = new[] { Table("piece_table"), T(90f, "barrell") };
                    right = new[] { T(-90f, "piece_chest_wood") };
                    break;
                case Trade.Brewer:
                    left = new[] { T(90f, "fermenter"), T(0f, "barrell"), T(30f, "barrell") };
                    right = new[] { T(-90f, "piece_chest_wood"), T(0f, "barrell") };
                    break;
                default: // the player's own: its real beds and chests come from the altar (HomePlan)
                    left = new[] { Table("piece_table") };
                    right = new Thing[0];
                    break;
            }
            float rightFrom = back - 0.15f;
            if (trade != Trade.Home)
            {
                // the bed first, in the back corner on the right
                GameObject bed = SpProp(new[] { "bed", "piece_bed02" }, new Vector3(side - 1.2f, 0f, back - 1.5f), 0f, false);
                SpAlongZ(bed, new Vector3(side - 1.2f, 0f, back - 1.5f));
                SpAgainst(SpAgainst(bed, Vector3.right, side - 0.15f), Vector3.forward, back - 0.15f);
                rightFrom = back - 3.4f; // (past the bed, whatever its bounds say: 1.10.93 put a table over one)
                SpProp(new[] { "piece_groundtorch_wood", "piece_groundtorch" }, new Vector3(side - 0.6f, 0f, -back + 0.8f), 0f, false, -1, true);
            }
            else rightFrom = 1.5f; // (past the real chests)
            Along(left, -1f, side, back - 0.15f, -back + 2.2f);
            Along(right, 1f, side, rightFrom, -back + 2.2f);
            SpProp(new[] { trade == Trade.Smith ? "rug_wolf" : trade == Trade.Fisher ? "rug_deer" : "rug_fur", "rug_deer", "rug_fur" }, new Vector3(0f, 0.02f, -0.5f), (trade == Trade.Weaver ? 90f : 0f), false);
        }

        // Things against one wall (side -1 left, 1 right), from `from` (z) towards the door, no further than `to`.
        static void Along(Thing[] things, float wall, float side, float from, float to)
        {
            float cursor = from;
            int k = 0;
            foreach (Thing t in things)
            {
                Vector3 at = new Vector3(wall * (side - 1f), 0f, cursor - 1f);
                GameObject go = SpProp(t.Names, at, t.Table ? 0f : t.Yaw, false);
                if (go == null) continue;
                if (t.Table) SpAlongZ(go, at);
                SpAgainst(go, new Vector3(wall, 0f, 0f), side - 0.15f);
                Bounds b = LocalBounds(go);
                go.transform.localPosition += new Vector3(0f, 0f, cursor - b.max.z);
                b = LocalBounds(go);
                if (b.min.z < to) { Object.DestroyImmediate(go); built--; break; }
                cursor = b.min.z - 0.35f;
                if (t.Table)
                {
                    SpLaid(go, k++ + 7);
                    SpSeat(new[] { "piece_bench01" }, new Vector3(wall * (side - 0.15f) - wall * (b.size.x + 0.7f), 0f, b.center.z), new Vector3(wall, 0f, 0f));
                }
            }
        }

        // Round the outside of a house: its trade's things by its side wall once rebuilt (a smith's wood, a
        // weaver's bees, a fisher's drying rack, a brewer's barrels); wood piled by it while it's being rebuilt.
        static void Outside(Trade trade, int stage, float length, float width)
        {
            if (trade == Trade.Hall || trade == Trade.Home || stage == 0) return;
            float side = width * 0.5f + 0.9f;
            if (stage == 1) { SpProp(new[] { "wood_stack" }, new Vector3(side + 0.3f, 0f, 1f), 0f, false); return; }
            switch (trade)
            {
                case Trade.Smith:
                    SpProp(new[] { "wood_stack" }, new Vector3(side, 0f, 1.5f), 0f, false);
                    SpProp(new[] { "wood_core_stack", "wood_stack" }, new Vector3(side, 0f, -1.2f), 0f, false);
                    break;
                case Trade.Weaver:
                    SpProp(new[] { "piece_beehive" }, new Vector3(-side - 0.3f, 0f, 1.5f), 90f, false);
                    SpProp(new[] { "piece_beehive" }, new Vector3(-side - 0.3f, 0f, -0.5f), 90f, false);
                    break;
                case Trade.Fisher:
                    SpProp(new[] { "barrell" }, new Vector3(side, 0f, -1.5f), 0f, false);
                    SpProp(new[] { "wood_stack" }, new Vector3(side, 0f, 1.2f), 90f, false);
                    break;
                case Trade.Brewer:
                    foreach (float z in new[] { -2f, -0.8f, 0.4f })
                        SpProp(new[] { "barrell" }, new Vector3(side - 0.2f, 0f, z), z * 40f, false);
                    break;
            }
        }

        // Where the real pieces of the player's house go, in the house's own space (turned into the islet's
        // once it stands), each up against its wall by its own size: two beds in the back corners, two chests
        // by the right wall, a fire either side of the door inside.
        static void HomePlan(float length, float width)
        {
            float backIn = length * 0.5f - 0.15f, wallIn = width * 0.5f - 0.15f;
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

        // The light by a door: the building's own kind (each house its own), one on the side of the door away
        // from the middle of the wall; the hall's either side. Lit once rebuilt.
        static void Doorway(float length, string[] light, bool lit)
        {
            if (light == null) return;
            bool both = doorWide > 3f;
            foreach (float s in both ? new[] { -1f, 1f } : new[] { doorAt <= 0f ? -1f : 1f })
                SpProp(light, new Vector3(doorAt + s * (doorWide * 0.5f + 0.7f), 0f, -length * 0.5f - 0.8f), 0f, false, -1, lit, !lit);
        }

        // Stones and old wood lying about a ruin, and weeds through its floor.
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
                    case 1: SpProp(new[] { "wood_stack", "stone_pile" }, new Vector3(x, 0f, z), yaw, false, 2); break;
                    default: SpPlant(i % 2 == 0 ? "Bush01" : "Bush02_en", x, 0f, z, yaw, 0.1f); break;
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

        // ---- food and spits ----

        // A table laid as Valhalla's are (the user asked for it so): one of the game's feasts in the middle, as
        // full as it comes, where the table is long enough for one; tankards at its ends.
        static void SpLaid(GameObject table, int k)
        {
            if (table == null) return;
            Bounds tb = LocalBounds(table);
            float board = SolidBounds(table).max.y;
            bool longZ = tb.size.z >= tb.size.x;
            float length = Mathf.Max(tb.size.x, tb.size.z), width = Mathf.Min(tb.size.x, tb.size.z);
            Vector3 middle = new Vector3(tb.center.x, board, tb.center.z);
            if (length >= 3f)
            {
                string feast = FeastNames[k % FeastNames.Length];
                if (Find(new[] { feast }) == null) feast = FeastNames[0];
                GameObject dish = SpProp(new[] { feast }, middle, 0f, false, -1, false, false, FullFeast);
                Bounds db = dish != null ? LocalBounds(dish) : new Bounds();
                if (dish != null && (db.size.z >= db.size.x) != longZ)
                {
                    dish.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * dish.transform.localRotation;
                    dish.transform.localPosition = Vector3.zero;
                    Move(dish, LocalBounds(dish), middle, false);
                }
                SpQuiet(dish);
            }
            float along = Mathf.Min(2.2f, length * 0.5f - 0.5f), across = Mathf.Min(0.45f, width * 0.5f - 0.3f);
            int i = 0;
            foreach (float a in new[] { -along, along })
                foreach (float c in new[] { -across, across })
                {
                    if (length < 3f && i % 2 == 1) { i++; continue; } // a small table: two places
                    Vector3 at = middle + (longZ ? new Vector3(c, 0f, a) : new Vector3(a, 0f, c));
                    SpQuiet(SpProp(new[] { (i + k) % 4 == 3 ? TankardNames[1] : TankardNames[0], TankardNames[0] }, at, k * 47f + i * 83f, false));
                    i++;
                }
        }

        // Meat roasting over a fire (a fire burning with nothing on it looked purposeless to the user): the
        // game's spit over it, along its length, the cooked meat's own look on each of its places (as Valhalla's).
        static void SpSpit(GameObject fire, int k)
        {
            if (fire == null) return;
            Bounds hb = LocalBounds(fire);
            Vector3 at = new Vector3(hb.center.x, hb.min.y, hb.center.z);
            bool hearth = fire.name.StartsWith("hearth");
            GameObject spit = SpProp(hearth ? new[] { "piece_cookingstation_iron", "piece_cookingstation" } : new[] { "piece_cookingstation" }, at, 0f, false, -1, false, false, go => Meat(go, k));
            if (spit == null) return;
            Bounds sb = LocalBounds(spit);
            if ((sb.size.x > sb.size.z) != (hb.size.x > hb.size.z))
            {
                spit.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * spit.transform.localRotation;
                spit.transform.localPosition = Vector3.zero;
                Move(spit, LocalBounds(spit), at, false);
            }
        }

        // An item laid out as a thing to look at: not solid, no glint.
        static void SpQuiet(GameObject go)
        {
            if (go == null) return;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (ParticleSystem p in go.GetComponentsInChildren<ParticleSystem>(true)) p.gameObject.SetActive(false);
        }

        // ---- the square, the lighthouse, the yard, the field, the memorial, the commons, the shore ----

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
                        SpPlant(i % 2 == 0 ? "Bush01" : "Bush02_en", q.x, q.y, q.z, a, 0.1f);
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

        // The lighthouse (the user: the watchtower, as a lighthouse): a tall square tower of stone, a gallery
        // with a railing at its top, a roof over the gallery and a great fire under it, its light thrown far out
        // to sea once rebuilt. A broken stump in the ruin; the tower standing, unlit, once cleared.
        static void Lighthouse(Vector2 at)
        {
            float ground = Height(at.x, at.y) - 0.3f;
            Vector3 stone = Size(Stone);
            const float half = 2f;
            const int rows = 10;
            for (int stage = 0; stage < 3; stage++)
            {
                parent = stageGroups[stage];
                int wear = stage == 0 ? 2 : stage == 1 ? 1 : 0;
                int high = stage == 0 ? 3 : rows;
                for (int r = 0; r < high; r++)
                {
                    float y = ground + r * stone.y;
                    double gone = stage == 0 ? (r == 0 ? 0.0 : r == 1 ? 0.25 : 0.6) : 0.0;
                    for (float t = -half + stone.x * 0.5f; t < half; t += stone.x)
                    {
                        if (villageRandom.NextDouble() >= gone) Wall(Stone, stone, new Vector3(at.x + t, y, at.y - half), 180f, wear, stone.x, stone.y);
                        if (villageRandom.NextDouble() >= gone) Wall(Stone, stone, new Vector3(at.x + t, y, at.y + half), 0f, wear, stone.x, stone.y);
                        if (villageRandom.NextDouble() >= gone) Wall(Stone, stone, new Vector3(at.x - half, y, at.y + t), -90f, wear, stone.x, stone.y);
                        if (villageRandom.NextDouble() >= gone) Wall(Stone, stone, new Vector3(at.x + half, y, at.y + t), 90f, wear, stone.x, stone.y);
                    }
                }
                foreach (float x in new[] { -half, half })
                    foreach (float z in new[] { -half, half })
                        Pole(new Vector3(at.x + x, ground, at.y + z), high * stone.y + 0.2f, wear);
                if (stage == 0) continue;
                // the gallery: a floor a little wider than the tower, a railing round it
                float deck = ground + rows * stone.y;
                for (float x = -half - 0.5f + 1f; x < half + 0.5f; x += 2f)
                    for (float z = -half - 0.5f + 1f; z < half + 0.5f; z += 2f)
                        SpProp(new[] { "wood_floor" }, new Vector3(at.x + x, deck + 0.25f, at.y + z), 0f, true, wear);
                Vector3 plank = Size(Planks);
                float g = half + 0.5f;
                for (float t = -g + 1f; t < g; t += 2f)
                {
                    Wall(Planks, plank, new Vector3(at.x + t, deck + 0.25f, at.y - g), 180f, wear, 2f, 1f);
                    Wall(Planks, plank, new Vector3(at.x + t, deck + 0.25f, at.y + g), 0f, wear, 2f, 1f);
                    Wall(Planks, plank, new Vector3(at.x - g, deck + 0.25f, at.y + t), -90f, wear, 2f, 1f);
                    Wall(Planks, plank, new Vector3(at.x + g, deck + 0.25f, at.y + t), 90f, wear, 2f, 1f);
                }
                if (stage < 2) continue;
                // the roof on four posts and the beams over them, its ends closed (1.10.93's hung off its posts)
                float eave = deck + 3.25f;
                foreach (float x in new[] { -half, half })
                    foreach (float z in new[] { -half, half })
                        Pole(new Vector3(at.x + x, deck + 0.25f, at.y + z), 3f, 0);
                Beam(new Vector3(at.x - half, eave - 0.15f, at.y - half), new Vector3(at.x + half, eave - 0.15f, at.y - half), 0);
                Beam(new Vector3(at.x - half, eave - 0.15f, at.y + half), new Vector3(at.x + half, eave - 0.15f, at.y + half), 0);
                Beam(new Vector3(at.x - half, eave - 0.15f, at.y - half), new Vector3(at.x - half, eave - 0.15f, at.y + half), 0);
                Beam(new Vector3(at.x + half, eave - 0.15f, at.y - half), new Vector3(at.x + half, eave - 0.15f, at.y + half), 0);
                Transform keep = parent;
                GameObject roofFrame = new GameObject("IoV_SpiritLighthouseRoof");
                roofFrame.transform.SetParent(keep, false);
                parent = roofFrame.transform;
                SpRoof(DarkRoof, DarkCap, eave, half + 0.15f, -half, half);
                Gables(half * 2f, half * 2f, eave, 0);
                roofFrame.transform.localPosition = new Vector3(at.x, 0f, at.y);
                parent = keep;
                GameObject fire = SpProp(new[] { "bonfire", "piece_brazierfloor01" }, new Vector3(at.x, deck + 0.25f, at.y), 0f, false, -1, true);
                if (fire != null)
                {
                    GameObject beam = new GameObject("IoV_LighthouseLight");
                    beam.transform.SetParent(parent, false);
                    beam.transform.localPosition = new Vector3(at.x, deck + 2f, at.y);
                    Light light = beam.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.75f, 0.45f);
                    light.range = 45f;
                    light.intensity = 2.2f;
                    light.shadows = LightShadows.None;
                }
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

        // The village's field: ploughed ground (always), weeds in the ruin, sown once rebuilt, a fence round it
        // once cleared, open on the side towards the village (1.10.92's was open at its back).
        static void Field(Vector2 at)
        {
            parent = groupAll;
            for (float dx = -4f; dx <= 4.01f; dx += 1f)
                for (float dz = -3f; dz <= 3.01f; dz += 1f)
                    SpPaint(at.x + dx, at.y + dz, 1.1f, TerrainModifier.PaintType.Cultivate);
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
                        SpPlant("Bush01", q.x, q.y, q.z, (float)villageRandom.NextDouble() * 360f, 0.1f);
                    }
                }
            Vector3[] c =
            {
                new Vector3(at.x - 4.5f, 0f, at.y - 3.5f), new Vector3(at.x + 4.5f, 0f, at.y - 3.5f),
                new Vector3(at.x + 4.5f, 0f, at.y + 3.5f), new Vector3(at.x - 4.5f, 0f, at.y + 3.5f),
            };
            // the side nearest the village's middle is its gate
            int open = 0;
            float nearest = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 m = (c[i] + c[(i + 1) % 4]) * 0.5f;
                float d = new Vector2(m.x, m.z).sqrMagnitude;
                if (d < nearest) { nearest = d; open = i; }
            }
            foreach (int stage in new[] { 1, 2 })
            {
                parent = stageGroups[stage];
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = c[i], b = c[(i + 1) % 4];
                    a.y = Height(a.x, a.z); b.y = Height(b.x, b.z);
                    Pole(a, 1.2f, stage == 1 ? 1 : 0);
                    if (i == open) continue;
                    Beam(new Vector3(a.x, Mathf.Max(a.y, b.y) + 0.9f, a.z), new Vector3(b.x, Mathf.Max(a.y, b.y) + 0.9f, b.z), stage == 1 ? 1 : 0);
                }
            }
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

        // The middle ring between the square and the houses, in mirrored pairs: a well and the evening fire
        // (its meat on a spit) south, either side of the axis; drying racks and a cart north, by the hall.
        static void Commons()
        {
            Vector2 well = At(8f, -15f), fire = At(-8f, -15f), drying = At(-11f, 16f), cart = At(11f, 16f);
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
            for (int stage = 0; stage < 3; stage++)
            {
                parent = stageGroups[stage];
                Vector3 f = new Vector3(fire.x, Height(fire.x, fire.y), fire.y);
                GameObject pit = SpProp(new[] { "fire_pit" }, f, 0f, false, -1, stage > 0, stage == 0);
                if (stage == 0) continue;
                if (stage == 2) SpSpit(pit, 5);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 120f + 30f;
                    Vector3 q = f + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 2.6f);
                    q.y = Height(q.x, q.z);
                    SpSeat(new[] { "piece_bench01" }, q, (f - q).normalized);
                }
            }
            // (1.10.93 had two spits standing by the hall, cooking nothing: the user asked what they were for)
            // the village's wood, stacked by the hall; the cart by it
            foreach (int stage in new[] { 1, 2 })
            {
                parent = stageGroups[stage];
                SpProp(new[] { "wood_stack" }, new Vector3(drying.x, Height(drying.x, drying.y), drying.y), 0f, false);
                SpProp(new[] { "wood_fine_stack", "wood_stack" }, new Vector3(drying.x - 1.6f, Height(drying.x - 1.6f, drying.y), drying.y), 0f, false);
                SpProp(new[] { "Cart" }, new Vector3(cart.x, Height(cart.x, cart.y), cart.y), 20f, false);
            }
            Taken(well, 2.6f);
            Taken(fire, 3.8f);
            Taken(drying, 3f);
            Taken(cart, 3f);
        }

        // ---- what's taken, so the meadow leaves it be ----

        struct Footprint { internal Vector2 At; internal float Yaw; internal Vector2 Half; }
        static readonly List<Footprint> occupied = new List<Footprint>();
        static readonly List<Vector3> takenCircles = new List<Vector3>(); // x, z, radius
        static readonly List<Vector2> pathPoints = new List<Vector2>();

        static void Taken(Vector2 at, float radius) { takenCircles.Add(new Vector3(at.x, at.y, radius)); }

        static bool Free(float x, float z, float margin)
        {
            foreach (Vector3 c in takenCircles)
                if ((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y) < (c.z + margin) * (c.z + margin)) return false;
            foreach (Footprint f in occupied)
            {
                Vector3 local = Quaternion.Euler(0f, -f.Yaw, 0f) * new Vector3(x - f.At.x, 0f, z - f.At.y);
                if (Mathf.Abs(local.x) < f.Half.x + margin && Mathf.Abs(local.z) < f.Half.y + margin) return false;
            }
            foreach (Vector2 p in pathPoints)
                if ((x - p.x) * (x - p.x) + (z - p.y) * (z - p.y) < (1.6f + margin) * (1.6f + margin)) return false;
            return true;
        }

        // The ground round the houses grown over as Valhalla's meadow (the user found it bare): drifts of wild
        // flowers, flax and thistles, a few bushes, mushrooms and half-buried stones, clear of the buildings,
        // the paths, the square and every place of the village; over the slopes too, down to the beach.
        static void SpMeadow(LargeKind kind)
        {
            parent = groupAll;
            string[] bushes = { "Bush01", "Bush02_en", "shrub_2" };
            int placed = 0;
            float reach = kind.Shore - 6f;
            for (float gx = -reach; gx <= reach; gx += 4.5f)
                for (float gz = -reach; gz <= reach; gz += 4.5f)
                {
                    float x = gx + 2f * (Mathf.PerlinNoise(gx * 0.31f + 5f, gz * 0.17f) - 0.5f) * 2f, z = gz + 2f * (Mathf.PerlinNoise(gz * 0.23f, gx * 0.29f + 9f) - 0.5f) * 2f;
                    if (x * x + z * z > reach * reach || Height(x, z) < 0.8f || !Free(x, z, 0.8f)) continue;
                    float pick = Mathf.PerlinNoise(x * 0.05f + 300f, z * 0.05f + 77f);
                    float jitter = Mathf.PerlinNoise(x * 0.9f + 11f, z * 0.9f + 3f);
                    if (jitter < 0.3f) continue; // the gaps between drifts
                    string name;
                    float sink = 0f;
                    if (pick < 0.3f) { name = bushes[(int)(jitter * 10f) % bushes.Length]; sink = 0.1f; }
                    else if (pick < 0.48f) name = "Pickable_Flax_Wild";
                    else if (pick < 0.56f) name = "Pickable_Thistle";
                    else if (pick < 0.63f) { name = jitter > 0.6f ? "Rock_4" : "Rock_7_meadows"; sink = 0.15f; }
                    else if (pick < 0.7f) name = "Pickable_Mushroom";
                    else name = "Pickable_Dandelion";
                    SpPlant(name, x, Height(x, z), z, Mathf.Repeat(x * 37f + z * 11f, 360f), sink);
                    placed++;
                    if (name == "Pickable_Flax_Wild" || name == "Pickable_Dandelion")
                        for (int k = 0; k < 3; k++)
                        {
                            float cx = x + 1.3f * Mathf.Cos(k * 2.1f + jitter * 6f), cz = z + 1.3f * Mathf.Sin(k * 2.1f + jitter * 6f);
                            if (!Free(cx, cz, 0.5f)) continue;
                            SpPlant(name, cx, Height(cx, cz), cz, k * 97f, sink);
                            placed++;
                        }
                }
            Plugin.Debug("Brim village: its meadow, " + placed + " plants and stones");
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
                else SpPlant(i % 4 == 1 ? "RaspberryBush" : "Bush01", q.x, Height(q.x, q.y), q.y, a * 90f, 0.1f);
            }
        }

        // A worn dirt path from one point to another (from `from` out of a circle of `skip` round it); a light
        // halfway along once rebuilt, of several kinds in turn.
        static readonly string[][] PathLights =
        {
            new[] { "piece_groundtorch_wood" }, new[] { "CastleKit_groundtorch", "piece_groundtorch_wood" }, new[] { "piece_groundtorch", "piece_groundtorch_wood" },
        };

        // A path out from the square's edge: the axis paved and straight (to the hall, to the fisher's house);
        // the rest worn dirt, a gentle bend in each, laid close so its edges run smooth (1.10.93's were rows of
        // blots); a light halfway along once rebuilt.
        static void Path(Vector2 from, Vector2 to, float skip, int k)
        {
            Vector2 d = to - from;
            float length = d.magnitude;
            if (length < skip + 1f) return;
            Vector2 dir = d / length, across = new Vector2(-dir.y, dir.x);
            Vector2 a = from + dir * skip, b = to;
            bool axis = Mathf.Abs(to.x) < 1f;
            Vector2 bend = (a + b) * 0.5f + across * (axis ? 0f : (k % 2 == 0 ? 1f : -1f) * (b - a).magnitude * 0.12f);
            float span = (b - a).magnitude;
            for (float t = 0f; t <= 1.001f; t += 0.9f / Mathf.Max(1f, span))
            {
                Vector2 q = Bezier(a, bend, b, t);
                SpPaint(q.x, q.y, axis ? 1.5f : 1.1f, axis ? TerrainModifier.PaintType.Paved : TerrainModifier.PaintType.Dirt);
                pathPoints.Add(q);
            }
            if (span < 8f) return;
            Transform keep = parent;
            parent = stageGroups[2];
            Vector2 m = Bezier(a, bend, b, 0.5f) + across * (axis ? 2.4f : 2f);
            SpProp(PathLights[k % PathLights.Length], new Vector3(m.x, Height(m.x, m.y), m.y), 0f, false, -1, true);
            parent = keep;
        }

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

        internal static void SpiritHomeToIslet()
        {
            HomePieces.Clear();
            Quaternion turn = Quaternion.Euler(0f, homePose.Yaw, 0f);
            foreach (HomePiece h in homeLocal)
                HomePieces.Add(new HomePiece { Prefab = h.Prefab, At = homePose.At + turn * h.At, Yaw = homePose.Yaw + h.Yaw, AlongZ = h.AlongZ });
        }

        // ---- pieces ----

        // A plant (or bush, or small stone) by its own root, as Valhalla's meadow is laid: its pivot sunk a
        // little into the ground at (x, z) of the current parent, whose ground is at `ground` (1.10.93's
        // bushes, put by their size, stood in the air).
        static GameObject SpPlant(string name, float x, float ground, float z, float yaw, float sink)
        {
            return SkyPivot(name, new Vector3(x, ground - sink, z), Quaternion.Euler(0f, yaw, 0f));
        }

        static GameObject SpProp(string[] names, Vector3 at, float yaw, bool byTop, int wear = -1, bool lit = false, bool cold = false, System.Action<GameObject> first = null)
        {
            GameObject prefab = Find(names);
            if (prefab == null) { missing++; Plugin.Debug("Brim village: none of " + string.Join(", ", names)); return null; }
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (wear >= 0) Wear(go, wear);
            if (lit) LightFires(go);
            else if (cold) Cold(go);
            if (first != null) first(go);
            // a crafting station without its working marks (the ring of its reach, the hammer over it in use)
            foreach (CraftingStation station in go.GetComponentsInChildren<CraftingStation>(true))
                foreach (GameObject mark in new[] { station.m_areaMarker, station.m_inUseObject })
                    if (mark != null) mark.SetActive(false);
            Strip(go);
            go.transform.localPosition = Vector3.zero;
            Move(go, LocalBounds(go), at, byTop);
            built++;
            return go;
        }

        static GameObject SpSeat(string[] names, Vector3 at, Vector3 look)
        {
            Vector3 own = SeatFacing(Find(names));
            if (own == Vector3.zero) Plugin.Debug("Brim village: " + names[0] + " has no seat to face by");
            return SpProp(names, at, YawFor(own, look), false);
        }

        // Moved along out (+x, -x or +z of its frame) until its side that way touches a wall's inner face, which
        // lies at |plane| along out.
        static GameObject SpAgainst(GameObject go, Vector3 out_, float plane)
        {
            if (go == null) return null;
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
