using System;
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
    /// multipliers apply, and educators keep a measurable assessment. The
    /// level completes when the exam finishes, regardless of score (the score
    /// itself is the differentiator).
    /// </summary>
    public class CertExamStation : MonoBehaviour, IInteractable
    {
        public event Action Completed;
        public bool IsComplete { get; private set; }

        private QuizQuestion[] questions;
        private bool active, running;
        private int qIndex;
        private Renderer screenRenderer;
        private TextMesh statusText;

        public bool CanInteract => !IsComplete;
        public string Prompt => active ? "Take the Certification Exam" : "Certification Exam  (locked)";

        /// <summary>Called by the level manager when all tasks are complete.</summary>
        public void Activate()
        {
            if (active) return;
            active = true;
            if (screenRenderer != null)
                screenRenderer.sharedMaterial = BuildKit.MakeHologram(new Color(0.30f, 1f, 0.45f));
            if (statusText != null) statusText.text = "EXAM READY\n[E]";
            BurstFX.Spawn(transform.position + Vector3.up * 2f, new Color(0.30f, 1f, 0.45f), 26);
        }

        public void Interact(GameObject interactor)
        {
            if (IsComplete || running) return;
            if (!active)
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Complete all four training tasks first.", new Color(1f, 0.55f, 0.4f));
                return;
            }

            running = true;
            qIndex = 0;
            AskNext();
        }

        private void AskNext()
        {
            if (questions == null || qIndex >= questions.Length || Quiz.QuizSystem.Instance == null)
            {
                Finish();
                return;
            }
            QuizSystem.Instance.Ask(questions[qIndex++], _ => AskNext());
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
                new Vector3(0f, 1.55f, 0.12f), new Vector3(-15f, 0f, 0f), new Vector3(1.3f, 0.85f, 0.06f), bodyMat, collider: true);
            var screen = BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", root.transform,
                new Vector3(0f, 1.55f, 0.08f), new Vector3(-15f, 0f, 0f), new Vector3(1.18f, 0.74f, 1f),
                BuildKit.MakeHologram(new Color(0.35f, 0.40f, 0.48f)), collider: false);

            var station = root.AddComponent<CertExamStation>();
            station.questions = questions;
            station.screenRenderer = screen.GetComponent<Renderer>();
            station.statusText = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.55f, 0.02f),
                "LOCKED", new Color(0.95f, 0.98f, 1f), 0.026f);
            station.statusText.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);

            BuildKit.MakeSign(root.transform, pos + new Vector3(0f, 2.7f, 0f), "CERTIFICATION", accent, 0.032f);

            return station;
        }
    }
}
