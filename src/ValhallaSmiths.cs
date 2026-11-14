using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Eitri and Brokkr, the dwarf smiths (Skáldskaparmál: they made Gullinbursti, Draupnir and Mjölnir, Brokkr
    // at the bellows, Loki as a fly stinging him), at their forge west of the einherjar's yard: Eitri hammering at
    // the anvil, Brokkr at the bellows (the forge's fire flares as he works them). Short and broad (the player's
    // body, squat), long-bearded. Ask Eitri for dwarf-work (DwarfWork): once a day.
    partial class ValhallaPeople
    {
        SpiritHuman eitri, brokkr;
        readonly List<Light> forgeFire = new List<Light>();
        readonly List<float> forgeFireWas = new List<float>();
        long smithBeat = long.MinValue;
        float flareAt = -1f;
        static EffectList forgeWork;

        static readonly Vector3 Squat = new Vector3(1.14f, 0.8f, 1.14f); // a dwarf: short and broad

        void Smiths()
        {
            Vector2 c = IsletBuilder.Smithy;
            Vector2 anvil = c + IsletBuilder.SmithAnvil, bellows = c + IsletBuilder.SmithBellows;
            eitri = Stand(new SpiritHuman.Look
            {
                Hair = "Hair5", Beard = "Beard10", HairColor = new Vector3(0.55f, 0.28f, 0.14f), SkinColor = Vector3.one * 0.9f,
                Chest = "ArmorLeatherChest", Legs = "ArmorLeatherLegs",
            }, new Vector3(anvil.x - 1f, 0f, anvil.y), Vector3.right, 4f, SmithHammer());
            brokkr = Stand(new SpiritHuman.Look
            {
                Hair = "Hair9", Beard = "Beard12", HairColor = new Vector3(0.12f, 0.1f, 0.09f), SkinColor = Vector3.one * 0.88f,
                Chest = "ArmorRagsChest", Legs = "ArmorLeatherLegs",
            }, new Vector3(bellows.x + 1f, 0f, bellows.y - 0.4f), Vector3.left, 4f);
            foreach (SpiritHuman d in new[] { eitri, brokkr })
                if (d != null) { d.transform.localScale = Vector3.Scale(d.transform.localScale, Squat); }
            Speaks(eitri, ValhallaSpeaker.Eitri, 1.8f);
            Speaks(brokkr, ValhallaSpeaker.Brokkr, 1.8f);
            // the forge's own fire (its lights), and the sound of its work (what the game plays as you craft at it)
            forgeFire.Clear();
            forgeFireWas.Clear();
            foreach (Transform forge in Children("forge"))
            {
                foreach (Light l in forge.GetComponentsInChildren<Light>(true)) { forgeFire.Add(l); forgeFireWas.Add(l.intensity); }
                break;
            }
            if (forgeWork == null)
            {
                GameObject prefab = PrefabManager.Instance.GetPrefab("forge");
                CraftingStation station = prefab != null ? prefab.GetComponent<CraftingStation>() : null;
                forgeWork = station != null ? station.m_craftItemEffects : new EffectList();
            }
            Plugin.Debug("Valhalla: Eitri and Brokkr at their forge (" + forgeFire.Count + " lights in its fire)");
        }

        // Eitri: three blows on the anvil, a breath, again (each with the forge's ring of work); Brokkr's bellows:
        // now and then the fire flares up and dies down.
        void Smithing(double now)
        {
            if (eitri != null)
            {
                long beat = (long)(now / 0.95);
                if (beat != smithBeat)
                {
                    smithBeat = beat;
                    int k = (int)(beat % 6);
                    if (k < 3)
                    {
                        eitri.Strike(k, null);
                        if (forgeWork != null && forgeWork.m_effectPrefabs != null && forgeWork.m_effectPrefabs.Length > 0 && Near(eitri.transform.position, 30f))
                            forgeWork.Create(eitri.transform.position + eitri.transform.forward * 0.9f + Vector3.up * 0.9f, Quaternion.identity);
                    }
                }
            }
            if (forgeFire.Count == 0) return;
            long breath = (long)(now / 5.0);
            float t = (float)(now - breath * 5.0);
            // a pump: up over half a second, down over two
            float flare = t < 0.5f ? t / 0.5f : t < 2.5f ? 1f - (t - 0.5f) / 2f : 0f;
            for (int i = 0; i < forgeFire.Count; i++)
                if (forgeFire[i] != null) forgeFire[i].intensity = forgeFireWas[i] * (1f + 0.9f * flare);
        }

        // A hammer in his hand: the game's own (the builder's), if it swings; else an iron mace (a smith's hammer
        // to look at).
        static string SmithHammer()
        {
            foreach (string name in new[] { "Hammer", "MaceIron" })
            {
                GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(name) : null;
                ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
                Attack swing = drop != null ? drop.m_itemData.m_shared.m_attack : null;
                if (swing != null && !string.IsNullOrEmpty(swing.m_attackAnimation)) return name;
            }
            return "MaceIron";
        }

        bool Near(Vector3 at, float within)
        {
            Player player = Player.m_localPlayer;
            return player != null && Vector3.Distance(player.transform.position, at) < within;
        }
    }

    // Dwarf-work: once a day Eitri mends what you have on and works it over: for half an hour your gear takes no
    // wear, and your weapons strike a tenth harder (the game's own status effect, its icon at the top of the
    // screen while it lasts).
    static class DwarfWork
    {
        const string Effect = "IoV_SE_DwarfWork", DayKey = "IoV_DwarfDay";
        const float Lasts = 1800f;
        static int hash;

        internal static void Register()
        {
            SE_Stats effect = SeafarerGear.Effect(Effect, "iov_dwarfwork", false);
            effect.m_ttl = Lasts;
            effect.m_modifyAttackSkill = Skills.SkillType.All;
            effect.m_damageModifier = 1.1f;
            foreach (string icon in new[] { "Hammer", "SledgeIron" })
            {
                GameObject item = PrefabManager.Instance.GetPrefab(icon);
                ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
                if (drop == null || drop.m_itemData.m_shared.m_icons == null || drop.m_itemData.m_shared.m_icons.Length == 0) continue;
                effect.m_icon = drop.m_itemData.m_shared.m_icons[0];
                break;
            }
            hash = Effect.GetStableHashCode();
        }

        internal static void Give(Player player, ValhallaSpeaker eitri)
        {
            if (ValhallaGifts.UsedToday(player, DayKey)) { eitri.Say("$iov_eitri_later"); return; }
            ValhallaGifts.Use(player, DayKey);
            int mended = 0;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetEquippedItems())
            {
                if (item == null || !item.m_shared.m_useDurability) continue;
                if (item.m_durability < item.GetMaxDurability()) mended++;
                item.m_durability = item.GetMaxDurability();
            }
            kept.Clear();
            SEMan effects = player.GetSEMan();
            if (effects != null) effects.AddStatusEffect(hash, true, 0, 0f, (short)0);
            eitri.Say("$iov_eitri_given");
            Plugin.Debug("Valhalla: dwarf-work for " + player.GetPlayerName() + " (" + mended + " mended)");
        }

        // While it lasts: whatever you have on keeps the wear it had (worn down by anything: a blow, a block,
        // building, being hit), checked every frame.
        static readonly Dictionary<ItemDrop.ItemData, float> kept = new Dictionary<ItemDrop.ItemData, float>();

        internal static void Tick()
        {
            Player player = Player.m_localPlayer;
            SEMan effects = player != null ? player.GetSEMan() : null;
            if (effects == null || hash == 0 || !effects.HaveStatusEffect(hash)) { if (kept.Count > 0) kept.Clear(); return; }
            foreach (ItemDrop.ItemData item in player.GetInventory().GetEquippedItems())
            {
                if (item == null || !item.m_shared.m_useDurability) continue;
                float had;
                if (!kept.TryGetValue(item, out had) || item.m_durability > had) kept[item] = item.m_durability;
                else if (item.m_durability < had) item.m_durability = had;
            }
        }

        // (development: iov_gifts ready)
        internal static void Ready(Player player) { player.m_customData.Remove(DayKey); }
    }
}
