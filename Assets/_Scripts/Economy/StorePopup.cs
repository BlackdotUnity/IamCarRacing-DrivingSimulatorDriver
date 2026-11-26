using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// In-app purchase popup for coins and keys. Built in code, so it works in any scene
/// (gameplay fail panel, fuel panel, main menu) without extra scene setup.
/// Look &amp; feel come from Resources/Economy/StoreTheme (fonts and sprites of the game UI).
/// </summary>
public class StorePopup : MonoBehaviour
{
    public struct Pack
    {
        public string productId;
        public Currency currency;
        public int amount;
        public string badge;
        public Pack(string id, Currency c, int a, string b = "") { productId = id; currency = c; amount = a; badge = b; }
    }

    /// <summary>Coin and key packs, in the order of the INAPP Inspector "Products" list (SKU, amount, badge come from there).</summary>
    public static Pack[] Packs
    {
        get
        {
            var list = new System.Collections.Generic.List<Pack>();
            foreach (var p in INAPP.Catalog)
            {
                if (string.IsNullOrEmpty(p.sku)) continue;
                if (p.reward == IAPReward.Coins) list.Add(new Pack(p.sku, Currency.Coins, p.amount, p.badge));
                else if (p.reward == IAPReward.Keys) list.Add(new Pack(p.sku, Currency.Keys, p.amount, p.badge));
            }
            return list.ToArray();
        }
    }

    static StorePopup instance;
    static StoreTheme theme;

    Text messageText, coinsText, keysText, statusText;
    RectTransform coinRow, keyRow;
    Price pendingPrice;
    Action pendingAction;
    bool hasPending;

    // ───────────────────────── public API ─────────────────────────

    /// <summary>Open the store. With a pending action, it runs automatically once the player can afford it.</summary>
    public static void Open(Currency focus, string message, Price? price = null, Action onAffordable = null)
    {
        if (instance == null) instance = Create();
        if (instance == null) return;
        instance.Show(focus, message, price, onAffordable);
    }

    public static void Close()
    {
        if (instance != null) instance.gameObject.SetActive(false);
    }

    public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

    /// <summary>Meta Store prices loaded after the popup was built.</summary>
    public static void RefreshPrices()
    {
        if (instance == null) return;
        foreach (var p in Packs)
        {
            var t = instance.transform.Find("Panel/Rows/" + p.currency + "Row/" + p.productId + "/Price");
            if (t != null) t.GetComponent<Text>().text = INAPP.GetPriceText(p.productId);
        }
    }

    public static void OnPurchaseResult(bool success, string info)
    {
        if (instance == null || !instance.gameObject.activeSelf) return;
        instance.HandlePurchase(success, info);
    }

    // ───────────────────────── behaviour ─────────────────────────

    void OnEnable() => GameEconomy.BalanceChanged += RefreshBalance;
    void OnDisable() => GameEconomy.BalanceChanged -= RefreshBalance;
    void OnDestroy() { if (instance == this) instance = null; }

    void Show(Currency focus, string message, Price? price, Action onAffordable)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        hasPending = price.HasValue && onAffordable != null;
        pendingPrice = price ?? default;
        pendingAction = onAffordable;
        messageText.text = string.IsNullOrEmpty(message) ? "Get more coins and keys" : message;
        statusText.text = "";
        // Needed currency first.
        if (focus == Currency.Keys) keyRow.SetAsFirstSibling();
        else coinRow.SetAsFirstSibling();
        RefreshBalance();
    }

    void RefreshBalance()
    {
        if (coinsText) coinsText.text = GameEconomy.Coins.ToString("N0");
        if (keysText) keysText.text = GameEconomy.Keys.ToString();
    }

    void Buy(Pack p)
    {
        if (INAPP.Instance == null)
        {
            statusText.text = "Store not available. Please try again.";
            return;
        }
        statusText.text = "Connecting to store...";
        INAPP.Instance.BuyProduct(p.productId);
    }

    void HandlePurchase(bool success, string info)
    {
        RefreshBalance();
        if (!success)
        {
            statusText.text = "Purchase not completed." + (string.IsNullOrEmpty(info) ? "" : " (" + info + ")");
            return;
        }
        statusText.text = "Purchase successful!";
        if (hasPending && GameEconomy.CanAfford(pendingPrice))
        {
            var action = pendingAction;
            var price = pendingPrice;
            hasPending = false;
            pendingAction = null;
            gameObject.SetActive(false);
            GameEconomy.TrySpend(price, action);
        }
    }

    // ───────────────────────── UI construction ─────────────────────────

    static StorePopup Create()
    {
        if (theme == null) theme = Resources.Load<StoreTheme>(StoreTheme.ResourcePath);
        if (EventSystem.current == null && UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null) return null;

        var root = new GameObject("StorePopup", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        root.AddComponent<GraphicRaycaster>();

        var popup = root.AddComponent<StorePopup>();
        popup.Build((RectTransform)root.transform);
        VRCanvasUtil.NotifyRuntimeCanvas(canvas); // Meta Quest: becomes a world-space panel
        return popup;
    }

    Font F => theme != null && theme.font != null ? theme.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    void Build(RectTransform root)
    {
        // Dim background (blocks touches to the game).
        var dim = Img("Dim", root, null, new Color(0f, 0f, 0f, 0.75f));
        Stretch(dim.rectTransform);
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;

        // Panel
        var panel = Img("Panel", root, theme ? theme.panel : null, theme && theme.panel ? Color.white : new Color(0.08f, 0.12f, 0.2f, 0.97f));
        if (theme && theme.panel && theme.panel.border != Vector4.zero) panel.type = Image.Type.Sliced;
        Place(panel.rectTransform, Vector2.zero, new Vector2(1240f, 820f));

        var header = Img("Header", panel.rectTransform, theme ? theme.header : null, theme && theme.header ? Color.white : new Color(1f, 0.75f, 0.1f));
        Place(header.rectTransform, new Vector2(0f, 360f), new Vector2(1240f, 96f));
        var title = Txt("Title", header.rectTransform, "SHOP", 46, theme ? theme.headerTextColor : Color.black);
        Stretch(title.rectTransform);

        // Close
        var close = Img("Close", panel.rectTransform, theme ? theme.closeButton : null, theme && theme.closeButton ? Color.white : new Color(0.9f, 0.2f, 0.2f));
        Place(close.rectTransform, new Vector2(585f, 375f), new Vector2(90f, 90f));
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.onClick.AddListener(() => gameObject.SetActive(false));
        if (!(theme && theme.closeButton)) { var x = Txt("X", close.rectTransform, "X", 48, Color.white); Stretch(x.rectTransform); }

        // Message
        messageText = Txt("Message", panel.rectTransform, "", 32, Color.white);
        Place(messageText.rectTransform, new Vector2(0f, 262f), new Vector2(1100f, 90f));

        // Balance
        var bal = new GameObject("Balance", typeof(RectTransform)).GetComponent<RectTransform>();
        bal.SetParent(panel.rectTransform, false);
        Place(bal, new Vector2(0f, 190f), new Vector2(700f, 60f));
        coinsText = BalanceChip(bal, new Vector2(-170f, 0f), theme ? theme.coinIcon : null, new Color(1f, 0.82f, 0.2f));
        keysText = BalanceChip(bal, new Vector2(170f, 0f), theme ? theme.keyIcon : null, new Color(0.6f, 0.9f, 1f));

        // Pack rows
        var rows = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
        rows.SetParent(panel.rectTransform, false);
        Place(rows, new Vector2(0f, -110f), new Vector2(1160f, 470f));
        var vl = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 20f; vl.childAlignment = TextAnchor.MiddleCenter; vl.childControlHeight = true; vl.childControlWidth = true; vl.childForceExpandHeight = true;
        coinRow = Row(rows, Currency.Coins);
        keyRow = Row(rows, Currency.Keys);

        statusText = Txt("Status", panel.rectTransform, "", 26, new Color(0.7f, 0.95f, 1f));
        Place(statusText.rectTransform, new Vector2(0f, -375f), new Vector2(1100f, 44f));
    }

    Text BalanceChip(RectTransform parent, Vector2 pos, Sprite icon, Color tint)
    {
        var chip = Img("Chip", parent, null, new Color(1f, 1f, 1f, 0.1f));
        Place(chip.rectTransform, pos, new Vector2(300f, 58f));
        var ic = Img("Icon", chip.rectTransform, icon, icon ? Color.white : tint);
        ic.preserveAspect = true;
        Place(ic.rectTransform, new Vector2(-110f, 0f), new Vector2(54f, 54f));
        var t = Txt("Value", chip.rectTransform, "0", 34, tint);
        Place(t.rectTransform, new Vector2(30f, 0f), new Vector2(220f, 58f));
        return t;
    }

    RectTransform Row(RectTransform parent, Currency c)
    {
        var row = new GameObject(c + "Row", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 26f; hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlHeight = true; hl.childControlWidth = true; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
        foreach (var p in Packs) if (p.currency == c) PackButton(row, p);
        return row;
    }

    void PackButton(RectTransform row, Pack p)
    {
        bool coins = p.currency == Currency.Coins;
        var bg = Img(p.productId, row, theme ? theme.packButton : null, theme && theme.packButton ? Color.white : (coins ? new Color(1f, 0.72f, 0.1f) : new Color(0.2f, 0.6f, 1f)));
        if (theme && theme.packButton && theme.packButton.border != Vector4.zero) bg.type = Image.Type.Sliced;
        var btn = bg.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        var pack = p;
        btn.onClick.AddListener(() => Buy(pack));

        var icon = Img("Icon", bg.rectTransform, coins ? (theme ? theme.coinIcon : null) : (theme ? theme.keyIcon : null), Color.white);
        icon.preserveAspect = true;
        Anchor(icon.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.36f, 0.82f));

        var amount = Txt("Amount", bg.rectTransform, (coins ? p.amount.ToString("N0") : "x" + p.amount), 40, theme ? theme.buttonTextColor : Color.black);
        amount.alignment = TextAnchor.MiddleLeft;
        Anchor(amount.rectTransform, new Vector2(0.38f, 0.42f), new Vector2(0.98f, 0.92f));

        var price = Txt("Price", bg.rectTransform, INAPP.GetPriceText(p.productId), 28, theme ? theme.buttonTextColor : Color.black);
        price.alignment = TextAnchor.MiddleLeft;
        Anchor(price.rectTransform, new Vector2(0.38f, 0.1f), new Vector2(0.98f, 0.45f));

        if (!string.IsNullOrEmpty(p.badge))
        {
            var badge = Img("Badge", bg.rectTransform, null, new Color(0.9f, 0.15f, 0.15f));
            Anchor(badge.rectTransform, new Vector2(0.55f, 0.84f), new Vector2(1.02f, 1.08f));
            var bt = Txt("Text", badge.rectTransform, p.badge, 20, Color.white);
            Stretch(bt.rectTransform);
        }
    }

    // ───────────────────────── tiny UI helpers ─────────────────────────

    static Sprite white;
    static Sprite White()
    {
        if (white != null) return white;
        var tex = Texture2D.whiteTexture;
        white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return white;
    }

    static Image Img(string name, Transform parent, Sprite sprite, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite != null ? sprite : White();
        img.color = c;
        return img;
    }

    Text Txt(string name, Transform parent, string s, int size, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = F;
        t.text = s;
        t.fontSize = size;
        t.color = c;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 14;
        t.resizeTextMaxSize = size;
        return t;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rt) => Anchor(rt, Vector2.zero, Vector2.one);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; theme = null; white = null; }
}
