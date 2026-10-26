#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Cyverse.Forensics;
using Cyverse.Interaction;
using Cyverse.Level;
using Cyverse.Quiz;
using Cyverse.UI;

namespace Cyverse.Testing
{
    /// <summary>
    /// Deterministic input adapter used by the watchable TAS and PlayMode
    /// tests. It may know authored route data, but every state change is sent
    /// through GameplayActions just like live keyboard, mouse, and world
    /// interaction. Gameplay components therefore need no automation-only
    /// completion APIs.
    /// </summary>
    public static class DeterministicGameplayAdapter
    {
        // Entry 2 of the custody form: item #1, released by the SOC analyst,
        // received by Digital Forensics, for analysis (ChainOfCustodyForm.ConfigureFields).
        private static readonly int[] CustodyRoute = { 0, 1, 2, 0 };

        /// <summary>The correct option for a custody-form blank, for tests that
        /// fill the form one blank at a time.</summary>
        public static int CustodyAnswer(int field) => CustodyRoute[field];

        public static void ResetSocProgress()
        {
            PlayerPrefs.DeleteKey(SocProgress.CompromisedComputerKey);
            PlayerPrefs.DeleteKey(SocProgress.ChainOfCustodyKey);
            PlayerPrefs.DeleteKey(SocProgress.PlaybookKey);
            PlayerPrefs.DeleteKey(SocProgress.EvidenceJsonKey);
            PlayerPrefs.DeleteKey(SocProgress.EvidenceLockedKey);
            PlayerPrefs.Save();
        }

        /// <summary>SOC handoff, step two: pick up the seized device (the same Carryable
        /// pickup a player does) and lock it in the evidence locker.</summary>
        public static bool SecureSeizedDevice(EvidenceLocker locker, GameObject actor = null)
        {
            if (locker == null || locker.side != EvidenceLocker.Side.Soc) return false;
            if (locker.IsSealed) return true;

            Carryable device = null;
            foreach (Carryable candidate in Object.FindObjectsOfType<Carryable>())
                if (candidate.id == EvidenceLocker.SeizedDeviceId) { device = candidate; break; }
            if (device == null) return false;

            if (Carryable.Carried != device &&
                !GameplayActions.TryApply(device, GameplayAction.Interact(), actor))
                return false;
            if (Carryable.Carried != device) return false;
            return GameplayActions.TryApply(locker, GameplayAction.Interact(), actor) && locker.IsSealed;
        }

        /// <summary>Forensics Lab, first step: take the device out of the evidence
        /// locker. Evidence Intake refuses to open until this has been done.</summary>
        public static bool RetrieveEvidenceDevice(EvidenceLocker locker, GameObject actor = null)
        {
            if (locker == null || locker.side != EvidenceLocker.Side.Forensics) return false;
            if (locker.DeviceRetrieved) return true;
            return GameplayActions.TryApply(locker, GameplayAction.Interact(), actor) &&
                   locker.DeviceRetrieved;
        }

        public static bool SubmitConfiguredPassword(PasswordLockController controller,
            GameObject actor = null)
        {
            if (controller == null) return false;
            return GameplayActions.TryApply(controller, GameplayAction.ClearText(), actor) &&
                   GameplayActions.TryApply(controller, GameplayAction.Append(controller.password), actor) &&
                   GameplayActions.TryApply(controller, GameplayAction.Submit(), actor);
        }

        public static bool FinishBriefing(VideoStation station, GameObject actor = null)
        {
            return station != null && GameplayActions.TryApply(station,
                GameplayAction.Scrub(float.MaxValue), actor);
        }

        /// <summary>Talk to the MFA Specialist (which unlocks the vault's
        /// factors) and cut her recorded briefing short: an automated run has
        /// no one to listen to it. True when the vault is no longer waiting on her.</summary>
        public static bool TalkToMfaSpecialist(MfaSpecialist specialist, GameObject actor = null)
        {
            if (specialist == null) return true;
            if (!specialist.Briefed) GameplayActions.TryApply(specialist, GameplayAction.Interact(), actor);
            if (Cyverse.Dialogue.DialogueManager.Instance != null) Cyverse.Dialogue.DialogueManager.Instance.Stop();
            return specialist.Briefed;
        }

        public static IEnumerator CompleteMfaFactor(MfaFactor factor,
            GameObject actor = null)
        {
            if (factor == null || factor.gauntlet == null) yield break;
            MfaGauntlet gauntlet = factor.gauntlet;
            int before = gauntlet.ClearedCount;
            if (!GameplayActions.TryApply(factor, GameplayAction.Interact(), actor)) yield break;
            if (factor.kind == MfaFactor.Kind.Knowledge)
            {
                yield return null;
                TypingChallenge challenge = TypingChallenge.Instance;
                if (challenge == null || !challenge.IsOpen)
                    yield break;

                // Type the code from the phone, as a player does.
                if (challenge.IsOpen &&
                    (!GameplayActions.TryApply(challenge, GameplayAction.ClearText(), actor) ||
                     !GameplayActions.TryApply(challenge, GameplayAction.Append(factor.passcode), actor) ||
                     !GameplayActions.TryApply(challenge, GameplayAction.Submit(), actor)))
                    yield break;
            }

            float deadline = Time.realtimeSinceStartup + 3f;
            while (gauntlet.ClearedCount == before && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        public static bool SolveAuditRound(AuditStation station, int correctRow,
            GameObject actor = null)
        {
            if (station == null || station.IsComplete) return station != null;
            if (!station.IsAwaitingSelection &&
                !GameplayActions.TryApply(station, GameplayAction.Interact(), actor))
                return false;

            int guard = 0;
            while (station.SelectedRowIndex != correctRow && guard++ < 32)
                if (!GameplayActions.TryApply(station, GameplayAction.Navigate(1), actor))
                    return false;

            int before = station.Solved;
            return GameplayActions.TryApply(station, GameplayAction.Interact(), actor) &&
                (station.IsComplete || station.Solved > before);
        }

        public static IEnumerator CompleteExam(CertExamStation station, GameObject actor = null,
            float feedbackSeconds = 0.05f)
        {
            if (station == null) yield break;
            QuizSystem quiz = QuizSystem.Instance;
            if (quiz == null) yield break;

            float previousFeedback = quiz.feedbackSeconds;
            bool previousSkip = quiz.skipReadDelay;
            quiz.feedbackSeconds = Mathf.Max(0f, feedbackSeconds);
            quiz.skipReadDelay = true;
            if (!station.IsComplete)
                GameplayActions.TryApply(station, GameplayAction.Interact(), actor);

            int guard = 0;
            while (!station.IsComplete && guard++ < 32)
            {
                float deadline = Time.realtimeSinceStartup + 4f;
                while (!station.IsComplete && !quiz.IsAwaitingInput && !quiz.IsShowingMessage &&
                       Time.realtimeSinceStartup < deadline)
                    yield return null;

                if (station.IsComplete) break;
                if (quiz.IsShowingMessage)
                {
                    GameplayActions.TryApply(quiz, GameplayAction.Choose(0), actor);
                    yield return null;
                    continue;
                }
                if (!quiz.IsAwaitingInput || quiz.CurrentQuestion == null) break;
                if (!quiz.AnswerCorrectly(actor)) break;
                yield return null;
            }

            quiz.feedbackSeconds = previousFeedback;
            quiz.skipReadDelay = previousSkip;
        }

        public static bool CompleteCustodyForm(ChainOfCustodyForm form, GameObject actor = null)
        {
            if (form == null) return false;
            if (!EvidenceLocker.DeviceReadyForIntake &&
                !RetrieveEvidenceDevice(EvidenceLocker.Instance, actor))
                return false;
            if (!form.IsOpen)
            {
                ChainOfCustodyStation station = ChainOfCustodyStation.Instance;
                if (station == null ||
                    !GameplayActions.TryApply(station, GameplayAction.Interact(), actor))
                    return false;
            }

            // The LEFT custody rework removed the separate evidence-download
            // step (no 2D acquisition phone): the form is ready to fill as
            // soon as it is open, so there is no download gate to wait on.
            if (form.FieldCount != CustodyRoute.Length) return false;
            for (int field = 0; field < CustodyRoute.Length; field++)
                if (!GameplayActions.TryApply(form,
                    GameplayAction.Select(field, CustodyRoute[field]), actor)) return false;

            if (!GameplayActions.TryApply(form, GameplayAction.Submit(), actor) || !form.IsComplete)
                return false;
            GameplayActions.TryApply(form, GameplayAction.Cancel(), actor);
            return true;
        }

        public static IEnumerator CompleteCustodyFormRoutine(ChainOfCustodyForm form,
            GameObject actor = null)
        {
            if (form == null) yield break;
            // Intake needs the device in hand; a route that has not walked to the
            // locker yet takes it out first rather than skipping the step.
            if (!EvidenceLocker.DeviceReadyForIntake &&
                !RetrieveEvidenceDevice(EvidenceLocker.Instance, actor))
                yield break;
            if (!form.IsOpen)
            {
                ChainOfCustodyStation station = ChainOfCustodyStation.Instance;
                if (station == null ||
                    !GameplayActions.TryApply(station, GameplayAction.Interact(), actor))
                    yield break;
            }

            // No evidence-download step in the LEFT rework: once the station
            // interaction has opened the form it is immediately fillable, so
            // yield a frame for the open to settle and proceed.
            if (!form.IsOpen) yield break;
            yield return null;
            CompleteCustodyForm(form, actor);
        }

        public static IEnumerator CompleteForensics(ForensicsConsole console,
            GameObject actor = null)
        {
            if (console == null) yield break;

            // The first E at the desk docks the evidence device and starts the
            // upload; the case loop below then opens the Evidence Explorer.
            PlugInStation upload = PlugInStation.Instance;
            if (upload != null && !upload.UploadStarted)
            {
                if (!GameplayActions.TryApply(console, GameplayAction.Interact(), actor)) yield break;
                // Let the docking/imaging beat play, as a player would see it.
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!upload.UploadComplete && Time.realtimeSinceStartup < deadline)
                    yield return null;
            }

            int caseGuard = 0;
            while (!console.AllComplete && caseGuard++ < 8)
            {
                InvestigationCase investigation = console.ActiveCase;
                if (investigation == null ||
                    !GameplayActions.TryApply(console, GameplayAction.Interact(), actor))
                    yield break;
                yield return null;

                if (!TagCaseEvidence(console, investigation, actor)) yield break;
                yield return null;
            }
        }

        /// <summary>Work an Evidence Explorer case the way a player does: process
        /// the phone if it hasn't been yet, open
        /// the proving artifact's category, clear the filter, select the row,
        /// tag it. Then step away.</summary>
        private static bool TagCaseEvidence(ForensicsConsole console,
            InvestigationCase investigation, GameObject actor)
        {
            EvidenceExplorer explorer = EvidenceExplorer.Instance;
            if (explorer == null || !explorer.IsOpen || console.Phone == null) return false;

            // First time at the desk the phone has to be processed: the right
            // pick on each setup step, then start, skip and open Examine.
            int processGuard = 0;
            while (explorer.InProcess && processGuard++ < 16)
            {
                int pick = explorer.ProcessCorrectChoice;
                GameplayAction step = pick >= 0 ? GameplayAction.Choose(pick) : GameplayAction.Submit();
                if (!GameplayActions.TryApply(explorer, step, actor)) return false;
            }
            if (explorer.InProcess) return false;

            int questionGuard = 0;
            while (!investigation.IsComplete && questionGuard++ < 64)
            {
                CaseQuestion question = investigation.Current;
                PhoneArtifact artifact = question != null ? console.Phone.Find(question.artifactId) : null;
                if (artifact == null) return false;
                int categoryIndex = System.Array.IndexOf(PhoneImage.Categories, artifact.category);
                if (!GameplayActions.TryApply(explorer, GameplayAction.Choose(categoryIndex), actor) ||
                    !GameplayActions.TryApply(explorer, GameplayAction.ClearText(), actor))
                    return false;
                int row = -1;
                for (int i = 0; i < explorer.VisibleArtifacts.Count; i++)
                    if (explorer.VisibleArtifacts[i] == artifact) row = i;
                if (row < 0 ||
                    !GameplayActions.TryApply(explorer, GameplayAction.Select(row, 0), actor) ||
                    !GameplayActions.TryApply(explorer, GameplayAction.Submit(), actor))
                    return false;
            }
            return GameplayActions.TryApply(explorer, GameplayAction.Cancel(), actor);
        }

        public static bool CompleteSocInvestigation(SiemConsole console, GameObject actor = null)
        {
            if (console == null) return false;
            int guard = 0;
            while (!console.IsComplete && guard++ < 16)
            {
                Level2Content.SocScenario scenario = console.ActiveScenario;
                bool finalScenario = console.ScenarioIndex == console.ScenarioCount - 1;
                if (scenario == null ||
                    !GameplayActions.TryApply(console, GameplayAction.Interact(), actor))
                    return false;

                int rowGuard = 0;
                while (console.SelectedRowIndex != scenario.triggerRowIndex && rowGuard++ < 16)
                    if (!GameplayActions.TryApply(console, GameplayAction.Navigate(1), actor))
                        return false;
                if (!GameplayActions.TryApply(console, GameplayAction.Submit(), actor))
                    return false;

                EndpointStation endpoint = null;
                string computer = scenario.rows[scenario.triggerRowIndex].computer;
                foreach (EndpointStation candidate in Object.FindObjectsOfType<EndpointStation>())
                    if (candidate.def != null && candidate.def.hostname == computer)
                    {
                        endpoint = candidate;
                        break;
                    }
                if (endpoint == null ||
                    !GameplayActions.TryApply(endpoint, GameplayAction.Interact(), actor))
                    return false;

                if (!GameplayActions.TryApply(console,
                    GameplayAction.Choose((int)scenario.correctVerdict), actor))
                    return false;
                if (finalScenario && !console.IsComplete &&
                    !GameplayActions.TryApply(console, GameplayAction.Submit(), actor))
                    return false;
            }
            return console.IsComplete;
        }

        public static bool CompleteCyberAttack(Level4CyberAttackManager manager,
            GameObject actor = null)
        {
            if (manager == null) return false;
            if (!manager.ScenarioStarted)
            {
                VideoStation briefing = Object.FindObjectOfType<VideoStation>();
                if (!FinishBriefing(briefing, actor)) return false;
            }

            CyberAttackStation[] stations = Object.FindObjectsOfType<CyberAttackStation>();
            int guard = 0;
            while (manager.CurrentPhase != Level4CyberAttackManager.Phase.Complete && guard++ < 8)
            {
                CyberAttackStation current = null;
                foreach (CyberAttackStation station in stations)
                    if (station != null && station.StationIndex == manager.CurrentStationIndex)
                    {
                        current = station;
                        break;
                    }
                Level4CyberAttackContent.StationScenario scenario = manager.ScenarioFor(current);
                if (current == null || scenario == null ||
                    !GameplayActions.TryApply(current, GameplayAction.Interact(), actor) ||
                    !GameplayActions.TryApply(current,
                        GameplayAction.Choose(scenario.correctOption), actor))
                    return false;
            }
            return manager.CurrentPhase == Level4CyberAttackManager.Phase.Complete;
        }
    }
}
#endif
