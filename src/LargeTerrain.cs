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
        [SerializeField] internal int biome;        // the land it belongs to (Heightmap.Biome), for the ground's colours

        internal static readonly List<BakedGround> Loaded = new List<BakedGround>();
        static readonly List<Heightmap> near = new List<Heightmap>();

        // Built while the location is registered, from the builder's height function.
        internal static void Bake(GameObject on, float reach, float landRadius, Heightmap.Biome biome, System.Func<float, float, float> height)
        {
            BakedGround ground = on.AddComponent<BakedGround>();
            ground.biome = (int)biome;
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

            // The lands whose ground the game draws partly from the paint mask's alpha (WorldGenerator's
            // GetAshlandsHeight, GetMistlandsHeight and GetDeepNorthHeight write it; the sea leaves it at 1):
            // the Ashlands' lava, the Mistlands' grass instead of their moss, the Deep North's bare ground
            // instead of snow. On their islets it's cleared, so the ground shows as the land's own. Paths and
            // other paint stay.
            static void Postfix(Heightmap __instance)
            {
                if (Loaded.Count == 0 || paintMask == null) return;
                Texture2D mask = paintMask.GetValue(__instance) as Texture2D;
                if (mask == null) return;
                Vector3 origin = __instance.transform.position;
                float scale = __instance.m_scale, mapHalf = __instance.m_width * scale * 0.5f;
                bool changed = false;
                Color[] pixels = null;
                foreach (BakedGround g in Loaded)
                {
                    if (g == null || !MaskedLand((Heightmap.Biome)g.biome)) continue;
                    Vector3 c = g.transform.position;
                    if (Mathf.Abs(c.x - origin.x) > mapHalf + g.land || Mathf.Abs(c.z - origin.z) > mapHalf + g.land) continue;
                    if (pixels == null) pixels = mask.GetPixels();
                    int w = mask.width, h = mask.height;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            float wx = origin.x - mapHalf + x * (mapHalf * 2f) / Mathf.Max(1, w - 1);
                            float wz = origin.z - mapHalf + y * (mapHalf * 2f) / Mathf.Max(1, h - 1);
                            if (Utils.DistanceXZ(c, new Vector3(wx, 0f, wz)) > g.land) continue;
                            int i = y * w + x;
                            if (pixels[i].a == 0f) continue;
                            pixels[i].a = 0f;
                            changed = true;
                        }
                }
                if (!changed) return;
                mask.SetPixels(pixels);
                mask.Apply(false);
            }
        }

        static readonly System.Reflection.FieldInfo paintMask = AccessTools.Field(typeof(Heightmap), "m_paintMask");

        static bool MaskedLand(Heightmap.Biome biome)
        {
            return biome == Heightmap.Biome.AshLands || biome == Heightmap.Biome.Mistlands || biome == Heightmap.Biome.DeepNorth;
        }

        // The islet's ground is painted as its own land (ash, snow, swamp mud...), not as the sea it stands in:
        // the terrain shader picks its textures by these colours. Faded out towards the sea.
        static readonly System.Reflection.FieldInfo renderMesh = AccessTools.Field(typeof(Heightmap), "m_renderMesh");

        [HarmonyPatch(typeof(Heightmap), "RebuildRenderMesh")]
        static class ColourPatch
        {
            static void Postfix(Heightmap __instance)
            {
                if (Loaded.Count == 0 || renderMesh == null) return;
                Vector3 origin = __instance.transform.position;
                float mapHalf = __instance.m_width * __instance.m_scale * 0.5f;
                bool any = false;
                foreach (BakedGround g in Loaded)
                    if (g != null && g.biome != 0 && Mathf.Abs(g.transform.position.x - origin.x) < mapHalf + g.land &&
                        Mathf.Abs(g.transform.position.z - origin.z) < mapHalf + g.land) { any = true; break; }
                if (!any) return;
                Mesh mesh = renderMesh.GetValue(__instance) as Mesh;
                if (mesh == null) return;
                Vector3[] verts = mesh.vertices;
                Color32[] colours = mesh.colors32;
                if (colours == null || colours.Length != verts.Length) return;
                bool changed = false;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 p = __instance.transform.TransformPoint(verts[i]);
                    foreach (BakedGround g in Loaded)
                    {
                        if (g == null || g.biome == 0) continue;
                        float d = Utils.DistanceXZ(g.transform.position, p);
                        if (d > g.land) continue;
                        float w = Mathf.Clamp01((g.land - d) / 12f);
                        colours[i] = Color32.Lerp(colours[i], Heightmap.GetBiomeColor((Heightmap.Biome)g.biome), w);
                        changed = true;
                        break;
                    }
                }
                if (changed) mesh.colors32 = colours;
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
