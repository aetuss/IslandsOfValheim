using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // What stands on the large islets of the game's own terrain: each land has its designs, so the islets
    // of one world don't repeat one another. Some are dangerous (a dungeon or a lair, a guardian, guards),
    // some peaceful (people lived there; animals and things to find). Built from the game's own pieces,
    // props and whole structures (Vanilla), spread out over the wide plateau.
    static partial class IsletBuilder
    {
        // ---- helpers ----

        static int vanillaCount;

        // A whole vanilla structure (a house, a hut, dolmens, a ruin) copied onto the islet with its middle at
        // at, turned rotY, standing on the ground there. Whatever belongs to the game's own world is left
        // out: its terrain changes, its way-pointing stones, altars and traders, a dungeon generator.
        static GameObject Vanilla(string[] sources, Vector2 at, float rotY)
        {
            string source = FindLocation(sources);
            if (source == null) { missing++; Plugin.Log.LogWarning(locationName + ": none of the vanilla structures " + string.Join(", ", sources) + " exist."); return null; }
            CustomLocation clone = ZoneManager.Instance.CreateClonedLocation(locationName + "_Part" + (vanillaCount++), source);
            if (clone == null || clone.Prefab == null) return null;
            GameObject group = new GameObject("IoV_Vanilla_" + source);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = new Vector3(at.x, Height(at.x, at.y) - 0.05f, at.y);
            group.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            List<Transform> parts = new List<Transform>();
            foreach (Transform child in clone.Prefab.transform) parts.Add(child);
            foreach (Transform part in parts) part.SetParent(group.transform, false);
            int removed = 0;
            foreach (TerrainModifier m in group.GetComponentsInChildren<TerrainModifier>(true)) { Object.DestroyImmediate(m.gameObject); removed++; }
            foreach (TerrainOp op in group.GetComponentsInChildren<TerrainOp>(true)) { Object.DestroyImmediate(op.gameObject); removed++; }
            foreach (Vegvisir v in group.GetComponentsInChildren<Vegvisir>(true)) { Object.DestroyImmediate(v.gameObject); removed++; }
            foreach (OfferingBowl o in group.GetComponentsInChildren<OfferingBowl>(true)) { Object.DestroyImmediate(o.gameObject); removed++; }
            foreach (Trader t in group.GetComponentsInChildren<Trader>(true)) { Object.DestroyImmediate(t.gameObject); removed++; }
            foreach (DungeonGenerator g in group.GetComponentsInChildren<DungeonGenerator>(true)) { Object.DestroyImmediate(g.gameObject); removed++; }
            foreach (CreatureSpawner spawner in group.GetComponentsInChildren<CreatureSpawner>(true))
            {
                Vector3 p = root.transform.InverseTransformPoint(spawner.transform.position);
                float ground = Height(p.x, p.z);
                if (p.y < ground + 0.2f) spawner.transform.position = root.transform.TransformPoint(new Vector3(p.x, ground + 0.3f, p.z));
            }
            ZoneManager.Instance.DestroyCustomLocation(clone.Name);
            if (clone.Prefab != null) Object.DestroyImmediate(clone.Prefab);
            built++;
            Plugin.Debug(locationName + ": " + source + " at " + at.ToString("F0") + ", " + parts.Count + " parts, " + removed + " left out");
            return group;
        }

        // Where the islet's keeper stands (its animals, and on a dangerous islet its guardian and guards,
        // and the chest, come from it).
        static void Keeper(LargeKind kind, Vector2 at)
        {
            kind.KeeperOffset = new Vector3(at.x, PlateauHeight, at.y);
            PlaceNetworked(kind.KeeperName, kind.KeeperOffset, 0f);
        }

        // count of the candidates at angles from deg0 to deg1 and radii from r0 to r1, a little uneven;
        // networked (to pick, chop or break) or still props.
        static void Scatter(string[] candidates, int count, float deg0, float deg1, float r0, float r1, bool networked, float salt)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                float deg = Mathf.Lerp(deg0, deg1, t) + 6f * Noise(i * 2.3f + salt, salt * 0.7f);
                float r = Mathf.Lerp(r0, r1, 0.5f + 0.5f * Noise(salt + i * 1.7f, i * 0.9f));
                Vector2 at = Polar(deg, r);
                if (networked) PlaceNetworked(candidates, at, deg * 7f + salt, 0f);
                else Prop(candidates, at, deg * 7f + salt, 0.1f);
            }
        }

        // Trees (choppable) in a band round the islet.
        static void Grove(string[] trees, int count, float deg0, float deg1, float r0, float r1, float salt)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                float deg = Mathf.Lerp(deg0, deg1, t) + 8f * Noise(i * 1.9f + salt, salt);
                float r = Mathf.Lerp(r0, r1, 0.5f + 0.5f * Noise(salt + i * 2.1f, i * 1.3f));
                OnGround(trees, Polar(deg, r), deg * 11f + salt, 0f, 0.05f, false);
            }
        }

        // A straight run of a piece (a fence) from a to b, each length metres long, turned along it.
        static void Run(string piece, Vector2 a, Vector2 b, float length)
        {
            Vector2 d = b - a;
            int n = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / length));
            float yaw = 90f - Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = a + d * ((i + 0.5f) / n);
                At(piece, new Vector3(p.x, Height(p.x, p.y) - 0.05f, p.y), new Vector3(0f, yaw, 0f), false);
            }
        }

        // The way into the islet's dungeon, in its degrees (as Polar); the cliff's opposite side if none.
        static float DoorDeg()
        {
            return designInterior != null && !float.IsNaN(designInterior.DoorDeg) ? designInterior.DoorDeg : cliffDeg + 180f;
        }

        static float Footprint()
        {
            return designInterior != null ? Mathf.Max(designInterior.Footprint, CutsReach()) : 4f;
        }

        // ---- the Meadows ----

        // 1. The barrow hill: the crypt in the middle, three dolmens standing round it like old guards, the
        // dead of a village in two fields, what's left of the village on the far side, and the greydwarfs
        // that have come to the barrow.
        static void BarrowHill(LargeKind kind)
        {
            float door = DoorDeg();
            float ring = Mathf.Clamp(Footprint() + 9f, kind.Plateau * 0.45f, kind.Plateau - 6f);
            string[][] dolmens = { new[] { "Dolmen01", "Dolmen02" }, new[] { "Dolmen02", "Dolmen03" }, new[] { "Dolmen03", "Dolmen01" } };
            for (int i = 0; i < 3; i++)
            {
                float deg = door + 60f + i * 120f; // none in front of the way in
                Vanilla(dolmens[i], Polar(deg, ring), deg * 3f);
            }
            // The dead: grave fields either side of the way in, a few laid out in the open by the barrow.
            Village(door + 32f, ring - 1f, Graveyard);
            Village(door - 32f, ring - 1f, Graveyard);
            Scatter(new[] { "Pickable_ForestCryptRemains01", "Pickable_ForestCryptRemains02" }, 3, door + 70f, door + 150f, Footprint() + 2f, Footprint() + 4f, true, 3f);
            Scatter(new[] { "Pickable_ForestCryptRemains03", "Pickable_ForestCryptRemains04" }, 3, door + 210f, door + 290f, Footprint() + 2f, Footprint() + 4f, true, 7f);
            // The village that buried them, across the islet, long empty.
            float far = door + 180f;
            Village(far - 14f, kind.Plateau - 6f, () => RuinedHouse(false));
            Village(far + 14f, kind.Plateau - 5f, Well);
            Village(far, kind.Plateau - 12f, Campfire);
            RidgeDesign(); // the old stone tower on the cliff
            Keeper(kind, Polar(door + 120f, ring + 4f));
            LargeNature(kind);
            if (kind.Extra != null)
                foreach (string extra in kind.Extra)
                    PlaceNetworked(new[] { extra }, Polar(cliffDeg + 300f, kind.Plateau + 3f), cliffDeg, 0f);
            PlaceDock(cliffDeg + 229f);
            PlaceWreck(cliffDeg + 194f);
            PlaceHints();
            PlaceOutsideGuard();
        }

        // 2. The beekeeper's islet (peaceful): a farmhouse and its yard, hives along the garden fence, a
        // berry garden, a cart and a well, meadow flowers and oaks; deer and boars. No one is left, but
        // the hives still give honey and the berries grow back.
        static void BeekeepersIslet(LargeKind kind)
        {
            float home = cliffDeg + 160f;
            Vector2 house = Polar(home, kind.Plateau * 0.35f);
            Vanilla(new[] { "WoodHouse11", "WoodHouse12", "WoodHouse13", "WoodHouse9", "WoodHouse7" }, house, 90f - home + 180f);
            // The garden beside it: a fence round 10 x 8 m, a gate towards the house, berries in rows.
            float gdeg = home + 55f;
            Vector2 g = Polar(gdeg, kind.Plateau * 0.45f);
            Vector2 ax = Polar(gdeg + 90f, 1f), az = Polar(gdeg, 1f);
            Vector2 c1 = g - ax * 5f - az * 4f, c2 = g + ax * 5f - az * 4f, c3 = g + ax * 5f + az * 4f, c4 = g - ax * 5f + az * 4f;
            Run("wood_fence", c2, c3, 2f);
            Run("wood_fence", c3, c4, 2f);
            Run("wood_fence", c4, c1, 2f);
            Run("wood_fence", c1, g - ax * 1f - az * 4f, 2f);
            Run("wood_fence", g + ax * 1f - az * 4f, c2, 2f);
            Vector2 gate = g - az * 4f;
            At("wood_fence_gate", new Vector3(gate.x, Height(gate.x, gate.y) - 0.05f, gate.y), new Vector3(0f, 90f - (gdeg + 90f), 0f), false);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    Vector2 p = g + ax * (-3f + col * 3f) + az * (-1.5f + row * 2.2f);
                    PlaceNetworked(new[] { (row + col) % 3 == 0 ? "BlueberryBush" : "RaspberryBush", "RaspberryBush" }, p, row * 40f + col * 15f, 0f);
                }
            // The hives in a row along the garden's far side, facing the sun.
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = g + az * 5.5f + ax * (-4.5f + i * 3f);
                At("piece_beehive", new Vector3(p.x, Height(p.x, p.y) - 0.05f, p.y), new Vector3(0f, 90f - gdeg, 0f), false);
            }
            // The yard: the well, a cart, wood, the fire with benches.
            Village(home - 40f, kind.Plateau * 0.4f, Well);
            Vector2 cart = Polar(home - 20f, kind.Plateau * 0.55f);
            At("Cart", new Vector3(cart.x, Height(cart.x, cart.y), cart.y), new Vector3(0f, home * 2f, 0f), false);
            Village(home - 75f, kind.Plateau * 0.3f, Campfire);
            // A wild hive in the oaks, flowers in the grass, the oaks and beeches round the meadow.
            Grove(new[] { "Oak1", "Beech1" }, 7, home + 120f, home + 300f, kind.Plateau * 0.7f, kind.Plateau + 4f, 11f);
            Grove(new[] { "Beech_small1", "Beech_small2", "Birch1" }, 6, home + 10f, home + 330f, kind.Plateau + 3f, kind.Shore - 6f, 17f);
            PlaceNetworked(new[] { "Beehive" }, Polar(home + 200f, kind.Plateau * 0.75f), 30f, 0f);
            Scatter(new[] { "Pickable_Dandelion" }, 9, home + 90f, home + 330f, kind.Plateau * 0.3f, kind.Plateau * 0.85f, true, 23f);
            Scatter(new[] { "Pickable_Mushroom" }, 4, home + 140f, home + 280f, kind.Plateau + 1f, kind.Plateau + 5f, true, 29f);
            Keeper(kind, house + Polar(home + 180f, 6f));
            PlaceDock(home);
            PlacePickables();
        }

        // 3. The fishers' village (peaceful): three jetties along the low shore, their huts above them, boats
        // pulled up on the sand, barrels and nets of wood, the fire they met at, and a light on the point
        // for the boats coming home. The fish are still there.
        static void FishersVillage(LargeKind kind)
        {
            float shore = cliffDeg + 180f;
            for (int i = -1; i <= 1; i++) PlaceDock(shore + i * 22f);
            string[][] huts = { new[] { "WoodHouse1", "WoodHouse2" }, new[] { "WoodHouse3", "WoodHouse4" }, new[] { "WoodHouse2", "WoodHouse5" } };
            for (int i = -1; i <= 1; i++)
            {
                float deg = shore + i * 22f + 11f;
                Vanilla(huts[i + 1], Polar(deg, kind.Plateau - 5f), 270f - deg);
            }
            // Boats pulled up between the jetties, keels up.
            for (int i = 0; i < 2; i++)
            {
                float deg = shore - 11f + i * 22f;
                OnGround(new[] { "shipwreck_karve_bottomboards" }, Polar(deg, ShoreAt(deg) - 6f), deg + 90f, 0f, 0.15f);
            }
            Scatter(new[] { "barrell" }, 4, shore - 30f, shore + 30f, kind.Plateau - 1f, kind.Plateau + 2f, false, 5f);
            Scatter(new[] { "wood_stack" }, 2, shore - 20f, shore + 25f, kind.Plateau - 9f, kind.Plateau - 8f, false, 9f);
            // A rod left on the middle jetty.
            Vector2 rod = Polar(shore, ShoreAt(shore) - 0.5f);
            PlaceNetworked("Pickable_Fishingrod", new Vector3(rod.x, Mathf.Max(1.2f, Height(rod.x, rod.y) + 0.3f), rod.y), 90f - shore);
            // Where they met: the fire, benches round it.
            Village(shore, kind.Plateau * 0.45f, Campfire);
            // The light on the point: a stone pillar tower with a brazier on top.
            Vector2 point = Polar(cliffDeg, PlateauAt(cliffDeg) + 2f);
            float g = GroundMin(point.x, point.y, 1.2f) - 0.3f;
            float top = Top(At("stone_floor_2x2", new Vector3(point.x, g, point.y), Vector3.zero, false), g + 1f);
            for (int i = 0; i < 3; i++) top = Top(At("stone_pillar", new Vector3(point.x, top, point.y), Vector3.zero, false), top + 2f);
            At("piece_brazierfloor01", new Vector3(point.x, top, point.y), Vector3.zero, false);
            PlaceFishingGround();
            Grove(new[] { "Pinetree_01", "FirTree_small", "Beech1" }, 9, shore + 60f, shore + 300f, kind.Plateau * 0.6f, kind.Shore - 5f, 13f);
            Scatter(new[] { "Pickable_Flint", "Pickable_Stone" }, 6, shore - 40f, shore + 40f, kind.Shore - 4f, kind.Shore - 2f, true, 31f);
            Keeper(kind, Polar(shore, kind.Plateau * 0.3f));
            PlacePlants();
        }

        // ---- the Swamp ----

        // 1. The drowned crypt: the sunken crypt among dead trees, its dead in their fields, bone piles that
        // stir, mud with old iron in it, green fire at the door and the stilt houses of those who dug it.
        static void DrownedCrypt(LargeKind kind)
        {
            float door = DoorDeg();
            float ring = Mathf.Clamp(Footprint() + 8f, kind.Plateau * 0.45f, kind.Plateau - 6f);
            Vector2 left = Polar(door - 12f, Footprint() + 1.5f), right = Polar(door + 12f, Footprint() + 1.5f);
            At("piece_groundtorch_green", new Vector3(left.x, Height(left.x, left.y) - 0.05f, left.y), Vector3.zero, false);
            At("piece_groundtorch_green", new Vector3(right.x, Height(right.x, right.y) - 0.05f, right.y), Vector3.zero, false);
            Village(door + 45f, ring, Graveyard);
            Village(door + 100f, ring + 2f, Graveyard);
            PlaceNetworked(new[] { "BonePileSpawner_swamp", "BonePileSpawner" }, Polar(door + 160f, ring), 0f, 0f);
            PlaceNetworked(new[] { "BonePileSpawner_swamp", "BonePileSpawner" }, Polar(door - 120f, ring + 3f), 0f, 0f);
            Scatter(new[] { "mudpile" }, 3, door + 200f, door + 300f, ring - 2f, ring + 4f, true, 4f);
            Village(door - 60f, kind.Plateau - 5f, () => RuinedHouse(true));
            Village(door - 90f, kind.Plateau - 4f, () => RuinedHouse(true));
            Grove(tier.Trees, 12, door + 20f, door + 340f, kind.Plateau * 0.7f, kind.Shore - 5f, 5f);
            Scatter(new[] { "GuckSack", "GuckSack_small" }, 3, door + 130f, door + 230f, kind.Plateau * 0.75f, kind.Plateau, true, 19f);
            RidgeDesign();
            Keeper(kind, Polar(door + 70f, ring + 5f));
            PlaceDock(cliffDeg + 229f);
            PlaceWreck(cliffDeg + 194f);
            PlaceHints();
            PlaceOutsideGuard();
            PlacePickables();
        }

        // 2. The witch's hut: one hut on stilts in the dead wood, her cauldron at the door, skulls piled
        // round, green fire on poles, guck hanging in the trees and leeches in the water. Whatever lived
        // here comes out at night.
        static void WitchsHut(LargeKind kind)
        {
            float front = cliffDeg + 180f;
            Vanilla(new[] { "SwampHut5", "SwampHut4", "SwampHut3", "SwampHut1" }, Vector2.zero, 90f - front);
            Vector2 pot = Polar(front, 7f);
            Prop(new[] { "prop_piece_cauldron", "piece_cauldron" }, pot, front, 0.02f);
            Prop(new[] { "fire_pit" }, pot + Polar(front + 90f, 2.5f), 0f, 0.05f);
            for (int i = 0; i < 5; i++)
            {
                float deg = front + 40f + i * 70f;
                Prop(new[] { "skull_pile", "bone_stack" }, Polar(deg, 11f + (i % 2) * 2f), deg * 3f, 0.1f);
                Vector2 t = Polar(deg + 35f, 13f);
                At("piece_groundtorch_green", new Vector3(t.x, Height(t.x, t.y) - 0.05f, t.y), Vector3.zero, false);
            }
            Prop(new[] { "crypt_skeleton_laying" }, Polar(front + 120f, 9f), 40f, 0.05f);
            Grove(tier.Trees, 16, front + 15f, front + 345f, 16f, kind.Shore - 5f, 9f);
            Scatter(new[] { "GuckSack", "GuckSack_small" }, 5, front + 30f, front + 330f, 15f, kind.Plateau, true, 21f);
            Scatter(new[] { "mudpile" }, 2, front + 150f, front + 210f, kind.Plateau * 0.6f, kind.Plateau * 0.8f, true, 25f);
            Keeper(kind, Polar(front + 200f, 9f));
            PlaceDock(front + 25f);
            PlacePickables();
        }

        // 3. The misty graveyard: an old stone ruin in the middle of rows and rows of graves, draugr piles
        // and a bone pile among them, crows over the dead trees.
        static void MistyGraveyard(LargeKind kind)
        {
            float front = cliffDeg + 180f;
            Vanilla(new[] { "SwampRuin1", "SwampRuin2" }, Vector2.zero, 90f - front);
            for (int i = 0; i < 6; i++)
            {
                float deg = front + 30f + i * 50f;
                Village(deg, kind.Plateau * (i % 2 == 0 ? 0.55f : 0.75f), Graveyard);
            }
            PlaceNetworked(new[] { "Spawner_DraugrPile" }, Polar(front + 80f, kind.Plateau * 0.65f), 0f, 0f);
            PlaceNetworked(new[] { "Spawner_DraugrPile" }, Polar(front + 230f, kind.Plateau * 0.65f), 0f, 0f);
            PlaceNetworked(new[] { "BonePileSpawner_swamp", "BonePileSpawner" }, Polar(front + 155f, kind.Plateau * 0.5f), 0f, 0f);
            Scatter(new[] { "marker01", "marker02" }, 10, front + 20f, front + 340f, kind.Plateau * 0.85f, kind.Plateau - 1f, false, 33f);
            Grove(tier.Trees, 10, front + 10f, front + 350f, kind.Plateau + 2f, kind.Shore - 5f, 15f);
            Keeper(kind, Polar(front + 20f, kind.Plateau * 0.4f));
            PlaceDock(front);
            PlaceWreck(front + 60f);
            PlaceOutsideGuard();
            PlacePickables();
        }
    }
}
