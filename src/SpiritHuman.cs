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

        // Set before Instantiate: the next spirit made here takes these (its owner writes them to its ZDO).
        internal static Look NextLook;
        internal static int NextPose;
        internal static string NextSitAnimation = "";
        internal static bool NextGhost = true;

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
        bool ghost, loggedParams;
        int pose;
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
                NextLook = null; NextPose = Stand; NextSitAnimation = ""; NextGhost = true;
            }
            pose = zdo.GetInt(KeyPose, Stand);
            sitAnim = zdo.GetString(KeySitAnim, "");
            ghost = zdo.GetBool(KeyGhost, true);
            if (ghost) AddGlow();
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
            if (ghost && Time.time >= nextGhostCheck)
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
                for (int i = 0; i < mats.Length; i++) mats[i] = GhostMaterial(mats[i]);
                r.materials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                ghosted.Add(r);
            }
        }

        static Shader standard;

        static Material GhostMaterial(Material source)
        {
            if (standard == null) standard = Shader.Find("Standard");
            if (standard == null || source == null) return source;
            Material m = new Material(standard);
            if (source.HasProperty("_MainTex") && source.mainTexture != null) m.mainTexture = source.mainTexture;
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            m.color = new Color(0.75f, 0.88f, 1f, 0.45f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.25f, 0.4f, 0.55f));
            m.SetFloat("_Glossiness", 0.1f);
            return m;
        }

        // ---- the test (development builds): iov_spirits ----

        // A standing ghost in front of you, a seated one on a bench to your right, and the same man solid to
        // your left, to compare.
        internal static void SpawnTest(Terminal context)
        {
            Player player = Player.m_localPlayer;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(PrefabName) : null;
            if (player == null || prefab == null) { context.AddString("No spirit prefab."); return; }
            Vector3 p = player.transform.position, f = player.transform.forward, r = player.transform.right;
            f.y = 0f; f.Normalize(); r.y = 0f; r.Normalize();
            Quaternion facing = Quaternion.LookRotation(-f);

            Look sailor = new Look { Hair = "Hair3", Beard = "Beard5", Helmet = SeafarerGear.Hat, Chest = SeafarerGear.Vest, Legs = SeafarerGear.Trousers, Shoulder = SeafarerGear.Cape };
            NextLook = sailor; NextPose = Stand; NextGhost = true;
            Object.Instantiate(prefab, Ground(p + f * 3f), facing);

            Look woman = new Look { Model = 1, Hair = "Hair12", Chest = SeafarerGear.Vest, Legs = SeafarerGear.Trousers, HairColor = new Vector3(0.85f, 0.75f, 0.55f) };
            GameObject bench = ZNetScene.instance.GetPrefab("piece_bench01");
            Chair chair = bench != null ? bench.GetComponentInChildren<Chair>(true) : null;
            if (bench != null && chair != null && chair.m_attachPoint != null)
            {
                Vector3 at = Ground(p + f * 3f + r * 3f);
                Quaternion turn = Quaternion.LookRotation(-f);
                Object.Instantiate(bench, at, turn);
                Vector3 seat = at + turn * bench.transform.InverseTransformPoint(chair.m_attachPoint.position);
                Quaternion seatRot = turn * (Quaternion.Inverse(bench.transform.rotation) * chair.m_attachPoint.rotation);
                NextLook = woman; NextPose = Sit; NextSitAnimation = chair.m_attachAnimation; NextGhost = true;
                Object.Instantiate(prefab, seat, seatRot);
                Plugin.Log.LogInfo("Spirits: seated on piece_bench01 with " + chair.m_attachAnimation);
            }
            else context.AddString("No bench with a seat found.");

            NextLook = sailor; NextPose = Stand; NextGhost = false;
            Object.Instantiate(prefab, Ground(p + f * 3f - r * 3f), facing);
            context.AddString("Spirits: a ghost in front of you, one seated to your right, the same man solid to your left.");
        }

        static Vector3 Ground(Vector3 p)
        {
            if (ZoneSystem.instance != null) p.y = ZoneSystem.instance.GetSolidHeight(p);
            return p;
        }
    }
}
