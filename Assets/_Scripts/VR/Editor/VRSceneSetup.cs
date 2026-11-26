using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > VR > Setup VR Scenes (safe to run again).
/// Adds the VRIF "XR Rig Advanced" (disabled; enabled only on a headset) and a VRModeManager
/// to Splash, MainMenu and GamePlay. GamePlay also gets VRCarRig + a tweakable "VRSeat" on every car.
/// Existing objects are not removed.
/// </summary>
public static class VRSceneSetup
{
    const string RigPrefab = "Assets/BNG Framework/Prefabs/Player/XR Rig Advanced.prefab";
    const string WheelPrefab = "Assets/BNG Framework/Prefabs/SteeringWheel.prefab";

    static bool ReestimateSeats;

    [MenuItem("Tools/VR/Re-estimate Car Seats (overwrites VRSeat tweaks)")]
    public static void Reestimate()
    {
        ReestimateSeats = true;
        try { SetupAll(); } finally { ReestimateSeats = false; }
    }

    [MenuItem("Tools/VR/Setup VR Scenes")]
    public static void SetupAll()
    {
        EditorSceneManager.SaveOpenScenes();
        string previous = EditorSceneManager.GetActiveScene().path;
        EnsureOnTopMaterial();
        Setup("Assets/Scenes/Splash.unity", VRModeManager.SceneKind.Menu);
        Setup("Assets/Scenes/MainMenu.unity", VRModeManager.SceneKind.Menu);
        Setup("Assets/Scenes/GamePlay.unity", VRModeManager.SceneKind.Gameplay);
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[VR] Scenes set up: XR Rig + VRModeManager in Splash, MainMenu, GamePlay.");
    }

    /// <summary>
    /// Splash / MainMenu: lays the VR menu out inside the "Garage" prefab — player near the back,
    /// menu as a screen on the garage door (front wall), showroom car on the floor, ceiling lights.
    /// Re-running only creates missing anchors, so manual tweaks in the Scene view are kept.
    /// </summary>
    [MenuItem("Tools/VR/Place Garage Anchors (Splash + MainMenu)")]
    public static void PlaceGarageAnchors()
    {
        EditorSceneManager.SaveOpenScenes();
        string previous = EditorSceneManager.GetActiveScene().path;
        foreach (var path in new[] { "Assets/Scenes/Splash.unity", "Assets/Scenes/MainMenu.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var mgr = Object.FindFirstObjectByType<VRModeManager>(FindObjectsInactive.Include);
            GameObject garage = null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == "Garage") garage = r;
            if (mgr == null || garage == null) { Debug.LogWarning("[VR] " + path + ": VRModeManager or Garage missing"); continue; }

            var root = garage.transform.Find("VR Anchors");
            if (root == null)
            {
                root = new GameObject("VR Anchors").transform;
                root.SetParent(garage.transform, false);
            }
            Transform Anchor(string n, Vector3 local, float yaw)
            {
                var t = root.Find(n);
                if (t == null)
                {
                    t = new GameObject(n).transform;
                    t.SetParent(root, false);
                    t.localPosition = local;
                    t.localRotation = Quaternion.Euler(0f, yaw, 0f);
                }
                return t;
            }

            // Garage interior: floor y≈0, back wall z≈-2.4 (window), garage door z≈3.7.
            // Player a little forward of the middle and slightly low (seated-friendly view of the door screen).
            mgr.playerSpawn = Anchor("PlayerSpawn", new Vector3(0f, -0.12f, 0.3f), 0f);
            mgr.menuScreen = Anchor("MenuScreen (on garage door)", new Vector3(-0.03f, 1.4f, 3.64f), 0f);
            mgr.menuScreenWidth = 3.6f;
            // The garage car is shown on the GaragePannel (Tools/VR/Setup Garage Car Preview), so no floor showcase.

            // Interior lighting: the garage has emissive tubes but no real lights.
            if (root.Find("Ceiling Light L") == null)
                foreach (var (n, x) in new[] { ("Ceiling Light L", -1.0f), ("Ceiling Light R", 1.0f) })
                {
                    var l = new GameObject(n).AddComponent<Light>();
                    l.transform.SetParent(root, false);
                    l.transform.localPosition = new Vector3(x, 2.3f, 0.4f);
                    l.type = LightType.Point;
                    l.range = 7f;
                    l.intensity = 2.2f;
                    l.color = new Color(1f, 0.97f, 0.92f);
                    l.shadows = LightShadows.None;
                }

            EditorUtility.SetDirty(mgr);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[VR] Garage anchors placed in Splash and MainMenu.");
    }

    /// <summary>Resources/VR/UIOverlayAlways.mat (kept in Resources so the shader ships in the build).</summary>
    [MenuItem("Tools/VR/Create UI On-Top Material")]
    public static void EnsureOnTopMaterial()
    {
        const string path = "Assets/Resources/VR/UIOverlayAlways.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var shader = Shader.Find("UI/VR Overlay (Always On Top)");
        if (shader == null) { Debug.LogError("[VR] UI overlay shader not found"); return; }
        System.IO.Directory.CreateDirectory("Assets/Resources/VR");
        AssetDatabase.CreateAsset(new Material(shader) { name = "UIOverlayAlways" }, path);
        AssetDatabase.SaveAssets();
    }

    static void Setup(string path, VRModeManager.SceneKind kind)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        var mgr = Object.FindFirstObjectByType<VRModeManager>(FindObjectsInactive.Include);
        if (mgr == null) mgr = new GameObject("VRModeManager").AddComponent<VRModeManager>();
        mgr.sceneKind = kind;

        if (mgr.xrRig == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab);
            if (prefab == null) { Debug.LogError("[VR] " + RigPrefab + " not found"); return; }
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            rig.name = "XR Rig (VR)";
            rig.SetActive(false);
            mgr.xrRig = rig;
        }

        if (kind == VRModeManager.SceneKind.Gameplay)
        {
            var carRig = mgr.GetComponent<VRCarRig>();
            if (carRig == null) carRig = mgr.gameObject.AddComponent<VRCarRig>();
            if (carRig.steeringWheelPrefab == null) carRig.steeringWheelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WheelPrefab);

            var gm = Object.FindFirstObjectByType<GameMannager>(FindObjectsInactive.Include);
            if (gm != null)
                foreach (var carGo in gm.Cars)
                {
                    if (carGo == null) continue;
                    var car = carGo.GetComponent<RCC_CarControllerV3>();
                    if (car == null) continue;
                    // Estimated driver eye point, editable per car. Re-estimated only when "Re-estimate seats" is used.
                    var old = car.transform.Find("VRSeat");
                    if (old != null && ReestimateSeats) Object.DestroyImmediate(old.gameObject);
                    var seat = VRCarRig.EnsureSeat(car);
                    EditorUtility.SetDirty(seat.gameObject);
                }
            EditorUtility.SetDirty(carRig);
        }

        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
