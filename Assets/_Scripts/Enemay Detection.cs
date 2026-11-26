using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemayDetection : MonoBehaviour
{
    public Transform player;
    public Transform AiCar;
    public float failTooCloseDistance;
    public float failTooFarDistance;

    public float warningCloseDistance; // warning if too close but not fail
    public float warningFarDistance;   // warning if too far but not fail

    public void Start()
    {
        player = GameMannager.Instance.Cars[OverAllGameMannager.OA_GM.SaveData.CurrentCar].gameObject.transform;
    }
    void Update()
    {
        
        if (OverAllGameMannager.OA_GM.GameStart == true)
        {
            float distance = Vector3.Distance(player.position, AiCar.position);
            // Immediate fail
            if (distance <= failTooCloseDistance)
            {
                CanvasMannager.Instance.GameFailFun(FailReason.AiTooClose);
            }
            else if (distance >= failTooFarDistance)
            {
                CanvasMannager.Instance.GameFailFun(FailReason.AiTooFar);
            }
            // Warning zones
            else if (distance > failTooCloseDistance && distance <= warningCloseDistance)
            {
                // warningText.text = "Enemy is getting TOO CLOSE!";
                CanvasMannager.Instance.GamePlayMidText.SetActive(true);
                CanvasMannager.Instance.MidText.text = "Enemy is getting TOO CLOSE!";
            }
            else if (distance >= warningFarDistance && distance < failTooFarDistance)
            {
                //  warningText.text = "Enemy is getting TOO FAR!";
                CanvasMannager.Instance.GamePlayMidText.SetActive(true);
                CanvasMannager.Instance.MidText.text = "Enemy is getting TOO FAR!";
            }
            // Safe zone
            else
            {
               // CanvasMannager.Instance.GamePlayMidText.SetActive(true);
                CanvasMannager.Instance.MidText.text = "";
                // warningText.text = "";
            }
        }
    }
}
