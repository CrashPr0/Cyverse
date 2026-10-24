using UnityEngine;

namespace Cyverse.Audio
{
    /// <summary>
    /// Generates short SFX as PCM at runtime so the game ships with audio
    /// feedback before any real sound assets exist. Replace these with recorded
    /// clips later by assigning them on the Sfx component.
    ///
    /// The tones are modelled on soft acoustic instruments rather than bare
    /// sine beeps: notes are mallet strikes (a fundamental plus a quiet,
    /// quickly fading overtone, like a marimba bar) with a gentle attack, low
    /// pitches, a low-pass to take the edge off, and a short room reverb so
    /// they sit in a space. WebGL has no audio filters, so all of this is baked
    /// into the samples.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SR = 44100;

        // Notes (Hz), kept in the low-middle register.
        private const float C4 = 261.63f, E4 = 329.63f, G4 = 392.00f, C5 = 523.25f;

        // ---- Clips ----------------------------------------------------------

        /// <summary>A soft footfall: a low heel thump and a short, dark
        /// scuff. Each <paramref name="variant"/> differs a little so a run
        /// of steps does not repeat one sample.</summary>
        public static AudioClip Footstep(int variant = 0)
        {
            var rnd = new System.Random(1337 + variant * 101);
            float[] d = Buffer(0.22f);
            float thump = 68f + (float)rnd.NextDouble() * 22f;
            float[] scuff = Noise(d.Length, rnd);
            LowPass(scuff, 700f + (float)rnd.NextDouble() * 400f);
            LowPass(scuff, 1100f);
            float scuffDecay = 24f + (float)rnd.NextDouble() * 10f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                d[i] = Mathf.Sin(2f * Mathf.PI * thump * t) * Attack(t, 0.004f) * Mathf.Exp(-t * 40f) * 0.8f
                     + scuff[i] * Attack(t - 0.004f, 0.008f) * Mathf.Exp(-t * scuffDecay) * 2.4f;
            }
            HighPass(d, 45f);
            // Steps play constantly, so they sit well under the UI sounds.
            Finish(d, 0.3f);
            return Make("sfx_footstep_" + variant, d);
        }

        /// <summary>A soft wooden tap for UI navigation / interaction.</summary>
        public static AudioClip Click()
        {
            float[] d = Buffer(0.1f);
            var rnd = new System.Random(7);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float body = Mathf.Sin(2f * Mathf.PI * 460f * t) * Mathf.Exp(-t * 55f)
                           + Mathf.Sin(2f * Mathf.PI * 1160f * t) * Mathf.Exp(-t * 110f) * 0.25f;
                float knock = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 400f) * 0.12f;
                d[i] = (body + knock) * Attack(t, 0.002f);
            }
            LowPass(d, 2400f);
            Finish(d, 0.22f);
            return Make("sfx_click", d);
        }

        /// <summary>A very quiet, low relay tick for room texture.</summary>
        public static AudioClip Tick()
        {
            float[] d = Buffer(0.08f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                d[i] = Mathf.Sin(2f * Mathf.PI * 310f * t) * Mathf.Exp(-t * 70f) * Attack(t, 0.002f);
            }
            LowPass(d, 1500f);
            Finish(d, 0.2f);
            return Make("sfx_tick", d);
        }

        /// <summary>Two rising mallet notes (G4 to C5) for "station reviewed"
        /// confirmation.</summary>
        public static AudioClip Confirm()
        {
            float[] d = Buffer(0.7f);
            AddMallet(d, 0f, G4, 0.8f, 9f);
            AddMallet(d, 0.08f, C5, 1f, 8f);
            LowPass(d, 3000f);
            Reverb(d, 0.16f);
            Finish(d, 0.38f);
            return Make("sfx_confirm", d);
        }

        /// <summary>A rising four-note arpeggio (C-E-G-C) for a completed
        /// task; longer and fuller than Confirm so the two stay distinct.</summary>
        public static AudioClip TaskComplete()
        {
            float[] d = Buffer(1.2f);
            float[] notes = { C4, E4, G4, C5 };
            for (int n = 0; n < notes.Length; n++)
                AddMallet(d, n * 0.085f, notes[n], n == notes.Length - 1 ? 1f : 0.8f, 6f);
            // The last note rings over a quiet low C, so it lands rather than stops.
            AddMallet(d, 3 * 0.085f, C4 * 0.5f, 0.35f, 4f);
            LowPass(d, 3200f);
            Reverb(d, 0.2f);
            Finish(d, 0.42f);
            return Make("sfx_task_complete", d);
        }

        /// <summary>Soft, muted descending pair (E4 to C4) for a wrong answer:
        /// gentle, not punishing, and high enough to carry on laptop speakers.</summary>
        public static AudioClip Deny()
        {
            float[] d = Buffer(0.55f);
            AddMallet(d, 0f, E4, 0.9f, 11f, brightness: 0.5f);
            AddMallet(d, 0.11f, C4, 1f, 10f, brightness: 0.5f);
            LowPass(d, 1800f);
            Reverb(d, 0.12f);
            Finish(d, 0.34f);
            return Make("sfx_deny", d);
        }

        /// <summary>
        /// Room tone: soft, low air noise (like distant ventilation) with a
        /// faint mains hum and a slow swell. The last second is cross-faded
        /// into the first so the 8-second clip loops without a seam; the hum
        /// and swell complete whole cycles over the clip.
        /// </summary>
        public static AudioClip AmbientLoop()
        {
            const float seconds = 8f;
            int len = (int)(SR * seconds);
            int fade = SR;
            var rnd = new System.Random(55);
            var raw = new float[len + fade];
            float brown = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                brown = brown * 0.996f + (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.05f;
                raw[i] = brown;
            }
            LowPass(raw, 420f);
            LowPass(raw, 600f);
            HighPass(raw, 35f);
            Normalize(raw, 0.6f);

            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float s = raw[i];
                if (i < fade)
                {
                    float w = (float)i / fade;
                    s = raw[i] * Mathf.Sqrt(w) + raw[len + i] * Mathf.Sqrt(1f - w);
                }
                float t = (float)i / SR;
                float swell = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / seconds);
                float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.06f
                          + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.025f;
                data[i] = (s + hum) * swell;
            }
            Normalize(data, 0.32f);
            return Make("amb_room", data);
        }

        // ---- Building blocks ------------------------------------------------

        private static float[] Buffer(float seconds) => new float[(int)(SR * seconds)];

        private static float[] Noise(int length, System.Random rnd)
        {
            var n = new float[length];
            for (int i = 0; i < length; i++) n[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);
            return n;
        }

        /// <summary>A smooth (raised-cosine) rise from 0 to 1 over
        /// <paramref name="seconds"/>, so notes start without a click.</summary>
        private static float Attack(float t, float seconds)
        {
            if (t <= 0f) return 0f;
            if (t >= seconds) return 1f;
            return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t / seconds);
        }

        /// <summary>Adds one mallet note: the fundamental plus the marimba's
        /// quiet fourth partial and a touch of octave, both fading faster
        /// than the fundamental. <paramref name="brightness"/> scales the
        /// overtones (lower is more muted).</summary>
        private static void AddMallet(float[] d, float startSeconds, float freq, float amp, float decay,
            float brightness = 1f)
        {
            int start = (int)(startSeconds * SR);
            for (int i = start; i < d.Length; i++)
            {
                float t = (float)(i - start) / SR;
                float w = 2f * Mathf.PI * freq * t;
                float v = Mathf.Sin(w) * Mathf.Exp(-t * decay)
                        + Mathf.Sin(w * 3.98f) * Mathf.Exp(-t * decay * 3.5f) * 0.10f * brightness
                        + Mathf.Sin(w * 2f) * Mathf.Exp(-t * decay * 2f) * 0.05f * brightness;
                d[i] += v * Attack(t, 0.005f) * amp;
            }
        }

        /// <summary>One-pole low-pass, in place.</summary>
        private static void LowPass(float[] d, float cutoff)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SR);
            float y = 0f;
            for (int i = 0; i < d.Length; i++) d[i] = y += a * (d[i] - y);
        }

        /// <summary>One-pole high-pass, in place (removes rumble and DC).</summary>
        private static void HighPass(float[] d, float cutoff)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SR);
            float low = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                low += a * (d[i] - low);
                d[i] -= low;
            }
        }

        /// <summary>A small, dark room reverb (four damped comb filters into
        /// two all-passes) mixed under the dry signal. The clip needs a few
        /// hundred milliseconds of tail room for it to ring out.</summary>
        private static void Reverb(float[] d, float mix)
        {
            float[] combMs = { 29.7f, 37.1f, 41.1f, 43.7f };
            var wet = new float[d.Length];
            foreach (float ms in combMs)
            {
                var line = new float[(int)(ms * 0.001f * SR)];
                int p = 0;
                float damp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float out_ = line[p];
                    damp += 0.35f * (out_ - damp);
                    line[p] = d[i] + damp * 0.7f;
                    p = (p + 1) % line.Length;
                    wet[i] += out_ * 0.25f;
                }
            }
            foreach (float ms in new[] { 5.0f, 1.7f })
            {
                var line = new float[(int)(ms * 0.001f * SR)];
                int p = 0;
                for (int i = 0; i < wet.Length; i++)
                {
                    float delayed = line[p];
                    float x = wet[i];
                    float y = delayed - 0.5f * x;
                    line[p] = x + 0.5f * y;
                    p = (p + 1) % line.Length;
                    wet[i] = y;
                }
            }
            for (int i = 0; i < d.Length; i++) d[i] += wet[i] * mix;
        }

        private static void Normalize(float[] d, float peak)
        {
            float max = 0f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            if (max <= 0f) return;
            float k = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        /// <summary>Sets the clip's peak level, so every effect sits at a
        /// deliberate loudness, and fades the last 15 ms so it never ends on
        /// a click.</summary>
        private static void Finish(float[] d, float peak)
        {
            Normalize(d, peak);
            int fade = Mathf.Min(d.Length, (int)(SR * 0.015f));
            for (int i = 0; i < fade; i++) d[d.Length - 1 - i] *= (float)i / fade;
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
