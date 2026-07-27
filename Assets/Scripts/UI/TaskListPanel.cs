using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Core;

namespace Cyverse.UI
{
    /// <summary>
    /// A persistent objective checklist on the left of the HUD: the "what" to
    /// the ObjectiveBeacon's "where". Shows each task with a ✓ / □ (a SHAPE,
    /// not colour alone) and highlights the one the player should do next.
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
        private Text bodyText;
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
                    sb.Append($"<color=#4CE087>  ✓  <s>{t.label}</s></color>\n");
                else if (t.current)
                    sb.Append($"<color=#E5A823>  ▶  <b>{t.label}</b></color>\n");
                else
                    sb.Append($"<color=#7E93A6>  □  {t.label}</color>\n");
            }
            bodyText.text = sb.ToString();
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
            rt.sizeDelta = new Vector2(360f, 190f);
            HudUI.StylePanel(panel, new Color(0.02f, 0.04f, 0.07f, 0.72f), HudUI.Accent);

            var textGo = new GameObject("Body", typeof(RectTransform));
            textGo.transform.SetParent(panel.transform, false);
            bodyText = textGo.AddComponent<Text>();
            bodyText.font = HudUI.UIFont;
            bodyText.fontSize = 19;
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.color = Color.white;
            bodyText.supportRichText = true;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            var brt = bodyText.rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(14f, 10f);
            brt.offsetMax = new Vector2(-12f, -10f);

            panel.SetActive(false);
        }
    }
}
