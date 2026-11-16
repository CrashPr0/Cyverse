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
            "cv_hub_intro_done",
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

        /// <summary>Best score per level, suffixed with the scene name.</summary>
        public const string BestScorePrefix = "cv_best_";

        public const int MaxLevel = 4;

        /// <summary>Runs before any scene (and so before ScenarioRoster's
        /// startup pick) on every application start.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnLaunch()
        {
            ClearSavedPlaythrough();
        }

        /// <summary>
        /// Starts a new playthrough without reloading the page: the same
        /// wipe as a launch, plus the state a launch gets for free because
        /// the program starts empty (cached callsign and glossary, the
        /// culprit profile). The caller then loads the Hub, which resets
        /// score and Hard mode and shows the title card again. Built for
        /// study sessions, where the next participant sits down at a game
        /// the last one left running.
        /// </summary>
        public static void StartNewPlaythrough()
        {
            ClearSavedPlaythrough();
            PlayerIdentity.ForgetCached();
            Cyverse.Level.GlossaryProgress.ForgetCached();
            Cyverse.Level.ScenarioRoster.BeginNewPlaythrough();
            PlaytestMetrics.Record("playthrough_reset", "source=settings");
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

            // Per-level best scores are keyed by scene name (see ResultsScreen).
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < sceneCount; i++)
            {
                string scene = System.IO.Path.GetFileNameWithoutExtension(
                    UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i));
                PlayerPrefs.DeleteKey(BestScorePrefix + scene);
                PlayerPrefs.DeleteKey(BestScorePrefix + scene + "_hard");
            }

            PlayerPrefs.Save();
        }
    }
}
