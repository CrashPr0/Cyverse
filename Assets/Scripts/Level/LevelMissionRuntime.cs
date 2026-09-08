using UnityEngine;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Shared lifecycle for a playable mission. Level managers own only their
    /// progression rules; this module owns global reset, common runtime
    /// readiness, and the completion ceremony that must stay consistent.
    /// </summary>
    internal static class LevelMissionRuntime
    {
        internal sealed class Completion
        {
            public int levelNumber;
            public HubDoor exitDoor;
            public Color accent;
            public float elapsedSeconds;
            public string headerText;
            public string grantedLine;
            public string nextMissionText;
            public string replaySuffix;
            public int parScore;
        }

        public static void ResetScene(bool clearCarried)
        {
            GameState.Reset();
            ScoreSystem.Reset();
            if (clearCarried) Carryable.ClearCarried();
            Time.timeScale = 1f;
            Shader.SetGlobalFloat("_CyMotion", 1f);
        }

        public static void EnsureSharedRuntime(GameObject host, bool showEvidenceInventory)
        {
            if (host == null) return;

            Ensure<Quiz.QuizSystem>(host);
            Ensure<ResultsScreen>(host);
            Ensure<VisualDirector>(host);
            Ensure<Audio.AmbientHum>(host);
            Ensure<GlossaryPanel>(host);

            Camera camera = Camera.main;
            if (camera != null && camera.GetComponent<FirstPersonHands>() == null)
                camera.gameObject.AddComponent<FirstPersonHands>();

            if (showEvidenceInventory)
                EvidenceInventoryPanel.Ensure(host).RefreshNow();
        }

        public static void Complete(Completion completion)
        {
            if (completion == null) return;

            LevelProgress.MarkCompleted(completion.levelNumber);
            GameState.LevelComplete = true;
            FirstPersonController.LockCursor(false);
            if (completion.exitDoor != null) completion.exitDoor.SetUnlocked(true);

            if (completion.exitDoor != null)
            {
                BurstFX.SpawnAbove(completion.exitDoor.transform,
                    completion.accent, 70, 3.4f, 1.3f, 2.5f);
            }
            else
            {
                Vector3 fallback = Camera.main != null
                    ? Camera.main.transform.position + Camera.main.transform.forward * 2f
                    : Vector3.up * 2f;
                BurstFX.Spawn(fallback, completion.accent, 70, 3.4f, 1.3f);
            }

            if (ResultsScreen.Instance != null)
                ResultsScreen.Instance.Show(
                    ScoreSystem.Score, ScoreSystem.QuizCorrect, ScoreSystem.QuizTotal,
                    completion.elapsedSeconds,
                    completion.headerText,
                    completion.grantedLine,
                    completion.nextMissionText,
                    completion.replaySuffix,
                    completion.parScore);
        }

        private static void Ensure<T>(GameObject host) where T : MonoBehaviour
        {
            if (Object.FindObjectOfType<T>() == null) host.AddComponent<T>();
        }
    }
}
