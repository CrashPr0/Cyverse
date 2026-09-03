using UnityEngine;
using TMPro;
using Cyverse.Settings;

namespace Cyverse.Level
{
    /// <summary>
    /// Brings floating signage to life: a slow vertical bob, a soft brightness
    /// pulse, and an occasional split-second "hologram glitch" (brightness
    /// spike + tiny horizontal jitter). Per-sign random phase so the room
    /// doesn't move in lockstep. Fully static under Reduce Motion.
    /// </summary>
    public class SignFX : MonoBehaviour
    {
        public float bobAmplitude = 0.05f;
        public float bobSpeed = 1.1f;
        public float pulseAmount = 0.16f;
        public float pulseSpeed = 2.1f;

        private TextMesh tm;
        private TMP_Text tmp;
        private Vector3 basePos;
        private Color baseColor;
        private float seed;
        private float nextGlitch;
        private float glitchEnd;

        void Start()
        {
            tm = GetComponent<TextMesh>();
            tmp = GetComponent<TMP_Text>();
            if (tm == null && tmp == null)
            {
                enabled = false;
                return;
            }
            basePos = transform.localPosition;
            baseColor = TextColor;
            seed = Random.Range(0f, 10f);
            ScheduleGlitch();
        }

        void Update()
        {
            if (AccessibilitySettings.ReduceMotion)
            {
                transform.localPosition = basePos;
                TextColor = baseColor;
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
            TextColor = c;
        }

        private void ScheduleGlitch()
        {
            nextGlitch = Time.time + Random.Range(4f, 9f);
        }

        private Color TextColor
        {
            get => tm != null ? tm.color : tmp.color;
            set
            {
                if (tm != null) tm.color = value;
                else if (tmp != null) tmp.color = value;
            }
        }
    }
}
