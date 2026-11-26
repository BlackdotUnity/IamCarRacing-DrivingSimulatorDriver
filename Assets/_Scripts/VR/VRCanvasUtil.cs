using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Turns the game's screen-space canvases into world-space panels usable with VRIF laser pointers.</summary>
public static class VRCanvasUtil
{
    /// <summary>Raised for canvases created at runtime (store popup) so the VR manager can place them.</summary>
    public static event Action<Canvas> RuntimeCanvasCreated;

    public static void NotifyRuntimeCanvas(Canvas c)
    {
        if (c != null) RuntimeCanvasCreated?.Invoke(c);
    }

    /// <summary>Converts the canvas to world space, `metersWide` across, keeping its reference layout (1920x1080).</summary>
    public static void MakeWorldSpace(Canvas canvas, float metersWide)
    {
        if (canvas == null) return;
        var rt = (RectTransform)canvas.transform;
        Vector2 size = new Vector2(1920f, 1080f);
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) size = scaler.referenceResolution;

        if (canvas.renderMode != RenderMode.WorldSpace)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            if (scaler != null) scaler.enabled = false; // world-space canvases are sized by their RectTransform
        }
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.localScale = Vector3.one * (metersWide / size.x);

        if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        BNG.VRUISystem.Instance.AddCanvas(canvas);
        ApplyOnTopMaterial(canvas);
    }

    /// <summary>
    /// World space, exactly `metersWide` x `metersHigh` (e.g. a whole wall). Keeps the reference height (1080) and
    /// widens the layout instead, like a wide phone screen, so anchored UI spreads out rather than stretching.
    /// </summary>
    public static void MakeWorldSpace(Canvas canvas, float metersWide, float metersHigh)
    {
        MakeWorldSpace(canvas, metersWide);
        if (canvas != null && metersHigh > 0f) FitSize(canvas, metersWide, metersHigh);
    }

    public static void FitSize(Canvas canvas, float metersWide, float metersHigh)
    {
        var rt = (RectTransform)canvas.transform;
        float h = 1080f;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) h = scaler.referenceResolution.y;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(h * metersWide / metersHigh, h);
        rt.localScale = Vector3.one * (metersHigh / h);
    }

    static Material onTop;

    /// <summary>
    /// Default-material UI (Images, Texts) switches to a ZTest-Always copy of UI/Default,
    /// so panels are never hidden by the car body or scenery. Custom UI materials are left alone.
    /// </summary>
    public static void ApplyOnTopMaterial(Canvas canvas)
    {
        if (onTop == null) onTop = Resources.Load<Material>("VR/UIOverlayAlways");
        if (onTop == null) return;
        foreach (var g in canvas.GetComponentsInChildren<Graphic>(true))
            if (g.material == null || g.material == g.defaultMaterial || g.material.shader.name == "UI/Default")
                g.material = onTop;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { onTop = null; RuntimeCanvasCreated = null; }

    /// <summary>Pose `distance` m in front of the head (yaw only), `drop` m below eye level, tilted to face the eyes.</summary>
    public static void PlaceInFront(Transform t, Transform head, float distance, float drop = 0.1f)
    {
        Vector3 fwd = head.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 pos = head.position + fwd * distance + Vector3.down * drop;
        t.position = pos;
        t.rotation = Quaternion.LookRotation(pos - head.position, Vector3.up);
    }

    /// <summary>True when the active canvas shows a panel that needs the player's attention (pause, fail, shop...).</summary>
    public static bool AnyModalOpen()
    {
        var cm = CanvasMannager.Instance;
        if (StorePopup.IsOpen) return true;
        if (cm == null) return false;
        return Active(cm.InfoPannel) || Active(cm.PausePannel) || Active(cm.FailPannel) || Active(cm.CompletePannel) ||
               Active(cm.ObjectivePannel) || Active(cm.FuelRewardPannel);
    }

    static bool Active(GameObject g) => g != null && g.activeInHierarchy;
}
