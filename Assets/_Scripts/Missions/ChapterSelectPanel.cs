using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "SELECT CHAPTER" screen: one card for Practice (free drive) and one per chapter.
/// Locked chapters show a lock and their unlock condition. Built by Tools > Missions > Setup Chapter Panel.
/// </summary>
public class ChapterSelectPanel : MonoBehaviour
{
    public ChapterCard[] cards;

    void OnEnable() => Refresh();

    public void Refresh()
    {
        if (OverAllGameMannager.OA_GM == null || cards == null) return;
        var data = OverAllGameMannager.OA_GM.SaveData;
        MissionSaveUtil.EnsureCampaign(data);
        foreach (var c in cards) if (c != null) c.Refresh(data);
    }
}

