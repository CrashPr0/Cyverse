#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.Level;
using Cyverse.Settings;

namespace Cyverse.Testing
{
    /// <summary>
    /// Deterministic smoke playthrough for Level 1. It exercises the real
    /// station events and delivery delegates, while accelerating only the
    /// briefing, audit selection, and exam input that would otherwise require
    /// human keyboard timing.
    /// </summary>
    public sealed class Level1AutomatedPlaythrough : MonoBehaviour
    {
        public bool Finished { get; private set; }
        public bool Passed { get; private set; }
        public string Failure { get; private set; }

        private bool hadCompletionKey;
        private int previousCompletion;
        private bool previousReduceMotion;

        private IEnumerator Start()
        {
            hadCompletionKey = PlayerPrefs.HasKey("cv_done_1");
            previousCompletion = PlayerPrefs.GetInt("cv_done_1", 0);
            previousReduceMotion = AccessibilitySettings.ReduceMotion;
            AccessibilitySettings.ReduceMotion = true;

            Debug.Log("[PLAYTHROUGH] Starting Level 1 visual-pass end-flow test");
            yield return null;
            yield return null;

            var manager = FindObjectOfType<Level1IamManager>();
            var briefing = FindObjectOfType<VideoStation>();
            var badge = FindObjectOfType<BadgeStation>();
            var mfa = FindObjectOfType<MfaGauntlet>();
            var sorting = FindObjectOfType<SortingStation>();
            var audit = FindObjectOfType<AuditStation>();
            var exam = FindObjectOfType<CertExamStation>();

            if (manager == null || briefing == null || badge == null || mfa == null ||
                sorting == null || audit == null || exam == null)
            {
                Fail("Level 1 is missing one or more required end-flow components.");
                yield break;
            }

            briefing.CompleteForAutomation();
            if (!WaitCondition(() => manager.CurrentPhase == Level1IamManager.Phase.Tasks,
                2f, "briefing did not unlock the task phase")) yield break;
            yield return null;
            Debug.Log("[PLAYTHROUGH] Briefing complete; task room unlocked");

            badge.Interact(gameObject);
            if (!WaitCondition(() => badge.IsEnrolled, 2f, "badge enrollment did not complete")) yield break;
            yield return null;

            // Exercise the real carry/drop plumbing for the physical MFA token.
            Carryable token = null;
            DropZone tokenSlot = null;
            foreach (var item in FindObjectsOfType<Carryable>())
                if (item.id == "mfa_token") { token = item; break; }
            foreach (var zone in FindObjectsOfType<DropZone>())
                if (zone.zoneName == "TOKEN SLOT") { tokenSlot = zone; break; }
            if (token == null || tokenSlot == null)
            {
                Fail("MFA token or TOKEN SLOT is missing.");
                yield break;
            }
            token.Interact(gameObject);
            tokenSlot.Interact(gameObject);
            mfa.FactorCleared(0);
            mfa.FactorCleared(2);

            // Deliver every data crate through the role predicate and normal
            // DropZone callback, catching lost delegate wiring in saved scenes.
            var zones = FindObjectsOfType<DropZone>();
            var items = FindObjectsOfType<Carryable>();
            foreach (var item in items)
            {
                if (item == null || item.id == "mfa_token") continue;
                DropZone destination = null;
                foreach (var zone in zones)
                {
                    if (zone == null || zone.accepts == null || !zone.accepts(item)) continue;
                    destination = zone;
                    break;
                }
                if (destination == null)
                {
                    Fail("No wired destination accepts data crate '" + item.id + "'.");
                    yield break;
                }
                item.Interact(gameObject);
                destination.Interact(gameObject);
                yield return null;
            }
            if (!sorting.IsComplete)
            {
                Fail("Data Triage did not complete after all valid deliveries.");
                yield break;
            }

            while (!audit.IsComplete)
            {
                int before = audit.Solved;
                audit.SolveCurrentRoundForAutomation();
                float deadline = Time.realtimeSinceStartup + 2.5f;
                while (!audit.IsComplete && audit.Solved == before && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (!audit.IsComplete && audit.Solved == before)
                {
                    Fail("Audit Hunt stalled on round " + (before + 1) + ".");
                    yield break;
                }
                if (!audit.IsComplete) yield return new WaitForSecondsRealtime(1.3f);
            }

            float taskDeadline = Time.realtimeSinceStartup + 3f;
            while (manager.CurrentPhase != Level1IamManager.Phase.Exam &&
                   Time.realtimeSinceStartup < taskDeadline)
                yield return null;
            if (manager.CurrentPhase != Level1IamManager.Phase.Exam)
            {
                Fail("All tasks completed, but the Certification Exam did not unlock.");
                yield break;
            }
            Debug.Log("[PLAYTHROUGH] Badge, MFA, Data Triage, and Audit tasks complete");

            exam.CompleteForAutomation();
            yield return null;
            if (manager.CurrentPhase != Level1IamManager.Phase.Complete ||
                !GameState.LevelComplete || !LevelProgress.IsCompleted(1))
            {
                Fail("Exam completion did not persist and enter the Complete phase.");
                yield break;
            }

            Passed = true;
            Finished = true;
            Debug.Log("[PLAYTHROUGH] PASS — Level 1 reached results and persisted completion");
            RestorePreferences();
        }

        // Used at synchronous checkpoints. Timed transitions are awaited in
        // the coroutine itself so the editor remains responsive.
        private bool WaitCondition(System.Func<bool> condition, float timeout, string failure)
        {
            if (condition()) return true;
            Fail(failure + " (expected an immediate event transition).");
            return false;
        }

        private void Fail(string message)
        {
            Failure = message;
            Passed = false;
            Finished = true;
            Debug.LogError("[PLAYTHROUGH] FAIL — " + message);
            RestorePreferences();
        }

        private void RestorePreferences()
        {
            AccessibilitySettings.ReduceMotion = previousReduceMotion;
            if (hadCompletionKey) PlayerPrefs.SetInt("cv_done_1", previousCompletion);
            else PlayerPrefs.DeleteKey("cv_done_1");
            PlayerPrefs.Save();
        }
    }
}
#endif
