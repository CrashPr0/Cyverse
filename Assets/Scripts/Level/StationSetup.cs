using System;
using System.Collections.Generic;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Dialogue;
using Cyverse.Interaction;
using Cyverse.Quiz;

namespace Cyverse.Level
{
    /// <summary>
    /// Configures a station from a serialized topic and drives its review loop:
    /// dialogue → knowledge-check question → points + completion feedback.
    /// Living on the station GameObject (instead of in the bootstrap) means
    /// hand-built / editor-authored scenes work the same as the runtime
    /// procedural scene: pick a topic in the Inspector and it loads its content.
    /// </summary>
    [RequireComponent(typeof(InteractableStation))]
    public class StationSetup : MonoBehaviour
    {
        // Shared across every level so Quiz/Glossary/Station plumbing needs no
        // per-level duplication — Level 0 uses IAM/CIA/NICE, Level 1 (Cyber
        // Defense) uses SIEM/EDR/INCIDENT. Extend here for future levels.
        public enum Topic { IAM, CIA, NICE, SIEM, EDR, INCIDENT }

        public Topic topic = Topic.IAM;
        public string prompt = "Inspect";

        [Tooltip("Points for completing this station's review (quiz points are separate).")]
        public int reviewPoints = 50;

        [Tooltip("Enabled when the station is reviewed (the green checkmark).")]
        public GameObject reviewedMark;

        [Tooltip("Dimmed to a calmer level once reviewed.")]
        public Light stationLight;
        public float completedLightIntensity = 1.2f;

        /// <summary>True once dialogue AND the knowledge check are done.</summary>
        public bool IsReviewed { get; private set; }

        // Level-specific hooks, wired by whichever scene factory builds this
        // station. Leaving all three null preserves the original Level 0
        // behavior exactly (IAM/CIA/NICE content, Level0Quiz, Level0Manager).
        public Func<List<DialogueLine>> contentProvider;
        public Func<QuizQuestion> quizProvider;
        public Action onReviewed;

        void Start()
        {
            var station = GetComponent<InteractableStation>();
            station.Configure(topic.ToString().ToLower(), prompt, Content());
            station.Completed += OnDialogueDone;
            if (reviewedMark != null) reviewedMark.SetActive(false);
        }

        private void OnDialogueDone(InteractableStation station)
        {
            var question = quizProvider != null ? quizProvider() : Level0Quiz.For(topic);
            if (QuizSystem.Instance != null)
                QuizSystem.Instance.Ask(question, OnQuizAnswered);
            else
                OnQuizAnswered(true); // no quiz system in scene — skip the check
        }

        private void OnQuizAnswered(bool correct)
        {
            if (IsReviewed) return;
            IsReviewed = true;

            ScoreSystem.Add(reviewPoints);
            if (reviewedMark != null) reviewedMark.SetActive(true);
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            if (stationLight != null) stationLight.intensity = completedLightIntensity;
            BurstFX.Spawn(transform.position + Vector3.up * 2.1f, new Color(0.30f, 1f, 0.45f), 26);

            int newTerms = GlossaryProgress.UnlockTopic(topic);
            if (newTerms > 0 && UI.HudUI.Instance != null)
            {
                string label = newTerms == 1 ? "entry" : "entries";
                UI.HudUI.Instance.ShowToast(
                    $"+{newTerms} Glossary {label} unlocked  [G]", new Color(0.90f, 0.66f, 0.14f));
            }

            if (onReviewed != null)
                onReviewed();
            else if (Level0Manager.Instance != null)
                Level0Manager.Instance.NotifyStationReviewed();
        }

        private List<DialogueLine> Content()
        {
            if (contentProvider != null) return contentProvider();

            // Legacy fallback for Level 0 stations built without an explicit
            // provider (the original SceneFactory/editor-built path).
            switch (topic)
            {
                case Topic.IAM: return Level0Content.IAM();
                case Topic.CIA: return Level0Content.CIA();
                case Topic.NICE: return Level0Content.Nice();
                default:
                    Debug.LogWarning($"StationSetup: no contentProvider set for topic {topic} " +
                                      "and it has no Level 0 fallback — showing no dialogue.");
                    return new List<DialogueLine>();
            }
        }
    }
}
