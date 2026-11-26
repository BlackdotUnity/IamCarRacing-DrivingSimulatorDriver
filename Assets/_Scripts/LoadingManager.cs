using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance;
    public Image fillImage;
    public Text progressText;

    private void Awake()
    {
        Instance = this;
    }
    void Start()
    {
       
    }

    public void LoadingNextScene(string SceneName)
    {
        StartCoroutine(LoadScene(SceneName));
    }

    public IEnumerator LoadScene( string SceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(SceneName);

        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            // Fill Image
            fillImage.fillAmount = progress;

            // Optional Text
            if (progressText)
                progressText.text = (progress * 100f).ToString("0") + "%";

            if (operation.progress >= 0.9f)
            {
                fillImage.fillAmount = 1f;
                if (progressText)
                    progressText.text = "100%";

                yield return new WaitForSeconds(0.3f);
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
