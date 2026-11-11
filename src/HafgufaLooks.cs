using UnityEngine;

namespace IslandsOfValheim
{
    // What makes the game's serpent Rán's beast, beyond its size, colour and strength: a few drowned souls' lights
    // caught in its body, hung on its tail bones so they move with it (lights need no skin to sit on).
    // (1.10.79-1.10.85 also tried rocks, weed, net ropes and eyes on it, placed from its bones and then from its
    // skin read at run time. Every way left some of them floating off it or hanging far below it, so 1.10.86 drops
    // them: the user asked for sure, simple ways, and its plain body was already approved.)
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
