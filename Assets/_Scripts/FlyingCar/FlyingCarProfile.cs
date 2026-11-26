using UnityEngine;

/// <summary>
/// All tunable values for the flying system. Create one asset per "feel"
/// (Assets > Create > Flying Car > Flying Profile) and assign it to the
/// FlyingCarManager (default for every car) or to a specific car's
/// RCC_FlyingCarModule (per-vehicle override).
/// All accelerations are mass-independent, so the same profile works on light and heavy cars.
/// </summary>
[CreateAssetMenu(fileName = "FlyingProfile", menuName = "Flying Car/Flying Profile")]
public class FlyingCarProfile : ScriptableObject
{
    public enum FlyInputMode { HoldToLift, Toggle }

    [Header("Input")]
    [Tooltip("HoldToLift: hold Fly to climb, release to descend.\nToggle: tap Fly to take off and hover at Cruise Height, tap again to land.")]
    public FlyInputMode inputMode = FlyInputMode.HoldToLift;

    [Header("Vertical Flight")]
    [Tooltip("Maximum climb speed (m/s).")]
    public float climbSpeed = 6f;
    [Tooltip("Descent speed after releasing Fly (m/s).")]
    public float descendSpeed = 4f;
    [Tooltip("Vertical speed at the moment of touchdown (m/s). Lower = softer landing.")]
    public float touchdownSpeed = 1.2f;
    [Tooltip("Below this height the descent slows down towards Touchdown Speed (m).")]
    public float flareHeight = 3.5f;
    [Tooltip("Maximum height above the ground (m).")]
    public float maxHeight = 30f;
    [Tooltip("Hover height used in Toggle mode (m).")]
    public float cruiseHeight = 12f;
    [Tooltip("How strongly the car is pulled towards its target height (1/s).")]
    public float altitudeGain = 1.2f;
    [Tooltip("How fast vertical speed follows its target (1/s). Higher = snappier.")]
    public float verticalResponse = 3f;
    [Tooltip("Hard limit on vertical acceleration (m/s²). Prevents sudden jumps.")]
    public float maxVerticalAcceleration = 14f;

    [Header("Transitions")]
    [Tooltip("Seconds for the lift system to ramp from 0 to full power on take off.")]
    public float liftRampTime = 0.8f;
    [Tooltip("Seconds for the lift system to fade out after the wheels touch down (lets the suspension take the weight smoothly).")]
    public float landingSettleTime = 0.4f;
    [Tooltip("Height above ground at which take off becomes full flight (m).")]
    public float liftOffHeight = 1.2f;

    [Header("Horizontal Flight")]
    [Tooltip("Top forward speed in the air (km/h).")]
    public float maxFlightSpeedKmh = 140f;
    [Tooltip("Top reverse speed in the air (km/h).")]
    public float maxReverseSpeedKmh = 40f;
    [Tooltip("How quickly forward speed follows the throttle (1/s).")]
    public float forwardResponse = 1.2f;
    [Tooltip("Hard limit on horizontal acceleration (m/s²).")]
    public float maxHorizontalAcceleration = 12f;
    [Tooltip("Air resistance when no throttle is applied (1/s).")]
    public float airResistance = 0.35f;
    [Tooltip("How fast sideways sliding is removed (1/s). Lower = more drifty in the air.")]
    public float lateralGrip = 2.5f;

    [Header("Rotation")]
    [Tooltip("Yaw (turn) speed at full steering (deg/s).")]
    public float yawSpeed = 70f;
    [Tooltip("Fraction of yaw speed available when hovering without moving.")]
    [Range(0f, 1f)] public float minYawFactor = 0.5f;
    [Tooltip("How quickly the car's rotation follows the desired rotation (1/s).")]
    public float rotationResponse = 6f;
    [Tooltip("How strongly the car levels itself (1/s).")]
    public float levelStrength = 4f;
    [Tooltip("Bank (roll) angle when steering (deg).")]
    public float maxBankAngle = 16f;
    [Tooltip("Nose-down pitch when accelerating (deg).")]
    public float maxPitchAngle = 6f;
    [Tooltip("Nose-up pitch when climbing (deg).")]
    public float climbPitchAngle = 5f;

    [Header("Collision Safety")]
    [Tooltip("Layers treated as ground / obstacles for height and avoidance probes.")]
    public LayerMask groundMask = ~(1 << 2); // Everything except Ignore Raycast
    [Tooltip("Slow down before hitting walls and buildings while flying.")]
    public bool obstacleAvoidance = true;
    [Tooltip("Look-ahead time for obstacle avoidance (s).")]
    public float avoidanceLookAhead = 0.8f;
    [Tooltip("Braking strength against obstacles (1/s).")]
    public float avoidanceStrength = 4f;
    [Tooltip("Use continuous collision detection while flying (prevents tunnelling at high speed).")]
    public bool continuousCollisionInFlight = true;

    [Header("Flight Energy")]
    public bool useEnergy = true;
    public float maxEnergy = 100f;
    [Tooltip("Energy per second while climbing.")]
    public float climbDrain = 7f;
    [Tooltip("Energy per second while cruising / hovering.")]
    public float cruiseDrain = 4f;
    [Tooltip("Energy per second while descending.")]
    public float descendDrain = 1f;
    [Tooltip("Energy regained per second on the ground.")]
    public float regenRate = 12f;
    [Tooltip("Seconds on the ground before energy starts regenerating.")]
    public float regenDelay = 1f;
    [Tooltip("Energy needed to take off again after the tank ran empty.")]
    public float minEnergyToTakeOff = 25f;
    [Tooltip("Also consume the car's RCC fuel while flying (uses the existing fuel UI / fail logic).")]
    public bool drainVehicleFuel = false;
    [Tooltip("RCC fuel consumed per second of flight.")]
    public float vehicleFuelPerSecond = 0.3f;

    [Header("Camera (added on top of RCC_Camera TPS values)")]
    public float cameraExtraDistance = 2.5f;
    public float cameraExtraHeight = 1.2f;
    public float cameraExtraFOV = 8f;
    public float cameraExtraPitch = 4f;
    [Tooltip("Camera blend time (s).")]
    public float cameraBlendTime = 0.7f;

    [Header("Visuals")]
    [Tooltip("Tesla-style: wheels fold flat into hover pads while flying.")]
    public bool foldWheels = true;
    public float wheelFoldAngle = 90f;
    [Tooltip("Seconds to fold / unfold the wheels.")]
    public float wheelFoldTime = 0.6f;
    [ColorUsage(false, true)] public Color thrusterColor = new Color(0.25f, 0.85f, 2.2f, 1f);
    [Tooltip("Thruster particles per second per wheel at full lift.")]
    public float thrusterEmission = 55f;

    /// <summary>Default values used when no profile asset is assigned anywhere.</summary>
    public static FlyingCarProfile CreateRuntimeDefault()
    {
        var p = CreateInstance<FlyingCarProfile>();
        p.name = "FlyingProfile (Runtime Default)";
        return p;
    }
}
