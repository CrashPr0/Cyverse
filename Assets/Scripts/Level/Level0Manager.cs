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
    /// Orchestrates Level 0: plays the intro, tracks which learning stations
    /// the player has reviewed, updates the objective banner, and triggers the
    /// "Access Granted" completion once every station is done.
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

        /// <summary>Bootstrap calls this for each station it spawns.</summary>
        public void Register(InteractableStation station)
        {
            stations.Add(station);
            station.Completed += OnStationCompleted;
        }

        /// <summary>Kick off the level: play the intro, then show the objective.</summary>
        public void Begin()
        {
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Play(Level0Content.Intro(), UpdateObjective);
            else
                UpdateObjective();
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
