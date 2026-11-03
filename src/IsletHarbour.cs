using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The sea trader's harbour: the last working harbour of the seafaring people whose ruins are on the other
    // islets. On the stone foundation of their old harbour hall he has put up a plank shelter; in front of
    // it are his counter, a campfire with benches and a fisher's corner, and down at the shore an old hull
    // he's mending. A sound jetty with a moored karve, a statue of one of their captains on its plinth, and
    // on the ridge one of their beacon towers, whole, its fire never out. No guards, no chest, and creatures
    // keep away from it.
    //
    // Like Haldor, it uses vanilla's unique locations: the game picks several possible spots in the Swamp
    // and Plains waters, and the first one somebody comes near is built; the others vanish. Once it's
    // there, its anchor icon is on everyone's map for good.
    static partial class IsletBuilder
    {
        internal const string HarbourName = "IoV_SeaHarbour";
        const float PeaceRadius = 40f;
        const float NoBuildRadius = 20f; // the plateau and its slopes, like the ground around a vendor

        // The harbour's frame: centre, towards the jetty (forward), and across (side).
        static Vector2 hc, hf, hs;
        static float jettyLand; // how far out from the middle the jetty starts, on dry land

        internal static void RegisterHarbour(int count)
        {
            tier = IsletTier.All[0]; // Meadows ground, rocks and plants: green and gentle
            locationName = HarbourName;
            SetVariant(0);
            seed = 911f;
            // A little bigger than an ordinary islet, so the harbour has room on the plateau.
            plateauRadius = 11f;
            shoreRadius = 23f;
            maxRadius = shoreRadius + 26f;
            root = ZoneManager.Instance.CreateLocationContainer(locationName);
            parent = root.transform;
            built = 0;
            missing = 0;

            Material ground = BuildGround();
            float jetty = cliffDeg + 229f; // the low side, between two of the shore rocks
            HarbourRocks();
            PlaceHarbour(jetty);
            Pave(ground, PavedPath(jetty), 0.8f);
            Plugin.Debug(locationName + " built: " + built + " parts, " + missing + " prefabs not found");

            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.BiomeArea = Heightmap.BiomeArea.Median;
            config.Quantity = count;
            config.Priotized = true;
            config.Unique = true;      // the first one built stays, the other candidates are removed
            config.IconPlaced = true;  // and its icon shows from then on
            config.SnapToWater = true;
            config.RandomRotation = true;
            config.ClearArea = false;
            config.ExteriorRadius = 34f;
            config.MinAltitude = -1000f;
            config.MaxAltitude = -6f;
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 1000f;
            config.MinDistance = IsletTier.All[1].MinDistance; // the Swamp and Plains waters
            config.MaxDistance = IsletTier.All[2].MaxDistance;
            config.Group = HarbourName;
            config.MinDistanceFromSimilar = 1000f;
            Location location = root.AddComponent<Location>();
            location.m_applyRandomDamage = false;
            location.m_noBuild = true;
            location.m_noBuildRadiusOverride = NoBuildRadius;
            NoBuildRing();
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + locationName + " (" + count + " possible spots, one is kept)");
        }

        // The shore rocks of an ordinary islet and a few outcrops on the cliff side, away from the harbour.
        static void HarbourRocks()
        {
            float[] angles = { 159f, 209f, 249f, 304f, 354f, 29f, 84f, 124f };
            float[] sizes = { 7f, 4.5f, 6f, 5f, 9f, 7.5f, 6f, 4f };
            for (int i = 0; i < angles.Length; i++)
            {
                float deg = cliffDeg + angles[i];
                OnGround(tier.BigRocks, Polar(deg, ShoreAt(deg) - 2f), deg * 3f, sizes[i], 0.4f);
            }
            float[] slope = { cliffDeg + 25f, cliffDeg - 35f, cliffDeg + 70f };
            for (int i = 0; i < slope.Length; i++)
            {
                float r = (PlateauAt(slope[i]) + ShoreAt(slope[i])) * 0.5f + 1f;
                OnGround(tier.BigRocks, Polar(slope[i], r), slope[i] * 2f, 3.5f + i, 0.5f);
            }
        }

        // A point in the harbour's frame: lx across, lz towards the jetty.
        static Vector2 H(float lx, float lz) { return hc + hs * lx + hf * lz; }

        static void PlaceHarbour(float j)
        {
            hf = Polar(j, 1f);
            hs = new Vector2(-hf.y, hf.x);
            hc = Polar(j + 180f, 2.5f); // the house stands back from the plateau's middle
            float face = 90f - j;       // a piece's +z towards the jetty

            HarbourJetty(j);
            House(face);

            // The trader at his counter in front of the house, looking down to the jetty.
            Vector2 trader = H(0.6f, 4.1f);
            PlaceNetworked(SeaTrader.PrefabName, new Vector3(trader.x, Height(trader.x, trader.y), trader.y), face);
            GameObject counter = OnGround(new[] { "piece_table", "piece_table_oak" }, H(-1.3f, 4.3f), face + 90f, 0f, 0.05f);
            OnTop(counter, new[] { "Fish1" }, -0.3f, face + 30f);
            OnTop(counter, new[] { "Fish2", "Fish1" }, 0.25f, face - 60f);
            Prop(new[] { "barrell_static", "barrell" }, H(-2.7f, 4.1f), 30f, 0.05f);
            Prop(new[] { "CargoCrate", "dvergrprops_crate" }, H(2.5f, 4.4f), face + 10f, 0.05f);
            string[] lantern = { "deepnorth_lantern_standing", "dvergrprops_lantern_standing" };
            Prop(lantern, H(-2.8f, 3.3f), 0f, 0.05f);
            Prop(lantern, H(2.8f, 3.3f), 0f, 0.05f);

            // A campfire with two benches on one side, the fisher's corner on the other.
            Vector2 fire = H(-5.8f, 5f);
            LitProp(new[] { "fire_pit", "fire_pit_haldor" }, fire, 0f, 0.05f);
            Prop(new[] { "piece_cookingstation" }, fire, face + 90f, 0.02f);
            Bench(fire, H(-7.6f, 4.8f));
            Bench(fire, H(-5.6f, 6.9f));
            FishersCorner(H(5.8f, 4.6f));

            // Down by the shore, beside the jetty: an old hull he's mending.
            float wreck = j + 38f;
            OnGround(new[] { "shipwreck_karve_bottomboards" }, Polar(wreck, ShoreAt(wreck) - 3.5f), wreck + 100f, 0f, 0.15f);
            Seal(j - 24f);

            CaptainStatue(Polar(cliffDeg + 30f, PlateauAt(cliffDeg + 30f) - 1.5f), 90f - (cliffDeg + 30f) + 180f);
            BeaconTower(cliffDeg - 20f);

            // Old stones of the harbour hall that didn't make it into the house.
            At("stone_wall_2x1_ruin", Ground3(H(-4.4f, -3.4f), -0.1f), new Vector3(0f, face + 8f, 0f), false);
            At("stone_wall_1x1_ruin", Ground3(H(4.6f, -3f), -0.1f), new Vector3(0f, face - 12f, 0f), false);
            At("stone_wall_1x1", Ground3(H(6f, -1f), -0.15f), new Vector3(12f, face + 40f, 80f), false);

            for (int i = 0; i < 2; i++)
                PlaceNetworked(new[] { IsletPrefabs.GullSpawner }, Polar(cliffDeg + 90f + i * 150f, 3.5f), 0f, 0.3f);
            Optional(0.8f, () => OnGround(tier.Bushes, Polar(cliffDeg + 100f, PlateauAt(cliffDeg + 100f) + 1.5f), 120f, 0f, 0.15f, false));
            Optional(0.8f, () => OnGround(tier.Bushes, Polar(cliffDeg + 135f, PlateauAt(cliffDeg + 135f) + 1f), 300f, 0f, 0.15f, false));
            Optional(0.8f, () => OnGround(tier.Trees, Polar(cliffDeg - 55f, PlateauAt(cliffDeg - 55f) + 1f), 45f, 0f, 0.05f, false));
            // Green around the edges of the plateau, in the gaps between his things.
            float[] bushes = { 85f, 150f, 175f, 260f, 290f, 315f };
            for (int i = 0; i < bushes.Length; i++)
            {
                float deg = cliffDeg + bushes[i];
                OnGround(tier.Bushes, Polar(deg, PlateauAt(deg) + 1f + (i % 2) * 0.8f), deg * 3f, 0f, 0.15f, false);
            }
            OnGround(tier.SmallTrees, Polar(cliffDeg + 200f, PlateauAt(cliffDeg + 200f) + 0.8f), 30f, 0f, 0.05f, false);
            OnGround(tier.SmallTrees, Polar(cliffDeg + 275f, PlateauAt(cliffDeg + 275f) + 0.6f), 160f, 0f, 0.05f, false);
            Vector2[] flowers = { H(-6.8f, -0.8f), H(6.8f, 1.2f), H(-3.4f, 7.6f), H(3.8f, 8f), H(-8f, 2f) };
            for (int i = 0; i < flowers.Length; i++)
                PlaceNetworked("Pickable_Dandelion", Ground3(flowers[i], 0f), i * 70f);
            Peace();
        }

        // The paved ground, painted the way the hoe paves it: from the counter down to the jetty, and in
        // front of the house. Pairs of points, the ends of each stretch.
        static Vector2[] PavedPath(float j)
        {
            return new[] { H(0.4f, 3.2f), Polar(j, jettyLand + 1f), H(-3f, 3.6f), H(3f, 3.6f) };
        }

        // The terrain shader reads a paint mask: blue is paved. Drawn over the whole islet, soft at the edges.
        static void Pave(Material ground, Vector2[] stretches, float halfWidth)
        {
            if (ground == null || !ground.HasProperty("_ClearedMaskTex")) return;
            const int size = 256;
            Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
            mask.name = "IoV_HarbourPaintMask";
            mask.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // The ground's uv: x / (2 * maxRadius) + 0.5 (see BuildGround).
                    Vector2 p = new Vector2(((x + 0.5f) / size - 0.5f) * 2f * maxRadius, ((y + 0.5f) / size - 0.5f) * 2f * maxRadius);
                    float d = float.MaxValue;
                    for (int i = 0; i + 1 < stretches.Length; i += 2) d = Mathf.Min(d, Segment(p, stretches[i], stretches[i + 1]));
                    float edge = halfWidth + 0.25f * Mathf.PerlinNoise(p.x * 0.7f + 3f, p.y * 0.7f + 9f);
                    float paved = Mathf.Clamp01((edge - d) / 0.35f);
                    pixels[y * size + x] = new Color(0f, 0f, paved, 1f);
                }
            mask.SetPixels(pixels);
            mask.Apply(false, true);
            ground.SetTexture("_ClearedMaskTex", mask);
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        // Bára, his seal, on the bank by the jetty: a seal's body without its scripts, lying there. She can be petted.
        static void Seal(float deg)
        {
            GameObject prefab = Find(new[] { "Seal" });
            if (prefab == null) return;
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = "IoV_HarbourSeal";
            Strip(go);
            foreach (Animator animator in go.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
            Vector2 at = Polar(deg, ShoreAt(deg) - 2.5f);
            go.transform.localPosition = new Vector3(at.x, GroundMin(at.x, at.y, 0.6f) - 0.05f, at.y);
            go.transform.localRotation = Quaternion.Euler(0f, 90f - deg, 0f); // looking out to sea
            go.AddComponent<HarbourSeal>();
            built++;
        }

        // The ring a ward shows, around the ground nobody can build on.
        static void NoBuildRing()
        {
            GameObject ward = PrefabManager.Instance.GetPrefab("guard_stone");
            CircleProjector source = ward != null ? ward.GetComponentInChildren<CircleProjector>(true) : null;
            if (source == null) return;
            GameObject ring = Object.Instantiate(source.gameObject, root.transform, false);
            ring.name = "IoV_HarbourRing";
            ring.SetActive(true);
            ring.transform.localPosition = new Vector3(0f, PlateauHeight + 1f, 0f);
            ring.transform.localRotation = Quaternion.identity;
            CircleProjector projector = ring.GetComponent<CircleProjector>();
            projector.m_nrOfSegments = Mathf.RoundToInt(projector.m_nrOfSegments * NoBuildRadius / Mathf.Max(1f, projector.m_radius));
            projector.m_radius = NoBuildRadius;
        }

        // A static copy of a prop lying on top of another (an item on a table, say), off its centre by along.
        static void OnTop(GameObject under, string[] candidates, float along, float rotY)
        {
            if (under == null) return;
            Bounds top = SolidBounds(under);
            GameObject go = Copy(candidates, new Vector3(0f, rotY, 0f), 0f, true);
            if (go == null) return;
            Vector3 at = top.center + under.transform.localRotation * new Vector3(along, 0f, 0f);
            Move(go, LocalBounds(go), new Vector3(at.x, top.max.y - 0.01f, at.z), false);
        }

        static Vector3 Ground3(Vector2 at, float lift)
        {
            return new Vector3(at.x, Height(at.x, at.y) + lift, at.y);
        }

        // A building piece as a static copy: it looks and blocks like the real one but needs no support, so it
        // can stand on the floor as built (a real piece would have to reach down to the ground) and never falls.
        static GameObject Static(string name, Vector2 at, float y, float yaw, bool alignTop)
        {
            GameObject go = Copy(new[] { name }, new Vector3(0f, yaw, 0f), 0f, true);
            if (go != null) Move(go, SolidBounds(go), new Vector3(at.x, y, at.y), alignTop);
            return go;
        }

        // His house, on the old stone floor: plank walls at the back and sides (their beams on the inside),
        // posts and a beam over the open front where he trades, a steep thatched roof with gables, and a
        // carved dragon over the door.
        static void House(float face)
        {
            const float halfWidth = 2f, back = -3f, front = 3f, wall = 2f, rise = 2f; // 45 degree roof
            float floor = Height(hc.x, hc.y) + 0.12f;
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -2; iz <= 2; iz += 2)
                {
                    Vector2 p = H(ix, iz);
                    Static("stone_floor_2x2", p, floor, face, true);
                }
            float inBack = face + 180f, inRight = face - 90f, inLeft = face + 90f; // walls turned so their beams face in
            for (int ix = -1; ix <= 1; ix += 2) Static("woodwall", H(ix, back), floor, inBack, false);
            for (int iz = -2; iz <= 2; iz += 2)
            {
                Static("woodwall", H(halfWidth, iz), floor, inRight, false);
                Static("woodwall", H(-halfWidth, iz), floor, inLeft, false);
            }
            for (int side = -1; side <= 1; side += 2) Static("wood_pole2", H(side * (halfWidth - 0.1f), front - 0.1f), floor, face, false);
            for (int ix = -1; ix <= 1; ix += 2) Static("wood_beam", H(ix, front - 0.1f), floor + wall, face, true);

            float eave = floor + wall;
            float rightYaw = RoofYaw("wood_roof_45", inRight), leftYaw = RoofYaw("wood_roof_45", inLeft); // low side outwards
            for (int iz = -2; iz <= 2; iz += 2)
            {
                Static("wood_roof_45", H(1f, iz), eave - 0.05f, rightYaw, false);
                Static("wood_roof_45", H(-1f, iz), eave - 0.05f, leftYaw, false);
                Static("wood_roof_top_45", H(0f, iz), eave + rise + 0.35f, face + 90f, true);
            }
            Gable(back, inBack, eave, 1);
            Gable(front, face, eave, -1);
            Static("wood_dragon1", H(0f, front + 0.15f), eave + 0.6f, face, false);

            // Inside: his things.
            Prop(new[] { "rug_seal", "rug_deer" }, H(0f, -0.5f), face, 0f);
            Prop(new[] { "barrell_static", "barrell" }, H(-1.3f, -2.3f), 10f, 0.05f);
            Prop(new[] { "barrell_static", "barrell" }, H(-0.4f, -2.4f), 70f, 0.05f);
            Prop(new[] { "CargoCrate", "dvergrprops_crate" }, H(1.2f, -2.2f), face, 0.05f);
            Prop(new[] { "piece_bench01", "piece_logbench01" }, H(1.4f, 0.3f), inRight, 0.05f);
        }

        // The triangle of wall under the roof at one end of the house. The game's gable piece is half of it (a
        // right triangle, 2 m by 2 m); whichever side its tall edge is on, the two halves are put so the tall
        // edges meet in the middle (one mirrored). xSign maps the piece's local x onto the house's x: +1 at
        // the back (turned round), -1 at the front.
        static void Gable(float lz, float yaw, float bottom, int xSign)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab("wood_wall_roof_45");
            if (prefab == null) return;
            int tall = TallSide(prefab);
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject go = Copy(new[] { "wood_wall_roof_45" }, new Vector3(0f, yaw, 0f), 0f, true);
                if (go == null) return;
                // The tall edge must point to the middle, i.e. away from s.
                if (tall * xSign != -s) go.transform.localScale = new Vector3(-1f, 1f, 1f);
                Vector2 at = H(s * 1f, lz);
                Move(go, SolidBounds(go), new Vector3(at.x, bottom - 0.12f, at.y), false); // down into the wall, no gap
            }
        }

        // Which side (+1 or -1 along local x) a triangular wall piece has its tall edge on: the mass of a right
        // triangle's vertices lies towards it. If the mesh can't be read, +1.
        static int TallSide(GameObject prefab)
        {
            MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
            {
                Plugin.Debug("Gable piece " + prefab.name + ": mesh not readable, tall edge taken as +x");
                return 1;
            }
            Vector3[] vertices = filter.sharedMesh.vertices;
            Vector3 sum = Vector3.zero, min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            foreach (Vector3 v in vertices)
            {
                Vector3 p = prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(v));
                sum += p;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            Vector3 mean = sum / Mathf.Max(1, vertices.Length);
            float shift = mean.x - (min.x + max.x) * 0.5f;
            Plugin.Debug("Gable piece " + prefab.name + ": size " + (max - min).ToString("F2") + ", vertex mean off centre by " + shift.ToString("F2") + " in x");
            return shift < 0f ? -1 : 1;
        }
        // Which way to turn a roof piece so that it slopes down towards the front. The tilt of its collider
        // tells which side is low; a piece without a tilted collider is taken to rise towards its +z.
        static float RoofYaw(string name, float face)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            Vector3 low = new Vector3(0f, 0f, -1f);
            if (prefab != null)
                foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
                {
                    if (collider.isTrigger) continue;
                    Vector3 up = prefab.transform.InverseTransformDirection(collider.transform.up);
                    if (new Vector2(up.x, up.z).magnitude > 0.1f) { low = new Vector3(up.x, 0f, up.z).normalized; break; }
                }
            // Turning by yaw takes the piece's low side (at angle a from +z) to face + a.
            float a = Mathf.Atan2(low.x, low.z) * Mathf.Rad2Deg;
            Plugin.Debug("Roof " + name + " slopes down towards " + low.ToString("F2"));
            return face - a;
        }

        static void Bench(Vector2 fire, Vector2 at)
        {
            Vector2 to = fire - at;
            float yaw = Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg; // its seat facing the fire
            Prop(new[] { "piece_logbench01", "piece_bench01" }, at, yaw, 0.05f);
        }

        // Drying racks with a barrel and crates beside them.
        static void FishersCorner(Vector2 c)
        {
            float face = Mathf.Atan2(hf.x, hf.y) * Mathf.Rad2Deg;
            for (int i = 0; i < 2; i++)
            {
                Vector2 p = c + hf * (i * 1.6f - 0.8f);
                Vector2 a = p - hs * 1f, b = p + hs * 1f;
                float top = Top(At("wood_pole2", new Vector3(a.x, PlateauHeight, a.y), Vector3.zero, false), PlateauHeight + 1.9f);
                At("wood_pole2", new Vector3(b.x, PlateauHeight, b.y), Vector3.zero, false);
                At("wood_beam", new Vector3(p.x, top - 0.25f, p.y), new Vector3(0f, face, 0f), false);
                // Fish hung up to dry.
                for (int f = -1; f <= 1; f++)
                {
                    GameObject fish = Copy(new[] { f == 0 ? "Fish2" : "Fish1", "Fish1" }, new Vector3(90f, face + f * 20f, 0f), 0f, true);
                    Vector2 q = p + hs * (f * 0.6f);
                    if (fish != null) fish.transform.localPosition = new Vector3(q.x, top - 0.75f, q.y);
                }
            }
            Prop(new[] { "barrell_static", "barrell" }, c + hs * 2f + hf * 1.2f, 20f, 0.05f);
            Prop(new[] { "CargoCrate", "dvergrprops_crate" }, c + hs * 2.2f - hf * 0.6f, face, 0.05f);
            Prop(new[] { "CargoCrate", "dvergrprops_crate" }, c - hs * 2f, face + 25f, 0.05f);
        }

        // One of their captains, what's left of him, on a stone plinth; the rest of him lies beside it.
        static void CaptainStatue(Vector2 at, float yaw)
        {
            float g = GroundMin(at.x, at.y, 1.2f);
            GameObject plinth = At("stone_floor_2x2", new Vector3(at.x, g + 0.35f, at.y), new Vector3(0f, yaw, 0f), true);
            float top = plinth != null ? SolidBounds(plinth).max.y : g + 0.35f;
            GameObject statue = Copy(new[] { "StatueThor_broken_bottom", "StatueFreya_broken_left", "StatueEvil" }, new Vector3(0f, yaw, 0f), 0f, true);
            if (statue != null)
            {
                Bounds b = LocalBounds(statue);
                float extent = Mathf.Max(b.size.x, b.size.z);
                if (extent > 1.8f) { statue.transform.localScale = Vector3.one * (1.8f / extent); b = LocalBounds(statue); }
                Move(statue, b, new Vector3(at.x, top - 0.02f, at.y), false);
            }
            Vector2 side = Polar(90f - yaw, 1f);
            Lying(new[] { "StatueThor_broken_top", "StatueFreya_broken_right" }, at + new Vector2(side.y, -side.x) * 2.3f, yaw + 70f, 1.8f);
        }

        // A beacon tower like the ruined ones on the islets, but whole, its fire never out.
        static void BeaconTower(float deg)
        {
            Vector2 at = Polar(deg, PlateauAt(deg) + 2.5f);
            Vector3 turn = new Vector3(0f, 90f - deg, 0f);
            float g = GroundMin(at.x, at.y, 1f) - 0.3f;
            float top = Top(At("stone_floor_2x2", new Vector3(at.x, g, at.y), turn, false), g + 1f);
            top = Top(At("stone_pillar", new Vector3(at.x, top, at.y), turn, false), top + 2f);
            top = Top(At("stone_pillar", new Vector3(at.x, top, at.y), turn, false), top + 2f);
            GameObject brazier = LitAt(new[] { "piece_brazierfloor01", "bonfire" }, new Vector3(at.x, top, at.y), 0f);
            if (brazier != null) Glow(brazier.transform, 1.2f);
        }

        // A sound jetty, five planks long, with the karve moored along it.
        static void HarbourJetty(float deg)
        {
            float s = ShoreAt(deg);
            Vector2 u = Polar(deg, 1f), side = new Vector2(-u.y, u.x);
            float yaw = 90f - deg;
            const float top = 1f;
            // The first planks lie on the bank: go in from the shore line until the ground is up at the planks.
            jettyLand = s - 2f;
            for (float r = s; r > s - 8f; r -= 0.25f)
            {
                Vector2 q = u * r;
                if (Height(q.x, q.y) >= top - 0.15f) { jettyLand = r; break; }
            }
            float end = s + 8f;
            int sections = Mathf.CeilToInt((end - jettyLand) / 2f);
            for (int i = 0; i < sections; i++)
            {
                Vector2 p = u * (jettyLand + 1f + i * 2f);
                At(tier.JettyFloor, new Vector3(p.x, top, p.y), new Vector3(0f, yaw, 0f), true);
            }
            // Posts wherever the planks are well off the ground or over the water.
            for (float along = jettyLand + 1.9f; along <= end + 0.05f; along += 1.95f)
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector2 p = u * along + side * (0.9f * k);
                    float surface = GroundMin(p.x, p.y, 0.2f);
                    if (top - surface < 0.4f) continue;
                    int count = Mathf.Max(1, Mathf.CeilToInt((top - (surface - 0.3f)) / 2f));
                    for (int n = 0; n < count; n++)
                    {
                        float poleEnd = top - 2f * (count - 1 - n);
                        if (poleEnd < surface - 0.25f) continue;
                        At(tier.JettyPole, new Vector3(p.x, poleEnd, p.y), new Vector3(0f, yaw, 0f), true);
                    }
                }
            Prop(new[] { "deepnorth_lantern_standing", "dvergrprops_lantern_standing" }, u * (jettyLand - 0.8f) + side * 1.5f, 0f, 0.05f);

            MooredKarve(u * (s + 4.5f) + side * 3f, yaw);
        }

        // A static copy of a karve, nothing to sail away with: sail furled, no spray from its bow, sitting
        // in the water.
        static void MooredKarve(Vector2 at, float yaw)
        {
            GameObject prefab = Find(new[] { "Karve" });
            if (prefab == null) return;
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localPosition = Vector3.zero;
            Ship ship = go.GetComponent<Ship>();
            if (ship != null && ship.m_sailObject != null) ship.m_sailObject.SetActive(false);
            // The sail cloth itself is a part of its own, not under the sail object.
            foreach (Transform part in go.GetComponentsInChildren<Transform>(true))
                if (part != go.transform && part.name.ToLowerInvariant().Contains("sail"))
                {
                    Plugin.Debug("Karve part furled: " + part.name);
                    part.gameObject.SetActive(false);
                }
            foreach (ParticleSystem particles in go.GetComponentsInChildren<ParticleSystem>(true))
                if (particles.GetComponent<MeshFilter>() == null) particles.gameObject.SetActive(false);
            foreach (AudioSource sound in go.GetComponentsInChildren<AudioSource>(true)) sound.enabled = false;
            Strip(go);
            built++;
            Bounds b = LocalBounds(go);
            Plugin.Debug("Karve mesh bounds " + b.size.ToString("F1") + " min y " + b.min.y.ToString("F2") + ", solid bounds min y " + SolidBounds(go).min.y.ToString("F2"));
            Move(go, b, new Vector3(at.x, -1.4f, at.y), false);
        }

        // A fire prop that always burns, on the ground: its flames switched on for good before its scripts go.
        static GameObject LitProp(string[] candidates, Vector2 at, float rotY, float sink)
        {
            GameObject go = Lit(candidates, rotY);
            if (go == null) return null;
            Bounds b = LocalBounds(go);
            float y = GroundMin(at.x, at.y, 0.4f * Mathf.Max(b.size.x, b.size.z)) - sink * b.size.y;
            Move(go, b, new Vector3(at.x, y, at.y), false);
            return go;
        }

        // The same, with its bottom at pos (on top of something).
        static GameObject LitAt(string[] candidates, Vector3 pos, float rotY)
        {
            GameObject go = Lit(candidates, rotY);
            if (go == null) return null;
            Move(go, LocalBounds(go), pos, false);
            return go;
        }

        static GameObject Lit(string[] candidates, float rotY)
        {
            GameObject prefab = Find(candidates);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            go.transform.localPosition = Vector3.zero;
            foreach (Fireplace fire in go.GetComponentsInChildren<Fireplace>(true))
                foreach (GameObject flames in new[] { fire.m_enabledObject, fire.m_enabledObjectHigh })
                    if (flames != null) flames.SetActive(true);
            Strip(go);
            built++;
            return go;
        }

        static void Glow(Transform on, float height)
        {
            GameObject glow = new GameObject("IoV_HarbourGlow");
            glow.transform.SetParent(on, false);
            glow.transform.localPosition = new Vector3(0f, height, 0f);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 30f;
            light.intensity = 2.5f;
            light.color = new Color(1f, 0.65f, 0.35f);
        }

        // A broken piece lying on its side, resting on the ground rather than sunk into it.
        static void Lying(string[] candidates, Vector2 at, float rotY, float maxSize)
        {
            GameObject go = Copy(candidates, new Vector3(0f, rotY, 88f), 0f, true);
            if (go == null) return;
            Bounds b = LocalBounds(go);
            float extent = Mathf.Max(b.size.x, b.size.z);
            if (extent > maxSize) { go.transform.localScale = Vector3.one * (maxSize / extent); b = LocalBounds(go); }
            float y = GroundMin(at.x, at.y, 0.25f * Mathf.Max(b.size.x, b.size.z)) - 0.05f * b.size.y;
            Move(go, b, new Vector3(at.x, y, at.y), false);
        }

        // Creatures keep away and don't spawn here, as near a player's base.
        static void Peace()
        {
            GameObject area = new GameObject("IoV_HarbourPeace");
            area.layer = LayerMask.NameToLayer("character_trigger");
            area.transform.SetParent(root.transform, false);
            area.transform.localPosition = new Vector3(0f, PlateauHeight, 0f);
            SphereCollider sphere = area.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = PeaceRadius;
            area.AddComponent<EffectArea>().m_type = EffectArea.Type.PlayerBase | EffectArea.Type.NoMonsters;
        }
    }

    // The trader's seal. Hover shows her name; using her pets her, with the hearts a petted lox gets.
    class HarbourSeal : MonoBehaviour, Hoverable, Interactable
    {
        static EffectList hearts;
        float pettedAt = -10f;

        public string GetHoverText()
        {
            return Localization.instance.Localize("$iov_seal_name\n[<color=yellow><b>$KEY_Use</b></color>] $hud_pet");
        }

        public string GetHoverName() { return Localization.instance.Localize("$iov_seal_name"); }

        public float GetHoverOffset() { return 0f; }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || Time.time - pettedAt < 1f) return false;
            pettedAt = Time.time;
            if (hearts == null && ZNetScene.instance != null)
            {
                GameObject lox = ZNetScene.instance.GetPrefab("Lox");
                Tameable tame = lox != null ? lox.GetComponent<Tameable>() : null;
                if (tame != null) hearts = tame.m_petEffect;
            }
            if (hearts != null) hearts.Create(transform.position + Vector3.up * 0.5f, transform.rotation, null, 1f, -1, new ZDOID());
            Player player = user as Player;
            if (player != null) player.Message(MessageHud.MessageType.Center, "$iov_seal_pet", 0, null, false);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
}
