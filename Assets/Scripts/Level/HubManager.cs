using UnityEngine;
using Cyverse.Core;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Runs the Main Hub: resets global state, self-heals the shared systems,
    /// and shows a progress objective. No title screen (the Password Lock
    /// scene is the game's front door) and no completion — the hub is a menu
    /// you walk around in.
    /// </summary>
    public class HubManager : MonoBehaviour
    {
        public static HubManager Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            GameState.Reset();
            ScoreSystem.Reset();
            Time.timeScale = 1f;
            Shader.SetGlobalFloat("_CyMotion", 1f);
        }

        void Start()
        {
            if (Quiz.QuizSystem.Instance == null) gameObject.AddComponent<Quiz.QuizSystem>();
            if (ResultsScreen.Instance == null) gameObject.AddComponent<ResultsScreen>();
            if (VisualDirector.Instance == null) gameObject.AddComponent<VisualDirector>();

            var cam = Camera.main;
            if (cam != null && cam.GetComponent<FirstPersonHands>() == null)
                cam.gameObject.AddComponent<FirstPersonHands>();
            if (Audio.AmbientHum.Instance == null) gameObject.AddComponent<Audio.AmbientHum>();
            if (GlossaryPanel.Instance == null) gameObject.AddComponent<GlossaryPanel>();

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();

            int done = LevelProgress.CompletedStoryLevels();
            if (HudUI.Instance != null)
            {
                HudUI.Instance.ShowObjective($"Choose a level  ·  {done}/4 complete");
                HudUI.Instance.SetProgress(done, 4);
            }
        }
    }
}
