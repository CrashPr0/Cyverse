#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Cyverse.Level;

namespace Cyverse.EditorTools
{
    /// <summary>
    /// Editor menu that generates the full Level 0 as real, editable GameObjects
    /// in the active scene (using the same SceneFactory as the runtime
    /// bootstrap). Build it once, then move/retune/replace objects by hand and
    /// save the scene. Because the systems self-wire (Level0Manager discovers
    /// stations; StationSetup loads each station's content), the hand-edited
    /// scene is fully playable without the Level0Bootstrap object.
    /// </summary>
    public static class Level0SceneBuilder
    {
        [MenuItem("CyVerse/Build Level 0 Scene")]
        public static void Build()
        {
            if (Object.FindObjectOfType<Level0Bootstrap>() != null)
            {
                if (!EditorUtility.DisplayDialog("Build Level 0",
                        "This scene has a Level0Bootstrap object, which also builds the level at " +
                        "Play time, so you'd get a doubled scene. Build anyway?",
                        "Build anyway", "Cancel"))
                    return;
            }

            if (GameObject.Find("GameSystems") != null)
            {
                if (!EditorUtility.DisplayDialog("Build Level 0",
                        "A Level 0 appears to already be built in this scene. Build another?",
                        "Build anyway", "Cancel"))
                    return;
            }

            SceneFactory.BuildAll();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CyVerse: Level 0 built into '" + scene.name +
                      "'. Tweak the objects, then File > Save. Tip: build into an empty scene.");
        }

        /// <summary>Upgrade path for scenes saved before the Security Scanner
        /// existed: adds just the scanner without rebuilding the level.</summary>
        [MenuItem("CyVerse/Add Security Scanner")]
        public static void AddScanner()
        {
            if (Object.FindObjectOfType<Cyverse.Interaction.FaceScanner>() != null)
            {
                EditorUtility.DisplayDialog("Add Security Scanner",
                    "This scene already has a Security Scanner.", "OK");
                return;
            }

            SceneFactory.BuildScanner();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("CyVerse: Security Scanner added at (8, 0, 15). Move it where you like and save.");
        }

        /// <summary>Upgrade path for existing scenes: adds the furniture set
        /// (desk pods, lounge, reception, server wall, lockers, drones, plants)
        /// under a single "Furnishings" root without rebuilding the level.</summary>
        [MenuItem("CyVerse/Add Furnishings")]
        public static void AddFurnishings()
        {
            if (GameObject.Find("Furnishings") != null)
            {
                EditorUtility.DisplayDialog("Add Furnishings",
                    "This scene already has a Furnishings root. Delete or rename it first " +
                    "if you want a fresh set.", "OK");
                return;
            }

            PropFactory.BuildFurnishings();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("CyVerse: Furnishings added under a 'Furnishings' root. " +
                      "Move/edit/delete props freely, then save the scene.");
        }

        /// <summary>Upgrade path: adds the security guard NPC near the spawn
        /// without rebuilding the level.</summary>
        [MenuItem("CyVerse/Add Guard NPC")]
        public static void AddGuard()
        {
            if (Object.FindObjectOfType<Cyverse.Interaction.GuardNPC>() != null)
            {
                EditorUtility.DisplayDialog("Add Guard NPC",
                    "This scene already has a Guard NPC.", "OK");
                return;
            }

            Cyverse.Interaction.GuardNPC.Build(new Vector3(2.2f, 0f, -5.5f), 180f);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("CyVerse: Guard NPC added at (2.2, 0, -5.5). Move him where you like and save.");
        }
    }
}
#endif
