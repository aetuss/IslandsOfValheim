using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The hidden islet as the tale's last climb (from 2.0): a dark crag far out in the Deep North waters, of
    // the game's own terrain, with the fights in hollows cut down into it and narrow gorges between them, so
    // no place can be seen from the one before. Each room is laid out like the game's own boss places (a focus
    // in the middle, a frame of great stones round an open floor, nothing in the way), on the true sizes of the
    // game's pieces (measured in the game):
    // - a hidden cove, where the warning stone stands;
    // - Grímr's hollow: a jötun gate in the far wall, which he comes out of, four great stones round the floor,
    //   and the frozen remains of giants lying on the rim above;
    // - Ormr's hollow: a mountain altar in the middle, which he stands up on, fire and his frozen crew round it;
    // - the summit, open to the sky and the sea: a ring of great stones, her ship's dragon head over the cliff;
    //   Hafdís comes down out of the sky into it.
    // Ice closes each gorge until the keeper below it falls (FinalArena). The old hidden islet
    // (IsletStory.HiddenIslet) stays known for the worlds that have it already built.
    static partial class IsletBuilder
    {
        // The crag: an oval, its rim this high over the sea; towards the summit it comes down to the summit's
        // own level, so the top is open.
        // (Longer to the north than to the south, so the summit's floor reaches 7 m past its middle before the
        // crag falls to the sea.)
        const float CragAx = 30f, CragAz = 54f, CragCz = -2f, CragRim = 19f;
        const float ArenaReach = 64f, ArenaLand = 54f;
        internal const float ArenaTop = CragRim + 2f; // over the rim, for iov_goto

        // The hollows: the cove, then each keeper's. Middle (x, z), radius, floor over the sea.
        internal static readonly Vector4[] ArenaHollows =
        {
            new Vector4(0f, -46f, 7f, 1.2f),     // the cove
            new Vector4(0f, -21f, 12.5f, 3f),    // Grímr's
            new Vector4(-2f, 6f, 9.5f, 7.5f),    // Ormr's
            new Vector4(0f, 35f, 14f, 13f),      // Hafdís's: the summit
        };

        // Where each keeper comes: Grímr out of the jötun gate, Ormr in the middle of his ring of stone, Hafdís
        // down out of the sky (FinalArena brings Ormr in on the ring and Hafdís in high).
        internal static readonly Vector2[] ArenaSpots = { new Vector2(0f, -13.5f), new Vector2(-2f, 6f), new Vector2(0f, 35f) };
        internal static readonly float[] ArenaSpotsAbove = { 0.4f, 0.9f, 10f };

        // The gorges: points along each (x, z, floor).
        static readonly Vector3[][] Gorges =
        {
            new[] { new Vector3(0f, -41f, 1.4f), new Vector3(0f, -32.5f, 3f) },
            new[] { new Vector3(11f, -17f, 3f), new Vector3(16f, -10f, 4.5f), new Vector3(14f, -1f, 6.5f), new Vector3(7f, 3f, 7.5f) },
            new[] { new Vector3(-11f, 9f, 7.5f), new Vector3(-16f, 15f, 9.5f), new Vector3(-15f, 22f, 12f), new Vector3(-10f, 27f, 13f) },
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
            top = Mathf.Lerp(top, ArenaHollows[3].w + 0.5f + 0.6f * CragNoise(x, z, 0.25f, 77f), Mathf.Clamp01((z - 27f) / 8f));
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

        // Work in progress (not for release): the arena without its summit, unturned, to lay out the summit's
        // draft on with the dev tool (iov_goto draft_arena; drafts/Arena_Summit.json).
        internal const string ArenaBase = "IoVTest_Base_Arena";
        static bool arenaBare;

        static void FinalArena()
        {
            FinalArenaOf(Story.Arena, false);
            arenaBare = true;
            try { FinalArenaOf(ArenaBase, true); }
            finally { arenaBare = false; }
        }

        static void FinalArenaOf(string name, bool bare)
        {
            tier = IsletTier.All[6];
            locationName = name;
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
            config.Quantity = bare ? 0 : StorySpots;
            config.Priotized = !bare;
            config.Unique = !bare;
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
            if (bare) Plugin.Log.LogInfo("Registered test location " + locationName + " (the arena without its summit, for its draft)");
            else Plugin.Log.LogInfo("Registered location " + locationName + " (" + StorySpots + " possible spots, one is kept)");
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

            // The game's structures the rooms take pieces from, each copied once for this islet.
            Dictionary<string, GameObject> sources = new Dictionary<string, GameObject>();
            try { ArenaRooms(sources); }
            finally { foreach (GameObject source in sources.Values) if (source != null) Object.DestroyImmediate(source); }

            // Snow pines up on the rim, seen from the sea (clear of the giants).
            Vector2[] trees = { new Vector2(-18f, 0f), new Vector2(20f, 8f), new Vector2(-20f, 10f), new Vector2(20f, 20f), new Vector2(-16f, 32f) };
            for (int i = 0; i < trees.Length; i++)
            {
                if (ArenaHeight(trees[i].x, trees[i].y) < 12f) continue;
                int k = i;
                Optional(0.85f, () => OnGround(k % 2 == 0 ? tier.Trees : tier.SmallTrees, trees[k], k * 71f, 0f, 0.05f, false));
            }

            // (The bare arena, for the draft, has no gates and nobody runs its fights.)
            if (arenaBare) return;

            // The ice across each gorge, until the keeper below it falls.
            GorgeGate(Gorges[1][1], Gorges[1][2], 0.5f, Story.GrimrDown);
            GorgeGate(Gorges[2][1], Gorges[2][2], 0.5f, Story.OrmrDown);

            // What runs the fights, at the islet's middle.
            PlaceNetworked(Story.ArenaStand, Vector3.zero, 0f);
        }

        // The rooms, as planned (scratchpad plan "crag2"): every piece where it stands clear of the way through.
        static void ArenaRooms(Dictionary<string, GameObject> sources)
        {
            // The cove: a beach between the rocks, an old jetty, and the stone that warns, its face to the sea.
            PlaceDock(266f);
            PlaceNetworked(new[] { "IoV_StoryStone_Hidden" }, new Vector2(3.2f, -43f), 0f, 0f);
            Brazier(new Vector2(-3f, -40.5f));
            Brazier(new Vector2(3f, -40.5f));

            // Grímr's hollow: the jötun gate in the far wall, blue fire either side of it; four great stones on
            // the diagonals framing an open floor; the frozen remains of giants on the rim either side, above.
            Vector4 g = ArenaHollows[1];
            Vector2 gc = new Vector2(g.x, g.y);
            // (The gate is set back into the far wall, its face where the wall meets the floor, so it stands in
            // the rock; the great stones stand back against the walls: two as its posts, two either side of the way
            // in from the cove. The way on, to the east, stays open.)
            StaticPart(sources, "MorkBorg", "exterior/Morkhalla_jotun_gate", new Vector2(0f, -7.6f), 0f, 1.25f, false, -0.4f);
            Brazier(new Vector2(-2.8f, -12f));
            Brazier(new Vector2(2.8f, -12f));
            foreach (float deg in new[] { 55f, 125f, 240f, 300f })
                OnGround(new[] { "Rock_7_deepnorth", "Rock_4_deepnorth" }, gc + Polar(deg, deg < 180f ? 11.4f : 10.5f), deg * 2f + 30f, 5f, 0.1f);
            StaticPart(sources, "DN_gammeltrollFrac01", "TrollFrost_Frac_legs", new Vector2(19f, -28f), 0f, 1f, true, 4.8f);
            StaticPart(sources, "DN_gammeltrollFrac02", "TrollFrost_Frac_arm", new Vector2(-19f, -16f), 90f, 1f, true, 4.6f);

            // Ormr's hollow: a carved ring of dark stone filling the floor (the Fader's arena's middle, 15 m across,
            // half a metre high), a glowing stone altar at its far side; he stands up in the middle of the ring.
            // Blue fire on the diagonals round it; his frozen crew knelt facing it. (Moder's altar was tried: only
            // the top of a mountain, it sank into the floor.)
            Vector4 o = ArenaHollows[2];
            Vector2 oc = new Vector2(o.x, o.y);
            StaticPart(sources, "FaderLocation", "center", oc, 0f, 0.88f, true, 0.45f);
            StaticPart(sources, "GDKing", "offeraltar_gdking", oc + new Vector2(0f, 5.6f), 180f, 1.2f, false, 0.3f);
            foreach (float deg in new[] { 45f, 135f, 225f, 315f }) Brazier(oc + Polar(deg, 7.8f));
            quietProps = true;
            foreach (float deg in new[] { 90f, 200f, 270f })
                Prop(new[] { deg == 200f ? "FrozenSkeleton_Pose2" : "FrozenSkeleton_Pose1" }, oc + Polar(deg, 7.6f), 270f - deg, 0.05f);
            quietProps = false;

            // Hafdís's summit: laid out by eye in the game with the dev tool, on the bare arena (iov_goto
            // draft_arena), and read from its draft. (Tried first, blind: a ring of rough great stones, the
            // Mistlands' marble statues and columns, the Fader's bells: none of them looked right.)
            if (!arenaBare) SummitDraft();
        }

        // The summit's draft (drafts/Arena_Summit.json), in the arena's own space. Everything in it stands as it
        // is and never breaks, whatever the fight does: the game's structures and pieces are copied without
        // their scripts. Its KEEPER mark is where Hafdís comes down.
        const string SummitDraftId = "Arena_Summit";

        static void SummitDraft()
        {
            List<DraftEntry> draft = ReadDraft(SummitDraftId);
            if (draft == null) { Plugin.Log.LogWarning(locationName + ": no draft " + SummitDraftId + "; the summit is bare"); return; }
            draftLeft = 0;
            BuildEntries(draft, SummitEntry);
            Plugin.Debug(locationName + ": summit draft, " + draft.Count + " lines, " + draftLeft + " left out");
        }

        static void SummitEntry(DraftEntry e, Dictionary<string, GameObject> sources)
        {
            Vector2 at = new Vector2(e.X, e.Z);
            switch (e.Kind)
            {
                case "marker":
                    if (e.Prefab == "KEEPER") ArenaSpots[2] = at;
                    else { draftLeft++; Plugin.Log.LogWarning(locationName + ": the summit's draft has a mark " + e.Prefab + ", left out"); }
                    return;
                case "paint":
                    Paint(e);
                    return;
                case "vanilla":
                    GameObject group = Vanilla(new[] { e.Prefab }, at, e.Yaw, e.Above);
                    if (group != null) Strip(group);
                    return;
                case "locpart":
                    GameObject part = DraftPartObject(e, sources);
                    if (part != null) Strip(part); else draftLeft++;
                    return;
                case "creature":
                case "level":
                    draftLeft++;
                    return;
            }
            GameObject prefab = PlantOrSelf(e.Prefab);
            if (prefab == null) { missing++; Plugin.Log.LogWarning(locationName + ": the summit's draft has " + e.Prefab + ", which doesn't exist"); return; }
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            Strip(go);
            go.transform.localPosition = new Vector3(e.X, Height(e.X, e.Z) + e.Above, e.Z);
            go.transform.localRotation = e.Rot;
            if (e.Scale != Vector3.one) go.transform.localScale = e.Scale;
            built++;
        }

        // A piece of one of the game's structures (path inside the location), copied without its scripts so it
        // stands as it is and never breaks, turned yaw more than it was there and scaled; its bottom (or, byTop,
        // its top) y metres over the ground at its middle. Logged with where it ended up, to check the layout.
        static GameObject StaticPart(Dictionary<string, GameObject> sources, string location, string path, Vector2 at, float yaw, float scale, bool byTop, float y)
        {
            GameObject source;
            if (!sources.TryGetValue(location, out source))
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
                sources[location] = source;
            }
            Transform part = source != null ? source.transform.Find(path) : null;
            if (part == null)
            {
                missing++;
                Plugin.Log.LogWarning(locationName + ": no " + location + "/" + path);
                return null;
            }
            GameObject go = Object.Instantiate(part.gameObject, parent, false);
            go.name = part.name;
            Strip(go);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * (Quaternion.Inverse(source.transform.rotation) * part.rotation);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = part.lossyScale * scale;
            Bounds b = LocalBounds(go);
            Move(go, b, new Vector3(at.x, Height(at.x, at.y) + y, at.y), byTop);
            Bounds placed = LocalBounds(go);
            built++;
            Plugin.Log.LogInfo(locationName + ": " + location + "/" + path + " placed at " + at.ToString("F1") + ", spans x " + placed.min.x.ToString("F1") + ".." + placed.max.x.ToString("F1") +
                " z " + placed.min.z.ToString("F1") + ".." + placed.max.z.ToString("F1") + " y " + placed.min.y.ToString("F1") + ".." + placed.max.y.ToString("F1") + " (ground " + Height(at.x, at.y).ToString("F1") + ")");
            return go;
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
