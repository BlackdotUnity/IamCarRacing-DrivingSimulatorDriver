using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameMannager : MonoBehaviour
{
    public static GameMannager Instance;
    public GameObject[] Levels;
    public GameObject[] LevelPos;

    public GameObject[] Cars;
    public GameObject RccCam;
    public GameObject[] Arrow;

    public int CarNumber, LevelNumber;

    // OverAllGameMannager //
    SaveData data;
    int chapter;
    int level;
    int car;
    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        data = OverAllGameMannager.OA_GM.SaveData;
        chapter = data.CurrentChapter;
        level = data.CurrentLevel;
        // Out-of-range save values (old saves, removed cars) fall back to the first car / level slot.
        if (data.CurrentCar < 0 || data.CurrentCar >= Cars.Length || Cars[data.CurrentCar] == null) data.CurrentCar = 0;
        if (level < 0 || level >= LevelPos.Length) level = 0;
        car = data.CurrentCar;
        Cars[car].transform.position = LevelPos[level].transform.position;
        Cars[car].transform.rotation = LevelPos[level].transform.rotation;
        Cars[car].SetActive(true);
        
        StopTheCar();
        MyMusicMannager.OA_MM.GameMusicPlayFun();
        CarFuelTank();
    }
    public void CarFuelTank()
    {
        if (data.Cars[car].CarFuel <= 0)
        {
            data.Cars[car].CarFuel = 10f;
        }
        Cars[car].GetComponent<RCC_CarControllerV3>().fuelTank = data.Cars[car].CarFuel;
    }
    public void UpdateCarFuel()
    {
        // Practice gives a full tank for free; don't write it into the save.
        if (PracticeMode.Active) return;
        data.Cars[car].CarFuel= Cars[car].GetComponent<RCC_CarControllerV3>().fuelTank;
    }
    public void LevelActivated()
    {
        // Chapter 2+ levels have no hand-built level object: MissionRunner builds them.
        if (MissionDatabase.IsGenerated(chapter, level)) return;
        if (level >= 0 && level < Levels.Length && Levels[level] != null)
            Levels[level].SetActive(true);
    }

    public void SetCameraPos()
    {
        RccCam.GetComponent<RCC_Camera>().TPSDistance = 5f;
        RccCam.GetComponent<RCC_Camera>().TPSHeight = 1f;
    }



    public void StopTheCar()
    {
        Cars[car].GetComponent<Rigidbody>().isKinematic = true;
        Cars[car].GetComponent<RCC_CarControllerV3>().engineRunning = false;
    }

    public void UnStopTheCar()
    {
        Cars[car].GetComponent<Rigidbody>().isKinematic = false;
        Cars[car].GetComponent<RCC_CarControllerV3>().engineRunning = true;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
