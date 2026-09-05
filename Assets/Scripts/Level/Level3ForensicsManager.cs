using System.Collections.Generic;
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
    ///   Investigate — solve two linked cases (14 findings) at the terminal
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
        private ChainOfCustodyForm custodyForm;
        private ChainOfCustodyStation custodyStation;
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
            custodyForm = ChainOfCustodyForm.Ensure(gameObject);
            custodyStation = ChainOfCustodyStation.Ensure();
            if (ResultsScreen.Instance == null) gameObject.AddComponent<ResultsScreen>();
            if (VisualDirector.Instance == null) gameObject.AddComponent<VisualDirector>();
            if (FindObjectOfType<Level3ForensicsPolish>() == null)
                gameObject.AddComponent<Level3ForensicsPolish>();

            var cam = Camera.main;
            if (cam != null && cam.GetComponent<FirstPersonHands>() == null)
                cam.gameObject.AddComponent<FirstPersonHands>();
            if (Audio.AmbientHum.Instance == null) gameObject.AddComponent<Audio.AmbientHum>();
            if (GlossaryPanel.Instance == null) gameObject.AddComponent<GlossaryPanel>();
            EvidenceInventoryPanel.Ensure(gameObject).RefreshNow();

            if (!SocProgress.TryGetEvidence(out var receivedEvidence))
                Debug.LogError("[DF HANDOFF] Level 3 loaded without structured SOC evidence.");
            else
                Debug.Log($"[DF HANDOFF] Received {receivedEvidence.inventoryItem} from {receivedEvidence.computer}.");

            console = FindObjectOfType<ForensicsConsole>();
            briefing = FindObjectOfType<VideoStation>();
            taskDoor = FindObjectOfType<LockedDoor>();
            exitDoor = FindObjectOfType<HubDoor>();

            // Never trap the player: unlock every exit and make sure one
            // exists in the briefing room they spawn in (divider at z=2).
            HubDoor.EnsureReachableExit(2f, new Color(0.90f, 0.66f, 0.14f));
            if (exitDoor != null) exitDoor.SetUnlocked(true);

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
            custodyForm.Changed += UpdateObjective;
            custodyForm.Completed += OnCustodyCompleted;

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();
            if (receivedEvidence != null && HudUI.Instance != null)
                HudUI.Instance.ShowToast("SOC EVIDENCE RECEIVED — " + receivedEvidence.computer +
                    " disk image and chain of custody loaded.", new Color(0.30f, 1f, 0.55f));
            UpdateObjective();
        }

        private void OnDestroy()
        {
            if (custodyForm == null) return;
            custodyForm.Changed -= UpdateObjective;
            custodyForm.Completed -= OnCustodyCompleted;
        }

        private void OnCustodyCompleted()
        {
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("Custody accepted — forensic analysis is unlocked",
                    new Color(0.30f, 1f, 0.55f));
            if (custodyStation != null)
                BurstFX.SpawnAbove(custodyStation.transform, new Color(0.30f, 1f, 0.55f),
                    28, minimumHeight: 2.0f);
            UpdateObjective();
        }

        private bool pendingComplete;

        private void OnCaseCompleted()
        {
            if (console != null && console.AllComplete &&
                (custodyForm == null || custodyForm.IsComplete))
            {
                pendingComplete = true;
            }
            else
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("CASE CLOSED — a new case file hit your desk",
                        new Color(0.90f, 0.66f, 0.14f));
                if (console != null)
                    BurstFX.SpawnAbove(console.transform, new Color(0.90f, 0.66f, 0.14f),
                        40, minimumHeight: 2.2f);
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
                HudUI.Instance.ShowToast("Briefing complete — the Forensics Lab is open", new Color(0.30f, 1f, 0.45f));
            if (taskDoor != null) BurstFX.SpawnAbove(taskDoor.transform,
                new Color(0.30f, 1f, 0.45f), 30, minimumHeight: 2.5f);
            else BurstFX.Spawn(Vector3.up * 2f, new Color(0.30f, 1f, 0.45f), 30);
            UpdateObjective();
        }

        private void UpdateObjective()
        {
            if (HudUI.Instance == null) return;
            int total = console != null ? console.TotalQuestions : 14;
            int done = console != null ? console.TotalAnswered : 0;
            string caseName = console != null && console.ActiveCase != null ? console.ActiveCase.title : "the case";
            bool custodyComplete = custodyForm == null || custodyForm.IsComplete;
            int workflowTotal = total + 1;
            int workflowDone = done + (custodyComplete ? 1 : 0);

            switch (CurrentPhase)
            {
                case Phase.Watch:
                    HudUI.Instance.ShowObjective("Objective: Watch the analyst briefing  (E to play, ←/→ to scrub)");
                    HudUI.Instance.SetProgress(0, workflowTotal, "▶");
                    break;
                case Phase.Investigate:
                    if (!custodyComplete)
                        HudUI.Instance.ShowObjective(
                            $"Objective: Complete the chain-of-custody form at EVIDENCE INTAKE  ({custodyForm.SelectedCount}/{custodyForm.FieldCount} blanks)");
                    else
                        HudUI.Instance.ShowObjective($"Objective: Solve {caseName} at the Investigation Desk  ({done}/{total})");
                    HudUI.Instance.SetProgress(workflowDone, workflowTotal);
                    break;
                case Phase.Complete:
                    HudUI.Instance.ShowObjective("LEVEL 3 COMPLETE — exit to the Hub");
                    HudUI.Instance.SetProgress(workflowTotal, workflowTotal, "✓");
                    break;
            }
            UpdateTaskList(done, total, custodyComplete);
        }

        private void UpdateTaskList(int answered, int total, bool custodyComplete)
        {
            TaskListPanel list = TaskListPanel.Ensure(gameObject);
            list.SetHeader("DIGITAL FORENSICS");
            bool watched = CurrentPhase != Phase.Watch;
            list.Show(new List<TaskListPanel.Task>
            {
                new TaskListPanel.Task("Watch analyst briefing", watched, !watched),
                new TaskListPanel.Task("Chain of custody  (4 fields)", custodyComplete,
                    watched && !custodyComplete),
                new TaskListPanel.Task($"Investigate cases  ({answered}/{total})",
                    answered >= total, watched && custodyComplete && answered < total),
                new TaskListPanel.Task("Return to Hub", CurrentPhase == Phase.Complete,
                    CurrentPhase == Phase.Complete),
            });
        }

        private void CompleteLevel()
        {
            if (CurrentPhase == Phase.Complete) return;
            if (custodyForm != null && !custodyForm.IsComplete)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Complete the chain-of-custody record before closing the case.",
                        new Color(1f, 0.55f, 0.4f));
                return;
            }
            CurrentPhase = Phase.Complete;

            LevelProgress.MarkCompleted(3); // unlocks Level 4 in the Hub
            GameState.LevelComplete = true;
            UpdateObjective();
            FirstPersonController.LockCursor(false);

            if (exitDoor != null) BurstFX.SpawnAbove(exitDoor.transform,
                new Color(0.90f, 0.66f, 0.14f), 70, 3.4f, 1.3f, 2.5f);
            else BurstFX.Spawn(Camera.main != null
                ? Camera.main.transform.position + Camera.main.transform.forward * 2f : Vector3.up * 2f,
                new Color(0.90f, 0.66f, 0.14f), 70, 3.4f, 1.3f);

            if (ResultsScreen.Instance != null)
                ResultsScreen.Instance.Show(
                    ScoreSystem.Score, ScoreSystem.QuizCorrect, ScoreSystem.QuizTotal,
                    Time.time - startTime,
                    headerText: "LEVEL 3 COMPLETE",
                    grantedLine: "Case Closed — Digital Forensics Certified",
                    nextMissionText: "Level 4 — Cyber Attack  (in development)",
                    replaySuffix: "Level 3",
                    parScore: 1800);
        }
    }
}
