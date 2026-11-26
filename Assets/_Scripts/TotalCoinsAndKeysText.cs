using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TotalCoinsAndKeysText : MonoBehaviour
{
      private Text txt; // assign in inspector
    [SerializeField] private bool showCoins = true; // true = show coins, false = show keys

    private int lastValue = -1;
    private float timer;
    private const float refreshRate = 0.5f; // check every 0.5 seconds

    void Awake()
    {
        if (!txt) txt = GetComponent<Text>();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0) return;
        timer = refreshRate;

        // Pick which value to show
        int currentValue = showCoins ?
            OverAllGameMannager.OA_GM.SaveData.TotalCoins :
            OverAllGameMannager.OA_GM.SaveData.TotalKeys;

        // Only update text if value changed
        if (currentValue != lastValue)
        {
            lastValue = currentValue;
            txt.text = currentValue.ToString();
        }
    }

}
