using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The networked parts of an islet: one-time creature spawners and the treasure chest.
    // They need their own registered prefabs, because a zone that is loaded again rebuilds its
    // objects from ZNetScene by name, and a changed vanilla prefab would come back as vanilla.
    static class IsletPrefabs
    {
        internal const string GreydwarfSpawner = "IoV_Spawner_Greydwarf";
        internal const string ShamanSpawner = "IoV_Spawner_GreydwarfShaman";
        internal const string Chest = "IoV_TreasureChest_Meadows";

        static readonly string[] chestBases = { "TreasureChest_meadows", "TreasureChest_blackforest", "piece_chest_wood" };

        internal static void Register()
        {
            AddSpawner(GreydwarfSpawner, "Greydwarf", 1, 2, 15f);
            AddSpawner(ShamanSpawner, "Greydwarf_Shaman", 1, 2, 10f);
            AddChest();
        }

        static void AddSpawner(string name, string creature, int minLevel, int maxLevel, float levelUpChance)
        {
            GameObject creaturePrefab = PrefabManager.Instance.GetPrefab(creature);
            if (creaturePrefab == null)
            {
                Plugin.Log.LogWarning("Creature prefab " + creature + " not found, " + name + " not created.");
                return;
            }

            // CreateEmptyPrefab makes a cube with a ZNetView; the spawner must be invisible.
            GameObject go = PrefabManager.Instance.CreateEmptyPrefab(name, true);
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<ZNetView>().m_persistent = true;

            CreatureSpawner spawner = go.AddComponent<CreatureSpawner>();
            spawner.m_creaturePrefab = creaturePrefab;
            spawner.m_minLevel = minLevel;
            spawner.m_maxLevel = maxLevel;
            spawner.m_levelupChance = levelUpChance;
            spawner.m_respawnTimeMinuts = 0f; // spawn once, never again
            spawner.m_triggerDistance = 40f;
            spawner.m_spawnAtDay = true;
            spawner.m_spawnAtNight = true;
            spawner.m_requireSpawnArea = false;
            spawner.m_setPatrolSpawnPoint = true;
            spawner.m_wakeUpAnimation = false;

            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
            Plugin.Debug("Created spawner " + name + " for " + creature);
        }

        static void AddChest()
        {
            GameObject go = null;
            foreach (string baseName in chestBases)
            {
                if (PrefabManager.Instance.GetPrefab(baseName) == null) continue;
                go = PrefabManager.Instance.CreateClonedPrefab(Chest, baseName);
                Plugin.Debug("Chest " + Chest + " cloned from " + baseName);
                break;
            }
            if (go == null)
            {
                Plugin.Log.LogWarning("No vanilla chest found to clone, islets will have no treasure.");
                return;
            }

            Container container = go.GetComponent<Container>();
            container.m_defaultItems = MeadowsLoot();
            // A location chest must not be picked up or deconstructed for its wood.
            Piece piece = go.GetComponent<Piece>();
            if (piece != null) piece.m_canBeRemoved = false;

            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // Meadows / Black Forest tier: a bit better than a Black Forest burial chest, since it takes a boat and a fight.
        static DropTable MeadowsLoot()
        {
            DropTable table = new DropTable();
            table.m_dropMin = 3;
            table.m_dropMax = 5;
            table.m_dropChance = 1f;
            table.m_oneOfEach = true;
            table.m_drops = new List<DropTable.DropData>();
            AddDrop(table, "Coins", 20, 45, 3f);
            AddDrop(table, "Amber", 1, 3, 2f);
            AddDrop(table, "AmberPearl", 1, 2, 1.5f);
            AddDrop(table, "Ruby", 1, 2, 0.8f);
            AddDrop(table, "ArrowFire", 10, 20, 1f);
            AddDrop(table, "Resin", 5, 10, 1f);
            AddDrop(table, "Feathers", 4, 8, 1f);
            return table;
        }

        static void AddDrop(DropTable table, string item, int min, int max, float weight)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(item);
            if (prefab == null)
            {
                Plugin.Log.LogWarning("Loot item " + item + " not found, skipped.");
                return;
            }
            DropTable.DropData drop = new DropTable.DropData();
            drop.m_item = prefab;
            drop.m_stackMin = min;
            drop.m_stackMax = max;
            drop.m_weight = weight;
            table.m_drops.Add(drop);
        }
    }
}
