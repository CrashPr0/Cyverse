using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;

namespace Cyverse.UI
{
    /// <summary>
    /// Shared frame for the short arcade rounds played on Level 4's game
    /// machines. A round runs after the player picks the right attack choice
    /// and acts that step out; it never blocks progress. The frame owns the
    /// cabinet-style panel, the INSERT TOKEN start, the result screen with
    /// prize tickets, Esc to skip, and the hand-back to whoever launched it.
    /// Subclasses draw their own playfield and run their own rules.
    ///
    /// Drawn on the HUD canvas over the station's modal, and timed with
    /// unscaled time because that modal may pause the game clock.
    /// </summary>
    public abstract class ArcadeRound : MonoBehaviour
    {
        public enum State { Attract, Playing, Result }

        /// <summary>The round on screen, if any.</summary>
        public static ArcadeRound Active { get; private set; }
        public static bool IsPlaying => Active != null;

        /// <summary>Tickets won this playthrough (resets on reload with the
        /// rest of the run).</summary>
        public static int TicketsThisRun { get; private set; }

        public State Phase { get; private set; }
        public bool Won { get; private set; }
        public int Tickets { get; private set; }
        /// <summary>True when the player left with Esc instead of finishing.</summary>
        public bool Skipped { get; private set; }

        protected abstract string MachineName { get; }
        protected abstract string Tagline { get; }
        protected abstract string PlayKeys { get; }
        protected abstract Color Accent { get; }
        protected virtual Vector2 PanelSize => new Vector2(980f, 660f);

        /// <summary>Builds the round's art inside the playfield rect.</summary>
        protected abstract void BuildPlayfield(RectTransform field);
        protected abstract void BeginPlay();
        protected abstract void TickPlay(float dt);
        /// <summary>Test hook: jump straight to a win or a loss.</summary>
        protected abstract void ResolveForTest(bool win);

        protected static readonly Color Ink = new Color(0.86f, 0.93f, 1f);
        protected static readonly Color Dim = new Color(0.45f, 0.54f, 0.64f);
        protected static readonly Color Good = new Color(0.30f, 0.88f, 0.53f);
        protected static readonly Color Bad = new Color(1f, 0.42f, 0.36f);
        protected static readonly Color Gold = new Color(1f, 0.78f, 0.22f);

        private Action<ArcadeRound> onDone;
        private GameObject panel;
        private GameObject overlay;
        private TMP_Text overlayText;
        private TMP_Text statusText, feedbackText, keysText;
        private float feedbackLeft;
        private float resultAge;
        private bool ended;

        protected RectTransform Field { get; private set; }

        /// <summary>Starts a round. <paramref name="done"/> runs once, when the
        /// player continues past the result screen or skips with Esc.</summary>
        public static T Launch<T>(Action<ArcadeRound> done) where T : ArcadeRound
        {
            if (Active != null) Active.Abort();
            var round = new GameObject(typeof(T).Name).AddComponent<T>();
            round.onDone = done;
            round.Build();
            if (round.panel == null)
            {
                // No HUD to draw on (headless edge case): hand straight back.
                Destroy(round.gameObject);
                done?.Invoke(round);
                return null;
            }
            Active = round;
            round.ShowAttract();
            return round;
        }

        /// <summary>Removes the round without calling back (its owner closed).</summary>
        public void Abort()
        {
            ended = true;
            onDone = null;
            if (Active == this) Active = null;
            if (panel != null) Destroy(panel);
            Destroy(gameObject);
        }

        /// <summary>Test hook: finish now as a win or a loss, then continue.</summary>
        public void CompleteForTest(bool win)
        {
            if (Phase == State.Attract) { Phase = State.Playing; HideOverlay(); BeginPlay(); }
            if (Phase == State.Playing) ResolveForTest(win);
            if (Phase == State.Result) End();
        }

        /// <summary>Test hook: press start without a key.</summary>
        public void StartForTest()
        {
            if (Phase != State.Attract) return;
            Phase = State.Playing;
            HideOverlay();
            if (keysText != null) keysText.text = PlayKeys + "     ·     ESC SKIP";
            BeginPlay();
        }

        /// <summary>Test hook: leave the way Esc does.</summary>
        public void SkipForTest() => Skip();

        private void Update()
        {
            if (ended) return;
            float dt = Time.unscaledDeltaTime;
            if (feedbackLeft > 0f && (feedbackLeft -= dt) <= 0f && feedbackText != null)
                feedbackText.text = string.Empty;

            switch (Phase)
            {
                case State.Attract:
                    if (GameInput.CancelDown) { Skip(); return; }
                    if (ContinuePressed())
                    {
                        Phase = State.Playing;
                        HideOverlay();
                        if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                        if (keysText != null) keysText.text = PlayKeys + "     ·     ESC SKIP";
                        BeginPlay();
                    }
                    else PulseOverlay();
                    break;
                case State.Playing:
                    if (GameInput.CancelDown) { Skip(); return; }
                    TickPlay(dt);
                    break;
                case State.Result:
                    resultAge += dt;
                    // A short beat so a held key from play doesn't skip it.
                    if (resultAge > 0.6f && (ContinuePressed() || GameInput.CancelDown)) End();
                    else PulseOverlay();
                    break;
            }
        }

        private static bool ContinuePressed() =>
            Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter);

        // ---- For subclasses ---------------------------------------------------------

        protected void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        protected void Flash(string text, float seconds = 2.4f)
        {
            if (feedbackText == null) return;
            feedbackText.text = text;
            feedbackLeft = seconds;
        }

        /// <summary>Ends play and shows the result screen.</summary>
        protected void Finish(bool won, int tickets, string headline, string detail)
        {
            if (Phase == State.Result || ended) return;
            Phase = State.Result;
            Won = won;
            Tickets = Mathf.Max(0, tickets);
            TicketsThisRun += Tickets;
            if (Tickets > 0) ScoreSystem.Add(Tickets);
            resultAge = 0f;
            if (Sfx.Instance != null)
            {
                if (won) Sfx.Instance.PlayTaskComplete();
                else Sfx.Instance.PlayDeny();
            }
            string color = won ? "#4CE087" : "#FF8A6A";
            ShowOverlay(
                $"<size=46><b><color={color}>{headline}</color></b></size>\n\n" +
                $"<size=21>{detail}</size>\n\n" +
                $"<size=30><color=#FFC838><b>+{Tickets} TICKETS</b></color></size>\n\n" +
                "<size=19><color=#9FB4C4>PRESS SPACE TO CONTINUE</color></size>");
            if (keysText != null) keysText.text = "SPACE / ENTER CONTINUE";
        }

        protected static RectTransform Box(Transform parent, string name, Color color,
            Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rt;
        }

        protected static TMP_Text Label(Transform parent, string name, string text, float size,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = size;
            t.alignment = alignment;
            t.richText = true;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            t.color = color;
            t.text = text;
            Fill(t.rectTransform);
            return t;
        }

        protected static void Fill(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        // ---- Frame --------------------------------------------------------------------

        private void Skip()
        {
            Skipped = true;
            Won = false;
            Tickets = 0;
            End();
        }

        private void End()
        {
            if (ended) return;
            ended = true;
            if (Active == this) Active = null;
            Action<ArcadeRound> callback = onDone;
            onDone = null;
            if (panel != null) Destroy(panel);
            Destroy(gameObject);
            callback?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (panel != null) Destroy(panel);
            if (Active == this) Active = null;
        }

        private void ShowAttract()
        {
            Phase = State.Attract;
            ShowOverlay(
                $"<size=26><color=#9FB4C4>{MachineName}</color></size>\n" +
                "<size=50><b>INSERT TOKEN</b></size>\n\n" +
                $"<size=22>{Tagline}</size>\n\n" +
                "<size=24><color=#FFC838><b>PRESS SPACE TO PLAY</b></color></size>\n" +
                "<size=17><color=#7F93A3>or ESC to skip straight to the lesson</color></size>");
            if (keysText != null) keysText.text = "SPACE PLAY     ·     ESC SKIP";
        }

        private void ShowOverlay(string text)
        {
            if (overlay == null) return;
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
            overlayText.text = text;
            overlayText.alpha = 1f;
        }

        private void HideOverlay()
        {
            if (overlay != null) overlay.SetActive(false);
        }

        private void PulseOverlay()
        {
            if (overlayText == null) return;
            // A gentle attract-mode throb; the whole block stays readable.
            overlayText.alpha = 0.86f + 0.14f * Mathf.Sin(Time.unscaledTime * 4f);
        }

        private void Build()
        {
            if (HudUI.Instance == null) return;

            panel = new GameObject(GetType().Name + "Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            panel.transform.SetAsLastSibling();
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = PanelSize;
            HudUI.StylePanel(panel, new Color(0.035f, 0.025f, 0.05f, 0.985f), Accent);

            // Marquee header: the machine's name in its own lit strip.
            RectTransform marquee = Box(panel.transform, "Marquee", new Color(Accent.r * 0.22f,
                Accent.g * 0.22f, Accent.b * 0.22f, 1f), Vector2.zero, Vector2.zero);
            marquee.anchorMin = new Vector2(0f, 1f);
            marquee.anchorMax = new Vector2(1f, 1f);
            marquee.pivot = new Vector2(0.5f, 1f);
            marquee.sizeDelta = new Vector2(-28f, 58f);
            marquee.anchoredPosition = new Vector2(0f, -16f);
            TMP_Text title = Label(marquee, "Title", $"<b>{MachineName}</b>", 34f, Accent);
            title.characterSpacing = 8f;
            title.enableWordWrapping = false;

            statusText = Label(panel.transform, "Status", string.Empty, 21f, Ink);
            RectTransform srt = statusText.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(-40f, 30f);
            srt.anchoredPosition = new Vector2(0f, -80f);

            // Playfield: the "glass" the round draws behind.
            var fieldGo = new GameObject("Playfield", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            fieldGo.transform.SetParent(panel.transform, false);
            Field = fieldGo.GetComponent<RectTransform>();
            Field.anchorMin = Vector2.zero;
            Field.anchorMax = Vector2.one;
            Field.offsetMin = new Vector2(24f, 92f);
            Field.offsetMax = new Vector2(-24f, -118f);
            var fieldImage = fieldGo.GetComponent<Image>();
            fieldImage.color = new Color(0.015f, 0.02f, 0.035f, 1f);
            fieldImage.raycastTarget = false;
            var edge = fieldGo.AddComponent<Outline>();
            edge.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
            edge.effectDistance = new Vector2(2f, 2f);

            BuildPlayfield(Field);

            overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(Field, false);
            Fill(overlay.GetComponent<RectTransform>());
            var shade = overlay.GetComponent<Image>();
            shade.color = new Color(0.01f, 0.01f, 0.02f, 0.95f);
            shade.raycastTarget = false;
            overlayText = Label(overlay.transform, "OverlayText", string.Empty, 22f, Color.white);
            Fill(overlayText.rectTransform, 30f);
            overlayText.alignment = TextAlignmentOptions.Center;
            overlay.SetActive(false);

            feedbackText = Label(panel.transform, "Feedback", string.Empty, 20f, Ink);
            RectTransform frt = feedbackText.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(1f, 0f);
            frt.pivot = new Vector2(0.5f, 0f);
            frt.sizeDelta = new Vector2(-40f, 50f);
            frt.anchoredPosition = new Vector2(0f, 38f);

            keysText = Label(panel.transform, "Keys", string.Empty, 16f, Dim);
            RectTransform krt = keysText.rectTransform;
            krt.anchorMin = new Vector2(0f, 0f);
            krt.anchorMax = new Vector2(1f, 0f);
            krt.pivot = new Vector2(0.5f, 0f);
            krt.sizeDelta = new Vector2(-40f, 24f);
            krt.anchoredPosition = new Vector2(0f, 10f);
            keysText.characterSpacing = 2f;

            UiMotion.PanelIn(panel, null, 0.97f);
        }
    }
}
