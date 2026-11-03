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

        // The harbour's frame: centre, towards the jetty (forward), and across (side).
        static Vector2 hc, hf, hs;

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

            BuildGround();
            float jetty = cliffDeg + 229f; // the low side, between two of the shore rocks
            HarbourRocks();
            PlaceHarbour(jetty);
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
            root.AddComponent<Location>().m_applyRandomDamage = false;
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
            hc = Polar(j + 180f, 2.5f); // the shelter stands back from the plateau's middle
            float face = 90f - j;       // a piece's +z towards the jetty

            HarbourJetty(j);
            Shelter(face);

            // The trader at his counter in front of the shelter, looking down to the jetty.
            Vector2 trader = H(0.6f, 3.4f);
            PlaceNetworked(SeaTrader.PrefabName, new Vector3(trader.x, Height(trader.x, trader.y), trader.y), face);
            Prop(new[] { "piece_table", "piece_table_oak" }, H(-1.2f, 3.6f), face + 90f, 0.05f);
            Prop(new[] { "barrell_static", "barrell" }, H(-2.6f, 3.3f), 30f, 0.05f);
            Prop(new[] { "CargoCrate", "dvergrprops_crate" }, H(2.4f, 3.8f), face + 10f, 0.05f);
            string[] lantern = { "deepnorth_lantern_standing", "dvergrprops_lantern_standing" };
            Prop(lantern, H(-2.6f, 2.4f), 0f, 0.05f);
            Prop(lantern, H(2.7f, 2.4f), 0f, 0.05f);

            // A campfire with two benches on one side, the fisher's corner on the other.
            Vector2 fire = H(-5.6f, 4.6f);
            LitProp(new[] { "fire_pit", "fire_pit_haldor" }, fire, 0f, 0.05f);
            Bench(fire, H(-7.4f, 4.4f));
            Bench(fire, H(-5.4f, 6.5f));
            FishersCorner(H(5.6f, 4.2f));

            // Down by the shore, beside the jetty: an old hull he's mending.
            float wreck = j + 38f;
            Vector2 hull = Polar(wreck, ShoreAt(wreck) - 3.5f);
            OnGround(new[] { "shipwreck_karve_bottomboards" }, hull, wreck + 100f, 0f, 0.15f);
            Prop(new[] { "wood_stack" }, Polar(wreck - 9f, ShoreAt(wreck - 9f) - 5f), wreck, 0.05f);

            CaptainStatue(Polar(cliffDeg + 30f, PlateauAt(cliffDeg + 30f) - 1.5f), 90f - (cliffDeg + 30f) + 180f);
            BeaconTower(cliffDeg - 20f);

            // Old stones of the harbour hall that didn't make it into the shelter.
            At("stone_wall_2x1_ruin", Ground3(H(-4.2f, -2.6f), -0.1f), new Vector3(0f, face + 8f, 0f), false);
            At("stone_wall_1x1_ruin", Ground3(H(4.4f, -2.2f), -0.1f), new Vector3(0f, face - 12f, 0f), false);
            At("stone_wall_1x1", Ground3(H(5.8f, -0.4f), -0.15f), new Vector3(12f, face + 40f, 80f), false);

            for (int i = 0; i < 2; i++)
                PlaceNetworked(new[] { IsletPrefabs.GullSpawner }, Polar(cliffDeg + 90f + i * 150f, 3.5f), 0f, 0.3f);
            Optional(0.8f, () => OnGround(tier.Bushes, Polar(cliffDeg + 100f, PlateauAt(cliffDeg + 100f) + 1.5f), 120f, 0f, 0.15f, false));
            Optional(0.8f, () => OnGround(tier.Bushes, Polar(cliffDeg + 135f, PlateauAt(cliffDeg + 135f) + 1f), 300f, 0f, 0.15f, false));
            Optional(0.8f, () => OnGround(tier.Trees, Polar(cliffDeg - 55f, PlateauAt(cliffDeg - 55f) + 1f), 45f, 0f, 0.05f, false));
            Peace();
        }

        static Vector3 Ground3(Vector2 at, float lift)
        {
            return new Vector3(at.x, Height(at.x, at.y) + lift, at.y);
        }

        // A lean-to on the old stone floor: plank wall at the back, plank walls at the back of the sides, posts at
        // the front and a sloping roof from the back wall down to the posts. The pieces stand on the ground
        // through the floor (a piece needs the ground for support), so they're a metre taller than they look.
        static void Shelter(float face)
        {
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    Vector2 p = H(ix, iz);
                    At("stone_floor_2x2", new Vector3(p.x, FloorTop, p.y), new Vector3(0f, face, 0f), true);
                }
            float b = FloorTop - 0.05f;
            float backTop = b + 4f;
            for (int ix = -1; ix <= 1; ix += 2)
            {
                Vector2 p = H(ix, -2f);
                float top = Top(At("woodwall", new Vector3(p.x, b, p.y), new Vector3(0f, face, 0f), false), b + 2f);
                top = Top(At("woodwall", new Vector3(p.x, top, p.y), new Vector3(0f, face, 0f), false), top + 2f);
                backTop = Top(At("wood_wall_half", new Vector3(p.x, top, p.y), new Vector3(0f, face, 0f), false), top + 1f);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 back = H(2f * side, -1f);
                float top = Top(At("woodwall", new Vector3(back.x, b, back.y), new Vector3(0f, face + 90f, 0f), false), b + 2f);
                At("wood_wall_half", new Vector3(back.x, top, back.y), new Vector3(0f, face + 90f, 0f), false);
            }
            float frontTop = b + 3f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 p = H(2f * side, 1.9f);
                float top = Top(At("wood_pole2", new Vector3(p.x, b, p.y), Vector3.zero, false), b + 2f);
                frontTop = Top(At("wood_pole", new Vector3(p.x, top, p.y), Vector3.zero, false), top + 1f);
            }
            // The roof rises one metre over each two: the front row rests on the posts, the back row on the wall.
            float yaw = RoofYaw("wood_roof", face);
            float rise = Mathf.Max(0.5f, (backTop - frontTop) * 0.5f);
            for (int ix = -1; ix <= 1; ix += 2)
            {
                Vector2 front = H(ix, 1f), back = H(ix, -1f);
                At("wood_roof", new Vector3(front.x, frontTop - 0.1f, front.y), new Vector3(0f, yaw, 0f), false);
                At("wood_roof", new Vector3(back.x, frontTop + rise - 0.1f, back.y), new Vector3(0f, yaw, 0f), false);
            }
            // Inside: his things.
            Prop(new[] { "rug_seal", "rug_deer" }, H(0f, -0.3f), face, 0f);
            Prop(new[] { "barrell_static", "barrell" }, H(-1.4f, -1.4f), 10f, 0.05f);
            Prop(new[] { "CargoCrate", "dvergrprops_crate" }, H(1.3f, -1.3f), face, 0.05f);
            Prop(new[] { "piece_bench01", "piece_logbench01" }, H(1.4f, 0.2f), face - 90f, 0.05f);
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
            Vector2 land = u * (s - 2f);
            float top = Mathf.Max(0.9f, GroundMin(land.x, land.y, 1f) + 0.1f);
            for (int i = 0; i < 5; i++)
            {
                Vector2 p = u * (s - 1f + i * 2f);
                At(tier.JettyFloor, new Vector3(p.x, top, p.y), new Vector3(0f, yaw, 0f), true);
            }
            for (int i = 0; i < 5; i++)
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector2 p = u * (s + 0.1f + i * 1.95f) + side * (0.9f * k);
                    float surface = GroundMin(p.x, p.y, 0.2f);
                    int count = Mathf.Max(1, Mathf.CeilToInt((top - (surface - 0.3f)) / 2f));
                    for (int n = 0; n < count; n++)
                    {
                        float poleEnd = top - 2f * (count - 1 - n);
                        if (poleEnd < surface - 0.25f) continue;
                        At(tier.JettyPole, new Vector3(p.x, poleEnd, p.y), new Vector3(0f, yaw, 0f), true);
                    }
                }
            Prop(new[] { "deepnorth_lantern_standing", "dvergrprops_lantern_standing" }, u * (s - 3f) + side * 1.6f, 0f, 0.05f);

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
}
