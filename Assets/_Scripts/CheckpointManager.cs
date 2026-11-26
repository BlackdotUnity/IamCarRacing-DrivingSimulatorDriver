using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance;

    public Transform activeCheckpoint;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void SetActiveCheckpoint(Transform cp)
    {
        activeCheckpoint = cp;
    }
}
