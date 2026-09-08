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
            new VideoStation.Slide("CYBER ATTACK  //  CONTROLLED SIMULATION",
                "You are playing the red-team role inside a closed training lab. Every employee, host and record is synthetic. Choose concepts, not real tools: the goal is to understand which defensive control would have stopped each stage.", 10f),
            new VideoStation.Slide("THE ATTACK SURFACE",
                "Recon turns public details into a target map. Strong data minimization, staff awareness and monitored public services reduce what an attacker can learn before access is attempted.", 10f),
            new VideoStation.Slide("ACCESS  →  PIVOT",
                "A weak entry point is only useful when it leads somewhere. Phishing-resistant MFA, least privilege and network segmentation turn a single compromised test endpoint into a dead end.", 11f),
            new VideoStation.Slide("DATA  →  COVER TRACKS",
                "The final stages are deliberately capped. Endpoint telemetry, egress controls and immutable audit trails should reveal the simulated transfer or tampering before sensitive data leaves the vault.", 11f),
            new VideoStation.Slide("RULES OF ENGAGEMENT",
                "Stay inside the blue-line simulation. Do not use real credentials, external services or payloads. A high score means you recognized the right attack surface while making the defensive lesson visible.", 9f),
        };

        public static StationScenario[] Stations() => new[]
        {
            new StationScenario(
                StationKind.BypassMfa,
                "BYPASS MFA  //  INITIAL ACCESS",
                "Choose a simulated entry vector against the training tenant.",
                "A test mailbox accepts one of three proposals. The red-team rules allow only a sandboxed exercise with synthetic data. Which vector demonstrates the risk while preserving consent and auditability?",
                new[]
                {
                    "Reuse a real employee password from an unrelated service",
                    "Send a simulated phishing lure to the approved test mailbox",
                    "Deliver an unreviewed executable to a production workstation",
                },
                1,
                "TEST MAILBOX OPENED — the lure reached the synthetic tenant.",
                "Phishing-resistant MFA, mail filtering and user reporting can stop a lure before it becomes access.",
                22,
                12),
            new StationScenario(
                StationKind.EscalatePrivileges,
                "ESCALATE PRIVILEGES  //  LATERAL MOVEMENT",
                "Pivot from the sandbox endpoint toward the synthetic finance vault.",
                "The compromised test endpoint can see three identity paths. Only one intentionally models an over-permissioned account in the lab. Which path exposes the least-privilege failure without crossing the simulation boundary?",
                new[]
                {
                    "Use the intentionally over-permissioned sandbox identity",
                    "Break into a real administrator account until it works",
                    "Disable network monitoring so every route looks the same",
                },
                0,
                "PIVOT REACHED — an over-permissioned sandbox identity widened the blast radius.",
                "Least privilege, segmentation and step-up authentication limit lateral movement after an endpoint is lost.",
                24,
                16),
            new StationScenario(
                StationKind.ExtractData,
                "EXTRACT DATA  //  CONTROLLED TRANSFER",
                "Move only the capped synthetic training dataset through the monitored lab channel.",
                "The synthetic vault is protected by endpoint telemetry and egress monitoring. Which final action shows the risk while keeping the exercise bounded and observable?",
                new[]
                {
                    "Copy unrestricted personal records to an unknown external host",
                    "Leave an untracked scheduled task on a production machine",
                    "Queue a capped synthetic export through the monitored lab channel",
                },
                2,
                "SIMULATED EXPORT QUEUED — the meter records what crossed the lab boundary.",
                "Strong egress policy, immutable audit logs and rapid containment reduce data loss.",
                28,
                20),
            new StationScenario(
                StationKind.CoverTracks,
                "COVER TRACKS  //  AUDIT TRAIL",
                "Test whether simulated activity can be hidden without erasing the evidence defenders need.",
                "The training SOC is watching an immutable synthetic timeline. Which action tests the cover-tracks risk while keeping the exercise reversible and reviewable?",
                new[]
                {
                    "Add a labeled sandbox noise marker and leave the audit trail intact",
                    "Alter or delete immutable logs on a production system",
                    "Disable telemetry so every action disappears from monitoring",
                },
                0,
                "COVER-TRACKS TEST LOGGED — the immutable timeline kept the action reviewable.",
                "Immutable audit trails, alert correlation and rapid response expose attempts to hide activity.",
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

        public static string ShortLabel(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.BypassMfa: return "BYPASS MFA";
                case StationKind.EscalatePrivileges: return "ESCALATE PRIVILEGES";
                case StationKind.ExtractData: return "EXTRACT DATA";
                case StationKind.CoverTracks: return "COVER TRACKS";
                default: return "SIMULATION";
            }
        }
    }
}
