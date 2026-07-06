using UnityEngine;
using Cyverse.Core;

namespace Cyverse.Audio
{
    /// <summary>
    /// Room tone: plays the procedurally generated facility hum on loop so the
    /// space never sits in dead silence, plus an occasional soft, randomly
    /// pitched "server blip" for texture. Volume rides the master channel
    /// (AudioListener), so the existing settings menu already controls it.
    /// </summary>
    public class AmbientHum : MonoBehaviour
    {
        public static AmbientHum Instance { get; private set; }

        [Range(0f, 1f)] public float volume = 0.20f;
        public Vector2 blipIntervalRange = new Vector2(14f, 32f);

        private AudioSource blipSrc;
        private AudioClip blip;
        private float nextBlip;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            var src = gameObject.AddComponent<AudioSource>();
            src.clip = ProceduralAudio.AmbientLoop();
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = volume;
            src.Play();

            blipSrc = gameObject.AddComponent<AudioSource>();
            blipSrc.playOnAwake = false;
            blipSrc.spatialBlend = 0f;
            blip = ProceduralAudio.Click();
            ScheduleBlip();
        }

        void Update()
        {
            if (Time.unscaledTime < nextBlip) return;
            ScheduleBlip();
            // Stay quiet while a pause-style menu is up.
            if (GameState.MenuOpen || GameState.GlossaryOpen) return;
            blipSrc.pitch = Random.Range(0.55f, 1.4f);
            blipSrc.PlayOneShot(blip, 0.06f);
        }

        private void ScheduleBlip()
        {
            nextBlip = Time.unscaledTime + Random.Range(blipIntervalRange.x, blipIntervalRange.y);
        }
    }
}
