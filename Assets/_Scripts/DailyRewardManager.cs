using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

public class DailyRewardManager : MonoBehaviour
{
    public GameObject rewardPanel, congratulationPanel;
    public Text [] rewardTexts;
    public Text CongraulationText;
    public GameObject[] CongraulationIcon;
    public Button[] dayButtons;   // Size = 7
    public Text nextRewardTimerText;
    public Text[] ClaimText;
    public GameObject DaliyRewardBtn;

    void Start()
    {
      
    }
    Animator btnAnim;
    Animator BtnAnim => btnAnim != null ? btnAnim : (btnAnim = DaliyRewardBtn.GetComponent<Animator>());

    public void Update()
    {
        UpdateNextRewardTimer();
    }


    void UpdateRewardUI()
    {
        rewardTexts[0].text = "500 Coins";
        rewardTexts[1].text = "1000 Coins";
        rewardTexts[2].text = "2000 Coins";
        rewardTexts[3].text = "3000 Coins";
        rewardTexts[4].text = "5000 Coins";
        rewardTexts[5].text = "7k Coins + 1 Key + " + CarName(1);
        rewardTexts[6].text = "10k Coins + 3 Keys + " + CarName(2);
    }
   
    void UpdateNextRewardTimer()
    {
        var data = OverAllGameMannager.OA_GM.SaveData;

        // No reward claimed yet
        if (string.IsNullOrEmpty(data.LastDailyRewardTime))
        {
            nextRewardTimerText.text = "Reward available!";
            BtnAnim.enabled=true;
            return;
        }

        if (!DateTime.TryParse(data.LastDailyRewardTime, out DateTime lastTime))
        {
            nextRewardTimerText.text = "Reward available!";
            BtnAnim.enabled = true;
            return;
        }

        TimeSpan remaining = lastTime.AddHours(24) - DateTime.Now;

        // Reward ready
        if (remaining <= TimeSpan.Zero)
        {
            nextRewardTimerText.text = "Reward available!";
            BtnAnim.enabled = true;
            return;
        }
        BtnAnim.enabled = false;
        // Countdown
        nextRewardTimerText.text = string.Format(
            "Next reward in {0:00}:{1:00}:{2:00}",
            remaining.Hours,
            remaining.Minutes,
            remaining.Seconds
        );
    }
    bool IsRewardAvailable()
    {
        var data = OverAllGameMannager.OA_GM.SaveData;

        if (string.IsNullOrEmpty(data.LastDailyRewardTime))
            return true;

        if (!DateTime.TryParse(data.LastDailyRewardTime, out DateTime lastTime)) return true;
        double hoursPassed = (DateTime.Now - lastTime).TotalHours;

        // Missed a day → reset to Day 1
        if (hoursPassed >= 48)
        {
            data.DailyRewardDay = 0;
            OverAllGameMannager.OA_GM.SaveDataToJson();
            return true;
        }

        return hoursPassed >= 24;

    }
    void UpdateButtons()
    {
        var data = OverAllGameMannager.OA_GM.SaveData;

        for (int i = 0; i < dayButtons.Length; i++)
            dayButtons[i].interactable = false;

        if (!IsRewardAvailable())
            return;

        int day = data.DailyRewardDay;
        if (day >= 7) day = 0;

        dayButtons[day].interactable = true;
    }

    void UpdatesButtonsFun()
    {
        var data = OverAllGameMannager.OA_GM.SaveData;

        int currentDay = data.DailyRewardDay;
        if (currentDay >= dayButtons.Length)
            currentDay = 0;

        bool rewardAvailable = IsRewardAvailable();

        for (int i = 0; i < dayButtons.Length; i++)
        {
            Button btn = dayButtons[i];
            btn.interactable = false;

            // Safety check
            if (i >= ClaimText.Length)
                continue;

            // Already claimed days
            if (i < currentDay)
            {
                ClaimText[i].text = "Claimed";
            }
            // Current day
            else if (i == currentDay)
            {
                if (rewardAvailable)
                {
                    btn.interactable = true;
                    ClaimText[i].text = "Claim";
                }
                else
                {
                    ClaimText[i].text = "Claim";
                }
            }
            // Future days
            else
            {
                ClaimText[i].text = "Locked";
            }
        }
    }


    static string CarName(int i)
    {
        var d = OverAllGameMannager.OA_GM != null ? OverAllGameMannager.OA_GM.SaveData : null;
        return d != null && i < d.Cars.Count ? d.Cars[i].CarName : "a new";
    }

    static void UnlockCar(SaveData data, int i)
    {
        if (i < data.Cars.Count) data.Cars[i].isUnlocked = true;
    }

    public void CloseButtonFun()
    {
        rewardPanel.SetActive(false);
        congratulationPanel.SetActive(false);
    }

    public void ShowDaliyRewardBtnFun()
    {
        rewardPanel.SetActive(true);
        UpdateRewardUI();
        UpdatesButtonsFun();

    }



    public void ClaimReward(int dayIndex)
    {
        var data = OverAllGameMannager.OA_GM.SaveData;

        // (IsRewardAvailable first: after a missed day it resets the streak to Day 1)
        if (!IsRewardAvailable() || dayIndex != data.DailyRewardDay)
            return;
        foreach (var icon in CongraulationIcon) if (icon != null) icon.SetActive(false); // only this day's icon

        switch (dayIndex)
        {
            case 0: // Day 1
                data.TotalCoins += 500;
                CongraulationText.text = "You Got 500 Coins for Today's Check-In. Don't Miss Tomorrow's Reward!";
                CongraulationIcon[0].SetActive(true);
                break;
            case 1: // Day 2
                data.TotalCoins += 1000;
                CongraulationText.text = "You Got 1000 Coins Today. Your Daily Streak is growing!";
                CongraulationIcon[0].SetActive(true);
                break;
            case 2: // Day 3
                data.TotalCoins += 2000;
                CongraulationText.text = "You got 2000 coins today. Keep playing to earn even more!";
                CongraulationIcon[0].SetActive(true);
                break;
            case 3: // Day 4
                data.TotalCoins += 3000;
                CongraulationText.text = "You got 3000 coins today. Your dedication is paying off!";
                CongraulationIcon[0].SetActive(true);
                break;

            case 4: // Day 5 → Coins + Car Unlock
                data.TotalCoins += 5000;
                CongraulationText.text = "You got 5000 coins. Keep playing to earn even more!";
                CongraulationIcon[1].SetActive(true);
                break;

            case 5: // Day 6 → Key + Car Lock
                data.TotalCoins += 7000;
                UnlockCar(data, 1);
                data.TotalKeys += 1;
                CongraulationText.text = "You got 7000 coins, a key, and the " + CarName(1) + " car today. Nice progress!";
                CongraulationIcon[2].SetActive(true);
                break;

            case 6: // Day 7 → Key + Car Unlock + Lock
                data.TotalCoins += 10000;
                UnlockCar(data, 2);
                data.TotalKeys += 3;
                CongraulationText.text = "You got 10000 coins, 3 keys, and the " + CarName(2) + " car today. Maximum reward achieved!";
                CongraulationIcon[2].SetActive(true);
                break;
        }

        // ---- Save time & move day ----
        data.LastDailyRewardTime = DateTime.Now.ToString("o"); // round-trip format: parses the same in every device language
        data.DailyRewardDay++;

        if (data.DailyRewardDay >= 7)
            data.DailyRewardDay = 0;

        // ---- Save Game (+ refresh the coin / key labels) ----
        GameEconomy.Add(Currency.Coins, 0);

        // ---- UI ----
        rewardPanel.SetActive(false);
        congratulationPanel.SetActive(true);
    }
}
