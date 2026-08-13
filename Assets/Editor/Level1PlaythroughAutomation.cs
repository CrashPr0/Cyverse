using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Cyverse.Testing;

namespace Cyverse.Editor
{
    /// <summary>Menu and command-line front end for the Level 1 smoke run.</summary>
    [InitializeOnLoad]
    public static class Level1PlaythroughAutomation
    {
        private const string ScenePath = "Assets/Scenes/Level1_IAM_VisualPass.unity";
        private const string ActiveKey = "Cyverse.Playthrough.Active";
        private const string CiKey = "Cyverse.Playthrough.CI";
        private const string PreviousSceneKey = "Cyverse.Playthrough.PreviousScene";
        private const string ResultKey = "Cyverse.Playthrough.Result";
        private const string WatchKey = "Cyverse.Playthrough.Watch";
        private const string CampaignKey = "Cyverse.Playthrough.Campaign";
        private static Level1AutomatedPlaythrough driver;
        private static Level1TasPlayback tas;
        private static CampaignTasPlayback campaign;

        static Level1PlaythroughAutomation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        [MenuItem("CyVerse/Testing/Run Level 1 Automated Playthrough")]
        public static void RunFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PLAYTHROUGH] Stop Play Mode before starting an automated run.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Begin(false, false, false);
        }

        [MenuItem("CyVerse/Testing/Watch Level 1 TAS Replay")]
        public static void WatchTasFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[TAS] Stop Play Mode before starting a replay.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Begin(false, true, false);
        }

        [MenuItem("CyVerse/Testing/Watch Full Campaign TAS (Password to Level 3)")]
        public static void WatchCampaignTasFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[CAMPAIGN TAS] Stop Play Mode before starting a replay.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Begin(false, true, true);
        }

        /// <summary>CI entry point: omit -quit; this method exits Unity with 0
        /// on pass or 1 on failure after the asynchronous Play Mode run.</summary>
        public static void RunFromCommandLine()
        {
            Begin(true, false, false);
        }

        private static void Begin(bool ci, bool watch, bool campaignRun)
        {
            string previous = EditorSceneManager.GetActiveScene().path;
            SessionState.SetString(PreviousSceneKey, previous ?? "");
            SessionState.SetBool(CiKey, ci);
            SessionState.SetBool(WatchKey, watch);
            SessionState.SetBool(CampaignKey, campaignRun);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(ResultKey, -1);
            string startScene = campaignRun ? "Assets/Scenes/PasswordLock.unity" : ScenePath;
            EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);
            Debug.Log((campaignRun ? "[CAMPAIGN TAS]" : "[PLAYTHROUGH]") + " Loaded " + startScene + "; entering Play Mode");
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                bool watch = SessionState.GetBool(WatchKey, false);
                bool campaignRun = SessionState.GetBool(CampaignKey, false);
                var go = new GameObject(campaignRun ? "Campaign TAS Playback" : watch ? "Level1 TAS Playback" : "Level1 Automated Playthrough");
                if (campaignRun) campaign = go.AddComponent<CampaignTasPlayback>();
                else if (watch) tas = go.AddComponent<Level1TasPlayback>();
                else driver = go.AddComponent<Level1AutomatedPlaythrough>();
            }
            else if (state == PlayModeStateChange.EnteredEditMode &&
                     SessionState.GetInt(ResultKey, -1) >= 0)
            {
                FinishInEditor();
            }
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            bool watch = SessionState.GetBool(WatchKey, false);
            bool campaignRun = SessionState.GetBool(CampaignKey, false);
            bool finished = campaignRun ? campaign != null && campaign.Finished :
                watch ? tas != null && tas.Finished : driver != null && driver.Finished;
            if (!finished) return;

            bool passed = campaignRun ? campaign.Passed : watch ? tas.Passed : driver.Passed;
            string failure = campaignRun ? campaign.Failure : watch ? tas.Failure : driver.Failure;
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            if (!passed) Debug.LogError((watch ? "[TAS] " : "[PLAYTHROUGH] ") + failure);
            EditorApplication.isPlaying = false;
        }

        private static void FinishInEditor()
        {
            int result = SessionState.GetInt(ResultKey, 1);
            bool ci = SessionState.GetBool(CiKey, false);
            bool watch = SessionState.GetBool(WatchKey, false);
            bool campaignRun = SessionState.GetBool(CampaignKey, false);
            string previous = SessionState.GetString(PreviousSceneKey, "");
            SessionState.SetBool(ActiveKey, false);
            driver = null;
            tas = null;
            campaign = null;

            if (!ci && !string.IsNullOrEmpty(previous) && previous != ScenePath)
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

            string label = campaignRun ? "[CAMPAIGN TAS]" : watch ? "[TAS]" : "[PLAYTHROUGH]";
            Debug.Log(result == 0
                ? label + " Automated run finished successfully."
                : label + " Automated run failed; inspect the preceding log.");
            if (ci) EditorApplication.delayCall += () => EditorApplication.Exit(result);
        }
    }
}
