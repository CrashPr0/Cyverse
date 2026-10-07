using System;
using System.Collections.Generic;
using Cyverse.Audio;
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

        /// <summary>Recorded MFA explainer, played once as the player first
        /// reaches the MFA Vault: it introduces exactly the three factors the
        /// vault asks for. Captions match the audio word for word.</summary>
        public static List<DialogueLine> MfaBriefing() => new List<DialogueLine>
        {
            new DialogueLine("MFA",
                "Multi-factor authentication, also known as MFA, is a security method used to protect data, information, and systems from unauthorized access.",
                Narration.Clip("mfa_01")),
            new DialogueLine("MFA",
                "Instead of relying on just one form of verification, MFA requires users to provide two or more factors to prove their identity. There are three primary factors of authentication.",
                Narration.Clip("mfa_02")),
            new DialogueLine("MFA",
                "The first factor is something you know. This includes information such as a password, passphrase, or a personal identification number. It is the most common form of authentication. However, when used alone, it can be easily compromised.",
                Narration.Clip("mfa_03")),
            new DialogueLine("MFA",
                "The second factor is something you have. This refers to a physical item or digital tool, such as a one-time authentication app, security token, or a one-time code sent via email or text message.",
                Narration.Clip("mfa_04")),
            new DialogueLine("MFA",
                "The third factor is something you are. This includes biometric authentication, which relies on unique physical characteristics such as fingerprints, eye scans, facial recognition, or voice recognition.",
                Narration.Clip("mfa_05")),
            new DialogueLine("MFA",
                "By combining multiple authentication factors, MFA significantly strengthens security and reduces the risk of unauthorized access.",
                Narration.Clip("mfa_06")),
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

        // ---- Task data (the gamified task room) -------------------------------

        /// <summary>The MFA vault's "something you know": a four-digit code
        /// generated once per UTC day for the Spartan Authenticator phone.</summary>
        private static string dailyPasscode;
        private static DateTime dailyPasscodeDate;

        public static string DailyPasscode
        {
            get
            {
                DateTime today = DateTime.UtcNow.Date;
                if (dailyPasscode == null || dailyPasscodeDate != today)
                {
                    // A daily code should feel random while remaining stable
                    // for every run of the same day's training scenario.
                    int seed = today.Year * 10000 + today.Month * 100 + today.Day;
                    var random = new Random(seed);
                    dailyPasscode = random.Next(0, 10000).ToString("D4");
                    dailyPasscodeDate = today;
                }
                return dailyPasscode;
            }
        }

        /// <summary>A data crate for the sorting task.</summary>
        public class CrateDef
        {
            public readonly string id, label, role, why;
            public CrateDef(string id, string label, string role, string why)
            { this.id = id; this.label = label; this.role = role; this.why = why; }
        }

        public static CrateDef[] SortingCrates() => new[]
        {
            new CrateDef("faq", "PUBLIC FAQ", "INTERN",
                "Public info — everyone may handle it, including interns."),
            new CrateDef("payroll", "PAYROLL DATA", "HR MANAGER",
                "Salary records are need-to-know: HR only."),
            new CrateDef("rootkeys", "SERVER ROOT KEYS", "SYSADMIN",
                "Admin credentials go only to admins."),
            new CrateDef("medical", "MEDICAL FORMS", "HR MANAGER",
                "Health records are confidential — HR handles them."),
        };

        /// <summary>One round of the audit log hunt: 6 entries, one anomalous.</summary>
        public class LogRound
        {
            public readonly string[] lines;
            public readonly int anomaly;
            public readonly string hint, why;
            public LogRound(string[] lines, int anomaly, string hint, string why)
            { this.lines = lines; this.anomaly = anomaly; this.hint = hint; this.why = why; }
        }

        public static LogRound[] AuditRounds()
        {
            // Carry the playthrough's roster identity into I/AM so the same
            // person appears in the impossible-travel trail, SOC investigation,
            // and downstream forensic evidence for the entire campaign. The
            // other usernames here must stay OUTSIDE the roster pool
            // (d.chen, k.ramos, j.okafor, n.singh) so a bystander can never
            // share a name with the culprit.
            string scenarioUser = ScenarioRoster.Current.socUser;
            return new[]
            {
                new LogRound(new[]
                {
                    $"08:59  {PlayerIdentity.Callsign}  badge-in  ·  Lobby",
                    "09:04  r.alvarez  view  ·  HR/records",
                    "09:17  sysadmin  patch  ·  server-03",
                    "03:12  guest-04  download  ·  PAYROLL/*",
                    "09:31  t.nguyen  print  ·  Marketing/brief",
                    "09:45  r.alvarez  badge-out  ·  Lobby",
                }, 3,
                "Look for an account acting far outside its role (and its hours).",
                "A guest account bulk-downloading payroll at 3 AM — access beyond its role."),

                new LogRound(new[]
                {
                    $"10:02  {scenarioUser}  login  ·  San José, US",
                    $"10:06  {scenarioUser}  login  ·  Kyiv, UA",
                    "10:11  sysadmin  backup  ·  server-01",
                    "10:19  m.silva  view  ·  Sales/pipeline",
                    "10:24  intern-02  view  ·  Public/FAQ",
                    $"10:30  {scenarioUser}  logout  ·  —",
                }, 1,
                "Check WHERE each login comes from — and how fast they'd have to travel.",
                "The same account logged in from two countries four minutes apart — impossible travel."),
            };
        }

        /// <summary>The Certification Exam bank (the four knowledge checks,
        /// now asked together as the level's boss check).</summary>
        public static Quiz.QuizQuestion[] ExamQuestions() => new[]
        {
            IdentificationQuiz(), AuthenticationQuiz(), AuthorizationQuiz(), AccountabilityQuiz(),
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
