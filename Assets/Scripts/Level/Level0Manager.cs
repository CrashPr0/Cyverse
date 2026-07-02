using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Dialogue;
using Cyverse.Interaction;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Orchestrates Level 0 as three phases, per the CyVerse Script:
    ///   Review        — learn at every station (dialogue + knowledge check)
    ///   Authenticate  — face-scan at the Security Scanner
    ///   Complete      — "Access Granted", results screen
    /// On Start it discovers stations and the scanner in the scene, so it works
    /// for both the procedural (bootstrap) and hand-built (editor) scenes.
    /// </summary>
    public class Level0Manager : MonoBehaviour
    {
        public enum Phase { Review, Authenticate, Complete }

        public static Level0Manager Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Review;

        private readonly List<StationSetup> stations = new List<StationSetup>();
        private FaceScanner scanner;
        private float startTime;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            // Runs in both bootstrap and hand-built scenes, so global state is
            // reset here (statics survive scene reloads and disabled domain reload).
            GameState.Reset();
            ScoreSystem.Reset();
            Time.timeScale = 1f;
            // Shader animation on by default; AccessibilitySettings re-applies
            // the user's Reduce Motion preference right after.
            Shader.SetGlobalFloat("_CyMotion", 1f);
        }

        void Start()
        {
            startTime = Time.time;

            // Self-heal scenes saved before these systems existed (they're
            // invisible UI singletons, so adding them is always safe).
            if (Quiz.QuizSystem.Instance == null) gameObject.AddComponent<Quiz.QuizSystem>();
            if (ResultsScreen.Instance == null) gameObject.AddComponent<ResultsScreen>();

            stations.AddRange(FindObjectsOfType<StationSetup>());
            scanner = FindObjectOfType<FaceScanner>();
            if (scanner != null) scanner.Completed += CompleteLevel;
            else Debug.LogWarning(
                "Level0Manager: no SecurityScanner in this scene — the level will " +
                "complete when all stations are reviewed. Add one via the editor " +
                "menu: CyVerse > Add Security Scanner.");

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();

            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Play(Level0Content.Intro(), UpdateObjective);
            else
                UpdateObjective();
        }

        /// <summary>Called by StationSetup when a station's review loop finishes.</summary>
        public void NotifyStationReviewed()
        {
            UpdateObjective();
            if (CurrentPhase != Phase.Review) return;
            if (ReviewedCount < stations.Count || stations.Count == 0) return;

            if (scanner != null) BeginAuthenticate();
            else CompleteLevel(); // no scanner in this scene — finish directly
        }

        private void BeginAuthenticate()
        {
            CurrentPhase = Phase.Authenticate;
            scanner.Activate();
            UpdateObjective();

            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Play(Level0Content.AllReviewed());
        }

        private int ReviewedCount
        {
            get
            {
                int n = 0;
                foreach (var s in stations) if (s.IsReviewed) n++;
                return n;
            }
        }

        private void UpdateObjective()
        {
            if (HudUI.Instance == null) return;
            switch (CurrentPhase)
            {
                case Phase.Review:
                    HudUI.Instance.ShowObjective(
                        $"Objective: Review all stations  ({ReviewedCount}/{stations.Count})");
                    break;
                case Phase.Authenticate:
                    HudUI.Instance.ShowObjective("Objective: Authenticate at the Security Scanner");
                    break;
                case Phase.Complete:
                    HudUI.Instance.ShowObjective("LEVEL 0 COMPLETE");
                    break;
            }
        }

        private void CompleteLevel()
        {
            if (CurrentPhase == Phase.Complete) return;
            CurrentPhase = Phase.Complete;

            GameState.LevelComplete = true;
            UpdateObjective();
            FirstPersonController.LockCursor(false);

            if (ResultsScreen.Instance != null)
                ResultsScreen.Instance.Show(
                    ScoreSystem.Score, ScoreSystem.QuizCorrect, ScoreSystem.QuizTotal,
                    Time.time - startTime);
        }
    }
}
