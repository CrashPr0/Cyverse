using System;
using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using UnityEngine.UI;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The Audit Hunt's full-screen log: one round's entries as a table
    /// (# · TIME · ACCOUNT · ACTION · RESOURCE / LOCATION), a highlighted
    /// selection, a feedback line and a row of key caps saying how to play.
    /// Display only — AuditStation owns the rules and the input.
    /// </summary>
    public sealed class AuditLogPanel
    {
        public enum RowState { Normal, Wrong, Correct }

        private const int RowCount = 6;
        private const float RowHeight = 70f;
        private const float TableTop = -186f;
        // Tall enough for the title, brief, table, a two-line feedback note
        // and the key row, and no taller: a screen-height card left a large
        // empty band above the keys.
        private const float CardHeight = 772f;
        // Column edges as fractions of the card width.
        private static readonly float[] ColX = { 0.03f, 0.075f, 0.175f, 0.40f, 0.575f, 0.97f };
        private static readonly string[] Headings = { "#", "TIME", "ACCOUNT", "ACTION", "RESOURCE / LOCATION" };

        private static readonly Color Ink = new Color(0.86f, 0.93f, 1f);
        private static readonly Color Muted = new Color(0.58f, 0.68f, 0.78f);
        private static readonly Color Good = new Color(0.30f, 1f, 0.45f);
        private static readonly Color Bad = new Color(1f, 0.45f, 0.35f);

        private readonly Color accent;
        private GameObject screen, card;
        private Text roundText, feedbackText;
        private readonly Image[] rowBacks = new Image[RowCount];
        private readonly Text[,] cells = new Text[RowCount, 5];
        private readonly Text[] markers = new Text[RowCount];
        private readonly RowState[] states = new RowState[RowCount];
        private int rowsInUse, cursor;

        public AuditLogPanel(Color accent) { this.accent = accent; }

        public bool IsVisible => screen != null && screen.activeSelf;

        public void Show()
        {
            if (screen == null) Build();
            if (screen == null) return;
            screen.transform.SetAsLastSibling();
            screen.SetActive(true);
        }

        public void Hide()
        {
            if (screen != null) screen.SetActive(false);
        }

        public void Destroy()
        {
            if (screen != null) UnityEngine.Object.Destroy(screen);
            screen = null;
            card = null;
        }

        /// <summary>Fill the table with one round's entries.</summary>
        public void SetRound(int round, int rounds, IList<string> lines)
        {
            if (card == null) Build();
            if (card == null) return;
            roundText.text = $"ROUND {round} OF {rounds}";
            rowsInUse = Mathf.Min(lines.Count, RowCount);
            for (int i = 0; i < RowCount; i++)
            {
                string[] columns = i < rowsInUse ? Columns(lines[i]) : new[] { "", "", "", "" };
                cells[i, 0].text = i < rowsInUse ? (i + 1).ToString() : "";
                for (int c = 0; c < 4; c++) cells[i, c + 1].text = columns[c];
                states[i] = RowState.Normal;
                rowBacks[i].gameObject.SetActive(i < rowsInUse);
            }
            Refresh();
        }

        public void SetCursor(int row)
        {
            cursor = row;
            Refresh();
        }

        public void SetRowState(int row, RowState state)
        {
            if (row < 0 || row >= RowCount) return;
            states[row] = state;
            Refresh();
        }

        public void SetFeedback(string richText)
        {
            if (feedbackText != null) feedbackText.text = richText ?? "";
        }

        public void SetTitleDone()
        {
            if (roundText != null) roundText.text = "AUDIT CLEAR";
        }

        private void Refresh()
        {
            for (int i = 0; i < RowCount; i++)
            {
                bool selected = i == cursor && i < rowsInUse;
                Color text = states[i] == RowState.Correct ? Good
                           : states[i] == RowState.Wrong ? Bad
                           : selected ? Color.white : Ink;
                rowBacks[i].color = states[i] == RowState.Correct ? new Color(0.08f, 0.30f, 0.16f, 0.95f)
                                  : states[i] == RowState.Wrong ? new Color(0.36f, 0.10f, 0.08f, 0.95f)
                                  : selected ? new Color(accent.r * 0.32f, accent.g * 0.32f, accent.b * 0.32f, 0.95f)
                                  : new Color(0.04f, 0.07f, 0.10f, 0.9f);
                markers[i].text = selected ? "▶" : "";
                for (int c = 0; c < 5; c++)
                {
                    cells[i, c].color = c == 0 && !selected && states[i] == RowState.Normal ? Muted : text;
                    cells[i, c].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                }
            }
        }

        /// <summary>"08:59  r.alvarez  view  ·  HR/records" → time, account,
        /// action, resource. Anything that doesn't parse shows whole in the
        /// last column rather than being dropped.</summary>
        private static string[] Columns(string line)
        {
            var parts = new List<string>();
            foreach (string piece in line.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries))
            {
                string p = piece.Trim();
                if (p.Length == 0 || p == "·") continue;
                parts.Add(p);
            }
            return parts.Count == 4 ? parts.ToArray() : new[] { "", "", "", line.Trim() };
        }

        // ---- Construction ----------------------------------------------------

        private void Build()
        {
            if (HudUI.Instance == null) return;
            // A dark backdrop over the whole screen, so the room and its
            // signs drop away behind the log.
            screen = new GameObject("AuditLogPanel", typeof(RectTransform), typeof(Image));
            screen.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            var srt = screen.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            screen.GetComponent<Image>().color = new Color(0f, 0.01f, 0.02f, 0.92f);

            card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(screen.transform, false);
            var rt = card.GetComponent<RectTransform>();
            // Clear of the objective bar above and the task list to the left;
            // hangs from the top at a fixed height that fits its content.
            rt.anchorMin = new Vector2(0.11f, 0.88f);
            rt.anchorMax = new Vector2(0.89f, 0.88f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, CardHeight);
            HudUI.StylePanel(card, new Color(0.012f, 0.03f, 0.05f, 0.985f), accent);

            Text title = MakeText("Title", card.transform, 34, TextAnchor.MiddleLeft);
            title.text = "ACCESS AUDIT LOG";
            title.fontStyle = FontStyle.Bold;
            title.color = accent;
            Place(title.rectTransform, 0f, 1f, 40f, -22f, -300f, 46f);

            roundText = MakeText("Round", card.transform, 24, TextAnchor.MiddleRight);
            roundText.fontStyle = FontStyle.Bold;
            roundText.color = Muted;
            Place(roundText.rectTransform, 0.6f, 1f, 0f, -24f, -40f, 42f);

            Text brief = MakeText("Brief", card.transform, 22, TextAnchor.UpperLeft);
            brief.text = "Check each entry: <b>WHO</b> did <b>WHAT</b>, <b>WHEN</b>, and from <b>WHERE</b>. " +
                         "One of them doesn't belong. Select it and flag it.";
            brief.color = new Color(0.80f, 0.88f, 0.94f);
            Place(brief.rectTransform, 0f, 1f, 40f, -78f, -80f, 60f);

            // Column headings.
            var header = new GameObject("TableHeader", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(card.transform, false);
            header.GetComponent<Image>().color = new Color(0.06f, 0.13f, 0.18f, 1f);
            PlaceRow(header.GetComponent<RectTransform>(), TableTop + 44f, 40f);
            for (int c = 0; c < Headings.Length; c++)
            {
                Text h = MakeText("Head_" + c, card.transform, 18, TextAnchor.MiddleLeft);
                h.text = Headings[c];
                h.fontStyle = FontStyle.Bold;
                h.color = Muted;
                PlaceCell(h.rectTransform, c, TableTop + 44f, 40f);
            }

            for (int i = 0; i < RowCount; i++)
            {
                float top = TableTop - i * RowHeight;
                var back = new GameObject("Row_" + i, typeof(RectTransform), typeof(Image));
                back.transform.SetParent(card.transform, false);
                rowBacks[i] = back.GetComponent<Image>();
                PlaceRow(back.GetComponent<RectTransform>(), top, RowHeight - 8f);

                markers[i] = MakeText("Marker_" + i, card.transform, 24, TextAnchor.MiddleCenter);
                markers[i].color = accent;
                var mrt = markers[i].rectTransform;
                mrt.anchorMin = new Vector2(0f, 1f);
                mrt.anchorMax = new Vector2(ColX[0], 1f);
                mrt.pivot = new Vector2(0f, 1f);
                mrt.anchoredPosition = new Vector2(2f, top);
                mrt.sizeDelta = new Vector2(-2f, RowHeight - 8f);

                for (int c = 0; c < 5; c++)
                {
                    cells[i, c] = MakeText($"Cell_{i}_{c}", card.transform, 27, TextAnchor.MiddleLeft);
                    PlaceCell(cells[i, c].rectTransform, c, top, RowHeight - 8f);
                }
            }

            feedbackText = MakeText("Feedback", card.transform, 22, TextAnchor.MiddleCenter);
            feedbackText.color = Ink;
            Place(feedbackText.rectTransform, 0f, 1f, 40f, TableTop - RowCount * RowHeight - 6f, -80f, 64f);

            BuildControls();
            screen.SetActive(false);
        }

        /// <summary>Key caps along the bottom: how to move, flag and close.
        /// One cap each; Enter (flag) and Tab (close) still work unlisted.</summary>
        private void BuildControls()
        {
            var bar = new GameObject("Controls", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bar.transform.SetParent(card.transform, false);
            var brt = bar.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 26f);
            brt.sizeDelta = new Vector2(-80f, 52f);
            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Key(bar.transform, "↑ ↓ ← →");
            Word(bar.transform, "MOVE", Ink, 90f);
            Spacer(bar.transform, 48f);
            Key(bar.transform, "E");
            Word(bar.transform, "FLAG", Ink, 80f);
            Spacer(bar.transform, 48f);
            Key(bar.transform, "ESC");
            Word(bar.transform, "CLOSE", Ink, 90f);
        }

        private void Key(Transform parent, string label)
        {
            var cap = new GameObject("Key_" + label, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            cap.transform.SetParent(parent, false);
            cap.GetComponent<Image>().color = new Color(0.10f, 0.16f, 0.22f, 1f);
            var outline = cap.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            Text t = MakeText("Label", cap.transform, 22, TextAnchor.MiddleCenter);
            t.text = label;
            t.fontStyle = FontStyle.Bold;
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            var le = cap.GetComponent<LayoutElement>();
            le.preferredWidth = Mathf.Max(46f, t.preferredWidth + 26f);
            le.preferredHeight = 44f;
        }

        private static void Word(Transform parent, string word, Color color, float width)
        {
            Text t = MakeText("Word_" + word, parent, 22, TextAnchor.MiddleCenter);
            t.text = word;
            t.color = color;
            t.fontStyle = color == Muted ? FontStyle.Normal : FontStyle.Bold;
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = Mathf.Max(width, t.preferredWidth + 8f);
            le.preferredHeight = 44f;
        }

        private static void Spacer(Transform parent, float width)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = width;
        }

        private static void PlaceRow(RectTransform rt, float top, float height)
        {
            rt.anchorMin = new Vector2(ColX[0] - 0.012f, 1f);
            rt.anchorMax = new Vector2(ColX[ColX.Length - 1] + 0.005f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, top);
            rt.sizeDelta = new Vector2(0f, height);
        }

        private static void PlaceCell(RectTransform rt, int column, float top, float height)
        {
            rt.anchorMin = new Vector2(ColX[column], 1f);
            rt.anchorMax = new Vector2(ColX[column + 1], 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, top);
            rt.sizeDelta = new Vector2(-16f, height);
        }

        /// <summary>A stretch-anchored strip from x-anchor <paramref name="ax0"/>
        /// to <paramref name="ax1"/>, offset from the card's top.</summary>
        private static void Place(RectTransform rt, float ax0, float ax1, float left, float top, float rightInset, float height)
        {
            rt.anchorMin = new Vector2(ax0, 1f);
            rt.anchorMax = new Vector2(ax1, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(left, top);
            rt.sizeDelta = new Vector2(rightInset - left, height);
        }

        private static Text MakeText(string name, Transform parent, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = HudUI.UIFont;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Ink;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }
    }
}
