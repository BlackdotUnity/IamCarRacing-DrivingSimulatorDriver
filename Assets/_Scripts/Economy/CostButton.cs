using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows "SKIP  [key] 2" style labels on buttons that used to be rewarded ads.
/// The price always comes from GameEconomy, so tuning a price updates the UI.
/// </summary>
public class CostButton : MonoBehaviour
{
    public enum Action { SkipLevel, ExtraTime, SmallRefuel, FullRefuel, ContinueAfterCrash }

    public Action action;
    public string title = "";
    public Text titleText;
    public Text priceText;
    public Image icon;
    public Sprite coinSprite, keySprite;

    public Price CurrentPrice
    {
        get
        {
            switch (action)
            {
                case Action.SkipLevel: return GameEconomy.SkipLevel;
                case Action.ExtraTime: return GameEconomy.ExtraTime;
                case Action.SmallRefuel: return GameEconomy.SmallRefuel;
                case Action.FullRefuel: return GameEconomy.FullRefuel;
                default: return GameEconomy.ContinueAfterCrash;
            }
        }
    }

    void OnEnable()
    {
        GameEconomy.BalanceChanged += Refresh;
        Refresh();
    }

    void OnDisable() => GameEconomy.BalanceChanged -= Refresh;

    public void Refresh()
    {
        var p = CurrentPrice;
        if (titleText) titleText.text = title;
        if (priceText)
        {
            priceText.text = p.currency == Currency.Coins ? p.amount.ToString("N0") : "x" + p.amount;
            // Red price = not enough, tapping opens the store.
            priceText.color = GameEconomy.CanAfford(p) ? new Color(0.06f, 0.06f, 0.06f) : new Color(0.75f, 0.05f, 0.05f);
        }
        if (icon) icon.sprite = p.currency == Currency.Coins ? coinSprite : keySprite;
    }
}
