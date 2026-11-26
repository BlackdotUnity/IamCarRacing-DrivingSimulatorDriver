using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > Economy > Setup Economy UI (safe to run again):
///  • Resources/Economy/StoreTheme.asset (fonts / sprites for the store popup)
///  • GamePlay: rewarded-ad buttons become coin / key buttons (no "AD" artwork)
///  • MainMenu: hides "Remove Ads", adds key packs to the shop panel
///  • Splash: removes the old ads manager object and any missing scripts
/// </summary>
public static class EconomySetup
{
    const string ThemePath = "Assets/Resources/Economy/StoreTheme.asset";
    const string Splash = "Assets/Scenes/Splash.unity";
    const string GamePlay = "Assets/Scenes/GamePlay.unity";
    const string MainMenu = "Assets/Scenes/MainMenu.unity";
    const string PlainButton = "Assets/UI/level failed/fuel.png";

    [MenuItem("Tools/Economy/Setup Economy UI")]
    public static void SetupAll()
    {
        EditorSceneManager.SaveOpenScenes();
        string previous = EditorSceneManager.GetActiveScene().path;
        var theme = EnsureTheme();

        var s = EditorSceneManager.OpenScene(Splash, OpenSceneMode.Single);
        CleanSplash();
        RemoveMissingScripts(s);
        EditorSceneManager.MarkSceneDirty(s);
        EditorSceneManager.SaveScene(s);

        var g = EditorSceneManager.OpenScene(GamePlay, OpenSceneMode.Single);
        SetupGamePlay(theme);
        RemoveMissingScripts(g);
        EditorSceneManager.MarkSceneDirty(g);
        EditorSceneManager.SaveScene(g);

        var m = EditorSceneManager.OpenScene(MainMenu, OpenSceneMode.Single);
        SetupMainMenu(theme);
        RemoveMissingScripts(m);
        EditorSceneManager.MarkSceneDirty(m);
        EditorSceneManager.SaveScene(m);

        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[Economy] Setup complete: ads removed from scenes, coin/key buttons and shop ready.");
    }

    // ───────────────────────── theme ─────────────────────────

    static StoreTheme EnsureTheme()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
        var t = AssetDatabase.LoadAssetAtPath<StoreTheme>(ThemePath);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<StoreTheme>();
            AssetDatabase.CreateAsset(t, ThemePath);
        }
        if (t.font == null) t.font = FindAsset<Font>("FredokaOne", "t:Font");
        if (t.panel == null) t.panel = FindSprite("panel", "Assets/UI/level failed");
        if (t.header == null) t.header = FindSprite("panel-header1", "Assets/UI");
        if (t.packButton == null) t.packButton = AssetDatabase.LoadAssetAtPath<Sprite>(PlainButton);
        if (t.closeButton == null) t.closeButton = FindSprite("close", "Assets/UI") ?? FindSprite("cross", "Assets/UI");
        if (t.coinIcon == null) t.coinIcon = EconomyIcons.Coin();
        if (t.keyIcon == null) t.keyIcon = EconomyIcons.Key();
        if (t.panel == null || t.panel.name != "panel") t.panel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Store/panel.png") ?? t.panel;
        EditorUtility.SetDirty(t);
        AssetDatabase.SaveAssetIfDirty(t);
        return t;
    }

    static T FindAsset<T>(string name, string filter) where T : Object
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " " + filter))
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null) return a;
        }
        return null;
    }

    /// <summary>Sprite whose file name matches exactly, preferring the given folder.</summary>
    static Sprite FindSprite(string name, string preferFolder)
    {
        Sprite fallback = null;
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetFileNameWithoutExtension(p), name, System.StringComparison.OrdinalIgnoreCase)) continue;
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s == null) continue;
            if (p.StartsWith(preferFolder)) return s;
            if (fallback == null) fallback = s;
        }
        return fallback;
    }

    // ───────────────────────── Splash ─────────────────────────

    static void CleanSplash()
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "AdsMannager" || root.name == "AdsManager")
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    static void RemoveMissingScripts(UnityEngine.SceneManagement.Scene scene)
    {
        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        if (removed > 0) Debug.Log("[Economy] Removed " + removed + " missing script(s) in " + scene.name);
    }

    // ───────────────────────── GamePlay ─────────────────────────

    static void SetupGamePlay(StoreTheme theme)
    {
        var cm = Object.FindFirstObjectByType<CanvasMannager>(FindObjectsInactive.Include);
        if (cm == null) { Debug.LogWarning("[Economy] CanvasMannager not found."); return; }

        Style(cm.SkipLevelBtn, CostButton.Action.SkipLevel, "SKIP", theme);
        // Skip and Continue used to be exclusive and shared one slot; now both can show,
        // so Skip moves to the slot above (where the full-tank button sits for fuel fails).
        if (cm.SkipLevelBtn != null && cm.Fuel1000coinsBtn != null)
        {
            var skip = (RectTransform)cm.SkipLevelBtn.transform;
            var above = (RectTransform)cm.Fuel1000coinsBtn.transform;
            skip.anchorMin = above.anchorMin; skip.anchorMax = above.anchorMax; skip.pivot = above.pivot;
            skip.anchoredPosition = above.anchoredPosition;
            skip.sizeDelta = above.sizeDelta;
        }
        Style(cm.KeyLifeBtn, CostButton.Action.ContinueAfterCrash, "CONTINUE", theme);
        Style(cm.TimeBtn, CostButton.Action.ExtraTime, "+30 SEC", theme);
        Style(cm.FuelBtn, CostButton.Action.SmallRefuel, "+30% FUEL", theme);
        Style(cm.Fuel1000coinsBtn, CostButton.Action.FullRefuel, "FULL TANK", theme);
        Style(cm.ExteraFueWatchVideoAds, CostButton.Action.SmallRefuel, "+30% FUEL", theme);
        if (cm.FuelRewardPannel != null)
        {
            var coinsBtn = cm.FuelRewardPannel.transform.Find("Panel/CoinsBtn");
            if (coinsBtn != null) Style(coinsBtn.gameObject, CostButton.Action.FullRefuel, "FULL TANK", theme);
            foreach (var t in cm.FuelRewardPannel.GetComponentsInChildren<Text>(true))
            {
                if (t.text == "Fuel Reward") t.text = "Refuel";
                if (t.text.Contains("Watch Video"))
                    t.text = "Low on fuel? Refuel with coins.\n+30% fuel for " + GameEconomy.SmallRefuel.amount + " coins, or a full tank for " + GameEconomy.FullRefuel.amount + " coins.";
            }
        }
        EditorUtility.SetDirty(cm);
    }

    /// <summary>Plain button art + "TITLE  [icon] price" label driven by CostButton.</summary>
    static void Style(GameObject go, CostButton.Action action, string title, StoreTheme theme)
    {
        if (go == null) return;
        var img = go.GetComponent<Image>();
        var plain = AssetDatabase.LoadAssetAtPath<Sprite>(PlainButton);
        if (img != null && plain != null) img.sprite = plain;

        // Hide the old label (e.g. "Buy Fuel (1000coins)"), keep it in the hierarchy.
        foreach (Transform c in go.transform)
            if (c.name != "CostLabel" && c.GetComponent<Text>() != null) c.gameObject.SetActive(false);

        var old = go.transform.Find("CostLabel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var label = new GameObject("CostLabel", typeof(RectTransform));
        label.transform.SetParent(go.transform, false);
        Fill((RectTransform)label.transform, Vector2.zero, Vector2.one);

        var titleText = NewText("Title", label.transform, theme.font, title, TextAnchor.MiddleLeft);
        Fill(titleText.rectTransform, new Vector2(0.08f, 0.1f), new Vector2(0.62f, 0.9f));

        var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
        icon.transform.SetParent(label.transform, false);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Fill(icon.rectTransform, new Vector2(0.63f, 0.12f), new Vector2(0.77f, 0.88f));

        var price = NewText("Price", label.transform, theme.font, "", TextAnchor.MiddleLeft);
        Fill(price.rectTransform, new Vector2(0.78f, 0.1f), new Vector2(0.95f, 0.9f));

        var cost = go.GetComponent<CostButton>();
        if (cost == null) cost = go.AddComponent<CostButton>();
        cost.action = action;
        cost.title = title;
        cost.titleText = titleText;
        cost.priceText = price;
        cost.icon = icon;
        cost.coinSprite = theme.coinIcon;
        cost.keySprite = theme.keyIcon;
        cost.Refresh();
        EditorUtility.SetDirty(go);
    }

    static Text NewText(string name, Transform parent, Font font, string s, TextAnchor anchor)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.text = s;
        t.fontSize = 30;
        t.color = new Color(0.06f, 0.06f, 0.06f);
        t.alignment = anchor;
        t.raycastTarget = false;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = 30;
        return t;
    }

    static void Fill(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ───────────────────────── MainMenu ─────────────────────────

    static void SetupMainMenu(StoreTheme theme)
    {
        var mm = Object.FindFirstObjectByType<MainMenuMannager>(FindObjectsInactive.Include);
        if (mm == null) { Debug.LogWarning("[Economy] MainMenuMannager not found."); return; }
        MainMenuKeyPacks.Build(mm, theme);
        EditorUtility.SetDirty(mm);
    }
}
