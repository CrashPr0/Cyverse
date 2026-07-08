using UnityEngine;
using Cyverse.Settings;

namespace Cyverse.Audio
{
    /// <summary>
    /// Central one-shot sound effects, scaled by the SFX accessibility channel
    /// (separate from master and voice). Clips are generated procedurally on
    /// Awake; swap in real AudioClips here when available.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx Instance { get; private set; }

        private AudioSource src;
        private AudioSource stepSrc;  // separate source so pitch jitter never bends UI sounds
        private AudioSource comboSrc; // separate source so streak pitch-ramp never bends confirm/click
        private AudioClip footstep, click, confirm, deny;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;

            stepSrc = gameObject.AddComponent<AudioSource>();
            stepSrc.playOnAwake = false;
            stepSrc.spatialBlend = 0f;

            comboSrc = gameObject.AddComponent<AudioSource>();
            comboSrc.playOnAwake = false;
            comboSrc.spatialBlend = 0f;

            footstep = ProceduralAudio.Footstep();
            click = ProceduralAudio.Click();
            confirm = ProceduralAudio.Confirm();
            deny = ProceduralAudio.Deny();
        }

        private float Vol => AccessibilitySettings.Instance != null
            ? AccessibilitySettings.Instance.SfxVolume
            : 1f;

        public void PlayFootstep()
        {
            if (stepSrc == null) return;
            // Pitch/volume jitter keeps repeated steps from sounding robotic.
            stepSrc.pitch = Random.Range(0.86f, 1.14f);
            stepSrc.PlayOneShot(footstep, Vol * Random.Range(0.55f, 0.75f));
        }
        public void PlayClick() { if (src != null) src.PlayOneShot(click, Vol); }
        public void PlayConfirm() { if (src != null) src.PlayOneShot(confirm, Vol); }
        public void PlayDeny() { if (src != null) src.PlayOneShot(deny, Vol); }

        /// <summary>Ascending confirm chime for answer streaks — each
        /// consecutive correct answer (up to level 5) rings a touch higher.</summary>
        public void PlayStreak(int level)
        {
            if (comboSrc == null) return;
            comboSrc.pitch = 1f + Mathf.Clamp(level - 1, 0, 4) * 0.12f;
            comboSrc.PlayOneShot(confirm, Vol);
        }
    }
}
