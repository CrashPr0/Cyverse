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
    /// Orchestrates Level 1 (Cyber Defense) as three phases, mirroring
    /// Level0Manager's proven flow:
    ///   Review    — learn at every station (dialogue + knowledge check)
    ///   Certify   — certify at the Threat Response Console
    ///   Complete  — "Certification confirmed", results screen
    /// On Start it discovers stations and the gate in the scene, so it works
    /// for both the procedural (bootstrap) and hand-built (editor) scenes.
    /// </summary>
    public class Level1Manager : MonoBehaviour
    {
        public enum Phase { Review, Certify, Complete }

        public static Level1Manager Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Review;

        private readonly List<StationSetup> stations = new List<StationSetup>();
        private Level1Gate gate;
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
            if (VisualDirector.Instance == null) gameObject.AddComponent<VisualDirector>();

            var cam = Camera.main;
            if (cam != null && cam.GetComponent<FirstPersonHands>() == null)
                cam.gameObject.AddComponent<FirstPersonHands>();
            if (MainMenu.Instance == null) gameObject.AddComponent<MainMenu>();
            if (Audio.AmbientHum.Instance == null) gameObject.AddComponent<Audio.AmbientHum>();
            if (GlossaryPanel.Instance == null) gameObject.AddComponent<GlossaryPanel>();

            stations.AddRange(FindObjectsOfType<StationSetup>());
            gate = FindObjectOfType<Level1Gate>();
            if (gate != null) gate.Completed += CompleteLevel;
            else Debug.LogWarning(
                "Level1Manager: no Level1Gate in this scene — the level will " +
                "complete when all stations are reviewed. Add one via the editor " +
                "menu: CyVerse > Add Threat Response Console.");

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();

            // Title screen first; the intro dialogue starts once it's dismissed.
            if (MainMenu.Instance != null)
                MainMenu.Instance.Show(BeginIntro);
            else
                BeginIntro();
        }

        private void BeginIntro()
        {
            startTime = Time.time; // don't count time spent on the title screen

            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Play(Level1Content.Intro(), UpdateObjective);
            else
                UpdateObjective();
        }

        /// <summary>Called by StationSetup when a station's review loop finishes.</summary>
        public void NotifyStationReviewed()
        {
            UpdateObjective();
            if (CurrentPhase != Phase.Review) return;
            if (ReviewedCount < stations.Count || stations.Count == 0) return;

            if (gate != null) BeginCertify();
            else CompleteLevel(); // no gate in this scene — finish directly
        }

        private void BeginCertify()
        {
            CurrentPhase = Phase.Certify;
            gate.Activate();
            UpdateObjective();

            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Play(Level1Content.AllReviewed());
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
                    HudUI.Instance.SetProgress(ReviewedCount, stations.Count);
                    break;
                case Phase.Certify:
                    HudUI.Instance.ShowObjective("Objective: Certify at the Threat Response Console");
                    HudUI.Instance.SetProgress(stations.Count, stations.Count, "✓");
                    break;
                case Phase.Complete:
                    HudUI.Instance.ShowObjective("LEVEL 1 COMPLETE");
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

            // Send-off: a gold shower where the player finished.
            Vector3 burstPos = gate != null
                ? gate.transform.position + Vector3.up * 2.5f
                : (Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 2f : Vector3.up * 2f);
            BurstFX.Spawn(burstPos, new Color(0.90f, 0.66f, 0.14f), 70, 3.4f, 1.3f);

            if (ResultsScreen.Instance != null)
                ResultsScreen.Instance.Show(
                    ScoreSystem.Score, ScoreSystem.QuizCorrect, ScoreSystem.QuizTotal,
                    Time.time - startTime,
                    headerText: "LEVEL 1 COMPLETE",
                    grantedLine: "Certification Confirmed — SOC Analyst",
                    nextMissionText: "Level 2 — Digital Forensics  (in development)",
                    replaySuffix: "Level 1");
        }
    }
}
