using UnityEngine;

namespace IslandsOfValheim
{
    // The final arena's keepers (see IsletBuilder.FinalArena). Nobody is there until it's their turn: once
    // the tale has come this far (Hafdís's shore done), the next keeper steps out of a gathering mist at the
    // far side of their hollow when a player comes into it. Grímr and Ormr come for you at once; Hafdís speaks
    // her offer and fights when struck, as on the old hidden islet. Before the tale is there, the hollows are
    // empty, and a line says something waits. The client that owns the marker brings them; every client shows
    // their words.
    class FinalArena : MonoBehaviour
    {
        const float Search = 100f; // a keeper this far from the middle still counts as standing

        ZNetView nview;
        bool coming;
        Character hafdis, speaking;
        int spoken;
        float nextLine, nextWaiting;

        void Awake()
        {
            nview = GetComponent<ZNetView>();
            if (nview == null || nview.GetZDO() == null) return;
            InvokeRepeating("Tick", 6f, 1f); // gives a keeper already there time to load with the zone
        }

        // Islet space to the world (the marker stands at the islet's middle, at sea level, turned with it).
        Vector3 World(float x, float y, float z) { return transform.position + transform.rotation * new Vector3(x, y, z); }

        // Hollow i + 1 (0 is the cove): the keeper i's.
        bool InHollow(Player player, int i)
        {
            if (player == null) return false;
            Vector4 o = IsletBuilder.ArenaHollows[i + 1];
            Vector3 middle = World(o.x, o.w, o.y), p = player.transform.position;
            return Utils.DistanceXZ(middle, p) < o.z + 1f && Mathf.Abs(p.y - middle.y) < 4f;
        }

        Vector3 Spot(int i)
        {
            Vector2 s = IsletBuilder.ArenaSpots[i];
            return World(s.x, IsletBuilder.ArenaHollows[i + 1].w + 0.4f, s.y);
        }

        void Tick()
        {
            if (ZNetScene.instance == null || !nview.IsValid()) return;
            Story.Keeper next = Story.NextKeeper();
            Player local = Player.m_localPlayer;
            hafdis = null;
            foreach (Story.Keeper keeper in Story.Keepers)
            {
                Character c = Find(keeper);
                if (c == null) continue;
                Speak(keeper, c);
                if (keeper.Prefab == Story.Hafdis) { FinalStand.Awaken(keeper, c); hafdis = c; }
            }
            if (next == null) return;
            int i = System.Array.IndexOf(Story.Keepers, next);
            if (i < 0 || i >= IsletBuilder.ArenaSpots.Length) return;
            if (!Story.Key(Story.AshesDone))
            {
                // The tale hasn't come this far: the hollow is empty, and something waits.
                if (InHollow(local, i) && Time.time >= nextWaiting)
                {
                    nextWaiting = Time.time + 60f;
                    local.Message(MessageHud.MessageType.Center, "$iov_arena_waiting", 0, null, false);
                }
                return;
            }
            if (coming || !nview.IsOwner() || Find(next) != null) return;
            foreach (Player player in Player.GetAllPlayers())
                if (InHollow(player, i)) { StartCoroutine(Come(i, next)); return; }
        }

        Character Find(Story.Keeper keeper)
        {
            foreach (Character c in Character.GetAllCharacters())
                if (c != null && !c.IsDead() && Utils.GetPrefabName(c.gameObject) == keeper.Prefab &&
                    Vector3.Distance(c.transform.position, transform.position) < Search) return c;
            return null;
        }

        // A mist gathers at the far side of the hollow, and the keeper steps out of it, facing whoever came.
        System.Collections.IEnumerator Come(int i, Story.Keeper keeper)
        {
            coming = true;
            Vector3 at = Spot(i);
            Effect("vfx_prespawn", at);
            Effect("fx_frostcloud", at);
            yield return new WaitForSeconds(2.5f);
            coming = false;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(keeper.Prefab) : null;
            if (prefab == null || !nview.IsValid() || Find(keeper) != null) yield break;
            Player closest = Player.GetClosestPlayer(at, 60f);
            Vector3 to = closest != null ? closest.transform.position - at : -(transform.rotation * Vector3.forward);
            to.y = 0f;
            GameObject go = Instantiate(prefab, at, to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to) : transform.rotation);
            Character c = go.GetComponent<Character>();
            if (c != null) c.SetLevel(keeper.Level);
            BaseAI ai = go.GetComponent<BaseAI>();
            if (ai != null && keeper.Prefab != Story.Hafdis) ai.Alert();
            Effect("vfx_spawn_large", at);
            Plugin.Debug("Final arena: " + keeper.Prefab + " comes");
        }

        static void Effect(string name, Vector3 at)
        {
            GameObject fx = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (fx != null) Instantiate(fx, at, Quaternion.identity);
        }

        // Hafdís is kept low over her summit, and looks at you while she speaks her offer.
        void LateUpdate()
        {
            if (hafdis == null || hafdis.IsDead()) return;
            ZNetView view = hafdis.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            Story.Keeper keeper = Story.KeeperOf(hafdis);
            if (keeper == null) return;
            Vector4 top = IsletBuilder.ArenaHollows[3];
            FinalStand.KeepInReach(hafdis, World(top.x, top.w, top.y));
            Player player = Player.m_localPlayer;
            if (player == null || hafdis.m_faction == keeper.Faction) return; // struck: she fights as she will
            Vector3 to = player.transform.position - hafdis.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f || to.sqrMagnitude > 30f * 30f) return;
            hafdis.transform.rotation = Quaternion.RotateTowards(hafdis.transform.rotation, Quaternion.LookRotation(to.normalized), 240f * Time.deltaTime);
            BaseAI ai = hafdis.GetBaseAI();
            if (ai != null) ai.LookTowards(to.normalized);
        }

        void Speak(Story.Keeper keeper, Character standing)
        {
            Player player = Player.m_localPlayer;
            if (player == null || Chat.instance == null) return;
            if (standing != speaking)
            {
                if (speaking != null && !speaking.IsDead() && Vector3.Distance(player.transform.position, speaking.transform.position) < 25f) return;
                speaking = standing; spoken = 0; nextLine = 0f;
            }
            if (Time.time < nextLine || Vector3.Distance(player.transform.position, standing.transform.position) > 25f) return;
            string line = null;
            if (spoken == 0) line = "$" + keeper.Lines + "spawn";
            else if (keeper.Prefab == Story.Hafdis && spoken < 3 && !FinalStand.Struck(standing)) line = "$" + keeper.Lines + "offer" + (spoken + 1);
            if (line == null) return;
            Chat.instance.SetNpcText(standing.gameObject, Vector3.up * (standing.GetHeight() + 0.6f), 40f, 7f, "", line, false);
            spoken++;
            nextLine = Time.time + 8f;
        }
    }
}
