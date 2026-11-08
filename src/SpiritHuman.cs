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

        // How the ghost is drawn. The game leaves out the Standard shader's see-through kind, so these use
        // shaders it does draw see-through (the ones Odin's light beam and the game's own smoke use).
        internal const int Solid = 0, GhostSoft = 1, GhostGlow = 2, GhostShade = 3;

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
            if (ghost != Solid && Time.time >= nextGhostCheck)
            {
                nextGhostCheck = Time.time + 0.5f;
                Ghostly();
            }
        }

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
        // see-through, pale and faintly glowing.
        void Ghostly()
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer || ghosted.Contains(r)) continue;
                Material[] mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!loggedShaders && mats[i] != null && mats[i].shader != null)
                        Plugin.Debug("Spirits: " + r.name + " was drawn with " + mats[i].shader.name);
                    mats[i] = GhostMaterial(mats[i], ghost);
                }
                r.materials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                ghosted.Add(r);
            }
            loggedShaders = true;
        }

        bool loggedShaders;
        static Shader sprite, additive;

        static Material GhostMaterial(Material source, int style)
        {
            if (source == null) return source;
            if (sprite == null) sprite = Shader.Find("Sprites/Default");
            if (additive == null) additive = Shader.Find("Legacy Shaders/Particles/Additive");
            Texture texture = source.HasProperty("_MainTex") ? source.mainTexture : null;
            Material m;
            switch (style)
            {
                case GhostGlow: // light only: glows, the darker its clothes the fainter
                    if (additive == null) return source;
                    m = new Material(additive);
                    if (texture != null) m.mainTexture = texture;
                    m.SetColor("_TintColor", new Color(0.3f, 0.45f, 0.6f, 0.6f));
                    break;
                case GhostShade: // no clothes' pattern at all: a pale see-through shape
                    if (sprite == null) return source;
                    m = new Material(sprite);
                    m.color = new Color(0.62f, 0.8f, 1f, 0.32f);
                    break;
                default: // its clothes and face as they are, pale blue and half see-through
                    if (sprite == null) return source;
                    m = new Material(sprite);
                    if (texture != null) m.mainTexture = texture;
                    m.color = new Color(0.72f, 0.86f, 1f, 0.5f);
                    break;
            }
            m.renderQueue = 3000;
            return m;
        }

        // ---- the test (development builds): iov_spirits ----

        // In a row in front of you, left to right: the man solid (to compare), then the three ways of drawing a
        // ghost (soft, glowing, shade), then a woman seated on a bench (soft). The spirits a test made before,
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
            int[] styles = { Solid, GhostSoft, GhostGlow, GhostShade };
            for (int i = 0; i < styles.Length; i++)
            {
                NextLook = sailor; NextPose = Stand; NextGhost = styles[i];
                Object.Instantiate(prefab, Ground(p + f * 4f + r * (i * 2.5f - 5f)), facing);
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

            context.AddString("Spirits (" + removed + " old ones removed), left to right: solid, soft, glowing, shade, and seated on a bench (soft).");
        }

        static Vector3 Ground(Vector3 p)
        {
            if (ZoneSystem.instance != null) p.y = ZoneSystem.instance.GetSolidHeight(p);
            return p;
        }
    }
}
