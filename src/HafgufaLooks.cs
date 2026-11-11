using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // What makes the game's serpent Rán's beast (the design: "rocks, shells, weed and net ropes on it, glowing
    // eyes"): all of it the game's own models, hung on its bones so it moves with it.
    // - rocks grown on its back, and pale shells (the chitin's) between them;
    // - seaweed trailing from its sides;
    // - torn ropes of Rán's net over its back, sagging between its bones (drawn as lines each frame);
    // - two cold eyes on its head, and a few drowned souls' lights caught in its body.
    // Laid out on the prefab from its bones' places at rest; logged, to tune from photos.
    static class HafgufaLooks
    {
        static Transform root;
        static Bounds body;        // what's drawn of it at rest, in its own space
        static readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();

        internal static void Dress(GameObject go)
        {
            root = go.transform;
            bones.Clear();
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            body = Drawn(go);
            Plugin.Debug("Hafgufa's body at rest: " + body.center.ToString("F2") + " size " + body.size.ToString("F2"));
            foreach (string n in new[] { "Main", "Head", "EyePos", "Tail1", "Tail3", "Tail6", "Tail9", "Tail13" })
            {
                Transform b;
                if (bones.TryGetValue(n, out b)) Plugin.Debug("  bone " + n + " at " + root.InverseTransformPoint(b.position).ToString("F2"));
            }

            // (1.10.79's photos: the rocks came out big and snow-white, the chitin as dark spikes with glowing tips
            // sticking out sideways, the weed floating off its flanks, the ropes as black sticks, and the eyes floating
            // over it: EyePos is the game's eye height over its middle, not on its head. Now: fewer, smaller, darker
            // things, sunk into its skin; the shells gone; ropes hanging along its flanks; eyes on its head.)
            // rocks grown on its back, in the deep's colours, and little ones like barnacles between
            Part("Rock_3", "Main", 0f, 0.8f, 0f, 0.6f, 20f, Dark);
            Part("Rock_3", "Tail2", 0.2f, 0.8f, 0f, 0.5f, 140f, Dark);
            Part("Rock_3", "Tail4", -0.2f, 0.8f, 0f, 0.45f, 260f, Dark);
            Part("Rock_3", "Tail7", 0.1f, 0.8f, 0f, 0.35f, 80f, Dark);
            Part("Rock_3", "Tail1", -0.55f, 0.75f, 0f, 0.22f, 10f, Dark);
            Part("Rock_3", "Tail3", 0.55f, 0.75f, 0f, 0.2f, 200f, Dark);
            Part("Rock_3", "Tail5", -0.5f, 0.75f, 0f, 0.2f, 120f, Dark);
            // weed hanging from its flanks, dark
            foreach (string n in new[] { "Main", "Tail2", "Tail4", "Tail6" })
            {
                Part("FreshSeaweed", n, 0.9f, 0.25f, 0f, 0.6f, 90f, Weed);
                Part("FreshSeaweed", n, -0.9f, 0.25f, 0f, 0.6f, 270f, Weed);
            }
            // the net's torn ropes, hanging in loops along its flanks
            Material rope = RopeMaterial();
            if (rope != null)
            {
                Rope(rope, "Main", 0.85f, "Tail3", 0.85f, 0.5f);
                Rope(rope, "Tail1", -0.85f, "Tail4", -0.85f, 0.55f);
                Rope(rope, "Tail4", 0.8f, "Tail7", 0.8f, 0.5f);
                Rope(rope, "Tail6", -0.75f, "Tail9", -0.75f, 0.45f);
            }
            Eyes(go);
            // the drowned caught in it
            foreach (string n in new[] { "Tail3", "Tail6", "Tail9" }) SoulLight(n);
        }

        // How thick it is at a bone: full at its middle, thinner down the tail.
        static float Radius(string bone)
        {
            float k = 1f;
            if (bone.StartsWith("Tail"))
            {
                int i;
                if (int.TryParse(bone.Substring(4), out i)) k = Mathf.Lerp(1f, 0.3f, i / 13f);
            }
            return BodyRadius * k;
        }

        // (its body is about 2.9 across at rest; its height counts the fins on its back)
        const float BodyRadius = 1.25f;

        static readonly Recolor.Shift Dark = BossKit.Cast(new Color(0.08f, 0.14f, 0.16f), 0.65f, 0.6f);
        static readonly Recolor.Shift Weed = BossKit.Cast(new Color(0.12f, 0.2f, 0.1f), 0.6f, 0.7f);

        // A point on its skin at a bone: side (-1 left .. 1 right) and up (0 its side .. 1 its back), in its own
        // space, then the world's.
        static Vector3 Skin(Transform bone, float side, float up)
        {
            Vector3 p = root.InverseTransformPoint(bone.position);
            float r = Radius(bone.name);
            Vector3 dir = new Vector3(side, up, 0f);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.up;
            p += dir.normalized * r;
            return root.TransformPoint(p);
        }

        // A copy of a model of the game's (its mesh and materials only), this big, on its skin at a bone.
        static void Part(string prefab, string boneName, float side, float up, float along, float size, float yaw, Recolor.Shift colour)
        {
            Transform bone;
            if (!bones.TryGetValue(boneName, out bone)) { Plugin.Debug("Hafgufa: no bone " + boneName); return; }
            GameObject source = PrefabManager.Instance.GetPrefab(prefab);
            if (source == null) { Plugin.Debug("Hafgufa: no " + prefab); return; }
            MeshFilter filter = null;
            MeshRenderer drawn = null;
            foreach (MeshFilter f in source.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer r = f.GetComponent<MeshRenderer>();
                if (f.sharedMesh == null || r == null) continue;
                filter = f; drawn = r;
                break;
            }
            if (filter == null) { Plugin.Debug("Hafgufa: " + prefab + " has no mesh"); return; }
            GameObject part = new GameObject("IoV_Hafgufa_" + prefab);
            part.transform.SetParent(bone, false);
            part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = drawn.sharedMaterials;
            Vector3 extents = filter.sharedMesh.bounds.size;
            float extent = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            float world = (size * RanNetSize()) / Mathf.Max(0.001f, extent);
            float boneScale = Mathf.Max(0.0001f, bone.lossyScale.x);
            part.transform.localScale = Vector3.one * (world / boneScale);
            part.transform.position = Skin(bone, side, up);
            part.transform.rotation = root.rotation * Quaternion.Euler(Random.Range(-15f, 15f), yaw, Random.Range(-15f, 15f));
            // (its pivot at its middle: sunk a little into the skin, not standing on it)
            part.transform.position -= part.transform.rotation * Vector3.Scale(filter.sharedMesh.bounds.center, Vector3.one * world) * 0.5f;
            if (colour != null) BossKit.Tint(part, colour, 0f, "Hafgufa's " + prefab);
            NoSnow(renderer, prefab);
        }

        // A rock's shader can lay snow on whatever faces up (the first rocks came out snow-white): none on it.
        static readonly HashSet<string> snowLogged = new HashSet<string>();

        static void NoSnow(Renderer renderer, string what)
        {
            foreach (Material m in renderer.sharedMaterials)
            {
                if (m == null || m.shader == null) continue;
                Shader s = m.shader;
                List<string> found = new List<string>();
                for (int i = 0; i < s.GetPropertyCount(); i++)
                {
                    string name = s.GetPropertyName(i);
                    if (name.IndexOf("snow", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    UnityEngine.Rendering.ShaderPropertyType type = s.GetPropertyType(i);
                    if (type == UnityEngine.Rendering.ShaderPropertyType.Float || type == UnityEngine.Rendering.ShaderPropertyType.Range)
                    {
                        m.SetFloat(name, 0f);
                        found.Add(name);
                    }
                }
                if (snowLogged.Add(what)) Plugin.Debug("Hafgufa's " + what + ": " + s.name + (found.Count > 0 ? ", snow off (" + string.Join(", ", found.ToArray()) + ")" : ", no snow settings"));
            }
        }

        // (sizes above are for the game's serpent; Hafgufa is RanNet.Size times bigger)
        static float RanNetSize() { return 2.5f; }

        static Material RopeMaterial()
        {
            Shader shader = ValhallaClouds.Find("Sprites/Default");
            if (shader == null) { Plugin.Debug("Hafgufa: no shader for its ropes"); return null; }
            Material m = new Material(shader);
            m.color = new Color(0.45f, 0.39f, 0.3f, 1f); // (old hemp; darker came out as black sticks in the mist)
            return m;
        }

        static void Rope(Material material, string from, float fromSide, string to, float toSide, float sag)
        {
            Transform a, b;
            if (!bones.TryGetValue(from, out a) || !bones.TryGetValue(to, out b)) return;
            GameObject go = new GameObject("IoV_HafgufaRope");
            go.transform.SetParent(a, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 9;
            line.widthMultiplier = 0.05f * RanNetSize();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            HafgufaRope r = go.AddComponent<HafgufaRope>();
            r.a = a; r.b = b;
            r.offA = a.InverseTransformPoint(Skin(a, fromSide, 0.3f));
            r.offB = b.InverseTransformPoint(Skin(b, toSide, 0.3f));
            r.sag = sag * RanNetSize();
        }

        // Two cold eyes either side of its head, a little behind its snout. (At rest, in its own space: its snout
        // ends about 1.6 ahead of its middle, the head bone 1.5 behind; its body about 2.9 across.)
        static readonly Vector3 EyeAt = new Vector3(0.72f, 0.5f, 0.55f);

        static void Eyes(GameObject go)
        {
            Transform head;
            if (!bones.TryGetValue("Head", out head)) { Plugin.Debug("Hafgufa: no head"); return; }
            Shader shader = ValhallaClouds.Find("Sprites/Default");
            if (shader == null) return;
            Material glow = new Material(shader);
            glow.color = new Color(0.75f, 1f, 1f, 1f);
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(ball.GetComponent<Collider>());
                ball.name = "IoV_HafgufaEye";
                ball.GetComponent<MeshRenderer>().sharedMaterial = glow;
                ball.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ball.transform.SetParent(head, true);
                ball.transform.position = root.TransformPoint(new Vector3(s * EyeAt.x, EyeAt.y, EyeAt.z));
                float world = 0.16f * RanNetSize();
                ball.transform.localScale = Vector3.one * (world / Mathf.Max(0.0001f, head.lossyScale.x));
            }
            Plugin.Debug("Hafgufa's eyes at " + EyeAt.ToString("F2") + " (head " + root.InverseTransformPoint(head.position).ToString("F2") + ")");
        }

        static void SoulLight(string boneName)
        {
            Transform bone;
            if (!bones.TryGetValue(boneName, out bone)) return;
            GameObject l = new GameObject("IoV_HafgufaSoul");
            l.transform.SetParent(bone, false);
            l.transform.position = Skin(bone, 0f, 0.6f);
            Light light = l.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = RanNet.SoulColour;
            light.range = 4f;
            light.intensity = 1.3f;
            light.shadows = LightShadows.None;
        }

        // What's drawn of the creature at rest (its skinned meshes as made), in its own space.
        static Bounds Drawn(GameObject go)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) continue;
                Bounds mb = r.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = go.transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }
    }

    // A rope of Rán's net between two of Hafgufa's bones, sagging, redrawn as it moves.
    // (Its fields public, so the prefab's copy keeps them.)
    class HafgufaRope : MonoBehaviour
    {
        public Transform a, b;
        public Vector3 offA, offB;
        public float sag = 1f;
        LineRenderer line;

        void Awake() { line = GetComponent<LineRenderer>(); }

        void LateUpdate()
        {
            if (line == null || a == null || b == null) return;
            Vector3 p = a.TransformPoint(offA), q = b.TransformPoint(offB);
            int n = line.positionCount;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 at = Vector3.Lerp(p, q, t);
                at.y -= Mathf.Sin(t * Mathf.PI) * sag;
                line.SetPosition(i, at);
            }
        }
    }
}
