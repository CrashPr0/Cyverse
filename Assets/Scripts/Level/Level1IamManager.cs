using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 1 (I/AM) flow, implementing the level template:
    ///   Watch    — the briefing screen must be viewed once; the divider door
    ///              unlocks on first completion (video stays repeatable)
    ///   Task     — review all four I/AM stations (dialogue + knowledge check)
    ///   Complete — level marked done (unlocking Level 2 in the Hub), results
    ///              shown, exit door to the Hub opens.
    /// Discovers its pieces from the scene, so procedural and hand-built
    /// scenes both work; self-heals shared systems like the other managers.
    /// </summary>
    public class Level1IamManager : MonoBehaviour
    {
        public enum Phase { Watch, Task, Complete }

        public static Level1IamManager Instance { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Watch;

        private readonly List<StationSetup> stations = new List<StationSetup>();
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
            if (ResultsScreen.Instance == null) gameObject.AddComponent<ResultsScreen>();
            if (VisualDirector.Instance == null) gameObject.AddComponent<VisualDirector>();

            var cam = Camera.main;
            if (cam != null && cam.GetComponent<FirstPersonHands>() == null)
                cam.gameObject.AddComponent<FirstPersonHands>();
            if (Audio.AmbientHum.Instance == null) gameObject.AddComponent<Audio.AmbientHum>();
            if (GlossaryPanel.Instance == null) gameObject.AddComponent<GlossaryPanel>();

            stations.AddRange(FindObjectsOfType<StationSetup>());
            briefing = FindObjectOfType<VideoStation>();
            taskDoor = FindObjectOfType<LockedDoor>();
            exitDoor = FindObjectOfType<HubDoor>();

            if (briefing != null) briefing.FirstCompleted += OnBriefingCompleted;
            else OnBriefingCompleted(); // no screen in scene — don't soft-lock

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();
            UpdateObjective();
        }

        private void OnBriefingCompleted()
        {
            if (CurrentPhase != Phase.Watch) return;
            CurrentPhase = Phase.Task;

            if (taskDoor != null) taskDoor.Unlock();
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("Briefing complete — Task Room unlocked", new Color(0.30f, 1f, 0.45f));
            BurstFX.Spawn(taskDoor != null ? taskDoor.transform.position + Vector3.up * 2.5f : Vector3.up * 2f,
                new Color(0.30f, 1f, 0.45f), 30);
            UpdateObjective();
        }

        /// <summary>Wired to each station's onReviewed by the scene factory.</summary>
        public void NotifyStationReviewed()
        {
            UpdateObjective();
            if (CurrentPhase != Phase.Task) return;
            if (ReviewedCount < stations.Count || stations.Count == 0) return;
            CompleteLevel();
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
                case Phase.Watch:
                    HudUI.Instance.ShowObjective("Objective: Watch the security briefing  (E to play, ←/→ to scrub)");
                    HudUI.Instance.SetProgress(0, stations.Count, "▶");
                    break;
                case Phase.Task:
                    HudUI.Instance.ShowObjective(
                        $"Objective: Review the I/AM stations  ({ReviewedCount}/{stations.Count})");
                    HudUI.Instance.SetProgress(ReviewedCount, stations.Count);
                    break;
                case Phase.Complete:
                    HudUI.Instance.ShowObjective("LEVEL 1 COMPLETE — exit to the Hub");
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
