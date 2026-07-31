using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;

namespace Cyverse.UI
{
    /// <summary>
    /// Security+ Prep Terminal: an optional study aid drawing on Dr. Rocca's
    /// CompTIA Security+ practice bank (Level/SecurityPlusContent.cs), organized
    /// by NICE Workforce Framework category. Two screens:
    ///   Roles    — pick a category (1-5); shows question count + personal best
    ///   Question — a definition-matching MCQ, answered with 1-4; feedback,
    ///              then the next question from a shuffled, no-repeat bag
    /// Esc steps back one screen (Question -> Roles -> closed), following the
    /// one-menu-at-a-time standard via GameState.QuizActive/MenuTransitionFrame.
    ///
    /// Deliberately separate from ScoreSystem: this is Security+ practice, not
    /// part of level completion, so answers here never touch ScoreSystem.Score.
    /// Only a per-role personal-best accuracy is persisted (PlayerPrefs), purely
    /// as light gamification for a study tool.
    /// </summary>
    public class SecurityPlusTerminal : MonoBehaviour
    {
        public static SecurityPlusTerminal Instance { get; private set; }

        private enum Screen { Roles, Question }

        private GameObject card;
        private Text titleText, bodyText, feedbackText;
        private Screen screen;
        private bool open;

        private NiceRole currentRole;
        private SecurityPlusQuestion[] bag;
        private int[] order;
        private int cursor;
        private bool awaitingInput;

        private int sessionAttempted, sessionCorrect;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public void Open()
        {
            if (open || GameState.AnyMenuOpen) return;
            if (card == null) Build();

            open = true;
            GameState.QuizActive = true;
            GameState.MenuTransitionFrame = Time.frameCount;
            sessionAttempted = 0;
            sessionCorrect = 0;
            ShowRoles();
            card.SetActive(true);
        }

        private void Close()
        {
            open = false;
            card.SetActive(false);
            GameState.QuizActive = false;
            GameState.MenuTransitionFrame = Time.frameCount;
        }

        void Update()
        {
            if (!open) return;
            if (Time.frameCount == GameState.MenuTransitionFrame) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (screen == Screen.Question)
                {
                    GameState.MenuTransitionFrame = Time.frameCount;
                    ShowRoles();
                }
                else Close();
                return;
            }

            if (screen == Screen.Roles) HandleRolesInput();
            else if (screen == Screen.Question && awaitingInput) HandleQuestionInput();
        }

        // ---- Role select -------------------------------------------------------

        private void ShowRoles()
        {
            screen = Screen.Roles;
            titleText.text = "SECURITY+ PREP — CHOOSE A CATEGORY";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<color=#8FB8CC>NICE Workforce Framework categories — practice questions drawn from a shared CompTIA Security+ bank.</color>\n");
            for (int i = 0; i < SecurityPlusContent.AllRoles.Length; i++)
            {
                var role = SecurityPlusContent.AllRoles[i];
                int count = SecurityPlusContent.For(role).Length;
                float best = PlayerPrefs.GetFloat(BestKey(role), -1f);
                string bestText = best >= 0f ? $"  <color=#E5A823>best {best:0}%</color>" : "";
                sb.AppendLine($"<color=#5BC8FF><b>[{i + 1}]</b></color>  {SecurityPlusContent.DisplayName(role)}" +
                              $"  <color=#607585>({count} questions)</color>{bestText}");
            }
            bodyText.text = sb.ToString();
            feedbackText.text = sessionAttempted > 0
                ? $"<color=#8FB8CC>This session: {sessionCorrect}/{sessionAttempted} correct</color>"
                : "<color=#607585>Esc to close</color>";
        }

        private void HandleRolesInput()
        {
            int choice = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) choice = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) choice = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) choice = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) choice = 3;
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) choice = 4;

            if (choice < 0 || choice >= SecurityPlusContent.AllRoles.Length) return;
            SelectRole(SecurityPlusContent.AllRoles[choice]);
        }

        private void SelectRole(NiceRole role)
        {
            currentRole = role;
            bag = SecurityPlusContent.For(role);
            ShuffleNewOrder();
            cursor = 0;
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            ShowQuestion();
        }

        private void ShuffleNewOrder()
        {
            order = new int[bag.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }
        }

        // ---- Question -----------------------------------------------------------

        private void ShowQuestion()
        {
            screen = Screen.Question;
            if (bag == null || bag.Length == 0)
            {
                bodyText.text = "<color=#FF8866>No questions in this category.</color>";
                feedbackText.text = "<color=#607585>Esc for categories</color>";
                awaitingInput = false;
                return;
            }
            if (cursor >= order.Length) { ShuffleNewOrder(); cursor = 0; }

            var q = bag[order[cursor]];
            titleText.text = $"{SecurityPlusContent.DisplayName(currentRole).ToUpperInvariant()}" +
                              $"   ·   this session {sessionCorrect}/{sessionAttempted}";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Wrap(q.prompt, 74));
            sb.AppendLine();
            for (int i = 0; i < q.options.Length; i++)
                sb.AppendLine($"<color=#5BC8FF><b>[{i + 1}]</b></color>  {q.options[i]}");
            bodyText.text = sb.ToString();
            feedbackText.text = "<color=#8FB8CC>Press 1-4 to answer   ·   Esc for categories</color>";
            awaitingInput = true;
        }

        private void HandleQuestionInput()
        {
            int choice = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) choice = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) choice = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) choice = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) choice = 3;
            if (choice >= 0) Answer(choice);
        }

        private void Answer(int choice)
        {
            awaitingInput = false;
            var q = bag[order[cursor]];
            bool correct = choice == q.correctIndex;

            sessionAttempted++;
            if (correct) sessionCorrect++;
            SaveBest(currentRole, sessionCorrect, sessionAttempted);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Wrap(q.prompt, 74));
            sb.AppendLine();
            for (int i = 0; i < q.options.Length; i++)
            {
                string marker = i == q.correctIndex ? "<color=#4CE087>> </color>"
                    : i == choice ? "<color=#FFB347>> </color>" : "   ";
                sb.AppendLine($"{marker}<color=#5BC8FF><b>[{i + 1}]</b></color>  {q.options[i]}");
            }
            bodyText.text = sb.ToString();

            feedbackText.text = correct
                ? $"<color=#4CE087><b>Correct.</b></color>  {q.concept}"
                : $"<color=#FFB347><b>Not quite.</b></color>  Correct answer: {q.concept}";
            if (Sfx.Instance != null) { if (correct) Sfx.Instance.PlayConfirm(); else Sfx.Instance.PlayDeny(); }

            cursor++;
            StartCoroutine(NextAfterDelay());
        }

        private IEnumerator NextAfterDelay()
        {
            yield return new WaitForSecondsRealtime(1.8f);
            if (open && screen == Screen.Question) ShowQuestion();
        }

        private static string BestKey(NiceRole role) => "cv_secplus_best_" + role;

        private static void SaveBest(NiceRole role, int correct, int attempted)
        {
            if (attempted <= 0) return;
            float pct = 100f * correct / attempted;
            float prior = PlayerPrefs.GetFloat(BestKey(role), -1f);
            if (pct > prior)
            {
                PlayerPrefs.SetFloat(BestKey(role), pct);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Legacy UI Text has no word-wrap; insert line breaks manually.</summary>
        private static string Wrap(string text, int maxLine)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder();
            int lineLen = 0;
            foreach (string word in text.Split(' '))
            {
                if (lineLen > 0 && lineLen + word.Length + 1 > maxLine) { sb.Append('\n'); lineLen = 0; }
                else if (lineLen > 0) { sb.Append(' '); lineLen++; }
                sb.Append(word);
                lineLen += word.Length;
            }
            return sb.ToString();
        }

        // ---- Construction ----------------------------------------------------

        private void Build()
        {
            var canvas = HudUI.Instance != null ? HudUI.Instance.Canvas.transform : null;
            card = new GameObject("SecurityPlusCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(canvas, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 20);
            rt.sizeDelta = new Vector2(1020, 640);
            HudUI.StylePanel(card, new Color(0.02f, 0.04f, 0.07f, 0.96f), new Color(0.62f, 0.45f, 0.95f));

            titleText = MakeText(card.transform, "Title", 28, TextAnchor.UpperCenter);
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(0.72f, 0.58f, 1f);
            var trt = titleText.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(-70, 44); trt.anchoredPosition = new Vector2(0, -20);

            bodyText = MakeText(card.transform, "Body", 23, TextAnchor.UpperLeft);
            var brt = bodyText.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(46, 78); brt.offsetMax = new Vector2(-46, -76);

            feedbackText = MakeText(card.transform, "Feedback", 21, TextAnchor.MiddleCenter);
            var frt = feedbackText.rectTransform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0); frt.pivot = new Vector2(0.5f, 0);
            frt.sizeDelta = new Vector2(-60, 64); frt.anchoredPosition = new Vector2(0, 14);

            card.SetActive(false);
        }

        private static Text MakeText(Transform parent, string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = HudUI.UIFont;
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
