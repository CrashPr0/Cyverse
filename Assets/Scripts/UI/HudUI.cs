using UnityEngine;
using UnityEngine.UI;
using Cyverse.Settings;

namespace Cyverse.UI
{
    /// <summary>
    /// Builds the heads-up display entirely in code so there are no scene
    /// references to break: a captions bar, an interaction prompt, an objective
    /// banner, and a crosshair. Other systems call the public Show/Hide methods.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        public static HudUI Instance { get; private set; }
        public static Font UIFont { get; private set; }

        public Canvas Canvas { get; private set; }

        private const int BaseCaptionSize = 28;
        private const int BasePromptSize = 24;
        private const int BaseObjectiveSize = 24;

        private GameObject captionPanel;
        private Text captionText;
        private Text promptText;
        private Text objectiveText;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            UIFont = LoadFont();
            BuildCanvas();
            BuildObjective();
            BuildCaption();
            BuildPrompt();
            BuildCrosshair();
        }

        /// <summary>Built-in Unity font; name changed to LegacyRuntime in 2022+.</summary>
        public static Font LoadFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        // ---- Public API -----------------------------------------------------

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

        public void ShowPrompt(string text)
        {
            promptText.text = text;
            promptText.enabled = true;
        }

        public void HidePrompt()
        {
            promptText.text = string.Empty;
            promptText.enabled = false;
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

        private void BuildPrompt()
        {
            promptText = CreateText("Prompt", Canvas.transform, BasePromptSize, TextAnchor.MiddleCenter);
            var rt = promptText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 250);
            rt.sizeDelta = new Vector2(800, 50);
            promptText.enabled = false;
        }

        private void BuildCrosshair()
        {
            var dot = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(Canvas.transform, false);
            var rt = dot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(6, 6);
            dot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.75f);
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
