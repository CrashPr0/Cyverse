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
        private AudioClip click, confirm, deny, taskComplete;
        private AudioClip[] footsteps;
        private int lastSynthStep = -1;
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

            footsteps = new AudioClip[4];
            for (int i = 0; i < footsteps.Length; i++) footsteps[i] = ProceduralAudio.Footstep(i);
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
            AudioClip clip = clips.Length > 0
                ? clips[NextIndex(clips.Length, ref lastStep)]
                : footsteps[NextIndex(footsteps.Length, ref lastSynthStep)];
            // Light pitch/volume jitter keeps repeats from sounding robotic;
            // recorded clips get less, since they already vary.
            float spread = clips.Length > 0 ? 0.05f : 0.08f;
            stepSrc.pitch = Random.Range(1f - spread, 1f + spread);
            stepSrc.PlayOneShot(clip, Vol * footstepVolume * Random.Range(0.85f, 1.15f));
        }

        /// <summary>A random index, never the same one twice in a row.</summary>
        private static int NextIndex(int count, ref int last)
        {
            int i = Random.Range(0, count);
            if (count > 1 && i == last) i = (i + 1 + Random.Range(0, count - 1)) % count;
            return last = i;
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
        // Mix levels, relative to each other: the frequent UI tap sits
        // under the feedback sounds, and the task fanfare is the loudest.
        private const float ClickGain = 0.7f, ConfirmGain = 0.85f, DenyGain = 0.8f, TaskGain = 0.95f;

        public void PlayClick() { if (src != null) src.PlayOneShot(click, Vol * ClickGain); }
        public void PlayConfirm() { if (src != null) src.PlayOneShot(confirm, Vol * ConfirmGain); }
        public void PlayTaskComplete() { if (src != null) src.PlayOneShot(taskComplete, Vol * TaskGain); }
        public void PlayDeny() { if (src != null) src.PlayOneShot(deny, Vol * DenyGain); }

        // Streak steps climb a major scale (semitones 0, 2, 4, 5, 7), so a
        // long streak tops out a fifth up rather than squealing.
        private static readonly int[] StreakSemitones = { 0, 2, 4, 5, 7 };

        /// <summary>Ascending confirm chime for answer streaks — each
        /// consecutive correct answer (up to level 5) rings a step higher.</summary>
        public void PlayStreak(int level)
        {
            if (comboSrc == null) return;
            int step = StreakSemitones[Mathf.Clamp(level - 1, 0, StreakSemitones.Length - 1)];
            comboSrc.pitch = Mathf.Pow(2f, step / 12f);
            comboSrc.PlayOneShot(confirm, Vol * ConfirmGain);
        }
    }
}
