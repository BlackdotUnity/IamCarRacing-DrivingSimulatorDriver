using UnityEngine;

/// <summary>
/// Quest performance: a camera that draws into a texture (e.g. the minimap) renders only `fps` times per second
/// instead of every frame. Each render of an extra camera costs a full culling pass over the scene on the CPU.
/// </summary>
[RequireComponent(typeof(Camera))]
public class RenderTextureCameraThrottle : MonoBehaviour
{
    [Tooltip("Renders per second. 15–20 looks smooth for a minimap.")]
    public float fps = 20f;

    Camera cam;
    float next;

    void Awake() => cam = GetComponent<Camera>();

    void OnDisable() { if (cam != null) cam.enabled = true; } // leave the camera as it was

    void LateUpdate()
    {
        if (cam == null || cam.targetTexture == null) return;
        if (Time.unscaledTime >= next)
        {
            next = Time.unscaledTime + 1f / Mathf.Max(1f, fps);
            cam.enabled = true;   // renders this frame
        }
        else cam.enabled = false; // keeps the last picture in its texture
    }
}
