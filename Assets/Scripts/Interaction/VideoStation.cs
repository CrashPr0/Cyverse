using System;
using UnityEngine;
using UnityEngine.Video;
using TMPro;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The in-game briefing TV for the level template:
    ///   - E plays/pauses (and replays after the end)
    ///   - Left/Right arrows scrub while the player is near
    ///   - a progress bar under the screen shows position
    ///   - <see cref="FirstCompleted"/> fires the first time playback reaches
    ///     the end — the level manager uses it to unlock the door
    ///   - fully repeatable afterwards
    /// Content: assign a VideoClip or URL (Unity VideoPlayer; for WebGL use a
    /// URL) and the screen shows the video. With neither assigned it falls
    /// back to timed text "slides" on the screen, so the whole flow is
    /// playable before any video files exist — drop the real video in later
    /// without touching level logic.
    /// </summary>
    public class VideoStation : MonoBehaviour, IInteractable
    {
        [Serializable]
        public class Slide
        {
            public string title;
            [TextArea] public string body;
            public float duration = 9f;

            public Slide(string title, string body, float duration = 9f)
            {
                this.title = title;
                this.body = body;
                this.duration = duration;
            }
        }

        [Header("Content (video takes priority over slides)")]
        public VideoClip clip;
        public string videoUrl;
        public Slide[] slides;

        [Header("Controls")]
        public float scrubSecondsPerSecond = 8f;
        public float controlRange = 7f;

        /// <summary>Fires once, the first time playback reaches the end.</summary>
        public event Action FirstCompleted;
        public bool HasCompletedOnce { get; private set; }

        // Wired by Build().
        public Renderer screenRenderer;
        public TextMeshPro titleText;
        public TextMeshPro bodyText;
        public Transform barFill;
        private TextMeshPro controlsText;

        private VideoPlayer vp;
        private bool useVideo;
        private bool playing;
        private float time;    // slides mode clock
        private int lastSlide = -1;

        void Awake()
        {
            ResolveVisualReferences();
            UpgradeLegacyText();
            NormalizeLayout();
        }

        void OnValidate()
        {
            ResolveVisualReferences();
            NormalizeLayout();
        }

        private void ResolveVisualReferences()
        {
            if (screenRenderer == null)
            {
                Transform t = transform.Find("Screen");
                if (t != null) screenRenderer = t.GetComponent<Renderer>();
            }
            if (titleText == null)
            {
                Transform t = transform.Find("TitleText");
                if (t != null) titleText = t.GetComponent<TextMeshPro>();
                if (titleText == null)
                {
                    t = transform.Find("TitleText_TMP");
                    if (t != null) titleText = t.GetComponent<TextMeshPro>();
                }
            }
            if (bodyText == null)
            {
                Transform t = transform.Find("BodyText");
                if (t != null) bodyText = t.GetComponent<TextMeshPro>();
                if (bodyText == null)
                {
                    t = transform.Find("BodyText_TMP");
                    if (t != null) bodyText = t.GetComponent<TextMeshPro>();
                }
            }
            if (controlsText == null)
            {
                Transform t = transform.Find("ControlsText");
                if (t == null) t = transform.Find("ControlsText_TMP");
                if (t != null) controlsText = t.GetComponent<TextMeshPro>();
            }
            if (barFill == null) barFill = transform.Find("BarFillPivot");
        }

        /// <summary>Visual-pass scenes serialize the former TextMesh objects.
        /// Convert them once at runtime so old scenes gain TMP without being
        /// rebuilt, while new scene factories create TMP directly.</summary>
        private void UpgradeLegacyText()
        {
            if (titleText == null)
                titleText = UpgradeText("TitleText", "TitleText_TMP", new Vector3(0f, 2.92f, -0.20f),
                    new Vector2(3.75f, 0.58f), 38f, 20f, true, new Color(0.25f, 0.65f, 1f));
            if (bodyText == null)
                bodyText = UpgradeText("BodyText", "BodyText_TMP", new Vector3(0f, 2.08f, -0.20f),
                    new Vector2(3.60f, 1.30f), 29f, 18f, false, new Color(0.92f, 0.96f, 1f));
            if (controlsText == null)
                controlsText = UpgradeText("PanelLabel", "ControlsText_TMP", new Vector3(0f, 0.28f, -0.30f),
                    new Vector2(3.8f, 0.34f), 24f, 16f, false, new Color(0.95f, 0.98f, 1f),
                    "E play/pause  ·  ←/→ scrub");
        }

        private TextMeshPro UpgradeText(string legacyName, string tmpName, Vector3 localPosition,
            Vector2 worldBounds, float maxSize, float minSize, bool bold, Color fallbackColor,
            string fallbackText = "")
        {
            Transform legacyTransform = transform.Find(legacyName);
            TextMesh legacy = legacyTransform != null ? legacyTransform.GetComponent<TextMesh>() : null;
            string content = legacy != null ? legacy.text : fallbackText;
            Color color = legacy != null ? legacy.color : fallbackColor;

            TextMeshPro tmp = MakeTmp(transform, tmpName, localPosition, worldBounds,
                maxSize, minSize, bold, color, content);
            if (legacyTransform != null) legacyTransform.gameObject.SetActive(false);
            return tmp;
        }

        private void NormalizeLayout()
        {
            Transform track = transform.Find("BarTrack");
            if (track != null)
            {
                // Forward of the frame and central stand, preventing the
                // tracker from disappearing into either surface.
                track.localPosition = new Vector3(0f, 1.05f, -0.24f);
                track.localScale = new Vector3(4f, 0.055f, 0.025f);
            }
            if (barFill != null)
            {
                barFill.localPosition = new Vector3(-2f, 1.05f, -0.27f);
                Transform fill = barFill.Find("BarFill");
                if (fill != null)
                {
                    fill.localPosition = new Vector3(2f, 0f, 0f);
                    fill.localRotation = Quaternion.identity;
                    fill.localScale = new Vector3(4f, 0.04f, 0.025f);
                }
            }
            Transform legacyControls = transform.Find("PanelLabel");
            if (legacyControls != null)
            {
                legacyControls.localPosition = new Vector3(0f, 0.28f, -0.30f);
                TextMesh tm = legacyControls.GetComponent<TextMesh>();
                if (tm != null) tm.characterSize = 0.020f;
            }
            if (controlsText != null) controlsText.transform.localPosition = new Vector3(0f, 0.28f, -0.30f);
            if (titleText != null) titleText.transform.localPosition = new Vector3(0f, 2.92f, -0.20f);
            if (bodyText != null)
                bodyText.transform.localPosition = new Vector3(0f, 2.08f, -0.20f);
        }

        public string Prompt => playing ? "Pause Briefing"
            : AtEnd ? "Replay Briefing"
            : HasCompletedOnce ? "Play Briefing" : "Play Security Briefing";
        public bool CanInteract => true;

        private float Duration
        {
            get
            {
                if (useVideo) return vp != null && vp.length > 0.5 ? (float)vp.length : 1f;
                float total = 0f;
                if (slides != null) foreach (var s in slides) total += Mathf.Max(0.5f, s.duration);
                return Mathf.Max(1f, total);
            }
        }

        private float Position => useVideo ? (vp != null ? (float)vp.time : 0f) : time;
        private bool AtEnd => Position >= Duration - 0.05f;

        void Start()
        {
            useVideo = clip != null || !string.IsNullOrEmpty(videoUrl);
            if (useVideo) SetUpVideoPlayer();
            RefreshScreen();
        }

        private void SetUpVideoPlayer()
        {
            vp = gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.isLooping = false;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            if (clip != null) vp.clip = clip;
            else { vp.source = VideoSource.Url; vp.url = videoUrl; }

            var rt = new RenderTexture(1024, 576, 0);
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            if (screenRenderer != null)
            {
                screenRenderer.material.SetTexture("_MainTex", rt);
                screenRenderer.material.color = Color.white;
            }
            if (titleText != null) titleText.gameObject.SetActive(false);
            if (bodyText != null) bodyText.gameObject.SetActive(false);
            vp.loopPointReached += _ => OnReachedEnd();
            vp.Prepare();
        }

        public void Interact(GameObject interactor)
        {
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();

            if (playing) { SetPlaying(false); return; }
            if (AtEnd) Seek(0f);
            SetPlaying(true);
        }

        private void SetPlaying(bool value)
        {
            playing = value;
            if (useVideo && vp != null)
            {
                if (value) vp.Play();
                else vp.Pause();
            }
        }

        void Update()
        {
            // Scrubbing: hold Left/Right near the screen (menus block via Busy
            // elsewhere; scrub keys are harmless during dialogue).
            var cam = Camera.main;
            bool near = cam != null &&
                (cam.transform.position - transform.position).sqrMagnitude < controlRange * controlRange;
            if (near && !Core.GameState.AnyMenuOpen)
            {
                float scrub = 0f;
                if (Input.GetKey(KeyCode.LeftArrow)) scrub -= 1f;
                if (Input.GetKey(KeyCode.RightArrow)) scrub += 1f;
                if (scrub != 0f)
                    Seek(Position + scrub * scrubSecondsPerSecond * Time.deltaTime);
            }

            if (!useVideo && playing)
            {
                time += Time.deltaTime;
                if (time >= Duration)
                {
                    time = Duration;
                    SetPlaying(false);
                    OnReachedEnd();
                }
            }

            RefreshScreen();
        }

        private void Seek(float to)
        {
            to = Mathf.Clamp(to, 0f, Duration);
            if (useVideo && vp != null) vp.time = to;
            else time = to;
        }

        private void OnReachedEnd()
        {
            playing = false;
            if (HasCompletedOnce) return;
            HasCompletedOnce = true;
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            FirstCompleted?.Invoke();
        }

        private void RefreshScreen()
        {
            if (barFill != null)
            {
                var s = barFill.localScale;
                s.x = Mathf.Clamp01(Position / Duration);
                barFill.localScale = s;
            }

            if (useVideo || slides == null || slides.Length == 0) return;

            // Which slide is the clock inside?
            float t = time;
            int idx = 0;
            for (int i = 0; i < slides.Length; i++)
            {
                float d = Mathf.Max(0.5f, slides[i].duration);
                if (t < d || i == slides.Length - 1) { idx = i; break; }
                t -= d;
            }
            if (idx == lastSlide) return;
            lastSlide = idx;

            if (titleText != null) titleText.text = slides[idx].title;
            if (bodyText != null) bodyText.text = slides[idx].body;
        }

        // ---- Construction ----------------------------------------------------

        /// <summary>Builds the TV (stand, frame, screen, progress bar, signage)
        /// facing local -Z, and returns the wired VideoStation.</summary>
        public static VideoStation Build(Vector3 position, float rotY, Slide[] slides, Color accent)
        {
            var root = new GameObject("BriefingScreen");
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            Material dark = BuildKit.MakeStandard(new Color(0.06f, 0.07f, 0.10f), 0.5f, 0.4f);

            Child(root.transform, PrimitiveType.Cube, "Stand",
                new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 1.1f, 0.4f), dark, true);
            Child(root.transform, PrimitiveType.Cube, "Frame",
                new Vector3(0f, 2.15f, 0f), new Vector3(4.3f, 2.5f, 0.18f), dark, true);

            // Unity's Quad renders on its LOCAL -Z face, so identity rotation
            // already faces the viewer standing on -Z. (It was flipped 180°
            // before, which backface-culled the display surface — invisible
            // screen, and a real video would never have shown.)
            var screen = Child(root.transform, PrimitiveType.Quad, "Screen",
                new Vector3(0f, 2.15f, -0.11f), new Vector3(4.0f, 2.2f, 1f),
                BuildKit.MakeStandard(new Color(0.02f, 0.03f, 0.05f), 0.2f, 0f), false);

            var title = MakeTmp(root.transform, "TitleText", new Vector3(0f, 2.92f, -0.20f),
                new Vector2(3.75f, 0.58f), 38f, 20f, true, accent, "");
            var body = MakeTmp(root.transform, "BodyText", new Vector3(0f, 2.08f, -0.20f),
                new Vector2(3.60f, 1.30f), 29f, 18f, false, new Color(0.92f, 0.96f, 1f), "");

            Child(root.transform, PrimitiveType.Cube, "BarTrack",
                new Vector3(0f, 1.05f, -0.24f), new Vector3(4.0f, 0.055f, 0.025f),
                BuildKit.MakeStandard(new Color(0.15f, 0.17f, 0.22f), 0.3f, 0f), false);

            var fill = Child(root.transform, PrimitiveType.Cube, "BarFill",
                new Vector3(-2.0f, 1.05f, -0.27f), new Vector3(4.0f, 0.04f, 0.025f),
                BuildKit.MakeEmissive(accent, 2f), false);
            // Pivot the fill from the left edge so localScale.x = progress.
            var pivot = new GameObject("BarFillPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(-2.0f, 1.05f, -0.27f);
            fill.transform.SetParent(pivot, true);
            fill.transform.localPosition = new Vector3(2.0f, 0f, 0f);
            var fs = pivot.localScale; fs.x = 0f; pivot.localScale = fs;

            // The screen is the room's light source — without this the video
            // room is lit by ambient alone.
            var glow = new GameObject("ScreenLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.15f, -1.6f);
            var sl = glow.AddComponent<Light>();
            sl.type = LightType.Point;
            sl.color = accent;
            sl.range = 12f;
            sl.intensity = 2.2f;

            BuildKit.MakeSign(root.transform, position + new Vector3(0f, 3.9f, 0f),
                "SECURITY BRIEFING", accent, 0.035f);
            var controls = MakeTmp(root.transform, "ControlsText", new Vector3(0f, 0.28f, -0.30f),
                new Vector2(3.8f, 0.34f), 24f, 16f, false, new Color(0.95f, 0.98f, 1f),
                "E play/pause  ·  ←/→ scrub");

            var station = root.AddComponent<VideoStation>();
            station.slides = slides;
            station.screenRenderer = screen.GetComponent<Renderer>();
            station.titleText = title;
            station.bodyText = body;
            station.controlsText = controls;
            station.barFill = pivot;
            station.NormalizeLayout();
            return station;
        }

        private static GameObject Child(Transform parent, PrimitiveType type, string name,
            Vector3 localPos, Vector3 localScale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider) BuildKit.StripCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static TextMeshPro MakeTmp(Transform parent, string name, Vector3 localPos,
            Vector2 worldBounds, float maxSize, float minSize, bool bold, Color color, string text)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.1f;

            var tmp = go.GetComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.enableWordWrapping = true;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = maxSize;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.rectTransform.sizeDelta = worldBounds * 10f;
            return tmp;
        }
    }
}
