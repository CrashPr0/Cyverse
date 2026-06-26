using System;
using System.Collections.Generic;
using UnityEngine;
using Cyverse.Dialogue;

namespace Cyverse.Interaction
{
    /// <summary>
    /// A learning station in Level 0 (the I/AM kiosk, the CIA Triad hologram,
    /// the NICE roles board). Interacting plays its dialogue; the first time it
    /// finishes, the station reports completion to the level manager.
    /// </summary>
    public class InteractableStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private string stationId = "station";
        [SerializeField] private string promptText = "Inspect";

        private List<DialogueLine> lines = new List<DialogueLine>();
        private bool completed;

        /// <summary>Raised once, the first time this station's dialogue completes.</summary>
        public event Action<InteractableStation> Completed;

        public string Id => stationId;
        public bool IsCompleted => completed;

        public string Prompt => completed ? $"{promptText}  (reviewed)" : promptText;
        public bool CanInteract => true;

        /// <summary>Called by the bootstrap to inject this station's content.</summary>
        public void Configure(string id, string prompt, List<DialogueLine> dialogue)
        {
            stationId = id;
            promptText = prompt;
            lines = dialogue ?? new List<DialogueLine>();
        }

        public void Interact(GameObject interactor)
        {
            if (DialogueManager.Instance == null) return;
            DialogueManager.Instance.Play(lines, OnDialogueDone);
        }

        private void OnDialogueDone()
        {
            if (completed) return;
            completed = true;
            Completed?.Invoke(this);
        }
    }
}
