using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared, pooled flight effects: blue thruster jets under every wheel, a ground-dust ring on
/// lift-off and touchdown, an underglow light and a thruster hum. One set of effects is moved to
/// whichever car is active, so nothing is duplicated per car prefab.
/// Everything is created once at start (no runtime allocations while playing).
/// </summary>
[DisallowMultipleComponent]
public class FlyingCarEffects : MonoBehaviour
{
    [Header("Materials (assign the generated FlyingThruster material)")]
    [Tooltip("Additive particle material. If empty, one is created at runtime (may be pink in builds if the URP particle shader is stripped).")]
    public Material particleMaterial;

    [Header("Thrusters")]
    public bool thrusters = true;
    [Tooltip("Max particles per thruster. Lower for low-end Android.")]
    public int maxParticlesPerThruster = 40;

    [Header("Ground Burst (lift-off / landing)")]
    public bool groundBurst = true;
    public int burstParticles = 24;
    public Color burstColor = new Color(0.75f, 0.9f, 1f, 0.45f);

    [Header("Underglow Light")]
    public bool underglowLight = true;
    public float lightIntensity = 3f;
    public float lightRange = 7f;

    [Header("Audio")]
    [Tooltip("Looping thruster sound. If empty a soft electric hum is synthesised.")]
    public AudioClip thrusterLoop;
    public AudioClip liftOffSound;
    public AudioClip landingSound;
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("Multiply volume by the game's Sound Volume setting (OverAllGameMannager).")]
    public bool useGameSoundVolume = true;

    readonly List<ParticleSystem> thrusterPool = new List<ParticleSystem>(6);
    ParticleSystem burst;
    Light glow;
    AudioSource loopSource, oneShotSource;
    Transform poolRoot;

    RCC_FlyingCarModule module;
    int activeThrusters;
    float lastRate = -1f;
    float flash;
    Color thrusterColor;

    void Start()
    {
        poolRoot = new GameObject("FlyingCarEffects Pool").transform;
        poolRoot.SetParent(transform, false);

        if (!particleMaterial) particleMaterial = CreateFallbackMaterial();
        burst = CreateBurst();
        glow = CreateLight();
        CreateAudio();

        var mgr = FlyingCarManager.Instance;
        if (mgr)
        {
            mgr.ActiveModuleChanged += Attach;
            mgr.StateChanged += OnStateChanged;
            Attach(mgr.ActiveModule);
        }
    }

    void OnDestroy()
    {
        var mgr = FlyingCarManager.Instance;
        if (mgr)
        {
            mgr.ActiveModuleChanged -= Attach;
            mgr.StateChanged -= OnStateChanged;
        }
    }

    // ───────────────────────── Attach to the active car ─────────────────────────

    void Attach(RCC_FlyingCarModule m)
    {
        module = m;
        lastRate = -1f;
        activeThrusters = 0;

        for (int i = 0; i < thrusterPool.Count; i++)
        {
            if (!thrusterPool[i]) continue;
            thrusterPool[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            thrusterPool[i].transform.SetParent(poolRoot, false);
        }
        if (glow) { glow.transform.SetParent(poolRoot, false); glow.enabled = false; }
        if (loopSource) loopSource.Stop();

        if (!m || !m.Car) return;

        thrusterColor = m.Profile.thrusterColor;
        var wheels = m.Car.allWheelColliders;
        if (thrusters && wheels != null)
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                if (i >= thrusterPool.Count) thrusterPool.Add(null);
                if (!thrusterPool[i]) thrusterPool[i] = CreateThruster();
                ParticleSystem ps = thrusterPool[i];

                WheelCollider wc = wheels[i].wheelCollider;
                Vector3 local = m.Car.transform.InverseTransformPoint(wc.transform.position);
                local.y -= wc.suspensionDistance * (1f - wc.suspensionSpring.targetPosition); // wheel centre at rest
                ps.transform.SetParent(m.Car.transform, false);
                ps.transform.localPosition = local;
                ps.transform.localRotation = Quaternion.identity;
                SetThrusterColor(ps, thrusterColor);
                activeThrusters++;
            }
        }

        if (glow)
        {
            glow.transform.SetParent(m.Car.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            Color c = thrusterColor;
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            glow.color = max > 1f ? c / max : c;
        }
    }

    void OnStateChanged(RCC_FlyingCarModule m, FlightState prev, FlightState next)
    {
        if (prev == FlightState.Grounded && next == FlightState.LiftOff)
        {
            EmitBurst(m);
            flash = 1f;
            PlayOneShot(liftOffSound);
        }
        else if (next == FlightState.Grounded)
        {
            EmitBurst(m);
            PlayOneShot(landingSound);
        }
    }

    // ───────────────────────── Per-frame (only while flying or fading) ─────────────────────────

    void LateUpdate()
    {
        float lift = module && module.IsFlying ? module.LiftWeight : 0f;
        float thrust = module && module.IsFlying ? module.ThrustDemand : 0f;
        float intensity = lift * Mathf.Lerp(0.35f, 1f, thrust);
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 2.5f);

        // Thrusters
        float rate = module ? module.Profile.thrusterEmission * intensity : 0f;
        if (Mathf.Abs(rate - lastRate) > 0.5f || (rate == 0f && lastRate != 0f))
        {
            for (int i = 0; i < activeThrusters; i++)
            {
                ParticleSystem ps = thrusterPool[i];
                if (!ps) continue;
                var em = ps.emission;
                em.rateOverTime = rate;
                if (rate > 0f && !ps.isPlaying) ps.Play();
            }
            lastRate = rate;
        }

        // Light
        if (glow)
        {
            float li = underglowLight ? lightIntensity * (intensity + flash * 1.5f) : 0f;
            glow.intensity = li;
            glow.enabled = li > 0.01f;
        }

        // Audio
        if (loopSource)
        {
            float vol = volume * GameVolume() * Mathf.Clamp01(lift * (0.6f + 0.4f * thrust));
            if (vol > 0.001f)
            {
                loopSource.volume = vol;
                loopSource.pitch = 0.85f + 0.35f * thrust;
                if (!loopSource.isPlaying) loopSource.Play();
            }
            else if (loopSource.isPlaying) loopSource.Stop();
        }
    }

    void EmitBurst(RCC_FlyingCarModule m)
    {
        if (!groundBurst || !burst || !m) return;
        burst.transform.position = m.SampleGroundPoint() + Vector3.up * 0.15f;
        burst.Emit(burstParticles);
    }

    void PlayOneShot(AudioClip clip)
    {
        if (clip && oneShotSource) oneShotSource.PlayOneShot(clip, volume * GameVolume());
    }

    float GameVolume()
    {
        if (!useGameSoundVolume) return 1f;
        var gm = OverAllGameMannager.OA_GM;
        return gm != null && gm.SaveData != null ? Mathf.Clamp01(gm.SaveData.SoundVolume) : 1f;
    }

    // ───────────────────────── One-time creation ─────────────────────────

    ParticleSystem CreateThruster()
    {
        var go = new GameObject("FlyThruster");
        go.transform.SetParent(poolRoot, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(5, maxParticlesPerThruster);
        main.gravityModifier = 0f;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.15f;
        shape.rotation = new Vector3(90f, 0f, 0f); // emit straight down

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMaterial;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static void SetThrusterColor(ParticleSystem ps, Color c)
    {
        var main = ps.main;
        main.startColor = c;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    ParticleSystem CreateBurst()
    {
        var go = new GameObject("FlyGroundBurst");
        go.transform.SetParent(poolRoot, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startColor = burstColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(burstParticles * 2, 10);

        var em = ps.emission;
        em.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.9f;
        shape.rotation = new Vector3(90f, 0f, 0f); // flat ring on the ground

        var lv = ps.limitVelocityOverLifetime;
        lv.enabled = true;
        lv.limit = 1f;
        lv.dampen = 0.15f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    Light CreateLight()
    {
        var go = new GameObject("FlyUnderglow");
        go.transform.SetParent(poolRoot, false);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = lightRange;
        l.intensity = 0f;
        l.shadows = LightShadows.None;
        l.enabled = false;
        return l;
    }

    void CreateAudio()
    {
        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.playOnAwake = false;
        loopSource.loop = true;
        loopSource.spatialBlend = 0f;
        loopSource.clip = thrusterLoop ? thrusterLoop : SynthesiseHum();

        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.spatialBlend = 0f;
    }

    static AudioClip SynthesiseHum()
    {
        const int rate = 22050;
        const int len = rate; // 1 s loop; 90 Hz and 180 Hz fit a whole number of cycles
        var data = new float[len];
        var rng = new System.Random(7);
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.08f; // soft filtered air noise
            data[i] = 0.32f * Mathf.Sin(2f * Mathf.PI * 90f * t)
                    + 0.14f * Mathf.Sin(2f * Mathf.PI * 180f * t)
                    + 0.05f * Mathf.Sin(2f * Mathf.PI * 1440f * t)
                    + 0.45f * lp;
        }
        // Cross-fade the noise seam so the loop is click-free.
        const int fade = 1500;
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            data[i] = Mathf.Lerp(data[len - fade + i], data[i], k);
        }
        var trimmed = new float[len - fade];
        System.Array.Copy(data, trimmed, trimmed.Length);
        var clip = AudioClip.Create("FlyHum", trimmed.Length, 1, rate, false);
        clip.SetData(trimmed, 0);
        return clip;
    }

    static Material CreateFallbackMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (!s) s = Shader.Find("Particles/Standard Unlit");
        if (!s) s = Shader.Find("Sprites/Default");
        Debug.LogWarning("[FlyingCar] No particle material assigned to FlyingCarEffects; using a runtime fallback.");
        var m = new Material(s) { name = "FlyThruster (Runtime)" };
        return m;
    }
}
