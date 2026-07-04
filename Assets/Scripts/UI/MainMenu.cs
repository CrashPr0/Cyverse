using System;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Settings;

namespace Cyverse.UI
{
    /// <summary>
    /// Title screen shown before the level begins: game title, subtitle, and a
    /// pulsing "Press ENTER" prompt over a full-screen backdrop. Movement and
    /// interaction are held (GameState.TitleActive) until dismissed, then the
    /// provided callback starts the intro. Esc still opens Settings on top.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public static MainMenu Instance { get; private set; }

        /// <summary>True while the title screen is up (read by ControlsOverlay).</summary>
        public static bool Active { get; private set; }

        private GameObject panel;
        private Text promptText;
        private Action onBegin;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Active = false;
        }

        public void Show(Action begin)
        {
            onBegin = begin;
            if (panel == null) Build();
            panel.transform.SetAsLastSibling(); // above the rest of the HUD
            panel.SetActive(true);
            Active = true;
            GameState.TitleActive = true;
        }

        void Update()
        {
            if (!Active || GameState.MenuOpen) return;

            // Gentle prompt pulse (skipped under Reduce Motion).
            if (promptText != null && !AccessibilitySettings.ReduceMotion)
            {
                var c = promptText.color;
                c.a = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 2.4f);
                promptText.color = c;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                Dismiss();
        }

        private void Dismiss()
        {
            Active = false;
            GameState.TitleActive = false;
            panel.SetActive(false);
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            var cb = onBegin;
            onBegin = null;
            cb?.Invoke();
        }

        private void Build()
        {
            if (HudUI.Instance == null) return;

            panel = new GameObject("TitleScreen", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.008f, 0.016f, 0.035f, 0.97f);

            var title = MakeText("Title", "CYVERSE", 110, FontStyle.Bold, HudUI.Accent, new Vector2(0, 160));
            HudUI.AddOutline(title);

            MakeText("Subtitle", "Cybersecurity Onboarding — Level 0", 30, FontStyle.Normal,
                new Color(0.85f, 0.92f, 1f), new Vector2(0, 80));

            MakeText("Blurb",
                "Learn Identification & Access Management, the CIA Triad,\nand the NICE workforce roles — then authenticate to join CyberVerse.",
                22, FontStyle.Normal, new Color(0.55f, 0.65f, 0.78f), new Vector2(0, 10));

            promptText = MakeText("Prompt", "[ ENTER ]  Begin Onboarding", 32, FontStyle.Bold,
                new Color(0.90f, 0.66f, 0.14f), new Vector2(0, -110)); // Spartan gold
            HudUI.AddOutline(promptText);

            MakeText("Hint", "WASD move   ·   Mouse look   ·   E interact   ·   Esc settings",
                20, FontStyle.Normal, new Color(0.45f, 0.55f, 0.68f), new Vector2(0, -170));

            MakeText("Credit", "San José State University  ·  CyberVerse Project",
                18, FontStyle.Normal, new Color(0.40f, 0.48f, 0.60f), new Vector2(0, -300));

            panel.SetActive(false);
        }

        private Text MakeText(string name, string text, int size, FontStyle style, Color color, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var t = go.AddComponent<Text>();
            t.font = HudUI.UIFont;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.supportRichText = true;
            t.text = text;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(1600, size * 2.4f);
            return t;
        }
    }
}
