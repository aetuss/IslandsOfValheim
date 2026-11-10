using System.Collections;
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
