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
                Vector3 front = transform.position + transform.forward * 3f + Vector3.up * 0.5f;
                player.TeleportTo(front, Quaternion.LookRotation(transform.forward), false);
                Plugin.Debug("Gate: came out of " + (valhallaSide ? "Valhalla's" : "the harbour's") + " gate");
            }
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
                // up to Valhalla's rock (placed there first, if it isn't yet), then out in front of its gate
                arriving = ToValhalla;
                TerrainIsletTest.Goto(Console.instance, IsletBuilder.ValhallaBase, IsletBuilder.SkyHeight + 1f, 150f, 900f, 0f,
                    IsletBuilder.ValhallaArrival.x, IsletBuilder.ValhallaArrival.z);
            }
            Plugin.Debug("Gate: through " + (valhallaSide ? "Valhalla's gate to the harbour" : "the harbour's gate to Valhalla"));
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
