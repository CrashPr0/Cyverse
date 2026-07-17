using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;

namespace Cyverse.UI
{
    /// <summary>
    /// A modal typed-answer card ("enter the passcode"), styled like the quiz
    /// card and following the one-menu-at-a-time standard: it owns the screen
    /// via GameState.QuizActive, refuses to open over another menu, and stamps
    /// MenuTransitionFrame on open/close so its Esc can't leak into settings.
    /// Case-insensitive comparison; Enter submits, Esc steps away (failure).
    /// </summary>
    public class TypingChallenge : MonoBehaviour
    {
        public static TypingChallenge Instance { get; private set; }

        public int maxLength = 32;

        private GameObject card;
        private Text headerText, bodyText, inputLine, feedbackText;
        private string answer, typed = "";
        private Action<bool> onDone;
        private bool open, closing;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        /// <summary>Show the card. done(true) on a correct entry, done(false)
        /// if the player steps away (Esc) — wrong entries just retry.</summary>
        public void Show(string header, string body, string expectedAnswer, Action<bool> done)
        {
            if (open || GameState.AnyMenuOpen) { done?.Invoke(false); return; }
            if (card == null) Build();

            answer = expectedAnswer;
            onDone = done;
            typed = "";
            open = true;
            closing = false;
            GameState.QuizActive = true;
            GameState.MenuTransitionFrame = Time.frameCount;

            headerText.text = header;
            bodyText.text = body;
            feedbackText.text = "<color=#8FB8CC>ENTER submit   ·   ESC step away</color>";
            card.SetActive(true);
        }

        void Update()
        {
            if (!open || closing) return;
            if (Time.frameCount == GameState.MenuTransitionFrame) return;

            if (Input.GetKeyDown(KeyCode.Escape)) { Close(false); return; }

            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
                }
                else if (c == '\n' || c == '\r')
                {
                    Submit();
                    return;
                }
                else if (!char.IsControl(c) && typed.Length < maxLength)
                {
                    typed += c;
                }
            }
            RefreshInput();
        }

        private void Submit()
        {
            bool correct = string.Equals(typed.Trim(), answer.Trim(), StringComparison.OrdinalIgnoreCase);
            if (correct)
            {
                feedbackText.text = "<color=#4CE087><b>VERIFIED</b></color>";
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                closing = true;
                StartCoroutine(CloseSoon());
            }
            else
            {
                typed = "";
                RefreshInput();
                feedbackText.text = "<color=#FF8866><b>NOT RECOGNIZED</b></color>   Copy it exactly, including symbols.";
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            }
        }

        private IEnumerator CloseSoon()
        {
            yield return new WaitForSecondsRealtime(1.0f);
            Close(true);
        }

        private void Close(bool success)
        {
            open = false;
            closing = false;
            card.SetActive(false);
            GameState.QuizActive = false;
            GameState.MenuTransitionFrame = Time.frameCount;
            var cb = onDone;
            onDone = null;
            cb?.Invoke(success);
        }

        private void RefreshInput()
        {
            bool blink = Mathf.Sin(Time.unscaledTime * 6f) > 0f;
            inputLine.text = $">  <color=#5BC8FF>{typed}{(blink ? "_" : " ")}</color>";
        }

        void LateUpdate()
        {
            if (open && !closing) RefreshInput(); // keep the cursor blinking
        }

        // ---- Construction ----------------------------------------------------

        private void Build()
        {
            var canvas = HudUI.Instance != null ? HudUI.Instance.Canvas.transform : null;
            card = new GameObject("TypingCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(canvas, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 40);
            rt.sizeDelta = new Vector2(860, 380);
            HudUI.StylePanel(card, new Color(0.02f, 0.04f, 0.07f, 0.95f), HudUI.Accent);

            headerText = MakeText(card.transform, "Header", 30, TextAnchor.UpperCenter);
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = HudUI.Accent;
            var hrt = headerText.rectTransform;
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1);
            hrt.sizeDelta = new Vector2(0, 46); hrt.anchoredPosition = new Vector2(0, -20);

            bodyText = MakeText(card.transform, "Body", 24, TextAnchor.UpperLeft);
            var brt = bodyText.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(46, 150); brt.offsetMax = new Vector2(-46, -76);

            inputLine = MakeText(card.transform, "Input", 28, TextAnchor.MiddleLeft);
            inputLine.fontStyle = FontStyle.Bold;
            var irt = inputLine.rectTransform;
            irt.anchorMin = new Vector2(0, 0); irt.anchorMax = new Vector2(1, 0); irt.pivot = new Vector2(0.5f, 0);
            irt.sizeDelta = new Vector2(-92, 46); irt.anchoredPosition = new Vector2(0, 84);

            feedbackText = MakeText(card.transform, "Feedback", 22, TextAnchor.MiddleCenter);
            var frt = feedbackText.rectTransform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0); frt.pivot = new Vector2(0.5f, 0);
            frt.sizeDelta = new Vector2(-60, 60); frt.anchoredPosition = new Vector2(0, 14);

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
