using Cyverse.Forensics;
using Cyverse.Quiz;

namespace Cyverse.Level
{
    /// <summary>
    /// End-of-level knowledge check for Level 3, asked after the forensic
    /// report is filed and before the results card. One question per learning
    /// goal the lab teaches; every answer is something the player did or saw.
    /// Data only, so educators can review the wording without touching the
    /// flow (the answer key is in Docs/Level3-Forensics-Guide.md, section 5.6).
    /// Options are shuffled when asked, so the first option here is not
    /// necessarily first on screen.
    /// </summary>
    public static class Level3KnowledgeCheck
    {
        public static readonly QuizQuestion[] Questions =
        {
            new QuizQuestion(
                "Entry 2 of the custody form moved the phone from the SOC to the lab. Which item number did it keep?",
                new[] { "#1, the same item number at every handoff", "#2, because it is a new entry", "None, it stayed inside the company" },
                0,
                "An item keeps its number for its whole life. Each handoff adds a row, not a new item."),
            new QuizQuestion(
                "Why was the phone imaged through a write blocker and the copy checked with a SHA-256 hash?",
                new[] { "To prove the copy matches the phone and nothing changed it", "To unlock the phone without its passcode", "To make the processing run faster" },
                0,
                "The write blocker stops any change to the evidence; matching hashes prove the copy is identical."),
            new QuizQuestion(
                "Case 2 needed deleted chats and photo GPS. Which acquisition finds them?",
                new[] { "Full file system", "Quick (logical)", "SIM card only" },
                0,
                "A quick logical copy skips app databases, deleted records and photo metadata."),
            new QuizQuestion(
                $"The Gold Claim app talked to {PhoneImage.AttackerIp}, and gold-updates.net pointed to the same IP. What is that step called?",
                new[] { "Pivoting: following one clue to a new lead", "Hashing", "Chain of custody" },
                0,
                "Pivoting uses one finding (the IP) to uncover another (a second attacker domain)."),
            new QuizQuestion(
                "AI triage flagged the sunset photo as a possible deepfake. What should an examiner do?",
                new[] { "Treat it as a lead and check the metadata before trusting it", "Report it as proof", "Delete the photo from the image" },
                0,
                "AI flags are leads, not proof. The sunset photo's metadata was ordinary, so the flag was wrong."),
        };
    }
}
