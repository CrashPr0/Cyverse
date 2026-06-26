using UnityEngine;
using Cyverse.Settings;

namespace Cyverse.Level
{
    /// <summary>Slowly spins an object for ambient "live hologram" motion.
    /// Honors the Reduce Motion accessibility setting.</summary>
    public class Rotator : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new Vector3(0f, 25f, 0f);

        void Update()
        {
            if (AccessibilitySettings.ReduceMotion) return;
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
