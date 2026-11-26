using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put on a price Text (e.g. the main-menu shop cards). Shows the price of `item` from the INAPP Inspector
/// "Products" list, or the Meta Store's localized price once it has loaded. Updates by itself.
/// </summary>
[RequireComponent(typeof(Text))]
public class IAPPriceLabel : MonoBehaviour
{
    public IAPItem item;
    [Tooltip("Optional text before / after the price, e.g. \"ONLY \".")]
    public string prefix, suffix;

    Text label;

    void OnEnable()
    {
        label = GetComponent<Text>();
        GameEconomy.BalanceChanged += Refresh; // also raised when store prices arrive
        Refresh();
    }

    void OnDisable() => GameEconomy.BalanceChanged -= Refresh;

    public void Refresh()
    {
        if (label == null) label = GetComponent<Text>();
        if (label != null) label.text = prefix + INAPP.GetPriceText(item) + suffix;
    }
}
