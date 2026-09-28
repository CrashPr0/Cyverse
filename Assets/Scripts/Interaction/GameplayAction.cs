using UnityEngine;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The small action vocabulary shared by live input, UI controls,
    /// deterministic playthroughs, and integration tests. Targets own the
    /// rules for accepting an action; adapters only translate their input
    /// source into one of these commands.
    /// </summary>
    public enum GameplayActionKind
    {
        Interact,
        Move,
        Navigate,
        Choose,
        Select,
        AppendText,
        PasteText,
        Backspace,
        ClearText,
        Submit,
        Scrub,
        Toggle,
        CompleteText,
        Cancel,
    }

    /// <summary>Immutable payload for one player-visible gameplay action.</summary>
    public readonly struct GameplayAction
    {
        public GameplayActionKind Kind { get; }
        public int Index { get; }
        public int ValueIndex { get; }
        public float Amount { get; }
        public string Text { get; }
        public Vector3 Vector { get; }

        private GameplayAction(GameplayActionKind kind, int index = 0,
            int valueIndex = 0, float amount = 0f, string text = null,
            Vector3 vector = default)
        {
            Kind = kind;
            Index = index;
            ValueIndex = valueIndex;
            Amount = amount;
            Text = text;
            Vector = vector;
        }

        public static GameplayAction Interact() => new GameplayAction(GameplayActionKind.Interact);
        public static GameplayAction Move(Vector3 worldVelocity, float deltaSeconds) =>
            new GameplayAction(GameplayActionKind.Move, amount: deltaSeconds, vector: worldVelocity);
        public static GameplayAction Navigate(int delta) =>
            new GameplayAction(GameplayActionKind.Navigate, index: delta);
        public static GameplayAction Choose(int optionIndex) =>
            new GameplayAction(GameplayActionKind.Choose, index: optionIndex);
        public static GameplayAction Select(int targetIndex, int valueIndex) =>
            new GameplayAction(GameplayActionKind.Select, targetIndex, valueIndex);
        public static GameplayAction Append(string text) =>
            new GameplayAction(GameplayActionKind.AppendText, text: text ?? string.Empty);
        public static GameplayAction Paste(string text) =>
            new GameplayAction(GameplayActionKind.PasteText, text: text ?? string.Empty);
        public static GameplayAction Backspace() => new GameplayAction(GameplayActionKind.Backspace);
        public static GameplayAction ClearText() => new GameplayAction(GameplayActionKind.ClearText);
        public static GameplayAction Submit() => new GameplayAction(GameplayActionKind.Submit);
        public static GameplayAction Scrub(float seconds) =>
            new GameplayAction(GameplayActionKind.Scrub, amount: seconds);
        public static GameplayAction Toggle() => new GameplayAction(GameplayActionKind.Toggle);
        public static GameplayAction CompleteText() => new GameplayAction(GameplayActionKind.CompleteText);
        public static GameplayAction Cancel() => new GameplayAction(GameplayActionKind.Cancel);
    }

    /// <summary>
    /// Implemented by gameplay surfaces that accept actions beyond the
    /// ordinary world interaction. Implementations must use their normal
    /// validation, scoring, feedback, and completion path.
    /// </summary>
    public interface IGameplayActionTarget
    {
        bool TryApply(GameplayAction action, GameObject actor);
    }

    /// <summary>
    /// Single dispatch seam for every input adapter. World interaction keeps
    /// using IInteractable; richer screens opt into IGameplayActionTarget.
    /// </summary>
    public static class GameplayActions
    {
        public static bool TryApply(object target, GameplayAction action, GameObject actor = null)
        {
            if (target == null || (target is Object unityTarget && unityTarget == null))
                return false;

            if (action.Kind == GameplayActionKind.Interact)
            {
                if (!(target is IInteractable interactable) || !interactable.CanInteract)
                    return false;
                interactable.Interact(actor);
                return true;
            }

            return target is IGameplayActionTarget actionTarget &&
                actionTarget.TryApply(action, actor);
        }
    }
}
