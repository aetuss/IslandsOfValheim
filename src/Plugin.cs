using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

// Thunderstore asks mods made with AI to say so here and in the README.
[assembly: System.Reflection.AssemblyMetadata("AI_Assisted_Creation", "This assembly was fully created with the assistance of Generative AI: the code was written by Claude (Anthropic) through Claude Code.")]
[assembly: System.Reflection.AssemblyMetadata("AI_Model_Vendor", "Anthropic")]

namespace IslandsOfValheim
{
    [BepInPlugin(Guid, ModName, Version)]
    [BepInDependency("com.jotunn.jotunn")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "IslandsOfValheim";
        public const string ModName = "IslandsOfValheim";
        public const string Version = "1.10.68";

        internal static ConfigEntry<string> GroundStyle;
        internal static ConfigEntry<int> SeaTraderIslets;
        internal static ConfigEntry<bool> PlaceInExistingWorlds;
        internal static ConfigEntry<int> LargeIslets;
        internal static ConfigEntry<bool> DebugLog;

        internal const string GroundTerrain = "Terrain";
        internal const string GroundRock = "Rock";

        internal static BepInEx.Logging.ManualLogSource Log;
        internal static Plugin Instance;

        void Awake()
        {
            Log = Logger;
            Instance = this;

            IsletTier.BindConfig(Config);
            GroundStyle = Config.Bind("1 - World generation", "GroundStyle", GroundTerrain,
                new ConfigDescription("How the islet ground looks: Terrain (like the game's own land: grass, sand, cliff rock) or Rock (bare stone). Needs a restart.",
                    new AcceptableValueList<string>(GroundTerrain, GroundRock)));
            SeaTraderIslets = Config.Bind("1 - World generation", "SeaTraderIslets", 6,
                new ConfigDescription("How many possible spots the game picks for the sea trader's harbour islet, in the Swamp and Plains waters. Like Haldor, only the first one somebody comes near is built; the others vanish. 0: no sea trader. Only used when locations are generated (new world or genloc).",
                    new AcceptableValueRange<int>(0, 30)));
            LargeIslets = Config.Bind("1 - World generation", "LargeIsletsPerKind", 3,
                new ConfigDescription("How many large islets of each kind the game tries to place, in the same ring as that kind's small islets. 0 turns them off. Only used when locations are generated (a new world, genloc, or the existing-world pass). Needs a restart.",
                    new AcceptableValueRange<int>(0, 10)));
            PlaceInExistingWorlds = Config.Bind("1 - World generation", "PlaceInExistingWorlds", true,
                "Once per world, the server (or the host) adds the islets an existing world is missing (one made before the mod, or before a kind of islet came out): " +
                "where nobody has sailed yet, they appear when somebody gets there; if there's no room left there, a few are built in sailed ocean, away from anything players built. " +
                "Sævar's harbour and the islets of his tale are added too. The console command iov_placeislets (devcommands) runs it again.");
            DebugLog = Config.Bind("2 - Debug", "DebugLog", false,
                "Writes how the islands were built and where they are to the BepInEx log. Useful when reporting a problem.");

            Texts.LoadOwn();
            gameObject.AddComponent<RestlessSea>();
#if IOV_DEV
            gameObject.AddComponent<ValhallaCall>(); // Odin's call after the tale (Valhalla arc, work in progress)
#endif
            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabs;
            ZoneManager.OnVanillaLocationsAvailable += OnVanillaLocations;

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
        }

        void Update()
        {
            ExistingWorlds.Tick();
        }

        // Spawners and chests are networked objects, so they need prefabs of their own in ZNetScene.
        void OnVanillaPrefabs()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabs;
            SeaChart.Register(); // before the chests, whose loot includes it
            SeafarerGear.Register();
            Story.Register(); // before the trader, who sells some of its goods
            SeaTrader.Register(); // after the goods he sells
            IsletPrefabs.Register();
            IsletBeacons.Register();
            IsletFishing.Register();
            IsletBuilder.RegisterLargePrefabs(); // after the beacons and chests
            SpiritHuman.Register(); // the Brim Folk's spirits in human form (Valhalla arc, work in progress)
            EinherjarFighter.Register(); // the einherjar of the ring's duels (Valhalla arc, work in progress)
        }

        void OnVanillaLocations()
        {
            ZoneManager.OnVanillaLocationsAvailable -= OnVanillaLocations;
            foreach (IsletTier tier in IsletTier.All)
                for (int variant = 0; variant < IsletBuilder.Variants * 2; variant++) IsletBuilder.Register(tier, variant);
#if IOV_DEV
            IsletBuilder.RegisterSmallTests(); // design work: each small design on its own (iov_goto small_meadows_2)
#endif
            if (SeaTraderIslets.Value > 0)
            {
                IsletBuilder.RegisterHarbour(SeaTraderIslets.Value);
                IsletBuilder.RegisterStoryIslets(); // the tale is his to tell
            }
            IsletBuilder.RegisterLargeIslets();
#if IOV_DEV
            TerrainIsletTest.Register(); // research prototype (branch iov-terrain-test)
            IsletBuilder.RegisterValhallaBase(); // Valhalla's bare rock, to lay out its draft on (iov_goto draft_valhalla)
#endif
        }

        internal static void Debug(string message)
        {
            if (DebugLog.Value) Log.LogInfo(message);
        }
    }
}
