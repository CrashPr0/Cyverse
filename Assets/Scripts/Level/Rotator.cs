using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>Slowly spins an object for ambient "live hologram" motion.</summary>
    public class Rotator : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new Vector3(0f, 25f, 0f);

        void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
