using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
//using RCCP;

public class MainMenuMannager : MonoBehaviour
{
    public static MainMenuMannager MM_Instance;

    public GameObject MainMenuPannel,GaragePannel,InappPannel,ModeLevelPannel, LevelPannel,ExitPannel,SettingPannel, LoadingPannel;




    public int CurrentCar = 0;
    
    public GameObject[] Cars;
    public GameObject MyCamera;

    /// Garage ///

    public GameObject BuyBtn, BikePrice, SelectBtn;
    public Text Car_PriceText,Car_NameText;
    [Tooltip("Optional: \"CAR 3 / 10\" in the garage.")]
    public Text Car_CountText;
    [Tooltip("Optional: SELECTED / OWNED / LOCKED under the car name.")]
    public Text Car_StatusText;


    /// setting ///
    public Slider MyMusicSlider, MySoundSlider;

    /// Modes ///

    /// Levels ///

    public int Level;

    public GameObject []Levels;
    public GameObject []LevelLocks;

    /// InApps ///

    public GameObject UnlockAllLevelBtn, UnlockAllGameBtn, UnlockAllCars, MainMenuUnlockAllGame;

    // OverAllGameMannager //
    SaveData data;
    int chapter;
    int level;
    int car;


    // Start is called before the first frame update
    void Start()
    {
        MM_Instance = this;
        data = OverAllGameMannager.OA_GM.SaveData;
        chapter = data.CurrentChapter;
        level = data.CurrentLevel;
        car = data.CurrentCar;
        if (data.UnlockAllCars || data.UnlockAllGame) INAPP.UnlockAllCarsInSave(data);
        Time.timeScale = 1f;
        MyMusicMannager.OA_MM.MainMenuMusicPlayFun();
        CheckInAppIsPurashedFun();
        GameEconomy.BalanceChanged += OnBalanceChanged;
    }

    void OnDestroy() => GameEconomy.BalanceChanged -= OnBalanceChanged;

    /// Coins changed (purchase, daily reward, unlock): the garage's "need X more coins" / buy state follows.
    void OnBalanceChanged()
    {
        if (GaragePannel != null && GaragePannel.activeInHierarchy) LockSystemCar();
        CheckInAppIsPurashedFun();
    }
    
    

    /// Levels ///

    [Tooltip("SELECT CHAPTER screen (Practice + 5 chapters). Built by Tools > Missions > Setup Chapter Panel.")]
    public GameObject ChapterPannel;

    public void BackLevelBtnFunction()
    {
        LevelPannel.SetActive(false);
        // Levels → back to the chapter screen (or the main menu if it isn't set up).
        if (ChapterPannel != null) ChapterPannel.SetActive(true);
        else MainMenuPannel.SetActive(true);
    }

    // ───────────── Chapter screen ─────────────

    public void OpenChapterPanel()
    {
        if (ChapterPannel == null) { LevelPannel.SetActive(true); LevelLockSystem(); return; }
        ChapterPannel.SetActive(true);
        var panel = ChapterPannel.GetComponent<ChapterSelectPanel>();
        if (panel != null) panel.Refresh();
    }

    public void BackChapterBtnFun()
    {
        if (ChapterPannel != null) ChapterPannel.SetActive(false);
        MainMenuPannel.SetActive(true);
    }

    /// <summary>Chapter card tapped: show its 10 levels.</summary>
    public void OpenChapterBtnFun(int chapterIndex)
    {
        PracticeMode.Active = false;
        SetChapter(chapterIndex);
        if (ChapterPannel != null) ChapterPannel.SetActive(false);
        LevelPannel.SetActive(true);
        var selector = LevelPannel.GetComponentInChildren<ChapterSelectorUI>(true);
        if (selector != null) selector.Refresh();
    }

    /// <summary>Practice card tapped: free drive in the city.</summary>
    public void StartPracticeBtnFun()
    {
        PracticeMode.Active = true;
        data.CurrentChapter = 0;
        data.CurrentLevel = PracticeMode.StartLevelSlot;
        if (ChapterPannel != null) ChapterPannel.SetActive(false);
        LoadingPannel.SetActive(true);
        LoadingManager.Instance.LoadingNextScene("GamePlay");
    }
    /// <summary>Called by the chapter selector arrows in the level panel.</summary>
    public void SetChapter(int newChapter)
    {
        MissionSaveUtil.EnsureCampaign(data);
        data.CurrentChapter = Mathf.Clamp(newChapter, 0, data.Chapters.Count - 1);
        LevelLockSystem();
    }

    public void LevelLockSystem()
    {
        MissionSaveUtil.EnsureCampaign(data);
        data.Chapters[0].isUnlocked = true;
        data.Chapters[0].levels[0].isUnlocked = true;
        chapter = Mathf.Clamp(data.CurrentChapter, 0, data.Chapters.Count - 1);
        bool chapterOpen = data.Chapters[chapter].isUnlocked;
        if (chapterOpen) data.Chapters[chapter].levels[0].isUnlocked = true;
        for (int i = 0; i < Levels.Length; i++)
        {
            // Level buttons show the campaign number (Chapter 3 → levels 21-30).
            var label = Levels[i].GetComponentInChildren<Text>(true);
            if (label != null) label.text = "LEVEL " + (chapter * 10 + i + 1);

            if (chapterOpen && data.Chapters[chapter].levels[i].isUnlocked == true)
            {
                Levels[i].gameObject.GetComponent<Button>().interactable = true;
                LevelLocks[i].SetActive(false);
            }
            else 
            {
                Levels[i].gameObject.GetComponent<Button>().interactable = false;
                LevelLocks[i].SetActive(true);
            }
           
        }
    }
    public void UnlockAllLevel()
    {
        // Unlock every level of every chapter.
        MissionSaveUtil.UnlockEverything(data);
        LevelLockSystem();
    }
    public void LevelBtnFun(int LevelNumber)
    {
        PracticeMode.Active = false;
        data.CurrentLevel = LevelNumber;
        LevelPannel.SetActive(false);
        LoadingPannel.SetActive(true);
        // No interstitial ads any more: go straight to the level.
        LoadGamePlayAfterAdsShowFun();
    }


    public void LoadGamePlayAfterAdsShowFun()
    {
        LoadingManager.Instance.LoadingNextScene("GamePlay");
    }
    /// <Setting> ///
    public void SettingBtnFun()
    {
        SettingPannel.SetActive(true);
        MainMenuPannel.SetActive(false);
        UpdateSetting();
    }

    public void UpdateSetting()
    {
        MySoundSlider.value = OverAllGameMannager.OA_GM.SaveData.SoundVolume;
        MyMusicSlider.value=OverAllGameMannager.OA_GM.SaveData.MusicVolume;
    }

    public void SoundSliderBtnFuntion()
    {
        AudioListener.volume = MySoundSlider.value;
        OverAllGameMannager.OA_GM.SaveData.SoundVolume = MySoundSlider.value;
    }

    public void MusicSliderBtnFunction()
    {

        MyMusicMannager.OA_MM.MyMusicAudioSource.volume = MyMusicSlider.value;
        OverAllGameMannager.OA_GM.SaveData.MusicVolume = MyMusicSlider.value;
    }


    public void SaveSettingBtnFun()
    {
        SettingPannel.SetActive(false);
        MainMenuPannel.SetActive(true);
        OverAllGameMannager.OA_GM.SaveDataToJson();
        
    }

        /// <Garage> ///
    public void garageBtnFun()
    {
        disable_all();
        GaragePannel.SetActive(true);
        MainMenuPannel.SetActive(false);
        // Open on the car the player drives (was: showed it but name / price / buttons of car #1).
        CurrentCar = Mathf.Clamp(data.CurrentCar, 0, Cars.Length - 1);
        Cars[CurrentCar].SetActive(true);
        LockSystemCar();
        OverAllGameMannager.OA_GM.SaveDataToJson();
    }
    public void BackGargeBtnFunction()
    {
        disable_all();
        MainMenuPannel.SetActive(true);
        GaragePannel.SetActive(false);
    }

    public void LeftBtnFun()
    {
        if (CurrentCar > 0)
        {

            disable_all();
            int tEMP = CurrentCar - 1;

            Cars[tEMP].SetActive(true);

            CurrentCar = tEMP;
            LockSystemCar();
        }
        else if (CurrentCar == 0)
        {
            disable_all();

            CurrentCar = Cars.Length - 1;
            Cars[CurrentCar].SetActive(true);

            LockSystemCar();
        }
        else
        {

            disable_all();

            Cars[CurrentCar - 1].SetActive(true);
            CurrentCar = CurrentCar - 1;
            LockSystemCar();
        }
    }

    public void RightBtnFun()
    {
        if (CurrentCar < Cars.Length - 1)
        {
            disable_all();

            int TEMP = CurrentCar + 1;
            Cars[TEMP].SetActive(true);
            CurrentCar = TEMP;
            LockSystemCar();
        }
        else
        {
            disable_all();

            Cars[0].SetActive(true);
            CurrentCar = 0;
            LockSystemCar();
        }
    }

    public void LockSystemCar()
    {
        data.Cars[0].isUnlocked = true;
        CurrentCar = Mathf.Clamp(CurrentCar, 0, Mathf.Min(Cars.Length, data.Cars.Count) - 1);
        var info = data.Cars[CurrentCar];
        Car_NameText.text = info.CarName.ToUpperInvariant();
        if (Car_CountText != null) Car_CountText.text = "CAR " + (CurrentCar + 1) + " / " + Cars.Length;
        if (info.isUnlocked == true)
        {

            SelectBtn.SetActive(true);
            BuyBtn.SetActive(false);
            Car_PriceText.text = "OWNED";
            BikePrice.SetActive(false);
            SetStatus(CurrentCar == data.CurrentCar ? "SELECTED" : "OWNED", CurrentCar == data.CurrentCar ? new Color(0.2f, 0.75f, 0.3f) : new Color(0.25f, 0.45f, 0.8f));
        }
        else
        {
            BikePrice.SetActive(true);
            Car_PriceText.text = info.CarPrice.ToString("N0");
            BuyBtn.SetActive(true);
            SelectBtn.SetActive(false);
            bool afford = data.TotalCoins >= info.CarPrice;
            SetStatus(afford ? "LOCKED  •  BUY FOR " + info.CarPrice.ToString("N0") + " COINS" : "LOCKED  •  NEED " + (info.CarPrice - data.TotalCoins).ToString("N0") + " MORE COINS",
                      afford ? new Color(0.95f, 0.6f, 0.05f) : new Color(0.85f, 0.2f, 0.2f));
        }
    }

    void SetStatus(string text, Color color)
    {
        if (Car_StatusText == null) return;
        Car_StatusText.text = text;
        Car_StatusText.color = color;
    }
    public void disable_all()
    {
        for (int i = 0; i < Cars.Length; i++)
        {

            Cars[i].SetActive(false);

        }
    }
    public void BuyBtnFunction()
    {
        int index = CurrentCar;
        if (data.Cars[index].isUnlocked) { LockSystemCar(); return; }
        // Enough coins: pay + save + refresh the coin labels. Too few: the coin store opens and the car is bought
        // right after a successful coin purchase (the car stays on screen instead of jumping to the big shop).
        GameEconomy.TrySpend(new Price(Currency.Coins, data.Cars[index].CarPrice), () =>
        {
            data.Cars[index].isUnlocked = true;
            OverAllGameMannager.OA_GM.SaveDataToJson();
            if (CurrentCar == index) LockSystemCar();
        }, "the " + data.Cars[index].CarName);
    }

    public void SelectCarBtnFun()
    {
        OverAllGameMannager.OA_GM.SaveData.CurrentCar = CurrentCar;

        disable_all();
        GaragePannel.SetActive(false);
        MissionSaveUtil.EnsureCampaign(data);
        int ch = OverAllGameMannager.OA_GM.SaveData.CurrentChapter;
        if (ch < 0 || ch >= data.Chapters.Count || !data.Chapters[ch].isUnlocked) ch = 0;
        OverAllGameMannager.OA_GM.SaveData.CurrentChapter = ch;
        if (ChapterPannel != null)
        {
            // Car chosen → pick Practice or a chapter first.
            OpenChapterPanel();
        }
        else
        {
            LevelPannel.SetActive(true);
            LevelLockSystem();
            var selector = LevelPannel.GetComponentInChildren<ChapterSelectorUI>(true);
            if (selector != null) selector.Refresh();
        }
        //  AdsManager.Instance.HideBanner();
        // SceneManager.LoadScene("HumanGarage");
        // AnalyticsManager.Instance.CustomEvent("selectbikebtn");
        // AnalyticsManager.Instance.LogParametEvent("selectbikenumber", OverAllGameMannager.OA_GM.SaveData.CurrentBike);
    }
    public void UnlockAllCarsFunsSystem()
    {
        INAPP.UnlockAllCarsInSave(data);
        LockSystemCar();
    }

    /// <Inapp> ///
    public void InappBtnFun()
    {
        disable_all();
        InappPannel.SetActive(true);
        CheckInAppIsPurashedFun();
    }

    public void Buy10000CoinsBtnFun()
    {
        INAPP.Instance.Buy10000CoinsFunction();
    }

    public void Buy15000CoinsBtnFun()
    {
        INAPP.Instance.Buy15000CoinsFunction();
    }
    public void Buy20000CoinsBtnFun()
    {
        INAPP.Instance.Buy20000CoinsFunction();
    }

    // Key packs (keys replace rewarded ads for skip level / continue).
    public void Buy5KeysBtnFun() { INAPP.Instance.Buy5KeysFunction(); }
    public void Buy15KeysBtnFun() { INAPP.Instance.Buy15KeysFunction(); }
    public void Buy40KeysBtnFun() { INAPP.Instance.Buy40KeysFunction(); }

    /// <summary>"KEY PACK" card in the store.</summary>
    public void OpenKeyStoreBtnFun()
    {
        StorePopup.Open(Currency.Keys, "Keys let you skip a level or continue after a crash.");
    }

    public void UnlockAllCarBtnFun()
    {
        INAPP.Instance.BuyUnAllCarsBtnFunction();
    }
    public void UnlockAllLevelsBtnFun()
    {
        INAPP.Instance.BuyUnAllLevelBtnFunction();
    }
   
    public void UnAllGameBtnFun()
    {
        INAPP.Instance.BuyUnAllGameBtnFunction();
    }
    public void BackInappBtnFun()
    {
        InappPannel.SetActive(false);
        if (GaragePannel.activeInHierarchy == true)
        {
            Cars[CurrentCar].SetActive(true); 
        }
        CheckInAppIsPurashedFun();
    }


    public void CheckInAppIsPurashedFun()
    {
        if (OverAllGameMannager.OA_GM.SaveData.UnlockAllGame == true)
        {
            if (UnlockAllGameBtn.GetComponent<Button>().interactable)
            {
                UnlockAllGameBtn.GetComponent<Button>().interactable = false;
            }
            if (MainMenuUnlockAllGame.activeInHierarchy)
            {
                MainMenuUnlockAllGame.SetActive(false);
            }
        }

        if (OverAllGameMannager.OA_GM.SaveData.UnlockAllCars == true)
        {
            if (UnlockAllCars.GetComponent<Button>().interactable)
            {
                UnlockAllCars.GetComponent<Button>().interactable = false;


            }

        }
       
        if (OverAllGameMannager.OA_GM.SaveData.UnlockAllLevel == true)
        {
            if (UnlockAllLevelBtn.GetComponent<Button>().interactable)
            {
                UnlockAllLevelBtn.GetComponent<Button>().interactable = false;



            }

        }
    }

    /// <Exit> ///

    public void ExitBtnFun()
    {
        ExitAfterAdShowFun();
    }

    public void ExitAfterAdShowFun()
    {
        ExitPannel.SetActive(true);
        MainMenuPannel.SetActive(false);
    }
    public void YesBtnFun()
    {
        Application.Quit();
    }
    public void NoBtnFun()
    {
        ExitPannel.SetActive(false);
        MainMenuPannel.SetActive(true);
    }
    [Tooltip("Store page for \"Rate Us\" (empty = button does nothing).")]
    public string rateUsUrl = "";

    public void RateUsBtnFun()
    {
        if (!string.IsNullOrEmpty(rateUsUrl)) Application.OpenURL(rateUsUrl);
    }
    public void MoreGameBtnFun()
    {
        Application.OpenURL("https://play.google.com/store/apps/developer?id=Simudrive+3D+Game");
    }
    public void PrivacyBtnFun()
    {
        Application.OpenURL("https://simudrive-3d-game-privacy-policy.blogspot.com/2026/02/i-am-car-privacy-policy.html");
    }

}
