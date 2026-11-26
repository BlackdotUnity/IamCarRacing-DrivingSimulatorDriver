using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quest performance: hides small scenery (signals, sign boards, props, trees...) when it is far from the player,
/// using Unity's CullingGroup (cheap, no per-frame loops). Big things (buildings, walls, roads) are left to
/// occlusion culling. Put it on the environment root; it only touches renderers under that root that were visible
/// when the scene started.
/// </summary>
public class VRDistanceCuller : MonoBehaviour
{
    [System.Serializable]
    public struct Band
    {
        [Tooltip("Objects up to this size (metres, largest side) use this band.")]
        public float maxSize;
        [Tooltip("Hidden beyond this distance from the player (metres).")]
        public float hideBeyond;
    }

    [Tooltip("Smallest band first. Larger objects than the last band are never distance-culled.")]
    public Band[] bands =
    {
        new Band { maxSize = 2.5f, hideBeyond = 90f },   // small props, bollards, small signs
        new Band { maxSize = 6f,   hideBeyond = 170f },  // signals, sign boards, cars, trees
        new Band { maxSize = 15f,  hideBeyond = 320f },  // kiosks, stops, big trees
        new Band { maxSize = 30f,  hideBeyond = 400f },  // wall segments
    };

    readonly List<CullingGroup> groups = new List<CullingGroup>();
    readonly List<Renderer[]> groupRenderers = new List<Renderer[]>();
    readonly List<float> groupDistance = new List<float>();
    Transform viewer;

    void Start()
    {
        var lists = new List<Renderer>[bands.Length];
        var spheres = new List<BoundingSphere>[bands.Length];
        for (int i = 0; i < bands.Length; i++) { lists[i] = new List<Renderer>(); spheres[i] = new List<BoundingSphere>(); }

        foreach (var r in GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            var b = r.bounds;
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            for (int i = 0; i < bands.Length; i++)
            {
                if (size > bands[i].maxSize) continue;
                lists[i].Add(r);
                spheres[i].Add(new BoundingSphere(b.center, b.extents.magnitude));
                break;
            }
        }

        for (int i = 0; i < bands.Length; i++)
        {
            if (lists[i].Count == 0) continue;
            var g = new CullingGroup();
            g.SetBoundingSpheres(spheres[i].ToArray());
            g.SetBoundingSphereCount(spheres[i].Count);
            g.SetBoundingDistances(new[] { bands[i].hideBeyond });
            var rs = lists[i].ToArray();
            g.onStateChanged = ev => { if (ev.hasBecomeVisible || ev.hasBecomeInvisible || ev.currentDistance != ev.previousDistance) rs[ev.index].enabled = ev.currentDistance == 0; };
            groups.Add(g);
            groupRenderers.Add(rs);
            groupDistance.Add(bands[i].hideBeyond);
        }
        Debug.Log("[VR] Distance culling: " + string.Join(", ", System.Array.ConvertAll(lists, l => l.Count.ToString())) + " renderers in bands " +
                  string.Join("/", System.Array.ConvertAll(bands, b => b.hideBeyond + "m")));
    }

    Vector3 lastFullPass;
    const float TeleportDistance = 40f;

    void LateUpdate()
    {
        // The head can change (garage rig → car seat), so look it up every frame (cheap).
        Transform current = null;
        if (VRModeManager.Instance != null && VRModeManager.Instance.Head != null) current = VRModeManager.Instance.Head;
        else if (Camera.main != null) current = Camera.main.transform;
        if (current == null) return;

        if (current != viewer)
        {
            viewer = current;
            // A CullingGroup without a target camera is never evaluated, so distance changes would never arrive.
            var cam = viewer.GetComponent<Camera>();
            foreach (var g in groups) { g.targetCamera = cam; g.SetDistanceReferencePoint(viewer); }
            FullPass();
        }
        // After a jump (car spawn, level restart, teleport) re-apply everything at once instead of waiting for events.
        else if ((viewer.position - lastFullPass).sqrMagnitude > TeleportDistance * TeleportDistance) FullPass();
    }

    /// Applies the current band to every renderer (onStateChanged only reports changes).
    void FullPass()
    {
        lastFullPass = viewer.position;
        for (int gi = 0; gi < groups.Count; gi++)
        {
            var rs = groupRenderers[gi];
            for (int i = 0; i < rs.Length; i++) if (rs[i] != null) rs[i].enabled = Vector3.Distance(viewer.position, rs[i].bounds.center) <= groupDistance[gi];
        }
    }

    void OnDestroy()
    {
        foreach (var g in groups) g.Dispose();
        groups.Clear();
    }
}
