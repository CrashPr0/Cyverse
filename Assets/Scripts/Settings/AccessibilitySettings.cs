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

        /// <summary>Browser text-to-speech for unvoiced dialogue (WebGL builds).</summary>
        public bool TtsEnabled { get; private set; } = true;

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

            public RectTransform Rect, Bar, LeftArrow;
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
            Switch("Audio", "Voiceover", () => TtsEnabled
                    ? "Reads dialogue that has no recorded voice aloud, using your browser's voice."
                    : "Off: dialogue without a recorded voice is shown as text only.",
                () => TtsEnabled, () => { TtsEnabled = !TtsEnabled; if (!TtsEnabled) Speech.Cancel(); });

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
                Value = () => resetArmed ? "Press again to confirm" : "",
                Step = _ => { if (resetArmed) RestoreDefaults(); resetArmed = !resetArmed; },
            });
        }

        private void Add(Row row) => rows.Add(row);

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
            // One-menu-at-a-time standard: Esc toggles settings only when
            // settings is already the open menu, or when nothing owns the
            // screen. This keeps it from stacking over the title screen, the
            // glossary (whose close key is also Esc), a quiz, or the results.
            // The MenuTransitionFrame check stops the Esc press that closed
            // the glossary from also opening settings later this same frame.
            bool canToggle = (GameState.MenuOpen || !GameState.AnyMenuOpen)
                             && Time.frameCount != GameState.MenuTransitionFrame;
            if (GameInput.CancelDown && canToggle)
                SetMenuOpen(!GameState.MenuOpen);

            if (!GameState.MenuOpen || panel == null) return;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Select(selected - 1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Select(selected + 1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Change(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Change(1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                     Input.GetKeyDown(KeyCode.Space))
                Change(1);

            HandleMouse();
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
                else
                    Change(1);
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
                resetArmed = false;
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
            if (sound && Sfx.Instance != null) Sfx.Instance.PlayClick();
            Refresh();
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

        private void RestoreDefaults()
        {
            MasterVolume = VoiceVolume = SfxVolume = 1f;
            CaptionScale = 1f;
            MouseSensitivity = 1f;
            FieldOfView = 60f;
            ReduceMotion = false;
            TtsEnabled = true;
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
            TtsEnabled = PlayerPrefs.GetInt("cv_tts", 1) == 1;
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
                        row.LabelText.color = resetArmed ? Warn : sel ? accent : Muted;
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

            const float width = 920f, rowHeight = 38f, rowGap = 3f, sectionHeight = 28f, sectionGap = 4f;
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
