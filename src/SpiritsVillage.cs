using System.Collections.Generic;
using Jotunn.Managers;
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
            soulCount = 0;
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
            Memorial(memorial, At(7.75f, -15.5f)); // its path comes from the gap south-east of the square (se)
            Taken(Vector2.zero, 9f);
            Taken(lighthouse, 5f);
            Taken(yard, SpiritYardRadius + 1f);
            Taken(memorial, 5f);
            occupied.Add(new Footprint { At = field, Yaw = 0f, Half = new Vector2(5.5f, 4.5f) });
            // The pier continues the axis south, past the fisher's house, into the sea (north, behind the hall, if
            // the cliff is south).
            float dock = Mathf.Abs(Mathf.DeltaAngle(270f, cliffDeg)) < 70f ? 90f : 270f;
            // The paths (the user: the hall's of stone, the rest all alike, earth with a fence either side): from
            // the square's edge to every door, straight; to the corners' places through the gaps between the
            // houses (1.10.95-97 ran straight at them, along house walls); to the pier past the fisher's house.
            // (each gap's point on the line from the middle that passes as far from both houses' corners as it can:
            // the hall's and the smith's, the hall's and the player's, the weaver's and the fisher's, the brewer's
            // and the fisher's)
            Vector2 nw = At(-11.4f, 14.35f), ne = At(11.4f, 14.35f), sw = At(-8.75f, -15.5f), se = At(7.75f, -15.5f);
            Lane(0, true, Vector2.zero, Door(hall, hallStyle));
            for (int i = 0; i < places.Length; i++) Lane(i + 1, false, Vector2.zero, Door(places[i], styles[i]));
            Lane(6, false, Vector2.zero, nw, lighthouse - (lighthouse - nw).normalized * 4.5f);
            Lane(7, false, Vector2.zero, ne, yard - (yard - ne).normalized * (SpiritYardRadius + 0.5f));
            Lane(8, false, Vector2.zero, sw, field - (field - sw).normalized * 5f);
            Lane(9, false, Vector2.zero, se, memorial - (memorial - se).normalized * 4.5f);
            Vector2 pierFoot = Pier(dock, top);
            Vector2 bySide = dock > 180f ? At(-6.5f, -31f) : At(-9.5f, 36f);
            Lane(10, false, dock > 180f ? sw : nw, bySide, pierFoot);
            DressLanes();
            // The Brim Folk at home (T5 done): a few walk the lanes, from the square's edge (clear of its benches)
            // and back, resting a while at either end; one goes slowly round the square.
            {
                const float edge = 7.6f;
                Vector2 hallDoor = Door(hall, hallStyle), pierGap = dock > 180f ? sw : nw;
                SoulWalk(0.9f, 5f, false, hallDoor.normalized * edge, hallDoor);
                SoulWalk(1.0f, 6f, false, pierGap.normalized * edge, pierGap, bySide, pierFoot);
                SoulWalk(0.85f, 8f, false, se.normalized * edge, se, memorial - (memorial - se).normalized * 4.5f);
                SoulWalk(0.9f, 5f, false, nw.normalized * edge, nw, lighthouse - (lighthouse - nw).normalized * 4.5f);
                Vector2[] round = new Vector2[12];
                for (int i = 0; i < round.Length; i++)
                {
                    float a = i * Mathf.PI * 2f / round.Length;
                    round[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 7.2f;
                }
                SoulWalk(0.8f, 0f, true, round);
                GameObject souls = new GameObject("IoV_BrimSouls");
                souls.transform.SetParent(top, false);
                souls.AddComponent<BrimSouls>().village = true;
            }
            parent = groupAll;
            Trees(kind);
            Slopes(kind);
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
            wallsTop = wallTop;

            // the doorway: the hall's great dark gate, the game's own (1.10.92/93 made a copy of it that the game
            // never had, and a wooden door stood in for it), or a wooden door a quarter taller than the game's
            // The hall's dark gate is one leaf (the game: "to use by itself or as part of a pair"): a pair of them,
            // the second turned about so its hinges are on the other side, as the user's own photo of it shows.
            // Every door measured as it stands (only its parts that show: 1.10.92-97 measured all its models,
            // its unseen worn and broken ones too, and cut the doorway too wide for it).
            GameObject gate = style.Trade == Trade.Hall ? SpiritsIslet.DarkGate() : null;
            Vector3 leaf = DoorSize(gate != null ? gate.name : "wood_door", gate != null ? 1f : DoorScale);
            doorAt = gate != null ? 0f : DoorX(width, Size(rows[0]).x);
            doorWide = (gate != null ? leaf.x * 2f : leaf.x) + 0.04f;
            doorHigh = Mathf.Min(leaf.y + 0.03f, wallTop - 0.3f);
            Plugin.Debug("Brim village: " + style.Trade + "'s door " + (gate != null ? gate.name + " (a pair)" : "wood_door") + ", a leaf " + leaf.ToString("F2") + ", the doorway " + doorWide.ToString("F2") + " x " + doorHigh.ToString("F2"));

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
            // The gate's leaves with their hinges at the doorway's sides and their rings meeting in the middle,
            // the braces rising to an arch there (1.10.98 had the two the other way round: hinges in the middle).
            if (gate != null)
                foreach (float s in new[] { -1f, 1f })
                    DoorPieces.Add(new HomePiece { Prefab = new[] { gate.name }, At = pose + turn * new Vector3(s * leaf.x * 0.5f, 0f, -length * 0.5f), Yaw = yaw + (s < 0f ? 180f : 0f), Scale = 1f });
            else
                DoorPieces.Add(new HomePiece { Prefab = new[] { "wood_door" }, At = pose + turn * new Vector3(doorAt, 0f, -length * 0.5f), Yaw = yaw, Scale = DoorScale });
        }

        // A door's leaf as it shows (width across its doorway, height, thickness), scaled: a still copy measured
        // and taken away again.
        static Vector3 DoorSize(string name, float scale)
        {
            Transform keep = parent;
            parent = groupAll;
            GameObject probe = SpProp(new[] { name }, Vector3.zero, 0f, false);
            parent = keep;
            if (probe == null) return new Vector3(1.6f, 2.5f, 0.2f) * scale;
            Bounds b = LocalBounds(probe);
            Object.DestroyImmediate(probe);
            built--;
            return new Vector3(b.size.x, b.size.y, b.size.z) * scale;
        }

        static FramePose homePose;
        // the doorway of the building being built: its middle's x, its width and height
        static float doorAt, doorWide = 2f, doorHigh = 2f;
        static float wallsTop = 3f; // how high its walls stand

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

        static int hallSeat, hallSouls;

        static void Inside(Trade trade, int stage, float length, float width)
        {
            hallSeat = hallSouls = 0;
            float back = length * 0.5f, side = width * 0.5f;
            if (stage == 0)
            {
                Rubble(length, width, trade == Trade.Hall ? 6 : 3);
                if (trade == Trade.Hall) SpProp(new[] { "hearth" }, new Vector3(0f, 0f, -back + 7f), 0f, false, -1, false, true);
                return;
            }
            if (trade == Trade.Hall)
            {
                if (stage == 1) { SpSpit(Hearth(new Vector3(0f, 0f, -back + 7f)), 0, null, true); return; }
                // two fires down the middle, meat roasting over them (hearth and spit along the hall, so the way
                // past them stays open); a long table either side well clear of them, benches both sides, the
                // feast laid on every other table, drink on the rest
                // (each fire its own: red meat on one, fish and fowl on the other; 1.10.99-100 put nearly the
                // same meats on both)
                int k = 0;
                foreach (float z in new[] { -back + 7f, back - 9f })
                {
                    SpSpit(Hearth(new Vector3(0f, 0f, z)), 0, HallSpits[k], true);
                    k++;
                }
                // (the oak table is 6.6 m long: 1.10.89-97 set them 4.5 m apart, one into the next, and the
                // drink at their ends lay hidden in the next one's feast) two either side, end to end with a gap
                foreach (float s in new[] { -1f, 1f })
                    foreach (float z in new[] { -back + 6.3f, -back + 13.1f })
                    {
                        float x = s * (side - 2.6f);
                        GameObject t = SpProp(new[] { "piece_table_oak", "piece_table" }, new Vector3(x, 0f, z), 0f, false);
                        SpAlongZ(t, new Vector3(x, 0f, z));
                        SpFeastTable(t, k++);
                        foreach (float off in new[] { -1.15f, 1.15f })
                            foreach (float dz in new[] { -1.6f, 1.6f })
                            {
                                GameObject b = SpSeat(new[] { "piece_bench01" }, new Vector3(x + off, 0f, z + dz), new Vector3(-Mathf.Sign(off), 0f, 0f));
                                // the folk at home (T5 done): here one, there two, a good many places empty; every
                                // third with a tankard; the first of them to talk to
                                foreach (float along in new[] { -0.55f, 0.55f })
                                {
                                    if (SoulPick(hallSeat++, 23) > 0.34f) continue;
                                    SoulSpot soul = SoulOn(b, along, true, hallSouls % 3 == 1 ? "Tankard" : "");
                                    if (soul != null && hallSouls++ == 0) soul.talks = true;
                                }
                            }
                    }
                // the high seat on a stone dais at the far end, blue fires either side, furs before it
                float daisZ = back - 2f;
                for (float x = -2f; x <= 2.01f; x += 2f)
                    SpProp(new[] { "stone_floor_2x2" }, new Vector3(x, 0.35f, daisZ), 0f, true);
                GameObject highSeat = SpSeat(new[] { "piece_throne01", "piece_chair02" }, new Vector3(0f, 0.35f, daisZ), Vector3.back);
                SoulSpot elder = SoulOn(highSeat, 0f, false); // the village's elder on it, to talk to
                if (elder != null) { elder.who = SoulSpot.Elder; elder.talks = true; }
                // standing iron torches either side of it, and sconces down both side walls over the benches,
                // between the tables' ends (no braziers indoors: the user, 1.10.99)
                foreach (float x in new[] { -2.4f, 2.4f })
                    SpProp(new[] { "piece_groundtorch", "piece_groundtorch_wood" }, new Vector3(x, 0.35f, daisZ), 0f, false, -1, true);
                foreach (char w in new[] { 'L', 'R' })
                    foreach (float z in new[] { -back + 3f, -back + 9.7f, back - 4.5f })
                        Sconce(w, z, 2.2f, side, back);
                // The walls hung as a fishing village's hall (the user found them bare and asked for them filled
                // as Valhalla's are, in the village's own way; 1.10.101 hung Valhalla's very banners and hunting
                // trophies here): on the game's item stands, between the sconces, the catches the village was
                // proud of, a different fish on each, the folk's own plain shields above them; by the door its
                // rod and its harpoon; by the high seat the great angler and the pufferfish; and over the high
                // seat, high on the back wall (where the user pointed: 1.10.102 had it by the seat, too big for
                // there), the sea serpent's head, a banner either side of it a good way off.
                float inner = side - 0.15f;
                int n = 0;
                foreach (float z in new[] { -back + 4.6f, -back + 8f, -back + 11.4f, -back + 14.8f })
                    foreach (float s in new[] { -1f, 1f })
                    {
                        SpOnStand(HallCatches[n % HallCatches.Length], new Vector3(s, 0f, 0f), inner, 2.1f, z);
                        SpOnStand(HallShields[n % HallShields.Length], new Vector3(s, 0f, 0f), inner, Mathf.Min(3.5f, wallsTop - 1.4f), z);
                        n++;
                    }
                SpOnStand(new[] { "FishingRod" }, Vector3.left, inner, 2.1f, -back + 1.4f);
                SpOnStand(new[] { "SpearChitin", "FishingRod" }, Vector3.right, inner, 2.1f, -back + 1.4f);
                SpOnStand(new[] { "Fish12", "Fish11", "Fish4_cave" }, Vector3.left, inner, 2.1f, back - 2.8f);
                SpOnStand(new[] { "Fish9", "Fish12" }, Vector3.right, inner, 2.1f, back - 2.8f);
                GameObject serpent = SpOnStand(new[] { "TrophySerpent" }, Vector3.forward, back - 0.15f, wallsTop + 0.3f, 0f);
                Bounds sh = serpent != null ? LocalBounds(serpent) : new Bounds();
                float reach = sh.size.x > 0.5f ? sh.extents.x : 1.6f, high = sh.size.y > 0.5f ? sh.max.y : wallsTop + 2.3f;
                foreach (float s in new[] { -1f, 1f })
                    SpBannerOn("piece_banner02", Vector3.forward, back - 0.15f, high, s * (reach + 1.6f));
                Plugin.Debug("Brim village: the serpent's head over the high seat reaches " + reach.ToString("F1") + " m either side, up to " + high.ToString("F1") + " m");
                SpProp(new[] { "rug_fur", "rug_wolf", "rug_deer" }, new Vector3(0f, 0.02f, daisZ - 2.8f), 0f, false);
                SpProp(new[] { "rug_wolf", "rug_fur" }, new Vector3(0f, 0.02f, -back + 3f), 90f, false);
                return;
            }
            if (stage == 1) { if (trade != Trade.Home) Piles(length, width); return; }
            // Rebuilt: each house laid out by hand for its own trade and shape (1.10.89-97 put the same things in
            // the same corners of every house, and in the player's put a table over the real bed), the door's way
            // in kept clear. In frame space: the door at -z (its middle at doorAt), x left (-) and right (+).
            string[] bed = { "bed", "piece_bed02" }, chest = { "piece_chest_wood" }, table = { "piece_table" }, bench = { "piece_bench01" }, barrel = { "barrell" };
            string[] torch = { "piece_groundtorch_wood", "piece_groundtorch" };
            switch (trade)
            {
                case Trade.Smith: // the forge at the back, the anvil beside it, the bench on the right; he sleeps by the door
                    P(new[] { "forge" }, -1.6f, back - 1f, 180f, ' ', side, back, 'B');
                    {
                        GameObject anvil = P(new[] { "forge_ext1", "forge_ext2" }, -side + 0.6f, back - 3.5f, 90f, ' ', side, back, 'L');
                        // (the smith at it, hammer in hand, at work: T5 done)
                        if (anvil != null)
                        {
                            Bounds ab = LocalBounds(anvil);
                            SoulAt(new Vector3(ab.max.x + 0.6f, 0f, ab.center.z), Vector3.left, "Hammer", 1.4f).chest = "ArmorLeatherChest";
                        }
                    }
                    P(new[] { "piece_workbench" }, side - 1f, 1f, -90f, 'z', side, back, 'R');
                    P(chest, side - 0.5f, back - 0.6f, -90f, ' ', side, back, 'R', 'B');
                    P(bed, side - 1f, -back + 2.4f, 0f, 'z', side, back, 'R');
                    SpProp(torch, new Vector3(-side + 0.6f, 0f, -back + 0.8f), 0f, false, -1, true);
                    Sconce('B', 1.3f, 1.6f, side, back);  // between the forge and the chest
                    Sconce('L', -1.5f, 1.6f, side, back); // between the anvil and the door
                    SpProp(new[] { "rug_wolf", "rug_fur" }, new Vector3(0.5f, 0.02f, -0.8f), 0f, false);
                    break;
                case Trade.Weaver: // her bed along the back wall, the wheel on the right, chests, a table by the front wall
                    P(bed, -side + 1.6f, back - 1f, 90f, 'x', side, back, 'L', 'B');
                    P(chest, side - 0.5f, back - 0.6f, -90f, ' ', side, back, 'R', 'B');
                    {
                        GameObject wheel = P(new[] { "piece_spinningwheel" }, side - 1f, 0.5f, -90f, ' ', side, back, 'R');
                        // (the weaver at it: T5 done; a woman, as the folk's odd ones are)
                        if (wheel != null)
                        {
                            Bounds wb = LocalBounds(wheel);
                            SoulAt(new Vector3(wb.min.x - 0.5f, 0f, wb.center.z), Vector3.right).index = 101;
                        }
                    }
                    P(chest, -side + 0.5f, 0f, 90f, ' ', side, back, 'L');
                    {
                        GameObject t = P(table, side - 1.6f, -back + 0.8f, 0f, 'x', side, back, 'F');
                        SpLaid(t, 11);
                        if (t != null) SpSeat(bench, new Vector3(LocalBounds(t).center.x, 0f, LocalBounds(t).max.z + 0.55f), Vector3.back);
                    }
                    SpProp(torch, new Vector3(-side + 0.6f, 0f, -back + 0.8f), 0f, false, -1, true);
                    Sconce('B', 1f, 1.6f, side, back);    // over the bed's foot, short of the chest
                    Sconce('R', -2.2f, 1.6f, side, back); // between the wheel and the table
                    SpProp(new[] { "rug_fur", "rug_deer" }, new Vector3(-0.5f, 0.02f, 1f), 90f, false);
                    break;
                case Trade.Fisher: // a narrow house: the bed across its back, the table on the left, chest and barrels on the right
                    P(bed, 0f, back - 1f, 90f, 'x', side, back, 'B');
                    {
                        GameObject t = P(table, -side + 1f, 0.3f, 0f, 'z', side, back, 'L');
                        SpLaid(t, 12);
                        if (t != null) SoulOn(SpSeat(bench, new Vector3(LocalBounds(t).max.x + 0.55f, 0f, LocalBounds(t).center.z), Vector3.left), 0f, true); // (at his supper: T5 done)
                    }
                    P(chest, side - 0.5f, 1.2f, -90f, ' ', side, back, 'R');
                    P(barrel, side - 0.5f, -back + 0.6f, 0f, ' ', side, back, 'R', 'F');
                    P(barrel, side - 0.5f, -back + 1.6f, 40f, ' ', side, back, 'R');
                    SpProp(torch, new Vector3(-side + 0.5f, 0f, -back + 0.8f), 0f, false, -1, true);
                    Sconce('L', -2.5f, 1.6f, side, back); // between the table and the door
                    Sconce('R', -1.2f, 1.6f, side, back); // between the chest and the barrels
                    SpProp(new[] { "rug_deer", "rug_fur" }, new Vector3(0.3f, 0.02f, -1.2f), 0f, false);
                    break;
                case Trade.Brewer: // a long house: two fermenters at the back, barrels by the door, a table, the bed on the right
                    P(new[] { "fermenter" }, -side + 1.2f, back - 1f, 180f, ' ', side, back, 'B', 'L');
                    P(new[] { "fermenter" }, 0.6f, back - 1f, 180f, ' ', side, back, 'B');
                    P(barrel, -side + 0.5f, -back + 0.6f, 0f, ' ', side, back, 'L', 'F');
                    P(barrel, -side + 0.5f, -back + 1.6f, 30f, ' ', side, back, 'L');
                    P(barrel, -side + 1.4f, -back + 0.6f, 70f, ' ', side, back, 'F');
                    {
                        GameObject t = P(table, -side + 1f, 1.2f, 0f, 'z', side, back, 'L');
                        SpLaid(t, 13);
                        if (t != null) SoulOn(SpSeat(bench, new Vector3(LocalBounds(t).max.x + 0.55f, 0f, LocalBounds(t).center.z), Vector3.left), 0f, true, "Tankard"); // (tasting his own: T5 done)
                    }
                    P(bed, side - 1f, 2f, 0f, 'z', side, back, 'R');
                    P(chest, side - 0.5f, -1.2f, -90f, ' ', side, back, 'R');
                    SpProp(torch, new Vector3(side - 0.6f, 0f, -back + 0.8f), 0f, false, -1, true);
                    Sconce('L', -2f, 1.6f, side, back);   // between the table and the barrels
                    Sconce('R', -3.5f, 1.6f, side, back); // between the chest and the door
                    SpProp(new[] { "rug_fur", "rug_wolf" }, new Vector3(1f, 0.02f, -3f), 0f, false);
                    break;
                default: // the player's own: its real beds (back corners) and chests (right wall) come from the
                         // altar (HomePlan); here a table on the left, a rug, and sconces either side of the door
                         // and between the beds (1.10.98 lit it with two braziers by the door)
                    {
                        GameObject t = P(table, -side + 1f, -1.8f, 0f, 'z', side, back, 'L');
                        SpLaid(t, 14);
                        if (t != null) SpSeat(bench, new Vector3(LocalBounds(t).max.x + 0.55f, 0f, LocalBounds(t).center.z), Vector3.left);
                    }
                    foreach (float s in new[] { -1f, 1f })
                        Sconce('F', doorAt + s * (doorWide * 0.5f + 0.7f), 1.6f, side, back);
                    Sconce('B', 0f, 1.6f, side, back);
                    // the game's bed asks for a fire's warmth where it stands (Bed.CheckFire: a heat area), which
                    // the braziers gave and a sconce may not: the house's warmth round each of its beds
                    foreach (HomePiece h in homeLocal)
                        if (h.Prefab[0] == "bed") Warmth(h.At + Vector3.up * 0.6f, 2.2f);
                    SpProp(new[] { "rug_deer", "rug_fur" }, new Vector3(0.6f, 0.02f, 1f), 0f, false);
                    break;
            }
        }

        // A thing put by hand in a house: at (x, z) of its frame, turned yaw, made to lie along z or x ('z', 'x';
        // ' ' as it comes), then pushed up against the walls named (L, R, B back, F front).
        static GameObject P(string[] names, float x, float z, float yaw, char lie, float side, float back, params char[] walls)
        {
            Vector3 at = new Vector3(x, 0f, z);
            GameObject go = SpProp(names, at, yaw, false);
            if (go == null) return null;
            if (lie == 'z') SpAlongZ(go, at);
            else if (lie == 'x') SpAlongX(go, at);
            foreach (char w in walls)
                switch (w)
                {
                    case 'L': SpAgainst(go, Vector3.left, side - 0.15f); break;
                    case 'R': SpAgainst(go, Vector3.right, side - 0.15f); break;
                    case 'B': SpAgainst(go, Vector3.forward, back - 0.15f); break;
                    case 'F': SpAgainst(go, Vector3.back, back - 0.15f); break;
                }
            return go;
        }

        // A sconce on a wall inside (L, R, B back, F front), `along` it (z on a side wall, x on an end one), its
        // foot `y` up, lit, its torch leaning into the room and its back on the wall. (The user, 1.10.99: no
        // braziers indoors, and no house without light.) Where the game has none, a standing torch by that wall.
        static GameObject Sconce(char wall, float along, float y, float side, float back)
        {
            bool onSide = wall == 'L' || wall == 'R';
            Vector3 inward = wall == 'L' ? Vector3.right : wall == 'R' ? Vector3.left : wall == 'B' ? Vector3.back : Vector3.forward;
            float plane = onSide ? side - 0.15f : back - 0.15f;
            Vector3 at = -inward * (plane - 0.6f) + (onSide ? new Vector3(0f, 0f, along) : new Vector3(along, 0f, 0f));
            Vector3 lean = SconceLean();
            if (lean == Vector3.zero)
                return SpAgainst(SpProp(new[] { "piece_groundtorch_wood" }, at, 0f, false, -1, true), -inward, plane - 0.1f);
            at.y = y;
            return SpAgainst(SpProp(SconceNames, at, YawFor(lean, inward), false, -1, true), -inward, plane);
        }

        // A fire's warmth with no fire to see: the game's own heat area (a trigger sphere on the layer its
        // areas are looked for on), as a fire's.
        static void Warmth(Vector3 at, float radius)
        {
            GameObject go = new GameObject("IoV_SpiritWarmth");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.layer = LayerMask.NameToLayer("character_trigger");
            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = radius;
            go.AddComponent<EffectArea>().m_type = EffectArea.Type.Heat;
        }

        static readonly string[] SconceNames = { "piece_walltorch" };
        static bool sconceMeasured;
        static Vector3 sconceLean;

        // Which way (level, in its own space) the game's sconce leans out from the wall it hangs on: from the
        // middle of it towards its flame, or else from its snap points towards its middle; zero if neither
        // tells (or the game has none). Measured once, on a still copy, and logged.
        static Vector3 SconceLean()
        {
            if (sconceMeasured) return sconceLean;
            sconceMeasured = true;
            GameObject prefab = Find(SconceNames);
            if (prefab == null) { Plugin.Log.LogWarning("Brim village: the game has no sconce (piece_walltorch); standing torches by the walls instead"); return sconceLean; }
            Transform keep = parent;
            parent = groupAll;
            GameObject probe = SpProp(SconceNames, Vector3.zero, 0f, false, -1, true);
            parent = keep;
            if (probe == null) return sconceLean;
            Bounds b = LocalBounds(probe);
            Vector3 byFlame = Vector3.zero, bySnap = Vector3.zero;
            Light light = probe.GetComponentInChildren<Light>(true);
            if (light != null) byFlame = root.transform.InverseTransformPoint(light.transform.position) - b.center;
            byFlame.y = 0f;
            List<Vector3> snaps = SnapPoints(prefab);
            if (snaps.Count > 0)
            {
                Vector3 mid = Vector3.zero;
                foreach (Vector3 s in snaps) mid += s;
                mid /= snaps.Count;
                bySnap = b.center - root.transform.InverseTransformPoint(probe.transform.TransformPoint(mid));
                bySnap.y = 0f;
            }
            Object.DestroyImmediate(probe);
            built--;
            sconceLean = byFlame.magnitude > 0.05f ? byFlame.normalized : bySnap.magnitude > 0.05f ? bySnap.normalized : Vector3.zero;
            Plugin.Debug("Brim village: the sconce is " + b.size.ToString("F2") + "; out from its middle to its flame " + byFlame.ToString("F2") + ", from its " + snaps.Count + " snap points to its middle " + bySnap.ToString("F2") + "; leans out along " + sconceLean.ToString("F2"));
            if (sconceLean == Vector3.zero) Plugin.Log.LogWarning("Brim village: can't tell which way the sconce leans; standing torches by the walls instead");
            return sconceLean;
        }

        static void SpAlongX(GameObject go, Vector3 at)
        {
            if (go == null) return;
            Bounds b = LocalBounds(go);
            if (b.size.z <= b.size.x) return;
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * go.transform.localRotation;
            go.transform.localPosition = Vector3.zero;
            Move(go, LocalBounds(go), at, false);
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
            float wall = width * 0.5f + 0.15f; // the walls' outer faces
            if (stage == 1) { Out(SpProp(new[] { "wood_stack" }, new Vector3(wall + 1f, 0f, 1f), 0f, false), 1f, wall); return; }
            switch (trade)
            {
                case Trade.Smith:
                    Out(SpProp(new[] { "wood_stack" }, new Vector3(wall + 1f, 0f, 1.5f), 0f, false), 1f, wall);
                    Out(SpProp(new[] { "wood_core_stack", "wood_stack" }, new Vector3(wall + 1f, 0f, -1.4f), 0f, false), 1f, wall);
                    break;
                case Trade.Weaver:
                    Out(SpProp(new[] { "piece_beehive" }, new Vector3(-wall - 1f, 0f, 1.5f), 90f, false), -1f, wall);
                    Out(SpProp(new[] { "piece_beehive" }, new Vector3(-wall - 1f, 0f, -0.5f), 90f, false), -1f, wall);
                    break;
                case Trade.Fisher:
                    Out(SpProp(new[] { "barrell" }, new Vector3(wall + 1f, 0f, -1.5f), 0f, false), 1f, wall);
                    Out(SpProp(new[] { "wood_stack" }, new Vector3(wall + 1f, 0f, 1.2f), 90f, false), 1f, wall);
                    break;
                case Trade.Brewer:
                    foreach (float z in new[] { -2f, -0.8f, 0.4f })
                        Out(SpProp(new[] { "barrell" }, new Vector3(wall + 1f, 0f, z), z * 40f, false), 1f, wall);
                    break;
            }
        }

        // Moved out from a side wall (side 1 right, -1 left) until its inner side is a hand clear of the wall's
        // outer face (1.10.94's piles reached into the houses).
        static void Out(GameObject go, float side, float wall)
        {
            if (go == null) return;
            Bounds b = LocalBounds(go);
            float inner = side > 0f ? b.min.x : -b.max.x;
            go.transform.localPosition += new Vector3(side * (wall + 0.12f - inner), 0f, 0f);
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
            // (its light: sconces on its walls, Inside; 1.10.98's braziers by the door are gone)
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
            if (length >= 3f && k % 2 == 1)
            {
                // a table of drink: tankards and horns down both sides (the user asked for drink on the hall's)
                int j = 0;
                for (float a = -length * 0.5f + 0.5f; a < length * 0.5f - 0.3f; a += 0.75f)
                    foreach (float c in new[] { -0.3f, 0.3f })
                    {
                        Vector3 at = middle + (longZ ? new Vector3(c * width, 0f, a) : new Vector3(a, 0f, c * width));
                        SpQuiet(SpProp(new[] { (j + k) % 5 == 2 ? TankardNames[1] : TankardNames[0], TankardNames[0] }, at, k * 31f + j * 67f, false));
                        j++;
                    }
                return;
            }
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

        // What the hall's two fires roast: red meat on one, fish and fowl on the other.
        static readonly string[][] HallSpits =
        {
            new[] { "CookedMeat", "CookedLoxMeat", "CookedBjornMeat", "CookedDeerMeat" },
            new[] { "FishCooked", "CookedChickenMeat", "SerpentMeatCooked", "CookedHareMeat" },
        };

        // The dishes beside a feast, each one of the game's served on a plate or in a bowl (the user asked for
        // the food on plates; 1.10.101-102 laid bread and meat on the bare board): the first of each it has.
        static readonly string[][] TableFood =
        {
            new[] { "MeatPlatter", "SerpentStew" }, new[] { "BlackSoup", "CarrotSoup" }, new[] { "SerpentStew", "TurnipStew" },
            new[] { "CarrotSoup", "TurnipStew" }, new[] { "Salad", "MeatPlatter" }, new[] { "TurnipStew", "CarrotSoup" },
            new[] { "LoxPie", "MeatPlatter" }, new[] { "MinceMeatSauce", "TurnipStew" },
        };

        // A hall table laid for a feast (the user: food and drink on every table, so it shows it's a feast):
        // the game's feast in the middle as Valhalla's; past it at each end two more dishes, then the drink of
        // those sitting at the ends; and beside the feast, where the board has room, a tankard before each place.
        static void SpFeastTable(GameObject table, int k)
        {
            if (table == null) return;
            Bounds tb = LocalBounds(table);
            float board = SolidBounds(table).max.y;
            bool longZ = tb.size.z >= tb.size.x;
            float length = Mathf.Max(tb.size.x, tb.size.z), width = Mathf.Min(tb.size.x, tb.size.z);
            Vector3 middle = new Vector3(tb.center.x, board, tb.center.z);
            string feast = FeastNames[k % FeastNames.Length];
            if (Find(new[] { feast }) == null) feast = FeastNames[0];
            GameObject dish = SpProp(new[] { feast }, middle, 0f, false, -1, false, false, FullFeast);
            float half = 1.75f, beside = 0.5f;
            if (dish != null)
            {
                Bounds db = LocalBounds(dish);
                if ((db.size.z >= db.size.x) != longZ)
                {
                    dish.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * dish.transform.localRotation;
                    dish.transform.localPosition = Vector3.zero;
                    Move(dish, LocalBounds(dish), middle, false);
                    db = LocalBounds(dish);
                }
                half = longZ ? db.extents.z : db.extents.x;
                beside = longZ ? db.extents.x : db.extents.z;
                SpQuiet(dish);
            }
            float end = length * 0.5f - 0.2f, edge = width * 0.5f - 0.12f;
            float across = Mathf.Min(0.35f, edge - 0.25f), drinkAcross = Mathf.Min(0.45f, edge - 0.1f);
            int j = 0;
            foreach (float e in new[] { -1f, 1f })
            {
                float food = half + 0.4f, drink = food + 0.65f;
                if (food + 0.25f < end)
                    foreach (float c in new[] { -across, across })
                    {
                        SpQuiet(SpProp(TableFood[(k * 3 + j) % TableFood.Length], OnBoard(middle, longZ, e * food, c), k * 53f + j * 71f, false));
                        j++;
                    }
                if (drink + 0.1f < end)
                    foreach (float c in new[] { -drinkAcross, drinkAcross })
                    {
                        SpQuiet(SpProp(new[] { (j + k) % 4 == 3 ? TankardNames[1] : TankardNames[0], TankardNames[0] }, OnBoard(middle, longZ, e * drink, c), k * 47f + j * 83f, false));
                        j++;
                    }
            }
            float room = edge - beside;
            if (room > 0.2f)
                for (float a = -half + 0.4f; a <= half - 0.39f; a += 0.8f)
                    foreach (float s in new[] { -1f, 1f })
                    {
                        SpQuiet(SpProp(new[] { TankardNames[0] }, OnBoard(middle, longZ, a, s * (beside + room * 0.5f)), k * 29f + j * 61f, false));
                        j++;
                    }
            Plugin.Debug("Brim village: a hall table " + length.ToString("F1") + " x " + width.ToString("F1") + " m, its feast " + (half * 2f).ToString("F1") + " x " + (beside * 2f).ToString("F1") + " m, " + j + " dishes and drinks round it");
        }

        // A point on a table's board, `a` along it and `c` across from its middle.
        static Vector3 OnBoard(Vector3 middle, bool longZ, float a, float c)
        {
            return middle + (longZ ? new Vector3(c, 0f, a) : new Vector3(a, 0f, c));
        }

        // The hall's catches, one to a stand (the first of each the game shows on a stand): perch, pike,
        // tuna, troll fish, giant herring, grouper, coral cod, northern salmon; a beast of the shore where the
        // game has no such fish.
        static readonly string[][] HallCatches =
        {
            new[] { "Fish1", "TrophyNeck" }, new[] { "Fish2", "TrophyNeck" }, new[] { "Fish3", "TrophyDeer" }, new[] { "Fish5", "TrophyBoar" },
            new[] { "Fish6", "TrophyNeck" }, new[] { "Fish7", "TrophyDeer" }, new[] { "Fish8", "TrophyBoar" }, new[] { "Fish10", "TrophyNeck" },
        };

        // The village folk's own shields: wood, banded, bronze, a tall wooden one.
        static readonly string[][] HallShields =
        {
            new[] { "ShieldWood" }, new[] { "ShieldBanded", "ShieldWood" }, new[] { "ShieldBronzeBuckler", "ShieldWood" }, new[] { "ShieldWoodTower", "ShieldBanded" },
        };

        // An item on the game's wall item stand on a wall's inner face (the wall out along n, one of the frame's
        // axes, `inner` from the middle; `along` it; its foot y up), the stand flat on the wall, its face into
        // the hall, the item shown on it as the game itself shows one (its "attach" part, or that part's
        // "attachobj", on the stand's attach point, at the part's own place, turn and size:
        // ItemStand.SetVisualItem). The first of the names the game can show so. Gives back the item shown.
        static GameObject SpOnStand(string[] names, Vector3 n, float inner, float y, float along)
        {
            Transform look = null;
            string shown = null;
            foreach (string name in names)
            {
                GameObject item = PrefabManager.Instance.GetPrefab(name);
                look = item != null ? item.transform.Find("attach") : null;
                if (look != null) { shown = name; break; }
            }
            if (look == null) { Plugin.Debug("Brim village: none of " + string.Join(", ", names) + " can be shown on a stand"); return null; }
            Vector3 t = Mathf.Abs(n.x) > 0.5f ? Vector3.forward : Vector3.right; // along the wall
            Transform attach = null;
            Vector3 at = n * (inner - 0.06f) + t * along + Vector3.up * y;
            GameObject stand = SpProp(new[] { "itemstand" }, at, 0f, false, -1, false, false, go =>
            {
                ItemStand s = go.GetComponent<ItemStand>();
                if (s != null) attach = s.m_attachOther;
            });
            if (stand == null) return null;
            // thin out from the wall
            if (Vector3.Dot(LocalBounds(stand).size, Abs(n)) > Vector3.Dot(LocalBounds(stand).size, t))
            {
                stand.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * stand.transform.localRotation;
                stand.transform.localPosition = Vector3.zero;
                Move(stand, LocalBounds(stand), at, false);
            }
            // its attach point into the hall, away from the wall
            if (attach != null && Vector3.Dot(root.transform.InverseTransformPoint(attach.position) - LocalBounds(stand).center, n) > 0f)
            {
                stand.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * stand.transform.localRotation;
                stand.transform.localPosition = Vector3.zero;
                Move(stand, LocalBounds(stand), at, false);
            }
            Bounds sb = LocalBounds(stand);
            stand.transform.localPosition += n * (inner - (Vector3.Dot(sb.center, n) + Vector3.Dot(sb.extents, Abs(n))));
            string wall = n.x > 0.5f ? "right" : n.x < -0.5f ? "left" : n.z > 0.5f ? "back" : "front";
            Transform holder = attach != null ? attach : stand.transform;
            Transform obj = look.Find("attachobj");
            GameObject thing = Object.Instantiate(obj != null ? obj.gameObject : look.gameObject, holder, false);
            thing.transform.localPosition = attach != null ? look.localPosition : new Vector3(0f, 0f, 0.15f);
            thing.transform.localRotation = look.localRotation;
            thing.transform.localScale = look.localScale;
            thing.SetActive(true);
            Strip(thing);
            SpQuiet(thing);
            thing.name = "IoV_OnStand:" + shown;
            built++;
            Plugin.Debug("Brim village: " + shown + " on a stand by the hall's " + wall + " wall, " + along.ToString("F1") + " m along");
            return thing;
        }

        static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

        // A banner of the game's hanging on a wall's inner face (out along n, `inner` from the middle; `along`
        // it), its top at `top`, flat against it.
        static GameObject SpBannerOn(string name, Vector3 n, float inner, float top, float along)
        {
            Vector3 t = Mathf.Abs(n.x) > 0.5f ? Vector3.forward : Vector3.right;
            Vector3 at = n * (inner - 0.1f) + t * along + Vector3.up * top;
            GameObject banner = SpProp(new[] { name, "piece_banner01" }, at, 0f, true);
            if (banner == null) return null;
            if (Vector3.Dot(LocalBounds(banner).size, Abs(n)) > Vector3.Dot(LocalBounds(banner).size, t))
            {
                banner.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * banner.transform.localRotation;
                banner.transform.localPosition = Vector3.zero;
                Move(banner, LocalBounds(banner), at, true);
            }
            Bounds b = LocalBounds(banner);
            banner.transform.localPosition += n * (inner - (Vector3.Dot(b.center, n) + Vector3.Dot(b.extents, Abs(n))));
            return banner;
        }

        // Meat roasting over a fire (a fire burning with nothing on it looked purposeless to the user): the
        // game's spit over it, along its length, the cooked meat's own look on each of its places (as Valhalla's).
        static GameObject Hearth(Vector3 at)
        {
            GameObject h = SpProp(new[] { "hearth" }, at, 0f, false, -1, true);
            SpAlongZ(h, at);
            return h;
        }

        // alongZ: the spit lies along its frame's z (in the hall, along the hall: across it, its legs stood in the
        // way between the fire and the benches).
        static void SpSpit(GameObject fire, int k, string[] items = null, bool alongZ = false)
        {
            if (fire == null) return;
            Bounds hb = LocalBounds(fire);
            Vector3 at = new Vector3(hb.center.x, hb.min.y, hb.center.z);
            bool hearth = fire.name.StartsWith("hearth");
            GameObject spit = SpProp(hearth ? new[] { "piece_cookingstation_iron", "piece_cookingstation" } : new[] { "piece_cookingstation" }, at, 0f, false, -1, false, false,
                go => { if (items == null) Meat(go, k); else MeatOn(go, items, k); });
            if (spit == null) return;
            Bounds sb = LocalBounds(spit);
            bool turn = alongZ ? sb.size.x > sb.size.z : (sb.size.x > sb.size.z) != (hb.size.x > hb.size.z);
            if (turn)
            {
                spit.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * spit.transform.localRotation;
                spit.transform.localPosition = Vector3.zero;
                Move(spit, LocalBounds(spit), at, false);
            }
        }

        // An item laid out as a thing to look at: not solid, no glint. Its effects stopped and unseen, nothing
        // of it turned off (1.10.89-102 turned off each effect's object: where that held the item's own look,
        // as the tankard's does, the whole tankard was gone, and the tables had no drink on them).
        static void SpQuiet(GameObject go)
        {
            if (go == null) return;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (ParticleSystem p in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.EmissionModule emission = p.emission;
                emission.enabled = false;
                ParticleSystemRenderer shown = p.GetComponent<ParticleSystemRenderer>();
                if (shown != null) shown.enabled = false;
            }
        }

        // ---- the square, the lighthouse, the yard, the field, the memorial, the commons, the shore ----

        // The altar's platform of stone in the middle (gives back its top), the square paved round it, benches
        // round it once rebuilt, and then the altar a great fire.
        static float Square()
        {
            parent = groupAll;
            SpPaint(0f, 0f, 8f, TerrainModifier.PaintType.Paved);
            float ground = GroundMin(0f, 0f, 3.5f);
            float top = ground + 0.3f;
            for (float x = -2f; x <= 2.01f; x += 2f)
                for (float z = -2f; z <= 2.01f; z += 2f)
                    SpProp(new[] { "stone_floor_2x2" }, new Vector3(x, top, z), 0f, true);
            // (no fires at the platform's corners: the user asked twice for them to go; 1.10.95-98 still left
            // them in every stage)
            for (int stage = 0; stage < 3; stage++)
            {
                parent = stageGroups[stage];
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
                    GameObject bench = SpSeat(new[] { "piece_bench01" }, q, -q.normalized);
                    // (the folk at home, T5 done: on three of them by the fire, two on one; one to talk to)
                    if (i == 0) { SoulSpot s = SoulOn(bench, 0f, false); if (s != null) s.talks = true; }
                    if (i == 3) SoulOn(bench, 0f, false, "Tankard");
                    if (i == 6) { SoulOn(bench, -0.55f, false); SoulOn(bench, 0.55f, false); }
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
                        Pole(new Vector3(at.x + x, ground, at.y + z), high * stone.y + (stage == 0 ? 0.2f : 0f), wear); // under the gallery's floor, not through it
                if (stage == 0) continue;
                // The gallery (1.10.98's was a 5 m square laid with 2 m floor two by two, so a metre of it was
                // bare, and its railing stood off the posts, past the floor's edge, one board short on each
                // side): 6 by 6 m of floor, three by three, a metre out over the tower all round, on joists;
                // posts every 2 m round its edge, a board between each two, a rail along their tops.
                float deck = ground + rows * stone.y, walk = deck + 0.25f;
                const float g = 3f;
                float under = walk;
                for (float x = -2f; x <= 2.01f; x += 2f)
                    for (float z = -2f; z <= 2.01f; z += 2f)
                    {
                        GameObject board = SpProp(new[] { "wood_floor" }, new Vector3(at.x + x, walk, at.y + z), 0f, true, wear);
                        if (board != null) under = Mathf.Min(under, LocalBounds(board).min.y);
                    }
                foreach (float t in new[] { -g + 0.1f, -1f, 1f, g - 0.1f })
                {
                    Beam(new Vector3(at.x - g, under, at.y + t), new Vector3(at.x + g, under, at.y + t), wear, true);
                    Beam(new Vector3(at.x + t, under, at.y - g), new Vector3(at.x + t, under, at.y + g), wear, true);
                }
                for (float x = -g; x <= g + 0.01f; x += 2f)
                    for (float z = -g; z <= g + 0.01f; z += 2f)
                        if (Mathf.Abs(x) > g - 0.01f || Mathf.Abs(z) > g - 0.01f) Pole(new Vector3(at.x + x, walk, at.y + z), 1.25f, wear);
                Vector3 plank = Size(Planks);
                for (float t = -g + 1f; t < g; t += 2f)
                {
                    Wall(Planks, plank, new Vector3(at.x + t, walk, at.y - g), 180f, wear, 2f, 1f);
                    Wall(Planks, plank, new Vector3(at.x + t, walk, at.y + g), 0f, wear, 2f, 1f);
                    Wall(Planks, plank, new Vector3(at.x - g, walk, at.y + t), -90f, wear, 2f, 1f);
                    Wall(Planks, plank, new Vector3(at.x + g, walk, at.y + t), 90f, wear, 2f, 1f);
                }
                float rail = walk + 1.2f;
                Beam(new Vector3(at.x - g, rail, at.y - g), new Vector3(at.x + g, rail, at.y - g), wear);
                Beam(new Vector3(at.x - g, rail, at.y + g), new Vector3(at.x + g, rail, at.y + g), wear);
                Beam(new Vector3(at.x - g, rail, at.y - g), new Vector3(at.x - g, rail, at.y + g), wear);
                Beam(new Vector3(at.x + g, rail, at.y - g), new Vector3(at.x + g, rail, at.y + g), wear);
                if (stage < 2) continue;
                // the roof on four posts and the beams over them, its ends closed (1.10.93's hung off its posts)
                float eave = deck + 3.25f;
                foreach (float x in new[] { -half, half })
                    foreach (float z in new[] { -half, half })
                        Pole(new Vector3(at.x + x, walk, at.y + z), 3f, 0);
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
                // its fire in an iron basket (1.10.98's bonfire burned up into its roof and over the railing)
                // its keeper at a corner of the gallery, looking out to sea (T5 done)
                SoulAt(new Vector3(at.x + Mathf.Sign(at.x) * 2.45f, walk, at.y + Mathf.Sign(at.y) * 2.45f), new Vector3(at.x, 0f, at.y).sqrMagnitude > 1f ? new Vector3(at.x, 0f, at.y) : Vector3.forward);
                GameObject fire = SpProp(new[] { "fire_pit_iron", "fire_pit" }, new Vector3(at.x, walk, at.y), 0f, false, -1, true);
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

        // The village's field: ploughed ground (always), weeds in the ruin, sown once rebuilt; open, no fence.
        static void Field(Vector2 at)
        {
            parent = groupAll;
            // tilled within its 8 by 6 m and no further (1.10.92-100 painted circles 1.1 m wide out to its
            // edge, and the soil ran a metre past it)
            for (float dx = -3.5f; dx <= 3.51f; dx += 0.5f)
                for (float dz = -2.5f; dz <= 2.51f; dz += 0.5f)
                    SpPaint(at.x + dx, at.y + dz, 0.5f, TerrainModifier.PaintType.Cultivate);
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
            // (no fence round it: the user, 1.10.101)
            // one at work in it, hoe in hand (T5 done)
            parent = stageGroups[2];
            SoulAt(new Vector3(at.x + 1.5f, Height(at.x + 1.5f, at.y - 0.5f), at.y - 0.5f), Vector3.left, "Hoe", 2.4f);
        }

        // A dark beam from one point to another, sloping as they do, stretched to fit, its middle on the line
        // between them (a fence's rail from one post's top to the next).
        static void Rail(Vector3 from, Vector3 to, int wear)
        {
            Vector3 d = to - from, mid = (from + to) * 0.5f;
            if (d.magnitude < 0.1f) return;
            GameObject go = SpProp(new[] { "darkwood_beam", "wood_beam" }, mid, 0f, false, wear);
            if (go == null) return;
            Bounds b = LocalBounds(go);
            bool alongX = b.size.x >= b.size.z;
            float own = Mathf.Max(0.1f, alongX ? b.size.x : b.size.z);
            go.transform.localScale = alongX ? new Vector3(d.magnitude / own, 1f, 1f) : new Vector3(1f, 1f, d.magnitude / own);
            // its long side (its own x or z) turned onto the line, kept upright
            go.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up) * (alongX ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity);
            go.transform.localPosition = Vector3.zero;
            Bounds nb = LocalBounds(go);
            Move(go, nb, new Vector3(mid.x, mid.y - nb.size.y * 0.5f, mid.z), false);
        }

        // A dark beam from one point to another (level), stretched to fit: its middle at their height, or its
        // top (under: a joist under a floor).
        static void Beam(Vector3 from, Vector3 to, int wear, bool under = false)
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
            Move(go, nb, new Vector3(mid.x, mid.y - nb.size.y * (under ? 1f : 0.5f), mid.z), false);
        }

        // The memorial of the drowned: a ring of standing stones round a cold blue fire, lit once the village is
        // cleared (the souls Rán held), looking out to sea.
        static void Memorial(Vector2 at, Vector2 from)
        {
            float ground = Height(at.x, at.y);
            parent = groupAll;
            // the stones stand on the paving's edge, all round (1.10.92-100 painted it 4.5 m out and set them at
            // 3.4 m: they stood in the middle of it, the user said)
            // (1.10.101-102 painted exactly as far as the ring: the game's paint falls on its own 1 m grid, and one
            // stone came out on the grass; now the paving runs a little past the ring, and is laid under each
            // stone too)
            const float ring = 3.9f;
            SpPaint(at.x, at.y, ring + 0.4f, TerrainModifier.PaintType.Paved);
            // a gap between two of them where its path comes in
            float gap = Mathf.Atan2(from.x - at.x, from.y - at.y) * Mathf.Rad2Deg;
            for (int i = 0; i < 7; i++)
            {
                float a = gap + (i + 0.5f) * (360f / 7f);
                Vector3 q = new Vector3(at.x, 0f, at.y) + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, ring);
                q.y = Height(q.x, q.z) - 0.2f;
                SpPaint(q.x, q.z, 1.1f, TerrainModifier.PaintType.Paved);
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
            // one standing quiet by its fire, come in by the path (T5 done)
            parent = stageGroups[2];
            Vector2 by = at + (from - at).normalized * 2.3f;
            SoulAt(new Vector3(by.x, Height(by.x, by.y), by.y), new Vector3(at.x - by.x, 0f, at.y - by.y));
        }

        // (No well, cart or evening fire about the village: the user had the well taken away in 1.10.99, the
        // others in 1.10.98. The square's great fire is the village's.)

        // ---- what's taken, so the meadow leaves it be ----

        struct Footprint { internal Vector2 At; internal float Yaw; internal Vector2 Half; }
        static readonly List<Footprint> occupied = new List<Footprint>();
        static readonly List<Vector3> takenCircles = new List<Vector3>(); // x, z, radius
        static readonly List<Vector2> pathPoints = new List<Vector2>();

        static void Taken(Vector2 at, float radius) { takenCircles.Add(new Vector3(at.x, at.y, radius)); }

        // The first of these spots with room for something of this size (clear of buildings, paths and what's
        // been put already); the last if none has.
        static Vector2 PickFree(float radius, params Vector2[] spots)
        {
            foreach (Vector2 s in spots)
                if (Free(s.x, s.y, radius - 0.5f)) return s;
            return spots[spots.Length - 1];
        }

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
                    // which kind grows here: in drifts (noise), stretched over the whole range (raw noise sits mostly
                    // in its middle, and 1.10.94 laid wild flax nearly everywhere: the user kept it, but asked for
                    // the rest mixed in)
                    float pick = Mathf.Clamp01((Mathf.PerlinNoise(x * 0.07f + 300f, z * 0.07f + 77f) - 0.25f) / 0.5f);
                    float jitter = Mathf.PerlinNoise(x * 0.9f + 11f, z * 0.9f + 3f);
                    if (jitter < 0.3f) continue; // the gaps between drifts
                    string name;
                    float sink = 0f;
                    if (pick < 0.2f) { name = bushes[(int)(jitter * 10f) % bushes.Length]; sink = 0.1f; }
                    else if (pick < 0.28f) { name = jitter > 0.55f ? "Beech_small1" : "Beech_small2"; sink = 0.05f; }
                    else if (pick < 0.36f) { name = jitter > 0.6f ? "RaspberryBush" : "BlueberryBush"; sink = 0.05f; }
                    else if (pick < 0.46f) name = "Pickable_Flax_Wild";
                    else if (pick < 0.55f) name = "Pickable_Thistle";
                    else if (pick < 0.63f) { name = jitter > 0.6f ? "Rock_4" : "Rock_7_meadows"; sink = 0.15f; }
                    else if (pick < 0.72f) name = "Pickable_Mushroom";
                    else name = "Pickable_Dandelion";
                    if (Find(new[] { name }) == null) name = "Pickable_Dandelion";
                    SpPlant(name, x, Height(x, z), z, Mathf.Repeat(x * 37f + z * 11f, 360f), sink);
                    placed++;
                    if (name == "Pickable_Dandelion")
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

        // The pier, out from the beach into deep water at the side away from the cliff: a walk of planks on
        // posts, long enough that the ship moored along its far end lies in deep water from stem to stern
        // (1.10.98's was measured to where the sea first got deep, and its ship, 20 m long, lay half on the
        // sand), the ship still (no sail, no wake, no sound of sailing). Near the beach, a landing either side
        // of the walk, level, with the fishers' barrels and chest and the catch drying on it (1.10.98 stood
        // them on the slope above the beach, a chest in the air). The islet's own rocks cleared off its way.
        // A few planks and stumps and a wreck in the ruin, the walk weathered once cleared. Gives back where its
        // path comes down to it.
        static Vector2 Pier(float deg, Transform top)
        {
            Vector2 u = Polar(deg, 1f), side = new Vector2(-u.y, u.x);
            float yawAlong = Mathf.Atan2(u.x, u.y) * Mathf.Rad2Deg;
            const float deck = 1.1f;
            // from where the ground comes down to the deck's height
            float start = PlateauAt(deg);
            for (float r = PlateauAt(deg); r < PlateauAt(deg) + 80f; r += 0.5f)
            {
                float h = Height(u.x * r, u.y * r);
                if (h > deck - 0.2f) start = r;
                if (h < -2.5f) break;
            }
            start += 1f;
            Vector2 foot = u * (start - 2f);

            // the ship, measured: how long, how broad, which way it's long
            Transform keep = parent;
            parent = groupAll;
            GameObject probe = SpProp(ShipNames, Vector3.zero, 0f, false);
            parent = keep;
            float shipLong = 20f, shipBeam = 5f;
            bool longOnX = false;
            if (probe != null)
            {
                Bounds sb = LocalBounds(probe);
                longOnX = sb.size.x > sb.size.z;
                shipLong = Mathf.Max(sb.size.x, sb.size.z);
                shipBeam = Mathf.Min(sb.size.x, sb.size.z);
                Object.DestroyImmediate(probe);
                built--;
            }
            float off = 1.1f + shipBeam * 0.5f + 0.4f; // its side a little off the walk's edge
            float near = start + 4f;
            while (near < start + 150f && !Deep(u, side, near, shipLong, off, shipBeam)) near += 0.5f;
            float shipAt = near + shipLong * 0.5f;
            float end = Mathf.Max(start + 10f, near + shipLong - 3f);

            // the landing: the first two lengths of the walk with the beach under them low enough all across
            float land = -1f;
            for (float r0 = start + 1f; r0 < start + 10f && land < 0f; r0 += 2f)
            {
                bool clear = true;
                foreach (float rr in new[] { r0 - 1f, r0, r0 + 1f, r0 + 2f, r0 + 3f })
                    foreach (float w in new[] { -3f, -2f, -1f, 1f, 2f, 3f })
                    {
                        Vector2 q = u * rr + side * w;
                        if (Height(q.x, q.y) > deck - 0.3f) clear = false;
                    }
                if (clear) land = r0;
            }
            Plugin.Debug("Brim village: the pier from " + start.ToString("F1") + " to " + end.ToString("F1") + " m out, at " + deg.ToString("F0") + " degrees; the ship " + shipLong.ToString("F1") + " x " + shipBeam.ToString("F1") + " m, from " + near.ToString("F1") + " m out (deep enough: " + Deep(u, side, near, shipLong, off, shipBeam) + "); the landing at " + land.ToString("F1") + " m");

            // the islet's rocks off the walk, the landing and the ship's water
            for (int c = top.childCount - 1; c >= 0; c--)
            {
                Transform t = top.GetChild(c);
                if (t.name.IndexOf("rock", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                Bounds rb = LocalBounds(t.gameObject);
                Vector2 p = new Vector2(rb.center.x, rb.center.z);
                float rad = Mathf.Max(rb.extents.x, rb.extents.z);
                float along = Vector2.Dot(p, u), across = Vector2.Dot(p, side);
                if (along < start - 8f - rad || along > end + 3f + rad || across < -4f - rad || across > off + shipBeam * 0.5f + 1f + rad) continue;
                Plugin.Debug("Brim village: " + t.name + " cleared off the pier's way");
                Object.DestroyImmediate(t.gameObject);
            }

            for (int stage = 0; stage < 3; stage++)
            {
                parent = stageGroups[stage];
                int wear = stage == 0 ? 2 : stage == 1 ? 1 : 0;
                int i = 0;
                for (float r = start + 1f; r < end; r += 2f, i++)
                {
                    bool plank = stage > 0 || (i < 3 && villageRandom.NextDouble() < 0.6);
                    bool landing = stage > 0 && land > 0f && r > land - 0.5f && r < land + 2.5f; // its posts under the landing's floor, not up through it
                    Vector2 p = u * r;
                    if (plank) SpProp(new[] { "wood_floor" }, new Vector3(p.x, deck, p.y), yawAlong, true, wear);
                    if (i % 2 == 0)
                        foreach (float s in new[] { -1f, 1f })
                        {
                            Vector2 q = p + side * s * 1.05f;
                            float ground = Height(q.x, q.y);
                            float high = stage == 0 && !plank ? Mathf.Min(deck, ground + 1.2f) : landing ? deck - 0.05f : deck + 0.6f;
                            Pole(new Vector3(q.x, ground - 0.3f, q.y), high - ground + 0.3f, wear);
                        }
                }
                if (stage == 0)
                {
                    PlaceWreck(deg + 40f);
                    Vector2 b0 = u * (start + 2f) + side * 4.5f;
                    SpProp(new[] { "barrell" }, new Vector3(b0.x, Height(b0.x, b0.y), b0.y), 70f, false);
                    continue;
                }
                // the landing: a floor either side of the walk, posts at its outer corners
                if (land > 0f)
                {
                    foreach (float rr in new[] { land, land + 2f })
                        foreach (float s in new[] { -1f, 1f })
                        {
                            Vector2 p = u * rr + side * s * 2f;
                            SpProp(new[] { "wood_floor" }, new Vector3(p.x, deck, p.y), yawAlong, true, wear);
                        }
                    foreach (float rr in new[] { land - 1f, land + 3f })
                        foreach (float s in new[] { -1f, 1f })
                        {
                            Vector2 q = u * rr + side * s * 2.9f;
                            float ground = Height(q.x, q.y);
                            Pole(new Vector3(q.x, ground - 0.3f, q.y), deck + 0.6f - ground + 0.3f, wear);
                        }
                    // the barrels and the chest on the ship's side, the catch drying across the walk from them
                    Vector2 b1 = u * (land - 0.3f) + side * 2.2f, b2 = u * (land + 0.8f) + side * 2.3f, b3 = u * (land + 2.2f) + side * 2.2f;
                    SpProp(new[] { "barrell" }, new Vector3(b1.x, deck, b1.y), 10f, false);
                    SpProp(new[] { "barrell" }, new Vector3(b2.x, deck, b2.y), 50f, false);
                    SpProp(new[] { "piece_chest_wood" }, new Vector3(b3.x, deck, b3.y), yawAlong + 90f, false);
                    // (no fish on a spit across the walk: the user had it taken away, 1.10.101)
                }
                if (stage < 2) continue;
                // its end: two torches, and the ship along it
                Vector2 tip = u * (end - 1f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector2 q = tip + side * s * 0.8f;
                    SpProp(new[] { "piece_groundtorch_wood" }, new Vector3(q.x, deck, q.y), 0f, false, -1, true);
                }
                Vector2 ship = u * shipAt + side * off;
                GameObject boat = SpProp(ShipNames, new Vector3(ship.x, -1.3f, ship.y), yawAlong + (longOnX ? 90f : 0f), false, -1, false, false, Becalmed);
                if (boat != null)
                {
                    // its hull (its solid part) sunk 0.9 m into the water (1.10.98-102 set its lowest mesh at
                    // 1.3 m down: that was its oars, hanging below the hull, and the hull rode in the air)
                    float mesh = LocalBounds(boat).min.y, hull = SolidBounds(boat).min.y;
                    boat.transform.localPosition += Vector3.up * (-0.9f - hull);
                    Plugin.Debug("Brim village: the ship's lowest mesh at " + mesh.ToString("F2") + ", its hull at " + hull.ToString("F2") + "; the hull put at -0.90");
                }
                // The folk at home (T5 done): one fishing off the pier's end, on the side away from the ship (to
                // talk to), another off the landing; and Sævar on the landing, looking at his ship.
                Vector2 rod = u * (end - 2.6f) - side * 0.6f;
                SoulAt(new Vector3(rod.x, deck, rod.y), new Vector3(-side.x, 0f, -side.y), "FishingRod").talks = true;
                Vector2 look = u * shipAt + side * off;
                if (land > 0f)
                {
                    Vector2 rod2 = u * (land + 2.2f) - side * 2.4f;
                    SoulAt(new Vector3(rod2.x, deck, rod2.y), new Vector3(-side.x, 0f, -side.y), "FishingRod");
                    Vector2 sv = u * land - side * 1.4f;
                    SoulSpot saevar = SoulAt(new Vector3(sv.x, deck, sv.y), new Vector3(look.x - sv.x, 0f, look.y - sv.y));
                    saevar.who = SoulSpot.Saevar;
                    saevar.talks = true;
                }
                else
                {
                    Vector2 sv = u * (start + 2f) - side * 0.6f;
                    SoulSpot saevar = SoulAt(new Vector3(sv.x, deck, sv.y), new Vector3(look.x - sv.x, 0f, look.y - sv.y));
                    saevar.who = SoulSpot.Saevar;
                    saevar.talks = true;
                }
            }
            Taken(foot, 4f);
            for (float r = start - 3f; r < end; r += 3f) Taken(u * r, 3.2f);
            if (land > 0f) Taken(u * (land + 1f), 4.5f);
            // no grass under the walk and the landing (1.10.100's grew up through their planks)
            for (float r = start - 1f; r < end; r += 1.5f)
            {
                Vector2 q = u * r;
                if (Height(q.x, q.y) < -0.5f) break;
                SpPaint(q.x, q.y, land > 0f && r > land - 1.5f && r < land + 3.5f ? 3.4f : 1.6f, TerrainModifier.PaintType.ClearVegetation);
            }
            return foot;
        }

        static readonly string[] ShipNames = { "VikingShip", "Karve" };

        // Whether the sea is deep enough under all of a ship whose near end is `near` out along u, `off` to the
        // side of the walk, this long and broad (the ground 2.2 m under the water at least, along and across it).
        static bool Deep(Vector2 u, Vector2 side, float near, float length, float off, float beam)
        {
            for (float t = 0f; t <= length + 0.01f; t += 1f)
                foreach (float w in new[] { off - beam * 0.5f, off, off + beam * 0.5f })
                {
                    Vector2 q = u * (near + t) + side * w;
                    if (Height(q.x, q.y) > -2.2f) return false;
                }
            return true;
        }


        // A ship as it lies moored: its sail furled away, no wake or spray, no sound.
        static void Becalmed(GameObject ship)
        {
            // its sail taken down: the game's own sail object, its cloth, and every part named or painted as a
            // sail (1.10.96 turned off the cloth, 1.10.98 the sail object too, and the striped sail still stood
            // full: it's a part of its own)
            Ship s = ship.GetComponent<Ship>();
            if (s != null && s.m_sailObject != null) s.m_sailObject.SetActive(false);
            foreach (Cloth sail in ship.GetComponentsInChildren<Cloth>(true)) sail.gameObject.SetActive(false);
            foreach (Renderer r in ship.GetComponentsInChildren<Renderer>(true))
            {
                bool sail = r.name.IndexOf("sail", System.StringComparison.OrdinalIgnoreCase) >= 0;
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.name.IndexOf("sail", System.StringComparison.OrdinalIgnoreCase) >= 0) sail = true;
                if (!sail) continue;
                Plugin.Debug("Brim village: the ship's " + r.name + " taken down");
                r.gameObject.SetActive(false);
            }
            foreach (ParticleSystem p in ship.GetComponentsInChildren<ParticleSystem>(true)) p.gameObject.SetActive(false);
            foreach (AudioSource a in ship.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
        }

        // Things on a spit's places, each the item's own look for cooking (as Valhalla's meat): the first of
        // these the game has with such a look, in turn.
        static void MeatOn(GameObject station, string[] items, int k)
        {
            CookingStation cooking = station.GetComponent<CookingStation>();
            if (cooking == null || cooking.m_slots == null) return;
            for (int i = 0; i < cooking.m_slots.Length; i++)
            {
                Transform slot = cooking.m_slots[i];
                if (slot == null) continue;
                for (int n = 0; n < items.Length; n++)
                {
                    GameObject item = PrefabManager.Instance.GetPrefab(items[(k + i + n) % items.Length]);
                    if (item == null) continue;
                    Transform look = item.transform.Find("attach_cook");
                    if (look == null) look = item.transform.Find("attach");
                    if (look == null) continue;
                    GameObject meat = Object.Instantiate(look.gameObject, slot, false);
                    meat.transform.localPosition = Vector3.zero;
                    meat.transform.localRotation = Quaternion.identity;
                    meat.SetActive(true);
                    break;
                }
            }
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
                if (Height(q.x, q.y) < 1.5f || !Free(q.x, q.y, 2.5f)) continue; // off the paths and the pier
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
                if (Height(q.x, q.y) < 1f || !Free(q.x, q.y, 2f)) continue; // off the paths and the pier (1.10.98's walk ran over a boulder)
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

        // A lane through the points (the first, if it's the square's middle, taken from the square's edge): the
        // hall's paved and wide with no fence; every other alike (the user: symmetric, the same path and edge),
        // worn earth with a fence of posts and rails either side (a few broken posts in the ruin, weathered once
        // cleared, new once rebuilt). The fence keeps off the buildings and the places at the ends (its posts are
        // left out where they'd stand in them, and short of the lane's end, so the way in stays open); a light
        // along it once rebuilt.
        static void Lane(int k, bool stone, params Vector2[] points)
        {
            List<Vector2> p = new List<Vector2>(points);
            if (p.Count < 2) return;
            if (p[0] == Vector2.zero)
            {
                Vector2 d0 = p[1] - p[0];
                if (d0.magnitude < 9.5f) return;
                p[0] = d0.normalized * 8.5f;
            }
            float width = stone ? 1.6f : 1.05f;
            float total = 0f;
            for (int i = 1; i < p.Count; i++) total += (p[i] - p[i - 1]).magnitude;
            // the earth (or stone), laid close
            for (int i = 1; i < p.Count; i++)
            {
                Vector2 a = p[i - 1], b = p[i];
                float span = (b - a).magnitude;
                for (float t = 0f; t <= 1.001f; t += 0.7f / Mathf.Max(1f, span))
                {
                    Vector2 q = Vector2.Lerp(a, b, t);
                    SpPaint(q.x, q.y, width, stone ? TerrainModifier.PaintType.Paved : TerrainModifier.PaintType.Dirt);
                    pathPoints.Add(q);
                }
            }
            lanes.Add(new LaneDef { K = k, Stone = stone, Points = p, Width = width, Total = total });
        }

        struct LaneDef { internal int K; internal bool Stone; internal List<Vector2> Points; internal float Width, Total; }
        static readonly List<LaneDef> lanes = new List<LaneDef>();

        // Each lane's fences and light, once every lane is laid (a post never stands in another lane).
        static void DressLanes()
        {
            foreach (LaneDef l in lanes) DressLane(l.K, l.Stone, l.Points, l.Width, l.Total);
            lanes.Clear();
        }

        static void DressLane(int k, bool stone, List<Vector2> p, float width, float total)
        {
            Transform keep = parent;
            if (!stone)
                for (int stage = 0; stage < 3; stage++)
                {
                    parent = stageGroups[stage];
                    int wear = stage == 0 ? 2 : stage == 1 ? 1 : 0;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float walked = 0f;
                        for (int i = 1; i < p.Count; i++)
                        {
                            Vector2 a = p[i - 1], b = p[i];
                            float span = (b - a).magnitude;
                            Vector2 dir = (b - a) / Mathf.Max(0.01f, span), across = new Vector2(-dir.y, dir.x);
                            Vector2 prev = Vector2.zero;
                            bool have = false;
                            for (float s = (i == 1 ? 1.5f : 1.2f); s <= span - 1.2f; s += 2.5f)
                            {
                                Vector2 post = a + dir * s + across * side * (width + 0.6f);
                                bool clear = walked + s < total - 2.5f && PostFree(post.x, post.y) && OffLanes(post.x, post.y, width + 0.35f);
                                bool stands = clear && (stage > 0 || villageRandom.NextDouble() < 0.35);
                                if (stands) Pole(new Vector3(post.x, Height(post.x, post.y) - 0.1f, post.y), 1.2f, wear);
                                // the rail from post to post, as high over the ground at each (1.10.98-100 laid it
                                // level at the higher one's height: on a slope it hung in the air past the lower post)
                                if (stands && have && stage > 0)
                                    Rail(new Vector3(prev.x, Height(prev.x, prev.y) + 0.85f, prev.y), new Vector3(post.x, Height(post.x, post.y) + 0.85f, post.y), wear);
                                prev = post;
                                have = stands;
                            }
                            walked += span;
                        }
                    }
                }
            // a light by its longest stretch once rebuilt
            int longest = 1;
            for (int i = 2; i < p.Count; i++) if ((p[i] - p[i - 1]).sqrMagnitude > (p[longest] - p[longest - 1]).sqrMagnitude) longest = i;
            Vector2 la = p[longest - 1], lb = p[longest];
            if ((lb - la).magnitude >= 6f)
            {
                parent = stageGroups[2];
                Vector2 dir = (lb - la).normalized;
                Vector2 m = (la + lb) * 0.5f + new Vector2(-dir.y, dir.x) * (width + (stone ? 1f : 1.3f));
                if (PostFree(m.x, m.y)) SpProp(PathLights[k % PathLights.Length], new Vector3(m.x, Height(m.x, m.y), m.y), 0f, false, -1, true);
            }
            parent = keep;
        }

        // No lane's middle within this of the point.
        static bool OffLanes(float x, float z, float within)
        {
            foreach (Vector2 q in pathPoints)
                if ((x - q.x) * (x - q.x) + (z - q.y) * (z - q.y) < within * within) return false;
            return true;
        }

        // Where a fence post may stand: not in a building (with its margin) or a place.
        static bool PostFree(float x, float z)
        {
            foreach (Vector3 c in takenCircles)
                if ((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y) < c.z * c.z) return false;
            foreach (Footprint f in occupied)
            {
                Vector3 local = Quaternion.Euler(0f, -f.Yaw, 0f) * new Vector3(x - f.At.x, 0f, z - f.At.y);
                if (Mathf.Abs(local.x) < f.Half.x && Mathf.Abs(local.z) < f.Half.y) return false;
            }
            return true;
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

        // ---- the Brim Folk at home (the tale's end, T5): where each spirit is (SoulSpot; BrimSouls makes them) ----

        static int soulCount;

        static SoulSpot SoulMark(Transform under, int kind)
        {
            GameObject go = new GameObject("IoV_Soul");
            go.transform.SetParent(under, false);
            SoulSpot s = go.AddComponent<SoulSpot>();
            s.kind = kind;
            s.index = soulCount++;
            return s;
        }

        // On a seat (a bench, the high seat): along it; at a feast, a bite now and then.
        static SoulSpot SoulOn(GameObject seat, float along, bool feast, string holds = "")
        {
            if (seat == null) return null;
            SoulSpot s = SoulMark(seat.transform, feast ? SoulSpot.Feast : SoulSpot.Sit);
            s.along = along;
            s.holds = holds;
            return s;
        }

        // Standing at a place of the current parent, looking a way; at work, a swing every so often.
        static SoulSpot SoulAt(Vector3 at, Vector3 look, string holds = "", float every = 0f)
        {
            SoulSpot s = SoulMark(parent, every > 0f ? SoulSpot.Work : SoulSpot.Stand);
            s.transform.localPosition = at;
            look.y = 0f;
            s.transform.localRotation = Quaternion.LookRotation(look.sqrMagnitude > 0.001f ? look.normalized : Vector3.forward);
            s.holds = holds;
            s.every = every;
            return s;
        }

        // Walking a lane there and back (or round), slowly.
        static SoulSpot SoulWalk(float speed, float rest, bool loop, params Vector2[] route)
        {
            SoulSpot s = SoulMark(stageGroups[2], SoulSpot.Walk);
            s.route = new Vector3[route.Length];
            for (int i = 0; i < route.Length; i++) s.route[i] = new Vector3(route[i].x, Height(route[i].x, route[i].y), route[i].y);
            s.speed = speed;
            s.rest = rest;
            s.loop = loop;
            return s;
        }

        // A steady 0..1 for an index (not the village's own random: what it draws lays out the rest).
        static float SoulPick(int i, int salt)
        {
            uint h = (uint)(i * 2654435761L + salt * 40503L);
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 1023) / 1023f;
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
