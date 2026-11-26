using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Distance : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (OverAllGameMannager.OA_GM.GameStart == true)
        {
            if (!CanvasMannager.Instance.DistanceText.activeInHierarchy)
            {
                CanvasMannager.Instance.DistanceText.SetActive(true);
            }
            float distance = Vector3.Distance(this.transform.position, GameMannager.Instance.Cars[OverAllGameMannager.OA_GM.SaveData.CurrentCar].transform.position);
           // Debug.Log(" distance" + distance);
            int distInt = Mathf.RoundToInt(distance);
           
            CanvasMannager.Instance.Distance_Text.text = distInt + " m";
        }

    
    }
}
