using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Timer : MonoBehaviour
{

    public static Timer Instance;
    public Text timertext;
    public float miliSec, seconds, minutes;

    // OverAllGameMannager //
    SaveData data;
    int chapter;
    int level;
    // Use this for initialization
    void Start()
    {
        Instance = this;
        data = OverAllGameMannager.OA_GM.SaveData;
        chapter = data.CurrentChapter;
        level = data.CurrentLevel;
        minutes = data.Chapters[chapter].levels[level].minutes;
        seconds = data.Chapters[chapter].levels[level].seconds;

    }
    public void TimeAdjustmentFun(float min,float sec)
    {
        minutes = min;
        seconds = sec;
    }
    

    // Update is called once per frame
    void Update()
    {
        // Practice has no time limit.
        if (PracticeMode.Active) return;
        if (OverAllGameMannager.OA_GM.GameStart == true)
        {
            if (OverAllGameMannager.OA_GM.Timmer)
            {
                if (miliSec <= 0)
                {
                    if (seconds <= 0)
                    {
                        minutes--;
                        seconds = 59;

                    }
                    else if (seconds >= 0)
                    {
                        seconds--;
                    }

                    miliSec = 100;

                }

                    miliSec -= Time.deltaTime * 100;

                timertext.text = string.Format("{0}:{1:00}", Mathf.Max(0f, minutes), Mathf.Max(0f, seconds)); // 1:05, not 1:5

                if (miliSec <= 0 && seconds <= 0 && minutes <= 0)
                {
                    if (level < 0 || level > 9) return;
                   
                    if (level == 9)
                    {
                        OverAllGameMannager.OA_GM.Level10GateCloseOpen = false;
                        MyMusicMannager.OA_MM.AlaramSoundStopFun();
                    }
                   // CanvasMannager.Instance.GameFailFun();
                    CanvasMannager.Instance.GameFailFun(FailReason.TimeUp);
                }
            }
        }

    }

}
