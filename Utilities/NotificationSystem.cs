using TMPro;
using UnityEngine;

namespace InjuryMod.Utilities
{
    internal class NotificationSystem
    {
        private static GameObject lastNotificationText;

        internal static void Send(string text, float decayTime = 4)
        {
            if (lastNotificationText != null)
            {
                Object.DestroyImmediate(lastNotificationText); // destroy the overlapping notification text if there is one
            }
            GameObject txt = new GameObject("NotificationText");
            txt.transform.SetParent(Camera.main.transform, false);
            txt.transform.localPosition = new Vector3(0f, -0.3f, 0.8f);
            txt.transform.localRotation = Quaternion.identity;
            txt.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

            lastNotificationText = txt;

            TextMeshPro tmp = txt.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 4;
            tmp.richText = true;
            tmp.isOverlay = true;

            Object.Destroy(txt, decayTime);
        }
    }
}
