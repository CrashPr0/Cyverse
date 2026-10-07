using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.Level;
using Cyverse.Player;
using Cyverse.Settings;
using Cyverse.UI;

namespace Cyverse.Forensics
{
    /// <summary>
    /// A small, mouse-friendly evidence intake exercise. Each blank opens a
    /// custom dropdown so the interaction remains reliable in WebGL without a
    /// prefab or a scene-bound EventSystem.
    /// </summary>
    public sealed class ChainOfCustodyForm : MonoBehaviour, IGameplayActionTarget
    {
        private sealed class Field
        {
            public string label;
            public string[] options;
            public int correctIndex;
            public string hint;
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
        // The form rows are built lazily on first interaction, but the world
        // readout and mission objective are visible immediately. Keep their
        // denominator truthful before Build() populates the list.
        public int FieldCount => fields != null && fields.Count > 0 ? fields.Count : 4;
        public bool IsOpen => open;

        private readonly List<Field> fields = new List<Field>();
        private GameObject card;
        private GameObject dropdown;
        private TMP_Text feedback;
        private Button submitButton;
        private TMP_Text releasedSignature, receivedSignature;
        private SocEvidenceRecord evidence;
        private bool open;
        private ModalSession.Lease modal;

        // Optional in-world (diegetic) readout of custody progress, rendered on
        // the station's DiegeticScreen. The interactive form stays a HUD modal;
        // this mirrors its state onto the world screen at the intake plinth.
        private DiegeticCustodyReadout diegetic;

        /// <summary>Bind a diegetic readout so the form mirrors its live state
        /// onto a world screen. Set by <see cref="ChainOfCustodyStation"/> when
        /// it builds the station screen. Safe to leave null (HUD-only).</summary>
        public void BindDiegeticReadout(DiegeticCustodyReadout readout)
        {
            diegetic = readout;
            RefreshDiegetic();
        }

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
            modal?.Close();
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
            if (open) return;
            if (card == null) Build();
            if (card == null) return;
            if (!ModalSession.TryOpen(this, ModalSession.Channel.Quiz,
                out modal, releaseCursor: true)) return;

            open = true;
            card.SetActive(true);
            Refresh();
        }

        private void Update()
        {
            if (!open || Time.frameCount == GameState.MenuTransitionFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape))
                GameplayActions.TryApply(this, GameplayAction.Cancel());
        }

        private void Close()
        {
            CloseDropdown();
            open = false;
            if (card != null) card.SetActive(false);
            modal?.Close();
            modal = null;
        }

        // Custody table columns, as fractions of the card width:
        // ITEM # | DATE / TIME | RELEASED BY | RECEIVED BY | REASON.
        private static readonly float[] ColX = { 0.025f, 0.115f, 0.265f, 0.515f, 0.765f, 0.975f };
        private const int ColItem = 0, ColDate = 1, ColReleased = 2, ColReceived = 3, ColReason = 4;
        private const float TableTop = -296f;
        private const float Entry1Top = TableTop - 40f;
        private const float Entry2Top = Entry1Top - 92f;
        private static readonly Color Ink = new Color(0.62f, 0.80f, 1f);
        private static readonly Color Muted = new Color(0.55f, 0.66f, 0.70f);

        private void Build()
        {
            if (HudUI.Instance == null) return;
            ModalSession.EnsureEventSystem();
            SocProgress.TryGetEvidence(out evidence);
            ConfigureFields();

            card = new GameObject("ChainOfCustodyForm", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            RectTransform rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.11f, 0.12f);
            rt.anchorMax = new Vector2(0.89f, 0.88f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            HudUI.StylePanel(card, new Color(0.012f, 0.035f, 0.04f, 0.985f), Green);

            TMP_Text title = MakeText("Title", card.transform, 31, TextAlignmentOptions.TopLeft);
            title.text = "CHAIN OF CUSTODY FORM  //  EVIDENCE INTAKE";
            title.color = Green;
            title.fontStyle = FontStyles.Bold;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, -22f), new Vector2(-120f, 46f), new Vector2(0f, 1f));

            TMP_Text caseLine = MakeText("CaseNumber", card.transform, 18, TextAlignmentOptions.TopLeft);
            caseLine.text = $"CASE #  {CustodyLog.CaseNumber(evidence)}        CLIENT REF #  SOC / {CustodyLog.Computer(evidence)}";
            caseLine.color = Muted;
            SetRect(caseLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(34f, -68f), new Vector2(-120f, 26f), new Vector2(0f, 1f));

            Button close = MakeButton("Close", card.transform, "ESC", new Color(0.13f, 0.17f, 0.18f), Color.white);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-30f, -25f), new Vector2(70f, 38f), new Vector2(1f, 1f));
            close.onClick.AddListener(() =>
                GameplayActions.TryApply(this, GameplayAction.Cancel()));
            close.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;

            TMP_Text intro = MakeText("Instructions", card.transform, 19, TextAlignmentOptions.TopLeft);
            // This is the SECOND entry on the record the SOC started. Say so up
            // front so it doesn't read as a repeated-screen bug: the device
            // changed hands, and every handoff needs its own row.
            intro.text = "<color=#E5A823><b>WHY A SECOND ENTRY?</b></color> This is the record the SOC started. The device changed hands again, SOC to Digital Forensics, and every handoff gets its own row, even inside one department. Fill in entry 2: a missing row breaks the chain, and the evidence can be challenged in court.";
            intro.color = new Color(0.78f, 0.90f, 0.94f);
            SetRect(intro.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(34f, -104f), new Vector2(-68f, 56f), new Vector2(0f, 1f));

            BuildItemBlock();

            TMP_Text heading = MakeText("CustodyHeading", card.transform, 20, TextAlignmentOptions.MidlineLeft);
            heading.text = "CHAIN OF CUSTODY";
            heading.color = Green;
            heading.fontStyle = FontStyles.Bold;
            SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(34f, TableTop + 32f), new Vector2(-68f, 28f), new Vector2(0f, 1f));

            BuildTable();

            feedback = MakeText("Feedback", card.transform, 19, TextAlignmentOptions.MidlineLeft);
            feedback.color = new Color(0.68f, 0.82f, 0.88f);
            SetRect(feedback.rectTransform, new Vector2(0f, 0f), new Vector2(0.72f, 0f),
                new Vector2(32f, 26f), new Vector2(-12f, 76f), new Vector2(0f, 0f));

            submitButton = MakeButton("Submit", card.transform, "SIGN & SUBMIT ENTRY 2",
                new Color(0.12f, 0.42f, 0.25f), Color.white);
            SetRect(submitButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-32f, 30f), new Vector2(280f, 58f), new Vector2(1f, 0f));
            submitButton.onClick.AddListener(() =>
                GameplayActions.TryApply(this, GameplayAction.Submit()));
            TMP_Text submitLabel = submitButton.GetComponentInChildren<TMP_Text>();
            submitLabel.alignment = TextAlignmentOptions.Center;
            submitLabel.fontSize = 18f;

            card.SetActive(false);
        }

        /// <summary>The item the record is about: one item, described once, and
        /// referred to by its number in every custody row after.</summary>
        private void BuildItemBlock()
        {
            var block = new GameObject("ItemBlock", typeof(RectTransform), typeof(Image));
            block.transform.SetParent(card.transform, false);
            block.GetComponent<Image>().color = new Color(0.03f, 0.075f, 0.085f, 1f);
            Outline outline = block.AddComponent<Outline>();
            outline.effectColor = new Color(Green.r, Green.g, Green.b, 0.35f);
            outline.effectDistance = new Vector2(1f, -1f);
            SetRect(block.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, -172f), new Vector2(-64f, 76f), new Vector2(0f, 1f));

            TMP_Text text = MakeText("ItemDetails", block.transform, 19, TextAlignmentOptions.MidlineLeft);
            text.text =
                $"<color=#8CC7D1><b>CLIENT ITEM {CustodyLog.ItemNumber}</b></color>     Description: {CustodyLog.ItemDescription(evidence)}\n" +
                $"<color=#8CC7D1>Serial #</color>  {CustodyLog.Serial(evidence)}        <color=#8CC7D1>Packaging</color>  sealed, tamper-evident evidence bag";
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(16f, 6f);
            text.rectTransform.offsetMax = new Vector2(-16f, -6f);
        }

        private void BuildTable()
        {
            var header = new GameObject("TableHeader", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(card.transform, false);
            header.GetComponent<Image>().color = new Color(0.06f, 0.16f, 0.13f, 1f);
            PlaceCell(header.GetComponent<RectTransform>(), ColItem, ColReason, TableTop, 34f, 0f);
            string[] headings = { "ITEM #", "DATE / TIME", "RELEASED BY", "RECEIVED BY", "REASON" };
            for (int c = 0; c < headings.Length; c++)
                Cell("Head_" + c, c, TableTop, 34f, 16, $"<b>{headings[c]}</b>", Ink);

            // Entry 1, carried over from the SOC exactly as it was signed there.
            var entry1 = new GameObject("Entry1", typeof(RectTransform), typeof(Image));
            entry1.transform.SetParent(card.transform, false);
            entry1.GetComponent<Image>().color = new Color(0.025f, 0.06f, 0.07f, 1f);
            PlaceCell(entry1.GetComponent<RectTransform>(), ColItem, ColReason, Entry1Top, 84f, 0f);
            Cell("E1_Item", ColItem, Entry1Top, 84f, 20, CustodyLog.ItemNumber, Color.white);
            Cell("E1_Date", ColDate, Entry1Top, 84f, 17, TwoLineDate(CustodyLog.CollectedAt(evidence)), Color.white);
            Cell("E1_Released", ColReleased, Entry1Top, 84f, 18,
                $"{CustodyLog.Client}\n{Signature(CustodyLog.ClientName)}", Color.white);
            Cell("E1_Received", ColReceived, Entry1Top, 84f, 18,
                $"{CustodyLog.SocAnalyst}\n{Signature(CustodyLog.SocAnalystName)}", Color.white);
            Cell("E1_Reason", ColReason, Entry1Top, 84f, 18, CustodyLog.CollectionReason, Color.white);

            // Entry 2, the player's: the same item moving SOC -> Digital Forensics.
            var entry2 = new GameObject("Entry2", typeof(RectTransform), typeof(Image));
            entry2.transform.SetParent(card.transform, false);
            entry2.GetComponent<Image>().color = new Color(0.04f, 0.10f, 0.085f, 1f);
            PlaceCell(entry2.GetComponent<RectTransform>(), ColItem, ColReason, Entry2Top, 112f, 0f);
            Cell("E2_Date", ColDate, Entry2Top - 10f, 50f, 17, TwoLineDate(CustodyLog.Now()), Color.white);
            int[] columns = { ColItem, ColReleased, ColReceived, ColReason };
            for (int i = 0; i < fields.Count; i++) BuildBlank(i, columns[i], Entry2Top - 10f);
            releasedSignature = Cell("E2_ReleasedSig", ColReleased, Entry2Top - 64f, 40f, 17, "", Muted);
            receivedSignature = Cell("E2_ReceivedSig", ColReceived, Entry2Top - 64f, 40f, 17, "", Muted);
            RefreshSignatures();

            // What each column means, so players know what to track. Says what
            // the columns ask for, never which option answers them.
            TMP_Text key = MakeText("ColumnKey", card.transform, 17, TextAlignmentOptions.MidlineLeft);
            key.text = "<color=#8CC7D1>RELEASED BY</color> who hands the item over     " +
                       "<color=#8CC7D1>RECEIVED BY</color> who takes custody     " +
                       "<color=#8CC7D1>REASON</color> why it is moving     " +
                       "<color=#8CC7D1>ITEM #</color> which item, from the item block";
            key.color = Muted;
            SetRect(key.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(36f, Entry2Top - 124f), new Vector2(-72f, 28f), new Vector2(0f, 1f));
        }

        private void ConfigureFields()
        {
            fields.Clear();
            string[] people = { CustodyLog.Client, CustodyLog.SocAnalyst, CustodyLog.LabAnalyst };

            // Entry 2 of the custody table. Four blanks keeps FieldCount aligned
            // with the objective text / task list in Level3ForensicsManager. The
            // answers sit in different positions so "always pick the middle one"
            // doesn't work; DeterministicGameplayAdapter.CustodyRoute mirrors them.
            fields.Add(MakeField("ITEM #", new[] { CustodyLog.ItemNumber, "#2", "Not needed" }, 0,
                "Item #: it's the same phone as entry 1, so it keeps item #1. The number never changes between handoffs."));
            fields.Add(MakeField("RELEASED BY", people, 1,
                "Released by: who had custody before the lab? Look at entry 1: the SOC analyst who received it."));
            fields.Add(MakeField("RECEIVED BY", people, 2,
                "Received by: whoever takes custody now. That's you, in Digital Forensics."));
            fields.Add(MakeField("REASON",
                new[] { CustodyLog.TransferReason, "Return to client", "None: same department" }, 0,
                "Reason: the lab has it for analysis. A handoff inside one department still needs a reason."));
        }

        private static Field MakeField(string label, string[] options, int correctIndex, string hint) =>
            new Field { label = label, options = options, correctIndex = correctIndex, hint = hint };

        private void BuildBlank(int index, int column, float top)
        {
            Field field = fields[index];
            Button blank = MakeButton("Blank_" + index, card.transform, "SELECT", Neutral, Color.white);
            RectTransform blankRt = blank.GetComponent<RectTransform>();
            PlaceCell(blankRt, column, column, top, 50f, 6f);
            field.background = blank.GetComponent<Image>();
            field.value = blank.GetComponentInChildren<TMP_Text>();
            field.value.fontSize = 18f;
            field.value.color = Muted;
            field.value.rectTransform.offsetMax = new Vector2(-30f, -4f);

            TMP_Text arrow = MakeText("Arrow", blank.transform, 16, TextAlignmentOptions.MidlineRight);
            arrow.text = "v";
            arrow.color = Green;
            arrow.rectTransform.anchorMin = Vector2.zero;
            arrow.rectTransform.anchorMax = Vector2.one;
            arrow.rectTransform.offsetMin = new Vector2(0f, 0f);
            arrow.rectTransform.offsetMax = new Vector2(-12f, 0f);

            int captured = index;
            blank.onClick.AddListener(() => OpenDropdown(captured, blankRt));
        }

        private TMP_Text Cell(string name, int column, float top, float height, float size, string text, Color color)
        {
            TMP_Text cell = MakeText(name, card.transform, size, TextAlignmentOptions.MidlineLeft);
            cell.text = text;
            cell.color = color;
            cell.overflowMode = TextOverflowModes.Ellipsis;
            PlaceCell(cell.rectTransform, column, column, top, height, 10f);
            return cell;
        }

        private static void PlaceCell(RectTransform rt, int firstColumn, int lastColumn, float top, float height, float pad)
        {
            rt.anchorMin = new Vector2(ColX[firstColumn], 1f);
            rt.anchorMax = new Vector2(ColX[lastColumn + 1], 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pad, top);
            rt.sizeDelta = new Vector2(-2f * pad, height);
        }

        private static string TwoLineDate(string stamp)
        {
            int split = stamp.IndexOf(' ');
            return split < 0 ? stamp : stamp.Substring(0, split) + "\n" + stamp.Substring(split + 1);
        }

        private static string Signature(string name) =>
            $"<i><color=#9FC8FF><size=90%>signed:</size> {name}</color></i>";

        private void RefreshSignatures()
        {
            if (releasedSignature == null) return;
            if (IsComplete)
            {
                releasedSignature.text = Signature(CustodyLog.SocAnalystName);
                receivedSignature.text = Signature(PlayerIdentity.Callsign);
            }
            else
            {
                releasedSignature.text = "<i>signs on submit</i>";
                receivedSignature.text = "<i>signs on submit</i>";
            }
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
            drt.anchoredPosition = blank.anchoredPosition + new Vector2(0f, -blank.sizeDelta.y - 4f);
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
                option.onClick.AddListener(() => GameplayActions.TryApply(this,
                    GameplayAction.Select(fieldIndex, selected)));
            }
        }

        private void Select(int fieldIndex, int optionIndex)
        {
            Field field = fields[fieldIndex];
            field.selectedIndex = optionIndex;
            field.value.text = field.options[optionIndex];
            field.value.color = Color.white;
            field.background.color = Neutral;
            feedback.text = $"{SelectedCount}/{FieldCount} blanks completed";
            feedback.color = new Color(0.68f, 0.82f, 0.88f);
            CloseDropdown();
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            RefreshDiegetic();
            Changed?.Invoke();
        }

        public bool TryApply(GameplayAction action, GameObject actor)
        {
            if (!open) return false;

            switch (action.Kind)
            {
                case GameplayActionKind.Select:
                    if (action.Index < 0 || action.Index >= fields.Count) return false;
                    Field field = fields[action.Index];
                    if (action.ValueIndex < 0 || action.ValueIndex >= field.options.Length) return false;
                    Select(action.Index, action.ValueIndex);
                    return true;
                case GameplayActionKind.Submit:
                    if (IsComplete) return false;
                    Validate();
                    return true;
                case GameplayActionKind.Cancel:
                    Close();
                    return true;
                default:
                    return false;
            }
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
            string hint = null;
            foreach (Field field in fields)
            {
                bool correct = field.selectedIndex == field.correctIndex;
                field.background.color = correct
                    ? new Color(0.08f, 0.29f, 0.17f, 1f)
                    : new Color(0.38f, 0.10f, 0.08f, 1f);
                if (correct) continue;
                wrong++;
                if (hint == null) hint = field.hint;
            }

            if (wrong > 0)
            {
                // Name what is wrong with the first red blank: the form is where
                // players learn what each column means.
                feedback.text = $"<b>{wrong} blank{(wrong == 1 ? " breaks" : "s break")} the chain.</b> {hint}";
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
            feedback.text = "[OK] CUSTODY ACCEPTED - entry 2 signed and logged. Evidence cleared for analysis.";
            feedback.color = Green;
            submitButton.interactable = false;
            submitButton.GetComponentInChildren<TMP_Text>().text = "ENTRY 2 SIGNED";
            RefreshSignatures();
            RefreshDiegetic();
        }

        private void Refresh()
        {
            if (feedback == null) return;
            RefreshSignatures();
            if (IsComplete)
            {
                feedback.text = "[OK] CUSTODY ACCEPTED - entry 2 signed and logged. Evidence cleared for analysis.";
                feedback.color = Green;
            }
            else
            {
                feedback.text = $"{SelectedCount}/{FieldCount} blanks completed";
                feedback.color = new Color(0.68f, 0.82f, 0.88f);
            }
        }

        private void RefreshDiegetic()
        {
            if (diegetic == null) return;
            diegetic.SetState(SelectedCount, FieldCount, IsComplete);
        }

        private void CloseDropdown()
        {
            if (dropdown != null) Destroy(dropdown);
            dropdown = null;
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
