using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GarageCameraOrbit : MonoBehaviour
{
    public Transform target;

    [Header("Orbit Settings")]
    public float distance = 5f;
    public float rotationSpeed = 5f;
    public float minY = 10f;
    public float maxY = 65f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 2f;
    public float minDistance = 3f;
    public float maxDistance = 8f;

    float currentX;
    float currentY = 20f;

    void LateUpdate()
    {
        HandleInput();

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 direction = new Vector3(0, 0, -distance);
        transform.position = target.position + rotation * direction;

        transform.LookAt(target.position);
    }

    /// <summary>Orbit by a delta in degrees (used by the VR garage preview: controller-laser drag / idle spin).</summary>
    public void Orbit(Vector2 deltaDegrees)
    {
        currentX += deltaDegrees.x;
        currentY -= deltaDegrees.y;
    }

    void HandleInput()
    {
        // -------- PC INPUT --------
        if (Input.GetMouseButton(0))
        {
            currentX += Input.GetAxis("Mouse X") * rotationSpeed;
            currentY -= Input.GetAxis("Mouse Y") * rotationSpeed;
        }

        distance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;

        // -------- MOBILE INPUT --------
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved)
            {
                currentX += touch.deltaPosition.x * rotationSpeed * 0.02f;
                currentY -= touch.deltaPosition.y * rotationSpeed * 0.02f;
            }
        }

        // Pinch zoom
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            float prevMag = (t0.position - t0.deltaPosition - (t1.position - t1.deltaPosition)).magnitude;
            float currentMag = (t0.position - t1.position).magnitude;
            float diff = currentMag - prevMag;

            distance -= diff * 0.01f;
        }

        currentY = Mathf.Clamp(currentY, minY, maxY);
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }
}
