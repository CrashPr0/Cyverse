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

        [Tooltip("Typewriter reveal speed; 0 disables (instant text).")]
        public float charsPerSecond = 45f;

        // Browser TTS state: set true when a line is handed to the Web Speech
        // API, cleared by OnTtsEnd (SendMessage from the .jslib) or a skip.
        private bool ttsPending;

        /// <summary>Called from WebSpeech.jslib when the browser finishes speaking.</summary>
        public void OnTtsEnd(string _)
        {
            ttsPending = false;
        }

        private static float PitchFor(string speaker)
        {
            switch (speaker)
            {
                case "Security Guard": return 0.85f; // lower, authoritative
                case "System": return 1.15f;         // brighter, synthetic
                default: return 1.0f;
            }
        }

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
            Cyverse.Audio.Speech.Cancel(); // a restarted sequence must not talk over itself
            ttsPending = false;
            running = StartCoroutine(Run(lines, onComplete));
        }

        private IEnumerator Run(List<DialogueLine> lines, Action onComplete)
        {
            GameState.DialogueActive = true;

            if (lines != null)
            {
                foreach (DialogueLine line in lines)
                {
                    // The speaker header is always shown whole; only the body is
                    // typed out, so rich-text tags are never cut mid-tag.
                    string header = string.IsNullOrEmpty(line.speaker)
                        ? string.Empty
                        : $"<b><color=#5BC8FF>{line.speaker}</color></b>\n";
                    string body = line.text ?? string.Empty;

                    float voiceVol = AccessibilitySettings.Instance != null
                        ? AccessibilitySettings.Instance.VoiceVolume
                        : 1f;

                    bool usedTts = false;
                    if (line.clip != null)
                    {
                        voice.volume = voiceVol;
                        voice.clip = line.clip;
                        voice.Play();
                    }
                    else if (Cyverse.Audio.Speech.Available &&
                             (AccessibilitySettings.Instance == null || AccessibilitySettings.Instance.TtsEnabled))
                    {
                        // No recorded clip — let the browser read the line aloud.
                        Cyverse.Audio.Speech.Speak(body, 1f, PitchFor(line.speaker), voiceVol);
                        ttsPending = true;
                        usedTts = true;
                    }

                    // If the browser's end event never arrives, stop waiting on
                    // TTS after a generous reading-speed estimate.
                    float ttsBudget = 1.5f + body.Length / 11f;

                    float totalElapsed = 0f;

                    // Typewriter reveal; the advance key completes it instantly.
                    if (!AccessibilitySettings.ReduceMotion && charsPerSecond > 0f)
                    {
                        float shown = 0f;
                        while (shown < body.Length)
                        {
                            shown += charsPerSecond * Time.deltaTime;
                            totalElapsed += Time.deltaTime;
                            if (HudUI.Instance != null)
                                HudUI.Instance.ShowCaption(
                                    header + body.Substring(0, Mathf.Min((int)shown, body.Length)));
                            if (Input.GetKeyDown(advanceKey)) break;
                            yield return null;
                        }
                    }

                    if (HudUI.Instance != null) HudUI.Instance.ShowCaption(header + body);
                    yield return null; // the reveal-completing press must not also advance

                    // minDuration is how long the FULLY REVEALED text should hold
                    // before advancing. It must not share a clock with the
                    // typewriter above — a long line could take longer to type
                    // out than minDuration, which previously meant the wait-loop's
                    // elapsed check was already satisfied the instant the last
                    // character appeared, advancing with zero time to actually
                    // read it. totalElapsed keeps running (for the TTS timeout,
                    // which is measured from when speech started); holdElapsed is
                    // a fresh clock for the reading pause.
                    float holdElapsed = 0f;
                    float minWait = Mathf.Max(line.minDuration, line.clip != null ? line.clip.length : 0f);
                    while (true)
                    {
                        holdElapsed += Time.deltaTime;
                        totalElapsed += Time.deltaTime;
                        bool audioDone = line.clip == null || !voice.isPlaying;
                        bool ttsDone = !usedTts || !ttsPending || totalElapsed >= ttsBudget;

                        if (Input.GetKeyDown(advanceKey))
                        {
                            voice.Stop();
                            Cyverse.Audio.Speech.Cancel();
                            ttsPending = false;
                            break;
                        }
                        if (holdElapsed >= minWait && audioDone && ttsDone) break;
                        yield return null;
                    }
                    Cyverse.Audio.Speech.Cancel(); // budget-exceeded stragglers
                    ttsPending = false;

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
