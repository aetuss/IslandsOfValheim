using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Valhalla's first layout (work in progress, to be corrected in the game together): the way from where
    // Odin's light sets you down, lined with braziers and the shields of the fallen, up to the long hall
    // (open pillars, a shingle roof edged with shields, feast tables, the hearth, and at its far end the
    // throne, turned away); the duel ring of stone columns to the east; a meadow of golden birches and
    // flowers to the west. North is +z.
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

        // 1 where the ground is level (the hall, the way, the ring), fading to 0 over 6 m around them.
        internal static float ValhallaPads(float x, float z)
        {
            float pad = 0f;
            pad = Mathf.Max(pad, RectPad(x, z, 0f, (HallStart + HallEnd) * 0.5f, HallHalfWidth + 3f, (HallEnd - HallStart) * 0.5f + 3f));
            pad = Mathf.Max(pad, RectPad(x, z, 0f, (ValhallaArrival.z + HallStart) * 0.5f - 2f, 5f, (HallStart - ValhallaArrival.z) * 0.5f + 5f));
            float d = Mathf.Max(0f, new Vector2(x - DuelRing.x, z - DuelRing.y).magnitude - (DuelRadius + 3f));
            pad = Mathf.Max(pad, 1f - Mathf.SmoothStep(0f, 1f, d / 6f));
            return pad;
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
            Meadow();
            Plugin.Debug("Valhalla: layout of " + (built - before) + " pieces, " + missing + " not found");
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
            // golden birches and a few oaks, mostly to the west, some along the rim
            Vector2[] trees =
            {
                new Vector2(-38f, 8f), new Vector2(-50f, -12f), new Vector2(-30f, -26f), new Vector2(-55f, 20f), new Vector2(-44f, 36f),
                new Vector2(-24f, 46f), new Vector2(-62f, -2f), new Vector2(26f, 46f), new Vector2(52f, 34f), new Vector2(36f, -38f),
                new Vector2(-18f, -50f), new Vector2(20f, -54f),
            };
            string[] kinds = { "Birch1_aut", "Birch2_aut", "Birch1_aut", "Oak1", "Birch2_aut" };
            for (int i = 0; i < trees.Length; i++)
            {
                Vector2 t = trees[i];
                if (new Vector2(t.x, t.y).magnitude > RockEdge(Mathf.Atan2(t.y, t.x)) - 12f) continue;
                SkyProp(kinds[i % kinds.Length], new Vector3(t.x, RockMeadow(t.x, t.y) - 1f, t.y), new Vector3(0f, i * 67f, 0f));
            }
            // flowers in drifts
            for (int i = 0; i < 70; i++)
            {
                float a = i * 2.39996f; // golden angle: spread without rows
                float r = 14f + (i % 11) * 4.3f + 3f * Noise(i * 0.7f, 2f);
                float x = -34f + Mathf.Cos(a) * r * 0.9f, z = 4f + Mathf.Sin(a) * r;
                if (ValhallaPads(x, z) > 0.2f || new Vector2(x, z).magnitude > RockEdge(Mathf.Atan2(z, x)) - 6f) continue;
                SkyProp("Pickable_Dandelion", new Vector3(x, RockMeadow(x, z), z), new Vector3(0f, i * 41f, 0f));
            }
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
