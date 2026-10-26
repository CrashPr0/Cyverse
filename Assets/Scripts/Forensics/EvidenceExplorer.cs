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
        private static readonly Color Ink = new Color(0.85f, 0.95f, 1f);
        private static readonly Color RowIdle = new Color(0.08f, 0.13f, 0.16f, 1f);
        private static readonly Color RowSelected = new Color(0.10f, 0.32f, 0.20f, 1f);
        private static readonly Color RowTagged = new Color(0.30f, 0.24f, 0.06f, 1f);
        private static readonly Color CategoryIdle = new Color(0.08f, 0.12f, 0.14f, 1f);
        private static readonly Color CategoryActive = new Color(0.12f, 0.40f, 0.24f, 1f);

        private PhoneImage phone;
        private InvestigationCase activeCase;
        private string caseClosedNote;

        private ArtifactCategory category = ArtifactCategory.Messages;
        private string filter = "";
        private int selected = -1;
        private readonly List<PhoneArtifact> visible = new List<PhoneArtifact>();

        private GameObject card, examineRoot;
        private TMP_Text titleText, sourceText, questionText, filterText, detailText, feedbackText, listHeader;
        private Button tagButton, hintButton;
        private readonly List<Button> categoryButtons = new List<Button>();
        private readonly List<Button> rowButtons = new List<Button>();
        private bool open;
        private ModalSession.Lease modal;

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
            SetFeedback(activeCase.IsComplete
                ? "<color=#E5A823>Case closed. Browse freely, or Esc to step away.</color>"
                : "Pick a category, select an item, and read its details. Tag only what proves the answer.",
                Ink);
            RefreshList(keepSelection: true);
        }

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
                    category = PhoneImage.Categories[action.Index];
                    selected = -1;
                    RefreshList(keepSelection: false);
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
                SetFeedback($"<color=#4CE087><b>TAGGED AS EVIDENCE</b>  +{award} points</color>{next}", Ink);
                activeCase.NotifyAnswered();
                RefreshList(keepSelection: true);
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
            SetFeedback($"<color=#FF8866><b>That item does not prove it.</b></color>{where}", Ink);
        }

        private void ShowHint()
        {
            CaseQuestion q = activeCase.Current;
            if (q == null) { SetFeedback("Case closed — no hints needed.", Gold); return; }
            q.HintUsed = true;
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            RefreshQuestion();
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
                categoryButtons[i].GetComponentInChildren<TMP_Text>().text = $"{PhoneImage.Label(c)}  ({count})";
                categoryButtons[i].GetComponent<Image>().color = c == category ? CategoryActive : CategoryIdle;
            }

            listHeader.text = $"{PhoneImage.Label(category)}  ·  {visible.Count} item{(visible.Count == 1 ? "" : "s")}";
            for (int i = 0; i < rowButtons.Count; i++)
            {
                Button row = rowButtons[i];
                bool used = i < visible.Count;
                row.gameObject.SetActive(used);
                if (!used) continue;
                PhoneArtifact a = visible[i];
                string tag = a.Tagged ? "<color=#E5A823>[EVIDENCE]</color> " : "";
                row.GetComponentInChildren<TMP_Text>().text =
                    $"<color=#9FC4D6>{a.timestamp}</color>  {tag}{Badges(a)}{Escape(a.title)}";
                row.GetComponent<Image>().color = i == selected ? RowSelected : a.Tagged ? RowTagged : RowIdle;
            }
            if (visible.Count > MaxRows)
                listHeader.text += $"  ·  showing {MaxRows}, filter to narrow";

            RefreshFilter();

            RefreshDetail();
            RefreshQuestion();
        }

        private void RefreshDetail()
        {
            PhoneArtifact a = SelectedArtifact;
            if (a == null)
            {
                detailText.text = "<color=#607585>Select an item to see every field the tool recovered, " +
                    "and the file on the phone it came from.</color>";
                tagButton.interactable = false;
                return;
            }
            var sb = new StringBuilder();
            sb.Append("<b><size=24>").Append(Escape(a.title)).Append("</size></b>\n");
            if (a.Tagged) sb.Append("<color=#E5A823><b>TAGGED AS EVIDENCE</b></color>\n");
            sb.Append("<color=#8FB8CC>").Append(PhoneImage.Label(a.category)).Append("  ·  ")
              .Append(a.timestamp).Append("</color>\n\n");
            sb.Append(Escape(a.summary)).Append("\n\n");
            for (int i = 0; i < a.fieldNames.Length; i++)
                sb.Append("<color=#5BC8FF>").Append(Escape(a.fieldNames[i])).Append(":</color>  ")
                  .Append(Escape(a.fieldValues[i])).Append('\n');
            AppendPassFindings(sb, a);
            // Evidence information, as an examiner's tool shows under every record.
            sb.Append("\n<size=18><color=#8FB8CC>EVIDENCE INFORMATION</color>\n<color=#C8DCE6>")
              .Append("Source: ").Append(Escape(a.sourcePath)).Append('\n')
              .Append("Recovery: ").Append(Escape(a.Recovery)).Append('\n')
              .Append("Evidence: work phone of ").Append(Escape(phone.owner)).Append("</color></size>");
            detailText.text = sb.ToString();
            tagButton.interactable = !a.Tagged && !activeCase.IsComplete;
        }

        private void RefreshQuestion()
        {
            var sb = new StringBuilder();
            CaseQuestion q = activeCase.Current;
            if (q == null)
            {
                sb.Append("<color=#E5A823><b>CASE CLOSED [OK]</b></color>  All findings tagged.");
            }
            else
            {
                sb.Append($"<color=#5BC8FF><b>QUESTION {activeCase.CurrentIndex + 1}/{activeCase.questions.Length}</b></color>   ")
                  .Append(Escape(q.prompt));
            }
            sb.Append("\n<size=20><color=#B8CCD6>");
            for (int i = 0; i < activeCase.questions.Length; i++)
                sb.Append(activeCase.questions[i].Answered ? "<color=#4CE087>■</color>" : "□").Append(' ');
            sb.Append($"</color>   <color=#E5A823>SCORE {ScoreSystem.Score}</color></size>");
            questionText.text = sb.ToString();
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
            filterText.text = $"<color=#8FB8CC>FILTER</color>  {Escape(filter)}" +
                (Mathf.Sin(Time.unscaledTime * 6f) > 0f ? "<color=#4CE087>_</color>" : " ") +
                (filter.Length == 0 ? "<color=#607585>   type to search every field</color>" : "");
        }

        private void SetFeedback(string text, Color color)
        {
            if (feedbackText == null) return;
            feedbackText.text = text;
            feedbackText.color = color;
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
            Place(card.GetComponent<RectTransform>(), 0.035f, 0.055f, 0.965f, 0.945f, 0f);
            HudUI.StylePanel(card, new Color(0.008f, 0.025f, 0.04f, 0.98f), Green);
            Transform root = card.transform;

            titleText = MakeText(root, "Title", 24, TextAlignmentOptions.MidlineLeft);
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Green;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
            titleText.enableWordWrapping = false;
            Place(titleText.rectTransform, 0.02f, 0.935f, 0.86f, 0.99f, 4f);

            Button close = MakeButton("Close", root, "ESC  CLOSE", new Color(0.13f, 0.17f, 0.18f));
            Place(close.GetComponent<RectTransform>(), 0.87f, 0.935f, 0.985f, 0.985f, 2f);
            close.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Cancel()));

            BuildProcess(root);

            examineRoot = new GameObject("Examine", typeof(RectTransform));
            examineRoot.transform.SetParent(root, false);
            Place(examineRoot.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f, 0f);
            root = examineRoot.transform;

            sourceText = MakeText(root, "Source", 17, TextAlignmentOptions.MidlineLeft);
            sourceText.enableWordWrapping = false;
            sourceText.overflowMode = TextOverflowModes.Ellipsis;
            Place(sourceText.rectTransform, 0.02f, 0.895f, 0.98f, 0.935f, 4f);

            questionText = MakeText(root, "Question", 20, TextAlignmentOptions.TopLeft);
            Place(questionText.rectTransform, 0.02f, 0.795f, 0.98f, 0.895f, 4f);

            // Left rail: one button per artifact category.
            for (int i = 0; i < PhoneImage.Categories.Length; i++)
            {
                int captured = i;
                Button b = MakeButton("Category_" + i, root, PhoneImage.Label(PhoneImage.Categories[i]), CategoryIdle);
                float top = ColumnsTop - i * 0.074f;
                Place(b.GetComponent<RectTransform>(), 0.015f, top - 0.066f, 0.205f, top, 2f);
                b.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Choose(captured)));
                categoryButtons.Add(b);
            }

            // Middle: filter line, list header, rows.
            var filterBox = new GameObject("FilterBox", typeof(RectTransform), typeof(Image));
            filterBox.transform.SetParent(root, false);
            filterBox.GetComponent<Image>().color = new Color(0.03f, 0.07f, 0.08f, 1f);
            Place(filterBox.GetComponent<RectTransform>(), 0.215f, ColumnsTop - 0.065f, 0.545f, ColumnsTop, 2f);
            filterText = MakeText(filterBox.transform, "Filter", 20, TextAlignmentOptions.MidlineLeft);
            filterText.enableWordWrapping = false;
            filterText.overflowMode = TextOverflowModes.Ellipsis;
            Place(filterText.rectTransform, 0f, 0f, 1f, 1f, 10f);

            listHeader = MakeText(root, "ListHeader", 18, TextAlignmentOptions.MidlineLeft);
            listHeader.color = new Color(0.62f, 0.78f, 0.86f);
            listHeader.enableWordWrapping = false;
            listHeader.overflowMode = TextOverflowModes.Ellipsis;
            Place(listHeader.rectTransform, 0.215f, ColumnsTop - 0.11f, 0.545f, ColumnsTop - 0.07f, 4f);

            for (int i = 0; i < MaxRows; i++)
            {
                int captured = i;
                Button row = MakeButton("Row_" + i, root, "", RowIdle);
                float top = ColumnsTop - 0.115f - i * 0.052f;
                Place(row.GetComponent<RectTransform>(), 0.215f, top - 0.048f, 0.545f, top, 1f);
                TMP_Text label = row.GetComponentInChildren<TMP_Text>();
                label.fontSize = 18;
                label.richText = true;
                row.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Select(captured, 0)));
                rowButtons.Add(row);
            }

            // Right: detail of the selected artifact, then the actions.
            var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(root, false);
            divider.GetComponent<Image>().color = new Color(0.30f, 1f, 0.45f, 0.35f);
            Place(divider.GetComponent<RectTransform>(), 0.556f, 0.13f, 0.558f, ColumnsTop, 0f);

            detailText = MakeText(root, "Detail", 20, TextAlignmentOptions.TopLeft);
            Place(detailText.rectTransform, 0.57f, 0.22f, 0.98f, ColumnsTop, 4f);

            tagButton = MakeButton("TagButton", root, "TAG AS EVIDENCE", new Color(0.10f, 0.36f, 0.20f));
            Place(tagButton.GetComponent<RectTransform>(), 0.57f, 0.13f, 0.80f, 0.205f, 2f);
            tagButton.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
            tagButton.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Submit()));

            hintButton = MakeButton("HintButton", root, "HINT  (half points)", new Color(0.30f, 0.23f, 0.06f));
            Place(hintButton.GetComponent<RectTransform>(), 0.815f, 0.13f, 0.98f, 0.205f, 2f);
            hintButton.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
            hintButton.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Toggle()));

            feedbackText = MakeText(root, "Feedback", 20, TextAlignmentOptions.MidlineLeft);
            Place(feedbackText.rectTransform, 0.02f, 0.02f, 0.98f, 0.115f, 4f);

            card.SetActive(false);
        }

        // Top edge of the three columns, as a fraction of the card height.
        private const float ColumnsTop = 0.785f;

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
