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
    // - behind the hall, at the north rim, Hlidskjalf, Odin's high seat, looking out over the clouds;
    // - east, the duel ring of stone columns, and by it the einherjar's yard;
    // - west, under golden birches, the Brim Folk's own feast;
    // - south-east, the Brim Folk's longship drawn up on the meadow;
    // - south-west, a path of memorial stones from where you arrive to the feast.
    //
    // Everything is a still copy of the game's own pieces (their scripts taken out, so nothing breaks or
    // burns down); where it's lit, its flames are lit first. Each piece is put by its own measured size, so
    // they meet edge to edge whatever the game's pieces measure.
    static partial class IsletBuilder
    {
        internal static readonly Vector3 ValhallaArrival = new Vector3(0f, 0f, -64f);
        const float HallHalfWidth = 8f, HallStart = 4f, HallEnd = 40f, PillarHeight = 6f, FloorRise = 0.6f;
        static readonly Vector2 DuelRing = new Vector2(40f, 6f);
        const float DuelRadius = 10f;
        static readonly Vector2 Feast = new Vector2(-42f, 8f);
        static readonly Vector2 Yard = new Vector2(52f, -16f);
        static float LookoutZ { get { return Mathf.Min(60f, RockEdge(Mathf.PI * 0.5f) - 14f); } }

        // 1 where the ground is level (the hall, the way, the ring, the yard, the feast, the lookout), fading
        // to 0 over 6 m around them.
        internal static float ValhallaPads(float x, float z)
        {
            float pad = 0f;
            pad = Mathf.Max(pad, RectPad(x, z, 0f, (HallStart + HallEnd) * 0.5f, HallHalfWidth + 3f, (HallEnd - HallStart) * 0.5f + 3f));
            pad = Mathf.Max(pad, RectPad(x, z, 0f, (ValhallaArrival.z + HallStart) * 0.5f - 2f, 5f, (HallStart - ValhallaArrival.z) * 0.5f + 5f));
            pad = Mathf.Max(pad, CirclePad(x, z, DuelRing, DuelRadius + 3f));
            pad = Mathf.Max(pad, CirclePad(x, z, Feast, 11f));
            pad = Mathf.Max(pad, CirclePad(x, z, Yard, 12f));
            pad = Mathf.Max(pad, CirclePad(x, z, new Vector2(0f, LookoutZ), 8f));
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
            // A floor of black marble, 4 m tiles; where its front edge really is, measured, for the steps.
            float floorTop = 0f, floorFront = HallStart;
            for (float x = -HallHalfWidth + 2f; x < HallHalfWidth; x += 4f)
                for (float z = HallStart + 2f; z < HallEnd; z += 4f)
                {
                    GameObject tile = SkyProp("blackmarble_floor_large", new Vector3(x, 0f, z), Vector3.zero);
                    if (tile == null) continue;
                    // the tiles are thick blocks: most of each sunk, so the floor stands a little over the meadow
                    float thick = SkyBounds(tile).size.y;
                    if (thick > FloorRise) Put(tile, new Vector3(x, FloorRise - thick, z), false);
                    Bounds tb = SkyBounds(tile);
                    floorTop = Mathf.Max(floorTop, tb.max.y);
                    floorFront = Mathf.Min(floorFront, tb.min.z);
                }
            // Pillars along both sides, every 4 m, a shield hung on the outside of each, facing out.
            int n = 0;
            for (float z = HallStart; z <= HallEnd + 0.01f; z += 4f)
                foreach (float side in new[] { -1f, 1f })
                {
                    GameObject pillar = Pillar(new Vector3(side * HallHalfWidth, floorTop, z), PillarHeight);
                    if (pillar == null) continue;
                    Bounds pb = SkyBounds(pillar);
                    Vector3 at = new Vector3(side * (HallHalfWidth + 0.6f), floorTop + 2.2f, z);
                    PutAgainst(SkyItem(ShieldNames[(n++ * 5 + 3) % ShieldNames.Length], at, new Vector3(side, 0f, 0f), Vector3.up, false), at,
                        new Vector3(-side, 0f, 0f), side > 0f ? pb.max.x : pb.min.x);
                }
            float wallTop = floorTop + PillarHeight;
            // Beams along the tops of the pillars.
            for (float z = HallStart + 2f; z < HallEnd; z += 4f)
                foreach (float side in new[] { -1f, 1f })
                    AlongZ(SkyProp("darkwood_beam4x4", new Vector3(side * HallHalfWidth, wallTop, z), Vector3.zero), new Vector3(side * HallHalfWidth, wallTop, z));
            Roof(wallTop + 0.3f);
            // The feast: two long tables, their benches facing them, and down the middle a long fire.
            foreach (float side in new[] { -1f, 1f })
                for (float z = HallStart + 8f; z <= HallEnd - 10f; z += 6f)
                {
                    Vector3 table = new Vector3(side * 4.4f, floorTop, z);
                    AlongZ(SkyProp("piece_table_oak", table, Vector3.zero), table);
                    foreach (float off in new[] { -1.4f, 1.4f })
                        SkySeat("piece_bench01", new Vector3(side * 4.4f + off, floorTop, z), new Vector3(-Mathf.Sign(off), 0f, 0f));
                }
            foreach (float z in new[] { HallStart + 9f, HallStart + 17f, HallStart + 25f })
                SkyProp("hearth", new Vector3(0f, floorTop, z), Vector3.zero);
            // Braziers along the walls, inside.
            foreach (float x in new[] { -HallHalfWidth + 1f, HallHalfWidth - 1f })
                for (float z = HallStart + 2f; z <= HallEnd - 6f; z += 8f)
                    SkyProp("piece_brazierfloor01", new Vector3(x, floorTop, z), Vector3.zero);
            Steps(floorTop, floorFront);
            // The throne at the far end, on a dais, its seat looking north: its back to the hall.
            float dais = Block(new Vector3(0f, floorTop, HallEnd - 3f), 6f, 0.6f, 4f);
            SkySeat("piece_throne01", new Vector3(0f, dais, HallEnd - 3f), Vector3.forward);
        }

        // Steps of black marble from the ground up to the hall's floor, in front of its measured edge.
        static void Steps(float top, float front)
        {
            if (top < 0.15f) return;
            int count = Mathf.Max(2, Mathf.CeilToInt(top / 0.25f));
            const float depth = 0.45f;
            for (int i = 1; i <= count; i++)
            {
                float height = top * i / (count + 1f);
                float z = front - depth * (count - i) - depth * 0.5f;
                Block(new Vector3(0f, 0f, z), 7f, height, depth);
            }
            Plugin.Debug("Valhalla: " + count + " steps up to the hall floor at " + top.ToString("F2") + " m, its edge at z " + front.ToString("F2"));
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

        static GameObject Pillar(Vector3 foot, float height)
        {
            GameObject pole = SkyProp("darkwood_pole4", foot, Vector3.zero);
            if (pole == null) return null;
            // as tall as wanted, and thicker with it
            Bounds b = SkyBounds(pole);
            pole.transform.localScale *= height / Mathf.Max(0.1f, b.size.y);
            Put(pole, foot, false);
            return pole;
        }

        // The roof: shingles at 45 degrees from past the pillars on both sides up to the ridge cap, along the
        // whole hall. Which way a shingle runs downhill is read from the piece itself (the tilt of its
        // collider), so each side is turned to run down and out.
        static void Roof(float eave)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab("darkwood_roof_45");
            if (prefab == null) return;
            Vector3 down = Downhill(prefab);
            float yawL = down != Vector3.zero ? YawFor(down, Vector3.left) : 90f;
            float yawR = down != Vector3.zero ? YawFor(down, Vector3.right) : -90f;
            GameObject probe = SkyProp("darkwood_roof_45", Vector3.zero, new Vector3(0f, yawL, 0f));
            if (probe == null) return;
            Bounds pb = SkyBounds(probe);
            Object.DestroyImmediate(probe);
            built--;
            GameObject capProbe = SkyProp("darkwood_roof_top_45", Vector3.zero, new Vector3(0f, yawL, 0f));
            float cap = 0f;
            if (capProbe != null) { cap = SkyBounds(capProbe).size.x; Object.DestroyImmediate(capProbe); built--; }
            // The game fits these on its 2 m grid; what they measure past that is the lip where one overlaps the next.
            float across = Grid(pb.size.x), up = Grid(pb.size.y), along = Grid(pb.size.z);
            float capGrid = cap > 0f ? Grid(cap) : 0f;
            Plugin.Debug("Valhalla: roof shingle runs downhill along " + down.ToString("F2") + " in its own space; turned " + yawL.ToString("F0") + "/" + yawR.ToString("F0") +
                "; it spans " + across.ToString("F2") + " across, " + up.ToString("F2") + " up, " + along.ToString("F2") + " along; the cap " + cap.ToString("F2"));
            if (across < 0.2f || up < 0.2f || along < 0.2f) return;
            int rows = Mathf.Max(1, Mathf.RoundToInt(HallHalfWidth / across));
            float half = rows * across + capGrid * 0.5f;
            for (int k = 0; k < rows; k++)
                for (float z = HallStart - 1f + along * 0.5f; z < HallEnd + 1f; z += along)
                    foreach (float side in new[] { -1f, 1f })
                        SkyProp("darkwood_roof_45", new Vector3(side * (half - across * (k + 0.5f)), eave + up * k, z), new Vector3(0f, side < 0f ? yawL : yawR, 0f));
            if (cap <= 0f) return;
            for (float z = HallStart - 1f + along * 0.5f; z < HallEnd + 1f; z += along)
                SkyProp("darkwood_roof_top_45", new Vector3(0f, eave + up * rows - 0.4f, z), new Vector3(0f, yawL, 0f));
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
            for (float z = ValhallaArrival.z - 3f; z < HallStart - 2f; z += 2f)
                foreach (float x in new[] { -1f, 1f })
                    SkyProp("blackmarble_tile_floor_2x2", new Vector3(x, 0f, z), Vector3.zero);
            // where you arrive: a wide marble landing
            foreach (float x in new[] { -3f, 3f })
                foreach (float z in new[] { ValhallaArrival.z - 2f, ValhallaArrival.z + 2f })
                    SkyProp("blackmarble_tile_floor_2x2", new Vector3(x, 0f, z), Vector3.zero);
            // braziers either side, every 8 m, and between them a fallen warrior's spear planted upright with
            // his shield leaning on it, facing the way
            int n = 0;
            for (float z = ValhallaArrival.z + 6f; z < HallStart - 4f; z += 8f)
                foreach (float side in new[] { -1f, 1f })
                {
                    SkyProp("piece_brazierfloor01", new Vector3(side * 3.2f, 0f, z), Vector3.zero);
                    if (z + 4f >= HallStart - 4f) continue;
                    Planted(new Vector3(side * 4.6f, 0f, z + 4f), new Vector3(-side, 0f, 0f), n++);
                }
        }

        static readonly string[] SpearNames = { "SpearBronze", "SpearElderbark", "SpearChitin", "SpearCarapace" };

        // A spear stuck upright in the ground and a shield leaning back against it, its face looking along face.
        static void Planted(Vector3 at, Vector3 face, int n)
        {
            // (face is along x here: the way runs north)
            Vector3 spearAt = at + new Vector3(0f, -0.5f, 0f) - face * 0.3f;
            GameObject spear = SkyItem(SpearNames[n % SpearNames.Length], spearAt, face, Vector3.up, false, true);
            float shaft = spear != null ? SkyBounds(spear).center.x : spearAt.x;
            Vector3 leaning = (face + Vector3.up * 0.26f).normalized; // about 15 degrees back
            Vector3 shieldAt = at + new Vector3(0f, -0.05f, 0f);
            // its top resting on the shaft
            PutAgainst(SkyItem(ShieldNames[(n * 3 + 1) % ShieldNames.Length], shieldAt, leaning, Vector3.up, false), shieldAt, -face, shaft + face.x * 0.04f);
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
            // shields of those who fought here, leaning on the columns, facing in
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 3 + 1) * Mathf.PI * 2f / 10f;
                Vector3 inward = new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a));
                Vector3 at = new Vector3(DuelRing.x, 0f, DuelRing.y) - inward * (DuelRadius - 0.8f);
                SkyItem(ShieldNames[i * 2 % ShieldNames.Length], at, (inward + Vector3.up * 0.26f).normalized, Vector3.up, false);
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

        // ---- Hliðskjálf, Odin's high seat, behind the hall at the north rim ----

        // Three tiers of black marble (each its own step all round), and on top a throne looking out over the
        // clouds, two tall posts behind it with Huginn and Muninn, and a fire either side in front.
        static void Lookout()
        {
            float z0 = LookoutZ;
            float top = Block(new Vector3(0f, 0f, z0), 10f, 0.5f, 10f);
            top = Block(new Vector3(0f, top, z0), 7.6f, 0.5f, 7.6f);
            top = Block(new Vector3(0f, top, z0), 5.2f, 0.5f, 5.2f);
            SkySeat("piece_blackmarble_throne", new Vector3(0f, top, z0), Vector3.forward);
            foreach (float x in new[] { -2f, 2f })
            {
                GameObject post = Pillar(new Vector3(x, top, z0 - 2f), 5f);
                if (post != null)
                {
                    // the carving's board runs front to back, so from the throne the raven sits right on its post
                    GameObject raven = SkyProp("darkwood_raven", Vector3.zero, Vector3.zero);
                    if (raven != null)
                    {
                        raven.transform.localScale *= 1.4f;
                        Vector3 on = new Vector3(x, SkyBounds(post).max.y - 0.05f, z0 - 2f);
                        AlongZ(raven, on);
                        Put(raven, on, false);
                    }
                }
                SkyProp("piece_brazierfloor01", new Vector3(x, top, z0 + 2f), Vector3.zero);
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
                        SkySeat("piece_bench01", new Vector3(x + off, 0f, z), new Vector3(-Mathf.Sign(off), 0f, 0f));
                }
            SkyProp("fire_pit_iron", new Vector3(Feast.x, 0f, Feast.y), Vector3.zero);
            // torches round the tables
            foreach (float dx in new[] { -8f, 8f })
                foreach (float dz in new[] { -10f, 10f })
                    SkyProp("piece_groundtorch_wood", new Vector3(Feast.x + dx, 0f, Feast.y + dz), Vector3.zero);
        }

        // ---- the einherjar's yard, by the duel ring ----

        static void EinherjarYard()
        {
            float floor = Block(new Vector3(Yard.x, 0f, Yard.y), 18f, 0.25f, 14f);
            // a low wall along the south side, shields leaning on it
            float wall = Block(new Vector3(Yard.x, floor, Yard.y - 6.4f), 15f, 1.1f, 0.5f);
            float wallFront = Yard.y - 6.4f + 0.25f;
            for (int i = 0; i < 6; i++)
            {
                Vector3 at = new Vector3(Yard.x - 6f + i * 2.4f, floor, Yard.y - 5.6f);
                // its top resting on the wall
                PutAgainst(SkyItem(ShieldNames[(i * 3 + 2) % ShieldNames.Length], at, (Vector3.forward + Vector3.up * 0.27f).normalized, Vector3.up, false),
                    at, Vector3.back, wallFront);
            }
            // dummies to fight
            for (int i = 0; i < 4; i++)
                SkyProp(i % 2 == 0 ? "Morkhalla_Trainingdummy1" : "Morkhalla_Trainingdummy2", new Vector3(Yard.x - 6f + i * 4f, floor, Yard.y + 4f), new Vector3(0f, 180f + i * 12f, 0f));
            // armour stands either end
            foreach (float dx in new[] { -8f, 8f })
                SkyProp("ArmorStand_Male", new Vector3(Yard.x + dx, floor, Yard.y), new Vector3(0f, dx < 0f ? 90f : -90f, 0f));
            // a spear rack: a beam on two posts, spears standing against it
            foreach (float dx in new[] { -3f, 3f })
                SkyProp("darkwood_pole", new Vector3(Yard.x + dx, floor, Yard.y - 1f), Vector3.zero);
            Vector3 beam = new Vector3(Yard.x, floor + 1.4f, Yard.y - 1f);
            GameObject beamGo = SkyProp("darkwood_beam4x4", beam, Vector3.zero);
            AlongX(beamGo, beam);
            float beamFront = beamGo != null ? SkyBounds(beamGo).max.z : beam.z + 0.15f;
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = new Vector3(Yard.x - 2.4f + i * 1.2f, floor, Yard.y - 0.4f);
                // head up, leaning back on the beam
                PutAgainst(SkyItem(SpearNames[i % SpearNames.Length], at, Vector3.forward, (Vector3.up + Vector3.back * 0.12f).normalized, false, true),
                    at, Vector3.back, beamFront);
            }
            Plugin.Debug("Valhalla: the yard's wall top at " + wall.ToString("F2"));
        }

        // ---- the Brim Folk's longship, south-east, drawn up on the meadow, its bow towards the clouds ----

        static void Longship()
        {
            float a = -42f * Mathf.Deg2Rad;
            float edge = RockEdge(a);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 at = dir * (edge - 16f);
            float yaw = 90f - a * Mathf.Rad2Deg; // its bow (+z) towards the rim
            GameObject ship = SkyProp("VikingShip", Vector3.zero, new Vector3(0f, yaw, 0f)) ?? SkyProp("Karve", Vector3.zero, new Vector3(0f, yaw, 0f));
            if (ship == null) return;
            // no wake: it's on land
            foreach (ParticleSystem p in ship.GetComponentsInChildren<ParticleSystem>(true))
                if (p != null && p.gameObject != ship) Object.DestroyImmediate(p.gameObject);
            // its keel sunk in the turf, so it rests on it instead of hanging over it
            Put(ship, new Vector3(at.x, G(at.x, at.y) - 1.3f, at.y), false);
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

        // ---- which way the game's things face, read from them instead of guessed ----

        // The way a seat looks (where its sitter faces), flat, in its own space; zero if it has no seat.
        static Vector3 SeatFacing(GameObject prefab)
        {
            Chair chair = prefab != null ? prefab.GetComponentInChildren<Chair>(true) : null;
            if (chair == null || chair.m_attachPoint == null) return Vector3.zero;
            Vector3 f = prefab.transform.InverseTransformDirection(chair.m_attachPoint.forward);
            f.y = 0f;
            return f.sqrMagnitude > 0.01f ? f.normalized : Vector3.zero;
        }

        // The turn that takes a thing's own flat direction to the one wanted.
        static float YawFor(Vector3 own, Vector3 want)
        {
            return own == Vector3.zero ? 0f : Vector3.SignedAngle(own, want, Vector3.up);
        }

        // A seat (a bench, a throne) turned so its sitter looks along look.
        static GameObject SkySeat(string name, Vector3 at, Vector3 look)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            Vector3 own = SeatFacing(prefab);
            if (own == Vector3.zero) Plugin.Debug("Valhalla: " + name + " has no seat to face by");
            return SkyProp(name, at, new Vector3(0f, YawFor(own, look), 0f));
        }

        // An item (a shield, a spear) turned so its flat face looks along face and its long side, from its grip
        // to its far end, runs along up; its foot (or, byTop, its top) at at.y.
        static GameObject SkyItem(string name, Vector3 at, Vector3 face, Vector3 up, bool byTop, bool otherEndUp = false)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            if (prefab == null) { missing++; return null; }
            Bounds b = OwnBounds(prefab);
            Vector3 s = b.size;
            int thin = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
            int a1 = (thin + 1) % 3, a2 = (thin + 2) % 3;
            int along = s[a1] >= s[a2] ? a1 : a2;
            Vector3 thinAxis = Vector3.zero, longAxis = Vector3.zero;
            thinAxis[thin] = 1f;
            // the far end is the side that reaches further from where it's held
            // (a spear is held near its head, so for it the rule comes out the other way round: otherEndUp)
            longAxis[along] = (Mathf.Abs(b.max[along]) >= Mathf.Abs(b.min[along])) != otherEndUp ? 1f : -1f;
            Vector3 u = Vector3.ProjectOnPlane(up, face);
            if (u.sqrMagnitude < 0.001f) u = Vector3.ProjectOnPlane(Vector3.forward, face);
            Quaternion turn = Quaternion.LookRotation(face, u) * Quaternion.Inverse(Quaternion.LookRotation(thinAxis, longAxis));
            return SkyProp(name, at, turn.eulerAngles, byTop);
        }

        // Bounds of a prefab's visible meshes, in its own space.
        static Bounds OwnBounds(GameObject prefab)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (MeshFilter f in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || !ActiveBelow(f.transform, prefab.transform)) continue;
                Matrix4x4 m = prefab.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
                Bounds mb = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        // Which way a roof piece runs downhill, flat, in its own space: its roof face's tilt, from its largest
        // collider (a flat box: its thinnest side is the face). Zero if it can't be told.
        static Vector3 Downhill(GameObject prefab)
        {
            BoxCollider best = null;
            float bestArea = 0f;
            foreach (BoxCollider box in prefab.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger) continue;
                Vector3 s = Vector3.Scale(box.size, box.transform.lossyScale);
                float area = Mathf.Max(s.x * s.y, Mathf.Max(s.y * s.z, s.x * s.z));
                if (area > bestArea) { best = box; bestArea = area; }
            }
            if (best == null) return Vector3.zero;
            Vector3 bs = Vector3.Scale(best.size, best.transform.lossyScale);
            int thin = bs.x <= bs.y && bs.x <= bs.z ? 0 : bs.y <= bs.z ? 1 : 2;
            Vector3 axis = Vector3.zero;
            axis[thin] = 1f;
            Vector3 n = prefab.transform.InverseTransformDirection(best.transform.TransformDirection(axis));
            if (n.y < 0f) n = -n;
            Vector3 flat = new Vector3(n.x, 0f, n.z);
            // a sloped face's upward normal leans the way it runs down
            return flat.magnitude > 0.2f && n.y > 0.2f ? flat.normalized : Vector3.zero;
        }

        // Put so its side towards toward (one of the flat axes) just touches contact on that axis: something
        // leaning on something else.
        static void PutAgainst(GameObject go, Vector3 at, Vector3 toward, float contact)
        {
            if (go == null) return;
            Put(go, at, false);
            Bounds b = SkyBounds(go);
            Vector3 p = go.transform.localPosition;
            if (Mathf.Abs(toward.x) > 0.5f) p.x += contact - (toward.x > 0f ? b.max.x : b.min.x);
            else p.z += contact - (toward.z > 0f ? b.max.z : b.min.z);
            go.transform.localPosition = p;
        }

        // A size on the game's 2 m building grid (what's past it overlaps the next piece).
        static float Grid(float size)
        {
            return Mathf.Max(1f, Mathf.Floor(size / 2f + 0.05f) * 2f);
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
