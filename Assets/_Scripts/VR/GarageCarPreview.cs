using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// MainMenu garage: shows the selected car on the GaragePannel (inside the menu screen) instead of in the 3D garage.
/// The old garage camera (GarageCameraOrbit) renders the car "studio" into a RenderTexture shown by this RawImage.
/// The camera only renders while the garage panel is open. Drag with the controller laser to turn around the car;
/// when left alone it slowly spins.
/// Every car is framed the same way: the camera orbits the car's real centre from far away with a narrow lens,
/// so the car keeps the same size at every angle and stands on the turntable painted in the panel picture.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class GarageCarPreview : MonoBehaviour, IDragHandler
{
    public Camera previewCamera;
    public GarageCameraOrbit orbit;
    [Tooltip("The selectable cars (MainMenuMannager.Cars). Empty = taken from MainMenuMannager.")]
    public GameObject[] cars;

    [Header("Framing")]
    [Tooltip("Narrow lens = little perspective distortion when the car's nose points at the camera.")]
    public float fieldOfView = 28f;
    [Tooltip("Camera distance from the car centre (metres).")]
    public float distance = 11f;
    [Tooltip("Aim this far above the car centre: higher = car sits lower in the picture.")]
    public float aimAbove = 0.8f;

    [Header("Turning")]
    [Tooltip("Degrees per pixel of laser drag.")]
    public float dragSpeed = 0.2f;
    [Tooltip("Degrees per second of slow spin when not dragged (0 = off).")]
    public float idleSpin = 12f;
    public float idleDelay = 3f;

    float idle;
    readonly Dictionary<GameObject, Vector3> centres = new Dictionary<GameObject, Vector3>();

    void OnEnable()
    {
        if ((cars == null || cars.Length == 0))
        {
            var mm = FindFirstObjectByType<MainMenuMannager>(FindObjectsInactive.Include);
            if (mm != null) cars = mm.Cars;
        }
        if (previewCamera != null)
        {
            MatchAspect();
            previewCamera.fieldOfView = fieldOfView;
            previewCamera.farClipPlane = Mathf.Max(previewCamera.farClipPlane, distance + 10f);
            previewCamera.enabled = true;
        }
        if (orbit != null) orbit.minDistance = orbit.maxDistance = orbit.distance = distance;
        idle = 0f;
        Frame();
    }

    void OnDisable()
    {
        if (previewCamera != null) previewCamera.enabled = false; // no GPU cost while the panel is closed
    }

    RenderTexture ownTexture;

    /// <summary>
    /// The render texture must have the panel's shape, or the car is stretched (the menu wall screen is wider than 16:9).
    /// Keeps the texture height and the vertical lens, so cars keep their size and only the view gets wider.
    /// </summary>
    void MatchAspect()
    {
        var rect = ((RectTransform)transform).rect;
        var src = previewCamera.targetTexture;
        if (rect.height <= 0f || src == null) return;
        int h = src.height;
        int w = Mathf.Clamp(Mathf.RoundToInt(h * rect.width / rect.height), 16, 4096);
        if (src.width == w) return;
        if (ownTexture != null) { previewCamera.targetTexture = null; ownTexture.Release(); Destroy(ownTexture); }
        ownTexture = new RenderTexture(w, h, src.depth, src.format) { name = "GarageCarPreview (" + w + "x" + h + ")", antiAliasing = src.antiAliasing };
        previewCamera.targetTexture = ownTexture;
        GetComponent<RawImage>().texture = ownTexture;
    }

    void OnDestroy()
    {
        if (ownTexture != null) { ownTexture.Release(); Destroy(ownTexture); }
    }

    public void OnDrag(PointerEventData e)
    {
        if (orbit != null) orbit.Orbit(e.delta * dragSpeed);
        idle = 0f;
    }

    void Update()
    {
        idle += Time.deltaTime;
        if (orbit != null && idleSpin != 0f && idle > idleDelay) orbit.Orbit(new Vector2(idleSpin * Time.deltaTime, 0f));
        Frame(); // GarageCameraOrbit positions the camera in LateUpdate around this target
    }

    /// <summary>Points the orbit at the centre of the car that is currently shown.</summary>
    void Frame()
    {
        if (orbit == null || orbit.target == null || cars == null) return;
        foreach (var car in cars)
        {
            if (car == null || !car.activeInHierarchy) continue;
            if (!centres.TryGetValue(car, out var c))
            {
                var rs = car.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) return;
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                c = b.center;
                centres[car] = c;
            }
            orbit.target.position = c + Vector3.up * aimAbove;
            return;
        }
    }
}
