using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BusScalePulse : MonoBehaviour
{
    public float minScale = 0.9f;
    public float maxScale = 1.1f;
    public float speed = 2f;

    void Update()
    {
        float scale = Mathf.Lerp(minScale, maxScale,
            (Mathf.Sin(Time.time * speed) + 1f) / 2f);

        transform.localScale = Vector3.one * scale;
    }
}
