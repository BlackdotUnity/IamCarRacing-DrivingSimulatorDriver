using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FuelCheckPoint : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject NextPoint;
    public string Mid_Text;
    void Start()
    {
        
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            //RCC_SceneManager.Instance.activePlayerVehicle.fuelTank += 10f;
            NextPoint.gameObject.SetActive(true);
            GameMannager.Instance.StopTheCar();
            GameMannager.Instance.UnStopTheCar();
            CanvasMannager.Instance.GamePlayMidText.SetActive(true);
            CanvasMannager.Instance.MidText.text = Mid_Text;
            this.gameObject.SetActive(false);
        }
    }
}
