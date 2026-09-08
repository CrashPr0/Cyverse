using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Screen-space Level 4 scorecard: data stolen versus detection exposure.
    ///
    /// This is intentionally separate from HudUI so the attack scenario can
    /// add a persistent meter without changing the shared HUD or the other
    /// levels. It uses a scale-with-screen-size canvas, bounded TMP labels and
    /// no input-capturing graphics, so it remains readable at a small WebGL
    /// window and never blocks the interaction ray.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed class Level4ExfiltrationMeter : MonoBehaviour
    {
        public static Level4ExfiltrationMeter Instance { get; private set; }

        private static readonly Color PanelBackground = new Color(0.012f, 0.020f, 0.040f, 0.94f);
        private static readonly Color TextWhite = new Color(0.88f, 0.93f, 1.00f, 1f);
        private static readonly Color DataCyan = new Color(0.24f, 0.82f, 1.00f, 1f);
        private static readonly Color AlertAmber = new Color(1.00f, 0.64f, 0.18f, 1f);
        private static readonly Color AlertRose = new Color(1.00f, 0.20f, 0.35f, 1f);

        private Canvas canvas;
        private RectTransform panelRect;
        private TMP_Text titleText;
        private TMP_Text dataLabel;
        private TMP_Text detectionLabel;
        private TMP_Text dataValue;
        private TMP_Text detectionValue;
        private TMP_Text statusText;
        private Image dataFill;
        private Image detectionFill;
        private CanvasGroup group;
        private Level4CyberAttackManager manager;
        private bool completionShown;

        private float targetData;
        private float targetDetection;
        private float shownData;
        private float shownDetection;

        public float DataProgress => targetData;
        public float DetectionProgress => targetDetection;

        /// <summary>Gets or adds the overlay to a scene host.</summary>
        public static Level4ExfiltrationMeter Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            Level4ExfiltrationMeter existing = FindObjectOfType<Level4ExfiltrationMeter>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            if (host == null) host = new GameObject("Level4ExfiltrationMeter");
            return host.AddComponent<Level4ExfiltrationMeter>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            BuildCanvas();
            BuildPanel();
            SetProgress(0f, 0f);
            SetStatus("STAGE 1 / 4  ·  BYPASS MFA");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            SyncWithManager();
            float step = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            shownData = Mathf.Lerp(shownData, targetData, step);
            shownDetection = Mathf.Lerp(shownDetection, targetDetection, step);
            RefreshBars();
        }

        private void SyncWithManager()
        {
            if (manager == null) manager = FindObjectOfType<Level4CyberAttackManager>();
            if (manager == null) return;

            SetProgress(manager.ExfiltrationPercent / 100f, manager.DetectionPercent / 100f);

            if (manager.IsLevelComplete)
            {
                if (!completionShown)
                {
                    completionShown = true;
                    SetComplete(true);
                }
                return;
            }

            completionShown = false;
            if (!manager.ScenarioStarted)
            {
                SetStatus("BRIEFING  ·  WATCH THE RULES OF ENGAGEMENT");
                return;
            }

            int stage = Mathf.Clamp(manager.CurrentStationIndex + 1, 1, 4);
            string stageName = manager.CurrentPhase == Level4CyberAttackManager.Phase.EscalatePrivileges
                ? "ESCALATE PRIVILEGES"
                : manager.CurrentPhase == Level4CyberAttackManager.Phase.ExtractData
                    ? "EXTRACT DATA"
                    : manager.CurrentPhase == Level4CyberAttackManager.Phase.CoverTracks
                        ? "COVER TRACKS"
                        : "BYPASS MFA";
            string timer = manager.TimeExpired
                ? "  ·  OVERTIME"
                : "  ·  T-" + Mathf.FloorToInt(manager.TimeRemaining / 60f) + ":" +
                  Mathf.FloorToInt(manager.TimeRemaining % 60f).ToString("00");
            SetStatus("STAGE " + stage + " / 4  ·  " + stageName + timer);
        }

        /// <summary>Updates stolen-data and detection progress in [0,1].</summary>
        public void SetProgress(float dataStolen01, float detection01)
        {
            targetData = Mathf.Clamp01(dataStolen01);
            targetDetection = Mathf.Clamp01(detection01);
            RefreshBars();
        }

        /// <summary>Convenience overload for game mechanics that track counts.</summary>
        public void SetDataStolen(int stolen, int total, float detection01)
        {
            float progress = total > 0 ? (float)stolen / total : 0f;
            SetProgress(progress, detection01);
        }

        /// <summary>Sets the small stage/status line below the bars.</summary>
        public void SetStatus(string status)
        {
            if (statusText != null) statusText.text = status ?? string.Empty;
        }

        /// <summary>Marks the run complete and gives the scorecard a clear end state.</summary>
        public void SetComplete(bool complete)
        {
            if (statusText == null) return;
            statusText.text = complete ? "EXFILTRATION COMPLETE  ·  REPORT READY" : statusText.text;
            if (complete) titleText.color = new Color(0.35f, 1f, 0.55f);
        }

        public void SetVisible(bool visible)
        {
            if (group != null) group.alpha = visible ? 1f : 0f;
        }

        /// <summary>
        /// Static reporting helper keeps the mechanics layer independent of
        /// the meter's scene construction details.
        /// </summary>
        public static void Report(float dataStolen01, float detection01,
            string status = null, bool complete = false)
        {
            if (Instance == null) return;
            Instance.SetProgress(dataStolen01, detection01);
            if (status != null) Instance.SetStatus(status);
            if (complete) Instance.SetComplete(true);
        }

        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject("Level4AttackHUDCanvas",
                typeof(Canvas), typeof(CanvasScaler));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void BuildPanel()
        {
            GameObject panel = new GameObject("ExfiltrationDetectionPanel",
                typeof(RectTransform), typeof(Image), typeof(Outline), typeof(CanvasGroup));
            panel.transform.SetParent(canvas.transform, false);
            panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-28f, -92f);
            panelRect.sizeDelta = new Vector2(430f, 208f);

            Image background = panel.GetComponent<Image>();
            background.color = PanelBackground;
            background.raycastTarget = false;

            Outline outline = panel.GetComponent<Outline>();
            outline.effectColor = new Color(AlertAmber.r, AlertAmber.g, AlertAmber.b, 0.90f);
            outline.effectDistance = new Vector2(2.5f, 2.5f);

            group = panel.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Image accentBar = CreateImage("AccentBar", panel.transform, AlertAmber);
            RectTransform accentRect = accentBar.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(0f, 6f);

            titleText = CreateText("Title", panel.transform, 25f, TextAlignmentOptions.TopLeft,
                new Vector2(24f, -16f), new Vector2(-24f, -47f), AlertAmber);
            titleText.text = "EXFILTRATION  //  EXPOSURE";

            dataLabel = CreateText("DataLabel", panel.transform, 16f, TextAlignmentOptions.MidlineLeft,
                new Vector2(24f, -60f), new Vector2(180f, -83f), DataCyan);
            dataLabel.text = "DATA STOLEN";
            dataValue = CreateText("DataValue", panel.transform, 17f, TextAlignmentOptions.MidlineRight,
                new Vector2(335f, -60f), new Vector2(-24f, -83f), TextWhite);

            dataFill = CreateMeterRow("DataMeter", panel.transform, new Vector2(24f, -88f), DataCyan);

            detectionLabel = CreateText("DetectionLabel", panel.transform, 16f, TextAlignmentOptions.MidlineLeft,
                new Vector2(24f, -112f), new Vector2(180f, -135f), AlertAmber);
            detectionLabel.text = "DETECTION RISK";
            detectionValue = CreateText("DetectionValue", panel.transform, 17f, TextAlignmentOptions.MidlineRight,
                new Vector2(335f, -112f), new Vector2(-24f, -135f), TextWhite);

            detectionFill = CreateMeterRow("DetectionMeter", panel.transform, new Vector2(24f, -140f), AlertAmber);
            statusText = CreateText("Status", panel.transform, 14f, TextAlignmentOptions.MidlineLeft,
                new Vector2(24f, -164f), new Vector2(-24f, -184f), new Color(0.75f, 0.82f, 0.94f));
        }

        private Image CreateMeterRow(string name, Transform parent, Vector2 position, Color color)
        {
            GameObject track = new GameObject(name + "Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(parent, false);
            RectTransform trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 1f);
            trackRect.anchorMax = new Vector2(0f, 1f);
            trackRect.pivot = new Vector2(0f, 1f);
            trackRect.anchoredPosition = position;
            trackRect.sizeDelta = new Vector2(382f, 14f);
            Image trackImage = track.GetComponent<Image>();
            trackImage.color = new Color(0.05f, 0.075f, 0.12f, 0.94f);
            trackImage.raycastTarget = false;

            GameObject fill = new GameObject(name + "Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = color;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;
            return fillImage;
        }

        private void RefreshBars()
        {
            if (dataFill == null || detectionFill == null) return;
            dataFill.fillAmount = shownData;
            detectionFill.fillAmount = shownDetection;
            dataValue.text = Mathf.RoundToInt(shownData * 100f) + "%";
            detectionValue.text = Mathf.RoundToInt(shownDetection * 100f) + "%";

            Color detectionColor = shownDetection < 0.45f
                ? Color.Lerp(new Color(0.30f, 1f, 0.55f), AlertAmber, shownDetection / 0.45f)
                : Color.Lerp(AlertAmber, AlertRose, (shownDetection - 0.45f) / 0.55f);
            detectionFill.color = detectionColor;
            detectionValue.color = detectionColor;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, float size,
            TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            // All meter labels are specified as top-relative rectangles
            // (e.g. -112 to -135). The old full-rect anchors interpreted
            // those offsets from the bottom, pushing DETECTION RISK and the
            // status line below the card at runtime. Positive max.x values
            // denote a fixed left-column width; negative max.x values use a
            // stretched row with an inset from the right edge.
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = max.x >= 0f ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(min.x, Mathf.Min(min.y, max.y));
            rect.offsetMax = new Vector2(max.x, Mathf.Max(min.y, max.y));
            return text;
        }
    }
}
