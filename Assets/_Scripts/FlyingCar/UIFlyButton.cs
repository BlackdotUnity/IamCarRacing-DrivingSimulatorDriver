using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mobile Fly button. Press and hold = lift, release = descend (or tap to toggle, see the profile).
/// Works with multi-touch together with the RCC gas / brake / steer buttons.
/// Visual states: Normal, Pressed, Flying (pulsing glow), Disabled (no energy / car can't fly).
/// </summary>
[DisallowMultipleComponent]
public class UIFlyButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Graphics (optional)")]
    [Tooltip("Main button image, tinted by state.")]
    public Image background;
    [Tooltip("Icon / glow image, pulses while flying.")]
    public Image glow;
    public Text label;

    [Header("State Colours")]
    public Color normalColor = Color.white;
    public Color pressedColor = new Color(0.55f, 0.9f, 1f, 1f);
    public Color flyingColor = new Color(0.35f, 0.85f, 1f, 1f);
    public Color disabledColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Animation")]
    public float pressedScale = 0.9f;
    public float scaleSpeed = 18f;
    public float pulseSpeed = 4f;

    [Header("Labels")]
    public string normalLabel = "FLY";
    public string flyingLabel = "FLY";
    public string disabledLabel = "...";

    bool pressed;
    bool interactable = true;
    float checkTimer;
    Vector3 baseScale;

    void Awake()
    {
        baseScale = transform.localScale;
        if (!background) background = GetComponent<Image>();
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (pressed || !interactable) return;
        pressed = true;
        FlyingCarInput.PressUI();
    }

    public void OnPointerUp(PointerEventData e)
    {
        Release();
    }

    void Release()
    {
        if (!pressed) return;
        pressed = false;
        FlyingCarInput.ReleaseUI();
    }

    void OnDisable()
    {
        Release();
        transform.localScale = baseScale;
    }

    void Update()
    {
        var mgr = FlyingCarManager.Instance;
        var module = mgr ? mgr.ActiveModule : null;
        bool flying = module && module.IsFlying;

        // Cheap 5 Hz check for whether take-off is possible.
        checkTimer -= Time.unscaledDeltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = 0.2f;
            bool can = module && (flying || module.CanTakeOff);
            if (can != interactable)
            {
                interactable = can;
                if (!can && !flying) Release();
            }
        }

        // Scale feedback
        float targetScale = pressed ? pressedScale : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * targetScale, 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime));

        // Colours
        Color c = !interactable ? disabledColor : pressed ? pressedColor : flying ? flyingColor : normalColor;
        if (background) background.color = c;

        if (glow)
        {
            float a = flying ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * pulseSpeed) : (pressed ? 1f : 0f);
            Color gc = flyingColor;
            gc.a = Mathf.Clamp01(a);
            glow.color = gc;
        }

        if (label)
        {
            string s = !interactable ? disabledLabel : flying ? flyingLabel : normalLabel;
            if (!ReferenceEquals(label.text, s)) label.text = s;
        }
    }
}
