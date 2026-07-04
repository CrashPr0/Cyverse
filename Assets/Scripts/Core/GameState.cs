namespace Cyverse.Core
{
    /// <summary>
    /// Lightweight global flags that gameplay systems read to decide whether
    /// the player should be able to move, look around, or interact. Keeping
    /// these in one place avoids every system holding references to every
    /// other system just to ask "are we busy right now?".
    /// </summary>
    public static class GameState
    {
        /// <summary>A dialogue / voiceover beat is currently playing.</summary>
        public static bool DialogueActive;

        /// <summary>The pause / accessibility menu is open.</summary>
        public static bool MenuOpen;

        /// <summary>A knowledge-check quiz is on screen.</summary>
        public static bool QuizActive;

        /// <summary>The title screen is showing (before the level begins).</summary>
        public static bool TitleActive;

        /// <summary>Level 0 has been finished.</summary>
        public static bool LevelComplete;

        /// <summary>True whenever normal first-person control should be suspended.</summary>
        public static bool Busy => DialogueActive || MenuOpen || QuizActive || TitleActive || LevelComplete;

        /// <summary>
        /// Static fields survive Play-mode restarts when "Enter Play Mode
        /// Options" disables domain reload, so the bootstrap resets them.
        /// </summary>
        public static void Reset()
        {
            DialogueActive = false;
            MenuOpen = false;
            QuizActive = false;
            TitleActive = false;
            LevelComplete = false;
        }
    }
}
