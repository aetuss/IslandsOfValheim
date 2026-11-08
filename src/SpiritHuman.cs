using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandsOfValheim
{
    // A spirit of the Brim Folk in human form (the Valhalla arc): the player's own body, dressed and with hair
    // and beard, that stands or sits where it's put and does nothing else. Its looks are the game's own
    // (VisEquipment, as on a player); everything that makes a player a player is taken out. As a ghost, its
    // body and clothes are drawn see-through and pale, with a soft light about it.
    //
    // Its look and pose are kept in its ZDO, so every client shows the same spirit.
    class SpiritHuman : MonoBehaviour
    {
        internal const string PrefabName = "IoV_Spirit";

        // How a spirit is dressed. Items are prefab names (empty: none).
        internal class Look
        {
            internal int Model;              // 0 man, 1 woman
            internal string Hair = "", Beard = "", Helmet = "", Chest = "", Legs = "", Shoulder = "";
            internal Vector3 HairColor = new Vector3(0.6f, 0.45f, 0.3f);
            internal Vector3 SkinColor = Vector3.one;
        }

        internal const int Stand = 0, Sit = 1;

        // How the ghost is drawn. The game leaves out the Standard shader's see-through kind, so these use the
        // shader Odin's light beam uses, which it does draw see-through. Soft and veil differ only in how their
        // far side is kept from showing through (two shaders the game has; the test shows which works).
        internal const int Solid = 0, GhostSoft = 1, GhostFlat = 2, GhostShade = 3, GhostVeil = 4;

        // Set before Instantiate: the next spirit made here takes these (its owner writes them to its ZDO).
        internal static Look NextLook;
        internal static int NextPose;
        internal static string NextSitAnimation = "";
        internal static int NextGhost = GhostSoft;

        const string KeySet = "IoVSpirit.set", KeyPose = "IoVSpirit.pose", KeySitAnim = "IoVSpirit.sitanim", KeyGhost = "IoVSpirit.ghost";

        internal static void Register()
        {
            GameObject source = PrefabManager.Instance.GetPrefab("Player");
            if (source == null) { Plugin.Log.LogWarning("Spirits: no Player prefab"); return; }
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(PrefabName, source);
            for (int pass = 0; pass < 6; pass++)
            {
                bool any = false;
                MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = scripts.Length - 1; i >= 0; i--)
                {
                    if (scripts[i] is ZNetView || scripts[i] is VisEquipment) continue;
                    Object.DestroyImmediate(scripts[i]);
                    any = true;
                }
                if (!any) break;
            }
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c); // a ghost: walked through
            foreach (Animator animator in go.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
            ZNetView nview = go.GetComponent<ZNetView>();
            nview.m_persistent = true;
            VisEquipment vis = go.GetComponent<VisEquipment>();
            if (vis != null) vis.m_playerComponent = null;
            go.AddComponent<SpiritHuman>();
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
            Plugin.Log.LogInfo("Spirits: " + PrefabName + " made from the player's body (" + go.GetComponentsInChildren<Renderer>(true).Length + " renderers)");
        }

        ZNetView nview;
        VisEquipment vis;
        Animator animator;
        Light glow;
        bool loggedParams;
        int pose, ghost;
        string sitAnim = "";
        readonly HashSet<Renderer> ghosted = new HashSet<Renderer>();
        float nextGhostCheck;

        void Awake()
        {
            nextGhostCheck = Time.time + 1f; // its clothes are put on first
            nview = GetComponent<ZNetView>();
            vis = GetComponent<VisEquipment>();
            animator = GetComponentInChildren<Animator>(true);
            if (nview == null || nview.GetZDO() == null) return;
            ZDO zdo = nview.GetZDO();
            if (nview.IsOwner() && !zdo.GetBool(KeySet, false))
            {
                zdo.Set(KeySet, true);
                zdo.Set(KeyPose, NextPose);
                zdo.Set(KeySitAnim, NextSitAnimation ?? "");
                zdo.Set(KeyGhost, NextGhost);
                Dress(NextLook ?? new Look { Hair = "Hair3", Beard = "Beard5" });
                NextLook = null; NextPose = Stand; NextSitAnimation = ""; NextGhost = GhostSoft;
            }
            pose = zdo.GetInt(KeyPose, Stand);
            sitAnim = zdo.GetString(KeySitAnim, "");
            ghost = zdo.GetInt(KeyGhost, GhostSoft);
            if (ghost != Solid) AddGlow();
        }

        void Dress(Look look)
        {
            if (vis == null) return;
            vis.SetModel(look.Model);
            vis.SetSkinColor(look.SkinColor);
            vis.SetHairColor(look.HairColor);
            vis.SetHairItem(Hash(look.Hair));
            vis.SetBeardItem(Hash(look.Beard));
            vis.SetHelmetItem(Hash(look.Helmet));
            vis.SetChestItem(Hash(look.Chest));
            vis.SetLegItem(Hash(look.Legs));
            vis.SetShoulderItem(Hash(look.Shoulder), 0, 1);
        }

        static int Hash(string name) { return string.IsNullOrEmpty(name) ? 0 : name.GetStableHashCode(); }

        void AddGlow()
        {
            GameObject light = new GameObject("IoV_SpiritGlow");
            light.transform.SetParent(transform, false);
            light.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            glow = light.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.65f, 0.85f, 1f);
            glow.range = 3.5f;
            glow.intensity = 0.9f;
            glow.shadows = LightShadows.None;
        }

        void Update()
        {
            if (animator == null) return;
            if (!loggedParams)
            {
                loggedParams = true;
                List<string> names = new List<string>();
                foreach (AnimatorControllerParameter p in animator.parameters) names.Add(p.name + ":" + p.type);
                Plugin.Debug("Spirits: animator parameters " + string.Join(", ", names.ToArray()));
            }
            // As a player standing still on the ground; seated if it was put on a seat.
            SetBool("onGround", true);
            SetFloat("forward_speed", 0f);
            SetFloat("sideway_speed", 0f);
            if (pose == Sit && sitAnim.Length > 0) SetBool(sitAnim, true);
            if (ghost == Solid) return;
            // Unseen until it's dressed and made a ghost: no glimpse of a living man first.
            if (!shown)
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                    if (r != null && !ghosted.Contains(r)) r.forceRenderingOff = true;
            if (Time.time >= nextGhostCheck)
            {
                nextGhostCheck = Time.time + 0.5f;
                Ghostly();
                shown = true;
            }
        }

        bool shown;

        void SetBool(string name, bool value)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == name && p.type == AnimatorControllerParameterType.Bool) { animator.SetBool(name, value); return; }
        }

        void SetFloat(string name, float value)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == name && p.type == AnimatorControllerParameterType.Float) { animator.SetFloat(name, value); return; }
        }

        // Every part of it (body, hair, beard, clothes: they come and go as VisEquipment builds them) drawn
        // see-through and pale.
        void Ghostly()
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer || ghosted.Contains(r)) continue;
                ghosted.Add(r);
                r.forceRenderingOff = false;
                // The underwear's own little mesh: under clothes drawn see-through it would show white.
                if (r.name == "shorts") { r.enabled = false; continue; }
                Material[] mats = r.materials;
                // More materials than the mesh has parts draws its last part again, once per extra material:
                // solid, no one sees it; see-through, every layer thickens it.
                Mesh mesh = r is SkinnedMeshRenderer ? ((SkinnedMeshRenderer)r).sharedMesh
                    : r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null;
                int parts = mesh != null ? Mathf.Max(1, mesh.subMeshCount) : mats.Length;
                if (mats.Length > parts) System.Array.Resize(ref mats, parts);
                for (int i = 0; i < mats.Length; i++) mats[i] = GhostMaterial(mats[i], ghost, BodyTexture(mats[i]));
                r.materials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                Renderer depth = DepthTwin(r, mesh, parts, ghost);
                if (depth != null) ghosted.Add(depth);
            }
        }

        // ---- how a ghost is drawn ----

        static bool looked;
        static Shader sprite, blend, depthOnly, invisible;
        static Material painter, washer;
        static readonly Dictionary<string, Texture> painted = new Dictionary<string, Texture>();
        static readonly Color Wash = new Color(0.62f, 0.82f, 1f, 0.4f); // the pale blue over everything it wears

        // Shader.Find misses some of what the game has loaded; this doesn't.
        static Shader Loaded(string name)
        {
            foreach (Shader s in Resources.FindObjectsOfTypeAll<Shader>())
                if (s != null && s.name == name && s.isSupported) return s;
            return null;
        }

        static void Look4Shaders()
        {
            if (looked) return;
            looked = true;
            sprite = Loaded("Sprites/Default");
            blend = Loaded("Legacy Shaders/Particles/Alpha Blended");
            depthOnly = Loaded("Unlit/DepthWrite");
            invisible = Loaded("Unlit/Invis");
            Plugin.Debug("Spirits: shaders: see-through " + Name(sprite) + ", wash " + Name(blend) + ", depth " + Describe(depthOnly) + ", invisible " + Describe(invisible));
        }

        static string Name(Shader s) { return s != null ? s.name : "none"; }

        static string Describe(Shader s)
        {
            if (s == null) return "none";
            List<string> props = new List<string>();
            for (int i = 0; i < s.GetPropertyCount(); i++) props.Add(s.GetPropertyName(i));
            return s.name + " (queue " + s.renderQueue + ", " + s.passCount + " passes, properties " + string.Join(" ", props.ToArray()) + ")";
        }

        static Material GhostMaterial(Material source, int style, Texture body)
        {
            if (source == null) return source;
            Look4Shaders();
            if (sprite == null) return source;
            Material m = new Material(sprite);
            if (style == GhostShade) // no clothes' pattern at all: a pale see-through shape
                m.color = new Color(0.62f, 0.8f, 1f, 0.32f);
            else // its clothes and face, washed pale blue, see-through
            {
                Texture texture = body != null ? body : source.HasProperty("_MainTex") ? source.mainTexture : null;
                m.mainTexture = Washed(texture != null ? texture : Texture2D.whiteTexture);
                m.color = new Color(1f, 1f, 1f, style == GhostFlat ? 0.32f : 0.42f);
            }
            m.renderQueue = 3000;
            return m;
        }

        // Its own shape drawn first into the depth only, so only its nearest side is drawn see-through: the far
        // side of its body and the arm behind it don't show through and thicken it. (The flat ghost hasn't one,
        // to compare.)
        static Renderer DepthTwin(Renderer r, Mesh mesh, int parts, int style)
        {
            Shader shader = style == GhostVeil ? invisible : style == GhostSoft ? depthOnly : null;
            if (shader == null || mesh == null) return null;
            Material m = new Material(shader);
            m.renderQueue = 2990;
            Material[] mats = new Material[parts];
            for (int i = 0; i < parts; i++) mats[i] = m;
            GameObject twin = new GameObject("IoV_GhostDepth");
            twin.transform.SetParent(r.transform, false);
            Renderer made;
            SkinnedMeshRenderer skinned = r as SkinnedMeshRenderer;
            if (skinned != null)
            {
                SkinnedMeshRenderer s = twin.AddComponent<SkinnedMeshRenderer>();
                s.sharedMesh = skinned.sharedMesh;
                s.rootBone = skinned.rootBone;
                s.bones = skinned.bones;
                s.localBounds = skinned.localBounds;
                s.updateWhenOffscreen = skinned.updateWhenOffscreen;
                made = s;
            }
            else
            {
                twin.AddComponent<MeshFilter>().sharedMesh = mesh;
                made = twin.AddComponent<MeshRenderer>();
            }
            made.sharedMaterials = mats;
            made.shadowCastingMode = ShadowCastingMode.Off;
            made.receiveShadows = false;
            return made;
        }

        // A player's shirt and trousers aren't things of their own: the player's shader paints them over the
        // skin (_ChestTex, _LegsTex). The see-through shader can't, so for the body they're painted together
        // once, here, into one picture: skin, then trousers, then shirt.
        Texture BodyTexture(Material source)
        {
            if (source == null || !source.HasProperty("_ChestTex") || nview == null || nview.GetZDO() == null) return null;
            ZDO zdo = nview.GetZDO();
            Texture skin = source.mainTexture;
            int model = zdo.GetInt(ZDOVars.s_modelIndex, 0);
            if (vis != null && vis.m_models != null && model >= 0 && model < vis.m_models.Length && vis.m_models[model].m_baseMaterial != null
                && vis.m_models[model].m_baseMaterial.mainTexture != null)
                skin = vis.m_models[model].m_baseMaterial.mainTexture;
            if (skin == null) return null;
            Texture legs = ArmorTexture(zdo.GetInt(ZDOVars.s_legItem, 0), "_LegsTex");
            Texture chest = ArmorTexture(zdo.GetInt(ZDOVars.s_chestItem, 0), "_ChestTex");
            if (legs == null && chest == null) return skin;
            return Paint(skin, false, legs, chest);
        }

        static Texture Washed(Texture texture) { return Paint(texture, true); }

        static Texture ArmorTexture(int hash, string property)
        {
            if (hash == 0 || ObjectDB.instance == null) return null;
            GameObject item = ObjectDB.instance.GetItemPrefab(hash);
            ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
            Material armor = drop != null ? drop.m_itemData.m_shared.m_armorMaterial : null;
            return armor != null && armor.HasProperty(property) ? armor.GetTexture(property) : null;
        }

        // Paints the layers over the base into a new picture (kept, so each picture is painted once). Washed:
        // pale blue laid over its colours alone, so what was see-through in it (hair's edges) stays so.
        static Texture Paint(Texture under, bool wash, params Texture[] over)
        {
            string key = under.GetInstanceID() + (wash ? "w" : "");
            foreach (Texture layer in over) key += "/" + (layer != null ? layer.GetInstanceID() : 0);
            Texture made;
            if (painted.TryGetValue(key, out made) && made != null) return made;
            if (wash && blend == null) return under;
            int size = under.width; // as sharp as its sharpest layer (the skin's own picture is small)
            foreach (Texture layer in over) if (layer != null) size = Mathf.Max(size, layer.width);
            size = Mathf.Clamp(size, 16, 2048);
            int height = over.Length == 0 ? Mathf.Clamp(under.height, 16, 2048) : size;
            RenderTexture target = new RenderTexture(size, height, 0, RenderTextureFormat.ARGB32);
            target.Create();
            Graphics.Blit(under, target);
            RenderTexture before = RenderTexture.active;
            RenderTexture.active = target;
            GL.PushMatrix();
            GL.LoadOrtho();
            foreach (Texture layer in over)
            {
                if (layer == null) continue;
                if (painter == null) painter = new Material(sprite);
                painter.mainTexture = layer;
                painter.color = Color.white;
                Quad(painter);
            }
            if (wash)
            {
                // The particle shader leaves the picture's see-through alone and doubles what it's given.
                if (washer == null) washer = new Material(blend);
                washer.mainTexture = Texture2D.whiteTexture;
                washer.SetColor("_TintColor", new Color(Wash.r * 0.5f, Wash.g * 0.5f, Wash.b * 0.5f, Wash.a * 0.5f));
                Quad(washer);
            }
            GL.PopMatrix();
            RenderTexture.active = before;
            painted[key] = target;
            return target;
        }

        static void Quad(Material m)
        {
            m.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(Color.white);
            GL.TexCoord2(0f, 0f); GL.Vertex3(0f, 0f, 0f);
            GL.TexCoord2(0f, 1f); GL.Vertex3(0f, 1f, 0f);
            GL.TexCoord2(1f, 1f); GL.Vertex3(1f, 1f, 0f);
            GL.TexCoord2(1f, 0f); GL.Vertex3(1f, 0f, 0f);
            GL.End();
        }

        // ---- the test (development builds): iov_spirits ----

        // In a row in front of you, left to right: the man solid (to compare), then the ways of drawing a ghost
        // (soft, veil, flat, shade), then a woman seated on a bench (soft). The spirits a test made before,
        // near you, go first.
        internal static void SpawnTest(Terminal context)
        {
            Player player = Player.m_localPlayer;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(PrefabName) : null;
            if (player == null || prefab == null) { context.AddString("No spirit prefab."); return; }
            Vector3 p = player.transform.position, f = player.transform.forward, r = player.transform.right;
            f.y = 0f; f.Normalize(); r.y = 0f; r.Normalize();
            Quaternion facing = Quaternion.LookRotation(-f);

            int removed = 0;
            foreach (SpiritHuman old in Object.FindObjectsOfType<SpiritHuman>())
            {
                if (Vector3.Distance(old.transform.position, p) > 25f || old.nview == null || !old.nview.IsValid()) continue;
                old.nview.ClaimOwnership();
                ZNetScene.instance.Destroy(old.gameObject);
                removed++;
            }

            Look sailor = new Look { Hair = "Hair3", Beard = "Beard5", Helmet = SeafarerGear.Hat, Chest = SeafarerGear.Vest, Legs = SeafarerGear.Trousers, Shoulder = SeafarerGear.Cape };
            int[] styles = { Solid, GhostSoft, GhostVeil, GhostFlat, GhostShade };
            for (int i = 0; i < styles.Length; i++)
            {
                NextLook = sailor; NextPose = Stand; NextGhost = styles[i];
                Object.Instantiate(prefab, Ground(p + f * 4f + r * (i * 2.5f - 7.5f)), facing);
            }

            Look woman = new Look { Model = 1, Hair = "Hair12", Chest = SeafarerGear.Vest, Legs = SeafarerGear.Trousers, HairColor = new Vector3(0.85f, 0.75f, 0.55f) };
            GameObject bench = ZNetScene.instance.GetPrefab("piece_bench01");
            Chair chair = bench != null ? bench.GetComponentInChildren<Chair>(true) : null;
            if (bench != null && chair != null && chair.m_attachPoint != null)
            {
                Vector3 at = Ground(p + f * 4f + r * 5.5f);
                Quaternion turn = Quaternion.LookRotation(-f);
                Object.Instantiate(bench, at, turn);
                Vector3 seat = at + turn * bench.transform.InverseTransformPoint(chair.m_attachPoint.position);
                Quaternion seatRot = turn * (Quaternion.Inverse(bench.transform.rotation) * chair.m_attachPoint.rotation);
                NextLook = woman; NextPose = Sit; NextSitAnimation = chair.m_attachAnimation; NextGhost = GhostSoft;
                Object.Instantiate(prefab, seat, seatRot);
                Plugin.Log.LogInfo("Spirits: seated on piece_bench01 with " + chair.m_attachAnimation);
            }
            else context.AddString("No bench with a seat found.");

            context.AddString("Spirits (" + removed + " old ones removed), left to right: solid, soft, veil, flat, shade, and seated on a bench (soft).");
        }

        static Vector3 Ground(Vector3 p)
        {
            if (ZoneSystem.instance != null) p.y = ZoneSystem.instance.GetSolidHeight(p);
            return p;
        }
    }
}
