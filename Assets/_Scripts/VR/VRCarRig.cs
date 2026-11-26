using UnityEngine;

/// <summary>
/// GamePlay in VR: seats the VRIF XR Rig in the active car, adds a grabbable VRIF steering wheel,
/// moves the game HUD onto the dashboard and maps Quest controllers to the existing RCC input.
///
/// Controls (Quest Touch):
///   Right trigger  = throttle          Left trigger = brake / reverse
///   Steering wheel = grab with grips   (or left thumbstick)
///   A = handbrake / drift              B = boost (NOS)
///   X (hold) = fly                     Y = cockpit / chase view
///   Right stick click = re-centre seat Menu (☰) = pause / resume
///   Left stick click = refuel panel
/// UI lasers are shown only while a panel (info, objective, pause, fail, complete, fuel, shop) is open;
/// while driving they are off, so the right trigger is only the throttle.
/// </summary>
public class VRCarRig : MonoBehaviour
{
    public enum View { Cockpit, Chase }

    [Header("View")]
    public View view = View.Cockpit;
    [Tooltip("Chase view: distance behind / height above the car.")]
    public float chaseDistance = 6.5f, chaseHeight = 2.4f;
    [Tooltip("Chase view: how fast the view turns with the car (lower = calmer).")]
    public float chaseYawFollow = 2.2f;

    [Header("Steering")]
    [Tooltip("VRIF steering wheel prefab (Assets/BNG Framework/Prefabs/SteeringWheel.prefab).")]
    public GameObject steeringWheelPrefab;
    public float wheelMaxAngle = 450f;
    [Tooltip("How far the wheel must be turned (degrees) for full steering lock. Lower = more responsive.")]
    public float fullLockAngle = 120f;
    [Tooltip("Flip if turning the wheel steers the wrong way.")]
    public float wheelSteerSign = -1f;
    [Tooltip("Flip (1 / -1) if the car's steering wheel MODEL turns the opposite way to the hands / the car.")]
    public float wheelModelSign = 1f;
    public float stickDeadZone = 0.15f;
    [Tooltip("Extra grab margin around the steering wheel rim (m), so the wheel is easy to grab.")]
    public float grabMargin = 0.07f;
    [Tooltip("Show a glowing ring on the steering wheel when a hand is close enough to grab it.")]
    public bool showGrabHint = true;

    [Header("HUD")]
    [Tooltip("Driving HUD: width (m) of the whole HUD canvas. The timer / fuel / level cluster is its top strip.")]
    public float dashWidth = 1.0f;
    [Tooltip("Where the TOP-CENTRE of the HUD sits, relative to the driver's eyes (x right, y up, z forward). " +
             "Just above the steering wheel, at the bottom of the windscreen.")]
    public Vector3 dashOffset = new Vector3(0f, -0.12f, 0.75f);
    [Tooltip("Pause / fail / shop panels: shown larger, centred straight ahead.")]
    public float focusWidth = 2.0f;
    public Vector3 focusOffset = new Vector3(0f, 0.02f, 1.1f);
    [Tooltip("Pause / fail / complete / objective: the open panel's own box is shown this wide (metres) …")]
    public float panelWidth = 1.1f;
    [Tooltip("… and at most this tall (metres), so tall panels fit the view.")]
    public float panelMaxHeight = 0.8f;
    [Tooltip("Rearrange the phone HUD into one compact cluster at the top-centre (hides the on-screen pause / info buttons: use the controller).")]
    public bool compactHud = true;

    [Header("Controller lasers")]
    [Tooltip("UI lasers only while a panel is open; off while driving (right trigger = throttle only).")]
    public bool lasersOnlyOnPanels = true;

    Transform rig, head, seat, hudAnchor;
    RCC_CarControllerV3 car;
    BNG.SteeringWheel wheel;
    BNG.Grabbable wheelGrab;
    Renderer interiorWheel;
    Vector3 interiorWheelLocalPos;
    Quaternion interiorWheelLocalRot;
    LineRenderer grabHint;
    BNG.Grabber[] grabbers;
    bool wasHeld;
    float wheelRadius = 0.18f;
    Canvas hud;
    bool modalShown;
    GameObject[] laserPointers;
    bool lasersOn = true;
    UnityEngine.UI.Text pauseInfo;
    RectTransform modalBox;
    float chaseYaw;
    bool begun;

    /// <summary>Called by VRModeManager once the rig is enabled.</summary>
    public void Begin(Transform rigRoot, Transform headTransform)
    {
        rig = rigRoot;
        head = headTransform;
        car = FindPlayerCar();
        if (car == null) { Debug.LogWarning("[VR] No player car found."); return; }

        seat = EnsureSeat(car);
        if (steeringWheelPrefab != null) SpawnWheel();

        // The head rides on the car body: interpolate it so the cockpit doesn't judder between physics steps.
        var carRb = car.GetComponent<Rigidbody>();
        if (carRb != null) carRb.interpolation = RigidbodyInterpolation.Interpolate;

        // The player's hands / head must never push the car they sit in.
        var carCols = car.GetComponentsInChildren<Collider>(true);
        foreach (var rc in rig.GetComponentsInChildren<Collider>(true))
            foreach (var cc in carCols)
                if (wheel == null || !cc.transform.IsChildOf(wheel.transform)) Physics.IgnoreCollision(rc, cc, true);
        SetupHud();
        HideTouchControls();

        // The UI lasers that are active on this rig (VRIF: the right-hand UIPointer).
        var pointers = new System.Collections.Generic.List<GameObject>();
        foreach (var p in rig.GetComponentsInChildren<BNG.UIPointer>(false)) pointers.Add(p.gameObject);
        laserPointers = pointers.ToArray();

        VRDriveInput.Active = true;
        begun = true;
        ApplyView();
    }

    static RCC_CarControllerV3 FindPlayerCar()
    {
        var gm = GameMannager.Instance;
        if (gm != null && OverAllGameMannager.OA_GM != null)
        {
            int i = OverAllGameMannager.OA_GM.SaveData.CurrentCar;
            if (i >= 0 && i < gm.Cars.Length && gm.Cars[i] != null) return gm.Cars[i].GetComponent<RCC_CarControllerV3>();
        }
        return RCC_SceneManager.Instance != null ? RCC_SceneManager.Instance.activePlayerVehicle : null;
    }

    // ───────────────────────── seat & wheel ─────────────────────────

    /// <summary>
    /// Driver's eye point. Uses a "VRSeat" child if the car has one (tweak it in the Inspector);
    /// otherwise derives it from the interior steering wheel model, or from the wheel colliders.
    /// </summary>
    public static Transform EnsureSeat(RCC_CarControllerV3 car)
    {
        var t = car.transform.Find("VRSeat");
        if (t != null) return t;

        Vector3 eye;
        var interiorWheel = FindInteriorWheel(car);
        if (interiorWheel != null)
        {
            Vector3 w = car.transform.InverseTransformPoint(interiorWheel.bounds.center);
            eye = w + new Vector3(0f, 0.36f, -0.5f); // eyes ~50 cm behind and ~36 cm above the wheel
        }
        else
        {
            Vector3 fl = car.transform.InverseTransformPoint(car.FrontLeftWheelCollider.transform.position);
            Vector3 rl = car.transform.InverseTransformPoint(car.RearLeftWheelCollider.transform.position);
            float ground = fl.y - car.FrontLeftWheelCollider.wheelCollider.radius;
            eye = new Vector3(fl.x * 0.45f, ground + 1.12f, Mathf.Lerp(rl.z, fl.z, 0.42f));
        }

        var seatGo = new GameObject("VRSeat");
        seatGo.transform.SetParent(car.transform, false);
        seatGo.transform.localPosition = eye;
        seatGo.transform.localRotation = Quaternion.identity;
        return seatGo.transform;
    }

    /// <summary>The steering wheel mesh inside the car model (e.g. "Steering_wheel").</summary>
    public static Renderer FindInteriorWheel(RCC_CarControllerV3 car)
    {
        foreach (var r in car.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = r.name.ToLowerInvariant().Replace(" ", "_");
            // (some models spell it "Stering_wheel")
            if ((n.Contains("steering_wheel") || n.Contains("stering_wheel")) && r.GetComponentInParent<BNG.SteeringWheel>() == null) return r;
        }
        return null;
    }

    void SpawnWheel()
    {
        var existing = car.GetComponentInChildren<BNG.SteeringWheel>(true);
        if (existing != null) { wheel = existing; }
        else
        {
            var go = Instantiate(steeringWheelPrefab, seat);
            go.name = "VR Steering Wheel";
            interiorWheel = FindInteriorWheel(car);
            if (interiorWheel != null)
            {
                // Grab ring sits exactly on the car's own steering wheel; its own graphics are hidden.
                Vector3 c = interiorWheel.bounds.center;
                go.transform.position = c;
                go.transform.rotation = Quaternion.LookRotation(c - seat.position, car.transform.up);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                foreach (var cv in go.GetComponentsInChildren<Canvas>(true)) cv.gameObject.SetActive(false);
                interiorWheelLocalPos = interiorWheel.transform.localPosition;
                interiorWheelLocalRot = interiorWheel.transform.localRotation;
            }
            else
            {
                go.transform.localPosition = new Vector3(0f, -0.38f, 0.42f);
                go.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            }
            go.transform.localScale = Vector3.one;
            wheel = go.GetComponentInChildren<BNG.SteeringWheel>(true);
        }
        if (wheel == null) return;
        wheel.MinAngle = -wheelMaxAngle;
        wheel.MaxAngle = wheelMaxAngle;
        wheel.ReturnToCenter = true;
        wheelGrab = wheel.GetComponent<BNG.Grabbable>();
        foreach (var rb in wheel.GetComponentsInParent<Rigidbody>(true))
            if (rb.gameObject != car.gameObject && rb.transform.IsChildOf(seat)) rb.isKinematic = true;
        // The wheel lives under the car's dynamic Rigidbody: colliders must be convex, and as triggers
        // they can be grabbed without adding mass / inertia or collisions to the car.
        foreach (var col in wheel.GetComponentsInChildren<Collider>(true))
        {
            if (col is MeshCollider mc) mc.convex = true;
            col.isTrigger = true;
        }
        // The wheel must not collide with the car body it sits in.
        var carColliders = car.GetComponentsInChildren<Collider>(true);
        foreach (var wc in wheel.GetComponentsInChildren<Collider>(true))
            foreach (var cc in carColliders)
                if (!cc.transform.IsChildOf(wheel.transform)) Physics.IgnoreCollision(wc, cc, true);

        AddGrabZone();
        if (showGrabHint) CreateGrabHint();
    }

    /// <summary>
    /// A generous trigger box over the whole wheel face (rim + margin) that grabs the wheel,
    /// so the player doesn't have to find the thin rim exactly.
    /// </summary>
    void AddGrabZone()
    {
        if (wheelGrab == null) return;
        if (interiorWheel != null)
        {
            Vector3 e = interiorWheel.bounds.extents;
            wheelRadius = Mathf.Clamp(Mathf.Max(e.x, e.y, e.z) * 0.9f, 0.12f, 0.25f);
        }
        var old = wheel.transform.Find("GrabZone");
        if (old != null) Destroy(old.gameObject);
        var zone = new GameObject("GrabZone");
        zone.transform.SetParent(wheel.transform, false);
        zone.layer = wheel.gameObject.layer;
        var box = zone.AddComponent<BoxCollider>();
        float d = (wheelRadius + grabMargin) * 2f;
        box.size = new Vector3(d, d, 0.12f + grabMargin * 2f);
        box.isTrigger = true;
        zone.AddComponent<BNG.GrabbableChild>().ParentGrabbable = wheelGrab;
        foreach (var cc in car.GetComponentsInChildren<Collider>(true))
            if (cc != box && !cc.transform.IsChildOf(wheel.transform)) Physics.IgnoreCollision(box, cc, true);
    }

    void CreateGrabHint()
    {
        var go = new GameObject("GrabHint");
        go.transform.SetParent(wheel.transform, false);
        grabHint = go.AddComponent<LineRenderer>();
        grabHint.useWorldSpace = false;
        grabHint.loop = true;
        const int n = 40;
        grabHint.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            grabHint.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * wheelRadius);
        }
        grabHint.startWidth = grabHint.endWidth = 0.008f;
        var mat = Resources.Load<Material>("VR/UIOverlayAlways");
        if (mat != null) grabHint.sharedMaterial = mat;
        grabHint.startColor = grabHint.endColor = new Color(0.2f, 0.9f, 1f, 0.9f);
        grabHint.enabled = false;
    }

    /// <summary>Grab hint ring + haptic tick on grab / release; logs for device debugging.</summary>
    void UpdateGrabFeedback()
    {
        if (wheelGrab == null) return;
        bool held = wheelGrab.BeingHeld;
        if (held != wasHeld)
        {
            wasHeld = held;
            var ib = BNG.InputBridge.Instance;
            var g = wheelGrab.GetPrimaryGrabber();
            if (ib != null && g != null) ib.VibrateController(0.3f, held ? 0.4f : 0.2f, 0.08f, g.HandSide);
            Debug.Log(held ? "[VR] Steering wheel grabbed" : "[VR] Steering wheel released");
        }
        if (grabHint == null) return;
        bool near = false;
        if (!held && view == View.Cockpit)
        {
            if (grabbers == null) grabbers = rig.GetComponentsInChildren<BNG.Grabber>(false);
            float r = wheelRadius + grabMargin + 0.05f;
            foreach (var gr in grabbers)
                if (gr != null && gr.isActiveAndEnabled && (gr.transform.position - wheel.transform.position).sqrMagnitude < r * r) { near = true; break; }
        }
        if (grabHint.enabled != near) grabHint.enabled = near;
    }

    // ───────────────────────── HUD ─────────────────────────

    void SetupHud()
    {
        var cm = CanvasMannager.Instance;
        if (cm == null) return;
        hud = cm.GamePlayPannel.GetComponentInParent<Canvas>(true);
        if (hud == null) return;
        hud = hud.rootCanvas;
        VRCanvasUtil.MakeWorldSpace(hud, dashWidth);
        hudAnchor = new GameObject("VR HUD Anchor").transform;
        if (compactHud) LayoutHudForVR(cm);
        LayoutPanelsForVR(cm);
        CreatePauseInfo(cm);
    }

    /// <summary>
    /// Pause / fail / complete panels were phone screens: a full-screen picture, the box on the left and a
    /// full-width coin bar. In VR only the box is shown, centred, with the coin / key bar sitting on top of it.
    /// </summary>
    void LayoutPanelsForVR(CanvasMannager cm)
    {
        foreach (var p in new[] { cm.PausePannel, cm.FailPannel, cm.CompletePannel })
        {
            if (p == null) continue;
            var prt = (RectTransform)p.transform;
            var bg = p.GetComponent<UnityEngine.UI.Image>();
            if (bg != null && prt.anchorMin == Vector2.zero && prt.anchorMax == Vector2.one) bg.enabled = false;

            var box = p.transform.Find("Pannel") as RectTransform;
            if (box == null) continue;
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = new Vector2(0f, -20f);

            var bar = p.transform.Find("Header") as RectTransform;
            if (bar == null) continue;
            float barH = bar.rect.height;
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.sizeDelta = new Vector2(box.rect.width, barH);
            bar.anchoredPosition = new Vector2(0f, box.anchoredPosition.y + box.rect.height * 0.5f + barH * 0.5f + 6f);
        }
    }

    /// <summary>
    /// The phone HUD is spread over the screen corners. In VR it becomes one cluster at the top-centre of the
    /// HUD canvas (timer + fuel on the left, level + flight status on the right), which sits just above the wheel.
    /// The on-screen Pause / Info buttons are hidden: Menu button = pause, the pause panel shows the objective.
    /// </summary>
    void LayoutHudForVR(CanvasMannager cm)
    {
        var cb = cm.GamePlayPannel.transform.Find("Controller Buttons");
        if (cb == null) return;
        void Place(string n, Vector2 pos)
        {
            var t = cb.Find(n) as RectTransform;
            if (t == null) return;
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 1f);
            t.anchoredPosition = pos;
        }
        Place("TimmerText", new Vector2(-230f, -50f));
        Place("Fuel Text", new Vector2(-230f, -140f));
        Place("FuelBTN", new Vector2(-40f, -144f));
        Place("Key Text", new Vector2(-260f, -230f));
        Place("LevelTitle", new Vector2(190f, -55f));
        Place("FlyStatusHUD", new Vector2(190f, -138f));
        Place("Distance Text", new Vector2(190f, -230f));
        foreach (var n in new[] { "PauseBTN", "InfoIBtn" })
        {
            var t = cb.Find(n);
            if (t != null) t.gameObject.SetActive(false);
        }

        // Fuel button: show which controller button opens it.
        var fuel = cb.Find("FuelBTN");
        if (fuel != null && fuel.Find("VR Hint") == null)
            MakeLabel(fuel, "VR Hint", "L-STICK", 18, new Vector2(0f, -52f), new Vector2(120f, 26f), cm);
    }

    /// <summary>Objective + "MENU = resume" line inside the pause panel (replaces the HUD Info button).</summary>
    void CreatePauseInfo(CanvasMannager cm)
    {
        if (cm.PausePannel == null) return;
        var box = cm.PausePannel.transform.Find("Pannel");
        if (box == null) box = cm.PausePannel.transform;
        var old = box.Find("VR Objective");
        pauseInfo = old != null ? old.GetComponent<UnityEngine.UI.Text>()
                                : MakeLabel(box, "VR Objective", "", 26, Vector2.zero, new Vector2(620f, 260f), cm);
        var rt = (RectTransform)pauseInfo.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.56f);
        rt.anchoredPosition = Vector2.zero;
        pauseInfo.supportRichText = true;
    }

    UnityEngine.UI.Text MakeLabel(Transform parent, string name, string text, int size, Vector2 pos, Vector2 box, CanvasMannager cm)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<UnityEngine.UI.Text>();
        var font = cm.CurrentLevel_ObjText != null ? cm.CurrentLevel_ObjText.font : null;
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = size;
        VRCanvasUtil.ApplyOnTopMaterial(hud);
        return t;
    }

    void HideTouchControls()
    {
        if (hud == null) return;
        // RCC_MobileButtons re-shows the touch controls every frame and isn't needed (input comes from VRDriveInput).
        foreach (var mob in FindObjectsByType<RCC_MobileButtons>(FindObjectsInactive.Include, FindObjectsSortMode.None)) mob.enabled = false;
        // On-screen pedals / steering arrows are replaced by the controllers and the steering wheel.
        foreach (var mb in hud.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n == "RCC_UIController" || n == "RCC_UISteeringWheel" || n == "RCC_UISteeringWheelController" || n == "RCC_UIJoystick" ||
                n == "UIAxisButton" || n == "UIAxis" || n == "UIFlyButton") // fly = hold X on the controller
                mb.gameObject.SetActive(false);
        }
    }

    // ───────────────────────── per frame ─────────────────────────

    void Update()
    {
        if (!begun) return;
        var ib = BNG.InputBridge.Instance;
        if (ib == null) return;

        // Menu button: pause / resume.
        if (ib.StartButtonDown || ib.BackButtonDown) TogglePause();
        if (ib.YButtonDown) { view = view == View.Cockpit ? View.Chase : View.Cockpit; ApplyView(); }
        if (ib.RightThumbstickDown) Recenter();

        bool driving = OverAllGameMannager.OA_GM != null && OverAllGameMannager.OA_GM.GameStart;

        // Left stick click: refuel panel (same as the HUD fuel button; hidden when fuel is unlimited).
        var cmgr = CanvasMannager.Instance;
        if (driving && ib.LeftThumbstickDown && cmgr != null && cmgr.ExteraFuel_GameplayBtn != null && cmgr.ExteraFuel_GameplayBtn.activeInHierarchy)
        {
            cmgr.ExterFuelGamePlayBtnFun();
            driving = false;
        }

        if (!driving)
        {
            VRDriveInput.Clear();
            FlyingCarInput.XRHeld = false;
            return;
        }

        VRDriveInput.Throttle = Mathf.Clamp01(ib.RightTrigger);
        VRDriveInput.Brake = Mathf.Clamp01(ib.LeftTrigger);
        VRDriveInput.Handbrake = ib.AButton ? 1f : 0f;
        VRDriveInput.Boost = ib.BButton ? 1f : 0f;

        float steer = 0f;
        bool wheelHeld = wheelGrab != null && wheelGrab.BeingHeld && view == View.Cockpit;
        if (wheelHeld) steer = Mathf.Clamp(wheel.Angle / Mathf.Max(30f, fullLockAngle) * wheelSteerSign, -1f, 1f);
        else
        {
            float x = ib.LeftThumbstickAxis.x;
            steer = Mathf.Abs(x) > stickDeadZone ? Mathf.Sign(x) * Mathf.InverseLerp(stickDeadZone, 1f, Mathf.Abs(x)) : 0f;
        }
        VRDriveInput.Steer = steer;
        FlyingCarInput.XRHeld = ib.XButton;
    }

    void LateUpdate()
    {
        if (!begun) return;
        if (view == View.Chase) FollowChase();
        PlaceHud();
        SpinInteriorWheel();
        UpdateGrabFeedback();
        UpdateLasers();
        UpdatePauseInfo();
    }

    /// <summary>UI lasers on only while a panel needs clicking.</summary>
    void UpdateLasers()
    {
        if (laserPointers == null) return;
        bool want = !lasersOnlyOnPanels || VRCanvasUtil.AnyModalOpen();
        if (want == lasersOn) return;
        lasersOn = want;
        foreach (var p in laserPointers) if (p != null) p.SetActive(want);
    }

    void UpdatePauseInfo()
    {
        if (pauseInfo == null || !pauseInfo.gameObject.activeInHierarchy) return;
        var cm = CanvasMannager.Instance;
        string obj = cm != null && cm.CurrentLevel_ObjText != null ? cm.CurrentLevel_ObjText.text : "";
        string text = (string.IsNullOrEmpty(obj) ? "" : "<color=#FFD54A>OBJECTIVE</color>\n" + obj + "\n\n") +
                      "<size=20>Press the MENU button to resume</size>";
        if (pauseInfo.text != text) pauseInfo.text = text;
    }

    /// <summary>The car's own steering wheel model turns with the player's input (visual only).</summary>
    void SpinInteriorWheel()
    {
        if (interiorWheel == null || seat == null) return;
        var t = interiorWheel.transform;
        t.localPosition = interiorWheelLocalPos;
        t.localRotation = interiorWheelLocalRot;
        float angle = wheelGrab != null && wheelGrab.BeingHeld ? wheel.Angle : VRDriveInput.Steer * fullLockAngle * wheelSteerSign;
        Vector3 center = interiorWheel.bounds.center;
        // Axis points from the driver to the wheel: a negative angle turns it clockwise as the driver sees it,
        // so steering right (Steer > 0 → angle < 0) turns the rim's top to the right, same way as the hands.
        t.RotateAround(center, (center - seat.position).normalized, angle * wheelModelSign);
    }

    void TogglePause()
    {
        var cm = CanvasMannager.Instance;
        if (cm == null) return;
        // Menu = "back to driving" for the panels that pause the drive, otherwise pause.
        if (cm.PausePannel != null && cm.PausePannel.activeSelf) cm.GameResumeFun();
        else if (cm.FuelRewardPannel != null && cm.FuelRewardPannel.activeSelf) cm.BackFuelBtnFun();
        else if (cm.ObjectivePannel != null && cm.ObjectivePannel.activeSelf) cm.HideObjectiveLevelInfo();
        else if (OverAllGameMannager.OA_GM != null && OverAllGameMannager.OA_GM.GameStart) cm.GamePauseFun();
    }

    // ───────────────────────── views ─────────────────────────

    void ApplyView()
    {
        if (seat == null) return;
        if (view == View.Cockpit)
        {
            rig.SetParent(seat, true);
            Recenter();
        }
        else
        {
            rig.SetParent(null, true);
            chaseYaw = car.transform.eulerAngles.y;
            FollowChase(true);
        }
        if (wheel != null) wheel.gameObject.SetActive(view == View.Cockpit);
        PlaceGuideArrow();
    }

    [Header("Guide arrow")]
    [Tooltip("Cockpit view: the car's direction arrow floats this far ahead of the driver's eyes (outside the windscreen).")]
    public Vector3 cockpitArrowOffset = new Vector3(0f, -0.35f, 4f); // a little below the eyes: seen from above, over the bonnet
    Transform guideArrow;
    Vector3 guideArrowLocal;
    bool guideArrowFound;

    /// <summary>
    /// The level's direction arrow sits on the car roof for the old third-person camera; from the driver's seat it
    /// was cut by the roof. In cockpit view it floats ahead over the bonnet at eye height, in chase view it goes back.
    /// </summary>
    void PlaceGuideArrow()
    {
        if (!guideArrowFound)
        {
            guideArrowFound = true;
            foreach (var t in car.GetComponentsInChildren<ArrowOnlyLookAtDestination>(true)) { guideArrow = t.transform; break; }
            if (guideArrow != null) guideArrowLocal = guideArrow.localPosition;
        }
        if (guideArrow == null || seat == null) return;
        if (view == View.Cockpit)
        {
            Vector3 s = car.transform.InverseTransformPoint(seat.position);
            guideArrow.localPosition = new Vector3(cockpitArrowOffset.x, s.y + cockpitArrowOffset.y, s.z + cockpitArrowOffset.z);
        }
        else guideArrow.localPosition = guideArrowLocal;
    }

    /// <summary>Moves the rig so the player's head sits at the driver's eye point, facing forward.</summary>
    public void Recenter()
    {
        if (seat == null || head == null) return;
        if (view == View.Chase) { FollowChase(true); return; }
        Vector3 hf = head.forward; hf.y = 0f;
        Vector3 sf = seat.forward; sf.y = 0f;
        if (hf.sqrMagnitude > 0.001f && sf.sqrMagnitude > 0.001f)
            rig.RotateAround(head.position, Vector3.up, Vector3.SignedAngle(hf, sf, Vector3.up));
        rig.position += seat.position - head.position;
    }

    void FollowChase(bool snap = false)
    {
        Vector3 cf = car.transform.forward; cf.y = 0f;
        float targetYaw = cf.sqrMagnitude > 0.001f ? Quaternion.LookRotation(cf).eulerAngles.y : chaseYaw;
        chaseYaw = snap ? targetYaw : Mathf.LerpAngle(chaseYaw, targetYaw, Time.deltaTime * chaseYawFollow);
        Quaternion yaw = Quaternion.Euler(0f, chaseYaw, 0f);
        Vector3 eye = car.transform.position - yaw * Vector3.forward * chaseDistance + Vector3.up * chaseHeight;

        // Turn the rig around the head so the head's yaw matches the chase yaw, then move the head to the eye point.
        Vector3 hf = head.forward; hf.y = 0f;
        Vector3 want = yaw * Vector3.forward;
        if (snap && hf.sqrMagnitude > 0.001f) rig.RotateAround(head.position, Vector3.up, Vector3.SignedAngle(hf, want, Vector3.up));
        else
        {
            float rigYaw = Mathf.LerpAngle(rig.eulerAngles.y, chaseYaw, Time.deltaTime * chaseYawFollow);
            rig.RotateAround(head.position, Vector3.up, rigYaw - rig.eulerAngles.y);
        }
        Vector3 delta = eye - head.position;
        rig.position += snap ? delta : Vector3.Lerp(Vector3.zero, delta, Time.deltaTime * 8f);
    }

    void PlaceHud()
    {
        if (hud == null || head == null) return;
        bool modal = VRCanvasUtil.AnyModalOpen();
        var openBox = modal ? OpenPanelBox() : null;
        if (modal != modalShown || openBox != modalBox) // also when one panel replaces another
        {
            modalShown = modal;
            modalBox = openBox;
            var rt = (RectTransform)hud.transform;
            float scale = (modal ? focusWidth : dashWidth) / rt.sizeDelta.x;
            if (modal && openBox != null && openBox.rect.width > 1f && openBox.rect.height > 1f)
            {
                // Size the open panel itself (its box is often a small part of the phone-screen canvas),
                // not the whole canvas: pause / fail / complete were tiny before.
                float boxW = openBox.rect.width * openBox.lossyScale.x / Mathf.Max(1e-6f, rt.lossyScale.x);
                float boxH = openBox.rect.height * openBox.lossyScale.y / Mathf.Max(1e-6f, rt.lossyScale.y);
                scale = Mathf.Min(panelWidth / boxW, panelMaxHeight / boxH);
            }
            rt.localScale = Vector3.one * scale;
            if (modal)
            {
                // Panels appear straight ahead of where the player looks, then stay put.
                Vector3 f = head.forward; f.y = 0f; if (f.sqrMagnitude < 0.001f) f = seat.forward; f.Normalize();
                Vector3 p = head.position + f * focusOffset.z + Vector3.up * focusOffset.y;
                hud.transform.SetParent(view == View.Cockpit ? seat : null, true);
                hud.transform.SetPositionAndRotation(p, Quaternion.LookRotation(p - head.position));
                // Phone layouts put some panels off to one side (e.g. pause on the left): centre the open panel's box.
                if (openBox != null) hud.transform.position += p - openBox.TransformPoint(openBox.rect.center);
                return;
            }
        }
        if (modal) return;

        // Driving HUD: the top strip (timer / fuel / level) sits just above the steering wheel, tilted to the eyes.
        Transform anchor = view == View.Cockpit ? seat : head;
        Vector3 fwd = anchor.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward; fwd.Normalize();
        Vector3 basePos = view == View.Cockpit ? seat.position : head.position;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 top = basePos + fwd * dashOffset.z + Vector3.up * dashOffset.y + right * dashOffset.x;
        Quaternion rot = Quaternion.LookRotation(top - basePos);
        var hrt = (RectTransform)hud.transform;
        float height = dashWidth * hrt.sizeDelta.y / Mathf.Max(1f, hrt.sizeDelta.x);
        Vector3 pos = top - rot * Vector3.up * (height * 0.5f);
        if (view == View.Cockpit)
        {
            hud.transform.SetParent(seat, true);
            hud.transform.SetPositionAndRotation(pos, rot);
        }
        else
        {
            hud.transform.SetParent(null, true);
            hud.transform.position = Vector3.Lerp(hud.transform.position, pos, Time.deltaTime * 4f);
            hud.transform.rotation = Quaternion.Slerp(hud.transform.rotation, rot, Time.deltaTime * 4f);
        }
    }

    /// <summary>The main box of the panel that is open now (its "Pannel" / "Panel" child, or the panel itself).</summary>
    RectTransform OpenPanelBox()
    {
        var cm = CanvasMannager.Instance;
        if (cm == null) return null;
        foreach (var p in new[] { cm.PausePannel, cm.FailPannel, cm.CompletePannel, cm.FuelRewardPannel, cm.ObjectivePannel, cm.InfoPannel })
        {
            if (p == null || !p.activeInHierarchy) continue;
            float canvasW = ((RectTransform)hud.transform).rect.width;
            RectTransform best = null; float bestArea = 0f;
            foreach (Transform c in p.transform)
            {
                if (!c.gameObject.activeSelf || !(c is RectTransform r)) continue;
                var size = r.rect.size;
                if (size.x >= canvasW * 0.95f) continue; // full-width bars / dim backgrounds
                if (size.x * size.y > bestArea) { bestArea = size.x * size.y; best = r; }
            }
            return best != null ? best : (RectTransform)p.transform;
        }
        return null;
    }

    void OnDisable()
    {
        VRDriveInput.Active = false;
        VRDriveInput.Clear();
        FlyingCarInput.XRHeld = false;
    }
}
