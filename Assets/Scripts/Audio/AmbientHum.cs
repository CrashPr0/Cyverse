using UnityEngine;

namespace Cyverse.Audio
{
    /// <summary>
    /// Room tone: plays the procedurally generated facility hum on loop so the
    /// space never sits in dead silence. Volume rides the master channel
    /// (AudioListener), so the existing settings menu already controls it.
    /// </summary>
    public class AmbientHum : MonoBehaviour
    {
        public static AmbientHum Instance { get; private set; }

        [Range(0f, 1f)] public float volume = 0.20f;

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
        }
    }
}
