using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Stable, WebGL-safe identities used by the authored SOC and forensics
    /// scenarios.  A single profile is selected once per application session
    /// and then reused by every scene so the evidence trail cannot disagree
    /// with the question bank after a scene reload.
    ///
    /// This intentionally cycles through a small authored pool instead of
    /// using UnityEngine.Random.  PlayerPrefs is available in WebGL and gives
    /// the next browser launch a different culprit without making a run
    /// nondeterministic halfway through a campaign.
    /// </summary>
    public static class ScenarioRoster
    {
        public const string RotationKey = "cv_scenario_roster_index";

        public sealed class Profile
        {
            public readonly string socUser;
            public readonly string payloadEmployee;
            public readonly string payloadEmail;
            public readonly string payloadIp;
            public readonly string payloadHostname;
            public readonly string insiderEmployee;
            public readonly string insiderEmail;
            public readonly string insiderHostname;

            public Profile(string socUser,
                string payloadEmployee, string payloadEmail, string payloadIp,
                string payloadHostname,
                string insiderEmployee, string insiderEmail, string insiderHostname)
            {
                this.socUser = socUser;
                this.payloadEmployee = payloadEmployee;
                this.payloadEmail = payloadEmail;
                this.payloadIp = payloadIp;
                this.payloadHostname = payloadHostname;
                this.insiderEmployee = insiderEmployee;
                this.insiderEmail = insiderEmail;
                this.insiderHostname = insiderHostname;
            }
        }

        // The first profile preserves the original authored run.  Later
        // launches advance in a fixed order, which makes reports and TAS
        // captures reproducible while still changing the culprit.
        private static readonly Profile[] Profiles =
        {
            new Profile(
                "d.chen",
                "devon.james", "devon.james@cyverse.edu", "10.10.1.17", "WS-DJAMES",
                "drew.patel", "drew.patel@cyverse.edu", "WS-DPATEL"),
            new Profile(
                "k.ramos",
                "morgan.lee", "morgan.lee@cyverse.edu", "10.10.1.18", "WS-MLEE",
                "jordan.cruz", "jordan.cruz@cyverse.edu", "WS-JCRUZ"),
            new Profile(
                "j.okafor",
                "alex.kim", "alex.kim@cyverse.edu", "10.10.1.19", "WS-AKIM",
                "jamie.park", "jamie.park@cyverse.edu", "WS-JPARK"),
            new Profile(
                "n.singh",
                "sam.ortiz", "sam.ortiz@cyverse.edu", "10.10.1.14", "WS-SORTIZ",
                "quinn.baker", "quinn.baker@cyverse.edu", "WS-QBAKER"),
        };

        private static bool sessionInitialized;
        private static int sessionIndex;

        public static int ProfileCount => Profiles.Length;

        public static Profile Current
        {
            get
            {
                EnsureSession();
                return Profiles[sessionIndex];
            }
        }

        /// <summary>
        /// Select the profile for this application run. The stored index is
        /// advanced only once for a fresh campaign. Returning to the Hub,
        /// reloading a level, or reopening an in-progress campaign keeps the
        /// same identities and therefore the same evidence trail.
        /// </summary>
        public static void EnsureSession()
        {
            if (sessionInitialized) return;

            int previous = PlayerPrefs.GetInt(RotationKey, -1);
            sessionIndex = previous < 0 ? 0 : previous % Profiles.Length;

            PlayerPrefs.SetInt(RotationKey, sessionIndex);
            PlayerPrefs.Save();
            sessionInitialized = true;
        }

        /// <summary>
        /// Called by the password/startup scene. A completed campaign or a
        /// brand-new save advances to the next profile; an in-progress save
        /// deliberately keeps its profile so saved evidence and case answers
        /// cannot change underneath the player.
        /// </summary>
        public static void BeginStartupSession()
        {
            if (sessionInitialized) return;

            int previous = PlayerPrefs.GetInt(RotationKey, -1);
            if (previous < 0)
                sessionIndex = 0;
            else if (HasInProgressCampaign())
                sessionIndex = previous % Profiles.Length;
            else
                sessionIndex = (previous + 1) % Profiles.Length;

            PlayerPrefs.SetInt(RotationKey, sessionIndex);
            PlayerPrefs.Save();
            sessionInitialized = true;
        }

        private static bool HasInProgressCampaign()
        {
            // Level 3 is the final story level. Once it is complete, the next
            // startup is a fresh replay and may rotate. Any earlier progress,
            // including a structured SOC handoff, must remain stable.
            if (PlayerPrefs.GetInt("cv_done_3", 0) == 1) return false;
            return PlayerPrefs.GetInt("cv_done_0", 0) == 1 ||
                   PlayerPrefs.GetInt("cv_done_1", 0) == 1 ||
                   PlayerPrefs.GetInt("cv_done_2", 0) == 1 ||
                   PlayerPrefs.GetInt("cv_soc_compromised_computer", 0) == 1 ||
                   PlayerPrefs.GetInt("cv_soc_chain_of_custody", 0) == 1 ||
                   PlayerPrefs.GetInt("cv_soc_playbook_solved", 0) == 1 ||
                   PlayerPrefs.HasKey(SocProgress.EvidenceJsonKey);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Allows isolated editor/TAS runs to start from a known
        /// profile without changing a player's persisted rotation.</summary>
        public static void SetSessionForAutomation(int index)
        {
            sessionIndex = ((index % Profiles.Length) + Profiles.Length) % Profiles.Length;
            sessionInitialized = true;
        }
#endif
    }
}
