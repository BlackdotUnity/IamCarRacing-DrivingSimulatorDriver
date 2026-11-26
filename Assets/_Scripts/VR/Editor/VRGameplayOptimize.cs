using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > VR > Optimize GamePlay For Quest. Safe to run again.
///  • Distance culling: VRDistanceCuller on "new environment" (small props / signs / trees hide when far away)
///  • Occlusion culling: an OcclusionArea over the drivable / flyable space (ground .. 45 m), then bakes the
///    occlusion data in the background (buildings and walls hide what is behind them). The scene is saved when done.
/// The scenery already had Occluder / Occludee Static flags, but occlusion had never been baked.
/// </summary>
public static class VRGameplayOptimize
{
    const string ScenePath = "Assets/Scenes/GamePlay.unity";

    /// <summary>Distance culling only (no occlusion re-bake).</summary>
    [MenuItem("Tools/VR/Update GamePlay Distance Culling")]
    public static void CullingOnly()
    {
        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var log = new System.Text.StringBuilder("[VR] GamePlay distance culling:\n");
        SetupDistanceCulling(log);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(log.ToString());
    }

    [MenuItem("Tools/VR/Optimize GamePlay For Quest")]
    public static void Run()
    {
        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var log = new System.Text.StringBuilder("[VR] GamePlay optimization:\n");
        SetupDistanceCulling(log);
        BakeOcclusion(scene, log);
    }

    static void SetupDistanceCulling(System.Text.StringBuilder log)
    {
        // 1. Distance culling of small scenery (city + the level-start garage full of boxes / tyres).
        foreach (var rootName in new[] { "new environment", "GaragePrefabOriginal" })
        {
            var root = GameObject.Find(rootName);
            if (root == null) { log.AppendLine("  (no '" + rootName + "')"); continue; }
            var culler = root.GetComponent<VRDistanceCuller>();
            if (culler == null) culler = root.AddComponent<VRDistanceCuller>();
            culler.bands = new[]
            {
                new VRDistanceCuller.Band { maxSize = 2.5f, hideBeyond = 90f },
                new VRDistanceCuller.Band { maxSize = 6f,   hideBeyond = 170f },
                new VRDistanceCuller.Band { maxSize = 15f,  hideBeyond = 320f },
                new VRDistanceCuller.Band { maxSize = 30f,  hideBeyond = 400f },
            };
            EditorUtility.SetDirty(culler);
            log.AppendLine("  VRDistanceCuller on '" + rootName + "' (90 / 170 / 320 / 400 m)");
        }
    }

    static void BakeOcclusion(UnityEngine.SceneManagement.Scene scene, System.Text.StringBuilder log)
    {
        // 2. Occlusion area around the static scenery, ground to flying height.
        var b = new Bounds(); bool first = true;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var f = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            if (!f.HasFlag(StaticEditorFlags.OccluderStatic)) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        var areaGo = GameObject.Find("VR Occlusion Area");
        if (areaGo == null) areaGo = new GameObject("VR Occlusion Area");
        var area = areaGo.GetComponent<OcclusionArea>();
        if (area == null) area = areaGo.AddComponent<OcclusionArea>();
        const float bottom = -5f, top = 45f; // roads are at y ≈ 0–7; the flying car goes up to ~30 m above the ground
        areaGo.transform.SetPositionAndRotation(new Vector3(b.center.x, (bottom + top) * 0.5f, b.center.z), Quaternion.identity);
        areaGo.transform.localScale = Vector3.one;
        area.center = Vector3.zero;
        area.size = new Vector3(b.size.x, top - bottom, b.size.z);
        EditorUtility.SetDirty(areaGo);
        log.AppendLine("  OcclusionArea " + area.size.ToString("0") + " at " + areaGo.transform.position.ToString("0"));

        // 3. Bake settings: city blocks / walls hide what is behind them; small holes (fences) don't count.
        StaticOcclusionCulling.smallestOccluder = 6f;
        StaticOcclusionCulling.smallestHole = 0.5f;
        StaticOcclusionCulling.backfaceThreshold = 100f;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!StaticOcclusionCulling.GenerateInBackground()) { log.AppendLine("  Occlusion bake could not start"); Debug.LogWarning(log.ToString()); return; }
        log.AppendLine("  Occlusion bake started in the background (progress bar at the bottom-right of the Editor)...");
        Debug.Log(log.ToString());
        EditorApplication.update += WaitForBake;
    }

    static void WaitForBake()
    {
        if (StaticOcclusionCulling.isRunning) return;
        EditorApplication.update -= WaitForBake;
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path == ScenePath) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        Debug.Log("[VR] Occlusion bake finished: " + (StaticOcclusionCulling.umbraDataSize / 1024) + " KB of occlusion data. GamePlay saved.");
    }
}
