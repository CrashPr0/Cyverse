using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.UI;

namespace Cyverse.Forensics
{
    /// <summary>
    /// The Evidence Explorer: a simplified forensic workbench for the seized
    /// phone, modeled on how examiners use tools like Magnet AXIOM (our own
    /// look, not theirs). Categories on the left, the items in one category
    /// in the middle, every field of the selected item and the file on the
    /// phone it came from on the right. The case question sits on top; the
    /// player answers it by tagging the artifact that proves it.
    ///
    /// Before any of that the phone has to be processed (EvidenceExplorer.Process.cs):
    /// a short setup on the same screen, like AXIOM's Process before Examine.
    ///
    /// Mouse-first so it works in WebGL: categories, rows and buttons are uGUI
    /// buttons on the HUD canvas. Keyboard too: type to filter, Up/Down to move
    /// through the list, Enter to tag, Esc to step away. Progress lives in the
    /// InvestigationCase and the PhoneImage, so reopening resumes.
    ///
    /// Actions: Choose(i) picks category i · Select(row, _) picks a row ·
    /// Navigate(±1) moves the selection · Append/Backspace/ClearText edit the
    /// filter · Submit tags the selection · Toggle shows the hint · Cancel.
    /// </summary>
    public sealed partial class EvidenceExplorer : MonoBehaviour, IGameplayActionTarget
    {
        public static EvidenceExplorer Instance { get; private set; }

        private const int MaxRows = 10;
        private const int MaxFilter = 32;

        private static readonly Color Green = new Color(0.30f, 1f, 0.45f);
        private static readonly Color Gold = new Color(0.90f, 0.66f, 0.14f);
        private static readonly Color Cyan = new Color(0.36f, 0.78f, 1f);
        private static readonly Color Ink = new Color(0.85f, 0.95f, 1f);
        private static readonly Color Muted = new Color(0.56f, 0.72f, 0.80f);
        // Workbench chrome: dark panes with a thin border and a header strip,
        // the way desktop forensic suites lay out their panes.
        private static readonly Color Pane = new Color(0.022f, 0.045f, 0.058f, 1f);
        private static readonly Color PaneHeader = new Color(0.045f, 0.09f, 0.11f, 1f);
        private static readonly Color Line = new Color(0.16f, 0.30f, 0.34f, 1f);
        private static readonly Color RowIdle = new Color(0.055f, 0.095f, 0.115f, 1f);
        private static readonly Color RowAlt = new Color(0.04f, 0.075f, 0.09f, 1f);
        private static readonly Color RowSelected = new Color(0.08f, 0.30f, 0.19f, 1f);
        private static readonly Color RowTagged = new Color(0.26f, 0.20f, 0.05f, 1f);
        private static readonly Color CategoryIdle = new Color(0.045f, 0.085f, 0.10f, 1f);
        private static readonly Color CategoryActive = new Color(0.08f, 0.27f, 0.17f, 1f);
        private static readonly Color Wrong = new Color(1f, 0.45f, 0.35f);

        private PhoneImage phone;
        private InvestigationCase activeCase;
        private string caseClosedNote;

        private ArtifactCategory category = ArtifactCategory.Messages;
        private string filter = "";
        private int selected = -1;
        private readonly List<PhoneArtifact> visible = new List<PhoneArtifact>();

        private GameObject card, examineRoot;
        private TMP_Text titleText, sourceText, questionText, questionMeta, filterText, detailText, feedbackText, listHeader;
        private Image feedbackEdge;
        private Button tagButton, hintButton;
        private readonly List<Button> categoryButtons = new List<Button>();
        private readonly List<TMP_Text> categoryCounts = new List<TMP_Text>();
        private readonly List<Image> categoryMarks = new List<Image>();
        private readonly List<Button> rowButtons = new List<Button>();
        private readonly List<Image> rowMarks = new List<Image>();
        private bool open;
        private ModalSession.Lease modal;
        // What is on screen, so a refresh animates only real changes.
        private PhoneArtifact detailShown;
        private CaseQuestion questionShown;
        private string filterShown;
        private bool filterCaretShown;

        public bool IsOpen => open;
        public ArtifactCategory ActiveCategory => category;
        public string Filter => filter;
        public IReadOnlyList<PhoneArtifact> VisibleArtifacts => visible;
        public PhoneArtifact SelectedArtifact =>
            selected >= 0 && selected < visible.Count ? visible[selected] : null;
        public string Feedback => feedbackText != null ? feedbackText.text : "";

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            modal?.Close();
            if (Instance == this) Instance = null;
        }

        public void Open(PhoneImage image, InvestigationCase investigation, string closedNote = null)
        {
            if (open) return;
            if (image == null || investigation == null)
            {
                Toast("Evidence Explorer unavailable — the phone image is missing.");
                return;
            }
            if (card == null) Build();
            if (card == null)
            {
                Toast("Evidence Explorer unavailable — the HUD is missing.");
                return;
            }
            if (!ModalSession.TryOpen(this, ModalSession.Channel.Quiz,
                out modal, releaseCursor: true)) return;

            phone = image;
            activeCase = investigation;
            caseClosedNote = closedNote;
            open = true;

            // Draw over the HUD's toasts and score: a case-closed toast fired as
            // the explorer reopens would otherwise sit across the source line.
            card.transform.SetAsLastSibling();
            card.SetActive(true);
            UiMotion.PanelIn(card, null, 0.985f);
            if (!phone.Processed) { BeginProcess(); return; }
            ShowExamine();
        }

        private void ShowExamine()
        {
            processRoot.SetActive(false);
            examineRoot.SetActive(true);
            titleText.text = $"EVIDENCE EXPLORER  //  {activeCase.title}";
            sourceText.text = $"<color=#8FB8CC>SOURCE</color>  Work phone of {Escape(phone.owner)}" +
                $"  ·  full file system  ·  {phone.artifacts.Count} artifacts" +
                "  ·  <color=#4CE087>image hash verified</color>  ·  read-only copy" + PassSummary();
            SetAccent(Cyan);
            SetFeedback(activeCase.IsComplete
                ? "<color=#E5A823>Case closed. Browse freely, or Esc to step away.</color>"
                : "Pick a category, select an item, and read its details. Tag only what proves the answer.",
                Ink);
            RefreshList(keepSelection: true);
            RevealExamine();
        }

        /// <summary>Bring the workbench in pane by pane: the categories, then
        /// the rows a beat behind them.</summary>
        private void RevealExamine()
        {
            UiMotion.FadeIn(examineRoot, UiMotion.Base);
            UiMotion.Stagger(categoryButtons, 0.04f, UiMotion.Rise * 0.5f);
            RevealRows(0.1f);
            UiMotion.Reveal(detailText.gameObject, 0.12f, UiMotion.Rise * 0.5f);
        }

        private void RevealRows(float delay) =>
            UiMotion.Stagger(rowButtons, delay, UiMotion.Rise * 0.5f, UiMotion.Base, 0.025f);

        void Update()
        {
            if (!open) return;
            if (Time.frameCount == GameState.MenuTransitionFrame) return;

            if (GameInput.CancelDown)
            {
                GameplayActions.TryApply(this, GameplayAction.Cancel());
                return;
            }
            if (InProcess) { UpdateProcess(); return; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) GameplayActions.TryApply(this, GameplayAction.Navigate(-1));
            else if (Input.GetKeyDown(KeyCode.DownArrow)) GameplayActions.TryApply(this, GameplayAction.Navigate(1));

            bool submitted = false;
            foreach (char c in Input.inputString)
            {
                if (c == '\b') GameplayActions.TryApply(this, GameplayAction.Backspace());
                else if (c == '\n' || c == '\r')
                {
                    GameplayActions.TryApply(this, GameplayAction.Submit());
                    submitted = true;
                    break;
                }
                else if (!char.IsControl(c)) GameplayActions.TryApply(this, GameplayAction.Append(c.ToString()));
            }
            // Some browsers report Return as a key transition without a newline
            // in Input.inputString; submit on the key too, without double-firing.
            if (!submitted && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                GameplayActions.TryApply(this, GameplayAction.Submit());
        }

        public bool TryApply(GameplayAction action, GameObject actor)
        {
            if (!open) return false;
            if (InProcess) return TryApplyProcess(action);

            switch (action.Kind)
            {
                case GameplayActionKind.Choose:
                    if (action.Index < 0 || action.Index >= PhoneImage.Categories.Length) return false;
                    bool switched = category != PhoneImage.Categories[action.Index];
                    category = PhoneImage.Categories[action.Index];
                    selected = -1;
                    RefreshList(keepSelection: false);
                    if (switched) RevealRows(0f);
                    if (Sfx.Instance != null) Sfx.Instance.PlayClick();
                    return true;
                case GameplayActionKind.Select:
                    if (action.Index < 0 || action.Index >= visible.Count) return false;
                    selected = action.Index;
                    RefreshList(keepSelection: true);
                    return true;
                case GameplayActionKind.Navigate:
                    if (visible.Count == 0) return false;
                    selected = selected < 0
                        ? (action.Index > 0 ? 0 : visible.Count - 1)
                        : Mathf.Clamp(selected + action.Index, 0, visible.Count - 1);
                    RefreshList(keepSelection: true);
                    return true;
                case GameplayActionKind.AppendText:
                case GameplayActionKind.PasteText:
                    if (string.IsNullOrEmpty(action.Text)) return false;
                    foreach (char c in action.Text)
                        if (!char.IsControl(c) && filter.Length < MaxFilter) filter += c;
                    RefreshList(keepSelection: true);
                    return true;
                case GameplayActionKind.Backspace:
                    if (filter.Length > 0) filter = filter.Substring(0, filter.Length - 1);
                    RefreshList(keepSelection: true);
                    return true;
                case GameplayActionKind.ClearText:
                    filter = "";
                    RefreshList(keepSelection: true);
                    return true;
                case GameplayActionKind.Submit:
                    TagSelected();
                    return true;
                case GameplayActionKind.Toggle:
                    ShowHint();
                    return true;
                case GameplayActionKind.Cancel:
                    Close();
                    return true;
                default:
                    return false;
            }
        }

        private void Close()
        {
            open = false;
            if (card != null) card.SetActive(false);
            modal?.Close();
            modal = null;
        }

        // ---- Tagging -----------------------------------------------------------

        private void TagSelected()
        {
            PhoneArtifact artifact = SelectedArtifact;
            CaseQuestion q = activeCase.Current;
            if (artifact == null)
            {
                SetFeedback("Select an item in the list first, then tag it.", Gold);
                return;
            }
            if (q == null)
            {
                SetFeedback("Case closed — nothing left to tag.", Gold);
                return;
            }
            if (artifact.Tagged)
            {
                SetFeedback("Already tagged as evidence. The question above needs a different item.", Gold);
                return;
            }

            if (artifact.id == q.artifactId)
            {
                artifact.Tagged = true;
                int award = q.Award();
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                string next = activeCase.IsComplete
                    ? "\n<color=#E5A823>" + (caseClosedNote ??
                        "<b>CASE CLOSED.</b> Every finding is tagged. Esc to step away.") + "</color>"
                    : "  Next question is up top.";
                SetAccent(Green);
                SetFeedback($"<color=#4CE087><b>TAGGED AS EVIDENCE</b>  +{award} points</color>{next}", Ink);
                activeCase.NotifyAnswered();
                RefreshList(keepSelection: true);
                UiMotion.Pulse(tagButton.transform, 0.04f);
                return;
            }

            // A wrong tag costs points but teaches: say where to look instead of
            // just "wrong", the way a lead examiner would.
            q.AttemptedWrong = true;
            if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            PhoneArtifact answer = phone.Find(q.artifactId);
            string where = answer != null && answer.category != artifact.category
                ? $" The proof is in <b>{PhoneImage.Label(answer.category)}</b>."
                : " Read the fields on the right again.";
            SetAccent(Wrong);
            SetFeedback($"<color=#FF8866><b>That item does not prove it.</b></color>{where}", Ink);
            UiMotion.Shake(tagButton.GetComponent<RectTransform>());
        }

        private void ShowHint()
        {
            CaseQuestion q = activeCase.Current;
            if (q == null) { SetFeedback("Case closed — no hints needed.", Gold); return; }
            q.HintUsed = true;
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            RefreshQuestion();
            SetAccent(Gold);
            SetFeedback($"<color=#E5A823>HINT (halves this question's points):</color> {Escape(q.hint)}", Ink);
        }

        // ---- Rendering -------------------------------------------------------------

        private void RefreshList(bool keepSelection)
        {
            PhoneArtifact keep = keepSelection ? SelectedArtifact : null;
            visible.Clear();
            visible.AddRange(phone.List(category, filter));
            selected = keep != null ? visible.IndexOf(keep) : -1;

            for (int i = 0; i < categoryButtons.Count; i++)
            {
                ArtifactCategory c = PhoneImage.Categories[i];
                int total = phone.CountIn(c);
                int hits = phone.CountIn(c, filter);
                string count = string.IsNullOrWhiteSpace(filter) ? $"{total}" : $"{hits}/{total}";
                bool active = c == category;
                categoryButtons[i].GetComponentInChildren<TMP_Text>().text = PhoneImage.Label(c);
                categoryCounts[i].text = count;
                categoryCounts[i].color = active ? Green : Muted;
                categoryMarks[i].enabled = active;
                UiMotion.ColorTo(categoryButtons[i].GetComponent<Image>(), active ? CategoryActive : CategoryIdle);
            }

            listHeader.text = $"{PhoneImage.Label(category)}  <color=#607585>·</color>  " +
                $"<color=#D8ECF5>{visible.Count} ITEM{(visible.Count == 1 ? "" : "S")}</color>";
            for (int i = 0; i < rowButtons.Count; i++)
            {
                Button row = rowButtons[i];
                bool used = i < visible.Count;
                row.gameObject.SetActive(used);
                if (!used) continue;
                PhoneArtifact a = visible[i];
                string tag = a.Tagged ? "<color=#E5A823>[EVIDENCE]</color> " : "";
                row.GetComponentInChildren<TMP_Text>().text =
                    $"<color=#8FB8CC>{a.timestamp}</color><pos=25%>{tag}{Badges(a)}{Escape(a.title)}";
                UiMotion.ColorTo(row.GetComponent<Image>(), i == selected ? RowSelected
                    : a.Tagged ? RowTagged : i % 2 == 0 ? RowIdle : RowAlt);
                rowMarks[i].enabled = i == selected;
                rowMarks[i].color = Green;
            }
            if (visible.Count > MaxRows)
                listHeader.text += $"  <color=#E5A823>· SHOWING {MaxRows}, FILTER TO NARROW</color>";

            RefreshFilter();

            RefreshDetail();
            RefreshQuestion();
        }

        private void RefreshDetail()
        {
            PhoneArtifact a = SelectedArtifact;
            if (a != detailShown)
            {
                // A new item: its fields fade up rather than snapping in.
                detailShown = a;
                UiMotion.FadeIn(detailText, UiMotion.Fast);
            }
            if (a == null)
            {
                detailText.text = "<color=#607585>Select an item to see every field the tool recovered, " +
                    "and the file on the phone it came from.</color>";
                tagButton.interactable = false;
                return;
            }
            var sb = new StringBuilder();
            sb.Append("<b><size=27>").Append(Escape(a.title)).Append("</size></b>\n");
            sb.Append("<size=17><color=#8FB8CC>").Append(PhoneImage.Label(a.category)).Append("   ·   ")
              .Append(a.timestamp).Append("</color>");
            if (a.Tagged) sb.Append("   <color=#E5A823><b>[TAGGED AS EVIDENCE]</b></color>");
            sb.Append("</size>\n\n");
            sb.Append(Escape(a.summary)).Append("\n\n");
            sb.Append(SectionLabel("RECOVERED FIELDS"));
            for (int i = 0; i < a.fieldNames.Length; i++)
                sb.Append(FieldLine(Escape(a.fieldNames[i]), Escape(a.fieldValues[i]), "#5BC8FF"));
            var findings = new StringBuilder();
            AppendPassFindings(findings, a);
            if (findings.Length > 0)
                sb.Append('\n').Append(SectionLabel("PROCESSING FINDINGS")).Append(findings);
            // Evidence information, as an examiner's tool shows under every record.
            // A zero-width space after each '/' lets a long path wrap at a folder.
            sb.Append('\n').Append(SectionLabel("EVIDENCE INFORMATION"))
              .Append(FieldLine("Source", Escape(a.sourcePath).Replace("/", "/\u200B"), "#5BC8FF"))
              .Append(FieldLine("Recovery", Escape(a.Recovery), "#5BC8FF"))
              .Append(FieldLine("Evidence", "#1  work phone of " + Escape(phone.owner), "#5BC8FF"));
            detailText.text = sb.ToString();
            tagButton.interactable = !a.Tagged && !activeCase.IsComplete;
        }

        private static string SectionLabel(string label) =>
            $"<size=15><color=#607585><b><cspace=3>{label}</cspace></b></color></size>\n";

        /// <summary>One "name  value" line with a hanging indent, so a long
        /// value wraps under itself instead of under the field name.</summary>
        private static string FieldLine(string name, string value, string nameColor) =>
            $"<color={nameColor}>{name}</color><indent=31%>{value}</indent>\n";

        private void RefreshQuestion()
        {
            CaseQuestion q = activeCase.Current;
            if (q != questionShown)
            {
                if (questionShown != null) UiMotion.Reveal(questionText.gameObject, 0.05f, UiMotion.Rise * 0.5f);
                questionShown = q;
            }
            questionText.text = q == null
                ? "<size=16><color=#E5A823><b><cspace=3>CASE CLOSED</cspace></b></color></size>\n<b>All findings tagged.</b>"
                : $"<size=16><color=#E5A823><b><cspace=3>QUESTION {activeCase.CurrentIndex + 1} OF {activeCase.questions.Length}</cspace></b></color></size>\n" +
                  $"<b>{Escape(q.prompt)}</b>";

            var meta = new StringBuilder("<color=#B8CCD6>");
            for (int i = 0; i < activeCase.questions.Length; i++)
                meta.Append(activeCase.questions[i].Answered ? "<color=#4CE087>■</color>" : "□").Append(' ');
            meta.Append($"</color>   <color=#E5A823><b>SCORE {ScoreSystem.Score}</b></color>");
            questionMeta.text = meta.ToString();
            hintButton.interactable = q != null;
        }

        private void LateUpdate()
        {
            // Keep the filter caret blinking without rebuilding the list.
            if (open) RefreshFilter();
        }

        private void RefreshFilter()
        {
            if (filterText == null) return;
            bool caretOn = Mathf.Sin(Time.unscaledTime * 6f) > 0f;
            // Called every frame for the caret: skip the TMP mesh rebuild
            // unless the caret flipped or the filter changed.
            if (caretOn == filterCaretShown && filter == filterShown) return;
            filterCaretShown = caretOn;
            filterShown = filter;
            filterText.text = $"<color=#8FB8CC><b>SEARCH</b></color>   {Escape(filter)}" +
                // The caret blinks by alpha so the placeholder never shifts.
                (caretOn ? "<color=#4CE087>_</color>" : "<color=#4CE08700>_</color>") +
                (filter.Length == 0 ? "<color=#607585>   type to search every field</color>" : "");
        }

        private void SetFeedback(string text, Color color)
        {
            if (feedbackText == null) return;
            if (feedbackText.text != text) UiMotion.FadeIn(feedbackText, UiMotion.Base);
            feedbackText.text = text;
            feedbackText.color = color;
        }

        private void SetAccent(Color color)
        {
            UiMotion.ColorTo(feedbackEdge, color, UiMotion.Base);
        }

        private static void Toast(string message)
        {
            if (HudUI.Instance != null) HudUI.Instance.ShowToast(message, new Color(1f, 0.55f, 0.4f));
        }

        private static string Escape(string s) => string.IsNullOrEmpty(s)
            ? ""
            : s.Replace("<", "‹").Replace(">", "›");

        // ---- Construction ----------------------------------------------------

        private void Build()
        {
            if (HudUI.Instance == null) return;
            ModalSession.EnsureEventSystem();

            card = new GameObject("EvidenceExplorer", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            // Stretch inside safe margins so it stays usable in a non-maximized
            // WebGL canvas instead of assuming a fixed pixel size.
            // Kept below the HUD score and clear of the HUD ring.
            Place(card.GetComponent<RectTransform>(), 0.05f, 0.035f, 0.95f, 0.905f, 0f);
            HudUI.StylePanel(card, new Color(0.008f, 0.022f, 0.032f, 0.985f), Green);
            Transform root = card.transform;

            // Title bar: app badge, what is open, close.
            Band(root, "TitleBar", new Color(0.03f, 0.065f, 0.08f, 1f), 0f, 0.935f, 1f, 0.994f, false);
            Image badge = Band(root, "AppBadge", new Color(0.08f, 0.30f, 0.17f, 1f), 0.012f, 0.945f, 0.118f, 0.985f, false);
            TMP_Text badgeText = MakeText(badge.transform, "Label", 15, TextAlignmentOptions.Center);
            badgeText.text = "<b><cspace=2>CY FORENSICS</cspace></b>";
            badgeText.color = Green;
            badgeText.enableWordWrapping = false;
            Place(badgeText.rectTransform, 0f, 0f, 1f, 1f, 0f);

            titleText = MakeText(root, "Title", 23, TextAlignmentOptions.MidlineLeft);
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Ink;
            titleText.characterSpacing = 1.5f;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
            titleText.enableWordWrapping = false;
            Place(titleText.rectTransform, 0.128f, 0.937f, 0.86f, 0.992f, 4f);

            Button close = MakeButton("Close", root, "<b>ESC</b>   CLOSE", new Color(0.09f, 0.14f, 0.16f));
            Place(close.GetComponent<RectTransform>(), 0.88f, 0.944f, 0.988f, 0.986f, 0f);
            close.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
            close.GetComponentInChildren<TMP_Text>().fontSize = 16;
            AddBorder(close.gameObject);
            UiMotion.AddPress(close);
            close.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Cancel()));

            BuildProcess(root);

            examineRoot = new GameObject("Examine", typeof(RectTransform));
            examineRoot.transform.SetParent(root, false);
            Place(examineRoot.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f, 0f);
            root = examineRoot.transform;

            // Evidence source strip.
            Image sourceBand = Band(root, "SourceBar", Pane, 0.012f, 0.893f, 0.988f, 0.93f, true);
            sourceText = MakeText(sourceBand.transform, "Source", 17, TextAlignmentOptions.MidlineLeft);
            sourceText.enableWordWrapping = false;
            sourceText.overflowMode = TextOverflowModes.Ellipsis;
            Place(sourceText.rectTransform, 0f, 0f, 1f, 1f, 0f);
            Inset(sourceText.rectTransform, 14f, 0f);

            // The case question, in a banner with a gold edge.
            Image questionBand = Band(root, "QuestionBar", new Color(0.06f, 0.055f, 0.025f, 1f),
                0.012f, ColumnsTop + 0.012f, 0.988f, 0.884f, true);
            Edge(questionBand.transform, Gold, 6f);
            questionText = MakeText(questionBand.transform, "Question", 22, TextAlignmentOptions.MidlineLeft);
            questionText.lineSpacing = 4f;
            questionText.paragraphSpacing = 10f;
            Place(questionText.rectTransform, 0f, 0f, 1f, 1f, 0f);
            questionText.rectTransform.offsetMin = new Vector2(24f, 10f);
            questionText.rectTransform.offsetMax = new Vector2(-24f, -10f);
            // Answered pips and the score, on the label line at the right.
            questionMeta = MakeText(questionBand.transform, "Progress", 17, TextAlignmentOptions.TopRight);
            questionMeta.enableWordWrapping = false;
            Place(questionMeta.rectTransform, 0.6f, 0f, 1f, 1f, 0f);
            questionMeta.rectTransform.offsetMin = new Vector2(0f, 8f);
            questionMeta.rectTransform.offsetMax = new Vector2(-24f, -16f);

            // Left pane: one button per artifact category, with its count.
            Pane3(root, "CategoryPane", 0.012f, 0.205f, out _).text = "ARTIFACT CATEGORIES";
            for (int i = 0; i < PhoneImage.Categories.Length; i++)
            {
                int captured = i;
                Button b = MakeButton("Category_" + i, root, PhoneImage.Label(PhoneImage.Categories[i]), CategoryIdle);
                float top = ColumnsTop - PaneHeaderH - 0.008f - i * 0.072f;
                Place(b.GetComponent<RectTransform>(), 0.018f, top - 0.065f, 0.199f, top, 0f);
                TMP_Text label = b.GetComponentInChildren<TMP_Text>();
                label.fontSize = 19;
                label.fontStyle = FontStyles.Bold;
                label.rectTransform.offsetMax = new Vector2(-54f, -2f);
                TMP_Text count = MakeText(b.transform, "Count", 18, TextAlignmentOptions.MidlineRight);
                count.enableWordWrapping = false;
                Place(count.rectTransform, 0.6f, 0f, 1f, 1f, 0f);
                Inset(count.rectTransform, 0f, 12f);
                categoryCounts.Add(count);
                categoryMarks.Add(Edge(b.transform, Green, 5f));
                b.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Choose(captured)));
                UiMotion.AddPress(b, row: true);
                categoryButtons.Add(b);
            }

            // Middle pane: search box, column header, rows.
            listHeader = Pane3(root, "ListPane", 0.213f, 0.557f, out _);
            listHeader.richText = true;

            Image filterBox = Band(root, "FilterBox", new Color(0.012f, 0.03f, 0.04f, 1f),
                0.219f, ColumnsTop - PaneHeaderH - 0.068f, 0.551f, ColumnsTop - PaneHeaderH - 0.008f, true);
            filterText = MakeText(filterBox.transform, "Filter", 20, TextAlignmentOptions.MidlineLeft);
            filterText.enableWordWrapping = false;
            filterText.overflowMode = TextOverflowModes.Ellipsis;
            Place(filterText.rectTransform, 0f, 0f, 1f, 1f, 0f);
            Inset(filterText.rectTransform, 14f, 10f);

            float rowsTop = ColumnsTop - PaneHeaderH - 0.11f;
            TMP_Text columns = MakeText(root, "Columns", 14, TextAlignmentOptions.MidlineLeft);
            columns.text = "<b><cspace=3><color=#607585>TIME<pos=25%>ITEM</color></cspace></b>";
            columns.enableWordWrapping = false;
            Place(columns.rectTransform, 0.219f, rowsTop + 0.002f, 0.551f, rowsTop + 0.034f, 0f);
            Inset(columns.rectTransform, 16f, 10f);

            for (int i = 0; i < MaxRows; i++)
            {
                int captured = i;
                Button row = MakeButton("Row_" + i, root, "", RowIdle);
                float top = rowsTop - i * 0.0485f;
                Place(row.GetComponent<RectTransform>(), 0.219f, top - 0.046f, 0.551f, top, 0f);
                TMP_Text label = row.GetComponentInChildren<TMP_Text>();
                label.fontSize = 19;
                label.richText = true;
                rowMarks.Add(Edge(row.transform, Green, 4f));
                row.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Select(captured, 0)));
                UiMotion.AddPress(row, row: true);
                rowButtons.Add(row);
            }

            // Right pane: every field of the selected artifact, then the actions.
            Pane3(root, "DetailPane", 0.565f, 0.988f, out _).text = "ARTIFACT DETAILS";
            detailText = MakeText(root, "Detail", 20, TextAlignmentOptions.TopLeft);
            detailText.lineSpacing = 6f;
            detailText.overflowMode = TextOverflowModes.Ellipsis;
            Place(detailText.rectTransform, 0.565f, ColumnsBottom + 0.1f, 0.988f, ColumnsTop - PaneHeaderH, 0f);
            Inset(detailText.rectTransform, 20f, 18f);
            detailText.rectTransform.offsetMax = new Vector2(-18f, -14f);

            tagButton = MakeButton("TagButton", root, "<b>TAG AS EVIDENCE</b>   <size=15><color=#9FD8B4>ENTER</color></size>",
                new Color(0.10f, 0.40f, 0.22f));
            Place(tagButton.GetComponent<RectTransform>(), 0.573f, ColumnsBottom + 0.012f, 0.80f, ColumnsBottom + 0.088f, 0f);
            tagButton.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
            Edge(tagButton.transform, Green, 4f);

            tagButton.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Submit()));
            UiMotion.AddPress(tagButton);

            hintButton = MakeButton("HintButton", root, "<b>HINT</b>   <size=15><color=#E8C878>HALF POINTS</color></size>",
                new Color(0.25f, 0.19f, 0.05f));
            Place(hintButton.GetComponent<RectTransform>(), 0.81f, ColumnsBottom + 0.012f, 0.98f, ColumnsBottom + 0.088f, 0f);
            hintButton.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
            hintButton.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Toggle()));
            UiMotion.AddPress(hintButton);

            feedbackText = MakeStatusBar(root, "Feedback", out feedbackEdge);

            card.SetActive(false);
        }

        // Top and bottom edges of the three columns, and the height of a pane's
        // header strip, as fractions of the card height.
        private const float ColumnsTop = 0.742f;
        private const float ColumnsBottom = 0.1f;
        private const float PaneHeaderH = 0.042f;

        /// <summary>A bordered pane from ColumnsTop to ColumnsBottom with a header
        /// strip; returns the header's label.</summary>
        private static TMP_Text Pane3(Transform root, string name, float xMin, float xMax, out Image pane)
        {
            pane = Band(root, name, Pane, xMin, ColumnsBottom, xMax, ColumnsTop, true);
            return PaneTitle(root, name + "Header", xMin, ColumnsTop - PaneHeaderH, xMax, ColumnsTop);
        }

        private static TMP_Text PaneTitle(Transform root, string name, float xMin, float yMin, float xMax, float yMax)
        {
            Image header = Band(root, name, PaneHeader, xMin, yMin, xMax, yMax, false);
            TMP_Text label = MakeText(header.transform, "Label", 15, TextAlignmentOptions.MidlineLeft);
            label.fontStyle = FontStyles.Bold;
            label.color = Muted;
            label.characterSpacing = 5f;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            Place(label.rectTransform, 0f, 0f, 1f, 1f, 0f);
            Inset(label.rectTransform, 16f, 10f);
            return label;
        }

        /// <summary>The feedback line along the bottom of the card, shared by
        /// Process and Examine.</summary>
        private static TMP_Text MakeStatusBar(Transform root, string name, out Image accent)
        {
            Image bar = Band(root, name + "Bar", new Color(0.03f, 0.06f, 0.075f, 1f), 0.012f, 0.016f, 0.988f, 0.088f, true);
            accent = Edge(bar.transform, Cyan, 6f);
            TMP_Text text = MakeText(bar.transform, name, 20, TextAlignmentOptions.MidlineLeft);
            Place(text.rectTransform, 0f, 0f, 1f, 1f, 0f);
            Inset(text.rectTransform, 24f, 16f);
            return text;
        }

        /// <summary>A plain coloured rect at a fraction of its parent, optionally
        /// with a thin border.</summary>
        private static Image Band(Transform parent, string name, Color color,
            float xMin, float yMin, float xMax, float yMax, bool border)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            Place(image.rectTransform, xMin, yMin, xMax, yMax, 0f);
            if (border) AddBorder(go);
            return image;
        }

        private static void AddBorder(GameObject go)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(1.5f, 1.5f);
        }

        /// <summary>An accent bar down the left edge of <paramref name="parent"/>.</summary>
        private static Image Edge(Transform parent, Color color, float width)
        {
            var go = new GameObject("Edge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(width, 0f);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Pad a stretched rect horizontally by <paramref name="left"/>
        /// and <paramref name="right"/> pixels.</summary>
        private static void Inset(RectTransform rt, float left, float right)
        {
            rt.offsetMin = new Vector2(left, rt.offsetMin.y);
            rt.offsetMax = new Vector2(-right, rt.offsetMax.y);
        }

        /// <summary>Anchor a rect to a fraction of its parent, inset by
        /// <paramref name="pad"/> pixels, so the layout scales with the canvas.</summary>
        private static void Place(RectTransform rt, float xMin, float yMin, float xMax, float yMax, float pad)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        private static Button MakeButton(string name, Transform parent, string label, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = background;
            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.74f, 1f, 0.82f);
            colors.pressedColor = new Color(0.55f, 0.85f, 0.66f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;

            TMP_Text text = MakeText(go.transform, "Label", 19, TextAlignmentOptions.MidlineLeft);
            text.text = label;
            text.color = Color.white;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableWordWrapping = false;
            Place(text.rectTransform, 0f, 0f, 1f, 1f, 0f);
            text.rectTransform.offsetMin = new Vector2(14f, 2f);
            text.rectTransform.offsetMax = new Vector2(-10f, -2f);
            return button;
        }

        private static TMP_Text MakeText(Transform parent, string name, int size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = size;
            t.alignment = alignment;
            t.color = Ink;
            t.richText = true;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Truncate;
            t.raycastTarget = false;
            return t;
        }
    }
}
