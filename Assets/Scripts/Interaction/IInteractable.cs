using UnityEngine;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Anything the player can look at and activate with the interact key.
    /// Implemented by stations (kiosks, holograms, boards) in Level 0.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Short text shown in the HUD prompt, e.g. "Inspect Terminal".</summary>
        string Prompt { get; }

        /// <summary>Whether this object can currently be interacted with.</summary>
        bool CanInteract { get; }

        /// <summary>Called when the player presses interact while looking at it.</summary>
        void Interact(GameObject interactor);
    }
}
