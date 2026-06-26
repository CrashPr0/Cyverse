using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Core;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Settings
{
    /// <summary>
    /// Accessibility / pause menu opened with Esc. Keyboard-driven (Up/Down to
    /// select, Left/Right to adjust) so it works without any UI event-system
    /// wiring and is fully usable without a mouse. Values persist via PlayerPrefs.
    /// Covers the Level 0 demo requirements: audio level control, caption text
    /// scaling, and look-sensitivity.
    /// </summary>
    public class AccessibilitySettings : MonoBehaviour
    {
        public static AccessibilitySettings Instance { get; private set; }

        public float MasterVolume { get; private set; } = 1f;
        public float VoiceVolume { get; private set; } = 1f;
        public float CaptionScale { get; private set; } = 1f;
        public float MouseSensitivity { get; private set; } = 1f;

        private readonly string[] rows = { "Master Volume", "Voice Volume", "Caption Size", "Look Sensitivity" };
        private int selected;

        private GameObject panel;
        private Text menuText;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Load();
        }

        void Start()
        {
            BuildMenu();
            Apply();
            SetMenuOpen(false);
        }

        void Update()
        {
            // Don't allow pausing once the level's completion screen is up.
            if (Input.GetKeyDown(KeyCode.Escape) && !GameState.LevelComplete)
                SetMenuOpen(!GameState.MenuOpen);

            if (!GameState.MenuOpen) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                selected = (selected - 1 + rows.Length) % rows.Length;
                Refresh();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                selected = (selected + 1) % rows.Length;
                Refresh();
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Adjust(-0.1f);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Adjust(0.1f);
            }
        }

        public void SetMenuOpen(bool open)
        {
            GameState.MenuOpen = open;
            if (panel != null) panel.SetActive(open);
            FirstPersonController.LockCursor(!open);
            if (open) Refresh();
        }

        private void Adjust(float delta)
        {
            switch (selected)
            {
                case 0: MasterVolume = Step01(MasterVolume + delta); break;
                case 1: VoiceVolume = Step01(VoiceVolume + delta); break;
                case 2: CaptionScale = Mathf.Clamp(CaptionScale + delta, 0.6f, 2f); break;
                case 3: MouseSensitivity = Mathf.Clamp(MouseSensitivity + delta, 0.2f, 3f); break;
            }
            Apply();
            Save();
            Refresh();
        }

        private static float Step01(float v) => Mathf.Clamp01(Mathf.Round(v * 10f) / 10f);

        private void Apply()
        {
            AudioListener.volume = MasterVolume;
        }

        private void Refresh()
        {
            if (menuText == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("<b>SETTINGS</b>");
            sb.AppendLine();
            for (int i = 0; i < rows.Length; i++)
            {
                string cursor = i == selected ? "<color=#5BC8FF>> </color>" : "   ";
                sb.AppendLine($"{cursor}{rows[i]}:  {ValueLabel(i)}");
            }
            sb.AppendLine();
            sb.AppendLine("<size=20>Up/Down: select    Left/Right: adjust    Esc: close</size>");
            menuText.text = sb.ToString();
        }

        private string ValueLabel(int i)
        {
            switch (i)
            {
                case 0: return Percent(MasterVolume);
                case 1: return Percent(VoiceVolume);
                case 2: return $"{CaptionScale:0.0}x";
                case 3: return $"{MouseSensitivity:0.0}x";
                default: return string.Empty;
            }
        }

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        private void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat("cv_master", 1f);
            VoiceVolume = PlayerPrefs.GetFloat("cv_voice", 1f);
            CaptionScale = PlayerPrefs.GetFloat("cv_caption", 1f);
            MouseSensitivity = PlayerPrefs.GetFloat("cv_mouse", 1f);
        }

        private void Save()
        {
            PlayerPrefs.SetFloat("cv_master", MasterVolume);
            PlayerPrefs.SetFloat("cv_voice", VoiceVolume);
            PlayerPrefs.SetFloat("cv_caption", CaptionScale);
            PlayerPrefs.SetFloat("cv_mouse", MouseSensitivity);
            PlayerPrefs.Save();
        }

        private void BuildMenu()
        {
            if (HudUI.Instance == null) return;

            var go = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            var prt = go.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
            panel = go;

            var txtGo = new GameObject("SettingsText", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var rt = txtGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900, 500);

            menuText = txtGo.AddComponent<Text>();
            menuText.font = HudUI.UIFont;
            menuText.fontSize = 30;
            menuText.alignment = TextAnchor.MiddleCenter;
            menuText.color = Color.white;
            menuText.supportRichText = true;
        }
    }
}
