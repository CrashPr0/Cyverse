using System;
using Cyverse.Core;
using Cyverse.Interaction;

namespace Cyverse.Forensics
{
    /// <summary>One investigation question, answered in the Evidence
    /// Explorer by tagging the artifact that proves it (<see cref="artifactId"/>).
    /// <see cref="finding"/> is the short result the evidence board shows; a
    /// hint halves the award.</summary>
    public class CaseQuestion
    {
        public readonly string prompt;
        public readonly string artifactId;
        public readonly string finding;
        public readonly string hint;
        public readonly int points;

        public bool Answered;
        public bool HintUsed;
        public bool AttemptedWrong;

        public CaseQuestion(string prompt, string artifactId, string finding, string hint, int points = 120)
        {
            this.prompt = prompt;
            this.artifactId = artifactId;
            this.finding = finding;
            this.hint = hint;
            this.points = points;
        }

        /// <summary>Mark answered and bank the score. Full points first try
        /// without hint; hint halves; an earlier wrong attempt drops it to a
        /// third. Returns the award.</summary>
        public int Award()
        {
            Answered = true;
            int award = points;
            if (HintUsed) award /= 2;
            if (AttemptedWrong) award = Math.Max(20, points / 3);
            ScoreSystem.QuizTotal++;
            if (!AttemptedWrong)
            {
                ScoreSystem.QuizCorrect++;
                ScoreSystem.Streak++;
                if (ScoreSystem.Streak > ScoreSystem.BestStreak) ScoreSystem.BestStreak = ScoreSystem.Streak;
            }
            else
            {
                ScoreSystem.Streak = 0;
            }
            ScoreSystem.Add(award);
            return award;
        }
    }

    /// <summary>
    /// A Level 3 case file: questions worked in order on the seized phone in
    /// the Evidence Explorer. Progress lives here, so stepping away and
    /// reopening resumes.
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

        /// <summary>
        /// "Case: Spartan Gold" on the seized phone, worked in the Evidence
        /// Explorer: the first half of the one-device story (lure, click,
        /// payload, attacker infrastructure): the classic phishing kill chain
        /// and the pivot to the attacker's next domain. Artifact ids live in
        /// <see cref="PhoneImage.Build"/>.
        /// </summary>
        public static InvestigationCase SpartanGoldPhone()
        {
            return new InvestigationCase(
                "CASE 1: SPARTAN GOLD",
                new[]
                {
                new CaseQuestion(
                    "Users reported a \"Spartan Gold\" prize scam. Open TEXT MESSAGES on the left, find the text that delivered it to this phone, select it, and press TAG AS EVIDENCE.",
                    "msg_lure", "PRIZE TEXT",
                    "It is the message from a number that is not in the contacts, with a link in it. Type spartan in the filter box to narrow the list.",
                    120),
                new CaseQuestion(
                    "The same campaign also came by EMAIL. Tag the phishing email from prizes@spartan-rewards.com.",
                    "em_phish", "PHISH EMAIL",
                    "Two emails mention Spartan. Check the From field: only one comes from outside cyverse.edu.",
                    120),
                new CaseQuestion(
                    "Did the owner click? Tag the WEB HISTORY visit to the claim-now link.",
                    "web_claim", "CLAIM-NOW",
                    "Look for spartan-rewards.com/claim-now. The detail pane says what opened it.",
                    120),
                new CaseQuestion(
                    "A minute after the visit something new landed on the phone. Tag the INSTALLED APP that did not come from the app store.",
                    "app_goldclaim", "GOLD CLAIM APP",
                    "Read each app's \"Installed from\" field. Apps from a browser download skip the store's checks.",
                    120),
                new CaseQuestion(
                    "Infrastructure time. Tag the NETWORK record that shows the Gold Claim app sending data to the attacker's server.",
                    "net_goldclaim", PhoneImage.AttackerIp,
                    "Filter by gold claim. You want app traffic, not a DNS lookup.",
                    120),
                new CaseQuestion(
                    "The pivot: tag the DNS lookup of a DIFFERENT domain that resolves to that same IP. That is the attacker's next campaign.",
                    "dns_goldupdates", "GOLD-UPDATES.NET",
                    "Filter by 45.133.7.22. spartan-rewards.com is the domain you already know; the other one is new.",
                    140),
                });
        }

        /// <summary>
        /// "Case 2: Midnight Exfil" on the same phone: the second half of the
        /// story. A fake "IT Support" chat contact talks the owner into
        /// photographing confidential files after hours. Teaches chat
        /// artifacts, Wi-Fi history as location evidence, photo metadata, and
        /// tying an account back to known attacker infrastructure.
        /// </summary>
        public static InvestigationCase MidnightExfilPhone()
        {
            return new InvestigationCase(
                "CASE 2: MIDNIGHT EXFIL",
                new[]
                {
                new CaseQuestion(
                    "New case, same phone. The day after the phish, someone posing as IT contacted the owner. Tag the CHAT message that asks for confidential files.",
                    "chat_ask", "FAKE IT REQUEST",
                    "Filter by IT Support and read each message. One of them names the files it wants.",
                    140),
                new CaseQuestion(
                    "Office hours end at 19:00. Tag the WI-FI & LOCATION record that puts the phone back in the office after hours.",
                    "wifi_office_night", "23:40 OFFICE",
                    "The phone joins CyVerse-Staff more than once. Look at the times.",
                    140),
                new CaseQuestion(
                    "Tag the FIRST photo of a confidential document. Photo metadata records when and where each picture was taken.",
                    "img_payroll", "07-15 23:52",
                    "Most photos are personal. Check the Location field, then pick the earliest one taken in the office.",
                    140),
                new CaseQuestion(
                    "Where did the photos go? Tag the CHAT message that sent them out.",
                    "chat_sent", "4 PHOTOS SENT",
                    "Look for a sent message with attachments.",
                    140),
                new CaseQuestion(
                    "Tie it together: tag the record that proves \"CyVerse IT Support\" is not CyVerse at all, and links it to the attacker from case 1.",
                    "chat_contact", "ATTACKER ACCOUNT",
                    "Chat contacts are listed in CHATS too. Check the account's directory status and the IP it was last seen from.",
                    160),
                });
        }

        /// <summary>Video-room briefing: chain of custody, then how to work the
        /// phone in the Evidence Explorer.</summary>
        public static VideoStation.Slide[] BriefingSlides() => new[]
        {
            new VideoStation.Slide("DIGITAL FORENSICS",
                "A phishing campaign hit CyVerse this morning, and the SOC seized a work phone. Your job: examine it, find the evidence, and trace the attacker's infrastructure.", 10f),
            // Chain-of-custody explainer (placeholder for a recorded video). The
            // custody form shows up twice on purpose: once in the SOC, again at
            // evidence intake here, because every handoff needs its own entry.
            new VideoStation.Slide("CHAIN OF CUSTODY",
                "Evidence only counts if you can prove it never changed. A chain-of-custody record tracks every transfer: WHO handed it over, WHO received it, WHAT it is, WHEN, WHERE, and WHY.", 12f),
            new VideoStation.Slide("EVERY HANDOFF, EVERY TIME",
                "The device just moved from the SOC to Digital Forensics, so it needs a NEW entry. Even inside one department, every handoff is logged and signed. That is why you see this form twice, on purpose.", 12f),
            new VideoStation.Slide("SEAL IT. SIGN IT. LOG IT.",
                "Evidence goes in sealed, tamper-evident packaging with a label. Sender and receiver both sign, with the date and time, at every transfer. A broken seal or missing signature raises doubt.", 11f),
            new VideoStation.Slide("HASH VALUES",
                "A hash (like SHA-256) is a fingerprint of a device image. Take it at collection and again on receipt. If the two match, the image was not altered. Change even one bit and the hash changes completely.", 12f),
            new VideoStation.Slide("WHEN THE CHAIN BREAKS",
                "A gap in the record or a missing signature lets the other side ask: who had it, and what did they do? Evidence can be challenged or ruled inadmissible in court, even if it is real.", 12f),
            new VideoStation.Slide("FROM IMAGE TO ARTIFACTS",
                "A forensic tool never works on the original phone. You set up processing first (case type, device, how much to copy), then it reads the verified copy and sorts it into artifacts: messages, email, apps, network records.", 12f),
            new VideoStation.Slide("THE EVIDENCE EXPLORER",
                "Pick a category on the left, then an item in the list. The right side shows every field and the file on the phone it came from. Type in the filter box to search by keyword.", 12f),
            new VideoStation.Slide("TAG WHAT PROVES IT",
                "Most of a phone is ordinary life. Tag only the items that prove what happened. Tagged evidence is what goes in your report, so judge each one before you tag it.", 11f),
            new VideoStation.Slide("THE PIVOT",
                "The examiner's superpower: take a fact from one record and chase it into another. A text gives you a link; the link gives you a download; the app gives you the attacker's IP; the IP shows up somewhere you did not expect.", 12f),
            new VideoStation.Slide("METADATA TELLS TIME AND PLACE",
                "Photos record when and where they were taken. Wi-Fi history shows which networks a phone joined and when. Put those together and you can say where the phone was, even at midnight.", 12f),
        };
    }
}
