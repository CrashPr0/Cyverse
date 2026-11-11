using UnityEngine;
using Cyverse.Core;
using Cyverse.Interaction;
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
            HardMode.End(); // before the score reset, so the Hub never scores Hard
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
            HardWing.Build(); // before HubPolish, which dresses its doors too
            HubPolish.Apply(gameObject);

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();

            RenameOrientationDoor();

            // First arrival of a playthrough: title + callsign, then a short
            // learn-by-doing start. It hands back to ShowDoorGuidance.
            if (HubOnboarding.ShouldRun())
                gameObject.AddComponent<HubOnboarding>().Begin(ShowDoorGuidance);
            else
                ShowDoorGuidance();
        }

        /// <summary>Orientation is optional, not a demo. The Hub is a saved
        /// scene, so the door's name is corrected here at load.</summary>
        private static void RenameOrientationDoor()
        {
            const string Old = "Orientation (Demo)", New = "Orientation (optional)";
            foreach (HubDoor door in FindObjectsOfType<HubDoor>())
            {
                if (door.displayName != Old) continue;
                door.displayName = New;
                foreach (TextMesh label in door.GetComponentsInChildren<TextMesh>(true))
                    if (label.text == Old) label.text = New;
                    else if (label.text == Old.ToUpperInvariant()) label.text = New.ToUpperInvariant();
                foreach (TMPro.TMP_Text label in door.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (label.text == Old) label.text = New;
                    else if (label.text == Old.ToUpperInvariant()) label.text = New.ToUpperInvariant();
            }
        }

        /// <summary>Names and marks the door that moves the story on.</summary>
        public void ShowDoorGuidance()
        {
            int done = LevelProgress.CompletedStoryLevels();
            HubDoor next = NextStoryDoor();
            if (HudUI.Instance != null)
            {
                // Point at the one door that moves the story on. "Choose a
                // level" is only true once every level is open.
                if (next != null)
                    HudUI.Instance.ShowObjective(
                        (done == 0 ? "Start with " : "Next: ") + next.displayName + "  ·  follow the marker");
                else
                    HudUI.Instance.ShowObjective(done >= 4
                        ? "All 4 levels complete  ·  replay any level"
                        : $"Choose a level  ·  {done}/4 complete");
                HudUI.Instance.SetProgress(done, 4);
            }
            if (next != null)
                ObjectiveBeacon.Ensure().PointAt(next.transform,
                    "LEVEL " + next.levelIndex, new Color(0.90f, 0.66f, 0.14f));
        }

        /// <summary>The door of the first story level that is open but not yet
        /// finished, or null when there is none (all done, or the next one is
        /// still locked).</summary>
        private static HubDoor NextStoryDoor()
        {
            for (int level = 1; level <= 4; level++)
            {
                if (LevelProgress.IsCompleted(level)) continue;
                if (!LevelProgress.IsUnlocked(level)) return null;
                foreach (HubDoor door in FindObjectsOfType<HubDoor>())
                    if (door.IsStoryDoorFor(level)) return door;
                return null;
            }
            return null;
        }
    }
}
