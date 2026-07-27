using Cyverse.Interaction;
using Cyverse.Quiz;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 2 — Cyber Defense (SOC Analyst / Protection &amp; Defense) content:
    /// the briefing, the SIEM alert queue, the EDR endpoint fleet, the incident
    /// response playbook, and the certification exam. Data only — educators can
    /// edit copy here without touching gameplay code.
    /// </summary>
    public static class Level2Content
    {
        // ---- Briefing ---------------------------------------------------------

        public static VideoStation.Slide[] BriefingSlides() => new[]
        {
            new VideoStation.Slide("CYBER DEFENSE",
                "Welcome to the Security Operations Center. Defenders do three things: they WATCH (SIEM), they CATCH what gets through (EDR), and they RESPOND in a disciplined order (Incident Response).", 10f),
            new VideoStation.Slide("SIEM",
                "A SIEM collects events from every system and raises alerts. The hard part isn't seeing alerts — it's volume. Most are noise. Miss the real one in the flood and the attacker wins. That's alert fatigue.", 11f),
            new VideoStation.Slide("EDR",
                "Endpoint Detection & Response watches what actually RUNS on machines. Malware hides behind normal-looking names — a PDF that's really an .exe, PowerShell running encoded commands. Find it, then isolate the endpoint.", 11f),
            new VideoStation.Slide("INCIDENT RESPONSE",
                "When something is real, you follow the playbook in ORDER: Preparation, Detection & Analysis, Containment, Eradication, Recovery, and Post-Incident review. Skipping steps destroys evidence or reinfects the network.", 12f),
        };

        // ---- Task 1: SIEM alert queue ----------------------------------------

        /// <summary>One alert in the SIEM queue.</summary>
        public class Alert
        {
            public readonly string source, text, why;
            public readonly bool escalate; // true = a real incident
            public Alert(string source, string text, bool escalate, string why)
            { this.source = source; this.text = text; this.escalate = escalate; this.why = why; }
        }

        /// <summary>Deliberately noise-heavy: 4 real incidents buried in 5
        /// benign events, so triage — not spotting — is the skill.</summary>
        public static Alert[] Alerts() => new[]
        {
            new Alert("auth-svc", "Scheduled antivirus scan completed on WS-11", false,
                "Routine maintenance. Escalating noise is how teams burn out."),
            new Alert("auth-svc", "47 failed logins then ONE success — account svc_backup", true,
                "Failed attempts ending in success is classic password spraying."),
            new Alert("print-svc", "Printer PR-02 reported offline", false,
                "An IT nuisance, not a security incident."),
            new Alert("net-flow", "Sustained outbound traffic to 45.133.7.22 (known C2)", true,
                "Traffic to a known command-and-control address means an active foothold."),
            new Alert("hr-portal", "User updated their profile photo", false,
                "Normal user activity."),
            new Alert("edr-agent", "PowerShell launched with an encoded command on WS-14", true,
                "Encoded PowerShell hides what is being run — a hallmark of intrusions."),
            new Alert("backup-svc", "Nightly backup job started on schedule", false,
                "Expected automation."),
            new Alert("file-svc", "1,400 files renamed with .locked extension in /finance", true,
                "Mass renaming to a new extension is ransomware encrypting files."),
            new Alert("wifi-ap", "Guest Wi-Fi access point rebooted", false,
                "Infrastructure noise."),
        };

        // ---- Task 2: EDR endpoint fleet ---------------------------------------

        /// <summary>One workstation on the floor.</summary>
        public class EndpointDef
        {
            public readonly string hostname, why;
            public readonly string[] processes;
            public readonly bool compromised;
            public EndpointDef(string hostname, string[] processes, bool compromised, string why)
            { this.hostname = hostname; this.processes = processes; this.compromised = compromised; this.why = why; }
        }

        public static EndpointDef[] Endpoints() => new[]
        {
            new EndpointDef("WS-11", new[] { "chrome.exe", "outlook.exe", "teams.exe" }, false,
                "Everyday office software — nothing to isolate."),
            new EndpointDef("WS-12", new[] { "chrome.exe", "invoice_2026.pdf.exe", "cmd.exe" }, true,
                "invoice_2026.pdf.exe is a DOUBLE EXTENSION — an executable dressed as a PDF."),
            new EndpointDef("WS-13", new[] { "code.exe", "git.exe", "chrome.exe" }, false,
                "A developer's normal toolchain."),
            new EndpointDef("WS-14", new[] { "explorer.exe", "powershell.exe -enc SQBFAFgA", "rundll32.exe" }, true,
                "Encoded PowerShell (-enc) is used to hide the command being run."),
            new EndpointDef("WS-15", new[] { "excel.exe", "outlook.exe" }, false,
                "Finance user doing finance things."),
        };

        // ---- Task 3: Incident Response playbook -------------------------------

        /// <summary>The NIST-style IR lifecycle, in order. Slot i expects
        /// step i — placing them out of order is the failure mode the task
        /// is built to teach.</summary>
        public static string[] PlaybookSteps() => new[]
        {
            "PREPARATION",
            "DETECTION",
            "CONTAINMENT",
            "ERADICATION",
            "RECOVERY",
            "LESSONS LEARNED",
        };

        public static string[] PlaybookWhy() => new[]
        {
            "Tools, training and plans exist BEFORE the incident.",
            "Confirm it's real and determine the scope.",
            "Stop the spread first — isolate before you clean.",
            "Now remove the malware and close the way in.",
            "Restore service and verify the systems are clean.",
            "Write it up so the same thing can't work twice.",
        };

        // ---- Certification exam ------------------------------------------------

        public static QuizQuestion[] ExamQuestions() => new[]
        {
            new QuizQuestion(
                "Most SIEM alerts on a normal day are:",
                new[] { "Confirmed attacks", "Benign noise that still needs triage", "Hardware failures" },
                1,
                "The volume is mostly noise — triage is the skill, and alert fatigue is the risk."),
            new QuizQuestion(
                "A file named invoice_2026.pdf.exe is suspicious because:",
                new[] { "PDFs can't be emailed", "It's an executable disguised as a document", "It's too large" },
                1,
                "The real extension is .exe — the .pdf is bait for the eye."),
            new QuizQuestion(
                "Your EDR flags an active infection on one machine. What comes FIRST?",
                new[] { "Contain — isolate the endpoint", "Eradicate the malware", "Write the incident report" },
                0,
                "Containment stops the spread; cleaning before containing lets it move."),
            new QuizQuestion(
                "Why does Incident Response end with a Lessons Learned step?",
                new[] { "To assign blame", "To improve defenses so it can't recur", "It's optional paperwork" },
                1,
                "Post-incident review feeds back into Preparation — that's what closes the loop."),
        };

        /// <summary>Par score: SIEM 9x40=360, EDR 2x70+80=220, playbook
        /// 6x50=300, exam 4x100=400. Grades the results screen on this
        /// level's own scale rather than Level 0's.</summary>
        public const int ParScore = 1280;
    }
}
