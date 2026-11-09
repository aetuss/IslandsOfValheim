using UnityEngine;

namespace IslandsOfValheim
{
    // Valhalla's spring, falling off the rim into the clouds. The game has no waterfall of its own to copy, so
    // it's made of particles: a sheet of streaks arcing out and down from between the rocks, spray where it
    // leaves the rim, and mist rising where it meets the clouds; with the sound of running water when one is
    // found among the game's sounds.
    static class ValhallaWater
    {
        // lip: where the water leaves the rim (in parent's space); outward: which way it falls, flat.
        internal static void Fall(Transform parent, Vector3 lip, Vector2 outward)
        {
            Shader shader = ValhallaClouds.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = ValhallaClouds.Find("Sprites/Default");
            if (shader == null) { Plugin.Log.LogWarning("Valhalla: no see-through shader for the waterfall"); return; }
            GameObject go = new GameObject("IoV_ValhallaFall");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lip;
            go.transform.localRotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y)); // +z: out over the edge
            Material soft = new Material(shader);
            soft.mainTexture = ValhallaClouds.Dot();
            if (soft.HasProperty("_TintColor")) soft.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));

            // the falling sheet: streaks, out and down
            ParticleSystem sheet = Make(go.transform, "IoV_FallSheet", Vector3.zero, soft);
            ParticleSystem.MainModule main = sheet.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.8f, 4.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.86f, 0.94f, 1f, 0.55f), new Color(1f, 1f, 1f, 0.4f));
            main.gravityModifier = 1f;
            main.maxParticles = 1600;
            ParticleSystem.EmissionModule emission = sheet.emission;
            emission.rateOverTime = 260f;
            ParticleSystem.ShapeModule shape = sheet.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4.5f, 0.3f, 0.3f);
            ParticleSystemRenderer drawn = sheet.GetComponent<ParticleSystemRenderer>();
            drawn.renderMode = ParticleSystemRenderMode.Stretch;
            drawn.velocityScale = 0.12f;
            drawn.lengthScale = 1.6f;
            Fade(sheet, 0.05f, 0.85f);

            // spray where it leaves the rim
            ParticleSystem spray = Make(go.transform, "IoV_FallSpray", new Vector3(0f, -0.5f, 0.8f), soft);
            main = spray.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.25f), new Color(0.9f, 0.95f, 1f, 0.15f));
            main.gravityModifier = 0.15f;
            main.maxParticles = 200;
            emission = spray.emission;
            emission.rateOverTime = 25f;
            shape = spray.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4.5f, 0.5f, 0.5f);
            Fade(spray, 0.2f, 0.6f);

            // mist rising where it meets the clouds
            ParticleSystem mist = Make(go.transform, "IoV_FallMist", new Vector3(0f, -64f, 12f), soft);
            main = mist.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(9f, 18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.22f), new Color(0.95f, 0.9f, 0.95f, 0.14f));
            main.gravityModifier = -0.02f;
            main.maxParticles = 120;
            emission = mist.emission;
            emission.rateOverTime = 7f;
            shape = mist.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 7f;
            Fade(mist, 0.3f, 0.6f);

            Sound(go);
        }

        static ParticleSystem Make(Transform parent, string name, Vector3 at, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystemRenderer drawn = go.GetComponent<ParticleSystemRenderer>();
            drawn.sharedMaterial = material;
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            drawn.receiveShadows = false;
            return ps;
        }

        // in over the first part of its life, out over the last
        static void Fade(ParticleSystem ps, float inTill, float outFrom)
        {
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inTill), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
        }

        // The game's own sound of running water, if one of its sounds is called so.
        static void Sound(GameObject go)
        {
            AudioClip clip = null;
            foreach (string word in new[] { "waterfall", "river", "stream", "brook", "water" })
            {
                foreach (AudioClip c in Resources.FindObjectsOfTypeAll<AudioClip>())
                    if (c != null && c.name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0 && c.length > 2f) { clip = c; break; }
                if (clip != null) break;
            }
            if (clip == null) { Plugin.Debug("Valhalla: no sound of running water found for the spring"); return; }
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = 45f;
            source.volume = 0.7f;
            if (AudioMan.instance != null && AudioMan.instance.m_ambientMixer != null) source.outputAudioMixerGroup = AudioMan.instance.m_ambientMixer;
            Plugin.Debug("Valhalla: the spring sounds like " + clip.name);
        }
    }
}
