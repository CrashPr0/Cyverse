using UnityEngine;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Scene lookup details shared by the level realization modules. Managers
    /// receive already-resolved references and do not need to know how an
    /// authored scene or procedural scene stores its objects.
    /// </summary>
    internal static class LevelSceneLookup
    {
        public static HubDoor NearestExit()
        {
            Camera camera = Camera.main;
            Vector3 origin = camera != null ? camera.transform.position : Vector3.zero;
            HubDoor nearest = null;
            float nearestSqr = float.MaxValue;

            foreach (HubDoor door in Object.FindObjectsOfType<HubDoor>())
            {
                float sqr = (door.transform.position - origin).sqrMagnitude;
                if (sqr >= nearestSqr) continue;
                nearestSqr = sqr;
                nearest = door;
            }

            return nearest;
        }

        public static void HideTextObjectsNamed(string prefix)
        {
            foreach (TMPro.TMP_Text text in Object.FindObjectsOfType<TMPro.TMP_Text>(true))
                if (text != null && text.gameObject.name.StartsWith(prefix))
                    text.gameObject.SetActive(false);

            foreach (TextMesh text in Object.FindObjectsOfType<TextMesh>(true))
                if (text != null && text.gameObject.name.StartsWith(prefix))
                    text.gameObject.SetActive(false);
        }
    }
}
