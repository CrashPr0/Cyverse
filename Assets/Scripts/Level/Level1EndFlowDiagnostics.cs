using UnityEngine;
using UnityEngine.SceneManagement;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Development-only flight recorder for the Level 1 completion path.
    /// It logs only when meaningful state changes and warns when serialized
    /// visual-pass objects have lost their runtime wiring. Release WebGL builds
    /// never install this component.
    /// </summary>
    public sealed class Level1EndFlowDiagnostics : MonoBehaviour
    {
        private Level1IamManager manager;
        private BadgeStation badge;
        private MfaGauntlet mfa;
        private SortingStation sorting;
        private AuditStation audit;
        private CertExamStation exam;
        private LockedDoor taskDoor;
        private string lastSnapshot;
        private float nextSample;

        public static void Install(GameObject host)
        {
            if (host == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            if (host.GetComponent<Level1EndFlowDiagnostics>() == null)
                host.AddComponent<Level1EndFlowDiagnostics>();
        }

        void Start()
        {
            manager = FindObjectOfType<Level1IamManager>();
            badge = FindObjectOfType<BadgeStation>();
            mfa = FindObjectOfType<MfaGauntlet>();
            sorting = FindObjectOfType<SortingStation>();
            audit = FindObjectOfType<AuditStation>();
            exam = FindObjectOfType<CertExamStation>();
            taskDoor = FindObjectOfType<LockedDoor>();

            Debug.Log($"[END FLOW] Monitoring {SceneManager.GetActiveScene().name}");
            ValidateWiring();
            Sample(force: true);
        }

        void Update()
        {
            if (Time.unscaledTime < nextSample) return;
            nextSample = Time.unscaledTime + 0.25f;
            Sample(force: false);
        }

        private void ValidateWiring()
        {
            if (manager == null) Debug.LogError("[END FLOW] Level1IamManager is missing.");
            if (sorting == null) Debug.LogError("[END FLOW] Data Triage station is missing.");
            else if (sorting.Total <= 0) Debug.LogError("[END FLOW] Data Triage has no crate definitions.");
            if (exam == null) Debug.LogError("[END FLOW] Certification Exam is missing.");

            foreach (var zone in FindObjectsOfType<DropZone>())
            {
                bool relevant = zone.zoneName == "TOKEN SLOT" || zone.zoneName == "INTERN" ||
                    zone.zoneName == "HR MANAGER" || zone.zoneName == "SYSADMIN";
                if (relevant && (zone.accepts == null || zone.onAccepted == null))
                    Debug.LogError($"[END FLOW] Drop zone '{zone.zoneName}' is not wired.", zone);
            }
        }

        private void Sample(bool force)
        {
            if (manager == null) return;
            string snapshot =
                $"phase={manager.CurrentPhase} " +
                $"badge={(badge == null ? "missing" : badge.IsEnrolled.ToString())} " +
                $"mfa={(mfa == null ? "missing" : $"{mfa.ClearedCount}/3")} " +
                $"triage={(sorting == null ? "missing" : $"{sorting.Delivered}/{sorting.Total}")} " +
                $"audit={(audit == null ? "missing" : $"{audit.Solved}/{audit.Rounds}")} " +
                $"exam={(exam == null ? "missing" : exam.IsComplete.ToString())} " +
                $"taskDoor={(taskDoor == null ? "missing" : taskDoor.IsLocked ? "locked" : "open")} " +
                $"levelComplete={GameState.LevelComplete} persisted={LevelProgress.IsCompleted(1)}";

            if (!force && snapshot == lastSnapshot) return;
            lastSnapshot = snapshot;
            Debug.Log("[END FLOW] " + snapshot);

            if (manager.CurrentPhase != Level1IamManager.Phase.Watch && taskDoor != null && taskDoor.IsLocked)
                Debug.LogWarning("[END FLOW] Briefing phase ended but the task-room door is still locked.", taskDoor);
            if (manager.CurrentPhase == Level1IamManager.Phase.Complete)
            {
                if (!GameState.LevelComplete || !LevelProgress.IsCompleted(1))
                    Debug.LogError("[END FLOW] Complete phase did not persist all completion flags.");
                if (ResultsScreen.Instance == null)
                    Debug.LogError("[END FLOW] Complete phase has no ResultsScreen instance.");
            }
        }
    }
}
