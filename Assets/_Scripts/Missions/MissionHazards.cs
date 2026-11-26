using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mission-wide dangers: red scanner zones, drone / helicopter spotlights, EMP pylons,
/// glitch zones, falling rocks and missile strikes. Every hit becomes a "strike" on the runner.
/// </summary>
public class MissionHazards
{
    class Zone
    {
        public Transform t;
        public Renderer r;
        public float radius;
        public float timer;
        public float life;        // < 0 = permanent
        public bool inside;
        public float insideTime;
        public Vector3 a, b;      // drone patrol ends
        public float phase, speed;
        public Transform machine; // drone / helicopter model
        public Light spot;
    }

    class Strike
    {
        public Transform marker;
        public Renderer ring;
        public float timer;
        public float radius;
        public bool rock;
    }

    readonly MissionRunner run;
    readonly HazardKind kind;
    readonly int count;
    readonly System.Random rng;
    readonly List<Zone> zones = new List<Zone>();
    readonly List<Strike> strikes = new List<Strike>();
    readonly List<GameObject> debris = new List<GameObject>();
    float spawnTimer = 3f, exposure, strikeCooldown;
    Zone heli;

    public bool PlayerSpotted { get; private set; }

    public MissionHazards(MissionRunner runner, HazardKind kind, int count, System.Random rng)
    {
        run = runner;
        this.kind = kind;
        this.count = Mathf.Max(1, count);
        this.rng = rng;
    }

    // ───────────────────────── build ─────────────────────────

    public void Build(List<Vector3> route)
    {
        if (route == null || route.Count < 2) return;
        float len = MissionWorld.Length(route);

        switch (kind)
        {
            case HazardKind.RedZone:
                for (int i = 0; i < count; i++)
                {
                    float d = len * (0.18f + 0.7f * (i + 0.5f) / count) + Rand(-15f, 15f);
                    Vector3 p = MissionWorld.Along(route, d, out Vector3 tan);
                    Vector3 side = Vector3.Cross(Vector3.up, tan) * (rng.Next(2) == 0 ? 1f : -1f);
                    p += side * Rand(3.5f, 7f);
                    p.y = MissionWorld.GroundY(p, p.y);
                    AddStaticZone(p, Rand(4.5f, 6f), MissionVisuals.Red * 0.35f, "RedZone");
                }
                break;

            case HazardKind.EMP:
                for (int i = 0; i < count; i++)
                {
                    float d = len * (0.15f + 0.75f * (i + 0.5f) / count);
                    Vector3 p = MissionWorld.Along(route, d, out Vector3 tan);
                    p += Vector3.Cross(Vector3.up, tan) * Rand(-4f, 4f);
                    p.y = MissionWorld.GroundY(p, p.y);
                    AddStaticZone(p, Rand(6f, 8f), MissionVisuals.Purple * 0.3f, "EMP");
                }
                break;

            case HazardKind.Spotlight:
                for (int i = 0; i < count; i++)
                {
                    float d = len * (0.15f + 0.75f * (i + 0.5f) / count);
                    Vector3 c = MissionWorld.Along(route, d, out Vector3 tan);
                    Vector3 side = Vector3.Cross(Vector3.up, tan);
                    var z = new Zone
                    {
                        radius = 6.5f,
                        a = c - side * 18f - tan * 10f,
                        b = c + side * 18f + tan * 10f,
                        phase = (float)rng.NextDouble(),
                        speed = Rand(0.07f, 0.12f),
                        life = -1f,
                    };
                    z.machine = MissionVisuals.Drone(c + Vector3.up * 16f);
                    z.t = new GameObject("DroneBeam").transform;
                    z.t.SetParent(MissionVisuals.Root, false);
                    z.r = MissionVisuals.Zone(z.t, z.radius, 16f, MissionVisuals.Gold * 0.28f);
                    zones.Add(z);
                }
                break;

            case HazardKind.Helicopter:
            case HazardKind.Missiles:
                {
                    Vector3 p = run.PlayerPos + Vector3.up * 30f - run.PlayerForward * 40f;
                    heli = new Zone { radius = 9f, life = -1f };
                    heli.machine = MissionVisuals.Helicopter(p);
                    if (kind == HazardKind.Helicopter)
                    {
                        heli.t = new GameObject("HeliBeam").transform;
                        heli.t.SetParent(MissionVisuals.Root, false);
                        heli.t.position = run.PlayerPos - run.PlayerForward * 25f;
                        heli.r = MissionVisuals.Zone(heli.t, heli.radius, 30f, new Color(1f, 0.95f, 0.75f) * 0.25f);
                        var lg = new GameObject("HeliLight").AddComponent<Light>();
                        lg.transform.SetParent(heli.machine, false);
                        lg.type = LightType.Spot; lg.range = 60f; lg.spotAngle = 30f; lg.intensity = 6f; lg.shadows = LightShadows.None;
                        heli.spot = lg;
                    }
                    break;
                }
        }
    }

    void AddStaticZone(Vector3 p, float radius, Color color, string name)
    {
        var z = new Zone { radius = radius, life = -1f };
        z.t = new GameObject(name).transform;
        z.t.SetParent(MissionVisuals.Root, false);
        z.t.position = p;
        z.r = MissionVisuals.Zone(z.t, radius, 40f, color);
        zones.Add(z);
    }

    float Rand(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());

    // ───────────────────────── tick ─────────────────────────

    public void Tick(float dt)
    {
        Vector3 player = run.PlayerPos;
        bool covered = Physics.Raycast(player + Vector3.up * 2.5f, Vector3.up, 40f, MissionWorld.Mask, QueryTriggerInteraction.Ignore);
        if (strikeCooldown > 0f) strikeCooldown -= dt;
        PlayerSpotted = false;

        switch (kind)
        {
            case HazardKind.RedZone:
                foreach (var z in zones)
                    if (Inside(z, player))
                    {
                        run.Fail("RED ZONE! The scanner detected you.", FailReason.RedZone);
                        return;
                    }
                break;

            case HazardKind.EMP:
                foreach (var z in zones)
                {
                    MissionVisuals.Tint(z.r, MissionVisuals.Purple * (0.22f + 0.12f * Mathf.Sin(Time.time * 4f)));
                    if (Inside(z, player))
                    {
                        z.insideTime += dt;
                        PlayerSpotted = true;
                        if (z.insideTime > 1.2f && strikeCooldown <= 0f)
                        {
                            z.insideTime = 0f;
                            strikeCooldown = 3f;
                            run.AddStrike("EMP HIT! Systems offline.");
                            run.ControlLoss(2.5f);
                        }
                    }
                    else z.insideTime = 0f;
                }
                break;

            case HazardKind.Spotlight:
                foreach (var z in zones)
                {
                    z.phase += dt * z.speed;
                    float k = Mathf.PingPong(z.phase, 1f);
                    Vector3 c = Vector3.Lerp(z.a, z.b, Mathf.SmoothStep(0f, 1f, k));
                    c.y = MissionWorld.GroundY(c, c.y);
                    z.t.position = c;
                    z.machine.position = c + Vector3.up * 16f;
                    z.machine.Rotate(0f, 90f * dt, 0f);
                    bool hit = !covered && Inside(z, player);
                    if (hit) PlayerSpotted = true;
                    if (hit && !z.inside) z.insideTime = 0f;
                    if (hit) z.insideTime += dt;
                    if (hit && z.insideTime > 0.35f && z.timer <= 0f)
                    {
                        z.timer = 2.5f;
                        run.AddStrike("SPOTTED by a drone!");
                    }
                    if (z.timer > 0f && !hit) z.timer -= dt;
                    z.inside = hit;
                    MissionVisuals.Tint(z.r, hit ? MissionVisuals.Red * 0.4f : MissionVisuals.Gold * 0.28f);
                }
                break;

            case HazardKind.Helicopter:
                {
                    Vector3 target = player + run.PlayerVelocity * 0.6f;
                    Vector3 beam = heli.t.position;
                    float chase = Mathf.Max(9f, run.PlayerVelocity.magnitude * 0.82f);
                    beam = Vector3.MoveTowards(beam, target, chase * dt);
                    beam += new Vector3(Mathf.Sin(Time.time * 0.7f), 0f, Mathf.Cos(Time.time * 0.55f)) * 2.5f * dt;
                    beam.y = MissionWorld.GroundY(beam, player.y);
                    heli.t.position = beam;
                    Vector3 hp = Vector3.Lerp(heli.machine.position, beam + Vector3.up * 30f - run.PlayerForward * 6f, dt * 1.5f);
                    heli.machine.position = hp;
                    heli.machine.rotation = Quaternion.Slerp(heli.machine.rotation, Quaternion.LookRotation(MissionWorld.Flat(beam - hp) + Vector3.forward * 0.01f), dt * 2f);
                    if (heli.spot) heli.spot.transform.rotation = Quaternion.LookRotation(beam - heli.spot.transform.position);
                    bool hit = !covered && Inside(heli, player);
                    PlayerSpotted = hit;
                    exposure = hit ? exposure + dt : Mathf.Max(0f, exposure - dt * 0.5f);
                    if (exposure > 2.2f)
                    {
                        exposure = 0f;
                        run.AddStrike(covered ? "" : "The helicopter has you in its light!");
                    }
                    MissionVisuals.Tint(heli.r, hit ? MissionVisuals.Red * 0.35f : new Color(1f, 0.95f, 0.75f) * 0.25f);
                    break;
                }

            case HazardKind.Missiles:
                {
                    Vector3 hp = Vector3.Lerp(heli.machine.position, player + Vector3.up * 34f - run.PlayerForward * 35f
                        + new Vector3(Mathf.Sin(Time.time * 0.4f), 0f, Mathf.Cos(Time.time * 0.4f)) * 20f, dt * 0.8f);
                    heli.machine.position = hp;
                    heli.machine.rotation = Quaternion.Slerp(heli.machine.rotation, Quaternion.LookRotation(MissionWorld.Flat(player - hp) + Vector3.forward * 0.01f), dt * 2f);
                    SpawnStrikes(dt, 3.4f / count, 2.0f, 5f, false);
                    break;
                }

            case HazardKind.Glitch:
                SpawnGlitch(dt);
                break;

            case HazardKind.Rocks:
                SpawnStrikes(dt, 3.4f / count, 1.25f, 3.2f, true);
                break;
        }

        TickStrikes(dt, player);
    }

    bool Inside(Zone z, Vector3 p)
    {
        Vector3 d = p - z.t.position;
        d.y = 0f;
        return d.sqrMagnitude < z.radius * z.radius;
    }

    // Warning ring at the player's predicted position, then rock / missile.
    void SpawnStrikes(float dt, float interval, float warn, float radius, bool rock)
    {
        spawnTimer -= dt;
        if (spawnTimer > 0f) return;
        spawnTimer = interval * Rand(0.8f, 1.25f);

        Vector3 v = run.PlayerVelocity;
        Vector3 p = run.PlayerPos + MissionWorld.Flat(v) * (warn + 0.2f) + new Vector3(Rand(-5f, 5f), 0f, Rand(-5f, 5f));
        p.y = MissionWorld.GroundY(p, run.PlayerPos.y);

        var go = new GameObject(rock ? "RockWarning" : "MissileWarning");
        go.transform.SetParent(MissionVisuals.Root, false);
        go.transform.position = p;
        var ring = MissionVisuals.Zone(go.transform, radius, 0.08f, MissionVisuals.Red * 0.8f);
        strikes.Add(new Strike { marker = go.transform, ring = ring, timer = warn, radius = radius, rock = rock });
    }

    void TickStrikes(float dt, Vector3 player)
    {
        for (int i = strikes.Count - 1; i >= 0; i--)
        {
            var s = strikes[i];
            s.timer -= dt;
            MissionVisuals.Tint(s.ring, MissionVisuals.Red * (Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? 0.9f : 0.3f));
            if (s.timer > 0f) continue;

            Vector3 p = s.marker.position;
            if (s.rock)
            {
                var r = MissionVisuals.Prim(PrimitiveType.Sphere, MissionVisuals.Root, p + Vector3.up * 18f,
                    Vector3.one * Rand(1.6f, 2.6f), MissionVisuals.P.solidMaterial, new Color(0.35f, 0.3f, 0.27f), "Rock", true);
                var rb = r.AddComponent<Rigidbody>();
                rb.mass = 600f;
                rb.linearVelocity = Vector3.down * 12f;
                MissionVisuals.SetTagRecursive(r, "Obstacles");
                debris.Add(r);
                Object.Destroy(r, 9f);
            }
            else
            {
                MissionVisuals.Burst(p + Vector3.up, MissionVisuals.Orange, 40, 14f, 1.4f);
                Vector3 d = player - p; d.y = 0f;
                if (d.magnitude < s.radius)
                {
                    run.AddStrike("MISSILE HIT!");
                    run.Kick((d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward) * 6f + Vector3.up * 4f);
                }
            }
            Object.Destroy(s.marker.gameObject);
            strikes.RemoveAt(i);
        }
    }

    void SpawnGlitch(float dt)
    {
        spawnTimer -= dt;
        if (spawnTimer <= 0f)
        {
            spawnTimer = Rand(2.8f, 4.0f) / count;
            Vector3 fwd = run.PlayerForward;
            Vector3 p = run.PlayerPos + fwd * Rand(18f, 45f) + Vector3.Cross(Vector3.up, fwd) * Rand(-7f, 7f);
            p.y = MissionWorld.GroundY(p, run.PlayerPos.y);
            var z = new Zone { radius = Rand(4f, 6.5f), life = 7f };
            z.t = new GameObject("Glitch").transform;
            z.t.SetParent(MissionVisuals.Root, false);
            z.t.position = p;
            z.r = MissionVisuals.Zone(z.t, z.radius, 6f, MissionVisuals.Cyan * 0.35f);
            zones.Add(z);
        }

        Vector3 player = run.PlayerPos;
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            var z = zones[i];
            z.life -= dt;
            MissionVisuals.Tint(z.r, (Mathf.Repeat(Time.time * 9f + i, 1f) < 0.5f ? MissionVisuals.Cyan : MissionVisuals.Purple) * 0.35f);
            z.t.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 20f + i));
            if (Inside(z, player) && strikeCooldown <= 0f)
            {
                strikeCooldown = 2f;
                run.AddStrike("GLITCH! Control lost.");
                run.ControlLoss(1.5f);
                z.life = 0f;
            }
            if (z.life <= 0f)
            {
                Object.Destroy(z.t.gameObject);
                zones.RemoveAt(i);
            }
        }
    }
}
