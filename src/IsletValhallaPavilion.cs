using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Open shelters over Andhrímnir's kitchen and the Brim Folk's feast: a Norse open-sided shed, not the
    // user's garden pavilion copied (2.0.22 laid a four-sided hip roof of thatch tiles ring by ring by measured
    // sizes: its tiles sloped along the rows, stepped, with gaps, a hole at the top; the user: a reference is to
    // be taken from, not copied). Here: ashwood posts (the Ashlands' red wood) down both long sides, beams along
    // their tops and tied across, a steep thatch roof sloping two ways to a thatch ridge, laid as the Brim village's
    // thatch roofs are (the hall's own way, by the pieces' snap points: IsletBuilder.Roof); and at each gable end
    // two boards along the roof's edges, crossed over the ridge, as on the old halls. Nothing of the hall's or
    // the high seat's (their darkwood).
    static partial class IsletBuilder
    {
        static readonly Dictionary<string, string> found = new Dictionary<string, string>();

        // A prefab of the game's by the first of the names that exists, else the first whose name has all of
        // parts (any case; "!x": hasn't x), looked for among everything the game has.
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
            Plugin.Debug("Valhalla: shelter piece " + key + " -> " + (have ?? "none"));
            return have;
        }

        // A shelter: its middle c and floor; posts at x ±px, at each of zs; the roof rows thatch rows a side
        // (its eaves that far out from the middle), the ridge along z; posts so tall.
        static void Shelter(Vector2 c, float floor, float px, float[] zs, int rows, float height)
        {
            string pole = Prefab(new[] { "Ashwood_pole_2m", "ashwood_pole_2m" }, "ashwood", "pole", "!1m");
            string beam = Prefab(new[] { "Ashwood_beam_2m", "ashwood_beam_2m" }, "ashwood", "beam", "2m");
            if (pole == null) pole = "wood_pole2";
            if (beam == null) beam = "wood_beam";
            // (at 45°, as the Brim village's thatch: 2.0.23's at 26° looked squat to the user)
            const string Thatch = "wood_roof_45", ThatchCap = "wood_roof_top_45";
            float top = floor + height;
            const float BeamThick = 0.26f, PostThick = 0.32f;
            float zMin = float.MaxValue, zMax = float.MinValue;
            foreach (float z in zs) { zMin = Mathf.Min(zMin, z); zMax = Mathf.Max(zMax, z); }
            // posts down both long sides, a beam along each side's tops, and one across at each pair
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float z in zs)
                {
                    float x = c.x + side * px, zz = c.y + z;
                    Stick(pole, new Vector3(x, Mathf.Min(floor, G(x, zz)) - 0.15f, zz), new Vector3(x, top, zz), PostThick);
                }
                Stick(beam, new Vector3(c.x + side * px, top - BeamThick * 0.5f, c.y + zMin - 0.4f), new Vector3(c.x + side * px, top - BeamThick * 0.5f, c.y + zMax + 0.4f), BeamThick);
            }
            foreach (float z in zs)
                Stick(beam, new Vector3(c.x - px - 0.3f, top - BeamThick * 1.5f, c.y + z), new Vector3(c.x + px + 0.3f, top - BeamThick * 1.5f, c.y + z), BeamThick);
            // the roof: its underside over the posts at the beams' top
            float run, slope;
            if (!Slope(Thatch, out run, out slope)) { Plugin.Log.LogWarning("Valhalla: no " + Thatch + " to roof the shelter with"); return; }
            float outer = rows * run;
            float eave = top - (outer - px) * slope;
            float ridge = Roof(eave, outer, c.y + zMin, c.y + zMax, Thatch, ThatchCap, c.x);
            // the crossed boards at each gable end, along the roof's edge (a little over it), past the ridge
            float over = 0.22f, past = 0.9f, z0 = c.y + zMin - 1f - 0.12f, z1 = c.y + zMax + 1f + 0.12f;
            foreach (float zEnd in new[] { z0, z1 })
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 low = new Vector3(c.x + side * outer, eave + over, zEnd);
                    Vector3 high = new Vector3(c.x - side * past, eave + (outer + past) * slope + over, zEnd);
                    Stick(beam, low, high, BeamThick * 0.9f);
                }
            Plugin.Debug("Valhalla: shelter at " + c + ": " + zs.Length * 2 + " " + pole + " posts to " + top.ToString("F2") + ", eaves " + (outer * 2f).ToString("F1") + " m apart at " +
                eave.ToString("F2") + " (run " + run.ToString("F2") + ", slope " + slope.ToString("F2") + "), ridge at " + ridge.ToString("F2"));
        }

        // A sloped piece's run (how far in a row goes) and slope, by its snap points (lowest edge to highest).
        static bool Slope(string name, out float run, out float slope)
        {
            run = 2f;
            slope = 0.5f;
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            if (prefab == null) return false;
            List<Vector3> snaps = SnapPoints(prefab);
            if (snaps.Count < 4) return true;
            Vector3 lo = Mid(snaps, true), hi = Mid(snaps, false);
            float flat = new Vector2(hi.x - lo.x, hi.z - lo.z).magnitude, rise = hi.y - lo.y;
            if (flat > 0.2f && rise > 0.1f) { run = flat; slope = rise / flat; }
            return true;
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
