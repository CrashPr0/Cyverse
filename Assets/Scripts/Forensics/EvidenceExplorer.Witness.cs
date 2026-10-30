using System.Collections.Generic;
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
    /// The witness phone as a second evidence source in the Explorer: a
    /// coworker's phone (Jamie Park), shared by consent. Picked from the left
    /// pane under OTHER SOURCES, the middle list shows the contacts and the
    /// right pane shows the selected conversation as chat bubbles, which
    /// scroll (mouse wheel, drag, or Page Up/Page Down). Optional: it backs up
    /// Case 2 from a witness's side and nothing on it is tagged.
    ///
    /// Actions: Choose(WitnessSourceIndex) opens it · Select(row, _) opens a
    /// contact · Navigate(±1) moves between contacts · Submit explains why
    /// nothing here is tagged.
    /// </summary>
    public sealed partial class EvidenceExplorer
    {
        private const float BubbleFont = 18f, BubblePad = 12f, ScrollStep = 120f;

        private static readonly Color OutBubble = new Color(0.20f, 0.40f, 0.90f, 1f);
        private static readonly Color InBubble = new Color(0.12f, 0.17f, 0.22f, 1f);
        private static readonly Color Nova = new Color(0.45f, 0.64f, 1f);

        private bool witnessMode, witnessView;
        private int witnessSelected = -1;
        private int conversationShown = -1;
        private List<WitnessContact> witnessContacts;
        private readonly List<int> witnessVisible = new List<int>();

        private Button witnessButton;
        private Image witnessMark;
        private TMP_Text witnessCount, detailPaneTitle, keyLegend, columnsText, conversationHeader;
        private GameObject witnessRoot;
        private ScrollRect chatScroll;
        private RectTransform chatContent;

        /// <summary>Choose index of the witness phone, one past the last category.</summary>
        public static int WitnessSourceIndex => PhoneImage.Categories.Length;
        public bool WitnessMode => open && witnessMode;
        public IReadOnlyList<WitnessContact> WitnessContacts =>
            witnessContacts ?? (witnessContacts = WitnessPhoneContent.Build());
        /// <summary>Contacts in the list right now (after the search filter).</summary>
        public int VisibleContactCount => witnessVisible.Count;
        public WitnessContact SelectedContact =>
            witnessSelected >= 0 && witnessSelected < witnessVisible.Count
                ? WitnessContacts[witnessVisible[witnessSelected]] : null;
        /// <summary>1 at the top of the conversation, 0 at the bottom.</summary>
        public float ConversationScrollPosition => chatScroll != null ? chatScroll.verticalNormalizedPosition : 1f;
        /// <summary>How far the open conversation can scroll, in canvas units.</summary>
        public float ConversationScrollRange => chatScroll == null || SelectedContact == null ? 0f
            : Mathf.Max(0f, chatContent.rect.height - ((RectTransform)chatScroll.viewport).rect.height);

        private bool TryApplyWitness(GameplayAction action)
        {
            switch (action.Kind)
            {
                case GameplayActionKind.Select:
                    if (action.Index < 0 || action.Index >= witnessVisible.Count) return false;
                    witnessSelected = action.Index;
                    RefreshList(keepSelection: true);
                    if (Sfx.Instance != null) Sfx.Instance.PlayClick();
                    return true;
                case GameplayActionKind.Navigate:
                    if (witnessVisible.Count == 0) return false;
                    witnessSelected = witnessSelected < 0
                        ? (action.Index > 0 ? 0 : witnessVisible.Count - 1)
                        : Mathf.Clamp(witnessSelected + action.Index, 0, witnessVisible.Count - 1);
                    RefreshList(keepSelection: true);
                    return true;
                case GameplayActionKind.Submit:
                    SetFeedback("<color=#E5A823>Witness messages back up the case, but they are not on the seized phone, " +
                        "so nothing here is tagged.</color> Pick a category above to tag evidence.", Ink);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Scroll the open conversation; +1 is down.</summary>
        public bool ScrollConversation(int direction)
        {
            float range = ConversationScrollRange;
            if (!WitnessMode || range <= 0f) return false;
            chatScroll.verticalNormalizedPosition =
                Mathf.Clamp01(chatScroll.verticalNormalizedPosition - direction * ScrollStep / range);
            return true;
        }

        private void UpdateWitnessKeys()
        {
            if (!witnessMode) return;
            if (Input.GetKeyDown(KeyCode.PageDown)) ScrollConversation(1);
            else if (Input.GetKeyDown(KeyCode.PageUp)) ScrollConversation(-1);
        }

        // ---- Rendering -------------------------------------------------------------

        private string ExamineSourceLine() =>
            $"<color=#8FB8CC>SOURCE</color>  Work phone of {Escape(phone.owner)}" +
            $"  ·  full file system  ·  {phone.artifacts.Count} artifacts" +
            "  ·  <color=#4CE087>image hash verified</color>  ·  read-only copy" + PassSummary();

        /// <summary>Swap the right pane and labels between artifact details and
        /// the conversation view.</summary>
        private void SetWitnessView(bool on)
        {
            // The source line always follows the mode: reopening the bench
            // resets it to the seized phone's line.
            sourceText.text = on
                ? $"<color=#8FB8CC>SOURCE</color>  Witness phone of {WitnessPhoneContent.OwnerName}" +
                  "  ·  shared by consent  ·  read only  ·  <color=#E5A823>backs up the case, not tagged</color>"
                : ExamineSourceLine();
            if (witnessView == on) return;
            witnessView = on;
            detailText.gameObject.SetActive(!on);
            tagButton.gameObject.SetActive(!on);
            hintButton.gameObject.SetActive(!on);
            witnessRoot.SetActive(on);
            detailPaneTitle.text = on ? "CONVERSATION" : "ARTIFACT DETAILS";
            keyLegend.text = on
                ? "<color=#8FB8CC>UP/DOWN CONTACT · PG UP/PG DN OR WHEEL SCROLLS · TYPE TO SEARCH</color>"
                : "<color=#8FB8CC>←/→ CATEGORY · UP/DOWN ITEM · TYPE TO SEARCH · ENTER TAG</color>";
            columnsText.text = on
                ? "<b><cspace=3><color=#607585>LAST<pos=28%>CONTACT</color></cspace></b>"
                : "<b><cspace=3><color=#607585>TIME<pos=28%>ITEM</color></cspace></b>";
            conversationShown = -1;
        }

        private void RefreshWitnessSource()
        {
            witnessCount.text = $"{WitnessContacts.Count}";
            witnessCount.color = witnessMode ? Green : Muted;
            witnessMark.enabled = witnessMode;
            UiMotion.ColorTo(witnessButton.GetComponent<Image>(), witnessMode ? CategoryActive : CategoryIdle);
        }

        private void RefreshWitnessList()
        {
            WitnessContact keep = SelectedContact;
            witnessVisible.Clear();
            for (int i = 0; i < WitnessContacts.Count; i++)
                if (ContactMatches(WitnessContacts[i], filter)) witnessVisible.Add(i);
            witnessSelected = -1;
            for (int i = 0; i < witnessVisible.Count; i++)
                if (WitnessContacts[witnessVisible[i]] == keep) witnessSelected = i;

            listHeader.text = $"WITNESS PHONE  <color=#607585>·</color>  " +
                $"<color=#D8ECF5>{witnessVisible.Count} CONTACT{(witnessVisible.Count == 1 ? "" : "S")}</color>";
            for (int i = 0; i < rowButtons.Count; i++)
            {
                Button row = rowButtons[i];
                bool used = i < witnessVisible.Count;
                row.gameObject.SetActive(used);
                if (!used) continue;
                WitnessContact c = WitnessContacts[witnessVisible[i]];
                WitnessMessage last = c.Last;
                string preview = last.IsSystem ? last.text : (last.outgoing ? "You: " : "") + last.text;
                row.GetComponentInChildren<TMP_Text>().text =
                    $"<color=#8FB8CC>{last.time}</color><pos=28%><b>{Escape(c.name)}</b>  <color=#8FB8CC>{Escape(preview)}</color>";
                UiMotion.ColorTo(row.GetComponent<Image>(), i == witnessSelected ? RowSelected
                    : i % 2 == 0 ? RowIdle : RowAlt);
                rowMarks[i].enabled = i == witnessSelected;
                rowMarks[i].color = Green;
            }
            RefreshConversation();
        }

        private static bool ContactMatches(WitnessContact c, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            string q = query.Trim();
            if (Has(c.name, q) || Has(c.detail, q)) return true;
            foreach (WitnessMessage m in c.messages)
                if (Has(m.text, q) || Has(m.sender, q)) return true;
            return false;
        }

        private static bool Has(string text, string query) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private void RefreshConversation()
        {
            WitnessContact contact = SelectedContact;
            int index = contact != null ? witnessVisible[witnessSelected] : -1;
            if (index == conversationShown) return;
            conversationShown = index;

            for (int i = chatContent.childCount - 1; i >= 0; i--)
                Destroy(chatContent.GetChild(i).gameObject);
            if (contact == null)
            {
                conversationHeader.text = "<color=#607585>Select a contact to read the conversation.</color>";
                chatContent.sizeDelta = Vector2.zero;
                return;
            }
            conversationHeader.text = $"<b>{Escape(contact.name)}</b>   <size=16><color=#8FB8CC>{Escape(contact.detail)}</color></size>";
            FillConversation(contact);
            // Start at the beginning so the story reads in order.
            chatScroll.verticalNormalizedPosition = 1f;
            UiMotion.FadeIn(chatScroll.gameObject, UiMotion.Fast);
        }

        /// <summary>Lays the bubbles out by hand (measured with TMP) so the
        /// height is exact without nested layout groups.</summary>
        private void FillConversation(WitnessContact contact)
        {
            float width = ((RectTransform)chatScroll.viewport).rect.width;
            if (width <= 0f) width = 700f;
            float bubbleMax = width * 0.7f;
            float y = -10f;
            string day = null;
            foreach (WitnessMessage m in contact.messages)
            {
                string date = m.time.Substring(0, 5);
                if (date != day)
                {
                    day = date;
                    y -= 4f;
                    TMP_Text stamp = MakeText(chatContent, "Day", 14, TextAlignmentOptions.Center);
                    stamp.color = Muted;
                    stamp.text = "<b>" + DayLabel(date) + "</b>";
                    TopLeft(stamp.rectTransform, 0f, y, width, 22f);
                    y -= 28f;
                }

                if (m.IsSystem)
                {
                    TMP_Text note = MakeText(chatContent, "System", 15, TextAlignmentOptions.Center);
                    note.color = new Color(1f, 0.62f, 0.45f);
                    note.text = "<i>" + Escape(m.text) + "</i>";
                    TopLeft(note.rectTransform, 0f, y, width, 22f);
                    y -= 30f;
                    continue;
                }

                if (contact.group && !m.outgoing && !string.IsNullOrEmpty(m.sender))
                {
                    TMP_Text who = MakeText(chatContent, "Sender", 14, TextAlignmentOptions.Left);
                    who.color = Nova;
                    who.text = "<b>" + Escape(m.sender) + "</b>";
                    TopLeft(who.rectTransform, 18f, y, bubbleMax, 20f);
                    y -= 20f;
                }

                TMP_Text body = MakeText(chatContent, "Text", (int)BubbleFont, TextAlignmentOptions.TopLeft);
                body.text = Escape(m.text);
                body.color = Color.white;
                body.overflowMode = TextOverflowModes.Overflow;
                // TMP can measure 0 when no font atlas is rendered (batch mode),
                // so never go below an estimate from the character count.
                float estimateW = Mathf.Min(bubbleMax, m.text.Length * BubbleFont * 0.5f);
                Vector2 pref = body.GetPreferredValues(body.text, bubbleMax, 0f);
                float textW = Mathf.Min(bubbleMax, Mathf.Max(Mathf.Ceil(pref.x) + 2f, pref.x > 0f ? 0f : estimateW));
                int estimateLines = Mathf.Max(1, Mathf.CeilToInt(m.text.Length * BubbleFont * 0.5f / textW));
                float textH = Mathf.Max(Mathf.Ceil(body.GetPreferredValues(body.text, textW, 0f).y),
                    estimateLines * BubbleFont * 1.25f);
                float bubbleW = textW + BubblePad * 2f, bubbleH = textH + BubblePad * 1.4f;
                float x = m.outgoing ? width - 16f - bubbleW : 16f;

                Image bubble = PhoneScreenUi.RoundedImage("Bubble", chatContent, m.outgoing ? OutBubble : InBubble, 14f);
                TopLeft(bubble.rectTransform, x, y, bubbleW, bubbleH);
                body.transform.SetParent(bubble.transform, false);
                Place(body.rectTransform, 0f, 0f, 1f, 1f, 0f);
                body.rectTransform.offsetMin = new Vector2(BubblePad, BubblePad * 0.7f);
                body.rectTransform.offsetMax = new Vector2(-BubblePad, -BubblePad * 0.7f);
                y -= bubbleH + 3f;

                TMP_Text time = MakeText(chatContent, "Time", 12, m.outgoing ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
                time.color = new Color(0.50f, 0.57f, 0.68f);
                time.text = m.time.Substring(6);
                TopLeft(time.rectTransform, m.outgoing ? width - 16f - 120f : 18f, y, 120f, 16f);
                y -= 22f;
            }
            chatContent.sizeDelta = new Vector2(0f, -y + 10f);
        }

        private static string DayLabel(string monthDay)
        {
            switch (monthDay)
            {
                case "07-13": return "SUN 07-13";
                case "07-14": return "MON 07-14";
                case "07-15": return "TUE 07-15";
                case "07-16": return "WED 07-16";
                default: return monthDay;
            }
        }

        /// <summary>Top-anchored rect at x, y (y negative, down from the top).</summary>
        private static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        // ---- Construction ----------------------------------------------------

        /// <summary>The OTHER SOURCES entry under the categories.</summary>
        private void BuildWitnessSource(Transform root, float top)
        {
            TMP_Text other = MakeText(root, "OtherSources", 13, TextAlignmentOptions.BottomLeft);
            other.text = "<b><cspace=4>OTHER SOURCES</cspace></b>";
            other.color = Muted;
            other.enableWordWrapping = false;
            Place(other.rectTransform, 0.018f, top, 0.199f, top + 0.026f, 0f);
            Inset(other.rectTransform, 8f, 4f);

            float buttonTop = top - 0.002f;
            witnessButton = MakeButton("WitnessPhone", root, "WITNESS PHONE", CategoryIdle);
            Place(witnessButton.GetComponent<RectTransform>(), 0.018f, buttonTop - CategoryHeight, 0.199f, buttonTop, 0f);
            TMP_Text label = witnessButton.GetComponentInChildren<TMP_Text>();
            label.fontSize = 19;
            label.fontStyle = FontStyles.Bold;
            label.rectTransform.offsetMax = new Vector2(-54f, -2f);
            witnessCount = MakeText(witnessButton.transform, "Count", 18, TextAlignmentOptions.MidlineRight);
            witnessCount.enableWordWrapping = false;
            Place(witnessCount.rectTransform, 0.6f, 0f, 1f, 1f, 0f);
            Inset(witnessCount.rectTransform, 0f, 12f);
            witnessMark = Edge(witnessButton.transform, Green, 5f);
            witnessButton.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Choose(WitnessSourceIndex)));
            UiMotion.AddPress(witnessButton, row: true);
        }

        /// <summary>The conversation view, in the same place as the details text.</summary>
        private void BuildWitnessPane(Transform root)
        {
            witnessRoot = new GameObject("WitnessConversation", typeof(RectTransform));
            witnessRoot.transform.SetParent(root, false);
            Place(witnessRoot.GetComponent<RectTransform>(), 0.565f, ColumnsBottom, 0.988f, ColumnsTop - PaneHeaderH, 0f);
            Transform pane = witnessRoot.transform;

            conversationHeader = MakeText(pane, "Contact", 22, TextAlignmentOptions.MidlineLeft);
            conversationHeader.enableWordWrapping = false;
            conversationHeader.overflowMode = TextOverflowModes.Ellipsis;
            var hr = conversationHeader.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
            hr.sizeDelta = new Vector2(-40f, 48f); hr.anchoredPosition = new Vector2(0f, -6f);
            Image rule = Band(pane, "ContactRule", Line, 0f, 1f, 1f, 1f, false);
            rule.rectTransform.offsetMin = new Vector2(14f, -56f);
            rule.rectTransform.offsetMax = new Vector2(-14f, -55f);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(pane, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            Place(scrollRt, 0f, 0f, 1f, 1f, 0f);
            scrollRt.offsetMin = new Vector2(8f, 10f);
            scrollRt.offsetMax = new Vector2(-8f, -60f);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportGo.transform.SetParent(scrollRt, false);
            var viewport = (RectTransform)viewportGo.transform;
            Place(viewport, 0f, 0f, 1f, 1f, 0f);
            viewport.offsetMax = new Vector2(-10f, 0f);
            // A clear graphic so the wheel and drag land on the scroll view
            // anywhere, not only on the bubbles.
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            chatContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            chatContent.SetParent(viewport, false);
            chatContent.anchorMin = new Vector2(0f, 1f); chatContent.anchorMax = new Vector2(1f, 1f);
            chatContent.pivot = new Vector2(0.5f, 1f);
            chatContent.sizeDelta = Vector2.zero;

            Image track = Band(scrollRt, "ScrollTrack", new Color(1f, 1f, 1f, 0.05f), 1f, 0f, 1f, 1f, false);
            var tr = track.rectTransform;
            tr.pivot = new Vector2(1f, 0.5f);
            tr.offsetMin = new Vector2(-5f, 4f);
            tr.offsetMax = new Vector2(0f, -4f);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            var area = new GameObject("Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(tr, false);
            Place(area, 0f, 0f, 1f, 1f, 0f);
            Image handle = PhoneScreenUi.RoundedImage("Handle", area, new Color(1f, 1f, 1f, 0.35f), 2.5f);
            Place(handle.rectTransform, 0f, 0f, 1f, 1f, 0f);
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;

            chatScroll = scrollGo.AddComponent<ScrollRect>();
            chatScroll.viewport = viewport;
            chatScroll.content = chatContent;
            chatScroll.horizontal = false;
            chatScroll.vertical = true;
            chatScroll.movementType = ScrollRect.MovementType.Clamped;
            chatScroll.scrollSensitivity = 30f;
            chatScroll.verticalScrollbar = bar;
            chatScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            witnessRoot.SetActive(false);
        }
    }
}
