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
        private DropZone mfaSlot;
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

            LevelMissionRuntime.ResetScene(clearCarried: true);
        }

        void Start()
        {
            startTime = Time.time;

            LevelMissionRuntime.EnsureSharedRuntime(gameObject, showEvidenceInventory: false);
            if (TypingChallenge.Instance == null) gameObject.AddComponent<TypingChallenge>();

            // Both scene adapters are normalized through one realization seam
            // before the progression manager subscribes to anything.
            Level1IamSceneRealization.Bindings scene =
                Level1IamSceneRealization.Realize(gameObject);
            badge = scene.badge;
            gauntlet = scene.gauntlet;
            mfaSlot = scene.mfaSlot;
            sorting = scene.sorting;
            audit = scene.audit;
            exam = scene.exam;

            if (badge != null) badge.Completed += OnTaskCompleted;
            if (gauntlet != null) gauntlet.Completed += OnTaskCompleted;
            if (sorting != null) sorting.Completed += OnTaskCompleted;
            if (audit != null) audit.Completed += OnTaskCompleted;
            if (exam != null) exam.Completed += CompleteLevel;

            legacyStations.AddRange(scene.legacyStations);
            briefing = scene.briefing;
            taskDoor = scene.taskDoor;
            exitDoor = scene.exitDoor;

            if (briefing != null) briefing.FirstCompleted += OnBriefingCompleted;
            else OnBriefingCompleted(); // no screen in scene — don't soft-lock

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();
            UpdateObjective();
        }

        /// <summary>Closest return door to the player (levels have one per room).</summary>
        private HubDoor NearestExit() => LevelSceneLookup.NearestExit();

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
            if (taskDoor != null) BurstFX.SpawnAbove(taskDoor.transform,
                new Color(0.30f, 1f, 0.45f), 30, minimumHeight: 2.5f);
            else BurstFX.Spawn(Vector3.up * 2f, new Color(0.30f, 1f, 0.45f), 30);
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

        // StationSetup's delegate hooks are runtime-only (Func/Action don't
        // serialize), so a legacy scene saved from the editor has no way to
        // call NotifyStationReviewed — poll instead, once a second.
        private float legacyPollTimer;
        private float guidanceTimer;
        void Update()
        {
            // Guidance re-evaluates on a slow tick: the right target changes
            // from things the manager can't observe (picking up the token,
            // clearing one MFA factor), so events alone would leave it stale.
            guidanceTimer += Time.deltaTime;
            if (guidanceTimer >= 0.4f)
            {
                guidanceTimer = 0f;
                UpdateGuidance();
            }

            if (legacyStations.Count == 0 || CurrentPhase != Phase.Tasks) return;
            legacyPollTimer += Time.deltaTime;
            if (legacyPollTimer < 1f) return;
            legacyPollTimer = 0f;
            NotifyStationReviewed();
        }

        // ---- Player guidance --------------------------------------------------
        // Three complementary answers so the player is never lost:
        //   ObjectiveBeacon  → WHERE to go (light pillar + distance)
        //   TaskListPanel    → WHAT the level is asking for overall
        //   HUD objective    → the single next action, phrased as an instruction

        private static readonly Color GuideGold = new Color(0.90f, 0.66f, 0.14f);

        /// <summary>Holding the MFA token specifically (vs. a data crate).</summary>
        private bool CarryingToken =>
            Carryable.Carried != null && Carryable.Carried.id == "mfa_token";

        /// <summary>Aim the beacon at whatever the player should approach next,
        /// and refresh the checklist.</summary>
        private void UpdateGuidance()
        {
            var beacon = ObjectiveBeacon.Ensure();
            Transform t = null;
            string action = "";

            switch (CurrentPhase)
            {
                case Phase.Watch:
                    if (briefing != null) { t = briefing.transform; action = "WATCH THE BRIEFING"; }
                    break;

                case Phase.Tasks:
                    // Identification gates everything else, so it's always first.
                    if (badge != null && !badge.IsEnrolled)
                    {
                        t = badge.transform; action = "ENROLL YOUR ID BADGE";
                    }
                    else if (CarryingToken && mfaSlot != null)
                    {
                        // Carrying the token: point at where it goes, not at the rack.
                        t = mfaSlot.transform; action = "INSERT THE TOKEN";
                    }
                    else if (Carryable.Carried != null && sorting != null && !sorting.IsComplete)
                    {
                        // Carrying a data crate: point at the triage area, NOT at
                        // the correct pedestal — choosing the role is the puzzle.
                        t = sorting.transform; action = "CHOOSE THE RIGHT ROLE";
                    }
                    else
                    {
                        var next = NearestUnfinished(out action);
                        t = next;
                    }
                    break;

                case Phase.Exam:
                    if (exam != null) { t = exam.transform; action = "TAKE THE CERTIFICATION EXAM"; }
                    break;

                case Phase.Complete:
                    var nearest = NearestExit();
                    if (nearest != null) { t = nearest.transform; action = "RETURN TO THE HUB"; }
                    break;
            }

            if (t != null) beacon.PointAt(t, action, GuideGold);
            else beacon.Hide();

            UpdateObjective();
            UpdateTaskList();
        }

        /// <summary>Closest task the player hasn't finished — so the beacon
        /// never sends them across the room past something they could do.</summary>
        private Transform NearestUnfinished(out string action)
        {
            var cam = Camera.main;
            Vector3 from = cam != null ? cam.transform.position : Vector3.zero;

            Transform best = null;
            string bestLabel = "";
            float bestSqr = float.MaxValue;

            // NOTE: this must write to a plain local, not to `action` — C#
            // forbids a local function capturing a ref/out/in parameter.
            void Consider(Component c, string label)
            {
                if (c == null) return;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= bestSqr) return;
                bestSqr = d;
                best = c.transform;
                bestLabel = label;
            }

            if (gauntlet != null && !gauntlet.IsComplete) Consider(gauntlet, "CLEAR THE MFA VAULT");
            if (sorting != null && !sorting.IsComplete) Consider(sorting, "FILE THE DATA CRATES");
            if (audit != null && !audit.IsComplete) Consider(audit, "FIND THE AUDIT ANOMALY");

            action = bestLabel;
            return best;
        }

        private void UpdateTaskList()
        {
            var list = TaskListPanel.Ensure(gameObject);
            list.SetHeader("I/AM TRAINING");

            var tasks = new List<TaskListPanel.Task>();
            bool watched = CurrentPhase != Phase.Watch;
            tasks.Add(new TaskListPanel.Task("Watch the briefing", watched, !watched));

            if (badge != null)
            {
                bool cur = watched && !badge.IsEnrolled;
                tasks.Add(new TaskListPanel.Task("Enroll your ID badge", badge.IsEnrolled, cur));
            }
            bool enrolled = badge == null || badge.IsEnrolled;

            if (gauntlet != null)
                tasks.Add(new TaskListPanel.Task(
                    $"MFA Vault  ({gauntlet.ClearedCount}/3 factors)",
                    gauntlet.IsComplete, watched && enrolled && !gauntlet.IsComplete));
            if (sorting != null)
                tasks.Add(new TaskListPanel.Task(
                    $"Data Triage  ({sorting.Delivered}/{sorting.Total} filed)",
                    sorting.IsComplete, watched && enrolled && !sorting.IsComplete));
            if (audit != null)
                tasks.Add(new TaskListPanel.Task(
                    $"Audit Hunt  ({audit.Solved}/{audit.Rounds} flagged)",
                    audit.IsComplete, watched && enrolled && !audit.IsComplete));
            if (exam != null)
                tasks.Add(new TaskListPanel.Task("Certification Exam",
                    exam.IsComplete, CurrentPhase == Phase.Exam));

            list.Show(tasks);
        }

        /// <summary>The single next action, written as an instruction with the
        /// key to press — the HUD line should never make the player guess.</summary>
        private string NextActionText()
        {
            if (badge != null && !badge.IsEnrolled)
                return "Follow the marker to the ENROLLMENT kiosk — press E to create your ID badge";

            if (CarryingToken)
                return "Carry the token to the TOKEN SLOT by the vault — press E to insert it  (Q puts it down)";
            if (Carryable.Carried != null)
                return $"Carrying {Carryable.Carried.itemName} — press E on the role that should have access  (Q puts it down)";

            if (gauntlet != null && !gauntlet.IsComplete)
                return $"MFA Vault: verify all three factors  ({gauntlet.ClearedCount}/3) — passcode terminal, token, biometric pad";
            if (sorting != null && !sorting.IsComplete)
                return $"Data Triage: press E to pick up a crate, then E on the role that should have access  ({sorting.Delivered}/{sorting.Total})";
            if (audit != null && !audit.IsComplete)
                return $"Audit Hunt: press E to open the log, ↑/↓ to select, E to flag the anomaly  ({audit.Solved}/{audit.Rounds})";

            return $"Complete the training tasks  ({TasksDone}/{TotalTasks})";
        }

        private string lastObjective;

        /// <summary>Push to the HUD only when the wording actually changes —
        /// ShowObjective animates, and guidance re-evaluates several times a
        /// second.</summary>
        private void SetObjective(string text)
        {
            if (text == lastObjective) return;
            lastObjective = text;
            HudUI.Instance.ShowObjective(text);
        }

        private void UpdateObjective()
        {
            if (HudUI.Instance == null) return;
            switch (CurrentPhase)
            {
                case Phase.Watch:
                    SetObjective("Objective: Watch the security briefing  (E to play, ←/→ to scrub)");
                    HudUI.Instance.SetProgress(0, Mathf.Max(1, TotalSteps), "▶");
                    break;
                case Phase.Tasks:
                    if (legacyStations.Count > 0)
                    {
                        int n = 0;
                        foreach (var s in legacyStations) if (s.IsReviewed) n++;
                        SetObjective($"Objective: Review the I/AM stations  ({n}/{legacyStations.Count})");
                        HudUI.Instance.SetProgress(n, legacyStations.Count);
                    }
                    else
                    {
                        SetObjective(NextActionText());
                        HudUI.Instance.SetProgress(TasksDone, TotalSteps);
                    }
                    break;
                case Phase.Exam:
                    SetObjective("Objective: Pass the Certification Exam");
                    HudUI.Instance.SetProgress(TotalTasks, TotalSteps);
                    break;
                case Phase.Complete:
                    SetObjective("LEVEL 1 COMPLETE — exit to the Hub");
                    HudUI.Instance.SetProgress(TotalSteps, Mathf.Max(1, TotalSteps), "✓");
                    break;
            }
        }

        private void CompleteLevel()
        {
            if (CurrentPhase == Phase.Complete) return;
            CurrentPhase = Phase.Complete;

            UpdateObjective();
            LevelMissionRuntime.Complete(new LevelMissionRuntime.Completion
            {
                levelNumber = 1,
                exitDoor = exitDoor,
                accent = GuideGold,
                elapsedSeconds = Time.time - startTime,
                headerText = "LEVEL 1 COMPLETE",
                grantedLine = "I/AM Training Certified",
                nextMissionText = "Level 2 — Cyber Defense is now unlocked in the Hub.",
                replaySuffix = "Level 1",
                parScore = 1100,
            });
        }
    }
}
