using System;
using UnityEngine;
using UnityEngine.Video;
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
        public TextMesh titleText;
        public TextMesh bodyText;
        public Transform barFill;

        private VideoPlayer vp;
        private bool useVideo;
        private bool playing;
        private float time;    // slides mode clock
        private int lastSlide = -1;

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
            if (bodyText != null) bodyText.text = Wrap(slides[idx].body, 44);
        }

        /// <summary>TextMesh has no word-wrap; insert line breaks manually.</summary>
        private static string Wrap(string text, int maxLine)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder();
            int lineLen = 0;
            foreach (string word in text.Split(' '))
            {
                if (lineLen > 0 && lineLen + word.Length + 1 > maxLine)
                {
                    sb.Append('\n');
                    lineLen = 0;
                }
                else if (lineLen > 0)
                {
                    sb.Append(' ');
                    lineLen++;
                }
                sb.Append(word);
                lineLen += word.Length;
            }
            return sb.ToString();
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

            var screen = Child(root.transform, PrimitiveType.Quad, "Screen",
                new Vector3(0f, 2.15f, -0.11f), new Vector3(4.0f, 2.2f, 1f),
                BuildKit.MakeStandard(new Color(0.02f, 0.03f, 0.05f), 0.2f, 0f), false);
            screen.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face local -Z

            var font = HudUI.LoadFont();
            var title = MakeTm(root.transform, "TitleText", new Vector3(0f, 2.95f, -0.13f), 0.05f, FontStyle.Bold, accent, font);
            var body = MakeTm(root.transform, "BodyText", new Vector3(0f, 2.1f, -0.13f), 0.032f, FontStyle.Normal, new Color(0.92f, 0.96f, 1f), font);

            Child(root.transform, PrimitiveType.Cube, "BarTrack",
                new Vector3(0f, 0.82f, -0.12f), new Vector3(4.0f, 0.08f, 0.03f),
                BuildKit.MakeStandard(new Color(0.15f, 0.17f, 0.22f), 0.3f, 0f), false);

            var fill = Child(root.transform, PrimitiveType.Cube, "BarFill",
                new Vector3(-2.0f, 0.82f, -0.125f), new Vector3(4.0f, 0.06f, 0.03f),
                BuildKit.MakeEmissive(accent, 2f), false);
            // Pivot the fill from the left edge so localScale.x = progress.
            var pivot = new GameObject("BarFillPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(-2.0f, 0.82f, -0.125f);
            fill.transform.SetParent(pivot, true);
            fill.transform.localPosition = new Vector3(2.0f, 0f, 0f);
            var fs = pivot.localScale; fs.x = 0f; pivot.localScale = fs;

            BuildKit.MakeSign(root.transform, position + new Vector3(0f, 3.9f, 0f),
                "SECURITY BRIEFING", accent, 0.035f);
            BuildKit.AddPanelLabel(root.transform, position + new Vector3(0f, 0.45f, -0.3f),
                "E play/pause · ←/→ scrub");

            var station = root.AddComponent<VideoStation>();
            station.slides = slides;
            station.screenRenderer = screen.GetComponent<Renderer>();
            station.titleText = title;
            station.bodyText = body;
            station.barFill = pivot;
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

        private static TextMesh MakeTm(Transform parent, string name, Vector3 localPos,
            float charSize, FontStyle style, Color color, Font font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            // TextMesh is readable from the side its forward points AWAY from;
            // the viewer stands on local -Z, so identity (+Z forward) is right.
            go.transform.localRotation = Quaternion.identity;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.fontStyle = style;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }
    }
}
