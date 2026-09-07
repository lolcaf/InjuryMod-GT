using UnityEngine;

namespace InjuryMod.MonoBehaviors;

public class BeetleCollectable : MonoBehaviour
{
    public static GameObject collectableHolder;

    public static BeetleCollectable Create(Vector3 position)
    {
        Plugin.Log.WriteLine("Creating beetle collectible");
        GameObject beetle = Instantiate(Plugin.Instance.beetlePrefab, position, Quaternion.identity);
        beetle.transform.SetParent(collectableHolder.transform, true);
        return beetle.AddComponent<BeetleCollectable>();
    }

    internal static void InIt()
    {
        collectableHolder = new GameObject("BeetleCollectableHolder");
    }

    private void Start()
    {
        gameObject.layer = 18;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == GorillaTagger.Instance.leftHandTriggerCollider || other.gameObject == GorillaTagger.Instance.rightHandTriggerCollider)
        {
            if (Plugin.Instance.holdingBeetle) return;
            Plugin.Log.WriteLine("Beetle Collected");
            Plugin.Instance.GiveBeetle();
            Destroy(gameObject);
        }
    }
}
