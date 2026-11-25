using UnityEngine;

namespace Cyverse.Core
{
    /// <summary>
    /// Key bindings shared by every screen, so a binding means the same thing
    /// everywhere.
    /// </summary>
    public static class GameInput
    {
        /// <summary>The "back / close" key: Tab, on every screen. Esc is
        /// kept for Settings only (see <see cref="SettingsDown"/>): one key,
        /// one job, because players found two keys that both closed screens
        /// and both opened Settings confusing, and in a browser Esc also lets
        /// go of the mouse. A screen that gives Tab its own job (show-password)
        /// falls back to Esc while it is up.</summary>
        public static bool CancelDown => TabReservedBy == null
            ? Input.GetKeyDown(KeyCode.Tab)
            : Input.GetKeyDown(KeyCode.Escape);

        /// <summary>The Settings (pause) key: Esc. It never closes or skips
        /// another screen.</summary>
        public static bool SettingsDown =>
            TabReservedBy == null && Input.GetKeyDown(KeyCode.Escape);

        /// <summary>Set by a screen that uses Tab for its own job while it is
        /// up, so Tab does not also close or open anything else.</summary>
        public static object TabReservedBy;

        /// <summary>How the cancel key is written in on-screen hints.</summary>
        public const string CancelLabel = "TAB";

        /// <summary>How the Settings key is written in on-screen hints.</summary>
        public const string SettingsLabel = "ESC";

        /// <summary>Briefing-screen scrubbing: the left/right arrows. While
        /// the arrows also move the player, they scrub only while the player
        /// is looking at the screen (<paramref name="watching"/>) — and stop
        /// strafing then (see FirstPersonController).</summary>
        public static float ScrubAxis(bool watching)
        {
            bool arrowsScrub = watching || !Cyverse.Settings.AccessibilitySettings.ArrowKeysMove;
            if (!arrowsScrub) return 0f;
            return (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        }

        /// <summary>How the scrub keys are written in on-screen hints.</summary>
        public const string ScrubLabel = "←/→";
    }
}
