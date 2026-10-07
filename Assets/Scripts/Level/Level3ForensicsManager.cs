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
    ///   Retrieve    — take the seized device out of the evidence locker (south
    ///                 wall, the SOC handoff); needed before Evidence Intake
    ///   Watch       — analyst briefing (query syntax 101); unlocks the door
    ///   Investigate — solve two linked cases (14 findings) at the terminal
    ///   Complete    — level persisted, results, exit celebrated.
    /// Same discovery/self-heal pattern as the other level managers.
    /// </summary>
    public class Level3ForensicsManager : MonoBehaviour
    {
        public enum Phase { Watch, Investigate, Report, Complete }

        public static Level3ForensicsManager Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Watch;
        public bool ReportReady => custodyForm != null && custodyForm.IsComplete &&
            console != null && console.AllComplete;
        public bool ReportSubmitted { get; private set; }

        private ForensicsConsole console;
        private VideoStation briefing;
        private LockedDoor taskDoor;
        private HubDoor exitDoor;
        private ChainOfCustodyForm custodyForm;
        private ChainOfCustodyStation custodyStation;
        private EvidenceLocker locker;
        private float startTime;

        private static readonly Color GuideGold = new Color(0.90f, 0.66f, 0.14f);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            // This level now has a carried item (the device from the locker).
            LevelMissionRuntime.ResetScene(clearCarried: true);
        }

        void Start()
        {
            startTime = Time.time;

            LevelMissionRuntime.EnsureSharedRuntime(gameObject, showEvidenceInventory: true);

            if (!SocProgress.TryGetEvidence(out var receivedEvidence))
                Debug.LogWarning("[DF HANDOFF] Level 3 loaded without structured SOC evidence; using the training image.");
            else
                Debug.Log($"[DF HANDOFF] Received {receivedEvidence.inventoryItem} from {receivedEvidence.computer}.");

            Level3ForensicsSceneRealization.Bindings scene =
                Level3ForensicsSceneRealization.Realize(gameObject);
            console = scene.console;
            briefing = scene.briefing;
            taskDoor = scene.taskDoor;
            exitDoor = scene.exitDoor;
            custodyForm = scene.custodyForm;
            custodyStation = scene.custodyStation;
            // Procedural builds already have the locker; this covers any other way the
            // scene was assembled.
            locker = Level3ForensicsSceneFactory.BuildEvidenceLocker();
            if (locker != null) locker.Retrieved += OnDeviceRetrieved;

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
                HudUI.Instance.ShowToast("SOC EVIDENCE RECEIVED — the " + receivedEvidence.computer +
                    " device is waiting in the evidence locker.", new Color(0.30f, 1f, 0.55f));
            UpdateObjective();
        }

        private void OnDestroy()
        {
            if (console != null && console.Cases != null)
            {
                foreach (InvestigationCase investigation in console.Cases)
                {
                    if (investigation == null) continue;
                    investigation.QuestionAnswered -= UpdateObjective;
                    investigation.CaseCompleted -= OnCaseCompleted;
                }
            }
            if (custodyForm != null)
            {
                custodyForm.Changed -= UpdateObjective;
                custodyForm.Completed -= OnCustodyCompleted;
            }
            if (locker != null) locker.Retrieved -= OnDeviceRetrieved;
            if (Instance == this) Instance = null;
        }

        private bool DeviceRetrieved => locker == null || locker.DeviceRetrieved;

        private void OnDeviceRetrieved()
        {
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast(
                    CurrentPhase == Phase.Watch
                        ? "DEVICE RETRIEVED — watch the analyst briefing to open the lab, then take it to EVIDENCE INTAKE."
                        : "DEVICE RETRIEVED — take it to EVIDENCE INTAKE and complete the custody form.",
                    new Color(0.30f, 1f, 0.55f));
            UpdateObjective();
        }

        private void OnCustodyCompleted()
        {
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("Custody accepted — take the device to the Investigation Desk",
                    new Color(0.30f, 1f, 0.55f));
            if (custodyStation != null)
                BurstFX.SpawnAbove(custodyStation.transform, new Color(0.30f, 1f, 0.55f),
                    28, minimumHeight: 2.0f);
            UpdateObjective();
        }

        private bool pendingComplete;

        private void OnCaseCompleted()
        {
            if (ReportReady)
            {
                CurrentPhase = Phase.Report;
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("CASEWORK COMPLETE — submit the final report at the REPORT DESK",
                        new Color(0.90f, 0.66f, 0.14f));
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

        public void SubmitReport()
        {
            if (ReportSubmitted || !ReportReady) return;
            ReportSubmitted = true;
            pendingComplete = true;
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("FORENSIC REPORT SUBMITTED — case closed",
                    new Color(0.30f, 1f, 0.55f));
            UpdateObjective();
        }

        private float guidanceTimer;

        void Update()
        {
            if (pendingComplete && !GameState.AnyMenuOpen)
            {
                pendingComplete = false;
                CompleteLevel();
            }

            // The locker is the only thing this level points at; the beacon hides itself
            // up close and is cleared once the device is out.
            guidanceTimer += Time.deltaTime;
            if (guidanceTimer < 0.4f) return;
            guidanceTimer = 0f;
            if (locker != null && !locker.DeviceRetrieved)
            {
                ObjectiveBeacon.Ensure().PointAt(locker.transform, "RETRIEVE THE DEVICE", GuideGold);
                beaconShown = true;
            }
            else if (beaconShown)
            {
                ObjectiveBeacon.Ensure().Hide();
                beaconShown = false;
            }
        }

        private bool beaconShown;

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
            bool retrieved = DeviceRetrieved;
            // device retrieval + custody + case questions + report submission
            int workflowTotal = total + 3;
            int workflowDone = done + (retrieved ? 1 : 0) + (custodyComplete ? 1 : 0) + (ReportSubmitted ? 1 : 0);

            switch (CurrentPhase)
            {
                case Phase.Watch:
                    if (!retrieved)
                        HudUI.Instance.ShowObjective(
                            "Objective: Retrieve the seized device from the EVIDENCE LOCKER  (south wall, by the entrance)");
                    else
                        HudUI.Instance.ShowObjective("Objective: Watch the analyst briefing  (E to play, ←/→ to scrub)");
                    HudUI.Instance.SetProgress(workflowDone, workflowTotal, "▶");
                    break;
                case Phase.Investigate:
                    if (!retrieved)
                        HudUI.Instance.ShowObjective(
                            "Objective: Retrieve the seized device from the EVIDENCE LOCKER  (south wall, by the entrance)");
                    else if (!custodyComplete)
                        HudUI.Instance.ShowObjective(
                            $"Objective: Complete the chain-of-custody form at EVIDENCE INTAKE  ({custodyForm.SelectedCount}/{custodyForm.FieldCount} blanks)");
                    else
                        HudUI.Instance.ShowObjective($"Objective: Solve {caseName} at the Investigation Desk  ({done}/{total})");
                    HudUI.Instance.SetProgress(workflowDone, workflowTotal);
                    break;
                case Phase.Report:
                    HudUI.Instance.ShowObjective("Objective: Submit the final forensic report at the REPORT DESK");
                    HudUI.Instance.SetProgress(workflowDone, workflowTotal, "!");
                    break;
                case Phase.Complete:
                    HudUI.Instance.ShowObjective("LEVEL 3 COMPLETE — exit to the Hub");
                    HudUI.Instance.SetProgress(workflowTotal, workflowTotal, "[OK]");
                    break;
            }
            UpdateTaskList(done, total, custodyComplete, retrieved);
        }

        private void UpdateTaskList(int answered, int total, bool custodyComplete, bool retrieved)
        {
            TaskListPanel list = TaskListPanel.Ensure(gameObject);
            list.SetHeader("DIGITAL FORENSICS");
            bool watched = CurrentPhase != Phase.Watch;
            list.Show(new List<TaskListPanel.Task>
            {
                new TaskListPanel.Task("Retrieve device from evidence locker", retrieved, !retrieved),
                new TaskListPanel.Task("Watch analyst briefing", watched, retrieved && !watched),
                new TaskListPanel.Task("Chain of custody  (4 fields)", custodyComplete,
                    watched && retrieved && !custodyComplete),
                new TaskListPanel.Task($"Investigate cases  ({answered}/{total})",
                    answered >= total, watched && custodyComplete && answered < total),
                new TaskListPanel.Task("Submit forensic report", ReportSubmitted,
                    CurrentPhase == Phase.Report && !ReportSubmitted),
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
            if (!ReportSubmitted)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Submit the final report at the REPORT DESK before closing the case.",
                        new Color(1f, 0.55f, 0.4f));
                return;
            }
            CurrentPhase = Phase.Complete;

            UpdateObjective();
            LevelMissionRuntime.Complete(new LevelMissionRuntime.Completion
            {
                levelNumber = 3,
                exitDoor = exitDoor,
                accent = new Color(0.90f, 0.66f, 0.14f),
                elapsedSeconds = Time.time - startTime,
                headerText = "LEVEL 3 COMPLETE",
                grantedLine = "Case Closed — Digital Forensics Certified",
                nextMissionText = "Level 4 — Cyber Attack is now unlocked in the Hub.",
                replaySuffix = "Level 3",
                parScore = 1800,
            });
        }
    }
}
