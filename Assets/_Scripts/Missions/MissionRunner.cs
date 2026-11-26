using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds and runs the data-driven levels (Chapter 2 to 10) inside the GamePlay scene.
/// Chapter 1 levels are hand-built and untouched: for them this component does nothing.
///
/// Flow: Start() lays the level out (seeded by the level number) while the objective panel
/// is open → CanvasMannager.OkBtnFun() calls Begin() → steps run one by one → GameCompleteFun().
/// </summary>
[DefaultExecutionOrder(100)]
public class MissionRunner : MonoBehaviour
{
    public static MissionRunner Instance { get; private set; }

    /// <summary>True while a generated mission is actively being played (not paused / failed / finished).</summary>
    public static bool Running =>
        Instance != null && Instance.begun && !Instance.ended &&
        OverAllGameMannager.OA_GM != null && OverAllGameMannager.OA_GM.GameStart;

    [Tooltip("Prefabs and materials used to dress missions. Filled by Tools > Missions > Setup.")]
    public MissionPrefabs prefabs = new MissionPrefabs();
    [Tooltip("Optional: overrides Resources/Missions/MissionDatabase.")]
    public MissionDatabase databaseOverride;

    public MissionDef Def { get; private set; }
    public MissionHUD Hud { get; private set; }
    public bool ChoseAlternate { get; set; }
    public Transform CurrentTarget => target;
    public int CurrentStepIndex => stepIndex;
    public string CurrentStepName => stepIndex < steps.Count ? steps[stepIndex].GetType().Name : "Done";
    public bool Ended => ended;
    public float BuildMilliseconds { get; private set; }

    // Player state, refreshed every frame.
    public Transform PlayerTransform { get; private set; }
    public Vector3 PlayerPos { get; private set; }
    public Vector3 PlayerVelocity { get; private set; }
    public Vector3 PlayerForward { get; private set; }
    public float PlayerKmh { get; private set; }
    public bool Grounded => car != null && car.isGrounded;

    SaveData data;
    RCC_CarControllerV3 car;
    Rigidbody carRb;
    System.Random rng;
    readonly List<MissionStepRuntime> steps = new List<MissionStepRuntime>();
    MissionHazards hazards;
    MissionBuildContext ctx;
    int stepIndex;
    bool begun, ended;
    int crashes, strikes;
    float crashCooldownUntil, controlLoss, overSpeed, reversing, distanceTimer, speedWarn;
    float originalFuel = -1f;
    Transform target;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        RestoreFuel();
    }

    void Start()
    {
        if (OverAllGameMannager.OA_GM == null || GameMannager.Instance == null) { enabled = false; return; }
        data = OverAllGameMannager.OA_GM.SaveData;
        MissionSaveUtil.EnsureCampaign(data);

        Def = databaseOverride != null && databaseOverride.missions.Count > 0
            ? databaseOverride.missions[Mathf.Clamp(data.CurrentChapter * 10 + data.CurrentLevel, 0, databaseOverride.missions.Count - 1)]
            : MissionDatabase.Get(data.CurrentChapter, data.CurrentLevel);

        if (Def == null || Def.handBuilt) { Def = null; enabled = false; return; }
        Def.chapter = data.CurrentChapter;
        Def.level = data.CurrentLevel;

        var carGo = GameMannager.Instance.Cars[data.CurrentCar];
        car = carGo.GetComponent<RCC_CarControllerV3>();
        carRb = carGo.GetComponent<Rigidbody>();
        PlayerTransform = carGo.transform;
        MissionWorld.ResetCaches();
        MissionWorld.SetIgnoreRoot(PlayerTransform);

        MissionVisuals.P = prefabs;
        MissionVisuals.Root = new GameObject("MissionObjects").transform;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Build();
        SetupUI();
        BuildMilliseconds = (float)sw.Elapsed.TotalMilliseconds;
    }

    // ───────────────────────── build ─────────────────────────

    void Build()
    {
        rng = new System.Random(Def.GlobalNumber * 7919 + 13);
        ctx = new MissionBuildContext { rng = rng, def = Def, area = Def.area };
        ctx.avgSpeed = Def.area == MissionArea.Road ? 11f : 9f;

        foreach (var s in Def.steps)
        {
            var rt = MissionStepRuntime.Create(s);
            rt.Init(this, s, rng);
            steps.Add(rt);
        }

        // Start position: a step may need a special one (ramp, speed strip).
        Vector3 reference = GameMannager.Instance.LevelPos.Length > 2 && GameMannager.Instance.LevelPos[2] != null
            ? GameMannager.Instance.LevelPos[2].transform.position : PlayerTransform.position;
        // That point is in the garage alley (props block it off on the navigation map): use the nearest road instead,
        // so routes from it reach the whole city.
        if (MissionWorld.RoadMask != 0 &&
            UnityEngine.AI.NavMesh.SamplePosition(reference, out UnityEngine.AI.NavMeshHit roadHit, 80f, MissionWorld.RoadMask))
            reference = roadHit.position;
        bool forced = steps.Count > 0 && steps[0].ChooseStart(ctx);
        Vector3 start;
        if (forced) start = ctx.forcedStart.Value;
        else if (Def.area == MissionArea.Road || Def.area == MissionArea.Any)
        {
            if (!MissionWorld.PickPoint(rng, reference, 20f, 420f, MissionArea.Road, out start, out _)) start = reference;
        }
        else if (!MissionWorld.FindAnchor(rng, Def.area, out start))
        {
            if (!MissionWorld.PickPoint(rng, reference, 20f, 420f, MissionArea.Any, out start, out _)) start = reference;
        }
        ctx.start = start;
        ctx.cursor = start;

        foreach (var s in steps) s.Prepare(ctx);

        // Heading: along the first route, or the forced one.
        Vector3 heading = forced ? ctx.forcedHeading : Vector3.forward;
        if (!forced && ctx.mainRoute != null && ctx.mainRoute.Count > 1)
        {
            for (int i = 1; i < ctx.mainRoute.Count; i++)
            {
                Vector3 d = MissionWorld.Flat(ctx.mainRoute[i] - ctx.mainRoute[0]);
                if (d.magnitude > 3f) { heading = d.normalized; break; }
            }
        }
        PlaceCar(start, heading);

        if (ctx.mainRoute == null)
        {
            var path = new List<Vector3>();
            if (MissionWorld.PickPoint(rng, start, 200f, 350f, Def.area, out _, out _, path)) ctx.mainRoute = path;
        }
        BuildProps();

        // Time limit.
        float est = 0f;
        foreach (var s in steps) est += s.Estimate;
        float seconds = Def.fixedTime > 0f ? Def.fixedTime : Mathf.Clamp((est + 25f) * Mathf.Max(0.3f, Def.timeFactor), 45f, 900f);
        seconds = Mathf.Ceil(seconds / 5f) * 5f;
        int min = Mathf.FloorToInt(seconds / 60f);
        int sec = Mathf.RoundToInt(seconds - min * 60f);
        var lv = data.Chapters[Def.chapter].levels[Def.level];
        lv.minutes = min;
        lv.seconds = sec;
        if (Timer.Instance != null) Timer.Instance.TimeAdjustmentFun(min, sec);

        // Fuel missions.
        if (Def.startFuel > 0f && car != null && !data.HasUnlimitedFuel)
        {
            originalFuel = data.Cars[data.CurrentCar].CarFuel;
            car.fuelTank = Mathf.Min(car.fuelTank, Def.startFuel);
        }

        MissionEnvironment.Apply(Def.env, car, GameMannager.Instance.RccCam != null ? GameMannager.Instance.RccCam.transform : null, prefabs.beamMaterial);
    }

    void PlaceCar(Vector3 pos, Vector3 heading)
    {
        heading.y = 0f;
        if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(heading.normalized);
        Vector3 p = pos + Vector3.up * 0.6f;
        PlayerTransform.SetPositionAndRotation(p, rot);
        if (carRb != null)
        {
            carRb.position = p;
            carRb.rotation = rot;
            if (!carRb.isKinematic) { carRb.linearVelocity = Vector3.zero; carRb.angularVelocity = Vector3.zero; }
        }
        Physics.SyncTransforms();
    }

    void BuildProps()
    {
        var route = ctx.mainRoute;
        if (Def.props == PropKind.None || route == null || route.Count < 2) return;
        float len = MissionWorld.Length(route);
        int n = Mathf.Max(1, Def.propCount);
        float from = 28f, to = Mathf.Max(from + 10f, len - 15f);

        switch (Def.props)
        {
            case PropKind.ParkedCars:
                for (int i = 0; i < n; i++)
                {
                    float d = Mathf.Lerp(from, to, (i + 0.5f) / n);
                    Vector3 p = MissionWorld.Along(route, d, out Vector3 tan);
                    p += Vector3.Cross(Vector3.up, tan) * (i % 2 == 0 ? 2.3f : -2.3f);
                    MissionVisuals.ParkedCar(rng, Ground(p), Quaternion.LookRotation(tan));
                }
                break;

            case PropKind.Cones:
                for (int i = 0; i < n; i++)
                {
                    float d = Mathf.Lerp(from, to, (float)rng.NextDouble());
                    Vector3 p = MissionWorld.Along(route, d, out Vector3 tan);
                    p += Vector3.Cross(Vector3.up, tan) * Mathf.Lerp(-4.5f, 4.5f, (float)rng.NextDouble());
                    bool barrier = i % 5 == 4 && prefabs.barrier != null;
                    var go = MissionVisuals.Spawn(barrier ? prefabs.barrier : prefabs.cone, Ground(p), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    if (go == null) MissionVisuals.Prim(PrimitiveType.Cylinder, MissionVisuals.Root, Ground(p) + Vector3.up * 0.4f, new Vector3(0.5f, 0.4f, 0.5f), prefabs.solidMaterial, MissionVisuals.Orange, "Cone", true);
                }
                break;

            case PropKind.Pedestrians:
                for (int i = 0; i < n; i++)
                {
                    float d = Mathf.Lerp(from, to, (i + (float)rng.NextDouble()) / n);
                    Vector3 p = MissionWorld.Along(route, d, out Vector3 tan);
                    Vector3 side = Vector3.Cross(Vector3.up, tan);
                    p += side * Mathf.Lerp(-2.5f, 2.5f, (float)rng.NextDouble());
                    MissionVisuals.Pedestrian(rng, Ground(p), side, Mathf.Lerp(6f, 10f, (float)rng.NextDouble()));
                }
                break;

            case PropKind.TrafficJam:
                {
                    float d = from;
                    int placed = 0, row = 0;
                    while (placed < n && d < to)
                    {
                        Vector3 p = MissionWorld.Along(route, d, out Vector3 tan);
                        Vector3 side = Vector3.Cross(Vector3.up, tan);
                        float jitter = Mathf.Lerp(-8f, 8f, (float)rng.NextDouble());
                        Quaternion rot = Quaternion.LookRotation(Quaternion.Euler(0f, jitter, 0f) * tan);
                        // Alternate lanes so a weaving line always stays open.
                        float lane = row % 2 == 0 ? 2.8f : -2.8f;
                        MissionVisuals.ParkedCar(rng, Ground(p + side * lane), rot);
                        placed++;
                        if (row % 3 == 2 && placed < n) { MissionVisuals.ParkedCar(rng, Ground(p - side * lane * 2.1f), rot); placed++; }
                        d += Mathf.Lerp(9f, 13f, (float)rng.NextDouble());
                        row++;
                    }
                    break;
                }

            case PropKind.Convoy:
                for (int i = 0; i < n; i++)
                {
                    float d = Mathf.Clamp(len * 0.65f + i * 22f, from, len - 5f);
                    var back = new List<Vector3>();
                    for (float k = d; k > 0f; k -= 10f) back.Add(MissionWorld.Along(route, k, out _));
                    back.Add(route[0]);
                    var m = MissionVisuals.Vehicle(i % 2 == 0 ? MoverKind.Armored : MoverKind.Truck, rng, back[0], Quaternion.identity, "Convoy" + i);
                    for (int b = 0; b < back.Count; b++) back[b] += Vector3.Cross(Vector3.up, MissionWorld.Flat(back[Mathf.Min(b + 1, back.Count - 1)] - back[b]).normalized) * 2.6f;
                    m.SetRoute(back, false);
                    m.speed = 5f;
                }
                break;

            case PropKind.Barricade:
                for (int line = 0; line < n; line++)
                {
                    float d = Mathf.Lerp(len * 0.4f, len * 0.8f, n > 1 ? (float)line / (n - 1) : 0.5f);
                    Vector3 p = MissionWorld.Along(route, d, out Vector3 tan);
                    Vector3 side = Vector3.Cross(Vector3.up, tan);
                    for (float x = -6f; x <= 6f; x += 2.2f)
                    {
                        Vector3 q = Ground(p + side * x);
                        GameObject piece = prefabs.barrier != null
                            ? Instantiate(prefabs.barrier, q, Quaternion.LookRotation(side), MissionVisuals.Root)
                            : MissionVisuals.Prim(PrimitiveType.Cube, MissionVisuals.Root, q + Vector3.up * 0.5f, new Vector3(2f, 1f, 0.4f), prefabs.solidMaterial, MissionVisuals.Red, "Barrier", true);
                        MissionVisuals.EnsureCollider(piece);
                        MissionVisuals.SetTagRecursive(piece, "Untagged");
                        var rb = piece.GetComponent<Rigidbody>();
                        if (rb == null) rb = piece.AddComponent<Rigidbody>();
                        rb.mass = 45f;
                        rb.isKinematic = false;
                    }
                }
                break;
        }
    }

    Vector3 Ground(Vector3 p) { p.y = MissionWorld.GroundY(p, p.y); return p; }

    void SetupUI()
    {
        var cm = CanvasMannager.Instance;
        if (cm == null) return;
        if (cm.CurrentLevel_ObjText) cm.CurrentLevel_ObjText.text = Def.objective;
        if (cm.CurrentLevel_Text) cm.CurrentLevel_Text.text = "Level Number " + Def.GlobalNumber;
        Hud = MissionHUD.Create(cm.GamePlayPannel.transform, cm.MidText != null ? cm.MidText.font : null);
        Hud.SetStatus(Def.title.ToUpperInvariant());
        Hud.SetStrikes(StrikesText());
    }

    // ───────────────────────── run ─────────────────────────

    /// <summary>Called by CanvasMannager.OkBtnFun when the player closes the objective panel.</summary>
    public void Begin()
    {
        if (Def == null || begun) return;
        begun = true;

        var cm = CanvasMannager.Instance;
        if (cm != null)
        {
            if (cm.MiniMap) cm.MiniMap.SetActive(true);
            if (cm.DistanceText) cm.DistanceText.SetActive(true);
        }
        var arrow = GameMannager.Instance.Arrow;
        if (arrow != null && data.CurrentCar < arrow.Length && arrow[data.CurrentCar]) arrow[data.CurrentCar].SetActive(true);

        RefreshPlayer();
        if (Def.hazard != HazardKind.None)
        {
            hazards = new MissionHazards(this, Def.hazard, Def.hazardCount, rng);
            hazards.Build(ctx.allRoutes.Count > 1 ? ctx.allRoutes : ctx.mainRoute);
        }
        stepIndex = 0;
        if (steps.Count > 0) steps[0].Begin();
        Popup(Def.title.ToUpperInvariant());
    }

    void Update()
    {
        if (Def == null) return;
        RefreshPlayer();
        if (!Running) return;
        float dt = Time.deltaTime;

        TickRules(dt);
        if (!Running) return;
        hazards?.Tick(dt);
        if (!Running) return;

        if (stepIndex < steps.Count && steps[stepIndex].Tick(dt))
        {
            steps[stepIndex].End();
            stepIndex++;
            if (stepIndex >= steps.Count) { Complete(); return; }
            steps[stepIndex].Begin();
        }

        distanceTimer -= dt;
        if (distanceTimer <= 0f)
        {
            distanceTimer = 0.2f;
            var cm = CanvasMannager.Instance;
            if (cm != null && cm.Distance_Text != null)
                cm.Distance_Text.text = target != null ? Mathf.RoundToInt(MissionWorld.Flat(target.position - PlayerPos).magnitude) + " m" : "--";
        }
    }

    void RefreshPlayer()
    {
        if (PlayerTransform == null) return;
        PlayerPos = PlayerTransform.position;
        PlayerVelocity = carRb != null && !carRb.isKinematic ? carRb.linearVelocity : Vector3.zero;
        Vector3 f = MissionWorld.Flat(PlayerTransform.forward);
        PlayerForward = f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
        PlayerKmh = car != null ? Mathf.Abs(car.speed) : PlayerVelocity.magnitude * 3.6f;
    }

    void TickRules(float dt)
    {
        if (controlLoss > 0f)
        {
            controlLoss -= dt;
            if (car != null) { car.canControl = false; car.brakeInput = 0.6f; }
            if (controlLoss <= 0f && car != null) car.canControl = true;
        }

        if (Def.speedLimitKmh > 0f)
        {
            overSpeed = PlayerKmh > Def.speedLimitKmh ? overSpeed + dt : 0f;
            speedWarn -= dt;
            if (overSpeed > 0.3f && speedWarn <= 0f) { speedWarn = 2f; Popup("Slow down! Limit " + Mathf.RoundToInt(Def.speedLimitKmh) + " km/h"); }
            if (overSpeed > 2.5f) { Fail("TOO FAST! You attracted attention.", FailReason.RedZone); return; }
        }

        if (Def.noReverse && car != null)
        {
            bool rev = car.direction == -1 && PlayerKmh > 3f;
            reversing = rev ? reversing + dt : 0f;
            if (reversing > 1.2f) { Fail("NO REVERSING in this mission!", FailReason.RedZone); return; }
        }

        if (Def.fuelDrainPerSecond > 0f && car != null && !data.HasUnlimitedFuel)
            car.fuelTank = Mathf.Max(0f, car.fuelTank - Def.fuelDrainPerSecond * dt);
    }

    // ───────────────────────── services used by steps & hazards ─────────────────────────

    public void SetTarget(Transform t)
    {
        target = t;
        if (CheckpointManager.Instance != null && t != null) CheckpointManager.Instance.SetActiveCheckpoint(t);
    }

    public void Popup(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var cm = CanvasMannager.Instance;
        if (cm == null || cm.GamePlayMidText == null || cm.MidText == null) return;
        cm.MidText.text = text;
        // Re-enable to restart its auto-hide timer.
        if (cm.GamePlayMidText.activeSelf) cm.GamePlayMidText.SetActive(false);
        cm.GamePlayMidText.SetActive(true);
    }

    public void AddStrike(string message)
    {
        strikes++;
        Hud.SetStrikes(StrikesText());
        if (strikes > Def.maxStrikes)
        {
            Fail(string.IsNullOrEmpty(message) ? "DETECTED! Too many strikes." : message + " Mission failed.", FailReason.RedZone);
            return;
        }
        Popup(message + "  (" + strikes + "/" + Def.maxStrikes + ")");
    }

    string StrikesText()
    {
        string s = "";
        if (Def.hazard != HazardKind.None && Def.hazard != HazardKind.RedZone && Def.hazard != HazardKind.Rocks) s += "STRIKES " + strikes + "/" + Def.maxStrikes;
        if (Def.maxCrashes > 0 && Def.maxCrashes < 9) s += (s.Length > 0 ? "\n" : "") + "HITS " + crashes + "/" + Def.maxCrashes;
        return s;
    }

    public void ControlLoss(float seconds) => controlLoss = Mathf.Max(controlLoss, seconds);

    public void Kick(Vector3 velocityChange)
    {
        if (carRb != null && !carRb.isKinematic) carRb.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    /// <summary>Called by CarDetection for impacts with "Obstacles". Returns true when the mission handled it.</summary>
    public bool InterceptCrash(Collision collision, float impact)
    {
        if (Def == null || !begun || ended) return false;
        if (Time.time < crashCooldownUntil) return true;

        var mover = collision.collider.GetComponentInParent<MissionMover>();
        if (mover != null && stepIndex < steps.Count && steps[stepIndex].OnHit(mover, impact))
        {
            crashCooldownUntil = Time.time + 0.6f;
            return true;
        }

        var ped = collision.collider.GetComponentInParent<MissionPedestrian>();
        if (ped != null) ped.KnockDown();

        crashCooldownUntil = Time.time + 1f;
        crashes++;
        Hud.SetStrikes(StrikesText());
        if (crashes > Def.maxCrashes)
        {
            Fail(ped != null ? "PEDESTRIAN HIT! Mission failed." : "CAR CRASHED! Too many hits.", FailReason.Collision);
            return true;
        }
        Popup(ped != null ? "Watch out for pedestrians!" : "Careful! Hit " + crashes + "/" + Def.maxCrashes);
        return true;
    }

    public void Fail(string text, FailReason reason)
    {
        if (ended || !begun) return;
        var cm = CanvasMannager.Instance;
        if (car != null) car.canControl = true;
        controlLoss = 0f;
        if (cm == null) return;
        if (!OverAllGameMannager.OA_GM.GameStart) return; // a fail panel is already up
        cm.GameFailFun(reason);
        if (cm.Fail_ObjText != null) cm.Fail_ObjText.text = text;
        RestoreFuel();
    }

    void Complete()
    {
        if (ended) return;
        ended = true;
        if (car != null) car.canControl = true;
        Hud.SetStatus("MISSION COMPLETE");
        Hud.SetProgress(1f, MissionVisuals.Green);
        var cm = CanvasMannager.Instance;
        if (cm == null) return;
        cm.GameCompleteFun();
        if (ChoseAlternate && !string.IsNullOrEmpty(Def.altCompleteText) && cm.Complete_ObjText != null)
            cm.Complete_ObjText.text = Def.altCompleteText;
        RestoreFuel();
    }

    void RestoreFuel()
    {
        if (originalFuel < 0f || data == null) return;
        if (data.CurrentCar < data.Cars.Count)
            data.Cars[data.CurrentCar].CarFuel = Mathf.Max(data.Cars[data.CurrentCar].CarFuel, originalFuel);
        originalFuel = -1f;
    }

    // ───────────────────────── static helpers for existing scripts ─────────────────────────

    /// <summary>Objective text for any level: database for generated levels, the scene array for Chapter 1.</summary>
    public static string ObjectiveText(int chapter, int level, string[] handBuiltTexts)
    {
        var m = MissionDatabase.Get(chapter, level);
        if (m != null && !m.handBuilt) return m.objective;
        return handBuiltTexts != null && level >= 0 && level < handBuiltTexts.Length ? handBuiltTexts[level] : "";
    }

    public static string CompleteText(int chapter, int level, string[] handBuiltTexts)
    {
        var m = MissionDatabase.Get(chapter, level);
        if (m != null && !m.handBuilt) return m.completeText;
        return handBuiltTexts != null && level >= 0 && level < handBuiltTexts.Length ? handBuiltTexts[level] : "Mission complete!";
    }
}
