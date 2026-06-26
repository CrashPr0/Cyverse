using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Dialogue;
using Cyverse.Interaction;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Orchestrates Level 0: on Start it discovers every station in the scene
    /// (so it works for both the procedural and hand-built scenes), plays the
    /// intro, tracks which stations have been reviewed, updates the objective
    /// banner, and triggers the "Access Granted" completion.
    /// </summary>
    public class Level0Manager : MonoBehaviour
    {
        public static Level0Manager Instance { get; private set; }

        private readonly List<InteractableStation> stations = new List<InteractableStation>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            foreach (var s in FindObjectsOfType<InteractableStation>())
                Register(s);

            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Play(Level0Content.Intro(), UpdateObjective);
            else
                UpdateObjective();
        }

        private void Register(InteractableStation station)
        {
            if (stations.Contains(station)) return;
            stations.Add(station);
            station.Completed += OnStationCompleted;
        }

        private void OnStationCompleted(InteractableStation station)
        {
            UpdateObjective();
            if (CompletedCount >= stations.Count && stations.Count > 0)
                CompleteLevel();
        }

        private int CompletedCount
        {
            get
            {
                int n = 0;
                foreach (var s in stations) if (s.IsCompleted) n++;
                return n;
            }
        }

        private void UpdateObjective()
        {
            if (HudUI.Instance != null)
                HudUI.Instance.ShowObjective($"Objective: Review all stations  ({CompletedCount}/{stations.Count})");
        }

        private void CompleteLevel()
        {
            GameState.LevelComplete = true;
            if (HudUI.Instance != null) HudUI.Instance.ShowObjective("LEVEL 0 COMPLETE");
            FirstPersonController.LockCursor(false);

            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Play(Level0Content.Complete());
        }
    }
}
