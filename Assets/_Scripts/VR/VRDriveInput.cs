/// <summary>
/// VR driving input fed into RCC. VRCarRig writes these values every frame from the Quest
/// controllers / grabbed steering wheel; RCC_InputManager.GetInputs() returns them while Active.
/// The car physics (RCC, WheelColliders, Rigidbody) stay exactly as they are.
/// </summary>
public static class VRDriveInput
{
    public static bool Active;
    public static float Throttle;   // 0..1  right trigger
    public static float Brake;      // 0..1  left trigger (reverse when stopped, like RCC keyboard input)
    public static float Steer;      // -1..1 steering wheel or left thumbstick
    public static float Handbrake;  // 0..1  A button
    public static float Boost;      // 0..1  B button (NOS)

    public static void Clear()
    {
        Throttle = Brake = Steer = Handbrake = Boost = 0f;
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active = false; Clear(); }
}
