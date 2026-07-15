using UnityEngine;
using Cyverse.Core;

namespace Cyverse.Level
{
    /// <summary>
    /// The Hub's live mission-status board: one row per level, refreshed from
    /// LevelProgress on load, plus a header line that greets the player by
    /// callsign and shows overall clearance. Geometry is built by
    /// HubSceneFactory.BuildMissionBoard(), which wires these references; this
    /// component only owns the runtime text (so an editor-saved Hub scene
    /// shows fresh state for whoever is playing, not whoever saved it).
    /// </summary>
    public class MissionBoard : MonoBehaviour
    {
        public TextMesh headerText;
        public TextMesh[] statusTexts;
        public int[] levels;          // parallel to statusTexts; 0 = Orientation
        public bool[] inDevelopment;  // parallel to statusTexts

        private static readonly Color Gold = new Color(0.90f, 0.66f, 0.14f);
        private static readonly Color Ready = new Color(0.30f, 1f, 0.45f);
        private static readonly Color Locked = new Color(0.95f, 0.40f, 0.30f);
        private static readonly Color Dev = new Color(0.55f, 0.60f, 0.68f);

        void Start()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (headerText != null)
                headerText.text = $"OPERATIVE: {PlayerIdentity.Callsign}    CLEARANCE: {LevelProgress.CompletedStoryLevels()}/4";

            if (statusTexts == null) return;
            for (int i = 0; i < statusTexts.Length; i++)
            {
                var tm = statusTexts[i];
                if (tm == null) continue;

                bool dev = inDevelopment != null && i < inDevelopment.Length && inDevelopment[i];
                int level = levels != null && i < levels.Length ? levels[i] : 0;

                if (dev)                                   { tm.text = "IN DEVELOPMENT"; tm.color = Dev; }
                else if (LevelProgress.IsCompleted(level)) { tm.text = "COMPLETE ✓";     tm.color = Gold; }
                else if (LevelProgress.IsUnlocked(level))  { tm.text = "READY";          tm.color = Ready; }
                else                                       { tm.text = "LOCKED";         tm.color = Locked; }
            }
        }
    }
}
