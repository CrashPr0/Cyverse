#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.Testing;

namespace Cyverse.Editor
{
    /// <summary>
    /// Editor front end for portfolio screenshots. Open with
    /// Tools &gt; CyVerse &gt; Capture Toolbar, or run the whole set from the
    /// command line via <see cref="CyverseCaptureCli.CaptureAll"/>.
    ///
    /// PlayerPrefs are snapshotted in Edit Mode and restored when Play Mode
    /// ends, so the tool never leaves capture state in a saved playthrough.
    /// It never loads the PasswordLock scene and never uses the access code.
    /// </summary>
    [InitializeOnLoad]
    public static class CyverseCaptureSession
    {
        internal const string ActiveKey = "Cyverse.Capture.Active";
        internal const string CiKey = "Cyverse.Capture.CI";
        internal const string ModeKey = "Cyverse.Capture.Mode";
        internal const string ShotKey = "Cyverse.Capture.Shot";
        internal const string PrefsKey = "Cyverse.Capture.PrefsSnapshot";
        internal const string HasSnapshotKey = "Cyverse.Capture.HasSnapshot";
        internal const string ResultKey = "Cyverse.Capture.Result";

        internal const string ModeJump = "jump";
        internal const string ModeCaptureAll = "all";

        static CyverseCaptureSession()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        internal static void RequestJump(PortfolioShot shot)
        {
            if (shot == null) return;
            if (EditorApplication.isPlaying)
            {
                CyversePortfolioCaptureDriver.Ensure().BeginJump(shot, false);
                return;
            }
            if (!PrepareToEnterPlayMode()) return;
            SessionState.SetString(ModeKey, ModeJump);
            SessionState.SetString(ShotKey, shot.Id);
            SessionState.SetBool(CiKey, false);
            EnterPlayMode();
        }

        internal static void RequestCaptureCurrent()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning(CyversePortfolioCapture.LogPrefix +
                    " Jump to a shot first, or press Play, then Capture.");
                return;
            }
            CyversePortfolioCaptureDriver.Ensure().BeginCaptureCurrent();
        }

        internal static void RequestCaptureAll(bool ci)
        {
            if (EditorApplication.isPlaying)
            {
                SessionState.SetBool(CiKey, ci);
                SessionState.SetString(ModeKey, ModeCaptureAll);
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(ResultKey, -1);
                CyversePortfolioCaptureDriver.Ensure().BeginCaptureAll();
                return;
            }
            if (!PrepareToEnterPlayMode()) return;
            SessionState.SetString(ModeKey, ModeCaptureAll);
            SessionState.SetString(ShotKey, "");
            SessionState.SetBool(CiKey, ci);
            EnterPlayMode();
        }

        private static bool PrepareToEnterPlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(CyversePortfolioCapture.LogPrefix +
                    " Stop Play Mode before starting a fresh capture run, or use the toolbar while already playing.");
                return false;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            SnapshotProgressPrefs();
            return true;
        }

        private static void EnterPlayMode()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(ResultKey, -1);
            if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Debug.Log(CyversePortfolioCapture.LogPrefix + " Entering Play Mode");
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && SessionState.GetBool(ActiveKey, false))
            {
                // Re-take the snapshot immediately before the domain reload so
                // a mid-session edit-mode change is not lost.
                if (string.IsNullOrEmpty(SessionState.GetString(PrefsKey, "")))
                    SnapshotProgressPrefs();
            }
            else if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                var driver = CyversePortfolioCaptureDriver.Ensure();
                string mode = SessionState.GetString(ModeKey, ModeJump);
                if (mode == ModeCaptureAll) driver.BeginCaptureAll();
                else
                {
                    PortfolioShot shot = CyversePortfolioCapture.Find(SessionState.GetString(ShotKey, ""));
                    if (shot != null) driver.BeginJump(shot, false);
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (SessionState.GetBool(HasSnapshotKey, false))
                    RestoreProgressPrefs();
                if (SessionState.GetBool(ActiveKey, false) &&
                    SessionState.GetInt(ResultKey, -1) >= 0)
                    FinishInEditor();
            }
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            var driver = CyversePortfolioCaptureDriver.Instance;
            if (driver == null || !driver.Finished || driver.Busy) return;

            bool captureAll = SessionState.GetString(ModeKey, "") == ModeCaptureAll;
            SessionState.SetInt(ResultKey, driver.Passed ? 0 : 1);
            if (captureAll || SessionState.GetBool(CiKey, false))
                EditorApplication.isPlaying = false;
            else
                SessionState.SetBool(ActiveKey, false);
        }

        private static void FinishInEditor()
        {
            int result = SessionState.GetInt(ResultKey, 1);
            bool ci = SessionState.GetBool(CiKey, false);
            SessionState.SetBool(ActiveKey, false);
            Debug.Log(result == 0
                ? CyversePortfolioCapture.LogPrefix + " Run finished successfully."
                : CyversePortfolioCapture.LogPrefix + " Run finished with errors; inspect the preceding log.");
            if (ci) EditorApplication.delayCall += () => EditorApplication.Exit(result);
        }

        /// <summary>Stores known playthrough keys so Play Mode work can be
        /// reversed when returning to Edit Mode. Settings keys are left alone.</summary>
        internal static void SnapshotProgressPrefs()
        {
            var lines = new List<string>();
            foreach (string key in PlaythroughReset.Keys)
                CaptureKey(lines, key);
            foreach (string prefix in PlaythroughReset.LevelKeyPrefixes)
                for (int level = 0; level <= PlaythroughReset.MaxLevel; level++)
                    CaptureKey(lines, prefix + level);
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < sceneCount; i++)
            {
                string scene = Path.GetFileNameWithoutExtension(
                    UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i));
                CaptureKey(lines, PlaythroughReset.BestScorePrefix + scene);
                CaptureKey(lines, PlaythroughReset.BestScorePrefix + scene + "_hard");
            }
            foreach (string role in Enum.GetNames(typeof(NiceRole)))
                CaptureKey(lines, PlaythroughReset.SecPlusBestPrefix + role);
            SessionState.SetString(PrefsKey, string.Join("\n", lines.ToArray()));
            SessionState.SetBool(HasSnapshotKey, true);
        }

        private static void CaptureKey(List<string> lines, string key)
        {
            if (!PlayerPrefs.HasKey(key)) return;
            // Progress keys are ints except the SOC evidence JSON blob.
            if (key == SocProgress.EvidenceJsonKey)
                lines.Add("s\t" + key + "\t" + Encode(PlayerPrefs.GetString(key, "")));
            else
                lines.Add("i\t" + key + "\t" + PlayerPrefs.GetInt(key, 0));
        }

        internal static void RestoreProgressPrefs()
        {
            string blob = SessionState.GetString(PrefsKey, "");
            // Always clear the keys we may have written in Play Mode, then
            // put back only what was present before the session.
            foreach (string key in PlaythroughReset.Keys)
                PlayerPrefs.DeleteKey(key);
            foreach (string prefix in PlaythroughReset.LevelKeyPrefixes)
                for (int level = 0; level <= PlaythroughReset.MaxLevel; level++)
                    PlayerPrefs.DeleteKey(prefix + level);
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < sceneCount; i++)
            {
                string scene = Path.GetFileNameWithoutExtension(
                    UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i));
                PlayerPrefs.DeleteKey(PlaythroughReset.BestScorePrefix + scene);
                PlayerPrefs.DeleteKey(PlaythroughReset.BestScorePrefix + scene + "_hard");
            }
            foreach (string role in Enum.GetNames(typeof(NiceRole)))
                PlayerPrefs.DeleteKey(PlaythroughReset.SecPlusBestPrefix + role);

            if (!string.IsNullOrEmpty(blob))
            {
                foreach (string line in blob.Split('\n'))
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    string[] parts = line.Split('\t');
                    if (parts.Length < 3) continue;
                    if (parts[0] == "s") PlayerPrefs.SetString(parts[1], Decode(parts[2]));
                    else
                    {
                        int value;
                        if (int.TryParse(parts[2], out value))
                            PlayerPrefs.SetInt(parts[1], value);
                    }
                }
            }
            PlayerPrefs.Save();
            SessionState.EraseString(PrefsKey);
            SessionState.EraseBool(HasSnapshotKey);
        }

        private static string Encode(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));

        private static string Decode(string value)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? "")); }
            catch (FormatException) { return ""; }
        }
    }

    /// <summary>Command-line entry point for the portfolio capture set.</summary>
    public static class CyverseCaptureCli
    {
        /// <summary>
        /// Batch entry point. Launch Unity without -quit and without
        /// -nographics so Play Mode can render; this method exits the editor
        /// with 0 on success or 1 on failure after the run finishes.
        /// </summary>
        public static void CaptureAll()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError(CyversePortfolioCapture.LogPrefix +
                    " No graphics device. Re-run without -nographics so the Game view can be captured.");
                EditorApplication.Exit(1);
                return;
            }
            Directory.CreateDirectory(CyversePortfolioCapture.OutputDirectory);
            Debug.Log(string.Format("{0} Starting CaptureAll at {1}x{2} (scale {3}x) -> {4}",
                CyversePortfolioCapture.LogPrefix,
                CyversePortfolioCapture.ScaledWidth,
                CyversePortfolioCapture.ScaledHeight,
                CyversePortfolioCapture.Scale,
                CyversePortfolioCapture.OutputDirectory));
            CyverseCaptureSession.RequestCaptureAll(true);
        }
    }

    public sealed class CyverseCaptureToolbar : EditorWindow
    {
        private Vector2 scroll;
        private string status = "Pick a shot, then press Capture or Capture all.";

        [MenuItem("Tools/CyVerse/Capture Toolbar")]
        public static void Open()
        {
            var window = GetWindow<CyverseCaptureToolbar>("CyVerse Capture");
            window.minSize = new Vector2(280f, 360f);
            window.Show();
        }

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += RepaintOnPlayMode;
            EditorApplication.update += RepaintIfBusy;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= RepaintOnPlayMode;
            EditorApplication.update -= RepaintIfBusy;
        }

        private void RepaintOnPlayMode(PlayModeStateChange _)
        {
            Repaint();
        }

        private void RepaintIfBusy()
        {
            var driver = CyversePortfolioCaptureDriver.Instance;
            if (driver != null && driver.Busy) Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Portfolio screenshots", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each button enters Play Mode if needed, loads the scene, jumps to that state, and frames the camera. " +
                $"Capture writes a {CyversePortfolioCapture.ScaledWidth}x{CyversePortfolioCapture.ScaledHeight} PNG. Capture all runs the whole list. " +
                "PNGs land in Captures/portfolio/. Progress PlayerPrefs are restored when Play Mode ends. " +
                "The PasswordLock scene is never loaded.",
                MessageType.Info);

            EditorGUILayout.LabelField("Output", CyversePortfolioCapture.OutputDirectory);
            var driver = CyversePortfolioCaptureDriver.Instance;
            int currentScale = CyversePortfolioCapture.Scale;
            bool envLocked = CyversePortfolioCapture.HasEnvScaleOverride;
            EditorGUI.BeginDisabledGroup(envLocked || (driver != null && driver.Busy));
            int nextScale = EditorGUILayout.IntPopup(
                "Scale",
                currentScale,
                new[] { "1x (1920x1080)", "2x (3840x2160)", "3x (5760x3240)", "4x (7680x4320)" },
                new[] { 1, 2, 3, 4 });
            if (!envLocked && nextScale != currentScale)
                CyversePortfolioCapture.Scale = nextScale;
            EditorGUI.EndDisabledGroup();
            if (envLocked)
                EditorGUILayout.LabelField("Scale override", CyversePortfolioCapture.ScaleEnvVar);
            if (driver != null && !string.IsNullOrEmpty(driver.Status))
                status = driver.Status;
            EditorGUILayout.LabelField("Status", string.IsNullOrEmpty(status) ? "—" : status);
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (PortfolioShot shot in CyversePortfolioCapture.Shots)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(shot.Title, GUILayout.Height(22f)))
                {
                    status = "Jumping to " + shot.Title;
                    CyverseCaptureSession.RequestJump(shot);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(driver != null && driver.Busy);
            if (GUILayout.Button("Capture", GUILayout.Height(28f)))
            {
                status = "Capturing current view";
                CyverseCaptureSession.RequestCaptureCurrent();
            }
            if (GUILayout.Button("Capture all", GUILayout.Height(28f)))
            {
                status = "Capturing all shots";
                CyverseCaptureSession.RequestCaptureAll(false);
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}
#endif
