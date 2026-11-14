using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // Where the two ends of the way to Valhalla are: Sævar's harbour and Valhalla's rock. The world's list of
    // places is only on the server (a client's is empty), so a client asks the server, the way the tale's maps
    // and the sea charts do, and keeps the answer; on the server (or the host, or single player) it's read
    // straight from the list. The server also places Valhalla if it isn't yet, when someone asks to go there.
    // (Up to 2.1.0 the gates read the list themselves: a player on someone else's world or on a dedicated
    // server could neither go through to Valhalla nor come back, and Odin's light stood at the world's middle.)
    static class ValhallaWhere
    {
        const string RequestRpc = "IoV_WhereRequest", AnswerRpc = "IoV_WhereAnswer";

        static bool harbourKnown, harbourPlaced, valhallaKnown;
        static Vector3 harbour, valhalla;
        static float askedAt = -100f;

        // a gate stepped into before its far end was known: gone through once the answer comes (if still there)
        static ValhallaGate waiting;
        static float waitingSince;

        static bool Server { get { return ZNet.instance != null && ZNet.instance.IsServer(); } }

        // The harbour (the one that's been built, if there are still several spots for it).
        internal static bool Harbour(out Vector3 at)
        {
            if (Server) return OnList(out at, out harbourPlaced);
            if (!harbourKnown || !harbourPlaced) Ask(false, harbourKnown ? 30f : 5f);
            at = harbour;
            return harbourKnown;
        }

        // Valhalla's rock; place: placed now if it isn't yet (going through the harbour's gate).
        internal static bool Valhalla(out Vector3 at, bool place)
        {
            if (Server)
            {
                if (IsletBuilder.FindValhalla(out at)) return true;
                return place && IsletBuilder.PlaceValhalla() && IsletBuilder.FindValhalla(out at);
            }
            if (!valhallaKnown) Ask(place, place ? 1f : 5f);
            at = valhalla;
            return valhallaKnown;
        }

        // Asked for before it's needed (near an open gate), so stepping in goes at once.
        internal static void Prefetch(bool valhallaSide)
        {
            if (Server) return;
            if (valhallaSide ? !harbourKnown : !valhallaKnown) Ask(false, 5f);
        }

        internal static void WaitFor(ValhallaGate gate)
        {
            waiting = gate;
            waitingSince = Time.time;
            Ask(!gate.valhallaSide, 0f); // (asked now, whenever it was last asked: the answer takes them through)
        }

        static void Ask(bool place, float every)
        {
            if (ZRoutedRpc.instance == null || Time.time - askedAt < every) return;
            askedAt = Time.time;
            ZPackage request = new ZPackage();
            request.Write(place);
            ZRoutedRpc.instance.InvokeRoutedRPC(SeaChart.ServerPeer(), RequestRpc, request);
        }

        static bool OnList(out Vector3 at, out bool placed)
        {
            at = Vector3.zero;
            placed = false;
            bool found = false;
            if (ZoneSystem.instance == null) return false;
            foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.GetLocationList())
            {
                if (instance.m_location == null || instance.m_location.m_prefabName != IsletBuilder.HarbourName) continue;
                if (found && (placed || !instance.m_placed)) continue;
                at = instance.m_position;
                placed = instance.m_placed;
                found = true;
            }
            return found;
        }

        // Server: both ends, as far as they're known (Valhalla placed first if asked, once the way is open).
        static void OnRequest(long sender, ZPackage request)
        {
            if (!Server || ZoneSystem.instance == null) return;
            bool place = request.ReadBool();
            Vector3 h, v;
            bool placed;
            bool hk = OnList(out h, out placed);
            bool vk = IsletBuilder.FindValhalla(out v);
            if (!vk && place && ZoneSystem.instance.GetGlobalKey(ValhallaGate.OpenKey) && IsletBuilder.PlaceValhalla()) vk = IsletBuilder.FindValhalla(out v);
            ZPackage answer = new ZPackage();
            answer.Write(hk);
            answer.Write(placed);
            answer.Write(h);
            answer.Write(vk);
            answer.Write(v);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, answer);
            Plugin.Debug("Gate: told " + sender + " where the harbour" + (hk ? " (" + h.ToString("F0") + ")" : " (none)") + " and Valhalla" + (vk ? " (" + v.ToString("F0") + ")" : " (not yet)") + " are");
        }

        static void OnAnswer(long sender, ZPackage answer)
        {
            harbourKnown = answer.ReadBool();
            harbourPlaced = answer.ReadBool();
            harbour = answer.ReadVector3();
            valhallaKnown = answer.ReadBool();
            valhalla = answer.ReadVector3();
            ValhallaGate gate = waiting;
            waiting = null;
            Player player = Player.m_localPlayer;
            if (gate == null || player == null || Time.time - waitingSince > 10f) return;
            if (Vector3.Distance(player.transform.position, gate.transform.position) > 6f) return;
            if (gate.valhallaSide ? harbourKnown : valhallaKnown) gate.Enter(player);
            else player.Message(MessageHud.MessageType.Center, "$iov_gate_notyet");
        }

        [HarmonyPatch(typeof(Game), "Start")]
        static class RpcPatch
        {
            static void Postfix()
            {
                // a new world (or the same one again): nothing known yet
                harbourKnown = harbourPlaced = valhallaKnown = false;
                askedAt = -100f;
                waiting = null;
                ValhallaGate.Reset();
                IsletPhysics.Reset();
                ZRoutedRpc.instance.Register<ZPackage>(RequestRpc, OnRequest);
                ZRoutedRpc.instance.Register<ZPackage>(AnswerRpc, OnAnswer);
            }
        }
    }
}
