using System.Collections.Generic;
using UnityEngine;

namespace IslandsOfValheim
{
    // Signs of an islet for someone sailing past: gulls over it by day, a fire on its cliff by night.
    // Both are parts of the location, so they only exist while its zone is loaded, i.e. while a player is
    // close enough to see them.
    static partial class IsletBuilder
    {
        static readonly string[] beacons = { "MountainKit_brazier", "piece_brazierfloor01", "fire_pit" };

        static void PlaceHints()
        {
            for (int i = 0; i < 3; i++)
                PlaceNetworked(new[] { IsletPrefabs.GullSpawner }, Polar(cliffDeg + 60f + i * 120f, 3.5f), 0f, 0.3f);
            PlaceBeacon();
        }

        // A brazier up on the cliff ridge, next to where the tower stands on most islets.
        static void PlaceBeacon()
        {
            GameObject prefab = Find(beacons);
            if (prefab == null) return;
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            // A fire piece only shows its flames while it burns; light it for good before its scripts go.
            foreach (Fireplace fire in go.GetComponentsInChildren<Fireplace>(true))
                foreach (GameObject flames in new[] { fire.m_enabledObject, fire.m_enabledObjectHigh })
                    if (flames != null) flames.SetActive(true);
            Strip(go);
            go.AddComponent<NightFire>();
            built++;

            float deg = cliffDeg + 16f;
            Vector2 at = Polar(deg, PlateauAt(deg) + 3.5f);
            Bounds b = LocalBounds(go);
            float y = GroundMin(at.x, at.y, 0.4f * Mathf.Max(b.size.x, b.size.z)) - 0.1f * b.size.y;
            Move(go, b, new Vector3(at.x, y, at.y), false);
        }
    }

    // Keeps a fire's flames, light and smoke on only at night. Checked every few seconds; cheap.
    class NightFire : MonoBehaviour
    {
        readonly List<GameObject> parts = new List<GameObject>();
        bool lit = true;

        void Start()
        {
            foreach (Light light in GetComponentsInChildren<Light>(true))
                if (light.gameObject != gameObject) parts.Add(light.gameObject);
            foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
                if (particles.gameObject != gameObject && !parts.Contains(particles.gameObject)) parts.Add(particles.gameObject);
            InvokeRepeating("Check", Random.Range(0f, 2f), 4f);
        }

        void Check()
        {
            bool night = EnvMan.instance != null && EnvMan.IsNight();
            if (night == lit) return;
            lit = night;
            foreach (GameObject part in parts)
                if (part != null) part.SetActive(night);
        }
    }
}
