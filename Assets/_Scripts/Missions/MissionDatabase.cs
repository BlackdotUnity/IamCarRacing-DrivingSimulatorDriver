using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspector-editable list of all 100 missions. Lives at Resources/Missions/MissionDatabase
/// so the menu and gameplay scenes can read it without scene references.
/// </summary>
[CreateAssetMenu(menuName = "Missions/Mission Database", fileName = "MissionDatabase")]
public class MissionDatabase : ScriptableObject
{
    public const string ResourcePath = "Missions/MissionDatabase";

    public List<MissionDef> missions = new List<MissionDef>();

    static MissionDatabase cached;
    static List<MissionDef> fallback;

    public static MissionDatabase Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<MissionDatabase>(ResourcePath);
            return cached;
        }
    }

    static List<MissionDef> All
    {
        get
        {
            var db = Instance;
            if (db != null && db.missions != null && db.missions.Count > 0) return db.missions;
            if (fallback == null) fallback = MissionCatalog.Build();
            return fallback;
        }
    }

    public static int Count => All.Count;

    /// <summary>Mission for chapter (0-9) and level inside the chapter (0-9). Null if out of range.</summary>
    public static MissionDef Get(int chapter, int level)
    {
        var all = All;
        int index = chapter * MissionChapters.LevelsPerChapter + level;
        if (index < 0 || index >= all.Count) return null;
        var m = all[index];
        m.chapter = chapter;
        m.level = level;
        return m;
    }

    /// <summary>True when this level is built by MissionRunner (not one of the hand-made Chapter 1 levels).</summary>
    public static bool IsGenerated(int chapter, int level)
    {
        var m = Get(chapter, level);
        return m != null && !m.handBuilt;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cached = null;
        fallback = null;
    }
}

/// <summary>Keeps the save file in sync with the 5 x 10 campaign (old saves had 1-2 or 10 chapters).</summary>
public static class MissionSaveUtil
{
    public static void EnsureCampaign(SaveData data)
    {
        if (data == null) return;
        if (data.Chapters == null) data.Chapters = new List<ChapterData>();

        for (int c = 0; c < MissionChapters.ChapterCount; c++)
        {
            bool isNewChapter = c >= data.Chapters.Count;
            if (isNewChapter) data.Chapters.Add(new ChapterData());
            var ch = data.Chapters[c];
            if (ch.levels == null) ch.levels = new List<LevelData>();

            for (int l = 0; l < MissionChapters.LevelsPerChapter; l++)
            {
                bool isNewLevel = l >= ch.levels.Count;
                if (isNewLevel) ch.levels.Add(new LevelData());
                var lv = ch.levels[l];
                var def = MissionDatabase.Get(c, l);

                // Chapter 1 keeps its tuned values. Generated chapters get their reward from the database;
                // their time limit is computed by MissionRunner when the level is built.
                if (def != null && !def.handBuilt && (isNewLevel || lv.LevelRewards <= 0 || c >= 1))
                    lv.LevelRewards = def.reward;
                if (isNewLevel && lv.minutes <= 0f && lv.seconds <= 0f) lv.minutes = 3f;
            }

            if (c == 0) ch.isUnlocked = true;
            if (data.UnlockAllLevel || data.UnlockAllGame) ch.isUnlocked = true;
            if (ch.isUnlocked) ch.levels[0].isUnlocked = true;
            if (data.UnlockAllLevel || data.UnlockAllGame)
                for (int l = 0; l < ch.levels.Count; l++) ch.levels[l].isUnlocked = true;
        }

        // Older saves (10 chapters) → drop the removed chapters; a player who was in one continues in the last chapter.
        if (data.Chapters.Count > MissionChapters.ChapterCount)
            data.Chapters.RemoveRange(MissionChapters.ChapterCount, data.Chapters.Count - MissionChapters.ChapterCount);
        if (data.CurrentChapter >= MissionChapters.ChapterCount)
        {
            data.CurrentChapter = MissionChapters.ChapterCount - 1;
            data.CurrentLevel = 0;
        }
    }

    public static void UnlockEverything(SaveData data)
    {
        EnsureCampaign(data);
        foreach (var ch in data.Chapters)
        {
            ch.isUnlocked = true;
            foreach (var lv in ch.levels) lv.isUnlocked = true;
        }
    }
}
