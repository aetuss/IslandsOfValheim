using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // What stands on a large islet of the game's own terrain, drawn by eye on the islet itself: a draft is a
    // list of things and where they stand on it (drafts/<Land>_<design>.json, built into the dll). It is laid
    // out and corrected in the game on the islet's bare ground (iov_goto draft_meadows_2), then read here.
    //
    // Each line is one thing, its place in the islet's own space (x and z from the middle; how high above
    // the ground there), and its turn:
    // - vanilla: a whole structure of the game's (a house, dolmens, a hut) by its location's name;
    // - marker: a place for something of ours (KEEPER, DOCK, WRECK, FISHING);
    // - locpart: one part of a vanilla structure, "<location>|<path inside it>";
    // - anything else: one of the game's own objects by its prefab name (a tree, a rock, a pickable, a piece).
    static partial class IsletBuilder
    {
        internal class DraftEntry
        {
            internal string Kind = "", Prefab = "", Source = "", Group = "";
            internal float X, Z, Above, Radius;
            internal float Chance = 1f; // below 1: on some islets only (RandomSpawn)
            internal Quaternion Rot = Quaternion.identity;
            internal Vector3 Scale = Vector3.one;

            internal float Yaw { get { return Rot.eulerAngles.y; } }
        }

        static readonly Regex DraftField = new Regex("\"(\\w+)\": (\"((?:[^\"\\\\]|\\\\.)*)\"|[-0-9.eE]+)");

        // The draft of a land's design, or null if it has none (it keeps the old layout then).
        internal static List<DraftEntry> ReadDraft(string id)
        {
            Assembly assembly = typeof(IsletBuilder).Assembly;
            Stream stream = assembly.GetManifestResourceStream("IoV.Drafts." + id + ".json");
            if (stream == null) return null;
            List<DraftEntry> list = new List<DraftEntry>();
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.Contains("\"kind\":")) continue;
                    Dictionary<string, string> f = new Dictionary<string, string>();
                    foreach (Match m in DraftField.Matches(line))
                        f[m.Groups[1].Value] = m.Groups[3].Success ? m.Groups[3].Value.Replace("\\\"", "\"").Replace("\\\\", "\\") : m.Groups[2].Value;
                    DraftEntry e = new DraftEntry();
                    e.Kind = Field(f, "kind");
                    e.Prefab = Field(f, "prefab");
                    e.Source = Field(f, "source");
                    e.X = Number(f, "x", 0f);
                    e.Z = Number(f, "z", 0f);
                    e.Above = Number(f, "aboveGround", 0f);
                    e.Rot = new Quaternion(Number(f, "qx", 0f), Number(f, "qy", 0f), Number(f, "qz", 0f), Number(f, "qw", 1f));
                    e.Scale = new Vector3(Number(f, "sx", 1f), Number(f, "sy", 1f), Number(f, "sz", 1f));
                    e.Chance = Mathf.Clamp01(Number(f, "chance", 1f));
                    e.Group = Field(f, "group");
                    e.Radius = Number(f, "radius", 0f);
                    list.Add(e);
                }
            }
            return list;
        }

        static string Field(Dictionary<string, string> f, string key)
        {
            string v;
            return f.TryGetValue(key, out v) ? v : "";
        }

        static float Number(Dictionary<string, string> f, string key, float fallback)
        {
            string v;
            float n;
            return f.TryGetValue(key, out v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out n) ? n : fallback;
        }

        // Builds the design's draft on the islet; false if it has none.
        static bool BuildDraft(LargeKind kind)
        {
            List<DraftEntry> draft = ReadDraft(kind.DraftId);
            if (draft == null) return false;
            draftKeeper = false;
            draftLeft = 0;
            Dictionary<string, GameObject> sources = new Dictionary<string, GameObject>();
            // Things with the same group: one of them on each islet (RandomObject); a chance below 1: on some
            // islets only (RandomSpawn). So no two islets of a design are quite the same.
            Dictionary<string, List<DraftEntry>> groups = new Dictionary<string, List<DraftEntry>>();
            foreach (DraftEntry e in draft)
            {
                if (e.Group.Length == 0) continue;
                List<DraftEntry> members;
                if (!groups.TryGetValue(e.Group, out members)) { members = new List<DraftEntry>(); groups[e.Group] = members; }
                members.Add(e);
            }
            foreach (DraftEntry e in draft)
            {
                if (e.Group.Length > 0)
                {
                    if (groups[e.Group][0] != e) continue; // built with the first of its group
                    string group = e.Group;
                    System.Action build = () =>
                    {
                        List<DraftEntry> members = groups[group];
                        System.Action[] each = new System.Action[members.Count];
                        for (int i = 0; i < members.Count; i++) { DraftEntry m = members[i]; each[i] = () => BuildEntry(kind, m, sources); }
                        OneOf(each);
                    };
                    if (e.Chance < 1f) Optional(e.Chance, build); else build();
                }
                else if (e.Chance < 1f && e.Kind != "marker") Optional(e.Chance, () => BuildEntry(kind, e, sources));
                else BuildEntry(kind, e, sources);
            }
            foreach (GameObject source in sources.Values) if (source != null) Object.DestroyImmediate(source);
            bool keeper = draftKeeper;
            int left = draftLeft;
            // Every islet has its keeper: the animals come from it, and on a dangerous one the guardian and chest.
            if (!keeper) { Keeper(kind, Vector2.zero); Plugin.Log.LogWarning(locationName + ": its draft has no KEEPER mark; the keeper stands in the middle"); }
            PlaceHints();
            if (!kind.Peaceful) PlaceOutsideGuard();
            Plugin.Debug(locationName + ": draft " + kind.DraftId + ", " + draft.Count + " lines, " + left + " left out");
            return true;
        }

        static int vanillaCount;

        // A whole vanilla structure (a house, a hut, dolmens, a ruin) copied onto the islet with its middle at
        // at, turned rotY, above metres over the ground there. Whatever belongs to the game's own world is left
        // out: its terrain changes, its way-pointing stones, altars and traders, a dungeon generator.
        static GameObject Vanilla(string[] sources, Vector2 at, float rotY, float above)
        {
            string source = FindLocation(sources);
            if (source == null) { missing++; Plugin.Log.LogWarning(locationName + ": none of the vanilla structures " + string.Join(", ", sources) + " exist."); return null; }
            CustomLocation clone = ZoneManager.Instance.CreateClonedLocation(locationName + "_Part" + (vanillaCount++), source);
            if (clone == null || clone.Prefab == null) return null;
            GameObject group = new GameObject("IoV_Vanilla_" + source);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = new Vector3(at.x, Height(at.x, at.y) + above, at.y);
            group.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            List<Transform> parts = new List<Transform>();
            foreach (Transform child in clone.Prefab.transform) parts.Add(child);
            foreach (Transform part in parts) part.SetParent(group.transform, false);
            // Its own levelling and paint stay, as in the game's world: the ground under it is flattened and
            // worn the way its makers meant (the islet's terrain is the game's own, so they work the same).
            int removed = 0;
            foreach (TerrainOp op in group.GetComponentsInChildren<TerrainOp>(true)) { Object.DestroyImmediate(op.gameObject); removed++; }
            foreach (Vegvisir v in group.GetComponentsInChildren<Vegvisir>(true)) { Object.DestroyImmediate(v.gameObject); removed++; }
            foreach (OfferingBowl o in group.GetComponentsInChildren<OfferingBowl>(true)) { Object.DestroyImmediate(o.gameObject); removed++; }
            foreach (Trader t in group.GetComponentsInChildren<Trader>(true)) { Object.DestroyImmediate(t.gameObject); removed++; }
            foreach (DungeonGenerator g in group.GetComponentsInChildren<DungeonGenerator>(true)) { Object.DestroyImmediate(g.gameObject); removed++; }
            foreach (CreatureSpawner spawner in group.GetComponentsInChildren<CreatureSpawner>(true))
            {
                Vector3 p = root.transform.InverseTransformPoint(spawner.transform.position);
                float ground = Height(p.x, p.z);
                if (p.y < ground + 0.2f) spawner.transform.position = root.transform.TransformPoint(new Vector3(p.x, ground + 0.3f, p.z));
            }
            ZoneManager.Instance.DestroyCustomLocation(clone.Name);
            if (clone.Prefab != null) Object.DestroyImmediate(clone.Prefab);
            built++;
            Plugin.Debug(locationName + ": " + source + " at " + at.ToString("F0") + ", " + parts.Count + " parts, " + removed + " left out");
            return group;
        }

        // A terrain modifier a draft put there on purpose: a structure's own, or a worn path.
        static bool DraftGround(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name == "IoV_Paint" || t.name.StartsWith("IoV_Vanilla_")) return true;
            return false;
        }

        static bool draftKeeper;
        static int draftLeft;

        static void BuildEntry(LargeKind kind, DraftEntry e, Dictionary<string, GameObject> sources)
        {
            Vector2 at = new Vector2(e.X, e.Z);
            switch (e.Kind)
            {
                case "vanilla":
                    Vanilla(new[] { e.Prefab }, at, e.Yaw, e.Above);
                    break;
                case "marker":
                    float deg = Mathf.Atan2(e.Z, e.X) * Mathf.Rad2Deg;
                    switch (e.Prefab)
                    {
                        case "KEEPER":
                            if (draftKeeper) { draftLeft++; Plugin.Log.LogWarning(locationName + ": a second KEEPER mark, left out"); break; }
                            Keeper(kind, at);
                            draftKeeper = true;
                            break;
                        case "DOCK": PlaceDock(deg); break;
                        case "WRECK": PlaceWreck(deg); break;
                        case "FISHING": PlaceFishingGround(); break;
                        default: draftLeft++; Plugin.Log.LogWarning(locationName + ": unknown mark " + e.Prefab); break;
                    }
                    break;
                case "paint":
                    Paint(e);
                    break;
                case "locpart":
                    if (!DraftPart(e, sources)) draftLeft++;
                    break;
                case "creature":
                case "level":
                    draftLeft++; // creatures come from the keeper; the ground is the islet's own
                    break;
                default:
                    DraftObject(e);
                    break;
            }
        }

        // A worn patch of ground (a path, a yard): the game's own terrain paint, as its locations use it,
        // without reshaping the ground. prefab: dirt, paved or cultivate.
        static void Paint(DraftEntry e)
        {
            GameObject go = new GameObject("IoV_Paint");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(e.X, Height(e.X, e.Z), e.Z);
            TerrainModifier modifier = go.AddComponent<TerrainModifier>();
            modifier.m_level = false;
            modifier.m_smooth = false;
            modifier.m_paintCleared = true;
            modifier.m_paintHeightCheck = false;
            modifier.m_paintType = e.Prefab == "paved" ? TerrainModifier.PaintType.Paved : e.Prefab == "cultivate" ? TerrainModifier.PaintType.Cultivate : TerrainModifier.PaintType.Dirt;
            modifier.m_paintRadius = e.Radius > 0f ? e.Radius : 1.5f;
            built++;
        }

        // One of the game's objects where the draft put it. A building piece standing on the ground is sunk a
        // little into it, so it has support and doesn't collapse.
        static void DraftObject(DraftEntry e)
        {
            GameObject prefab = PlantOrSelf(e.Prefab);
            if (prefab == null) { missing++; Plugin.Log.LogWarning(locationName + ": the draft's " + e.Prefab + " doesn't exist"); return; }
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            float ground = Height(e.X, e.Z);
            go.transform.localPosition = new Vector3(e.X, ground + e.Above, e.Z);
            go.transform.localRotation = e.Rot;
            if (e.Scale != Vector3.one) go.transform.localScale = e.Scale;
            if (go.GetComponent<WearNTear>() != null)
            {
                Bounds b = SolidBounds(go);
                float under = GroundMin(e.X, e.Z, 0.35f * Mathf.Max(b.size.x, b.size.z));
                if (b.min.y > under - 0.1f && b.min.y < under + 0.6f) go.transform.localPosition += Vector3.up * (under - 0.2f - b.min.y);
            }
            built++;
        }

        // One part of a vanilla structure (as the dev tool saves them): taken out of a copy of that location.
        static bool DraftPart(DraftEntry e, Dictionary<string, GameObject> sources)
        {
            int bar = e.Source.IndexOf('|');
            if (bar < 0) return false;
            string location = e.Source.Substring(0, bar);
            GameObject source;
            if (!sources.TryGetValue(location, out source))
            {
                source = null;
                CustomLocation clone = ZoneManager.Instance.CreateClonedLocation(locationName + "_Part" + (vanillaCount++), location);
                if (clone != null && clone.Prefab != null)
                {
                    source = clone.Prefab;
                    ZoneManager.Instance.DestroyCustomLocation(clone.Name);
                }
                sources[location] = source;
            }
            if (source == null) { Plugin.Log.LogWarning(locationName + ": no vanilla structure " + location); return false; }
            Transform t = source.transform;
            foreach (string step in e.Source.Substring(bar + 1).Split('/'))
            {
                string name = step;
                int nth = 0;
                int hash = step.LastIndexOf('#');
                if (hash > 0 && int.TryParse(step.Substring(hash + 1), out nth)) name = step.Substring(0, hash);
                Transform next = null;
                int seen = 0;
                foreach (Transform child in t)
                    if (child.name == name && seen++ == nth) { next = child; break; }
                if (next == null) { Plugin.Log.LogWarning(locationName + ": no part " + e.Source); return false; }
                t = next;
            }
            GameObject go = Object.Instantiate(t.gameObject, parent, false);
            go.name = t.name;
            // Its synced pieces come as their own lines of the draft.
            foreach (ZNetView view in go.GetComponentsInChildren<ZNetView>(true))
                if (view != null && view.gameObject != go) Object.DestroyImmediate(view.gameObject);
            go.transform.localPosition = new Vector3(e.X, Height(e.X, e.Z) + e.Above, e.Z);
            go.transform.localRotation = e.Rot;
            go.transform.localScale = e.Scale;
            built++;
            return true;
        }
    }
}
