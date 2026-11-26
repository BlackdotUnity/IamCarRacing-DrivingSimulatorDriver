using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tools > VR > Make GamePlay Scene VR-Only. Safe to run again.
/// Saves the VR setup into GamePlay instead of converting the phone layout at runtime:
///  • RCC flat camera: Camera + AudioListener off (the RCC_Camera script keeps running for the game logic);
///    the XR Rig's eye camera is the only view
///  • RCCCanvas: world-space HUD (CanvasScaler off); VRCarRig places it on the dashboard at runtime
///  • EventSystem: StandaloneInputModule (touch / mouse) off, VRIF VRUISystem (controller lasers) on
///  • touch driving controls off: Gas / Brake / Left / Right / NOS / Handbrake / Gradual Gas / Steering Wheel /
///    Joystick / Gear buttons / touch Fly button and RCC_MobileButtons. (Pause / Fuel / Info buttons stay: laser-clickable)
///  • RCC Settings controller type: Mobile → Custom (input comes from the VR controllers through VRDriveInput)
///  • XR Rig active with walking / teleport parts already switched off
/// Nothing is deleted: everything is only disabled.
/// </summary>
public static class VRGameplaySceneBake
{
    const string ScenePath = "Assets/Scenes/GamePlay.unity";

    // On-screen touch driving controls (children of RCCCanvas/GamePlayPanel/Controller Buttons).
    static readonly string[] TouchControls =
    {
        "Gas", "Brake", "Left", "Right", "NOS", "Handbrake", "Gradual Gas (Long)", "Gradual Gas (Short)",
        "Steering Wheel", "Joystick", "Gear UP", "Gear Down", "Gear", "Change Camera Button", "FlyButton",
    };

    [MenuItem("Tools/VR/Make GamePlay Scene VR-Only")]
    public static void Bake()
    {
        EditorSceneManager.SaveOpenScenes();
        string previous = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var log = new System.Text.StringBuilder("[VR] GamePlay made VR-only:\n");

        var mgr = Object.FindFirstObjectByType<VRModeManager>(FindObjectsInactive.Include);
        if (mgr == null || mgr.xrRig == null) { Debug.LogWarning("[VR] GamePlay: run Tools/VR/Setup VR Scenes first"); return; }
        var rig = mgr.xrRig;
        var carRig = mgr.GetComponent<VRCarRig>();

        // 1. Flat cameras: rendering + listening off (render-texture cameras such as the minimap stay).
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.targetTexture != null || cam.transform.IsChildOf(rig.transform)) continue;
            if (cam.enabled) { cam.enabled = false; log.AppendLine("  flat camera off: " + cam.name); }
            var al = cam.GetComponent<AudioListener>();
            if (al != null && al.enabled) al.enabled = false;
            EditorUtility.SetDirty(cam.gameObject);
        }

        // 2. XR Rig on, seated-game setup (no walking / teleport / rig gravity).
        rig.SetActive(true);
        VRModeManager.DisableLocomotion(rig);
        foreach (var c in rig.GetComponentsInChildren<Component>(true)) if (c != null) EditorUtility.SetDirty(c);
        EditorUtility.SetDirty(rig);
        var center = VRModeManager.FindDeep(rig.transform, "CenterEyeAnchor");
        var eye = center != null ? center.GetComponent<Camera>() : null;

        // 3. EventSystem: controller lasers only.
        var es = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
        foreach (var m in es.GetComponents<BaseInputModule>())
            if (!(m is BNG.VRUISystem) && m.enabled) { m.enabled = false; log.AppendLine("  input module off: " + m.GetType().Name); }
        if (es.GetComponent<BNG.VRUISystem>() == null) es.gameObject.AddComponent<BNG.VRUISystem>();
        EditorUtility.SetDirty(es.gameObject);

        // 4. Phone (screen-space) canvases → world space. VRCarRig puts the HUD on the dashboard at runtime.
        float hudWidth = carRig != null ? carRig.dashWidth : 1f;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.transform.IsChildOf(rig.transform) || c.renderMode == RenderMode.WorldSpace) continue;
            var rt = (RectTransform)c.transform;
            var scaler = c.GetComponent<CanvasScaler>();
            Vector2 size = scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize ? scaler.referenceResolution : new Vector2(1920f, 1080f);
            c.renderMode = RenderMode.WorldSpace;
            c.worldCamera = eye;
            if (scaler != null) scaler.enabled = false;
            if (c.GetComponent<GraphicRaycaster>() == null) c.gameObject.AddComponent<GraphicRaycaster>();
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * (hudWidth / size.x);
            EditorUtility.SetDirty(c.gameObject);
            log.AppendLine("  canvas world-space: " + c.name + " (" + hudWidth + " m wide)");
        }

        // 5. Touch driving controls off.
        foreach (var mob in Object.FindObjectsByType<RCC_MobileButtons>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mob.enabled) { mob.enabled = false; EditorUtility.SetDirty(mob); log.AppendLine("  RCC_MobileButtons off"); }
        var cm = Object.FindFirstObjectByType<CanvasMannager>(FindObjectsInactive.Include);
        Transform buttons = null;
        if (cm != null && cm.GamePlayPannel != null) buttons = cm.GamePlayPannel.transform.Find("Controller Buttons");
        if (buttons != null)
            foreach (var n in TouchControls)
            {
                var t = buttons.Find(n);
                if (t != null && t.gameObject.activeSelf) { t.gameObject.SetActive(false); EditorUtility.SetDirty(t.gameObject); log.AppendLine("  touch control off: " + n); }
            }
        else log.AppendLine("  (GamePlayPanel/Controller Buttons not found)");

        // 6. RCC: no phone controller type.
        var rcc = RCC_Settings.Instance;
        if (rcc != null && rcc.controllerType != RCC_Settings.ControllerType.Custom)
        {
            log.AppendLine("  RCC controller type: " + rcc.controllerType + " → Custom (VR controllers)");
            rcc.controllerType = RCC_Settings.ControllerType.Custom;
            rcc.controllerSelectedIndex = 4;
            EditorUtility.SetDirty(rcc);
            AssetDatabase.SaveAssets();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(log.ToString());
        if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }
}
