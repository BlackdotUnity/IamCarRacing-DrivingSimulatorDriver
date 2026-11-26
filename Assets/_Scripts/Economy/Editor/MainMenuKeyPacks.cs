using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Main-menu store: the "KEY PACK" card (InAppPannel/Pannel/KeyPackBtn) opens the store popup on its
/// key packs (5 / 15 / 40). Re-running refreshes the card's art, texts and click action.
/// </summary>
public static class MainMenuKeyPacks
{
    public static void Build(MainMenuMannager mm, StoreTheme theme)
    {
        if (mm.InappPannel == null) return;
        Transform cardT = null;
        foreach (var t in mm.InappPannel.GetComponentsInChildren<Transform>(true)) if (t.name == "KeyPackBtn") { cardT = t; break; }
        if (cardT == null) { Debug.LogWarning("[Economy] KeyPackBtn not found in the InAppPannel."); return; }
        var card = cardT.gameObject;
        card.SetActive(true);

        var img = card.GetComponent<Image>();
        var art = EconomyIcons.KeyPackCard();
        if (img != null && art != null) img.sprite = art;

        var btn = card.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(mm.OpenKeyStoreBtnFun));

        foreach (var t in card.GetComponentsInChildren<Text>(true))
        {
            switch (t.name)
            {
                case "TitleText": t.text = "KEY PACK"; break;
                case "BuyText": t.text = "GET KEYS"; break;
                case "DollorText": t.text = "5-40"; break;
            }
        }
        EditorUtility.SetDirty(card);

        // Main-menu "Unlock every thing" banner art (old version advertised "no ads").
        if (mm.MainMenuUnlockAllGame != null)
        {
            var bannerArt = EconomyIcons.UnlockBanner();
            foreach (var bannerImg in mm.MainMenuUnlockAllGame.GetComponentsInChildren<Image>(true))
                if (bannerArt != null && bannerImg.sprite != null && bannerImg.sprite.name.StartsWith("Unlock-evry-thng")) { bannerImg.sprite = bannerArt; EditorUtility.SetDirty(bannerImg); }
        }

        // Mega pack art: a star where the old art said "no ads".
        if (mm.UnlockAllGameBtn != null)
        {
            var megaImg = mm.UnlockAllGameBtn.GetComponent<Image>();
            var megaArt = EconomyIcons.MegaPackCard();
            if (megaImg != null && megaArt != null) { megaImg.sprite = megaArt; EditorUtility.SetDirty(megaImg); }
        }
    }
}
