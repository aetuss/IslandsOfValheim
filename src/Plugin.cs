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
        public const string Version = "0.8.0";

        internal static ConfigEntry<string> GroundStyle;
        internal static ConfigEntry<bool> DebugLog;

        internal const string GroundTerrain = "Terrain";
        internal const string GroundRock = "Rock";

        internal static BepInEx.Logging.ManualLogSource Log;

        void Awake()
        {
            Log = Logger;

            IsletTier.BindConfig(Config);
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
            foreach (IsletTier tier in IsletTier.All)
                for (int variant = 0; variant < IsletBuilder.Variants; variant++) IsletBuilder.Register(tier, variant);
        }

        internal static void Debug(string message)
        {
            if (DebugLog.Value) Log.LogInfo(message);
        }
    }
}
