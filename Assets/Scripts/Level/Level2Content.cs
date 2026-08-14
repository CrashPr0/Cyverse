using Cyverse.Interaction;
using Cyverse.Quiz;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 2 — Cyber Defense (SOC Analyst / Protection &amp; Defense) content:
    /// the briefing, the SOC investigation scenarios, the incident-response
    /// playbook, and the certification exam. Data only — educators can
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

        public enum SocVerdict { MatchBenignPositive, NoMatchPossibleTruePositive }

        [System.Serializable]
        public class SocEventRow
        {
            public string time, computer, user, activity;
            public SocEventRow(string time, string computer, string user, string activity)
            { this.time = time; this.computer = computer; this.user = user; this.activity = activity; }
        }

        [System.Serializable]
        public class SocWorkstationView
        {
            public string computer;
            public string[] lines;
            public SocWorkstationView(string computer, string line1, string line2)
            { this.computer = computer; lines = new[] { line1, line2 }; }
        }

        [System.Serializable]
        public class SocScenario
        {
            public string title, description, resolution;
            public SocEventRow[] rows;
            public SocWorkstationView[] workstations;
            public int triggerRowIndex;
            public SocVerdict correctVerdict;

            public SocScenario(string title, string description, SocEventRow[] rows,
                int triggerRowIndex, SocWorkstationView[] workstations,
                SocVerdict correctVerdict, string resolution)
            {
                this.title = title;
                this.description = description;
                this.rows = rows;
                this.triggerRowIndex = triggerRowIndex;
                this.workstations = workstations;
                this.correctVerdict = correctVerdict;
                this.resolution = resolution;
            }

            public SocWorkstationView Workstation(string computer)
            {
                foreach (var view in workstations)
                    if (view.computer == computer) return view;
                return null;
            }
        }

        /// <summary>The SOC room's reusable three-case investigation sequence.
        /// Every case has four distinct computers and exactly one trigger row.</summary>
        public static SocScenario[] SocScenarios() => new[]
        {
            new SocScenario(
                "Unusual Script Execution Detected",
                "A script bypassed normal safety settings on WS-02. Verify whether an employee ran this on purpose.",
                new[]
                {
                    new SocEventRow("09:02", "WS-01", "j.smith",  "outlook.exe — reading email"),
                    new SocEventRow("09:04", "WS-02", "m.garcia", "powershell.exe running printer_fix.ps1 with safety checks bypassed"),
                    new SocEventRow("09:05", "WS-03", "d.chen",   "chrome.exe — browsing intranet"),
                    new SocEventRow("09:07", "WS-04", "a.patel",  "excel.exe — editing Q3_budget.xlsx"),
                },
                1,
                new[]
                {
                    new SocWorkstationView("WS-01", "Outlook — Inbox", "Mail sync complete"),
                    new SocWorkstationView("WS-02", "PowerShell — printer_fix.ps1 — Repairing print spooler... 78%", "IT Helpdesk #4412 — Run printer_fix.ps1 to resolve your issue"),
                    new SocWorkstationView("WS-03", "Chrome — Company Intranet", "No alerts on this workstation"),
                    new SocWorkstationView("WS-04", "Excel — Q3_budget.xlsx", "AutoSave complete"),
                },
                SocVerdict.MatchBenignPositive,
                "BENIGN POSITIVE — the employee is visibly running the helpdesk-approved printer repair."),

            new SocScenario(
                "Mass File Copy Detected",
                "A large number of files were copied off WS-04 after hours. Verify whether this was authorized.",
                new[]
                {
                    new SocEventRow("18:21", "WS-01", "j.smith",  "teams.exe — in a call"),
                    new SocEventRow("18:25", "WS-03", "d.chen",   "spotify.exe — playing audio"),
                    new SocEventRow("18:30", "WS-04", "a.patel",  "backup_util.exe copying 2,300 files to FILESERVER backups"),
                    new SocEventRow("18:32", "WS-02", "m.garcia", "outlook.exe — sending email"),
                },
                2,
                new[]
                {
                    new SocWorkstationView("WS-01", "Teams — active call", "Microphone connected"),
                    new SocWorkstationView("WS-02", "Outlook — composing message", "Connected to mail server"),
                    new SocWorkstationView("WS-03", "Spotify — playing audio", "No file activity"),
                    new SocWorkstationView("WS-04", "Backup Utility — 2,300 files → FILESERVER backups (62%)", "Calendar — Friday 6:30 PM weekly backup runs automatically"),
                },
                SocVerdict.MatchBenignPositive,
                "BENIGN POSITIVE — the visible activity and calendar confirm the scheduled backup."),

            new SocScenario(
                "Suspicious Account Discovery Commands",
                "Commands used to secretly map user accounts ran on WS-03 overnight. Verify whether the employee ran them.",
                new[]
                {
                    new SocEventRow("02:09", "WS-01", "j.smith",  "outlook.exe — background mail sync"),
                    new SocEventRow("02:11", "WS-02", "m.garcia", "onedrive.exe — background file sync"),
                    new SocEventRow("02:13", "WS-03", "d.chen",   "cmd.exe running net user /domain — listing employee accounts"),
                    new SocEventRow("02:15", "WS-04", "a.patel",  "teams.exe — background sync"),
                },
                2,
                new[]
                {
                    new SocWorkstationView("WS-01", "Outlook background mail sync", "Screen unlocked — user active"),
                    new SocWorkstationView("WS-02", "OneDrive background file sync", "Screen unlocked — user active"),
                    new SocWorkstationView("WS-03", "Screen locked — d.chen last active 17:42 yesterday", "No user programs running — machine idle since 17:45"),
                    new SocWorkstationView("WS-04", "Teams background sync", "No command prompt activity"),
                },
                SocVerdict.NoMatchPossibleTruePositive,
                "POSSIBLE TRUE POSITIVE — WS-03 was locked and idle; the account-discovery command is unexplained."),
        };

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
            new EndpointDef("WS-01", new[] { "Outlook — Inbox", "Mail sync complete" }, false,
                "SOC verification workstation"),
            new EndpointDef("WS-02", new[] { "PowerShell — printer_fix.ps1", "IT Helpdesk ticket #4412" }, false,
                "SOC verification workstation"),
            new EndpointDef("WS-03", new[] { "Screen locked", "Machine idle" }, true,
                "Possible compromised computer"),
            new EndpointDef("WS-04", new[] { "Backup Utility", "Weekly backup scheduled" }, false,
                "SOC verification workstation"),
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
