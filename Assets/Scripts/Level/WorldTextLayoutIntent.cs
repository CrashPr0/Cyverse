using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Declares how a world-space text element participates in scene layout.
    /// Text creators own this decision; the layout manager only enforces it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldTextLayoutIntent : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>Authored onto a panel, monitor, wall, or other surface.</summary>
            Mounted,

            /// <summary>Wayfinding text that may fade when it overlaps stronger content.</summary>
            Floating,

            /// <summary>
            /// Interaction text that must stay readable and reserve its screen space.
            /// It may still billboard, but it is never treated as optional signage.
            /// </summary>
            InteractionCritical
        }

        [SerializeField] private Mode layoutMode = Mode.Mounted;
        [SerializeField] private int layoutPriority = 100;

        public Mode LayoutMode => layoutMode;
        public int LayoutPriority => layoutPriority;
        public bool IsFloating => layoutMode == Mode.Floating;

        /// <summary>Idempotently attaches or updates an explicit layout contract.</summary>
        public static WorldTextLayoutIntent Configure(GameObject target, Mode mode,
            int priority = 100)
        {
            if (target == null) return null;
            WorldTextLayoutIntent intent = target.GetComponent<WorldTextLayoutIntent>();
            if (intent == null) intent = target.AddComponent<WorldTextLayoutIntent>();
            intent.layoutMode = mode;
            intent.layoutPriority = Mathf.Max(0, priority);
            return intent;
        }
    }
}
