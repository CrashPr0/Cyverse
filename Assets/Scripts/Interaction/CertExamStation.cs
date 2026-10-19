using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.Quiz;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The Certification Exam: the level's boss check. Locked until the
    /// manager activates it (all training tasks done), then one interaction
    /// chains the question bank through the QuizSystem — streaks and combo
    /// multipliers apply, and educators keep a measurable assessment. Options
    /// are shuffled on every attempt and the player must reach the pass mark;
    /// missed questions are reviewed and retried until it is met.
    /// </summary>
    public class CertExamStation : MonoBehaviour, IInteractable
    {
        public event Action Completed;
        public bool IsComplete { get; private set; }

        private QuizQuestion[] questions;
        private bool active, running;
        private int qIndex;
        private List<QuizQuestion> round;
        private List<QuizQuestion> missed;
        private HashSet<QuizQuestion> passed;

        private const float PassFraction = 0.8f;
        private const float ReadDelaySeconds = 1f;
        private const int MaxReviewed = 4;
        private Renderer screenRenderer;
        private TextMesh statusText;

        public bool CanInteract => !IsComplete;
        public string Prompt => active ? "Take the Certification Exam" : "Certification Exam  (locked)";

        void Awake()
        {
            NormalizeScreen();
            BuildKit.EnsureAimCollider(gameObject, height: 3.0f, width: 2.6f);
        }
        void OnValidate() => NormalizeScreen();

        private void NormalizeScreen()
        {
            Transform body = BuildKit.AlignKioskScreen(transform, "ScreenBody", "Screen");
            Transform sign = transform.Find("Sign_CERTIFICATION");
            if (sign != null)
            {
                Billboard billboard = sign.GetComponent<Billboard>();
                if (billboard != null) billboard.enabled = false;
                SignFX motion = sign.GetComponent<SignFX>();
                if (motion != null) motion.enabled = false;
                Vector3 signPosition = sign.localPosition;
                sign.localPosition = new Vector3(signPosition.x, 3.8f, signPosition.z);
                sign.localRotation = Quaternion.identity;
            }
            if (statusText == null)
            {
                foreach (TextMesh label in GetComponentsInChildren<TextMesh>(true))
                    if (!string.IsNullOrEmpty(label.text) &&
                        (label.text == "LOCKED" || label.text.Contains("EXAM READY") || label.text.Contains("CERTIFIED")))
                    { statusText = label; break; }
            }
            BuildKit.PlaceOnKioskScreen(statusText != null ? statusText.transform : null,
                body, Vector2.zero, 0.024f);
        }

        /// <summary>Installs the runtime-only state: the question bank and the
        /// screen renderer, neither of which survives being saved into a scene
        /// file. Called by Build and again by the level factory on load.</summary>
        public void Configure(QuizQuestion[] bank)
        {
            questions = bank;
            if (screenRenderer == null)
            {
                Transform screen = transform.Find("Screen");
                if (screen != null) screenRenderer = screen.GetComponent<Renderer>();
            }
            NormalizeScreen();
        }

        /// <summary>Called by the level manager when all tasks are complete.</summary>
        public void Activate()
        {
            if (active) return;
            active = true;
            if (screenRenderer != null)
                screenRenderer.sharedMaterial = BuildKit.MakeHologram(new Color(0.30f, 1f, 0.45f));
            if (statusText != null) statusText.text = "EXAM READY\n[E]";
            BurstFX.SpawnAbove(transform, new Color(0.30f, 1f, 0.45f), 26, minimumHeight: 2f);
        }

        public void Interact(GameObject interactor)
        {
            if (IsComplete || running) return;
            if (!active)
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Finish the SOC investigations, evidence locker and IR playbook first.", new Color(1f, 0.55f, 0.4f));
                return;
            }

            running = true;
            passed = new HashSet<QuizQuestion>();
            BeginRound(questions == null ? new List<QuizQuestion>() : new List<QuizQuestion>(questions));
        }

        private int PassMark => Mathf.CeilToInt((questions != null ? questions.Length : 0) * PassFraction);

        private void BeginRound(List<QuizQuestion> toAsk)
        {
            round = toAsk;
            missed = new List<QuizQuestion>();
            qIndex = 0;
            AskNext();
        }

        private void AskNext()
        {
            if (round == null || qIndex >= round.Count || Quiz.QuizSystem.Instance == null)
            {
                EndRound();
                return;
            }
            // Ask a shuffled copy, but tally against the authored question.
            QuizQuestion original = round[qIndex++];
            QuizSystem.Instance.Ask(original.Shuffled(), correct =>
            {
                if (correct) passed.Add(original); else missed.Add(original);
                AskNext();
            }, ReadDelaySeconds);
        }

        private void EndRound()
        {
            if (Quiz.QuizSystem.Instance == null || passed.Count >= PassMark || missed.Count == 0)
            {
                Finish();
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Score: {passed.Count}/{questions.Length}   Pass mark: {PassMark}");
            sb.AppendLine();
            sb.AppendLine("<color=#FFB347>Missed:</color>");
            for (int i = 0; i < missed.Count && i < MaxReviewed; i++)
            {
                sb.AppendLine($"- {missed[i].question}");
                if (!string.IsNullOrEmpty(missed[i].explanation))
                    sb.AppendLine($"  <color=#8FB8CC>{missed[i].explanation}</color>");
            }
            if (missed.Count > MaxReviewed) sb.AppendLine($"...and {missed.Count - MaxReviewed} more.");
            sb.AppendLine();
            sb.AppendLine("Retry the missed questions to reach the pass mark.");

            var retry = missed;
            QuizSystem.Instance.ShowMessage("EXAM NOT PASSED", sb.ToString(), () => BeginRound(retry));
        }

        private void Finish()
        {
            if (IsComplete) return;
            IsComplete = true;
            running = false;
            if (statusText != null) statusText.text = "CERTIFIED ✓";
            if (screenRenderer != null)
                screenRenderer.sharedMaterial = BuildKit.MakeHologram(new Color(0.90f, 0.66f, 0.14f));
            Completed?.Invoke();
        }

        // ---- Construction ----------------------------------------------------

        public static CertExamStation Build(Vector3 pos, float rotY, QuizQuestion[] questions, Color accent)
        {
            var root = new GameObject("CertExamStation");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Console", root.transform,
                new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(1.5f, 1.1f, 0.7f), bodyMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "ScreenBody", root.transform,
                new Vector3(0f, 1.55f, 0.12f), new Vector3(35f, 0f, 0f), new Vector3(1.3f, 0.85f, 0.06f), bodyMat, collider: true);
            var screen = BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", root.transform,
                new Vector3(0f, 1.526f, 0.086f), new Vector3(35f, 0f, 0f), new Vector3(1.18f, 0.74f, 1f),
                BuildKit.MakeHologram(new Color(0.35f, 0.40f, 0.48f)), collider: false);

            var station = root.AddComponent<CertExamStation>();
            station.screenRenderer = screen.GetComponent<Renderer>();
            station.statusText = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.55f, 0.02f),
                "LOCKED", new Color(0.95f, 0.98f, 1f), 0.026f);
            station.Configure(questions);

            BuildKit.MakeSign(root.transform, pos + new Vector3(0f, 3.8f, 0f), "CERTIFICATION", accent, 0.032f);

            var glow = new GameObject("ExamLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.2f, -1.2f);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = accent;
            l.range = 7f;
            l.intensity = 1.6f;

            return station;
        }
    }
}
