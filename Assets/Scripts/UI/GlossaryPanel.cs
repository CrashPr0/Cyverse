using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.Player;

namespace Cyverse.UI
{
    /// <summary>
    /// In-game glossary (G to toggle): a keyboard-driven card listing the
    /// CyVerse cybersecurity terms with the selected term's definition below.
    /// Up/Down select (the list window scrolls), Esc or G closes. Pauses the
    /// game while open, like the settings menu.
    /// </summary>
    public class GlossaryPanel : MonoBehaviour
    {
        public static GlossaryPanel Instance { get; private set; }

        public KeyCode toggleKey = KeyCode.G;
        private const int WindowSize = 7;

        private GameObject panel;
        private Text listText;
        private Text defText;
        private Text headerCounter;
        private int selected;
        private int windowStart;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Update()
        {
            if (GameState.GlossaryOpen)
            {
                if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(KeyCode.Escape))
                {
                    SetOpen(false);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.UpArrow)) Move(-1);
                else if (Input.GetKeyDown(KeyCode.DownArrow)) Move(1);
            }
            else if (Input.GetKeyDown(toggleKey) && !GameState.Busy)
            {
                SetOpen(true);
            }
        }

        private void Move(int dir)
        {
            int n = GlossaryContent.Entries.Length;
            selected = (selected + dir + n) % n;
            if (selected < windowStart) windowStart = selected;
            if (selected >= windowStart + WindowSize) windowStart = selected - WindowSize + 1;
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            Refresh();
        }

        private void SetOpen(bool open)
        {
            if (open && panel == null) Build();
            if (panel == null) return;

            GameState.GlossaryOpen = open;
            GameState.MenuTransitionFrame = Time.frameCount; // Esc must not also toggle settings this frame
            panel.SetActive(open);
            Time.timeScale = open ? 0f : 1f;
            FirstPersonController.LockCursor(!open);
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            if (open) Refresh();
        }

        private void Refresh()
        {
            var entries = GlossaryContent.Entries;
            var sb = new StringBuilder();
            int end = Mathf.Min(windowStart + WindowSize, entries.Length);

            if (windowStart > 0) sb.AppendLine("<color=#4A5A70>▲ more</color>");
            else sb.AppendLine();

            for (int i = windowStart; i < end; i++)
            {
                bool unlocked = GlossaryProgress.IsUnlocked(i);
                string label = unlocked ? entries[i].Term : "<color=#4A5A70>??? (locked)</color>";
                if (i == selected)
                    sb.AppendLine($"<color=#5BC8FF>> <b>{label}</b></color>");
                else
                    sb.AppendLine($"   {label}");
            }

            if (end < entries.Length) sb.AppendLine("<color=#4A5A70>▼ more</color>");
            listText.text = sb.ToString();

            if (GlossaryProgress.IsUnlocked(selected))
            {
                defText.text =
                    $"<b><color=#5BC8FF>{entries[selected].Term}</color></b>\n{entries[selected].Definition}";
            }
            else
            {
                string station = entries[selected].Topic.HasValue
                    ? GlossaryContent.StationName(entries[selected].Topic.Value)
                    : "another station";
                defText.text =
                    $"<b><color=#4A5A70>??? LOCKED</color></b>\nVisit {station} to decrypt this entry.";
            }

            headerCounter.text = $"({GlossaryProgress.UnlockedCount}/{GlossaryProgress.TotalCount} discovered)";
        }

        private void Build()
        {
            if (HudUI.Instance == null) return;

            // Dimmed backdrop + styled card, matching the settings menu look.
            panel = new GameObject("GlossaryBackdrop", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var card = new GameObject("GlossaryCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(panel.transform, false);
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(860, 640);
            HudUI.StylePanel(card, new Color(0.02f, 0.04f, 0.07f, 0.95f), HudUI.Accent);

            var header = MakeText(card.transform, "Header", 36, TextAnchor.UpperCenter);
            header.fontStyle = FontStyle.Bold;
            header.color = HudUI.Accent;
            header.text = "GLOSSARY";
            var hrt = header.rectTransform;
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1);
            hrt.sizeDelta = new Vector2(0, 56); hrt.anchoredPosition = new Vector2(0, -24);

            headerCounter = MakeText(card.transform, "HeaderCounter", 18, TextAnchor.UpperCenter);
            headerCounter.color = new Color(0.56f, 0.72f, 0.80f);
            var hcrt = headerCounter.rectTransform;
            hcrt.anchorMin = new Vector2(0, 1); hcrt.anchorMax = new Vector2(1, 1); hcrt.pivot = new Vector2(0.5f, 1);
            hcrt.sizeDelta = new Vector2(0, 26); hcrt.anchoredPosition = new Vector2(0, -62);

            listText = MakeText(card.transform, "TermList", 26, TextAnchor.UpperLeft);
            var lrt = listText.rectTransform;
            lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(1, 1); lrt.pivot = new Vector2(0.5f, 1);
            lrt.sizeDelta = new Vector2(-120, 330); lrt.anchoredPosition = new Vector2(0, -84);

            var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(card.transform, false);
            var drt = divider.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0, 1); drt.anchorMax = new Vector2(1, 1); drt.pivot = new Vector2(0.5f, 1);
            drt.sizeDelta = new Vector2(-80, 2); drt.anchoredPosition = new Vector2(0, -420);
            divider.GetComponent<Image>().color = new Color(HudUI.Accent.r, HudUI.Accent.g, HudUI.Accent.b, 0.5f);

            defText = MakeText(card.transform, "Definition", 24, TextAnchor.UpperLeft);
            var frt = defText.rectTransform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 1);
            frt.offsetMin = new Vector2(60, 56); frt.offsetMax = new Vector2(-60, -434);

            var hint = MakeText(card.transform, "Hint", 20, TextAnchor.LowerCenter);
            hint.color = new Color(0.56f, 0.72f, 0.80f);
            hint.text = "Up/Down: select    G or Esc: close";
            var nrt = hint.rectTransform;
            nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 0); nrt.pivot = new Vector2(0.5f, 0);
            nrt.sizeDelta = new Vector2(0, 34); nrt.anchoredPosition = new Vector2(0, 12);

            panel.SetActive(false);
        }

        private static Text MakeText(Transform parent, string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = HudUI.UIFont;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
