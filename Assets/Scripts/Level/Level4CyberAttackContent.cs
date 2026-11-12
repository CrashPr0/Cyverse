using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Authoring data for Level 4's controlled cyber-attack simulation. Every
    /// target, identity and result is synthetic and resolved locally; the
    /// simulation never reaches an external host or executes an attack tool.
    /// The four playable stations follow an attack from the first trick to
    /// the whole network: spot the social-engineering lure, bypass MFA,
    /// escalate privileges, then map where attacks land (endpoint devices,
    /// the router and the cloud).
    /// </summary>
    public static class Level4CyberAttackContent
    {
        /// <summary>The normal run is untimed: a first-time player should read
        /// each choice, not race a clock. The limit stays for a future timed
        /// (Hard) run.</summary>
        public static readonly bool TimedRun = false;
        public const float ScenarioTimeLimitSeconds = 240f;
        public const int TimeoutDetectionPenalty = 15;

        public enum StationKind
        {
            SpotThePhish,
            BypassMfa,
            EscalatePrivileges,
            NetworkAttack,
        }

        /// <summary>The order the machines are played in.</summary>
        public static readonly StationKind[] PlayOrder =
        {
            StationKind.SpotThePhish,
            StationKind.BypassMfa,
            StationKind.EscalatePrivileges,
            StationKind.NetworkAttack,
        };

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
            /// <summary>The line under the choices before the player picks.</summary>
            public readonly string hint;

            public StationScenario(StationKind kind, string title, string objective,
                string situation, string[] options, int correctOption,
                string successMessage, string lesson, int dataGain,
                int detectionPenalty,
                string hint = "Choose the simulated action that stays inside the rules of engagement.")
            {
                this.hint = hint;
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
                "You play the red team: the attackers, loose in a fake tech company's employee game room. Each game machine is one attack step. Every employee, account and computer here is fake. Learn which defense would have stopped each step.", 10f),
            new VideoStation.Slide("PEOPLE FIRST",
                "Most attacks start with a person, not a computer. Social engineering means tricking someone into helping: a fake job offer, a message from a friend's hacked account, an urgent email. Slowing down and checking the sender another way stops most of it.", 12f),
            new VideoStation.Slide("ACCESS  →  PIVOT",
                "One stolen password opens one account. Pivoting means hopping from there to the next computer or account. MFA that can't be phished and least privilege (only the access you need) make that a dead end.", 11f),
            new VideoStation.Slide("WHERE ATTACKS LAND",
                "On a network, attacks land in three kinds of places: endpoint devices (laptops and phones), the router that links the office to the internet, and the cloud services the company rents. Each one needs its own defense.", 12f),
            new VideoStation.Slide("RULES OF ENGAGEMENT",
                "Rules of engagement are the limits on what the red team may do. Stay inside the simulation: no real passwords, outside services or malware. A high score means you found the right weak spot and know which defense should catch it.", 10f),
        };

        public static StationScenario[] Stations() => new[]
        {
            new StationScenario(
                StationKind.SpotThePhish,
                "SPOT THE PHISH  //  SOCIAL ENGINEERING",
                "Attackers start with people. Learn to spot the lures they use.",
                "A recruiter you've never met messages you on a jobs site: a remote job paying $95 an hour, no experience needed. To start today you must sign in on their website and pay $60 for a training kit. What is this?",
                new[]
                {
                    "A normal hiring step, since real companies charge for training kits",
                    "A scam: a fake job built to steal your sign-in and your money",
                    "A computer virus that spreads on its own",
                },
                1,
                "SCAM SPOTTED — a fake offer is how many attacks begin.",
                "Pressure, offers that are too good, and requests to pay or sign in through a link are red flags. Check the person another way, and never pay to get a job.",
                22,
                10,
                "Pick the answer that best describes the message."),
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
                24,
                12),
            new StationScenario(
                StationKind.EscalatePrivileges,
                "ACCESS GRABBER  //  ESCALATE PRIVILEGES",
                "Get from the test computer you broke into to the fake finance vault.",
                "One test account was given more access than its job needs. Which move should you make?",
                new[]
                {
                    "Use the test account that has too much access",
                    "Keep guessing a real admin's password",
                    "Switch off the network alarms",
                },
                0,
                "PIVOT REACHED — one account with too much access opened up far more of the network.",
                "Least privilege (only the access you need), a walled-off network and extra sign-in checks stop an attacker spreading.",
                28,
                16),
            new StationScenario(
                StationKind.NetworkAttack,
                "NETWORK ATTACK  //  WHERE ATTACKS LAND",
                "Map where attacks can land on the fake company's network.",
                "The network map shows the staff laptops and phones, the office router, and the cloud storage the company rents. Which of these can an attacker target?",
                new[]
                {
                    "Only the laptops and phones, because that's where people click",
                    "Only the cloud, because that's where the data lives",
                    "All three: devices, the router and the cloud each need their own defense",
                },
                2,
                "NETWORK MAPPED — every connection point is a way in.",
                "Guard each point: keep devices updated with malware protection, lock down the router and its settings, and check the security of every cloud service you rely on.",
                26,
                18,
                "Pick the answer that best describes the network."),
        };

        public static string DisplayName(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.SpotThePhish: return "Social Engineering";
                case StationKind.BypassMfa: return "Bypass MFA";
                case StationKind.EscalatePrivileges: return "Escalate Privileges";
                case StationKind.NetworkAttack: return "Attack Points";
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
                case StationKind.SpotThePhish: return "SPOT THE PHISH";
                case StationKind.BypassMfa: return "PHISH FRENZY";
                case StationKind.EscalatePrivileges: return "ACCESS GRABBER";
                case StationKind.NetworkAttack: return "NETWORK ATTACK";
                default: return "GAME ROOM";
            }
        }

        /// <summary>The live screen: the game's name over the attack step.</summary>
        public static string ShortLabel(StationKind kind)
        {
            string step;
            switch (kind)
            {
                case StationKind.SpotThePhish: step = "SOCIAL ENGINEERING"; break;
                case StationKind.BypassMfa: step = "BYPASS MFA"; break;
                case StationKind.EscalatePrivileges: step = "ESCALATE PRIVILEGES"; break;
                case StationKind.NetworkAttack: step = "ATTACK POINTS"; break;
                default: step = "SIMULATION"; break;
            }
            return MachineTitle(kind) + "\n<size=60%>" + step + "</size>";
        }
    }
}
