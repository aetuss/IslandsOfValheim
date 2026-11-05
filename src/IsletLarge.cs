using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Rare large islets: two or three of each kind, about two and a half times a normal islet, with a
    // vanilla dungeon where the land has one, the kind's ruin and guards, a tall old beacon tower to see it
    // by, more trees and plants, and life of its own (LargeIslet): animals that come back, a starred guardian,
    // and a chest whose loot, like the creatures' stars, grows with how far out in its ring the islet is.
    // Players may build there (the ground is a mesh, so it can't be dug, levelled or cultivated).
    static partial class IsletBuilder
    {
        internal const string LargePrefix = "IoV_Large_";
        const float LargeScale = 2.5f;

        internal enum Where { Plateau, Slope, Shore, Water, Air }

        internal class Life
        {
            internal string Prefab;
            internal int Cap;
            internal Where Where;
        }

        internal class LargeKind
        {
            internal IsletTier Tier;
            internal int Variant;
            internal float Height;          // the plateau above the sea
            internal string[] Dungeon;      // vanilla dungeon locations, the first that exists; null: none
            internal string Guardian;
            internal Life[] Life;
            internal string[] Extra;        // more vanilla things to place (a wild beehive...)
            // Worked out while it's built: radii, and where the keeper stands from the middle.
            internal float Plateau, Shore;
            internal Vector3 KeeperOffset;

            internal string LocationName { get { return LargePrefix + Tier.Name; } }
            internal string KeeperName { get { return "IoV_LargeKeeper_" + Tier.Name; } }
            internal string ChestName(int band) { return "IoV_LargeChest_" + Tier.Name + "_" + band; }
        }

        static Life L(string prefab, int cap, Where where) { return new Life { Prefab = prefab, Cap = cap, Where = where }; }

        internal static readonly List<LargeKind> LargeKinds = new List<LargeKind>
        {
            new LargeKind { Tier = IsletTier.All[0], Variant = 0, Height = 5f, Dungeon = new[] { "Crypt4", "Crypt3", "Crypt2" }, Guardian = "Greydwarf_Elite",
                Life = new[] { L("Deer", 3, Where.Slope), L("Boar", 3, Where.Plateau), L("Neck", 2, Where.Shore) }, Extra = new[] { "Beehive" } },
            new LargeKind { Tier = IsletTier.All[1], Variant = 2, Height = 4f, Dungeon = new[] { "SunkenCrypt4", "SunkenCrypt3", "SunkenCrypt2", "SunkenCrypt1" }, Guardian = "Abomination",
                Life = new[] { L("Leech", 4, Where.Water), L("Crow", 3, Where.Air) } },
        };

        internal static LargeKind LargeKindOf(string prefabName)
        {
            foreach (LargeKind kind in LargeKinds)
                if (prefabName == kind.LocationName || prefabName == kind.KeeperName) return kind;
            return null;
        }

        // ---- prefabs: the keeper of each kind, and its three chests ----

        internal static void RegisterLargePrefabs()
        {
            foreach (LargeKind kind in LargeKinds)
            {
                GameObject go = PrefabManager.Instance.CreateEmptyPrefab(kind.KeeperName, true);
                Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(go.GetComponent<MeshFilter>());
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<ZNetView>().m_persistent = true;
                go.AddComponent<LargeIslet>();
                PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
                for (int band = 0; band < 3; band++) IsletPrefabs.AddLargeChest(kind.Tier, kind.ChestName(band), band);
                foreach (Life life in kind.Life)
                    if (PrefabManager.Instance.GetPrefab(life.Prefab) == null) Plugin.Log.LogWarning("Large " + kind.Tier.Name + " islets: " + life.Prefab + " not found, left out.");
            }
        }

        // ---- the location ----

        internal static void RegisterLargeIslets()
        {
            if (Plugin.LargeIslets.Value <= 0) return;
            foreach (LargeKind kind in LargeKinds)
            {
                try { LargeIsletOf(kind); }
                catch (System.Exception e) { Plugin.Log.LogError("Large " + kind.Tier.Name + " islet failed: " + e); }
                finally { PlateauHeight = SmallPlateau; segments = SmallSegments; }
            }
        }

        static void LargeIsletOf(LargeKind kind)
        {
            tier = kind.Tier;
            locationName = kind.LocationName;
            SetVariant(kind.Variant);
            plateauRadius *= LargeScale;
            shoreRadius *= LargeScale;
            maxRadius = shoreRadius * Mathf.Max(stretchX, stretchZ) + 26f;
            seed += 3000f;
            PlateauHeight = kind.Height;
            segments = 180; // the rings are much longer: keep the ground's triangles small
            kind.Plateau = plateauRadius * 0.88f * Mathf.Min(stretchX, stretchZ);
            kind.Shore = shoreRadius * 0.9f * Mathf.Min(stretchX, stretchZ);
            root = ZoneManager.Instance.CreateLocationContainer(locationName);
            parent = root.transform;
            built = 0;
            missing = 0;
            BuildGround();
            PlaceRocks();

            // The dungeon in the middle, where there is one.
            DungeonInterior interior = null;
            if (kind.Dungeon != null)
            {
                string source = FindLocation(kind.Dungeon);
                if (source != null) interior = AddDungeon(source, Vector2.zero, cliffDeg + 180f, locationName);
                else Plugin.Log.LogWarning("Large " + tier.Name + " islet: no vanilla dungeon " + string.Join(", ", kind.Dungeon) + "; it has none.");
            }
            // The kind's ruin to one side (its guards and chest with it), the ridge structure on the cliff.
            Shifted(Polar(cliffDeg + 150f, kind.Plateau * 0.6f), MainDesign());
            RidgeDesign();
            RidgeGuard();
            // The old beacon tower on the other side, tall enough to be seen from the sea.
            Vector2 tower = Polar(cliffDeg + 255f, kind.Plateau * 0.62f);
            BeaconTower(tower, 5);
            Vector2 keeper = tower + Polar(cliffDeg + 255f, -3f); // at the tower's foot, on the inner side
            kind.KeeperOffset = new Vector3(keeper.x, PlateauHeight, keeper.y);
            PlaceNetworked(kind.KeeperName, kind.KeeperOffset, 0f);
            if (kind.Extra != null)
                foreach (string extra in kind.Extra)
                    PlaceNetworked(new[] { extra }, Polar(cliffDeg + 300f, kind.Plateau + 3f), cliffDeg, 0f);

            LargeNature(kind);
            PlaceDock(cliffDeg + 229f);
            PlaceDock(cliffDeg + 120f);
            PlaceWreck(cliffDeg + 194f);
            PlaceHints();
            PlaceOutsideGuard();

            Location location = root.AddComponent<Location>();
            location.m_applyRandomDamage = tier.Ruined;
            location.m_exteriorRadius = 60f;
            if (interior != null) interior.CopyTo(location);
            location.m_noBuild = false; // players may settle here

            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.BiomeArea = Heightmap.BiomeArea.Median;
            config.Quantity = Plugin.LargeIslets.Value;
            config.Priotized = true;
            config.SnapToWater = true;
            config.RandomRotation = true;
            config.ClearArea = false;
            config.ExteriorRadius = 60f;
            config.MinAltitude = -1000f;
            config.MaxAltitude = -8f;
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 1000f;
            config.MinDistance = tier.MinDistance;
            config.MaxDistance = tier.MaxDistance;
            config.Group = "IoV_Large";
            config.MinDistanceFromSimilar = 900f;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + locationName + " (quantity " + config.Quantity + ", " + built + " parts, " + missing + " prefabs not found)");
        }

        // A ruin design built around another point of the plateau (the designs are laid out around (0, 0);
        // the plateau is flat, so the ground is the same there).
        static void Shifted(Vector2 at, System.Action design)
        {
            GameObject group = new GameObject("IoV_Shifted");
            group.transform.SetParent(parent, false);
            group.transform.localPosition = new Vector3(at.x, 0f, at.y);
            Transform outer = parent;
            parent = group.transform;
            design();
            parent = outer;
        }

        static System.Action MainDesign()
        {
            switch (tier.Style)
            {
                case RuinStyle.StoneHall: return StoneHall;
                case RuinStyle.StiltHut: return StiltHut;
                case RuinStyle.FulingCamp: return FulingCamp;
                case RuinStyle.DvergrRuin: return DvergrRuin;
                case RuinStyle.GraustenFort: return GraustenFort;
                case RuinStyle.DvergrCamp: return DvergrCamp;
                default: return FrostKeep;
            }
        }

        static void RidgeDesign()
        {
            switch (tier.Style)
            {
                case RuinStyle.StoneHall: StoneTower(); break;
                case RuinStyle.StiltHut: Graves(); break;
                case RuinStyle.FulingCamp: FulingLookout(); break;
                case RuinStyle.DvergrRuin: DvergrColumn(); break;
                case RuinStyle.GraustenFort: GraustenPillar(); break;
                case RuinStyle.DvergrCamp: DvergrWatch(); break;
                default: FrozenGraves(); break;
            }
        }

        // A base block, a stack of the kind's pillars and an old beacon on top.
        static void BeaconTower(Vector2 at, int pillars)
        {
            float deg = Mathf.Atan2(at.y, at.x) * Mathf.Rad2Deg;
            Vector3 turn = new Vector3(0f, 90f - deg, 0f);
            float g = GroundMin(at.x, at.y, 1.2f) - 0.3f;
            float top = Top(At(tier.BeaconBase, new Vector3(at.x, g, at.y), turn, false), g + 1f);
            for (int i = 0; i < pillars; i++) top = Top(At(tier.BeaconPillar, new Vector3(at.x, top, at.y), turn, false), top + 2f);
            At(IsletBeacons.PrefabName(tier), new Vector3(at.x, top, at.y), turn, false);
        }

        // Trees all round the slope, bushes at the plateau's edge, berries and mushrooms: more of everything
        // than a small islet, and all of it the game's own, so it can be chopped and picked and grows back.
        static void LargeNature(LargeKind kind)
        {
            for (int i = 0; i < 16; i++)
            {
                float deg = i * 22.5f + 7f * Noise(i * 1.3f, 2f);
                float r = kind.Plateau + 3f + (i % 3) * (kind.Shore - kind.Plateau - 8f) / 3f;
                int k = i;
                Optional(0.75f, () => OnGround(k % 4 == 3 ? tier.SmallTrees : tier.Trees, Polar(deg, r), deg * 7f, 0f, 0.05f, false));
            }
            for (int i = 0; i < 12; i++)
            {
                float deg = i * 30f + 13f;
                Optional(0.7f, () => OnGround(tier.Bushes, Polar(deg, kind.Plateau + 1.5f), deg * 5f, 0f, 0.15f, false));
            }
            for (int i = 0; i < 7; i++)
            {
                float deg = i * 51f + 20f;
                Optional(0.85f, () => PlaceNetworked(tier.Berries, Polar(deg, kind.Plateau - 2f + (i % 2) * 4f), deg, 0f));
            }
            for (int i = 0; i < 5; i++)
            {
                float deg = i * 72f + 40f;
                Optional(0.75f, () => PlaceNetworked(tier.Mushrooms, Polar(deg, kind.Plateau + 2f), 0f, 0f));
            }
            PlacePickables();
        }
    }

    // A large islet's life, run by the client that owns its keeper while a player is near:
    // - the first time: a starred guardian, a few of the kind's creatures and the chest;
    // - then the islet's animals, each kept to a few: one comes back every few minutes when there are fewer,
    //   unless a player's base is there (as with the game's own spawning near a base).
    // How far out in its ring the islet is (inner, middle or outer third) sets the stars and the chest.
    class LargeIslet : MonoBehaviour
    {
        const float ActiveRange = 110f;   // only while a player is this close
        const float CountRange = 90f;     // creatures this close count as the islet's
        const float RespawnSeconds = 240f;
        const string FirstKey = "IoV_LargeFirst";

        ZNetView nview;
        IsletBuilder.LargeKind kind;

        void Awake()
        {
            nview = GetComponent<ZNetView>();
            if (nview == null || nview.GetZDO() == null) return;
            kind = IsletBuilder.LargeKindOf(Utils.GetPrefabName(gameObject));
            if (kind == null) return;
            InvokeRepeating("Tick", Random.Range(3f, 6f), 5f);
        }

        // The islet's middle on its plateau (the keeper stands at the tower's foot, KeeperOffset from it).
        Vector3 Centre { get { return transform.position - transform.rotation * kind.KeeperOffset + Vector3.up * kind.Height; } }

        // Which third of its ring the islet is in: 0 inner, 1 middle, 2 outer.
        int Band
        {
            get
            {
                float min = kind.Tier.MinDistance * WorldSize.Scale, max = kind.Tier.MaxDistance * WorldSize.Scale;
                float d = new Vector2(transform.position.x, transform.position.z).magnitude;
                float f = max > min ? Mathf.Clamp01((d - min) / (max - min)) : 0f;
                return f < 1f / 3f ? 0 : f < 2f / 3f ? 1 : 2;
            }
        }

        // A creature's level (1 = no star) for the band: inner none, middle a chance of one star, outer a
        // chance of two. The guardian always has one, and in the outer third two.
        static int Level(int band, bool guardian)
        {
            float r = Random.value;
            if (guardian) return band == 0 ? 2 : band == 1 ? (r < 0.35f ? 3 : 2) : 3;
            if (band == 0) return 1;
            if (band == 1) return r < 0.4f ? 2 : 1;
            return r < 0.25f ? 3 : r < 0.65f ? 2 : 1;
        }

        void Tick()
        {
            if (kind == null || !nview.IsValid() || !nview.IsOwner() || ZNetScene.instance == null) return;
            if (!Player.IsPlayerInRange(transform.position, ActiveRange)) return;
            ZDO zdo = nview.GetZDO();
            if (!zdo.GetBool(FirstKey, false))
            {
                zdo.Set(FirstKey, true);
                First();
            }
            foreach (IsletBuilder.Life life in kind.Life) Keep(life, zdo);
        }

        void First()
        {
            int band = Band;
            Vector3 p;
            if (Point(IsletBuilder.Where.Plateau, out p)) Spawn(kind.Guardian, p, Level(band, true));
            string[] guards = kind.Tier.Guards;
            for (int i = 0; i < 4; i++)
                if (Point(IsletBuilder.Where.Plateau, out p)) Spawn(guards[i % guards.Length], p, Level(band, false));
            GameObject chest = ZNetScene.instance.GetPrefab(kind.ChestName(band));
            Vector3 at = transform.position + transform.rotation * new Vector3(1.8f, 0.2f, 1.2f);
            if (chest != null) Instantiate(chest, Ground(at), transform.rotation);
            Plugin.Debug("Large " + kind.Tier.Name + " islet at " + transform.position.ToString("F0") + ": band " + band + ", guardian " + kind.Guardian + " and guards out, chest " + (chest != null));
            foreach (IsletBuilder.Life life in kind.Life)
                for (int i = 0; i < life.Cap; i++)
                    if (Point(life.Where, out p)) Spawn(life.Prefab, p, Level(band, false));
        }

        // One more of an animal when there are fewer than its cap, one per RespawnSeconds.
        void Keep(IsletBuilder.Life life, ZDO zdo)
        {
            int have = 0;
            foreach (Character c in Character.GetAllCharacters())
                if (c != null && !c.IsDead() && Utils.GetPrefabName(c.gameObject) == life.Prefab && Vector3.Distance(c.transform.position, Centre) < CountRange) have++;
            string key = "IoV_LargeLast_" + life.Prefab;
            long now = ZNet.instance.GetTime().Ticks;
            if (have >= life.Cap)
            {
                zdo.Set(key, now);
                return;
            }
            long last = zdo.GetLong(key, 0L);
            if (last != 0L && now - last < (long)(RespawnSeconds * System.TimeSpan.TicksPerSecond)) return;
            Vector3 p;
            if (!Point(life.Where, out p)) return;
            zdo.Set(key, now);
            Spawn(life.Prefab, p, Level(Band, false));
            Plugin.Debug("Large " + kind.Tier.Name + " islet: a " + life.Prefab + " comes back (" + (have + 1) + "/" + life.Cap + ")");
        }

        static void Spawn(string prefabName, Vector3 at, int level)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null) return;
            GameObject go = Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            Character c = go.GetComponent<Character>();
            if (c != null && level > 1) c.SetLevel(level);
        }

        // A random spot of the kind asked for, on the islet's ground (or in the water around it, or in the
        // air above it), not inside a player's base.
        bool Point(IsletBuilder.Where where, out Vector3 p)
        {
            float water = ZoneSystem.instance.m_waterLevel;
            Vector3 centre = Centre;
            for (int tries = 0; tries < 12; tries++)
            {
                float r;
                switch (where)
                {
                    case IsletBuilder.Where.Slope: r = Random.Range(kind.Plateau + 2f, kind.Shore - 4f); break;
                    case IsletBuilder.Where.Shore: r = Random.Range(kind.Shore - 5f, kind.Shore); break;
                    case IsletBuilder.Where.Water: r = Random.Range(kind.Shore + 4f, kind.Shore + 12f); break;
                    default: r = Random.Range(4f, kind.Plateau * 0.85f); break;
                }
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 q = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (where == IsletBuilder.Where.Water)
                {
                    q.y = water - 0.5f;
                }
                else
                {
                    q = Ground(q);
                    if (q.y < water + 0.3f) continue;
                    if (where == IsletBuilder.Where.Air) q.y += 8f;
                }
                if (EffectArea.IsPointInsideArea(q, EffectArea.Type.PlayerBase, 0f) != null) continue;
                p = q;
                return true;
            }
            p = Vector3.zero;
            return false;
        }

        static Vector3 Ground(Vector3 q)
        {
            RaycastHit hit;
            int mask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece");
            Vector3 from = new Vector3(q.x, ZoneSystem.instance.m_waterLevel + 60f, q.z);
            if (Physics.Raycast(from, Vector3.down, out hit, 90f, mask, QueryTriggerInteraction.Ignore)) return hit.point + Vector3.up * 0.3f;
            return new Vector3(q.x, ZoneSystem.instance.m_waterLevel - 5f, q.z);
        }
    }
}
