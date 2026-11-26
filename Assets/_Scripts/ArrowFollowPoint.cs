using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowFollowPoint : MonoBehaviour
{
    // Start is called before the first frame update
    void OnEnable()
    {
        CheckpointManager.Instance.SetActiveCheckpoint(transform);
    }
}
