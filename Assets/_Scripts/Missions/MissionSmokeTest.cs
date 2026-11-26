#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only QA helper. Loads generated levels one after another, records how each one
/// was built and (optionally) auto-plays it by moving the car to the current target.
/// Start it in Play mode: new GameObject("SmokeTest").AddComponent&lt;MissionSmokeTest&gt;().
/// Results are written to Logs/MissionSmokeTest.txt.
/// </summary>
public class MissionSmokeTest : MonoBehaviour
{
    public int from = 10, to = 49;
    [Tooltip("Optional explicit list of level numbers (1-50). Overrides from/to.")]
    public int[] levels;
    public bool autoPlay = true;
    public float maxSecondsPerLevel = 45f;
    public float timeScale = 3f;
    [Tooltip("Drive level N with car N % 10, so every car is tested on real levels.")]
    public bool cycleCars;
    public static string Status = "";
    public static bool Done;

    readonly StringBuilder log = new StringBuilder();
    int errorsThisLevel;
    string firstError;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Application.logMessageReceived += OnLog;
        Done = false;
    }

    void OnDestroy() => Application.logMessageReceived -= OnLog;

    void OnLog(string msg, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        if (msg.StartsWith("ADmob")) return;
        errorsThisLevel++;
        if (firstError == null) firstError = msg + " @ " + (stack ?? "").Split('\n')[0];
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        var data = OverAllGameMannager.OA_GM.SaveData;
        MissionSaveUtil.EnsureCampaign(data);
        bool unlimited = data.HasUnlimitedFuel;
        data.HasUnlimitedFuel = true;

        var list = new System.Collections.Generic.List<int>();
        if (levels != null && levels.Length > 0) foreach (var n in levels) list.Add(n - 1);
        else for (int i = from; i <= to; i++) list.Add(i);

        foreach (int i in list)
        {
            errorsThisLevel = 0;
            firstError = null;
            MissionStepRuntime.Fallbacks = 0;
            data.CurrentChapter = i / 10;
            data.CurrentLevel = i % 10;
            if (cycleCars) data.CurrentCar = i % Mathf.Max(1, data.Cars.Count);
            Time.timeScale = 1f;
            Status = "Level " + (i + 1) + " loading";
            SceneManager.LoadScene("GamePlay");
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.4f);

            var r = MissionRunner.Instance;
            if (r == null || r.Def == null)
            {
                // Hand-built level (Chapter 1): start it and go to its finish trigger.
                yield return HandBuilt(i, data);
                continue;
            }
            var lv = data.Chapters[data.CurrentChapter].levels[data.CurrentLevel];
            string line = "car" + (data.CurrentCar + 1) + " " + string.Format("{0,3} {1,-22} build={2,5:0}ms time={3}:{4:00} fallbacks={5} start={6}",
                i + 1, r.Def.title, r.BuildMilliseconds, lv.minutes, lv.seconds, MissionStepRuntime.Fallbacks, Round(r.PlayerTransform.position));

            string result = "";
            if (autoPlay)
            {
                var car = r.PlayerTransform.GetComponent<RCC_CarControllerV3>();
                RCC.RegisterPlayerVehicle(car);
                CanvasMannager.Instance.OkBtnFun();
                Time.timeScale = timeScale;
                float t = 0f;
                while (t < maxSecondsPerLevel)
                {
                    yield return new WaitForSeconds(0.25f);
                    t += 0.25f;
                    var cm = CanvasMannager.Instance;
                    if (cm.CompletePannel.activeSelf) { result = "COMPLETE in " + t.ToString("0") + "s"; break; }
                    if (cm.FailPannel.activeSelf) { result = "FAIL at step " + r.CurrentStepIndex + " (" + r.CurrentStepName + "): " + cm.Fail_ObjText.text; break; }
                    Drive(r);
                }
                if (result == "") result = "RUNNING at step " + r.CurrentStepIndex + " (" + r.CurrentStepName + ") after " + maxSecondsPerLevel + "s";
                Time.timeScale = 1f;
            }
            if (errorsThisLevel > 0) result += "  ERRORS=" + errorsThisLevel + " first: " + firstError;
            log.AppendLine(line + "  " + result);
            Status = log.ToString();
        }

        data.HasUnlimitedFuel = unlimited;
        System.IO.File.WriteAllText("Logs/MissionSmokeTest.txt", log.ToString());
        Done = true;
        Status = log.ToString();
    }

    /// <summary>Hand-built levels 1-10: OK → (Level 1: gas + brake) / drive to the LevelComplete trigger.</summary>
    IEnumerator HandBuilt(int i, SaveData data)
    {
        var gm = GameMannager.Instance;
        if (gm == null) { log.AppendLine((i + 1) + ": NO GAME MANAGER"); yield break; }
        int lvl = data.CurrentLevel;
        var car = gm.Cars[data.CurrentCar];
        var lv = data.Chapters[data.CurrentChapter].levels[lvl];
        string line = "car" + (data.CurrentCar + 1) + " " + string.Format("{0,3} {1,-22} time={2}:{3:00} start={4}", i + 1, "Hand-built " + (lvl + 1), lv.minutes, lv.seconds, Round(car.transform.position));
        RCC.RegisterPlayerVehicle(car.GetComponent<RCC_CarControllerV3>());
        CanvasMannager.Instance.OkBtnFun();
        var levelObj = lvl < gm.Levels.Length ? gm.Levels[lvl] : null;
        string result = "";
        float t = 0f;
        Collider finish = null;
        if (levelObj != null)
            foreach (var c in levelObj.GetComponentsInChildren<Collider>(true)) if (c.CompareTag("LevelComplete")) { finish = c; break; }
        var tutorial = levelObj != null ? levelObj.GetComponentInChildren<Level1Complete>(true) : null;
        var det = car.GetComponent<CarDetection>();
        if (det != null) det.minImpactForce = 1e9f; // teleport bumps are not the player's crashes
        if (finish == null && tutorial == null) result = "NO FINISH TRIGGER";
        while (result == "" && t < maxSecondsPerLevel)
        {
            yield return new WaitForSeconds(0.25f);
            t += 0.25f;
            var cm = CanvasMannager.Instance;
            if (cm.CompletePannel.activeSelf) { result = "COMPLETE in " + t.ToString("0") + "s"; break; }
            if (cm.FailPannel.activeSelf) { result = "FAIL: " + cm.Fail_ObjText.text; break; }
            if (tutorial != null) { tutorial.Race_Press_Count += 30; tutorial.Brake_Press_Count += 30; continue; }
            var rb = car.GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic || t < 1f) continue;
            // Multi-stage levels (pick-up points, battery cells, safe points) reveal the finish one stage at a time:
            // go to the first active stage trigger, the finish once it is shown.
            Collider target = null;
            foreach (var c in levelObj.GetComponentsInChildren<Collider>(false))
                if (c.isTrigger && (c.GetComponent<PickUpPoint>() || c.GetComponent<FuelCheckPoint>() || c.CompareTag("LevelComplete"))) { target = c; break; }
            if (target == null) continue;
            Vector3 pos = target.bounds.center;
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y + 0.9f;
            rb.position = pos; rb.linearVelocity = car.transform.forward * 3f; rb.angularVelocity = Vector3.zero;
        }
        if (result == "") result = "NOT FINISHED after " + maxSecondsPerLevel + "s";
        if (det != null) det.minImpactForce = 5f;
        if (errorsThisLevel > 0) result += "  ERRORS=" + errorsThisLevel + " first: " + firstError;
        log.AppendLine(line + "  " + result);
        Status = log.ToString();
        Time.timeScale = 1f;
    }

    static string Round(Vector3 v) => "(" + Mathf.RoundToInt(v.x) + "," + Mathf.RoundToInt(v.y) + "," + Mathf.RoundToInt(v.z) + ")";

    /// <summary>Crude auto-pilot: teleports the car to (or beside) whatever the mission points at.</summary>
    void Drive(MissionRunner r)
    {
        var tgt = r.CurrentTarget;
        var rb = r.PlayerTransform.GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic) return;
        string step = r.CurrentStepName;
        // The autopilot teleports next to traffic / targets: those bumps are not the player's crashes.
        // Ram missions keep crash detection (hitting the target is the goal).
        var det = r.PlayerTransform.GetComponent<CarDetection>();
        if (det != null) det.minImpactForce = step == "RamStep" ? 5f : 1e9f;

        if (step == "EvadeStep" || step == "SurviveStep" || step == "SpeedBandStep" || step == "SpeedTrialStep" || step == "JumpStep")
        {
            // Keep away from pursuers / just cruise forward at a legal speed.
            Vector3 fwd = r.PlayerForward;
            float kmh = step == "SpeedTrialStep" ? 130f : step == "JumpStep" ? 100f : step == "SpeedBandStep" ? 75f : 50f;
            if (step == "EvadeStep" && tgt != null)
            {
                Teleport(r, rb, tgt.position, fwd, 0f);
                return;
            }
            rb.linearVelocity = fwd * (kmh / 3.6f) + Vector3.up * rb.linearVelocity.y;
            return;
        }
        if (tgt == null) return;

        var mover = tgt.GetComponent<MissionMover>();
        if (mover != null)
        {
            Vector3 f = mover.transform.forward;
            Vector3 pos;
            float speed = mover.CurrentSpeed;
            if (step == "PaceStep") pos = mover.transform.position + mover.transform.right * 4.5f;
            else if (step == "RamStep") { pos = mover.transform.position - f * 7f; speed += 12f; }
            else pos = mover.transform.position - f * 14f;
            Teleport(r, rb, pos, f, speed);
            return;
        }
        var beacon = tgt.GetComponent<MissionBeacon>();
        if (beacon != null)
        {
            Vector3 f = MissionWorld.Flat(tgt.position - r.PlayerPos);
            float speed = 0f;
            if (step == "CheckpointsStep") speed = 75f / 3.6f;
            if (step == "TowStep") speed = 0f;
            Teleport(r, rb, tgt.position, f.sqrMagnitude > 0.01f ? f.normalized : r.PlayerForward, speed);
        }
    }

    static void Teleport(MissionRunner r, Rigidbody rb, Vector3 pos, Vector3 fwd, float speed)
    {
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        pos.y = MissionWorld.GroundY(pos, pos.y) + 0.8f;
        rb.position = pos;
        rb.rotation = Quaternion.LookRotation(fwd.normalized);
        rb.linearVelocity = fwd.normalized * speed;
        rb.angularVelocity = Vector3.zero;
    }
}
#endif
