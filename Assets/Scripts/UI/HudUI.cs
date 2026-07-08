using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Core;
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

        // Score counter
        private Text scoreText;
        private float scorePop = 1f;
        private float objectivePop = 1f;

        // Progress ring (top-left): endowed-progress onboarding tracker.
        private Image ringFill;
        private Text ringLabel;
        private float ringDisplayed = 0.15f;
        private float ringTarget = 0.15f;
        private const float RingEndowedStart = 0.15f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            UIFont = LoadFont();
            BuildCanvas();
            BuildObjective();
            BuildScore();
            BuildProgressRing();
            BuildCaption();
            BuildInteractPrompt();
            BuildCrosshair();

            ScoreSystem.Changed += OnScoreChanged;
        }

        void OnDestroy()
        {
            if (Instance == this) ScoreSystem.Changed -= OnScoreChanged;
        }

        private void OnScoreChanged(int total, int delta)
        {
            if (scoreText == null) return;
            scoreText.text = $"SCORE  <b>{total}</b>";
            if (delta > 0 && !AccessibilitySettings.ReduceMotion)
            {
                scorePop = 1.45f;
                SpawnScorePopup(delta);
            }
        }

        /// <summary>Floating "+N" that drifts up from the score counter and fades.</summary>
        private void SpawnScorePopup(int delta)
        {
            var t = CreateText("ScorePopup", Canvas.transform, 30, TextAnchor.UpperRight);
            t.fontStyle = FontStyle.Bold;
            t.color = delta >= 100 ? new Color(0.90f, 0.66f, 0.14f) : Accent; // gold for the big ones
            t.text = "+" + delta;
            AddOutline(t);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-30, -66);
            rt.sizeDelta = new Vector2(200, 40);
            StartCoroutine(ScorePopupRoutine(t));
        }

        private IEnumerator ScorePopupRoutine(Text t)
        {
            const float duration = 0.9f;
            Vector2 start = t.rectTransform.anchoredPosition;
            float e = 0f;
            while (e < duration && t != null)
            {
                e += Time.unscaledDeltaTime;
                float p = e / duration;
                t.rectTransform.anchoredPosition = start + Vector2.up * (46f * p);
                var c = t.color;
                c.a = 1f - p * p;
                t.color = c;
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
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
                bool doPulse = interactActive && !AccessibilitySettings.ReduceMotion;
                float pulse = doPulse ? 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
                interactRect.localScale = Vector3.one * pop * pulse;
            }

            // Score counter pops when points are awarded, then settles back.
            if (scoreText != null)
            {
                scorePop = Mathf.Lerp(scorePop, 1f, k);
                scoreText.rectTransform.localScale = Vector3.one * scorePop;
            }
            if (objectiveText != null)
            {
                objectivePop = Mathf.Lerp(objectivePop, 1f, k);
                objectiveText.rectTransform.localScale = Vector3.one * objectivePop;
            }

            if (ringFill != null)
            {
                ringDisplayed = Mathf.Lerp(ringDisplayed, ringTarget, k);
                ringFill.fillAmount = ringDisplayed;
            }
        }

        /// <summary>Quick crosshair "kick" when the player activates something.</summary>
        public void PulseCrosshair()
        {
            if (AccessibilitySettings.ReduceMotion) return;
            crosshairScale = 3.2f; // Update eases it back to the resting size
        }

        public static Font LoadFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        /// <summary>Give a panel a consistent look: tinted bg, accent outline,
        /// and a thin accent bar along the top edge. Shared by the dialogue box,
        /// settings card and controls overlay.</summary>
        public static void StylePanel(GameObject panel, Color bg, Color accent)
        {
            var img = panel.GetComponent<Image>();
            if (img == null) img = panel.AddComponent<Image>();
            img.color = bg;

            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.9f);
            outline.effectDistance = new Vector2(2.5f, 2.5f);

            var bar = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(panel.transform, false);
            var brt = bar.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1);
            brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(0.5f, 1);
            brt.sizeDelta = new Vector2(0, 5);
            brt.anchoredPosition = Vector2.zero;
            var barImg = bar.GetComponent<Image>();
            barImg.color = accent;
            barImg.raycastTarget = false;
        }

        /// <summary>Cheap dark outline behind text for legibility on any background.</summary>
        public static void AddOutline(Graphic g)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
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
            PinObjectiveToTop();
            bool changed = objectiveText.text != text && !string.IsNullOrEmpty(objectiveText.text);
            objectiveText.text = text;
            if (changed && !AccessibilitySettings.ReduceMotion) objectivePop = 1.3f;
        }

        /// <summary>Updates the top-left progress ring. Starts pre-filled
        /// (endowed progress — "you're already underway") rather than empty, so
        /// the visible gap to 100% always looks smaller and more finishable.</summary>
        public void SetProgress(int completed, int total, string label = null)
        {
            if (ringFill == null) return;
            float frac = total > 0 ? Mathf.Clamp01((float)completed / total) : 0f;
            ringTarget = Mathf.Lerp(RingEndowedStart, 1f, frac);
            if (ringLabel != null) ringLabel.text = label ?? $"{completed}/{total}";
        }

        /// <summary>Short-lived centred announcement — used for combo call-outs
        /// and "glossary entries unlocked" — fades after a beat.</summary>
        public void ShowToast(string message, Color color)
        {
            var t = CreateText("Toast", Canvas.transform, 26, TextAnchor.UpperCenter);
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.text = message;
            AddOutline(t);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -66);
            rt.sizeDelta = new Vector2(700, 40);
            StartCoroutine(ToastRoutine(t, color));
        }

        private IEnumerator ToastRoutine(Text t, Color baseColor)
        {
            const float hold = 1.5f, fade = 0.6f;
            yield return new WaitForSecondsRealtime(hold);
            float e = 0f;
            while (e < fade && t != null)
            {
                e += Time.unscaledDeltaTime;
                var c = baseColor;
                c.a = baseColor.a * (1f - e / fade);
                t.color = c;
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        // ---- Construction ---------------------------------------------------

        private void BuildCanvas()
        {
            // IMPORTANT: keep this a ROOT object. If it were parented under a
            // GameObject that has a Canvas (e.g. GameSystems), it would become a
            // nested canvas whose RectTransform collapses to a zero-size rect at
            // screen centre — making every HUD element ignore its anchors and
            // pile up in the middle.
            var go = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

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
            AddOutline(objectiveText);
            PinObjectiveToTop();
            objectiveText.text = string.Empty;
        }

        /// <summary>Force the objective banner to the top-centre of the screen.
        /// Re-asserted on every update so nothing can leave it stranded in the
        /// middle of the view (e.g. a reset RectTransform in a built scene).</summary>
        private void PinObjectiveToTop()
        {
            var rt = objectiveText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -24);
            rt.sizeDelta = new Vector2(1200, 60);
        }

        private void BuildScore()
        {
            scoreText = CreateText("Score", Canvas.transform, 26, TextAnchor.UpperRight);
            var rt = scoreText.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-30, -24);
            rt.sizeDelta = new Vector2(320, 44);
            scoreText.color = Accent;
            AddOutline(scoreText);
            scoreText.text = "SCORE  <b>0</b>";
        }

        private void BuildProgressRing()
        {
            var container = new GameObject("ProgressRing", typeof(RectTransform));
            container.transform.SetParent(Canvas.transform, false);
            var rt = container.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(30, -24);
            rt.sizeDelta = new Vector2(60, 60);

            Sprite donut = BuildDonutSprite();

            var baseGo = new GameObject("RingBase", typeof(RectTransform), typeof(Image));
            baseGo.transform.SetParent(container.transform, false);
            FillParent(baseGo.GetComponent<RectTransform>());
            var ringBase = baseGo.GetComponent<Image>();
            ringBase.sprite = donut;
            ringBase.color = new Color(1f, 1f, 1f, 0.16f);
            ringBase.raycastTarget = false;

            var fillGo = new GameObject("RingFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(container.transform, false);
            FillParent(fillGo.GetComponent<RectTransform>());
            ringFill = fillGo.GetComponent<Image>();
            ringFill.sprite = donut;
            ringFill.color = new Color(0.90f, 0.66f, 0.14f); // Spartan gold — reads as "reward" vs the cyan HUD
            ringFill.type = Image.Type.Filled;
            ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top;
            ringFill.fillClockwise = true;
            ringFill.fillAmount = RingEndowedStart; // endowed progress: never starts empty
            ringFill.raycastTarget = false;

            ringLabel = CreateText("RingLabel", container.transform, 16, TextAnchor.MiddleCenter);
            FillParent(ringLabel.rectTransform);
            AddOutline(ringLabel);
            ringLabel.text = "0/3";

            ringDisplayed = RingEndowedStart;
            ringTarget = RingEndowedStart;
        }

        private static void FillParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Procedural donut texture for the progress ring — a plain
        /// white ring shape with antialiased edges, tinted by the Image color.</summary>
        private static Sprite BuildDonutSprite(int size = 128, float innerFrac = 0.66f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float c = (size - 1) / 2f;
            float outerR = size / 2f - 1f;
            float innerR = outerR * innerFrac;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(outerR - d + 1f) * Mathf.Clamp01(d - innerR + 1f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
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
            prt.sizeDelta = new Vector2(1400, 170);
            StylePanel(captionPanel, new Color(0.02f, 0.04f, 0.07f, 0.82f), Accent);

            captionText = CreateText("CaptionText", captionPanel.transform, BaseCaptionSize, TextAnchor.MiddleCenter);
            var rt = captionText.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(28, 18);
            rt.offsetMax = new Vector2(-28, -18);
            AddOutline(captionText);

            // "continue" hint in the corner of the dialogue box
            var hint = CreateText("ContinueHint", captionPanel.transform, 18, TextAnchor.LowerRight);
            hint.color = new Color(0.6f, 0.85f, 1f, 0.9f);
            var hrt = hint.rectTransform;
            hrt.anchorMin = new Vector2(1, 0);
            hrt.anchorMax = new Vector2(1, 0);
            hrt.pivot = new Vector2(1, 0);
            hrt.anchoredPosition = new Vector2(-18, 8);
            hrt.sizeDelta = new Vector2(280, 26);
            hint.text = "[Space] Continue";

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
