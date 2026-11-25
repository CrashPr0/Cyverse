using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Settings
{
    /// <summary>
    /// The settings / pause menu (Esc or Tab). Rows are grouped into Audio,
    /// Display, Controls and Accessibility, each drawn as a real control: a
    /// segmented bar for levels, an on/off switch, or a ‹ choice ›. A help line
    /// explains the selected row. It works from the keyboard (Up/Down select,
    /// Left/Right change, Enter toggles) or the mouse (hover to select, click
    /// to change, click along a bar to set it) without a UI event system.
    /// Opening it pauses the game; every change applies at once and persists
    /// via PlayerPrefs.
    /// </summary>
    public class AccessibilitySettings : MonoBehaviour
    {
        public static AccessibilitySettings Instance { get; private set; }

        // Read by shaders/HUD/rotators without needing an instance reference.
        public static bool ReduceMotion;

        /// <summary>The arrow keys move the player as well as WASD. While they
        /// do, the briefing screen scrubs with [ and ] instead.</summary>
        public static bool ArrowKeysMove = true;

        public float MasterVolume { get; private set; } = 1f;
        public float VoiceVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;
        public float CaptionScale { get; private set; } = 1f;
        public float MouseSensitivity { get; private set; } = 1f;
        public float FieldOfView { get; private set; } = 60f;

        /// <summary>Browser text-to-speech for unvoiced dialogue (WebGL builds).
        /// Off unless the player turns it on: it is the computer's synthetic
        /// voice, and recorded narration and captions cover the same text.</summary>
        public bool TtsEnabled { get; private set; } = false;
        /// <summary>Speed multiplier for browser text-to-speech (1 = normal).</summary>
        public float VoiceRate { get; private set; } = 1f;

        // ---- Rows -------------------------------------------------------------

        private enum Kind { Level, Switch, Choice, Action }

        private sealed class Row
        {
            public string Section, Label, Help;
            public Kind Kind;
            public Func<string> Value;
            public Func<float> Fraction;      // Level: 0..1 along the bar
            public Func<bool> On;             // Switch
            public Action<int> Step;          // Left/Right or a click
            public Action<float> SetFraction; // Level: a click along the bar
            public Action Activate;           // Choice: Enter or a click off the arrows (instead of Step)

            public RectTransform Rect, Bar, LeftArrow, RightArrow;
            public Image Back, Marker, Pill;
            public Text LabelText, ValueText, PillText;
            public Image[] Segments;
        }

        private const int Segments = 10;
        private static readonly Color Ink = new Color(0.86f, 0.93f, 1f);
        private static readonly Color Muted = new Color(0.58f, 0.68f, 0.78f);
        private static readonly Color Dim = new Color(0.15f, 0.21f, 0.29f);
        private static readonly Color Warn = new Color(1f, 0.70f, 0.38f);

        private readonly List<Row> rows = new List<Row>();
        private int selected;
        private bool resetArmed;
        private bool startOverArmed;
        private PlaytestExport.Format exportFormat = PlaytestExport.Format.Csv;
        private string exportStatus = "";
        private GameObject creditsCard;
        private bool creditsOpen;

        /// <summary>The credits shown from the settings menu. PLACEHOLDER:
        /// the full team list is still to come; edit this text to add it.</summary>
        public const string CreditsText =
            "<size=30><b>CyVerse</b></size>\n" +
            "A cybersecurity training game from San José State University\n\n" +
            "<color=#8FB8CC>DEVELOPMENT</color>\n" +
            "Christopher Anthony Velez\n\n" +
            "<color=#8FB8CC>WITH THANKS TO</color>\n" +
            "The CyberVerse Project team at San José State University\n" +
            "Full team credits are being collected and will appear here.\n\n" +
            "<color=#8FB8CC>BUILT WITH</color>\n" +
            "Unity  ·  Liberation Sans (SIL Open Font License)\n" +
            "Models by Brick Project Studio, Akduman, JustPlay and HQP Studios";
        private Vector3 lastMouse;

        private GameObject panel;
        private RectTransform card;
        private Text helpText;
        private Camera cam;
        private ModalSession.Lease modal;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Load();
            DefineRows();
        }

        void Start()
        {
            BuildMenu();
            Apply();
            SetMenuOpen(false);
        }

        private void DefineRows()
        {
            Volume("Audio", "Master Volume", "Overall loudness of the game.",
                () => MasterVolume, f => MasterVolume = f, Percent);
            Volume("Audio", "Voice Volume", "Narration and character voices.",
                () => VoiceVolume, f => VoiceVolume = f, Percent);
            Volume("Audio", "Sound Effects", "Footsteps, interface clicks and machine sounds.",
                () => SfxVolume, f => SfxVolume = f, Percent);
            Add(new Row
            {
                Section = "Audio", Label = "Voiceover", Kind = Kind.Choice,
                Help = "Off by default. Turn on to have your browser's voice read dialogue that has no recorded voice, at the speed you pick.",
                Value = () => TtsEnabled ? VoiceRateLabel(VoiceRate) : "Off",
                Step = CycleVoiceover,
            });

            Add(new Row
            {
                Section = "Display", Label = "Graphics", Kind = Kind.Choice,
                Help = "Auto starts on High and steps down by itself if the game runs slowly. " +
                       "High adds bloom and sharper reflections; Performance is lightest for older laptops.",
                Value = () => Level.LightingDirector.Chosen
                    ? Level.LightingDirector.Current.ToString()
                    : $"Auto ({Level.LightingDirector.Current})",
                Step = CycleGraphics,
            });
            Range("Display", "Field of View", "How wide you see. A wider view eases motion sickness for some players.",
                () => FieldOfView, v => FieldOfView = v, 50f, 100f, 5f, v => $"{Mathf.RoundToInt(v)}°");

            Range("Controls", "Look Sensitivity", "How fast the view turns when you move the mouse.",
                () => MouseSensitivity, v => MouseSensitivity = v, 0.2f, 3f, 0.1f, v => $"{v:0.0}x");
            Add(new Row
            {
                Section = "Controls", Label = "Move With", Kind = Kind.Choice,
                Help = "Whether the arrow keys move you as well as WASD.",
                Value = () => ArrowKeysMove ? "WASD + Arrows" : "WASD only",
                Step = _ => ArrowKeysMove = !ArrowKeysMove,
            });

            Range("Accessibility", "Caption Size", "Size of subtitles and dialogue text.",
                () => CaptionScale, v => CaptionScale = v, 0.6f, 2f, 0.1f, v => $"{v:0.0}x");
            Switch("Accessibility", "Reduce Motion", () => "Stops bobbing, spinning, scanning and flashing effects.",
                () => ReduceMotion, () => ReduceMotion = !ReduceMotion);
            Switch("Accessibility", "Invert Colors", () => "Swaps every colour for its opposite, so dark rooms turn light. " +
                    "Some players with light sensitivity or low vision find it easier to read.",
                () => InvertColors.Enabled, () => InvertColors.Set(!InvertColors.Enabled));

            Add(new Row
            {
                Section = null, Label = "Restore Defaults", Kind = Kind.Action,
                Help = "Puts every setting back to how the game started.",
                On = () => resetArmed,
                Value = () => resetArmed ? "Press again to confirm" : "",
                Step = _ => { if (resetArmed) RestoreDefaults(); resetArmed = !resetArmed; startOverArmed = false; },
            });
            Add(new Row
            {
                Section = null, Label = "Start Over", Kind = Kind.Action,
                Help = "Erases all progress, scores and the callsign, and returns to the title screen for a new player. Settings are kept.",
                On = () => startOverArmed,
                Value = () => startOverArmed ? "Press again to confirm" : "",
                Step = _ =>
                {
                    if (startOverArmed) { startOverArmed = false; StartOver(); }
                    else { startOverArmed = true; resetArmed = false; }
                },
            });
            Add(new Row
            {
                Section = null, Label = "Export Data", Kind = Kind.Choice,
                Help = "Saves your playtest data from this browser as a file. ← → pick CSV or JSON, Enter or click saves. Nothing is sent anywhere.",
                Value = () => exportStatus.Length > 0 ? exportStatus
                    : exportFormat == PlaytestExport.Format.Csv ? "Save as CSV" : "Save as JSON",
                Step = _ =>
                {
                    exportFormat = exportFormat == PlaytestExport.Format.Csv
                        ? PlaytestExport.Format.Json : PlaytestExport.Format.Csv;
                    exportStatus = "";
                },
                Activate = () => ExportData(),
            });
            Add(new Row
            {
                Section = null, Label = "Credits", Kind = Kind.Action,
                Help = "Who made CyVerse.",
                Value = () => "",
                Step = _ => ShowCredits(true),
            });
        }

        private void Add(Row row) => rows.Add(row);

        /// <summary>Settings → Export Data. Public so tests and study staff
        /// tooling can trigger the same save.</summary>
        public bool ExportData()
        {
            bool ok = PlaytestExport.Export(exportFormat);
            exportStatus = ok ? "Saved" : "Could not save";
            return ok;
        }

        private static readonly float[] VoiceRates = { 0.75f, 1f, 1.25f, 1.5f };

        private static string VoiceRateLabel(float r) => r + "x";

        // Off, then each speed; stepping past either end wraps around.
        private void CycleVoiceover(int dir)
        {
            int i = TtsEnabled ? Array.IndexOf(VoiceRates, VoiceRate) : -1;
            if (TtsEnabled && i < 0) i = 1;
            i = ((i + 1 + dir + VoiceRates.Length + 1) % (VoiceRates.Length + 1)) - 1;
            TtsEnabled = i >= 0;
            if (TtsEnabled) VoiceRate = VoiceRates[i];
            else Speech.Cancel();
        }

        private void Volume(string section, string label, string help, Func<float> get, Action<float> set,
            Func<float, string> format) =>
            Range(section, label, help, get, set, 0f, 1f, 0.1f, format);

        private void Range(string section, string label, string help, Func<float> get, Action<float> set,
            float min, float max, float step, Func<float, string> format)
        {
            float Snap(float v) => Mathf.Clamp(min + Mathf.Round((v - min) / step) * step, min, max);
            Add(new Row
            {
                Section = section, Label = label, Help = help, Kind = Kind.Level,
                Value = () => format(get()),
                Fraction = () => Mathf.InverseLerp(min, max, get()),
                Step = dir => set(Snap(get() + step * dir)),
                SetFraction = f => set(Snap(Mathf.Lerp(min, max, f))),
            });
        }

        private void Switch(string section, string label, Func<string> help, Func<bool> on, Action toggle)
        {
            var row = new Row { Section = section, Label = label, Kind = Kind.Switch, On = on, Step = _ => toggle() };
            row.Value = () => on() ? "ON" : "OFF";
            rows.Add(row);
            switchHelp[row] = help;
        }

        private readonly Dictionary<Row, Func<string>> switchHelp = new Dictionary<Row, Func<string>>();

        private string HelpFor(Row row) => switchHelp.TryGetValue(row, out var help) ? help() : row.Help;

        // ---- Input ------------------------------------------------------------

        void Update()
        {
            // Esc is the Settings key and Tab is the close key (GameInput).
            // Esc opens settings only when nothing owns the screen, or over a
            // screen that has no close key of its own (a quiz question, the
            // results card), which gets the screen back when settings closes.
            // Over any other screen Esc says how to close it instead.
            // Either key closes settings. The MenuTransitionFrame check stops
            // the key press that closed another screen from also acting here.
            bool transition = Time.frameCount == GameState.MenuTransitionFrame;
            if (!transition)
            {
                KeyRoute route = RouteKeys(GameInput.SettingsDown, GameInput.CancelDown,
                    GameState.MenuOpen, GameState.AnyMenuOpen && !GameState.MenuOpen,
                    ModalSession.SettingsOverlayAllowed,
                    UI.ResultsScreen.Instance != null && UI.ResultsScreen.Instance.CertificateOpen,
                    Time.unscaledTime - pausedByPointerAt < PointerPauseGuardSeconds);
                if (route == KeyRoute.OpenSettings) SetMenuOpen(true);
                else if (route == KeyRoute.CloseSettings) SetMenuOpen(false);
                else if (route == KeyRoute.ShowCloseHint && !GameState.TitleActive && UI.HudUI.Instance != null)
                    UI.HudUI.Instance.ShowToast(
                        $"Press {GameInput.CancelLabel} to close this screen. {GameInput.SettingsLabel} opens Settings.",
                        new Color(0.75f, 0.9f, 1f));
            }
            WatchPointerLock();

            if (!GameState.MenuOpen || panel == null) return;

            // The credits card replaces the settings card until dismissed.
            if (creditsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Backspace) ||
                    Input.GetMouseButtonDown(0))
                {
                    ShowCredits(false);
                    if (Sfx.Instance != null) Sfx.Instance.PlayClick();
                }
                lastMouse = Input.mousePosition;
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Select(selected - 1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Select(selected + 1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Change(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Change(1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                     Input.GetKeyDown(KeyCode.Space))
                Activate();

            HandleMouse();
        }

        // ---- Key routing ------------------------------------------------------

        public enum KeyRoute { None, OpenSettings, CloseSettings, ShowCloseHint }

        /// <summary>What the Settings key (Esc) and the close key (Tab) do this
        /// frame. Tab never opens settings; Esc never closes another screen.</summary>
        /// <param name="otherScreenOpen">A screen other than settings owns the view.</param>
        /// <param name="overlayAllowed">That screen lets settings open over it.</param>
        /// <param name="certificateOpen">The certificate is up (Tab or C close it).</param>
        /// <param name="justPausedByPointer">Settings opened from a lost mouse a moment ago,
        /// so the same Esc must not close it again.</param>
        public static KeyRoute RouteKeys(bool settingsKey, bool closeKey, bool settingsOpen,
            bool otherScreenOpen, bool overlayAllowed, bool certificateOpen, bool justPausedByPointer)
        {
            if (settingsOpen)
                return closeKey || (settingsKey && !justPausedByPointer) ? KeyRoute.CloseSettings : KeyRoute.None;
            if (!settingsKey) return KeyRoute.None;
            if (!certificateOpen && (!otherScreenOpen || overlayAllowed)) return KeyRoute.OpenSettings;
            return KeyRoute.ShowCloseHint;
        }

        // ---- Pointer lock -------------------------------------------------------

        // In a browser, the first Esc during play only lets go of the mouse:
        // the page never sees that key press. So losing the mouse in free
        // play counts as pressing Esc, and one press opens settings.
        private const float PointerPauseGuardSeconds = 0.35f;
        private float lockedSince = -1f;
        private float pausedByPointerAt = -10f;

        private void WatchPointerLock()
        {
            // Only a browser takes the mouse back on Esc without telling us.
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                if (lockedSince < 0f) lockedSince = Time.unscaledTime;
                return;
            }
            // Held long enough to rule out a lock request the browser refused.
            bool wasPlaying = lockedSince >= 0f && Time.unscaledTime - lockedSince > 0.5f;
            lockedSince = -1f;
            if (!wasPlaying || !Application.isFocused) return;
            if (GameState.AnyMenuOpen || GameState.MenuOpen || !FirstPersonController.LockWanted) return;
            if (Time.frameCount == GameState.MenuTransitionFrame) return;
            SetMenuOpen(true);
            if (GameState.MenuOpen) pausedByPointerAt = Time.unscaledTime;
        }

        private void HandleMouse()
        {
            Vector3 mouse = Input.mousePosition;
            bool moved = (mouse - lastMouse).sqrMagnitude > 1f;
            lastMouse = mouse;
            bool click = Input.GetMouseButtonDown(0);
            if (!moved && !click) return;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                if (!RectTransformUtility.RectangleContainsScreenPoint(row.Rect, mouse, null)) continue;
                if (i != selected) Select(i, sound: false);
                if (!click) return;

                if (row.Kind == Kind.Level &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(row.Bar, mouse, null, out Vector2 local) &&
                    row.Bar.rect.Contains(local))
                {
                    row.SetFraction(Mathf.Clamp01(Mathf.InverseLerp(row.Bar.rect.xMin, row.Bar.rect.xMax, local.x)));
                    Changed();
                }
                else if (row.LeftArrow != null &&
                         RectTransformUtility.RectangleContainsScreenPoint(row.LeftArrow, mouse, null))
                    Change(-1);
                else if (row.RightArrow != null && row.Activate != null &&
                         RectTransformUtility.RectangleContainsScreenPoint(row.RightArrow, mouse, null))
                    Change(1);
                else
                    Activate();
                return;
            }
        }

        public void SetMenuOpen(bool open)
        {
            if (open)
            {
                if (!ModalSession.TryOpen(this, ModalSession.Channel.Settings,
                    out modal, releaseCursor: true, pauseTime: true)) return;
            }
            else
            {
                modal?.Close();
                modal = null;
            }
            if (panel != null) panel.SetActive(open);
            if (open)
            {
                // Settings can open over a quiz question or the results card,
                // which share this canvas and were built after it. Draw last
                // so they do not cover it (and so clicks land here).
                if (panel != null) panel.transform.SetAsLastSibling();
                if (panel != null) UiMotion.PanelIn(panel, card);
                resetArmed = false;
                startOverArmed = false;
                exportStatus = "";
                ShowCredits(false);
                lastMouse = Input.mousePosition;
                if (Sfx.Instance != null) Sfx.Instance.PlayClick();
                Refresh();
            }
        }

        private void OnDestroy()
        {
            modal?.Close();
        }

        private void Select(int index, bool sound = true)
        {
            int next = (index + rows.Count) % rows.Count;
            if (next == selected) return;
            selected = next;
            resetArmed = false;
            startOverArmed = false;
            if (sound && Sfx.Instance != null) Sfx.Instance.PlayClick();
            Refresh();
        }

        /// <summary>Enter, Space or a click: a row with its own action runs it,
        /// every other row steps forward.</summary>
        private void Activate()
        {
            Row row = rows[selected];
            if (row.Activate == null) { Change(1); return; }
            row.Activate();
            Refresh();
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
        }

        private void Change(int dir)
        {
            rows[selected].Step(dir);
            Changed();
        }

        private void Changed()
        {
            Apply();
            Save();
            Refresh();
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
        }

        /// <summary>Auto → High → Balanced → Performance → Auto (Left/Right
        /// walk the same loop in opposite directions).</summary>
        private static void CycleGraphics(int dir)
        {
            // Positions on the loop: 0 Auto, 1 High, 2 Balanced, 3 Performance.
            int at = Level.LightingDirector.Chosen ? 3 - (int)Level.LightingDirector.Current : 0;
            at = (at + (dir >= 0 ? 1 : 3)) % 4;
            if (at == 0) Level.LightingDirector.ChooseAuto();
            else Level.LightingDirector.Choose((Level.LightingDirector.Tier)(3 - at));
        }

        /// <summary>Swaps the settings card for the credits card and back.</summary>
        private void ShowCredits(bool show)
        {
            if (show && creditsCard == null) BuildCredits();
            creditsOpen = show && creditsCard != null;
            if (creditsCard != null) creditsCard.SetActive(creditsOpen);
            if (card != null) card.gameObject.SetActive(!creditsOpen);
        }

        private void BuildCredits()
        {
            if (panel == null) return;
            Color accent = HudUI.Accent;
            const float width = 920f, height = 600f;
            creditsCard = NewImage("CreditsCard", panel.transform, new Color(0.012f, 0.03f, 0.05f, 0.97f));
            var rt = creditsCard.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, -28f);
            HudUI.StylePanel(creditsCard, new Color(0.012f, 0.03f, 0.05f, 0.97f), accent);

            Text title = NewText("Title", rt, "CREDITS", 34, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(title.rectTransform, 48f, -24f, 400f, 44f);
            var rule = NewImage("Rule", rt, new Color(accent.r, accent.g, accent.b, 0.25f));
            Place(rule.GetComponent<RectTransform>(), 48f, -74f, width - 96f, 1f);

            Text body = NewText("Body", rt, CreditsText, 22, new Color(0.90f, 0.94f, 0.98f),
                TextAnchor.UpperCenter, FontStyle.Normal);
            body.supportRichText = true;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            Place(body.rectTransform, 48f, -100f, width - 96f, height - 180f);

            Text hint = NewText("Hint", rt, "Enter or click to go back   ·   Esc or Tab to resume the game", 18, Muted,
                TextAnchor.MiddleCenter, FontStyle.Normal);
            Place(hint.rectTransform, 48f, -(height - 58f), width - 96f, 36f);
        }

        /// <summary>Wipes the playthrough and reloads the Hub, which opens on
        /// the title card again. Same result as reloading the page, without
        /// leaving fullscreen or re-downloading the game.</summary>
        private void StartOver()
        {
            SetMenuOpen(false);
            Speech.Cancel();
            PlaythroughReset.StartNewPlaythrough();
            Time.timeScale = 1f;
            string hub = SceneCatalog.Preferred("Hub");
            if (!Application.CanStreamedLevelBeLoaded(hub)) return;
            if (ScreenFader.Instance != null)
                ScreenFader.Instance.FadeToBlackThen(
                    () => UnityEngine.SceneManagement.SceneManager.LoadScene(hub));
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(hub);
        }

        private void RestoreDefaults()
        {
            MasterVolume = VoiceVolume = SfxVolume = 1f;
            CaptionScale = 1f;
            MouseSensitivity = 1f;
            FieldOfView = 60f;
            ReduceMotion = false;
            TtsEnabled = false;
            VoiceRate = 1f;
            ArrowKeysMove = true;
            InvertColors.Set(false);
            Level.LightingDirector.ChooseAuto();
        }

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        private void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (cam == null) cam = Camera.main;
            if (cam != null) cam.fieldOfView = FieldOfView;
            Shader.SetGlobalFloat("_CyMotion", ReduceMotion ? 0f : 1f);
        }

        private void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat("cv_master", 1f);
            VoiceVolume = PlayerPrefs.GetFloat("cv_voice", 1f);
            SfxVolume = PlayerPrefs.GetFloat("cv_sfx", 1f);
            CaptionScale = PlayerPrefs.GetFloat("cv_caption", 1f);
            MouseSensitivity = PlayerPrefs.GetFloat("cv_mouse", 1f);
            FieldOfView = PlayerPrefs.GetFloat("cv_fov", 60f);
            ReduceMotion = PlayerPrefs.GetInt("cv_reducemotion", 0) == 1;
            TtsEnabled = PlayerPrefs.GetInt("cv_tts", 0) == 1;
            VoiceRate = PlayerPrefs.GetFloat("cv_voicerate", 1f);
            if (Array.IndexOf(VoiceRates, VoiceRate) < 0) VoiceRate = 1f;
            ArrowKeysMove = PlayerPrefs.GetInt("cv_arrowmove", 1) == 1;
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
            PlayerPrefs.SetFloat("cv_voicerate", VoiceRate);
            PlayerPrefs.SetInt("cv_arrowmove", ArrowKeysMove ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ---- Drawing ------------------------------------------------------------

        private void Refresh()
        {
            if (card == null) return;
            Color accent = HudUI.Accent;
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                bool sel = i == selected;
                row.Back.color = sel ? new Color(accent.r, accent.g, accent.b, 0.13f) : new Color(1f, 1f, 1f, 0.025f);
                row.Marker.enabled = sel;
                row.LabelText.color = sel ? accent : Ink;

                switch (row.Kind)
                {
                    case Kind.Level:
                        int lit = Mathf.RoundToInt(row.Fraction() * Segments);
                        for (int s = 0; s < Segments; s++)
                            row.Segments[s].color = s < lit ? (sel ? accent : new Color(0.55f, 0.75f, 0.9f)) : Dim;
                        row.ValueText.text = row.Value();
                        break;
                    case Kind.Switch:
                        bool on = row.On();
                        row.Pill.color = on ? accent : Dim;
                        row.PillText.text = on ? "ON" : "OFF";
                        row.PillText.color = on ? new Color(0.02f, 0.05f, 0.08f) : Muted;
                        row.PillText.alignment = on ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                        break;
                    case Kind.Choice:
                        row.ValueText.text = row.Value();
                        row.ValueText.color = sel ? Color.white : Ink;
                        break;
                    case Kind.Action:
                        row.ValueText.text = row.Value();
                        row.ValueText.color = Warn;
                        row.LabelText.color = row.On != null && row.On() ? Warn : sel ? accent : Muted;
                        break;
                }
            }
            helpText.text = HelpFor(rows[selected]);
        }

        private void BuildMenu()
        {
            if (HudUI.Instance == null) return;
            Color accent = HudUI.Accent;

            // Dimmed backdrop behind the card.
            panel = NewImage("SettingsBackdrop", HudUI.Instance.Canvas.transform, new Color(0f, 0.01f, 0.02f, 0.72f));
            Stretch(panel.GetComponent<RectTransform>());

            const float width = 920f, rowHeight = 32f, rowGap = 2f, sectionHeight = 28f, sectionGap = 4f;
            const float top = 92f, helpHeight = 48f, footer = 64f;
            int sections = 0;
            string last = null;
            foreach (Row r in rows) if (r.Section != last) { last = r.Section; sections++; }
            float listHeight = rows.Count * (rowHeight + rowGap) + sections * (sectionHeight + sectionGap);
            float height = top + listHeight + 12f + helpHeight + footer;

            var cardGo = NewImage("SettingsCard", panel.transform, new Color(0.012f, 0.03f, 0.05f, 0.97f));
            card = cardGo.GetComponent<RectTransform>();
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(width, height);
            card.anchoredPosition = new Vector2(0f, -28f); // clear of the objective bar
            HudUI.StylePanel(cardGo, new Color(0.012f, 0.03f, 0.05f, 0.97f), accent);

            Text title = NewText("Title", card, "SETTINGS", 34, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(title.rectTransform, 48f, -24f, 400f, 44f);
            Text paused = NewText("Paused", card, "GAME PAUSED", 17, Muted, TextAnchor.MiddleRight, FontStyle.Bold);
            PlaceRight(paused.rectTransform, 48f, -28f, 300f, 36f);
            var rule = NewImage("Rule", card, new Color(accent.r, accent.g, accent.b, 0.25f));
            Place(rule.GetComponent<RectTransform>(), 48f, -74f, width - 96f, 1f);

            float y = -top;
            last = null;
            foreach (Row row in rows)
            {
                if (row.Section != last)
                {
                    last = row.Section;
                    y -= sectionGap;
                    if (row.Section != null)
                    {
                        Text head = NewText("Section_" + row.Section, card, row.Section.ToUpperInvariant(), 14, Muted,
                            TextAnchor.LowerLeft, FontStyle.Bold);
                        Place(head.rectTransform, 50f, y, 400f, sectionHeight - 6f);
                    }
                    else
                    {
                        var divider = NewImage("Divider", card, new Color(1f, 1f, 1f, 0.08f));
                        Place(divider.GetComponent<RectTransform>(), 48f, y - sectionHeight * 0.5f, width - 96f, 1f);
                    }
                    y -= sectionHeight;
                }
                BuildRow(row, y, width - 96f, rowHeight);
                y -= rowHeight + rowGap;
            }

            helpText = NewText("Help", card, "", 19, new Color(0.78f, 0.86f, 0.94f), TextAnchor.MiddleLeft, FontStyle.Normal);
            helpText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var hrt = helpText.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0f);
            hrt.anchorMax = new Vector2(1f, 0f);
            hrt.pivot = new Vector2(0.5f, 0f);
            hrt.offsetMin = new Vector2(56f, footer);
            hrt.offsetMax = new Vector2(-56f, footer + helpHeight);
            var helpRule = NewImage("HelpRule", card, new Color(accent.r, accent.g, accent.b, 0.18f));
            var hr = helpRule.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 0f);
            hr.anchorMax = new Vector2(1f, 0f);
            hr.pivot = new Vector2(0.5f, 0f);
            hr.offsetMin = new Vector2(48f, footer + helpHeight + 6f);
            hr.offsetMax = new Vector2(-48f, footer + helpHeight + 7f);

            BuildKeys(accent);
        }

        private void BuildRow(Row row, float y, float width, float height)
        {
            Color accent = HudUI.Accent;
            var go = NewImage("Row_" + row.Label.Replace(' ', '_'), card, Color.clear);
            row.Rect = go.GetComponent<RectTransform>();
            Place(row.Rect, 48f, y, width, height);
            row.Back = go.GetComponent<Image>();

            var marker = NewImage("Marker", row.Rect, accent);
            var mrt = marker.GetComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0f, 0f);
            mrt.anchorMax = new Vector2(0f, 1f);
            mrt.pivot = new Vector2(0f, 0.5f);
            mrt.sizeDelta = new Vector2(4f, 0f);
            row.Marker = marker.GetComponent<Image>();

            row.LabelText = NewText("Label", row.Rect, row.Label, 22, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            Place(row.LabelText.rectTransform, 22f, 0f, 360f, height);

            const float controlWidth = 380f;
            switch (row.Kind)
            {
                case Kind.Level:
                {
                    var bar = new GameObject("Bar", typeof(RectTransform)).GetComponent<RectTransform>();
                    bar.SetParent(row.Rect, false);
                    PlaceRight(bar, 104f, -(height - 14f) * 0.5f, controlWidth - 104f, 14f);
                    row.Bar = bar;
                    row.Segments = new Image[Segments];
                    float segment = (controlWidth - 104f) / Segments;
                    for (int s = 0; s < Segments; s++)
                    {
                        var seg = NewImage("Seg" + s, bar, Dim);
                        Place(seg.GetComponent<RectTransform>(), s * segment, 0f, segment - 4f, 14f);
                        row.Segments[s] = seg.GetComponent<Image>();
                    }
                    row.ValueText = NewText("Value", row.Rect, "", 22, Ink, TextAnchor.MiddleRight, FontStyle.Bold);
                    PlaceRight(row.ValueText.rectTransform, 18f, 0f, 80f, height);
                    break;
                }
                case Kind.Switch:
                {
                    var pill = NewImage("Switch", row.Rect, Dim);
                    PlaceRight(pill.GetComponent<RectTransform>(), 18f, -(height - 26f) * 0.5f, 92f, 26f);
                    row.Pill = pill.GetComponent<Image>();
                    row.PillText = NewText("State", pill.transform, "OFF", 16, Muted, TextAnchor.MiddleRight, FontStyle.Bold);
                    var srt = row.PillText.rectTransform;
                    Stretch(srt);
                    srt.offsetMin = new Vector2(12f, 0f);
                    srt.offsetMax = new Vector2(-12f, 0f);
                    break;
                }
                case Kind.Choice:
                {
                    Text left = NewText("Prev", row.Rect, "‹", 30, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                    PlaceRight(left.rectTransform, 18f + controlWidth - 40f, 0f, 40f, height);
                    row.LeftArrow = left.rectTransform;
                    Text right = NewText("Next", row.Rect, "›", 30, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                    PlaceRight(right.rectTransform, 18f, 0f, 40f, height);
                    row.RightArrow = right.rectTransform;
                    row.ValueText = NewText("Value", row.Rect, "", 22, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                    PlaceRight(row.ValueText.rectTransform, 58f, 0f, controlWidth - 80f, height);
                    break;
                }
                case Kind.Action:
                {
                    row.ValueText = NewText("Confirm", row.Rect, "", 20, Warn, TextAnchor.MiddleRight, FontStyle.Bold);
                    PlaceRight(row.ValueText.rectTransform, 18f, 0f, controlWidth, height);
                    break;
                }
            }
        }

        /// <summary>Key caps along the bottom of the card.</summary>
        private void BuildKeys(Color accent)
        {
            var bar = new GameObject("Keys", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bar.transform.SetParent(card, false);
            var brt = bar.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 16f);
            brt.sizeDelta = new Vector2(-96f, 36f);
            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            Key(bar.transform, "↑ ↓", accent); Word(bar.transform, "Select", 70f);
            Spacer(bar.transform, 24f);
            Key(bar.transform, "← →", accent); Word(bar.transform, "Change", 76f);
            Spacer(bar.transform, 24f);
            Key(bar.transform, "ESC", accent); Word(bar.transform, "Resume", 76f);
            Spacer(bar.transform, 24f);
            Word(bar.transform, "or click a setting", 170f, Muted);
        }

        private static void Key(Transform parent, string label, Color accent)
        {
            var cap = NewImage("Key_" + label, parent, new Color(0.10f, 0.16f, 0.22f, 1f));
            var outline = cap.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            Text t = NewText("Label", cap.transform, label, 18, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform);
            var le = cap.AddComponent<LayoutElement>();
            le.preferredWidth = Mathf.Max(40f, t.preferredWidth + 22f);
            le.preferredHeight = 30f;
        }

        private static void Word(Transform parent, string word, float width, Color? color = null)
        {
            Text t = NewText("Word_" + word, parent, word, 18, color ?? Ink, TextAnchor.MiddleLeft,
                color.HasValue ? FontStyle.Normal : FontStyle.Bold);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = Mathf.Max(width, t.preferredWidth + 6f);
            le.preferredHeight = 34f;
        }

        private static void Spacer(Transform parent, float width)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = width;
        }

        // ---- UI helpers ------------------------------------------------------------

        private static GameObject NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return go;
        }

        private static Text NewText(string name, Transform parent, string text, int size, Color color,
            TextAnchor anchor, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = HudUI.UIFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.supportRichText = false;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Top-left anchored: <paramref name="x"/> from the left,
        /// <paramref name="y"/> down from the top (negative).</summary>
        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Top-right anchored: <paramref name="x"/> in from the right.</summary>
        private static void PlaceRight(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-x, y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
