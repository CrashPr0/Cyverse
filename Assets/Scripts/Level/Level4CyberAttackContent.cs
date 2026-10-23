using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Authoring data for Level 4's controlled cyber-attack simulation. Every
    /// target, identity and result is synthetic and resolved locally; the
    /// simulation never reaches an external host or executes an attack tool.
    /// Reconnaissance is introduced in the briefing as context. The four
    /// playable stations then mirror the defensive concepts taught in Levels
    /// 1–3: bypass MFA, escalate privileges, extract data and cover tracks.
    /// </summary>
    public static class Level4CyberAttackContent
    {
        public const float ScenarioTimeLimitSeconds = 240f;
        public const int TimeoutDetectionPenalty = 15;

        public enum StationKind
        {
            BypassMfa,
            EscalatePrivileges,
            ExtractData,
            CoverTracks,
        }

        public sealed class StationScenario
        {
            public readonly StationKind kind;
            public readonly string title;
            public readonly string objective;
            public readonly string situation;
            public readonly string[] options;
            public readonly int correctOption;
            public readonly string successMessage;
            public readonly string lesson;
            public readonly int dataGain;
            public readonly int detectionPenalty;

            public StationScenario(StationKind kind, string title, string objective,
                string situation, string[] options, int correctOption,
                string successMessage, string lesson, int dataGain,
                int detectionPenalty)
            {
                this.kind = kind;
                this.title = title;
                this.objective = objective;
                this.situation = situation;
                this.options = options;
                this.correctOption = correctOption;
                this.successMessage = successMessage;
                this.lesson = lesson;
                this.dataGain = dataGain;
                this.detectionPenalty = detectionPenalty;
            }
        }

        public static VideoStation.Slide[] BriefingSlides() => new[]
        {
            new VideoStation.Slide("THE GAME ROOM  //  CONTROLLED SIMULATION",
                "You play the red team: the attackers, loose in a fake tech company's employee game room. Each game machine is one attack step. Every employee, computer and record here is fake. Learn which defense would have stopped each step.", 10f),
            new VideoStation.Slide("THE ATTACK SURFACE",
                "The attack surface is every possible way in. Attackers start with recon (reconnaissance): collecting public details to map a target. Sharing less online, training staff and watching public-facing services leave them less to find.", 11f),
            new VideoStation.Slide("ACCESS  →  PIVOT",
                "Getting in only matters if it leads somewhere. Pivoting means hopping from one hacked computer to the next. MFA that can't be phished, least privilege (only the access you need) and a walled-off network make that a dead end.", 12f),
            new VideoStation.Slide("DATA  →  COVER TRACKS",
                "The last steps are stealing data (exfiltration) and covering tracks (hiding what you did). Both are capped here. Monitoring each computer, checking data that leaves and logs nobody can edit should catch it first.", 12f),
            new VideoStation.Slide("RULES OF ENGAGEMENT",
                "Rules of engagement are the limits on what the red team may do. Stay inside the simulation: no real passwords, outside services or malware. A high score means you found the right weak spot and know which defense should catch it.", 10f),
        };

        public static StationScenario[] Stations() => new[]
        {
            new StationScenario(
                StationKind.BypassMfa,
                "PHISH FRENZY  //  BYPASS MFA",
                "Pick a pretend way in to the training company's accounts.",
                "A test mailbox will accept one of three attempts. The rules allow only a sandboxed (walled-off) exercise with fake data. Which way in shows the risk while staying approved and on the record?",
                new[]
                {
                    "Reuse a real employee's password leaked from another website",
                    "Send a practice phishing (trick) email to the approved test mailbox",
                    "Run an unchecked program on a real work computer",
                },
                1,
                "TEST MAILBOX OPENED — the phishing email reached the fake company.",
                "MFA that can't be phished, spam filters and staff who report odd emails stop a trick email before it gets anyone in.",
                22,
                12),
            new StationScenario(
                StationKind.EscalatePrivileges,
                "ACCESS GRABBER  //  ESCALATE PRIVILEGES",
                "Move from the hacked test computer toward the fake finance vault.",
                "From the hacked test computer you can reach three accounts. One was set up on purpose with more access than it needs. Which one exposes that mistake without leaving the simulation?",
                new[]
                {
                    "Use the test account that was given too much access",
                    "Guess a real administrator's password until it works",
                    "Turn off network monitoring so nobody sees you move",
                },
                0,
                "PIVOT REACHED — one account with too much access opened up far more of the network.",
                "Least privilege (only the access you need), a walled-off network and extra sign-in checks stop an attacker spreading.",
                24,
                16),
            new StationScenario(
                StationKind.ExtractData,
                "DATA VENDING  //  EXTRACT DATA",
                "Copy only the small fake dataset through the watched lab channel.",
                "The fake vault has activity monitoring on each computer and checks on data leaving the network. Which last step shows the risk while keeping the copy small and visible?",
                new[]
                {
                    "Copy every real personal record to an unknown outside server",
                    "Leave a hidden repeating task on a real work computer",
                    "Send a small fake export through the watched lab channel",
                },
                2,
                "PRACTICE EXPORT SENT — the meter shows what left the lab.",
                "Limits on what data can leave, logs nobody can edit and a fast response cut data loss.",
                28,
                20),
            new StationScenario(
                StationKind.CoverTracks,
                "HIGH SCORE LOG  //  COVER TRACKS",
                "Try hiding your activity without wiping the evidence defenders need.",
                "The training security team watches an activity timeline nobody can edit. Which step tests hiding your tracks while keeping it undoable and easy to review?",
                new[]
                {
                    "Add a labeled practice marker and leave the logs untouched",
                    "Change or delete the logs on a real system",
                    "Turn off monitoring so every action disappears",
                },
                0,
                "COVER-TRACKS TEST LOGGED — the locked timeline kept every action visible.",
                "Logs nobody can edit, linked alerts and a fast response expose attempts to hide.",
                26,
                22),
        };

        public static string DisplayName(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.BypassMfa: return "Bypass MFA";
                case StationKind.EscalatePrivileges: return "Escalate Privileges";
                case StationKind.ExtractData: return "Extract Data";
                case StationKind.CoverTracks: return "Cover Tracks";
                default: return kind.ToString();
            }
        }

        /// <summary>What a machine's screen shows until the briefing ends.</summary>
        public const string OutOfOrderLabel = "OUT OF ORDER";

        /// <summary>The game-room machine each attack step is played on.</summary>
        public static string MachineTitle(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.BypassMfa: return "PHISH FRENZY";
                case StationKind.EscalatePrivileges: return "ACCESS GRABBER";
                case StationKind.ExtractData: return "DATA VENDING";
                case StationKind.CoverTracks: return "HIGH SCORE LOG";
                default: return "GAME ROOM";
            }
        }

        /// <summary>The live screen: the game's name over the attack step.</summary>
        public static string ShortLabel(StationKind kind)
        {
            string step;
            switch (kind)
            {
                case StationKind.BypassMfa: step = "BYPASS MFA"; break;
                case StationKind.EscalatePrivileges: step = "ESCALATE PRIVILEGES"; break;
                case StationKind.ExtractData: step = "EXTRACT DATA"; break;
                case StationKind.CoverTracks: step = "COVER TRACKS"; break;
                default: step = "SIMULATION"; break;
            }
            return MachineTitle(kind) + "\n<size=60%>" + step + "</size>";
        }
    }
}
