using GorillaLocomotion;
using UnityEngine;

namespace InjuryMod.MonoBehaviors;

public class HandBreakDetector : MonoBehaviour
{
    public bool isLeftHand;

    private Vector3 lastVelocity;

    private bool lastHandTouching = false;

    private GTPlayer.HandState hand;

    public static HandBreakDetector LeftInstance;

    public static HandBreakDetector RightInstance;

    private void Start()
    {
        if (isLeftHand) 
        {
            hand = GTPlayer.Instance.LeftHand;
            LeftInstance = this;
        }
        else 
        {
            hand = GTPlayer.Instance.RightHand;
            RightInstance = this;
        }
    }

    private void Update()
    {
        if (!Plugin.Instance.inModdedRoom) return;
        if (GTPlayer.Instance.IsHandTouching(isLeftHand) && !lastHandTouching)
        {
            if (lastVelocity.magnitude > Plugin.Instance.handStrength.Value)
            {
                Plugin.Log.WriteLine("Hand hit detected");
                Plugin.Instance.DamagePlayer(25, "Hand Shattered");
                Plugin.Instance.boneBreakSFX.Play();
                if (hand.isLeftHand)
                {
                    Plugin.Instance.leftHandDisabled = true;
                }
                else
                {
                    Plugin.Instance.rightHandDisabled = true;
                }
            }
        }

        lastHandTouching = GTPlayer.Instance.IsHandTouching(isLeftHand);
        lastVelocity = hand.velocityTracker.GetLatestVelocity(true);
    }
}
