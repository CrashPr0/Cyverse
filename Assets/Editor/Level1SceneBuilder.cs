#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Cyverse.Level;

namespace Cyverse.EditorTools
{
    /// <summary>
    /// Editor menu that generates the full Level 1 (Cyber Defense) as real,
    /// editable GameObjects in the active scene (using the same
    /// Level1SceneFactory as the runtime bootstrap). Build it once, then
    /// move/retune/replace objects by hand and save the scene. Mirrors
    /// Level0SceneBuilder's workflow exactly.
    /// </summary>
    public static class Level1SceneBuilder
    {
        [MenuItem("CyVerse/Build Level 1 Scene")]
        public static void Build()
        {
            if (Object.FindObjectOfType<Level1Bootstrap>() != null)
            {
                if (!EditorUtility.DisplayDialog("Build Level 1",
                        "This scene has a Level1Bootstrap object, which also builds the level at " +
                        "Play time, so you'd get a doubled scene. Build anyway?",
                        "Build anyway", "Cancel"))
                    return;
            }

            if (GameObject.Find("GameSystems") != null)
            {
                if (!EditorUtility.DisplayDialog("Build Level 1",
                        "A level appears to already be built in this scene. Build another?",
                        "Build anyway", "Cancel"))
                    return;
            }

            Level1SceneFactory.BuildAll();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CyVerse: Level 1 built into '" + scene.name +
                      "'. Tweak the objects, then File > Save. Tip: build into an empty scene.");
        }

        /// <summary>Upgrade path for scenes saved before the Threat Response
        /// Console existed: adds just the gate without rebuilding the level.</summary>
        [MenuItem("CyVerse/Add Threat Response Console")]
        public static void AddGate()
        {
            if (Object.FindObjectOfType<Cyverse.Interaction.Level1Gate>() != null)
            {
                EditorUtility.DisplayDialog("Add Threat Response Console",
                    "This scene already has a Threat Response Console.", "OK");
                return;
            }

            Level1SceneFactory.BuildGate();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("CyVerse: Threat Response Console added at (8, 0, 15). Move it where you like and save.");
        }

        /// <summary>Upgrade path: adds the SOC Lead NPC near the spawn without
        /// rebuilding the level.</summary>
        [MenuItem("CyVerse/Add SOC Lead NPC")]
        public static void AddSocLead()
        {
            if (Object.FindObjectOfType<Cyverse.Interaction.GuardNPC>() != null)
            {
                EditorUtility.DisplayDialog("Add SOC Lead NPC",
                    "This scene already has a guide NPC.", "OK");
                return;
            }

            Level1SceneFactory.BuildGuard();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("CyVerse: SOC Lead NPC added at (2.2, 0, -5.5). Move them where you like and save.");
        }
    }
}
#endif
