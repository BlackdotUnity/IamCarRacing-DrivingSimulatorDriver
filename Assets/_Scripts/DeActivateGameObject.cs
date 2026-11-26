using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeActivateGameObject : MonoBehaviour
{
    public float deactivateTime;

    private Coroutine deactivateRoutine;

    void OnEnable()
    {
        deactivateRoutine = StartCoroutine(DeactivateObject());
    }

    void OnDisable()
    {
        if (deactivateRoutine != null)
            StopCoroutine(deactivateRoutine);
    }

    IEnumerator DeactivateObject()
    {
        yield return new WaitForSecondsRealtime(deactivateTime);
        gameObject.SetActive(false);
    }
}
