using UnityEngine;

/// <summary>Night / storm / sandstorm / blackout presets. Only touches runtime copies, never the assets.</summary>
public static class MissionEnvironment
{
    public static void Apply(MissionEnv env, RCC_CarControllerV3 car, Transform cameraRoot, Material beamMaterial)
    {
        if (env == MissionEnv.Day) return;

        Light sun = RenderSettings.sun;
        if (sun == null)
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }

        Material sky = RenderSettings.skybox != null ? new Material(RenderSettings.skybox) : null;
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

        switch (env)
        {
            case MissionEnv.Dusk:
                if (sun) { sun.color = new Color(1f, 0.62f, 0.38f); sun.intensity *= 0.7f; sun.transform.rotation = Quaternion.Euler(12f, sun.transform.eulerAngles.y, 0f); }
                SetSky(sky, 0.7f, new Color(1f, 0.6f, 0.4f));
                RenderSettings.ambientLight = new Color(0.45f, 0.36f, 0.33f);
                Fog(new Color(0.75f, 0.52f, 0.42f), 0.0035f);
                Headlights(car, true);
                break;

            case MissionEnv.Night:
                if (sun) { sun.color = new Color(0.45f, 0.55f, 0.9f); sun.intensity = 0.12f; }
                SetSky(sky, 0.06f, new Color(0.2f, 0.25f, 0.45f));
                RenderSettings.ambientLight = new Color(0.12f, 0.14f, 0.22f);
                Fog(new Color(0.03f, 0.04f, 0.08f), 0.006f);
                Headlights(car, true);
                break;

            case MissionEnv.Blackout:
                if (sun) { sun.intensity = 0.02f; }
                SetSky(sky, 0.01f, Color.black);
                RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.07f);
                Fog(new Color(0.0f, 0.0f, 0.01f), 0.011f);
                Headlights(car, true);
                if (car) car.highBeamHeadLightsOn = true;
                break;

            case MissionEnv.Storm:
                if (sun) { sun.color = new Color(0.6f, 0.65f, 0.75f); sun.intensity *= 0.35f; }
                SetSky(sky, 0.25f, new Color(0.4f, 0.42f, 0.48f));
                RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.4f);
                Fog(new Color(0.36f, 0.38f, 0.42f), 0.012f);
                Headlights(car, true);
                Weather(cameraRoot, beamMaterial, new Color(0.7f, 0.75f, 0.85f, 0.5f), 900, new Vector3(0f, -28f, 0f), 0.04f, 1.6f);
                break;

            case MissionEnv.Sandstorm:
                if (sun) { sun.color = new Color(1f, 0.8f, 0.55f); sun.intensity *= 0.5f; }
                SetSky(sky, 0.5f, new Color(0.85f, 0.65f, 0.4f));
                RenderSettings.ambientLight = new Color(0.62f, 0.5f, 0.36f);
                Fog(new Color(0.78f, 0.62f, 0.42f), 0.018f);
                Headlights(car, true);
                Weather(cameraRoot, beamMaterial, new Color(0.9f, 0.72f, 0.45f, 0.35f), 500, new Vector3(14f, -2f, 0f), 0.25f, 0.5f);
                break;
        }
    }

    static void SetSky(Material sky, float exposure, Color tint)
    {
        if (sky == null) return;
        if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", sky.GetFloat("_Exposure") * exposure);
        if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", tint);
        if (sky.HasProperty("_Tint")) sky.SetColor("_Tint", tint);
    }

    static void Fog(Color c, float density)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = c;
        RenderSettings.fogDensity = density;
        foreach (var cam in Camera.allCameras)
            if (cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = c;
    }

    static void Headlights(RCC_CarControllerV3 car, bool on)
    {
        if (car != null) car.lowBeamHeadLightsOn = on;
    }

    /// <summary>Rain / dust volume that follows the camera.</summary>
    static void Weather(Transform follow, Material mat, Color color, int rate, Vector3 velocity, float size, float lengthScale)
    {
        if (follow == null || mat == null) return;
        var go = new GameObject("MissionWeather");
        go.transform.SetParent(follow, false);
        go.transform.localPosition = new Vector3(0f, 8f, 10f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 1.4f;
        main.startSpeed = 0f;
        main.startSize = size;
        main.startColor = color;
        main.maxParticles = rate * 2;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(40f, 2f, 40f);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = velocity.x; vel.y = velocity.y; vel.z = velocity.z;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        if (lengthScale > 1f) { r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = lengthScale; r.velocityScale = 0.05f; }
        ps.Play();
    }
}
