using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // A large islet of the game's own terrain instead of our ground mesh, so the hoe, the pickaxe and the
    // cultivator work on it and players can settle there. The islet's shape is the same one the mesh is made
    // from (IsletBuilder.Height: the plateau, the cliff side, the slopes, the beach, a dug-in dungeon's
    // shaft), worked out once when the location is built and kept on it as a grid of heights. Whenever the
    // game builds a heightmap under it, the grid is written in, before the game's own modifiers and the
    // players' edits go on (so those work on it as on any land). It only ever raises the sea floor, never
    // digs it. Every client builds the same ground from the same location (the mod is on every client).
    // (The idea of a location raising real terrain is from Shuiei's Valheim World Editor, MIT; none of its
    // code is used.)
    class BakedGround : MonoBehaviour
    {
        [SerializeField] internal float[] heights;  // over the sea, row by row (z), from -half to +half
        [SerializeField] internal int size;         // points along each side
        [SerializeField] internal float half;       // how far the grid reaches from the middle
        [SerializeField] internal float step = 1f;  // metres between points
        [SerializeField] internal float land;       // how far out the islet is land (plants grow within it)

        internal static readonly List<BakedGround> Loaded = new List<BakedGround>();
        static readonly List<Heightmap> near = new List<Heightmap>();

        // Built while the location is registered, from the builder's height function.
        internal static void Bake(GameObject on, float reach, float landRadius, System.Func<float, float, float> height)
        {
            BakedGround ground = on.AddComponent<BakedGround>();
            ground.step = 1f;
            ground.half = Mathf.Ceil(reach);
            ground.size = Mathf.RoundToInt(ground.half * 2f / ground.step) + 1;
            ground.land = landRadius;
            ground.heights = new float[ground.size * ground.size];
            for (int iz = 0; iz < ground.size; iz++)
                for (int ix = 0; ix < ground.size; ix++)
                    ground.heights[iz * ground.size + ix] = height(-ground.half + ix * ground.step, -ground.half + iz * ground.step);
        }

        void OnEnable()
        {
            if (heights == null || heights.Length == 0) return;
            Loaded.Add(this);
            Poke();
        }

        void OnDisable()
        {
            if (Loaded.Remove(this)) Poke();
        }

        // The heightmaps under it are built again, with it (or without it, when it goes).
        void Poke()
        {
            near.Clear();
            Heightmap.FindHeightmap(transform.position, half * 1.42f + 4f, near);
            foreach (Heightmap h in near) if (h != null) h.Poke(0, false);
        }

        internal static bool Covers(Vector3 p)
        {
            foreach (BakedGround g in Loaded)
                if (g != null && Utils.DistanceXZ(g.transform.position, p) < g.land) return true;
            return false;
        }

        // The islet's height over the sea at a point given relative to its middle (turned with it), from the
        // grid; NaN outside it.
        float Sample(float x, float z)
        {
            float gx = (x + half) / step, gz = (z + half) / step;
            if (gx < 0f || gz < 0f || gx > size - 1 || gz > size - 1) return float.NaN;
            int x0 = Mathf.Min((int)gx, size - 2), z0 = Mathf.Min((int)gz, size - 2);
            float tx = gx - x0, tz = gz - z0;
            float a = heights[z0 * size + x0], b = heights[z0 * size + x0 + 1];
            float c = heights[(z0 + 1) * size + x0], d = heights[(z0 + 1) * size + x0 + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        // Raises this heightmap's ground to the islet's where the islet's is higher.
        internal void Apply(Heightmap map)
        {
            Vector3 origin = map.transform.position;
            int width = map.m_width;
            float scale = map.m_scale;
            float mapHalf = width * scale * 0.5f;
            Vector3 c = transform.position;
            float reach = half * 1.42f; // the grid turned any way
            if (Mathf.Abs(c.x - origin.x) > mapHalf + reach || Mathf.Abs(c.z - origin.z) > mapHalf + reach) return;
            Quaternion back = Quaternion.Inverse(transform.rotation);
            float sea = c.y; // the location sits on the water
            int raised = 0;
            for (int y = 0; y <= width; y++)
            {
                float wz = origin.z + (y - width / 2) * scale;
                for (int x = 0; x <= width; x++)
                {
                    float wx = origin.x + (x - width / 2) * scale;
                    Vector3 local = back * new Vector3(wx - c.x, 0f, wz - c.z);
                    float h = Sample(local.x, local.z);
                    if (float.IsNaN(h)) continue;
                    float want = sea + h;
                    if (want <= map.GetHeight(x, y)) continue;
                    map.SetHeight(x, y, want);
                    raised++;
                }
            }
            if (raised > 0) Plugin.Debug("Large islet ground: raised " + raised + " points of the heightmap at " + origin.ToString("F0"));
        }

        [HarmonyPatch(typeof(Heightmap), "ApplyModifiers")]
        static class GroundPatch
        {
            static void Prefix(Heightmap __instance)
            {
                if (Loaded.Count == 0) return;
                foreach (BakedGround ground in Loaded)
                    if (ground != null) ground.Apply(__instance);
            }
        }

        // A plant on a large islet grows as on land: the islet is Ocean to the game, where no crop grows.
        [HarmonyPatch(typeof(Plant), "UpdateHealth")]
        static class PlantPatch
        {
            static void Prefix(Plant __instance)
            {
                if ((__instance.m_biome & Heightmap.Biome.Ocean) != 0 || !Covers(__instance.transform.position)) return;
                __instance.m_biome |= Heightmap.Biome.Ocean;
            }
        }
    }
}
