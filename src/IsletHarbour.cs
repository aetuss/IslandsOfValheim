using System.Collections.Generic;
using System.Text;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // The sea trader's harbour: a peaceful islet with a jetty and a moored karve, a camp on the plateau (a copy
    // of Haldor's own camp, or Hildir's, the way the game builds it; a camp of our own if neither is there),
    // a toppled statue of his people on the ridge and a beacon that always burns. No guards, no chest, and
    // creatures keep away from it.
    //
    // Like Haldor, it uses vanilla's unique locations: the game picks several possible spots in the Swamp
    // and Plains waters, and the first one somebody comes near is built; the others vanish. Once it's
    // there, its anchor icon is on everyone's map for good.
    static partial class IsletBuilder
    {
        internal const string HarbourName = "IoV_SeaHarbour";
        const float PeaceRadius = 40f;
        static readonly string[] Camps = { "Vendor_BlackForest", "Hildir_camp" };
        // What isn't copied from a vendor's camp: the vendor himself, creatures, terrain changes, the raven.
        static readonly HashSet<string> NotCopied = new HashSet<string>
        {
            "Trader", "Character", "Humanoid", "MonsterAI", "AnimalAI", "CreatureSpawner", "SpawnArea", "SpawnSystem",
            "TerrainModifier", "TerrainOp", "Raven", "Location", "LocationProxy", "MusicLocation", "Tameable",
        };

        internal static void RegisterHarbour(int count)
        {
            tier = IsletTier.All[0]; // Meadows ground, rocks and plants: green and gentle
            locationName = HarbourName;
            SetVariant(0);
            seed = 911f;
            // Bigger than an ordinary islet, so a whole camp fits on the plateau.
            plateauRadius = 12.5f;
            shoreRadius = 25f;
            maxRadius = shoreRadius + 26f;
            root = ZoneManager.Instance.CreateLocationContainer(locationName);
            parent = root.transform;
            built = 0;
            missing = 0;

            BuildGround();
            float jetty = cliffDeg + 229f; // the low side, between two of the shore rocks
            HarbourRocks();
            PlaceHarbour(jetty);
            Plugin.Debug(locationName + " built: " + built + " parts, " + missing + " prefabs not found");

            LocationConfig config = new LocationConfig();
            config.Biome = Heightmap.Biome.Ocean;
            config.BiomeArea = Heightmap.BiomeArea.Median;
            config.Quantity = count;
            config.Priotized = true;
            config.Unique = true;      // the first one built stays, the other candidates are removed
            config.IconPlaced = true;  // and its icon shows from then on
            config.SnapToWater = true;
            config.RandomRotation = true;
            config.ClearArea = false;
            config.ExteriorRadius = 36f;
            config.MinAltitude = -1000f;
            config.MaxAltitude = -6f;
            config.MinTerrainDelta = 0f;
            config.MaxTerrainDelta = 1000f;
            config.MinDistance = IsletTier.All[1].MinDistance; // the Swamp and Plains waters
            config.MaxDistance = IsletTier.All[2].MaxDistance;
            config.Group = HarbourName;
            config.MinDistanceFromSimilar = 1000f;
            root.AddComponent<Location>().m_applyRandomDamage = false;
            ZoneManager.Instance.AddCustomLocation(new CustomLocation(root, false, config));
            Plugin.Log.LogInfo("Registered location " + locationName + " (" + count + " possible spots, one is kept)");
        }

        // The shore rocks of an ordinary islet and a few outcrops on the cliff side, away from the harbour.
        static void HarbourRocks()
        {
            float[] angles = { 159f, 209f, 249f, 304f, 354f, 29f, 84f, 124f };
            float[] sizes = { 7f, 4.5f, 6f, 5f, 9f, 7.5f, 6f, 4f };
            for (int i = 0; i < angles.Length; i++)
            {
                float deg = cliffDeg + angles[i];
                OnGround(tier.BigRocks, Polar(deg, ShoreAt(deg) - 2f), deg * 3f, sizes[i], 0.4f);
            }
            float[] slope = { cliffDeg + 25f, cliffDeg - 35f, cliffDeg + 70f };
            for (int i = 0; i < slope.Length; i++)
            {
                float r = (PlateauAt(slope[i]) + ShoreAt(slope[i])) * 0.5f + 1f;
                OnGround(tier.BigRocks, Polar(slope[i], r), slope[i] * 2f, 3.5f + i, 0.5f);
            }
        }

        static void PlaceHarbour(float j)
        {
            float face = 90f - j; // turned towards the jetty
            HarbourJetty(j);
            if (!VendorCamp(face)) OwnCamp(j, face);

            // Up on the ridge: what's left of a statue of his people, and the beacon that always burns.
            Vector2 statue = Polar(cliffDeg, PlateauAt(cliffDeg) + 3f);
            PropAtMost(new[] { "StatueThor_broken_bottom", "StatueFreya_broken_left", "StatueEvil" }, statue, 90f - cliffDeg + 180f, 0.15f, 2.6f);
            Lying(new[] { "StatueThor_broken_top", "StatueFreya_broken_right" }, Polar(cliffDeg + 14f, PlateauAt(cliffDeg) + 2.2f), cliffDeg * 2f, 2.4f);
            GameObject beacon = LitProp(new[] { "bonfire", "fire_pit" }, Polar(cliffDeg - 26f, PlateauAt(cliffDeg - 26f) + 3.2f), 0f, 0.1f);
            if (beacon != null) Glow(beacon.transform, 2.5f);

            for (int i = 0; i < 2; i++)
                PlaceNetworked(new[] { IsletPrefabs.GullSpawner }, Polar(cliffDeg + 90f + i * 150f, 3.5f), 0f, 0.3f);
            Optional(0.8f, () => OnGround(tier.Bushes, Polar(cliffDeg + 100f, PlateauAt(cliffDeg + 100f) + 1.5f), 120f, 0f, 0.15f, false));
            Optional(0.8f, () => OnGround(tier.Bushes, Polar(cliffDeg + 135f, PlateauAt(cliffDeg + 135f) + 1f), 300f, 0f, 0.15f, false));
            Peace();
        }

        // The camp of a vanilla vendor, copied part by part onto the plateau (turned so the vendor's spot
        // faces the jetty), with the sea trader where the vendor stands. Parts that are networked stay
        // networked, like in the vendor's own location: his fire burns, his chairs and crates are real.
        static bool VendorCamp(float face)
        {
            foreach (string name in Camps)
            {
                ZoneSystem.ZoneLocation location = ZoneManager.Instance.GetZoneLocation(name);
                if (location == null || !location.m_prefab.IsValid) continue;
                location.m_prefab.Load();
                GameObject camp = location.m_prefab.Asset;
                if (camp == null) continue;
                Plugin.Debug("Harbour camp from " + name + ":\n" + DescribeCamp(camp.transform));

                Vector3 spot = Vector3.zero;
                float yaw = 0f;
                foreach (Trader vendor in camp.GetComponentsInChildren<Trader>(true))
                {
                    spot = camp.transform.InverseTransformPoint(vendor.transform.position);
                    yaw = (Quaternion.Inverse(camp.transform.rotation) * vendor.transform.rotation).eulerAngles.y;
                    break;
                }
                Quaternion turn = Quaternion.Euler(0f, face - yaw, 0f);
                float reach = plateauRadius - 1f;
                int copied = 0;
                foreach (Transform child in camp.transform)
                {
                    if (!Copied(child)) continue;
                    Vector3 p = turn * child.localPosition;
                    if (new Vector2(p.x, p.z).magnitude > reach) continue;
                    GameObject copy = Object.Instantiate(child.gameObject, parent, false);
                    copy.name = child.name; // networked parts are spawned by this name
                    copy.transform.localRotation = turn * child.localRotation;
                    copy.transform.localScale = child.localScale;
                    copy.transform.localPosition = new Vector3(p.x, p.y + Height(p.x, p.z), p.z);
                    copied++;
                    built++;
                }
                if (copied == 0) continue;
                Vector3 at = turn * spot;
                if (new Vector2(at.x, at.z).magnitude > reach) at = Vector3.zero;
                PlaceNetworked(SeaTrader.PrefabName, new Vector3(at.x, Height(at.x, at.z), at.z), face);
                Plugin.Log.LogInfo("Sea trader's camp copied from " + name + " (" + copied + " parts)");
                return true;
            }
            return false;
        }

        static bool Copied(Transform part)
        {
            if (part.name.StartsWith("Music")) return false;
            foreach (Component c in part.GetComponentsInChildren<Component>(true))
                if (c != null && NotCopied.Contains(c.GetType().Name)) return false;
            return true;
        }

        static string DescribeCamp(Transform camp)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Transform child in camp)
            {
                sb.Append("  ").Append(child.name).Append(" at ").Append(child.localPosition.ToString("F1")).Append(Copied(child) ? "" : " (left out)").Append(':');
                foreach (Component c in child.GetComponents<Component>())
                    if (c != null && !(c is Transform)) sb.Append(' ').Append(c.GetType().Name);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        // If no vendor camp can be copied: a tent with lanterns, a campfire and a fisher's corner.
        static void OwnCamp(float j, float face)
        {
            float plateau = PlateauAt(j);
            Vector2 trader = Polar(j, plateau - 1.5f);
            PlaceNetworked(SeaTrader.PrefabName, new Vector3(trader.x, Height(trader.x, trader.y), trader.y), face);
            PropAtMost(new[] { "TraderTent", "hildir_tent" }, Polar(j + 180f, 1f), face, 0.05f, 5.5f);
            string[] lantern = { "deepnorth_lantern_standing", "dvergrprops_lantern_standing" };
            Prop(lantern, Polar(j - 25f, plateau - 1f), 0f, 0.05f);
            Prop(lantern, Polar(j + 25f, plateau - 1f), 0f, 0.05f);
            LitProp(new[] { "fire_pit_haldor", "fire_pit" }, Polar(j - 55f, 4.5f), 0f, 0.05f);
            Prop(new[] { "piece_logbench01" }, Polar(j - 78f, 5.6f), 90f - (j - 78f) + 90f, 0.05f);
            FishersCorner(j + 62f, plateau - 3f);
        }
        // A sound jetty, five planks long, with the karve moored along it.
        static void HarbourJetty(float deg)
        {
            float s = ShoreAt(deg);
            Vector2 u = Polar(deg, 1f), side = new Vector2(-u.y, u.x);
            float yaw = 90f - deg;
            Vector2 land = u * (s - 2f);
            float top = Mathf.Max(0.9f, GroundMin(land.x, land.y, 1f) + 0.1f);
            for (int i = 0; i < 5; i++)
            {
                Vector2 p = u * (s - 1f + i * 2f);
                At(tier.JettyFloor, new Vector3(p.x, top, p.y), new Vector3(0f, yaw, 0f), true);
            }
            for (int i = 0; i < 5; i++)
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector2 p = u * (s + 0.1f + i * 1.95f) + side * (0.9f * k);
                    float surface = GroundMin(p.x, p.y, 0.2f);
                    int count = Mathf.Max(1, Mathf.CeilToInt((top - (surface - 0.3f)) / 2f));
                    for (int n = 0; n < count; n++)
                    {
                        float poleEnd = top - 2f * (count - 1 - n);
                        if (poleEnd < surface - 0.25f) continue;
                        At(tier.JettyPole, new Vector3(p.x, poleEnd, p.y), new Vector3(0f, yaw, 0f), true);
                    }
                }
            Prop(new[] { "deepnorth_lantern_standing", "dvergrprops_lantern_standing" }, u * (s - 3f) + side * 1.6f, 0f, 0.05f);

            MooredKarve(u * (s + 4.5f) + side * 3f, yaw);
        }

        // A static copy of a karve, nothing to sail away with: sail furled, no spray from its bow, sitting
        // in the water.
        static void MooredKarve(Vector2 at, float yaw)
        {
            GameObject prefab = Find(new[] { "Karve" });
            if (prefab == null) return;
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localPosition = Vector3.zero;
            Ship ship = go.GetComponent<Ship>();
            if (ship != null && ship.m_sailObject != null) ship.m_sailObject.SetActive(false);
            foreach (ParticleSystem particles in go.GetComponentsInChildren<ParticleSystem>(true))
                if (particles.GetComponent<MeshFilter>() == null) particles.gameObject.SetActive(false);
            foreach (AudioSource sound in go.GetComponentsInChildren<AudioSource>(true)) sound.enabled = false;
            Strip(go);
            built++;
            Bounds b = LocalBounds(go);
            Plugin.Debug("Karve mesh bounds " + b.size.ToString("F1") + " min y " + b.min.y.ToString("F2") + ", solid bounds min y " + SolidBounds(go).min.y.ToString("F2"));
            Move(go, b, new Vector3(at.x, -1.4f, at.y), false);
        }

        // Drying racks, barrels and crates.
        static void FishersCorner(float deg, float r)
        {
            Vector2 c = Polar(deg, r);
            Vector2 u = Polar(deg, 1f), side = new Vector2(-u.y, u.x);
            for (int i = 0; i < 2; i++)
            {
                Vector2 p = c + side * (i * 1.6f - 0.8f);
                Vector2 a = p - u * 1f, b = p + u * 1f;
                float top = Top(At("wood_pole2", new Vector3(a.x, PlateauHeight, a.y), Vector3.zero, false), PlateauHeight + 1.9f);
                At("wood_pole2", new Vector3(b.x, PlateauHeight, b.y), Vector3.zero, false);
                At("wood_beam", new Vector3(p.x, top - 0.25f, p.y), new Vector3(0f, 90f - deg + 90f, 0f), false);
            }
            Prop(new[] { "barrell_static", "barrell" }, c + side * 2.4f + u * 0.4f, 20f, 0.05f);
            Prop(new[] { "barrell_static", "barrell" }, c + side * 2.6f - u * 0.6f, 80f, 0.05f);
            Prop(new[] { "CargoCrate", "dvergrprops_crate" }, c - side * 2.4f, 90f - deg, 0.05f);
            Prop(new[] { "CargoCrate", "dvergrprops_crate" }, c - side * 2.5f + u * 1.2f, 30f - deg, 0.05f);
        }

        // A fire prop that always burns: its flames switched on for good before its scripts go.
        static GameObject LitProp(string[] candidates, Vector2 at, float rotY, float sink)
        {
            GameObject prefab = Find(candidates);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            go.transform.localPosition = Vector3.zero;
            foreach (Fireplace fire in go.GetComponentsInChildren<Fireplace>(true))
                foreach (GameObject flames in new[] { fire.m_enabledObject, fire.m_enabledObjectHigh })
                    if (flames != null) flames.SetActive(true);
            Strip(go);
            built++;
            Bounds b = LocalBounds(go);
            float y = GroundMin(at.x, at.y, 0.4f * Mathf.Max(b.size.x, b.size.z)) - sink * b.size.y;
            Move(go, b, new Vector3(at.x, y, at.y), false);
            return go;
        }

        static void Glow(Transform on, float height)
        {
            GameObject glow = new GameObject("IoV_HarbourGlow");
            glow.transform.SetParent(on, false);
            glow.transform.localPosition = new Vector3(0f, height, 0f);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 30f;
            light.intensity = 2.5f;
            light.color = new Color(1f, 0.65f, 0.35f);
        }

        // A broken piece lying on its side.
        static void Lying(string[] candidates, Vector2 at, float rotY, float maxSize)
        {
            GameObject go = Copy(candidates, new Vector3(0f, rotY, 88f), 0f, true);
            if (go == null) return;
            Bounds b = LocalBounds(go);
            float extent = Mathf.Max(b.size.x, b.size.z);
            if (extent > maxSize) { go.transform.localScale = Vector3.one * (maxSize / extent); b = LocalBounds(go); }
            float y = GroundMin(at.x, at.y, 0.4f * Mathf.Max(b.size.x, b.size.z)) - 0.2f * b.size.y;
            Move(go, b, new Vector3(at.x, y, at.y), false);
        }

        // Creatures keep away and don't spawn here, as near a player's base.
        static void Peace()
        {
            GameObject area = new GameObject("IoV_HarbourPeace");
            area.layer = LayerMask.NameToLayer("character_trigger");
            area.transform.SetParent(root.transform, false);
            area.transform.localPosition = new Vector3(0f, PlateauHeight, 0f);
            SphereCollider sphere = area.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = PeaceRadius;
            area.AddComponent<EffectArea>().m_type = EffectArea.Type.PlayerBase | EffectArea.Type.NoMonsters;
        }
    }
}
