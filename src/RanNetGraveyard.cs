using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The graveyard of the Brim Folk's fleet (the Valhalla arc's T3, see RanNet): open, deep water far out in the
    // Deep North's waters, where the bows and masts of their sunken ships stand up out of the sea at all angles.
    // Under the water the drowned glow (GraveyardStand, on each client). No land: you come by ship and fight from
    // it. A unique location like the tale's islets; Sævar's chart points to it.
    static partial class IsletBuilder
    {
        const float FleetReach = 70f;

        // Each wreck: where (degrees, metres out), its turn, and how it lies: the bow's pitch and roll and how
        // high it stands out of the water, the mast's lean and its top over the water, and whether the stern
        // shows too.
        struct Wreck
        {
            internal float Deg, Out, Yaw, Pitch, Roll, BowTop, MastLean, MastTop;
            internal bool Stern;
        }

        static readonly Wreck[] Fleet =
        {
            new Wreck { Deg = 15f,  Out = 30f, Yaw = 40f,  Pitch = -32f, Roll = 12f,  BowTop = 3.5f, MastLean = 22f,  MastTop = 9f,  Stern = false },
            new Wreck { Deg = 85f,  Out = 46f, Yaw = 170f, Pitch = -20f, Roll = -18f, BowTop = 2.4f, MastLean = -15f, MastTop = 11f, Stern = true },
            new Wreck { Deg = 150f, Out = 22f, Yaw = 255f, Pitch = -38f, Roll = 6f,   BowTop = 4.2f, MastLean = 28f,  MastTop = 7f,  Stern = false },
            new Wreck { Deg = 205f, Out = 52f, Yaw = 80f,  Pitch = -25f, Roll = -25f, BowTop = 2.8f, MastLean = -20f, MastTop = 10f, Stern = true },
            new Wreck { Deg = 270f, Out = 36f, Yaw = 300f, Pitch = -30f, Roll = 15f,  BowTop = 3.2f, MastLean = 18f,  MastTop = 8f,  Stern = false },
            new Wreck { Deg = 330f, Out = 58f, Yaw = 15f,  Pitch = -18f, Roll = -10f, BowTop = 2f,   MastLean = -25f, MastTop = 9.5f, Stern = true },
        };

        static void SunkenFleet()
        {
            tier = IsletTier.All[6]; // the Deep North's waters
            locationName = RanNet.Graveyard;
            SetVariant(0);
            root = ZoneManager.Instance.CreateLocationContainer(locationName);
            parent = root.transform;
            built = 0;
            missing = 0;
            try { FleetParts(); }
            catch (System.Exception e) { Plugin.Log.LogError("The sunken fleet failed: " + e); }

            Plugin.Debug(locationName + " built: " + built + " parts, " + missing + " prefabs not found");
            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.BiomeArea = Heightmap.BiomeArea.Median;
            config.Quantity = StorySpots;
            config.Priotized = true;
            config.Unique = true;
            config.SnapToWater = true;
            config.RandomRotation = true;
            config.ClearArea = false;
            config.ExteriorRadius = FleetReach;
            config.MinAltitude = -1000f;
            config.MaxAltitude = -20f; // deep water: nothing of the sea floor shows
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 1000f;
            config.MinDistance = tier.MinDistance + 300f;
            config.MaxDistance = tier.MaxDistance;
            config.Group = locationName;
            config.MinDistanceFromSimilar = 600f;
            Location location = root.AddComponent<Location>();
            location.m_applyRandomDamage = false;
            location.m_exteriorRadius = FleetReach;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + locationName + " (" + StorySpots + " possible spots, one is kept)");
        }

        // (The location's root is on the water: y 0 is the sea's surface.)
        static void FleetParts()
        {
            foreach (Wreck w in Fleet)
            {
                Vector2 at = Polar(w.Deg, w.Out);
                Vector2 along = Polar(90f - w.Yaw, 1f); // the wreck's own forward, flat
                Sunk(new[] { "shipwreck_vikingship_front", "shipwreck_karve_bow" }, at, new Vector3(w.Pitch, w.Yaw, w.Roll), w.BowTop, 1f);
                Vector2 mast = at - along * 4f + new Vector2(along.y, -along.x) * 1.5f;
                Sunk(new[] { "shipwreck_vikingship_mast1" }, mast, new Vector3(w.MastLean, w.Yaw + 70f, w.MastLean * 0.4f), w.MastTop, 1.4f);
                if (w.Stern)
                    Sunk(new[] { "shipwreck_vikingship_rear", "shipwreck_karve_stern" }, at - along * 11f, new Vector3(-w.Pitch * 0.6f, w.Yaw, -w.Roll), 1.6f, 1f);
            }
            PlaceNetworked(RanNet.Stand, Vector3.zero, 0f);
        }

        // A piece of a wreck turned as it lies, its top this high over the water, scaled.
        static void Sunk(string[] names, Vector2 at, Vector3 euler, float top, float scale)
        {
            GameObject go = Copy(names, euler, 0f, true);
            if (go == null) return;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) if (ps.gameObject != go) ps.gameObject.SetActive(false);
            foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
            if (scale != 1f) go.transform.localScale = go.transform.localScale * scale;
            Bounds b = LocalBounds(go);
            Move(go, b, new Vector3(at.x, top, at.y), true);
            Plugin.Debug(locationName + ": " + go.name + " at " + at.ToString("F0") + ", " + b.size.ToString("F1") + " m, " + (b.size.y - top).ToString("F1") + " m under the water");
        }
    }
}
