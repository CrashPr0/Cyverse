using UnityEngine;
using Cyverse.Quiz;

namespace Cyverse.Level
{
    /// <summary>
    /// Knowledge-check question bank for Level 0, written against the CyVerse
    /// Script content. Two questions per topic; one is picked at random each
    /// time so replays vary. Keep questions here (data only) so educators can
    /// review and edit copy without touching gameplay code.
    /// </summary>
    public static class Level0Quiz
    {
        public static QuizQuestion For(StationSetup.Topic topic)
        {
            QuizQuestion[] pool = PoolFor(topic);
            return pool[Random.Range(0, pool.Length)];
        }

        private static QuizQuestion[] PoolFor(StationSetup.Topic topic)
        {
            switch (topic)
            {
                case StationSetup.Topic.CIA: return Cia;
                case StationSetup.Topic.NICE: return Nice;
                default: return Iam;
            }
        }

        private static readonly QuizQuestion[] Iam =
        {
            new QuizQuestion(
                "Which step of I/AM proves you are who you claim to be?",
                new[] { "Identification", "Authentication", "Authorization" },
                1,
                "Authentication verifies a claimed identity — a password, a device, or a biometric."),
            new QuizQuestion(
                "You logged in fine, but the payroll folder is blocked. Which I/AM step is stopping you?",
                new[] { "Authorization", "Identification", "Accountability" },
                0,
                "Authorization decides what a verified user is allowed to access."),
        };

        private static readonly QuizQuestion[] Cia =
        {
            new QuizQuestion(
                "An attacker secretly alters financial records. Which CIA principle is violated?",
                new[] { "Confidentiality", "Integrity", "Availability" },
                1,
                "Integrity guards against improper modification or destruction of information."),
            new QuizQuestion(
                "Keeping private data away from unauthorized users is which CIA principle?",
                new[] { "Confidentiality", "Integrity", "Availability" },
                0,
                "Confidentiality means only authorized people can access the information."),
        };

        private static readonly QuizQuestion[] Nice =
        {
            new QuizQuestion(
                "Which NICE workforce role investigates cybercrime and digital evidence?",
                new[] { "Investigation", "Design & Development", "Oversight & Governance" },
                0,
                "Investigation conducts cybercrime investigations and analyzes digital evidence."),
            new QuizQuestion(
                "A SOC analyst identifying and analyzing threats fits which NICE role?",
                new[] { "Protection & Defense", "Implementation & Operation", "Investigation" },
                0,
                "Protection & Defense protects against, identifies, and analyzes risks to systems."),
        };
    }
}
