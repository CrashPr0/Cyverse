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
        private AudioClip footstep, click, confirm, deny, taskComplete;
        private AudioClip[] stepClips;
        private string stepScene;
        private int lastStep = -1;

        /// <summary>Footstep clips live in Resources/Audio/Footsteps/Default
        /// (any number, any names). A sibling folder named after a scene, e.g.
        /// Footsteps/Level2_CyberDefense, replaces them in that level. With
        /// no clips at all the built-in synthesized step plays. See the
        /// README in Resources/Audio/Footsteps.</summary>
        public const string FootstepFolder = "Audio/Footsteps";

        /// <summary>Overall footstep loudness, before the SFX volume setting.</summary>
        public float footstepVolume = 0.65f;

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
            taskComplete = ProceduralAudio.TaskComplete();
        }

        private float Vol => AccessibilitySettings.Instance != null
            ? AccessibilitySettings.Instance.SfxVolume
            : 1f;

        public void PlayFootstep()
        {
            if (stepSrc == null) return;
            AudioClip[] clips = FootstepClips();
            AudioClip clip = footstep;
            if (clips.Length > 0)
            {
                // Random, but never the same recording twice in a row.
                int i = Random.Range(0, clips.Length);
                if (clips.Length > 1 && i == lastStep) i = (i + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
                lastStep = i;
                clip = clips[i];
            }
            // Light pitch/volume jitter keeps repeats from sounding robotic;
            // recorded clips get less, since they already vary.
            float spread = clips.Length > 0 ? 0.05f : 0.14f;
            stepSrc.pitch = Random.Range(1f - spread, 1f + spread);
            stepSrc.PlayOneShot(clip, Vol * footstepVolume * Random.Range(0.85f, 1.15f));
        }

        /// <summary>The clips for the current scene, loaded once per scene.</summary>
        private AudioClip[] FootstepClips()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (stepClips != null && stepScene == scene) return stepClips;
            stepScene = scene;
            lastStep = -1;
            AudioClip[] perLevel = Resources.LoadAll<AudioClip>(FootstepFolder + "/" + scene);
            stepClips = perLevel.Length > 0 ? perLevel : Resources.LoadAll<AudioClip>(FootstepFolder + "/Default");
            return stepClips;
        }
        public void PlayClick() { if (src != null) src.PlayOneShot(click, Vol); }
        public void PlayConfirm() { if (src != null) src.PlayOneShot(confirm, Vol); }
        public void PlayTaskComplete() { if (src != null) src.PlayOneShot(taskComplete, Vol); }
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
