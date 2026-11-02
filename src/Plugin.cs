using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace IslandsOfValheim
{
    [BepInPlugin(Guid, ModName, Version)]
    [BepInDependency("com.jotunn.jotunn")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "IslandsOfValheim";
        public const string ModName = "IslandsOfValheim";
        public const string Version = "0.4.1";

        internal static ConfigEntry<int> IsletCount;
        internal static ConfigEntry<string> GroundStyle;
        internal static ConfigEntry<bool> DebugLog;

        internal const string GroundTerrain = "Terrain";
        internal const string GroundRock = "Rock";

        internal static BepInEx.Logging.ManualLogSource Log;

        void Awake()
        {
            Log = Logger;

            IsletCount = Config.Bind("1 - World generation", "IsletCount", 30,
                new ConfigDescription("How many rocky islets the game tries to place in the ocean of a new world. " +
                    "Only used when a world's locations are generated (a new world, or the genloc command). Needs a restart.",
                    new AcceptableValueRange<int>(0, 200)));
            GroundStyle = Config.Bind("1 - World generation", "GroundStyle", GroundTerrain,
                new ConfigDescription("How the islet ground looks: Terrain (like the game's own land: grass, sand, cliff rock) or Rock (bare stone). Needs a restart.",
                    new AcceptableValueList<string>(GroundTerrain, GroundRock)));
            DebugLog = Config.Bind("2 - Debug", "DebugLog", true, // TODO before 1.0: default false
                "Writes how the islands were built and where they are to the BepInEx log. Useful when reporting a problem.");

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabs;
            ZoneManager.OnVanillaLocationsAvailable += OnVanillaLocations;

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
        }

        // Spawners and chests are networked objects, so they need prefabs of their own in ZNetScene.
        void OnVanillaPrefabs()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabs;
            IsletPrefabs.Register();
        }

        void OnVanillaLocations()
        {
            ZoneManager.OnVanillaLocationsAvailable -= OnVanillaLocations;
            IsletBuilder.Register();
        }

        internal static void Debug(string message)
        {
            if (DebugLog.Value) Log.LogInfo(message);
        }
    }
}
