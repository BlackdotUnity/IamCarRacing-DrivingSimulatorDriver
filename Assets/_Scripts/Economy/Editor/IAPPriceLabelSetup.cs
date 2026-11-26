using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > IAP > Link Shop Prices To INAPP. Safe to run again.
///  • Splash: makes sure the INAPP "Products" list is filled (IDs + prices that used to be hard-coded)
///  • MainMenu: adds IAPPriceLabel to every hard-typed "$" price text of the shop, so they follow the INAPP list
/// </summary>
public static class IAPPriceLabelSetup
{
    // Button name (MainMenu) → product.
    static readonly (string button, IAPItem item)[] Labels =
    {
        ("10000CoinsBtn", IAPItem.CoinPackSmall),
        ("15000CoinsBtn", IAPItem.CoinPackMedium),
        ("20000CoinsBtn", IAPItem.CoinPackLarge),
        ("UnlockAllGameBtn", IAPItem.UnlockAllGame),
        ("UnlockAllCarsBtn", IAPItem.UnlockAllCars),
        ("UnlockAllLevelsBtn", IAPItem.UnlockAllLevels),
        ("UnlockEveryThingBtn", IAPItem.UnlockAllGame),
    };

    [MenuItem("Tools/IAP/Link Shop Prices To INAPP")]
    public static void Run()
    {
        EditorSceneManager.SaveOpenScenes();
        string previous = EditorSceneManager.GetActiveScene().path;
        var log = new System.Text.StringBuilder("[IAP] Shop prices linked to INAPP:\n");

        // Splash: the INAPP product list.
        var splash = EditorSceneManager.OpenScene("Assets/Scenes/Splash.unity", OpenSceneMode.Single);
        var inapp = Object.FindFirstObjectByType<INAPP>(FindObjectsInactive.Include);
        if (inapp != null)
        {
            if (inapp.products == null || inapp.products.Count == 0) { inapp.products = INAPP.DefaultProducts(); log.AppendLine("  INAPP products list filled with defaults"); }
            log.AppendLine("  INAPP on '" + inapp.name + "' has " + inapp.products.Count + " products");
            EditorUtility.SetDirty(inapp);
            EditorSceneManager.MarkSceneDirty(splash);
            EditorSceneManager.SaveScene(splash);
        }
        else log.AppendLine("  WARNING: no INAPP in Splash");

        // MainMenu: price labels.
        var menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        foreach (var btn in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (var (name, item) in Labels)
            {
                if (btn.name != name) continue;
                Text price = null;
                foreach (var t in btn.GetComponentsInChildren<Text>(true))
                    if (t.name == "DollorText" || t.text.Contains("$")) { price = t; break; }
                if (price == null) { log.AppendLine("  (no price text on " + name + ")"); continue; }
                var label = price.GetComponent<IAPPriceLabel>();
                if (label == null) label = price.gameObject.AddComponent<IAPPriceLabel>();
                label.item = item;
                price.text = INAPP.GetPriceText(item); // preview in the Editor
                EditorUtility.SetDirty(price.gameObject);
                log.AppendLine("  " + name + "/" + price.name + " → " + item);
            }
        }
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);

        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log(log.ToString());
    }
}
