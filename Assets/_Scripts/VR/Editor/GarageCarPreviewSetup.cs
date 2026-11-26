using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > VR > Setup Garage Car Preview (MainMenu). Safe to run again.
/// The garage car is shown INSIDE the menu screen on the GaragePannel, not in the 3D garage room:
///  • the cars move to a hidden "Car Studio" under the garage floor (y = -100)
///  • the old flat "Main Camera" (GarageCameraOrbit) becomes the preview camera, rendering into a RenderTexture
///  • a RawImage "CarPreview" on the GaragePannel (above its background picture, below the buttons) shows it
///  • the GaragePannel background picture is switched back on
/// </summary>
public static class GarageCarPreviewSetup
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string RTPath = "Assets/_Scripts/VR/GarageCarPreview.renderTexture";
    static readonly Vector3 StudioPos = new Vector3(0f, -100f, 0f);
    const float StudioYaw = 145f; // three-quarter front view from the orbit camera's start angle

    [MenuItem("Tools/VR/Setup Garage Car Preview (MainMenu)")]
    public static void Setup()
    {
        EditorSceneManager.SaveOpenScenes();
        string previous = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var mm = Object.FindFirstObjectByType<MainMenuMannager>(FindObjectsInactive.Include);
        var mgr = Object.FindFirstObjectByType<VRModeManager>(FindObjectsInactive.Include);
        var orbit = Object.FindFirstObjectByType<GarageCameraOrbit>(FindObjectsInactive.Include);
        if (mm == null || mm.GaragePannel == null || orbit == null) { Debug.LogWarning("[VR] MainMenu: MainMenuMannager / GaragePannel / GarageCameraOrbit missing"); return; }

        // 1. Render texture.
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RTPath);
        if (rt == null)
        {
            rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { name = "GarageCarPreview", antiAliasing = 2 };
            AssetDatabase.CreateAsset(rt, RTPath);
        }

        // 2. Car studio below the garage floor (the VR player can't see it; the preview camera can't see the garage).
        var studio = GameObject.Find("Car Studio");
        if (studio == null) studio = new GameObject("Car Studio");
        studio.transform.SetPositionAndRotation(StudioPos, Quaternion.Euler(0f, StudioYaw, 0f));
        VRModeManager.ParkShowcaseCars(mm.Cars, studio.transform);
        foreach (var car in mm.Cars) if (car != null) EditorUtility.SetDirty(car.transform);
        if (orbit.target != null)
        {
            // CarCamPos: aiming above the car puts it low in the picture, on the panel's painted turntable.
            orbit.target.position = StudioPos + new Vector3(0f, 1.7f, 0f);
            EditorUtility.SetDirty(orbit.target);
        }

        // 3. Preview camera = the old garage camera, now rendering into the texture only.
        var cam = orbit.GetComponent<Camera>();
        cam.gameObject.SetActive(true);
        cam.gameObject.tag = "Untagged";          // not Camera.main any more (the VR head is)
        cam.targetTexture = rt;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent: the panel's garage picture shows behind the car
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 40f;
        cam.enabled = false;                       // GarageCarPreview turns it on while the panel is open
        var al = cam.GetComponent<AudioListener>();
        if (al != null) al.enabled = false;
        orbit.enabled = true;
        // Fixed framing in VR (no pinch / scroll zoom): only turns around the car.
        // GarageCarPreview re-aims at the shown car's centre and applies its distance / lens at runtime.
        orbit.minDistance = orbit.maxDistance = orbit.distance = 11f;
        cam.fieldOfView = 28f;
        orbit.minY = orbit.maxY = 12f;
        EditorUtility.SetDirty(orbit);
        var urp = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (urp != null) { urp.renderPostProcessing = false; urp.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None; }
        EditorUtility.SetDirty(cam.gameObject);

        // 4. RawImage on the GaragePannel.
        var panel = mm.GaragePannel.transform;
        var bg = panel.GetComponent<Image>();
        if (bg != null && !bg.enabled) { bg.enabled = true; EditorUtility.SetDirty(bg); }
        var view = panel.Find("CarPreview");
        if (view == null)
        {
            view = new GameObject("CarPreview", typeof(RectTransform), typeof(RawImage), typeof(GarageCarPreview)).transform;
            view.SetParent(panel, false);
        }
        view.SetSiblingIndex(0); // drawn over the background picture, under Header / arrows / Select
        var vrt = (RectTransform)view;
        vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
        var raw = view.GetComponent<RawImage>();
        raw.texture = rt;
        raw.raycastTarget = true; // laser drag turns the camera around the car
        var preview = view.GetComponent<GarageCarPreview>();
        preview.previewCamera = cam;
        preview.orbit = orbit;
        EditorUtility.SetDirty(view.gameObject);

        // 5. Cars are no longer parked on the 3D garage floor.
        if (mgr != null)
        {
            if (mgr.carShowcase != null)
            {
                var anchor = mgr.carShowcase.gameObject;
                mgr.carShowcase = null;
                if (anchor.name == "CarShowcase") Object.DestroyImmediate(anchor); // the anchor was created by Place Garage Anchors
            }
            EditorUtility.SetDirty(mgr);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[VR] Garage car preview set up: cars in 'Car Studio', rendered on GaragePannel/CarPreview.");
        if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }
}
