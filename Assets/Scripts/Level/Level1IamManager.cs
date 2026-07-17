using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 1 (I/AM) flow, implementing the level template with the gamified
    /// task room:
    ///   Watch    — the briefing screen must be viewed once; the divider door
    ///              unlocks on first completion (video stays repeatable)
    ///   Tasks    — four hands-on tasks: Badge Enrollment (Identification,
    ///              always first — the others gate on it), the MFA Vault
    ///              (Authentication), Data Triage (Authorization), and the
    ///              Audit Hunt (Accountability)
    ///   Exam     — the Certification Exam terminal unlocks: the four
    ///              knowledge checks as one boss check
    ///   Complete — level marked done (unlocking Level 2 in the Hub), results
    ///              shown, exit door to the Hub opens.
    /// Discovers its pieces from the scene, so procedural and hand-built
    /// scenes both work; scenes from before the task rework (StationSetup
    /// stations, no task components) fall back to the legacy review flow.
    /// </summary>
    public class Level1IamManager : MonoBehaviour
    {
        public enum Phase { Watch, Tasks, Exam, Complete }

        public static Level1IamManager Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Watch;

        private BadgeStation badge;
        private MfaGauntlet gauntlet;
        private SortingStation sorting;
        private AuditStation audit;
        private CertExamStation exam;
        private readonly List<StationSetup> legacyStations = new List<StationSetup>();

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
            if (TypingChallenge.Instance == null) gameObject.AddComponent<TypingChallenge>();
            if (ResultsScreen.Instance == null) gameObject.AddComponent<ResultsScreen>();
            if (VisualDirector.Instance == null) gameObject.AddComponent<VisualDirector>();

            var cam = Camera.main;
            if (cam != null && cam.GetComponent<FirstPersonHands>() == null)
                cam.gameObject.AddComponent<FirstPersonHands>();
            if (Audio.AmbientHum.Instance == null) gameObject.AddComponent<Audio.AmbientHum>();
            if (GlossaryPanel.Instance == null) gameObject.AddComponent<GlossaryPanel>();

            badge = FindObjectOfType<BadgeStation>();
            gauntlet = FindObjectOfType<MfaGauntlet>();
            sorting = FindObjectOfType<SortingStation>();
            audit = FindObjectOfType<AuditStation>();
            exam = FindObjectOfType<CertExamStation>();

            if (badge != null) badge.Completed += OnTaskCompleted;
            if (gauntlet != null) gauntlet.Completed += OnTaskCompleted;
            if (sorting != null) sorting.Completed += OnTaskCompleted;
            if (audit != null) audit.Completed += OnTaskCompleted;
            if (exam != null) exam.Completed += CompleteLevel;

            // Scenes saved before the task rework: no task components, but
            // StationSetup stations — keep the old review flow working.
            if (TotalTasks == 0)
                legacyStations.AddRange(FindObjectsOfType<StationSetup>());

            briefing = FindObjectOfType<VideoStation>();
            taskDoor = FindObjectOfType<LockedDoor>();
            exitDoor = FindObjectOfType<HubDoor>();

            if (briefing != null) briefing.FirstCompleted += OnBriefingCompleted;
            else OnBriefingCompleted(); // no screen in scene — don't soft-lock

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();
            UpdateObjective();
        }

        private int TotalTasks =>
            (badge != null ? 1 : 0) + (gauntlet != null ? 1 : 0) +
            (sorting != null ? 1 : 0) + (audit != null ? 1 : 0);

        private int TasksDone =>
            (badge != null && badge.IsEnrolled ? 1 : 0) +
            (gauntlet != null && gauntlet.IsComplete ? 1 : 0) +
            (sorting != null && sorting.IsComplete ? 1 : 0) +
            (audit != null && audit.IsComplete ? 1 : 0);

        /// <summary>Ring/objective steps: the tasks plus the exam (if present).</summary>
        private int TotalSteps => TotalTasks + (exam != null ? 1 : 0);

        private void OnBriefingCompleted()
        {
            if (CurrentPhase != Phase.Watch) return;
            CurrentPhase = Phase.Tasks;

            if (taskDoor != null) taskDoor.Unlock();
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("Briefing complete — Task Room unlocked", new Color(0.30f, 1f, 0.45f));
            BurstFX.Spawn(taskDoor != null ? taskDoor.transform.position + Vector3.up * 2.5f : Vector3.up * 2f,
                new Color(0.30f, 1f, 0.45f), 30);
            UpdateObjective();
        }

        private void OnTaskCompleted()
        {
            UpdateObjective();
            if (CurrentPhase != Phase.Tasks) return;
            if (TasksDone < TotalTasks || TotalTasks == 0) return;

            if (exam != null)
            {
                CurrentPhase = Phase.Exam;
                exam.Activate();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("All tasks complete — the Certification Exam is unlocked",
                        new Color(0.90f, 0.66f, 0.14f));
                UpdateObjective();
            }
            else
            {
                CompleteLevel(); // no exam in this scene — finish directly
            }
        }

        /// <summary>Legacy path: wired to StationSetup.onReviewed in scenes
        /// built before the task rework.</summary>
        public void NotifyStationReviewed()
        {
            UpdateObjective();
            if (CurrentPhase != Phase.Tasks || legacyStations.Count == 0) return;
            int n = 0;
            foreach (var s in legacyStations) if (s.IsReviewed) n++;
            if (n >= legacyStations.Count) CompleteLevel();
        }

        private void UpdateObjective()
        {
            if (HudUI.Instance == null) return;
            switch (CurrentPhase)
            {
                case Phase.Watch:
                    HudUI.Instance.ShowObjective("Objective: Watch the security briefing  (E to play, ←/→ to scrub)");
                    HudUI.Instance.SetProgress(0, Mathf.Max(1, TotalSteps), "▶");
                    break;
                case Phase.Tasks:
                    if (legacyStations.Count > 0)
                    {
                        int n = 0;
                        foreach (var s in legacyStations) if (s.IsReviewed) n++;
                        HudUI.Instance.ShowObjective($"Objective: Review the I/AM stations  ({n}/{legacyStations.Count})");
                        HudUI.Instance.SetProgress(n, legacyStations.Count);
                    }
                    else if (badge != null && !badge.IsEnrolled)
                    {
                        HudUI.Instance.ShowObjective("Objective: Enroll at the ID kiosk — everything starts with identity");
                        HudUI.Instance.SetProgress(TasksDone, TotalSteps);
                    }
                    else
                    {
                        HudUI.Instance.ShowObjective($"Objective: Complete the training tasks  ({TasksDone}/{TotalTasks})");
                        HudUI.Instance.SetProgress(TasksDone, TotalSteps);
                    }
                    break;
                case Phase.Exam:
                    HudUI.Instance.ShowObjective("Objective: Pass the Certification Exam");
                    HudUI.Instance.SetProgress(TotalTasks, TotalSteps);
                    break;
                case Phase.Complete:
                    HudUI.Instance.ShowObjective("LEVEL 1 COMPLETE — exit to the Hub");
                    HudUI.Instance.SetProgress(TotalSteps, Mathf.Max(1, TotalSteps), "✓");
                    break;
            }
        }

        private void CompleteLevel()
        {
            if (CurrentPhase == Phase.Complete) return;
            CurrentPhase = Phase.Complete;

            LevelProgress.MarkCompleted(1); // unlocks Level 2 in the Hub
            GameState.LevelComplete = true;
            UpdateObjective();
            FirstPersonController.LockCursor(false);
            if (exitDoor != null) exitDoor.SetUnlocked(true);

            Vector3 burstPos = exitDoor != null
                ? exitDoor.transform.position + Vector3.up * 2.5f
                : (Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 2f : Vector3.up * 2f);
            BurstFX.Spawn(burstPos, new Color(0.90f, 0.66f, 0.14f), 70, 3.4f, 1.3f);

            if (ResultsScreen.Instance != null)
                ResultsScreen.Instance.Show(
                    ScoreSystem.Score, ScoreSystem.QuizCorrect, ScoreSystem.QuizTotal,
                    Time.time - startTime,
                    headerText: "LEVEL 1 COMPLETE",
                    grantedLine: "I/AM Training Certified",
                    nextMissionText: "Level 2 — Cyber Defense is now unlocked in the Hub.",
                    replaySuffix: "Level 1");
        }
    }
}
