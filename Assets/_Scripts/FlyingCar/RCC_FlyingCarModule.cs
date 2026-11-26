using System;
using UnityEngine;

public enum FlightState
{
    Grounded,   // Normal RCC driving. Module does nothing except regenerate energy.
    LiftOff,    // Lift system ramping up, wheels leaving the ground.
    Flying,     // Full flight control.
    Landing     // Lift released: controlled descent, flare, touchdown and suspension settle.
}

/// <summary>
/// Universal flying add-on for RCC_CarControllerV3.
///
/// It does NOT replace the RCC controller: RCC keeps running every frame (engine, gears,
/// wheels, suspension, sounds, drifting). While flying this module only:
///   • adds mass-independent lift / thrust / rotation forces to the existing Rigidbody,
///   • temporarily sets RCC's speed-based downForce to 0 (restored on landing),
///   • temporarily switches to continuous collision detection (restored on landing).
/// WheelColliders produce no force without ground contact, so there is no physics conflict in
/// the air, and on touchdown the suspension takes the weight back gradually (landingSettleTime).
///
/// You never need to add this by hand: FlyingCarManager attaches it to whichever vehicle is the
/// active RCC player vehicle. Add it manually to a car prefab only to give that car its own
/// Profile Override or to disable flying for it (Can Fly = false).
/// </summary>
[DefaultExecutionOrder(100)] // after RCC_CarControllerV3 so our angular velocity is applied last
[DisallowMultipleComponent]
[RequireComponent(typeof(RCC_CarControllerV3))]
public class RCC_FlyingCarModule : MonoBehaviour
{
    [Tooltip("Optional per-vehicle settings. Leave empty to use the FlyingCarManager's Default Profile.")]
    public FlyingCarProfile profileOverride;

    [Tooltip("Untick to make this vehicle unable to fly.")]
    public bool canFly = true;

    // ───────────────────────── Public state ─────────────────────────
    public FlightState State { get; private set; }
    public bool IsFlying => State != FlightState.Grounded;
    public FlyingCarProfile Profile => profileOverride ? profileOverride : fallbackProfile;
    public RCC_CarControllerV3 Car { get; private set; }
    public Rigidbody Rigid { get; private set; }

    /// <summary>Lift system power 0..1 (ramps on take off / landing).</summary>
    public float LiftWeight { get; private set; }
    /// <summary>Upward thrust demand 0..1, useful for effects.</summary>
    public float ThrustDemand { get; private set; }
    public float HeightAboveGround { get; private set; }
    public Vector3 LastGroundPoint { get; private set; }
    public float Energy { get; private set; }
    public float Energy01 => Profile && Profile.maxEnergy > 0f ? Energy / Profile.maxEnergy : 1f;
    /// <summary>True after the energy ran out, until it recharges to minEnergyToTakeOff.</summary>
    public bool EnergyLocked { get; private set; }
    /// <summary>0..1 wheel fold amount (for effects).</summary>
    public float WheelFold { get; private set; }

    /// <summary>True when pressing Fly right now would lift the car.</summary>
    public bool CanTakeOff => enabled && canFly && Profile && Car && Car.canControl && Car.engineRunning && !Car.externalController
                              && (!Profile.useEnergy || (!EnergyLocked && Energy > 0f));

    /// <summary>(module, previousState, newState)</summary>
    public event Action<RCC_FlyingCarModule, FlightState, FlightState> StateChanged;

    // ───────────────────────── Internals ─────────────────────────
    FlyingCarProfile fallbackProfile;
    bool geometryCached;
    bool energyInitialised;

    RCC_WheelCollider[] wheels;
    Transform[] wheelModels;
    float[] wheelFoldSign;
    float groundOffset;     // distance from centre of mass down to the wheel contact patch at rest
    float probeRadius;      // for obstacle sphere casts
    float bodyTop;          // distance from centre of mass to the roof

    bool flightPhysicsApplied;
    float savedDownForce;
    CollisionDetectionMode savedCollisionMode;

    bool prevPressed;
    bool toggleActive;
    float regenTimer;
    float stuckTimer;
    bool hasGroundY;
    float lastGroundY;

    static readonly RaycastHit[] hitBuffer = new RaycastHit[12];

    // ───────────────────────── Setup ─────────────────────────

    /// <summary>Called by FlyingCarManager. Safe to call more than once.</summary>
    public void Initialize(FlyingCarProfile defaultProfile)
    {
        fallbackProfile = defaultProfile ? defaultProfile : (fallbackProfile ? fallbackProfile : FlyingCarProfile.CreateRuntimeDefault());
        CacheReferences();
        if (!energyInitialised)
        {
            Energy = Profile.maxEnergy;
            energyInitialised = true;
        }
    }

    void Awake()
    {
        CacheReferences();
    }

    void Start()
    {
        if (!fallbackProfile) Initialize(null);
        CacheGeometry();
    }

    void CacheReferences()
    {
        if (!Car) Car = GetComponent<RCC_CarControllerV3>();
        if (!Rigid) Rigid = GetComponent<Rigidbody>();
    }

    /// <summary>Measures wheels and body once. Call again if you swap wheels at runtime.</summary>
    public void CacheGeometry()
    {
        CacheReferences();
        wheels = (Car.allWheelColliders != null && Car.allWheelColliders.Length > 0)
            ? Car.allWheelColliders
            : GetComponentsInChildren<RCC_WheelCollider>(true);

        Vector3 localCom = Car.COM ? transform.InverseTransformPoint(Car.COM.position) : Rigid.centerOfMass;

        float lowest = float.MaxValue, minX = float.MaxValue, maxX = float.MinValue, maxRadius = 0.3f;
        wheelModels = new Transform[wheels.Length];
        wheelFoldSign = new float[wheels.Length];

        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wc = wheels[i].wheelCollider;
            Vector3 lp = transform.InverseTransformPoint(wc.transform.position);
            float restDrop = wc.suspensionDistance * (1f - wc.suspensionSpring.targetPosition);
            float bottom = lp.y - restDrop - wc.radius;
            if (bottom < lowest) lowest = bottom;
            if (lp.x < minX) minX = lp.x;
            if (lp.x > maxX) maxX = lp.x;
            if (wc.radius > maxRadius) maxRadius = wc.radius;

            wheelModels[i] = wheels[i].wheelModel;
            wheelFoldSign[i] = lp.x < 0f ? 1f : -1f; // both outer faces end up pointing down
        }

        if (wheels.Length == 0) { lowest = localCom.y - 0.6f; minX = -0.8f; maxX = 0.8f; }

        groundOffset = Mathf.Max(0.1f, localCom.y - lowest);
        probeRadius = Mathf.Clamp((maxX - minX) * 0.4f, 0.4f, 1.6f);

        // Roof height from the renderers' bounds (one-off cost).
        Bounds b = new Bounds(transform.position, Vector3.zero);
        var rends = GetComponentsInChildren<MeshRenderer>(false);
        for (int i = 0; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        bodyTop = Mathf.Max(0.5f, b.max.y - transform.TransformPoint(localCom).y);

        geometryCached = true;
    }

    // ───────────────────────── Public API ─────────────────────────

    public void RefillEnergy(float amount = float.MaxValue)
    {
        Energy = Mathf.Min(Profile.maxEnergy, Energy + amount);
        if (Energy >= Profile.minEnergyToTakeOff) EnergyLocked = false;
    }

    /// <summary>Probes the ground right now and returns the point under the car (for effects).</summary>
    public Vector3 SampleGroundPoint()
    {
        if (!geometryCached || !Profile) return transform.position;
        ProbeGround(Profile);
        return LastGroundPoint;
    }

    /// <summary>Ends flight. instant = true drops all flight physics immediately (e.g. on respawn).</summary>
    public void ForceLand(bool instant)
    {
        toggleActive = false;
        if (!IsFlying) return;
        if (instant) SetState(FlightState.Grounded);
        else if (State != FlightState.Landing) SetState(FlightState.Landing);
    }

    // ───────────────────────── Main loop ─────────────────────────

    void FixedUpdate()
    {
        if (!geometryCached || !fallbackProfile) return;

        FlyingCarProfile p = Profile;
        float dt = Time.fixedDeltaTime;

        // Game logic froze the car (start of level, checkpoints, pause panels): do nothing.
        if (Rigid.isKinematic)
        {
            prevPressed = FlyingCarInput.Held;
            return;
        }

        bool controllable = canFly && Car.enabled && Car.canControl && Car.engineRunning && !Car.externalController;
        bool pressed = controllable && FlyingCarInput.Held;

        // ── Requested lift (hold vs toggle) ──
        bool liftRequested;
        if (p.inputMode == FlyingCarProfile.FlyInputMode.Toggle)
        {
            if (pressed && !prevPressed)
                toggleActive = !(toggleActive && (State == FlightState.LiftOff || State == FlightState.Flying));
            if (!controllable) toggleActive = false;
            liftRequested = toggleActive;
        }
        else
        {
            liftRequested = pressed;
        }
        prevPressed = pressed;

        // ── Energy gate ──
        if (p.useEnergy)
        {
            if (Energy <= 0f) EnergyLocked = true;
            else if (EnergyLocked && Energy >= p.minEnergyToTakeOff) EnergyLocked = false;
        }
        bool liftActive = liftRequested && (!p.useEnergy || (!EnergyLocked && Energy > 0f));

        // ── State machine ──
        switch (State)
        {
            case FlightState.Grounded:
                if (liftActive) SetState(FlightState.LiftOff);
                break;
            case FlightState.LiftOff:
                if (!liftActive) SetState(FlightState.Landing);
                else if (LiftWeight >= 0.99f && HeightAboveGround >= p.liftOffHeight) SetState(FlightState.Flying);
                break;
            case FlightState.Flying:
                if (!liftActive) SetState(FlightState.Landing);
                break;
            case FlightState.Landing:
                if (liftActive) SetState(HeightAboveGround >= p.liftOffHeight ? FlightState.Flying : FlightState.LiftOff);
                break;
        }

        if (IsFlying)
        {
            UpdateLiftWeight(p, dt);
            if (IsFlying) ApplyFlightPhysics(p, dt, liftActive, controllable);
        }

        UpdateEnergy(p, dt, liftActive);
    }

    void UpdateLiftWeight(FlyingCarProfile p, float dt)
    {
        if (State == FlightState.Landing)
        {
            bool touching = AnyWheelGrounded();

            // Fallback for cars that land on their body / a ledge and never report wheel contact.
            if (!touching && HeightAboveGround < 0.35f && Mathf.Abs(Rigid.linearVelocity.y) < 0.5f) stuckTimer += dt;
            else stuckTimer = 0f;

            if (touching || stuckTimer > 1f)
            {
                LiftWeight = Mathf.MoveTowards(LiftWeight, 0f, dt / Mathf.Max(0.01f, p.landingSettleTime));
                if (LiftWeight <= 0f) SetState(FlightState.Grounded);
                return;
            }
        }
        LiftWeight = Mathf.MoveTowards(LiftWeight, 1f, dt / Mathf.Max(0.01f, p.liftRampTime));
    }

    void ApplyFlightPhysics(FlyingCarProfile p, float dt, bool liftActive, bool controllable)
    {
        ProbeGround(p);

        Vector3 v = Rigid.linearVelocity;
        float w = LiftWeight;

        // ── Vertical: velocity-tracking lift with gravity compensation ──
        float targetVy;
        if (liftActive)
        {
            float targetAlt = p.inputMode == FlyingCarProfile.FlyInputMode.Toggle ? Mathf.Min(p.cruiseHeight, p.maxHeight) : p.maxHeight;
            targetVy = Mathf.Clamp((targetAlt - HeightAboveGround) * p.altitudeGain, -p.descendSpeed, p.climbSpeed);
            if (targetVy > 0f && CeilingAbove(p)) targetVy = 0f;
        }
        else
        {
            float flare = Mathf.Clamp01(HeightAboveGround / Mathf.Max(0.01f, p.flareHeight));
            targetVy = -Mathf.Lerp(p.touchdownSpeed, p.descendSpeed, flare);
        }
        ThrustDemand = liftActive ? Mathf.Clamp01(0.45f + Mathf.Max(0f, targetVy) / Mathf.Max(0.1f, p.climbSpeed) * 0.55f) : 0.25f;

        float ay = Mathf.Clamp((targetVy - v.y) * p.verticalResponse, -p.maxVerticalAcceleration, p.maxVerticalAcceleration);
        ay -= Physics.gravity.y; // hold the car's weight
        Rigid.AddForce(new Vector3(0f, ay * w, 0f), ForceMode.Acceleration);

        // ── Horizontal: throttle / brake / air resistance / lateral grip ──
        Quaternion rot = Rigid.rotation;
        Vector3 fwd = rot * Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = rot * Vector3.up; // nose pointing straight up/down
        fwd.y = 0f;
        fwd.Normalize();
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

        float throttle = 0f, steer = 0f;
        if (controllable)
        {
            RCC_Inputs inp = RCC_InputManager.GetInputs();
            throttle = Mathf.Clamp(inp.throttleInput - inp.brakeInput, -1f, 1f);
            steer = Mathf.Clamp(inp.steerInput, -1f, 1f);
        }

        float vf = Vector3.Dot(v, fwd);
        float vs = Vector3.Dot(v, right);
        float af;
        if (throttle > 0.01f)
            af = (throttle * p.maxFlightSpeedKmh / 3.6f - vf) * p.forwardResponse;
        else if (throttle < -0.01f)
            af = (throttle * p.maxReverseSpeedKmh / 3.6f - vf) * p.forwardResponse * (vf > 0f ? 2f : 1f);
        else
            af = -vf * p.airResistance;

        Vector3 aH = fwd * af - right * (vs * p.lateralGrip);
        float maxH = p.maxHorizontalAcceleration;
        if (aH.sqrMagnitude > maxH * maxH) aH = aH.normalized * maxH;
        if (p.obstacleAvoidance) aH += AvoidanceAcceleration(p, v);
        Rigid.AddForce(aH * w, ForceMode.Acceleration);

        // ── Rotation: steer = yaw, auto-level with cosmetic bank & pitch ──
        float speedFactor = Mathf.Clamp01(Mathf.Abs(vf) / 10f);
        float yawRate = steer * p.yawSpeed * Mathf.Lerp(p.minYawFactor, 1f, speedFactor) * (vf < -0.5f ? -1f : 1f);
        float bank = -steer * p.maxBankAngle * Mathf.Clamp01(Mathf.Abs(vf) / 8f);
        float pitch = Mathf.Clamp(af / Mathf.Max(0.01f, maxH), -1f, 1f) * p.maxPitchAngle
                    - Mathf.Clamp(v.y / Mathf.Max(0.1f, p.climbSpeed), -1f, 1f) * p.climbPitchAngle;

        float heading = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        Quaternion desired = Quaternion.Euler(pitch, heading, bank);
        (desired * Quaternion.Inverse(rot)).ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;

        Vector3 levelVel = (Mathf.Abs(angle) > 0.01f && !float.IsInfinity(axis.x) && !float.IsNaN(axis.x))
            ? axis * (angle * Mathf.Deg2Rad * p.levelStrength)
            : Vector3.zero;
        Vector3 targetAngVel = levelVel + new Vector3(0f, yawRate * Mathf.Deg2Rad, 0f);

        float k = (1f - Mathf.Exp(-p.rotationResponse * dt)) * w;
        Rigid.angularVelocity = Vector3.Lerp(Rigid.angularVelocity, targetAngVel, k);
    }

    void UpdateEnergy(FlyingCarProfile p, float dt, bool liftActive)
    {
        if (p.useEnergy)
        {
            if (IsFlying)
            {
                float drain = liftActive
                    ? (Rigid.linearVelocity.y > 0.5f ? p.climbDrain : p.cruiseDrain)
                    : p.descendDrain;
                Energy = Mathf.Max(0f, Energy - drain * dt);
                regenTimer = 0f;
            }
            else
            {
                regenTimer += dt;
                if (regenTimer >= p.regenDelay) Energy = Mathf.Min(p.maxEnergy, Energy + p.regenRate * dt);
            }
        }

        if (p.drainVehicleFuel && IsFlying && liftActive && Car.useFuelConsumption)
            Car.fuelTank = Mathf.Max(0f, Car.fuelTank - p.vehicleFuelPerSecond * dt);
    }

    // ───────────────────────── Physics helpers ─────────────────────────

    bool AnyWheelGrounded()
    {
        for (int i = 0; i < wheels.Length; i++)
            if (wheels[i].isGrounded) return true;
        return false;
    }

    void ProbeGround(FlyingCarProfile p)
    {
        Vector3 origin = Rigid.worldCenterOfMass;
        float maxDist = groundOffset + p.maxHeight + 25f;
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, hitBuffer, maxDist, p.groundMask, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        int bestIndex = -1;
        for (int i = 0; i < n; i++)
        {
            if (hitBuffer[i].rigidbody == Rigid) continue;
            if (hitBuffer[i].distance < best) { best = hitBuffer[i].distance; bestIndex = i; }
        }

        if (bestIndex >= 0)
        {
            HeightAboveGround = Mathf.Max(0f, best - groundOffset);
            LastGroundPoint = hitBuffer[bestIndex].point;
            lastGroundY = LastGroundPoint.y;
            hasGroundY = true;
        }
        else
        {
            // Nothing below (off the map / over a gap): use the last known ground level.
            HeightAboveGround = hasGroundY ? Mathf.Max(0f, origin.y - groundOffset - lastGroundY) : maxDist;
            LastGroundPoint = new Vector3(origin.x, origin.y - groundOffset - HeightAboveGround, origin.z);
        }
    }

    bool CeilingAbove(FlyingCarProfile p)
    {
        Vector3 origin = Rigid.worldCenterOfMass;
        int n = Physics.RaycastNonAlloc(origin, Vector3.up, hitBuffer, bodyTop + 1.5f, p.groundMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (hitBuffer[i].rigidbody != Rigid) return true;
        return false;
    }

    Vector3 AvoidanceAcceleration(FlyingCarProfile p, Vector3 v)
    {
        Vector3 hv = new Vector3(v.x, 0f, v.z);
        float speed = hv.magnitude;
        if (speed < 2f) return Vector3.zero;

        Vector3 dir = hv / speed;
        float dist = speed * p.avoidanceLookAhead + 1.5f;
        int n = Physics.SphereCastNonAlloc(Rigid.worldCenterOfMass, probeRadius, dir, hitBuffer, dist, p.groundMask, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        Vector3 bestNormal = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.rigidbody == Rigid || h.distance <= 0f) continue;   // self / already overlapping
            Vector3 nH = new Vector3(h.normal.x, 0f, h.normal.z);
            if (nH.sqrMagnitude < 0.09f) continue;                      // floors & gentle slopes are handled by altitude hold
            if (h.distance < best) { best = h.distance; bestNormal = nH.normalized; }
        }
        if (best == float.MaxValue) return Vector3.zero;

        float closing = -Vector3.Dot(hv, bestNormal);
        if (closing <= 0f) return Vector3.zero;
        float urgency = 1f - best / dist;
        return bestNormal * (closing * p.avoidanceStrength * urgency);
    }

    // ───────────────────────── State changes ─────────────────────────

    void SetState(FlightState next)
    {
        if (next == State) return;
        FlightState prev = State;
        State = next;

        if (prev == FlightState.Grounded && next != FlightState.Grounded)
        {
            ApplyFlightPhysicsOverrides();
            stuckTimer = 0f;
        }
        else if (next == FlightState.Grounded)
        {
            RestorePhysicsOverrides();
            LiftWeight = 0f;
            ThrustDemand = 0f;
            toggleActive = false;
        }

        StateChanged?.Invoke(this, prev, next);
    }

    void ApplyFlightPhysicsOverrides()
    {
        if (flightPhysicsApplied) return;
        savedDownForce = Car.downForce;
        Car.downForce = 0f;   // RCC pushes the car down proportionally to speed; not wanted in the air
        savedCollisionMode = Rigid.collisionDetectionMode;
        if (Profile.continuousCollisionInFlight)
            Rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // cheap, safe for kinematic toggles
        flightPhysicsApplied = true;
    }

    void RestorePhysicsOverrides()
    {
        if (!flightPhysicsApplied) return;
        Car.downForce = savedDownForce;
        Rigid.collisionDetectionMode = savedCollisionMode;
        flightPhysicsApplied = false;
    }

    // ───────────────────────── Tesla-style wheel fold ─────────────────────────

    void LateUpdate()
    {
        // RCC_WheelCollider.Update re-poses each wheel model every frame, so this rotation never accumulates.
        if (!geometryCached || !fallbackProfile) return;
        FlyingCarProfile p = Profile;

        float target = (p.foldWheels && (State == FlightState.LiftOff || State == FlightState.Flying)) ? 1f : 0f;
        WheelFold = Mathf.MoveTowards(WheelFold, target, Time.deltaTime / Mathf.Max(0.01f, p.wheelFoldTime));
        if (WheelFold <= 0f || !Car.enabled) return;

        float angle = p.wheelFoldAngle * Mathf.SmoothStep(0f, 1f, WheelFold);
        Vector3 axis = transform.forward;
        for (int i = 0; i < wheelModels.Length; i++)
        {
            Transform m = wheelModels[i];
            if (m) m.RotateAround(m.position, axis, wheelFoldSign[i] * angle);
        }
    }

    void OnDisable()
    {
        // Vehicle switched, destroyed or deactivated: always leave RCC exactly as we found it.
        if (IsFlying) SetState(FlightState.Grounded);
        RestorePhysicsOverrides();
        WheelFold = 0f;
        prevPressed = false;
    }
}
