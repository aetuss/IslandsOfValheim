using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The tale of the Brim Folk, told by Sævar one step at a time. Progress belongs to the world, not to one
    // player: each step is a global key, so whoever brings the news, everyone hears the rest. The steps go
    // in order:
    //   1. Sævar gives a torn map to the old Brim harbour (Meadows waters). Lighting its beacon finishes it.
    //   2. Back at Sævar: the first page of the tale, and a map to Ketill's islet (Swamp waters), where his
    //      crew lies dead and Ketill walks as an Elite Draugr. He carries the first glimmering fragment.
    //   3. Bringing the fragment to Sævar: the second page and new goods.
    // While anyone carries a fragment the sea turns against them: storms, and serpents that come looking.
    static class Story
    {
        internal const string OldHarbour = "IoV_Story_OldHarbour";
        internal const string KetillIslet = "IoV_Story_Ketill";

        internal const string Map1 = "IoV_StoryMap1";
        internal const string Map2 = "IoV_StoryMap2";
        internal const string Piece1 = "IoV_BrimPiece1";
        internal const string BrimOil = "IoV_BrimOil";
        internal const string StormGlass = "IoV_StormGlass";
        internal const string Ketill = "IoV_Ketill";
        internal const string KetillSpawner = "IoV_Spawner_IoV_Ketill";

        // The world's progress (global keys, not text keys).
        internal const string HarbourLit = "iovstory_harbour_lit";
        internal const string FirstTold = "iovstory_first_told";
        internal const string KetillDone = "iovstory_ketill_done";

        const string RequestRpc = "IoV_StoryMapRequest";
        const string AnswerRpc = "IoV_StoryMapAnswer";

        // Each map: its item, the location it leads to and the pin it leaves.
        static readonly string[][] Maps =
        {
            new[] { Map1, OldHarbour, "$iov_storypin1" },
            new[] { Map2, KetillIslet, "$iov_storypin2" },
        };

        // Lore stones on the story islets: prefab, label, text.
        internal static readonly string[][] Stones =
        {
            new[] { "IoV_StoryStone_Harbour", "RuneStone_Meadows", "iov_stone_harbour" },
            new[] { "IoV_StoryStone_Ketill1", "RuneStone_Swamps", "iov_stone_ketill1" },
            new[] { "IoV_StoryStone_Ketill2", "RuneStone_Swamps", "iov_stone_ketill2" },
            new[] { "IoV_StoryStone_Ketill3", "RuneStone_Swamps", "iov_stone_ketill3" },
        };

        static int calmEffect;

        internal static void Register()
        {
            Recolor.Shift parchment = Recolor.ToHue(0.09f, 0.3f, 1.35f);
            Recolor.Shift glimmer = Recolor.ToHue(0.13f, 0.75f, 1.25f);
            foreach (string[] map in Maps)
            {
                CustomItem item = SeafarerGear.Simple(map[0], new[] { "LeatherScraps", "TrollHide" }, map[0] == Map1 ? "iov_storymap1" : "iov_storymap2", 1, 0.1f, parchment);
                if (item != null) item.ItemDrop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
            }
            CustomItem piece = SeafarerGear.Simple(Piece1, new[] { "Thunderstone", "AmberPearl" }, "iov_piece1", 1, 0.5f, glimmer);
            if (piece != null)
            {
                ItemDrop.ItemData.SharedData shared = piece.ItemDrop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
                shared.m_teleportable = false; // it has to cross the sea, and the sea knows
            }
            CustomItem oil = SeafarerGear.Simple(BrimOil, new[] { "Honey" }, "iov_brimoil", 10, 0.5f, Recolor.ToHue(0.07f, 0.8f, 0.7f));
            if (oil != null)
            {
                ItemDrop.ItemData.SharedData shared = oil.ItemDrop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
                shared.m_food = shared.m_foodStamina = shared.m_foodEitr = shared.m_foodRegen = 0f;
                shared.m_consumeStatusEffect = null;
            }
            SE_Stats calm = SeafarerGear.Effect("IoV_SE_StormGlass", "iov_se_calm", false);
            calm.m_ttl = 600f;
            CustomItem glass = SeafarerGear.Simple(StormGlass, new[] { "Crystal" }, "iov_stormglass", 10, 0.3f, Recolor.ToHue(0.55f, 0.5f, 1.1f));
            if (glass != null)
            {
                ItemDrop.ItemData.SharedData shared = glass.ItemDrop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
                shared.m_consumeStatusEffect = calm;
                calm.m_icon = glass.ItemDrop.m_itemData.GetIcon();
            }
            calmEffect = calm.NameHash();

            RegisterKetill();
            foreach (string[] stone in Stones) RegisterStone(stone[0], stone[1], stone[2]);
        }

        // Ketill: an Elite Draugr with his name, a boss's bar over him, and the first fragment.
        static void RegisterKetill()
        {
            if (PrefabManager.Instance.GetPrefab("Draugr_Elite") == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(Ketill, "Draugr_Elite");
            Humanoid body = go.GetComponent<Humanoid>();
            if (body != null)
            {
                body.m_name = "$iov_ketill";
                body.m_boss = true;
                body.m_bossEvent = "";
                body.m_health *= 1.5f;
            }
            CharacterDrop drops = go.GetComponent<CharacterDrop>();
            GameObject fragment = PrefabManager.Instance.GetPrefab(Piece1);
            if (drops != null && fragment != null)
            {
                CharacterDrop.Drop drop = new CharacterDrop.Drop();
                drop.m_prefab = fragment;
                drop.m_amountMin = drop.m_amountMax = 1;
                drop.m_chance = 1f;
                drop.m_levelMultiplier = false;
                drops.m_drops.Add(drop);
            }
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
            IsletPrefabs.AddSpawner(Ketill, KetillSpawner, 2, 2, 0f, 0f, true, 30f);
        }

        // A lore stone with a text of our own, standing on the islet (no physics dropping it to the sea floor).
        static void RegisterStone(string name, string vanilla, string token)
        {
            if (PrefabManager.Instance.GetPrefab(vanilla) == null) vanilla = "RuneStone_Meadows";
            if (PrefabManager.Instance.GetPrefab(vanilla) == null) return;
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(name, vanilla);
            foreach (StaticPhysics physics in go.GetComponentsInChildren<StaticPhysics>(true)) Object.DestroyImmediate(physics);
            RuneStone stone = go.GetComponentInChildren<RuneStone>(true);
            if (stone != null)
            {
                if (stone.m_randomTexts != null) stone.m_randomTexts.Clear();
                stone.m_label = "$" + token + "_label";
                stone.m_topic = "$" + token + "_label";
                stone.m_text = "$" + token;
                stone.m_locationName = "";
            }
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // ---- progress ----

        internal static bool Key(string key)
        {
            return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key);
        }

        static void Set(string key)
        {
            if (ZoneSystem.instance != null && !ZoneSystem.instance.GetGlobalKey(key)) ZoneSystem.instance.SetGlobalKey(key);
            Plugin.Debug("Story: " + key);
        }

        static bool Has(Player player, string prefab)
        {
            return Find(player, prefab) != null;
        }

        static ItemDrop.ItemData Find(Player player, string prefab)
        {
            GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (item == null) return null;
            return player.GetInventory().GetItem(item.GetComponent<ItemDrop>().m_itemData.m_shared.m_name, -1, false);
        }

        static void Give(Player player, string prefab)
        {
            GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (item == null) return;
            Inventory inventory = player.GetInventory();
            if (inventory.CanAddItem(item, 1)) inventory.AddItem(item, 1);
            else Object.Instantiate(item, player.transform.position + Vector3.up, Quaternion.identity);
            player.Message(MessageHud.MessageType.Center, Texts.Get("iov_story_received", item.GetComponent<ItemDrop>().m_itemData.m_shared.m_name), 0, null, false);
        }

        // The pages of the tale this world has reached, in the player's compendium.
        static void LearnPages(Player player)
        {
            if (Key(FirstTold)) player.AddKnownText("$iov_lore_brim", "$iov_lore_brim_text");
            if (Key(KetillDone)) player.AddKnownText("$iov_lore_ketill", "$iov_lore_ketill_text");
        }

        static void Tell(Player player, string label, string text)
        {
            player.AddKnownText(label, text);
            if (TextViewer.instance != null) TextViewer.instance.ShowText(TextViewer.Style.Rune, label, text, true); // the game says it's in the compendium
        }

        // Talking to Sævar: the next step of the tale if there is one to take, else nothing (the store opens).
        internal static bool Talk(Player player, SeaTraderNpc npc)
        {
            LearnPages(player);
            if (!Key(FirstTold))
            {
                if (Key(HarbourLit))
                {
                    npc.Say("$iov_story_q1_done");
                    Set(FirstTold);
                    Tell(player, "$iov_lore_brim", "$iov_lore_brim_text");
                    Give(player, Map2);
                    return true;
                }
                if (!Has(player, Map1))
                {
                    npc.Say("$iov_story_q1_give");
                    Give(player, Map1);
                    return true;
                }
                return false;
            }
            if (!Key(KetillDone))
            {
                ItemDrop.ItemData piece = Find(player, Piece1);
                if (piece != null)
                {
                    player.GetInventory().RemoveOneItem(piece);
                    npc.Say("$iov_story_q2_done");
                    Set(KetillDone);
                    Tell(player, "$iov_lore_ketill", "$iov_lore_ketill_text");
                    return true;
                }
                if (!Has(player, Map2))
                {
                    npc.Say("$iov_story_q2_give");
                    Give(player, Map2);
                    return true;
                }
            }
            return false;
        }

        // What he says as the store opens, while a step is waiting; null for his usual lines.
        internal static string Reminder()
        {
            if (!Key(FirstTold)) return "$iov_story_q1_wait";
            if (!Key(KetillDone)) return "$iov_story_q2_wait";
            return "$iov_story_later";
        }

        // ---- the old harbour's beacon ----

        internal static bool InOldHarbour(Vector3 point)
        {
            Location location = Location.GetLocation(point, false);
            return location != null && Utils.GetPrefabName(location.gameObject) == OldHarbour;
        }

        internal static void HarbourBeaconLit(Vector3 beacon)
        {
            if (Key(HarbourLit)) return;
            Set(HarbourLit);
            Player player = Player.m_localPlayer;
            if (player != null && Vector3.Distance(player.transform.position, beacon) < 60f)
                player.Message(MessageHud.MessageType.TopLeft, "$iov_story_lit", 0, null, false);
        }

        // ---- torn maps ----

        static float waitingSince = -100f;

        internal static bool IsMap(ItemDrop.ItemData item)
        {
            return MapOf(item) != null;
        }

        static string[] MapOf(ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab == null) return null;
            foreach (string[] map in Maps)
                if (item.m_dropPrefab.name == map[0]) return map;
            return null;
        }

        [HarmonyPatch(typeof(Humanoid), "UseItem")]
        static class UseMapPatch
        {
            static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
            {
                string[] map = MapOf(item);
                if (map == null) return true;
                if (__instance == Player.m_localPlayer && Time.time - waitingSince > 3f && ZRoutedRpc.instance != null)
                {
                    waitingSince = Time.time;
                    ZPackage request = new ZPackage();
                    request.Write(map[0]);
                    request.Write(__instance.transform.position);
                    ZRoutedRpc.instance.InvokeRoutedRPC(SeaChart.ServerPeer(), RequestRpc, request);
                }
                return false;
            }
        }

        // Server: where the map leads. The location that was built if there is one, else the candidate
        // closest to the reader (going there builds it, and the other candidates vanish). The pin is near it,
        // not on it: an old chart shows the way, not the spot.
        static void OnRequest(long sender, ZPackage request)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
            string mapName = request.ReadString();
            Vector3 from = request.ReadVector3();
            string[] map = null;
            foreach (string[] m in Maps) if (m[0] == mapName) map = m;
            if (map == null) return;
            bool found = false;
            ZoneSystem.LocationInstance best = new ZoneSystem.LocationInstance();
            foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.GetLocationList())
            {
                if (instance.m_location == null || instance.m_location.m_prefabName != map[1]) continue;
                if (!found || (instance.m_placed && !best.m_placed) ||
                    (instance.m_placed == best.m_placed && Commands.Distance(instance, from) < Commands.Distance(best, from))) best = instance;
                found = true;
            }
            ZPackage answer = new ZPackage();
            answer.Write(mapName);
            answer.Write(found);
            if (found)
            {
                float angle = (map[1].GetStableHashCode() & 0xffff) / 65535f * Mathf.PI * 2f;
                answer.Write(best.m_position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 70f);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, answer);
        }

        static void OnAnswer(long sender, ZPackage answer)
        {
            waitingSince = -100f;
            string mapName = answer.ReadString();
            Player player = Player.m_localPlayer;
            if (player == null || Minimap.instance == null) return;
            if (!answer.ReadBool())
            {
                player.Message(MessageHud.MessageType.Center, "$iov_story_nomap", 0, null, false);
                return;
            }
            Vector3 where = answer.ReadVector3();
            string pin = "";
            foreach (string[] m in Maps) if (m[0] == mapName) pin = m[2];
            if (!SeaChart.HasPin(where)) Minimap.instance.DiscoverLocation(where, Minimap.PinType.Icon3, pin, false);
            player.Message(MessageHud.MessageType.Center, "$iov_story_mapped", 0, null, false);
            if (InventoryGui.IsVisible()) InventoryGui.instance.Hide();
            Plugin.Instance.StartCoroutine(ShowLater(where));
        }

        static System.Collections.IEnumerator ShowLater(Vector3 where)
        {
            yield return new WaitForSeconds(0.3f);
            if (Minimap.instance != null && !InventoryGui.IsVisible()) Minimap.instance.ShowPointOnMap(where);
        }

        [HarmonyPatch(typeof(Game), "Start")]
        static class RpcPatch
        {
            static void Postfix()
            {
                ZRoutedRpc.instance.Register<ZPackage>(RequestRpc, OnRequest);
                ZRoutedRpc.instance.Register<ZPackage>(AnswerRpc, OnAnswer);
            }
        }

        // ---- the restless sea ----

        internal static bool CarriesPiece(Player player)
        {
            return Has(player, Piece1);
        }

        internal static bool Calmed(Player player)
        {
            return calmEffect != 0 && player.GetSEMan().HaveStatusEffect(calmEffect);
        }
    }

    // While the local player carries a fragment (and hasn't calmed the water with a storm glass): a storm
    // over them wherever they go, and now and then a serpent at sea. Each client does this for its own
    // player; the serpents are ordinary networked creatures.
    //
    // The game blends light and fog from one weather to the next, but rain starts and stops at once, so the
    // storm comes in steps (light rain, rain, thunderstorm) and leaves the same way.
    class RestlessSea : MonoBehaviour
    {
        const float SerpentEvery = 120f, SerpentChance = 0.5f, SerpentDistance = 45f;
        const float StepSeconds = 20f;  // between one step of the weather and the next
        const float Blend = 12f;        // how long each step takes to blend in
        static readonly string[] Steps = { "", "LightRain", "Rain", "ThunderStorm" };

        bool active;
        int level, wanted;
        float nextStep, nextSerpent;
        float savedTransition = -1f;

        void Start()
        {
            InvokeRepeating("Tick", 2f, 2f);
        }

        void Tick()
        {
            Player player = Player.m_localPlayer;
            bool carrying = player != null && !player.IsDead() && Story.CarriesPiece(player) && !Story.Calmed(player);
            if (carrying != active)
            {
                active = carrying;
                wanted = active ? Steps.Length - 1 : 0;
                nextStep = 0f;
                if (player != null) player.Message(MessageHud.MessageType.Center, active ? "$iov_sea_restless" : "$iov_sea_calm", 0, null, false);
                nextSerpent = Time.time + SerpentEvery * 0.5f;
                Plugin.Debug("Restless sea " + (active ? "on" : "off"));
            }
            if (level != wanted && Time.time >= nextStep)
            {
                level += wanted > level ? 1 : -1;
                while (level > 0 && level < Steps.Length - 1 && !Exists(Steps[level])) level += wanted > level ? 1 : -1;
                Weather(level == 0 ? "" : Steps[level]);
                nextStep = Time.time + StepSeconds;
            }
            else if (level == wanted && savedTransition >= 0f && Time.time >= nextStep && EnvMan.instance != null)
            {
                EnvMan.instance.m_transitionDuration = savedTransition; // back to the game's own once the change is done
                savedTransition = -1f;
            }
            if (!active || Time.time < nextSerpent) return;
            nextSerpent = Time.time + SerpentEvery;
            if (Random.value < SerpentChance) SendSerpent(player);
        }

        static bool Exists(string environment)
        {
            if (EnvMan.instance == null) return false;
            foreach (EnvSetup env in EnvMan.instance.m_environments)
                if (env != null && env.m_name == environment) return true;
            return false;
        }

        void Weather(string environment)
        {
            EnvMan env = EnvMan.instance;
            if (env == null) return;
            if (savedTransition < 0f) savedTransition = env.m_transitionDuration;
            env.m_transitionDuration = Mathf.Max(savedTransition, Blend);
            env.SetForceEnvironment(environment);
            Plugin.Debug("Restless sea weather: " + (environment == "" ? "the world's own" : environment));
        }
        static void SendSerpent(Player player)
        {
            if (ZoneSystem.instance == null || ZNetScene.instance == null || WorldGenerator.instance == null) return;
            if (Ship.GetLocalShip() == null && !player.IsSwimming()) return;
            Vector3 at = player.transform.position;
            if (WorldGenerator.instance.GetBiome(at) != Heightmap.Biome.Ocean) return;
            foreach (Character c in Character.GetAllCharacters())
                if (c.m_name == "$enemy_serpent" && Vector3.Distance(c.transform.position, at) < 120f) return; // one is enough
            GameObject serpent = ZNetScene.instance.GetPrefab("Serpent");
            if (serpent == null) return;
            float water = ZoneSystem.instance.m_waterLevel;
            for (int i = 0; i < 8; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 p = at + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SerpentDistance;
                if (WorldGenerator.instance.GetBiome(p) != Heightmap.Biome.Ocean || ZoneSystem.instance.GetGroundHeight(p) > water - 12f) continue;
                Object.Instantiate(serpent, new Vector3(p.x, water - 1f, p.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                Plugin.Debug("Restless sea: a serpent at " + p.ToString("F0"));
                return;
            }
        }
    }
}
