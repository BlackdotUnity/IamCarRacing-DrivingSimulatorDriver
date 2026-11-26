using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GateAnimation : MonoBehaviour
{
  
    public float speed = 20f;


    public float openAngle;
    public float closeAngle;

    void Update()
    {
        float targetAngle = OverAllGameMannager.OA_GM.Level10GateCloseOpen
            ? openAngle
            : closeAngle;

        float currentY = transform.localEulerAngles.y;

        float newY = Mathf.MoveTowardsAngle(
            currentY,
            targetAngle,
            speed * Time.deltaTime
        );

        transform.localEulerAngles = new Vector3(0f, newY, 0f);
    }
}
