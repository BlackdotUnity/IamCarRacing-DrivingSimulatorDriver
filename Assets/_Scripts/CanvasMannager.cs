using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum FailReason
{
    Collision,
    FuelEmpty,
    TimeUp,
    AiTooFar, 
    AiTooClose,
    RedZone
}
public class CanvasMannager : MonoBehaviour
{
   
    public static CanvasMannager Instance;
    public GameObject GamePlayPannel, PausePannel, FailPannel, CompletePannel,InfoPannel,FuelRewardPannel, ObjectivePannel,loadingPannel;
    public string [] LevelObjText;
    public Text CurrentLevel_ObjText;
    public Text GamePlay_ObjText;
    public Text CurrentLevel_Text;
    public GameObject GarageDoorAnimation;
    public GameObject TimerText;
    public GameObject DistanceText;
    public Image FuelImage;
    public Text Fuel_Text;
    public Text Distance_Text;
    public Text Fail_ObjText;
    public string[] LevelCompleteObjText;
    public Text Complete_ObjText;
    public GameObject NextChapterText;
    public GameObject MiniMap;
    public GameObject Fuel;
    public GameObject TimmerMannager;
    ///  <gameplay>  /// 

    public GameObject ExteraFuel_GameplayBtn, ExteraFueWatchVideoAds;


    public GameObject GamePlayMidText;
    public Text MidText;

    //   Fuel    //

    bool fuelFailed = false;

    // // Fail  // //

    public GameObject SkipLevelBtn, KeyLifeBtn,TimeBtn,FuelBtn, Fuel1000coinsBtn;

    public bool shouldBlink;
    // // Complete // //

    public GameObject NextBtn;

     // OverAllGameMannager //
    SaveData data;
    int chapter;
    int level;
    int car;

    // Start is called before the first frame update
    void Start()
    {
        data = OverAllGameMannager.OA_GM.SaveData;
        chapter = data.CurrentChapter;
        level = data.CurrentLevel;
        car= data.CurrentCar;
        Time.timeScale = 1f;
        Instance = this;
        // Generated levels (Chapter 2+) take their texts from the MissionDatabase.
        CurrentLevel_ObjText.text = MissionRunner.ObjectiveText(chapter, level, LevelObjText);
        CurrentLevel_Text.text = "Level Number "+ (chapter * 10 + level + 1).ToString();
        OverAllGameMannager.OA_GM.GameStart = false;
        if (PracticeMode.Active) StartCoroutine(BeginPractice());
    }

    /// <summary>Practice: skip the objective panel and drive freely (no timer, no fail).</summary>
    IEnumerator BeginPractice()
    {
        yield return null; // after GameMannager.Start has placed and frozen the car
        CurrentLevel_Text.text = "PRACTICE";
        InfoPannel.SetActive(false);
        GamePlayPannel.SetActive(true);
        if (TimerText) TimerText.SetActive(false);
        var info = GamePlayPannel.transform.Find("Controller Buttons/InfoIBtn");
        if (info) info.gameObject.SetActive(false);
        OverAllGameMannager.OA_GM.GameStart = true;
        OverAllGameMannager.OA_GM.Timmer = false;
        GameMannager.Instance.UnStopTheCar();
        GameMannager.Instance.SetCameraPos();
        GamePlayMidText.SetActive(true);
        MidText.text = "PRACTICE MODE  •  Free drive, no timer";
    }

    // Update is called once per frame
    void Update()
    {
        FuelTank();
    }


    /// <summary>No ads any more: paid buttons are always usable (they open the store when the player is short).</summary>
    public void RefreshRewardButtons()
    {
        foreach (var b in new[] { SkipLevelBtn, TimeBtn, FuelBtn, KeyLifeBtn, Fuel1000coinsBtn })
        {
            if (b == null) continue;
            var btn = b.GetComponent<Button>();
            if (btn != null) btn.interactable = true;
            var cost = b.GetComponent<CostButton>();
            if (cost != null) cost.Refresh();
        }
    }

    //// Objective //// 

    public void ObjectiveLevelInfo()
    {
        GamePlay_ObjText.text = MissionRunner.ObjectiveText(chapter, level, LevelObjText);
        ObjectivePannel.SetActive(true);
        GameMannager.Instance.UpdateCarFuel();
        GamePlayPannel.SetActive(false);
        GameMannager.Instance.StopTheCar();
        OverAllGameMannager.OA_GM.GameStart = false;
        OverAllGameMannager.OA_GM.Timmer = false;
        if (IsAlarmLevel())
        {
            MyMusicMannager.OA_MM.AlaramSoundStopFun();
        }
    }
    public void HideObjectiveLevelInfo()
    {
        ObjectivePannel.SetActive(false);
        GamePlayPannel.SetActive(true);
        OverAllGameMannager.OA_GM.GameStart = true;
        OverAllGameMannager.OA_GM.Timmer = true;
        GameMannager.Instance.UnStopTheCar();
        if (IsAlarmLevel())
        {
            MyMusicMannager.OA_MM.AlaramSoundFun();
        }
    }

    public void FuelTank()
    {

        if (!OverAllGameMannager.OA_GM.GameStart)
            return;

        var vehicle = RCC_SceneManager.Instance.activePlayerVehicle;
        if (vehicle == null)
            return;

        float fuel = vehicle.fuelTank;

        // ================= UNLIMITED FUEL (also in practice) =================
        if (data.HasUnlimitedFuel || PracticeMode.Active)
        {
            if (fuel < 100f)
                vehicle.fuelTank = 100f;

            FuelImage.fillAmount = 1f;
            Fuel_Text.text = "∞";

            shouldBlink = false;
            ExteraFuel_GameplayBtn.SetActive(false);
            fuelFailed = false;

            return; // ✅ correct place
        }
        // ==================================================

        // ---------------- NORMAL FUEL ----------------
        FuelImage.fillAmount = fuel / 100f;
        Fuel_Text.text = Mathf.CeilToInt(fuel) + "%";

        bool lowFuel = fuel > 0f && fuel <= 10f;

        if (shouldBlink != lowFuel)
        {
            shouldBlink = lowFuel;
            ExteraFuel_GameplayBtn.GetComponent<Animator>().enabled=lowFuel;
        }

        if (fuel <= 0f && !fuelFailed)
        {
            fuelFailed = true;
            GameFailFun(FailReason.FuelEmpty);
        }
    }

   
    public void GameFailFun(FailReason reason)
    {
        if (levelFinished) return; // already won: a late crash / timer tick must not show the fail panel
        // ------------------ STOP GAME ------------------
        OverAllGameMannager.OA_GM.Timmer = false;
        OverAllGameMannager.OA_GM.GameStart = false;

        GameMannager.Instance.UpdateCarFuel();
        GameMannager.Instance.StopTheCar();
        MyMusicMannager.OA_MM.AlaramSoundStopFun();
        // ------------------ RESET BUTTONS ------------------
        TimeBtn.SetActive(false);
        FuelBtn.SetActive(false);
        KeyLifeBtn.SetActive(false);
        SkipLevelBtn.SetActive(false);
        Fuel1000coinsBtn.SetActive(false);

        // ------------------ FAIL TEXT & LOGIC ------------------
        bool wantsKeyButton = false;

        switch (reason)
        {
            case FailReason.TimeUp:
                Fail_ObjText.text = "TIME'S UP! You were too slow!";
                TimeBtn.SetActive(true);
                break;

            case FailReason.FuelEmpty:
                Fail_ObjText.text = "OUT OF FUEL! Tank is empty.";
                
                FuelBtn.SetActive(true);
                Fuel1000coinsBtn.SetActive(true);
                break;

            case FailReason.Collision:
                Fail_ObjText.text = "CAR CRASHED! You hit the car.";
                wantsKeyButton = true;
                break;

            case FailReason.RedZone:
                Fail_ObjText.text = "RED ZONE! Restricted area entry.";
               // wantsKeyButton = true;
                break;

            case FailReason.AiTooFar:
                Fail_ObjText.text = "TARGET LOST! You are too far behind.";
                break;

            case FailReason.AiTooClose:
                Fail_ObjText.text = "TOO CLOSE! Maintain your distance.";
                break;
        }

        // ------------------ KEY / SKIP LOGIC ------------------
        // Both are paid with keys now (no ads). Too few keys → the store opens on tap.
        if (wantsKeyButton)
        {
            KeyLifeBtn.SetActive(true);                       // continue: 1 key
            SkipLevelBtn.SetActive(data.CurrentLevel < 9);    // skip: 2 keys (not on a chapter's last level)
        }
        RefreshRewardButtons();

        // ------------------ FINAL UI ------------------
        FailPannel.SetActive(true);
        GamePlayPannel.SetActive(false);
    }
    
    bool levelFinished;

    public void GameCompleteFun()
    {
        // Several triggers / mission steps can report the finish in the same moment: reward only once.
        if (levelFinished) return;
        levelFinished = true;
        MyMusicMannager.OA_MM.AlaramSoundStopFun();
        data.TotalCoins += data.Chapters[chapter].levels[level].LevelRewards;
        GameMannager.Instance.UpdateCarFuel();
        UnLockNextLevelSystem();
        if(data.CurrentLevel>=9)
        {
            // Last level of a chapter: "Next" opens the next chapter, except after the final chapter.
            bool hasNextChapter = chapter + 1 < data.Chapters.Count && chapter + 1 < MissionChapters.ChapterCount;
            NextChapterText.SetActive(!hasNextChapter);
            NextBtn.SetActive(hasNextChapter);
        }
        else
        {
            NextChapterText.SetActive(false);
            NextBtn.SetActive(true);
        }
        // Save now: quitting from the win screen must keep the unlocked level and the coins.
        OverAllGameMannager.OA_GM.SaveDataToJson();
        GameEconomy.Add(Currency.Coins, 0); // refresh coin labels
        Complete_ObjText.text = MissionRunner.CompleteText(chapter, level, LevelCompleteObjText);
        OverAllGameMannager.OA_GM.GameStart = false;
        OverAllGameMannager.OA_GM.Timmer = false;
        CompletePannel.SetActive(true);
        GamePlayPannel.SetActive(false);
        GameMannager.Instance.StopTheCar();

    }
    public void GamePauseFun()
    {
        GameMannager.Instance.UpdateCarFuel();
        PausePannel.SetActive(true);
        GamePlayPannel.SetActive(false);
        GameMannager.Instance.StopTheCar();
        OverAllGameMannager.OA_GM.GameStart = false;
        OverAllGameMannager.OA_GM.Timmer = false;
        if (IsAlarmLevel())
        {
            MyMusicMannager.OA_MM.AlaramSoundStopFun();
        }
            
       // Time.timeScale = 0.0000001f;
    }
    public void GameResumeFun()
    {
        //Time.timeScale = 1f;
        PausePannel.SetActive(false);
        GamePlayPannel.SetActive(true);
        OverAllGameMannager.OA_GM.GameStart = true;
        OverAllGameMannager.OA_GM.Timmer = true;
        GameMannager.Instance.UnStopTheCar();
        if (IsAlarmLevel())
        {
            MyMusicMannager.OA_MM.AlaramSoundFun();
        }

    }
    public void RestartBtnFun()
    {
       
        SceneManager.LoadScene("GamePlay");
    }

    public void MainMenuBtnFun()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void UnLockNextLevelSystem()
    {
        if (chapter < 0 || chapter >= data.Chapters.Count)
        {
            Debug.LogWarning("Invalid Chapter Index");
            return;
        }

        var currentChapter = data.Chapters[chapter];

        // LAST LEVEL OF CHAPTER
        if (level >= currentChapter.levels.Count - 1)
        {
            // Unlock next chapter
            if (chapter + 1 < data.Chapters.Count)
            {
                data.Chapters[chapter + 1].isUnlocked = true;
                if (data.Chapters[chapter + 1].levels.Count > 0)
                    data.Chapters[chapter + 1].levels[0].isUnlocked = true;
            }
            else
            {
                NextBtn.SetActive(false);
            }
        }
        else
        {
            // Unlock next level
            currentChapter.levels[level + 1].isUnlocked = true;
        }
    }
    public void NextBtnFun()
    {
      
        if (level <= 8)
        {

            data.CurrentLevel += 1;
        }
        else if (chapter + 1 < data.Chapters.Count)
        {
            // Continue into the next chapter.
            data.CurrentChapter = chapter + 1;
            data.CurrentLevel = 0;
        }

        SceneManager.LoadScene("GamePlay");
    }

    /// <summary>The alarm sound belongs to the hand-built "Escape Garage 2.0" level only.</summary>
    bool IsAlarmLevel()
    {
        return chapter == 0 && data.CurrentLevel == 9;
    }
    public void SkipLevelBtnFun()
    {
        // Was a rewarded ad: now costs keys (store opens if the player has too few).
        GameEconomy.TrySpend(GameEconomy.SkipLevel, SkipLevelAfterRewardAdsShowFun, "skipping the level");
    }
    public void SkipLevelAfterRewardAdsShowFun()
    {
        UnLockNextLevelSystem();
 
        if (level <= 8)
        {

            data.CurrentLevel += 1;
        }
        else
        {

        }
       // SkipLevelBtn.GetComponent<Button>().interactable = true;
      //  SkipLevelBtn.GetComponent<Animator>().enabled = true;
        SceneManager.LoadScene("GamePlay");
    }
    public void KeyLifeBtnFun()
    {
        GameEconomy.TrySpend(GameEconomy.ContinueAfterCrash, ContinueAfterCrash, "continuing");
    }

    void ContinueAfterCrash()
    {
        FailPannel.SetActive(false);
        GamePlayPannel.SetActive(true);
        GameMannager.Instance.UnStopTheCar();
        OverAllGameMannager.OA_GM.GameStart = true;
        OverAllGameMannager.OA_GM.Timmer = true;
        if (IsAlarmLevel())
        {
            MyMusicMannager.OA_MM.AlaramSoundFun();
        }
    }

    public void ExteraTimeBtnFun()
    {
        GameEconomy.TrySpend(GameEconomy.ExtraTime, ExteraTimeAfterAdsShowFun, "extra time");
    }
    public void ExteraTimeAfterAdsShowFun()
    {
        Timer.Instance.TimeAdjustmentFun(0f, GameEconomy.ExtraSeconds);
        FailPannel.SetActive(false);
        GamePlayPannel.SetActive(true);
        GameMannager.Instance.UnStopTheCar();
        OverAllGameMannager.OA_GM.Timmer = true;
        OverAllGameMannager.OA_GM.GameStart = true;
      //  TimeBtn.GetComponent<Button>().interactable = true;
       // TimeBtn.GetComponent<Animator>().enabled = true;
        if (IsAlarmLevel())
        {
            MyMusicMannager.OA_MM.AlaramSoundFun();
        }
    }
    public void ExteraFuelBtnFun()
    {
        
        GameEconomy.TrySpend(GameEconomy.SmallRefuel, ExteraFuelAfterAdsShowFun, "refuelling");
    }
    public void ExteraFuelAfterAdsShowFun()
    {
        data.Cars[data.CurrentCar].CarFuel = Mathf.Min(data.Cars[data.CurrentCar].CarFuel + GameEconomy.SmallRefuelAmount, 100f);
        GameMannager.Instance.CarFuelTank();
        FailPannel.SetActive(false);
        GamePlayPannel.SetActive(true);
        GameMannager.Instance.UnStopTheCar();
        OverAllGameMannager.OA_GM.GameStart = true;
        OverAllGameMannager.OA_GM.Timmer = true;
       // FuelBtn.GetComponent<Button>().interactable = true;
       // FuelBtn.GetComponent<Animator>().enabled = true;
        fuelFailed = false;
        if (IsAlarmLevel())
        {
            MyMusicMannager.OA_MM.AlaramSoundFun();
        }
    }
    public void ExteraFuelAfter1000coinsShowFun()
    {
        GameEconomy.TrySpend(GameEconomy.FullRefuel, FullRefuelFromFailPanel, "a full tank");
    }

    void FullRefuelFromFailPanel()
    {
        {
            data.Cars[data.CurrentCar].CarFuel = Mathf.Clamp(data.Cars[data.CurrentCar].CarFuel + 100, 0, 100);

            GameMannager.Instance.CarFuelTank();
            FailPannel.SetActive(false);
            GamePlayPannel.SetActive(true);
            GameMannager.Instance.UnStopTheCar();
            OverAllGameMannager.OA_GM.GameStart = true;
            OverAllGameMannager.OA_GM.Timmer = true;
         //   Fuel1000coinsBtn.GetComponent<Button>().interactable = true;
         //   Fuel1000coinsBtn.GetComponent<Animator>().enabled = true;
            fuelFailed = false;
            if (IsAlarmLevel())
            {
                MyMusicMannager.OA_MM.AlaramSoundFun();
            }
        }
    }

    public void ExterFuelGamePlayBtnFun()
    {
        GameMannager.Instance.UpdateCarFuel();
        FuelRewardPannel.SetActive(true);
        GamePlayPannel.SetActive(false);
        GameMannager.Instance.StopTheCar();
        OverAllGameMannager.OA_GM.GameStart = false;
        OverAllGameMannager.OA_GM.Timmer = false;
        if (IsAlarmLevel())
        {
            MyMusicMannager.OA_MM.AlaramSoundStopFun();
        }
    }
    public void BackFuelBtnFun()
    {
        
        FuelRewardPannel.SetActive(false);
        GamePlayPannel.SetActive(true);
        OverAllGameMannager.OA_GM.GameStart = true;
        OverAllGameMannager.OA_GM.Timmer = true; // was false: closing the fuel panel froze the level timer for good
        GameMannager.Instance.UnStopTheCar();
        if (IsAlarmLevel())
        {
            MyMusicMannager.OA_MM.AlaramSoundFun(); // the alarm level keeps its siren after the panel
        }
    }
    /// <summary>In-game fuel panel "+30%" button (was "Watch Video").</summary>
    public void FuelGamePLayWatchVideoAdsFun()
    {
        GameEconomy.TrySpend(GameEconomy.SmallRefuel, ExteraFuelGamePlayAfterAdsShowFun, "refuelling");
    }
    public void ExteraFuelGamePlayAfterAdsShowFun()
    {
        var car = data.Cars[data.CurrentCar];

        car.CarFuel = Mathf.Min(car.CarFuel + GameEconomy.SmallRefuelAmount, 100f);
        GameMannager.Instance.CarFuelTank();
        FuelRewardPannel.SetActive(false);
        GamePlayPannel.SetActive(true);
        GameMannager.Instance.UnStopTheCar();
        OverAllGameMannager.OA_GM.GameStart = true;
        OverAllGameMannager.OA_GM.Timmer = true;
        //FuelBtn.GetComponent<Button>().interactable = true;
        if (IsAlarmLevel())
        {
            MyMusicMannager.OA_MM.AlaramSoundFun();
        }
    }
    public void ExteraFuelGamePlayAfter1000coinsDetuctionFun()
    {
        GameEconomy.TrySpend(GameEconomy.FullRefuel, FullRefuelFromFuelPanel, "a full tank");
    }

    void FullRefuelFromFuelPanel()
    {
        {
            var car = data.Cars[data.CurrentCar];

            car.CarFuel = Mathf.Min(car.CarFuel + 100f, 100f);
            GameMannager.Instance.CarFuelTank();
            FuelRewardPannel.SetActive(false);
            GamePlayPannel.SetActive(true);
            GameMannager.Instance.UnStopTheCar();
            OverAllGameMannager.OA_GM.GameStart = true;
            OverAllGameMannager.OA_GM.Timmer = true;
           // FuelBtn.GetComponent<Button>().interactable = true;
            if (IsAlarmLevel())
            {
                MyMusicMannager.OA_MM.AlaramSoundFun();
            }
        }
    }
    public void OkBtnFun()
    {
        OverAllGameMannager.OA_GM.GameStart = true;
        OverAllGameMannager.OA_GM.Timmer = true;

        InfoPannel.SetActive(false);
        GamePlayPannel.SetActive(true);
        GameMannager.Instance.UnStopTheCar();
        GameMannager.Instance.SetCameraPos();
        GameMannager.Instance.LevelActivated();

        // Chapter 2+ levels are built and run by MissionRunner.
        if (MissionDatabase.IsGenerated(chapter, level))
        {
            if (MissionRunner.Instance != null) MissionRunner.Instance.Begin();
            return;
        }

        switch (level)
        {
            case 0:
                break;

            case 1:
                GarageDoorAnimation.GetComponent<Animator>().SetBool("OpenDoor", true);
                break;
            case 2:
                GameMannager.Instance.Arrow[car].SetActive(true);
                MiniMap.SetActive(true);
                DistanceText.SetActive(true);
                break;
            case 3:
                GameMannager.Instance.Arrow[car].SetActive(true);
                MiniMap.SetActive(true);
                DistanceText.SetActive(true);
                break;
            case 4:
                GameMannager.Instance.Arrow[car].SetActive(true);
                MiniMap.SetActive(true);
                DistanceText.SetActive(true);
                break;
            case 5:
                GameMannager.Instance.Arrow[car].SetActive(true);
                MiniMap.SetActive(true);
                DistanceText.SetActive(true);
                break;
            case 6:
                GameMannager.Instance.Arrow[car].SetActive(true);
                MiniMap.SetActive(true);
                DistanceText.SetActive(true);
                break;
            case 7:
                GameMannager.Instance.Arrow[car].SetActive(true);
                MiniMap.SetActive(true);
                DistanceText.SetActive(true);
                break;
            case 8:
                break;
            case 9:
                GameMannager.Instance.Arrow[car].SetActive(true);
                OverAllGameMannager.OA_GM.Level10GateCloseOpen = true;
                MyMusicMannager.OA_MM.AlaramSoundFun();
                MiniMap.SetActive(true);
                DistanceText.SetActive(true);
                break;

            case 10:
                break;
            default:
                break;
        }
    }
}
