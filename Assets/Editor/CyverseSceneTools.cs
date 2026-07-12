#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Cyverse.Level;

namespace Cyverse.EditorTools
{
    /// <summary>
    /// Editor tools for the new game flow: build the Hub and Level 1 (I/AM)
    /// as editable GameObjects (same pattern as the Level 0/SOC builders),
    /// and register every CyVerse scene in Build Settings so scene loading
    /// (Password Lock → Hub → levels) works in Play mode and in builds.
    /// </summary>
    public static class CyverseSceneTools
    {
        [MenuItem("CyVerse/Build Hub Scene")]
        public static void BuildHub()
        {
            if (!ConfirmSceneEmpty("Build Hub")) return;
            HubSceneFactory.BuildAll();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("CyVerse: Hub built. Tweak the objects, then File > Save. Tip: build into an empty scene.");
        }

        [MenuItem("CyVerse/Build Level 1 (I-AM) Scene")]
        public static void BuildLevel1Iam()
        {
            if (!ConfirmSceneEmpty("Build Level 1 (I/AM)")) return;
            Level1IamSceneFactory.BuildAll();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("CyVerse: Level 1 (I/AM) built. Tweak the objects, then File > Save.");
        }

        private static bool ConfirmSceneEmpty(string title)
        {
            if (GameObject.Find("GameSystems") == null) return true;
            return EditorUtility.DisplayDialog(title,
                "A level appears to already be built in this scene. Build another on top?",
                "Build anyway", "Cancel");
        }

        /// <summary>Registers the full scene flow in Build Settings, in load
        /// order: PasswordLock (entry) → Hub → the levels. Existing non-CyVerse
        /// entries are preserved after them.</summary>
        [MenuItem("CyVerse/Add Scenes To Build Settings")]
        public static void AddScenesToBuildSettings()
        {
            string[] wanted =
            {
                "Assets/Scenes/PasswordLock.unity",
                "Assets/Scenes/Hub.unity",
                "Assets/Scenes/Level0.unity",
                "Assets/Scenes/Level1_IAM.unity",
                "Assets/Scenes/Level1.unity",
            };

            var list = new List<EditorBuildSettingsScene>();
            foreach (string path in wanted)
            {
                if (System.IO.File.Exists(path))
                    list.Add(new EditorBuildSettingsScene(path, true));
                else
                    Debug.LogWarning("CyVerse: scene not found, skipping: " + path);
            }

            foreach (var existing in EditorBuildSettings.scenes)
                if (System.Array.IndexOf(wanted, existing.path) < 0)
                    list.Add(existing);

            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"CyVerse: Build Settings now has {list.Count} scenes " +
                      "(PasswordLock first — it's the game's entry point).");
        }
    }
}
#endif
