using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 2 (Cyber Defense) flow:
    ///   Watch    — defense briefing; unlocks the SOC floor door
    ///   Tasks    — SIEM alert shift, EDR endpoint containment, IR playbook
    ///   Exam     — certification terminal unlocks once all three are done
    ///   Complete — persisted (unlocks Level 3), results, exits open.
    /// Drives the same guidance stack as Level 1 (beacon + task checklist +
    /// an actionable objective line).
    /// </summary>
    public class Level2Manager : MonoBehaviour
    {
        public enum Phase { Watch, Tasks, Exam, Complete }

        public static Level2Manager Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Watch;

        private SiemConsole siem;
        private EdrFleet edr;
        private PlaybookStation playbook;
        private CertExamStation exam;

        private VideoStation briefing;
        private LockedDoor taskDoor;
        private HubDoor exitDoor;
        private float startTime;

        private static readonly Color GuideGold = new Color(0.90f, 0.66f, 0.14f);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            GameState.Reset();
            ScoreSystem.Reset();
            Carryable.ClearCarried();
            Time.timeScale = 1f;
            Shader.SetGlobalFloat("_CyMotion", 1f);
        }

        void Start()
        {
            startTime = Time.time;

            if (Quiz.QuizSystem.Instance == null) gameObject.AddComponent<Quiz.QuizSystem>();
            if (ResultsScreen.Instance == null) gameObject.AddComponent<ResultsScreen>();
            if (VisualDirector.Instance == null) gameObject.AddComponent<VisualDirector>();

            var cam = Camera.main;
            if (cam != null && cam.GetComponent<FirstPersonHands>() == null)
                cam.gameObject.AddComponent<FirstPersonHands>();
            if (Audio.AmbientHum.Instance == null) gameObject.AddComponent<Audio.AmbientHum>();
            if (GlossaryPanel.Instance == null) gameObject.AddComponent<GlossaryPanel>();

            siem = FindObjectOfType<SiemConsole>();
            edr = FindObjectOfType<EdrFleet>();
            playbook = FindObjectOfType<PlaybookStation>();
            exam = FindObjectOfType<CertExamStation>();

            if (siem != null) siem.Completed += OnTaskCompleted;
            if (edr != null) edr.Completed += OnTaskCompleted;
            if (playbook != null) playbook.Completed += OnTaskCompleted;
            if (exam != null) exam.Completed += CompleteLevel;

            briefing = FindObjectOfType<VideoStation>();
            taskDoor = FindObjectOfType<LockedDoor>();

            HubDoor.EnsureReachableExit(2f, GuideGold);
            exitDoor = NearestExit();

            if (briefing != null) briefing.FirstCompleted += OnBriefingCompleted;
            else OnBriefingCompleted();

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();
            UpdateObjective();
        }

        private HubDoor NearestExit()
        {
            var cam = Camera.main;
            Vector3 from = cam != null ? cam.transform.position : Vector3.zero;
            HubDoor best = null;
            float bestSqr = float.MaxValue;
            foreach (var d in FindObjectsOfType<HubDoor>())
            {
                float sqr = (d.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = d;
            }
            return best;
        }

        private int TotalTasks =>
            (siem != null ? 1 : 0) + (edr != null ? 1 : 0) + (playbook != null ? 1 : 0);

        private int TasksDone =>
            (siem != null && siem.IsComplete ? 1 : 0) +
            (edr != null && edr.IsComplete ? 1 : 0) +
            (playbook != null && playbook.IsComplete ? 1 : 0);

        private int TotalSteps => TotalTasks + (exam != null ? 1 : 0);

        private void OnBriefingCompleted()
        {
            if (CurrentPhase != Phase.Watch) return;
            CurrentPhase = Phase.Tasks;

            if (taskDoor != null) taskDoor.Unlock();
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("Briefing complete — the SOC floor is open",
                    new Color(0.30f, 1f, 0.45f));
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
                    HudUI.Instance.ShowToast("All tasks complete — the Certification Exam is unlocked", GuideGold);
                UpdateObjective();
            }
            else CompleteLevel();
        }

        // ---- Guidance (mirrors Level 1) ---------------------------------------

        private float guidanceTimer;
        void Update()
        {
            guidanceTimer += Time.deltaTime;
            if (guidanceTimer < 0.4f) return;
            guidanceTimer = 0f;
            UpdateGuidance();
        }

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
                    if (Carryable.Carried != null && playbook != null && !playbook.IsComplete)
                    {
                        // Point at the board, never at the correct slot — the
                        // ordering is the puzzle.
                        t = playbook.transform; action = "PLACE IT IN ORDER";
                    }
                    else t = NearestUnfinished(out action);
                    break;
                case Phase.Exam:
                    if (exam != null) { t = exam.transform; action = "TAKE THE CERTIFICATION EXAM"; }
                    break;
                case Phase.Complete:
                    var near = NearestExit();
                    if (near != null) { t = near.transform; action = "RETURN TO THE HUB"; }
                    break;
            }

            if (t != null) beacon.PointAt(t, action, GuideGold);
            else beacon.Hide();

            UpdateObjective();
            UpdateTaskList();
        }

        private Transform NearestUnfinished(out string action)
        {
            action = "";
            var cam = Camera.main;
            Vector3 from = cam != null ? cam.transform.position : Vector3.zero;
            Transform best = null;
            float bestSqr = float.MaxValue;

            void Consider(Component c, string label)
            {
                if (c == null) return;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= bestSqr) return;
                bestSqr = d; best = c.transform; action = label;
            }

            if (siem != null && !siem.IsComplete) Consider(siem, "WORK THE ALERT QUEUE");
            if (edr != null && !edr.IsComplete) Consider(edr, "CONTAIN THE ENDPOINTS");
            if (playbook != null && !playbook.IsComplete) Consider(playbook, "ORDER THE PLAYBOOK");
            return best;
        }

        private void UpdateTaskList()
        {
            var list = TaskListPanel.Ensure(gameObject);
            list.SetHeader("CYBER DEFENSE");

            var tasks = new List<TaskListPanel.Task>();
            bool watched = CurrentPhase != Phase.Watch;
            tasks.Add(new TaskListPanel.Task("Watch the briefing", watched, !watched));

            if (siem != null)
                tasks.Add(new TaskListPanel.Task(
                    $"SIEM alert shift  ({siem.Handled}/{siem.Total})",
                    siem.IsComplete, watched && !siem.IsComplete));
            if (edr != null)
                tasks.Add(new TaskListPanel.Task(
                    $"Contain endpoints  ({edr.Contained}/{edr.Threats})",
                    edr.IsComplete, watched && !edr.IsComplete));
            if (playbook != null)
                tasks.Add(new TaskListPanel.Task(
                    $"IR playbook  ({playbook.Placed}/{playbook.Total} steps)",
                    playbook.IsComplete, watched && !playbook.IsComplete));
            if (exam != null)
                tasks.Add(new TaskListPanel.Task("Certification Exam",
                    exam.IsComplete, CurrentPhase == Phase.Exam));

            list.Show(tasks);
        }

        private string NextActionText()
        {
            if (Carryable.Carried != null)
                return $"Carrying {Carryable.Carried.itemName} — place it on the next open playbook slot  (Q puts it down)";
            if (siem != null && !siem.IsComplete)
                return $"SIEM: press E to start the shift, then [1] ESCALATE / [2] DISMISS  ({siem.Handled}/{siem.Total})";
            if (edr != null && !edr.IsComplete)
                return $"EDR: read each workstation's processes, press E to isolate the infected ones  ({edr.Contained}/{edr.Threats})";
            if (playbook != null && !playbook.IsComplete)
                return $"IR Playbook: carry the response cards onto the slots in order  ({playbook.Placed}/{playbook.Total})";
            return $"Complete the defense tasks  ({TasksDone}/{TotalTasks})";
        }

        private string lastObjective;

        private void SetObjective(string text)
        {
            if (HudUI.Instance == null || text == lastObjective) return;
            lastObjective = text;
            HudUI.Instance.ShowObjective(text);
        }

        private void UpdateObjective()
        {
            if (HudUI.Instance == null) return;
            switch (CurrentPhase)
            {
                case Phase.Watch:
                    SetObjective("Objective: Watch the defense briefing  (E to play, ←/→ to scrub)");
                    HudUI.Instance.SetProgress(0, Mathf.Max(1, TotalSteps), "▶");
                    break;
                case Phase.Tasks:
                    SetObjective(NextActionText());
                    HudUI.Instance.SetProgress(TasksDone, TotalSteps);
                    break;
                case Phase.Exam:
                    SetObjective("Objective: Pass the Certification Exam");
                    HudUI.Instance.SetProgress(TotalTasks, TotalSteps);
                    break;
                case Phase.Complete:
                    SetObjective("LEVEL 2 COMPLETE — exit to the Hub");
                    HudUI.Instance.SetProgress(TotalSteps, Mathf.Max(1, TotalSteps), "✓");
                    break;
            }
        }

        private void CompleteLevel()
        {
            if (CurrentPhase == Phase.Complete) return;
            CurrentPhase = Phase.Complete;

            LevelProgress.MarkCompleted(2); // unlocks Level 3 in the Hub
            GameState.LevelComplete = true;
            UpdateObjective();
            FirstPersonController.LockCursor(false);

            Vector3 burstPos = exitDoor != null
                ? exitDoor.transform.position + Vector3.up * 2.5f
                : (Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 2f : Vector3.up * 2f);
            BurstFX.Spawn(burstPos, GuideGold, 70, 3.4f, 1.3f);

            if (ResultsScreen.Instance != null)
                ResultsScreen.Instance.Show(
                    ScoreSystem.Score, ScoreSystem.QuizCorrect, ScoreSystem.QuizTotal,
                    Time.time - startTime,
                    headerText: "LEVEL 2 COMPLETE",
                    grantedLine: "Certification Confirmed — SOC Analyst",
                    nextMissionText: "Level 3 — Digital Forensics is now unlocked in the Hub.",
                    replaySuffix: "Level 2",
                    parScore: Level2Content.ParScore);
        }
    }
}
