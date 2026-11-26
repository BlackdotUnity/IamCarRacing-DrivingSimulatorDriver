using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Tools > Missions > Setup Chapter Panel (safe to run again).
/// Builds "ChapterPannel" in MainMenu.unity by copying the level panel's look:
/// a Practice card + 10 chapter cards in a horizontal scroll view.
/// Flow becomes: Garage → SELECT CHAPTER → SELECT LEVEL (back returns to chapters).
/// </summary>
public static class ChapterPanelSetup
{
    const string MainMenuScene = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/Missions/Setup Chapter Panel")]
    public static void Setup()
    {
        EditorSceneManager.SaveOpenScenes();
        string previous = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(MainMenuScene, OpenSceneMode.Single);

        var mm = Object.FindFirstObjectByType<MainMenuMannager>(FindObjectsInactive.Include);
        if (mm == null || mm.LevelPannel == null) { Debug.LogWarning("[Chapters] MainMenuMannager / LevelPannel not found."); return; }

        var levelPanel = mm.LevelPannel;
        var parent = levelPanel.transform.parent;
        var old = parent.Find("ChapterPannel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // ── clone the level panel's look ──
        var panel = Object.Instantiate(levelPanel, parent);
        panel.name = "ChapterPannel";
        panel.SetActive(false);
        panel.transform.SetSiblingIndex(levelPanel.transform.GetSiblingIndex());

        var bar = panel.transform.Find("ChapterSelector");
        if (bar != null) Object.DestroyImmediate(bar.gameObject);

        var titleT = panel.transform.Find("Header/Title");
        if (titleT != null) foreach (var t in titleT.GetComponentsInChildren<Text>(true)) t.text = "CHAPTERS";

        var back = panel.transform.Find("Header/BackBtn");
        if (back != null)
        {
            var b = back.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(b.onClick, new UnityAction(mm.BackChapterBtnFun));
        }

        // ── cards ──
        var scroll = panel.GetComponentInChildren<ScrollRect>(true);
        var content = scroll.content;
        Transform template = null;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var c = content.GetChild(i);
            if (template == null && c.name == "Level1Btn") { template = c; continue; }
            Object.DestroyImmediate(c.gameObject);
        }
        if (template == null) { Debug.LogWarning("[Chapters] Level1Btn template not found."); return; }
        template.SetParent(null, false);

        // Horizontal auto layout so 11 cards scroll nicely on any screen.
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);
        var hl = content.GetComponent<HorizontalLayoutGroup>();
        if (hl == null) hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 40f;
        hl.padding = new RectOffset(70, 70, 0, 0);
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = hl.childControlHeight = false;
        hl.childForceExpandWidth = hl.childForceExpandHeight = false;
        var fit = content.GetComponent<ContentSizeFitter>();
        if (fit == null) fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        scroll.horizontal = true;
        scroll.vertical = false;

        Font font = null;
        var tmplText = template.GetComponentInChildren<Text>(true);
        if (tmplText != null) font = tmplText.font;

        var cards = new ChapterCard[MissionChapters.ChapterCount + 1];
        for (int i = -1; i < MissionChapters.ChapterCount; i++)
            cards[i + 1] = MakeCard(template, content, i, font);
        Object.DestroyImmediate(template.gameObject);

        var sel = panel.GetComponent<ChapterSelectPanel>();
        if (sel == null) sel = panel.AddComponent<ChapterSelectPanel>();
        sel.cards = cards;

        mm.ChapterPannel = panel;

        // The level panel no longer needs its own chapter arrows (chapters are picked on the new screen).
        var selector = levelPanel.GetComponentInChildren<ChapterSelectorUI>(true);
        if (selector != null)
        {
            if (selector.prevButton) selector.prevButton.gameObject.SetActive(false);
            if (selector.nextButton) selector.nextButton.gameObject.SetActive(false);
        }

        EditorUtility.SetDirty(mm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(previous) && previous != MainMenuScene) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[Chapters] Chapter panel ready: Practice + " + MissionChapters.ChapterCount + " chapters.");
    }

    static ChapterCard MakeCard(Transform template, RectTransform content, int chapter, Font font)
    {
        var go = Object.Instantiate(template.gameObject, content);
        go.name = chapter < 0 ? "PracticeCard" : "ChapterCard" + (chapter + 1);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(290f, 440f);

        var btn = go.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent();

        // Existing bottom label becomes the "CHAPTER 3" title.
        Text title = null;
        GameObject lockIcon = null;
        foreach (Transform c in go.transform)
        {
            if (title == null && c.GetComponent<Text>() != null) title = c.GetComponent<Text>();
            else if (c.name.StartsWith("Lock")) lockIcon = c.gameObject;
        }
        if (lockIcon != null) { lockIcon.name = "Lock"; lockIcon.transform.SetAsLastSibling(); }

        // Dark band at the top with the chapter name and progress.
        var band = new GameObject("Band", typeof(RectTransform)).AddComponent<Image>();
        band.transform.SetParent(go.transform, false);
        band.color = chapter < 0 ? new Color(0.05f, 0.35f, 0.15f, 0.85f) : new Color(0.02f, 0.05f, 0.12f, 0.78f);
        band.raycastTarget = false;
        Fill(band.rectTransform, new Vector2(0.04f, 0.66f), new Vector2(0.96f, 0.975f));
        band.transform.SetSiblingIndex(1);

        var name = NewText("Name", band.transform, font, 34, Color.white);
        Fill(name.rectTransform, new Vector2(0.04f, 0.5f), new Vector2(0.96f, 0.96f));
        var info = NewText("Info", band.transform, font, 22, new Color(0.75f, 0.95f, 1f));
        Fill(info.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.5f));

        // Progress bar just above the yellow title strip.
        Image fill = null;
        if (chapter >= 0)
        {
            var barBg = new GameObject("Progress", typeof(RectTransform)).AddComponent<Image>();
            barBg.transform.SetParent(go.transform, false);
            barBg.color = new Color(0f, 0f, 0f, 0.55f);
            barBg.raycastTarget = false;
            Fill(barBg.rectTransform, new Vector2(0.08f, 0.205f), new Vector2(0.92f, 0.235f));
            fill = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
            fill.transform.SetParent(barBg.transform, false);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = new Color(1f, 0.8f, 0.1f);
            fill.raycastTarget = false;
            Fill(fill.rectTransform, Vector2.zero, Vector2.one);
        }

        var card = go.AddComponent<ChapterCard>();
        card.chapter = chapter;
        card.titleText = title;
        card.nameText = name;
        card.infoText = info;
        card.lockIcon = lockIcon;
        card.progressFill = fill;

        // Static preview text (runtime Refresh() replaces it).
        if (title != null) title.text = chapter < 0 ? "PRACTICE" : "CHAPTER " + (chapter + 1);
        name.text = chapter < 0 ? "FREE DRIVE" : MissionChapters.Name(chapter);
        info.text = chapter < 0 ? "No timer • No fail" : "LEVELS " + (chapter * 10 + 1) + "-" + (chapter * 10 + 10);
        if (lockIcon != null) lockIcon.SetActive(chapter > 0);
        return card;
    }

    static Text NewText(string n, Transform parent, Font font, int size, Color c)
    {
        var t = new GameObject(n, typeof(RectTransform)).AddComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.fontSize = size;
        t.color = c;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 12;
        t.resizeTextMaxSize = size;
        t.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        return t;
    }

    static void Fill(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
