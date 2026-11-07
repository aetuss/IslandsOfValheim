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
        static bool buildingLarge; // while a large islet is being built (some designs leave things out)

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
            internal Layout Layout;
            internal bool Shaped;           // the ground follows the dungeon's own levelling (a pit, a shaft)
            internal float Scale = LargeScale; // times a normal islet
            internal string[] Guardian;     // the first that exists
            internal Life[] Life;
            internal string[] Extra;        // more vanilla things to place (a wild beehive...)
            // Each land has several designs (1, 2, 3...), so the islets of a world don't repeat one another.
            // Design 1 is also the old islet of our ground mesh, which keeps its old layout (Inhabit).
            internal int Design = 1;
            internal bool Peaceful;         // no guardian and no guards: only its animals and what's to find
            // Where its dungeon's middle stands on a land islet (the old mesh islet has it in the middle), and
            // how it's turned (NaN: its way in away from the cliff).
            internal Vector2 DungeonAt;
            internal float DungeonTurn = float.NaN;
            // Worked out while it's built: radii, and where the keeper stands from the middle (on the old mesh
            // islet and on the land one).
            internal float Plateau, Shore;
            internal Vector3 KeeperOffset, LandKeeperOffset;

            // The islet on our ground mesh, up to 1.9: kept for the ones already built in a world (players may
            // have settled there), but no longer placed anew. Design 1 only.
            internal string LocationName { get { return LargePrefix + Tier.Name; } }
            // The islet on the game's own terrain (from 2.0): every new one. What stands on it is its draft.
            internal string LandName { get { return LargePrefix + Tier.Name + "_Land" + (Design > 1 ? Design.ToString() : ""); } }
            internal string KeeperName { get { return "IoV_LargeKeeper_" + Tier.Name; } }
            internal string LandKeeperName { get { return "IoV_LargeKeeper_" + Tier.Name + "_Land" + Design; } }
            internal string DraftId { get { return Tier.Name + "_" + Design; } }
            // The land islet's bare ground (and dungeon), unturned, to draw its draft on: iov_goto draft_meadows_2.
            internal string BaseName { get { return "IoVTest_Base_" + DraftId; } }
            internal string ChestName(int band) { return "IoV_LargeChest_" + Tier.Name + "_" + band; }
        }

        static Life L(string prefab, int cap, Where where) { return new Life { Prefab = prefab, Cap = cap, Where = where }; }

        internal static readonly List<LargeKind> LargeKinds = new List<LargeKind>
        {
            new LargeKind { Tier = IsletTier.All[0], Variant = 0, Height = 5f, Scale = 3.2f, Dungeon = new[] { "Crypt4", "Crypt3", "Crypt2" }, Layout = Layout.Village,
                Guardian = new[] { "Greydwarf_Elite" }, DungeonAt = new Vector2(7.3f, 10.8f),
                Life = new[] { L("Deer", 3, Where.Slope), L("Boar", 3, Where.Plateau), L("Neck", 2, Where.Shore) }, Extra = new[] { "Beehive" } },
            new LargeKind { Tier = IsletTier.All[0], Design = 2, Variant = 1, Height = 4f, Scale = 3f, Layout = Layout.Village,
                Peaceful = true, Guardian = new string[0],
                Life = new[] { L("Deer", 4, Where.Slope), L("Boar", 2, Where.Plateau) } },
            new LargeKind { Tier = IsletTier.All[0], Design = 3, Variant = 2, Height = 3.5f, Scale = 3.1f, Layout = Layout.Village,
                Peaceful = true, Guardian = new string[0],
                Life = new[] { L("Neck", 4, Where.Shore), L("Deer", 2, Where.Slope) } },
            new LargeKind { Tier = IsletTier.All[1], Variant = 2, Height = 4f, Scale = 2.8f, Dungeon = new[] { "SunkenCrypt4", "SunkenCrypt3", "SunkenCrypt2", "SunkenCrypt1" }, Layout = Layout.Village,
                Guardian = new[] { "Abomination" }, DungeonAt = new Vector2(-11.3f, -4.1f),
                Life = new[] { L("Leech", 4, Where.Water), L("Crow", 3, Where.Air) } },
            new LargeKind { Tier = IsletTier.All[1], Design = 2, Variant = 3, Height = 3.5f, Scale = 2.6f, Layout = Layout.Village,
                Guardian = new[] { "Wraith" },
                Life = new[] { L("Leech", 5, Where.Water), L("Crow", 2, Where.Air) } },
            new LargeKind { Tier = IsletTier.All[1], Design = 3, Variant = 0, Height = 4.5f, Scale = 2.9f, Layout = Layout.Village,
                Guardian = new[] { "Draugr_Elite" },
                Life = new[] { L("Leech", 3, Where.Water), L("Crow", 4, Where.Air) } },
            new LargeKind { Tier = IsletTier.All[2], Variant = 0, Height = 5f, Layout = Layout.FulingStronghold,
                Guardian = new[] { "GoblinBrute" },
                Life = new[] { L("Lox", 2, Where.Slope), L("Deathsquito", 2, Where.Air) } },
            // The abandoned farm: peaceful, the lox graze on its fields.
            new LargeKind { Tier = IsletTier.All[2], Design = 2, Variant = 1, Height = 4.5f, Scale = 3f, Layout = Layout.Village,
                Peaceful = true, Guardian = new string[0],
                Life = new[] { L("Lox", 3, Where.Slope) } },
            // The tar lake: the tar's own creatures, and the fulings who come for it.
            new LargeKind { Tier = IsletTier.All[2], Design = 3, Variant = 2, Height = 4f, Scale = 2.9f, Layout = Layout.Village,
                Guardian = new[] { "GoblinShaman", "Goblin" },
                Life = new[] { L("Deathsquito", 2, Where.Air), L("BlobTar", 2, Where.Plateau) } },
            new LargeKind { Tier = IsletTier.All[5], Variant = 0, Height = 6f, Layout = Layout.CharredFortress,
                Guardian = new[] { "Morgen_NonSleeping", "Morgen", "Charred_Melee_Dyrnwyn" },
                Life = new[] { L("Asksvin", 3, Where.Plateau), L("Volture", 2, Where.Air) } },
            // The morgen pits.
            new LargeKind { Tier = IsletTier.All[5], Design = 2, Variant = 3, Height = 5.5f, Scale = 2.8f, Layout = Layout.Village,
                Guardian = new[] { "Morgen_NonSleeping", "Morgen" },
                Life = new[] { L("Asksvin", 2, Where.Plateau), L("Volture", 1, Where.Air) } },
            // The sulfur arch and the vultures' nest.
            new LargeKind { Tier = IsletTier.All[5], Design = 3, Variant = 1, Height = 5f, Scale = 3f, Layout = Layout.Village,
                Guardian = new[] { "Charred_Mage", "Charred_Melee" },
                Life = new[] { L("Volture", 3, Where.Air), L("Asksvin", 2, Where.Slope) } },
            // The dungeons dug into the ground stand on tall islets, so their pits stay above the sea.
            new LargeKind { Tier = IsletTier.All[3], Variant = 0, Height = 16f, Scale = 4f, Dungeon = new[] { "Mistlands_DvergrTownEntrance1", "Mistlands_DvergrTownEntrance2" },
                Shaped = true, Layout = Layout.Village, Guardian = new[] { "SeekerBrute" }, DungeonAt = new Vector2(7f, -4f),
                Life = new[] { L("Hare", 3, Where.Plateau) } },
            // The giants' graveyard: peaceful, only hares among the bones.
            new LargeKind { Tier = IsletTier.All[3], Design = 2, Variant = 2, Height = 6f, Scale = 3.2f, Layout = Layout.Village,
                Peaceful = true, Guardian = new string[0],
                Life = new[] { L("Hare", 4, Where.Plateau) } },
            // The fallen watchtower, where the Seekers nest.
            new LargeKind { Tier = IsletTier.All[3], Design = 3, Variant = 1, Height = 7f, Scale = 3f, Layout = Layout.Village,
                Guardian = new[] { "Seeker" },
                Life = new[] { L("Hare", 2, Where.Plateau), L("Tick", 2, Where.Slope) } },
            new LargeKind { Tier = IsletTier.All[4], Variant = 0, Height = 16f, Scale = 4f, Dungeon = new[] { "Mistlands_DvergrTownEntrance1" },
                Shaped = true, Layout = Layout.Village, Guardian = new[] { "SeekerBrute" }, DungeonAt = new Vector2(-7f, 4f),
                Life = new[] { L("Hare", 3, Where.Plateau) } },
            // The dvergr harbour in the islet's bay: peaceful while the player is.
            new LargeKind { Tier = IsletTier.All[4], Design = 2, Variant = 2, Height = 5f, Scale = 3.2f, Layout = Layout.Village,
                Peaceful = true, Guardian = new string[0],
                Life = new[] { L("Hare", 3, Where.Plateau) } },
            // The dvergr watchtower on its hill, and the Seekers who come for it.
            new LargeKind { Tier = IsletTier.All[4], Design = 3, Variant = 3, Height = 6f, Scale = 3f, Layout = Layout.Village,
                Guardian = new[] { "Seeker" },
                Life = new[] { L("Hare", 2, Where.Plateau) } },
            new LargeKind { Tier = IsletTier.All[6], Variant = 0, Height = 15f, Scale = 4f, Dungeon = new[] { "MorkBorg", "TheDarkestHole" },
                Shaped = true, Layout = Layout.Village, Guardian = new[] { "JotunWarrior", "Fenring" }, DungeonAt = new Vector2(-6f, -6f),
                Life = new[] { L("Moose", 2, Where.Slope), L("Seal", 3, Where.Shore), L("Bjorn", 1, Where.Slope) } },
            // The frozen ships: peaceful, seals on the ice-bound wrecks.
            new LargeKind { Tier = IsletTier.All[6], Design = 2, Variant = 2, Height = 5f, Scale = 3.2f, Layout = Layout.Village,
                Peaceful = true, Guardian = new string[0],
                Life = new[] { L("Seal", 4, Where.Shore), L("Moose", 2, Where.Slope) } },
            // The ghost town: a northern hamlet whose people never left.
            new LargeKind { Tier = IsletTier.All[6], Design = 3, Variant = 0, Height = 5f, Scale = 3.4f, Layout = Layout.Village,
                Guardian = new[] { "Fenring_Cultist", "Fenring" },
                Life = new[] { L("Seal", 2, Where.Shore), L("Moose", 1, Where.Slope) } },
        };

        // What stands in the middle of a large islet: a vanilla dungeon with the people's village around it,
        // or (where the land has no dungeon) a stronghold of whoever holds it now.
        internal enum Layout { Village, FulingStronghold, CharredFortress }

        internal static LargeKind LargeKindOf(string prefabName)
        {
            foreach (LargeKind kind in LargeKinds)
                if (prefabName == kind.LandName || prefabName == kind.LandKeeperName || (kind.Design == 1 && (prefabName == kind.LocationName || prefabName == kind.KeeperName))) return kind;
            return null;
        }

        // A large islet of this tier, either kind of ground.
        internal static bool IsLargeOf(string prefabName, IsletTier tier)
        {
            return prefabName == LargePrefix + tier.Name || prefabName.StartsWith(LargePrefix + tier.Name + "_Land");
        }

        // How many of a land's islets a world gets of this design: the setting shared out between its designs.
        static int QuantityOf(LargeKind kind)
        {
            int designs = 0;
            foreach (LargeKind k in LargeKinds) if (k.Tier == kind.Tier) designs++;
            int total = Plugin.LargeIslets.Value;
            return total / designs + (kind.Design - 1 < total % designs ? 1 : 0);
        }

        // ---- prefabs: the keeper of each kind, and its three chests ----

        internal static void RegisterLargePrefabs()
        {
            foreach (LargeKind kind in LargeKinds)
            {
                // The old mesh islet's keeper (design 1 only), and the land islet's.
                if (kind.Design == 1) AddKeeper(kind.KeeperName);
                AddKeeper(kind.LandKeeperName);
                if (kind.Design == 1) // the chests are the land's, shared by its designs
                    for (int band = 0; band < 3; band++) IsletPrefabs.AddLargeChest(kind.Tier, kind.ChestName(band), band);
                foreach (Life life in kind.Life)
                    if (PrefabManager.Instance.GetPrefab(life.Prefab) == null) Plugin.Log.LogWarning("Large " + kind.Tier.Name + " islets: " + life.Prefab + " not found, left out.");
            }
        }

        static void AddKeeper(string name)
        {
            GameObject go = PrefabManager.Instance.CreateEmptyPrefab(name, true);
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<ZNetView>().m_persistent = true;
            go.AddComponent<LargeIslet>();
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // ---- the location ----

        internal static void RegisterLargeIslets()
        {
            if (Plugin.LargeIslets.Value <= 0) return;
            foreach (LargeKind kind in LargeKinds)
            {
                // The old ones stay known to the game (quantity 0), so those already built keep coming back
                // as they are; every new one is of the game's own terrain.
                if (kind.Design == 1) BuildLarge(kind, kind.LocationName, false, 0, false);
                BuildLarge(kind, kind.LandName, true, QuantityOf(kind), false);
                // Work in progress (branch large-terrain, not for release): each design's islet with only its
                // ground, rocks and dungeon, unturned, to draw its draft on (iov_goto draft_meadows_2).
                BuildLarge(kind, kind.BaseName, true, 0, true);
            }
        }

        static bool onLand; // the islet being built is of the game's terrain (its own keeper)

        // Where the islet's keeper stands (its animals, and on a dangerous islet its guardian and guards, and
        // the chest, come from it), local to the islet.
        static void SetKeeper(LargeKind kind, Vector3 at)
        {
            if (onLand) kind.LandKeeperOffset = at;
            else kind.KeeperOffset = at;
            PlaceNetworked(onLand ? kind.LandKeeperName : kind.KeeperName, at, 0f);
        }

        static void Keeper(LargeKind kind, Vector2 at)
        {
            SetKeeper(kind, new Vector3(at.x, Height(at.x, at.y) + 0.3f, at.y));
        }

        static void BuildLarge(LargeKind kind, string name, bool terrain, int quantity, bool bare)
        {
            buildingLarge = true;
            onLand = terrain;
            try { LargeIsletOf(kind, name, terrain, quantity, bare); }
            catch (System.Exception e) { Plugin.Log.LogError("Large " + kind.Tier.Name + " islet failed: " + e); }
            finally { PlateauHeight = SmallPlateau; segments = SmallSegments; buildingLarge = false; cuts.Clear(); dungeonShift = 0f; }
        }

        // terrain: the ground is the game's own terrain, raised to the islet's shape (BakedGround), instead
        // of our mesh.
        // bare: only the ground, rocks and dungeon, to draw a draft on.
        static void LargeIsletOf(LargeKind kind, string name, bool terrain, int quantity, bool bare)
        {
            tier = kind.Tier;
            locationName = name;
            SetVariant(kind.Variant);
            plateauRadius *= kind.Scale;
            shoreRadius *= kind.Scale;
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
            // The dungeon, where there is one: in the middle of the old mesh islet, where its design puts it on
            // a land one. A dug-in one shapes the ground first; another, away from the middle, gets level
            // ground the plateau's height under it, so its entrance doesn't hang over the slope.
            string source = kind.Dungeon != null ? FindLocation(kind.Dungeon) : null;
            if (kind.Dungeon != null && source == null) Plugin.Log.LogWarning("Large " + tier.Name + " islet: no vanilla dungeon " + string.Join(", ", kind.Dungeon) + "; it has none.");
            float dungeonTurn = terrain && !float.IsNaN(kind.DungeonTurn) ? kind.DungeonTurn : cliffDeg + 180f;
            Vector2 dungeonAt = terrain ? kind.DungeonAt : Vector2.zero;
            if (source != null && kind.Shaped) ReadCuts(source, dungeonAt, dungeonTurn, locationName);
            else if (source != null && dungeonAt != Vector2.zero)
                cuts.Add(new TerrainCut { X = dungeonAt.x, Z = dungeonAt.y, Y = PlateauHeight - 0.05f, Level = 12f, Smooth = 19f });
            if (terrain)
            {
                GameObject ground = new GameObject("IoV_BakedGround");
                ground.transform.SetParent(root.transform, false);
                // Painted as its own land, except the Ashlands: on real terrain that colour with the default
                // paint mask is lava, which burns (the mesh islets could clear the mask; terrain keeps its own).
                // (BakedGround clears the lava from an Ashlands islet's paint, so it shows as ash.)
                BakedGround.Bake(ground, maxRadius, shoreRadius * Mathf.Max(stretchX, stretchZ) * 1.1f, tier.Biome, Height);
            }
            else BuildGround();
            PlaceRocks();
            DungeonInterior interior = source != null ? AddDungeon(source, dungeonAt, dungeonTurn, locationName, kind.Shaped) : null;
            // The old mesh islet keeps its old layout (it's built anew each time from here, over what players
            // left on it); a land one has its design's draft, or the old layout while it has none.
            if (!bare && !(terrain && BuildDraft(kind))) Inhabit(kind, interior);

            if (terrain)
            {
                // Nothing of ours may reshape the ground on top of the islet's own (on the mesh islets such
                // things only ever touched the sea floor under them).
                // (A draft's structures keep their own levelling, and its worn paths are paint: those stay.)
                int stray = 0;
                foreach (TerrainModifier m in root.GetComponentsInChildren<TerrainModifier>(true))
                    if (!DraftGround(m.transform)) { Object.DestroyImmediate(m.gameObject); stray++; }
                foreach (TerrainOp op in root.GetComponentsInChildren<TerrainOp>(true)) { Object.DestroyImmediate(op.gameObject); stray++; }
                if (stray > 0) Plugin.Log.LogWarning(locationName + ": removed " + stray + " terrain modifiers from the islet's parts");
            }
            Finish(kind, quantity, interior, bare);
        }

        // Everything that stands on a large islet: the village or stronghold, the keeper, nature, docks,
        // the wreck, hints and the outside guard.
        static void Inhabit(LargeKind kind, DungeonInterior interior)
        {
            Vector2 keeper;
            if (kind.Layout == Layout.Village)
            {
                // A ring of what the people left around it, clear of the dungeon's entrance, each facing the
                // middle and kept apart by its size: the kind's ruin (its guards and chest with it), two houses,
                // a well or graves, their fire, and a watchtower to see the islet by.
                RingItem[] village = VillageOf(kind);
                float biggest = 0f;
                foreach (RingItem item in village) biggest = Mathf.Max(biggest, item.Size);
                float clear = Mathf.Max(interior != null ? interior.Footprint : 4f, CutsReach());
                float ring = Mathf.Clamp(clear + biggest + 2f, kind.Plateau * 0.5f, kind.Plateau - biggest - 1f);
                if (clear + biggest + 2f > kind.Plateau - biggest - 1f) Plugin.Log.LogWarning(locationName + ": the plateau is small for its dungeon; buildings stand close to it.");
                float[] at = RingOf(ring, interior != null ? interior.DoorDeg : float.NaN, village);
                if (tier.DvergrOutpost) PlaceDvergrWard(); // the islet is theirs
                keeper = Polar(at[at.Length - 1], ring - 4.5f); // by the watchtower, on the inner side
            }
            else
            {
                if (kind.Layout == Layout.FulingStronghold) FulingStronghold();
                else CharredFortress();
                keeper = Polar(cliffDeg + 200f, kind.Plateau - 3f); // between the walls and the plateau's edge
            }
            RidgeDesign();
            RidgeGuard();
            SetKeeper(kind, new Vector3(keeper.x, PlateauHeight, keeper.y));
            if (kind.Extra != null)
                foreach (string extra in kind.Extra)
                    PlaceNetworked(new[] { extra }, Polar(cliffDeg + 300f, kind.Plateau + 3f), cliffDeg, 0f);

            LargeNature(kind);
            PlaceDock(cliffDeg + 229f);
            PlaceDock(cliffDeg + 120f);
            PlaceWreck(cliffDeg + 194f);
            PlaceHints();
            PlaceOutsideGuard();
        }

        static void Finish(LargeKind kind, int quantity, DungeonInterior interior, bool empty)
        {
            Location location = root.AddComponent<Location>();
            location.m_applyRandomDamage = tier.Ruined;
            location.m_exteriorRadius = 60f;
            if (interior != null) interior.CopyTo(location);
            location.m_noBuild = false; // players may settle here

            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.BiomeArea = Heightmap.BiomeArea.Median;
            config.Quantity = quantity;
            config.Priotized = true;
            config.SnapToWater = true;
            config.RandomRotation = !empty; // an empty one for designing on lies square to the world
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

        // What stands round a large islet's dungeon, by its kind; the landmark (a tower or a spire) last.
        static RingItem[] VillageOf(LargeKind kind)
        {
            switch (tier.Style)
            {
                case RuinStyle.StiltHut:
                    return new[] { Item(StiltHut, 6f), Item(() => RuinedHouse(true), 5.5f), Item(Graveyard, 3.6f), Item(() => RuinedHouse(true), 5.5f), Item(Campfire, 3f), Item(WatchTower, 3.6f) };
                case RuinStyle.DvergrRuin: // the Seekers' Mistlands: old dvergr ruins, a nest, a spire of black marble
                    return new[] { Item(DvergrRuin, 7f), Item(NestSite, 5.5f), Item(DvergrOutpost, 6f), Item(MarbleSpire, 2.6f) };
                case RuinStyle.DvergrCamp: // the dvergr who live here, digging round the mine
                    return new[] { Item(DvergrCamp, 7f), Item(DvergrDigSite, 6f), Item(MarbleSpire, 2.6f) };
                case RuinStyle.FrostKeep: // the frost keep, a house left to the snow, graves, their fire and the watchtower
                    return new[] { Item(FrostKeep, 6.5f), Item(() => RuinedHouse(false), 5.5f), Item(Graveyard, 3.6f), Item(Campfire, 3f), Item(WatchTower, 3.6f) };
                default:
                    return new[] { Item(StoneHall, 7f), Item(() => RuinedHouse(false), 5.5f), Item(Well, 2.4f), Item(() => RuinedHouse(false), 5.5f), Item(Campfire, 3f), Item(WatchTower, 3.6f) };
            }
        }

        // One design for RingOf, with the radius it takes up.
        class RingItem
        {
            internal System.Action Design;
            internal float Size;
        }

        static RingItem Item(System.Action design, float size) { return new RingItem { Design = design, Size = size }; }

        // The designs round the plateau at radius ring, in order, from just past the cliff side (where the
        // ridge structure stands) most of the way round, leaving the way in to the dungeon open (doorDeg,
        // NaN if none): each gets an arc for its size and a gap of at least Gap metres to the next, the
        // room left over shared out evenly. Returns where each went.
        static float[] RingOf(float ring, float doorDeg, params RingItem[] items)
        {
            const float Gap = 3f, DoorClear = 34f; // degrees kept open either side of the way in
            float start = cliffDeg + 28f, end = cliffDeg + 332f;
            float doorFrom = float.NaN, doorTo = float.NaN;
            if (!float.IsNaN(doorDeg))
            {
                doorFrom = start + Mathf.Repeat(doorDeg - DoorClear - start, 360f);
                doorTo = doorFrom + DoorClear * 2f;
                if (doorFrom > end) doorFrom = doorTo = float.NaN; // it opens towards the cliff side, already clear
            }
            float blocked = float.IsNaN(doorFrom) ? 0f : Mathf.Min(doorTo, end) - doorFrom;
            float room = (end - start - blocked) * Mathf.Deg2Rad * ring, need = 0f;
            foreach (RingItem item in items) need += item.Size * 2f + Gap;
            float spare = Mathf.Max(0f, room - need) / items.Length;
            if (need > room) Plugin.Log.LogWarning(locationName + ": its buildings need " + need.ToString("0") + " m of a " + room.ToString("0") + " m ring; some stand close.");
            float[] at = new float[items.Length];
            float deg = start;
            for (int i = 0; i < items.Length; i++)
            {
                float half = (items[i].Size + (Gap + spare) * 0.5f) / ring * Mathf.Rad2Deg;
                if (!float.IsNaN(doorFrom) && deg + half * 2f > doorFrom && deg < doorTo) deg = doorTo; // past the way in
                deg += half;
                at[i] = deg;
                Village(deg, ring, items[i].Design);
                deg += half;
            }
            return at;
        }

        // A design built at deg degrees and radius r on the plateau, its front (-z) towards the middle. The
        // designs are laid out around (0, 0), and the placing works in the location's own space, so it's
        // built there first and then moved and turned as a whole (like Turned); the plateau is flat, so the
        // ground is the same where it ends up.
        static void Village(float deg, float r, System.Action design)
        {
            Vector2 at = Polar(deg, r);
            GameObject group = new GameObject("IoV_Village");
            group.transform.SetParent(parent, false);
            Transform outer = parent;
            parent = group.transform;
            bool cutBefore = ignoreCuts;
            ignoreCuts = true; // it's laid out at the middle, where a dungeon may have dug the ground: it stands on the plateau
            try { design(); }
            finally { ignoreCuts = cutBefore; parent = outer; }
            group.transform.localPosition = new Vector3(at.x, 0f, at.y);
            group.transform.localRotation = Quaternion.Euler(0f, 90f - deg, 0f);
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
            string name = Utils.GetPrefabName(gameObject);
            kind = IsletBuilder.LargeKindOf(name);
            if (kind == null) return;
            land = name == kind.LandKeeperName;
            InvokeRepeating("Tick", Random.Range(3f, 6f), 5f);
        }

        bool land; // the keeper of a land islet (its own place on it) or of an old mesh one

        // The islet's middle on its plateau (the keeper stands where its design put it, this far from it).
        Vector3 Centre { get { return transform.position - transform.rotation * (land ? kind.LandKeeperOffset : kind.KeeperOffset) + Vector3.up * kind.Height; } }

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
            string guardian = null;
            foreach (string g in kind.Guardian) if (guardian == null && ZNetScene.instance.GetPrefab(g) != null) guardian = g;
            if (guardian != null && Point(IsletBuilder.Where.Plateau, out p)) Spawn(guardian, p, Level(band, true));
            string[] guards = kind.Tier.Guards;
            int extra = kind.Peaceful ? 0 : kind.Tier.Band >= 3 ? 2 : 4; // the strong lands' islets have plenty to fight already
            for (int i = 0; i < extra; i++)
                if (Point(IsletBuilder.Where.Plateau, out p)) Spawn(guards[i % guards.Length], p, Level(band, false));
            GameObject chest = ZNetScene.instance.GetPrefab(kind.ChestName(band));
            Vector3 at = transform.position + transform.rotation * new Vector3(1.8f, 0.2f, 1.2f);
            if (chest != null) Instantiate(chest, Ground(at) - Vector3.up * 0.35f, transform.rotation); // on the ground: one held up has no support and breaks
            Plugin.Debug("Large " + kind.Tier.Name + " islet at " + transform.position.ToString("F0") + ": band " + band + ", guardian " + guardian + " and guards out, chest " + (chest != null));
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

        // The islet's own ground below a point: not a tree's crown, a roof or a wall that happens to be over
        // it (a chest set down on those hangs in the air and breaks).
        static Vector3 Ground(Vector3 q)
        {
            int mask = LayerMask.GetMask("static_solid", "terrain");
            Vector3 from = new Vector3(q.x, ZoneSystem.instance.m_waterLevel + 60f, q.z);
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 90f, mask, QueryTriggerInteraction.Ignore))
                if (hit.collider != null && (hit.collider.name == "IoV_Ground" || hit.collider.GetComponentInParent<Heightmap>() != null))
                    return hit.point + Vector3.up * 0.3f; // our mesh, or a large islet of real terrain
            return new Vector3(q.x, ZoneSystem.instance.m_waterLevel - 5f, q.z);
        }
    }
}
