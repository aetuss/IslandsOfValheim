using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The way to Valhalla and back: the game's stone portal (the Ashlands' arch of standing blades) at both
    // ends, one at Sævar's harbour, one at the foot of Valhalla's way. Not the game's own pairing of portals
    // (by tags, through the world's saved objects): these are copies in their locations, and stepping through
    // one takes you to the other.
    //
    // Valhalla's is always open. The harbour's stands dark until Odin calls (the world's key OpenKey; for now
    // set by iov_gate); then it glows as a connected portal does, and lets you through.
    static partial class IsletBuilder
    {
        // A stone portal, still, its workings out and ours in. Its front (where you come out) looks along yaw.
        static GameObject Gate(Vector3 at, float yaw, bool valhallaSide)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab("portal_stone");
            if (prefab == null) { missing++; Plugin.Log.LogWarning("Gate: no portal_stone"); return null; }
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // what the portal's own script works with, read before it goes
            TeleportWorld portal = go.GetComponent<TeleportWorld>();
            TeleportWorldTrigger trigger = go.GetComponentInChildren<TeleportWorldTrigger>(true);
            GameObject glow = portal != null && portal.m_target_found != null ? portal.m_target_found.gameObject : null;
            MeshRenderer model = portal != null ? portal.m_model : null;
            Color dark = portal != null ? portal.m_colorUnconnected : Color.black, lit = portal != null ? portal.m_colorTargetfound : Color.white;
            GameObject door = trigger != null ? trigger.gameObject : null;
            Strip(go);
            ValhallaGate gate = go.AddComponent<ValhallaGate>();
            gate.valhallaSide = valhallaSide;
            gate.glow = glow;
            gate.model = model;
            gate.dark = dark;
            gate.lit = lit;
            if (door != null) door.AddComponent<ValhallaGateTrigger>().gate = gate;
            else Plugin.Log.LogWarning("Gate: the stone portal has no way through");
            built++;
            Plugin.Debug("Gate: a stone portal (" + (valhallaSide ? "Valhalla's" : "the harbour's") + ") at " + at.ToString("F1") + ", facing " + yaw.ToString("F0") +
                (glow != null ? ", its glow " + glow.name : ", no glow") + (door != null ? ", its way through " + door.name : ""));
            return go;
        }
    }

    // (Its fields are public so the location's copy of it keeps them.)
    class ValhallaGate : MonoBehaviour
    {
        internal const string OpenKey = "iovvalhallaopen";
        public bool valhallaSide;
        public GameObject glow;
        public MeshRenderer model;
        public Color dark, lit;

        // Who's on the way through, and to which end: set as they go, cleared when they're set down in front of
        // the other gate (its zone has to load first, so that gate does it).
        static int arriving;          // 0: no one; 1: to Valhalla; 2: to the harbour
        static float arrivedAt = -10f;
        const int ToValhalla = 1, ToHarbour = 2;

        int shown = -1;
        float nextLook;

        bool Open
        {
            get { return valhallaSide || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(OpenKey)); }
        }

        void Update()
        {
            if (Time.time >= nextLook)
            {
                nextLook = Time.time + 0.5f;
                int now = Open ? 1 : 0;
                if (now != shown)
                {
                    shown = now;
                    if (glow != null) glow.SetActive(now == 1);
                    if (model != null && model.material != null && model.material.HasProperty("_EmissionColor"))
                        model.material.SetColor("_EmissionColor", now == 1 ? lit : dark);
                }
            }
            // set down in front of this gate, if that's where someone was going
            Player player = Player.m_localPlayer;
            if (player == null || player.IsTeleporting()) return;
            if (arriving == (valhallaSide ? ToValhalla : ToHarbour))
            {
                arriving = 0;
                arrivedAt = Time.time;
                // Set straight down there: the game refuses a second teleport within two seconds of the last
                // (which is why the first try left everyone where the long jump put them).
                Vector3 front = transform.position + transform.forward * 3f;
                front.y = GroundY(front) + 0.1f;
                Place(player, front, Quaternion.LookRotation(transform.forward));
                Plugin.Debug("Gate: came out of " + (valhallaSide ? "Valhalla's" : "the harbour's") + " gate at " + front.ToString("F1"));
            }
        }

        // The ground (or paving) under a point: the first thing solid below from a little above it.
        static float GroundY(Vector3 p)
        {
            RaycastHit hit;
            int mask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain");
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out hit, 10f, mask, QueryTriggerInteraction.Ignore)) return hit.point.y;
            return p.y;
        }

        // A player moved a short way at once, still, and not counted as falling from wherever they were.
        static void Place(Player player, Vector3 at, Quaternion facing)
        {
            player.transform.SetPositionAndRotation(at, facing);
            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body != null) { body.position = at; body.velocity = Vector3.zero; }
            HarmonyLib.Traverse.Create(player).Field("m_maxAirAltitude").SetValue(at.y);
        }

        internal void Enter(Player player)
        {
            if (!Open || player.IsTeleporting() || Time.time - arrivedAt < 4f || arriving != 0) return;
            Quaternion facing = player.transform.rotation;
            if (valhallaSide)
            {
                // back to the harbour, over its plateau, then out in front of its gate
                ZoneSystem.LocationInstance harbour;
                if (!Find(IsletBuilder.HarbourName, out harbour)) { Plugin.Log.LogWarning("Gate: this world has no harbour to go back to"); return; }
                arriving = ToHarbour;
                player.TeleportTo(new Vector3(harbour.m_position.x, ZoneSystem.instance.m_waterLevel + IsletBuilder.SmallPlateau + 3f, harbour.m_position.z), facing, true);
            }
            else
            {
                // up to Valhalla's rock, right onto the ground in front of its gate (the rock isn't turned, and
                // its ground's height is known before it's there); placed there first, if it isn't yet
                arriving = ToValhalla;
                ZoneSystem.LocationInstance rock;
                if (Find(IsletBuilder.ValhallaBase, out rock))
                {
                    float z = IsletBuilder.ValhallaGateZ + 3f;
                    Vector3 target = new Vector3(rock.m_position.x, ZoneSystem.instance.m_waterLevel + IsletBuilder.SkyHeight + IsletBuilder.SkyGround(0f, z) + 0.3f, rock.m_position.z + z);
                    player.TeleportTo(target, Quaternion.identity, true);
                }
                else
                    TerrainIsletTest.Goto(Console.instance, IsletBuilder.ValhallaBase, IsletBuilder.SkyHeight + 1f, 150f, 900f, 0f,
                        IsletBuilder.ValhallaArrival.x, IsletBuilder.ValhallaArrival.z);
            }
            Plugin.Debug("Gate: through " + (valhallaSide ? "Valhalla's gate to the harbour" : "the harbour's gate to Valhalla"));
        }

        // iov_gate here (development): the harbour's gate moved, now, to where you stand, its front the way you
        // look; and where that is in the harbour's own frame, to build it there for good.
        internal static void MoveHere(Terminal context)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            ValhallaGate near = null;
            foreach (ValhallaGate g in Object.FindObjectsOfType<ValhallaGate>())
                if (!g.valhallaSide && Vector3.Distance(g.transform.position, player.transform.position) < 60f) near = g;
            if (near == null || near.transform.parent == null) { context.AddString("No harbour gate near you."); return; }
            Vector3 look = player.transform.forward;
            look.y = 0f;
            Vector3 at = player.transform.position;
            at.y = GroundY(at);
            near.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look.normalized));
            Transform frame = near.transform.parent;
            Vector3 local = frame.InverseTransformPoint(at), localLook = frame.InverseTransformDirection(look);
            Vector2 h, hLook;
            IsletBuilder.HarbourFrame(new Vector2(local.x, local.z), new Vector2(localLook.x, localLook.z), out h, out hLook);
            string line = "harbour gate at H(" + h.x.ToString("F1") + ", " + h.y.ToString("F1") + "), looking H(" + hLook.x.ToString("F2") + ", " + hLook.y.ToString("F2") + ")";
            context.AddString("Moved: " + line + ". (It goes back when the harbour loads again; tell me these numbers to keep it there.)");
            Plugin.Log.LogInfo("iov_gate here: " + line);
        }

        static bool Find(string name, out ZoneSystem.LocationInstance found)
        {
            found = default(ZoneSystem.LocationInstance);
            if (ZoneSystem.instance == null) return false;
            foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.GetLocationList())
                if (instance.m_location != null && instance.m_location.m_prefabName == name) { found = instance; return true; }
            return false;
        }
    }

    // On the portal's own way-through (the trigger inside its arch): the local player stepping in goes.
    class ValhallaGateTrigger : MonoBehaviour
    {
        public ValhallaGate gate;

        void OnTriggerEnter(Collider other)
        {
            Player player = other != null ? other.GetComponentInParent<Player>() : null;
            if (player != null && player == Player.m_localPlayer && gate != null) gate.Enter(player);
        }
    }
}
