using System.Collections;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // T2 on the tale's islets (TaleCaptains): what each captain left undone, there to be done. These are static
    // parts of the islet (they're in every world, built before or after: the islet's static parts are made
    // again from its prefab each time it loads); what's been done is the world's (its keys), and creatures that
    // come for you are made on your game, as the tale's others are.
    static partial class IsletBuilder
    {
        // Ketill's: Hrafn's stone, never raised, lying on its back by the graves on the ridge.
        static void HrafnsStone()
        {
            Vector2 c; Quaternion turn; float bottom;
            RidgeSpot(2.5f, out c, out turn, out bottom);
            Vector3 off = turn * new Vector3(-0.6f, 0f, -1.7f);
            Vector2 at = new Vector2(c.x + off.x, c.y + off.z);
            GameObject stone = Copy(new[] { "RuneStone_Swamps", "RuneStone_Meadows" }, Vector3.zero, 0f, true);
            if (stone == null) { Plugin.Debug("T2: no rune stone for Hrafn"); return; }
            // On a pivot at its foot (standing, the stone's base on the ground there), which tips it back to lie
            // flat, and stands it up when it's raised.
            GameObject pivot = new GameObject("IoV_HrafnStone");
            pivot.transform.SetParent(parent, false);
            float yaw = turn.eulerAngles.y + 180f;
            pivot.transform.localPosition = new Vector3(at.x, GroundMin(at.x, at.y, 0.6f), at.y);
            pivot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            stone.transform.SetParent(pivot.transform, false);
            stone.transform.localPosition = Vector3.zero;
            // turned about on its pivot: the model's runes are on its back, and raised they face the camp
            // (lying, they face the sky)
            stone.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Bounds b = OwnMeshBounds(stone.transform);
            stone.transform.localPosition = -(stone.transform.localRotation * new Vector3(b.center.x, b.min.y, b.center.z)); // its foot on the pivot
            HrafnStone raise = pivot.AddComponent<HrafnStone>();
            raise.yaw = yaw;
            raise.lift = Mathf.Min(b.size.x, b.size.z) * 0.5f;
            // the dead come up in the cold camp, before the tale's stones (not on the ridge behind you)
            Vector2 camp = Polar(cliffDeg, (PlateauAt(cliffDeg) - 0.8f) * 0.42f);
            raise.ambushAt = new Vector3(camp.x, GroundMin(camp.x, camp.y, 0.5f), camp.y);
            // lying on its back (lifted by half its thickness, so it lies on the ground, not in it)
            pivot.transform.localPosition += Vector3.up * raise.lift;
            pivot.transform.localRotation = Quaternion.Euler(-86f, yaw, 0f);
            built++;
        }

        // Ormr's: his last diary stone, apart from the other three (on the far side of the row), half sunk in the
        // moss, Seekers' eggs by it. Read once the captains' task has begun.
        static void OrmrsLastStone()
        {
            float deg = cliffDeg - 85f;
            Vector2 at = Polar(deg, PlateauAt(deg) - 0.6f);
            GameObject stone = Copy(new[] { "RuneStone_Mistlands", "RuneStone_Meadows" }, new Vector3(0f, 90f - deg, 0f), 0f, true); // faces the middle, as the others
            if (stone == null) { Plugin.Debug("T2: no rune stone for Ormr"); return; }
            Bounds b = LocalBounds(stone);
            Move(stone, b, new Vector3(at.x, GroundMin(at.x, at.y, 0.6f) - 0.2f, at.y), false);
            stone.name = "IoV_OrmrLastStone";
            OrmrsStone read = stone.AddComponent<OrmrsStone>();
            // the Seekers come up on the plateau, between the stone and the ruin
            Vector2 near = Polar(deg, PlateauAt(deg) * 0.45f);
            read.ambushAt = new Vector3(near.x, GroundMin(near.x, near.y, 0.5f), near.y);
            quietProps = true;
            Prop(new[] { "SeekerEgg" }, Polar(deg + 7f, PlateauAt(deg + 7f) - 1.4f), deg * 3f, 0.15f);
            Prop(new[] { "SeekerEgg" }, Polar(deg - 6f, PlateauAt(deg - 6f) - 1.8f), deg * 5f, 0.2f);
            quietProps = false;
            Plugin.Debug("T2: Ormr's last stone at " + at.ToString("F1"));
        }

        // Grímr's: the little wooden wolf his mother carved him, left where he began to keep watch. A copy of the
        // game's wolf (a cub if there is one), stilled, small, in the grain of wood, on its own holder: the holder
        // is what's looked at and taken (its fields public, so the location's copy keeps them).
        static void GrimrsToy(Vector2 at, float yaw, float floor, Vector3 ambushAt)
        {
            GameObject wolf = Copy(new[] { "Wolf_cub", "Wolf" }, Vector3.zero, 0f, true);
            if (wolf == null) { Plugin.Debug("T2: no wolf for Grímr's toy"); return; }
            foreach (Animator a in wolf.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (Collider c in wolf.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (ParticleSystem p in wolf.GetComponentsInChildren<ParticleSystem>(true))
                if (p.gameObject != wolf) p.gameObject.SetActive(false);
            Material wood = WoodMaterial();
            foreach (Renderer r in wolf.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || wood == null) continue;
                Material[] grain = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < grain.Length; i++) grain[i] = wood;
                r.sharedMaterials = grain;
            }

            GameObject holder = new GameObject("IoV_GrimrToy");
            holder.transform.SetParent(parent, false);
            holder.layer = LayerMask.NameToLayer("item");
            wolf.transform.SetParent(holder.transform, false);
            wolf.transform.localPosition = Vector3.zero;
            wolf.transform.localRotation = Quaternion.identity;
            Bounds b = DrawnBounds(holder.transform);
            const float Length = 0.42f; // a toy, held in two hands
            float k = Length / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            wolf.transform.localScale *= k;
            b = DrawnBounds(holder.transform);
            wolf.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z); // on its feet, on the holder
            BoxCollider box = holder.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, b.size.y * 0.5f, 0f);
            box.size = new Vector3(b.size.x + 0.12f, b.size.y + 0.1f, b.size.z + 0.12f);
            holder.transform.localPosition = new Vector3(at.x, floor + 0.01f, at.y);
            holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            GrimrsWolf toy = holder.AddComponent<GrimrsWolf>();
            toy.ambushAt = ambushAt;
            built++;
            Plugin.Debug("T2: Grímr's toy (" + wolf.name + ", " + (wood != null ? "wood" : "its own skin") + ") at " + at.ToString("F1") + ", " + b.size.ToString("F2") + " m");
        }

        // A wooden piece's own material, for a carving.
        static Material WoodMaterial()
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab("wood_beam");
            if (prefab == null) prefab = PrefabManager.Instance.GetPrefab("wood_pole");
            MeshRenderer r = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>(true) : null;
            return r != null ? r.sharedMaterial : null;
        }

        // What's drawn under t (skinned or not), in t's own space: a skinned mesh as it lies at rest.
        static Bounds DrawnBounds(Transform t)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                SkinnedMeshRenderer skinned = r as SkinnedMeshRenderer;
                if (skinned != null) mesh = skinned.sharedMesh;
                else if (r is MeshRenderer) { MeshFilter f = r.GetComponent<MeshFilter>(); if (f != null) mesh = f.sharedMesh; }
                if (mesh == null) continue;
                Bounds mb = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = t.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        // The bounds of what's drawn of a thing, in its own space.
        static Bounds OwnMeshBounds(Transform t)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (MeshFilter filter in t.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = t.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }
    }

    // Hrafn's stone: lying, it can be raised (once the captains' task has begun); raising it brings Ketill's
    // crew up out of the swamp one last time; raised, it's read as the game's rune stones are.
    // (Its fields are public so the location's copy keeps them.)
    class HrafnStone : MonoBehaviour, Hoverable, Interactable
    {
        public float lift;          // how far the pivot is lifted while lying (half the stone's thickness)
        public float yaw;           // which way it faces, standing
        public Vector3 ambushAt;    // where Ketill's crew comes up, in the islet's own space
        bool standing, raising;
        float nextLook;

        bool Raised { get { return TaleCaptains.Found(TaleCaptains.Ketill); } }

        void Start() { if (Raised) Stand(); }

        // (raised by someone else in the meantime: up it goes, here too)
        void Update()
        {
            if (standing || raising || Time.time < nextLook) return;
            nextLook = Time.time + 1f;
            if (Raised) Stand();
        }

        void Stand()
        {
            standing = true;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            transform.localPosition -= Vector3.up * lift;
        }

        public string GetHoverText()
        {
            if (standing) return Localization.instance.Localize("$iov_t2_hrafn_name\n[<color=yellow><b>$KEY_Use</b></color>] $piece_rune_read");
            if (raising || !TaleCaptains.Begun) return Localization.instance.Localize("$iov_t2_hrafn_fallen");
            return Localization.instance.Localize("$iov_t2_hrafn_fallen\n[<color=yellow><b>$KEY_Use</b></color>] $iov_t2_raise");
        }

        public string GetHoverName() { return Localization.instance.Localize(standing ? "$iov_t2_hrafn_name" : "$iov_t2_hrafn_fallen"); }
        public float GetHoverOffset() { return 0f; }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player player = user as Player;
            if (hold || player == null || player != Player.m_localPlayer || raising) return false;
            if (standing)
            {
                if (TextViewer.instance != null) TextViewer.instance.ShowText(TextViewer.Style.Rune, "$iov_t2_hrafn_name", "$iov_t2_hrafn_text", true);
                return true;
            }
            if (!TaleCaptains.Begun) return false;
            StartCoroutine(Raise(player));
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }

        // Up, slowly, grinding; then the dead answer.
        IEnumerator Raise(Player player)
        {
            raising = true;
            BossKit.Effect("sfx_build_hammer_stone", transform.position);
            Quaternion from = transform.localRotation, to = Quaternion.Euler(0f, yaw, 0f);
            Vector3 low = transform.localPosition, high = low - Vector3.up * lift;
            const float Takes = 2.6f;
            for (float t = 0f; t < Takes; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Takes);
                transform.localRotation = Quaternion.Slerp(from, to, k);
                transform.localPosition = Vector3.Lerp(low, high, k);
                yield return null;
            }
            transform.localRotation = to;
            transform.localPosition = high;
            BossKit.Effect("sfx_build_hammer_stone", transform.position);
            BossKit.Effect("vfx_Place_stone_wall_2x1", transform.position + Vector3.up * 0.5f);
            standing = true;
            raising = false;
            Story.Set(TaleCaptains.FoundKey(TaleCaptains.Ketill));
            if (player != null) player.Message(MessageHud.MessageType.Center, "$iov_t2_hrafn_raised", 0, null, false);
            Plugin.Debug("T2: Hrafn's stone raised");
            Vector3 camp = ambushAt != Vector3.zero && transform.parent != null ? transform.parent.TransformPoint(ambushAt) : transform.position;
            yield return Ambush.Rise(camp, transform.position, new[] { "Draugr", "Draugr", "Draugr_Ranged" }, new[] { 1, 2, 1 },
                "vfx_prespawn", "vfx_DraugrSpawn", 2.6f);
        }
    }

    // Ormr's last stone: worn and unreadable until the captains' task has begun; then read as the game's rune
    // stones are, and the first reading wakes the Seekers nesting by it.
    class OrmrsStone : MonoBehaviour, Hoverable, Interactable
    {
        public Vector3 ambushAt; // in the islet's own space
        bool woken;

        public string GetHoverText()
        {
            if (!TaleCaptains.Begun) return Localization.instance.Localize("$iov_t2_ormr_stone_worn");
            return Localization.instance.Localize("$iov_t2_ormr_stone_name\n[<color=yellow><b>$KEY_Use</b></color>] $piece_rune_read");
        }

        public string GetHoverName() { return Localization.instance.Localize(TaleCaptains.Begun ? "$iov_t2_ormr_stone_name" : "$iov_t2_ormr_stone_worn"); }
        public float GetHoverOffset() { return 0f; }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player player = user as Player;
            if (hold || player == null || player != Player.m_localPlayer || !TaleCaptains.Begun) return false;
            if (TextViewer.instance != null) TextViewer.instance.ShowText(TextViewer.Style.Rune, "$iov_t2_ormr_stone_name", "$iov_t2_ormr_stone_text", true);
            if (woken || TaleCaptains.Found(TaleCaptains.Ormr)) return true;
            woken = true;
            Story.Set(TaleCaptains.FoundKey(TaleCaptains.Ormr));
            player.Message(MessageHud.MessageType.Center, "$iov_t2_ormr_stone_read", 0, null, false);
            Plugin.Debug("T2: Ormr's last stone read");
            Vector3 around = ambushAt != Vector3.zero && transform.parent != null ? transform.parent.TransformPoint(ambushAt) : transform.position;
            StartCoroutine(Ambush.Rise(around, player.transform.position, new[] { "Seeker", "Seeker", "SeekerBrood", "SeekerBrood" }, new[] { 2, 1, 1, 1 },
                "vfx_prespawn", "vfx_spawn", 3.5f));
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }

    // Grímr's little wooden wolf: there until it's taken (the world's key, so it's gone for everyone, and back
    // if the task is reset); only taken once the captains' task has begun. Taking it rouses the north.
    class GrimrsWolf : MonoBehaviour, Hoverable, Interactable
    {
        public Vector3 ambushAt; // in the islet's own space
        bool shown = true, looked;
        float nextLook;

        void Update()
        {
            if (looked && Time.time < nextLook) return;
            nextLook = Time.time + 1f;
            bool show = !TaleCaptains.Found(TaleCaptains.Grimr);
            if (looked && show == shown) return;
            looked = true;
            Show(show);
        }

        void Show(bool show)
        {
            shown = show;
            foreach (Transform child in transform) child.gameObject.SetActive(show);
            Collider box = GetComponent<Collider>();
            if (box != null) box.enabled = show;
        }

        public string GetHoverText()
        {
            if (!shown) return "";
            if (!TaleCaptains.Begun) return Localization.instance.Localize("$iov_t2_grimr_toy");
            return Localization.instance.Localize("$iov_t2_grimr_toy\n[<color=yellow><b>$KEY_Use</b></color>] $iov_t2_take");
        }

        public string GetHoverName() { return Localization.instance.Localize("$iov_t2_grimr_toy"); }
        public float GetHoverOffset() { return 0.3f; }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player player = user as Player;
            if (hold || !shown || player == null || player != Player.m_localPlayer || !TaleCaptains.Begun) return false;
            if (TaleCaptains.Found(TaleCaptains.Grimr)) return false;
            Story.Set(TaleCaptains.FoundKey(TaleCaptains.Grimr));
            Show(false);
            BossKit.Effect("sfx_pickable_pick", transform.position);
            player.Message(MessageHud.MessageType.Center, "$iov_t2_grimr_taken", 0, null, false);
            Plugin.Debug("T2: Grímr's toy taken");
            Vector3 around = ambushAt != Vector3.zero && transform.parent != null ? transform.parent.TransformPoint(ambushAt) : transform.position;
            Plugin.Instance.StartCoroutine(Ambush.Rise(around, player.transform.position, new[] { "Ulv", "Ulv", "Fenring_Cultist", "Ulv" }, new[] { 2, 1, 1, 1 },
                "vfx_prespawn", "vfx_spawn", 4.5f));
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }

    // Sigrún's: her sister Ása's necklace, from a fish caught in her islet's waters once the captains' task has
    // begun (each fish there may have it, the third surely). The Fulings on her islet come down to the shore
    // nearest the fisher. Caught on the fisher's game; called from Story.FromFish.
    static class SigrunsNecklace
    {
        const float Chance = 0.35f;
        const int AtMost = 3;
        static int caught;

        // Whether the task has this fish (then the tale's own fragment hunt doesn't look at it).
        internal static bool Caught(Fish fish, Player player)
        {
            if (!TaleCaptains.Begun || TaleCaptains.Found(TaleCaptains.Sigrun)) return false;
            caught++;
            Plugin.Debug("T2: fish " + caught + " from Sigrún's waters");
            if (caught < AtMost && Random.value >= Chance) return true;
            caught = 0;
            Story.Set(TaleCaptains.FoundKey(TaleCaptains.Sigrun));
            player.Message(MessageHud.MessageType.Center, "$iov_t2_sigrun_caught", 0, null, false);
            Plugin.Debug("T2: Sigrún's necklace caught");
            Vector3 shore = Ashore(player.transform.position);
            Plugin.Instance.StartCoroutine(Ambush.Rise(shore, player.transform.position, new[] { "Goblin", "GoblinArcher", "Goblin", "GoblinShaman" }, new[] { 2, 1, 1, 1 },
                "vfx_prespawn", "vfx_spawn", 3.5f));
            return true;
        }

        // Dry land on her islet nearest the fisher (on the jetty or out in a boat): from the fisher towards the
        // islet's middle until the ground is above the sea, then a few metres further in.
        static Vector3 Ashore(Vector3 from)
        {
            Vector3 middle;
            if (!SigrunWatersMarker.Nearest(from, out middle) || ZoneSystem.instance == null) return from;
            float sea = ZoneSystem.instance.m_waterLevel;
            Vector3 flat = new Vector3(from.x, middle.y, from.z);
            Vector3 toward = middle - flat;
            toward.y = 0f;
            float length = toward.magnitude;
            if (length < 1f) return middle;
            toward /= length;
            for (float s = 0f; s < length; s += 1f)
            {
                Vector3 p = flat + toward * s;
                float h;
                if (ZoneSystem.instance.GetGroundHeight(p, out h) && h > sea + 0.6f)
                {
                    p += toward * Mathf.Min(5f, length - s);
                    if (ZoneSystem.instance.GetGroundHeight(p, out h)) p.y = h;
                    return p;
                }
            }
            return middle;
        }
    }

    // The ones who come for you when a captain's deed is done: a warning where each will stand, then each
    // comes up there, already roused (made on this client, owned by it, as the tale's other foes).
    static class Ambush
    {
        // around: where they come up (spread about it); face: what they turn to (where you are)
        internal static IEnumerator Rise(Vector3 around, Vector3 face, string[] creatures, int[] levels, string warn, string come, float distance)
        {
            Vector3[] spots = new Vector3[creatures.Length];
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < creatures.Length; i++)
            {
                float a = (start + i * 360f / creatures.Length) * Mathf.Deg2Rad;
                spots[i] = Ground(around + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * distance * (1f - 0.15f * i));
                if (warn.Length > 0) BossKit.Effect(warn, spots[i]);
            }
            yield return new WaitForSeconds(2.2f);
            for (int i = 0; i < creatures.Length; i++)
            {
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(creatures[i]) : null;
                if (prefab == null) continue;
                if (come.Length > 0) BossKit.Effect(come, spots[i]);
                Vector3 look = face - spots[i];
                look.y = 0f;
                GameObject go = Object.Instantiate(prefab, spots[i] + Vector3.up * 0.2f, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
                Character body = go.GetComponent<Character>();
                if (body != null && i < levels.Length) body.SetLevel(levels[i]);
                BaseAI ai = go.GetComponent<BaseAI>();
                if (ai != null) ai.Alert();
            }
            Plugin.Debug("T2: an ambush of " + creatures.Length);
        }

        // The ground (or a floor) under a point, never the water, nor the top of a stone or a crate standing
        // there (well above the ground it was asked about).
        static Vector3 Ground(Vector3 p)
        {
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out hit, 60f, LayerMask.GetMask("Default", "static_solid", "terrain", "piece"), QueryTriggerInteraction.Ignore)
                && hit.point.y < p.y + 1.2f)
                return hit.point;
            float h;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(p, out h)) return new Vector3(p.x, h, p.z);
            return p;
        }
    }
}
