using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared state while a mission is being laid out.</summary>
public class MissionBuildContext
{
    public System.Random rng;
    public MissionDef def;
    public MissionArea area;
    public Vector3 start;
    public Vector3 cursor;              // where the player is expected to be when the next step starts
    public Vector3? forcedStart;        // a step that needs a special start (ramp, speed strip)
    public Vector3 forcedHeading;
    public List<Vector3> mainRoute;     // first real route: props are placed along it
    public readonly List<Vector3> allRoutes = new List<Vector3>();
    public float avgSpeed = 11f;        // m/s used for time limits

    public void AddRoute(List<Vector3> pts)
    {
        if (pts == null || pts.Count < 2) return;
        if (mainRoute == null && MissionWorld.Length(pts) > 60f) mainRoute = new List<Vector3>(pts);
        allRoutes.AddRange(pts);
    }
}

/// <summary>Base class of every step kind.</summary>
public abstract class MissionStepRuntime
{
    protected MissionRunner R;
    protected MissionStep S;
    protected System.Random rng;
    public float Estimate { get; protected set; }
    /// <summary>How many points had to use the relaxed fallback (diagnostics).</summary>
    public static int Fallbacks;

    public static MissionStepRuntime Create(MissionStep s)
    {
        switch (s.kind)
        {
            case StepKind.Checkpoints: return new CheckpointsStep();
            case StepKind.PickupDeliver: return new DeliverStep();
            case StepKind.Collect: return new CollectStep();
            case StepKind.Hold: return new HoldStep();
            case StepKind.Follow: return new FollowStep();
            case StepKind.Pace: return new PaceStep();
            case StepKind.Escort: return new EscortStep();
            case StepKind.Tow: return new TowStep();
            case StepKind.Evade: return new EvadeStep();
            case StepKind.Race: return new RaceStep();
            case StepKind.SpeedTrial: return new SpeedTrialStep();
            case StepKind.SpeedBand: return new SpeedBandStep();
            case StepKind.Survive: return new SurviveStep();
            case StepKind.Ram: return new RamStep();
            case StepKind.Jump: return new JumpStep();
            case StepKind.Choice: return new ChoiceStep();
            default: return new ReachStep();
        }
    }

    public void Init(MissionRunner runner, MissionStep step, System.Random random)
    {
        R = runner;
        S = step;
        rng = random;
    }

    /// <summary>Called before the start position is chosen. Return true if this step set ctx.forcedStart.</summary>
    public virtual bool ChooseStart(MissionBuildContext ctx) => false;
    public abstract void Prepare(MissionBuildContext ctx);
    public virtual void Begin() { }
    /// <summary>Returns true when the step is complete.</summary>
    public abstract bool Tick(float dt);
    public virtual void End() { }
    /// <summary>The player hit a mission vehicle. Return true if it was a valid target hit (not a crash).</summary>
    public virtual bool OnHit(MissionMover mover, float impact) => false;

    // ───────── helpers ─────────

    protected Vector3 Pick(MissionBuildContext ctx, Vector3 from, float min, float max, List<Vector3> corners, out float len, Vector3? avoid = null)
    {
        if (MissionWorld.PickPoint(ctx.rng, from, min, max, ctx.area, out Vector3 p, out len, corners, avoid)) return p;
        Fallbacks++;
        // Last resort: any NavMesh point in range.
        for (int t = 0; t < 40; t++)
        {
            float a = (float)ctx.rng.NextDouble() * Mathf.PI * 2f;
            Vector3 c = from + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Mathf.Lerp(min, max, (float)ctx.rng.NextDouble());
            if (UnityEngine.AI.NavMesh.SamplePosition(c, out UnityEngine.AI.NavMeshHit h, 25f, UnityEngine.AI.NavMesh.AllAreas)
                && MissionWorld.TryPath(from, h.position, corners, out len)) return h.position;
        }
        len = min;
        if (corners != null) { corners.Clear(); corners.Add(from); corners.Add(from + Vector3.forward * min); }
        return from + Vector3.forward * min;
    }

    protected string Label(string fallback) => string.IsNullOrEmpty(S.label) ? fallback : S.label;

    protected static string Meters(float d) => Mathf.RoundToInt(d) + " m";

    protected float Dist(Transform t) => t == null ? 0f : MissionWorld.Flat(t.position - R.PlayerPos).magnitude;
    protected float Dist(Vector3 p) => MissionWorld.Flat(p - R.PlayerPos).magnitude;

    /// <summary>Static decoration matching the label (broken car, crate, tanker...).</summary>
    protected GameObject Decor(Vector3 pos, Vector3 forward)
    {
        string l = (S.label ?? "").ToLowerInvariant();
        Quaternion rot = Quaternion.LookRotation(forward.sqrMagnitude > 0.01f ? forward : Vector3.forward);
        Vector3 off = Vector3.Cross(Vector3.up, forward.normalized) * (S.radius + 2.5f);
        if (l.Contains("truck") || l.Contains("tanker")) return MissionVisuals.ParkedCar(rng, pos + off, rot, MoverKind.Truck);
        if (l.Contains("van")) return MissionVisuals.ParkedCar(rng, pos + off, rot, MoverKind.Van);
        if (l.Contains("car")) return MissionVisuals.ParkedCar(rng, pos + off, rot, MoverKind.Car);
        if (l.Contains("crate") || l.Contains("device") || l.Contains("warehouse") || l.Contains("box"))
            return MissionVisuals.Spawn(MissionVisuals.P.crate, pos + off * 0.7f, rot);
        return null;
    }
}

// ───────────────────────────── Reach ─────────────────────────────

public class ReachStep : MissionStepRuntime
{
    Vector3 target;
    MissionBeacon beacon;

    public override void Prepare(MissionBuildContext ctx)
    {
        var path = new List<Vector3>();
        float len;
        if (S.toStart)
        {
            target = ctx.start;
            if (!MissionWorld.TryPath(ctx.cursor, target, path, out len)) len = Vector3.Distance(ctx.cursor, target);
        }
        else target = Pick(ctx, ctx.cursor, S.minDist, S.maxDist, path, out len);
        ctx.AddRoute(path);
        ctx.cursor = target;
        Estimate = len / ctx.avgSpeed;
    }

    public override void Begin()
    {
        beacon = MissionVisuals.Beacon(target, S.radius, MissionVisuals.Gold, Label("Destination"));
        R.SetTarget(beacon.transform);
    }

    public override bool Tick(float dt)
    {
        float d = Dist(target);
        R.Hud.SetStatus("Reach " + Label("the destination") + "  •  " + Meters(d));
        R.Hud.SetProgress(-1f, Color.white);
        if (!beacon.Contains(R.PlayerPos)) return false;
        R.Popup(Label("Destination") + " reached!");
        return true;
    }

    public override void End() { if (beacon) Object.Destroy(beacon.gameObject); }
}

// ───────────────────────────── Checkpoints ─────────────────────────────

public class CheckpointsStep : MissionStepRuntime
{
    readonly List<Vector3> points = new List<Vector3>();
    readonly List<MissionBeacon> beacons = new List<MissionBeacon>();
    readonly List<bool> done = new List<bool>();
    int next, taken;
    float slowPopup;

    public override void Prepare(MissionBuildContext ctx)
    {
        Vector3 c = ctx.cursor;
        var path = new List<Vector3>();
        float total = 0f;
        for (int i = 0; i < Mathf.Max(1, S.count); i++)
        {
            Vector3 p = Pick(ctx, c, S.minDist, S.maxDist, path, out float len);
            ctx.AddRoute(path);
            points.Add(p);
            done.Add(false);
            total += len;
            c = p;
        }
        ctx.cursor = c;
        Estimate = total / ctx.avgSpeed * (S.ordered ? 1f : 1.25f);
    }

    public override void Begin()
    {
        for (int i = 0; i < points.Count; i++)
        {
            var b = MissionVisuals.Beacon(points[i], S.radius, MissionVisuals.Gold, Label("Checkpoint") + " " + (i + 1));
            beacons.Add(b);
            if (S.ordered && i > 0) b.gameObject.SetActive(false);
        }
        Refresh();
    }

    void Refresh()
    {
        if (S.ordered)
        {
            for (int i = 0; i < beacons.Count; i++) if (beacons[i]) beacons[i].gameObject.SetActive(i == next || i == next + 1);
            if (next + 1 < beacons.Count && beacons[next + 1]) beacons[next + 1].SetColor(MissionVisuals.Gold * 0.35f);
            if (next < beacons.Count && beacons[next]) { beacons[next].SetColor(MissionVisuals.Gold); R.SetTarget(beacons[next].transform); }
        }
        else
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                if (done[i]) continue;
                float d = Dist(points[i]);
                if (d < bd) { bd = d; best = i; }
            }
            if (best >= 0) R.SetTarget(beacons[best].transform);
        }
    }

    public override bool Tick(float dt)
    {
        if (slowPopup > 0f) slowPopup -= dt;
        for (int i = 0; i < points.Count; i++)
        {
            if (done[i] || (S.ordered && i != next) || beacons[i] == null) continue;
            if (!beacons[i].Contains(R.PlayerPos)) continue;
            if (S.minKmh > 0f && R.PlayerKmh < S.minKmh)
            {
                if (slowPopup <= 0f) { R.Popup("Too slow! Pass above " + Mathf.RoundToInt(S.minKmh) + " km/h"); slowPopup = 2f; }
                continue;
            }
            done[i] = true;
            taken++;
            MissionVisuals.Burst(points[i] + Vector3.up, MissionVisuals.Gold, 30, 8f, 0.8f);
            Object.Destroy(beacons[i].gameObject);
            if (S.ordered) next++;
            if (taken < points.Count) R.Popup(Label("Checkpoint") + " " + taken + "/" + points.Count);
        }
        if (Time.frameCount % 10 == 0) Refresh();

        string speedHint = S.minKmh > 0f ? "  •  above " + Mathf.RoundToInt(S.minKmh) + " km/h" : "";
        R.Hud.SetStatus(Label("Checkpoint") + "s " + taken + "/" + points.Count + speedHint);
        R.Hud.SetProgress((float)taken / points.Count, MissionVisuals.Gold);
        return taken >= points.Count;
    }

    public override void End() { foreach (var b in beacons) if (b) Object.Destroy(b.gameObject); }
}

// ───────────────────────────── Pickup & deliver ─────────────────────────────

public class DeliverStep : MissionStepRuntime
{
    Vector3 pickup;
    readonly List<Vector3> drops = new List<Vector3>();
    MissionBeacon beacon;
    int phase; // 0 = pickup, 1.. = drops
    GameObject decor;

    public override void Prepare(MissionBuildContext ctx)
    {
        var path = new List<Vector3>();
        pickup = Pick(ctx, ctx.cursor, S.minDist * 0.6f, S.maxDist * 0.7f, path, out float total);
        ctx.AddRoute(path);
        decor = Decor(pickup, path.Count > 1 ? MissionWorld.Flat(path[path.Count - 1] - path[path.Count - 2]) : Vector3.forward);
        Vector3 c = pickup;
        for (int i = 0; i < Mathf.Max(1, S.count); i++)
        {
            Vector3 p = Pick(ctx, c, S.minDist, S.maxDist, path, out float len);
            ctx.AddRoute(path);
            drops.Add(p);
            total += len;
            c = p;
        }
        ctx.cursor = c;
        Estimate = total / ctx.avgSpeed;
    }

    public override void Begin() { Show(); }

    void Show()
    {
        if (beacon) Object.Destroy(beacon.gameObject);
        bool pick = phase == 0;
        Vector3 p = pick ? pickup : drops[phase - 1];
        beacon = MissionVisuals.Beacon(p, S.radius, pick ? MissionVisuals.Gold : MissionVisuals.Green, pick ? Label("Pickup") : S.altLabel);
        R.SetTarget(beacon.transform);
    }

    public override bool Tick(float dt)
    {
        bool pick = phase == 0;
        string name = pick ? Label("Pickup") : (string.IsNullOrEmpty(S.altLabel) ? "Drop" : S.altLabel) + (drops.Count > 1 ? " " + phase + "/" + drops.Count : "");
        R.Hud.SetStatus((pick ? "Pick up at " : "Deliver to ") + name + "  •  " + Meters(Dist(beacon.transform)));
        R.Hud.SetProgress(drops.Count > 1 ? (float)Mathf.Max(0, phase - 1) / drops.Count : -1f, MissionVisuals.Green);
        if (!beacon.Contains(R.PlayerPos)) return false;

        if (pick) R.Popup(drops.Count > 1 ? drops.Count + " packages loaded!" : "Package picked up!");
        else R.Popup("Delivered " + phase + "/" + drops.Count);
        MissionVisuals.Burst(beacon.transform.position + Vector3.up, pick ? MissionVisuals.Gold : MissionVisuals.Green, 30, 8f, 0.8f);
        phase++;
        if (phase > drops.Count) return true;
        Show();
        return false;
    }

    public override void End()
    {
        if (beacon) Object.Destroy(beacon.gameObject);
    }
}

// ───────────────────────────── Collect ─────────────────────────────

public class CollectStep : MissionStepRuntime
{
    readonly List<Vector3> items = new List<Vector3>();
    readonly List<MissionBeacon> beacons = new List<MissionBeacon>();
    readonly List<Transform> cells = new List<Transform>();
    int next, taken;

    public override void Prepare(MissionBuildContext ctx)
    {
        var path = new List<Vector3>();
        Vector3 c = ctx.cursor;
        float total = 0f;
        for (int i = 0; i < Mathf.Max(1, S.count); i++)
        {
            Vector3 from = S.ordered ? c : ctx.cursor;
            Vector3 p = Pick(ctx, from, S.minDist * 0.5f, S.maxDist * 0.6f, path, out float len);
            ctx.AddRoute(path);
            items.Add(p);
            total += len;
            c = p;
        }
        ctx.cursor = c;
        Estimate = total / ctx.avgSpeed * (S.ordered ? 1f : 1.4f);
    }

    public override void Begin()
    {
        for (int i = 0; i < items.Count; i++)
        {
            var b = MissionVisuals.Beacon(items[i], S.radius, MissionVisuals.Cyan, Label("Item") + " " + (i + 1), 12f);
            var cell = MissionVisuals.Prim(PrimitiveType.Cube, b.transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.7f, 1.2f, 0.7f), MissionVisuals.P.beamMaterial, MissionVisuals.Cyan, "Cell");
            beacons.Add(b);
            cells.Add(cell.transform);
            if (S.ordered && i > 0) b.gameObject.SetActive(false);
        }
        if (beacons.Count > 0) R.SetTarget(beacons[0].transform);
    }

    public override bool Tick(float dt)
    {
        foreach (var c in cells) if (c) c.Rotate(0f, 120f * dt, 0f);
        for (int i = 0; i < items.Count; i++)
        {
            if (beacons[i] == null || (S.ordered && i != next)) continue;
            if (!beacons[i].Contains(R.PlayerPos)) continue;
            MissionVisuals.Burst(items[i] + Vector3.up * 1.5f, MissionVisuals.Cyan, 35, 7f, 0.7f);
            Object.Destroy(beacons[i].gameObject);
            beacons[i] = null;
            taken++;
            next++;
            if (taken < items.Count) R.Popup(Label("Item") + " " + taken + "/" + items.Count);
            if (S.ordered && next < beacons.Count && beacons[next]) beacons[next].gameObject.SetActive(true);
        }

        // Arrow: next in order, or nearest.
        Transform tgt = null;
        if (S.ordered) { if (next < beacons.Count && beacons[next]) tgt = beacons[next].transform; }
        else
        {
            float bd = float.MaxValue;
            foreach (var b in beacons) if (b) { float d = Dist(b.transform); if (d < bd) { bd = d; tgt = b.transform; } }
        }
        if (tgt) R.SetTarget(tgt);

        R.Hud.SetStatus(Label("Item") + "s " + taken + "/" + items.Count + (tgt ? "  •  " + Meters(Dist(tgt)) : ""));
        R.Hud.SetProgress((float)taken / items.Count, MissionVisuals.Cyan);
        return taken >= items.Count;
    }

    public override void End() { foreach (var b in beacons) if (b) Object.Destroy(b.gameObject); }
}

// ───────────────────────────── Hold zones (hack / park / refuel / burnout) ─────────────────────────────

public class HoldStep : MissionStepRuntime
{
    readonly List<Vector3> zones = new List<Vector3>();
    MissionBeacon beacon;
    int index;
    float progress;

    public override void Prepare(MissionBuildContext ctx)
    {
        var path = new List<Vector3>();
        Vector3 c = ctx.cursor;
        float total = 0f;
        for (int i = 0; i < Mathf.Max(1, S.count); i++)
        {
            Vector3 p = Pick(ctx, c, S.minDist, S.maxDist, path, out float len);
            ctx.AddRoute(path);
            zones.Add(p);
            Decor(p, path.Count > 1 ? MissionWorld.Flat(path[path.Count - 1] - path[path.Count - 2]) : Vector3.forward);
            total += len + S.hold * ctx.avgSpeed + 40f;
            c = p;
        }
        ctx.cursor = c;
        Estimate = total / ctx.avgSpeed;
    }

    public override void Begin() { Show(); }

    void Show()
    {
        if (beacon) Object.Destroy(beacon.gameObject);
        beacon = MissionVisuals.Beacon(zones[index], S.radius, MissionVisuals.Green, Label("Zone"), S.radius < 4f ? 10f : 24f);
        R.SetTarget(beacon.transform);
        progress = 0f;
    }

    public override bool Tick(float dt)
    {
        bool inside = beacon.Contains(R.PlayerPos);
        float kmh = R.PlayerKmh;
        bool slowOk = S.holdMaxSpeedKmh <= 0f || kmh <= S.holdMaxSpeedKmh;
        bool fastOk = S.minKmh <= 0f || kmh >= S.minKmh;
        string name = Label("Zone") + (zones.Count > 1 ? " " + (index + 1) + "/" + zones.Count : "");

        if (inside && slowOk && fastOk) progress += dt / Mathf.Max(0.1f, S.hold);
        else progress = Mathf.Max(0f, progress - dt / Mathf.Max(0.1f, S.hold) * 0.5f);

        string hint;
        if (!inside) hint = "Go to " + name + "  •  " + Meters(Dist(beacon.transform));
        else if (!slowOk) hint = "Slow down below " + Mathf.RoundToInt(S.holdMaxSpeedKmh) + " km/h!";
        else if (!fastOk) hint = "Keep moving above " + Mathf.RoundToInt(S.minKmh) + " km/h!";
        else hint = name + "  •  " + Mathf.RoundToInt(progress * 100f) + "%";
        R.Hud.SetStatus(hint);
        R.Hud.SetProgress(inside ? progress : -1f, MissionVisuals.Green);

        if (progress < 1f) return false;
        MissionVisuals.Burst(zones[index] + Vector3.up, MissionVisuals.Green, 40, 9f, 0.9f);
        index++;
        if (index >= zones.Count) { R.Popup(Label("Zone") + " complete!"); return true; }
        R.Popup(Label("Zone") + " " + index + "/" + zones.Count + " done!");
        Show();
        return false;
    }

    public override void End() { if (beacon) Object.Destroy(beacon.gameObject); }
}

// ───────────────────────────── Follow ─────────────────────────────

public class FollowStep : MissionStepRuntime
{
    List<Vector3> route = new List<Vector3>();
    MissionMover mover;
    bool following;
    float tooClose, tooFar, checkTimer, warnTimer;

    public override void Prepare(MissionBuildContext ctx)
    {
        Pick(ctx, ctx.cursor, S.minDist, S.maxDist, route, out float len);
        ctx.AddRoute(route);
        ctx.cursor = route[route.Count - 1];
        Estimate = len / (S.moverSpeedKmh / 3.6f) * 1.25f + 25f;
    }

    public override void Begin()
    {
        float startAt = Mathf.Min(20f, MissionWorld.Length(route) * 0.2f);
        var trimmed = new List<Vector3>();
        trimmed.Add(MissionWorld.Along(route, startAt, out _));
        float acc = 0f;
        for (int i = 1; i < route.Count; i++)
        {
            acc += Vector3.Distance(route[i - 1], route[i]);
            if (acc > startAt + 1f) trimmed.Add(route[i]);
        }
        if (trimmed.Count < 2) trimmed.Add(route[route.Count - 1]);
        route = trimmed;
        mover = MissionVisuals.Vehicle(S.mover, rng, route[0], Quaternion.identity, Label("Target"));
        mover.SetRoute(route, false);
        mover.mode = MissionMover.Mode.Idle;
        mover.speed = S.moverSpeedKmh / 3.6f;
        R.SetTarget(mover.transform);
    }

    public override bool Tick(float dt)
    {
        float d = Dist(mover.transform);
        if (!following)
        {
            R.Hud.SetStatus("Get behind the " + Label("target") + "  •  " + Meters(d));
            if (d < S.farDist * 0.8f) { following = true; mover.mode = MissionMover.Mode.Route; R.Popup("Follow the " + Label("target") + "!"); }
            return false;
        }

        bool close = d < S.nearDist, far = d > S.farDist;
        tooClose = close ? tooClose + dt : 0f;
        tooFar = far ? tooFar + dt : 0f;
        if (tooClose > 1.5f) { R.Fail("TOO CLOSE! You were spotted by the " + Label("target") + ".", FailReason.AiTooClose); return false; }
        if (tooFar > 3f) { R.Fail("TARGET LOST! You fell too far behind.", FailReason.AiTooFar); return false; }

        // No overtaking (checked 5x per second).
        checkTimer -= dt;
        if (checkTimer <= 0f)
        {
            checkTimer = 0.2f;
            float pp = MissionWorld.Project(route, R.PlayerPos);
            float mp = MissionWorld.Project(route, mover.transform.position);
            if (pp > mp + 4f && d < 25f) { R.Fail("DON'T OVERTAKE! Stay behind the " + Label("target") + ".", FailReason.AiTooClose); return false; }
        }

        warnTimer -= dt;
        if (warnTimer <= 0f && (d < S.nearDist * 1.5f || d > S.farDist * 0.8f))
        {
            warnTimer = 2f;
            R.Popup(d < S.nearDist * 1.5f ? "Too close! Back off." : "Too far! Speed up.");
        }

        Color band = (d < S.nearDist * 1.5f || d > S.farDist * 0.8f) ? MissionVisuals.Orange : MissionVisuals.Green;
        R.Hud.SetStatus("Follow the " + Label("target") + "  •  " + Meters(d));
        R.Hud.SetProgress(mover.RouteLength > 0f ? mover.Travelled / mover.RouteLength : 0f, band);
        return mover.Finished && d < S.farDist;
    }

    public override void End() { if (mover) mover.mode = MissionMover.Mode.Idle; }
}

// ───────────────────────────── Pace (side by side) ─────────────────────────────

public class PaceStep : MissionStepRuntime
{
    readonly List<Vector3> spawns = new List<Vector3>();
    MissionMover mover;
    int index;
    float fill;
    bool rolling;

    public override void Prepare(MissionBuildContext ctx)
    {
        Vector3 c = ctx.cursor;
        var path = new List<Vector3>();
        float total = 0f;
        for (int i = 0; i < Mathf.Max(1, S.count); i++)
        {
            Vector3 p = i == 0 ? Pick(ctx, c, 40f, 90f, path, out float len) : Pick(ctx, c, 120f, 250f, path, out len);
            ctx.AddRoute(path);
            spawns.Add(p);
            total += len + S.duration * 2.5f * (S.moverSpeedKmh / 3.6f);
            c = p;
        }
        ctx.cursor = c;
        Estimate = total / ctx.avgSpeed + 20f * spawns.Count;
    }

    public override void Begin() { Spawn(); }

    void Spawn()
    {
        if (mover) Object.Destroy(mover.gameObject, 6f);
        Vector3 p = spawns[index];
        var route = new List<Vector3>();
        if (!MissionWorld.PickPoint(rng, p, S.minDist, S.maxDist, MissionArea.Any, out _, out _, route))
        { route.Clear(); route.Add(p); route.Add(p + Vector3.forward * 200f); }
        mover = MissionVisuals.Vehicle(S.mover, rng, p, Quaternion.identity, Label("Target"));
        mover.SetRoute(route, false);
        mover.mode = MissionMover.Mode.Idle;
        mover.speed = S.moverSpeedKmh / 3.6f;
        fill = 0f;
        rolling = false;
        R.SetTarget(mover.transform);
    }

    public override bool Tick(float dt)
    {
        float d = Dist(mover.transform);
        string name = Label("target") + (spawns.Count > 1 ? " " + (index + 1) + "/" + spawns.Count : "");
        if (!rolling)
        {
            R.Hud.SetStatus("Reach the " + name + "  •  " + Meters(d));
            R.Hud.SetProgress(-1f, Color.white);
            if (d < 35f) { rolling = true; mover.mode = MissionMover.Mode.Route; R.Popup("Drive beside the " + Label("target") + "!"); }
            return false;
        }

        // Keep the target moving forever.
        if (mover.Finished)
        {
            var route = new List<Vector3>();
            if (MissionWorld.PickPoint(rng, mover.transform.position, S.minDist, S.maxDist, MissionArea.Any, out _, out _, route))
            { mover.SetRoute(route, false); mover.mode = MissionMover.Mode.Route; }
        }

        Vector3 local = mover.transform.InverseTransformPoint(R.PlayerPos);
        bool beside = Mathf.Abs(local.z) < 5.5f && Mathf.Abs(local.x) > 1.6f && Mathf.Abs(local.x) < S.farDist;
        if (beside) fill += dt / Mathf.Max(0.5f, S.duration);
        else fill = Mathf.Max(0f, fill - dt / Mathf.Max(0.5f, S.duration) * 0.35f);

        R.Hud.SetStatus(beside ? "Syncing with " + name + "  •  " + Mathf.RoundToInt(fill * 100f) + "%" : "Get side by side with the " + name + "  •  " + Meters(d));
        R.Hud.SetProgress(fill, beside ? MissionVisuals.Green : MissionVisuals.Orange);

        if (fill < 1f) return false;
        MissionVisuals.Burst(mover.transform.position + Vector3.up * 2f, MissionVisuals.Cyan, 40, 6f, 0.8f);
        index++;
        if (index >= spawns.Count) { R.Popup("Upload complete!"); return true; }
        R.Popup("Done! Next: " + Label("target") + " " + (index + 1));
        Spawn();
        return false;
    }

    public override void End() { if (mover) Object.Destroy(mover.gameObject, 8f); }
}

// ───────────────────────────── Escort ─────────────────────────────

public class EscortStep : MissionStepRuntime
{
    List<Vector3> route = new List<Vector3>();
    MissionMover escort;
    MissionBeacon goal;
    bool rolling;
    float lost;
    readonly List<MissionMover> attackers = new List<MissionMover>();
    readonly List<float> attackTimers = new List<float>();
    float respawn = 6f;
    int spawned;

    public override void Prepare(MissionBuildContext ctx)
    {
        Pick(ctx, ctx.cursor, S.minDist, S.maxDist, route, out float len);
        ctx.AddRoute(route);
        ctx.cursor = route[route.Count - 1];
        Estimate = len / (S.moverSpeedKmh / 3.6f) * 1.4f + 30f;
    }

    public override void Begin()
    {
        float startAt = Mathf.Min(14f, MissionWorld.Length(route) * 0.1f);
        var r = new List<Vector3> { MissionWorld.Along(route, startAt, out _) };
        float acc = 0f;
        for (int i = 1; i < route.Count; i++) { acc += Vector3.Distance(route[i - 1], route[i]); if (acc > startAt + 1f) r.Add(route[i]); }
        if (r.Count < 2) r.Add(route[route.Count - 1]);
        escort = MissionVisuals.Vehicle(S.mover, rng, r[0], Quaternion.identity, Label("Convoy"));
        escort.SetRoute(r, false);
        escort.mode = MissionMover.Mode.Idle;
        escort.speed = S.moverSpeedKmh / 3.6f;
        goal = MissionVisuals.Beacon(route[route.Count - 1], 10f, MissionVisuals.Green, "EscortGoal");
        R.SetTarget(escort.transform);
    }

    public override bool Tick(float dt)
    {
        float d = Dist(escort.transform);
        if (!rolling)
        {
            R.Hud.SetStatus("Join the " + Label("vehicle") + "  •  " + Meters(d));
            if (d < S.farDist * 0.7f) { rolling = true; escort.mode = MissionMover.Mode.Route; R.Popup("Escort the " + Label("vehicle") + "!"); }
            return false;
        }

        escort.SpeedMultiplier = d > S.farDist * 0.75f ? 0.35f : 1f;
        lost = d > S.farDist ? lost + dt : 0f;
        if (lost > 5f) { R.Fail("You left the " + Label("vehicle") + " behind!", FailReason.AiTooFar); return false; }
        if (lost > 0f && Time.frameCount % 60 == 0) R.Popup("Stay close to the " + Label("vehicle") + "!");

        TickAttackers(dt);

        R.Hud.SetStatus("Escort the " + Label("vehicle") + "  •  " + Meters(d) + (S.count > 0 ? "  •  attackers " + attackers.Count : ""));
        R.Hud.SetProgress(escort.RouteLength > 0f ? escort.Travelled / escort.RouteLength : 0f, d > S.farDist * 0.75f ? MissionVisuals.Orange : MissionVisuals.Green);
        return escort.Finished;
    }

    void TickAttackers(float dt)
    {
        if (S.count <= 0) return;
        respawn -= dt;
        if (respawn <= 0f && attackers.Count < S.count && spawned < S.count * 4)
        {
            respawn = 7f;
            Vector3 back = escort.transform.position - escort.transform.forward * 70f;
            if (UnityEngine.AI.NavMesh.SamplePosition(back, out UnityEngine.AI.NavMeshHit h, 30f, UnityEngine.AI.NavMesh.AllAreas))
            {
                var a = MissionVisuals.Vehicle(MoverKind.Car, rng, h.position, escort.transform.rotation, "Attacker");
                a.Place(h.position, escort.transform.forward);
                a.StartChase(escort.transform, S.moverSpeedKmh / 3.6f + 6f);
                a.stopDistance = 1.5f;
                attackers.Add(a);
                attackTimers.Add(0f);
                spawned++;
                R.Popup("Attackers incoming! Block them!");
            }
        }
        for (int i = attackers.Count - 1; i >= 0; i--)
        {
            var a = attackers[i];
            if (a == null) { attackers.RemoveAt(i); attackTimers.RemoveAt(i); continue; }
            bool near = MissionWorld.Flat(a.transform.position - escort.transform.position).magnitude < 6f;
            attackTimers[i] = near ? attackTimers[i] + dt : Mathf.Max(0f, attackTimers[i] - dt);
            if (attackTimers[i] > 2.5f) { R.Fail("The " + Label("vehicle") + " was attacked!", FailReason.RedZone); return; }
        }
    }

    public override bool OnHit(MissionMover mover, float impact)
    {
        int i = attackers.IndexOf(mover);
        if (i < 0) return false;
        MissionVisuals.Burst(mover.transform.position + Vector3.up, MissionVisuals.Orange, 45, 10f, 1.2f);
        Object.Destroy(mover.gameObject);
        attackers.RemoveAt(i);
        attackTimers.RemoveAt(i);
        R.Popup("Attacker knocked out!");
        return true;
    }

    public override void End()
    {
        if (goal) Object.Destroy(goal.gameObject);
        foreach (var a in attackers) if (a) a.mode = MissionMover.Mode.Idle;
        if (escort) escort.mode = MissionMover.Mode.Idle;
    }
}

// ───────────────────────────── Tow ─────────────────────────────

public class TowStep : MissionStepRuntime
{
    Vector3 dest, carPos, carDir;
    MissionBeacon goal;
    GameObject towed;
    MissionMover mover;
    LineRenderer rope;
    readonly List<Vector3> trail = new List<Vector3>();
    float overSpeed;

    public override void Prepare(MissionBuildContext ctx)
    {
        carPos = ctx.cursor;
        var path = new List<Vector3>();
        dest = Pick(ctx, ctx.cursor, S.minDist, S.maxDist, path, out float len);
        ctx.AddRoute(path);
        carDir = path.Count > 1 ? MissionWorld.Flat(path[1] - path[0]) : Vector3.forward;
        // The broken car waits at the previous marker.
        towed = MissionVisuals.ParkedCar(rng, carPos + Vector3.Cross(Vector3.up, carDir.normalized) * 3f, Quaternion.LookRotation(carDir), MoverKind.Car);
        ctx.cursor = dest;
        Estimate = len / Mathf.Min(ctx.avgSpeed, S.maxKmh / 3.6f * 0.7f) + 25f;
    }

    public override void Begin()
    {
        if (towed != null)
        {
            var rb = towed.GetComponent<Rigidbody>();
            if (rb == null) rb = towed.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            mover = towed.AddComponent<MissionMover>();
            mover.mode = MissionMover.Mode.Trail;
            mover.trail = trail;
            mover.trailGap = 7.5f;
            // Being towed: not an obstacle for the player.
            MissionVisuals.SetTagRecursive(towed, "Untagged");
            foreach (var c in towed.GetComponentsInChildren<Collider>()) c.enabled = false;
            var lineGo = new GameObject("TowCable");
            lineGo.transform.SetParent(MissionVisuals.Root, false);
            rope = lineGo.AddComponent<LineRenderer>();
            rope.sharedMaterial = MissionVisuals.P.beamMaterial;
            rope.startWidth = rope.endWidth = 0.12f;
            rope.startColor = rope.endColor = MissionVisuals.Gold;
            rope.positionCount = 2;
        }
        trail.Add(towed != null ? towed.transform.position : carPos);
        trail.Add(R.PlayerPos);
        goal = MissionVisuals.Beacon(dest, S.radius, MissionVisuals.Green, Label("Garage"));
        R.SetTarget(goal.transform);
        R.Popup("Tow cable connected! Drive slowly.");
    }

    public override bool Tick(float dt)
    {
        if (Vector3.Distance(trail[trail.Count - 1], R.PlayerPos) > 1f) { trail.Add(R.PlayerPos); if (trail.Count > 400) trail.RemoveAt(0); }
        if (rope && towed) { rope.SetPosition(0, R.PlayerPos + Vector3.up * 0.6f - R.PlayerForward * 2f); rope.SetPosition(1, towed.transform.position + Vector3.up * 0.6f); }

        float limit = S.maxKmh > 0f ? S.maxKmh : 45f;
        overSpeed = R.PlayerKmh > limit ? overSpeed + dt : 0f;
        if (overSpeed > 2.5f) { R.Fail("TOW CABLE SNAPPED! Keep below " + Mathf.RoundToInt(limit) + " km/h.", FailReason.RedZone); return false; }

        R.Hud.SetStatus((R.PlayerKmh > limit ? "TOO FAST! " : "") + "Tow to " + Label("the garage") + "  •  " + Meters(Dist(dest)));
        R.Hud.SetProgress(Mathf.Clamp01(R.PlayerKmh / limit), R.PlayerKmh > limit ? MissionVisuals.Red : MissionVisuals.Green);
        return goal.Contains(R.PlayerPos);
    }

    public override void End()
    {
        if (goal) Object.Destroy(goal.gameObject);
        if (rope) Object.Destroy(rope.gameObject);
        if (mover) mover.mode = MissionMover.Mode.Idle;
    }
}

// ───────────────────────────── Evade (police chase) ─────────────────────────────

public class EvadeStep : MissionStepRuntime
{
    Vector3 safe;
    bool hasSafe;
    MissionBeacon goal;
    readonly List<MissionMover> pursuers = new List<MissionMover>();
    float elapsed, caught, warn;

    public override void Prepare(MissionBuildContext ctx)
    {
        hasSafe = S.maxDist > 0f;
        float len = 0f;
        if (hasSafe)
        {
            var path = new List<Vector3>();
            safe = Pick(ctx, ctx.cursor, S.minDist, S.maxDist, path, out len);
            ctx.AddRoute(path);
            ctx.cursor = safe;
        }
        else
        {
            // Give props / hazards a route to work with.
            var path = new List<Vector3>();
            Pick(ctx, ctx.cursor, 250f, 400f, path, out _);
            ctx.AddRoute(path);
        }
        Estimate = hasSafe ? len / ctx.avgSpeed * 1.3f + 15f : S.duration + 20f;
    }

    public override void Begin()
    {
        if (hasSafe)
        {
            goal = MissionVisuals.Beacon(safe, S.radius, MissionVisuals.Green, Label("Safe Zone"));
            R.SetTarget(goal.transform);
        }
        int n = Mathf.Max(1, S.count);
        for (int i = 0; i < n; i++)
        {
            Vector3 back = R.PlayerPos - R.PlayerForward * (45f + i * 14f) + Vector3.Cross(Vector3.up, R.PlayerForward) * ((i % 2 == 0 ? 1f : -1f) * 3f);
            if (!UnityEngine.AI.NavMesh.SamplePosition(back, out UnityEngine.AI.NavMeshHit h, 30f, UnityEngine.AI.NavMesh.AllAreas)) h.position = back;
            var m = MissionVisuals.Vehicle(S.mover, rng, h.position, Quaternion.identity, "Pursuer" + i);
            m.Place(h.position, R.PlayerPos - h.position);
            m.StartChase(R.PlayerTransform, S.moverSpeedKmh / 3.6f * (0.9f + 0.05f * i));
            m.acceleration = 7f;
            pursuers.Add(m);
        }
        R.Popup(S.mover == MoverKind.Police ? "POLICE! Lose them!" : "They're coming! Run!");
    }

    public override bool Tick(float dt)
    {
        elapsed += dt;
        float nearest = float.MaxValue;
        foreach (var p in pursuers) if (p) nearest = Mathf.Min(nearest, Dist(p.transform));

        caught = nearest < 5.5f ? caught + dt : Mathf.Max(0f, caught - dt * 0.5f);
        if (caught > 2.5f) { R.Fail(S.mover == MoverKind.Police ? "BUSTED! The police caught you." : "CAUGHT! They boxed you in.", FailReason.RedZone); return false; }
        warn -= dt;
        if (nearest < 12f && warn <= 0f) { warn = 2.5f; R.Popup("They're right behind you!"); }

        string who = S.mover == MoverKind.Police ? "Police" : "Pursuers";
        if (hasSafe)
        {
            R.Hud.SetStatus("Reach " + Label("the safe zone") + "  •  " + Meters(Dist(safe)) + "  •  " + who + " " + Meters(nearest));
            R.Hud.SetProgress(Mathf.Clamp01(caught / 2.5f), MissionVisuals.Red);
            return goal.Contains(R.PlayerPos);
        }
        float left = Mathf.Max(0f, S.duration - elapsed);
        R.Hud.SetStatus("Survive " + Mathf.CeilToInt(left) + " s  •  " + who + " " + Meters(nearest));
        R.Hud.SetProgress(elapsed / Mathf.Max(1f, S.duration), nearest < 15f ? MissionVisuals.Red : MissionVisuals.Green);
        return elapsed >= S.duration;
    }

    public override void End()
    {
        if (goal) Object.Destroy(goal.gameObject);
        foreach (var p in pursuers) if (p) p.mode = MissionMover.Mode.Idle;
    }
}

// ───────────────────────────── Race ─────────────────────────────

public class RaceStep : MissionStepRuntime
{
    readonly List<Vector3> cps = new List<Vector3>();
    readonly List<float> cpDist = new List<float>(); // cumulative distance at each checkpoint
    readonly List<Vector3> lap = new List<Vector3>();
    readonly List<MissionMover> opponents = new List<MissionMover>();
    readonly List<float> gridOffset = new List<float>();
    MissionBeacon current, preview;
    int passed;
    float lapLen;
    int laps, total;

    public override void Prepare(MissionBuildContext ctx)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(S.duration), 3, 10);
        laps = Mathf.Max(1, S.hits);
        Vector3 c = ctx.start;
        var path = new List<Vector3>();
        lap.Add(c);
        for (int i = 0; i < n; i++)
        {
            Vector3 p;
            if (i == n - 1)
            {
                p = ctx.start;
                if (!MissionWorld.TryPath(c, p, path, out _)) { path.Clear(); path.Add(c); path.Add(p); }
            }
            else
            {
                // Spiral around the start so the loop closes.
                Vector3 away = MissionWorld.Flat(c - ctx.start);
                Vector3? avoid = i > n / 2 && away.sqrMagnitude > 1f ? (Vector3?)away.normalized : null;
                p = Pick(ctx, c, S.minDist, S.maxDist, path, out _, avoid);
            }
            for (int k = 1; k < path.Count; k++) lap.Add(path[k]);
            cps.Add(p);
            c = p;
        }
        lapLen = MissionWorld.Length(lap);
        float acc = 0f;
        int cpIndex = 0;
        for (int i = 1; i < lap.Count && cpIndex < cps.Count; i++)
        {
            acc += Vector3.Distance(lap[i - 1], lap[i]);
            if ((lap[i] - cps[cpIndex]).sqrMagnitude < 0.01f) { cpDist.Add(acc); cpIndex++; }
        }
        while (cpDist.Count < cps.Count) cpDist.Add(lapLen * (cpDist.Count + 1) / cps.Count);
        ctx.AddRoute(lap);
        ctx.cursor = ctx.start;
        total = cps.Count * laps;
        Estimate = lapLen * laps / 12f + 25f;
    }

    public override void Begin()
    {
        int n = Mathf.Max(0, S.count);
        Vector3 fwd = R.PlayerForward;
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        for (int i = 0; i < n; i++)
        {
            float back = 7f + 7f * (i / 2);
            float lat = (i % 2 == 0 ? 1f : -1f) * 3.2f;
            Vector3 p = R.PlayerPos - fwd * back + side * lat;
            var m = MissionVisuals.Vehicle(S.mover, rng, p, Quaternion.LookRotation(fwd), "Racer" + (i + 1));
            var route = new List<Vector3> { p };
            for (int k = 1; k < lap.Count; k++) route.Add(lap[k]);
            m.SetRoute(route, true);
            m.speed = S.moverSpeedKmh / 3.6f * Mathf.Lerp(0.82f, 1f, n > 1 ? (float)i / (n - 1) : 1f);
            m.acceleration = 4.5f;
            opponents.Add(m);
            gridOffset.Add(back);
        }
        ShowNext();
        R.Popup("GO! GO! GO!");
    }

    void ShowNext()
    {
        if (current) Object.Destroy(current.gameObject);
        if (preview) Object.Destroy(preview.gameObject);
        if (passed >= total) return;
        int i = passed % cps.Count;
        bool finish = passed == total - 1;
        current = MissionVisuals.Beacon(cps[i], S.radius, finish ? MissionVisuals.Green : MissionVisuals.Gold, finish ? "Finish" : "RaceCP");
        if (passed + 1 < total) { preview = MissionVisuals.Beacon(cps[(i + 1) % cps.Count], S.radius, MissionVisuals.Gold * 0.3f, "RacePreview"); }
        R.SetTarget(current.transform);
    }

    float PlayerProgress()
    {
        int lapsDone = passed / cps.Count;
        int i = passed % cps.Count;
        float prev = i == 0 ? 0f : cpDist[i - 1];
        float seg = cpDist[i] - prev;
        float remain = Dist(cps[i]);
        return lapsDone * lapLen + prev + Mathf.Clamp(seg - remain, 0f, seg);
    }

    int Rank()
    {
        float me = PlayerProgress();
        int rank = 1;
        for (int i = 0; i < opponents.Count; i++)
            if (opponents[i] && opponents[i].Travelled - gridOffset[i] > me) rank++;
        return rank;
    }

    public override bool Tick(float dt)
    {
        // Stop opponents that finished.
        float raceLen = lapLen * laps;
        for (int i = 0; i < opponents.Count; i++)
            if (opponents[i] && opponents[i].Travelled - gridOffset[i] >= raceLen) opponents[i].mode = MissionMover.Mode.Idle;

        if (current && current.Contains(R.PlayerPos))
        {
            passed++;
            if (passed >= total)
            {
                int rank = Rank();
                if (rank > S.requiredRank)
                {
                    R.Fail("You finished " + Ordinal(rank) + ". You needed top " + S.requiredRank + ".", FailReason.RedZone);
                    return false;
                }
                R.Popup("You finished " + Ordinal(rank) + "!");
                return true;
            }
            if (passed % cps.Count == 0) R.Popup("Lap " + (passed / cps.Count + 1) + "/" + laps);
            ShowNext();
        }

        int lapNow = Mathf.Min(laps, passed / cps.Count + 1);
        int r = Rank();
        R.Hud.SetStatus("Lap " + lapNow + "/" + laps + "  •  CP " + (passed % cps.Count + 1) + "/" + cps.Count + "  •  Position " + r + "/" + (opponents.Count + 1));
        R.Hud.SetProgress((float)passed / total, r <= S.requiredRank ? MissionVisuals.Green : MissionVisuals.Orange);
        return false;
    }

    static string Ordinal(int n) => n + (n == 1 ? "st" : n == 2 ? "nd" : n == 3 ? "rd" : "th");

    public override void End()
    {
        if (current) Object.Destroy(current.gameObject);
        if (preview) Object.Destroy(preview.gameObject);
        foreach (var o in opponents) if (o) o.mode = MissionMover.Mode.Idle;
    }
}

// ───────────────────────────── Speed trial ─────────────────────────────

public class SpeedTrialStep : MissionStepRuntime
{
    Vector3 end;
    MissionBeacon goal;
    Vector3 last;
    float odometer, top;

    public override bool ChooseStart(MissionBuildContext ctx) => StraightStrip.Find(ctx, Mathf.Max(200f, S.maxDist) + 40f, out _);

    public override void Prepare(MissionBuildContext ctx)
    {
        end = ctx.start + ctx.forcedHeading * Mathf.Max(200f, S.maxDist);
        end.y = MissionWorld.GroundY(end, ctx.start.y);
        ctx.AddRoute(new List<Vector3> { ctx.start, end });
        ctx.cursor = end;
        Estimate = S.maxDist / 12f + 25f;
    }

    public override void Begin()
    {
        goal = MissionVisuals.Beacon(end, 12f, MissionVisuals.Red, "SpeedTrapEnd");
        R.SetTarget(goal.transform);
        last = R.PlayerPos;
    }

    public override bool Tick(float dt)
    {
        odometer += MissionWorld.Flat(R.PlayerPos - last).magnitude;
        last = R.PlayerPos;
        top = Mathf.Max(top, R.PlayerKmh);
        R.Hud.SetStatus("Speed " + Mathf.RoundToInt(R.PlayerKmh) + " / " + Mathf.RoundToInt(S.minKmh) + " km/h  •  " + Meters(Mathf.Max(0f, S.maxDist - odometer)) + " left");
        R.Hud.SetProgress(R.PlayerKmh / S.minKmh, R.PlayerKmh >= S.minKmh * 0.85f ? MissionVisuals.Green : MissionVisuals.Gold);
        if (R.PlayerKmh >= S.minKmh) { R.Popup("TOP SPEED " + Mathf.RoundToInt(R.PlayerKmh) + " km/h!"); return true; }
        if (odometer > S.maxDist * 1.3f || goal.Contains(R.PlayerPos))
            R.Fail("TOO SLOW! Top speed " + Mathf.RoundToInt(top) + " km/h, needed " + Mathf.RoundToInt(S.minKmh) + ".", FailReason.RedZone);
        return false;
    }

    public override void End() { if (goal) Object.Destroy(goal.gameObject); }
}

// ───────────────────────────── Speed band ─────────────────────────────

public class SpeedBandStep : MissionStepRuntime
{
    MissionBeacon guide;
    float progress, outside, warm = 8f;

    public override void Prepare(MissionBuildContext ctx)
    {
        var path = new List<Vector3>();
        Pick(ctx, ctx.cursor, 400f, 700f, path, out _);
        ctx.AddRoute(path);
        Estimate = S.duration * 1.6f + 25f;
    }

    public override void Begin() { NewGuide(); }

    void NewGuide()
    {
        if (guide) Object.Destroy(guide.gameObject);
        MissionWorld.PickPoint(rng, R.PlayerPos, 400f, 700f, MissionArea.Any, out Vector3 p, out _, null, -R.PlayerForward);
        guide = MissionVisuals.Beacon(p, 12f, MissionVisuals.Cyan, "BandGuide");
        R.SetTarget(guide.transform);
    }

    public override bool Tick(float dt)
    {
        float kmh = R.PlayerKmh;
        bool ok = kmh >= S.minKmh && kmh <= S.maxKmh;
        if (warm > 0f) warm -= dt;
        if (ok) { progress += dt / Mathf.Max(1f, S.duration); outside = 0f; }
        else if (warm <= 0f)
        {
            outside += dt;
            if (outside > 3f) { R.Fail("DATA TRANSFER LOST! Keep " + Mathf.RoundToInt(S.minKmh) + "-" + Mathf.RoundToInt(S.maxKmh) + " km/h.", FailReason.RedZone); return false; }
        }
        if (guide && guide.Contains(R.PlayerPos)) NewGuide();

        string state = ok ? "STABLE" : (kmh < S.minKmh ? "TOO SLOW" : "TOO FAST");
        R.Hud.SetStatus(state + "  •  " + Mathf.RoundToInt(kmh) + " km/h  (" + Mathf.RoundToInt(S.minKmh) + "-" + Mathf.RoundToInt(S.maxKmh) + ")  •  " + Mathf.RoundToInt(progress * 100f) + "%");
        R.Hud.SetProgress(progress, ok ? MissionVisuals.Green : MissionVisuals.Red);
        return progress >= 1f;
    }

    public override void End() { if (guide) Object.Destroy(guide.gameObject); }
}

// ───────────────────────────── Survive ─────────────────────────────

public class SurviveStep : MissionStepRuntime
{
    float elapsed;

    public override void Prepare(MissionBuildContext ctx)
    {
        var path = new List<Vector3>();
        Pick(ctx, ctx.cursor, 300f, 500f, path, out _);
        ctx.AddRoute(path);
        Estimate = S.duration + 25f;
    }

    public override void Begin() { R.SetTarget(null); }

    public override bool Tick(float dt)
    {
        elapsed += dt;
        float left = Mathf.Max(0f, S.duration - elapsed);
        R.Hud.SetStatus(Label("Survive") + "  •  " + Mathf.CeilToInt(left) + " s");
        R.Hud.SetProgress(elapsed / Mathf.Max(1f, S.duration), MissionVisuals.Cyan);
        return elapsed >= S.duration;
    }
}

// ───────────────────────────── Ram targets ─────────────────────────────

public class RamStep : MissionStepRuntime
{
    readonly List<Vector3> spawns = new List<Vector3>();
    readonly List<MissionMover> targets = new List<MissionMover>();
    readonly List<int> hitsLeft = new List<int>();
    int disabled;

    public override void Prepare(MissionBuildContext ctx)
    {
        var path = new List<Vector3>();
        float total = 0f;
        for (int i = 0; i < Mathf.Max(1, S.count); i++)
        {
            Vector3 p = Pick(ctx, ctx.cursor, S.minDist, S.maxDist, path, out float len);
            ctx.AddRoute(path);
            spawns.Add(p);
            total += len;
        }
        Estimate = total / ctx.avgSpeed + 30f * spawns.Count * Mathf.Max(1, S.hits);
    }

    public override void Begin()
    {
        foreach (var p in spawns)
        {
            var m = MissionVisuals.Vehicle(S.mover, rng, p, Quaternion.identity, Label("Target"));
            var loop = new List<Vector3>();
            if (MissionWorld.PickPoint(rng, p, 150f, 300f, MissionArea.Any, out Vector3 far, out _, loop))
            {
                var back = new List<Vector3>();
                if (MissionWorld.TryPath(far, p, back, out _)) for (int k = 1; k < back.Count; k++) loop.Add(back[k]);
            }
            else { loop.Clear(); loop.Add(p); loop.Add(p + Vector3.forward * 60f); loop.Add(p); }
            m.SetRoute(loop, true);
            m.speed = S.moverSpeedKmh / 3.6f;
            targets.Add(m);
            hitsLeft.Add(Mathf.Max(1, S.hits));
            var mark = MissionVisuals.Prim(PrimitiveType.Sphere, m.transform, new Vector3(0f, 4f, 0f), Vector3.one * 0.9f, MissionVisuals.P.beamMaterial, MissionVisuals.Red, "TargetMark");
            mark.transform.localScale = Vector3.one * 0.9f;
        }
    }

    public override bool OnHit(MissionMover mover, float impact)
    {
        int i = targets.IndexOf(mover);
        if (i < 0) return false;
        if (hitsLeft[i] <= 0) return true;
        if (impact < 6f) { R.Popup("Hit harder!"); return true; }
        hitsLeft[i]--;
        MissionVisuals.Burst(mover.transform.position + Vector3.up, MissionVisuals.Orange, 35, 9f, 1f);
        if (hitsLeft[i] <= 0)
        {
            disabled++;
            mover.mode = MissionMover.Mode.Idle;
            var mark = mover.transform.Find("TargetMark");
            if (mark) Object.Destroy(mark.gameObject);
            R.Popup(Label("Target") + " disabled! " + disabled + "/" + targets.Count);
        }
        else
        {
            mover.SpeedMultiplier = 1.15f;
            R.Popup("HIT! " + hitsLeft[i] + " more");
        }
        return true;
    }

    public override bool Tick(float dt)
    {
        Transform nearest = null;
        float bd = float.MaxValue;
        for (int i = 0; i < targets.Count; i++)
        {
            if (!targets[i] || hitsLeft[i] <= 0) continue;
            float d = Dist(targets[i].transform);
            if (d < bd) { bd = d; nearest = targets[i].transform; }
        }
        R.SetTarget(nearest);
        R.Hud.SetStatus(Label("Target") + "s disabled " + disabled + "/" + targets.Count + (nearest ? "  •  " + Meters(bd) : ""));
        R.Hud.SetProgress((float)disabled / targets.Count, MissionVisuals.Red);
        return disabled >= targets.Count;
    }
}

// ───────────────────────────── Ramp jump ─────────────────────────────

public class JumpStep : MissionStepRuntime
{
    const float RunUp = 90f, RampLen = 12f, RampHeight = 2.6f, LandAt = 34f, LandRadius = 11f;
    Vector3 origin, dir;
    MissionBeacon landing;
    bool armed = true, airborne;
    float rampSpeed, airTime;

    public override bool ChooseStart(MissionBuildContext ctx) => StraightStrip.Find(ctx, RunUp + RampLen + LandAt + LandRadius + 20f, out _);

    public override void Prepare(MissionBuildContext ctx)
    {
        origin = ctx.start;
        dir = ctx.forcedHeading;
        Vector3 rampPos = origin + dir * RunUp;
        rampPos.y = MissionWorld.GroundY(rampPos, origin.y);
        MissionVisuals.Ramp(rampPos, Quaternion.LookRotation(dir), RampLen, 6f, RampHeight);
        Vector3 land = origin + dir * (RunUp + RampLen + LandAt);
        land.y = MissionWorld.GroundY(land, origin.y);
        ctx.AddRoute(new List<Vector3> { origin, land });
        ctx.cursor = land;
        landingPos = land;
        Estimate = 45f;
    }

    Vector3 landingPos;

    public override void Begin()
    {
        landing = MissionVisuals.Beacon(landingPos, LandRadius, MissionVisuals.Green, "LandingZone", 8f);
        R.SetTarget(landing.transform);
    }

    public override bool Tick(float dt)
    {
        Vector3 rel = R.PlayerPos - origin;
        float s = Vector3.Dot(rel, dir);
        float lateral = Vector3.Dot(rel, Vector3.Cross(Vector3.up, dir));

        if (!airborne)
        {
            if (!armed && s < RunUp - 20f) armed = true;
            R.Hud.SetStatus("Hit the ramp at " + Mathf.RoundToInt(S.minKmh) + "+ km/h  •  " + Mathf.RoundToInt(R.PlayerKmh) + " km/h");
            R.Hud.SetProgress(R.PlayerKmh / S.minKmh, R.PlayerKmh >= S.minKmh ? MissionVisuals.Green : MissionVisuals.Gold);
            if (armed && s > RunUp + RampLen - 0.5f)
            {
                armed = false;
                if (Mathf.Abs(lateral) > 3.6f) { R.Popup("Use the ramp! Go back and try again."); return false; }
                rampSpeed = R.PlayerKmh;
                if (rampSpeed < S.minKmh) { R.Fail("TOO SLOW for the jump! " + Mathf.RoundToInt(rampSpeed) + " km/h, needed " + Mathf.RoundToInt(S.minKmh) + ".", FailReason.RedZone); return false; }
                airborne = true;
                airTime = 0f;
                R.Popup("AIRBORNE!");
            }
            return false;
        }

        airTime += dt;
        R.Hud.SetStatus("Land in the zone!");
        if (airTime > 0.4f && R.Grounded)
        {
            if (landing.Contains(R.PlayerPos, 1f)) { R.Popup("PERFECT LANDING! " + Mathf.RoundToInt(rampSpeed) + " km/h"); return true; }
            R.Fail("MISSED THE LANDING ZONE!", FailReason.RedZone);
        }
        else if (airTime > 6f) R.Fail("MISSED THE LANDING ZONE!", FailReason.RedZone);
        return false;
    }

    public override void End() { if (landing) Object.Destroy(landing.gameObject); }
}

/// <summary>Finds a long, flat, obstacle-free straight for ramps and speed traps.</summary>
public static class StraightStrip
{
    public static bool Find(MissionBuildContext ctx, float length, out Vector3 start)
    {
        start = Vector3.zero;
        for (int t = 0; t < 160; t++)
        {
            MissionArea area = t < 100 ? ctx.area : MissionArea.Any;
            Vector3 p;
            if (!MissionWorld.FindAnchor(ctx.rng, area == MissionArea.Any ? MissionArea.Road : area, out p))
            {
                if (!MissionWorld.PickPoint(ctx.rng, ctx.start, 20f, 600f, area, out p, out _)) continue;
            }
            for (int k = 0; k < 8; k++)
            {
                float a = (k * 45f + (float)ctx.rng.NextDouble() * 20f) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (Clear(p, d, length))
                {
                    start = p;
                    ctx.forcedStart = p;
                    ctx.forcedHeading = d;
                    return true;
                }
            }
        }
        return false;
    }

    static bool Clear(Vector3 p, Vector3 d, float length)
    {
        if (Physics.SphereCast(p + Vector3.up * 1.8f, 2.4f, d, out _, length, MissionWorld.Mask, QueryTriggerInteraction.Ignore)) return false;
        for (float s = 8f; s <= length; s += 8f)
        {
            Vector3 q = p + d * s;
            if (!MissionWorld.GroundHit(q + Vector3.up * 4f, 9f, out RaycastHit h)) return false;
            if (h.collider.CompareTag("Obstacles") || h.normal.y < 0.97f) return false;
            if (Mathf.Abs(h.point.y - p.y) > 0.8f) return false;
        }
        return true;
    }
}

// ───────────────────────────── Choice (two endings) ─────────────────────────────

public class ChoiceStep : MissionStepRuntime
{
    Vector3 a, b;
    MissionBeacon ba, bb;

    public override void Prepare(MissionBuildContext ctx)
    {
        var path = new List<Vector3>();
        a = Pick(ctx, ctx.cursor, S.minDist, S.maxDist, path, out float la);
        ctx.AddRoute(path);
        Vector3 away = MissionWorld.Flat(a - ctx.cursor).normalized;
        b = Pick(ctx, ctx.cursor, S.minDist, S.maxDist, path, out float lb, away);
        ctx.AddRoute(path);
        Estimate = Mathf.Max(la, lb) / ctx.avgSpeed + 20f;
    }

    public override void Begin()
    {
        ba = MissionVisuals.Beacon(a, S.radius, MissionVisuals.Green, "EndingA", 40f);
        bb = MissionVisuals.Beacon(b, S.radius, MissionVisuals.Purple, "EndingB", 40f);
    }

    public override bool Tick(float dt)
    {
        float da = Dist(a), db = Dist(b);
        R.SetTarget(da < db ? ba.transform : bb.transform);
        R.Hud.SetStatus("GREEN: " + S.label + " " + Meters(da) + "   |   PURPLE: " + S.altLabel + " " + Meters(db));
        R.Hud.SetProgress(-1f, Color.white);
        if (ba.Contains(R.PlayerPos)) { R.ChoseAlternate = false; return true; }
        if (bb.Contains(R.PlayerPos)) { R.ChoseAlternate = true; return true; }
        return false;
    }

    public override void End()
    {
        if (ba) Object.Destroy(ba.gameObject);
        if (bb) Object.Destroy(bb.gameObject);
    }
}
