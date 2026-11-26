using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickUpPoint : MonoBehaviour
{
    public GameObject NextPoint;
    public string Mid_Text;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
           
            NextPoint.gameObject.SetActive(true);
            GameMannager.Instance.StopTheCar();
            GameMannager.Instance.UnStopTheCar();
            CanvasMannager.Instance.GamePlayMidText.SetActive(true);
            CanvasMannager.Instance.MidText.text = Mid_Text;
            this.gameObject.SetActive(false);
        }
    }
}
