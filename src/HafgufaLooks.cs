using System.Collections;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // What makes the game's serpent Rán's beast (the design: "rocks, shells, weed and net ropes on it, glowing
    // eyes"): the game's own models, hung on its bones so they move with it.
    // - rocks grown on its back, in the deep's colours, small ones like barnacles between;
    // - dark weed on its flanks;
    // - torn ropes of Rán's net hanging in loops along its flanks (drawn as lines each frame);
    // - two cold eyes on its head, and a few drowned souls' lights caught in its body.
    // Put on its real skin: a moment after it comes into the world, its body as drawn right then is read once
    // (the skinned mesh baked), and each thing goes on the skin near its bone, then rides on that bone. (1.10.79/80
    // placed them from its bones at rest on the prefab: the bones there don't sit where the body is drawn, and
    // the things floated off it.) Each client dresses its own.
    static class HafgufaLooks
    {
        // On the prefab: what dresses each Hafgufa, and the souls' lights (lights need no skin).
        internal static void Dress(GameObject go)
        {
            go.AddComponent<HafgufaDress>();
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == "Tail3" || t.name == "Tail6" || t.name == "Tail9")
                {
                    GameObject l = new GameObject("IoV_HafgufaSoul");
                    l.transform.SetParent(t, false);
                    Light light = l.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = RanNet.SoulColour;
                    light.range = 4f;
                    light.intensity = 1.3f;
                    light.shadows = LightShadows.None;
                }
        }
    }

    class HafgufaDress : MonoBehaviour
    {
        // what goes where: the model, the bone, side (-1 left .. 1 right), up (0 its flank .. 1 its back), size in m
        struct Thing { internal string Model, Bone; internal float Side, Up, Size, Yaw; internal bool Weed; }

        static readonly Thing[] Things =
        {
            new Thing { Model = "Rock_3", Bone = "Main",  Side = 0f,    Up = 1f, Size = 1.5f,  Yaw = 20f },
            new Thing { Model = "Rock_3", Bone = "Tail2", Side = 0.2f,  Up = 1f, Size = 1.25f, Yaw = 140f },
            new Thing { Model = "Rock_3", Bone = "Tail4", Side = -0.2f, Up = 1f, Size = 1.1f,  Yaw = 260f },
            new Thing { Model = "Rock_3", Bone = "Tail7", Side = 0.1f,  Up = 1f, Size = 0.9f,  Yaw = 80f },
            new Thing { Model = "Rock_3", Bone = "Tail1", Side = -0.6f, Up = 0.8f, Size = 0.55f, Yaw = 10f },
            new Thing { Model = "Rock_3", Bone = "Tail3", Side = 0.6f,  Up = 0.8f, Size = 0.5f,  Yaw = 200f },
            new Thing { Model = "Rock_3", Bone = "Tail5", Side = -0.6f, Up = 0.8f, Size = 0.5f,  Yaw = 120f },
            new Thing { Model = "FreshSeaweed", Bone = "Main",  Side = 1f,  Up = 0.3f, Size = 1.5f, Yaw = 90f,  Weed = true },
            new Thing { Model = "FreshSeaweed", Bone = "Main",  Side = -1f, Up = 0.3f, Size = 1.5f, Yaw = 270f, Weed = true },
            new Thing { Model = "FreshSeaweed", Bone = "Tail2", Side = 1f,  Up = 0.3f, Size = 1.4f, Yaw = 90f,  Weed = true },
            new Thing { Model = "FreshSeaweed", Bone = "Tail2", Side = -1f, Up = 0.3f, Size = 1.4f, Yaw = 270f, Weed = true },
            new Thing { Model = "FreshSeaweed", Bone = "Tail4", Side = 1f,  Up = 0.3f, Size = 1.3f, Yaw = 90f,  Weed = true },
            new Thing { Model = "FreshSeaweed", Bone = "Tail4", Side = -1f, Up = 0.3f, Size = 1.3f, Yaw = 270f, Weed = true },
            new Thing { Model = "FreshSeaweed", Bone = "Tail6", Side = 1f,  Up = 0.3f, Size = 1.1f, Yaw = 90f,  Weed = true },
            new Thing { Model = "FreshSeaweed", Bone = "Tail6", Side = -1f, Up = 0.3f, Size = 1.1f, Yaw = 270f, Weed = true },
        };

        // the ropes: from a bone to another, on one flank, and how far they sag (m)
        struct Line { internal string From, To; internal float Side, Sag; }

        static readonly Line[] Ropes =
        {
            new Line { From = "Main",  To = "Tail3", Side = 1f,  Sag = 1.2f },
            new Line { From = "Tail1", To = "Tail4", Side = -1f, Sag = 1.4f },
            new Line { From = "Tail4", To = "Tail7", Side = 1f,  Sag = 1.2f },
            new Line { From = "Tail6", To = "Tail9", Side = -1f, Sag = 1.1f },
        };

        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        Vector3[] skin;      // its skin as drawn when it was dressed, in the world
        float reach = 6f;    // how far round a bone its skin is looked for

        IEnumerator Start()
        {
            yield return new WaitForSeconds(0.6f); // (its animator has posed it by then)
            try { Dress(); }
            catch (System.Exception e) { Plugin.Log.LogWarning("Hafgufa's looks: " + e.Message); }
        }

        void Dress()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            ReadSkin();
            foreach (Thing t in Things) Put(t);
            Material rope = RopeMaterial();
            if (rope != null) foreach (Line l in Ropes) Rope(rope, l);
            Eyes();
        }

        // Its body as drawn right now (the biggest of its skinned meshes), in the world.
        void ReadSkin()
        {
            SkinnedMeshRenderer body = null;
            foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMesh != null && (body == null || r.sharedMesh.vertexCount > body.sharedMesh.vertexCount)) body = r;
            if (body == null) { Plugin.Debug("Hafgufa: no skin to read"); return; }
            Mesh baked = new Mesh();
            body.BakeMesh(baked, true);
            Vector3[] local = baked.vertices;
            Matrix4x4 m = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
            skin = new Vector3[local.Length];
            for (int i = 0; i < local.Length; i++) skin[i] = m.MultiplyPoint3x4(local[i]);
            Destroy(baked);
            // its girth: how far its skin is from its middle bone, across (in a thin slice there)
            Transform main;
            if (bones.TryGetValue("Main", out main) && skin.Length > 0)
            {
                float far = 0f;
                foreach (Vector3 v in skin)
                {
                    Vector3 d = v - main.position;
                    if (Mathf.Abs(Vector3.Dot(d, transform.forward)) > 1.5f) continue;
                    far = Mathf.Max(far, Vector3.ProjectOnPlane(d, transform.forward).magnitude);
                }
                reach = Mathf.Clamp(far * 1.3f, 2f, 10f);
            }
            Plugin.Debug("Hafgufa's skin read: " + skin.Length + " points, looked for " + reach.ToString("F1") + " m round each bone");
        }

        // The point of its skin near a bone that lies farthest that way (side, up), or null.
        Vector3? Skin(Transform bone, float side, float up)
        {
            if (skin == null || skin.Length == 0) return null;
            Vector3 dir = transform.right * side + transform.up * up;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.up;
            dir.Normalize();
            float best = float.MinValue;
            Vector3 found = bone.position;
            bool any = false;
            foreach (Vector3 v in skin)
            {
                Vector3 d = v - bone.position;
                if (d.sqrMagnitude > reach * reach) continue;
                // (a slice of the body round the bone, not the whole ball: no point from the next bend over)
                Vector3 along = Vector3.Project(d, transform.forward);
                if (along.magnitude > reach * 0.5f) continue;
                float s = Vector3.Dot(d, dir);
                if (s > best) { best = s; found = v; any = true; }
            }
            return any ? (Vector3?)found : null;
        }

        static readonly Dictionary<string, Material[]> tinted = new Dictionary<string, Material[]>();
        static readonly Recolor.Shift Dark = BossKit.Cast(new Color(0.08f, 0.14f, 0.16f), 0.65f, 0.6f);
        static readonly Recolor.Shift Weed = BossKit.Cast(new Color(0.12f, 0.2f, 0.1f), 0.6f, 0.7f);

        void Put(Thing thing)
        {
            Transform bone;
            if (!bones.TryGetValue(thing.Bone, out bone)) return;
            Vector3? at = Skin(bone, thing.Side, thing.Up);
            if (at == null) return;
            GameObject source = PrefabManager.Instance.GetPrefab(thing.Model);
            if (source == null) return;
            MeshFilter filter = null;
            MeshRenderer drawn = null;
            foreach (MeshFilter f in source.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer r = f.GetComponent<MeshRenderer>();
                if (f.sharedMesh == null || r == null) continue;
                filter = f; drawn = r;
                break;
            }
            if (filter == null) return;
            GameObject part = new GameObject("IoV_Hafgufa_" + thing.Model);
            part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            string key = thing.Model + (thing.Weed ? "_weed" : "_rock");
            Material[] materials;
            if (!tinted.TryGetValue(key, out materials))
            {
                renderer.sharedMaterials = drawn.sharedMaterials;
                BossKit.Tint(part, thing.Weed ? Weed : Dark, 0f, "Hafgufa's " + thing.Model);
                NoSnow(renderer, thing.Model);
                materials = renderer.sharedMaterials;
                tinted[key] = materials;
            }
            renderer.sharedMaterials = materials;
            Bounds mb = filter.sharedMesh.bounds;
            float extent = Mathf.Max(mb.size.x, Mathf.Max(mb.size.y, mb.size.z));
            float scale = thing.Size / Mathf.Max(0.001f, extent);
            part.transform.localScale = Vector3.one * scale;
            Vector3 outward = (at.Value - bone.position).normalized;
            part.transform.rotation = Quaternion.FromToRotation(Vector3.up, outward) * Quaternion.Euler(0f, thing.Yaw, 0f);
            // its middle on the skin, sunk in a third of its size (grown there, not set down on it)
            part.transform.position = at.Value - part.transform.rotation * (mb.center * scale) - outward * (thing.Size * (thing.Weed ? 0.15f : 0.3f));
            part.transform.SetParent(bone, true);
        }

        // A rock's shader can lay snow on whatever faces up (the first rocks came out snow-white): none on it.
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
                Plugin.Debug("Hafgufa's " + what + ": " + s.name + (found.Count > 0 ? ", snow off (" + string.Join(", ", found.ToArray()) + ")" : ", no snow settings"));
            }
        }

        static Material ropeMaterial, eyeMaterial;

        static Material RopeMaterial()
        {
            if (ropeMaterial != null) return ropeMaterial;
            Shader shader = ValhallaClouds.Find("Sprites/Default");
            if (shader == null) return null;
            ropeMaterial = new Material(shader);
            ropeMaterial.color = new Color(0.45f, 0.39f, 0.3f, 1f); // old hemp (darker came out as black sticks in the mist)
            return ropeMaterial;
        }

        void Rope(Material material, Line l)
        {
            Transform a, b;
            if (!bones.TryGetValue(l.From, out a) || !bones.TryGetValue(l.To, out b)) return;
            Vector3? p = Skin(a, l.Side, 0.3f), q = Skin(b, l.Side, 0.3f);
            if (p == null || q == null) return;
            GameObject go = new GameObject("IoV_HafgufaRope");
            go.transform.SetParent(a, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 9;
            line.widthMultiplier = 0.12f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            HafgufaRope r = go.AddComponent<HafgufaRope>();
            r.a = a; r.b = b;
            r.offA = a.InverseTransformPoint(p.Value);
            r.offB = b.InverseTransformPoint(q.Value);
            r.sag = l.Sag;
        }

        // Two cold eyes either side of its head, a little behind the tip of its snout, on its skin.
        void Eyes()
        {
            Transform head, main;
            if (skin == null || !bones.TryGetValue("Head", out head) || !bones.TryGetValue("Main", out main)) return;
            Vector3 forward = head.position - main.position;
            if (forward.sqrMagnitude < 0.01f) forward = transform.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(transform.up, forward).normalized;
            float snout = 0f;
            foreach (Vector3 v in skin)
            {
                Vector3 d = v - head.position;
                if (Vector3.ProjectOnPlane(d, forward).magnitude > reach) continue;
                snout = Mathf.Max(snout, Vector3.Dot(d, forward));
            }
            if (snout <= 0.1f) { Plugin.Debug("Hafgufa: no snout found for its eyes"); return; }
            float at = snout * 0.62f;
            if (eyeMaterial == null)
            {
                Shader shader = ValhallaClouds.Find("Sprites/Default");
                if (shader == null) return;
                eyeMaterial = new Material(shader);
                eyeMaterial.color = new Color(0.75f, 1f, 1f, 1f);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 want = (right * s + transform.up * 0.7f).normalized;
                float best = float.MinValue;
                Vector3 eye = head.position;
                foreach (Vector3 v in skin)
                {
                    Vector3 d = v - head.position;
                    if (Mathf.Abs(Vector3.Dot(d, forward) - at) > 0.6f || Vector3.ProjectOnPlane(d, forward).magnitude > reach) continue;
                    float score = Vector3.Dot(d, want);
                    if (score > best) { best = score; eye = v; }
                }
                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(ball.GetComponent<Collider>());
                ball.name = "IoV_HafgufaEye";
                MeshRenderer r = ball.GetComponent<MeshRenderer>();
                r.sharedMaterial = eyeMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ball.transform.localScale = Vector3.one * 0.42f;
                ball.transform.position = eye;
                ball.transform.SetParent(head, true);
            }
            Plugin.Debug("Hafgufa's eyes " + at.ToString("F1") + " m ahead of its head bone (snout " + snout.ToString("F1") + " m)");
        }
    }

    // A rope of Rán's net between two of Hafgufa's bones, sagging, redrawn as it moves.
    // (Its fields public, so a copy keeps them.)
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
