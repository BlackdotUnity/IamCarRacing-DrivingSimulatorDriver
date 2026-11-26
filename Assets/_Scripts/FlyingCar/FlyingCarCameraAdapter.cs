using UnityEngine;

/// <summary>
/// Smoothly widens the existing RCC_Camera while flying. It does not replace the RCC camera:
/// it only adds small offsets on top of TPSDistance / TPSHeight / TPSPitchAngle / FOV, so any value
/// your game sets (e.g. GameMannager.SetCameraPos) is respected. Removing or disabling this
/// component returns the camera exactly to its own values.
/// </summary>
[DisallowMultipleComponent]
public class FlyingCarCameraAdapter : MonoBehaviour
{
    [Tooltip("Leave empty to use RCC_SceneManager's active player camera.")]
    public RCC_Camera rccCamera;

    RCC_Camera appliedTo;
    float weight, weightVelocity;
    float appliedDistance, appliedHeight, appliedPitch, appliedMinFov, appliedMaxFov;
    RCC_SceneManager rccScene;

    void Update()
    {
        FlyingCarManager mgr = FlyingCarManager.Instance;
        RCC_FlyingCarModule module = mgr ? mgr.ActiveModule : null;

        RCC_Camera cam = rccCamera;
        if (!cam)
        {
            if (!rccScene) rccScene = FindFirstObjectByType<RCC_SceneManager>();
            if (rccScene) cam = rccScene.activePlayerCamera;
        }
        if (cam != appliedTo)
        {
            RemoveOffsets();
            appliedTo = cam;
        }
        if (!cam) return;

        FlyingCarProfile p = module ? module.Profile : (mgr ? mgr.defaultProfile : null);
        if (!p) return;

        float target = (module && module.IsFlying) ? (module.State == FlightState.Landing ? 0.5f * module.LiftWeight : 1f) : 0f;
        if (target == 0f && weight == 0f) return; // idle on the ground: zero cost

        weight = Mathf.SmoothDamp(weight, target, ref weightVelocity, Mathf.Max(0.05f, p.cameraBlendTime), Mathf.Infinity, Time.deltaTime);
        if (target == 0f && weight < 0.0005f) { weight = 0f; weightVelocity = 0f; }

        Apply(cam, p.cameraExtraDistance * weight, p.cameraExtraHeight * weight, p.cameraExtraPitch * weight, p.cameraExtraFOV * weight);
    }

    void Apply(RCC_Camera cam, float dist, float height, float pitch, float fov)
    {
        // Delta application: only our own contribution is replaced each frame.
        cam.TPSDistance += dist - appliedDistance;
        cam.TPSHeight += height - appliedHeight;
        cam.TPSPitchAngle += pitch - appliedPitch;
        cam.TPSMinimumFOV += fov - appliedMinFov;
        cam.TPSMaximumFOV += fov - appliedMaxFov;
        appliedDistance = dist;
        appliedHeight = height;
        appliedPitch = pitch;
        appliedMinFov = fov;
        appliedMaxFov = fov;
    }

    void RemoveOffsets()
    {
        if (appliedTo) Apply(appliedTo, 0f, 0f, 0f, 0f);
        appliedDistance = appliedHeight = appliedPitch = appliedMinFov = appliedMaxFov = 0f;
        weight = weightVelocity = 0f;
    }

    void OnDisable()
    {
        RemoveOffsets();
    }
}
