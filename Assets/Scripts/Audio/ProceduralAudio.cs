using UnityEngine;

namespace Cyverse.Audio
{
    /// <summary>
    /// Generates short SFX as PCM at runtime so the game ships with audio
    /// feedback before any real sound assets exist. Replace these with recorded
    /// clips later by assigning them on the Sfx component.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SR = 44100;

        /// <summary>Soft low-frequency thud for footsteps (filtered noise burst).</summary>
        public static AudioClip Footstep()
        {
            int len = (int)(SR * 0.12f);
            var data = new float[len];
            var rnd = new System.Random(1337);
            float prev = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / len;
                float env = Mathf.Exp(-t * 18f);
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                prev = Mathf.Lerp(prev, n, 0.22f); // crude low-pass -> softer thud
                data[i] = prev * env * 0.6f;
            }
            return Make("sfx_footstep", data);
        }

        /// <summary>Short high blip for UI navigation / interaction.</summary>
        public static AudioClip Click()
        {
            int len = (int)(SR * 0.06f);
            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / len;
                float env = Mathf.Exp(-t * 30f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1200f * ((float)i / SR)) * env * 0.4f;
            }
            return Make("sfx_click", data);
        }

        /// <summary>Two-tone rising chime for "station reviewed" confirmation.</summary>
        public static AudioClip Confirm()
        {
            int len = (int)(SR * 0.28f);
            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float ti = (float)i / SR;
                float t = (float)i / len;
                float freq = t < 0.5f ? 700f : 1050f;
                float env = Mathf.Exp(-t * 5.5f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * ti) * env * 0.4f;
            }
            return Make("sfx_confirm", data);
        }

        /// <summary>Soft descending two-tone for a wrong answer (gentle, not punishing).</summary>
        public static AudioClip Deny()
        {
            int len = (int)(SR * 0.24f);
            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float ti = (float)i / SR;
                float t = (float)i / len;
                float freq = t < 0.5f ? 420f : 300f;
                float env = Mathf.Exp(-t * 6f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * ti) * env * 0.32f;
            }
            return Make("sfx_deny", data);
        }

        /// <summary>
        /// Low facility hum for room tone, built from stacked sines. Every
        /// frequency (and the slow swell LFO) completes a whole number of
        /// cycles over the 6-second clip, so it loops seamlessly.
        /// </summary>
        public static AudioClip AmbientLoop()
        {
            const float seconds = 6f;
            int len = (int)(SR * seconds);
            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SR;
                float lfo = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / seconds); // 1 cycle
                float s =
                    Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.50f +   // 330 cycles
                    Mathf.Sin(2f * Mathf.PI * 55.5f * t) * 0.35f + // 333 cycles (slow beat vs 55)
                    Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.22f +  // 660 cycles
                    Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.06f;   // 1320 cycles
                data[i] = s * lfo * 0.22f;
            }
            return Make("amb_hum", data);
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
