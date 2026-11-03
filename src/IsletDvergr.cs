using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Dvergr islets: a living outpost, not a ruin. The dvergr are left in peace unless the player starts
    // something, the way their camps on land work: a dvergr ward (vanilla's dverger_guardstone) makes them
    // take it badly when their things are broken, their steins and lanterns are vanilla pickables that
    // anger them when taken, and here opening their chest does too.
    static partial class IsletBuilder
    {
        // A camp on a slab deck behind stake walls: beds, a table with their stein and lantern, supplies.
        static void DvergrCamp()
        {
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -1; iz <= 1; iz += 2)
                    At("blackmarble_floor", new Vector3(ix, FloorTop, iz), Vector3.zero, true);
            float b = FloorTop - 0.05f, g = PlateauHeight - 0.05f;
            At("dvergrprops_wood_wall", new Vector3(-1.2f, g, 3.2f), Vector3.zero, false);
            At("dvergrprops_wood_wall", new Vector3(1.4f, g, 3.2f), Vector3.zero, false);
            At("dvergrprops_wood_wall", new Vector3(3.2f, g, 1.2f), new Vector3(0f, 90f, 0f), false);
            float[] stakes = { 160f, 200f, 235f, 305f, 340f, 20f };
            foreach (float deg in stakes)
            {
                Vector2 p = Polar(deg, 6f);
                At("dvergrprops_wood_stakewall", new Vector3(p.x, Height(p.x, p.y) - 0.1f, p.y), new Vector3(0f, -(deg + 90f), 0f), false);
            }
            // The table, with what they'd miss: taking it is stealing.
            GameObject table = At("dvergrprops_table", new Vector3(1f, b, -1.3f), Vector3.zero, false);
            OnTop(table, "Pickable_DvergrStein", -0.4f, 0f, 30f);
            OnTop(table, "Pickable_DvergrLantern", 0.45f, 0.1f, 0f);
            At("dvergrprops_chair", new Vector3(-0.3f, b, -1.3f), new Vector3(0f, 90f, 0f), false);
            At("dvergrprops_stool", new Vector3(2.4f, b, -1.5f), Vector3.zero, false);
            At("dvergrprops_bed", new Vector3(-3f, g, 1.6f), Vector3.zero, false);
            At("dvergrprops_bed", new Vector3(-3f, g, -1.2f), Vector3.zero, false);
            At("dvergrprops_barrel", new Vector3(2.6f, g, -2.6f), Vector3.zero, false);
            At("dvergrprops_barrel", new Vector3(-2.4f, g, -3.2f), new Vector3(0f, 40f, 0f), false);
            At("dvergrprops_crate", new Vector3(2.4f, g, 2.4f), new Vector3(0f, 15f, 0f), false);
            At("dvergrprops_crate_long", new Vector3(-0.4f, g, 4.3f), Vector3.zero, false);
            At("dvergrprops_lantern_standing", new Vector3(1.6f, g, -3.6f), Vector3.zero, false);
            At("dvergrprops_lantern_standing", new Vector3(-2.2f, g, 3.8f), Vector3.zero, false);
            CourtyardGuardsAndChest(FloorTop, new Vector3(0.6f, FloorTop, 2f), 180f);
        }

        // Where they dig: marble blocks cut out of the islet, a frame with a beam over a black core on its
        // stand (theirs: taking it angers them), tools and crates.
        static void DvergrDigSite()
        {
            float g = PlateauHeight - 0.05f;
            float block = Top(At("blackmarble_2x2x1", new Vector3(-3f, g, 2.5f), Vector3.zero, false), g + 1f);
            At("blackmarble_2x1x1", new Vector3(-3f, block, 2.5f), Vector3.zero, false);
            At("blackmarble_2x2x1", new Vector3(-0.8f, g, 3.4f), new Vector3(0f, 10f, 0f), false);
            At("blackmarble_2x2x1", new Vector3(3.6f, g, 2.8f), new Vector3(0f, 35f, 0f), false);
            float left = Top(At("dvergrprops_wood_pole", new Vector3(-1.6f, g, -0.4f), Vector3.zero, false), g + 2f);
            float right = Top(At("dvergrprops_wood_pole", new Vector3(1.6f, g, -0.4f), Vector3.zero, false), g + 2f);
            Across("dvergrprops_wood_beam", new Vector3(0f, Mathf.Min(left, right), -0.4f));
            PlaceNetworked("Pickable_BlackCoreStand", new Vector3(0f, PlateauHeight, -0.4f), 0f);
            Prop(new[] { "dvergrprops_pickaxe" }, new Vector2(-2.2f, 1f), 70f, 0.05f);
            GameObject crate = At("dvergrprops_crate", new Vector3(2.4f, g, -2.6f), new Vector3(0f, 20f, 0f), false);
            OnTop(crate, "Pickable_DvergrLantern", 0f, 0f, 0f);
            At("dvergrprops_crate_long", new Vector3(3.4f, g, 0.2f), new Vector3(0f, 90f, 0f), false);
            At("dvergrprops_barrel", new Vector3(-3.2f, g, -2.4f), Vector3.zero, false);
            At("dvergrprops_lantern_standing", new Vector3(-2.8f, g, -0.6f), Vector3.zero, false);
            At("dvergrprops_lantern_standing", new Vector3(2.8f, g, 1.6f), Vector3.zero, false);
            CourtyardGuardsAndChest(PlateauHeight, new Vector3(-1.2f, PlateauHeight, 2.2f), 180f);
        }

        // A lookout on the cliff ridge: a stake wall towards the sea, a lantern and supplies.
        static void DvergrWatch()
        {
            Vector2 c; Quaternion turn; float b;
            RidgeSpot(2f, out c, out turn, out b);
            Local("dvergrprops_wood_stakewall", c, turn, 0f, 1.3f, b, 0f);
            Local("dvergrprops_lantern_standing", c, turn, -1.3f, 0f, b, 0f);
            Local("dvergrprops_crate", c, turn, 1.1f, -0.4f, b, 20f);
            Local("dvergrprops_barrel", c, turn, 1.7f, 0.7f, b, 0f);
        }

        // The dvergr ward: everything on the islet is theirs.
        static void PlaceDvergrWard()
        {
            PlaceNetworked(DvergrOutposts.Ward, new Vector3(0f, PlateauHeight, 0f), 0f);
        }

        // On some outposts Seekers come up from the shore just as the player arrives (vanilla's dvergr and
        // Seekers fight each other).
        static void PlaceRaiders()
        {
            Optional(0.3f, () =>
            {
                for (int i = 0; i < tier.Raiders.Length; i++)
                {
                    float deg = cliffDeg + 215f + i * 14f;
                    PlaceNetworked(new[] { IsletTier.SpawnerName(tier.Raiders[i]) }, Polar(deg, ShoreAt(deg) - 4f), 0f, 0.3f);
                }
            });
        }

        // A vanilla pickable on top of a placed piece (a stein on a table).
        static void OnTop(GameObject under, string pickable, float dx, float dz, float rotY)
        {
            if (under == null) return;
            Bounds b = SolidBounds(under);
            PlaceNetworked(pickable, new Vector3(b.center.x + dx, b.max.y, b.center.z + dz), rotY);
        }

        // A beam lying across the top of two poles along x, whichever way the vanilla beam is long.
        static void Across(string name, Vector3 pos)
        {
            GameObject beam = At(name, pos, Vector3.zero, false);
            if (beam == null) return;
            Bounds b = SolidBounds(beam);
            if (b.size.z <= b.size.x) return;
            beam.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Move(beam, SolidBounds(beam), pos, false);
        }
    }

    static class DvergrOutposts
    {
        internal const string Ward = "IoV_DvergrWard";
        const float WardRadius = 30f;      // the whole islet
        const float AlarmRadius = 40f;     // who hears it when their chest is opened

        // The dvergr's things on the islets, copied so that breaking one counts as an attack on the outpost
        // (WearNTear / Destructible m_triggerPrivateArea) whatever the vanilla prefab says, and so that
        // nothing drops off the islet onto the sea floor (StaticPhysics). The pickables keep vanilla's own
        // anger at being taken.
        internal static readonly string[] Pieces =
        {
            "dvergrprops_wood_wall", "dvergrprops_wood_stakewall", "dvergrprops_wood_pole", "dvergrprops_wood_beam",
            "dvergrprops_table", "dvergrprops_chair", "dvergrprops_stool", "dvergrprops_bed", "dvergrprops_barrel",
            "dvergrprops_crate", "dvergrprops_crate_long", "dvergrprops_lantern_standing",
            "blackmarble_floor", "blackmarble_2x2x1", "blackmarble_2x1x1",
            "Pickable_DvergrStein", "Pickable_DvergrLantern", "Pickable_BlackCoreStand",
        };

        internal static void Register()
        {
            GameObject vanilla = PrefabManager.Instance.GetPrefab("dverger_guardstone");
            if (vanilla == null || vanilla.GetComponent<PrivateArea>() == null)
            {
                Plugin.Log.LogWarning("The vanilla dvergr ward wasn't found: dvergr outposts won't mind being raided.");
                return;
            }
            GameObject go = PrefabManager.Instance.CreateClonedPrefab(Ward, "dverger_guardstone");
            PrivateArea area = go.GetComponent<PrivateArea>();
            Plugin.Debug("Dvergr ward: vanilla radius " + area.m_radius + ", faction " + area.m_ownerFaction + ", on by default " + area.m_enabledByDefault);
            area.m_radius = WardRadius;
            area.m_ownerFaction = Character.Faction.Dverger;
            area.m_enabledByDefault = true;
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
        }

        // Breaking one of these angers the dvergr around the ward it stands in.
        internal static void MarkOwned(GameObject go)
        {
            foreach (WearNTear piece in go.GetComponentsInChildren<WearNTear>(true)) piece.m_triggerPrivateArea = true;
            foreach (Destructible thing in go.GetComponentsInChildren<Destructible>(true)) thing.m_triggerPrivateArea = true;
        }

        static bool IsOutpostChest(Container container)
        {
            string name = Utils.GetPrefabName(container.gameObject);
            foreach (IsletTier tier in IsletTier.All)
                if (tier.DvergrOutpost && (name == tier.ChestName || name == tier.HoardName)) return true;
            return false;
        }

        // Opening their chest is stealing from them, like taking a lantern off their table.
        [HarmonyPatch(typeof(Container), "Interact")]
        static class ChestPatch
        {
            static void Postfix(Container __instance, Humanoid character, bool hold, bool __result)
            {
                if (hold || !__result || character != Player.m_localPlayer || !IsOutpostChest(__instance)) return;
                BaseAI.AggravateAllInArea(__instance.transform.position, AlarmRadius, BaseAI.AggravatedReason.Theif);
                Plugin.Debug("Dvergr chest opened: the outpost is alarmed");
            }
        }
    }
}
