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
    ///   Tasks    — three SOC investigations, the IR playbook, and sealing the
    ///              seized device in the evidence locker (the SOC-to-DF handoff)
    ///   Exam     — certification terminal unlocks once all of them are done
    ///   Complete — persisted (unlocks Level 3), results, exits open — including
    ///              the direct door to the Forensics Lab.
    /// Drives the same guidance stack as Level 1 (beacon + task checklist +
    /// an actionable objective line).
    /// </summary>
    public class Level2Manager : MonoBehaviour
    {
        public enum Phase { Watch, Tasks, Exam, Complete }

        public static Level2Manager Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Watch;

        private SiemConsole siem;
        private PlaybookStation playbook;
        private CertExamStation exam;
        private EvidenceLocker locker;
        private HubDoor forensicsDoor;
        private Carryable seizedDevice;
        private bool deviceIssued;

        private VideoStation briefing;
        private LockedDoor taskDoor;
        private HubDoor exitDoor;
        private float startTime;

        private static readonly Color GuideGold = new Color(0.90f, 0.66f, 0.14f);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            LevelMissionRuntime.ResetScene(clearCarried: true);
        }

        void Start()
        {
            startTime = Time.time;

            LevelMissionRuntime.EnsureSharedRuntime(gameObject, showEvidenceInventory: true);

            // The shipped Level 2 is a saved scene (BuildAll never runs on it), so the
            // locker and the Forensics door are added here. Idempotent for procedural
            // builds, which already made them.
            forensicsDoor = Level2SceneFactory.BuildEvidenceHandoff();
            locker = EvidenceLocker.Instance;

            Level2SceneRealization.Bindings scene =
                Level2SceneRealization.Realize(gameObject);
            siem = scene.siem;
            playbook = scene.playbook;
            exam = scene.exam;

            if (siem != null) siem.Completed += OnSiemCompleted;
            if (playbook != null) playbook.Completed += OnTaskCompleted;
            if (locker != null) locker.Sealed += OnEvidenceSealed;
            if (exam != null) exam.Completed += CompleteLevel;

            briefing = scene.briefing;
            // The shipped Level 2 is a saved scene with its slides serialized in
            // the file; refresh them from code so content edits reach it.
            if (briefing != null && briefing.clip == null && string.IsNullOrEmpty(briefing.videoUrl))
                briefing.slides = Level2Content.BriefingSlides();
            taskDoor = scene.taskDoor;
            exitDoor = scene.exitDoor;
            // "Nearest exit" can be the Forensics door standing beside the Hub door; the
            // completion burst and the Hub guidance belong to the Hub exit.
            if (exitDoor == null || exitDoor == forensicsDoor) exitDoor = NearestHubExit();

            if (briefing != null) briefing.FirstCompleted += OnBriefingCompleted;
            else OnBriefingCompleted();

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();
            UpdateObjective();
        }

        /// <summary>The Hub exit only. The nearest-HubDoor lookup would also pick the
        /// Forensics Lab door standing next to it.</summary>
        private static HubDoor NearestHubExit()
        {
            Camera camera = Camera.main;
            Vector3 origin = camera != null ? camera.transform.position : Vector3.zero;
            HubDoor nearest = null;
            float nearestSqr = float.MaxValue;
            foreach (HubDoor door in FindObjectsOfType<HubDoor>())
            {
                if (SceneCatalog.Preferred(door.sceneName) != "Hub") continue;
                float sqr = (door.transform.position - origin).sqrMagnitude;
                if (sqr >= nearestSqr) continue;
                nearestSqr = sqr;
                nearest = door;
            }
            return nearest;
        }

        // ---- SOC -> DF direct door ---------------------------------------------

        /// <summary>The direct door to the Forensics Lab opens once the device is sealed
        /// in the locker AND the mission is complete. The second condition keeps it from
        /// being a shortcut around the playbook and the exam: the Hub gate for Level 3
        /// needs them too, and the lab must never be reachable by a path that skips it.</summary>
        public static bool ForensicsTransferOpen =>
            EvidenceLocker.Instance != null && EvidenceLocker.Instance.side == EvidenceLocker.Side.Soc &&
            EvidenceLocker.Instance.IsSealed &&
            Instance != null && Instance.CurrentPhase == Phase.Complete;

        public static string ForensicsTransferLockedMessage()
        {
            EvidenceLocker evidence = EvidenceLocker.Instance;
            if (evidence == null || !evidence.IsSealed)
                return "Lock the seized device in the evidence locker first — it is handed to the lab from there.";
            return "Finish the remaining SOC tasks (IR playbook, certification exam) before heading to the lab.";
        }

        private bool IsCarryingSeizedDevice =>
            Carryable.Carried != null && Carryable.Carried.id == EvidenceLocker.SeizedDeviceId;

        private bool EvidencePending => locker != null && deviceIssued && !locker.IsSealed;

        private int TotalTasks =>
            (siem != null ? 1 : 0) + (playbook != null ? 1 : 0) + (locker != null ? 1 : 0);

        private int TasksDone =>
            (siem != null && siem.IsComplete ? 1 : 0) +
            (playbook != null && playbook.IsComplete ? 1 : 0) +
            (locker != null && locker.IsSealed ? 1 : 0);

        private int TotalSteps => TotalTasks + (exam != null ? 1 : 0);

        private void OnBriefingCompleted()
        {
            if (CurrentPhase != Phase.Watch) return;
            CurrentPhase = Phase.Tasks;

            if (taskDoor != null) taskDoor.Unlock();
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("Briefing complete — the SOC floor is open",
                    new Color(0.30f, 1f, 0.45f));
            if (taskDoor != null) BurstFX.SpawnAbove(taskDoor.transform,
                new Color(0.30f, 1f, 0.45f), 30, minimumHeight: 2.5f);
            else BurstFX.Spawn(Vector3.up * 2f, new Color(0.30f, 1f, 0.45f), 30);
            UpdateObjective();
        }

        private void OnSiemCompleted()
        {
            IssueSeizedDevice();
            OnTaskCompleted();
        }

        /// <summary>The seized device appears on WS-03's desk once the SOC alert is
        /// resolved. It is a Carryable, so it is picked up and put down like every other
        /// task item, and delivered to the locker on the north wall.</summary>
        private void IssueSeizedDevice()
        {
            if (locker == null || deviceIssued) return;
            deviceIssued = true;

            seizedDevice = EvidenceLocker.BuildSeizedDevice(SeizedDeviceSpot());
            BurstFX.Spawn(seizedDevice.transform.position + Vector3.up * 0.5f,
                new Color(0.90f, 0.66f, 0.14f), 24);
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast(
                    "EVIDENCE SEIZED — pick up the SEIZED DEVICE at WS-03 and lock it in the EVIDENCE LOCKER on the north wall.",
                    GuideGold);
        }

        private Vector3 SeizedDeviceSpot()
        {
            // Right of the keyboard on the WS-03 desk (desk top 0.74, local -Z is the
            // side the analyst stands on).
            foreach (EndpointStation station in FindObjectsOfType<EndpointStation>())
                if (station.def != null && station.def.hostname == "WS-03")
                    return station.transform.TransformPoint(new Vector3(0.42f, 0.74f, -0.12f));
            return siem != null
                ? siem.transform.TransformPoint(new Vector3(1.0f, 1.0f, -0.1f))
                : new Vector3(-16.88f, 0.74f, 10.92f);
        }

        private void OnEvidenceSealed()
        {
            if (forensicsDoor != null) forensicsDoor.SetUnlocked(true); // re-evaluates the gate
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast(
                    "EVIDENCE LOCKED — the device is waiting for you in the Digital Forensics Lab. " +
                    "Finish the SOC tasks, then take the Forensics Lab door to hand it off.",
                    new Color(0.30f, 1f, 0.45f));
            OnTaskCompleted();
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
                    if (IsCarryingSeizedDevice && locker != null)
                    {
                        t = locker.transform; action = "LOCK IT IN THE EVIDENCE LOCKER";
                    }
                    else if (Carryable.Carried != null && playbook != null && !playbook.IsComplete)
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
                    if (forensicsDoor != null && ForensicsTransferOpen)
                    {
                        t = forensicsDoor.transform; action = "TAKE THE DEVICE TO THE FORENSICS LAB";
                        break;
                    }
                    var near = NearestHubExit();
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
                bestSqr = d; best = c.transform; bestLabel = label;
            }

            if (siem != null && !siem.IsComplete) Consider(siem, "WORK THE ALERT QUEUE");
            if (EvidencePending)
                Consider(seizedDevice != null ? (Component)seizedDevice : locker, "PICK UP THE SEIZED DEVICE");
            if (playbook != null && !playbook.IsComplete) Consider(playbook, "ORDER THE PLAYBOOK");

            action = bestLabel;
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
                    $"SOC investigations  ({siem.CompletedScenarios}/{siem.ScenarioCount})",
                    siem.IsComplete, watched && !siem.IsComplete));
            if (locker != null)
                tasks.Add(new TaskListPanel.Task("Lock the seized device in the evidence locker",
                    locker.IsSealed, EvidencePending));
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
            if (IsCarryingSeizedDevice)
                return "Evidence: carry the SEIZED DEVICE to the EVIDENCE LOCKER on the north wall, then press E  (Q puts it down)";
            if (Carryable.Carried != null)
            {
                if (playbook != null && !playbook.IsComplete)
                    return playbook.IsExpectedCard(Carryable.Carried)
                        ? $"Carry {Carryable.Carried.itemName} to STEP {playbook.NextSlot}  (Q puts it down)"
                        : $"Next is {playbook.NextStep}, not {Carryable.Carried.itemName} — press Q to put it down";
                return $"Carrying {Carryable.Carried.itemName}  (Q puts it down)";
            }
            if (EvidencePending)
                return "Evidence: pick up the SEIZED DEVICE at WS-03 and lock it in the EVIDENCE LOCKER (north wall)";
            if (siem != null && !siem.IsComplete)
                return $"SOC: review the Alert Board, flag a row, then verify its workstation  ({siem.ScenarioIndex}/{siem.ScenarioCount})";
            if (playbook != null && !playbook.IsComplete)
                return $"IR Playbook: pick up {playbook.NextStep} from the rack, then place it on STEP {playbook.NextSlot}  ({playbook.Placed}/{playbook.Total})";
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
                    SetObjective(ForensicsTransferOpen
                        ? "LEVEL 2 COMPLETE — take the device to the Digital Forensics Lab (or return to the Hub)"
                        : "LEVEL 2 COMPLETE — exit to the Hub");
                    HudUI.Instance.SetProgress(TotalSteps, Mathf.Max(1, TotalSteps), "✓");
                    break;
            }
        }

        private void CompleteLevel()
        {
            if (CurrentPhase == Phase.Complete) return;
            if (!SocProgress.HasAllDfKeys)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Digital Forensics handoff incomplete: " +
                        SocProgress.MissingDfKeysText(), new Color(1f, 0.55f, 0.4f));
                return;
            }
            if (locker != null && !locker.IsSealed)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Lock the seized device in the evidence locker before leaving the SOC.",
                        new Color(1f, 0.55f, 0.4f));
                return;
            }
            CurrentPhase = Phase.Complete;
            if (forensicsDoor != null)
            {
                forensicsDoor.SetUnlocked(true); // gate is now satisfied
                BurstFX.SpawnAbove(forensicsDoor.transform, Level3ForensicsSceneFactory.ForensicGreen,
                    40, minimumHeight: 3.2f);
            }

            UpdateObjective();
            LevelMissionRuntime.Complete(new LevelMissionRuntime.Completion
            {
                levelNumber = 2,
                exitDoor = exitDoor,
                accent = GuideGold,
                elapsedSeconds = Time.time - startTime,
                headerText = "LEVEL 2 COMPLETE",
                grantedLine = "Certification Confirmed — SOC Analyst",
                nextMissionText = locker != null
                    ? "Level 3 — Digital Forensics is unlocked: use the Forensics Lab door in this room, or the Hub."
                    : "Level 3 — Digital Forensics is now unlocked in the Hub.",
                replaySuffix = "Level 2",
                parScore = Level2Content.ParScore,
            });
        }
    }
}
