using UnityEngine;
using UnityEngine.UI;
using Cyverse.Settings;

namespace Cyverse.UI
{
    /// <summary>
    /// A styled "controls" card shown at the start of the level. Fades out once
    /// the player presses a movement key or after a timeout. By default it only
    /// appears the first time (PlayerPrefs); set onlyFirstTime = false to show
    /// it every session (useful for classroom / kiosk play).
    /// </summary>
    public class ControlsOverlay : MonoBehaviour
    {
        public bool onlyFirstTime = true;
        public float minDisplaySeconds = 5f;  // stays at least this long
        public float autoHideSeconds = 14f;

        private CanvasGroup group;
        private bool dismissing;
        private float elapsed;
        private Vector3 lastMouse;

        void Start()
        {
            if (onlyFirstTime && PlayerPrefs.GetInt("cv_seen_controls", 0) == 1)
            {
                Destroy(this);
                return;
            }
            PlayerPrefs.SetInt("cv_seen_controls", 1);
            PlayerPrefs.Save();

            Build();
            lastMouse = Input.mousePosition;
        }

        void Update()
        {
            if (group == null) return;

            if (!dismissing)
            {
                elapsed += Time.unscaledDeltaTime;

                // Keep it up for a guaranteed minimum so it's actually readable;
                // ignore (and re-baseline) input until then.
                if (elapsed < minDisplaySeconds)
                {
                    lastMouse = Input.mousePosition;
                    return;
                }

                bool move = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
                            Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);
                bool mouse = (Input.mousePosition - lastMouse).sqrMagnitude > 25f;
                if (move || mouse || elapsed >= autoHideSeconds) dismissing = true;
            }
            else
            {
                float speed = AccessibilitySettings.ReduceMotion ? 100f : 2.5f;
                group.alpha = Mathf.MoveTowards(group.alpha, 0f, speed * Time.unscaledDeltaTime);
                if (group.alpha <= 0.001f)
                {
                    Destroy(group.gameObject);
                    Destroy(this);
                }
            }
        }

        private void Build()
        {
            if (HudUI.Instance == null) return;

            var card = new GameObject("ControlsOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            card.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 60);
            rt.sizeDelta = new Vector2(560, 380);
            HudUI.StylePanel(card, new Color(0.02f, 0.04f, 0.07f, 0.92f), HudUI.Accent);
            group = card.GetComponent<CanvasGroup>();

            // Header
            var header = MakeText(card.transform, "Header", "CONTROLS", 34, FontStyle.Bold,
                HudUI.Accent, TextAnchor.UpperCenter);
            var hrt = header.rectTransform;
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1);
            hrt.sizeDelta = new Vector2(0, 50); hrt.anchoredPosition = new Vector2(0, -22);

            float y = 60f;
            AddRow(card.transform, y, "WASD", "Move"); y -= 50f;
            AddRow(card.transform, y, "Mouse", "Look around"); y -= 50f;
            AddRow(card.transform, y, "E", "Interact"); y -= 50f;
            AddRow(card.transform, y, "Space", "Advance dialogue"); y -= 50f;
            AddRow(card.transform, y, "Esc", "Settings");

            var footer = MakeText(card.transform, "Footer", "Press a movement key to begin", 20,
                FontStyle.Normal, new Color(0.6f, 0.85f, 1f, 0.9f), TextAnchor.LowerCenter);
            var frt = footer.rectTransform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0); frt.pivot = new Vector2(0.5f, 0);
            frt.sizeDelta = new Vector2(0, 36); frt.anchoredPosition = new Vector2(0, 14);
        }

        private void AddRow(Transform parent, float y, string key, string label)
        {
            var badge = new GameObject("Key_" + key, typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(parent, false);
            var brt = badge.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0f, 0.5f);
            float w = 34f + key.Length * 16f;
            brt.sizeDelta = new Vector2(w, 38);
            brt.anchoredPosition = new Vector2(-230, y);
            badge.GetComponent<Image>().color = HudUI.Accent;

            var letter = MakeText(badge.transform, "k", key, 22, FontStyle.Bold,
                new Color(0.02f, 0.05f, 0.08f), TextAnchor.MiddleCenter);
            var krt = letter.rectTransform;
            krt.anchorMin = Vector2.zero; krt.anchorMax = Vector2.one;
            krt.offsetMin = Vector2.zero; krt.offsetMax = Vector2.zero;

            var lbl = MakeText(parent, "lbl_" + key, label, 24, FontStyle.Normal,
                Color.white, TextAnchor.MiddleLeft);
            var lrt = lbl.rectTransform;
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = new Vector2(-80, y);
            lrt.sizeDelta = new Vector2(320, 38);
        }

        private static Text MakeText(Transform parent, string name, string text, int size,
            FontStyle style, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = HudUI.UIFont;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.supportRichText = true;
            t.text = text;
            return t;
        }
    }
}
