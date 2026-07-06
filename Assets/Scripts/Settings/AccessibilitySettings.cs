using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Settings
{
    /// <summary>
    /// Accessibility / pause menu (Esc). Keyboard-driven (Up/Down select,
    /// Left/Right adjust) so it works with no mouse and no UI event-system.
    /// Opening it pauses the game. Covers volume channels (master/voice/SFX),
    /// caption text scaling, look sensitivity, field of view (motion comfort),
    /// and a reduce-motion toggle (photosensitivity). Persists via PlayerPrefs.
    /// </summary>
    public class AccessibilitySettings : MonoBehaviour
    {
        public static AccessibilitySettings Instance { get; private set; }

        // Read by shaders/HUD/rotators without needing an instance reference.
        public static bool ReduceMotion;

        public float MasterVolume { get; private set; } = 1f;
        public float VoiceVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;
        public float CaptionScale { get; private set; } = 1f;
        public float MouseSensitivity { get; private set; } = 1f;
        public float FieldOfView { get; private set; } = 60f;

        /// <summary>Browser text-to-speech for unvoiced dialogue (WebGL builds).</summary>
        public bool TtsEnabled { get; private set; } = true;

        private readonly string[] rows =
        {
            "Master Volume", "Voice Volume", "Voiceover (TTS)", "SFX Volume",
            "Caption Size", "Look Sensitivity", "Field of View", "Reduce Motion"
        };
        private int selected;

        private GameObject panel;
        private Text menuText;
        private Camera cam;

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
            // Esc is the glossary's close key while it's open — don't fight it.
            if (Input.GetKeyDown(KeyCode.Escape) && !GameState.LevelComplete && !GameState.GlossaryOpen)
                SetMenuOpen(!GameState.MenuOpen);

            if (!GameState.MenuOpen) return;

            if (Input.GetKeyDown(KeyCode.UpArrow)) { Move(-1); }
            else if (Input.GetKeyDown(KeyCode.DownArrow)) { Move(1); }
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) { Adjust(-1); }
            else if (Input.GetKeyDown(KeyCode.RightArrow)) { Adjust(1); }
        }

        public void SetMenuOpen(bool open)
        {
            GameState.MenuOpen = open;
            if (panel != null) panel.SetActive(open);
            FirstPersonController.LockCursor(!open);
            Time.timeScale = open ? 0f : 1f; // a pause menu should actually pause
            if (open)
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayClick();
                Refresh();
            }
        }

        private void Move(int dir)
        {
            selected = (selected + dir + rows.Length) % rows.Length;
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            Refresh();
        }

        private void Adjust(int dir)
        {
            switch (selected)
            {
                case 0: MasterVolume = Step01(MasterVolume + 0.1f * dir); break;
                case 1: VoiceVolume = Step01(VoiceVolume + 0.1f * dir); break;
                case 2:
                    TtsEnabled = !TtsEnabled;
                    if (!TtsEnabled) Audio.Speech.Cancel();
                    break;
                case 3: SfxVolume = Step01(SfxVolume + 0.1f * dir); break;
                case 4: CaptionScale = Mathf.Clamp(CaptionScale + 0.1f * dir, 0.6f, 2f); break;
                case 5: MouseSensitivity = Mathf.Clamp(MouseSensitivity + 0.1f * dir, 0.2f, 3f); break;
                case 6: FieldOfView = Mathf.Clamp(FieldOfView + 5f * dir, 50f, 100f); break;
                case 7: ReduceMotion = !ReduceMotion; break;
            }
            Apply();
            Save();
            Refresh();
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
        }

        private static float Step01(float v) => Mathf.Clamp01(Mathf.Round(v * 10f) / 10f);

        private void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (cam == null) cam = Camera.main;
            if (cam != null) cam.fieldOfView = FieldOfView;
            Shader.SetGlobalFloat("_CyMotion", ReduceMotion ? 0f : 1f);
        }

        private void Refresh()
        {
            if (menuText == null) return;

            var sb = new StringBuilder();
            for (int i = 0; i < rows.Length; i++)
            {
                bool sel = i == selected;
                string cursor = sel ? "<color=#5BC8FF>> </color>" : "   ";
                string name = sel ? $"<color=#5BC8FF>{rows[i]}</color>" : rows[i];
                sb.AppendLine($"{cursor}{name}:  {ValueLabel(i)}");
                sb.AppendLine();
            }
            sb.AppendLine("<size=20><color=#8FB8CC>Up/Down: select    Left/Right: adjust    Esc: resume</color></size>");
            menuText.text = sb.ToString();
        }

        private string ValueLabel(int i)
        {
            switch (i)
            {
                case 0: return Percent(MasterVolume);
                case 1: return Percent(VoiceVolume);
                case 2:
                    if (!TtsEnabled) return "Off";
                    return Audio.Speech.Available ? "On" : "On (browser builds)";
                case 3: return Percent(SfxVolume);
                case 4: return $"{CaptionScale:0.0}x";
                case 5: return $"{MouseSensitivity:0.0}x";
                case 6: return $"{Mathf.RoundToInt(FieldOfView)}°";
                case 7: return ReduceMotion ? "On" : "Off";
                default: return string.Empty;
            }
        }

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        private void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat("cv_master", 1f);
            VoiceVolume = PlayerPrefs.GetFloat("cv_voice", 1f);
            SfxVolume = PlayerPrefs.GetFloat("cv_sfx", 1f);
            CaptionScale = PlayerPrefs.GetFloat("cv_caption", 1f);
            MouseSensitivity = PlayerPrefs.GetFloat("cv_mouse", 1f);
            FieldOfView = PlayerPrefs.GetFloat("cv_fov", 60f);
            ReduceMotion = PlayerPrefs.GetInt("cv_reducemotion", 0) == 1;
            TtsEnabled = PlayerPrefs.GetInt("cv_tts", 1) == 1;
        }

        private void Save()
        {
            PlayerPrefs.SetFloat("cv_master", MasterVolume);
            PlayerPrefs.SetFloat("cv_voice", VoiceVolume);
            PlayerPrefs.SetFloat("cv_sfx", SfxVolume);
            PlayerPrefs.SetFloat("cv_caption", CaptionScale);
            PlayerPrefs.SetFloat("cv_mouse", MouseSensitivity);
            PlayerPrefs.SetFloat("cv_fov", FieldOfView);
            PlayerPrefs.SetInt("cv_reducemotion", ReduceMotion ? 1 : 0);
            PlayerPrefs.SetInt("cv_tts", TtsEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void BuildMenu()
        {
            if (HudUI.Instance == null) return;

            // Dimmed backdrop behind the card.
            var backdrop = new GameObject("SettingsBackdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            var prt = backdrop.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            panel = backdrop;

            // Centred settings card with accent styling (matches the dialogue box).
            var card = new GameObject("SettingsCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(backdrop.transform, false);
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(760, 700);
            HudUI.StylePanel(card, new Color(0.02f, 0.04f, 0.07f, 0.95f), HudUI.Accent);

            // Header.
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(card.transform, false);
            var htext = header.AddComponent<Text>();
            htext.font = HudUI.UIFont;
            htext.fontSize = 36;
            htext.fontStyle = FontStyle.Bold;
            htext.alignment = TextAnchor.UpperCenter;
            htext.color = HudUI.Accent;
            htext.text = "SETTINGS";
            var hrt = header.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0, 1);
            hrt.anchorMax = new Vector2(1, 1);
            hrt.pivot = new Vector2(0.5f, 1);
            hrt.sizeDelta = new Vector2(0, 60);
            hrt.anchoredPosition = new Vector2(0, -24);

            // Settings list.
            var txtGo = new GameObject("SettingsText", typeof(RectTransform));
            txtGo.transform.SetParent(card.transform, false);
            var rt = txtGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, -30);
            rt.sizeDelta = new Vector2(660, 580);

            menuText = txtGo.AddComponent<Text>();
            menuText.font = HudUI.UIFont;
            menuText.fontSize = 26;
            menuText.alignment = TextAnchor.UpperLeft;
            menuText.color = Color.white;
            menuText.supportRichText = true;
            menuText.horizontalOverflow = HorizontalWrapMode.Wrap;
            menuText.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
