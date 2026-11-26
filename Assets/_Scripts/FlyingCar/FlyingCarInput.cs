using UnityEngine;

/// <summary>
/// Central, allocation-free input hub for the Fly action.
/// UI buttons (UIFlyButton) and the keyboard/gamepad (read by FlyingCarManager)
/// write into it; the active RCC_FlyingCarModule reads from it.
/// </summary>
public static class FlyingCarInput
{
    static int uiPressCount;

    /// <summary>Set by FlyingCarManager from keyboard / gamepad each frame.</summary>
    public static bool DeviceHeld;

    /// <summary>True while any Fly UI button is held.</summary>
    public static bool UIHeld => uiPressCount > 0;

    /// <summary>Set by VRCarRig from the Quest controllers (X button) each frame.</summary>
    public static bool XRHeld;

    /// <summary>True while Fly is held on any device.</summary>
    public static bool Held => uiPressCount > 0 || DeviceHeld || XRHeld;

    public static void PressUI() { uiPressCount++; }

    public static void ReleaseUI() { if (uiPressCount > 0) uiPressCount--; }

    /// <summary>Clears all input (scene change, pause, etc.).</summary>
    public static void ResetAll()
    {
        uiPressCount = 0;
        DeviceHeld = false;
        XRHeld = false;
    }

    // Supports "Enter Play Mode Options" with domain reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { ResetAll(); }
}
