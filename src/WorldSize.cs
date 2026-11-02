using System.Globalization;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace IslandsOfValheim
{
    // The tier distances are set for vanilla's 10 km world radius. Expand World Size (and Expand World Data
    // when it's installed) already multiply every location's min/max distance by radius / 10000 when a world
    // loads, ours included, so the islets are placed in the right rings without our help: scaling them here
    // too would do it twice. Only the mod's own distance checks (which tier a spot belongs to) use this.
    static class WorldSize
    {
        const float VanillaRadius = 10000f;
        const string ExpandWorldSize = "expand_world_size";

        // How much bigger (or smaller) than vanilla this world is. Read each time: the server can change it.
        internal static float Scale
        {
            get { return Radius / VanillaRadius; }
        }

        internal static float Radius
        {
            get
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(ExpandWorldSize, out info) || info.Instance == null) return VanillaRadius;
                ConfigDefinition key = new ConfigDefinition("1. General", "World radius");
                if (!info.Instance.Config.ContainsKey(key)) return VanillaRadius;
                ConfigEntryBase entry = info.Instance.Config[key];
                float radius;
                if (entry.BoxedValue != null && float.TryParse(entry.BoxedValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out radius) && radius > 0f)
                    return radius;
                return VanillaRadius;
            }
        }
    }
}
