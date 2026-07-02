using UnityEngine;
using Cyverse.Settings;

namespace Cyverse.Level
{
    /// <summary>
    /// Brings floating signage to life: a slow vertical bob, a soft brightness
    /// pulse, and an occasional split-second "hologram glitch" (brightness
    /// spike + tiny horizontal jitter). Per-sign random phase so the room
    /// doesn't move in lockstep. Fully static under Reduce Motion.
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public class SignFX : MonoBehaviour
    {
        public float bobAmplitude = 0.05f;
        public float bobSpeed = 1.1f;
        public float pulseAmount = 0.16f;
        public float pulseSpeed = 2.1f;

        private TextMesh tm;
        private Vector3 basePos;
        private Color baseColor;
        private float seed;
        private float nextGlitch;
        private float glitchEnd;

        void Start()
        {
            tm = GetComponent<TextMesh>();
            basePos = transform.localPosition;
            baseColor = tm.color;
            seed = Random.Range(0f, 10f);
            ScheduleGlitch();
        }

        void Update()
        {
            if (AccessibilitySettings.ReduceMotion)
            {
                transform.localPosition = basePos;
                tm.color = baseColor;
                return;
            }

            float t = Time.time + seed;
            Vector3 p = basePos + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobAmplitude);

            float pulse = 1f + Mathf.Sin(t * pulseSpeed) * pulseAmount * 0.5f;
            Color c = baseColor * pulse;
            c.a = baseColor.a;

            if (Time.time >= nextGlitch)
            {
                glitchEnd = Time.time + 0.12f;
                ScheduleGlitch();
            }
            if (Time.time < glitchEnd)
            {
                p.x += Random.Range(-0.02f, 0.02f);
                c = baseColor * 1.6f;
                c.a = baseColor.a;
            }

            transform.localPosition = p;
            tm.color = c;
        }

        private void ScheduleGlitch()
        {
            nextGlitch = Time.time + Random.Range(4f, 9f);
        }
    }
}
