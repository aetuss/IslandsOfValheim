using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IslandsOfValheim
{
    // Islets in worlds made before the mod (or before a kind of islet was added). Valheim decides where
    // locations go when a world is created, so an older world has none of ours. Once per world the server
    // (or the host) counts each of our locations and adds the missing ones in ocean of the right ring:
    // - where nobody has sailed yet, registered like any location, so the game builds them when somebody
    //   gets near;
    // - in ocean players have already sailed (a generated zone), only when there's no room left elsewhere,
    //   built there at once the way the game builds a location in a new zone.
    // A spot must be open, deep sea, well away from land, from the game's own locations, from our other
    // islets and (in sailed ocean) from anything players built, ships included, and from players.
    // Sævar's harbour and the islets of his tale are unique: one is added if the world has none.
    // When it's done a world key is set, so it doesn't run again (iov_placeislets runs it again).
    static class ExistingWorlds
    {
        internal const string DoneKey = "islandsofvalheim_existing_v1"; // a world key, not a text key
        const float BuildingDistance = 100f; // no islet built this close to a player's piece or ship
        const float PlayerDistance = 250f;   // nor this close to a player
        const float IsletSpacing = 150f;     // between any two of our locations
        const float Ring = 35f;              // all open sea this far around
        const int MaxBuiltPerPass = 40;      // islets built at once in sailed ocean, the rest wait

        static readonly System.Reflection.MethodInfo registerLocation = AccessTools.Method(typeof(ZoneSystem), "RegisterLocation");
        static readonly System.Reflection.MethodInfo spawnLocation = AccessTools.Method(typeof(ZoneSystem), "SpawnLocation");
        static readonly System.Reflection.FieldInfo generatedZones = AccessTools.Field(typeof(ZoneSystem), "m_generatedZones");
        static readonly System.Reflection.FieldInfo objectsByID = AccessTools.Field(typeof(ZDOMan), "m_objectsByID");

        static bool started;
        static float readySince = -1f;

        // Every frame (Plugin.Update): waits until the world and its locations are ready on the server,
        // then runs the pass once if this world hasn't had it.
        internal static void Tick()
        {
            if (ZNet.instance == null)
            {
                started = false; // back at the main menu: the next world gets its own check
                converted = false;
                readySince = -1f;
                return;
            }
            if (!converted && ZNet.instance.IsServer() && ZoneSystem.instance != null && ZoneSystem.instance.LocationsGenerated)
            {
                converted = true; // as soon as the world's locations are there, before anyone sails near
                LargeToLand(ZoneSystem.instance);
            }
            if (started || !Plugin.PlaceInExistingWorlds.Value) return;
            ZoneSystem zones = ZoneSystem.instance;
            if (!ZNet.instance.IsServer() || zones == null || ZNetScene.instance == null || ZDOMan.instance == null ||
                WorldGenerator.instance == null || !zones.LocationsGenerated)
            {
                readySince = -1f;
                return;
            }
            if (readySince < 0f) readySince = Time.time;
            if (Time.time - readySince < 10f) return; // let the world settle first
            started = true;
            if (zones.GetGlobalKey(DoneKey))
            {
                Plugin.Debug("Islets in existing worlds: this world already had its pass.");
                return;
            }
            Plugin.Instance.StartCoroutine(Run(null));
        }

        static bool converted;

        // Islets of an older kind that the world has room for but nobody has come near yet become the new kind,
        // where they are: nothing is built there, so nothing is lost. Those already built stay as they are,
        // with whatever players made on them. Every load (cheap).
        // - large islets on our ground mesh become islets of the game's own terrain;
        // - small islets with our old ruins become the same shape with the game's own structures.
        static void LargeToLand(ZoneSystem zones)
        {
            Dictionary<string, ZoneSystem.ZoneLocation> land = new Dictionary<string, ZoneSystem.ZoneLocation>();
            foreach (ZoneSystem.ZoneLocation location in zones.m_locations)
                if (location != null && location.m_prefabName != null) land[location.m_prefabName] = location;
            // How many of each design the world has already, so the converted ones go to the designs it has
            // fewest of (a world gets to see them all, not three of the first).
            Dictionary<string, int> have = new Dictionary<string, int>();
            foreach (ZoneSystem.LocationInstance instance in zones.m_locationInstances.Values)
                if (instance.m_location != null && instance.m_location.m_prefabName != null) Add(have, instance.m_location.m_prefabName);
            int changed = 0, small = 0;
            foreach (Vector2s zone in new List<Vector2s>(zones.m_locationInstances.Keys))
            {
                ZoneSystem.LocationInstance instance = zones.m_locationInstances[zone];
                if (instance.m_placed || instance.m_location == null) continue;
                string templated = IsletBuilder.TemplatedName(instance.m_location.m_prefabName);
                ZoneSystem.ZoneLocation newer;
                if (templated != null)
                {
                    if (!land.TryGetValue(templated, out newer)) continue;
                    instance.m_location = newer;
                    zones.m_locationInstances[zone] = instance;
                    small++;
                    continue;
                }
                IsletBuilder.LargeKind old = IsletBuilder.LargeKindOf(instance.m_location.m_prefabName);
                if (old == null || instance.m_location.m_prefabName != old.LocationName) continue;
                ZoneSystem.ZoneLocation to = null;
                int fewest = int.MaxValue;
                foreach (IsletBuilder.LargeKind design in IsletBuilder.LargeKinds)
                {
                    ZoneSystem.ZoneLocation candidate;
                    if (design.Tier != old.Tier || !land.TryGetValue(design.LandName, out candidate)) continue;
                    int n;
                    have.TryGetValue(design.LandName, out n);
                    if (n < fewest) { fewest = n; to = candidate; }
                }
                if (to == null) continue;
                instance.m_location = to;
                zones.m_locationInstances[zone] = instance;
                Add(have, to.m_prefabName);
                changed++;
            }
            if (changed > 0) Plugin.Log.LogInfo("Large islets: " + changed + " not yet built will be of the game's own terrain");
            if (small > 0) Plugin.Log.LogInfo("Small islets: " + small + " not yet built will have the new designs");
        }

        // Old and new islets of a kind count as one: a world that has its old ones gets no new ones on top.
        static string Kind(string prefabName)
        {
            string templated = IsletBuilder.TemplatedName(prefabName);
            if (templated != null) return templated;
            IsletBuilder.LargeKind kind = IsletBuilder.LargeKindOf(prefabName);
            return kind != null && prefabName == kind.LocationName ? kind.LandName : prefabName;
        }

        internal static void RunNow(Terminal context)
        {
            Plugin.Instance.StartCoroutine(Run(context));
        }

        // Our locations that the pass looks after: the islets, the harbour, the tale's islets (not the
        // dungeon tests).
        static bool Ours(ZoneSystem.ZoneLocation location)
        {
            return location != null && location.m_enable && location.m_prefabName != null && location.m_prefabName.StartsWith("IoV_");
        }

        // Which half of the world a location keeps to, as IsletPlacement does when a world is made.
        static bool SideAllows(string prefabName, Vector3 p)
        {
            if (prefabName == Story.AshIslet) return p.z <= 0f;
            if (prefabName == Story.HiddenIslet) return p.z > 0f;
            foreach (IsletTier tier in IsletTier.All)
                if (prefabName.StartsWith(tier.LocationPrefix) || IsletBuilder.IsLargeOf(prefabName, tier)) return tier.Allows(p);
            return true;
        }

        // ---- what players have out there ----

        static readonly Dictionary<int, bool> isBuilt = new Dictionary<int, bool>();

        static bool Built(int prefabHash)
        {
            bool built;
            if (isBuilt.TryGetValue(prefabHash, out built)) return built;
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabHash);
            built = prefab != null && (prefab.GetComponent<Piece>() != null || prefab.GetComponent<Ship>() != null || prefab.GetComponent<TombStone>() != null);
            isBuilt[prefabHash] = built;
            return built;
        }

        // Players' pieces, ships and graves, by zone.
        static Dictionary<Vector2s, List<Vector3>> BuildIndex()
        {
            Dictionary<Vector2s, List<Vector3>> index = new Dictionary<Vector2s, List<Vector3>>();
            IDictionary all = objectsByID != null ? objectsByID.GetValue(ZDOMan.instance) as IDictionary : null;
            if (all == null) return index;
            foreach (object value in all.Values)
            {
                ZDO zdo = value as ZDO;
                if (zdo == null || !Built(zdo.GetPrefab())) continue;
                Vector3 p = zdo.GetPosition();
                Vector2s zone = ZoneSystem.GetZone(p);
                List<Vector3> list;
                if (!index.TryGetValue(zone, out list)) index[zone] = list = new List<Vector3>();
                list.Add(p);
            }
            return index;
        }

        static bool AnyNear(Dictionary<Vector2s, List<Vector3>> index, Vector3 p, float radius)
        {
            Vector2s zone = ZoneSystem.GetZone(p);
            int reach = Mathf.CeilToInt(radius / 64f);
            for (int dx = -reach; dx <= reach; dx++)
                for (int dy = -reach; dy <= reach; dy++)
                {
                    List<Vector3> list;
                    if (!index.TryGetValue(new Vector2s(zone.x + dx, zone.y + dy), out list)) continue;
                    foreach (Vector3 q in list)
                        if (new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude <= radius * radius) return true;
                }
            return false;
        }

        static bool PlayerNear(Vector3 p)
        {
            List<Vector3> players = new List<Vector3>();
            players.Add(ZNet.instance.GetReferencePosition());
            foreach (ZNetPeer peer in ZNet.instance.GetPeers()) players.Add(peer.m_refPos);
            foreach (Vector3 q in players)
                if (new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude < PlayerDistance * PlayerDistance) return true;
            return false;
        }

        // ---- the pass ----

        class Spots
        {
            internal readonly List<Vector2s> Unexplored = new List<Vector2s>();
            internal readonly List<Vector2s> Explored = new List<Vector2s>();
        }

        static IEnumerator Run(Terminal context)
        {
            ZoneSystem zones = ZoneSystem.instance;
            HashSet<Vector2s> generated = generatedZones != null ? generatedZones.GetValue(zones) as HashSet<Vector2s> : null;
            if (generated == null || registerLocation == null || spawnLocation == null)
            {
                Plugin.Log.LogWarning("Islets in existing worlds: this version of the game isn't the one the mod knows; nothing placed.");
                yield break;
            }
            Plugin.Log.LogInfo("Islets in existing worlds: looking for missing islets...");
            Dictionary<Vector2s, List<Vector3>> buildings = BuildIndex();
            yield return null;

            // What the world already has: our locations by name, and where every one of them is.
            Dictionary<string, int> counts = new Dictionary<string, int>();
            List<Vector3> ours = new List<Vector3>();
            Dictionary<string, List<Vector3>> byGroup = new Dictionary<string, List<Vector3>>();
            foreach (ZoneSystem.LocationInstance instance in zones.GetLocationList())
            {
                if (!Ours(instance.m_location)) continue;
                Add(counts, Kind(instance.m_location.m_prefabName));
                ours.Add(instance.m_position);
                AddTo(byGroup, instance.m_location.m_group, instance.m_position);
            }

            // Every open-sea zone with no location yet, sailed or not, worked out once.
            Spots sea = new Spots();
            int reach = Mathf.CeilToInt(WorldSize.Radius / 64f);
            float edge = WorldSize.Radius - 200f;
            for (int x = -reach; x <= reach; x++)
            {
                for (int y = -reach; y <= reach; y++)
                {
                    Vector2s zone = new Vector2s(x, y);
                    Vector3 center = ZoneSystem.GetZonePos(zone);
                    if (center.magnitude > edge || zones.m_locationInstances.ContainsKey(zone)) continue;
                    if (WorldGenerator.instance.GetBiome(center) != Heightmap.Biome.Ocean) continue;
                    (generated.Contains(zone) ? sea.Explored : sea.Unexplored).Add(zone);
                }
                if (x % 16 == 0) yield return null;
            }
            Plugin.Debug("Islets in existing worlds: " + sea.Unexplored.Count + " open-sea zones nobody has sailed, " + sea.Explored.Count + " sailed");

            System.Random random = new System.Random(WorldGenerator.instance.GetSeed() ^ 0x1e7a);
            int registered = 0, built = 0, short_ = 0;
            List<string> report = new List<string>();
            foreach (ZoneSystem.ZoneLocation location in new List<ZoneSystem.ZoneLocation>(zones.m_locations))
            {
                if (!Ours(location)) continue;
                int have;
                counts.TryGetValue(location.m_prefabName, out have);
                int missing = location.m_unique ? (have > 0 ? 0 : 1) : location.m_quantity - have;
                if (missing <= 0) continue;
                int added = 0, addedBuilt = 0;
                List<Vector2s> unexplored = Candidates(location, sea.Unexplored, random);
                List<Vector2s> explored = null;
                int u = 0, e = 0;
                for (int n = 0; n < missing; n++)
                {
                    Vector3 spot;
                    bool found = Next(location, unexplored, ref u, ours, byGroup, null, random, out spot);
                    bool build = false;
                    if (!found && built < MaxBuiltPerPass)
                    {
                        if (explored == null) explored = Candidates(location, sea.Explored, random);
                        found = Next(location, explored, ref e, ours, byGroup, buildings, random, out spot);
                        build = found;
                    }
                    if (!found) { short_ += missing - n; break; }
                    spot.y = zones.m_waterLevel;
                    if (build)
                    {
                        registerLocation.Invoke(zones, new object[] { location, spot, true });
                        Quaternion rotation = Quaternion.Euler(0f, random.Next(16) * 22.5f, 0f);
                        spawnLocation.Invoke(zones, new object[] { location, random.Next(), spot, rotation, ZoneSystem.SpawnMode.Full, new List<GameObject>(), false });
                        built++;
                        addedBuilt++;
                        Plugin.Debug("Islets in existing worlds: built " + location.m_prefabName + " at " + spot.ToString("F0") + " in sailed ocean");
                        yield return null;
                    }
                    else
                    {
                        registerLocation.Invoke(zones, new object[] { location, spot, false });
                        registered++;
                    }
                    ours.Add(spot);
                    AddTo(byGroup, location.m_group, spot);
                    added++;
                }
                if (added > 0) report.Add(location.m_prefabName + " " + added + (addedBuilt > 0 ? " (" + addedBuilt + " built now)" : ""));
                yield return null;
            }

            zones.SetGlobalKey(DoneKey);
            string summary = report.Count == 0 ? "the world already had every islet; nothing added."
                : "added " + (registered + built) + " islets, " + built + " built now in sailed ocean and " + registered + " where nobody has sailed yet: " + string.Join(", ", report.ToArray()) + ".";
            if (short_ > 0) summary += " " + short_ + " didn't fit (no open sea left in their ring).";
            Plugin.Log.LogInfo("Islets in existing worlds: " + summary);
            if (context != null) context.AddString("Islets in existing worlds: " + summary);
        }

        static void Add(Dictionary<string, int> map, string key)
        {
            int n;
            map.TryGetValue(key, out n);
            map[key] = n + 1;
        }

        static void AddTo(Dictionary<string, List<Vector3>> map, string key, Vector3 p)
        {
            if (string.IsNullOrEmpty(key)) return;
            List<Vector3> list;
            if (!map.TryGetValue(key, out list)) map[key] = list = new List<Vector3>();
            list.Add(p);
        }

        // The sea zones in the location's ring and half of the world, shuffled.
        static List<Vector2s> Candidates(ZoneSystem.ZoneLocation location, List<Vector2s> zones, System.Random random)
        {
            List<Vector2s> result = new List<Vector2s>();
            foreach (Vector2s zone in zones)
            {
                Vector3 center = ZoneSystem.GetZonePos(zone);
                float d = new Vector2(center.x, center.z).magnitude;
                if ((location.m_minDistance > 0f && d < location.m_minDistance - 32f) || (location.m_maxDistance > 0f && d > location.m_maxDistance + 32f)) continue;
                if (!SideAllows(location.m_prefabName, center)) continue;
                result.Add(zone);
            }
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                Vector2s swap = result[i]; result[i] = result[j]; result[j] = swap;
            }
            return result;
        }

        // The next good spot from the candidates (from where the last search stopped). buildings: only for
        // sailed ocean, where players may have left things.
        static bool Next(ZoneSystem.ZoneLocation location, List<Vector2s> candidates, ref int from, List<Vector3> ours,
            Dictionary<string, List<Vector3>> byGroup, Dictionary<Vector2s, List<Vector3>> buildings, System.Random random, out Vector3 spot)
        {
            spot = Vector3.zero;
            for (; from < candidates.Count; from++)
            {
                if (ZoneSystem.instance.m_locationInstances.ContainsKey(candidates[from])) continue;
                Vector3 center = ZoneSystem.GetZonePos(candidates[from]);
                for (int tries = 0; tries < 3; tries++)
                {
                    Vector3 p = center + new Vector3((float)random.NextDouble() * 32f - 16f, 0f, (float)random.NextDouble() * 32f - 16f);
                    if (!Suits(location, p, ours, byGroup)) continue;
                    if (buildings != null && (AnyNear(buildings, p, BuildingDistance) || PlayerNear(p))) continue;
                    spot = p;
                    from++;
                    return true;
                }
            }
            return false;
        }

        // Open, deep sea in the location's ring and half, all sea around it, away from the game's own
        // locations and from our others.
        static bool Suits(ZoneSystem.ZoneLocation location, Vector3 p, List<Vector3> ours, Dictionary<string, List<Vector3>> byGroup)
        {
            WorldGenerator world = WorldGenerator.instance;
            float water = ZoneSystem.instance.m_waterLevel;
            float d = new Vector2(p.x, p.z).magnitude;
            if ((location.m_minDistance > 0f && d < location.m_minDistance) || (location.m_maxDistance > 0f && d > location.m_maxDistance)) return false;
            if (!SideAllows(location.m_prefabName, p)) return false;
            if (world.GetBiome(p) != Heightmap.Biome.Ocean || world.GetHeight(p.x, p.z) > water - 6f) return false;
            for (int i = 0; i < 8; i++)
            {
                float rad = i * Mathf.PI / 4f;
                Vector3 q = p + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * Ring;
                if (world.GetBiome(q) != Heightmap.Biome.Ocean || world.GetHeight(q.x, q.z) > water - 2f) return false;
            }
            float spacing = location.m_prefabName.StartsWith(IsletBuilder.LargePrefix) ? 250f : IsletSpacing;
            foreach (Vector3 o in ours)
                if (new Vector2(o.x - p.x, o.z - p.z).sqrMagnitude < spacing * spacing) return false;
            List<Vector3> similar;
            if (!string.IsNullOrEmpty(location.m_group) && location.m_minDistanceFromSimilar > 0f && byGroup.TryGetValue(location.m_group, out similar))
                foreach (Vector3 o in similar)
                    if (new Vector2(o.x - p.x, o.z - p.z).sqrMagnitude < location.m_minDistanceFromSimilar * location.m_minDistanceFromSimilar) return false;
            Vector2s zone = ZoneSystem.GetZone(p);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    ZoneSystem.LocationInstance other;
                    if (!ZoneSystem.instance.m_locationInstances.TryGetValue(new Vector2s(zone.x + dx, zone.y + dy), out other) || other.m_location == null) continue;
                    if (dx == 0 && dy == 0) return false; // one location per zone
                    float keep = Mathf.Max(other.m_location.m_exteriorRadius, other.m_location.m_interiorRadius) + 40f;
                    if (new Vector2(other.m_position.x - p.x, other.m_position.z - p.z).sqrMagnitude < keep * keep) return false;
                }
            return true;
        }
    }
}
