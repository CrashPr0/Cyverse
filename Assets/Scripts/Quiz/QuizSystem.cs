using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;

namespace Cyverse.Quiz
{
    /// <summary>One multiple-choice knowledge-check question.</summary>
    [Serializable]
    public class QuizQuestion
    {
        public string question;
        public string[] options;    // exactly 3
        public int correctIndex;    // 0..2
        public string explanation;  // shown when the answer is wrong

        public QuizQuestion(string question, string[] options, int correctIndex, string explanation)
        {
            this.question = question;
            this.options = options;
            this.correctIndex = correctIndex;
            this.explanation = explanation;
        }
    }

    /// <summary>
    /// Gamified assessment: shows a styled multiple-choice card, answered with
    /// the 1/2/3 keys. Awards points (more for a correct first answer), gives
    /// immediate feedback, then reports back via callback. Keyboard-only, so it
    /// needs no EventSystem and stays accessible.
    /// </summary>
    public class QuizSystem : MonoBehaviour
    {
        public static QuizSystem Instance { get; private set; }

        public int correctPoints = 100;
        public int wrongPoints = 25;
        public float feedbackSeconds = 2.2f;

        private GameObject card;
        private Text bodyText;
        private Text feedbackText;

        private QuizQuestion current;
        private Action<bool> onAnswered;
        private bool awaitingInput;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        /// <summary>Show a question; the callback receives whether it was correct.</summary>
        public void Ask(QuizQuestion q, Action<bool> answered)
        {
            if (q == null) { answered?.Invoke(true); return; }
            if (card == null) Build();

            current = q;
            onAnswered = answered;
            GameState.QuizActive = true;

            bodyText.text = BodyFor(q, chosen: -1);
            feedbackText.text = "<color=#8FB8CC>Press 1, 2 or 3 to answer</color>";
            card.SetActive(true);
            awaitingInput = true;
        }

        void Update()
        {
            if (!awaitingInput || GameState.MenuOpen) return;

            int choice = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) choice = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) choice = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) choice = 2;

            if (choice >= 0) Answer(choice);
        }

        private void Answer(int choice)
        {
            awaitingInput = false;
            bool correct = choice == current.correctIndex;

            ScoreSystem.QuizTotal++;
            if (correct)
            {
                ScoreSystem.QuizCorrect++;
                ScoreSystem.Add(correctPoints);
                feedbackText.text = $"<color=#4CE087><b>Correct!</b>  +{correctPoints} points</color>";
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            }
            else
            {
                ScoreSystem.Add(wrongPoints);
                feedbackText.text =
                    $"<color=#FFB347><b>Not quite.</b>  {current.explanation}  +{wrongPoints} points</color>";
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            }

            bodyText.text = BodyFor(current, choice);
            StartCoroutine(CloseAfterFeedback(correct));
        }

        private IEnumerator CloseAfterFeedback(bool correct)
        {
            yield return new WaitForSecondsRealtime(feedbackSeconds);
            card.SetActive(false);
            GameState.QuizActive = false;
            var cb = onAnswered;
            onAnswered = null;
            cb?.Invoke(correct);
        }

        private string BodyFor(QuizQuestion q, int chosen)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(q.question);
            sb.AppendLine();
            for (int i = 0; i < q.options.Length; i++)
            {
                string marker = "   ";
                if (chosen >= 0)
                {
                    if (i == q.correctIndex) marker = "<color=#4CE087>> </color>";
                    else if (i == chosen) marker = "<color=#FFB347>> </color>";
                }
                sb.AppendLine($"{marker}<color=#5BC8FF><b>[{i + 1}]</b></color>  {q.options[i]}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private void Build()
        {
            var canvas = UI.HudUI.Instance != null ? UI.HudUI.Instance.Canvas.transform : null;
            card = new GameObject("QuizCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(canvas, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 40);
            rt.sizeDelta = new Vector2(920, 520);
            UI.HudUI.StylePanel(card, new Color(0.02f, 0.04f, 0.07f, 0.95f), UI.HudUI.Accent);

            var header = MakeText(card.transform, "Header", 32, TextAnchor.UpperCenter);
            header.fontStyle = FontStyle.Bold;
            header.color = UI.HudUI.Accent;
            header.text = "KNOWLEDGE CHECK";
            var hrt = header.rectTransform;
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1);
            hrt.sizeDelta = new Vector2(0, 50); hrt.anchoredPosition = new Vector2(0, -22);

            bodyText = MakeText(card.transform, "Body", 26, TextAnchor.UpperLeft);
            var brt = bodyText.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(50, 90); brt.offsetMax = new Vector2(-50, -84);

            feedbackText = MakeText(card.transform, "Feedback", 24, TextAnchor.MiddleCenter);
            var frt = feedbackText.rectTransform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0); frt.pivot = new Vector2(0.5f, 0);
            frt.sizeDelta = new Vector2(-60, 70); frt.anchoredPosition = new Vector2(0, 12);

            card.SetActive(false);
        }

        private static Text MakeText(Transform parent, string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = UI.HudUI.UIFont;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
