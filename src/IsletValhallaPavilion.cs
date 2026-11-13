using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Open pavilions over Andhrímnir's kitchen and the Brim Folk's feast (the user's picture: a garden pavilion,
    // posts and beams, a low roof sloping on all four sides). In none of the hall's or the high seat's things
    // (their darkwood poles, beams and shingles): posts and beams of ashwood (the Ashlands' red wood), the roof
    // thatch (the game's straw roof, 26°), its hips of the thatch's own outer corners.
    //
    // The roof is laid in rings from the eaves up, as one builds it: each ring a row of straight pieces along
    // each side and an outer corner piece at each corner, the next ring a piece's run in and its rise up, till
    // the sides meet (a point, over a square; a ridge, over a longer one, a beam along it). Every piece is put
    // by its own measured size (bounds), turned by its own slope (which way its collider tilts) and, for the
    // corners, by where its highest snap point is (the hip's top): nothing guessed.
    static partial class IsletBuilder
    {
        static readonly Dictionary<string, string> found = new Dictionary<string, string>();

        // A prefab of the game's by the first of the names that exists, else the first whose name has all of
        // parts (any case), looked for among everything the game has.
        static string Prefab(string[] names, params string[] parts)
        {
            string key = string.Join("|", names) + "#" + string.Join("|", parts);
            string have;
            if (found.TryGetValue(key, out have)) return have;
            have = null;
            foreach (string n in names) if (PrefabManager.Instance.GetPrefab(n) != null) { have = n; break; }
            if (have == null && ZNetScene.instance != null)
                foreach (GameObject p in ZNetScene.instance.m_prefabs)
                {
                    if (p == null) continue;
                    string lower = p.name.ToLowerInvariant();
                    bool all = true;
                    foreach (string part in parts) if (part.StartsWith("!") ? lower.Contains(part.Substring(1)) : !lower.Contains(part)) { all = false; break; }
                    if (all) { have = p.name; break; }
                }
            found[key] = have;
            Plugin.Debug("Valhalla: pavilion piece " + key + " -> " + (have ?? "none"));
            return have;
        }

        // A pavilion: its middle c, its floor's height, its roof nx by nz pieces' runs from the middle to the
        // eaves each way, its posts (their places from the middle), their height.
        static void Pavilion(Vector2 c, float floor, int nx, int nz, Vector2[] posts, float height)
        {
            string pole = Prefab(new[] { "Ashwood_pole_2m", "ashwood_pole_2m" }, "ashwood", "pole", "!1m");
            string beam = Prefab(new[] { "Ashwood_beam_2m", "ashwood_beam_2m" }, "ashwood", "beam", "2m");
            string roof = Prefab(new[] { "wood_roof" }, "wood_roof", "!dark", "!45", "!67", "!top", "!corner");
            string corner = Prefab(new[] { "wood_roof_ocorner" }, "wood_roof_ocorner", "!dark", "!45", "!67");
            if (pole == null) pole = "wood_pole2";
            if (beam == null) beam = "wood_beam";
            // posts, and the beams on them round the outside (their top: the roof's underside over them)
            float top = floor + height;
            foreach (Vector2 p in posts) Stick(pole, new Vector3(c.x + p.x, Mathf.Min(floor, G(c.x + p.x, c.y + p.y)) - 0.15f, c.y + p.y), new Vector3(c.x + p.x, top, c.y + p.y), 0.32f);
            List<Vector2> ring = Round(posts);
            const float BeamThick = 0.26f;
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % ring.Count];
                Stick(beam, new Vector3(c.x + a.x, top - BeamThick * 0.5f, c.y + a.y), new Vector3(c.x + b.x, top - BeamThick * 0.5f, c.y + b.y), BeamThick);
            }
            if (roof == null || corner == null) { Plugin.Log.LogWarning("Valhalla: no thatch for the pavilion (" + roof + ", " + corner + ")"); return; }
            GameObject straight = PrefabManager.Instance.GetPrefab(roof), cornerPrefab = PrefabManager.Instance.GetPrefab(corner);
            Vector3 down = Downhill(straight);
            float slope = SlopeOf(straight);
            Bounds sb = OwnBounds(straight), cb = OwnBounds(cornerPrefab);
            if (down == Vector3.zero || slope <= 0f) { Plugin.Log.LogWarning("Valhalla: can't tell which way the thatch slopes"); return; }
            bool downX = Mathf.Abs(down.x) > Mathf.Abs(down.z);
            float run = downX ? sb.size.x : sb.size.z, along = downX ? sb.size.z : sb.size.x;
            // the corner's hip top: its highest snap point, from its middle
            Vector3 inner = Vector3.zero;
            float high = float.MinValue;
            foreach (Vector3 s in SnapPoints(cornerPrefab)) if (s.y > high) { high = s.y; inner = new Vector3(Mathf.Sign(s.x - cb.center.x), 0f, Mathf.Sign(s.z - cb.center.z)); }
            if (inner == Vector3.zero) { Plugin.Log.LogWarning("Valhalla: the thatch corner has no snap points to turn it by"); return; }
            float hx = nx * run, hz = nz * run;
            // the roof's underside over the posts as high as the beams' tops
            float mx = float.MaxValue, mz = float.MaxValue;
            foreach (Vector2 p in posts) { mx = Mathf.Min(mx, hx - Mathf.Abs(p.x)); mz = Mathf.Min(mz, hz - Mathf.Abs(p.y)); }
            float eave = top - Mathf.Min(mx, mz) * slope;
            int rings = Mathf.Min(nx, nz);
            int laid = 0;
            for (int k = 0; k < rings; k++)
            {
                float y = eave + k * run * slope, ex = hx - k * run, ez = hz - k * run;
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        Vector3 at = new Vector3(c.x + sx * (ex - run * 0.5f), y, c.y + sz * (ez - run * 0.5f));
                        if (SkyProp(corner, at, new Vector3(0f, YawFor(inner, new Vector3(-sx, 0f, -sz)), 0f)) != null) laid++;
                    }
                // along the sides parallel to x (sloping out to ±z), then those parallel to z (out to ±x)
                laid += Side(roof, down, downX, along, c, y, ex - run, sz: ez - run * 0.5f, alongX: true);
                laid += Side(roof, down, downX, along, c, y, ez - run, sz: ex - run * 0.5f, alongX: false);
            }
            // over a longer one, the ridge where the last ring's long sides meet: a beam along it
            float ridgeY = eave + rings * run * slope;
            float thick = Mathf.Max(0.05f, sb.size.y - run * slope);
            if (nx != nz)
            {
                bool alongZ = nz > nx;
                float half = alongZ ? hz - hx : hx - hz;
                Vector3 from = new Vector3(c.x - (alongZ ? 0f : half), ridgeY + thick + 0.1f, c.y - (alongZ ? half : 0f));
                Vector3 to = new Vector3(c.x + (alongZ ? 0f : half), ridgeY + thick + 0.1f, c.y + (alongZ ? half : 0f));
                Stick(beam, from - (to - from).normalized * 0.4f, to + (to - from).normalized * 0.4f, BeamThick);
            }
            Plugin.Debug("Valhalla: pavilion at " + c + ": " + posts.Length + " " + pole + " posts to " + top.ToString("F2") + ", roof " + (hx * 2f).ToString("F1") + " x " + (hz * 2f).ToString("F1") +
                " of " + laid + " " + roof + "/" + corner + " (run " + run.ToString("F2") + ", along " + along.ToString("F2") + ", slope " + slope.ToString("F2") + ", the corner's top toward " + inner +
                "), eaves at " + eave.ToString("F2") + ", top at " + (ridgeY + thick).ToString("F2"));
        }

        // One ring's two sides parallel to one axis: straight pieces along from -half to half (stretched a
        // little to fill it exactly), each sloping out to its side.
        static int Side(string roof, Vector3 down, bool downX, float along, Vector2 c, float y, float half, float sz, bool alongX)
        {
            if (half < 0.05f) return 0;
            float length = half * 2f;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / along));
            float width = length / count;
            int laid = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 outward = alongX ? new Vector3(0f, 0f, side) : new Vector3(side, 0f, 0f);
                float yaw = YawFor(down, outward);
                for (int i = 0; i < count; i++)
                {
                    float t = -half + width * (i + 0.5f);
                    Vector3 at = alongX ? new Vector3(c.x + t, y, c.y + side * sz) : new Vector3(c.x + side * sz, y, c.y + t);
                    GameObject go = SkyProp(roof, at, new Vector3(0f, yaw, 0f));
                    if (go == null) continue;
                    if (Mathf.Abs(width - along) > 0.01f)
                    {
                        // (its own width is along its own x or z: the one across its slope)
                        Vector3 scale = go.transform.localScale;
                        if (downX) scale.z *= width / along; else scale.x *= width / along;
                        go.transform.localScale = scale;
                        Put(go, at, false);
                    }
                    laid++;
                }
            }
            return laid;
        }

        // How steep a piece's slope is (rise over run), from its biggest collider's tilt.
        static float SlopeOf(GameObject prefab)
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
            if (best == null) return 0f;
            Vector3 bs = Vector3.Scale(best.size, best.transform.lossyScale);
            int thin = bs.x <= bs.y && bs.x <= bs.z ? 0 : bs.y <= bs.z ? 1 : 2;
            Vector3 axis = Vector3.zero;
            axis[thin] = 1f;
            Vector3 n = prefab.transform.InverseTransformDirection(best.transform.TransformDirection(axis));
            if (n.y < 0f) n = -n;
            float flat = new Vector2(n.x, n.z).magnitude;
            return n.y > 0.05f ? flat / n.y : 0f;
        }

        // The posts in order round the outside (by their angle from the middle).
        static List<Vector2> Round(Vector2[] posts)
        {
            List<Vector2> ring = new List<Vector2>(posts);
            ring.Sort((a, b) => Mathf.Atan2(a.y, a.x).CompareTo(Mathf.Atan2(b.y, b.x)));
            return ring;
        }

        // A pole or beam of the game's stretched from a to b, so thick: its long side along the way, its middle
        // between them.
        static GameObject Stick(string name, Vector3 a, Vector3 b, float thick)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            if (prefab == null) { missing++; Plugin.Debug("Valhalla: no " + name); return null; }
            Bounds own = OwnBounds(prefab);
            int longest = own.size.x >= own.size.y && own.size.x >= own.size.z ? 0 : own.size.y >= own.size.z ? 1 : 2;
            Vector3 axis = Vector3.zero;
            axis[longest] = 1f;
            Vector3 way = b - a;
            float length = way.magnitude;
            if (length < 0.05f) return null;
            Vector3 scale = Vector3.one;
            float across = 0f;
            for (int i = 0; i < 3; i++) if (i != longest) across = Mathf.Max(across, own.size[i]);
            for (int i = 0; i < 3; i++) scale[i] = i == longest ? length / Mathf.Max(0.05f, own.size[i]) : thick / Mathf.Max(0.02f, across);
            Quaternion turn = Quaternion.FromToRotation(axis, way / length);
            GameObject go = SkyPivot(name, Vector3.zero, turn);
            if (go == null) return null;
            go.transform.localScale = scale;
            go.transform.localPosition = (a + b) * 0.5f - turn * Vector3.Scale(own.center, scale);
            return go;
        }
    }
}
