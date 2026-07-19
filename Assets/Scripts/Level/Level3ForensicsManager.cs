using UnityEngine;
using Cyverse.Core;
using Cyverse.Forensics;
using Cyverse.Interaction;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 3 (Digital Forensics) flow:
    ///   Watch       — analyst briefing (query syntax 101); unlocks the door
    ///   Investigate — solve the 8-question case at the forensic terminal
    ///   Complete    — level persisted, results, exit celebrated.
    /// Same discovery/self-heal pattern as the other level managers.
    /// </summary>
    public class Level3ForensicsManager : MonoBehaviour
    {
        public enum Phase { Watch, Investigate, Complete }

        public static Level3ForensicsManager Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Watch;

        private ForensicsConsole console;
        private VideoStation briefing;
        private LockedDoor taskDoor;
        private HubDoor exitDoor;
        private float startTime;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            GameState.Reset();
            ScoreSystem.Reset();
            Time.timeScale = 1f;
            Shader.SetGlobalFloat("_CyMotion", 1f);
        }

        void Start()
        {
            startTime = Time.time;

            if (Quiz.QuizSystem.Instance == null) gameObject.AddComponent<Quiz.QuizSystem>();
            if (QueryTerminal.Instance == null) gameObject.AddComponent<QueryTerminal>();
            if (ResultsScreen.Instance == null) gameObject.AddComponent<ResultsScreen>();
            if (VisualDirector.Instance == null) gameObject.AddComponent<VisualDirector>();

            var cam = Camera.main;
            if (cam != null && cam.GetComponent<FirstPersonHands>() == null)
                cam.gameObject.AddComponent<FirstPersonHands>();
            if (Audio.AmbientHum.Instance == null) gameObject.AddComponent<Audio.AmbientHum>();
            if (GlossaryPanel.Instance == null) gameObject.AddComponent<GlossaryPanel>();

            console = FindObjectOfType<ForensicsConsole>();
            briefing = FindObjectOfType<VideoStation>();
            taskDoor = FindObjectOfType<LockedDoor>();
            exitDoor = FindObjectOfType<HubDoor>();

            if (exitDoor != null) exitDoor.SetUnlocked(true); // never trap the player

            if (console != null && console.Cases != null)
            {
                foreach (var c in console.Cases)
                {
                    c.QuestionAnswered += UpdateObjective;
                    // Don't complete while the terminal modal is open (results
                    // would stack over it) — flag when the LAST case closes and
                    // finish once the screen is free. Case 1 closing instead
                    // celebrates and points at the new case file.
                    c.CaseCompleted += OnCaseCompleted;
                }
            }

            if (briefing != null) briefing.FirstCompleted += OnBriefingCompleted;
            else OnBriefingCompleted();

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();
            UpdateObjective();
        }

        private bool pendingComplete;

        private void OnCaseCompleted()
        {
            if (console != null && console.AllComplete)
            {
                pendingComplete = true;
            }
            else
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("CASE CLOSED — a new case file hit your desk",
                        new Color(0.90f, 0.66f, 0.14f));
                if (console != null)
                    BurstFX.Spawn(console.transform.position + Vector3.up * 2.2f,
                        new Color(0.90f, 0.66f, 0.14f), 40);
            }
            UpdateObjective();
        }

        void Update()
        {
            if (pendingComplete && !GameState.AnyMenuOpen)
            {
                pendingComplete = false;
                CompleteLevel();
            }
        }

        private void OnBriefingCompleted()
        {
            if (CurrentPhase != Phase.Watch) return;
            CurrentPhase = Phase.Investigate;

            if (taskDoor != null) taskDoor.Unlock();
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("Briefing complete — the SOC floor is open", new Color(0.30f, 1f, 0.45f));
            BurstFX.Spawn(taskDoor != null ? taskDoor.transform.position + Vector3.up * 2.5f : Vector3.up * 2f,
                new Color(0.30f, 1f, 0.45f), 30);
            UpdateObjective();
        }

        private void UpdateObjective()
        {
            if (HudUI.Instance == null) return;
            int total = console != null ? console.TotalQuestions : 14;
            int done = console != null ? console.TotalAnswered : 0;
            string caseName = console != null && console.ActiveCase != null ? console.ActiveCase.title : "the case";

            switch (CurrentPhase)
            {
                case Phase.Watch:
                    HudUI.Instance.ShowObjective("Objective: Watch the analyst briefing  (E to play, ←/→ to scrub)");
                    HudUI.Instance.SetProgress(0, total, "▶");
                    break;
                case Phase.Investigate:
                    HudUI.Instance.ShowObjective($"Objective: Solve {caseName} at the Investigation Desk  ({done}/{total})");
                    HudUI.Instance.SetProgress(done, total);
                    break;
                case Phase.Complete:
                    HudUI.Instance.ShowObjective("LEVEL 3 COMPLETE — exit to the Hub");
                    HudUI.Instance.SetProgress(total, total, "✓");
                    break;
            }
        }

        private void CompleteLevel()
        {
            if (CurrentPhase == Phase.Complete) return;
            CurrentPhase = Phase.Complete;

            LevelProgress.MarkCompleted(3); // unlocks Level 4 in the Hub
            GameState.LevelComplete = true;
            UpdateObjective();
            FirstPersonController.LockCursor(false);

            Vector3 burstPos = exitDoor != null
                ? exitDoor.transform.position + Vector3.up * 2.5f
                : (Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 2f : Vector3.up * 2f);
            BurstFX.Spawn(burstPos, new Color(0.90f, 0.66f, 0.14f), 70, 3.4f, 1.3f);

            if (ResultsScreen.Instance != null)
                ResultsScreen.Instance.Show(
                    ScoreSystem.Score, ScoreSystem.QuizCorrect, ScoreSystem.QuizTotal,
                    Time.time - startTime,
                    headerText: "LEVEL 3 COMPLETE",
                    grantedLine: "Case Closed — Digital Forensics Certified",
                    nextMissionText: "Level 4 — Cyber Attack  (in development)",
                    replaySuffix: "Level 3");
        }
    }
}
