using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// One per gameplay scene. Watches RCC_SceneManager.activePlayerVehicle and makes sure whichever
/// car the player is driving has an RCC_FlyingCarModule — so every car in Cars[] (and any car
/// added later) can fly without copying scripts onto prefabs.
/// Also reads keyboard / gamepad Fly input and re-broadcasts flight events for UI, camera and effects.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class FlyingCarManager : MonoBehaviour
{
    public static FlyingCarManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Profile used by every car that has no Profile Override of its own.")]
    public FlyingCarProfile defaultProfile;

    [Tooltip("Automatically add RCC_FlyingCarModule to whichever car becomes the active RCC player vehicle.")]
    public bool autoAttachToPlayerVehicle = true;

    [Tooltip("Optional: cars to prepare at scene start (e.g. the same objects as GameMannager.Cars). Avoids a tiny hitch the first time a car is used. Modules stay disabled until that car is driven.")]
    public GameObject[] preAttachVehicles;

    [Header("Keyboard / Gamepad (editor & desktop testing)")]
    public bool enableDeviceInput = true;
#if ENABLE_INPUT_SYSTEM
    public Key flyKey = Key.F;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
    public KeyCode legacyFlyKey = KeyCode.F;
#endif

    [Header("Links (found automatically if left empty)")]
    public FlyingCarCameraAdapter cameraAdapter;
    public FlyingCarEffects effects;

    /// <summary>The flying module of the car currently being driven (may be null).</summary>
    public RCC_FlyingCarModule ActiveModule { get; private set; }

    public event Action<RCC_FlyingCarModule> ActiveModuleChanged;
    /// <summary>(module, previousState, newState) for the active car only.</summary>
    public event Action<RCC_FlyingCarModule, FlightState, FlightState> StateChanged;

    RCC_SceneManager rccScene;
    RCC_CarControllerV3 activeCar;
    float nextSceneManagerSearch;

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogWarning("[FlyingCar] More than one FlyingCarManager in the scene. Disabling the duplicate.", this);
            enabled = false;
            return;
        }
        Instance = this;
        FlyingCarInput.ResetAll();

        if (!defaultProfile) defaultProfile = FlyingCarProfile.CreateRuntimeDefault();
        if (!cameraAdapter) cameraAdapter = GetComponent<FlyingCarCameraAdapter>();
        if (!effects) effects = GetComponent<FlyingCarEffects>();
    }

    void Start()
    {
        if (preAttachVehicles != null)
        {
            for (int i = 0; i < preAttachVehicles.Length; i++)
            {
                if (!preAttachVehicles[i]) continue;
                var car = preAttachVehicles[i].GetComponent<RCC_CarControllerV3>();
                if (!car) continue;
                var module = GetOrAddModule(car);
                if (module && car != activeCar) module.enabled = false;
            }
        }
    }

    void Update()
    {
        ReadDeviceInput();

        if (!rccScene)
        {
            if (Time.unscaledTime < nextSceneManagerSearch) return;
            nextSceneManagerSearch = Time.unscaledTime + 1f;
            rccScene = FindFirstObjectByType<RCC_SceneManager>(); // never use RCC_SceneManager.Instance here: it creates one if missing
            if (!rccScene) return;
        }

        RCC_CarControllerV3 car = rccScene.activePlayerVehicle;
        if (car && !car.gameObject.activeInHierarchy) car = null;
        if (car != activeCar) SwitchVehicle(car);
    }

    void ReadDeviceInput()
    {
        bool held = false;
        if (enableDeviceInput)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && flyKey != Key.None && kb[flyKey].isPressed) held = true;
            var pad = Gamepad.current;
            if (pad != null && pad.buttonNorth.isPressed) held = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            if (Input.GetKey(legacyFlyKey)) held = true;
#endif
        }
        FlyingCarInput.DeviceHeld = held;
    }

    void SwitchVehicle(RCC_CarControllerV3 car)
    {
        if (ActiveModule)
        {
            ActiveModule.StateChanged -= OnModuleStateChanged;
            ActiveModule.enabled = false; // restores RCC values if it was flying
        }

        activeCar = car;
        ActiveModule = null;

        if (car && !car.externalController)
        {
            ActiveModule = autoAttachToPlayerVehicle ? GetOrAddModule(car) : car.GetComponent<RCC_FlyingCarModule>();
            if (ActiveModule)
            {
                ActiveModule.enabled = true;
                ActiveModule.StateChanged += OnModuleStateChanged;
            }
        }

        ActiveModuleChanged?.Invoke(ActiveModule);
    }

    RCC_FlyingCarModule GetOrAddModule(RCC_CarControllerV3 car)
    {
        var module = car.GetComponent<RCC_FlyingCarModule>();
        if (!module) module = car.gameObject.AddComponent<RCC_FlyingCarModule>();
        module.Initialize(defaultProfile);
        return module;
    }

    void OnModuleStateChanged(RCC_FlyingCarModule module, FlightState prev, FlightState next)
    {
        StateChanged?.Invoke(module, prev, next);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) FlyingCarInput.ResetAll();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        if (ActiveModule) ActiveModule.StateChanged -= OnModuleStateChanged;
        FlyingCarInput.ResetAll();
        Instance = null;
    }
}
