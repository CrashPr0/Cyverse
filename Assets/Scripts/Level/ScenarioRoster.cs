using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Stable, WebGL-safe identities used by the authored SOC and forensics
    /// scenarios.  A single profile (the "bad actor" plus the Level 3 payload
    /// and insider employees) is selected once per PLAYTHROUGH and then
    /// reused by every scene, so Level 1's impossible-travel trail, Level 2's
    /// SOC alert, the SOC to DF evidence record and Level 3's logs all name
    /// the same person.
    ///
    /// What counts as a playthrough
    /// ----------------------------
    /// The game has no "new game" button, so a playthrough is defined at the
    /// one place every run passes through: the password/startup scene
    /// (<see cref="BeginStartupSession"/>).
    ///  - Empty storage (first visit, cleared site data, private window):
    ///    a RANDOM profile is picked.  This used to be profile 0 ("d.chen")
    ///    every time, which is why every new visitor saw the same name.
    ///  - Campaign in progress (Level 1/2 complete or any SOC handoff data
    ///    saved): the profile is kept, so a browser reload cannot change the
    ///    culprit underneath a half-finished investigation.
    ///  - Previous playthrough finished (Level 3 completed since the last
    ///    pick) or nothing to resume: advance to the NEXT profile, which by
    ///    construction can never equal the previous one.
    /// Scene reloads and Hub round-trips never re-select (static session).
    ///
    /// Selection state lives in PlayerPrefs (IndexedDB in WebGL, so it
    /// survives reloads; a private window starts empty).
    /// </summary>
    public static class ScenarioRoster
    {
        public const string RotationKey = "cv_scenario_roster_index";

        /// <summary>1 once the playthrough that owns <see cref="RotationKey"/>
        /// has completed the roster story arc; 0 once a new playthrough has
        /// begun.  Absent in saves that predate it (see
        /// <see cref="PlaythroughFinished"/>).  Needed because cv_done_3 is
        /// permanent and so cannot tell "finished just now" from "finished
        /// in an earlier playthrough".</summary>
        public const string FinishedKey = "cv_scenario_roster_finished";

        /// <summary>The last level whose content uses the roster identity
        /// (Digital Forensics).  Completing it ends the playthrough.  Raise
        /// this if a later level starts consuming the roster.</summary>
        public const int LastRosterLevel = 3;

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

        // Profile 0 is the original authored run (legacy saves made before
        // the roster existed always saw it).  Later playthroughs advance in a
        // fixed order, which keeps reports and TAS captures reproducible.
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
        private static System.Random fallbackRandom;

        internal readonly struct SessionState
        {
            internal readonly bool initialized;
            internal readonly int index;

            internal SessionState(bool initialized, int index)
            {
                this.initialized = initialized;
                this.index = index;
            }
        }

        internal static SessionState CaptureSessionState() => new SessionState(sessionInitialized, sessionIndex);

        internal static void RestoreSessionState(SessionState state)
        {
            sessionInitialized = state.initialized;
            sessionIndex = state.index;
        }

        /// <summary>Swap the RNG used for a first-ever pick (tests/TAS):
        /// returns an index in [0, count).  Null (default) uses a
        /// time-seeded RNG.  Saves that already hold a selection are
        /// deterministic (fixed rotation order) and never consult it.</summary>
        public static System.Func<int, int> RandomIndexSource;

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
        /// First access outside the startup scene (direct scene launch in the
        /// editor, tests): resume whatever is saved without rotating.  The
        /// saved SOC evidence, when present, is authoritative because it is
        /// what the player was actually shown in Level 2.
        /// </summary>
        public static void EnsureSession()
        {
            if (sessionInitialized) return;

            int stored = ReadStoredIndex();
            int fromEvidence = EvidenceProfileIndex();
            int index = fromEvidence >= 0 ? fromEvidence : (stored >= 0 ? stored : 0);

            Commit(index, newPlaythrough: false);
            Debug.Log($"[ScenarioRoster] session resumed without startup selection: " +
                      $"{Profiles[index].socUser} (index {index}, stored {stored})");
        }

        /// <summary>
        /// Called by the password/startup scene.  See the class summary for
        /// the rules.  Idempotent per application run.
        /// </summary>
        public static void BeginStartupSession()
        {
            if (sessionInitialized) return;

            int count = Profiles.Length;
            int stored = ReadStoredIndex();
            int fromEvidence = EvidenceProfileIndex();
            bool inProgress = HasInProgressCampaign();

            int index;
            bool newPlaythrough;
            string reason;

            if (stored < 0)
            {
                // Nothing selected yet.  A save that predates the roster
                // (SOC evidence, or any progress) was played as profile 0
                // ("d.chen" was hard-coded then), so continue from there.
                if (fromEvidence >= 0) stored = fromEvidence;
                else if (inProgress || PlayerPrefs.GetInt("cv_done_3", 0) == 1) stored = 0;
            }

            if (stored < 0)
            {
                // Genuinely empty storage (first visit, cleared site data,
                // private window): random, NOT profile 0 as before.
                index = PickRandomIndex(count);
                newPlaythrough = true;
                reason = "empty storage, random pick";
            }
            else if (PlaythroughFinished())
            {
                index = (stored + 1) % count;
                newPlaythrough = true;
                reason = "previous playthrough finished, advanced";
            }
            else if (inProgress)
            {
                index = fromEvidence >= 0 ? fromEvidence : stored;
                newPlaythrough = false;
                reason = "campaign in progress, kept";
            }
            else
            {
                index = (stored + 1) % count;
                newPlaythrough = true;
                reason = "nothing in progress, advanced";
            }

            // A fresh playthrough inherits the previous one's saved SOC
            // handoff only if the record names the new culprit; otherwise
            // Level 3's EvidenceManifest would disagree with its own logs.
            if (newPlaythrough && fromEvidence >= 0 && fromEvidence != index)
                RetargetSavedEvidence(Profiles[index].socUser);

            Commit(index, newPlaythrough);
            Debug.Log($"[ScenarioRoster] {Profiles[index].socUser} (index {index}); " +
                      $"stored {stored}, evidence {fromEvidence}, inProgress {inProgress}: {reason}");
        }

        /// <summary>
        /// Called by <see cref="Cyverse.Core.LevelProgress.MarkCompleted"/>.
        /// Completing the last roster level ends this playthrough, so the
        /// next startup rotates even though cv_done_N flags never reset.
        /// </summary>
        public static void NotifyLevelCompleted(int level)
        {
            if (level != LastRosterLevel) return;
            PlayerPrefs.SetInt(FinishedKey, 1);
            PlayerPrefs.Save();
        }

        private static void Commit(int index, bool newPlaythrough)
        {
            sessionIndex = index;
            PlayerPrefs.SetInt(RotationKey, index);
            if (newPlaythrough) PlayerPrefs.SetInt(FinishedKey, 0);
            PlayerPrefs.Save();
            sessionInitialized = true;
        }

        private static int ReadStoredIndex()
        {
            int stored = PlayerPrefs.GetInt(RotationKey, -1);
            return stored < 0 ? -1 : stored % Profiles.Length;
        }

        private static int PickRandomIndex(int count)
        {
            if (RandomIndexSource != null)
            {
                int forced = RandomIndexSource(count);
                return ((forced % count) + count) % count;
            }
            if (fallbackRandom == null)
            {
                // Not UnityEngine.Random: nothing may InitState it to a
                // constant and silently pin every launch to one culprit.
                long ticks = System.DateTime.UtcNow.Ticks;
                fallbackRandom = new System.Random(unchecked((int)ticks ^ (int)(ticks >> 32) ^ System.Environment.TickCount));
            }
            return fallbackRandom.Next(count);
        }

        /// <summary>Index of the profile whose socUser the saved SOC
        /// evidence names, or -1 when there is no evidence or the user is
        /// not a roster user (hand-written fixtures).</summary>
        private static int EvidenceProfileIndex()
        {
            if (!SocProgress.TryGetEvidence(out SocEvidenceRecord evidence) || evidence == null)
                return -1;
            for (int i = 0; i < Profiles.Length; i++)
                if (string.Equals(Profiles[i].socUser, evidence.user, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static void RetargetSavedEvidence(string socUser)
        {
            if (!SocProgress.TryGetEvidence(out SocEvidenceRecord evidence) || evidence == null) return;
            evidence.user = socUser;
            PlayerPrefs.SetString(SocProgress.EvidenceJsonKey, JsonUtility.ToJson(evidence));
        }

        private static bool PlaythroughFinished()
        {
            int flag = PlayerPrefs.GetInt(FinishedKey, -1);
            if (flag >= 0) return flag == 1;
            // Save from before FinishedKey existed: Level 3 complete means
            // the campaign was finished at some point.
            return PlayerPrefs.GetInt("cv_done_3", 0) == 1;
        }

        private static bool HasInProgressCampaign()
        {
            // Level 0 (Orientation) never shows the identity, so it does not
            // pin the culprit; Level 1 onward does.  Flags from a finished
            // playthrough stay set forever, which is fine: after a new pick
            // they keep the new identity pinned until it finishes too.
            return PlayerPrefs.GetInt("cv_done_1", 0) == 1 ||
                   PlayerPrefs.GetInt("cv_done_2", 0) == 1 ||
                   PlayerPrefs.GetInt(SocProgress.CompromisedComputerKey, 0) == 1 ||
                   PlayerPrefs.GetInt(SocProgress.ChainOfCustodyKey, 0) == 1 ||
                   PlayerPrefs.GetInt(SocProgress.PlaybookKey, 0) == 1 ||
                   PlayerPrefs.HasKey(SocProgress.EvidenceJsonKey);
        }
    }
}
