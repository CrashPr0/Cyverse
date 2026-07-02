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
        private AudioClip footstep, click, confirm, deny;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;

            footstep = ProceduralAudio.Footstep();
            click = ProceduralAudio.Click();
            confirm = ProceduralAudio.Confirm();
            deny = ProceduralAudio.Deny();
        }

        private float Vol => AccessibilitySettings.Instance != null
            ? AccessibilitySettings.Instance.SfxVolume
            : 1f;

        public void PlayFootstep() { if (src != null) src.PlayOneShot(footstep, Vol * 0.7f); }
        public void PlayClick() { if (src != null) src.PlayOneShot(click, Vol); }
        public void PlayConfirm() { if (src != null) src.PlayOneShot(confirm, Vol); }
        public void PlayDeny() { if (src != null) src.PlayOneShot(deny, Vol); }
    }
}
