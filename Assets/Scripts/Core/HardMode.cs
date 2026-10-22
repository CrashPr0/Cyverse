using UnityEngine;

namespace Cyverse.Core
{
    /// <summary>
    /// The Hard copies of the levels, reached through the Hub's west wing.
    /// A Hard run loads the same scene as Normal with this flag on; each level
    /// reads it at load to switch on its Hard rules (no guide beacon, the
    /// level's own mechanical twists, a par clock). Hard for level N unlocks
    /// once N is complete on Normal.
    ///
    /// The flag is static (session) state: a Hub door sets it on the way in,
    /// the Hub clears it, and a reload from the results screen keeps it, so
    /// "R to replay" replays Hard. Clears and best scores live in PlayerPrefs
    /// apart from Normal's, so Hard never changes campaign progress.
    /// </summary>
    public static class HardMode
    {
        /// <summary>Positive points are scaled by this on a Hard run.</summary>
        public const float ScoreMultiplier = 1.5f;

        public static bool Active { get; private set; }

        /// <summary>The level the current Hard run is for, or -1.</summary>
        public static int Level { get; private set; } = -1;

        public static bool IsActiveFor(int level) => Active && Level == level;

        public static void Begin(int level)
        {
            Active = true;
            Level = level;
        }

        public static void End()
        {
            Active = false;
            Level = -1;
        }

        private static string ClearedKey(int level) => "cv_hard_done_" + level;
        private static string BestKey(int level) => "cv_hard_best_" + level;

        public static bool IsUnlocked(int level) => LevelProgress.IsCompleted(level);

        public static bool IsCleared(int level) => PlayerPrefs.GetInt(ClearedKey(level), 0) == 1;

        public static int BestScore(int level) => PlayerPrefs.GetInt(BestKey(level), 0);

        /// <summary>Records a Hard clear; returns true when the score is a new best.</summary>
        public static bool MarkCleared(int level, int score)
        {
            bool best = score > BestScore(level);
            PlayerPrefs.SetInt(ClearedKey(level), 1);
            if (best) PlayerPrefs.SetInt(BestKey(level), score);
            PlayerPrefs.Save();
            return best;
        }
    }
}
