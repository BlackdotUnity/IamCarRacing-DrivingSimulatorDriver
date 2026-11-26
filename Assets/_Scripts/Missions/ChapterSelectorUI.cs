using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "&lt; CHAPTER 3 • HACKER ALLIANCE &gt;" bar in the level-select panel.
/// Switches MainMenuMannager to another chapter; locked chapters show their unlock condition.
/// </summary>
public class ChapterSelectorUI : MonoBehaviour
{
    public Button prevButton, nextButton;
    public Text titleText;
    public Text infoText;

    void Awake()
    {
        if (prevButton) prevButton.onClick.AddListener(() => Step(-1));
        if (nextButton) nextButton.onClick.AddListener(() => Step(1));
    }

    void OnEnable() => Refresh();

    void Step(int dir)
    {
        if (OverAllGameMannager.OA_GM == null || MainMenuMannager.MM_Instance == null) return;
        var data = OverAllGameMannager.OA_GM.SaveData;
        int c = Mathf.Clamp(data.CurrentChapter + dir, 0, MissionChapters.ChapterCount - 1);
        MainMenuMannager.MM_Instance.SetChapter(c);
        Refresh();
    }

    public void Refresh()
    {
        if (OverAllGameMannager.OA_GM == null) return;
        var data = OverAllGameMannager.OA_GM.SaveData;
        MissionSaveUtil.EnsureCampaign(data);
        int c = Mathf.Clamp(data.CurrentChapter, 0, data.Chapters.Count - 1);
        bool unlocked = data.Chapters[c].isUnlocked;

        if (titleText) titleText.text = "CHAPTER " + (c + 1) + "  •  " + MissionChapters.Name(c);
        if (infoText)
        {
            int done = 0;
            foreach (var l in data.Chapters[c].levels) if (l.isUnlocked) done++;
            infoText.text = unlocked
                ? "LEVELS " + (c * 10 + 1) + "-" + (c * 10 + 10) + "   •   " + done + "/10 OPEN"
                : "LOCKED  •  FINISH CHAPTER " + c + " TO UNLOCK";
            infoText.color = unlocked ? new Color(0.75f, 0.95f, 1f) : new Color(1f, 0.55f, 0.45f);
        }
        if (prevButton) prevButton.interactable = c > 0;
        if (nextButton) nextButton.interactable = c < MissionChapters.ChapterCount - 1;
    }
}
