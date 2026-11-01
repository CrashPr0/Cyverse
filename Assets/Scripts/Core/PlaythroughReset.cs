using UnityEngine;

namespace Cyverse.Core
{
    /// <summary>
    /// Every launch of the game (a page load or reload on WebGL, entering
    /// Play mode in the editor) starts a fresh playthrough: the previous
    /// run's saved progress is wiped before the first scene loads. Progress
    /// still carries across scene changes within one run, because it is only
    /// cleared here, once per application start.
    ///
    /// Cleared: level completion, Hard clears and bests, best scores, SOC
    /// handoff evidence, glossary unlocks, the callsign and the "seen the
    /// controls" flag (so the next player gets the intro card again).
    ///
    /// Kept on purpose: player settings (volume, captions, mouse, FOV,
    /// graphics tier, invert colours, motion, TTS), the scenario roster
    /// index (so each fresh run rotates to a different culprit), and the
    /// playtest metrics log (research telemetry, not game state).
    /// </summary>
    public static class PlaythroughReset
    {
        /// <summary>Exact keys of the saved playthrough.</summary>
        public static readonly string[] Keys =
        {
            "cv_best",
            "cv_callsign",
            "cv_seen_controls",
            "cv_glossary_unlocked",
            "cv_scenario_roster_finished",
            "cv_soc_compromised_computer",
            "cv_soc_chain_of_custody",
            "cv_soc_playbook_solved",
            "cv_soc_evidence_json",
            "cv_soc_evidence_locked",
        };

        /// <summary>Per-level keys, suffixed with the level index 0..4.</summary>
        public static readonly string[] LevelKeyPrefixes =
        {
            "cv_done_",
            "cv_hard_done_",
            "cv_hard_best_",
        };

        /// <summary>Security+ terminal keys, suffixed with the role name.</summary>
        public const string SecPlusBestPrefix = "cv_secplus_best_";

        public const int MaxLevel = 4;

        /// <summary>Runs before any scene (and so before ScenarioRoster's
        /// startup pick) on every application start.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnLaunch()
        {
            ClearSavedPlaythrough();
        }

        public static void ClearSavedPlaythrough()
        {
            foreach (string key in Keys)
                PlayerPrefs.DeleteKey(key);

            foreach (string prefix in LevelKeyPrefixes)
                for (int level = 0; level <= MaxLevel; level++)
                    PlayerPrefs.DeleteKey(prefix + level);

            foreach (string role in System.Enum.GetNames(typeof(Cyverse.Level.NiceRole)))
                PlayerPrefs.DeleteKey(SecPlusBestPrefix + role);

            PlayerPrefs.Save();
        }
    }
}
