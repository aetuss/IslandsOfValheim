using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The hidden islet as the tale's last climb (from 2.0): a dark crag far out in the Deep North waters, of
    // the game's own terrain, with the fights in hollows cut down into it and narrow gorges between them, so
    // no place can be seen from the one before. From a hidden cove a cleft leads into Grímr's hollow, where an
    // old stone ship-setting lies; a gorge winds up to Ormr's, where his ship lies frozen into the rock; another
    // up to the summit, open to the sky and the sea, where Hafdís keeps her hall. Ice closes each gorge until
    // the keeper below it falls; each keeper comes out of the mist when it's their turn (FinalArena). The old
    // hidden islet (IsletStory.HiddenIslet) stays known for the worlds that have it already built.
    static partial class IsletBuilder
    {
        // The crag: an oval, its rim this high over the sea; towards the summit it comes down to the summit's
        // own level, so the top is open.
        const float CragAx = 30f, CragAz = 48f, CragCz = -4f, CragRim = 19f;
        const float ArenaReach = 60f, ArenaLand = 50f;
        internal const float ArenaTop = CragRim + 2f; // over the rim, for iov_goto

        // The hollows: the cove, then each keeper's. Middle (x, z), radius, floor over the sea.
        internal static readonly Vector4[] ArenaHollows =
        {
            new Vector4(0f, -42f, 7f, 1.2f),    // the cove
            new Vector4(0f, -20f, 10f, 3f),     // Grímr's: the ship-setting
            new Vector4(-2f, 6f, 9f, 7.5f),     // Ormr's: his cultists' circle
            new Vector4(0f, 32f, 12.5f, 13f),   // Hafdís's: the summit
        };

        // Where each keeper comes: Grímr out of the ice gate in the far wall of his hollow, Ormr in the middle
        // of his circle, Hafdís down out of the sky over her summit (FinalArena brings her in high).
        internal static readonly Vector2[] ArenaSpots = { new Vector2(0f, -13.5f), new Vector2(-3f, 9.5f), new Vector2(0f, 32f) };

        // The gorges: points along each (x, z, floor).
        static readonly Vector3[][] Gorges =
        {
            new[] { new Vector3(0f, -37f, 1.4f), new Vector3(0f, -29f, 3f) },
            new[] { new Vector3(9f, -16f, 3f), new Vector3(15f, -9f, 4.5f), new Vector3(13f, 0f, 6.5f), new Vector3(6f, 4f, 7.5f) },
            new[] { new Vector3(-10f, 9f, 7.5f), new Vector3(-15f, 16f, 9.5f), new Vector3(-14f, 23f, 12f), new Vector3(-9f, 28f, 13f) },
        };
        static readonly float[] GorgeWidths = { 4.5f, 3.6f, 3.6f };

        static float CragNoise(float x, float z, float scale, float shift)
        {
            return (Mathf.PerlinNoise(x * scale + shift, z * scale + shift * 0.7f) - 0.5f) * 2f;
        }

        // The crag before anything is cut into it.
        static float CragHeight(float x, float z)
        {
            float ex = x / CragAx, ez = (z - CragCz) / CragAz;
            float angle = Mathf.Atan2(ez, ex);
            float e = Mathf.Sqrt(ex * ex + ez * ez) * (1f + 0.06f * Mathf.Sin(angle * 3f + 1.3f) + 0.04f * Mathf.Sin(angle * 7f + 0.4f));
            float top = CragRim + 2.5f * CragNoise(x, z, 0.09f, 31f);
            top = Mathf.Lerp(top, ArenaHollows[3].w + 0.5f + 0.6f * CragNoise(x, z, 0.25f, 77f), Mathf.Clamp01((z - 25f) / 8f));
            if (e <= 0.82f) return top;
            if (e <= 1f)
            {
                float t = (e - 0.82f) / 0.18f;
                return Mathf.Lerp(top, -1.5f, t * t * (3f - 2f * t)) + 1.5f * CragNoise(x, z, 0.3f, 13f) * Mathf.Sin(Mathf.PI * t);
            }
            float d = (e - 1f) * CragAx; // metres past the shore, about
            return d <= 6f ? -1.5f - d * 0.8f : Mathf.Max(-30f, -6f - (d - 6f) * 4f);
        }

        internal static float ArenaHeight(float x, float z)
        {
            float h = CragHeight(x, z);
            float cut = float.MaxValue;
            for (int i = 0; i < ArenaHollows.Length; i++)
            {
                Vector4 o = ArenaHollows[i];
                float d = Mathf.Sqrt((x - o.x) * (x - o.x) + (z - o.y) * (z - o.y));
                float r = o.z * (1f + 0.08f * CragNoise(x, z, 0.3f, 5f + i * 17f));
                cut = Mathf.Min(cut, o.w + Mathf.Max(0f, d - r) * 3.5f); // walls nobody climbs
            }
            for (int g = 0; g < Gorges.Length; g++)
            {
                Vector3[] p = Gorges[g];
                float best = float.MaxValue, floor = 0f;
                for (int i = 0; i + 1 < p.Length; i++)
                {
                    float t;
                    float d = SegmentDistance(x, z, p[i], p[i + 1], out t);
                    if (d < best) { best = d; floor = Mathf.Lerp(p[i].z, p[i + 1].z, t); }
                }
                cut = Mathf.Min(cut, floor + Mathf.Max(0f, best - GorgeWidths[g] * 0.5f) * 4f);
            }
            if (cut < h) h = cut + 0.2f * CragNoise(x, z, 0.8f, 3f);
            return h;
        }

        // How far (x, z) is from the segment a-b (their x and y), and where along it (t, 0..1) is nearest.
        static float SegmentDistance(float x, float z, Vector3 a, Vector3 b, out float t)
        {
            float dx = b.x - a.x, dz = b.y - a.y, l = dx * dx + dz * dz;
            t = l > 0f ? Mathf.Clamp01(((x - a.x) * dx + (z - a.y) * dz) / l) : 0f;
            float px = a.x + dx * t - x, pz = a.y + dz * t - z;
            return Mathf.Sqrt(px * px + pz * pz);
        }

        // How far out the land ends in a direction (degrees, as Polar): for the jetty and the wreck in the cove.
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
            config.ExteriorRadius = ArenaReach;
            config.MinAltitude = -1000f;
            config.MaxAltitude = -8f;
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 1000f;
            config.MinDistance = tier.MinDistance + 600f;
            config.MaxDistance = tier.MaxDistance;
            config.Group = locationName;
            config.MinDistanceFromSimilar = 600f;
            Location location = root.AddComponent<Location>();
            location.m_applyRandomDamage = false;
            location.m_exteriorRadius = ArenaReach;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + locationName + " (" + StorySpots + " possible spots, one is kept)");
        }

        static void ArenaParts()
        {
            GameObject ground = new GameObject("IoV_BakedGround");
            ground.transform.SetParent(root.transform, false);
            BakedGround.Bake(ground, ArenaReach, ArenaLand, tier.Biome, ArenaHeight);

            // Dark stone underfoot in the hollows and the gorges (the snow stays up on the rim), so the
            // keepers' frost and light show against it.
            for (int i = 1; i < ArenaHollows.Length; i++)
            {
                Vector4 o = ArenaHollows[i];
                Paved(o.x, o.y, o.z * 0.55f);
                for (int k = 0; k < 6; k++)
                {
                    Vector2 p = new Vector2(o.x, o.y) + Polar(k * 60f + 15f, o.z * 0.62f);
                    Paved(p.x, p.y, o.z * 0.45f);
                }
            }
            for (int g = 0; g < Gorges.Length; g++)
                for (int i = 0; i + 1 < Gorges[g].Length; i++)
                {
                    Vector3 a = Gorges[g][i], b = Gorges[g][i + 1];
                    float length = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
                    for (float s = 0f; s <= length; s += 2.5f)
                    {
                        float t = length > 0f ? s / length : 0f;
                        Paved(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), GorgeWidths[g] * 0.75f);
                    }
                }

            // The cove: a beach between the rocks, an old jetty, and the stone that warns, facing the sea.
            PlaceDock(266f);
            PlaceNetworked(new[] { "IoV_StoryStone_Hidden" }, new Vector2(3.2f, -39f), 0f, 0f); // its face to the sea, where you come from
            Brazier(new Vector2(-3f, -36.5f));
            Brazier(new Vector2(3f, -36.5f));
            quietProps = true;
            Prop(new[] { "FrozenSkeleton_Pose1" }, new Vector2(-3.5f, -40f), 60f, 0.05f);
            quietProps = false;

            // Grímr's hollow: an old ship-setting of standing stones, the dead who came before, and in the far
            // wall a gate of ice between two blue fires, where he comes out (ArenaSpots).
            Vector4 grimr = ArenaHollows[1];
            Vanilla(new[] { "ShipSetting01" }, new Vector2(grimr.x, grimr.y), 0f, 0f);
            quietProps = true;
            Prop(new[] { "FrozenSkeleton_Pose2" }, new Vector2(grimr.x - 5.5f, grimr.y - 4f), 30f, 0.05f);
            Prop(new[] { "FrozenSkeleton_Pose1" }, new Vector2(grimr.x + 6f, grimr.y + 2.5f), 200f, 0.05f);
            Lying(new[] { "SwordSilver", "SwordIron" }, new Vector2(grimr.x - 4.4f, grimr.y - 5.2f), 70f, 1.4f);
            quietProps = false;
            Vector2 door = new Vector2(grimr.x, grimr.y + grimr.z); // the middle of the far wall
            OnGround(tier.ShoreIce, door + new Vector2(0f, 0.6f), 10f, 5f, 0.15f);
            OnGround(tier.ShoreIce, door + new Vector2(-2.8f, -1.2f), 120f, 3.4f, 0.15f);
            OnGround(tier.ShoreIce, door + new Vector2(2.8f, -1.2f), 250f, 3.4f, 0.15f);
            Brazier(door + new Vector2(-4.6f, -2.8f));
            Brazier(door + new Vector2(4.6f, -2.8f));

            // Ormr's hollow: his cultists' circle of stones, his crew knelt round it frozen, fire at the way in;
            // he stands up in its middle (ArenaSpots). It's open towards the gorge he's come by.
            Vector4 ormr = ArenaHollows[2];
            Vector2 ring = ArenaSpots[1];
            float[] stones = { 25f, 70f, 115f, 160f, 205f, 250f };
            for (int i = 0; i < stones.Length; i++)
                PropAtMost(new[] { i % 2 == 0 ? "marker01" : "marker02" }, ring + Polar(stones[i], 4f), 90f - stones[i], 0.1f, 1.6f);
            quietProps = true;
            Prop(new[] { "FrozenSkeleton_Pose1" }, ring + Polar(140f, 5.6f), 320f, 0.05f);
            Prop(new[] { "FrozenSkeleton_Pose2" }, ring + Polar(220f, 5.6f), 40f, 0.05f);
            Prop(new[] { "FrozenSkeleton_Pose1" }, ring + Polar(95f, 5.8f), 270f, 0.05f);
            quietProps = false;
            Brazier(ring + Polar(15f, 5.6f));
            Brazier(ring + Polar(300f, 5.6f));
            Ice(new Vector2(ormr.x + 7f, ormr.y + 6f), 250f, 2f);

            // Hafdís's summit: a crown of tall pillars round a wide floor, open to the sea on the north, where
            // her ship's dragon head looks out; ice grown up between the pillars, blue fire. She comes down out
            // of the sky into its middle. All of it stone that doesn't break (the fight up here is in the air).
            Vector4 top = ArenaHollows[3];
            Vector2 h = new Vector2(top.x, top.y);
            // (The way in from the west gorge, about 205 degrees, between the pillars at 175 and 235.)
            float[] crown = { 0f, 35f, 145f, 175f, 235f, 280f, 320f };
            foreach (float deg in crown) StonePillar(h + Polar(deg, 10f), 3);
            float[] crystals = { 18f, 160f, 300f };
            foreach (float deg in crystals) OnGround(tier.ShoreIce, h + Polar(deg, 11f), deg * 3f, 4.5f, 0.15f);
            // (The summit's floor ends about 6 m north of its middle, where the crag falls to the sea.)
            OnGround(new[] { "shipwreck_karve_dragonhead", "shipwreck_vikingship_front" }, h + new Vector2(0f, 5.8f), 0f, 5f, 0.1f);
            float[] fires = { 30f, 150f, 210f, 270f, 330f };
            foreach (float deg in fires) Brazier(h + Polar(deg, 6f));

            // Snow pines up on the rim, seen from the sea.
            Vector2[] trees = { new Vector2(-17f, -20f), new Vector2(17f, -24f), new Vector2(-18f, 0f), new Vector2(20f, 8f), new Vector2(-16f, 32f), new Vector2(15f, -38f) };
            for (int i = 0; i < trees.Length; i++)
            {
                if (ArenaHeight(trees[i].x, trees[i].y) < 12f) continue;
                int k = i;
                Optional(0.85f, () => OnGround(k % 2 == 0 ? tier.Trees : tier.SmallTrees, trees[k], k * 71f, 0f, 0.05f, false));
            }

            // The ice across each gorge, until the keeper below it falls.
            GorgeGate(Gorges[1][1], Gorges[1][2], 0.5f, Story.GrimrDown);
            GorgeGate(Gorges[2][1], Gorges[2][2], 0.5f, Story.OrmrDown);

            // What runs the fights, at the islet's middle.
            PlaceNetworked(Story.ArenaStand, Vector3.zero, 0f);
        }

        static void Brazier(Vector2 at)
        {
            Prop(new[] { "MountainKit_brazier_blue", "MountainKit_brazier" }, at, 0f, 0.05f);
        }

        // The game's own paving, painted on its terrain (nothing reshaped).
        static void Paved(float x, float z, float radius)
        {
            GameObject go = new GameObject("IoV_Paint");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, Height(x, z), z);
            TerrainModifier modifier = go.AddComponent<TerrainModifier>();
            modifier.m_level = false;
            modifier.m_smooth = false;
            modifier.m_paintCleared = true;
            modifier.m_paintHeightCheck = false;
            modifier.m_paintType = TerrainModifier.PaintType.Paved;
            modifier.m_paintRadius = radius;
        }

        // A pillar of stone_pillar blocks, static: one stands on the other.
        static void StonePillar(Vector2 at, int blocks)
        {
            float bottom = GroundMin(at.x, at.y, 0.5f) - 0.3f;
            for (int i = 0; i < blocks; i++)
            {
                GameObject go = Copy(new[] { "stone_pillar" }, Vector3.zero, 0f, true);
                if (go == null) return;
                Bounds b = LocalBounds(go);
                Move(go, b, new Vector3(at.x, bottom, at.y), false);
                bottom += b.size.y - 0.05f;
            }
        }

        // Ice across a gorge at t along its segment a-b, and a wall nobody slips through, in a group of their
        // own that FinalArenaGate takes away once its keeper has fallen.
        static void GorgeGate(Vector3 a, Vector3 b, float t, string key)
        {
            Vector2 at = new Vector2(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t));
            Vector2 along = new Vector2(b.x - a.x, b.y - a.y).normalized;
            Vector2 across = new Vector2(along.y, -along.x);
            float yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
            GameObject gate = new GameObject("IoV_IceGate");
            gate.transform.SetParent(parent, false);
            GameObject ice = new GameObject("Ice");
            ice.transform.SetParent(gate.transform, false);
            GameObject wall = new GameObject("IoV_IceWall");
            wall.transform.SetParent(ice.transform, false);
            wall.layer = LayerMask.NameToLayer("static_solid");
            float y = Height(at.x, at.y);
            wall.transform.localPosition = new Vector3(at.x, y + 1.6f, at.y);
            wall.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            BoxCollider box = wall.AddComponent<BoxCollider>();
            box.size = new Vector3(6f, 3.4f, 1.6f);
            FinalArenaGate g = gate.AddComponent<FinalArenaGate>();
            g.key = key;
            g.middle = wall.transform.localPosition;
            Transform outer = parent;
            parent = ice.transform;
            for (int i = -1; i <= 1; i++)
                OnGround(tier.ShoreIce, at + across * (i * 1.6f) + along * (i == 0 ? 0.3f : 0f), yaw + 60f + i * 50f, 3f, 0.2f);
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
