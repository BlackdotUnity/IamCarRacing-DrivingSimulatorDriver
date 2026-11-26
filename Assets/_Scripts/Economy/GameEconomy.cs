using System;
using UnityEngine;

/// <summary>What a paid action costs.</summary>
public enum Currency { Coins, Keys }

[Serializable]
public struct Price
{
    public Currency currency;
    public int amount;

    public Price(Currency c, int a) { currency = c; amount = a; }

    public override string ToString() => amount + (currency == Currency.Coins ? " COINS" : amount == 1 ? " KEY" : " KEYS");
}

/// <summary>
/// Coins &amp; keys economy that replaces every rewarded ad.
/// All prices live here so they can be tuned in one place.
///
/// Flow: TrySpend(price, onPaid) → enough balance: deduct + save + onPaid().
///       Not enough: the store popup opens with the action pending; after a successful
///       in-app purchase the action completes automatically if the player can now afford it.
/// </summary>
public static class GameEconomy
{
    // ───────────── prices (rewarded-ad replacements) ─────────────
    /// <summary>Fail panel "Skip level" (was a rewarded ad).</summary>
    public static Price SkipLevel = new Price(Currency.Keys, 2);
    /// <summary>Fail panel "Extra time +30 s" (was a rewarded ad).</summary>
    public static Price ExtraTime = new Price(Currency.Coins, 500);
    /// <summary>"+30% fuel" on the fail panel and the in-game fuel panel (was a rewarded ad).</summary>
    public static Price SmallRefuel = new Price(Currency.Coins, 500);
    /// <summary>Full tank (the existing 1000-coin button).</summary>
    public static Price FullRefuel = new Price(Currency.Coins, 1000);
    /// <summary>Continue after a crash (the existing key button).</summary>
    public static Price ContinueAfterCrash = new Price(Currency.Keys, 1);

    public const float ExtraSeconds = 30f;
    public const float SmallRefuelAmount = 30f;

    /// <summary>Raised whenever coins or keys change (purchases, spending).</summary>
    public static event Action BalanceChanged;

    static SaveData Data => OverAllGameMannager.OA_GM != null ? OverAllGameMannager.OA_GM.SaveData : null;

    public static int Coins => Data != null ? Data.TotalCoins : 0;
    public static int Keys => Data != null ? Data.TotalKeys : 0;

    public static int Balance(Currency c) => c == Currency.Coins ? Coins : Keys;

    public static bool CanAfford(Price p) => Balance(p.currency) >= p.amount;

    /// <summary>Pay for an action. Opens the store when the balance is too low.</summary>
    public static bool TrySpend(Price price, Action onPaid, string actionName = "")
    {
        if (Data == null) return false;
        if (!CanAfford(price))
        {
            StorePopup.Open(price.currency, "Not enough " + (price.currency == Currency.Coins ? "coins" : "keys") +
                (string.IsNullOrEmpty(actionName) ? "" : " for " + actionName) + ".\nYou need " + price + ".", price, onPaid);
            return false;
        }
        Deduct(price);
        onPaid?.Invoke();
        return true;
    }

    static void Deduct(Price price)
    {
        if (price.currency == Currency.Coins) Data.TotalCoins -= price.amount;
        else Data.TotalKeys -= price.amount;
        Save();
    }

    public static void Add(Currency c, int amount)
    {
        if (Data == null) return;
        if (c == Currency.Coins) Data.TotalCoins += amount;
        else Data.TotalKeys += amount;
        Save();
    }

    static void Save()
    {
        if (OverAllGameMannager.OA_GM != null) OverAllGameMannager.OA_GM.SaveDataToJson();
        BalanceChanged?.Invoke();
    }

    /// <summary>INAPP calls this after it granted a product.</summary>
    public static void NotifyPurchased(string productId)
    {
        BalanceChanged?.Invoke();
        StorePopup.OnPurchaseResult(true, productId);
    }

    /// <summary>Store prices arrived (or restored items were granted): refresh labels.</summary>
    public static void NotifyPricesUpdated()
    {
        BalanceChanged?.Invoke();
        StorePopup.RefreshPrices();
    }

    public static void NotifyPurchaseFailed(string productId, string reason)
    {
        StorePopup.OnPurchaseResult(false, reason);
    }
}
