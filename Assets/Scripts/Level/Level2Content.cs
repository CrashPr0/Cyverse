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
                "Welcome to the Security Operations Center. Defenders do three things: they WATCH (SIEM: the system that collects security alerts), they CATCH what gets through (EDR: antivirus-style software on each computer), and they RESPOND in a set order (Incident Response, or IR: the plan for handling an attack). SOC means the team doing this work.", 10f),
            new VideoStation.Slide("SIEM",
                "A SIEM (Security Information and Event Management) collects logs, the activity records, from every system and raises alerts. The hard part isn't seeing alerts — it's volume. Most are noise. Miss the real one in the flood and the attacker wins. That's alert fatigue.", 11f),
            new VideoStation.Slide("EDR",
                "EDR (Endpoint Detection & Response) watches what actually RUNS on each computer (an endpoint). Malware hides behind normal-looking names — a PDF that's really a program (.exe), or a script running scrambled commands. Find it, then isolate the endpoint.", 11f),
            new VideoStation.Slide("INCIDENT RESPONSE",
                "When something is real, you follow the playbook in ORDER: Preparation (get ready), Detection (spot and confirm it), Containment (stop it spreading), Eradication (remove it), Recovery (get back to normal), and Lessons Learned (review). Skipping steps destroys evidence or reinfects the network.", 12f),
            // Text-only intro to the chain-of-custody record the player confirms at
            // the end of the SIEM task. Digital Forensics repeats the form on
            // purpose (and explains why), so keep this one brief.
            new VideoStation.Slide("EVIDENCE & CUSTODY",
                "When an alert is real, the machine becomes evidence. A chain-of-custody form logs WHO collected it, WHEN, and WHERE it goes next. Log every handoff, even inside one department, or it may not hold up in court.", 11f),
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
        public static SocScenario[] SocScenarios()
        {
            string culprit = ScenarioRoster.Current.socUser;
            return new[]
            {
            new SocScenario(
                "Unusual Script Execution Detected",
                "A script bypassed normal safety settings on WS-02. Verify whether an employee ran this on purpose.",
                new[]
                {
                    new SocEventRow("09:02", "WS-01", "j.smith",  "mail app — reading email"),
                    new SocEventRow("09:04", "WS-02", "m.garcia", "script tool running printer_fix.ps1 with safety checks bypassed"),
                    new SocEventRow("09:05", "WS-03", culprit,     "web browser — browsing intranet"),
                    new SocEventRow("09:07", "WS-04", "a.patel",  "spreadsheet app — editing Q3_budget.xlsx"),
                },
                1,
                new[]
                {
                    new SocWorkstationView("WS-01", "Mail app — Inbox", "Mail sync complete"),
                    new SocWorkstationView("WS-02", "Script tool — printer_fix.ps1 — Repairing print spooler... 78%", "IT Helpdesk #4412 — Run printer_fix.ps1 to resolve your issue"),
                    new SocWorkstationView("WS-03", "Web browser — Company Intranet", "No alerts on this workstation"),
                    new SocWorkstationView("WS-04", "Spreadsheet — Q3_budget.xlsx", "AutoSave complete"),
                },
                SocVerdict.MatchBenignPositive,
                "FALSE ALARM (benign positive) — the employee is visibly running the helpdesk-approved printer repair."),

            new SocScenario(
                "Mass File Copy Detected",
                "A large number of files were copied off WS-04 after hours. Verify whether this was authorized.",
                new[]
                {
                    new SocEventRow("18:21", "WS-01", "j.smith",  "video call app — in a call"),
                    new SocEventRow("18:25", "WS-03", culprit,     "music app — playing audio"),
                    new SocEventRow("18:30", "WS-04", "a.patel",  "backup tool copying 2,300 files to the file server backups"),
                    new SocEventRow("18:32", "WS-02", "m.garcia", "mail app — sending email"),
                },
                2,
                new[]
                {
                    new SocWorkstationView("WS-01", "Video call — active call", "Microphone connected"),
                    new SocWorkstationView("WS-02", "Mail app — composing message", "Connected to mail server"),
                    new SocWorkstationView("WS-03", "Music app — playing audio", "No file activity"),
                    new SocWorkstationView("WS-04", "Backup tool — 2,300 files → file server backups (62%)", "Calendar — Friday 6:30 PM weekly backup runs automatically"),
                },
                SocVerdict.MatchBenignPositive,
                "FALSE ALARM (benign positive) — the visible activity and calendar confirm the scheduled backup."),

            new SocScenario(
                "Suspicious Account Discovery Commands",
                "Commands used to secretly map user accounts ran on WS-03 overnight. Verify whether the employee ran them.",
                new[]
                {
                    new SocEventRow("02:09", "WS-01", "j.smith",  "mail app — background mail sync"),
                    new SocEventRow("02:11", "WS-02", "m.garcia", "file sync app — background file sync"),
                    new SocEventRow("02:13", "WS-03", culprit,     "command window running net user /domain — a command that lists every employee account"),
                    new SocEventRow("02:15", "WS-04", "a.patel",  "video call app — background sync"),
                },
                2,
                new[]
                {
                    new SocWorkstationView("WS-01", "Mail app background sync", "Screen unlocked — user active"),
                    new SocWorkstationView("WS-02", "File sync app running in the background", "Screen unlocked — user active"),
                    new SocWorkstationView("WS-03", $"Screen locked — {culprit} last active 17:42 yesterday", "No user programs running — machine idle since 17:45"),
                    new SocWorkstationView("WS-04", "Video call app background sync", "No command prompt activity"),
                },
                SocVerdict.NoMatchPossibleTruePositive,
                "POSSIBLY REAL (true positive) — WS-03 was locked and idle, so nobody at the desk ran that command."),
            };
        }

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
                "Many failed attempts ending in one success means someone guessed the password (password spraying)."),
            new Alert("print-svc", "Printer PR-02 reported offline", false,
                "An IT nuisance, not a security incident."),
            new Alert("net-flow", "Sustained outbound traffic to 45.133.7.22 (known C2)", true,
                "Traffic to a known command-and-control (C2) address, a server that remotely directs malware, means an active foothold."),
            new Alert("hr-portal", "User updated their profile photo", false,
                "Normal user activity."),
            new Alert("edr-agent", "A script tool launched with a scrambled (encoded) command on WS-14", true,
                "A scrambled command hides what is being run — a hallmark of intrusions."),
            new Alert("backup-svc", "Nightly backup job started on schedule", false,
                "Expected automation."),
            new Alert("file-svc", "1,400 files renamed with .locked extension in /finance", true,
                "Mass renaming to a new extension is ransomware (malware that locks files for a ransom) encrypting files."),
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
            new EndpointDef("WS-01", new[] { "Mail app — Inbox", "Mail sync complete" }, false,
                "SOC verification workstation"),
            new EndpointDef("WS-02", new[] { "Script tool — printer_fix.ps1", "IT Helpdesk ticket #4412" }, false,
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
            "Get ready first: tools, training and a plan, before any attack.",
            "Spot the attack and check how big it is.",
            "Stop it spreading: unplug or isolate the machine before cleaning.",
            "Remove the malware and fix the hole it got in through.",
            "Get systems back to normal and check they're clean.",
            "Review what happened so the same attack can't work twice.",
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
