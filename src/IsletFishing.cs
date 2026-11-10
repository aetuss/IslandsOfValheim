using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Fishing grounds: about half the islets have more fish in the shallow water around them than the open
    // sea. The species are the tier's vanilla fish (each wants its own vanilla bait), with more stars
    // further out. Fish caught are gone for a while: the ground puts one back every RespawnSeconds, up to
    // MaxFish, and only while a player is near. Now and then the hook brings up a sodden pouch instead.
    static partial class IsletBuilder
    {
        static void PlaceFishingGround()
        {
            Optional(0.5f, () => PlaceNetworked(IsletFishing.PrefabName(tier), new Vector3(0f, PlateauHeight, 0f), 0f));
        }
    }

    static class IsletFishing
    {
        internal const string GroundKey = "IoV_FishGround"; // on a fish: 1 + the index of the tier whose ground it's from
        internal const string LastFishKey = "IoV_LastFish"; // on a ground: when it last put a fish back (or was full)
        internal const int MaxFish = 6;
        internal const float RespawnSeconds = 180f;
        internal const float ActiveRange = 70f;            // only while a player is this close
        internal const float Radius = 55f;                 // how far from the islet its fish count as its own
        const float PouchChance = 0.05f;
        const float ShardChance = 0.15f;                   // of the pouches

        internal static string PrefabName(IsletTier tier) { return "IoV_FishingGround_" + tier.Name; }

        internal static int TierIndex(string prefabName)
        {
            for (int i = 0; i < IsletTier.All.Count; i++)
                if (PrefabName(IsletTier.All[i]) == prefabName) return i;
            return -1;
        }

        // An invisible networked marker per tier at the islet's centre that runs the ground.
        internal static void Register()
        {
            foreach (IsletTier tier in IsletTier.All)
            {
                GameObject go = PrefabManager.Instance.CreateEmptyPrefab(PrefabName(tier), true);
                Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(go.GetComponent<MeshFilter>());
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<ZNetView>().m_persistent = true;
                go.AddComponent<FishingGround>();
                PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
                if (!Plugin.DebugLog.Value) continue;
                foreach (FishKind kind in tier.Fishes)
                {
                    GameObject prefab = PrefabManager.Instance.GetPrefab(kind.Prefab);
                    Fish fish = prefab != null ? prefab.GetComponent<Fish>() : null;
                    List<string> baits = new List<string>();
                    if (fish != null && fish.m_baits != null)
                        foreach (Fish.BaitSetting bait in fish.m_baits)
                            if (bait.m_bait != null) baits.Add(bait.m_bait.name + " " + bait.m_chance.ToString("0.##"));
                    Plugin.Debug("Fishing ground " + tier.Name + ": " + kind.Prefab + (fish != null ? " (" + fish.m_name + ") baits: " + string.Join(", ", baits.ToArray()) : " not found"));
                }
            }
        }

        // ---- the pouch ----

        // A small share of the tier's treasure: some coins, one other thing, rarely a sea vegvisir shard.
        static string GivePouch(IsletTier tier, Humanoid fisher)
        {
            List<string> parts = new List<string>();
            Loot coins = null;
            List<Loot> others = new List<Loot>();
            float total = 0f;
            foreach (Loot loot in tier.Loot)
            {
                if (loot.Item == "Coins") coins = loot;
                else { others.Add(loot); total += loot.Weight; }
            }
            if (coins != null) Give(fisher, "Coins", Random.Range(coins.Min, coins.Max + 1) / 3 + 1, parts);
            float roll = Random.Range(0f, total);
            foreach (Loot loot in others)
            {
                roll -= loot.Weight;
                if (roll > 0f) continue;
                Give(fisher, loot.Item, loot.Min, parts);
                break;
            }
            if (Random.value < ShardChance) Give(fisher, SeaChart.PrefabName, 1, parts);
            return Texts.Get("iov_pouch_caught", string.Join(", ", parts.ToArray()));
        }

        static void Give(Humanoid fisher, string item, int amount, List<string> parts)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null || amount <= 0) return;
            Inventory inventory = fisher.GetInventory();
            if (inventory.CanAddItem(prefab, amount)) inventory.AddItem(prefab, amount);
            else
            {
                GameObject dropped = Object.Instantiate(prefab, fisher.transform.position + Vector3.up, Quaternion.identity);
                dropped.GetComponent<ItemDrop>().SetStack(amount);
            }
            parts.Add(Texts.Amount(amount, drop.m_itemData.m_shared.m_name));
        }

        // A fish is taken either by reeling it all the way in (FishingFloat.Catch, which picks it up and
        // returns the "caught" message) or by walking up to it and picking it up (Fish.Interact). Both go
        // through Fish.Pickup. While Catch runs, a pouch's message replaces Catch's own.
        static bool inCatch;
        static string pouchText;

        [HarmonyPatch(typeof(FishingFloat), "Catch")]
        static class CatchPatch
        {
            static void Prefix() { inCatch = true; pouchText = null; }

            static void Postfix(ref string __result)
            {
                inCatch = false;
                if (pouchText != null) __result = pouchText;
                pouchText = null;
            }
        }

        // A ground's fish going into a bag. Around Sigrún's islet one of them has her fragment in it; and
        // now and then it's a sodden pouch instead of a fish.
        // (The game's fish are items now: reeling one in and picking one up both end in ItemDrop.Pickup;
        // Fish.Pickup only hands over to it. Fish.Pickup is still watched for a fish that isn't an item.)
        [HarmonyPatch(typeof(ItemDrop), "Pickup")]
        static class ItemPickupPatch
        {
            static bool Prefix(ItemDrop __instance, Humanoid character)
            {
                Fish fish = __instance != null ? __instance.GetComponent<Fish>() : null;
                return fish == null || !Taken(fish, character);
            }
        }

        [HarmonyPatch(typeof(Fish), "Pickup")]
        static class PickupPatch
        {
            static bool Prefix(Fish __instance, Humanoid character, ref bool __result)
            {
                if (__instance == null || __instance.GetComponent<ItemDrop>() != null) return true; // (its ItemDrop.Pickup sees it)
                if (!Taken(__instance, character)) return true;
                __result = true;
                return false;
            }
        }

        // A message about a fish just taken. While it's being reeled in, the game says "caught ..." right after
        // and that would cover it, so it takes that line's place (several, one under the other); otherwise
        // it's shown at once.
        internal static void Say(Character to, string text)
        {
            if (to == null || string.IsNullOrEmpty(text)) return;
            if (Localization.instance != null) text = Localization.instance.Localize(text);
            if (inCatch) { pouchText = pouchText == null ? text : pouchText + "\n" + text; return; }
            to.Message(MessageHud.MessageType.Center, text, 0, null, false);
        }

        static ZDOID lastTaken = ZDOID.None;

        // A fish being taken: the tale's look at it, and maybe a pouch instead (then true: the fish is gone, and
        // the game's own pickup mustn't go on).
        static bool Taken(Fish fish, Humanoid character)
        {
            ZNetView nview = fish.GetComponent<ZNetView>();
            if (character == null || character != Player.m_localPlayer || nview == null || !nview.IsValid()) return false;
            int ground = nview.GetZDO().GetInt(GroundKey, 0);
            if (ground <= 0 || ground > IsletTier.All.Count) return false;
            if (nview.GetZDO().m_uid == lastTaken) return false; // (the same fish asked for twice)
            lastTaken = nview.GetZDO().m_uid;
            Plugin.Debug("Fishing: " + fish.name + " from a " + IsletTier.All[ground - 1].Name + " ground picked up at " + fish.transform.position.ToString("F0"));
            Story.FromFish(fish, character);
            if (Random.value >= PouchChance) return false;
            string text = GivePouch(IsletTier.All[ground - 1], character);
            Say(character, text);
            nview.ClaimOwnership();
            ZNetScene.instance.Destroy(fish.gameObject);
            Plugin.Debug("Fishing: a pouch instead of a fish (" + text + ")");
            return true;
        }

    }

    // Runs one islet's fishing ground.
    class FishingGround : MonoBehaviour
    {
        ZNetView nview;
        int tierIndex = -1;
        List<Vector3> water;
        static GameObject ripple;

        void Awake()
        {
            nview = GetComponent<ZNetView>();
            if (nview == null || nview.GetZDO() == null) return;
            tierIndex = IsletFishing.TierIndex(Utils.GetPrefabName(gameObject));
            Plugin.Debug("Fishing ground " + (Tier != null ? Tier.Name : "?") + " loaded at " + transform.position.ToString("F0"));
            InvokeRepeating("Tick", Random.Range(2f, 6f), 10f);
            InvokeRepeating("Hint", Random.Range(8f, 15f), 15f);
        }

        IsletTier Tier { get { return tierIndex >= 0 ? IsletTier.All[tierIndex] : null; } }

        // On the owner: put fish back, one per RespawnSeconds since the ground was last full, all at once
        // after a long absence.
        void Tick()
        {
            if (Tier == null || nview == null || !nview.IsValid() || !nview.IsOwner()) return;
            if (!Player.IsPlayerInRange(transform.position, IsletFishing.ActiveRange)) return;
            if (water == null) water = WaterSpots();
            if (water.Count == 0 || Tier.Fishes.Count == 0) return;
            ZDO zdo = nview.GetZDO();
            long now = ZNet.instance.GetTime().Ticks;
            int missing = IsletFishing.MaxFish - Nearby().Count;
            if (missing <= 0)
            {
                zdo.Set(IsletFishing.LastFishKey, now);
                return;
            }
            long last = zdo.GetLong(IsletFishing.LastFishKey, 0L);
            int due = last == 0L ? IsletFishing.MaxFish : (int)((now - last) / (long)(IsletFishing.RespawnSeconds * System.TimeSpan.TicksPerSecond));
            int count = Mathf.Min(missing, due);
            if (count <= 0) return;
            for (int i = 0; i < count; i++) SpawnFish();
            zdo.Set(IsletFishing.LastFishKey, now);
            Plugin.Debug("Fishing ground " + Tier.Name + " at " + transform.position.ToString("F0") + ": " + count + " fish put back, " + (missing - count) + " still missing");
        }

        void SpawnFish()
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(PickSpecies());
            if (prefab == null) return;
            Vector3 at = water[Random.Range(0, water.Count)];
            GameObject go = Object.Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            // Low levels are common, the most stars rare.
            int span = Tier.FishMaxLevel - Tier.FishMinLevel + 1;
            int level = Tier.FishMinLevel + Mathf.Min(span - 1, (int)(Random.value * Random.value * span));
            ItemDrop drop = go.GetComponent<ItemDrop>();
            if (drop != null) drop.SetQuality(level);
            ZNetView fishView = go.GetComponent<ZNetView>();
            if (fishView != null && fishView.GetZDO() != null) fishView.GetZDO().Set(IsletFishing.GroundKey, tierIndex + 1);
        }

        string PickSpecies()
        {
            float total = 0f;
            foreach (FishKind kind in Tier.Fishes) total += kind.Weight;
            float roll = Random.Range(0f, total);
            foreach (FishKind kind in Tier.Fishes)
            {
                roll -= kind.Weight;
                if (roll <= 0f) return kind.Prefab;
            }
            return Tier.Fishes[0].Prefab;
        }

        // Fish of any islet's ground swimming around this one.
        List<Fish> Nearby()
        {
            List<Fish> result = new List<Fish>();
            foreach (Fish fish in Object.FindObjectsByType<Fish>(FindObjectsSortMode.None))
            {
                if (Utils.DistanceXZ(fish.transform.position, transform.position) > IsletFishing.Radius) continue;
                ZNetView view = fish.GetComponent<ZNetView>();
                if (view != null && view.IsValid() && view.GetZDO().GetInt(IsletFishing.GroundKey, 0) > 0) result.Add(fish);
            }
            return result;
        }

        // Spots in the shallow water close around the islet, 2-8 m deep, a little under the surface: where a line cast from the shore lands.
        List<Vector3> WaterSpots()
        {
            List<Vector3> spots = new List<Vector3>();
            float surface = ZoneSystem.instance.m_waterLevel;
            int mask = LayerMask.GetMask("Default", "static_solid", "terrain");
            for (int i = 0; i < 36; i++)
            {
                float a = i * 10f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                int found = 0;
                for (float r = 14f; r < 50f && found < 2; r += 1.5f)
                {
                    Vector3 from = transform.position + dir * r;
                    from.y = surface + 30f;
                    RaycastHit hit;
                    if (!Physics.Raycast(from, Vector3.down, out hit, 120f, mask)) break;
                    float depth = surface - hit.point.y;
                    if (depth < 2f) continue;
                    if (depth > 8f) break;
                    spots.Add(new Vector3(hit.point.x, surface - Mathf.Min(1.5f, depth * 0.4f), hit.point.z));
                    found++;
                    r += 3f;
                }
            }
            Plugin.Debug("Fishing ground at " + transform.position.ToString("F0") + ": " + spots.Count + " spots in the water");
            return spots;
        }

        // Every client: now and then a ripple where one of the fish swims, a quiet sign of the ground.
        void Hint()
        {
            Player player = Player.m_localPlayer;
            if (player == null || Utils.DistanceXZ(player.transform.position, transform.position) > IsletFishing.ActiveRange) return;
            if (ripple == null) ripple = ZNetScene.instance.GetPrefab("vfx_water_surface_fish");
            // Only a plain local effect; a networked one would need the net scene to remove it.
            if (ripple == null || ripple.GetComponent<ZNetView>() != null) return;
            List<Fish> fish = Nearby();
            if (fish.Count == 0) return;
            Vector3 at = fish[Random.Range(0, fish.Count)].transform.position;
            at.y = ZoneSystem.instance.m_waterLevel;
            Object.Destroy(Object.Instantiate(ripple, at, Quaternion.identity), 6f);
        }
    }
}
