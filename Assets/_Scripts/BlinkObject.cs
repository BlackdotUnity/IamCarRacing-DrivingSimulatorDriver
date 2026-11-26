using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BlinkObject : MonoBehaviour
{
    public float blinkTime = 0.5f;
    private Image img;
    private Coroutine blinkRoutine;

    void Awake()
    {
        img = GetComponent<Image>();
    }

    void OnEnable()
    {
        blinkRoutine = StartCoroutine(Blink());
    }

    void OnDisable()
    {
        if (blinkRoutine != null)
            StopCoroutine(blinkRoutine);

        img.enabled = true; // reset state
    }

    IEnumerator Blink()
    {
        while (true)
        {
            if (CanvasMannager.Instance.shouldBlink)
            {
                img.enabled = !img.enabled;
                yield return new WaitForSecondsRealtime(blinkTime);
            }
            else
            {
                img.enabled = true;
                yield return null; // wait one frame
            }
        }
    }
}
