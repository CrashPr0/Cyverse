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
        private static readonly int[] CustodyRoute = { 1, 1, 1, 1 };

        public static void ResetSocProgress()
        {
            PlayerPrefs.DeleteKey(SocProgress.CompromisedComputerKey);
            PlayerPrefs.DeleteKey(SocProgress.ChainOfCustodyKey);
            PlayerPrefs.DeleteKey(SocProgress.PlaybookKey);
            PlayerPrefs.DeleteKey(SocProgress.EvidenceJsonKey);
            PlayerPrefs.Save();
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

                // The Level 1 phone OTP enters itself. Wait for that normal
                // player-facing flow before checking the factor result. Keep
                // the manual path for older scenes that still show a plain
                // typing card.
                float entryDeadline = Time.realtimeSinceStartup + 4f;
                while (challenge.IsOpen && challenge.IsAutoTyping &&
                       Time.realtimeSinceStartup < entryDeadline)
                    yield return null;

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
            quiz.feedbackSeconds = Mathf.Max(0f, feedbackSeconds);
            if (!station.IsComplete)
                GameplayActions.TryApply(station, GameplayAction.Interact(), actor);

            int guard = 0;
            while (!station.IsComplete && guard++ < 32)
            {
                float deadline = Time.realtimeSinceStartup + 4f;
                while (!station.IsComplete && !quiz.IsAwaitingInput &&
                       Time.realtimeSinceStartup < deadline)
                    yield return null;

                if (station.IsComplete) break;
                QuizQuestion question = quiz.CurrentQuestion;
                if (!quiz.IsAwaitingInput || question == null) break;
                if (!GameplayActions.TryApply(quiz,
                    GameplayAction.Choose(question.correctIndex), actor)) break;
                yield return null;
            }

            quiz.feedbackSeconds = previousFeedback;
        }

        public static bool CompleteCustodyForm(ChainOfCustodyForm form, GameObject actor = null)
        {
            if (form == null) return false;
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

            int caseGuard = 0;
            while (!console.AllComplete && caseGuard++ < 8)
            {
                InvestigationCase investigation = console.ActiveCase;
                if (investigation == null ||
                    !GameplayActions.TryApply(console, GameplayAction.Interact(), actor))
                    yield break;
                yield return null;

                QueryTerminal terminal = QueryTerminal.Instance;
                if (terminal == null || !terminal.IsOpen) yield break;

                // Exercise the same first-use guide a player sees before the
                // adapter submits the first finding. This keeps the watchable
                // TAS and acceptance route honest as the tutorial evolves.
                if (terminal.TutorialActive)
                {
                    if (!SendTerminalCommand(terminal, "tables", actor) ||
                        !SendTerminalCommand(terminal,
                            "EvidenceManifest | project computer", actor))
                        yield break;
                    yield return null;
                }

                int questionGuard = 0;
                while (!investigation.IsComplete && questionGuard++ < 64)
                {
                    CaseQuestion question = investigation.Current;
                    if (question == null || question.answers == null || question.answers.Length == 0)
                        yield break;
                    if (!SendTerminalCommand(terminal,
                        "answer " + question.answers[0], actor))
                        yield break;
                    yield return null;
                }

                GameplayActions.TryApply(terminal, GameplayAction.Cancel(), actor);
                yield return null;
            }
        }

        private static bool SendTerminalCommand(QueryTerminal terminal,
            string command, GameObject actor)
        {
            return GameplayActions.TryApply(terminal, GameplayAction.ClearText(), actor) &&
                GameplayActions.TryApply(terminal, GameplayAction.Append(command), actor) &&
                GameplayActions.TryApply(terminal, GameplayAction.Submit(), actor);
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
