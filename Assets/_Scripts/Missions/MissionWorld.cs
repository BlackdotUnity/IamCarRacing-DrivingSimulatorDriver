using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Finds drivable, reachable points in the city using the baked NavMesh.
/// Every query is seeded by the level number, so a level is laid out the same way every time.
/// </summary>
public static class MissionWorld
{
    /// <summary>Everything except Ignore Raycast (2) and UI (5).</summary>
    public const int Mask = ~((1 << 2) | (1 << 5));

    static readonly Dictionary<int, MissionArea> areaCache = new Dictionary<int, MissionArea>();
    static readonly RaycastHit[] hits = new RaycastHit[16];
    static readonly Collider[] overlap = new Collider[16];
    static NavMeshPath scratchPath;
    static List<Collider> sandFloors, grassFloors, roadFloors;
    static Transform ignoreRoot;

    /// <summary>Called when a level is built: caches hold colliders of the previous scene load.</summary>
    public static void ResetCaches() => ResetStatics();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        areaCache.Clear();
        scratchPath = null;
        sandFloors = grassFloors = roadFloors = null;
        ignoreRoot = null;
    }

    /// <summary>Colliders under this root (the player car) are ignored by clearance checks.</summary>
    public static void SetIgnoreRoot(Transform root) => ignoreRoot = root;

    static NavMeshPath Scratch => scratchPath ?? (scratchPath = new NavMeshPath());

    // ───────────────────────── surface classification ─────────────────────────

    public static MissionArea Classify(Collider c)
    {
        if (c == null) return MissionArea.Any;
        int id = c.GetInstanceID();
        if (areaCache.TryGetValue(id, out var cached)) return cached;

        MissionArea result = MissionArea.Any;
        var r = c.GetComponent<Renderer>();
        if (r == null) r = c.GetComponentInParent<Renderer>();
        if (r != null && r.sharedMaterial != null)
        {
            string n = r.sharedMaterial.name.ToLowerInvariant();
            if (n.Contains("road") || n.Contains("asphalt")) result = MissionArea.Road;
            else if (n.Contains("sand") || n.Contains("dune")) result = MissionArea.Sand;
            else if (n.Contains("grass") || n.Contains("moss")) result = MissionArea.Grass;
        }
        areaCache[id] = result;
        return result;
    }

    // ───────────────────────── point validation ─────────────────────────

    /// <summary>Open, flat, roof-free ground of the wanted type at (or just below) p.</summary>
    public static bool IsDrivable(Vector3 p, MissionArea want, out Vector3 ground)
    {
        ground = p;
        if (!GroundHit(p + Vector3.up * 4f, 14f, out RaycastHit hit)) return false;
        if (hit.collider.CompareTag("Obstacles")) return false;
        if (hit.normal.y < 0.94f) return false;
        ground = hit.point;

        if (want != MissionArea.Any && Classify(hit.collider) != want) return false;

        // No roof / bridge deck right above.
        if (Physics.Raycast(ground + Vector3.up * 0.6f, Vector3.up, 9f, Mask, QueryTriggerInteraction.Ignore)) return false;

        // Room for a car.
        int n = Physics.OverlapSphereNonAlloc(ground + Vector3.up * 1.7f, 1.5f, overlap, Mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (ignoreRoot == null || !overlap[i].transform.IsChildOf(ignoreRoot)) return false;
        return true;
    }

    /// <summary>Closest non-trigger hit straight down, skipping the player car.</summary>
    public static bool GroundHit(Vector3 from, float dist, out RaycastHit best)
    {
        best = default;
        int n = Physics.RaycastNonAlloc(from, Vector3.down, hits, dist, Mask, QueryTriggerInteraction.Ignore);
        float bestD = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            if (ignoreRoot != null && hits[i].transform.IsChildOf(ignoreRoot)) continue;
            if (hits[i].collider.attachedRigidbody != null && !hits[i].collider.attachedRigidbody.isKinematic) continue;
            if (hits[i].collider.GetComponentInParent<MissionMover>() != null) continue;
            if (hits[i].distance < bestD) { bestD = hits[i].distance; best = hits[i]; found = true; }
        }
        return found;
    }

    public static float GroundY(Vector3 p, float fallback)
    {
        return GroundHit(p + Vector3.up * 3f, 12f, out RaycastHit h) ? h.point.y : fallback;
    }

    // ───────────────────────── paths ─────────────────────────

    /// <summary>NavMesh area "Road" (cheap: AI routes stay on asphalt). 0 if the project has no such area.</summary>
    public static int RoadMask
    {
        get
        {
            if (roadMask < 0) { int a = NavMesh.GetAreaFromName("Road"); roadMask = a >= 0 ? 1 << a : 0; }
            return roadMask;
        }
    }
    static int roadMask = -1;

    public static bool TryPath(Vector3 a, Vector3 b, List<Vector3> corners, out float length)
    {
        length = 0f;
        if (!NavMesh.SamplePosition(a, out NavMeshHit ha, 10f, NavMesh.AllAreas)) return false;
        if (!NavMesh.SamplePosition(b, out NavMeshHit hb, 10f, NavMesh.AllAreas)) return false;
        var path = Scratch;
        if (!NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path)) return false;
        if (path.status != NavMeshPathStatus.PathComplete) return false;

        var c = path.corners;
        for (int i = 1; i < c.Length; i++) length += Vector3.Distance(c[i - 1], c[i]);
        if (corners != null)
        {
            corners.Clear();
            corners.Add(a);
            for (int i = 1; i < c.Length - 1; i++) corners.Add(c[i]);
            corners.Add(b);
        }
        return true;
    }

    /// <summary>
    /// A reachable drivable point between minDist and maxDist (straight line) from `from`.
    /// Relaxes the surface type and distances when the area is crowded.
    /// </summary>
    public static bool PickPoint(System.Random rng, Vector3 from, float minDist, float maxDist, MissionArea area,
        out Vector3 point, out float pathLength, List<Vector3> corners = null, Vector3? avoidDir = null)
    {
        point = from;
        pathLength = 0f;
        const int Tries = 90;
        for (int t = 0; t < Tries; t++)
        {
            MissionArea want = t < 45 ? area : MissionArea.Any;
            float relax = t < 60 ? 1f : 1.6f;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            if (avoidDir.HasValue && t < 70 && Vector3.Dot(dir, avoidDir.Value) > 0.3f) continue;
            float d = Mathf.Lerp(minDist / relax, maxDist * relax, (float)rng.NextDouble());
            Vector3 cand = from + dir * d;

            // City missions: snap to the "Road" navigation area first (asphalt), so targets and AI cars start on the road.
            int mask = want == MissionArea.Road && t < 60 && RoadMask != 0 ? RoadMask : NavMesh.AllAreas;
            if (!NavMesh.SamplePosition(cand, out NavMeshHit nh, 14f, mask)) continue;
            if (!IsDrivable(nh.position, want, out Vector3 ground)) continue;
            if (!TryPath(from, ground, corners, out float len)) continue;
            if (len > maxDist * 2.4f * relax || len < minDist * 0.6f / relax) continue;

            point = ground;
            pathLength = len;
            return true;
        }
        return false;
    }

    /// <summary>A random drivable point somewhere on floors of the given type (used for desert / off-road starts).</summary>
    public static bool FindAnchor(System.Random rng, MissionArea area, out Vector3 point)
    {
        point = Vector3.zero;
        var floors = Floors(area);
        if (floors == null || floors.Count == 0) return false;

        for (int t = 0; t < 120; t++)
        {
            var c = floors[rng.Next(floors.Count)];
            var b = c.bounds;
            Vector3 cand = new Vector3(
                Mathf.Lerp(b.min.x, b.max.x, (float)rng.NextDouble()),
                b.max.y + 2f,
                Mathf.Lerp(b.min.z, b.max.z, (float)rng.NextDouble()));
            if (!NavMesh.SamplePosition(cand, out NavMeshHit nh, 20f, NavMesh.AllAreas)) continue;
            if (!IsDrivable(nh.position, area, out Vector3 ground)) continue;

            // Needs some room around it for the mission.
            int ok = 0;
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f;
                if (NavMesh.SamplePosition(ground + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 60f, out NavMeshHit nh2, 15f, NavMesh.AllAreas)
                    && TryPath(ground, nh2.position, null, out _)) ok++;
            }
            if (ok < 2) continue;
            point = ground;
            return true;
        }
        return false;
    }

    static List<Collider> Floors(MissionArea area)
    {
        if (roadFloors == null)
        {
            roadFloors = new List<Collider>();
            sandFloors = new List<Collider>();
            grassFloors = new List<Collider>();
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c.isTrigger || c.attachedRigidbody != null || c.CompareTag("Obstacles")) continue;
                var b = c.bounds;
                if (b.size.x < 30f || b.size.z < 30f || b.size.y > 25f) continue;
                switch (Classify(c))
                {
                    case MissionArea.Road: roadFloors.Add(c); break;
                    case MissionArea.Sand: sandFloors.Add(c); break;
                    case MissionArea.Grass: grassFloors.Add(c); break;
                }
            }
        }
        switch (area)
        {
            case MissionArea.Sand: return sandFloors;
            case MissionArea.Grass: return grassFloors;
            default: return roadFloors;
        }
    }

    // ───────────────────────── polyline helpers ─────────────────────────

    public static float Length(List<Vector3> pts)
    {
        float l = 0f;
        for (int i = 1; i < pts.Count; i++) l += Vector3.Distance(pts[i - 1], pts[i]);
        return l;
    }

    /// <summary>Point and flat tangent at distance d along a polyline.</summary>
    public static Vector3 Along(List<Vector3> pts, float d, out Vector3 tangent)
    {
        tangent = Vector3.forward;
        if (pts == null || pts.Count == 0) return Vector3.zero;
        if (pts.Count == 1) return pts[0];
        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 seg = pts[i] - pts[i - 1];
            float len = seg.magnitude;
            Vector3 flat = new Vector3(seg.x, 0f, seg.z);
            if (flat.sqrMagnitude > 0.0001f) tangent = flat.normalized;
            if (d <= len || i == pts.Count - 1)
                return pts[i - 1] + seg * (len > 0.001f ? Mathf.Clamp01(d / len) : 0f);
            d -= len;
        }
        return pts[pts.Count - 1];
    }

    /// <summary>Distance along the polyline of the point closest to p.</summary>
    public static float Project(List<Vector3> pts, Vector3 p)
    {
        float best = float.MaxValue, bestAlong = 0f, acc = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 a = pts[i - 1], b = pts[i];
            Vector3 ab = b - a;
            float len = ab.magnitude;
            float t = len > 0.001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / (len * len)) : 0f;
            float dist = (a + ab * t - p).sqrMagnitude;
            if (dist < best) { best = dist; bestAlong = acc + t * len; }
            acc += len;
        }
        return bestAlong;
    }

    public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
