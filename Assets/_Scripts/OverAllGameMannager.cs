using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class LevelData
{
    public bool isUnlocked;
          // 0–3
    public int LevelRewards;
    public float minutes, seconds;
    
}

[System.Serializable]
public class ChapterData
{
    public bool isUnlocked;
    public List<LevelData> levels = new List<LevelData>();
}

[System.Serializable]
public class CarData
{
    public bool isUnlocked;


    public string CarName;
    public int CarPrice;
    public float CarFuel;

}

[Serializable]
public class SaveData
{

    public List<ChapterData> Chapters = new List<ChapterData>();
    public List<CarData> Cars = new List<CarData>();


    public bool UnlockAllGame;
    public bool UnlockAllCars;
    public bool UnlockAllLevel;
    public bool HasUnlimitedFuel;
    public bool ShowRewardedadFirstTime;
    public bool ShowInterialad_FirstTime;
    public int CurrentChapter;
    public int CurrentLevel;
    public int CurrentCar;
    public int TotalCoins;
    public int TotalKeys;
    public int CurrentFuel;

    public string NextSceneName;

    // Daily Reward
    public string LastDailyRewardTime;
    public int DailyRewardDay;


    ////// Audio ////

    public float MusicVolume;
    public float SoundVolume;

}

public class OverAllGameMannager : MonoBehaviour
{
    public static OverAllGameMannager OA_GM;

    public SaveData SaveData;
    /////////////JSON ITEMS///////////////////
    [Space(10)]
    [SerializeField] string path;
    string potion;

    public  bool GameStart, Timmer,Level10GateCloseOpen,TimmerFail;
    public  float miliSec, seconds, minutes;


    void Awake()
    {
        if (OA_GM == null)
        {
            OA_GM = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        path = Path.Combine(Application.persistentDataPath + "/SaveData.json");

        // Car list as set up in the Inspector (all garage cars), kept to grow older saves that have fewer cars.
        var defaultCars = new List<CarData>();
        foreach (var c in SaveData.Cars)
            defaultCars.Add(new CarData { isUnlocked = c.isUnlocked, CarName = c.CarName, CarPrice = c.CarPrice, CarFuel = c.CarFuel });
        // Chapter 1 (hand-built levels) time limits as set in the Inspector: old saves get re-tuned times too.
        var defaultTimes = new List<Vector2>();
        if (SaveData.Chapters.Count > 0)
            foreach (var lv in SaveData.Chapters[0].levels) defaultTimes.Add(new Vector2(lv.minutes, lv.seconds));

        if (File.Exists(path) && PlayerPrefs.HasKey("SetOldData1"))
        {
            LoadDataFromJson();
        }
        else
        {
            SaveDataToJson();
            PlayerPrefs.SetInt("SetOldData1", 1);
        }

        // Fit old saves to the 5 chapters x 10 levels campaign.
        MissionSaveUtil.EnsureCampaign(SaveData);

        // Grow old saves to the full garage (new cars added later), keeping progress on the existing ones.
        bool carsChanged = false;
        for (int i = 0; i < defaultCars.Count; i++)
        {
            if (i < SaveData.Cars.Count)
            {
                // Names / prices follow the Inspector so renamed or re-priced cars update in old saves too.
                if (SaveData.Cars[i].CarName != defaultCars[i].CarName || SaveData.Cars[i].CarPrice != defaultCars[i].CarPrice)
                {
                    SaveData.Cars[i].CarName = defaultCars[i].CarName;
                    SaveData.Cars[i].CarPrice = defaultCars[i].CarPrice;
                    carsChanged = true;
                }
                continue;
            }
            var d = defaultCars[i];
            SaveData.Cars.Add(new CarData { isUnlocked = d.isUnlocked || SaveData.UnlockAllCars, CarName = d.CarName, CarPrice = d.CarPrice, CarFuel = d.CarFuel });
            carsChanged = true;
        }
        if (SaveData.CurrentCar < 0 || SaveData.CurrentCar >= SaveData.Cars.Count) { SaveData.CurrentCar = 0; carsChanged = true; }
        if (SaveData.Chapters.Count > 0)
            for (int i = 0; i < defaultTimes.Count && i < SaveData.Chapters[0].levels.Count; i++)
            {
                var lv = SaveData.Chapters[0].levels[i];
                if (defaultTimes[i].x + defaultTimes[i].y <= 0f) continue;
                if (lv.minutes != defaultTimes[i].x || lv.seconds != defaultTimes[i].y) { lv.minutes = defaultTimes[i].x; lv.seconds = defaultTimes[i].y; carsChanged = true; }
            }
        // "Unlock All Cars" / "Unlock Everything" owners also get cars added in later updates.
        if (SaveData.UnlockAllCars || SaveData.UnlockAllGame)
            foreach (var c in SaveData.Cars) if (!c.isUnlocked) { c.isUnlocked = true; carsChanged = true; }
        if (SaveData.Cars.Count > 0 && !SaveData.Cars[0].isUnlocked) { SaveData.Cars[0].isUnlocked = true; carsChanged = true; }
        if (carsChanged) SaveDataToJson();
    }
    #region JSON
    public void LoadDataFromJson()
    {
        try
        {
            if (File.Exists(path))
            {
                string loadedJsonDataString = File.ReadAllText(path);
                JsonUtility.FromJsonOverwrite(loadedJsonDataString, SaveData);
            }
        }
        catch
        {

        }
    }
    public void SaveDataToJson()
    {
        try
        {
            potion = JsonUtility.ToJson(SaveData, true);
            File.WriteAllText(path, potion);
        }
        catch
        {

        }
    }

    #endregion

    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            SaveDataToJson();
        }
    }

    // Quest: taking the headset off / Oculus button pauses the app; closing it may not send a focus change first.
    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveDataToJson();
    }

    private void OnApplicationQuit()
    {
        SaveDataToJson();
    }

}
