using System.Collections.Generic;
using Cyverse.Core;
using Cyverse.Dialogue;
using Cyverse.Interaction;
using Cyverse.Quiz;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 1 (I/AM) content: the video-room briefing slides (placeholder for
    /// the real video — same timings, scrubbable), the four task-room station
    /// lessons, and their knowledge checks. Data only; edit copy freely.
    /// </summary>
    public static class Level1IamContent
    {
        // ---- Briefing (video room). Swap for a real video by assigning a
        // clip/URL on the BriefingScreen's VideoStation in the editor. --------

        public static VideoStation.Slide[] BriefingSlides() => new[]
        {
            new VideoStation.Slide("I / AM",
                "Identification and Access Management: the security controls that verify who you are and decide what you can access. Four steps — Identification, Authentication, Authorization, Accountability.", 10f),
            new VideoStation.Slide("IDENTIFICATION",
                $"Claiming an identity — telling the system who you are. A username, email, or your employee ID ({PlayerIdentity.Callsign}) are identifiers.", 9f),
            new VideoStation.Slide("AUTHENTICATION",
                "Proving the claim: something you know (a password), something you have (a device or token), or something you are (a fingerprint or face).", 9f),
            new VideoStation.Slide("AUTHORIZATION + ACCOUNTABILITY",
                "Once verified, authorization decides what you may do — and accountability logs what you did, so actions can be traced to a person.", 10f),
        };

        // ---- Task room stations ----------------------------------------------

        public static List<DialogueLine> Identification() => new List<DialogueLine>
        {
            new DialogueLine("Identification",
                $"Identification is claiming an identity — how you tell a system who you are. Your employee ID, {PlayerIdentity.Callsign}, is an identifier; so is a username or an email address."),
            new DialogueLine("Identification",
                "Identifiers distinguish you from every other user, but a claim alone proves nothing — that's the next step's job."),
        };

        public static List<DialogueLine> Authentication() => new List<DialogueLine>
        {
            new DialogueLine("Authentication",
                "Authentication proves you are who you claim to be: something you know (a password), something you have (a token or device), or something you are (a biometric)."),
            new DialogueLine("Authentication",
                "Combining two different kinds is multi-factor authentication — far stronger than any single factor."),
        };

        public static List<DialogueLine> Authorization() => new List<DialogueLine>
        {
            new DialogueLine("Authorization",
                "Authorization comes after your identity is verified: the system decides what you're allowed to do. Being inside a system doesn't mean you can access everything in it."),
        };

        public static List<DialogueLine> Accountability() => new List<DialogueLine>
        {
            new DialogueLine("Accountability",
                "Accountability means actions are traceable to a person. Activity is logged and audited — a digital trail that supports investigations and compliance."),
        };

        // ---- Knowledge checks (one fixed question per station) ----------------

        public static QuizQuestion IdentificationQuiz() => new QuizQuestion(
            "Typing your username at a login screen is an example of:",
            new[] { "Identification", "Authorization", "Accountability" },
            0,
            "Identification is claiming an identity — the username tells the system who you say you are.");

        public static QuizQuestion AuthenticationQuiz() => new QuizQuestion(
            "A fingerprint scan is which kind of authentication factor?",
            new[] { "Something you know", "Something you have", "Something you are" },
            2,
            "Biometrics — fingerprints, faces — are \"something you are.\"");

        public static QuizQuestion AuthorizationQuiz() => new QuizQuestion(
            "You're logged in, but the payroll folder is blocked. Which step is stopping you?",
            new[] { "Identification", "Authorization", "Authentication" },
            1,
            "Authorization decides what a verified user is allowed to access.");

        public static QuizQuestion AccountabilityQuiz() => new QuizQuestion(
            "Audit logs that trace actions back to specific users support which I/AM step?",
            new[] { "Accountability", "Identification", "Authentication" },
            0,
            "Accountability connects actions to people via logging and auditing.");
    }
}
