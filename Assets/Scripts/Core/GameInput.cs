using UnityEngine;

namespace Cyverse.Core
{
    /// <summary>
    /// Key bindings shared by every screen, so a binding means the same thing
    /// everywhere.
    /// </summary>
    public static class GameInput
    {
        /// <summary>The "back / close" key: Esc or Tab. Tab doubles for Esc
        /// because in a browser Esc also releases the mouse (and leaves
        /// fullscreen), so closing a panel with it drops the player out of
        /// mouse look. Screens that give Tab its own job (command completion,
        /// show-password) keep handling Esc alone.</summary>
        public static bool CancelDown =>
            Input.GetKeyDown(KeyCode.Escape) || (Input.GetKeyDown(KeyCode.Tab) && TabReservedBy == null);

        /// <summary>Set by a screen that uses Tab for its own job while it is
        /// up, so Tab does not also close or open anything else.</summary>
        public static object TabReservedBy;

        /// <summary>How the cancel key is written in on-screen hints.</summary>
        public const string CancelLabel = "ESC / TAB";

        /// <summary>Briefing-screen scrub keys: the arrows, or [ and ] while
        /// the arrows move the player.</summary>
        public static float ScrubAxis()
        {
            bool arrowsMove = Cyverse.Settings.AccessibilitySettings.ArrowKeysMove;
            KeyCode back = arrowsMove ? KeyCode.LeftBracket : KeyCode.LeftArrow;
            KeyCode ahead = arrowsMove ? KeyCode.RightBracket : KeyCode.RightArrow;
            return (Input.GetKey(ahead) ? 1f : 0f) - (Input.GetKey(back) ? 1f : 0f);
        }

        /// <summary>How the scrub keys are written in on-screen hints.</summary>
        public static string ScrubLabel =>
            Cyverse.Settings.AccessibilitySettings.ArrowKeysMove ? "[ / ]" : "←/→";
    }
}
