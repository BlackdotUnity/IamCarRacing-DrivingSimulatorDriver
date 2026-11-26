using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Prefab references used to dress missions. Filled by Tools > Missions > Setup.</summary>
[Serializable]
public class MissionPrefabs
{
    public Material beamMaterial;      // additive, colour via _BaseColor
    public Material solidMaterial;     // lit, colour via _BaseColor
    public GameObject[] cars;          // generic traffic / AI cars
    public GameObject taxi, van, truck, bus, police, armored, train;
    public GameObject cone, barrier, crate, helicopter;
}

/// <summary>Builds every mission object: beacons, zones, pedestrians, drones, ramps, vehicles.</summary>
public static class MissionVisuals
{
    public static MissionPrefabs P;
    public static Transform Root;

    static MaterialPropertyBlock mpb;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static Mesh cylinder, sphere, cube, capsule;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { P = null; Root = null; mpb = null; cylinder = sphere = cube = capsule = null; }

    public static readonly Color Gold = new Color(1f, 0.78f, 0.15f);
    public static readonly Color Green = new Color(0.2f, 1f, 0.45f);
    public static readonly Color Cyan = new Color(0.15f, 0.85f, 1f);
    public static readonly Color Red = new Color(1f, 0.15f, 0.12f);
    public static readonly Color Purple = new Color(0.75f, 0.3f, 1f);
    public static readonly Color Orange = new Color(1f, 0.5f, 0.1f);

    static Mesh PrimMesh(PrimitiveType t)
    {
        switch (t)
        {
            case PrimitiveType.Cylinder: return cylinder ? cylinder : (cylinder = Grab(t));
            case PrimitiveType.Sphere: return sphere ? sphere : (sphere = Grab(t));
            case PrimitiveType.Capsule: return capsule ? capsule : (capsule = Grab(t));
            default: return cube ? cube : (cube = Grab(PrimitiveType.Cube));
        }
    }

    static Mesh Grab(PrimitiveType t)
    {
        var go = GameObject.CreatePrimitive(t);
        var m = go.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.Destroy(go);
        return m;
    }

    public static void Tint(Renderer r, Color c)
    {
        if (r == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColor, c);
        mpb.SetColor(ColorId, c);
        r.SetPropertyBlock(mpb);
    }

    /// <summary>Mesh-only primitive (no collider unless asked).</summary>
    public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Color color, string name = null, bool collider = false)
    {
        var go = new GameObject(name ?? t.ToString());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = PrimMesh(t);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Tint(r, color);
        if (collider)
        {
            switch (t)
            {
                case PrimitiveType.Sphere: go.AddComponent<SphereCollider>(); break;
                case PrimitiveType.Capsule: go.AddComponent<CapsuleCollider>(); break;
                case PrimitiveType.Cylinder: var cc = go.AddComponent<CapsuleCollider>(); cc.height = 2f; cc.radius = 0.5f; break;
                default: go.AddComponent<BoxCollider>(); break;
            }
        }
        return go;
    }

    // ───────────────────────── beacons & zones ─────────────────────────

    /// <summary>A glowing ground ring + light column. No collider: MissionRunner polls distances.</summary>
    public static MissionBeacon Beacon(Vector3 pos, float radius, Color color, string name, float beamHeight = 28f)
    {
        var go = new GameObject("Mission_" + name);
        go.transform.SetParent(Root, false);
        go.transform.position = pos;
        var b = go.AddComponent<MissionBeacon>();
        b.radius = radius;
        b.ring = Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.08f, 0f), new Vector3(radius * 2f, 0.04f, radius * 2f), P.beamMaterial, color * 0.9f, "Ring").GetComponent<Renderer>();
        b.column = Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, beamHeight * 0.5f, 0f), new Vector3(radius * 0.9f, beamHeight * 0.5f, radius * 0.9f), P.beamMaterial, color * 0.22f, "Column").GetComponent<Renderer>();
        b.core = Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, beamHeight * 0.5f, 0f), new Vector3(0.6f, beamHeight * 0.5f, 0.6f), P.beamMaterial, color * 0.8f, "Core").GetComponent<Renderer>();
        b.baseColor = color;
        return b;
    }

    /// <summary>A hazard column (red zones, EMP, spotlights).</summary>
    public static Renderer Zone(Transform parent, float radius, float height, Color color)
    {
        return Prim(PrimitiveType.Cylinder, parent, new Vector3(0f, height * 0.5f, 0f), new Vector3(radius * 2f, height * 0.5f, radius * 2f), P.beamMaterial, color, "Zone").GetComponent<Renderer>();
    }

    // ───────────────────────── props ─────────────────────────

    public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, string tag = "Obstacles")
    {
        if (prefab == null) return null;
        var go = UnityEngine.Object.Instantiate(prefab, pos, rot, Root);
        foreach (var body in go.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
        EnsureCollider(go);
        if (!string.IsNullOrEmpty(tag)) SetTagRecursive(go, tag);
        return go;
    }

    public static void EnsureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        var box = go.AddComponent<BoxCollider>();
        box.center = go.transform.InverseTransformPoint(b.center);
        Vector3 s = go.transform.lossyScale;
        box.size = new Vector3(b.size.x / Mathf.Max(0.001f, Mathf.Abs(s.x)), b.size.y / Mathf.Max(0.001f, Mathf.Abs(s.y)), b.size.z / Mathf.Max(0.001f, Mathf.Abs(s.z)));
    }

    public static void SetTagRecursive(GameObject go, string tag)
    {
        try
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.tag = tag;
        }
        catch (UnityException) { /* tag missing in project: leave untagged */ }
    }

    public static GameObject VehiclePrefab(MoverKind kind, System.Random rng)
    {
        GameObject p = null;
        switch (kind)
        {
            case MoverKind.Taxi: p = P.taxi; break;
            case MoverKind.Van: p = P.van; break;
            case MoverKind.Truck: p = P.truck; break;
            case MoverKind.Bus: p = P.bus; break;
            case MoverKind.Police: p = P.police; break;
            case MoverKind.Armored: p = P.armored; break;
            case MoverKind.Train: p = P.train; break;
        }
        if (p == null && P.cars != null && P.cars.Length > 0) p = P.cars[rng.Next(P.cars.Length)];
        return p;
    }

    /// <summary>Static parked vehicle (traffic, decor).</summary>
    public static GameObject ParkedCar(System.Random rng, Vector3 pos, Quaternion rot, MoverKind kind = MoverKind.Car)
    {
        var go = Spawn(VehiclePrefab(kind, rng), pos, rot);
        if (go != null) go.isStatic = false;
        return go;
    }

    /// <summary>Kinematic moving vehicle.</summary>
    public static MissionMover Vehicle(MoverKind kind, System.Random rng, Vector3 pos, Quaternion rot, string name)
    {
        var prefab = VehiclePrefab(kind, rng);
        GameObject go;
        if (prefab != null) go = Spawn(prefab, pos, rot);
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.SetPositionAndRotation(pos, rot);
            Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.8f, 0f), new Vector3(2f, 1.4f, 4.4f), P.solidMaterial, Color.gray, "Body", true);
            SetTagRecursive(go, "Obstacles");
        }
        go.name = name;
        if (kind == MoverKind.Police) AddSiren(go);
        if (kind == MoverKind.Train) go.transform.localScale = new Vector3(1f, 1f, 1.6f);
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        var mover = go.AddComponent<MissionMover>();
        mover.kind = kind;
        return mover;
    }

    static void AddSiren(GameObject car)
    {
        var rends = car.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        var bar = new GameObject("SirenBar");
        bar.transform.SetParent(car.transform, false);
        bar.transform.position = new Vector3(b.center.x, b.max.y + 0.08f, b.center.z);
        var red = Prim(PrimitiveType.Cube, bar.transform, new Vector3(-0.35f, 0f, 0f), new Vector3(0.6f, 0.16f, 0.25f), P.beamMaterial, Red, "Red");
        var blue = Prim(PrimitiveType.Cube, bar.transform, new Vector3(0.35f, 0f, 0f), new Vector3(0.6f, 0.16f, 0.25f), P.beamMaterial, new Color(0.15f, 0.35f, 1f), "Blue");
        var lr = new GameObject("SirenLight").AddComponent<Light>();
        lr.transform.SetParent(bar.transform, false);
        lr.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        lr.type = LightType.Point; lr.range = 9f; lr.intensity = 2.5f; lr.shadows = LightShadows.None;
        var s = bar.AddComponent<MissionSiren>();
        s.red = red.GetComponent<Renderer>(); s.blue = blue.GetComponent<Renderer>(); s.lamp = lr;
    }

    // ───────────────────────── characters & machines ─────────────────────────

    public static MissionPedestrian Pedestrian(System.Random rng, Vector3 pos, Vector3 walkDir, float walkDist)
    {
        var go = new GameObject("Pedestrian");
        go.transform.SetParent(Root, false);
        go.transform.position = pos;
        Color shirt = Color.HSVToRGB((float)rng.NextDouble(), 0.6f, 0.85f);
        Color skin = Color.Lerp(new Color(0.95f, 0.78f, 0.62f), new Color(0.45f, 0.3f, 0.2f), (float)rng.NextDouble());
        Prim(PrimitiveType.Capsule, go.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.42f, 0.5f, 0.42f), P.solidMaterial, new Color(0.18f, 0.2f, 0.3f), "Legs");
        Prim(PrimitiveType.Capsule, go.transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.55f, 0.42f, 0.4f), P.solidMaterial, shirt, "Body");
        Prim(PrimitiveType.Sphere, go.transform, new Vector3(0f, 1.72f, 0f), new Vector3(0.3f, 0.32f, 0.3f), P.solidMaterial, skin, "Head");
        var col = go.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.95f, 0f); col.height = 1.9f; col.radius = 0.35f;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        SetTagRecursive(go, "Obstacles");
        var p = go.AddComponent<MissionPedestrian>();
        p.Init(pos, walkDir, walkDist, 1.1f + (float)rng.NextDouble() * 0.6f, (float)rng.NextDouble());
        return p;
    }

    public static Transform Drone(Vector3 pos)
    {
        var go = new GameObject("Drone");
        go.transform.SetParent(Root, false);
        go.transform.position = pos;
        Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(1.1f, 0.35f, 1.1f), P.solidMaterial, new Color(0.15f, 0.15f, 0.18f), "Body");
        Prim(PrimitiveType.Sphere, go.transform, new Vector3(0f, -0.25f, 0f), new Vector3(0.45f, 0.45f, 0.45f), P.beamMaterial, Red, "Eye");
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f + 45f;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.95f;
            Prim(PrimitiveType.Cube, go.transform, d * 0.5f, new Vector3(0.12f, 0.08f, 1.0f), P.solidMaterial, new Color(0.25f, 0.25f, 0.28f), "Arm").transform.localRotation = Quaternion.Euler(0f, a, 0f);
            Prim(PrimitiveType.Cylinder, go.transform, d + Vector3.up * 0.12f, new Vector3(0.75f, 0.015f, 0.75f), P.beamMaterial, new Color(0.6f, 0.6f, 0.7f) * 0.4f, "Rotor");
        }
        return go.transform;
    }

    public static Transform Helicopter(Vector3 pos)
    {
        if (P.helicopter != null)
        {
            var h = UnityEngine.Object.Instantiate(P.helicopter, pos, Quaternion.identity, Root);
            foreach (var c in h.GetComponentsInChildren<Collider>()) c.enabled = false;
            return h.transform;
        }
        var d = Drone(pos);
        d.localScale = Vector3.one * 4f;
        return d;
    }

    /// <summary>Wedge ramp, `length` long, rising to `height` at its far end. Pivot at the low edge centre.</summary>
    public static GameObject Ramp(Vector3 pos, Quaternion rot, float length, float width, float height)
    {
        var go = new GameObject("StuntRamp");
        go.transform.SetParent(Root, false);
        go.transform.SetPositionAndRotation(pos, rot);
        float w = width * 0.5f;
        var v = new[]
        {
            new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f), new Vector3(-w, height, length), new Vector3(w, height, length), // slope
            new Vector3(-w, 0f, length), new Vector3(w, 0f, length),                                                            // back bottom
        };
        var verts = new List<Vector3>();
        var tris = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);
        }
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }
        Quad(v[0], v[1], v[2], v[3]);           // slope
        Quad(v[3], v[5], v[2], v[4]);           // back wall
        Tri(v[0], v[2], v[4]);                  // left side
        Tri(v[1], v[5], v[3]);                  // right side
        var mesh = new Mesh { name = "RampMesh" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = P.solidMaterial;
        Tint(r, new Color(0.95f, 0.55f, 0.1f));
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        // Glowing guide strips on the slope.
        for (int s = -1; s <= 1; s += 2)
        {
            var strip = Prim(PrimitiveType.Cube, go.transform, new Vector3(s * (w - 0.3f), height * 0.5f + 0.05f, length * 0.5f),
                new Vector3(0.25f, 0.06f, Mathf.Sqrt(length * length + height * height)), P.beamMaterial, Gold, "Strip");
            strip.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(height, length) * Mathf.Rad2Deg, 0f, 0f);
        }
        return go;
    }

    public static ParticleSystem Burst(Vector3 pos, Color color, int count, float speed, float size)
    {
        var go = new GameObject("Burst");
        go.transform.SetParent(Root, false);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f; main.loop = false; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = P.beamMaterial;
        ps.Emit(count);
        ps.Play();
        return ps;
    }
}

/// <summary>Glowing ring + light column marking a mission point. Pulses gently.</summary>
public class MissionBeacon : MonoBehaviour
{
    public float radius = 7f;
    public Renderer ring, column, core;
    public Color baseColor = Color.yellow;
    float phase;

    void Update()
    {
        phase += Time.deltaTime * 2.4f;
        float k = 0.75f + 0.25f * Mathf.Sin(phase);
        MissionVisuals.Tint(ring, baseColor * (0.7f + 0.3f * k));
        MissionVisuals.Tint(core, baseColor * (0.55f * k));
        if (column) column.transform.localRotation = Quaternion.Euler(0f, phase * 20f, 0f);
    }

    public void SetColor(Color c)
    {
        baseColor = c;
        MissionVisuals.Tint(column, c * 0.22f);
    }

    public bool Contains(Vector3 p, float extraRadius = 0f)
    {
        Vector3 d = p - transform.position;
        if (Mathf.Abs(d.y) > 8f) return false;
        d.y = 0f;
        float r = radius + extraRadius;
        return d.sqrMagnitude <= r * r;
    }
}

/// <summary>Red / blue flashing bar on police vehicles.</summary>
public class MissionSiren : MonoBehaviour
{
    public Renderer red, blue;
    public Light lamp;
    float t;

    void Update()
    {
        t += Time.deltaTime * 6f;
        bool a = Mathf.Repeat(t, 2f) < 1f;
        MissionVisuals.Tint(red, a ? MissionVisuals.Red * 2f : MissionVisuals.Red * 0.2f);
        MissionVisuals.Tint(blue, !a ? new Color(0.2f, 0.4f, 1f) * 2f : new Color(0.2f, 0.4f, 1f) * 0.2f);
        if (lamp) lamp.color = a ? Color.red : new Color(0.2f, 0.4f, 1f);
    }
}

/// <summary>Walks back and forth across the street. Falls over when hit.</summary>
public class MissionPedestrian : MonoBehaviour
{
    Vector3 a, b;
    float speed, t;
    bool forward = true, down;
    Rigidbody rb;

    public void Init(Vector3 pos, Vector3 dir, float dist, float spd, float startT)
    {
        dir.y = 0f;
        dir.Normalize();
        a = pos - dir * dist * 0.5f;
        b = pos + dir * dist * 0.5f;
        speed = spd / Mathf.Max(1f, dist);
        t = startT;
        rb = GetComponent<Rigidbody>();
    }

    public void KnockDown()
    {
        if (down) return;
        down = true;
        transform.rotation = Quaternion.Euler(85f, transform.eulerAngles.y, 0f);
        transform.position += Vector3.up * 0.3f;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    void FixedUpdate()
    {
        if (down || !MissionRunner.Running) return;
        t += (forward ? 1f : -1f) * speed * Time.fixedDeltaTime;
        if (t >= 1f) { t = 1f; forward = false; }
        if (t <= 0f) { t = 0f; forward = true; }
        Vector3 p = Vector3.Lerp(a, b, t);
        p.y = MissionWorld.GroundY(p, a.y);
        Vector3 look = (forward ? b - a : a - b); look.y = 0f;
        rb.MovePosition(p);
        if (look.sqrMagnitude > 0.01f) rb.MoveRotation(Quaternion.LookRotation(look));
    }
}
