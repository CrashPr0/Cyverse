using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.Settings;

namespace Cyverse.UI
{
    /// <summary>
    /// A modal typed-answer card ("enter the passcode"), styled like the quiz
    /// card and following the one-menu-at-a-time standard: it owns the screen
    /// via ModalSession, refuses to open over another menu, and suppresses the
    /// transition frame so its Esc cannot leak into settings.
    /// Case-insensitive comparison; Enter submits, Esc steps away (failure).
    /// The phone OTP variant animates a fake authenticator phone into view and
    /// types its four-digit code into the terminal automatically.
    /// </summary>
    public class TypingChallenge : MonoBehaviour, IGameplayActionTarget
    {
        public static TypingChallenge Instance { get; private set; }

        public int maxLength = 32;

        private GameObject card;
        private Text headerText, bodyText, inputLine, feedbackText;
        private Text phoneHeaderText, phoneCodeText, phoneStatusText;
        private RectTransform bodyRect, phoneRect;
        private GameObject phonePanel;
        private Cyverse.Interaction.DiegeticPhone diegetic;
        private string answer, typed = "";
        private Action<bool> onDone;
        private bool open, closing;
        private bool autoTyping;
        private Coroutine autoTypeRoutine;
        private ModalSession.Lease modal;

        public bool IsOpen => open;
        public bool IsAutoTyping => autoTyping;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        /// <summary>Show the card. done(true) on a correct entry, done(false)
        /// if the player steps away (Esc) — wrong entries just retry.</summary>
        public void Show(string header, string body, string expectedAnswer, Action<bool> done)
        {
            ShowInternal(header, body, expectedAnswer, done, phone: false);
        }

        /// <summary>Show a phone-delivered OTP. The fake phone displays the
        /// generated code and enters it into the terminal on the player's
        /// behalf, so the interaction reads as "something you have" becoming
        /// a short-lived code they know.</summary>
        public void ShowPhoneOtp(string header, string body, string expectedAnswer,
            Action<bool> done)
        {
            ShowInternal(header, body, expectedAnswer, done, phone: true);
        }

        private void ShowInternal(string header, string body, string expectedAnswer,
            Action<bool> done, bool phone)
        {
            if (open) { done?.Invoke(false); return; }
            if (card == null) Build();
            if (!ModalSession.TryOpen(this, ModalSession.Channel.Quiz, out modal))
            {
                done?.Invoke(false);
                return;
            }

            answer = expectedAnswer;
            onDone = done;
            typed = "";
            open = true;
            closing = false;
            autoTyping = false;

            headerText.text = header;
            bodyText.text = body;
            feedbackText.text = "<color=#8FB8CC>ENTER submit   ·   ESC step away</color>";
            bodyRect.offsetMax = phone
                ? new Vector2(-280f, -76f)
                : new Vector2(-46f, -76f);
            card.SetActive(true);
            if (phone)
            {
                diegetic = Cyverse.Interaction.DiegeticPhone.Active;
                feedbackText.text = "<color=#8FB8CC>PHONE OTP  ·  ENTERING AUTOMATICALLY   ·   ESC cancel</color>";
                if (diegetic != null)
                {
                    // In-world phone: drive the RenderTexture screen; the HUD
                    // corner panel stays hidden.
                    phonePanel.SetActive(false);
                    diegetic.ShowCode(expectedAnswer);
                    diegetic.SetStatus("<color=#8FB8CC>DAILY OTP  ·  SPARTAN</color>");
                }
                else
                {
                    // Fallback (levels without a diegetic phone): old HUD panel.
                    phonePanel.SetActive(true);
                    phoneCodeText.text = expectedAnswer;
                    phoneStatusText.text = "<color=#8FB8CC>DAILY OTP  ·  SPARTAN</color>";
                    phoneRect.anchoredPosition = new Vector2(-38f, -330f);
                }
                autoTypeRoutine = StartCoroutine(AutoTypePhoneCode());
            }
            else
            {
                phonePanel.SetActive(false);
            }
        }

        void Update()
        {
            if (!open || closing) return;
            if (Time.frameCount == GameState.MenuTransitionFrame) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                GameplayActions.TryApply(this, GameplayAction.Cancel(), gameObject);
                return;
            }

            if (autoTyping) return;

            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    GameplayActions.TryApply(this, GameplayAction.Backspace(), gameObject);
                }
                else if (c == '\n' || c == '\r')
                {
                    GameplayActions.TryApply(this, GameplayAction.Submit(), gameObject);
                    return;
                }
                else if (!char.IsControl(c))
                {
                    GameplayActions.TryApply(this, GameplayAction.Append(c.ToString()), gameObject);
                }
            }
            RefreshInput();
        }

        public bool TryApply(GameplayAction action, GameObject actor)
        {
            if (!open || closing) return false;
            if (autoTyping && action.Kind != GameplayActionKind.Cancel) return false;

            switch (action.Kind)
            {
                case GameplayActionKind.AppendText:
                case GameplayActionKind.PasteText:
                    Append(action.Text);
                    return true;
                case GameplayActionKind.Backspace:
                    if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
                    RefreshInput();
                    return true;
                case GameplayActionKind.ClearText:
                    typed = string.Empty;
                    RefreshInput();
                    return true;
                case GameplayActionKind.Submit:
                    Submit();
                    return true;
                case GameplayActionKind.Cancel:
                    Close(false);
                    return true;
                default:
                    return false;
            }
        }

        private void Append(string value)
        {
            if (string.IsNullOrEmpty(value) || typed.Length >= maxLength) return;
            foreach (char c in value)
            {
                if (char.IsControl(c) || typed.Length >= maxLength) continue;
                typed += c;
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
            if (autoTypeRoutine != null)
            {
                StopCoroutine(autoTypeRoutine);
                autoTypeRoutine = null;
            }
            autoTyping = false;
            open = false;
            closing = false;
            card.SetActive(false);
            phonePanel.SetActive(false);
            if (diegetic != null) { diegetic.Hide(); diegetic = null; }
            bodyRect.offsetMax = new Vector2(-46f, -76f);
            modal?.Close();
            modal = null;
            var cb = onDone;
            onDone = null;
            cb?.Invoke(success);
        }

        private void OnDestroy()
        {
            modal?.Close();
        }

        private void RefreshInput()
        {
            bool blink = Mathf.Sin(Time.unscaledTime * 6f) > 0f;
            inputLine.text = $">  <color=#5BC8FF>{typed}{(blink ? "_" : " ")}</color>";
        }

        private IEnumerator AutoTypePhoneCode()
        {
            autoTyping = true;
            bool useDiegetic = diegetic != null;

            if (useDiegetic)
            {
                // In-world phone: no corner slide-in; hold for the same beat so
                // the timing matches the old animation.
                float appear = AccessibilitySettings.ReduceMotion ? 0.05f : 0.45f;
                yield return new WaitForSecondsRealtime(appear);
            }
            else
            {
                Vector2 hidden = phoneRect.anchoredPosition;
                Vector2 shown = new Vector2(-38f, -64f);
                float duration = AccessibilitySettings.ReduceMotion ? 0.05f : 0.45f;
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    float progress = Mathf.Clamp01(elapsed / duration);
                    phoneRect.anchoredPosition = Vector2.Lerp(hidden, shown,
                        Mathf.SmoothStep(0f, 1f, progress));
                    yield return null;
                }
                phoneRect.anchoredPosition = shown;
            }

            float pause = AccessibilitySettings.ReduceMotion ? 0.08f : 0.35f;
            yield return new WaitForSecondsRealtime(pause);

            typed = string.Empty;
            RefreshInput();
            if (useDiegetic) diegetic.SetStatus("<color=#4CE087>ENTERING CODE…</color>");
            else phoneStatusText.text = "<color=#4CE087>ENTERING CODE…</color>";
            float perDigit = AccessibilitySettings.ReduceMotion ? 0.04f : 0.18f;
            foreach (char digit in answer.Trim())
            {
                typed += digit;
                RefreshInput();
                yield return new WaitForSecondsRealtime(perDigit);
            }

            if (useDiegetic) diegetic.SetStatus("<color=#4CE087>CODE ACCEPTED BY DEVICE</color>");
            else phoneStatusText.text = "<color=#4CE087>CODE ACCEPTED BY DEVICE</color>";
            autoTyping = false;
            autoTypeRoutine = null;
            Submit();
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
            rt.sizeDelta = new Vector2(920, 400);
            HudUI.StylePanel(card, new Color(0.02f, 0.04f, 0.07f, 0.95f), HudUI.Accent);

            headerText = MakeText(card.transform, "Header", 30, TextAnchor.UpperCenter);
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = HudUI.Accent;
            var hrt = headerText.rectTransform;
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1);
            hrt.sizeDelta = new Vector2(0, 46); hrt.anchoredPosition = new Vector2(0, -20);

            bodyText = MakeText(card.transform, "Body", 24, TextAnchor.UpperLeft);
            bodyRect = bodyText.rectTransform;
            bodyRect.anchorMin = new Vector2(0, 0); bodyRect.anchorMax = new Vector2(1, 1);
            bodyRect.offsetMin = new Vector2(46, 150); bodyRect.offsetMax = new Vector2(-46, -76);

            inputLine = MakeText(card.transform, "Input", 28, TextAnchor.MiddleLeft);
            inputLine.fontStyle = FontStyle.Bold;
            var irt = inputLine.rectTransform;
            irt.anchorMin = new Vector2(0, 0); irt.anchorMax = new Vector2(1, 0); irt.pivot = new Vector2(0.5f, 0);
            irt.sizeDelta = new Vector2(-92, 46); irt.anchoredPosition = new Vector2(0, 84);

            feedbackText = MakeText(card.transform, "Feedback", 22, TextAnchor.MiddleCenter);
            var frt = feedbackText.rectTransform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0); frt.pivot = new Vector2(0.5f, 0);
            frt.sizeDelta = new Vector2(-60, 60); frt.anchoredPosition = new Vector2(0, 14);

            BuildPhonePanel();

            card.SetActive(false);
        }

        private void BuildPhonePanel()
        {
            phonePanel = new GameObject("SpartanAuthenticatorPhone", typeof(RectTransform), typeof(Image));
            phonePanel.transform.SetParent(card.transform, false);
            phoneRect = phonePanel.GetComponent<RectTransform>();
            phoneRect.anchorMin = new Vector2(1f, 1f);
            phoneRect.anchorMax = new Vector2(1f, 1f);
            phoneRect.pivot = new Vector2(1f, 1f);
            phoneRect.sizeDelta = new Vector2(218f, 286f);
            phoneRect.anchoredPosition = new Vector2(-38f, -64f);
            HudUI.StylePanel(phonePanel, new Color(0.015f, 0.025f, 0.045f, 1f),
                new Color(0.25f, 0.80f, 1f));

            phoneHeaderText = MakeText(phonePanel.transform, "PhoneHeader", 17, TextAnchor.MiddleCenter);
            phoneHeaderText.fontStyle = FontStyle.Bold;
            phoneHeaderText.color = new Color(0.70f, 0.86f, 0.96f);
            var headerRect = phoneHeaderText.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f); headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(-24f, 32f); headerRect.anchoredPosition = new Vector2(0f, -18f);
            phoneHeaderText.text = "SPARTAN AUTHENTICATOR";

            var screen = new GameObject("PhoneScreen", typeof(RectTransform), typeof(Image));
            screen.transform.SetParent(phonePanel.transform, false);
            var screenRect = screen.GetComponent<RectTransform>();
            screenRect.anchorMin = new Vector2(0.5f, 0.5f); screenRect.anchorMax = new Vector2(0.5f, 0.5f);
            screenRect.pivot = new Vector2(0.5f, 0.5f);
            screenRect.sizeDelta = new Vector2(174f, 170f); screenRect.anchoredPosition = new Vector2(0f, -18f);
            screen.GetComponent<Image>().color = new Color(0.02f, 0.10f, 0.14f, 1f);

            var otpLabel = MakeText(screen.transform, "OtpLabel", 15, TextAnchor.MiddleCenter);
            otpLabel.color = new Color(0.45f, 0.78f, 0.90f);
            var otpLabelRect = otpLabel.rectTransform;
            otpLabelRect.anchorMin = new Vector2(0f, 1f); otpLabelRect.anchorMax = new Vector2(1f, 1f);
            otpLabelRect.pivot = new Vector2(0.5f, 1f);
            otpLabelRect.sizeDelta = new Vector2(-12f, 28f); otpLabelRect.anchoredPosition = new Vector2(0f, -12f);
            otpLabel.text = "DAILY VERIFICATION CODE";

            phoneCodeText = MakeText(screen.transform, "OtpCode", 42, TextAnchor.MiddleCenter);
            phoneCodeText.fontStyle = FontStyle.Bold;
            phoneCodeText.color = new Color(0.30f, 1f, 0.45f);
            var codeRect = phoneCodeText.rectTransform;
            codeRect.anchorMin = new Vector2(0f, 0.5f); codeRect.anchorMax = new Vector2(1f, 0.5f);
            codeRect.pivot = new Vector2(0.5f, 0.5f);
            codeRect.sizeDelta = new Vector2(-10f, 58f); codeRect.anchoredPosition = Vector2.zero;
            phoneCodeText.text = "----";

            phoneStatusText = MakeText(phonePanel.transform, "PhoneStatus", 14, TextAnchor.MiddleCenter);
            phoneStatusText.color = new Color(0.55f, 0.70f, 0.80f);
            var statusRect = phoneStatusText.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f); statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(-18f, 42f); statusRect.anchoredPosition = new Vector2(0f, 8f);

            phonePanel.SetActive(false);
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
