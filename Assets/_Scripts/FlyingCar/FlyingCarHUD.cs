using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flight status indicator: mode label, energy bar and altitude.
/// Refreshes at a fixed low rate (default 10 Hz) and only writes UI when values change,
/// to keep canvas rebuilds (and Android frame time) low.
/// </summary>
[DisallowMultipleComponent]
public class FlyingCarHUD : MonoBehaviour
{
    [Tooltip("Object shown while a flying-capable car is active (usually this object's first child).")]
    public GameObject content;
    public Text statusText;
    public Image statusDot;
    public Image energyFill;
    public Text energyText;
    public Text altitudeText;

    [Header("Behaviour")]
    [Tooltip("Hide the indicator while driving on the ground with full energy.")]
    public bool hideWhenIdle = false;
    public float refreshRate = 10f;

    [Header("Colours")]
    public Color groundColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    public Color liftOffColor = new Color(0.4f, 0.9f, 1f, 1f);
    public Color flyingColor = new Color(0.2f, 0.75f, 1f, 1f);
    public Color landingColor = new Color(1f, 0.8f, 0.3f, 1f);
    public Color lowEnergyColor = new Color(1f, 0.3f, 0.25f, 1f);

    float timer;
    int lastEnergy = -1, lastAlt = -1;
    FlightState lastState = (FlightState)(-1);
    bool lastLocked;

    void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = 1f / Mathf.Max(1f, refreshRate);

        var mgr = FlyingCarManager.Instance;
        var m = mgr ? mgr.ActiveModule : null;

        bool show = m && m.canFly && (!hideWhenIdle || m.IsFlying || m.Energy01 < 0.999f);
        if (content && content.activeSelf != show) content.SetActive(show);
        if (!m) return;

        bool locked = m.EnergyLocked;
        if (m.State != lastState || locked != lastLocked)
        {
            lastState = m.State;
            lastLocked = locked;
            string s; Color c;
            switch (m.State)
            {
                case FlightState.LiftOff: s = "LIFT OFF"; c = liftOffColor; break;
                case FlightState.Flying: s = "FLYING"; c = flyingColor; break;
                case FlightState.Landing: s = "LANDING"; c = landingColor; break;
                default: s = locked ? "RECHARGING" : "DRIVE"; c = locked ? lowEnergyColor : groundColor; break;
            }
            if (statusText) { statusText.text = s; statusText.color = c; }
            if (statusDot) statusDot.color = c;
        }

        int e = Mathf.RoundToInt(m.Energy01 * 100f);
        if (e != lastEnergy)
        {
            lastEnergy = e;
            if (energyFill)
            {
                energyFill.fillAmount = e / 100f;
                energyFill.color = e <= 20 ? lowEnergyColor : flyingColor;
            }
            if (energyText) energyText.text = e + "%";
        }

        if (altitudeText)
        {
            int alt = m.IsFlying ? Mathf.RoundToInt(m.HeightAboveGround) : 0;
            if (alt != lastAlt)
            {
                lastAlt = alt;
                altitudeText.text = m.IsFlying ? "ALT " + alt + " m" : "";
            }
        }
    }
}
