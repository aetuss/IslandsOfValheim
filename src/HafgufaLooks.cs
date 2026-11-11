using UnityEngine;

namespace IslandsOfValheim
{
    // Hafgufa's own looks beyond its colours (RanNet): a few drowned souls' lights caught in its body.
    //
    // (Tried and dropped, 1.10.79-1.10.81: rocks, weed, net ropes and eyes made of the game's own models, hung on
    // its bones. Placed from the prefab's bones at rest they floated off its body; placed on its skin as drawn
    // (the skinned mesh baked a moment after it came) they mostly found no skin near their bones and weren't
    // placed at all. The user saw nothing wrong with it as it is, plain, and that's how it stays.)
    static class HafgufaLooks
    {
        internal static void Dress(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == "Tail3" || t.name == "Tail6" || t.name == "Tail9")
                {
                    GameObject l = new GameObject("IoV_HafgufaSoul");
                    l.transform.SetParent(t, false);
                    Light light = l.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = RanNet.SoulColour;
                    light.range = 4f;
                    light.intensity = 1.3f;
                    light.shadows = LightShadows.None;
                }
        }
    }
}
