using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tools > VR > Make Menu Scenes VR-Only (Splash + MainMenu). Safe to run again.
/// Saves the VR setup into the scenes instead of converting a phone layout at runtime:
///  • flat Main Camera (touch / mouse orbit, AudioListener) switched off — the XR Rig is the only camera
///  • screen-space menu canvases become world-space screens on the garage door (CanvasScaler off)
///  • EventSystem: StandaloneInputModule (touch / mouse) off, VRIF VRUISystem (controller lasers) on
///  • XR Rig active and standing at PlayerSpawn
///  • MainMenu: the garage car stays on the GaragePannel (see GarageCarPreviewSetup), not in the 3D garage
/// Nothing is deleted: the old components are only disabled, so they can be re-enabled by hand.
/// </summary>
public static class VRMenuSceneBake
{
    [MenuItem("Tools/VR/Make Menu Scenes VR-Only (Splash + MainMenu)")]
    public static void BakeAll()
    {
        EditorSceneManager.SaveOpenScenes();
        string previous = EditorSceneManager.GetActiveScene().path;
        foreach (var path in new[] { "Assets/Scenes/Splash.unity", "Assets/Scenes/MainMenu.unity" })
            Bake(path);
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    static void Bake(string path)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var mgr = Object.FindFirstObjectByType<VRModeManager>(FindObjectsInactive.Include);
        if (mgr == null || mgr.xrRig == null) { Debug.LogWarning("[VR] " + path + ": run Tools/VR/Setup VR Scenes first"); return; }
        if (mgr.menuScreen == null || mgr.playerSpawn == null) { Debug.LogWarning("[VR] " + path + ": run Tools/VR/Place Garage Anchors first"); return; }
        var log = new System.Text.StringBuilder("[VR] " + scene.name + " made VR-only:\n");
        var rig = mgr.xrRig;

        // 1. Flat cameras off (render-texture cameras stay).
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.targetTexture != null || cam.transform.IsChildOf(rig.transform)) continue;
            Undo.RecordObject(cam.gameObject, "VR bake");
            foreach (var mb in cam.GetComponents<Behaviour>())
                if (mb is AudioListener || mb is GarageCameraOrbit) mb.enabled = false;
            cam.gameObject.SetActive(false);
            log.AppendLine("  flat camera off: " + cam.name);
        }

        // 2. XR Rig on, standing at the spawn point.
        rig.SetActive(true);
        Vector3 f = mgr.playerSpawn.forward; f.y = 0f;
        rig.transform.SetPositionAndRotation(mgr.playerSpawn.position, Quaternion.LookRotation(f.sqrMagnitude > 0.001f ? f : Vector3.forward));
        var ib = rig.GetComponent<BNG.InputBridge>();
        if (ib != null && ib.TrackingOrigin != UnityEngine.XR.TrackingOriginModeFlags.Floor)
        {
            ib.TrackingOrigin = UnityEngine.XR.TrackingOriginModeFlags.Floor; // eyes at real height above the floor
            EditorUtility.SetDirty(ib);
            log.AppendLine("  tracking origin: Floor");
        }
        var center = VRModeManager.FindDeep(rig.transform, "CenterEyeAnchor");
        var eye = center != null ? center.GetComponent<Camera>() : null;

        // 3. EventSystem: controller lasers only.
        var es = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
        foreach (var m in es.GetComponents<BaseInputModule>())
            if (!(m is BNG.VRUISystem) && m.enabled) { m.enabled = false; log.AppendLine("  input module off: " + m.GetType().Name); }
        if (es.GetComponent<BNG.VRUISystem>() == null) es.gameObject.AddComponent<BNG.VRUISystem>();
        EditorUtility.SetDirty(es.gameObject);

        // 4. Menu canvases -> world-space screen on the garage door.
        var wall = new System.Collections.Generic.List<Canvas>();
        if (mgr.wallCanvases != null) foreach (var c in mgr.wallCanvases) if (c != null) wall.Add(c);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.transform.IsChildOf(rig.transform) || wall.Contains(c)) continue;
            if (c.renderMode == RenderMode.WorldSpace) continue; // already a 3D canvas (not a phone screen)
            wall.Add(c);
        }
        foreach (var c in wall)
        {
            var rt = (RectTransform)c.transform;
            var scaler = c.GetComponent<CanvasScaler>();
            Vector2 size = scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize ? scaler.referenceResolution : new Vector2(1920f, 1080f);
            c.renderMode = RenderMode.WorldSpace;
            c.worldCamera = eye;
            if (scaler != null) scaler.enabled = false;
            if (c.GetComponent<GraphicRaycaster>() == null) c.gameObject.AddComponent<GraphicRaycaster>();
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * (mgr.menuScreenWidth / size.x);
            rt.SetPositionAndRotation(mgr.menuScreen.position - mgr.menuScreen.forward * (0.004f + 0.002f * c.sortingOrder), mgr.menuScreen.rotation);
            EditorUtility.SetDirty(c.gameObject);
            log.AppendLine("  canvas on garage door: " + c.name + " (" + mgr.menuScreenWidth + " m wide)");
        }
        // A canvas that was screen-space keeps its old screen position until the canvas system refreshes,
        // so the door pose is applied again after the mode change has taken effect.
        Canvas.ForceUpdateCanvases();
        foreach (var c in wall)
            c.transform.SetPositionAndRotation(mgr.menuScreen.position - mgr.menuScreen.forward * (0.004f + 0.002f * c.sortingOrder), mgr.menuScreen.rotation);
        mgr.wallCanvases = wall.ToArray();

        // 5. MainMenu: the garage car is shown on the GaragePannel (Tools/VR/Setup Garage Car Preview).
        //    Only if a CarShowcase anchor is assigned are the cars parked on the 3D garage floor instead.
        var mm = Object.FindFirstObjectByType<MainMenuMannager>(FindObjectsInactive.Include);
        if (mm != null)
        {
            if (mgr.carShowcase != null)
            {
                VRModeManager.ParkShowcaseCars(mm.Cars, mgr.carShowcase);
                foreach (var car in mm.Cars) if (car != null) EditorUtility.SetDirty(car.transform);
                log.AppendLine("  showroom cars parked at " + mgr.carShowcase.name);
            }
        }

        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(log.ToString());
    }
}
