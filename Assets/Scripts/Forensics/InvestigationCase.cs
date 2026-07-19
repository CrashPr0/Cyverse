using System;
using Cyverse.Interaction;

namespace Cyverse.Forensics
{
    /// <summary>One investigation question: prompt, accepted answers, a hint
    /// (using it halves the award), and an example query shown by `hint`.</summary>
    public class CaseQuestion
    {
        public readonly string prompt;
        public readonly string[] answers;   // any match (case-insensitive) counts
        public readonly string hint;
        public readonly string exampleQuery;
        public readonly int points;

        public bool Answered;
        public bool HintUsed;
        public bool AttemptedWrong;

        public CaseQuestion(string prompt, string[] answers, string hint, string exampleQuery, int points = 120)
        {
            this.prompt = prompt;
            this.answers = answers;
            this.hint = hint;
            this.exampleQuery = exampleQuery;
            this.points = points;
        }

        public bool Matches(string given)
        {
            if (string.IsNullOrWhiteSpace(given)) return false;
            string norm = Normalize(given);
            foreach (var a in answers)
                if (Normalize(a) == norm) return true;
            return false;
        }

        private static string Normalize(string s)
        {
            s = s.Trim().ToLowerInvariant().Trim('"');
            if (s.StartsWith("http://")) s = s.Substring(7);
            if (s.StartsWith("https://")) s = s.Substring(8);
            return s.TrimEnd('/');
        }
    }

    /// <summary>
    /// "Case: Spartan Gold" — the KC7-style investigation for Level 3.
    /// Eight questions that walk the classic phishing kill-chain pivot:
    /// victim org → campaign emails → clicked link → payload execution →
    /// attacker infrastructure → the attacker's NEXT campaign.
    /// </summary>
    public class InvestigationCase
    {
        public readonly string title;
        public readonly CaseQuestion[] questions;

        public event Action QuestionAnswered;
        public event Action CaseCompleted;

        public InvestigationCase(string title, CaseQuestion[] questions)
        {
            this.title = title;
            this.questions = questions;
        }

        public int AnsweredCount
        {
            get { int n = 0; foreach (var q in questions) if (q.Answered) n++; return n; }
        }

        public bool IsComplete => AnsweredCount >= questions.Length;

        public CaseQuestion Current
        {
            get { foreach (var q in questions) if (!q.Answered) return q; return null; }
        }

        public int CurrentIndex
        {
            get { for (int i = 0; i < questions.Length; i++) if (!questions[i].Answered) return i; return questions.Length; }
        }

        public void NotifyAnswered()
        {
            QuestionAnswered?.Invoke();
            if (IsComplete) CaseCompleted?.Invoke();
        }

        // ---- Level 3 content --------------------------------------------------

        public static InvestigationCase SpartanGold() => new InvestigationCase(
            "CASE: SPARTAN GOLD",
            new[]
            {
                new CaseQuestion(
                    "Warm-up. How many employees does CyVerse have on file?",
                    new[] { "12" },
                    "Count the rows of the Employees table.",
                    "Employees | count"),
                new CaseQuestion(
                    "Users reported a suspicious sender: prizes@spartan-rewards.com. How many emails did that sender deliver?",
                    new[] { "6" },
                    "Filter Email by sender, then count.",
                    "Email | where sender == \"prizes@spartan-rewards.com\" | count"),
                new CaseQuestion(
                    "What link are those phishing emails trying to get people to click?",
                    new[] { "http://spartan-rewards.com/claim-now", "spartan-rewards.com/claim-now" },
                    "Same filter, but look at the link column instead of counting.",
                    "Email | where sender == \"prizes@spartan-rewards.com\" | distinct link"),
                new CaseQuestion(
                    "How many machines actually VISITED that link? (Browsing is logged in WebVisits.)",
                    new[] { "3" },
                    "Filter WebVisits where url contains the phishing domain, then count.",
                    "WebVisits | where url contains \"spartan-rewards\" | count"),
                new CaseQuestion(
                    "One of those visits dropped a payload. What is the malware's process name? (Check ProcessEvents for something a browser shouldn't spawn.)",
                    new[] { "gold_claim.exe", "gold_claim" },
                    "Look at processes whose parent_process is chrome.exe — browsers download things; they shouldn't launch strange executables.",
                    "ProcessEvents | where parent_process == \"chrome.exe\""),
                new CaseQuestion(
                    "Which EMPLOYEE's machine ran gold_claim.exe? Give their name.",
                    new[] { "devon.james", "devon james", "devon.james@cyverse.edu" },
                    "Find the hostname in ProcessEvents, then look that hostname up in Employees.",
                    "ProcessEvents | where process_name == \"gold_claim.exe\""),
                new CaseQuestion(
                    "Infrastructure time. What IP address does spartan-rewards.com resolve to?",
                    new[] { "45.133.7.22" },
                    "DnsLookups maps domains to IPs.",
                    "DnsLookups | where domain == \"spartan-rewards.com\""),
                new CaseQuestion(
                    "The pivot: what OTHER domain resolves to that same IP? That's the attacker's next campaign — flag it and close the case.",
                    new[] { "gold-updates.net" },
                    "Filter DnsLookups by the resolved_ip you just found and see what else is there.",
                    "DnsLookups | where resolved_ip == \"45.133.7.22\""),
            });

        /// <summary>
        /// "Case: Midnight Exfil" — the follow-up insider-threat case. Requires
        /// the analytics operators (summarize / sort): the anomaly hides in
        /// aggregates, not in any single row. New tables: LogonEvents, FileAccess.
        /// </summary>
        public static InvestigationCase MidnightExfil() => new InvestigationCase(
            "CASE 2: MIDNIGHT EXFIL",
            new[]
            {
                new CaseQuestion(
                    "New case. The SIEM flagged unusual account activity on 07-15. Which employee has the MOST logon events? (summarize is your friend.)",
                    new[] { "drew.patel", "drew patel", "drew.patel@cyverse.edu" },
                    "Group the LogonEvents by employee and count each group — the biggest group is your answer.",
                    "LogonEvents | summarize count by employee", 140),
                new CaseQuestion(
                    "Business hours end at 19:00. At what TIME did the suspicious after-hours logon happen?",
                    new[] { "23:40", "07-15 23:40" },
                    "Sort LogonEvents by timestamp and look at the end of the day — or filter where timestamp contains \"07-15 23\".",
                    "LogonEvents | sort by timestamp desc", 140),
                new CaseQuestion(
                    "Right after that logon, files started moving. How many files were copied to USB?",
                    new[] { "4" },
                    "FileAccess logs every action — filter where action == \"copy_to_usb\" and count.",
                    "FileAccess | where action == \"copy_to_usb\" | count", 140),
                new CaseQuestion(
                    "Confirm the insider: who performed ALL of those USB copies?",
                    new[] { "drew.patel", "drew patel", "drew.patel@cyverse.edu" },
                    "Same filter, but look at the employee column — distinct makes it unambiguous.",
                    "FileAccess | where action == \"copy_to_usb\" | distinct employee", 140),
                new CaseQuestion(
                    "Which WORKSTATION did the insider use? (Pivot back through LogonEvents.)",
                    new[] { "WS-DPATEL", "ws-dpatel" },
                    "Filter LogonEvents by the employee you identified and read the workstation column.",
                    "LogonEvents | where employee == \"drew.patel\"", 140),
                new CaseQuestion(
                    "For the HR report: what is the exact TIMESTAMP of the FIRST file copied to USB?",
                    new[] { "07-15 23:52", "23:52" },
                    "Filter the USB copies and sort by timestamp ascending — the first row is your answer.",
                    "FileAccess | where action == \"copy_to_usb\" | sort by timestamp", 140),
            });

        /// <summary>Video-room briefing: teaches just enough query syntax.</summary>
        public static VideoStation.Slide[] BriefingSlides() => new[]
        {
            new VideoStation.Slide("DIGITAL FORENSICS",
                "A phishing campaign hit CyVerse this morning. Your job: work the logs, follow the trail, and find the attacker's infrastructure. Analysts don't scroll — they QUERY.", 10f),
            new VideoStation.Slide("TABLES",
                "Evidence lives in five tables: Employees, Email, WebVisits, ProcessEvents, DnsLookups. Type a table's name to see its rows; type 'fields Email' to see its columns.", 10f),
            new VideoStation.Slide("FILTERING",
                "Narrow with where:  Email | where sender == \"someone\"  — or use contains for partial matches. Chain steps with the | pipe, and end with | count to count rows.", 11f),
            new VideoStation.Slide("THE PIVOT",
                "The analyst's superpower: take a fact from one table and chase it through another. An email gives you a link; the link gives you visitors; a visitor's machine gives you malware. Answer with: answer <your finding>.", 12f),
            new VideoStation.Slide("AGGREGATES",
                "Some anomalies hide in totals, not rows. summarize count by employee groups and counts; sort by timestamp desc orders. When one bar towers over the rest — that's your lead. You'll need both for the second case.", 11f),
        };
    }
}
