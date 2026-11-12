using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // T4 of the Valhalla arc: the Brim village (its layout: SpiritsVillage). Once Rán's net is torn, Sævar asks
    // the player to make the people a home again: the ruined village in sight of his harbour. Its altar takes
    // two rounds of donations, and the village stands again in stages; the second gives the player a house
    // of their own beside the hall (its bed, chests and fires real and working, but never damaged or taken
    // apart), and a yard where they may build (the rest of the village is the people's).
    // The islet is placed by the server near the harbour once the harbour itself is placed (a world picks
    // one of the harbour's candidate spots only when someone first comes near it), in a new world or one that
    // already has its harbour.
    static class SpiritsIslet
    {
        internal const string AltarName = "IoV_SpiritAltar";
        // The houses' doors are the game's own (wood_door, a quarter taller; the hall's darkwood_gate): marked as
        // the village's, they're kept like the house's pieces (HomeKey), hidden while the village lies in ruins
        // and made as tall again when they come back (1.10.91-93 used copies, and the gate's copy never made it
        // into the game: a wooden door stood in for it).
        const string DoorKey = "IoV_T4Door", DoorScaleKey = "IoV_T4DoorScale";

        // The game's dark wooden gate as the game places it (a networked piece with a door on it): found among
        // the game's own networked prefabs, not by a guessed name (1.10.94 took "darkwood_gate", a model of that
        // name the game doesn't place, and a wooden door stood in for it again). Every door the game has is
        // logged once, to know them by.
        internal static GameObject DarkGate()
        {
            if (darkGate != null || ZNetScene.instance == null) return darkGate;
            List<string> doors = new List<string>();
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null || prefab.GetComponent<Door>() == null) continue;
                Piece piece = prefab.GetComponent<Piece>();
                string token = piece != null ? piece.m_name : "";
                doors.Add(prefab.name + " (" + token + ")");
                string n = prefab.name.ToLowerInvariant(), t = token.ToLowerInvariant();
                bool dark = n.Contains("dark") || t.Contains("dark"), gate = n.Contains("gate") || t.Contains("gate");
                if (dark && gate && darkGate == null) darkGate = prefab;
            }
            Plugin.Debug("Brim village: the game's doors: " + string.Join(", ", doors.ToArray()) + "; the hall's gate: " + (darkGate != null ? darkGate.name : "none found"));
            return darkGate;
        }

        static GameObject darkGate;

        [HarmonyPatch(typeof(Door), "Awake")]
        static class VillageDoorPatch
        {
            static void Postfix(Door __instance)
            {
                ZNetView view = __instance.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.GetZDO().GetBool(DoorKey)) return;
                float scale = view.GetZDO().GetFloat(DoorScaleKey, 1f);
                if (scale > 0.1f) __instance.transform.localScale = Vector3.one * scale;
                if (__instance.GetComponent<SpiritDoor>() == null) __instance.gameObject.AddComponent<SpiritDoor>();
            }
        }
        internal const string DoorsKey = "IoV_T4Doors";    // on the altar: the houses' doors are in
        internal const string Map = "IoV_StoryMapBrim";
        internal const string Stage1Key = "iovt4stage1", Stage2Key = "iovt4stage2";
        const string PlacedKey = "iovt4placed";
        const string ToldKey = "iovt4told";              // (the player has heard Sævar ask: per player)
        internal const string HomeKey = "IoV_T4Home";     // on the house's real pieces
        const string HomeBuiltKey = "IoV_T4HomeBuilt";    // on the altar: the house's pieces are in

        // The village as the world has it: 0 ruined, 1 cleared, 2 rebuilt.
        internal static int Stage { get { return Story.Key(Stage2Key) ? 2 : Story.Key(Stage1Key) ? 1 : 0; } }

        // What each round asks (the first that clears it: plentiful things; the second that builds it again:
        // what a seasoned yolgezer brings back from the Mistlands and the swamps).
        static readonly string[][] Clearing = { new[] { "Wood", "50" }, new[] { "Stone", "50" }, new[] { "Resin", "20" } };
        static readonly string[][] Rebuilding = { new[] { "YggdrasilWood", "30" }, new[] { "BlackMarble", "30" }, new[] { "IronNails", "50" } };

        // ---- the altar ----

        internal static void Register()
        {
            string source = null;
            foreach (string name in new[] { "piece_offerbowl", "fire_pit_iron", "fire_pit" })
                if (PrefabManager.Instance.GetPrefab(name) != null) { source = name; break; }
            if (source == null) { Plugin.Log.LogWarning("Brim village: no altar to make; its donations are left out."); return; }
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(AltarName, source);
            Fireplace fire = go.GetComponent<Fireplace>();
            if (fire != null)
            {
                foreach (GameObject state in new[] { fire.m_enabledObject, fire.m_enabledObjectLow, fire.m_enabledObjectHigh, fire.m_halfObject })
                    if (state != null && state != fire.m_emptyObject) state.SetActive(false);
                if (fire.m_playerBaseObject != null) Object.DestroyImmediate(fire.m_playerBaseObject);
            }
            for (int pass = 0; pass < 4; pass++)
                foreach (MonoBehaviour script in go.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(script is ZNetView)) Object.DestroyImmediate(script);
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            go.GetComponent<ZNetView>().m_persistent = true;
            // a cold, pale light over it, the souls' own colour
            GameObject glow = new GameObject("IoV_AltarGlow");
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = RanNet.SoulColour;
            light.range = 6f;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
            go.AddComponent<SpiritAltar>();
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
            Plugin.Debug("Brim village: the altar made from " + source);
        }

        internal static string[][] Cost(int stage) { return stage == 0 ? Clearing : Rebuilding; }

        internal static string ItemName(string prefab)
        {
            GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            return item != null ? item.GetComponent<ItemDrop>().m_itemData.m_shared.m_name : null;
        }

        // The real pieces of the player's house, where the village's plan puts them (the altar knows the islet's
        // middle from where it stands itself), marked as the house's.
        internal static void BuildHome(Transform altar)
        {
            int made = Spawn(altar, IsletBuilder.HomePieces, HomeKey);
            Plugin.Debug("Brim village: the player's house furnished, " + made + " pieces");
        }

        // The doors of the village's houses (real, opening and shutting; never broken or taken apart: the
        // door prefab has nothing to break), put in by the altar once; hidden while the village lies in ruins.
        internal static void BuildDoors(Transform altar)
        {
            int made = Spawn(altar, IsletBuilder.DoorPieces, HomeKey, true);
            Plugin.Debug("Brim village: " + made + " doors put in");
        }

        // The meadow's pickables (the game's own, picked and growing again), put in by the altar once.
        internal const string MeadowKey = "IoV_T4Meadow"; // on the altar: the meadow's pickables are in

        internal static void BuildMeadow(Transform altar)
        {
            int made = Spawn(altar, IsletBuilder.MeadowPickables, null);
            Plugin.Debug("Brim village: " + made + " things to pick put in the meadow");
        }

        static int Spawn(Transform altar, List<IsletBuilder.HomePiece> pieces, string mark, bool doors = false)
        {
            Quaternion turn = altar.rotation;
            Vector3 middle = altar.position - turn * IsletBuilder.SpiritAltarAt;
            int made = 0;
            foreach (IsletBuilder.HomePiece h in pieces)
            {
                GameObject prefab = null;
                foreach (string name in h.Prefab)
                    if ((prefab = name == "@darkgate" ? DarkGate() : ZNetScene.instance.GetPrefab(name)) != null) break;
                if (prefab != null && h.Prefab.Length > 1 && prefab.name != h.Prefab[0]) Plugin.Log.LogWarning("Brim village: " + h.Prefab[0] + " not found, " + prefab.name + " in its place");
                if (prefab == null) { Plugin.Log.LogWarning("Brim village: none of " + string.Join(", ", h.Prefab) + " for the player's house"); continue; }
                Vector3 at = middle + turn * h.At;
                Quaternion rotation = turn * Quaternion.Euler(0f, h.Yaw, 0f);
                // a bed lies along the house (its long side front to back)
                if (h.AlongZ)
                {
                    Vector3 size = OwnSize(prefab);
                    if (size.x > size.z) rotation *= Quaternion.Euler(0f, 90f, 0f);
                }
                GameObject go = Object.Instantiate(prefab, at, rotation);
                // a door taller than the game's (the user found them low to go through)
                if (h.Scale > 0f && Mathf.Abs(h.Scale - 1f) > 0.01f) go.transform.localScale = Vector3.one * h.Scale;
                // its foot on the floor, wherever its own middle is (1.10.88's braziers sank into it)
                Bounds drawn = new Bounds();
                bool any = false;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer || !r.enabled) continue;
                    if (!any) { drawn = r.bounds; any = true; } else drawn.Encapsulate(r.bounds);
                }
                if (any)
                {
                    Vector3 shift = new Vector3(at.x - drawn.center.x, at.y - drawn.min.y, at.z - drawn.center.z);
                    go.transform.position += shift;
                }
                ZNetView view = go.GetComponent<ZNetView>();
                if (view != null && view.IsValid())
                {
                    // where it ends up is where it comes back (a piece has nothing that keeps its place in step)
                    view.GetZDO().SetPosition(go.transform.position);
                    view.GetZDO().SetRotation(go.transform.rotation);
                    if (mark != null) view.GetZDO().Set(mark, true);
                    if (go.GetComponent<Bed>() != null) view.GetZDO().Set(BedTurnedKey, true); // (laid the right way)
                    if (doors)
                    {
                        view.GetZDO().Set(DoorKey, true);
                        view.GetZDO().Set(DoorScaleKey, go.transform.localScale.x);
                    }
                }
                if (doors && go.GetComponent<SpiritDoor>() == null) go.AddComponent<SpiritDoor>();
                if (mark != null) Keep(go);
                if (mark != null && !doors) homePieces.Add(go.transform); // (the house's, as it's furnished)
                made++;
            }
            return made;
        }

        // The door: the game's wooden door with nothing on it that can be broken or taken apart.
        static void RegisterDoor(string name, string source)
        {
            if (PrefabManager.Instance.GetPrefab(source) == null) { Plugin.Log.LogWarning("Brim village: no " + source + "; " + name + " left out."); return; }
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(name, source);
            foreach (WearNTear wear in go.GetComponentsInChildren<WearNTear>(true)) Object.DestroyImmediate(wear);
            foreach (Piece piece in go.GetComponentsInChildren<Piece>(true)) Object.DestroyImmediate(piece);
            go.GetComponent<ZNetView>().m_persistent = true;
            go.GetComponent<ZNetView>().m_syncInitialScale = true; // (made taller where it's put in: kept so)
            go.AddComponent<SpiritDoor>();
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // A prefab's size in its own space, from its meshes.
        internal static Vector3 OwnSize(GameObject prefab)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (MeshFilter f in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                Bounds mb = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = prefab.transform.InverseTransformPoint(f.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; } else result.Encapsulate(p);
                }
            }
            return result.size;
        }

        // A piece of the player's house: never worn by weather or want of support, never taken apart, its fire
        // never out.
        static void Keep(GameObject go)
        {
            WearNTear wear = go.GetComponent<WearNTear>();
            if (wear != null) { wear.m_noRoofWear = false; wear.m_noSupportWear = false; }
            Piece piece = go.GetComponent<Piece>();
            if (piece != null) piece.m_canBeRemoved = false;
            Fireplace fire = go.GetComponent<Fireplace>();
            if (fire != null) fire.m_infiniteFuel = true;
        }

        static bool Home(Component c)
        {
            ZNetView view = c.GetComponent<ZNetView>();
            return view != null && view.IsValid() && view.GetZDO().GetBool(HomeKey);
        }

        internal static void HomeBuilt(ZNetView altar, bool built) { if (altar != null && altar.IsValid()) altar.GetZDO().Set(HomeBuiltKey, built); }
        internal static bool IsHomeBuilt(ZNetView altar) { return altar != null && altar.IsValid() && altar.GetZDO().GetBool(HomeBuiltKey); }

        [HarmonyPatch(typeof(WearNTear), "Awake")]
        static class HomeWearPatch
        {
            static void Postfix(WearNTear __instance)
            {
                if (!Home(__instance)) return;
                Keep(__instance.gameObject);
                homePieces.Add(__instance.transform);
                TurnBed(__instance);
            }
        }

        // The house's beds, put in by 1.10.89-124 the wrong way round (their heads to the room, the user saw):
        // turned about where they lie, once, by their owner (the ones put in since lie right: BedTurnedKey).
        const string BedTurnedKey = "IoV_T4BedTurned";

        // (and from the altar, now and then, for any its owner didn't own as it loaded)
        internal static void TurnBeds()
        {
            homePieces.RemoveAll(t => t == null);
            foreach (Transform t in homePieces)
            {
                WearNTear piece = t.GetComponent<WearNTear>();
                ZNetView view = t.GetComponent<ZNetView>();
                if (piece == null || view == null || !view.IsValid() || t.GetComponent<Bed>() == null || view.GetZDO().GetBool(BedTurnedKey)) continue;
                if (!view.IsOwner()) view.ClaimOwnership();
                TurnBed(piece);
            }
        }

        static void TurnBed(WearNTear piece)
        {
            ZNetView view = piece.GetComponent<ZNetView>();
            if (piece.GetComponent<Bed>() == null || view == null || !view.IsValid() || !view.IsOwner() || view.GetZDO().GetBool(BedTurnedKey)) return;
            Bounds drawn = new Bounds();
            bool any = false;
            foreach (Renderer r in piece.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || !r.enabled) continue;
                if (!any) { drawn = r.bounds; any = true; } else drawn.Encapsulate(r.bounds);
            }
            if (!any) return;
            piece.transform.RotateAround(drawn.center, Vector3.up, 180f);
            view.GetZDO().SetPosition(piece.transform.position);
            view.GetZDO().SetRotation(piece.transform.rotation);
            view.GetZDO().Set(BedTurnedKey, true);
            Plugin.Debug("Brim village: a bed of the player's house turned the right way round");
        }

        // The pieces of the player's house in the Brim village loaded here (its beds, chests, fires): whether a
        // place is in it (the Brim hearth: OdinBlessing).
        static readonly List<Transform> homePieces = new List<Transform>();

        internal static bool InBrimHome(Vector3 p)
        {
            homePieces.RemoveAll(t => t == null);
            foreach (Transform t in homePieces)
                if (Vector3.Distance(t.position, p) < 6f) return true;
            return false;
        }

        [HarmonyPatch(typeof(Fireplace), "Awake")]
        static class HomeFirePatch { static void Postfix(Fireplace __instance) { if (Home(__instance)) Keep(__instance.gameObject); } }

        [HarmonyPatch(typeof(WearNTear), "ApplyDamage")]
        static class HomeDamagePatch { static void Prefix(WearNTear __instance, ref float damage) { if (damage > 0f && Home(__instance)) damage = 0f; } }

        [HarmonyPatch(typeof(Piece), "CanBeRemoved")]
        static class HomeRemovePatch { static void Postfix(Piece __instance, ref bool __result) { if (__result && Home(__instance)) __result = false; } }

        // The village is the people's (no building), but for the player's yard.
        [HarmonyPatch(typeof(Location), "IsInsideNoBuildLocation")]
        static class YardPatch
        {
            static void Postfix(Vector3 point, ref bool __result)
            {
                if (__result && SpiritYard.Covers(point)) __result = false;
            }
        }

        // ---- the islet near the harbour (on the server) ----

        static float wait = 10f;

        internal static void Tick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
            wait -= Time.deltaTime;
            if (wait > 0f) return;
            wait = 5f;
            if (ZoneSystem.instance.GetGlobalKey(PlacedKey)) return;
            Place(null);
        }

        static readonly System.Reflection.MethodInfo registerLocation = AccessTools.Method(typeof(ZoneSystem), "RegisterLocation");
        static readonly System.Reflection.MethodInfo spawnLocation = AccessTools.Method(typeof(ZoneSystem), "SpawnLocation");
        static readonly System.Reflection.FieldInfo generatedZones = AccessTools.Field(typeof(ZoneSystem), "m_generatedZones");

        // Within sight of the placed harbour, in open sea round all its shores; built at once where the sea has
        // been sailed, else when someone comes near. False while there's no harbour yet.
        internal static bool Place(Terminal context)
        {
            ZoneSystem zones = ZoneSystem.instance;
            ZoneSystem.ZoneLocation location = null;
            foreach (ZoneSystem.ZoneLocation l in zones.m_locations)
                if (l.m_prefabName == IsletBuilder.SpiritsName) { location = l; break; }
            if (location == null) { if (context != null) context.AddString("The Brim village isn't registered (no sea trader islets?)."); return false; }
            Vector3 harbour = Vector3.zero;
            bool found = false;
            List<ZoneSystem.LocationInstance> others = new List<ZoneSystem.LocationInstance>();
            foreach (ZoneSystem.LocationInstance i in zones.GetLocationList())
            {
                if (i.m_location == null) continue;
                if (i.m_location.m_prefabName == IsletBuilder.SpiritsName)
                {
                    zones.SetGlobalKey(PlacedKey);
                    if (context != null) context.AddString("The Brim village is already at " + i.m_position.ToString("F0"));
                    return true;
                }
                if (i.m_location.m_prefabName == IsletBuilder.HarbourName && i.m_placed) { harbour = i.m_position; found = true; }
                others.Add(i);
            }
            if (!found) { if (context != null) context.AddString("Sævar's harbour isn't placed yet (sail near it first)."); return false; }

            float turn0 = (WorldGenerator.instance.GetSeed() & 0xff) * 1.4f;
            foreach (float distance in new[] { 340f, 300f, 380f, 420f, 460f, 520f })
                for (int k = 0; k < 24; k++)
                {
                    float a = (turn0 + k * 15f) * Mathf.Deg2Rad;
                    Vector3 p = harbour + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * distance;
                    if (!Suits(p, others)) continue;
                    p.y = zones.m_waterLevel;
                    Vector2s zone = ZoneSystem.GetZone(p);
                    HashSet<Vector2s> generated = generatedZones != null ? generatedZones.GetValue(zones) as HashSet<Vector2s> : null;
                    bool now = generated != null && generated.Contains(zone);
                    registerLocation.Invoke(zones, new object[] { location, p, now });
                    if (now)
                    {
                        // its back to the harbour, so its square and houses face it
                        Vector3 away = p - harbour;
                        Quaternion rotation = Quaternion.Euler(0f, Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg, 0f);
                        spawnLocation.Invoke(zones, new object[] { location, WorldGenerator.instance.GetSeed() ^ 0x5b1, p, rotation, ZoneSystem.SpawnMode.Full, new List<GameObject>(), false });
                    }
                    zones.SetGlobalKey(PlacedKey);
                    string said = "Brim village placed " + distance.ToString("F0") + " m from Sævar's harbour, at " + p.ToString("F0") + (now ? " (built now: that sea had been sailed)" : "");
                    Plugin.Log.LogInfo(said);
                    if (context != null) context.AddString(said);
                    return true;
                }
            Plugin.Log.LogWarning("Brim village: no open sea round Sævar's harbour within 520 m; tried again later.");
            if (context != null) context.AddString("No open sea round the harbour within 520 m.");
            wait = 120f;
            return false;
        }

        // Deep open sea, sea all round as far as the islet reaches, and clear of every other place.
        static bool Suits(Vector3 p, List<ZoneSystem.LocationInstance> others)
        {
            WorldGenerator world = WorldGenerator.instance;
            float water = ZoneSystem.instance.m_waterLevel;
            if (world.GetBiome(p) != Heightmap.Biome.Ocean || world.GetHeight(p.x, p.z) > water - 6f) return false;
            foreach (float ring in new[] { 45f, 95f, 112f })
                for (int i = 0; i < 12; i++)
                {
                    float rad = i * Mathf.PI / 6f;
                    Vector3 q = p + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * ring;
                    if (world.GetBiome(q) != Heightmap.Biome.Ocean || world.GetHeight(q.x, q.z) > water - 2f) return false;
                }
            foreach (ZoneSystem.LocationInstance o in others)
            {
                float keep = Mathf.Max(o.m_location.m_exteriorRadius, o.m_location.m_interiorRadius) + 100f;
                if (o.m_location.m_prefabName.StartsWith(IsletBuilder.LargePrefix)) keep = Mathf.Max(keep, 250f);
                if (new Vector2(o.m_position.x - p.x, o.m_position.z - p.z).sqrMagnitude < keep * keep) return false;
            }
            return true;
        }

        // ---- Sævar ----

        // Once the net is torn: the village (a page and the chart) the first time; then a reminder (the chart
        // again if it's been lost); once it stands again, his thanks.
        static int doneTurn;

        internal static string SaevarLine(Player player)
        {
            // once it stands again: to Hafdís at the rock's edge (T5); once she has yielded, not yet the end
            // (and the tale done, by turns: his people going home, and where else you'll find him now, his spirit
            // at home in three places: here, by his harbour's old fire, on his village's pier)
            if (Stage >= 2) return Story.Key(ValhallaPeople.DoneKey) ? (doneTurn++ % 2 == 0 ? "$iov_valsaevar_done" : "$iov_valsaevar_done2") : Story.Key(ValhallaDuel.HafdisWonKey) ? "$iov_valsaevar_t5won" : "$iov_valsaevar_t5";
            if (!player.HaveSeenTutorial(ToldKey))
            {
                player.SetSeenTutorial(ToldKey);
                Story.Tell(player, "$iov_t4_page", "$iov_t4_page_text");
                Story.Give(player, Map);
                return "$iov_valsaevar_t4";
            }
            player.AddKnownText("$iov_t4_page", "$iov_t4_page_text");
            if (!Story.Has(player, Map)) Story.Give(player, Map);
            return "$iov_valsaevar_t4remind";
        }

        // ---- development ----

        internal static void Dev(string what, string arg, Terminal context)
        {
            Player player = Player.m_localPlayer;
            ZoneSystem zones = ZoneSystem.instance;
            if (zones == null) return;
            switch (what)
            {
                case "stage":
                    int stage;
                    if (!int.TryParse(arg, out stage) || stage < 0 || stage > 2) { context.AddString("iov_t4 stage 0|1|2"); return; }
                    if (stage >= 1) Story.Set(Stage1Key); else zones.RemoveGlobalKey(Stage1Key);
                    if (stage >= 2) Story.Set(Stage2Key); else zones.RemoveGlobalKey(Stage2Key);
                    break;
                case "reset":
                    zones.RemoveGlobalKey(Stage1Key);
                    zones.RemoveGlobalKey(Stage2Key);
                    if (player != null) RemoveSeen(player, ToldKey);
                    // the house's pieces and the altar's memory of them, where they're loaded
                    foreach (ZNetView view in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
                    {
                        if (view == null || !view.IsValid()) continue;
                        if (view.GetZDO().GetBool(HomeKey)) { view.ClaimOwnership(); view.Destroy(); }
                        else if (view.GetComponent<SpiritDoor>() != null) { view.ClaimOwnership(); view.Destroy(); }
                        else if (view.GetComponent<SpiritAltar>() != null) { view.ClaimOwnership(); HomeBuilt(view, false); view.GetZDO().Set(DoorsKey, false); }
                    }
                    break;
                case "place":
                    Place(context);
                    return;
                case "map":
                    if (player != null) Story.Give(player, Map);
                    break;
                default:
                    context.AddString("iov_t4 stage 0|1|2 | reset | place | map");
                    return;
            }
            context.AddString("T4 " + what + ": the village is " + (Stage == 2 ? "rebuilt" : Stage == 1 ? "cleared" : "ruined") + (RanNet.Done ? "" : " (the net isn't torn yet: its altar is silent)"));
        }

        static void RemoveSeen(Player player, string key)
        {
            System.Reflection.FieldInfo field = AccessTools.Field(typeof(Player), "m_shownTutorials");
            HashSet<string> seen = field != null ? field.GetValue(player) as HashSet<string> : null;
            if (seen != null) seen.Remove(key);
        }
    }

    // The Brim village's altar: what the village needs next, taken from whoever brings it.
    class SpiritAltar : MonoBehaviour, Hoverable, Interactable
    {
        ZNetView view;
        bool shown = true;
        internal static readonly List<SpiritAltar> All = new List<SpiritAltar>();

        void Awake()
        {
            view = GetComponent<ZNetView>();
            InvokeRepeating("Furnish", 3f, 3f);
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        // The house's real pieces, once the village is rebuilt (its owner puts them in, once).
        void Furnish()
        {
            // once the village stands again the altar is the square's great fire (the village's, round it):
            // its own stone and light hidden, still there to look at
            bool show = SpiritsIslet.Stage < 2;
            if (show != shown)
            {
                shown = show;
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = show;
                foreach (Light l in GetComponentsInChildren<Light>(true)) l.enabled = show;
            }
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            SpiritsIslet.TurnBeds();
            if (!view.GetZDO().GetBool(SpiritsIslet.DoorsKey))
            {
                view.GetZDO().Set(SpiritsIslet.DoorsKey, true);
                SpiritsIslet.BuildDoors(transform);
            }
            if (!view.GetZDO().GetBool(SpiritsIslet.MeadowKey) && IsletBuilder.MeadowPickables.Count > 0)
            {
                view.GetZDO().Set(SpiritsIslet.MeadowKey, true);
                SpiritsIslet.BuildMeadow(transform);
            }
            if (SpiritsIslet.Stage < 2 || SpiritsIslet.IsHomeBuilt(view)) return;
            SpiritsIslet.HomeBuilt(view, true);
            SpiritsIslet.BuildHome(transform);
        }

        public string GetHoverName() { return Localization.instance.Localize("$iov_t4_altar"); }

        public float GetHoverOffset() { return 0f; }

        public string GetHoverText()
        {
            string text = "$iov_t4_altar";
            int stage = SpiritsIslet.Stage;
            if (!RanNet.Done) text += "\n$iov_t4_altar_silent";
            else if (stage >= 2) text += "\n$iov_t4_altar_done";
            else
            {
                text += "\n[<color=yellow><b>$KEY_Use</b></color>] $iov_t4_altar_give\n" + (stage == 0 ? "$iov_t4_altar_clear" : "$iov_t4_altar_build");
                Inventory inventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
                foreach (string[] need in SpiritsIslet.Cost(stage))
                {
                    string name = SpiritsIslet.ItemName(need[0]);
                    if (name == null) continue;
                    int want = int.Parse(need[1]);
                    int have = inventory != null ? inventory.CountItems(name, -1, true) : 0;
                    text += "\n" + (have >= want ? "" : "<color=#ff9a8a>") + Mathf.Min(have, want) + "/" + want + " " + name + (have >= want ? "" : "</color>");
                }
            }
            return Localization.instance.Localize(text);
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !RanNet.Done) return false;
            int stage = SpiritsIslet.Stage;
            if (stage >= 2) return false;
            Inventory inventory = user.GetInventory();
            foreach (string[] need in SpiritsIslet.Cost(stage))
            {
                string name = SpiritsIslet.ItemName(need[0]);
                if (name != null && inventory.CountItems(name, -1, true) < int.Parse(need[1]))
                {
                    user.Message(MessageHud.MessageType.Center, "$iov_t4_short");
                    return true;
                }
            }
            foreach (string[] need in SpiritsIslet.Cost(stage))
            {
                string name = SpiritsIslet.ItemName(need[0]);
                if (name != null) inventory.RemoveItem(name, int.Parse(need[1]), -1, true);
            }
            Story.Set(stage == 0 ? SpiritsIslet.Stage1Key : SpiritsIslet.Stage2Key);
            user.Message(MessageHud.MessageType.Center, stage == 0 ? "$iov_t4_stage1" : "$iov_t4_stage2");
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }

    // Shows the village as its stage is (each client from the world's keys).
    class SpiritStages : MonoBehaviour
    {
        internal static string GroupName(int stage) { return "IoV_SpiritStage" + stage; }

        readonly Transform[] groups = new Transform[3];
        int shown = -1;

        void Start()
        {
            for (int s = 0; s < 3; s++) groups[s] = transform.parent.Find(GroupName(s));
            InvokeRepeating("Check", 0.1f, 2f);
        }

        void OnDisable() { if (near) { near = false; VillageMusic.Want(-1); } }

        bool near;

        void Check()
        {
            KeepAltar();
            int stage = SpiritsIslet.Stage;
            // its music while the player is in it
            Player player = Player.m_localPlayer;
            bool inside = player != null && Utils.DistanceXZ(player.transform.position, transform.parent.position) < VillageMusic.Reach;
            if (inside) VillageMusic.Want(stage);
            else if (near) VillageMusic.Want(-1);
            near = inside;
            if (stage == shown) return;
            shown = stage;
            for (int s = 0; s < 3; s++)
                if (groups[s] != null) groups[s].gameObject.SetActive(s == stage);
        }

        // The altar where the plan has it now (an altar already in the world stays where an older plan put it;
        // its owner moves it).
        void KeepAltar()
        {
            Transform root = transform.parent;
            Vector3 want = root.TransformPoint(IsletBuilder.SpiritAltarAt);
            foreach (SpiritAltar altar in SpiritAltar.All)
            {
                if (altar == null || Utils.DistanceXZ(altar.transform.position, root.position) > 80f) continue;
                ZNetView view = altar.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner() || Vector3.Distance(altar.transform.position, want) < 0.3f) continue;
                altar.transform.SetPositionAndRotation(want, root.rotation);
                view.GetZDO().SetPosition(want);
                view.GetZDO().SetRotation(root.rotation);
                Plugin.Debug("Brim village: the altar moved to where the plan has it, " + want.ToString("F1"));
            }
        }
    }

    // A door of the Brim village: not there while the village lies in ruins (its walls broken round it), there
    // once it's cleared.
    class SpiritDoor : MonoBehaviour
    {
        bool? shown;

        void Start() { InvokeRepeating("Check", 0.2f, 2f); }

        void Check()
        {
            bool show = SpiritsIslet.Stage > 0;
            if (shown == show) return;
            shown = show;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = show;
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = show;
        }
    }

    // The Brim village's music, its own for each stage, from mourning to joy as it's restored (the user: more
    // cheerful and peaceful the nearer it is to done), the game's own pieces (all 46 listed with iov_music):
    //   0 ruined:   Deep North - Memorial Site (a lament for the drowned);
    //   1 cleared:  Deep North - Timberhall Village (a village again, warmer, hopeful);
    //   2 rebuilt:  End Credits - Mead and Merriment! (the game's most joyful), looped while here.
    // Each the first of its list the game has. Played the way a raid's music is (above a place's, the sea's and
    // the weather's), while the player is in the village; the game's own when they leave. Each player's own.
    static class VillageMusic
    {
        static readonly string[][] ByStage =
        {
            new[] { "location_deepnorth_memorialsite", "location_ashlands_ruins", "evening" },
            new[] { "location_deepnorth_timberhallvillage", "home", "morning" },
            new[] { "credits", "home", "meadows" },
        };
        internal const float Reach = 65f;

        static int wanted = -1;
        static string playing;
        static MusicMan.NamedMusic looped; // a piece made to loop here, and whether it did before
        static bool loopedBefore;

        internal static void Want(int stage)
        {
            if (stage == wanted) return;
            wanted = stage;
            Unloop();
            playing = stage >= 0 && stage < ByStage.Length ? First(ByStage[stage]) : null;
            Plugin.Debug("Brim village: music " + (playing ?? "the game's own"));
        }

        static string First(string[] list)
        {
            MusicMan music = MusicMan.instance;
            if (music == null || music.m_music == null) return list[0];
            foreach (string name in list)
                foreach (MusicMan.NamedMusic m in music.m_music)
                    if (m != null && m.m_name == name && m.m_clips != null && m.m_clips.Length > 0)
                    {
                        // (a piece that plays once, as the credits do, loops while it's the village's)
                        if (!m.m_loop) { looped = m; loopedBefore = m.m_loop; m.m_loop = true; }
                        return name;
                    }
            return null;
        }

        static void Unloop()
        {
            if (looped != null) looped.m_loop = loopedBefore;
            looped = null;
        }

        [HarmonyPatch(typeof(RandEventSystem), "GetMusicOverride")]
        static class OverridePatch
        {
            static void Postfix(ref string __result)
            {
                if (string.IsNullOrEmpty(__result) && playing != null) __result = playing;
            }
        }
    }

    // The player's yard in the Brim village: building allowed within it.
    class SpiritYard : MonoBehaviour
    {
        static readonly List<SpiritYard> all = new List<SpiritYard>();

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        internal static bool Covers(Vector3 p)
        {
            foreach (SpiritYard y in all)
                if (y != null && Utils.DistanceXZ(p, y.transform.position) < IsletBuilder.SpiritYardRadius) return true;
            return false;
        }
    }
}
