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

        // ---- Eitri and Brokkr's forge, west of the einherjar's yard (where the daily battle was) ----

        internal static readonly Vector2 Smithy = new Vector2(19f, -16f);
        // where the smith's anvil and the bellows stand, from its middle (the smiths stand by them, by their real
        // size: ValhallaSmiths)
        internal static readonly Vector2 SmithAnvil = new Vector2(2.6f, 2.6f), SmithBellows = new Vector2(-3f, 3.7f);
        const float SmithyHalf = 5f, SmithyPostX = 4.4f, SmithyPostZ = 4f, SmithyPosts = 3.8f;

        // A stone floor (10 x 10 m, a metre high: its stones are blocks) with steps up the middle of each side and
        // the way from each across it kept clear; in its four corners the work: the forge with its bellows beside
        // it, the smith's anvil, the grinding wheel, the small anvils and the cooler; a dwarves' lantern either
        // side of the east and west steps, its lamp over the floor; under a shelter like the kitchen's.
        // (2.0.25-2.0.26: 8 x 8, all close together, one way up, the lanterns by the posts looking out, and the
        // tool rack, a thing hung on a wall, standing in the air with nothing behind it.)
        static void SmithyBuild()
        {
            Vector3 k = new Vector3(Smithy.x, 0f, Smithy.y);
            float floor = float.MinValue;
            string tile = PieceNamed("$piece_stonefloor2x2", "stone_floor_2x2");
            if (tile != null)
                for (float x = -4f; x <= 4.01f; x += 2f)
                    for (float z = -4f; z <= 4.01f; z += 2f)
                    {
                        GameObject t = SkyProp(tile, k + new Vector3(x, 0f, z), Vector3.zero);
                        if (t != null) floor = Mathf.Max(floor, SkyBounds(t).max.y);
                    }
            if (floor == float.MinValue) floor = G(Smithy.x, Smithy.y);
            Vector3 f = new Vector3(k.x, floor, k.z);
            // the forge with its fire burning (a copy's fire shows only while it has fuel: shown here)
            GameObject forgeGo = null;
            string forge = PieceNamed("$piece_forge", "forge");
            if (forge != null) forgeGo = SkyProp(forge, f + new Vector3(-3f, 0f, 2.2f), new Vector3(0f, 90f, 0f), false, go =>
            {
                CraftingStation station = go.GetComponent<CraftingStation>();
                if (station != null && station.m_haveFireObject != null) station.m_haveFireObject.SetActive(true);
                // (and none of a working station's markers: a hammer hung over it in 2.0.25, the user saw it)
                if (station != null && station.m_inUseObject != null) station.m_inUseObject.SetActive(false);
                if (station != null && station.m_areaMarker != null) station.m_areaMarker.SetActive(false);
            });
            // the bellows right against the forge's north side, as the game sets them by it, Brokkr behind them
            // (2.0.27-2.0.29: on its south side, him hidden behind them)
            GameObject bellows = Ext("$piece_forge_ext1", "forge_ext1", f, SmithBellows, 90f);
            if (forgeGo != null && bellows != null)
            {
                Bounds fb = SkyBounds(forgeGo), bb = SkyBounds(bellows);
                Put(bellows, new Vector3(fb.center.x, floor, fb.max.z + bb.extents.z + 0.05f), false);
            }
            Ext("$piece_forge_ext5", "forge_ext5", f, new Vector2(-1.6f, -3.6f), 0f);  // the cooler
            Ext("$piece_forge_ext2", "forge_ext2", f, new Vector2(-3.2f, -3f), 0f);    // the small anvils
            Ext("$piece_forge_ext4", "forge_ext4", f, SmithAnvil, 90f);                // the smith's anvil
            Ext("$piece_forge_ext3", "forge_ext3", f, new Vector2(3f, -3f), 0f);       // the grinding wheel
            // a hooded lantern hung under the middle of each of the four beams, its light turned in over the floor
            // (2.0.25-2.0.27's dwarves' pole lanterns stood by the posts looking out: the user had these instead,
            // hung so)
            string lantern = PieceNamed("$piece_hoodedlantern", "piece_hoodedlantern");
            float top = floor + SmithyPosts;
            if (lantern != null)
            {
                Hang(lantern, new Vector3(k.x - SmithyPostX, top - 0.26f, k.z), Vector3.right);
                Hang(lantern, new Vector3(k.x + SmithyPostX, top - 0.26f, k.z), Vector3.left);
                Hang(lantern, new Vector3(k.x, top - 0.52f, k.z - SmithyPostZ), Vector3.forward);
                Hang(lantern, new Vector3(k.x, top - 0.52f, k.z + SmithyPostZ), Vector3.back);
            }            // steps up the middle of each side (its stones stand a metre high: 2.0.25 had none, 2.0.26 one)
            if (tile != null)
                foreach (Vector2 way in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                    StoneSteps(tile, Smithy, way, SmithyHalf, floor, 2.4f);
            Shelter(Smithy, floor, SmithyPostX, new[] { -SmithyPostZ, SmithyPostZ }, 3, SmithyPosts);
            if (tile != null) SmithWorks(tile);
        }

        // ---- the smiths' works, shown north of the forge, either side of its north steps ----

        // where Gullinbursti stands, from the forge's middle (made with the people: ValhallaSmiths)
        internal static readonly Vector2 GullinburstiAt = new Vector2(3.5f, 6.2f);

        // Two low stone daises either side of the north steps: on the west one Mjölnir, Gungnir and Draupnir, each
        // on a pedestal; on the east one, Gullinbursti (ValhallaSmiths). Each says what it is.
        static void SmithWorks(string stone)
        {
            foreach (float side in new[] { -1f, 1f })
                StoneBlock(stone, new Vector2(Smithy.x + side * 3.5f, Smithy.y + 6.2f), 3.4f, 0.4f, 1.6f, 0f);
            float dais = G(Smithy.x - 3.5f, Smithy.y + 6.2f) + 0.4f;
            // Mjölnir: a twin of Thor's hammer, standing on its head, its short handle up (2.0.29 laid it down small,
            // its head off the pedestal's edge)
            float mTop = StoneBlock(stone, new Vector2(Smithy.x - 4.5f, Smithy.y + 6.2f), 0.6f, 0.6f, 0.6f, dais);
            Vector3 mAt = new Vector3(Smithy.x - 4.5f, mTop, Smithy.y + 6.2f);
            GameObject hammer = SkyItem("SledgeIron", mAt, Vector3.back, Vector3.up, false, true);
            if (hammer != null) { Grow(hammer, 0.6f, mAt); Plaque(hammer, "iov_mjolnir"); }
            // Gungnir: a twin of Odin's spear (the sons of Ivaldi's work, not theirs: Eitri keeps it to show his is
            // finer), upright, its butt set in its pedestal (the user asked for it between Mjölnir and the ring)
            float gTop = StoneBlock(stone, new Vector2(Smithy.x - 3.4f, Smithy.y + 6.2f), 0.5f, 0.45f, 0.5f, dais);
            Vector3 gAt = new Vector3(Smithy.x - 3.4f, gTop - 0.15f, Smithy.y + 6.2f);
            string spear = PrefabManager.Instance.GetPrefab("SpearWolfFang") != null ? "SpearWolfFang" : "SpearElderbark";
            GameObject gungnir = SkyItem(spear, gAt, Vector3.back, Vector3.up, false);
            if (gungnir != null) { Grow(gungnir, 0.85f, gAt); Plaque(gungnir, "iov_gungnir"); }
            // Draupnir: a gold ring standing on its edge over its pedestal, turning slowly, faintly lit
            float dTop = StoneBlock(stone, new Vector2(Smithy.x - 2.3f, Smithy.y + 6.2f), 0.6f, 0.75f, 0.6f, dais);
            GameObject ring = GoldRing(new Vector3(Smithy.x - 2.3f, dTop + 0.42f, Smithy.y + 6.2f));
            if (ring != null) Plaque(ring, "iov_draupnir");
            // (Gullinbursti's low plinth on the east dais; he's made with the people)
            StoneBlock(stone, new Vector2(Smithy.x + GullinburstiAt.x, Smithy.y + GullinburstiAt.y), 1.7f, 0.3f, 0.9f, G(Smithy.x + 3.5f, Smithy.y + 6.2f) + 0.4f);
        }

        // A block of the floor's stone cut to size, its foot at foot (or the ground there, 0), its middle at c.
        // Gives back its top.
        static float StoneBlock(string stone, Vector2 c, float sx, float sy, float sz, float foot)
        {
            float y = foot != 0f ? foot : G(c.x, c.y);
            Vector3 at = new Vector3(c.x, y - 0.3f, c.y);
            GameObject go = SkyProp(stone, at, Vector3.zero);
            if (go == null) return y + sy;
            Bounds b = SkyBounds(go);
            go.transform.localScale = new Vector3(sx / Mathf.Max(0.1f, b.size.x), (sy + 0.3f) / Mathf.Max(0.05f, b.size.y), sz / Mathf.Max(0.1f, b.size.z));
            Put(go, at, false);
            return y + sy;
        }

        // A ring of gold (the game has none to lay out: made here, of the gold of the game's coins).
        static GameObject GoldRing(Vector3 at)
        {
            GameObject coins = PrefabManager.Instance.GetPrefab("Coins");
            Renderer gold = coins != null ? coins.GetComponentInChildren<MeshRenderer>(true) : null;
            if (gold == null) return null;
            GameObject go = new GameObject("IoV_Draupnir");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = Torus(0.2f, 0.045f, 40, 14);
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = gold.sharedMaterial;
            go.AddComponent<Spin>().degrees = 25f;
            GameObject glow = new GameObject("IoV_DraupnirGlow");
            glow.transform.SetParent(go.transform, false);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.4f);
            light.range = 2.2f;
            light.intensity = 0.9f;
            light.shadows = LightShadows.None;
            built++;
            return go;
        }

        // A ring standing on its edge (its hole looking along z), round its middle.
        static Mesh Torus(float big, float small, int around, int across)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> tris = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                float a = i * Mathf.PI * 2f / around;
                Vector3 centre = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * big;
                for (int j = 0; j <= across; j++)
                {
                    float b = j * Mathf.PI * 2f / across;
                    Vector3 normal = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a) * Mathf.Cos(b), Mathf.Sin(b));
                    v.Add(centre + normal * small);
                    n.Add(normal);
                    uv.Add(new Vector2((float)i / around, (float)j / across));
                }
            }
            for (int i = 0; i < around; i++)
                for (int j = 0; j < across; j++)
                {
                    int p = i * (across + 1) + j, q = p + across + 1;
                    tris.Add(p); tris.Add(q); tris.Add(p + 1);
                    tris.Add(p + 1); tris.Add(q); tris.Add(q + 1);
                }
            Mesh mesh = new Mesh { name = "IoV_Ring" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // What a work is, shown when you look at it: a box round it you can point at.
        static void Plaque(GameObject work, string token)
        {
            Bounds b = SkyBounds(work);
            GameObject box = new GameObject("IoV_Plaque");
            box.layer = LayerMask.NameToLayer("piece");
            box.transform.SetParent(parent, false);
            box.transform.localPosition = b.center;
            BoxCollider c = box.AddComponent<BoxCollider>();
            c.size = Vector3.Max(b.size, Vector3.one * 0.35f);
            box.AddComponent<ValhallaWork>().token = token;
        }

        // Hung by its top from under a beam at at, its light turned toward look (as far as it points sideways).
        static void Hang(string name, Vector3 at, Vector3 look)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            Light lamp = prefab != null ? prefab.GetComponentInChildren<Light>(true) : null;
            Vector3 own = lamp != null ? prefab.transform.InverseTransformDirection(lamp.transform.forward) : Vector3.zero;
            own.y = 0f;
            float yaw = own.sqrMagnitude > 0.04f ? YawFor(own.normalized, look) : 0f;
            SkyProp(name, at, new Vector3(0f, yaw, 0f), true);
        }
        // Steps going down from the floor's edge (half from the middle c, way out) to the ground there, width
        // wide, each a stone of the floor's own kind, cut to size.
        static void StoneSteps(string stone, Vector2 c, Vector2 way, float half, float top, float width)
        {
            Vector2 edge = c + way * half;
            float ground = G(edge.x + way.x, edge.y + way.y), rise = top - ground;
            if (rise < 0.15f) return;
            int count = Mathf.Max(2, Mathf.CeilToInt(rise / 0.25f));
            const float depth = 0.45f;
            bool alongX = Mathf.Abs(way.x) > 0.5f;
            for (int i = 1; i <= count; i++)
            {
                float height = rise * i / (count + 1f);
                Vector2 p = edge + way * (depth * (count - i) + depth * 0.5f);
                Vector3 at = new Vector3(p.x, ground - 0.3f, p.y);
                GameObject go = SkyProp(stone, at, Vector3.zero);
                if (go == null) return;
                Bounds b = SkyBounds(go);
                go.transform.localScale = new Vector3((alongX ? depth : width) / Mathf.Max(0.1f, b.size.x), (height + 0.3f) / Mathf.Max(0.05f, b.size.y),
                    (alongX ? width : depth) / Mathf.Max(0.1f, b.size.z));
                Put(go, at, false);
            }
        }
        static GameObject Ext(string token, string name, Vector3 f, Vector2 at, float yaw)
        {
            string piece = PieceNamed(token, name);
            return piece != null ? SkyProp(piece, f + new Vector3(at.x, 0f, at.y), new Vector3(0f, yaw, 0f)) : null;
        }

        // A piece of the game's by its prefab name, else by its own (shown) name: one whose Piece is called token.
        static string PieceNamed(string token, string name)
        {
            if (PrefabManager.Instance.GetPrefab(name) != null) return name;
            if (ZNetScene.instance != null)
                foreach (GameObject p in ZNetScene.instance.m_prefabs)
                {
                    Piece piece = p != null ? p.GetComponent<Piece>() : null;
                    if (piece != null && piece.m_name == token) { Plugin.Debug("Valhalla: " + token + " is " + p.name); return p.name; }
                }
            Plugin.Debug("Valhalla: no " + name + " (" + token + ")");
            return null;
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
