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
    }
}
#endif
