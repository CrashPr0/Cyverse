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

        /// <summary>The glossary panel is open.</summary>
        public static bool GlossaryOpen;

        /// <summary>Level 0 has been finished.</summary>
        public static bool LevelComplete;

        /// <summary>True whenever normal first-person control should be suspended.</summary>
        public static bool Busy => DialogueActive || MenuOpen || QuizActive || TitleActive || GlossaryOpen || LevelComplete;

        /// <summary>
        /// A full-screen menu/modal currently owns the screen (title, settings,
        /// glossary, quiz, or results). THE standard for UI exclusivity: only
        /// one of these may be visible at a time — anything wanting to open
        /// must check this first, and passive overlays (controls card, etc.)
        /// must hide while it's true. Dialogue captions are gameplay, not a
        /// menu, so DialogueActive is deliberately not included.
        /// </summary>
        public static bool AnyMenuOpen => TitleActive || MenuOpen || GlossaryOpen || QuizActive || LevelComplete;

        /// <summary>
        /// Frame on which a menu last opened or closed. Menus that share a key
        /// (Esc closes the glossary AND toggles settings) must ignore input on
        /// this frame, or one keypress can close one menu and open another in
        /// the same Update cycle (whichever component happens to run later).
        /// </summary>
        public static int MenuTransitionFrame = -1;

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
            GlossaryOpen = false;
            LevelComplete = false;
            MenuTransitionFrame = -1;
        }
    }
}
