using System;

namespace Cyverse.Core
{
    /// <summary>
    /// Global score for the gamification loop (points for reviewing stations,
    /// answering knowledge checks, and authenticating). Static so any system
    /// can award points; the HUD subscribes to Changed to animate the counter.
    /// </summary>
    public static class ScoreSystem
    {
        public static int Score { get; private set; }

        /// <summary>Knowledge-check tallies for the results screen.</summary>
        public static int QuizCorrect;
        public static int QuizTotal;

        /// <summary>(new total, points just added)</summary>
        public static event Action<int, int> Changed;

        public static void Add(int points)
        {
            Score += points;
            Changed?.Invoke(Score, points);
        }

        public static void Reset()
        {
            Score = 0;
            QuizCorrect = 0;
            QuizTotal = 0;
            Changed?.Invoke(0, 0);
        }
    }
}
