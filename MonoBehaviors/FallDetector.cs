using UnityEngine;

namespace InjuryMod.MonoBehaviors;

[RequireComponent(typeof(Rigidbody))]
public class FallDetector : MonoBehaviour // this must go on the player rigidbody
{
    private Vector3 lastVelocity = Vector3.zero;

    private void LateUpdate()
    {
        lastVelocity = GetComponent<Rigidbody>().linearVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (lastVelocity.y < -10f) // if the player was falling fast enough
        {
            float damage = Mathf.Abs(lastVelocity.y) * Plugin.Instance.fallMultiplier.Value; // calculate damage based on fall speed
            Plugin.Instance.DamagePlayer(Mathf.RoundToInt(damage), "fall");
        }
    }
}
