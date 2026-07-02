using UnityEngine;
using Cyverse.Settings;

namespace Cyverse.Level
{
    /// <summary>
    /// Ambient drone motion: gentle vertical bob, a slow yaw, and fast rotor
    /// spin. Freezes under Reduce Motion.
    /// </summary>
    public class Hoverer : MonoBehaviour
    {
        public float bobAmplitude = 0.15f;
        public float bobSpeed = 1.1f;
        public float yawSpeed = 16f;
        public float rotorSpeed = 900f;
        public Transform[] rotors;

        private Vector3 basePos;
        private float seed;

        void Start()
        {
            basePos = transform.position;
            seed = Random.Range(0f, 10f);
        }

        void Update()
        {
            if (AccessibilitySettings.ReduceMotion) return;

            float y = Mathf.Sin(Time.time * bobSpeed + seed) * bobAmplitude;
            transform.position = basePos + Vector3.up * y;
            transform.Rotate(0f, yawSpeed * Time.deltaTime, 0f, Space.World);

            if (rotors == null) return;
            foreach (var r in rotors)
                if (r != null) r.Rotate(0f, rotorSpeed * Time.deltaTime, 0f, Space.Self);
        }
    }
}
