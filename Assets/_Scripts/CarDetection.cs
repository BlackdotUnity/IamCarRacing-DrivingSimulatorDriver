using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarDetection : MonoBehaviour
{
    
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("LevelComplete"))
        {
            other.gameObject.SetActive(false);
            CanvasMannager.Instance.GameCompleteFun();
        }
    }


    [Header("Damage Settings")]
    public float minImpactForce = 5f;   // ignore small touches
    public float damageCooldown = 1f;   // time lock

    private bool canTakeDamage = true;

    private void OnCollisionEnter(Collision collision)
    {
        if (!canTakeDamage) return;

        if (!collision.gameObject.CompareTag("Obstacles")) return;

        float impact = collision.relativeVelocity.magnitude;

        if (impact < minImpactForce) return;

        // Practice: crashes never fail.
        if (PracticeMode.Active) return;

        // Generated missions count crashes themselves (e.g. "max 2 hits") and handle ram targets.
        if (MissionRunner.Instance != null && MissionRunner.Instance.InterceptCrash(collision, impact)) return;

        TakeDamage();
    }

    private void TakeDamage()
    {
        canTakeDamage = false;
        CanvasMannager.Instance.GameFailFun(FailReason.Collision);
        StartCoroutine(DamageCooldown());
    }

    private IEnumerator DamageCooldown()
    {
        yield return new WaitForSeconds(damageCooldown);
        canTakeDamage = true;
    }

}
