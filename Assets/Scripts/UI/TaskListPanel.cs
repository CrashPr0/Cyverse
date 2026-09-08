using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Core;

namespace Cyverse.UI
{
    /// <summary>
    /// A persistent objective checklist on the left of the HUD: the "what" to
    /// the ObjectiveBeacon's "where". Shows each task with [x] / [ ] (a
    /// shape, not colour alone) and highlights the one the player should do
    /// next. ASCII markers keep the checklist deterministic on WebGL even
    /// when the selected TMP font has no symbol fallback asset.
    ///
    /// Passive overlay: it hides itself whenever a modal owns the screen, per
    /// the one-menu-at-a-time standard.
    /// </summary>
    public class TaskListPanel : MonoBehaviour
    {
        public static TaskListPanel Instance { get; private set; }

        /// <summary>One checklist row.</summary>
        public struct Task
        {
            public string label;
            public bool done;
            public bool current;
            public Task(string label, bool done, bool current)
            { this.label = label; this.done = done; this.current = current; }
        }

        private GameObject panel;
        private TMP_Text bodyText;
        private string header = "TASKS";

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public static TaskListPanel Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            var found = FindObjectOfType<TaskListPanel>();
            if (found != null) { Instance = found; return found; }
            return host.AddComponent<TaskListPanel>();
        }

        public void SetHeader(string text) => header = text;

        public void Show(List<Task> tasks)
        {
            if (panel == null) Build();
            if (panel == null) return; // no HUD in this scene

            var sb = new System.Text.StringBuilder();
            sb.Append($"<color=#5BC8FF><b>{header}</b></color>\n");
            foreach (var t in tasks)
            {
                if (t.done)
                    sb.Append($"<color=#4CE087>  [x]  {t.label}</color>\n");
                else if (t.current)
                    sb.Append($"<color=#E5A823>  &gt;  <b>{t.label}</b></color>\n");
                else
                    sb.Append($"<color=#7E93A6>  [ ]  {t.label}</color>\n");
            }
            bodyText.text = sb.ToString();
            FitToContent(tasks.Count);
            panel.SetActive(!GameState.AnyMenuOpen);
        }

        public void HidePanel()
        {
            if (panel != null) panel.SetActive(false);
        }

        void LateUpdate()
        {
            // Never sit on top of a menu, and come back when the screen frees up.
            if (panel == null) return;
            bool shouldShow = !GameState.AnyMenuOpen && !string.IsNullOrEmpty(bodyText.text);
            if (panel.activeSelf != shouldShow) panel.SetActive(shouldShow);
        }

        private void Build()
        {
            if (HudUI.Instance == null) return;

            panel = new GameObject("TaskList", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -150f); // below the progress ring
            rt.sizeDelta = new Vector2(390f, 220f);
            HudUI.StylePanel(panel, new Color(0.02f, 0.04f, 0.07f, 0.72f), HudUI.Accent);
            panel.AddComponent<RectMask2D>();

            var textGo = new GameObject("Body", typeof(RectTransform));
            textGo.transform.SetParent(panel.transform, false);
            bodyText = textGo.AddComponent<TextMeshProUGUI>();
            bodyText.font = TMP_Settings.defaultFontAsset;
            bodyText.fontSize = 18;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.color = Color.white;
            bodyText.richText = true;
            bodyText.enableWordWrapping = true;
            bodyText.overflowMode = TextOverflowModes.Ellipsis;
            bodyText.raycastTarget = false;
            // Positive leading keeps six-step lists readable at small WebGL
            // sizes; the old negative leading made adjacent rows collide.
            bodyText.lineSpacing = 4f;
            var brt = bodyText.rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(14f, 12f);
            brt.offsetMax = new Vector2(-14f, -12f);

            panel.SetActive(false);
        }

        private void FitToContent(int taskCount)
        {
            if (panel == null || bodyText == null) return;

            // Long Level 4 task labels used to overflow the fixed 190 px
            // legacy-Text card. TMP gives us a preferred height, while the
            // mask provides a final safety net at narrow WebGL resolutions.
            bodyText.fontSize = taskCount >= 6 ? 17f : 18f;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            float contentWidth = Mathf.Max(120f, panelRect.sizeDelta.x - 28f);
            float preferred = bodyText.GetPreferredValues(bodyText.text, contentWidth, 0f).y;
            float height = Mathf.Clamp(preferred + 24f, 190f, 330f);
            panelRect.sizeDelta = new Vector2(390f, height);
        }
    }
}
