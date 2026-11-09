using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Valhalla's layout (work in progress, corrected in the game together). North is +z.
    // - the way from where Odin's light sets you down, lined with braziers and the shields of the fallen, up to
    //   the long hall (open pillars, a shingle roof edged with shields, the feast, a long fire, and at its far
    //   end the throne on a dais, turned away);
    // - behind the hall, at the north rim, the ravens' lookout over the clouds;
    // - east, the duel ring of stone columns, and by it the einherjar's yard;
    // - west, under golden birches, the Brim Folk's own feast; past it a spring falling off the rim;
    // - south-east, the Brim Folk's longship, its bow out over the clouds;
    // - south-west, a path of memorial stones from where you arrive to the feast.
    //
    // Everything is a still copy of the game's own pieces (their scripts taken out, so nothing breaks or
    // burns down); where it's lit, its flames are lit first. Each piece is put by its own measured size, so
    // they meet edge to edge whatever the game's pieces measure.
    static partial class IsletBuilder
    {
        internal static readonly Vector3 ValhallaArrival = new Vector3(0f, 0f, -64f);
        const float HallHalfWidth = 8f, HallStart = 4f, HallEnd = 40f, PillarHeight = 6f;
        static readonly Vector2 DuelRing = new Vector2(40f, 6f);
        const float DuelRadius = 10f;
        static readonly Vector2 Feast = new Vector2(-42f, 8f);
        static readonly Vector2 Yard = new Vector2(58f, -14f);
        static float LookoutZ { get { return Mathf.Min(60f, RockEdge(Mathf.PI * 0.5f) - 13f); } }

        // 1 where the ground is level (the hall, the way, the ring, the yard, the feast, the lookout), fading
        // to 0 over 6 m around them.
        internal static float ValhallaPads(float x, float z)
        {
            float pad = 0f;
            pad = Mathf.Max(pad, RectPad(x, z, 0f, (HallStart + HallEnd) * 0.5f, HallHalfWidth + 3f, (HallEnd - HallStart) * 0.5f + 3f));
            pad = Mathf.Max(pad, RectPad(x, z, 0f, (ValhallaArrival.z + HallStart) * 0.5f - 2f, 5f, (HallStart - ValhallaArrival.z) * 0.5f + 5f));
            pad = Mathf.Max(pad, CirclePad(x, z, DuelRing, DuelRadius + 3f));
            pad = Mathf.Max(pad, CirclePad(x, z, Feast, 11f));
            pad = Mathf.Max(pad, CirclePad(x, z, Yard, 8f));
            pad = Mathf.Max(pad, CirclePad(x, z, new Vector2(0f, LookoutZ), 6.5f));
            return pad;
        }

        static float CirclePad(float x, float z, Vector2 centre, float radius)
        {
            float d = Mathf.Max(0f, new Vector2(x - centre.x, z - centre.y).magnitude - radius);
            return 1f - Mathf.SmoothStep(0f, 1f, d / 6f);
        }

        static float RectPad(float x, float z, float cx, float cz, float hx, float hz)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(x - cx) - hx), dz = Mathf.Max(0f, Mathf.Abs(z - cz) - hz);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dz * dz) / 6f);
        }

        static readonly string[] ShieldNames = { "ShieldBanded", "ShieldWood", "ShieldIronSquare", "ShieldSilver", "ShieldGold", "ShieldSerpentscale", "ShieldBlackmetal", "ShieldFlametal" };

        static void BuildValhallaLayout()
        {
            int before = built;
            Hall();
            Way();
            Ring();
            Lookout();
            BrimFeast();
            Spring();
            EinherjarYard();
            Longship();
            MemorialPath();
            Meadow();
            Plugin.Debug("Valhalla: layout of " + (built - before) + " pieces, " + missing + " not found");
        }

        // The meadow's height there (0 on the level places).
        static float G(float x, float z) { return RockMeadow(x, z); }

        static bool OnRock(float x, float z, float margin)
        {
            return new Vector2(x, z).magnitude < RockEdge(Mathf.Atan2(z, x)) - margin;
        }

        // ---- the hall ----

        static void Hall()
        {
            // A floor of black marble, 4 m tiles.
            float floorTop = 0f;
            for (float x = -HallHalfWidth + 2f; x < HallHalfWidth; x += 4f)
                for (float z = HallStart + 2f; z < HallEnd; z += 4f)
                {
                    GameObject tile = SkyProp("blackmarble_floor_large", new Vector3(x, 0f, z), Vector3.zero);
                    if (tile != null) floorTop = Mathf.Max(floorTop, SkyBounds(tile).max.y);
                }
            // Pillars along both sides, every 4 m.
            for (float z = HallStart; z <= HallEnd + 0.01f; z += 4f)
                foreach (float side in new[] { -1f, 1f })
                    Pillar(new Vector3(side * HallHalfWidth, floorTop, z));
            float wallTop = floorTop + PillarHeight;
            // Beams along the tops of the pillars.
            for (float z = HallStart + 2f; z < HallEnd; z += 4f)
                foreach (float side in new[] { -1f, 1f })
                    AlongZ(SkyProp("darkwood_beam4x4", new Vector3(side * HallHalfWidth, wallTop, z), Vector3.zero), new Vector3(side * HallHalfWidth, wallTop, z));
            Roof(wallTop + 0.3f);
            // Shields hung all along the eaves, facing out.
            int n = 0;
            for (float z = HallStart + 1f; z < HallEnd; z += 2f)
                foreach (float side in new[] { -1f, 1f })
                {
                    string shield = ShieldNames[(n++ * 5 + 3) % ShieldNames.Length];
                    SkyProp(shield, new Vector3(side * (HallHalfWidth + 0.45f), wallTop - 0.2f, z), new Vector3(0f, side * 90f, 0f), true);
                }
            // The feast: two long tables with benches either side, and down the middle a long fire of three
            // hearths between them.
            foreach (float side in new[] { -1f, 1f })
            {
                for (float z = HallStart + 8f; z <= HallEnd - 10f; z += 6f)
                {
                    Vector3 table = new Vector3(side * 4.4f, floorTop, z);
                    AlongZ(SkyProp("piece_table_oak", table, Vector3.zero), table);
                    foreach (float off in new[] { -1.4f, 1.4f })
                    {
                        Vector3 bench = new Vector3(side * 4.4f + off, floorTop, z);
                        AlongZ(SkyProp("piece_bench01", bench, Vector3.zero), bench);
                    }
                }
            }
            foreach (float z in new[] { HallStart + 9f, HallStart + 17f, HallStart + 25f })
                SkyProp("hearth", new Vector3(0f, floorTop, z), Vector3.zero);
            // Braziers along the walls, inside.
            foreach (float x in new[] { -HallHalfWidth + 1f, HallHalfWidth - 1f })
                for (float z = HallStart + 2f; z <= HallEnd - 6f; z += 8f)
                    SkyProp("piece_brazierfloor01", new Vector3(x, floorTop, z), Vector3.zero);
            // Steps up to the floor at the door.
            Steps(floorTop);
            // The throne at the far end, on a dais, turned away from the hall.
            float dais = Block(new Vector3(0f, floorTop, HallEnd - 3f), 6f, 0.6f, 4f);
            SkyProp("piece_throne01", new Vector3(0f, dais, HallEnd - 3f), new Vector3(0f, 180f, 0f));
        }

        // Steps of black marble from the ground up to the hall's floor, as wide as the way and more.
        static void Steps(float top)
        {
            if (top < 0.15f) return;
            int count = Mathf.Max(2, Mathf.CeilToInt(top / 0.3f));
            const float depth = 0.6f;
            for (int i = 1; i <= count; i++)
            {
                float height = top * i / (count + 1f);
                float z = HallStart - depth * (count - i) - depth * 0.5f;
                Block(new Vector3(0f, 0f, z), 7f, height, depth);
            }
        }

        // A solid block of black marble, sized to fit (any of the game's blocks, stretched): its foot at
        // at.y, its middle at (at.x, at.z). Gives back its top.
        static float Block(Vector3 at, float width, float height, float depth)
        {
            GameObject go = SkyProp("blackmarble_2x2x1", at, Vector3.zero);
            if (go == null) return at.y + height;
            Bounds b = SkyBounds(go);
            go.transform.localScale = new Vector3(width / Mathf.Max(0.1f, b.size.x), height / Mathf.Max(0.05f, b.size.y), depth / Mathf.Max(0.1f, b.size.z));
            Put(go, at, false);
            return SkyBounds(go).max.y;
        }

        static void Pillar(Vector3 foot)
        {
            GameObject pole = SkyProp("darkwood_pole4", foot, Vector3.zero);
            if (pole == null) return;
            // as tall as the hall wants it, and thicker with it
            Bounds b = SkyBounds(pole);
            float scale = PillarHeight / Mathf.Max(0.1f, b.size.y);
            pole.transform.localScale *= scale;
            Put(pole, foot, false);
        }

        // The roof: shingles at 45 degrees from past the pillars on both sides up to the ridge cap, along the
        // whole hall. (Which way a shingle piece slopes is the game's: the first try had them all the wrong
        // way round, so they're turned; RoofFlip turns them back.)
        const bool RoofFlip = true;

        static void Roof(float eave)
        {
            GameObject probe = SkyProp("darkwood_roof_45", Vector3.zero, new Vector3(0f, 90f, 0f));
            if (probe == null) return;
            Bounds pb = SkyBounds(probe);
            Object.DestroyImmediate(probe);
            built--;
            GameObject capProbe = SkyProp("darkwood_roof_top_45", Vector3.zero, new Vector3(0f, 90f, 0f));
            float cap = 0f;
            if (capProbe != null) { cap = SkyBounds(capProbe).size.x; Object.DestroyImmediate(capProbe); built--; }
            // after the turn: along the hall (z) its 2 m, across (x) and up its slope
            float across = pb.size.x, up = pb.size.y, along = pb.size.z;
            if (across < 0.2f || up < 0.2f || along < 0.2f) return;
            // whole shingles from the eave in, the cap covering what's left in the middle
            int rows = Mathf.Max(1, Mathf.RoundToInt(HallHalfWidth / across));
            float half = rows * across + cap * 0.5f;
            for (int k = 0; k < rows; k++)
                for (float z = HallStart - 1f + along * 0.5f; z < HallEnd + 1f; z += along)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float x = side * (half - across * (k + 0.5f));
                        float yaw = (side < 0f ? 90f : -90f) + (RoofFlip ? 180f : 0f);
                        SkyProp("darkwood_roof_45", new Vector3(x, eave + up * k, z), new Vector3(0f, yaw, 0f));
                    }
            if (cap <= 0f) return;
            for (float z = HallStart - 1f + along * 0.5f; z < HallEnd + 1f; z += along)
                SkyProp("darkwood_roof_top_45", new Vector3(0f, eave + up * rows - 0.05f, z), new Vector3(0f, 90f, 0f));
        }

        // Turned to lie along the hall (z) if it came out lying across it, and put back in place.
        static void AlongZ(GameObject go, Vector3 at)
        {
            if (go == null) return;
            Bounds b = SkyBounds(go);
            if (b.size.x <= b.size.z) return;
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * go.transform.localRotation;
            Put(go, at, false);
        }

        // ---- the way ----

        static void Way()
        {
            // a paved way two tiles wide
            for (float z = ValhallaArrival.z - 3f; z < HallStart; z += 2f)
                foreach (float x in new[] { -1f, 1f })
                    SkyProp("blackmarble_tile_floor_2x2", new Vector3(x, 0f, z), Vector3.zero);
            // where you arrive: a wide marble landing
            foreach (float x in new[] { -3f, 3f })
                foreach (float z in new[] { ValhallaArrival.z - 2f, ValhallaArrival.z + 2f })
                    SkyProp("blackmarble_tile_floor_2x2", new Vector3(x, 0f, z), Vector3.zero);
            // braziers either side, every 8 m, and between them the shields of the fallen, planted, leaning
            int n = 0;
            for (float z = ValhallaArrival.z + 6f; z < HallStart - 2f; z += 8f)
                foreach (float side in new[] { -1f, 1f })
                {
                    SkyProp("piece_brazierfloor01", new Vector3(side * 3.2f, 0f, z), Vector3.zero);
                    if (z + 4f >= HallStart - 2f) continue;
                    string shield = ShieldNames[(n++ * 3 + 1) % ShieldNames.Length];
                    float lean = 12f + 6f * Noise(z * 0.3f, side);
                    // sunk a little into the ground, turned to face the way
                    SkyProp(shield, new Vector3(side * 4.4f, -0.25f, z + 4f), new Vector3(lean, side * -90f, 8f * Noise(z, side * 3f)));
                }
        }

        // ---- the duel ring ----

        static void Ring()
        {
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                Vector3 at = new Vector3(DuelRing.x + Mathf.Cos(a) * DuelRadius, 0f, DuelRing.y + Mathf.Sin(a) * DuelRadius);
                SkyProp("blackmarble_column_3", at, new Vector3(0f, -a * Mathf.Rad2Deg, 0f));
            }
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI / 2f;
                SkyProp("MountainKit_brazier", new Vector3(DuelRing.x + Mathf.Cos(a) * (DuelRadius + 2.5f), 0f, DuelRing.y + Mathf.Sin(a) * (DuelRadius + 2.5f)), Vector3.zero);
            }
            // a few shields leaning on the columns, of those who fought here
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 3 + 1) * Mathf.PI * 2f / 10f;
                Vector3 at = new Vector3(DuelRing.x + Mathf.Cos(a) * (DuelRadius - 0.9f), -0.1f, DuelRing.y + Mathf.Sin(a) * (DuelRadius - 0.9f));
                SkyProp(ShieldNames[i * 2 % ShieldNames.Length], at, new Vector3(-18f, -a * Mathf.Rad2Deg + 90f, 0f));
            }
        }

        // ---- the meadow ----

        static void Meadow()
        {
            // golden birches and a few oaks: round the Brim Folk's feast, the west and the rim
            Vector2[] trees =
            {
                new Vector2(-56f, 22f), new Vector2(-47f, 32f), new Vector2(-30f, 28f), new Vector2(-62f, 4f), new Vector2(-24f, 12f),
                new Vector2(-58f, -14f), new Vector2(-36f, -22f), new Vector2(26f, 46f), new Vector2(50f, 34f), new Vector2(-22f, 50f),
                new Vector2(22f, -30f), new Vector2(70f, 8f),
            };
            string[] kinds = { "Birch1_aut", "Birch2_aut", "Birch1_aut", "Oak1", "Birch2_aut" };
            for (int i = 0; i < trees.Length; i++)
            {
                Vector2 t = trees[i];
                if (!OnRock(t.x, t.y, 12f)) continue;
                SkyProp(kinds[i % kinds.Length], new Vector3(t.x, G(t.x, t.y) - 1f, t.y), new Vector3(0f, i * 67f, 0f));
            }
            // flowers in drifts, west and round the feast
            for (int i = 0; i < 90; i++)
            {
                float a = i * 2.39996f; // golden angle: spread without rows
                float r = 12f + (i % 13) * 4f + 3f * Noise(i * 0.7f, 2f);
                float x = -36f + Mathf.Cos(a) * r * 0.9f, z = 2f + Mathf.Sin(a) * r;
                if (ValhallaPads(x, z) > 0.2f || !OnRock(x, z, 6f)) continue;
                SkyProp("Pickable_Dandelion", new Vector3(x, G(x, z), z), new Vector3(0f, i * 41f, 0f));
            }
        }

        // ---- the ravens' lookout, behind the hall at the north rim ----

        static void Lookout()
        {
            float z0 = LookoutZ;
            float floor = Block(new Vector3(0f, 0f, z0), 9f, 0.35f, 9f);
            // a great fire in the middle
            GameObject fire = SkyProp("piece_brazierfloor01", new Vector3(0f, floor, z0), Vector3.zero);
            if (fire != null) { fire.transform.localScale *= 1.8f; Put(fire, new Vector3(0f, floor, z0), false); }
            // four posts, Odin's ravens on the two over the rim
            foreach (float x in new[] { -3.6f, 3.6f })
                foreach (float dz in new[] { -3.6f, 3.6f })
                {
                    GameObject post = SkyProp("darkwood_pole4", new Vector3(x, floor, z0 + dz), Vector3.zero);
                    if (post == null || dz < 0f) continue;
                    float top = SkyBounds(post).max.y;
                    SkyProp("darkwood_raven", new Vector3(x, top, z0 + dz), new Vector3(0f, x < 0f ? 200f : 160f, 0f));
                }
            // carved benches, looking out over the clouds
            foreach (float x in new[] { -2f, 2f })
            {
                Vector3 at = new Vector3(x, floor, z0 - 2.4f);
                AlongX(SkyProp("piece_bench_runed", at, Vector3.zero), at);
            }
        }

        // ---- the Brim Folk's feast, under the trees to the west ----

        static void BrimFeast()
        {
            foreach (float x in new[] { Feast.x - 4.2f, Feast.x + 4.2f })
                for (float z = Feast.y - 6f; z <= Feast.y + 6.01f; z += 6f)
                {
                    Vector3 table = new Vector3(x, 0f, z);
                    AlongZ(SkyProp("piece_table_oak", table, Vector3.zero), table);
                    foreach (float off in new[] { -1.4f, 1.4f })
                    {
                        Vector3 bench = new Vector3(x + off, 0f, z);
                        AlongZ(SkyProp("piece_bench01", bench, Vector3.zero), bench);
                    }
                }
            SkyProp("fire_pit_iron", new Vector3(Feast.x, 0f, Feast.y), Vector3.zero);
            // torches round the tables
            foreach (float dx in new[] { -8f, 8f })
                foreach (float dz in new[] { -10f, 10f })
                    SkyProp("piece_groundtorch_wood", new Vector3(Feast.x + dx, 0f, Feast.y + dz), Vector3.zero);
        }

        // ---- the spring that falls off the west rim ----

        static void Spring()
        {
            float a = 200f * Mathf.Deg2Rad;
            float edge = RockEdge(a);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 lip = dir * (edge - 1.5f);
            float y = G(lip.x, lip.y) - 1.4f;
            // mossy rocks the water comes from
            for (int i = 0; i < 4; i++)
            {
                Vector2 side = new Vector2(-dir.y, dir.x) * (i % 2 == 0 ? 3.2f : -3.2f);
                Vector2 at = dir * (edge - 4f - i * 1.5f) + side * (i < 2 ? 1f : 0.5f);
                GameObject rock = Copy(new[] { i % 2 == 0 ? "rock4_coast" : "Rock_3" }, new Vector3(0f, i * 83f, 0f), 3.5f + i * 0.6f, true);
                if (rock != null) Put(rock, new Vector3(at.x, G(at.x, at.y) - 0.8f, at.y), false);
            }
            ValhallaWater.Fall(parent, new Vector3(lip.x, y, lip.y), dir);
        }

        // ---- the einherjar's yard, by the duel ring ----

        static void EinherjarYard()
        {
            float floor = Block(new Vector3(Yard.x, 0f, Yard.y), 12f, 0.25f, 10f);
            // dummies to fight
            for (int i = 0; i < 3; i++)
                SkyProp(i % 2 == 0 ? "Morkhalla_Trainingdummy1" : "Morkhalla_Trainingdummy2", new Vector3(Yard.x - 3.5f + i * 3.5f, floor, Yard.y + 3f), new Vector3(0f, 180f + i * 15f, 0f));
            // armour stands
            foreach (float dx in new[] { -4.5f, 4.5f })
                SkyProp("ArmorStand_Male", new Vector3(Yard.x + dx, floor, Yard.y - 3.5f), new Vector3(0f, dx < 0f ? 90f : -90f, 0f));
            // a spear rack: a beam on two posts, spears leaning on it
            Vector3 rackL = new Vector3(Yard.x - 1.6f, floor, Yard.y - 3.5f), rackR = new Vector3(Yard.x + 1.6f, floor, Yard.y - 3.5f);
            GameObject postL = SkyProp("darkwood_pole", rackL, Vector3.zero);
            SkyProp("darkwood_pole", rackR, Vector3.zero);
            float rackTop = postL != null ? SkyBounds(postL).max.y : floor + 2f;
            Vector3 beam = new Vector3(Yard.x, rackTop - 0.6f, Yard.y - 3.5f);
            AlongX(SkyProp("darkwood_beam", beam, Vector3.zero), beam);
            string[] spears = { "SpearBronze", "SpearElderbark", "SpearChitin", "SpearCarapace", "SpearBronze" };
            for (int i = 0; i < spears.Length; i++)
                SkyProp(spears[i], new Vector3(Yard.x - 1.2f + i * 0.6f, floor, Yard.y - 3.0f), new Vector3(-75f, 0f, 0f));
            // shields stacked against the edge
            for (int i = 0; i < 4; i++)
                SkyProp(ShieldNames[(i * 3 + 2) % ShieldNames.Length], new Vector3(Yard.x + 5.4f, floor, Yard.y + 1f - i * 0.9f), new Vector3(-20f, -90f, 0f));
        }

        // ---- the Brim Folk's longship, south-east, its bow out over the clouds ----

        static void Longship()
        {
            float a = -42f * Mathf.Deg2Rad;
            float edge = RockEdge(a);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 at = dir * (edge - 7f);
            float yaw = 90f - a * Mathf.Rad2Deg; // its bow (+z) pointing out over the rim
            GameObject ship = SkyProp("VikingShip", Vector3.zero, new Vector3(0f, yaw, 0f)) ?? SkyProp("Karve", Vector3.zero, new Vector3(0f, yaw, 0f));
            if (ship == null) return;
            Put(ship, new Vector3(at.x, G(at.x, at.y) - 0.6f, at.y), false);
        }

        // ---- memorial stones, from where you arrive to the Brim Folk's feast ----

        static readonly Dictionary<string, GameObject> skySources = new Dictionary<string, GameObject>();

        static void MemorialPath()
        {
            Vector2 from = new Vector2(-6f, ValhallaArrival.z + 4f), to = new Vector2(Feast.x + 9f, Feast.y - 12f);
            Vector2 bend = new Vector2(-34f, -46f);
            int n = 0;
            for (float t = 0f; t <= 1.001f; t += 0.035f)
            {
                Vector2 p = Bezier(from, bend, to, t);
                // stepping stones
                SkyProp("blackmarble_tile_floor_1x1", new Vector3(p.x, G(p.x, p.y) - 0.05f, p.y), new Vector3(0f, t * 400f, 0f));
                if (n++ % 4 != 2) continue;
                // a stone either side, now a carved pillar, now a rune stone
                Vector2 ahead = Bezier(from, bend, to, Mathf.Min(1f, t + 0.02f)) - p;
                Vector2 side = new Vector2(-ahead.y, ahead.x).normalized;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector2 q = p + side * s * 2.6f;
                    bool pillar = (n / 4 + (s > 0f ? 1 : 0)) % 2 == 0;
                    GameObject stone = pillar
                        ? SkyPart("GDKing", "platforms/StonePillarTall/StonePillar", "", new Vector3(q.x, G(q.x, q.y) - 0.3f, q.y), n * 31f, 0.7f)
                        : SkyPart("Runestone_Meadows", "", "runestone", new Vector3(q.x, G(q.x, q.y) - 0.2f, q.y), n * 47f, 1f);
                    if (stone == null && !pillar) SkyPart("GDKing", "platforms/StonePillarTall/StonePillar", "", new Vector3(q.x, G(q.x, q.y) - 0.3f, q.y), n * 31f, 0.7f);
                }
            }
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        // A part of one of the game's structures (by its path in the location, or the first part whose name
        // has that word), copied still, turned and scaled, its foot at at.
        static GameObject SkyPart(string location, string path, string word, Vector3 at, float yaw, float scale)
        {
            GameObject source;
            if (!skySources.TryGetValue(location, out source))
            {
                source = null;
                if (FindLocation(new[] { location }) != null)
                {
                    CustomLocation clone = ZoneManager.Instance.CreateClonedLocation(locationName + "_Part" + (vanillaCount++), location);
                    if (clone != null && clone.Prefab != null)
                    {
                        source = clone.Prefab;
                        ZoneManager.Instance.DestroyCustomLocation(clone.Name);
                    }
                }
                skySources[location] = source;
            }
            if (source == null) { missing++; return null; }
            Transform part = path.Length > 0 ? source.transform.Find(path) : Named(source.transform, word);
            if (part == null) { missing++; Plugin.Log.LogWarning("Valhalla: no " + location + "/" + (path.Length > 0 ? path : "*" + word + "*")); return null; }
            GameObject go = Object.Instantiate(part.gameObject, parent, false);
            go.name = part.name;
            Strip(go);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * (Quaternion.Inverse(source.transform.rotation) * part.rotation);
            go.transform.localScale = part.lossyScale * scale;
            Put(go, at, false);
            built++;
            return go;
        }

        static Transform Named(Transform t, string word)
        {
            foreach (Transform child in t)
            {
                if (child.name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0 && child.GetComponentInChildren<MeshFilter>(true) != null) return child;
                Transform deeper = Named(child, word);
                if (deeper != null) return deeper;
            }
            return null;
        }

        // Turned to lie across the hall (x) if it came out lying along it, and put back in place.
        static void AlongX(GameObject go, Vector3 at)
        {
            if (go == null) return;
            Bounds b = SkyBounds(go);
            if (b.size.x >= b.size.z) return;
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * go.transform.localRotation;
            Put(go, at, false);
        }

        // ---- putting things ----

        // A still copy of one of the game's things in Valhalla: its flames lit, its scripts out, turned, then put
        // with the middle of its footprint at (at.x, at.z) and its foot (or, byTop, its top) at at.y.
        static GameObject SkyProp(string name, Vector3 at, Vector3 euler, bool byTop = false)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            if (prefab == null) { missing++; Plugin.Debug("Valhalla: no " + name); return null; }
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localRotation = Quaternion.Euler(euler);
            LightFires(go);
            Strip(go);
            // An item (a shield) as decoration: without the glint the game puts on things lying to be picked up.
            if (prefab.GetComponent<ItemDrop>() != null)
                foreach (ParticleSystem shine in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (shine.gameObject != go) { shine.gameObject.SetActive(false); continue; }
                    ParticleSystem.MainModule main = shine.main;
                    main.playOnAwake = false;
                    ParticleSystem.EmissionModule emission = shine.emission;
                    emission.enabled = false;
                    ParticleSystemRenderer drawn = shine.GetComponent<ParticleSystemRenderer>();
                    if (drawn != null) drawn.enabled = false;
                }
            Put(go, at, byTop);
            built++;
            return go;
        }

        // (Measured where it stands now, so first back to the middle: Move takes the bounds of a thing at 0.)
        static void Put(GameObject go, Vector3 at, bool byTop)
        {
            go.transform.localPosition = Vector3.zero;
            Move(go, SkyBounds(go), at, byTop);
        }

        // LocalBounds is in the location's space; Valhalla's things hang under its sky floor.
        static Bounds SkyBounds(GameObject go)
        {
            Bounds b = LocalBounds(go);
            b.center -= new Vector3(0f, SkyHeight, 0f);
            return b;
        }
    }
}
