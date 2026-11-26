using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SplashMannager : MonoBehaviour
{
    // Start is called before the first frame update
    public void Start()
    {
        StartCoroutine(LoadMenuAfterDelay());
    }

    IEnumerator LoadMenuAfterDelay()
    {
        // The splash interstitial ad was removed; keep the same splash duration.
        yield return new WaitForSeconds(7.5f);
        LoadingMainMenu();
    }

    public void LoadingMainMenu()
    {
        LoadingManager.Instance.LoadingNextScene("MainMenu");
    }


}
