using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Management;

/// <summary>
/// One per scene (Splash, MainMenu, GamePlay). The game is Meta Quest VR only, so it always:
///  • enables the VRIF XR Rig and turns off the flat cameras (the minimap render camera stays)
///  • swaps the EventSystem to VRIF's VRUISystem so controller lasers click the existing UI
///  • converts every screen-space canvas to a world-space panel
///  • in GamePlay, hands the rig to VRCarRig (driver's seat, steering wheel, controller input)
/// There is no flat (phone / touch) mode any more. In the Editor without a headset,
/// VRIF's VR Emulator drives the rig with mouse / keyboard.
/// </summary>
[DefaultExecutionOrder(-80)]
public class VRModeManager : MonoBehaviour
{
    public enum SceneKind { Menu, Gameplay }

    public static VRModeManager Instance { get; private set; }
    /// <summary>Always true: VR-only game.</summary>
    public static bool VRActive => true;
    /// <summary>True when a real headset is running (false in the Editor emulator).</summary>
    public static bool HeadsetPresent { get; private set; }

    public SceneKind sceneKind = SceneKind.Menu;
    [Tooltip("The VRIF XR Rig in this scene.")]
    public GameObject xrRig;

    [Header("Garage layout (Splash / MainMenu)")]
    [Tooltip("Where the player stands (floor level). Forward = the direction the player faces.")]
    public Transform playerSpawn;
    [Tooltip("Menus are shown as a screen on the wall here. Forward must point INTO the wall.")]
    public Transform menuScreen;
    [Tooltip("Width of the wall screen in metres (height follows 16:9).")]
    public float menuScreenWidth = 3.2f;
    [Tooltip("Height of the wall screen in metres. 0 = 16:9 from the width; set it to cover the whole wall (the UI layout widens to match).")]
    public float menuScreenHeight = 0f;
    [Tooltip("MainMenu: the selectable cars are parked here, on the garage floor.")]
    public Transform carShowcase;
    [Tooltip("Menu canvases saved in the scene as world-space screens (filled by Tools/VR/Make Menu Scenes VR-Only).")]
    public Canvas[] wallCanvases;
    [Tooltip("Splash / MainMenu: eye height used when no headset is tracking (Editor emulator). On a Quest the real head height is used (Floor tracking).")]
    public float standingEyeHeight = 1.65f;

    [Header("Menu panels (when no wall screen is set)")]
    public float menuDistance = 2.0f;
    public float menuWidth = 2.4f;
    [Tooltip("Seated eye height above the rig origin when it replaces a flat camera.")]
    public float seatedEyeHeight = 1.2f;

    public Transform Head { get; private set; }
    readonly List<Canvas> menuCanvases = new List<Canvas>();

    void Awake()
    {
        Instance = this;
        var s = XRGeneralSettings.Instance;
        HeadsetPresent = s != null && s.Manager != null && s.Manager.activeLoader != null && UnityEngine.XR.XRSettings.isDeviceActive;

        // Standing menus: Floor tracking puts the eyes at the player's real height above the garage floor.
        // ("Device" tracking would leave the eyes at the rig origin, i.e. in the floor.) Runs before InputBridge applies it.
        if (sceneKind == SceneKind.Menu && xrRig != null)
        {
            var ib = xrRig.GetComponent<BNG.InputBridge>();
            if (ib != null) ib.TrackingOrigin = UnityEngine.XR.TrackingOriginModeFlags.Floor;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        VRCanvasUtil.RuntimeCanvasCreated -= OnRuntimeCanvas;
        if (sceneKind == SceneKind.Gameplay) { VRDriveInput.Active = false; VRDriveInput.Clear(); FlyingCarInput.XRHeld = false; }
    }

    IEnumerator Start()
    {
        if (xrRig == null) { Debug.LogError("[VR] VRModeManager has no XR Rig assigned."); yield break; }

        Camera flat = Camera.main;
        if (flat == null)
            foreach (var c in Camera.allCameras)
                if (c.targetTexture == null && !c.transform.IsChildOf(xrRig.transform)) { flat = c; break; }
        Pose start = flat != null ? new Pose(flat.transform.position, flat.transform.rotation) : new Pose(Vector3.zero, Quaternion.identity);

        // Flat cameras off (keep render-texture cameras such as the minimap).
        foreach (var cam in Camera.allCameras)
        {
            if (cam.targetTexture != null || cam.transform.IsChildOf(xrRig.transform)) continue;
            cam.enabled = false;
            var al = cam.GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
        }

        // Texture cameras (car minimaps) render 20x a second instead of every frame.
        if (sceneKind == SceneKind.Gameplay)
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cam.targetTexture != null && cam.GetComponent<RenderTextureCameraThrottle>() == null)
                    cam.gameObject.AddComponent<RenderTextureCameraThrottle>();

        SetupEventSystem();
        xrRig.SetActive(true);
        DisableLocomotion(xrRig);
        var center = FindDeep(xrRig.transform, "CenterEyeAnchor");
        var rigCam = center != null ? center.GetComponent<Camera>() : xrRig.GetComponentInChildren<Camera>(true);
        Head = rigCam != null ? rigCam.transform : xrRig.transform;
        ConfigureForQuest(rigCam);

        // Minimap-only layer (car PlayerIcon / map markers: 14) is drawn by the cars' MinimapView cameras only,
        // never by the headset – same as the old RCC camera did. (Layer 13 = roads: seen by both, keep it.)
        const int MinimapOnly = 1 << 14;
        foreach (var cam in xrRig.GetComponentsInChildren<Camera>(true))
            if (cam.targetTexture == null) cam.cullingMask &= ~MinimapOnly;

        if (playerSpawn != null)
        {
            // Garage: stand on the floor at the spawn point (head height comes from the headset).
            Vector3 sf = playerSpawn.forward; sf.y = 0f;
            xrRig.transform.SetPositionAndRotation(playerSpawn.position, Quaternion.LookRotation(sf.sqrMagnitude > 0.001f ? sf : Vector3.forward));

            // No tracked headset (Editor emulator): nothing lifts the camera off the floor, so raise it to eye height.
            // (VRIF's BNGPlayerController normally does this but is disabled for the seated game.)
            if (!HeadsetPresent)
            {
                var camRig = FindDeep(xrRig.transform, "CameraRig");
                if (camRig != null) camRig.localPosition = new Vector3(camRig.localPosition.x, standingEyeHeight, camRig.localPosition.z);
            }
        }
        else
        {
            // Stand the player where the flat camera was (yaw only).
            Vector3 fwd = start.rotation * Vector3.forward; fwd.y = 0f;
            xrRig.transform.SetPositionAndRotation(start.position - Vector3.up * seatedEyeHeight,
                Quaternion.LookRotation(fwd.sqrMagnitude > 0.001f ? fwd : Vector3.forward));
        }

        yield return null; // let tracking / UIPointers initialise
        yield return null;

        // The rig is enabled after the scene loaded, so VRIF's fader missed its "scene loaded" fade: run it now.
        foreach (var fader in xrRig.GetComponentsInChildren<BNG.ScreenFader>(true)) fader.DoFadeOut();

        VRCanvasUtil.RuntimeCanvasCreated += OnRuntimeCanvas;

        if (sceneKind == SceneKind.Gameplay)
        {
            var car = GetComponent<VRCarRig>();
            if (car == null) car = gameObject.AddComponent<VRCarRig>();
            car.Begin(xrRig.transform, Head);
        }
        else
        {
            // The garage car is shown on the GaragePannel (GarageCarPreview render texture).
            // Only when a showcase anchor is set are the cars parked on the 3D garage floor instead.
            var mm = FindFirstObjectByType<MainMenuMannager>(FindObjectsInactive.Include);
            if (mm != null && carShowcase != null) ParkShowcaseCars(mm.Cars, carShowcase);

            // Screens saved in the scene: only need the laser-pointer registration + on-top material.
            if (wallCanvases != null)
                foreach (var c in wallCanvases)
                {
                    if (c == null) continue;
                    if (menuScreen != null) // keep it on the door even if the anchor was moved in the Scene view
                    {
                        c.transform.SetPositionAndRotation(menuScreen.position - menuScreen.forward * (0.004f + 0.002f * c.sortingOrder), menuScreen.rotation);
                        if (menuScreenHeight > 0f) VRCanvasUtil.FitSize(c, menuScreenWidth, menuScreenHeight);
                    }
                    BNG.VRUISystem.Instance.AddCanvas(c);
                    VRCanvasUtil.ApplyOnTopMaterial(c);
                    menuCanvases.Add(c);
                }

            // Any remaining phone (screen-space) canvas is converted at runtime.
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.renderMode == RenderMode.WorldSpace || c.transform.IsChildOf(xrRig.transform)) continue;
                if (menuScreen != null)
                {
                    // A screen on the garage wall; canvases with a higher sorting order sit a hair in front.
                    VRCanvasUtil.MakeWorldSpace(c, menuScreenWidth, menuScreenHeight);
                    c.transform.SetPositionAndRotation(menuScreen.position - menuScreen.forward * (0.004f + 0.002f * c.sortingOrder), menuScreen.rotation);
                }
                else
                {
                    VRCanvasUtil.MakeWorldSpace(c, menuWidth);
                    VRCanvasUtil.PlaceInFront(c.transform, Head, menuDistance + 0.002f * c.sortingOrder * -1f, 0.15f);
                }
                menuCanvases.Add(c);
            }
        }
    }

    /// <summary>Moves the main-menu display cars from behind the old flat background onto the garage floor.</summary>
    public static void ParkShowcaseCars(GameObject[] cars, Transform carShowcase)
    {
        if (cars == null || carShowcase == null) return;
        float floorY = carShowcase.position.y;
        foreach (var car in cars)
        {
            if (car == null) continue;
            car.transform.rotation = carShowcase.rotation;
            car.transform.position = carShowcase.position;
            // Lowest point of the (possibly inactive) car, from its mesh bounds.
            float minY = float.MaxValue;
            foreach (var mf in car.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    minY = Mathf.Min(minY, mf.transform.TransformPoint(corner).y);
                }
            }
            if (minY < float.MaxValue) car.transform.position += Vector3.up * (floorY - minY);
        }
    }

    void Update()
    {
        if (!VRActive || Head == null) return;
        // Menu: Y / right-stick click re-centres the panels in front of the player.
        var ib = BNG.InputBridge.Instance;
        if (sceneKind == SceneKind.Menu && menuScreen == null && ib != null && (ib.YButtonDown || ib.RightThumbstickDown))
            foreach (var c in menuCanvases) if (c != null) VRCanvasUtil.PlaceInFront(c.transform, Head, menuDistance, 0.15f);
    }

    /// <summary>Per-camera / runtime settings that keep Quest at a stable frame rate and comfortable.</summary>
    void ConfigureForQuest(Camera cam)
    {
        if (cam != null)
        {
            cam.farClipPlane = Mathf.Min(cam.farClipPlane, 600f);
            var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data != null)
            {
                data.renderPostProcessing = false;    // no post FX in VR
                data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None; // MSAA from the URP asset
            }
        }
        try
        {
            // Fixed foveated rendering (dynamic) + 72 Hz: the Quest default sweet spot for this scene size.
            OVRPlugin.foveatedRenderingLevel = OVRPlugin.FoveatedRenderingLevel.High;
            OVRPlugin.useDynamicFoveatedRendering = true;
            if (OVRPlugin.systemDisplayFrequenciesAvailable != null && System.Array.IndexOf(OVRPlugin.systemDisplayFrequenciesAvailable, 72f) >= 0)
                OVRPlugin.systemDisplayFrequency = 72f;
            // Ask the headset for steady high CPU / GPU clocks (it still lowers them if it gets too hot).
            OVRPlugin.suggestedCpuPerfLevel = OVRPlugin.ProcessorPerformanceLevel.SustainedHigh;
            OVRPlugin.suggestedGpuPerfLevel = OVRPlugin.ProcessorPerformanceLevel.SustainedHigh;
        }
        catch (System.Exception e) { Debug.Log("[VR] Foveation / refresh rate not set: " + e.Message); }

        // Physics at 72 Hz matches the display (VRIF also does this) so the cockpit doesn't judder.
        Time.fixedDeltaTime = 1f / 72f;
    }

    void OnRuntimeCanvas(Canvas c)
    {
        if (!VRActive || Head == null) return;
        VRCanvasUtil.MakeWorldSpace(c, 1.6f);
        VRCanvasUtil.PlaceInFront(c.transform, Head, 1.25f, 0.1f); // a little closer than other panels
    }

    void SetupEventSystem()
    {
        var es = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
        foreach (var m in es.GetComponents<BaseInputModule>())
            if (!(m is BNG.VRUISystem)) m.enabled = false;
        if (es.GetComponent<BNG.VRUISystem>() == null) es.gameObject.AddComponent<BNG.VRUISystem>();
    }

    /// <summary>Seated game: no walking, teleporting, snap-turning or rig gravity.</summary>
    public static void DisableLocomotion(GameObject rig)
    {
        string[] off =
        {
            "SmoothLocomotion", "PlayerTeleport", "PlayerRotation", "PlayerGravity", "PlayerClimbing", "BNGPlayerController",
            "LocomotionManager", "ArmSwingLocomotion", "PlayerMovingPlatformSupport", "HeadCollisionMove", "HeadCollisionFade",
        };
        foreach (var mb in rig.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null && System.Array.IndexOf(off, mb.GetType().Name) >= 0) mb.enabled = false;
        foreach (var cc in rig.GetComponentsInChildren<CharacterController>(true)) cc.enabled = false;
        // Walking-player parts that make no sense in a seated driving game.
        foreach (var n in new[] { "HeadCollision", "Inventory", "Locomotion", "BodyIK" })
        {
            var t = FindDeep(rig.transform, n);
            if (t != null) t.gameObject.SetActive(false);
        }
        var pc = FindDeep(rig.transform, "PlayerController");
        if (pc != null) foreach (var col in pc.GetComponents<Collider>()) col.enabled = false;
        foreach (var rb in rig.GetComponentsInChildren<Rigidbody>(true))
            if (rb.GetComponent<BNG.Grabber>() == null && rb.GetComponentInParent<BNG.Grabbable>() == null) rb.isKinematic = true;
    }

    public static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }
}
