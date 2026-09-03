using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Forensics
{
    /// <summary>
    /// The full-screen forensic terminal (the KC7 experience in-engine).
    /// A modal, keyboard-only console: type mini-KQL queries against the log
    /// database, read the results, and submit findings with `answer <value>`.
    /// Commands: help · tables · fields <table> · hint · answer <x> · case ·
    /// clear. Esc steps away (progress lives in the InvestigationCase, so
    /// reopening resumes). Owns the screen via GameState.QuizActive per the
    /// one-menu-at-a-time standard; ↑/↓ cycle command history.
    /// </summary>
    public class QueryTerminal : MonoBehaviour
    {
        public static QueryTerminal Instance { get; private set; }

        public int maxInput = 90;

        private LogDatabase db;
        private InvestigationCase activeCase;
        private string caseClosedNote;

        private GameObject card;
        private TMP_Text outputText, sidebarText, inputText, titleText;
        private string typed = "";
        private bool open;

        private readonly List<string> history = new List<string>();
        private int historyIndex = -1;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public void Open(LogDatabase database, InvestigationCase investigation, string closedNote = null)
        {
            if (open || GameState.AnyMenuOpen) return;
            if (card == null) Build();

            db = database;
            activeCase = investigation;
            caseClosedNote = closedNote;
            typed = "";
            open = true;
            GameState.QuizActive = true;
            GameState.MenuTransitionFrame = Time.frameCount;

            string evidenceHeader = EvidenceHeader();
            titleText.text = $"CYVERSE FORENSIC TERMINAL  //  {activeCase.title}";
            PrintBlock((activeCase.IsComplete
                ? "<color=#E5A823>Case closed. Review the logs freely, or Esc to step away.</color>"
                : "Type  <color=#5BC8FF>help</color>  for commands, or start with the example under your current question.") +
                evidenceHeader);
            RefreshSidebar();
            card.SetActive(true);
        }

        void Update()
        {
            if (!open) return;
            if (Time.frameCount == GameState.MenuTransitionFrame) return;

            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }

            if (Input.GetKeyDown(KeyCode.Tab)) TabComplete();

            // Command history.
            if (Input.GetKeyDown(KeyCode.UpArrow) && history.Count > 0)
            {
                historyIndex = historyIndex < 0 ? history.Count - 1 : Mathf.Max(0, historyIndex - 1);
                typed = history[historyIndex];
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) && historyIndex >= 0)
            {
                historyIndex++;
                if (historyIndex >= history.Count) { historyIndex = -1; typed = ""; }
                else typed = history[historyIndex];
            }

            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
                }
                else if (c == '\n' || c == '\r')
                {
                    Submit();
                    break;
                }
                else if (!char.IsControl(c) && typed.Length < maxInput)
                {
                    typed += c;
                }
            }
            RefreshInput();
        }

        private void Close()
        {
            open = false;
            card.SetActive(false);
            GameState.QuizActive = false;
            GameState.MenuTransitionFrame = Time.frameCount;
        }

        /// <summary>Tab-completes the last token of the input against table
        /// names, column names, operators, and commands. Unique match →
        /// completed; several matches → shows them without losing the input.</summary>
        private void TabComplete()
        {
            int start = typed.Length;
            while (start > 0 && !char.IsWhiteSpace(typed[start - 1]) && typed[start - 1] != '|') start--;
            string partial = typed.Substring(start);
            if (partial.Length == 0) return;

            var candidates = new List<string>();
            void Consider(string word)
            {
                if (word.StartsWith(partial, System.StringComparison.OrdinalIgnoreCase) &&
                    !candidates.Contains(word)) candidates.Add(word);
            }

            foreach (var t in db.tables)
            {
                Consider(t.name);
                foreach (var c in t.columns) Consider(c);
            }
            foreach (var w in new[] { "where", "project", "distinct", "summarize", "sort", "take",
                                      "count", "by", "contains", "desc", "answer", "tables",
                                      "fields", "hint", "help", "clear", "case" })
                Consider(w);

            if (candidates.Count == 1)
            {
                typed = typed.Substring(0, start) + candidates[0];
            }
            else if (candidates.Count > 1)
            {
                // Extend to the longest common prefix, then show the options.
                string common = candidates[0];
                foreach (var c in candidates)
                {
                    int n = 0;
                    while (n < common.Length && n < c.Length &&
                           char.ToLowerInvariant(common[n]) == char.ToLowerInvariant(c[n])) n++;
                    common = common.Substring(0, n);
                }
                if (common.Length > partial.Length)
                    typed = typed.Substring(0, start) + common;
                PrintBlock($"<color=#8FB8CC>Tab:</color> {Escape(string.Join("   ", candidates))}");
            }
        }

        // ---- Command handling ------------------------------------------------

        private void Submit()
        {
            string line = typed.Trim();
            typed = "";
            historyIndex = -1;
            if (line.Length == 0) return;

            history.Add(line);
            if (history.Count > 30) history.RemoveAt(0);

            string lower = line.ToLowerInvariant();
            string echo = $"<color=#5BC8FF>> {Escape(line)}</color>\n\n";

            if (lower == "help") { PrintBlock(echo + HelpText()); return; }
            if (lower == "clear") { PrintBlock(""); return; }
            if (lower == "tables") { PrintBlock(echo + TablesText()); return; }
            if (lower == "case") { PrintBlock(echo + CaseText()); return; }
            if (lower == "hint") { DoHint(echo); return; }
            if (lower.StartsWith("fields"))
            {
                string arg = line.Length > 6 ? line.Substring(6).Trim() : "";
                var t = arg.Length > 0 ? db.Find(arg) : null;
                PrintBlock(echo + (t == null
                    ? "<color=#FFB347>fields needs a table name, e.g.  fields Email</color>"
                    : $"<b>{t.name}</b> columns:  {string.Join(", ", t.columns)}   ({t.rows.Count} rows)"));
                return;
            }
            if (lower.StartsWith("answer"))
            {
                DoAnswer(echo, line.Length > 6 ? line.Substring(6).Trim() : "");
                return;
            }

            // Everything else is a query.
            var result = MiniKql.Run(db, line);
            PrintBlock(echo + Render(result));
            if (result.error == null && Sfx.Instance != null) Sfx.Instance.PlayClick();
        }

        private void DoHint(string echo)
        {
            var q = activeCase.Current;
            if (q == null) { PrintBlock(echo + "Case closed — no more hints needed."); return; }
            q.HintUsed = true;
            PrintBlock(echo +
                $"<color=#E5A823>HINT (halves this question's points):</color> {q.hint}\n\n" +
                $"Try:  <color=#5BC8FF>{Escape(q.exampleQuery)}</color>");
            RefreshSidebar();
        }

        private void DoAnswer(string echo, string given)
        {
            var q = activeCase.Current;
            if (q == null) { PrintBlock(echo + "Case already closed."); return; }
            if (given.Length == 0)
            {
                PrintBlock(echo + "<color=#FFB347>answer needs a value, e.g.  answer 42</color>");
                return;
            }

            if (q.Matches(given))
            {
                q.Answered = true;

                // Score: full points first try without hint; hint halves;
                // an earlier wrong attempt drops it to a third.
                int award = q.points;
                if (q.HintUsed) award /= 2;
                if (q.AttemptedWrong) award = Mathf.Max(20, q.points / 3);
                ScoreSystem.QuizTotal++;
                if (!q.AttemptedWrong)
                {
                    ScoreSystem.QuizCorrect++;
                    ScoreSystem.Streak++;
                    if (ScoreSystem.Streak > ScoreSystem.BestStreak) ScoreSystem.BestStreak = ScoreSystem.Streak;
                }
                else
                {
                    ScoreSystem.Streak = 0;
                }
                ScoreSystem.Add(award);

                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                string next = activeCase.IsComplete
                    ? "\n\n<color=#E5A823>" + (caseClosedNote ??
                        "<b>CASE CLOSED.</b> Outstanding work, analyst. Esc to step away — your results are waiting.") + "</color>"
                    : $"\n\n<color=#8FB8CC>Next question is up on the case file →</color>";
                PrintBlock(echo + $"<color=#4CE087><b>CORRECT</b>  +{award} points</color>{next}");

                activeCase.NotifyAnswered();
                RefreshSidebar();
            }
            else
            {
                q.AttemptedWrong = true;
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                PrintBlock(echo + "<color=#FF8866><b>Not what the evidence says.</b></color>  Re-check your query — or type  <color=#5BC8FF>hint</color>.");
            }
        }

        // ---- Rendering -------------------------------------------------------

        private string Render(QueryResult r)
        {
            if (r.error != null) return $"<color=#FFB347>{Escape(r.error)}</color>";
            if (r.isScalar) return $"<b><size=30>{r.scalar}</size></b>   <color=#8FB8CC>(row count)</color>";

            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#8FB8CC>").Append(Escape(string.Join("   ·   ", r.headers))).Append("</color>\n");
            sb.Append("<color=#33505F>――――――――――――――――――――――――――――――――――――</color>\n");

            const int maxRows = 13;
            int shown = Mathf.Min(maxRows, r.rows.Count);
            for (int i = 0; i < shown; i++)
                sb.Append(Escape(string.Join("   ·   ", r.rows[i]))).Append('\n');

            if (r.rows.Count > maxRows)
                sb.Append($"<color=#8FB8CC>… {r.rows.Count - maxRows} more rows — filter with | where, or | take {r.rows.Count}</color>\n");
            if (r.rows.Count == 0)
                sb.Append("<color=#8FB8CC>(no rows matched)</color>\n");
            return sb.ToString();
        }

        private string HelpText() =>
            "<b>QUERIES</b> — pipe steps together, KQL-style:\n" +
            "  Email | where sender == \"x\" | count\n" +
            "  WebVisits | where url contains \"word\"\n" +
            "  Email | project sender, subject | take 5\n" +
            "  DnsLookups | distinct domain\n" +
            "  LogonEvents | summarize count by employee\n" +
            "  FileAccess | sort by timestamp desc\n" +
            "  TAB autocompletes tables, columns and operators.\n\n" +
            "<b>COMMANDS</b>\n" +
            "  tables            list the log tables\n" +
            "  fields Email      a table's columns\n" +
            "  case              reprint the current question\n" +
            "  hint              a nudge (halves the points)\n" +
            "  answer 42         submit your finding\n" +
            "  clear             wipe the screen";

        private string TablesText()
        {
            var sb = new System.Text.StringBuilder("<b>LOG TABLES</b>\n");
            foreach (var t in db.tables)
                sb.Append($"  <color=#5BC8FF>{t.name}</color>  —  {string.Join(", ", t.columns)}  ({t.rows.Count} rows)\n");
            return sb.ToString();
        }

        private string CaseText()
        {
            var q = activeCase.Current;
            return q == null
                ? "<color=#E5A823>Case closed.</color>"
                : $"<b>QUESTION {activeCase.CurrentIndex + 1}/{activeCase.questions.Length}</b>\n{Escape(q.prompt)}";
        }

        private void PrintBlock(string content)
        {
            outputText.text = content;
        }

        private void RefreshSidebar()
        {
            var sb = new System.Text.StringBuilder();
            if (SocProgress.TryGetEvidence(out var evidence))
            {
                sb.Append("<color=#4CE087><b>SOC HANDOFF  ✓ VERIFIED</b></color>\n")
                  .Append($"<size=17>{Escape(evidence.computer)}  ·  {Escape(evidence.user)}\n")
                  .Append($"{Escape(evidence.alertTitle)}\n")
                  .Append("DISK IMAGE + CUSTODY RECORD</size>\n\n");
            }
            else
            {
                sb.Append("<color=#FF8866><b>SOC HANDOFF  MISSING</b></color>\n\n");
            }

            sb.Append($"<b>CASE FILE</b>   {activeCase.AnsweredCount}/{activeCase.questions.Length} solved\n\n");
            var q = activeCase.Current;
            if (q != null)
            {
                sb.Append($"<color=#5BC8FF><b>Q{activeCase.CurrentIndex + 1}:</b></color> {Escape(q.prompt)}\n\n");
                if (q.HintUsed) sb.Append($"<color=#E5A823>Hint:</color> <size=18>{Escape(q.hint)}</size>\n\n");
            }
            else
            {
                sb.Append("<color=#E5A823><b>CASE CLOSED ✓</b></color>\n\n");
            }
            sb.Append("<color=#8FB8CC><size=18>");
            for (int i = 0; i < activeCase.questions.Length; i++)
                sb.Append(activeCase.questions[i].Answered ? "<color=#4CE087>■</color>" : "□").Append(' ');
            sb.Append("</size></color>\n\n");
            sb.Append($"<color=#E5A823>SCORE: {ScoreSystem.Score}</color>");
            if (ScoreSystem.Streak >= 2)
                sb.Append($"   <color=#4CE087>streak x{ScoreSystem.Streak}</color>");
            sb.Append("\n\n<size=17><color=#607585>help · tables · fields ‹t›\nhint · answer ‹x› · clear · TAB\nEsc steps away</color></size>");
            sidebarText.text = sb.ToString();
        }

        private static string EvidenceHeader()
        {
            if (!SocProgress.TryGetEvidence(out var evidence))
                return "\n\n<color=#FF8866>SOC evidence package unavailable. Query training data remains accessible.</color>";

            return "\n\n<color=#4CE087><b>EVIDENCE MOUNTED:</b></color> " +
                $"{Escape(evidence.computer)} disk image  ·  SHA-256 verified  ·  chain of custody intact";
        }

        private void RefreshInput()
        {
            bool blink = Mathf.Sin(Time.unscaledTime * 6f) > 0f;
            inputText.text = $"<color=#4CE087>kql></color> {Escape(typed)}{(blink ? "<color=#4CE087>_</color>" : " ")}";
        }

        void LateUpdate()
        {
            if (open) RefreshInput();
        }

        private static string Escape(string s) => s.Replace("<", "‹").Replace(">", "›");

        // ---- Construction ----------------------------------------------------

        private void Build()
        {
            var canvas = HudUI.Instance != null ? HudUI.Instance.Canvas.transform : null;
            card = new GameObject("ForensicTerminal", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(canvas, false);
            var rt = card.GetComponent<RectTransform>();
            // Stretch inside safe margins so the terminal remains usable in a
            // non-maximized WebGL canvas instead of assuming 1560x860 pixels.
            rt.anchorMin = new Vector2(0.035f, 0.055f);
            rt.anchorMax = new Vector2(0.965f, 0.945f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            HudUI.StylePanel(card, new Color(0.008f, 0.025f, 0.04f, 0.98f), new Color(0.30f, 1f, 0.45f));

            titleText = MakeText(card.transform, "Title", 26, TextAlignmentOptions.MidlineLeft);
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(0.30f, 1f, 0.45f);
            var trt = titleText.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(-60, 48); trt.anchoredPosition = new Vector2(0, -12);

            outputText = MakeText(card.transform, "Output", 21, TextAlignmentOptions.TopLeft);
            var ort = outputText.rectTransform;
            ort.anchorMin = new Vector2(0f, 0f); ort.anchorMax = new Vector2(0.70f, 1f);
            ort.offsetMin = new Vector2(30, 84); ort.offsetMax = new Vector2(-18, -66);

            sidebarText = MakeText(card.transform, "Sidebar", 20, TextAlignmentOptions.TopLeft);
            var srt = sidebarText.rectTransform;
            srt.anchorMin = new Vector2(0.70f, 0); srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = new Vector2(18, 84); srt.offsetMax = new Vector2(-24, -66);

            // Sidebar divider.
            var div = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            div.transform.SetParent(card.transform, false);
            var drt = div.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.70f, 0); drt.anchorMax = new Vector2(0.70f, 1); drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(2, -138); drt.anchoredPosition = Vector2.zero;
            div.GetComponent<Image>().color = new Color(0.30f, 1f, 0.45f, 0.35f);

            inputText = MakeText(card.transform, "Input", 24, TextAlignmentOptions.MidlineLeft);
            inputText.fontStyle = FontStyles.Bold;
            var irt = inputText.rectTransform;
            irt.anchorMin = new Vector2(0, 0); irt.anchorMax = new Vector2(1, 0); irt.pivot = new Vector2(0.5f, 0);
            irt.sizeDelta = new Vector2(-60, 52); irt.anchoredPosition = new Vector2(0, 16);

            card.SetActive(false);
        }

        private static TMP_Text MakeText(Transform parent, string name, int size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = size;
            t.alignment = alignment;
            t.color = new Color(0.85f, 0.95f, 1f);
            t.richText = true;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Truncate;
            t.raycastTarget = false;
            return t;
        }
    }
}
