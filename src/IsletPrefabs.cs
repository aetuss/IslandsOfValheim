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
                List<string> creatures = new List<string>(tier.Guards);
                if (tier.Raiders != null) creatures.AddRange(tier.Raiders);
                foreach (string creature in creatures)
                    if (spawners.Add(creature))
                    {
                        IsletTier first = TierOf(creature);
                        AddSpawner(creature, IsletTier.SpawnerName(creature), first.MinLevel, first.MaxLevel, first.LevelUpChance, 0f, true, 40f);
                    }
                AddChest(tier);
            }
            // Gulls over every islet in daylight, a sign of land from a boat; they come back after a while.
            AddSpawner("Seagal", GullSpawner, 1, 1, 0f, 10f, false, 150f);
            HashSet<string> copied = new HashSet<string>();
            foreach (string plant in IsletTier.AllPlants())
                if (copied.Add(plant)) AddPlant(plant, false);
            foreach (string piece in DvergrOutposts.Pieces)
                if (copied.Add(piece)) AddPlant(piece, true);
            DvergrOutposts.Register();
        }

        internal const string GullSpawner = "IoV_Spawner_Gull";

        // respawnMinutes 0: spawn once, never again (the guards).
        internal static void AddSpawner(string creature, string name, int minLevel, int maxLevel, float levelUpChance, float respawnMinutes, bool atNight, float triggerDistance)
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
            // Inside a dvergr ward a chest that checks for wards couldn't be opened at all.
            container.m_checkGuardStone = false;
            // A location chest must not be picked up or deconstructed for its wood.
            Piece piece = go.GetComponent<Piece>();
            if (piece != null) piece.m_canBeRemoved = false;

            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // A large islet's chest, for the inner (0), middle (1) or outer (2) third of its ring: a hoard, with
        // one more thing and a little bigger stacks the further out it is, and in the outer third the tier's
        // rarest things come up a little more often.
        internal static void AddLargeChest(IsletTier tier, string name, int band)
        {
            AddChest(tier, name, true);
            GameObject go = PrefabManager.Instance.GetPrefab(name);
            Container container = go != null ? go.GetComponent<Container>() : null;
            if (container == null) return;
            DropTable table = container.m_defaultItems;
            table.m_dropMin += band;
            table.m_dropMax += band;
            float stacks = 1f + 0.15f * band;
            float rarest = float.MaxValue;
            foreach (DropTable.DropData drop in table.m_drops) rarest = Mathf.Min(rarest, drop.m_weight);
            for (int i = 0; i < table.m_drops.Count; i++)
            {
                DropTable.DropData drop = table.m_drops[i];
                drop.m_stackMin = Mathf.Max(1, Mathf.RoundToInt(drop.m_stackMin * stacks));
                drop.m_stackMax = Mathf.Max(drop.m_stackMin, Mathf.RoundToInt(drop.m_stackMax * stacks));
                if (band == 2 && drop.m_weight <= rarest + 0.01f) drop.m_weight *= 1.5f;
                table.m_drops[i] = drop;
            }
            container.m_defaultItems = table;
        }

        // Every islet chest's stacks, small and large (1.9.21: a little more than the cut of 1.9.18).
        const float LootScale = 1.3f;

        static DropTable LootTable(IsletTier tier, bool hoard)
        {
            DropTable table = new DropTable();
            // About what the game's own chests of that land give; the hoard one thing more, in bigger stacks.
            table.m_dropMin = tier.DropMin + (hoard ? 1 : 0);
            table.m_dropMax = tier.DropMax + (hoard ? 1 : 0);
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
                float scale = (hoard ? 1.5f : 1f) * LootScale;
                drop.m_stackMin = Mathf.Max(1, Mathf.RoundToInt(loot.Min * scale));
                drop.m_stackMax = Mathf.Max(drop.m_stackMin, Mathf.RoundToInt(loot.Max * scale));
                drop.m_weight = loot.Weight;
                table.m_drops.Add(drop);
            }
            // Sometimes a sea vegvisir shard, pointing to the next islet.
            if (SeaChart.Prefab != null)
            {
                DropTable.DropData shard = new DropTable.DropData();
                shard.m_item = SeaChart.Prefab;
                shard.m_stackMin = 1;
                shard.m_stackMax = hoard ? 2 : 1;
                shard.m_weight = 1f;
                table.m_drops.Add(shard);
            }
            return table;
        }

        // Vanilla plants, nests and runestones have StaticPhysics, which drops them onto the terrain: out
        // here that's the sea floor. The islets use copies without it ("IoV_" + name).
        internal static string PlantName(string vanilla)
        {
            return "IoV_" + vanilla;
        }

        // owned: a dvergr outpost's thing (DvergrOutposts.Pieces).
        static void AddPlant(string name, bool owned)
        {
            if (PrefabManager.Instance.GetPrefab(name) == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(PlantName(name), name);
            foreach (StaticPhysics physics in go.GetComponentsInChildren<StaticPhysics>(true)) Object.DestroyImmediate(physics);
            // A nest only spawns on terrain if it asks for "ground"; the islet's ground counts as blocked.
            foreach (SpawnArea area in go.GetComponentsInChildren<SpawnArea>(true)) area.m_onGroundOnly = false;
            if (owned) DvergrOutposts.MarkOwned(go);
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }
    }
}
