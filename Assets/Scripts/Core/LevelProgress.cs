using UnityEngine;

namespace Cyverse.Core
{
    /// <summary>
    /// Persistent level completion/unlock state driving the Hub doors.
    /// Level indices: 0 = Orientation (Level0 scene), 1 = I/AM, 2 = Cyber
    /// Defense (SOC), 3 = Digital Forensics, 4 = Cyber Attack.
    /// Rule: levels 0 and 1 are always open; level N unlocks when N-1 is
    /// completed. Stored in PlayerPrefs so it works on WebGL with no backend.
    /// </summary>
    public static class LevelProgress
    {
        private static string Key(int level) => "cv_done_" + level;

        public static bool IsCompleted(int level) => PlayerPrefs.GetInt(Key(level), 0) == 1;

        public static void MarkCompleted(int level)
        {
            PlayerPrefs.SetInt(Key(level), 1);
            PlayerPrefs.Save();
        }

        public static bool IsUnlocked(int level) => level <= 1 || IsCompleted(level - 1);

        /// <summary>Completed count among the story levels 1..4 (Orientation excluded).</summary>
        public static int CompletedStoryLevels()
        {
            int n = 0;
            for (int i = 1; i <= 4; i++)
                if (IsCompleted(i)) n++;
            return n;
        }
    }
}
