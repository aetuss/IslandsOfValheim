using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The networked parts of the islets: one-time creature spawners, treasure chests and plants.
    // They need their own registered prefabs, because a zone that is loaded again rebuilds its
    // objects from ZNetScene by name, and a changed vanilla prefab would come back as vanilla.
    static class IsletPrefabs
    {
        internal static void Register()
        {
            HashSet<string> spawners = new HashSet<string>();
            foreach (IsletTier tier in IsletTier.All)
            {
                foreach (string creature in tier.Guards)
                    if (spawners.Add(creature)) AddSpawner(creature);
                AddChest(tier);
            }
            foreach (string plant in IsletTier.AllPlants()) AddPlant(plant);
        }

        // One spawner prefab per creature; its levels come from the (first) tier that uses that creature.
        static void AddSpawner(string creature)
        {
            GameObject creaturePrefab = PrefabManager.Instance.GetPrefab(creature);
            if (creaturePrefab == null)
            {
                Plugin.Log.LogWarning("Creature prefab " + creature + " not found, its islet guards are left out.");
                return;
            }
            IsletTier tier = TierOf(creature);
            string name = IsletTier.SpawnerName(creature);

            // CreateEmptyPrefab makes a cube with a ZNetView; the spawner must be invisible.
            GameObject go = PrefabManager.Instance.CreateEmptyPrefab(name, true);
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<ZNetView>().m_persistent = true;

            CreatureSpawner spawner = go.AddComponent<CreatureSpawner>();
            spawner.m_creaturePrefab = creaturePrefab;
            spawner.m_minLevel = tier.MinLevel;
            spawner.m_maxLevel = tier.MaxLevel;
            spawner.m_levelupChance = tier.LevelUpChance;
            spawner.m_respawnTimeMinuts = 0f; // spawn once, never again
            spawner.m_triggerDistance = 40f;
            spawner.m_spawnAtDay = true;
            spawner.m_spawnAtNight = true;
            spawner.m_requireSpawnArea = false;
            spawner.m_setPatrolSpawnPoint = true;
            spawner.m_wakeUpAnimation = false;

            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
            Plugin.Debug("Created spawner " + name);
        }

        static IsletTier TierOf(string creature)
        {
            foreach (IsletTier tier in IsletTier.All)
                if (System.Array.IndexOf(tier.Guards, creature) >= 0) return tier;
            return IsletTier.All[0];
        }

        static void AddChest(IsletTier tier)
        {
            GameObject go = null;
            foreach (string baseName in tier.ChestBases)
            {
                if (PrefabManager.Instance.GetPrefab(baseName) == null) continue;
                go = PrefabManager.Instance.CreateClonedPrefab(tier.ChestName, baseName);
                Plugin.Debug("Chest " + tier.ChestName + " cloned from " + baseName);
                break;
            }
            if (go == null)
            {
                Plugin.Log.LogWarning("No vanilla chest found to clone, " + tier.Name + " islets will have no treasure.");
                return;
            }

            Container container = go.GetComponent<Container>();
            container.m_defaultItems = LootTable(tier);
            // A location chest must not be picked up or deconstructed for its wood.
            Piece piece = go.GetComponent<Piece>();
            if (piece != null) piece.m_canBeRemoved = false;

            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        static DropTable LootTable(IsletTier tier)
        {
            DropTable table = new DropTable();
            table.m_dropMin = tier.DropMin;
            table.m_dropMax = tier.DropMax;
            table.m_dropChance = 1f;
            table.m_oneOfEach = true;
            table.m_drops = new List<DropTable.DropData>();
            foreach (Loot loot in tier.Loot)
            {
                GameObject prefab = PrefabManager.Instance.GetPrefab(loot.Item);
                if (prefab == null)
                {
                    Plugin.Log.LogWarning("Loot item " + loot.Item + " not found, skipped.");
                    continue;
                }
                DropTable.DropData drop = new DropTable.DropData();
                drop.m_item = prefab;
                drop.m_stackMin = loot.Min;
                drop.m_stackMax = loot.Max;
                drop.m_weight = loot.Weight;
                table.m_drops.Add(drop);
            }
            return table;
        }

        // Vanilla plants have StaticPhysics, which drops them onto the terrain: out here that's the sea
        // floor. The islets use copies without it ("IoV_" + name).
        internal static string PlantName(string vanilla)
        {
            return "IoV_" + vanilla;
        }

        static void AddPlant(string name)
        {
            if (PrefabManager.Instance.GetPrefab(name) == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(PlantName(name), name);
            foreach (StaticPhysics physics in go.GetComponentsInChildren<StaticPhysics>(true)) Object.DestroyImmediate(physics);
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }
    }
}
