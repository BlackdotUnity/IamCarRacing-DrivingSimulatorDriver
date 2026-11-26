using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIAxisButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // Value to assign when pressed (1 = forward)
    public float value = 1f;

    public void OnPointerDown(PointerEventData eventData)
    {
        UIAxis.Vertical = value;
        Debug.Log(UIAxis.Vertical);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        UIAxis.Vertical = 0f;
        Debug.Log(UIAxis.Vertical);
    }

    // Also handle pointer/cancel cases (optional)
    public void OnPointerExit(PointerEventData eventData)
    {
        // If you want to stop when finger slides off:
        UIAxis.Vertical = 0f;
    }
}
