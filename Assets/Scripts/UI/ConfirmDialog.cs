using System;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;

namespace Cyverse.UI
{
    /// <summary>
    /// A small "are you sure?" card: a title, a line of explanation and two
    /// choices. Keyboard: E / Enter / Y confirms, Esc / Tab / N cancels; the
    /// buttons take clicks too. Follows the one-menu-at-a-time standard.
    /// </summary>
    public sealed class ConfirmDialog : MonoBehaviour
    {
        public static ConfirmDialog Instance { get; private set; }

        public bool IsOpen => card != null && card.activeSelf;

        private GameObject card;
        private Text titleText, bodyText, confirmLabel, cancelLabel;
        private Action onConfirm, onCancel;
        private ModalSession.Lease modal;

        /// <summary>Ask; <paramref name="confirm"/> runs on yes, <paramref name="cancel"/> on no.
        /// Returns false (and runs nothing) if another menu owns the screen.</summary>
        public static bool Show(string title, string body, string confirmText, string cancelText,
            Action confirm, Action cancel = null)
        {
            if (HudUI.Instance == null) { confirm?.Invoke(); return true; }
            if (Instance == null) Instance = HudUI.Instance.gameObject.AddComponent<ConfirmDialog>();
            return Instance.Open(title, body, confirmText, cancelText, confirm, cancel);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            modal?.Close();
            if (Instance == this) Instance = null;
        }

        private bool Open(string title, string body, string confirmText, string cancelText,
            Action confirm, Action cancel)
        {
            if (IsOpen) return false;
            if (!ModalSession.TryOpen(this, ModalSession.Channel.Quiz, out modal, releaseCursor: true))
                return false;
            if (card == null) Build();
            ModalSession.EnsureEventSystem();
            onConfirm = confirm;
            onCancel = cancel;
            titleText.text = title;
            bodyText.text = body;
            confirmLabel.text = $"{confirmText}   <size=18><color=#BFE9FF>[E]</color></size>";
            cancelLabel.text = $"{cancelText}   <size=18><color=#BFE9FF>[{GameInput.CancelLabel}]</color></size>";
            card.transform.SetAsLastSibling();
            card.SetActive(true);
            UiMotion.PanelIn(card, card.transform.Find("Card"), 0.96f);
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            return true;
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == GameState.MenuTransitionFrame) return;
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Y))
                Close(true);
            else if (GameInput.CancelDown || Input.GetKeyDown(KeyCode.N))
                Close(false);
        }

        private void Close(bool confirmed)
        {
            card.SetActive(false);
            modal?.Close();
            modal = null;
            Action next = confirmed ? onConfirm : onCancel;
            onConfirm = onCancel = null;
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            next?.Invoke();
        }

        private void Build()
        {
            // Full-screen dim behind the card.
            card = new GameObject("ConfirmDialog", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            card.GetComponent<Image>().color = new Color(0f, 0.01f, 0.02f, 0.7f);

            var panel = new GameObject("Card", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(card.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(720f, 300f);
            HudUI.StylePanel(panel, new Color(0.02f, 0.04f, 0.07f, 0.97f), HudUI.Accent);

            titleText = MakeText(panel.transform, "Title", 32, TextAnchor.UpperCenter);
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = HudUI.Accent;
            Place(titleText.rectTransform, new Vector2(0f, -28f), new Vector2(-60f, 44f));

            bodyText = MakeText(panel.transform, "Body", 22, TextAnchor.UpperCenter);
            bodyText.color = new Color(0.82f, 0.90f, 0.95f);
            Place(bodyText.rectTransform, new Vector2(0f, -84f), new Vector2(-80f, 90f));

            confirmLabel = MakeButton(panel.transform, "Confirm", new Vector2(-170f, 44f),
                new Color(0.12f, 0.42f, 0.25f), () => Close(true));
            cancelLabel = MakeButton(panel.transform, "Cancel", new Vector2(170f, 44f),
                new Color(0.13f, 0.17f, 0.20f), () => Close(false));
            card.SetActive(false);
        }

        private static void Place(RectTransform rt, Vector2 top, Vector2 size)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = top;
            rt.sizeDelta = size;
        }

        private static Text MakeButton(Transform parent, string name, Vector2 bottomCentre, Color background, Action click)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(bottomCentre.x, 36f);
            rt.sizeDelta = new Vector2(300f, 58f);
            go.GetComponent<Image>().color = background;
            go.GetComponent<Button>().onClick.AddListener(() => click());
            UiMotion.AddPress(go.GetComponent<Button>());
            Text label = MakeText(go.transform, "Label", 22, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return label;
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
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
