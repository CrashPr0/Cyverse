using UnityEngine;
using Cyverse.Quiz;

namespace Cyverse.Level
{
    /// <summary>
    /// Knowledge-check question bank for Level 1 (Cyber Defense / SOC Analyst).
    /// Two questions per topic; one is picked at random each time so replays
    /// vary. Keep questions here (data only) so educators can review and edit
    /// copy without touching gameplay code. Mirrors Level0Quiz's shape.
    /// </summary>
    public static class Level1Quiz
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
                case StationSetup.Topic.EDR: return Edr;
                case StationSetup.Topic.INCIDENT: return Incident;
                default: return Siem;
            }
        }

        private static readonly QuizQuestion[] Siem =
        {
            new QuizQuestion(
                "What is the main advantage of a SIEM over checking individual system logs?",
                new[] { "It correlates events across systems", "It encrypts data at rest", "It replaces firewalls" },
                0,
                "SIEM correlates log data from across the network to reveal patterns a single log wouldn't show."),
            new QuizQuestion(
                "Reviewing and prioritizing SIEM alerts to separate real threats from noise is called:",
                new[] { "Alert triage", "Eradication", "Recovery" },
                0,
                "Alert triage sorts real threats from false positives before escalating to incident response."),
        };

        private static readonly QuizQuestion[] Edr =
        {
            new QuizQuestion(
                "EDR isolating a compromised laptop from the network is an example of:",
                new[] { "Containment", "Identification", "Recovery" },
                0,
                "Containment isolates an affected endpoint so the threat can't spread further."),
            new QuizQuestion(
                "Which best describes AI-driven detection in modern EDR tools?",
                new[] { "Manually reviewing every log line", "Recognizing attack patterns via machine learning", "Physically disconnecting the network" },
                1,
                "AI-driven detection uses machine learning to spot attack patterns faster and at greater scale than manual review."),
        };

        private static readonly QuizQuestion[] Incident =
        {
            new QuizQuestion(
                "Patching a vulnerability to reduce the severity of a threat is an example of:",
                new[] { "Mitigation", "Identification", "Authentication" },
                0,
                "Mitigation is any action that reduces the severity or likelihood of a threat."),
            new QuizQuestion(
                "Removing the root cause of an incident — not just its symptoms — is called:",
                new[] { "Recovery", "Eradication", "Containment" },
                1,
                "Eradication removes the underlying cause (malware, a backdoor, a compromised account), not just what's visible."),
        };
    }
}
