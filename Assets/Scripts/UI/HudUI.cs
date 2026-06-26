using UnityEngine;
using UnityEngine.UI;
using Cyverse.Settings;

namespace Cyverse.UI
{
    /// <summary>
    /// Builds the heads-up display entirely in code (no scene references to
    /// break): captions, an animated crosshair, a popping "E" interaction
    /// badge, and an objective banner. Other systems call the public methods;
    /// the crosshair/badge animate smoothly in Update.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        public static HudUI Instance { get; private set; }
        public static Font UIFont { get; private set; }

        public Canvas Canvas { get; private set; }

        public static readonly Color Accent = new Color(0.35f, 0.85f, 1f, 1f);

        private const int BaseCaptionSize = 28;
        private const int BaseObjectiveSize = 24;

        // Captions / objective
        private GameObject captionPanel;
        private Text captionText;
        private Text objectiveText;

        // Crosshair
        private RectTransform crosshairRect;
        private Image crosshairImage;
        private float crosshairScale = 1f;

        // Interaction prompt
        private CanvasGroup interactGroup;
        private RectTransform interactRect;
        private Text badgeLetter;
        private Text interactLabel;
        private bool interactActive;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            UIFont = LoadFont();
            BuildCanvas();
            BuildObjective();
            BuildCaption();
            BuildInteractPrompt();
            BuildCrosshair();
        }

        void Update()
        {
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime); // frame-rate independent ease

            // Crosshair grows + tints toward the accent colour when targeting.
            float targetScale = interactActive ? 2.4f : 1f;
            crosshairScale = Mathf.Lerp(crosshairScale, targetScale, k);
            crosshairRect.localScale = Vector3.one * crosshairScale;

            Color targetColor = interactActive ? Accent : new Color(1f, 1f, 1f, 0.75f);
            crosshairImage.color = Color.Lerp(crosshairImage.color, targetColor, k);

            // Interaction badge fades + pops in, with a gentle pulse while active.
            float targetAlpha = interactActive ? 1f : 0f;
            interactGroup.alpha = Mathf.MoveTowards(interactGroup.alpha, targetAlpha, Time.deltaTime * 9f);

            bool visible = interactGroup.alpha > 0.001f;
            if (interactGroup.gameObject.activeSelf != visible)
                interactGroup.gameObject.SetActive(visible);

            if (visible)
            {
                float pop = Mathf.Lerp(0.8f, 1f, interactGroup.alpha);
                float pulse = interactActive ? 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
                interactRect.localScale = Vector3.one * pop * pulse;
            }
        }

        public static Font LoadFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        // ---- Public API -----------------------------------------------------

        /// <summary>Show/hide the interaction affordance (crosshair grow + E badge).</summary>
        public void SetInteract(bool active, string action = null, string key = null)
        {
            interactActive = active;
            if (active)
            {
                if (!string.IsNullOrEmpty(action)) interactLabel.text = action;
                if (!string.IsNullOrEmpty(key)) badgeLetter.text = key;
            }
        }

        public void ShowCaption(string text)
        {
            float scale = AccessibilitySettings.Instance != null
                ? AccessibilitySettings.Instance.CaptionScale
                : 1f;
            captionText.fontSize = Mathf.RoundToInt(BaseCaptionSize * scale);
            captionText.text = text;
            captionPanel.SetActive(true);
        }

        public void HideCaption()
        {
            captionText.text = string.Empty;
            captionPanel.SetActive(false);
        }

        public void ShowObjective(string text)
        {
            objectiveText.text = text;
        }

        // ---- Construction ---------------------------------------------------

        private void BuildCanvas()
        {
            var go = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            Canvas = go.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void BuildObjective()
        {
            objectiveText = CreateText("Objective", Canvas.transform, BaseObjectiveSize, TextAnchor.UpperCenter);
            var rt = objectiveText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -24);
            rt.sizeDelta = new Vector2(1200, 60);
            objectiveText.text = string.Empty;
        }

        private void BuildCaption()
        {
            captionPanel = new GameObject("CaptionPanel", typeof(RectTransform), typeof(Image));
            captionPanel.transform.SetParent(Canvas.transform, false);
            var prt = captionPanel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0, 60);
            prt.sizeDelta = new Vector2(1400, 160);
            captionPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            captionText = CreateText("CaptionText", captionPanel.transform, BaseCaptionSize, TextAnchor.MiddleCenter);
            var rt = captionText.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20, 12);
            rt.offsetMax = new Vector2(-20, -12);

            captionPanel.SetActive(false);
        }

        private void BuildInteractPrompt()
        {
            var container = new GameObject("InteractPrompt", typeof(RectTransform), typeof(CanvasGroup));
            container.transform.SetParent(Canvas.transform, false);
            interactRect = container.GetComponent<RectTransform>();
            interactRect.anchorMin = new Vector2(0.5f, 0.5f);
            interactRect.anchorMax = new Vector2(0.5f, 0.5f);
            interactRect.pivot = new Vector2(0.5f, 0.5f);
            interactRect.anchoredPosition = new Vector2(0, -120);
            interactRect.sizeDelta = new Vector2(420, 72);

            interactGroup = container.GetComponent<CanvasGroup>();
            interactGroup.alpha = 0f;
            container.SetActive(false);

            // Key badge (the "large E").
            var badge = new GameObject("KeyBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(container.transform, false);
            var brt = badge.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(-150, 0);
            brt.sizeDelta = new Vector2(58, 58);
            badge.GetComponent<Image>().color = Accent;

            badgeLetter = CreateText("KeyLetter", badge.transform, 36, TextAnchor.MiddleCenter);
            badgeLetter.fontStyle = FontStyle.Bold;
            badgeLetter.color = new Color(0.02f, 0.05f, 0.08f); // dark on cyan
            badgeLetter.text = "E";
            var lrt = badgeLetter.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            // Action label to the right of the badge.
            interactLabel = CreateText("InteractLabel", container.transform, 26, TextAnchor.MiddleLeft);
            var ilrt = interactLabel.rectTransform;
            ilrt.anchorMin = new Vector2(0.5f, 0.5f);
            ilrt.anchorMax = new Vector2(0.5f, 0.5f);
            ilrt.pivot = new Vector2(0f, 0.5f);
            ilrt.anchoredPosition = new Vector2(-110, 0);
            ilrt.sizeDelta = new Vector2(300, 58);
            interactLabel.text = "Interact";
        }

        private void BuildCrosshair()
        {
            var dot = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(Canvas.transform, false);
            crosshairRect = dot.GetComponent<RectTransform>();
            crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRect.pivot = new Vector2(0.5f, 0.5f);
            crosshairRect.anchoredPosition = Vector2.zero;
            crosshairRect.sizeDelta = new Vector2(10, 10);
            crosshairImage = dot.GetComponent<Image>();
            crosshairImage.color = new Color(1f, 1f, 1f, 0.75f);
        }

        private Text CreateText(string name, Transform parent, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var t = go.AddComponent<Text>();
            t.font = UIFont;
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
