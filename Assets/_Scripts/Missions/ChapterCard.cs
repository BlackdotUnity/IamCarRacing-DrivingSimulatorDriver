using UnityEngine;
using UnityEngine.UI;

/// <summary>One card on the chapter screen. chapter = -1 means Practice.</summary>
public class ChapterCard : MonoBehaviour
{
    public int chapter = -1;
    public Text titleText, nameText, infoText;
    public GameObject lockIcon;
    public Image progressFill;

    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClick);
    }

    public void Refresh(SaveData data)
    {
        if (chapter < 0)
        {
            Set(titleText, "PRACTICE");
            Set(nameText, "FREE DRIVE");
            Set(infoText, "No timer • No fail\nTry driving & flying");
            if (lockIcon) lockIcon.SetActive(false);
            if (progressFill) progressFill.transform.parent.gameObject.SetActive(false);
            if (button) button.interactable = true;
            return;
        }

        bool open = chapter < data.Chapters.Count && data.Chapters[chapter].isUnlocked;
        int done = 0;
        if (chapter < data.Chapters.Count)
        {
            // A level counts as cleared once the next one is unlocked (or the next chapter, for level 10).
            var levels = data.Chapters[chapter].levels;
            for (int i = 0; i < levels.Count; i++)
            {
                bool cleared = i + 1 < levels.Count ? levels[i + 1].isUnlocked
                    : chapter + 1 < data.Chapters.Count && data.Chapters[chapter + 1].isUnlocked;
                if (cleared) done++;
            }
        }

        Set(titleText, "CHAPTER " + (chapter + 1));
        Set(nameText, MissionChapters.Name(chapter));
        Set(infoText, open ? "LEVELS " + (chapter * 10 + 1) + "-" + (chapter * 10 + 10) + "\n" + done + "/10 DONE"
                           : "LOCKED\nFinish Chapter " + chapter);
        if (lockIcon) lockIcon.SetActive(!open);
        if (progressFill)
        {
            progressFill.transform.parent.gameObject.SetActive(open);
            progressFill.fillAmount = done / 10f;
        }
        if (button) button.interactable = open;
    }

    static void Set(Text t, string s) { if (t != null) t.text = s; }

    void OnClick()
    {
        var mm = MainMenuMannager.MM_Instance;
        if (mm == null) return;
        if (chapter < 0) mm.StartPracticeBtnFun();
        else mm.OpenChapterBtnFun(chapter);
    }
}
