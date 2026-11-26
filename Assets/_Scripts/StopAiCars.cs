using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StopAiCars : MonoBehaviour
{
    public RCC_AICarController ai;
    public RCC_CarControllerV3 car;
    // Start is called before the first frame update
    void Start()
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        if (OverAllGameMannager.OA_GM.GameStart == true)
        {
           
            car.brakeInput = 0f;
            ai.enabled = true;
        }
        else 
        {
            ai.enabled = false;
            car.throttleInput = 0f;
            car.brakeInput = 1f;
        }

       
        

    }
}
