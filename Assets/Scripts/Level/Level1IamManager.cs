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
        private MfaSpecialist specialist;
        private DropZone mfaSlot;
        private SortingStation sorting;
        private AuditStation audit;
        private CertExamStation exam;
        private readonly List<StationSetup> legacyStations = new List<StationSetup>();

        private VideoStation briefing;
        private LockedDoor taskDoor;
        private HubDoor exitDoor;
        private float startTime;

        // Hard copy (entered from the Hub's Hard wing): see HardMode.
        private bool hard;
        private const float HardParSeconds = 420f;
        private const int HardParBonus = 300;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            LevelMissionRuntime.ResetScene(clearCarried: true);
        }

        void Start()
        {
            startTime = Time.time;
            hard = HardMode.IsActiveFor(1);

            LevelMissionRuntime.EnsureSharedRuntime(gameObject, showEvidenceInventory: false);
            if (TypingChallenge.Instance == null) gameObject.AddComponent<TypingChallenge>();

            // Both scene adapters are normalized through one realization seam
            // before the progression manager subscribes to anything.
            Level1IamSceneRealization.Bindings scene =
                Level1IamSceneRealization.Realize(gameObject);
            badge = scene.badge;
            gauntlet = scene.gauntlet;
            specialist = scene.specialist;
            mfaSlot = scene.mfaSlot;
            sorting = scene.sorting;
            audit = scene.audit;
            exam = scene.exam;

            if (badge != null) badge.Completed += OnTaskCompleted;
            if (gauntlet != null) gauntlet.Completed += OnTaskCompleted;
            if (specialist != null) specialist.BriefingStarted += UpdateGuidance;
            if (sorting != null) sorting.Completed += OnTaskCompleted;
            if (audit != null) audit.Completed += OnTaskCompleted;
            if (exam != null)
            {
                exam.Completed += CompleteLevel;
                exam.lockedMessage = ExamLockedMessage;
            }

            legacyStations.AddRange(scene.legacyStations);
            briefing = scene.briefing;
            taskDoor = scene.taskDoor;
            exitDoor = scene.exitDoor;

            if (briefing != null) briefing.FirstCompleted += OnBriefingCompleted;
            else OnBriefingCompleted(); // no screen in scene — don't soft-lock

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();
            UpdateObjective();
        }

        /// <summary>Names what is still open in THIS level when the player
        /// tries the locked exam.</summary>
        private string ExamLockedMessage()
        {
            if (CurrentPhase == Phase.Watch) return "Watch the briefing first.";
            var left = new List<string>();
            if (badge != null && !badge.IsEnrolled) left.Add("ID badge");
            if (gauntlet != null && !gauntlet.IsComplete) left.Add("MFA Vault");
            if (sorting != null && !sorting.IsComplete) left.Add("Data Triage");
            if (audit != null && !audit.IsComplete) left.Add("Audit Hunt");
            return left.Count == 0 ? "Finish this level's tasks first."
                : "Still to do before the exam: " + string.Join(", ", left) + ".";
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

        /// <summary>The vault is still locked behind the specialist's briefing.</summary>
        private bool NeedsMfaBriefing => specialist != null && !specialist.Briefed;

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
                    break; // the results card owns the screen; nowhere to walk
            }

            if (t != null) beacon.PointAt(t, action, GuideGold);
            else beacon.Hide();

            UpdateObjective();
            UpdateTaskList();
        }

        /// <summary>Closest task the player hasn't finished — so the beacon
        /// never sends them across the room past something they could do.</summary>
        private Component beaconFocus;

        private bool IsDone(Component task) =>
            task == null ||
            (task == gauntlet && gauntlet.IsComplete) ||
            (task == sorting && sorting.IsComplete) ||
            (task == audit && audit.IsComplete);

        private Transform NearestUnfinished(out string action)
        {
            // Stay on the task the marker already chose until it is finished.
            // Re-picking the nearest every tick made the marker switch targets
            // while the player was still walking to the first one.
            if (beaconFocus != null && !IsDone(beaconFocus))
            {
                if (beaconFocus == gauntlet)
                {
                    action = NeedsMfaBriefing ? "TALK TO THE MFA SPECIALIST" : "CLEAR THE MFA VAULT";
                    return NeedsMfaBriefing && specialist != null ? specialist.transform : gauntlet.transform;
                }
                action = beaconFocus == sorting ? "FILE THE DATA CRATES" : "FIND THE AUDIT ANOMALY";
                return beaconFocus.transform;
            }

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

            if (gauntlet != null && !gauntlet.IsComplete)
            {
                if (NeedsMfaBriefing) Consider(specialist, "TALK TO THE MFA SPECIALIST");
                else Consider(gauntlet, "CLEAR THE MFA VAULT");
            }
            if (sorting != null && !sorting.IsComplete) Consider(sorting, "FILE THE DATA CRATES");
            if (audit != null && !audit.IsComplete) Consider(audit, "FIND THE AUDIT ANOMALY");

            beaconFocus = best == null ? null
                : sorting != null && best == sorting.transform ? (Component)sorting
                : audit != null && best == audit.transform ? (Component)audit
                : gauntlet;
            action = bestLabel;
            return best;
        }

        private void UpdateTaskList()
        {
            var list = TaskListPanel.Ensure(gameObject);
            list.SetHeader(hard ? HardHeader() : "I/AM TRAINING");

            var tasks = new List<TaskListPanel.Task>();
            bool watched = CurrentPhase != Phase.Watch;
            tasks.Add(new TaskListPanel.Task("Watch the briefing", watched, !watched));

            if (badge != null)
            {
                bool cur = watched && !badge.IsEnrolled;
                tasks.Add(new TaskListPanel.Task("Enroll your ID badge", badge.IsEnrolled, cur));
            }
            bool enrolled = badge == null || badge.IsEnrolled;

            if (specialist != null && gauntlet != null)
                tasks.Add(new TaskListPanel.Task("Talk to the MFA Specialist",
                    specialist.Briefed, watched && enrolled && !specialist.Briefed));
            if (gauntlet != null)
                tasks.Add(new TaskListPanel.Task(
                    $"MFA Vault  ({gauntlet.ClearedCount}/3 factors)",
                    gauntlet.IsComplete, watched && enrolled && !NeedsMfaBriefing && !gauntlet.IsComplete));
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

        /// <summary>The Hard run's clock against par, in the checklist header.</summary>
        private string HardHeader()
        {
            float t = (CurrentPhase == Phase.Complete ? completedAt : Time.time) - startTime;
            string clock = $"{(int)t / 60}:{(int)t % 60:00}";
            string colour = t <= HardParSeconds ? "#FFFFFF" : "#FF8866";
            return $"I/AM HARD  ·  <color={colour}>{clock}</color> / par {(int)HardParSeconds / 60}:{(int)HardParSeconds % 60:00}";
        }

        private float completedAt;

        /// <summary>The single next action, written as an instruction with the
        /// key to press — the HUD line should never make the player guess.</summary>
        private string NextActionText()
        {
            // Verb + place, short. How to do it (which key) is on the prompt
            // that appears when the player aims at the thing.
            if (badge != null && !badge.IsEnrolled)
                return "Create your ID badge at the ENROLLMENT kiosk";

            if (CarryingToken)
                return "Insert the token in the TOKEN SLOT by the vault";
            if (Carryable.Carried != null)
                return $"Give {Carryable.Carried.itemName} to the role that should have access";

            // Name the task the marker is on, so the line and the marker agree.
            Component focus = beaconFocus;
            if (focus == null || IsDone(focus))
                focus = gauntlet != null && !gauntlet.IsComplete ? (Component)gauntlet
                      : sorting != null && !sorting.IsComplete ? (Component)sorting
                      : audit != null && !audit.IsComplete ? (Component)audit : null;

            if (focus == gauntlet && gauntlet != null)
                return NeedsMfaBriefing
                    ? "Talk to the MFA SPECIALIST by the vault"
                    : $"Verify all three factors at the MFA VAULT  ({gauntlet.ClearedCount}/3)";
            if (focus == sorting && sorting != null)
                return $"Give each data crate to the right role at DATA TRIAGE  ({sorting.Delivered}/{sorting.Total})";
            if (focus == audit && audit != null)
                return $"Flag the odd entry in the AUDIT LOG  ({audit.Solved}/{audit.Rounds})";

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

        /// <summary>Briefing is step 1, each task one step, the exam last.</summary>
        private void SetStep(int step, string text)
        {
            string key = step + "|" + text;
            if (key == lastObjective) return;
            lastObjective = key;
            HudUI.Instance.ShowStep(step, TotalSteps + 1, text);
        }

        private void UpdateObjective()
        {
            if (HudUI.Instance == null) return;
            switch (CurrentPhase)
            {
                case Phase.Watch:
                    SetStep(1, "Watch the security briefing");
                    HudUI.Instance.SetProgress(0, TotalSteps + 1);
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
                        SetStep(TasksDone + 2, NextActionText());
                        HudUI.Instance.SetProgress(TasksDone + 1, TotalSteps + 1);
                    }
                    break;
                case Phase.Exam:
                    SetStep(TotalSteps + 1, "Pass the Certification Exam");
                    HudUI.Instance.SetProgress(TotalTasks + 1, TotalSteps + 1);
                    break;
                case Phase.Complete:
                    SetObjective("LEVEL 1 COMPLETE");
                    HudUI.Instance.SetProgress(1, 1);
                    break;
            }
        }

        private void CompleteLevel()
        {
            if (CurrentPhase == Phase.Complete) return;
            completedAt = Time.time;
            if (hard && completedAt - startTime <= HardParSeconds)
            {
                ScoreSystem.Add(HardParBonus);
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast($"UNDER PAR  ·  +{Mathf.RoundToInt(HardParBonus * HardMode.ScoreMultiplier)}",
                        GuideGold);
            }
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
