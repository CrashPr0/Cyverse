using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// One station in the controlled Level 4 red-team simulation. The station
    /// presents conceptual, fictional choices only; it never executes a
    /// command, contacts a host, or accepts real credentials. The manager owns
    /// progression and scoring while this component owns the world target and
    /// WebGL-safe choice UI.
    /// </summary>
    public sealed class CyberAttackStation : MonoBehaviour, IInteractable
    {
        public int StationIndex { get; private set; }
        public Level4CyberAttackContent.StationKind Kind { get; private set; }

        private Level4CyberAttackManager manager;
        private GameObject panel;
        private TMP_Text titleText;
        private TMP_Text objectiveText;
        private TMP_Text situationText;
        private TMP_Text feedbackText;
        private TMP_Text meterText;
        private Button[] optionButtons;
        private bool open;
        private int openedFrame = -1;
        private ModalSession.Lease modal;

        private static readonly Color Accent = new Color(1f, 0.42f, 0.25f);
        private static readonly Color SafeGreen = new Color(0.30f, 1f, 0.55f);
        private static readonly Color Gold = new Color(0.90f, 0.66f, 0.14f);

        public bool CanInteract => manager != null &&
            !GameState.AnyMenuOpen && !manager.IsLevelComplete;

        public string Prompt
        {
            get
            {
                if (manager == null) return "Cyber Attack Simulation";
                return manager.StationPrompt(this);
            }
        }

        public void Configure(Level4CyberAttackManager owner, int index,
            Level4CyberAttackContent.StationKind kind)
        {
            manager = owner;
            StationIndex = index;
            Kind = kind;
            RefreshWorldLabel();
        }

        public void Interact(GameObject interactor)
        {
            if (manager == null) return;
            if (!manager.CanAttempt(this))
            {
                manager.ExplainUnavailable(this);
                return;
            }
            OpenPanel();
        }

        private void OpenPanel()
        {
            if (open || manager == null) return;
            if (panel == null) BuildPanel();
            if (panel == null) return;

            Level4CyberAttackContent.StationScenario scenario = manager.ScenarioFor(this);
            if (scenario == null) return;
            if (!ModalSession.TryOpen(this, ModalSession.Channel.Quiz,
                out modal, releaseCursor: true)) return;

            open = true;
            openedFrame = Time.frameCount;
            panel.SetActive(true);
            // The in-panel telemetry below is easier to read while choosing;
            // avoid layering the persistent scorecard over the same modal.
            Level4ExfiltrationMeter.Instance?.SetVisible(false);
            titleText.text = scenario.title + "  //  CONTROLLED SIMULATION";
            objectiveText.text = scenario.objective;
            situationText.text = scenario.situation;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool available = scenario.options != null && i < scenario.options.Length;
                optionButtons[i].gameObject.SetActive(available);
                if (available)
                {
                    TMP_Text label = optionButtons[i].GetComponentInChildren<TMP_Text>();
                    if (label != null) label.text = $"[{i + 1}]  {scenario.options[i]}";
                }
            }
            feedbackText.text = "Choose the simulated action that stays inside the rules of engagement.";
            feedbackText.color = new Color(0.72f, 0.86f, 0.92f);
            RefreshMeter();
        }

        private void Update()
        {
            if (!open || Time.frameCount == openedFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
            else if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Choose(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Choose(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Choose(2);
        }

        private void Choose(int optionIndex)
        {
            if (!open || manager == null) return;
            bool resolved = manager.TryResolve(this, optionIndex);
            if (resolved)
            {
                Close();
                return;
            }

            feedbackText.text = manager.LastFeedback;
            feedbackText.color = new Color(1f, 0.48f, 0.34f);
            RefreshMeter();
        }

        public void CloseForManager()
        {
            if (open) Close();
        }

        private void Close()
        {
            if (!open) return;
            open = false;
            if (panel != null) panel.SetActive(false);
            Level4ExfiltrationMeter.Instance?.SetVisible(true);
            modal?.Close();
            modal = null;
        }

        private void RefreshMeter()
        {
            if (meterText == null || manager == null) return;
            meterText.text = manager.MeterSummary;
        }

        private void RefreshWorldLabel()
        {
            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text label in labels)
            {
                if (label.gameObject.name == "StationScreenLabel")
                    label.text = Level4CyberAttackContent.ShortLabel(Kind);
            }
        }

        private void BuildPanel()
        {
            if (HudUI.Instance == null) return;
            ModalSession.EnsureEventSystem();

            panel = new GameObject("CyberAttackChoicePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.08f);
            panelRect.anchorMax = new Vector2(0.90f, 0.92f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            HudUI.StylePanel(panel, new Color(0.025f, 0.018f, 0.035f, 0.985f), Accent);

            titleText = MakeText("Title", 26f, TextAlignmentOptions.TopLeft);
            SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(36f, -82f), new Vector2(-130f, -28f));
            titleText.color = Accent;
            titleText.fontStyle = FontStyles.Bold;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 18f;
            titleText.fontSizeMax = 26f;
            titleText.enableWordWrapping = false;

            Button close = MakeButton("Close", "ESC", new Color(0.12f, 0.10f, 0.15f, 1f));
            SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-124f, -68f), new Vector2(-32f, -27f));
            TMP_Text closeLabel = close.GetComponentInChildren<TMP_Text>();
            closeLabel.alignment = TextAlignmentOptions.Center;
            closeLabel.fontSize = 15f;
            closeLabel.enableWordWrapping = false;
            close.onClick.AddListener(Close);

            objectiveText = MakeText("Objective", 20f, TextAlignmentOptions.TopLeft);
            SetRect(objectiveText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(38f, -132f), new Vector2(-38f, -88f));
            objectiveText.color = new Color(0.78f, 0.90f, 0.96f);

            situationText = MakeText("Situation", 19f, TextAlignmentOptions.TopLeft);
            SetRect(situationText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(42f, -230f), new Vector2(-42f, -142f));
            situationText.color = Color.white;

            optionButtons = new Button[3];
            for (int i = 0; i < optionButtons.Length; i++)
            {
                Button choice = MakeButton("Option_" + i, "", new Color(0.055f, 0.07f, 0.10f, 1f));
                optionButtons[i] = choice;
                const float choiceHeight = 68f;
                float top = -254f - i * 80f;
                SetRect(choice.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(40f, top - choiceHeight), new Vector2(-40f, top));
                int captured = i;
                choice.onClick.AddListener(() => Choose(captured));
            }

            feedbackText = MakeText("Feedback", 19f, TextAlignmentOptions.MidlineLeft);
            SetRect(feedbackText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(42f, 95f), new Vector2(-42f, 145f));
            feedbackText.color = new Color(0.72f, 0.86f, 0.92f);

            meterText = MakeText("Meter", 18f, TextAlignmentOptions.MidlineRight);
            SetRect(meterText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(42f, 48f), new Vector2(-42f, 92f));
            meterText.color = Gold;

            TMP_Text controls = MakeText("Controls", 16f, TextAlignmentOptions.Center);
            SetRect(controls.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(42f, 12f), new Vector2(-42f, 43f));
            controls.text = "CLICK A CHOICE OR PRESS 1 / 2 / 3     ·     ESC CLOSE     ·     SYNTHETIC DATA ONLY";
            controls.color = new Color(0.60f, 0.74f, 0.82f);

            panel.SetActive(false);
        }

        private TMP_Text MakeText(string name, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(panel.transform, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.richText = true;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private Button MakeButton(string name, string label, Color background)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panel.transform, false);
            Image image = go.GetComponent<Image>();
            image.color = background;
            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.78f, 0.64f);
            colors.pressedColor = new Color(1f, 0.55f, 0.40f);
            button.colors = colors;

            TMP_Text text = MakeText(name + "Label", 19f, TextAlignmentOptions.MidlineLeft);
            text.transform.SetParent(go.transform, false);
            text.text = label;
            text.color = Color.white;
            text.raycastTarget = false;
            RectTransform rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(18f, 5f);
            rt.offsetMax = new Vector2(-18f, -5f);
            return button;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private void OnDestroy()
        {
            modal?.Close();
        }

        public static CyberAttackStation Build(Vector3 position, float yaw,
            Level4CyberAttackContent.StationKind kind, int index, Color accent)
        {
            GameObject root = new GameObject("CyberAttackStation_" + (index + 1));
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            Material body = BuildKit.MakeStandard(new Color(0.10f, 0.075f, 0.09f), 0.58f, 0.42f);
            Material panel = BuildKit.MakeStandard(new Color(0.045f, 0.035f, 0.055f), 0.6f, 0.5f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Desk", root.transform,
                new Vector3(0f, 0.48f, 0f), Vector3.zero, new Vector3(3.15f, 0.96f, 1.35f), body, true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "MonitorBody", root.transform,
                new Vector3(0f, 1.72f, 0.15f), new Vector3(-8f, 0f, 0f),
                new Vector3(2.6f, 1.55f, 0.12f), panel, true);
            BuildKit.SpawnLocal(PrimitiveType.Quad, "MonitorScreen", root.transform,
                new Vector3(0f, 1.72f, 0.075f), new Vector3(-8f, 0f, 0f),
                new Vector3(2.38f, 1.28f, 1f), BuildKit.MakeEmissive(accent, 0.68f), false);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Keyboard", root.transform,
                new Vector3(0f, 1.02f, -0.32f), Vector3.zero,
                new Vector3(0.95f, 0.035f, 0.34f), body, false);

            BuildKit.MakeSign(root.transform, position + new Vector3(0f, 3.38f, 0f),
                $"0{index + 1}  {Level4CyberAttackContent.DisplayName(kind).ToUpperInvariant()}", accent, 0.030f);
            TextMeshPro label = BuildScreenLabel(root.transform, new Vector3(0f, 1.72f, 0.015f),
                Level4CyberAttackContent.ShortLabel(kind), new Color(1f, 0.90f, 0.84f));
            label.gameObject.name = "StationScreenLabel";

            GameObject glow = new GameObject("StationLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.0f, -1.0f);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = accent;
            light.range = 6.5f;
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForceVertex;

            BuildKit.AddAimCollider(root, height: 3.2f, width: 3.6f);
            CyberAttackStation station = root.AddComponent<CyberAttackStation>();
            station.StationIndex = index;
            station.Kind = kind;
            return station;
        }

        private static TextMeshPro BuildScreenLabel(Transform parent, Vector3 localPosition,
            string text, Color color)
        {
            GameObject go = new GameObject("StationScreenLabel", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            TextMeshPro tmp = go.GetComponent<TextMeshPro>();
            tmp.text = text;
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = 0.32f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            WorldTextLayoutIntent.Configure(go, WorldTextLayoutIntent.Mode.Mounted);
            return tmp;
        }
    }
}
