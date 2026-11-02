using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The networked parts of the islets: creature spawners (one-time guards, returning gulls), treasure
    // chests and plants.
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
                    if (spawners.Add(creature))
                    {
                        IsletTier first = TierOf(creature);
                        AddSpawner(creature, IsletTier.SpawnerName(creature), first.MinLevel, first.MaxLevel, first.LevelUpChance, 0f, true, 40f);
                    }
                AddChest(tier);
            }
            // Gulls over every islet in daylight, a sign of land from a boat; they come back after a while.
            AddSpawner("Seagal", GullSpawner, 1, 1, 0f, 10f, false, 150f);
            foreach (string plant in IsletTier.AllPlants()) AddPlant(plant);
        }

        internal const string GullSpawner = "IoV_Spawner_Gull";

        // respawnMinutes 0: spawn once, never again (the guards).
        static void AddSpawner(string creature, string name, int minLevel, int maxLevel, float levelUpChance, float respawnMinutes, bool atNight, float triggerDistance)
        {
            GameObject creaturePrefab = PrefabManager.Instance.GetPrefab(creature);
            if (creaturePrefab == null)
            {
                Plugin.Log.LogWarning("Creature prefab " + creature + " not found, " + name + " is left out.");
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
            spawner.m_respawnTimeMinuts = respawnMinutes;
            spawner.m_triggerDistance = triggerDistance;
            spawner.m_spawnAtDay = true;
            spawner.m_spawnAtNight = atNight;
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
            AddChest(tier, tier.ChestName, false);
            AddChest(tier, tier.HoardName, true);
        }

        // hoard: the rare hidden chest, with more of everything.
        static void AddChest(IsletTier tier, string name, bool hoard)
        {
            GameObject go = null;
            foreach (string baseName in tier.ChestBases)
            {
                if (PrefabManager.Instance.GetPrefab(baseName) == null) continue;
                go = PrefabManager.Instance.CreateClonedPrefab(name, baseName);
                Plugin.Debug("Chest " + name + " cloned from " + baseName);
                break;
            }
            if (go == null)
            {
                Plugin.Log.LogWarning("No vanilla chest found to clone, " + tier.Name + " islets will have no treasure.");
                return;
            }

            Container container = go.GetComponent<Container>();
            container.m_defaultItems = LootTable(tier, hoard);
            // A location chest must not be picked up or deconstructed for its wood.
            Piece piece = go.GetComponent<Piece>();
            if (piece != null) piece.m_canBeRemoved = false;

            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        static DropTable LootTable(IsletTier tier, bool hoard)
        {
            DropTable table = new DropTable();
            table.m_dropMin = tier.DropMin + (hoard ? 2 : 0);
            table.m_dropMax = tier.DropMax + (hoard ? 3 : 0);
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
                int scale = hoard ? 2 : 1;
                drop.m_stackMin = loot.Min * scale;
                drop.m_stackMax = loot.Max * scale;
                drop.m_weight = loot.Weight;
                table.m_drops.Add(drop);
            }
            return table;
        }

        // Vanilla plants, nests and runestones have StaticPhysics, which drops them onto the terrain: out
        // here that's the sea floor. The islets use copies without it ("IoV_" + name).
        internal static string PlantName(string vanilla)
        {
            return "IoV_" + vanilla;
        }

        static void AddPlant(string name)
        {
            if (PrefabManager.Instance.GetPrefab(name) == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(PlantName(name), name);
            foreach (StaticPhysics physics in go.GetComponentsInChildren<StaticPhysics>(true)) Object.DestroyImmediate(physics);
            // A nest only spawns on terrain if it asks for "ground"; the islet's ground counts as blocked.
            foreach (SpawnArea area in go.GetComponentsInChildren<SpawnArea>(true)) area.m_onGroundOnly = false;
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }
    }
}
