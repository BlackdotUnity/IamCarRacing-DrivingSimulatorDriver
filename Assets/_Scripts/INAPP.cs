using System;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Platform;
using Oculus.Platform.Models;

/// <summary>Which shop item a button / label refers to. The SKU and price behind it are set in the INAPP Inspector.</summary>
/// <remarks>Numbers are fixed so saved scenes keep pointing at the same item (6 was the removed "No Ads").</remarks>
public enum IAPItem
{
    CoinPackSmall = 0, CoinPackMedium = 1, CoinPackLarge = 2,
    KeyPackSmall = 3, KeyPackMedium = 4, KeyPackLarge = 5,
    UnlockAllGame = 7, UnlockAllCars = 8, UnlockAllLevels = 9,
}

/// <summary>What the player receives. (2 was the removed "No Ads".)</summary>
public enum IAPReward { Coins = 0, Keys = 1, UnlockAllGame = 3, UnlockAllCars = 4, UnlockAllLevels = 5 }

/// <summary>One Meta Store add-on, edited in the INAPP Inspector.</summary>
[Serializable]
public class IAPProduct
{
    [Tooltip("Shown in the Inspector list only.")]
    public string name;
    public IAPItem item;
    [Tooltip("Add-on SKU, exactly as in the Meta Developer Dashboard.")]
    public string sku;
    [Tooltip("Consumable = can be bought again (coins, keys). Off = Durable (unlocks), restored automatically.")]
    public bool consumable;
    public IAPReward reward;
    [Tooltip("Coins / keys given (coin and key packs only).")]
    public int amount;
    [Tooltip("Price text shown in the game, e.g. \"$2.99\". Keep it equal to the price set in the Meta Dashboard.")]
    public string price;
    [Tooltip("Optional tag in the shop popup, e.g. POPULAR / BEST VALUE.")]
    public string badge;

    public IAPProduct(string name, IAPItem item, string sku, bool consumable, IAPReward reward, int amount, string price, string badge = "")
    {
        this.name = name; this.item = item; this.sku = sku; this.consumable = consumable;
        this.reward = reward; this.amount = amount; this.price = price; this.badge = badge;
    }
}

/// <summary>
/// Meta Quest Store purchases (Meta XR Platform SDK).
/// ALL product IDs (SKUs), prices, rewards and types are set in ONE place: the "Products" list in this
/// component's Inspector (Splash scene → InnAppMannager). The main-menu price labels (IAPPriceLabel),
/// the shop popup and the buy buttons all read from that list.
///
/// Flow: Core.AsyncInitialize → entitlement check → load store prices (IAP.GetProductsBySKU)
///       → restore owned items (IAP.GetViewerPurchases).
/// Buy:  IAP.LaunchCheckoutFlow(sku) → grant → IAP.ConsumePurchase(sku) for consumables.
/// </summary>
public class INAPP : MonoBehaviour
{
    public static INAPP Instance { set; get; }

    [Header("Products (Meta Store add-ons) — IDs, prices, rewards")]
    [Tooltip("Create an add-on with each SKU in the Meta Developer Dashboard (Consumable / Durable as ticked here).")]
    public List<IAPProduct> products = DefaultProducts();

    [Header("Prices")]
    [Tooltip("On: once the Meta Store answers, show its localized price (e.g. €2.79) instead of the Inspector price.")]
    public bool useStorePrices = true;

    [Header("Store")]
    [Tooltip("Quit the app if the user is not entitled to it (required for Meta Store submission).")]
    public bool quitIfNotEntitled = true;
    [Tooltip("Editor only: simulate successful purchases (the Platform SDK needs a logged-in test user in the Editor).")]
    public bool simulatePurchasesInEditor = true;

    // ── Legacy SKU fields (kept for older code); filled from the Products list on Awake ──
    public static string Product_10000_Coins = "iamcarcoins10000";
    public static string Product_15000_Coins = "iamcarcoins15000";
    public static string Product_20000_Coins = "iamcarcoins20000";
    public static string Product_UnLock_AllGame = "iamcarunlockallgame";
    public static string Product_UnLock_AllCars = "iamcarunlockallcars";
    public static string Product_UnLock_AllLevel = "iamcarunlockalllevel";
    public static string Product_5_Keys = "iamcarkeys5";
    public static string Product_15_Keys = "iamcarkeys15";
    public static string Product_40_Keys = "iamcarkeys40";

    public static bool IsReady { get; private set; }
    public static bool IsEntitled { get; private set; }

    static readonly Dictionary<string, string> storePrices = new Dictionary<string, string>();
    static List<IAPProduct> fallbackCatalog;
    bool initializing;

    const string GrantedKey = "meta_iap_granted_"; // + purchase ID: consumables already granted but maybe not consumed yet

    /// <summary>The values that were hard-coded before; used for a new component and as a fallback.</summary>
    public static List<IAPProduct> DefaultProducts() => new List<IAPProduct>
    {
        new IAPProduct("10,000 Coins", IAPItem.CoinPackSmall,  "iamcarcoins10000", true, IAPReward.Coins, 10000, "$2.99"),
        new IAPProduct("15,000 Coins", IAPItem.CoinPackMedium, "iamcarcoins15000", true, IAPReward.Coins, 15000, "$3.99", "POPULAR"),
        new IAPProduct("20,000 Coins", IAPItem.CoinPackLarge,  "iamcarcoins20000", true, IAPReward.Coins, 20000, "$5.99", "BEST VALUE"),
        new IAPProduct("5 Keys",       IAPItem.KeyPackSmall,   "iamcarkeys5",      true, IAPReward.Keys, 5,  ""),
        new IAPProduct("15 Keys",      IAPItem.KeyPackMedium,  "iamcarkeys15",     true, IAPReward.Keys, 15, "", "POPULAR"),
        new IAPProduct("40 Keys",      IAPItem.KeyPackLarge,   "iamcarkeys40",     true, IAPReward.Keys, 40, "", "BEST VALUE"),
        new IAPProduct("Unlock Everything", IAPItem.UnlockAllGame,  "iamcarunlockallgame",  false, IAPReward.UnlockAllGame, 0, "$51.99"),
        new IAPProduct("Unlock All Cars",   IAPItem.UnlockAllCars,  "iamcarunlockallcars",  false, IAPReward.UnlockAllCars, 0, "$19.99"),
        new IAPProduct("Unlock All Levels", IAPItem.UnlockAllLevels,"iamcarunlockalllevel", false, IAPReward.UnlockAllLevels, 0, "$19.99"),
    };

    // ───────────────────────── catalog (read by UI) ─────────────────────────

    /// <summary>The Inspector list of the live INAPP (defaults if the store object isn't loaded, e.g. a scene played directly).</summary>
    public static List<IAPProduct> Catalog
    {
        get
        {
            if (Instance != null && Instance.products != null && Instance.products.Count > 0) return Instance.products;
            return fallbackCatalog ??= DefaultProducts();
        }
    }

    public static IAPProduct Find(IAPItem item)
    {
        foreach (var p in Catalog) if (p.item == item) return p;
        return null;
    }

    public static IAPProduct FindBySku(string sku)
    {
        if (string.IsNullOrEmpty(sku)) return null;
        foreach (var p in Catalog) if (p.sku == sku) return p;
        return null;
    }

    public static string Sku(IAPItem item) => Find(item)?.sku ?? "";

    public void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SyncLegacyIds();
    }

    void SyncLegacyIds()
    {
        Product_10000_Coins = Sku(IAPItem.CoinPackSmall);
        Product_15000_Coins = Sku(IAPItem.CoinPackMedium);
        Product_20000_Coins = Sku(IAPItem.CoinPackLarge);
        Product_5_Keys = Sku(IAPItem.KeyPackSmall);
        Product_15_Keys = Sku(IAPItem.KeyPackMedium);
        Product_40_Keys = Sku(IAPItem.KeyPackLarge);
        Product_UnLock_AllGame = Sku(IAPItem.UnlockAllGame);
        Product_UnLock_AllCars = Sku(IAPItem.UnlockAllCars);
        Product_UnLock_AllLevel = Sku(IAPItem.UnlockAllLevels);
    }

    void OnValidate()
    {
        if (products == null) return;
        var items = new HashSet<IAPItem>();
        var skus = new HashSet<string>();
        foreach (var p in products)
        {
            if (!items.Add(p.item)) Debug.LogWarning("[MetaIAP] Products: " + p.item + " is listed twice.", this);
            if (string.IsNullOrWhiteSpace(p.sku)) Debug.LogWarning("[MetaIAP] Products: " + p.item + " has no SKU.", this);
            else if (!skus.Add(p.sku)) Debug.LogWarning("[MetaIAP] Products: SKU '" + p.sku + "' is used twice.", this);
        }
        if (UnityEngine.Application.isPlaying && Instance == this) { SyncLegacyIds(); GameEconomy.NotifyPricesUpdated(); }
    }

    void Start() => InitializePurchasing();

    // ───────────────────────── init / entitlement ─────────────────────────

    public void InitializePurchasing()
    {
        if (IsReady || initializing) return;
        initializing = true;
        try
        {
            if (Core.IsInitialized()) OnPlatformInitialized(null);
            else Core.AsyncInitialize().OnComplete(OnPlatformInitialized);
        }
        catch (Exception e)
        {
            initializing = false;
            Debug.LogWarning("[MetaIAP] Platform init failed: " + e.Message);
        }
    }

    void OnPlatformInitialized(Message<PlatformInitialize> msg)
    {
        if (msg != null && (msg.IsError || msg.Data == null || msg.Data.Result != PlatformInitializeResult.Success))
        {
            initializing = false;
            Debug.LogWarning("[MetaIAP] Platform init result: " + (msg.Data != null ? msg.Data.Result.ToString() : "error") +
                             (msg.IsError ? " - " + msg.GetError().Message : ""));
            return;
        }
        Entitlements.IsUserEntitledToApplication().OnComplete(OnEntitlement);
    }

    void OnEntitlement(Message msg)
    {
        initializing = false;
        if (msg.IsError)
        {
            IsEntitled = false;
            Debug.LogError("[MetaIAP] User is NOT entitled to this app: " + msg.GetError().Message);
#if !UNITY_EDITOR
            if (quitIfNotEntitled) UnityEngine.Application.Quit();
#endif
            return;
        }
        IsEntitled = true;
        IsReady = true;
        LoadPrices();
        RestorePurchases();
    }

    void LoadPrices()
    {
        var skus = new List<string>();
        foreach (var p in Catalog) if (!string.IsNullOrEmpty(p.sku)) skus.Add(p.sku);
        IAP.GetProductsBySKU(skus.ToArray()).OnComplete(msg =>
        {
            if (msg.IsError) { Debug.LogWarning("[MetaIAP] Products: " + msg.GetError().Message); return; }
            foreach (Product p in msg.Data)
                if (!string.IsNullOrEmpty(p.Sku)) storePrices[p.Sku] = p.FormattedPrice;
            GameEconomy.NotifyPricesUpdated();
        });
    }

    /// <summary>
    /// Re-applies durable items (unlocks) and finishes consumables whose consume call never completed.
    /// Called automatically after the entitlement check; also usable from a "Restore" button.
    /// </summary>
    public void RestorePurchases()
    {
        if (!IsReady) { InitializePurchasing(); return; }
        IAP.GetViewerPurchases().OnComplete(msg =>
        {
            if (msg.IsError) { Debug.LogWarning("[MetaIAP] Restore: " + msg.GetError().Message); return; }
            bool changed = false;
            foreach (Purchase p in msg.Data)
            {
                if (IsConsumable(p.Sku))
                {
                    // Bought but not consumed (app closed mid-purchase): grant once, then consume.
                    if (!PlayerPrefs.HasKey(GrantedKey + p.ID)) { ApplyProduct(p.Sku); MarkGranted(p.ID); changed = true; }
                    Consume(p.Sku, p.ID);
                }
                else
                {
                    ApplyProduct(p.Sku); // durable unlocks are idempotent
                    changed = true;
                }
            }
            if (changed) AfterGrant(null);
        });
    }

    static bool IsConsumable(string sku) => FindBySku(sku)?.consumable ?? false;

    // ───────────────────────── buy ─────────────────────────

    public void Buy(IAPItem item)
    {
        var p = Find(item);
        if (p == null || string.IsNullOrEmpty(p.sku)) { Debug.LogWarning("[MetaIAP] No product set up for " + item); return; }
        BuyProductID(p.sku);
    }

    // Existing button / script entry points.
    public void Buy10000CoinsFunction() => Buy(IAPItem.CoinPackSmall);
    public void Buy15000CoinsFunction() => Buy(IAPItem.CoinPackMedium);
    public void Buy20000CoinsFunction() => Buy(IAPItem.CoinPackLarge);
    public void BuyUnAllGameBtnFunction() => Buy(IAPItem.UnlockAllGame);
    public void BuyUnAllCarsBtnFunction() => Buy(IAPItem.UnlockAllCars);
    public void BuyUnAllLevelBtnFunction() => Buy(IAPItem.UnlockAllLevels);
    public void Buy5KeysFunction() => Buy(IAPItem.KeyPackSmall);
    public void Buy15KeysFunction() => Buy(IAPItem.KeyPackMedium);
    public void Buy40KeysFunction() => Buy(IAPItem.KeyPackLarge);

    /// <summary>Used by the store popup.</summary>
    public void BuyProduct(string productId) => BuyProductID(productId);

    /// <summary>Price text for a SKU: Meta Store's localized price when loaded (and enabled), else the Inspector price, else "BUY".</summary>
    public static string GetPriceText(string productId)
    {
        bool store = Instance == null || Instance.useStorePrices;
        if (store && storePrices.TryGetValue(productId, out var s) && !string.IsNullOrEmpty(s)) return s;
        var p = FindBySku(productId);
        return p != null && !string.IsNullOrEmpty(p.price) ? p.price : "BUY";
    }

    public static string GetPriceText(IAPItem item) => GetPriceText(Sku(item));

    void BuyProductID(string sku)
    {
#if UNITY_EDITOR
        if (simulatePurchasesInEditor && !IsReady)
        {
            Debug.Log("[MetaIAP] Editor: simulated purchase of " + sku);
            ApplyProduct(sku);
            AfterGrant(sku);
            return;
        }
#endif
        if (!IsReady)
        {
            GameEconomy.NotifyPurchaseFailed(sku, IsEntitled ? "store not ready" : "Meta Store not available");
            InitializePurchasing();
            return;
        }

        IAP.LaunchCheckoutFlow(sku).OnComplete(msg =>
        {
            if (msg.IsError)
            {
                var err = msg.GetError();
                Debug.Log("[MetaIAP] Checkout " + sku + " not completed: " + err.Message);
                GameEconomy.NotifyPurchaseFailed(sku, err.Message);
                return;
            }
            Purchase p = msg.Data;
            string bought = p != null && !string.IsNullOrEmpty(p.Sku) ? p.Sku : sku;
            ApplyProduct(bought);
            if (IsConsumable(bought))
            {
                if (p != null) MarkGranted(p.ID);
                Consume(bought, p != null ? p.ID : null);
            }
            AfterGrant(bought);
        });
    }

    void Consume(string sku, string purchaseId)
    {
        IAP.ConsumePurchase(sku).OnComplete(msg =>
        {
            if (msg.IsError) { Debug.LogWarning("[MetaIAP] Consume " + sku + " failed (will retry on next launch): " + msg.GetError().Message); return; }
            if (!string.IsNullOrEmpty(purchaseId)) PlayerPrefs.DeleteKey(GrantedKey + purchaseId);
        });
    }

    static void MarkGranted(string purchaseId)
    {
        if (string.IsNullOrEmpty(purchaseId)) return;
        PlayerPrefs.SetInt(GrantedKey + purchaseId, 1);
        PlayerPrefs.Save();
    }

    // ───────────────────────── grant ─────────────────────────

    /// <summary>Gives the player what the product's reward (Inspector) says.</summary>
    static void ApplyProduct(string sku)
    {
        var data = OverAllGameMannager.OA_GM != null ? OverAllGameMannager.OA_GM.SaveData : null;
        if (data == null) return;
        var p = FindBySku(sku);
        if (p == null) { Debug.LogWarning("[MetaIAP] Unknown SKU granted: " + sku); return; }

        switch (p.reward)
        {
            case IAPReward.Coins: data.TotalCoins += p.amount; break;
            case IAPReward.Keys: data.TotalKeys += p.amount; break;
            case IAPReward.UnlockAllGame:
                data.UnlockAllCars = true;
                UnlockAllCarsInSave(data); // also when bought / restored outside the main menu (Splash, GamePlay)
                data.UnlockAllLevel = true;
                data.UnlockAllGame = true;
                data.HasUnlimitedFuel = true;
                MissionSaveUtil.UnlockEverything(data);
                if (MainMenuMannager.MM_Instance != null)
                {
                    MainMenuMannager.MM_Instance.UnlockAllCarsFunsSystem();
                    MainMenuMannager.MM_Instance.UnlockAllLevel();
                }
                break;
            case IAPReward.UnlockAllCars:
                data.UnlockAllCars = true;
                UnlockAllCarsInSave(data);
                if (MainMenuMannager.MM_Instance != null) MainMenuMannager.MM_Instance.UnlockAllCarsFunsSystem();
                break;
            case IAPReward.UnlockAllLevels:
                data.UnlockAllLevel = true;
                MissionSaveUtil.UnlockEverything(data);
                if (MainMenuMannager.MM_Instance != null) MainMenuMannager.MM_Instance.UnlockAllLevel();
                break;
        }
    }

    public static void UnlockAllCarsInSave(SaveData data)
    {
        if (data == null) return;
        foreach (var c in data.Cars) c.isUnlocked = true;
    }

    static void AfterGrant(string sku)
    {
        if (MainMenuMannager.MM_Instance != null) MainMenuMannager.MM_Instance.CheckInAppIsPurashedFun();
        if (OverAllGameMannager.OA_GM != null) OverAllGameMannager.OA_GM.SaveDataToJson();
        if (sku != null) GameEconomy.NotifyPurchased(sku);
        else GameEconomy.NotifyPricesUpdated();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { storePrices.Clear(); fallbackCatalog = null; IsReady = false; IsEntitled = false; Instance = null; }
}
