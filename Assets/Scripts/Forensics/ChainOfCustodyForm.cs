using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Forensics
{
    /// <summary>
    /// A small, mouse-friendly evidence intake exercise. Each blank opens a
    /// custom dropdown so the interaction remains reliable in WebGL without a
    /// prefab or a scene-bound EventSystem.
    /// </summary>
    public sealed class ChainOfCustodyForm : MonoBehaviour
    {
        private sealed class Field
        {
            public string label;
            public string[] options;
            public int correctIndex;
            public int selectedIndex = -1;
            public Image background;
            public TMP_Text value;
        }

        public static ChainOfCustodyForm Instance { get; private set; }

        public event Action Changed;
        public event Action Completed;

        public bool IsComplete { get; private set; }
        public int SelectedCount
        {
            get
            {
                int count = 0;
                if (fields != null)
                    foreach (Field field in fields) if (field.selectedIndex >= 0) count++;
                return count;
            }
        }
        public int FieldCount => fields != null ? fields.Count : 4;

        private readonly List<Field> fields = new List<Field>();
        private GameObject card;
        private GameObject dropdown;
        private TMP_Text feedback;
        private Button submitButton;
        private bool open;

        private static readonly Color Green = new Color(0.30f, 1f, 0.55f);
        private static readonly Color Gold = new Color(0.90f, 0.66f, 0.14f);
        private static readonly Color Neutral = new Color(0.055f, 0.09f, 0.105f, 0.98f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static ChainOfCustodyForm Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            ChainOfCustodyForm found = FindObjectOfType<ChainOfCustodyForm>();
            if (found != null) return found;
            return host.AddComponent<ChainOfCustodyForm>();
        }

        public void Open()
        {
            if (open || GameState.AnyMenuOpen) return;
            if (card == null) Build();
            if (card == null) return;

            open = true;
            GameState.QuizActive = true;
            GameState.MenuTransitionFrame = Time.frameCount;
            FirstPersonController.LockCursor(false);
            card.SetActive(true);
            Refresh();
        }

        private void Update()
        {
            if (!open || Time.frameCount == GameState.MenuTransitionFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void Close()
        {
            CloseDropdown();
            open = false;
            if (card != null) card.SetActive(false);
            GameState.QuizActive = false;
            GameState.MenuTransitionFrame = Time.frameCount;
            FirstPersonController.LockCursor(true);
        }

        private void Build()
        {
            if (HudUI.Instance == null) return;
            EnsureEventSystem();
            ConfigureFields();

            card = new GameObject("ChainOfCustodyForm", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            RectTransform rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.17f, 0.10f);
            rt.anchorMax = new Vector2(0.83f, 0.90f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            HudUI.StylePanel(card, new Color(0.012f, 0.035f, 0.04f, 0.985f), Green);

            TMP_Text title = MakeText("Title", card.transform, 31, TextAlignmentOptions.TopLeft);
            title.text = "CHAIN OF CUSTODY  //  EVIDENCE INTAKE";
            title.color = Green;
            title.fontStyle = FontStyles.Bold;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, -24f), new Vector2(-120f, 52f), new Vector2(0f, 1f));

            TMP_Text intro = MakeText("Instructions", card.transform, 20, TextAlignmentOptions.TopLeft);
            intro.text = "Complete each blank from the SOC evidence package. Click a blank, choose the defensible record, then submit.";
            intro.color = new Color(0.78f, 0.90f, 0.94f);
            SetRect(intro.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, -82f), new Vector2(-64f, 58f), new Vector2(0f, 1f));

            Button close = MakeButton("Close", card.transform, "ESC", new Color(0.13f, 0.17f, 0.18f), Color.white);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-30f, -25f), new Vector2(70f, 38f), new Vector2(1f, 1f));
            close.onClick.AddListener(Close);
            close.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;

            for (int i = 0; i < fields.Count; i++) BuildRow(i);

            feedback = MakeText("Feedback", card.transform, 20, TextAlignmentOptions.MidlineLeft);
            feedback.color = new Color(0.68f, 0.82f, 0.88f);
            SetRect(feedback.rectTransform, new Vector2(0f, 0f), new Vector2(0.68f, 0f),
                new Vector2(32f, 36f), new Vector2(-12f, 54f), new Vector2(0f, 0f));

            submitButton = MakeButton("Submit", card.transform, "SUBMIT CUSTODY RECORD",
                new Color(0.12f, 0.42f, 0.25f), Color.white);
            SetRect(submitButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-32f, 30f), new Vector2(280f, 58f), new Vector2(1f, 0f));
            submitButton.onClick.AddListener(Validate);
            TMP_Text submitLabel = submitButton.GetComponentInChildren<TMP_Text>();
            submitLabel.alignment = TextAlignmentOptions.Center;
            submitLabel.fontSize = 18f;

            card.SetActive(false);
        }

        private void ConfigureFields()
        {
            fields.Clear();
            string source = SocProgress.TryGetEvidence(out SocEvidenceRecord evidence)
                ? evidence.computer : "WS-03";

            fields.Add(MakeField("EVIDENCE SOURCE",
                new[] { "WS-01", source, "WS-04" }, 1));
            fields.Add(MakeField("COLLECTED ITEM",
                new[] { "Printed alert screenshot", "Forensic disk image", "Live production workstation" }, 1));
            fields.Add(MakeField("INTEGRITY CHECK",
                new[] { "Filename visually checked", "SHA-256 hash verified", "No hash required" }, 1));
            fields.Add(MakeField("CUSTODY ACTION",
                new[] { "Return device to service", "Seal, log, and transfer", "Copy to personal USB" }, 1));
        }

        private static Field MakeField(string label, string[] options, int correctIndex) =>
            new Field { label = label, options = options, correctIndex = correctIndex };

        private void BuildRow(int index)
        {
            Field field = fields[index];
            float y = -170f - index * 92f;

            TMP_Text label = MakeText("Label_" + field.label, card.transform, 18, TextAlignmentOptions.MidlineLeft);
            label.text = field.label;
            label.color = new Color(0.55f, 0.78f, 0.82f);
            label.fontStyle = FontStyles.Bold;
            SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(34f, y), new Vector2(245f, 58f), new Vector2(0f, 1f));

            Button blank = MakeButton("Blank_" + index, card.transform,
                "CLICK TO SELECT                                      v", Neutral, Color.white);
            RectTransform blankRt = blank.GetComponent<RectTransform>();
            SetRect(blankRt, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(280f, y), new Vector2(-315f, 58f), new Vector2(0f, 1f));
            field.background = blank.GetComponent<Image>();
            field.value = blank.GetComponentInChildren<TMP_Text>();
            int captured = index;
            blank.onClick.AddListener(() => OpenDropdown(captured, blankRt));
        }

        private void OpenDropdown(int fieldIndex, RectTransform blank)
        {
            CloseDropdown();
            Field field = fields[fieldIndex];

            dropdown = new GameObject("CustodyDropdown", typeof(RectTransform), typeof(Image));
            dropdown.transform.SetParent(card.transform, false);
            dropdown.transform.SetAsLastSibling();
            RectTransform drt = dropdown.GetComponent<RectTransform>();
            drt.anchorMin = blank.anchorMin;
            drt.anchorMax = blank.anchorMax;
            drt.pivot = new Vector2(0f, 1f);
            drt.anchoredPosition = blank.anchoredPosition + new Vector2(0f, -60f);
            drt.sizeDelta = new Vector2(blank.sizeDelta.x, field.options.Length * 48f + 8f);
            dropdown.GetComponent<Image>().color = new Color(0.018f, 0.045f, 0.052f, 1f);
            Outline outline = dropdown.AddComponent<Outline>();
            outline.effectColor = new Color(Green.r, Green.g, Green.b, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            for (int optionIndex = 0; optionIndex < field.options.Length; optionIndex++)
            {
                int selected = optionIndex;
                Button option = MakeButton("Option_" + optionIndex, dropdown.transform,
                    field.options[optionIndex], new Color(0.04f, 0.095f, 0.105f, 1f), Color.white);
                RectTransform ort = option.GetComponent<RectTransform>();
                ort.anchorMin = new Vector2(0f, 1f);
                ort.anchorMax = new Vector2(1f, 1f);
                ort.pivot = new Vector2(0.5f, 1f);
                ort.anchoredPosition = new Vector2(0f, -4f - optionIndex * 48f);
                ort.sizeDelta = new Vector2(-8f, 44f);
                option.onClick.AddListener(() => Select(fieldIndex, selected));
            }
        }

        private void Select(int fieldIndex, int optionIndex)
        {
            Field field = fields[fieldIndex];
            field.selectedIndex = optionIndex;
            field.value.text = field.options[optionIndex] + "                                  v";
            field.background.color = Neutral;
            feedback.text = $"{SelectedCount}/{FieldCount} blanks completed";
            feedback.color = new Color(0.68f, 0.82f, 0.88f);
            CloseDropdown();
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            Changed?.Invoke();
        }

        private void Validate()
        {
            CloseDropdown();
            if (SelectedCount < FieldCount)
            {
                feedback.text = "Complete every blank before submitting.";
                feedback.color = Gold;
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                return;
            }

            int wrong = 0;
            foreach (Field field in fields)
            {
                bool correct = field.selectedIndex == field.correctIndex;
                field.background.color = correct
                    ? new Color(0.08f, 0.29f, 0.17f, 1f)
                    : new Color(0.38f, 0.10f, 0.08f, 1f);
                if (!correct) wrong++;
            }

            if (wrong > 0)
            {
                feedback.text = $"{wrong} entr{(wrong == 1 ? "y does" : "ies do")} not preserve defensible custody. Review the red blank{(wrong == 1 ? "" : "s")}.";
                feedback.color = new Color(1f, 0.48f, 0.34f);
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                return;
            }

            if (!IsComplete)
            {
                IsComplete = true;
                ScoreSystem.Add(150);
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                Completed?.Invoke();
            }
            feedback.text = "[OK] CUSTODY ACCEPTED - evidence is cleared for analysis.";
            feedback.color = Green;
            submitButton.interactable = false;
            submitButton.GetComponentInChildren<TMP_Text>().text = "RECORD VERIFIED";
        }

        private void Refresh()
        {
            if (feedback == null) return;
            if (IsComplete)
            {
                feedback.text = "[OK] CUSTODY ACCEPTED - evidence is cleared for analysis.";
                feedback.color = Green;
            }
            else
            {
                feedback.text = $"{SelectedCount}/{FieldCount} blanks completed";
                feedback.color = new Color(0.68f, 0.82f, 0.88f);
            }
        }

        private void CloseDropdown()
        {
            if (dropdown != null) Destroy(dropdown);
            dropdown = null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void CompleteForAutomation()
        {
            if (card == null) Build();
            for (int i = 0; i < fields.Count; i++)
            {
                fields[i].selectedIndex = fields[i].correctIndex;
                fields[i].value.text = fields[i].options[fields[i].correctIndex] + "  v";
            }
            Validate();
            if (open) Close();
        }
#endif

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject events = new GameObject("UIEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(events);
        }

        private static Button MakeButton(string name, Transform parent, string label, Color background, Color foreground)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = background;
            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.74f, 1f, 0.82f);
            colors.pressedColor = new Color(0.55f, 0.85f, 0.66f);
            button.colors = colors;

            TMP_Text text = MakeText("Value", go.transform, 20, TextAlignmentOptions.MidlineLeft);
            text.text = label;
            text.color = foreground;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableWordWrapping = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(16f, 4f);
            text.rectTransform.offsetMax = new Vector2(-12f, -4f);
            return button;
        }

        private static TMP_Text MakeText(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            return text;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
