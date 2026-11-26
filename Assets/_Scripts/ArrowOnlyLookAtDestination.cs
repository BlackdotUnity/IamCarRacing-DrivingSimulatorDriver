using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowOnlyLookAtDestination : MonoBehaviour
{
   

    void Update()
    {
        Transform cp = CheckpointManager.Instance.activeCheckpoint;
        if (!cp) return;

        Vector3 dir = cp.position - transform.position;
        dir.y = 0f;

        transform.rotation = Quaternion.LookRotation(dir);
    }
}
