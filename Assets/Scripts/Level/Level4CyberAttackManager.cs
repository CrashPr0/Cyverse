using System;
using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Progression and scoring for Level 4's closed-world cyber-attack
    /// simulation. Reconnaissance is covered by the briefing; the run is a
    /// timed sequence of four conceptual stations: bypass MFA, escalate
    /// privileges, extract data, and cover tracks. The manager deliberately
    /// contains no networking,
    /// process-launching or exploit code; stations only resolve authored
    /// choices against synthetic scenarios.
    /// </summary>
    public sealed class Level4CyberAttackManager : MonoBehaviour
    {
        public enum Phase
        {
            Briefing,
            BypassMfa,
            EscalatePrivileges,
            ExtractData,
            CoverTracks,
            Complete,
        }

        public static Level4CyberAttackManager Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Briefing;
        public int CurrentStationIndex { get; private set; }
        public int CorrectAttempts { get; private set; }
        public int WrongAttempts { get; private set; }
        public int ExfiltrationPercent { get; private set; }
        public int DetectionPercent { get; private set; }
        public bool ScenarioStarted { get; private set; }
        public bool TimeExpired { get; private set; }
        public bool ReportShown { get; private set; }
        public bool IsLevelComplete => CurrentPhase == Phase.Complete && GameState.LevelComplete;
        public float ElapsedSeconds => ScenarioStarted ? Mathf.Max(0f, activeElapsedSeconds) : 0f;
        public float TimeRemaining => Mathf.Max(0f,
            Level4CyberAttackContent.ScenarioTimeLimitSeconds - ElapsedSeconds);
        public string LastFeedback { get; private set; } = string.Empty;
        public string MeterSummary =>
            $"EXFILTRATION  {ExfiltrationPercent:00}%    ·    CAUGHT  {DetectionPercent:00}%" +
            (TimeExpired ? "    ·    OVERTIME" :
                $"    ·    T-{Mathf.FloorToInt(TimeRemaining / 60f)}:{Mathf.FloorToInt(TimeRemaining % 60f):00}");

        private readonly bool[] completed = new bool[4];
        private Level4CyberAttackContent.StationScenario[] scenarios;
        private CyberAttackStation[] stations;
        private VideoStation briefing;
        private LockedDoor taskDoor;
        private HubDoor exitDoor;
        private float activeElapsedSeconds;
        private float guidanceTimer;
        private bool pendingCompletion;

        private static readonly Color AttackOrange = new Color(1f, 0.42f, 0.25f);
        private static readonly Color SafeGreen = new Color(0.30f, 1f, 0.55f);
        private static readonly Color Gold = new Color(0.90f, 0.66f, 0.14f);

        public event Action ProgressChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            LevelMissionRuntime.ResetScene(clearCarried: true);
            scenarios = Level4CyberAttackContent.Stations();
        }

        private void Start()
        {
            if (scenarios == null || scenarios.Length != 4)
                scenarios = Level4CyberAttackContent.Stations();

            LevelMissionRuntime.EnsureSharedRuntime(gameObject, showEvidenceInventory: false);

            Level4CyberAttackSceneRealization.Bindings scene =
                Level4CyberAttackSceneRealization.Realize(gameObject, this, scenarios);
            briefing = scene.briefing;
            taskDoor = scene.taskDoor;
            stations = scene.stations;
            exitDoor = scene.exitDoor;

            if (briefing != null)
            {
                briefing.FirstCompleted += OnBriefingCompleted;
                if (briefing.HasCompletedOnce) OnBriefingCompleted();
            }
            else OnBriefingCompleted();

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();
            UpdateObjective();
        }

        private void OnDestroy()
        {
            if (briefing != null) briefing.FirstCompleted -= OnBriefingCompleted;
            if (Instance == this) Instance = null;
        }

        private void OnBriefingCompleted()
        {
            if (CurrentPhase != Phase.Briefing) return;
            // Recon is context in the briefing, not a fifth completion gate.
            CurrentPhase = Phase.BypassMfa;
            ScenarioStarted = true;
            activeElapsedSeconds = 0f;
            if (taskDoor != null) taskDoor.Unlock();
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("Briefing complete — the attack simulation is live",
                    SafeGreen);
            if (taskDoor != null)
                BurstFX.SpawnAbove(taskDoor.transform, SafeGreen, 30, minimumHeight: 2.5f);
            UpdateObjective();
        }

        private void Update()
        {
            // The countdown represents hands-on scenario time, not time spent
            // reading a choice modal or a browser tab that lost focus. Using
            // unscaled delta keeps it honest if another menu changes
            // timeScale, while the focus/menu guards make WebGL pauses safe.
            if (ScenarioStarted && CurrentPhase != Phase.Complete &&
                !GameState.AnyMenuOpen && Application.isFocused)
                activeElapsedSeconds += Time.unscaledDeltaTime;

            if (ScenarioStarted && !TimeExpired && CurrentPhase != Phase.Complete && TimeRemaining <= 0f)
            {
                TimeExpired = true;
                DetectionPercent = Mathf.Clamp(DetectionPercent + Level4CyberAttackContent.TimeoutDetectionPenalty, 0, 100);
                LastFeedback = "TIME EXPIRED — the defensive telemetry caught the simulation late. Finish the run to review the lesson.";
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("TIME EXPIRED — defensive telemetry is catching up", AttackOrange);
                UpdateObjective();
            }

            if (pendingCompletion && !GameState.AnyMenuOpen)
            {
                pendingCompletion = false;
                CompleteLevel();
            }

            guidanceTimer += Time.deltaTime;
            if (guidanceTimer < 0.4f) return;
            guidanceTimer = 0f;
            UpdateGuidance();
        }

        private void UpdateGuidance()
        {
            ObjectiveBeacon beacon = ObjectiveBeacon.Ensure();
            Transform target = null;
            string action = string.Empty;

            if (CurrentPhase == Phase.Briefing)
            {
                target = briefing != null ? briefing.transform : null;
                action = "WATCH THE ATTACK BRIEFING";
            }
            else if (CurrentPhase == Phase.Complete)
            {
                exitDoor = exitDoor != null ? exitDoor : LevelSceneLookup.NearestExit();
                target = exitDoor != null ? exitDoor.transform : null;
                action = "RETURN TO THE HUB";
            }
            else if (stations != null && CurrentStationIndex >= 0 && CurrentStationIndex < stations.Length)
            {
                target = stations[CurrentStationIndex].transform;
                action = "RUN THE NEXT SIMULATED STAGE";
            }

            if (target != null) beacon.PointAt(target, action, AttackOrange);
            else beacon.Hide();
            UpdateObjective();
        }

        public Level4CyberAttackContent.StationScenario ScenarioFor(CyberAttackStation station)
        {
            if (station == null || scenarios == null || station.StationIndex < 0 ||
                station.StationIndex >= scenarios.Length) return null;
            return scenarios[station.StationIndex];
        }

        public bool IsStationComplete(CyberAttackStation station)
        {
            return station != null && station.StationIndex >= 0 && station.StationIndex < completed.Length &&
                completed[station.StationIndex];
        }

        public bool CanAttempt(CyberAttackStation station)
        {
            return station != null && !IsLevelComplete && ScenarioStarted &&
                station.StationIndex == CurrentStationIndex &&
                CurrentStationIndex >= 0 && CurrentStationIndex < scenarios.Length &&
                scenarios[CurrentStationIndex].kind == station.Kind &&
                !completed[CurrentStationIndex];
        }

        public string StationPrompt(CyberAttackStation station)
        {
            if (station == null) return "Cyber Attack Simulation";
            if (IsLevelComplete) return "Simulation complete";
            if (!ScenarioStarted) return "Watch the attack briefing first";
            if (IsStationComplete(station)) return "Stage complete — review lesson";
            if (station.StationIndex == CurrentStationIndex)
                return $"Run {Level4CyberAttackContent.DisplayName(station.Kind)} simulation";
            if (station.StationIndex < CurrentStationIndex) return "Stage complete — review lesson";
            return $"Locked — complete {Level4CyberAttackContent.DisplayName(scenarios[CurrentStationIndex].kind)} first";
        }

        public void ExplainUnavailable(CyberAttackStation station)
        {
            if (station == null || HudUI.Instance == null) return;
            if (!ScenarioStarted)
            {
                HudUI.Instance.ShowToast("Watch the controlled attack briefing before choosing a vector.", Gold);
                return;
            }
            if (station.StationIndex > CurrentStationIndex)
                HudUI.Instance.ShowToast("That stage is locked — follow the simulated attack chain in order.", Gold);
            else if (IsStationComplete(station))
                HudUI.Instance.ShowToast("Stage complete — use the checklist to review what the defense should catch.", SafeGreen);
        }

        public bool TryResolve(CyberAttackStation station, int optionIndex)
        {
            if (!CanAttempt(station)) return false;
            Level4CyberAttackContent.StationScenario scenario = scenarios[CurrentStationIndex];
            if (scenario.options == null || optionIndex < 0 || optionIndex >= scenario.options.Length)
            {
                LastFeedback = "That choice is not available in this scenario.";
                return false;
            }

            if (optionIndex != scenario.correctOption)
            {
                WrongAttempts++;
                DetectionPercent = Mathf.Clamp(DetectionPercent + scenario.detectionPenalty, 0, 100);
                LastFeedback = $"SIMULATION DETECTED  +{scenario.detectionPenalty}% caught — {scenario.lesson}";
                if (HudUI.Instance != null) HudUI.Instance.ShowToast("Choice detected — review the defensive control it bypassed.", AttackOrange);
                UpdateObjective();
                ProgressChanged?.Invoke();
                return false;
            }

            completed[CurrentStationIndex] = true;
            CorrectAttempts++;
            ExfiltrationPercent = Mathf.Clamp(ExfiltrationPercent + scenario.dataGain, 0, 100);
            ScoreSystem.Add(Mathf.RoundToInt(220f + scenario.dataGain * 4f - DetectionPercent * 0.35f));
            LastFeedback = scenario.successMessage + "  " + scenario.lesson;
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast(scenario.successMessage, SafeGreen);
            if (stations != null && CurrentStationIndex < stations.Length && stations[CurrentStationIndex] != null)
                BurstFX.SpawnAbove(stations[CurrentStationIndex].transform, SafeGreen, 26, minimumHeight: 2.0f);

            if (CurrentStationIndex >= scenarios.Length - 1)
            {
                CurrentPhase = Phase.Complete;
                pendingCompletion = true;
            }
            else
            {
                CurrentStationIndex++;
                CurrentPhase = (Phase)((int)Phase.BypassMfa + CurrentStationIndex);
            }

            UpdateObjective();
            ProgressChanged?.Invoke();
            return true;
        }

        private void UpdateObjective()
        {
            if (HudUI.Instance == null) return;
            string timer = ScenarioStarted && !IsLevelComplete
                ? $"  [T-{Mathf.FloorToInt(TimeRemaining / 60f)}:{Mathf.FloorToInt(TimeRemaining % 60f):00}]" : string.Empty;
            switch (CurrentPhase)
            {
                case Phase.Briefing:
                    HudUI.Instance.ShowObjective("Objective: Watch the controlled attack briefing  (E to play, ←/→ to scrub)");
                    break;
                case Phase.Complete:
                    HudUI.Instance.ShowObjective("LEVEL 4 COMPLETE — exit to the Hub");
                    break;
                default:
                    string name = CurrentStationIndex < scenarios.Length
                        ? Level4CyberAttackContent.DisplayName(scenarios[CurrentStationIndex].kind)
                        : "final stage";
                    // The dedicated meter owns the exfiltration/detection
                    // telemetry. Keeping that summary out of the objective
                    // banner prevents a duplicate timer and leaves the next
                    // action readable at narrow WebGL aspect ratios.
                    HudUI.Instance.ShowObjective($"Objective: Run {name}  ({CompletedCount}/4){timer}");
                    break;
            }
            HudUI.Instance.SetProgress(CurrentPhase == Phase.Complete ? 4 : CompletedCount, 4,
                CurrentPhase == Phase.Briefing ? ">" : CurrentPhase == Phase.Complete ? "OK" : null);
            UpdateMeter();
            UpdateTaskList();
        }

        private void UpdateMeter()
        {
            string status;
            switch (CurrentPhase)
            {
                case Phase.Briefing:
                    status = "BRIEFING  ·  WATCH THE RULES OF ENGAGEMENT";
                    break;
                case Phase.Complete:
                    status = "EXFILTRATION COMPLETE  ·  REPORT READY";
                    break;
                default:
                    string stage = CurrentStationIndex < scenarios.Length
                        ? Level4CyberAttackContent.DisplayName(scenarios[CurrentStationIndex].kind).ToUpperInvariant()
                        : "FINAL STAGE";
                    status = $"STAGE {Mathf.Clamp(CurrentStationIndex + 1, 1, 4)} / 4  ·  {stage}" +
                        (TimeExpired ? "  ·  OVERTIME" :
                            $"  ·  T-{Mathf.FloorToInt(TimeRemaining / 60f)}:{Mathf.FloorToInt(TimeRemaining % 60f):00}");
                    break;
            }
            Level4ExfiltrationMeter.Report(ExfiltrationPercent / 100f,
                DetectionPercent / 100f, status, IsLevelComplete);
        }

        private int CompletedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < completed.Length; i++) if (completed[i]) count++;
                return count;
            }
        }

        private void UpdateTaskList()
        {
            TaskListPanel list = TaskListPanel.Ensure(gameObject);
            list.SetHeader("CYBER ATTACK  //  RED TEAM");
            bool active = CurrentPhase != Phase.Briefing;
            list.Show(new List<TaskListPanel.Task>
            {
                new TaskListPanel.Task("Watch briefing", active, !active),
                new TaskListPanel.Task("Bypass MFA", completed[0], active && CurrentStationIndex == 0),
                new TaskListPanel.Task("Escalate privileges", completed[1], active && CurrentStationIndex == 1),
                new TaskListPanel.Task("Extract data", completed[2], active && CurrentStationIndex == 2),
                new TaskListPanel.Task("Cover tracks", completed[3], active && CurrentStationIndex == 3),
                new TaskListPanel.Task("Return to Hub", IsLevelComplete, IsLevelComplete),
            });
        }

        private void CompleteLevel()
        {
            if (ReportShown || CompletedCount < scenarios.Length) return;
            ReportShown = true;
            ObjectiveBeacon.Ensure().Hide();
            UpdateObjective();

            string overtime = TimeExpired ? "  ·  TIME EXPIRED" : string.Empty;
            string result = $"SIMULATION COMPLETE — DATA STOLEN {ExfiltrationPercent}% / CAUGHT {DetectionPercent}%{overtime}";
            Level4ExfiltrationMeter.Report(ExfiltrationPercent / 100f,
                DetectionPercent / 100f, "EXFILTRATION COMPLETE  ·  REPORT READY", true);
            LevelMissionRuntime.Complete(new LevelMissionRuntime.Completion
            {
                levelNumber = 4,
                exitDoor = exitDoor,
                accent = Gold,
                elapsedSeconds = ElapsedSeconds,
                headerText = "LEVEL 4 COMPLETE",
                grantedLine = result,
                nextMissionText = "Campaign complete — replay to improve your stealth score.",
                replaySuffix = "Level 4",
                parScore = 1400,
            });
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Deterministic hook for the campaign TAS and PlayMode tests.
        /// It resolves the same authored choices as the UI without launching
        /// any tools or touching a real system.</summary>
        public void CompleteForAutomation()
        {
            if (!ScenarioStarted) OnBriefingCompleted();
            if (stations == null || stations.Length == 0)
                stations = FindObjectsOfType<CyberAttackStation>();
            for (int i = 0; i < scenarios.Length; i++)
            {
                CyberAttackStation station = null;
                for (int j = 0; j < stations.Length; j++)
                    if (stations[j] != null && stations[j].StationIndex == i) { station = stations[j]; break; }
                if (station != null && !completed[i]) TryResolve(station, scenarios[i].correctOption);
            }
        }
#endif
    }
}
