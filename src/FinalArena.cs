using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // The final arena's keepers (see IsletBuilder.FinalArena): all three stand on their terraces from the
    // first time a player comes near, still and beyond harm ("asleep"), until the tale has come this far
    // (Hafdís's shore done), it's their turn (the one before fell) and a player sets foot on their terrace.
    // Then they wake: Grímr and Ormr come at once, Hafdís speaks her offer and fights when struck, as before.
    // The client that owns the marker brings them and wakes them; every client keeps the asleep ones still,
    // hides their boss bars and shows their words.
    class FinalArena : MonoBehaviour
    {
        const float Near = 70f;       // a player this close brings the keepers who aren't there
        const float Search = 90f;     // a keeper this far from the middle still counts as standing
        internal const string AsleepKey = "IoV_ArenaAsleep";

        // The keepers asleep on this client, whatever client runs them.
        internal static readonly HashSet<BaseAI> Asleep = new HashSet<BaseAI>();

        ZNetView nview;
        readonly bool[] coming = new bool[3];
        Character current;
        int spoken;
        float nextLine, nextWaiting;

        void Awake()
        {
            nview = GetComponent<ZNetView>();
            if (nview == null || nview.GetZDO() == null) return;
            InvokeRepeating("Tick", 8f, 1f); // gives the keepers already there time to load with the zone
        }

        // Where keeper i stands, in the world.
        Vector3 Spot(int i)
        {
            Vector3 local = IsletBuilder.ArenaSpots[i];
            return transform.position + transform.rotation * new Vector3(local.x, local.y + 0.3f, local.z);
        }

        bool OnTerrace(Player player, int i)
        {
            if (player == null) return false;
            Vector3 spot = Spot(i), p = player.transform.position;
            return Utils.DistanceXZ(spot, p) < IsletBuilder.ArenaRooms[i] && Mathf.Abs(p.y - spot.y) < 3f;
        }

        void Tick()
        {
            if (ZNetScene.instance == null || !nview.IsValid()) return;
            Asleep.RemoveWhere(a => a == null);
            bool near = Player.IsPlayerInRange(transform.position, Near);
            bool ready = Story.Key(Story.AshesDone);
            Story.Keeper next = Story.NextKeeper();
            Player local = Player.m_localPlayer;
            for (int i = 0; i < Story.Keepers.Length && i < IsletBuilder.ArenaSpots.Length; i++)
            {
                Story.Keeper keeper = Story.Keepers[i];
                if (Story.Key(keeper.Down) || ZNetScene.instance.GetPrefab(keeper.Prefab) == null) continue;
                Character c = Find(keeper);
                if (c == null)
                {
                    if (nview.IsOwner() && near && !coming[i]) StartCoroutine(Come(i, keeper));
                    continue;
                }
                ZNetView view = c.GetComponent<ZNetView>();
                if (view == null || !view.IsValid()) continue;
                bool asleep = view.GetZDO().GetBool(AsleepKey, false);
                BaseAI ai = c.GetBaseAI();
                if (asleep) { if (ai != null) Asleep.Add(ai); c.m_boss = false; }
                else { if (ai != null) Asleep.Remove(ai); c.m_boss = true; }
                if (!asleep)
                {
                    Speak(keeper, c);
                    if (keeper.Prefab == Story.Hafdis)
                    {
                        FinalStand.Awaken(keeper, c);
                        current = c;
                    }
                    continue;
                }
                // Not yet: the tale hasn't come this far. Said once in a while to whoever stands among them.
                if (!ready)
                {
                    if (OnTerrace(local, i) && Time.time >= nextWaiting)
                    {
                        nextWaiting = Time.time + 60f;
                        local.Message(MessageHud.MessageType.Center, "$iov_arena_waiting", 0, null, false);
                    }
                    continue;
                }
                if (keeper != next || !nview.IsOwner()) continue;
                bool someone = false;
                foreach (Player player in Player.GetAllPlayers()) if (OnTerrace(player, i)) someone = true;
                if (someone) Wake(keeper, c, view, ai);
            }
        }

        // The keeper wakes: from now on it's an ordinary fight (Grímr and Ormr turn on you at once).
        static void Wake(Story.Keeper keeper, Character c, ZNetView view, BaseAI ai)
        {
            if (!view.IsOwner()) view.ClaimOwnership();
            view.GetZDO().Set(AsleepKey, false);
            if (ai != null) Asleep.Remove(ai);
            c.m_boss = true;
            if (ai != null && keeper.Prefab != Story.Hafdis) ai.Alert();
            Effect("fx_frostcloud", c.transform.position + Vector3.up);
            Plugin.Debug("Final arena: " + keeper.Prefab + " wakes");
        }

        Character Find(Story.Keeper keeper)
        {
            foreach (Character c in Character.GetAllCharacters())
                if (c != null && !c.IsDead() && Utils.GetPrefabName(c.gameObject) == keeper.Prefab &&
                    Vector3.Distance(c.transform.position, transform.position) < Search) return c;
            return null;
        }

        // A keeper who isn't there (the first time anyone comes, or after it wandered off and was lost) steps
        // out of a gathering mist on its terrace, facing the way up from the landing, asleep.
        System.Collections.IEnumerator Come(int i, Story.Keeper keeper)
        {
            coming[i] = true;
            Vector3 at = Spot(i);
            Effect("vfx_prespawn", at);
            Effect("fx_frostcloud", at);
            yield return new WaitForSeconds(2.5f);
            coming[i] = false;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(keeper.Prefab) : null;
            if (prefab == null || !nview.IsValid() || Find(keeper) != null) yield break;
            GameObject go = Instantiate(prefab, at, transform.rotation * Quaternion.Euler(0f, 180f, 0f));
            Character c = go.GetComponent<Character>();
            if (c != null) { c.SetLevel(keeper.Level); c.m_boss = false; }
            ZNetView view = go.GetComponent<ZNetView>();
            if (view != null && view.IsValid()) view.GetZDO().Set(AsleepKey, true);
            BaseAI ai = go.GetComponent<BaseAI>();
            if (ai != null) Asleep.Add(ai);
            Effect("vfx_spawn_large", at);
            Plugin.Debug("Final arena: " + keeper.Prefab + " stands on its terrace");
        }

        static void Effect(string name, Vector3 at)
        {
            GameObject fx = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (fx != null) Instantiate(fx, at, Quaternion.identity);
        }

        // Hafdís, awake, is kept low over her hall, and looks at you while she speaks her offer (as on the old
        // hidden islet).
        void LateUpdate()
        {
            if (current == null || current.IsDead()) return;
            ZNetView view = current.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            Story.Keeper keeper = Story.KeeperOf(current);
            if (keeper == null || keeper.Prefab != Story.Hafdis) return;
            FinalStand.KeepInReach(current, Spot(2));
            Player player = Player.m_localPlayer;
            if (player == null || current.m_faction == keeper.Faction) return; // struck: she fights as she will
            Vector3 to = player.transform.position - current.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f || to.sqrMagnitude > 30f * 30f) return;
            current.transform.rotation = Quaternion.RotateTowards(current.transform.rotation, Quaternion.LookRotation(to.normalized), 240f * Time.deltaTime);
            BaseAI ai = current.GetBaseAI();
            if (ai != null) ai.LookTowards(to.normalized);
        }

        Character speaking;

        void Speak(Story.Keeper keeper, Character standing)
        {
            Player player = Player.m_localPlayer;
            if (player == null || Chat.instance == null) return;
            if (standing != speaking) { speaking = standing; spoken = 0; nextLine = 0f; }
            if (Time.time < nextLine || Vector3.Distance(player.transform.position, standing.transform.position) > 25f) return;
            string line = null;
            if (spoken == 0) line = "$" + keeper.Lines + "spawn";
            else if (keeper.Prefab == Story.Hafdis && spoken < 3 && !FinalStand.Struck(standing)) line = "$" + keeper.Lines + "offer" + (spoken + 1);
            if (line == null) return;
            Chat.instance.SetNpcText(standing.gameObject, Vector3.up * (standing.GetHeight() + 0.6f), 40f, 7f, "", line, false);
            spoken++;
            nextLine = Time.time + 8f;
        }

        // An asleep keeper does nothing (its AI doesn't run)...
        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        static class AsleepAIPatch
        {
            static bool Prefix(MonsterAI __instance, ref bool __result)
            {
                if (Asleep.Count == 0 || !Asleep.Contains(__instance)) return true;
                __result = false;
                return false;
            }
        }

        // ...and takes no harm, so it can't be fought before its time.
        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        static class AsleepHarmPatch
        {
            static bool Prefix(Character __instance)
            {
                if (Asleep.Count == 0) return true;
                BaseAI ai = __instance.GetBaseAI();
                return ai == null || !Asleep.Contains(ai);
            }
        }
    }
}
