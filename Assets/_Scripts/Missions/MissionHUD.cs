using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small top-centre panel for generated missions: step status, progress bar and strikes.
/// Built in code at runtime, so the scene needs no extra UI setup. Writes only on change.
/// </summary>
public class MissionHUD : MonoBehaviour
{
    Text status, strikes;
    Image barBg, barFill;
    RectTransform root;
    string lastStatus, lastStrikes;
    float lastProgress = -1f;
    Color lastColor;

    static Sprite white;

    public static MissionHUD Create(Transform parent, Font font)
    {
        var go = new GameObject("MissionHUD", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var hud = go.AddComponent<MissionHUD>();
        hud.Build(font);
        return hud;
    }

    static Sprite White()
    {
        if (white != null) return white;
        var tex = Texture2D.whiteTexture;
        white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return white;
    }

    void Build(Font font)
    {
        root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(17f, -182f);
        root.sizeDelta = new Vector2(620f, 92f);

        var bg = gameObject.AddComponent<Image>();
        bg.sprite = White();
        bg.color = new Color(0.03f, 0.06f, 0.12f, 0.72f);
        bg.raycastTarget = false;

        var edge = NewImage("Edge", root, new Color(0.15f, 0.85f, 1f, 0.9f));
        Stretch(edge.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 3f));

        status = NewText("Status", root, font, 26, TextAnchor.MiddleCenter);
        Stretch(status.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 1f), new Vector2(14f, 0f), new Vector2(-14f, -4f));

        barBg = NewImage("BarBg", root, new Color(1f, 1f, 1f, 0.12f));
        Stretch(barBg.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 14f), new Vector2(-150f, 30f));

        barFill = NewImage("BarFill", barBg.rectTransform, new Color(0.15f, 0.85f, 1f));
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillAmount = 0f;
        Stretch(barFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        strikes = NewText("Strikes", root, font, 20, TextAnchor.MiddleRight);
        Stretch(strikes.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-140f, 6f), new Vector2(-14f, 38f));
        strikes.color = new Color(1f, 0.45f, 0.4f);

        SetProgress(-1f, Color.white);
    }

    static Image NewImage(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = White();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    static Text NewText(string name, Transform parent, Font font, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 14;
        t.resizeTextMaxSize = size;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
        return t;
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    public void SetStatus(string s)
    {
        if (s == lastStatus) return;
        lastStatus = s;
        status.text = s;
    }

    /// <summary>progress &lt; 0 hides the bar.</summary>
    public void SetProgress(float progress, Color color)
    {
        bool show = progress >= 0f;
        if (barBg.gameObject.activeSelf != show) barBg.gameObject.SetActive(show);
        if (!show) { lastProgress = -1f; return; }
        progress = Mathf.Clamp01(progress);
        if (Mathf.Abs(progress - lastProgress) > 0.004f) { barFill.fillAmount = progress; lastProgress = progress; }
        if (color != lastColor) { barFill.color = color; lastColor = color; }
    }

    public void SetStrikes(string s)
    {
        if (s == lastStrikes) return;
        lastStrikes = s;
        strikes.text = s;
    }

    public void Show(bool on)
    {
        if (gameObject.activeSelf != on) gameObject.SetActive(on);
    }
}
