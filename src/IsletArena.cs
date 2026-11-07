using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The hidden islet as the tale's last climb (from 2.0): an islet of the game's own terrain, far out in the
    // Deep North waters, rising from a beach in three terraces joined by ramps. Grímr keeps the lowest, Ormr
    // the middle one, Hafdís her frost-bound hall at the top. All three stand there from the start, still, until
    // the tale has come this far (Hafdís's shore done) and it's their turn (FinalArena); ice closes each ramp
    // until the keeper below it falls. The old hidden islet (IsletStory.HiddenIslet) stays known for the
    // worlds that have it already built.
    static partial class IsletBuilder
    {
        // Heights over the sea: the landing, then the terraces.
        internal const float ArenaLanding = 1.5f, ArenaT1 = 4.5f, ArenaT2 = 8.5f, ArenaT3 = 12.5f;
        // The land, an oval longer from the landing (-z) to the top (+z), and where its terraces rise.
        const float ArenaAx = 27f, ArenaAz = 37f, ArenaCz = -3f;
        const float RiseT1 = -21f, RiseT2 = -2f, RiseT3 = 15f;
        const float ArenaReach = 50f, ArenaLand = 40f;

        // Where each keeper stands (islet space, the terrace's height), and how far its terrace reaches round it.
        internal static readonly Vector3[] ArenaSpots = { new Vector3(0f, ArenaT1, -11f), new Vector3(1f, ArenaT2, 4f), new Vector3(0f, ArenaT3, 20f) };
        internal static readonly float[] ArenaRooms = { 11f, 10f, 9f };

        internal static float ArenaHeight(float x, float z)
        {
            float ex = x / ArenaAx, ez = (z - ArenaCz) / ArenaAz;
            float angle = Mathf.Atan2(ez, ex);
            float e = Mathf.Sqrt(ex * ex + ez * ez) * (1f + 0.05f * Mathf.Sin(angle * 3f + 1.3f) + 0.03f * Mathf.Sin(angle * 7f + 0.4f));
            float rough = Mathf.PerlinNoise(x * 0.11f + 613f, z * 0.11f + 71f) - 0.5f;
            float inner = ArenaTerraces(x, z) + rough * 0.3f;
            if (e <= 0.8f) return inner;
            if (e <= 1f)
            {
                float t = (e - 0.8f) / 0.2f;
                return Mathf.Lerp(inner, -1.2f, t * t * (3f - 2f * t)) + rough * 1.2f * Mathf.Sin(Mathf.PI * t);
            }
            float d = (e - 1f) * ArenaAx; // metres past the shore, about
            if (d <= 6f) return -1.2f - d * 0.8f;
            return Mathf.Max(-30f, -6f - (d - 6f) * 4f);
        }

        // The landing and the three terraces, each rising steeply from the one below, and the ramps up.
        static float ArenaTerraces(float x, float z)
        {
            float h = ArenaLanding + Rise(z, RiseT1, ArenaT1 - ArenaLanding) + Rise(z, RiseT2, ArenaT2 - ArenaT1) + Rise(z, RiseT3, ArenaT3 - ArenaT2);
            h = Mathf.Max(h, Ramp(x, z, 0f, -28.5f, RiseT1 + 0.8f, ArenaLanding, ArenaT1));  // up from the landing
            h = Mathf.Max(h, Ramp(x, z, 6.5f, -9.5f, RiseT2 + 0.8f, ArenaT1, ArenaT2));       // Grímr's terrace to Ormr's
            h = Mathf.Max(h, Ramp(x, z, -6.5f, 7.5f, RiseT3 + 0.8f, ArenaT2, ArenaT3));       // Ormr's to Hafdís's
            return h;
        }

        // A 4 m rise over 2 m around z = at.
        static float Rise(float z, float at, float height)
        {
            float t = Mathf.Clamp01((z - at + 1f) / 2f);
            return height * t * t * (3f - 2f * t);
        }

        // A ramp 4.5 m wide along z, centred on x = cx, from h0 at z0 to h1 at z1; its sides fall away.
        static float Ramp(float x, float z, float cx, float z0, float z1, float h0, float h1)
        {
            if (z < z0 || z > z1 + 0.5f) return float.MinValue;
            float h = Mathf.Lerp(h0, h1, Mathf.Clamp01((z - z0) / (z1 - z0)));
            float side = Mathf.Abs(x - cx) - 2.25f;
            return side <= 0f ? h : h - side * 4f;
        }

        // How far out the land ends in a direction (degrees, as Polar), for the jetty and the wreck.
        static float ArenaShore(float degrees)
        {
            Vector2 u = Polar(degrees, 1f);
            for (float r = 4f; r < ArenaReach; r += 0.25f)
                if (ArenaHeight(u.x * r, u.y * r) < 0f) return r;
            return ArenaReach;
        }

        static void FinalArena()
        {
            tier = IsletTier.All[6];
            locationName = Story.Arena;
            SetVariant(0);
            root = ZoneManager.Instance.CreateLocationContainer(locationName);
            parent = root.transform;
            built = 0;
            missing = 0;
            heightOverride = ArenaHeight;
            shoreOverride = ArenaShore;
            try { ArenaParts(); }
            catch (System.Exception e) { Plugin.Log.LogError("The final arena failed: " + e); }
            finally { heightOverride = null; shoreOverride = null; }

            Plugin.Debug(locationName + " built: " + built + " parts, " + missing + " prefabs not found");
            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.BiomeArea = Heightmap.BiomeArea.Median;
            config.Quantity = StorySpots;
            config.Priotized = true;
            config.Unique = true;
            config.SnapToWater = true;
            config.RandomRotation = true;
            config.ClearArea = false;
            config.ExteriorRadius = 50f;
            config.MinAltitude = -1000f;
            config.MaxAltitude = -8f;
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 1000f;
            config.MinDistance = tier.MinDistance + 600f;
            config.MaxDistance = tier.MaxDistance;
            config.Group = locationName;
            config.MinDistanceFromSimilar = 600f;
            Location location = root.AddComponent<Location>();
            location.m_applyRandomDamage = true;
            location.m_exteriorRadius = 50f;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + locationName + " (" + StorySpots + " possible spots, one is kept)");
        }

        static void ArenaParts()
        {
            GameObject ground = new GameObject("IoV_BakedGround");
            ground.transform.SetParent(root.transform, false);
            BakedGround.Bake(ground, ArenaReach, ArenaLand, tier.Biome, ArenaHeight);

            // Rocks round the cliffs that ring it (not on the landing), and at the foot of each rise.
            for (float deg = 0f; deg < 360f; deg += 24f)
            {
                if (deg > 228f && deg < 312f) continue;
                float s = ArenaShore(deg);
                OnGround(tier.BigRocks, Polar(deg, s - 1.5f), deg * 3f, 5f + (deg % 48f == 0f ? 2.5f : 0f), 0.45f);
            }
            float[,] foot = { { -10f, RiseT1 - 1.5f }, { 9f, RiseT1 - 1.8f }, { -5f, RiseT2 - 1.6f }, { -14f, RiseT2 - 1.4f }, { 14f, RiseT2 - 1.5f }, { 3f, RiseT3 - 1.5f }, { 11f, RiseT3 - 1.6f } };
            for (int i = 0; i < foot.GetLength(0); i++)
                OnGround(tier.BigRocks, new Vector2(foot[i, 0], foot[i, 1]), i * 53f, 3f + (i % 2) * 0.8f, 0.45f);

            // Snow pines on the edges, and what grows in the cold.
            Vector2[] trees = { new Vector2(-15f, -14f), new Vector2(16f, -9f), new Vector2(-16f, 3f), new Vector2(16f, 7f), new Vector2(-12f, -27f), new Vector2(13f, -26f), new Vector2(11f, 19f), new Vector2(-11f, 21f) };
            for (int i = 0; i < trees.Length; i++)
            {
                if (ArenaHeight(trees[i].x, trees[i].y) < 1f) continue;
                int k = i;
                Optional(0.85f, () => OnGround(k % 3 == 2 ? tier.SmallTrees : tier.Trees, trees[k], k * 71f, 0f, 0.05f, false));
            }
            Vector2[] berries = { new Vector2(-8f, -24f), new Vector2(9f, -15f), new Vector2(-12f, 9f), new Vector2(6f, 21f) };
            foreach (Vector2 b in berries) { Vector2 at = b; Optional(0.7f, () => PlaceNetworked(tier.Berries, at, 0f, 0f)); }

            // The landing: an old jetty, a wreck, the stone that warns, cold fire at the way up.
            PlaceDock(255f);
            PlaceWreck(290f);
            PlaceNetworked(new[] { "IoV_StoryStone_Hidden" }, new Vector2(3.6f, -27f), 180f, 0f);
            Brazier(new Vector2(-3.2f, -18.6f));
            Brazier(new Vector2(3.2f, -18.6f));

            // Grímr's terrace: standing stones round where he waits, and those who came before.
            Vector2 g = new Vector2(ArenaSpots[0].x, ArenaSpots[0].z);
            // (Clear of the ramps: the one up from the landing at x 0, the one on to Ormr's at x 6.5.)
            float[] stones = { 110f, 160f, 210f, 250f, 340f };
            foreach (float deg in stones)
            {
                Vector2 p = g + Polar(deg, 7f);
                float y = GroundMin(p.x, p.y, 0.5f) - 0.3f;
                float top = Top(At("stone_pillar", new Vector3(p.x, y, p.y), Vector3.zero, false), y + 2f);
                if (deg != 210f) At("stone_pillar", new Vector3(p.x, top, p.y), Vector3.zero, false);
            }
            quietProps = true;
            Prop(new[] { "FrozenSkeleton_Pose1" }, g + new Vector2(-4f, -3.5f), 30f, 0.05f);
            Prop(new[] { "FrozenSkeleton_Pose2" }, g + new Vector2(-5f, 3f), 200f, 0.05f);
            Lying(new[] { "SwordSilver", "SwordIron" }, g + new Vector2(-3.2f, -4.6f), 70f, 1.4f);
            quietProps = false;
            Ice(g + new Vector2(-9f, 4f), 40f, 2.2f);
            Ice(g + new Vector2(9.5f, -2f), 160f, 2f);

            // Ormr's terrace: his cultists' circle of marker stones and blue fire.
            Vector2 o = new Vector2(ArenaSpots[1].x, ArenaSpots[1].z);
            for (int i = 0; i < 6; i++)
            {
                float deg = i * 60f + 15f;
                Prop(new[] { i % 2 == 0 ? "marker01" : "marker02" }, o + Polar(deg, 5.5f), 90f - deg, 0.15f);
            }
            Brazier(o + new Vector2(-3f, 5.5f));
            Brazier(o + new Vector2(3f, 5.5f));
            quietProps = true;
            Prop(new[] { "FrozenSkeleton_Pose2" }, o + new Vector2(7f, 2f), 120f, 0.05f);
            quietProps = false;
            Ice(o + new Vector2(9f, 4f), 250f, 1.8f);

            // Hafdís's hall at the top, as it was on the old hidden islet: a floor, a high wall behind her,
            // pillars round it (none at the front, where the ramp comes up), lanterns and blue fire.
            Vector2 h = new Vector2(ArenaSpots[2].x, ArenaSpots[2].z);
            float floor = Height(h.x, h.y) + 0.3f;
            for (int ix = -3; ix <= 3; ix += 2)
                for (int iz = -3; iz <= 3; iz += 2)
                    At("MountainKit_int_floor_2x2", new Vector3(h.x + ix, floor, h.y + iz), Vector3.zero, true);
            float wall = floor - 0.05f;
            At("MountainKit_int_wall_4x4", new Vector3(h.x - 2f, wall, h.y + 4.4f), Vector3.zero, false);
            At("MountainKit_int_wall_4x4", new Vector3(h.x + 2f, wall, h.y + 4.4f), Vector3.zero, false);
            Vector2[] pillars = { new Vector2(-5.8f, 0.8f), new Vector2(5.8f, 0.8f), new Vector2(-4.6f, 4.4f), new Vector2(4.6f, 4.4f) };
            foreach (Vector2 p in pillars)
            {
                Vector2 at = h + p;
                float y = GroundMin(at.x, at.y, 0.5f) - 0.3f;
                float top = Top(At("stone_pillar", new Vector3(at.x, y, at.y), Vector3.zero, false), y + 2f);
                At("stone_pillar", new Vector3(at.x, top, at.y), Vector3.zero, false);
            }
            Brazier(h + new Vector2(-3.5f, -2.6f));
            Brazier(h + new Vector2(3.5f, -2.6f));
            Prop(new[] { "deepnorth_lantern_standing", "dvergrprops_lantern_standing" }, h + new Vector2(-3.2f, 3.4f), 0f, 0.05f);
            Prop(new[] { "deepnorth_lantern_standing", "dvergrprops_lantern_standing" }, h + new Vector2(3.2f, 3.4f), 0f, 0.05f);

            // The ice across the middle of each ramp (where its sides fall away, so there's no way round),
            // until the keeper below it falls.
            IceGate(new Vector2(6.5f, -5.2f), Story.GrimrDown);
            IceGate(new Vector2(-6.5f, 11.3f), Story.OrmrDown);

            // What runs the fights, at the islet's middle.
            PlaceNetworked(Story.ArenaStand, Vector3.zero, 0f);
        }

        static void Brazier(Vector2 at)
        {
            Prop(new[] { "MountainKit_brazier_blue", "MountainKit_brazier" }, at, 0f, 0.05f);
        }

        // Blocks of ice across a ramp's top and a wall nobody slips through, in a group of their own that
        // FinalArenaGate takes away once its keeper has fallen.
        static void IceGate(Vector2 at, string key)
        {
            GameObject gate = new GameObject("IoV_IceGate");
            gate.transform.SetParent(parent, false);
            GameObject ice = new GameObject("Ice");
            ice.transform.SetParent(gate.transform, false);
            ice.layer = LayerMask.NameToLayer("static_solid");
            float y = Height(at.x, at.y);
            BoxCollider wall = ice.AddComponent<BoxCollider>();
            wall.center = new Vector3(at.x, y + 1.5f, at.y);
            wall.size = new Vector3(6f, 3.2f, 1.6f);
            FinalArenaGate g = gate.AddComponent<FinalArenaGate>();
            g.key = key;
            g.middle = wall.center;
            Transform outer = parent;
            parent = ice.transform;
            for (int i = -1; i <= 1; i++)
                OnGround(tier.ShoreIce, new Vector2(at.x + i * 2.2f, at.y + (i == 0 ? 0.3f : 0f)), 60f + i * 50f, 3.2f, 0.2f);
            parent = outer;
        }
    }

    // An ice gate on the final arena: there while its keeper stands, gone once the keeper's fall is known (a
    // world key, so every client sees the same), with a burst of frost if it happens while someone watches.
    class FinalArenaGate : MonoBehaviour
    {
        [SerializeField] internal string key;
        [SerializeField] internal Vector3 middle; // in the islet's space
        bool open, seen;

        void OnEnable() { InvokeRepeating("Check", 0.5f, 1f); }
        void OnDisable() { CancelInvoke("Check"); }

        static readonly string[] breaking = { "vfx_ice_destroyed", "fx_frostcloud", "sfx_ice_destroyed", "sfx_rock_destroyed" };

        void Check()
        {
            bool now = !string.IsNullOrEmpty(key) && Story.Key(key);
            if (seen && now == open) return;
            Transform ice = transform.Find("Ice");
            if (ice != null) ice.gameObject.SetActive(!now);
            if (seen && now && ZNetScene.instance != null)
            {
                Vector3 at = transform.parent != null ? transform.parent.TransformPoint(middle) : middle;
                foreach (string name in breaking)
                {
                    GameObject fx = ZNetScene.instance.GetPrefab(name);
                    if (fx != null) Instantiate(fx, at, Quaternion.identity);
                }
            }
            open = now;
            seen = true;
        }
    }
}
