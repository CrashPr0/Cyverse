using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Settings;
using Cyverse.UI;

namespace Cyverse.Dialogue
{
    /// <summary>
    /// One spoken / captioned beat. The audio clip is optional so the game is
    /// fully playable with captions only until real voiceover is recorded.
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea] public string text;
        public AudioClip clip;
        public float minDuration = 3f;

        public DialogueLine(string speaker, string text, AudioClip clip = null, float minDuration = 3f)
        {
            this.speaker = speaker;
            this.text = text;
            this.clip = clip;
            this.minDuration = minDuration;
        }
    }

    /// <summary>
    /// Plays an ordered list of <see cref="DialogueLine"/>s: shows the caption,
    /// plays optional voiceover, and waits for the line's minimum time (or until
    /// the player presses the advance key) before moving on.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        public KeyCode advanceKey = KeyCode.Space;

        private AudioSource voice;
        private Coroutine running;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f; // 2D narration
        }

        /// <summary>Play a sequence; <paramref name="onComplete"/> fires when it ends.</summary>
        public void Play(List<DialogueLine> lines, Action onComplete = null)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Run(lines, onComplete));
        }

        private IEnumerator Run(List<DialogueLine> lines, Action onComplete)
        {
            GameState.DialogueActive = true;

            if (lines != null)
            {
                foreach (DialogueLine line in lines)
                {
                    string label = string.IsNullOrEmpty(line.speaker)
                        ? line.text
                        : $"{line.speaker}: {line.text}";

                    if (HudUI.Instance != null) HudUI.Instance.ShowCaption(label);

                    if (line.clip != null)
                    {
                        voice.volume = AccessibilitySettings.Instance != null
                            ? AccessibilitySettings.Instance.VoiceVolume
                            : 1f;
                        voice.clip = line.clip;
                        voice.Play();
                    }

                    float elapsed = 0f;
                    float minWait = Mathf.Max(line.minDuration, line.clip != null ? line.clip.length : 0f);
                    while (true)
                    {
                        elapsed += Time.deltaTime;
                        bool audioDone = line.clip == null || !voice.isPlaying;

                        if (Input.GetKeyDown(advanceKey)) { voice.Stop(); break; }
                        if (elapsed >= minWait && audioDone) break;
                        yield return null;
                    }

                    yield return null; // tiny gap so the advance press isn't double-counted
                }
            }

            if (HudUI.Instance != null) HudUI.Instance.HideCaption();
            GameState.DialogueActive = false;
            running = null;
            onComplete?.Invoke();
        }
    }
}
