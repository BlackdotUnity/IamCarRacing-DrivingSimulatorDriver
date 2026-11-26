using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kinematic mission vehicle. Drives along NavMesh routes, chases a target, or trails behind
/// the player (tow). Cheaper and far more predictable than a full RCC AI car, and it never
/// registers itself as a player vehicle.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MissionMover : MonoBehaviour
{
    public enum Mode { Idle, Route, Chase, Trail }

    public MoverKind kind;
    public Mode mode = Mode.Idle;
    [Tooltip("Cruise speed in m/s.")]
    public float speed = 12f;
    public float acceleration = 5f;
    public bool loop;

    [Header("Chase")]
    public Transform chaseTarget;
    public float repathInterval = 0.8f;
    public float stopDistance = 3.5f;

    public List<Vector3> route = new List<Vector3>();
    public float CurrentSpeed { get; private set; }
    public float Travelled { get; private set; }
    public float RouteLength { get; private set; }
    public bool Finished { get; private set; }
    public int Laps { get; private set; }
    public float SpeedMultiplier { get; set; } = 1f;

    // Trail (tow)
    public List<Vector3> trail;
    public float trailGap = 7f;

    Rigidbody rb;
    int index = 1;
    float repathTimer, stunTimer;
    readonly List<Vector3> chasePath = new List<Vector3>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void SetRoute(List<Vector3> pts, bool loopRoute)
    {
        route = new List<Vector3>(pts);
        loop = loopRoute;
        index = 1;
        Travelled = 0f;
        Laps = 0;
        Finished = false;
        RouteLength = MissionWorld.Length(route);
        mode = Mode.Route;
        if (route.Count > 1)
        {
            Vector3 d = MissionWorld.Flat(route[1] - route[0]);
            Place(route[0], d);
        }
    }

    public void Place(Vector3 pos, Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
        pos.y = MissionWorld.GroundY(pos, pos.y);
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(forward.normalized));
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.position = pos;
        rb.rotation = transform.rotation;
    }

    public void StartChase(Transform target, float chaseSpeed)
    {
        chaseTarget = target;
        speed = chaseSpeed;
        mode = Mode.Chase;
        repathTimer = 0f;
    }

    public void Stun(float seconds)
    {
        stunTimer = Mathf.Max(stunTimer, seconds);
        CurrentSpeed = 0f;
    }

    public bool Stunned => stunTimer > 0f;

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (!MissionRunner.Running || mode == Mode.Idle)
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, acceleration * 2f * dt);
            return;
        }
        if (stunTimer > 0f)
        {
            stunTimer -= dt;
            return;
        }

        Vector3 pos = rb.position;
        Vector3 target;
        float desired = speed * SpeedMultiplier;

        switch (mode)
        {
            case Mode.Route:
                if (Finished || route.Count < 2) { desired = 0f; target = pos + transform.forward; break; }
                target = route[index];
                if (MissionWorld.Flat(target - pos).magnitude < 3.5f)
                {
                    index++;
                    if (index >= route.Count)
                    {
                        if (loop) { index = 0; Laps++; }
                        else { Finished = true; index = route.Count - 1; }
                    }
                    target = route[index];
                }
                desired *= CornerFactor(pos, index, route);
                break;

            case Mode.Chase:
                if (chaseTarget == null) { desired = 0f; target = pos + transform.forward; break; }
                repathTimer -= dt;
                if (repathTimer <= 0f)
                {
                    repathTimer = repathInterval;
                    if (!MissionWorld.TryPath(pos, chaseTarget.position, chasePath, out _))
                    {
                        chasePath.Clear();
                        chasePath.Add(pos);
                        chasePath.Add(chaseTarget.position);
                    }
                    index = 1;
                }
                float toTarget = MissionWorld.Flat(chaseTarget.position - pos).magnitude;
                if (toTarget < 25f || chasePath.Count < 2) target = chaseTarget.position;
                else
                {
                    if (index < chasePath.Count && MissionWorld.Flat(chasePath[index] - pos).magnitude < 4f) index++;
                    target = chasePath[Mathf.Min(index, chasePath.Count - 1)];
                }
                if (toTarget < stopDistance + 4f)
                {
                    // Close in, but never ram at full speed.
                    var tb = chaseTarget.GetComponentInParent<Rigidbody>();
                    float ts = tb != null ? tb.linearVelocity.magnitude : 0f;
                    desired = Mathf.Min(desired, ts + 1.5f);
                    if (toTarget < stopDistance) desired = Mathf.Min(desired, ts * 0.8f);
                }
                else if (toTarget > 140f) desired *= 1.25f; // rubber band
                if (index < chasePath.Count) desired *= CornerFactor(pos, index, chasePath);
                break;

            case Mode.Trail:
                if (trail == null || trail.Count == 0) { desired = 0f; target = pos + transform.forward; break; }
                // Point `trailGap` metres behind the newest breadcrumb.
                float acc = 0f;
                target = trail[trail.Count - 1];
                for (int i = trail.Count - 1; i > 0; i--)
                {
                    acc += Vector3.Distance(trail[i], trail[i - 1]);
                    if (acc >= trailGap) { target = trail[i - 1]; break; }
                }
                float gap = MissionWorld.Flat(target - pos).magnitude;
                desired = Mathf.Clamp(gap * 2.2f, 0f, 30f);
                break;

            default:
                return;
        }

        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, desired, acceleration * dt * (desired < CurrentSpeed ? 2f : 1f));

        Vector3 to = MissionWorld.Flat(target - pos);
        Quaternion rot = rb.rotation;
        if (to.sqrMagnitude > 0.05f)
        {
            Quaternion want = Quaternion.LookRotation(to.normalized);
            float turnRate = Mathf.Lerp(140f, 70f, Mathf.InverseLerp(5f, 30f, CurrentSpeed));
            rot = Quaternion.RotateTowards(Quaternion.Euler(0f, rot.eulerAngles.y, 0f), want, turnRate * dt);
        }

        Vector3 step = rot * Vector3.forward * CurrentSpeed * dt;
        Vector3 next = pos + step;
        next.y = Mathf.Lerp(pos.y, MissionWorld.GroundY(next, pos.y), 0.35f);
        rb.MovePosition(next);
        rb.MoveRotation(rot);
        Travelled += step.magnitude;
    }

    static float CornerFactor(Vector3 pos, int index, List<Vector3> pts)
    {
        if (index <= 0 || index >= pts.Count - 1) return 1f;
        float dist = MissionWorld.Flat(pts[index] - pos).magnitude;
        if (dist > 30f) return 1f;
        Vector3 a = MissionWorld.Flat(pts[index] - pts[index - 1]).normalized;
        Vector3 b = MissionWorld.Flat(pts[index + 1] - pts[index]).normalized;
        float angle = Vector3.Angle(a, b);
        float slow = Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(20f, 100f, angle));
        return Mathf.Lerp(slow, 1f, Mathf.InverseLerp(8f, 30f, dist));
    }

    /// <summary>Distance along the current route, including completed laps.</summary>
    public float Progress => Laps * RouteLength + (route.Count > 1 ? MissionWorld.Project(route, rb != null ? rb.position : transform.position) : 0f);
}
