using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>Keeps world-space signage facing the player (rotates about Y).</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 away = transform.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) return;
            // TextMesh is readable when its forward points away from the viewer.
            transform.rotation = Quaternion.LookRotation(away);
        }
    }
}
