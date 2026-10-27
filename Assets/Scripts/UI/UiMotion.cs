using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Settings;

namespace Cyverse.UI
{
    /// <summary>
    /// The game's one motion vocabulary for screen UI: a few durations, a few
    /// easing curves, and helpers for the moves every panel shares (fade and
    /// settle in, staggered reveals, colour cross-fades, a pulse for success
    /// and a short shake for a wrong pick). Everything runs on unscaled time,
    /// so menus that pause the game still animate.
    ///
    /// Motion is decoration only: game state always changes at once, and a
    /// tween only moves alpha, scale, position or colour toward the state the
    /// code already set. Batch mode (CI) and <see cref="Instant"/> snap every
    /// tween to its end on the spot, so tests never wait on an animation.
    /// Reduce Motion keeps the fades and drops the movement.
    ///
    /// WebGL: nothing runs when no tween is live, and tweens touch only
    /// CanvasGroup alpha, CanvasRenderer alpha, transforms and colours, which
    /// re-batch the canvas but never force a layout or text rebuild.
    /// </summary>
    public static class UiMotion
    {
        // Timing tokens. Exits are faster than entrances; hover is fastest.
        public const float Fast = 0.15f;    // hover, press, colour changes
        public const float Base = 0.22f;    // panels and content coming in
        public const float Slow = 0.30f;    // step changes, larger moves
        public const float Step = 0.035f;   // delay between staggered items

        // Distances, in reference pixels (1920x1080 canvas).
        public const float Rise = 12f;      // how far content travels in
        public const float Nudge = 6f;      // list-row hover shift

        /// <summary>Snap every tween to its end at once. Set by tests and the
        /// TAS player; batch mode implies it.</summary>
        public static bool Instant { get; set; }

        /// <summary>Let tweens play in batch mode, for the tests of this class.</summary>
        public static bool AnimateInBatch { get; set; }

        internal static bool Snap => Instant || (Application.isBatchMode && !AnimateInBatch);
        internal static bool Still => AccessibilitySettings.ReduceMotion;

        // ---- Easing ------------------------------------------------------------

        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float OutQuart(float t) { t = 1f - t; return 1f - t * t * t * t; }
        public static float InOutCubic(float t) =>
            t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        /// <summary>A soft overshoot (about 4%) for pops and success pulses.</summary>
        public static float OutBack(float t)
        {
            const float c1 = 1.2f, c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        // ---- Tween channels -------------------------------------------------------

        // One tween per (target, channel); a new one replaces the old.
        internal const int ChAlpha = 1, ChScale = 2, ChMove = 3, ChColor = 4, ChShake = 5;

        /// <summary>Run <paramref name="apply"/> from 0 to 1 over
        /// <paramref name="duration"/> unscaled seconds after
        /// <paramref name="delay"/>; <paramref name="apply"/> receives the eased
        /// progress. The tween dies with <paramref name="owner"/>.</summary>
        public static void Run(UnityEngine.Object owner, int channel, float duration, float delay,
            Func<float, float> ease, Action<float> apply, Action done = null)
        {
            if (owner == null || apply == null) return;
            if (Snap || duration <= 0f)
            {
                UiMotionRunner.Cancel(owner, channel);
                apply(1f);
                done?.Invoke();
                return;
            }
            apply(0f);
            UiMotionRunner.Add(owner, channel, duration, delay, ease ?? OutCubic, apply, done);
        }

        /// <summary>Jump any running tweens on <paramref name="owner"/> to their end.</summary>
        public static void Finish(UnityEngine.Object owner) => UiMotionRunner.Complete(owner);

        // ---- Building blocks ---------------------------------------------------------

        /// <summary>The CanvasGroup on <paramref name="go"/>, added on first use.</summary>
        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }

        /// <summary>Fade a whole subtree in through its CanvasGroup.</summary>
        public static void FadeIn(GameObject go, float duration = Base, float delay = 0f)
        {
            if (go == null) return;
            CanvasGroup g = Group(go);
            Run(g, ChAlpha, duration, delay, OutCubic, p => g.alpha = p);
        }

        /// <summary>Fade one Graphic in through its CanvasRenderer, leaving its
        /// colour alone. Not for button targets: their tint owns that channel.</summary>
        public static void FadeIn(Graphic graphic, float duration = Fast, float delay = 0f)
        {
            if (graphic == null) return;
            CanvasRenderer cr = graphic.canvasRenderer;
            Run(graphic, ChAlpha, duration, delay, OutCubic, p => cr.SetAlpha(p));
        }

        /// <summary>Settle in from slightly smaller (scale only, so layout and
        /// anchored positions never change).</summary>
        public static void ScaleIn(Transform t, float from = 0.97f, float duration = Base, float delay = 0f)
        {
            if (t == null) return;
            if (Still) { Run(t, ChScale, 0f, 0f, null, p => t.localScale = Vector3.one); return; }
            Run(t, ChScale, duration, delay, OutQuart,
                p => t.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, p));
        }

        /// <summary>Ease the uniform scale from where it is to <paramref name="to"/>.</summary>
        public static void ScaleTo(Transform t, float to, float duration = Fast)
        {
            if (t == null) return;
            float from = t.localScale.x;
            if (Still) to = Mathf.Approximately(to, 0f) ? 0f : 1f;
            Run(t, ChScale, Mathf.Approximately(from, to) ? 0f : duration, 0f, OutCubic,
                p => t.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, p));
        }

        /// <summary>Travel in from <paramref name="offset"/> back to the rect's
        /// current anchored position. A slide already running is finished
        /// first, so the resting position is never lost.</summary>
        public static void SlideIn(RectTransform rt, Vector2 offset, float duration = Base, float delay = 0f)
        {
            if (rt == null) return;
            UiMotionRunner.Complete(rt, ChShake);
            UiMotionRunner.Complete(rt, ChMove);
            if (Still) return;
            Vector2 rest = rt.anchoredPosition;
            Run(rt, ChMove, duration, delay, OutCubic,
                p => rt.anchoredPosition = rest + offset * (1f - p));
        }

        /// <summary>Ease an anchored position from where it is to <paramref name="to"/>.</summary>
        public static void MoveTo(RectTransform rt, Vector2 to, float duration = Base)
        {
            if (rt == null) return;
            Vector2 from = rt.anchoredPosition;
            Run(rt, ChMove, Still || from == to ? 0f : duration, 0f, OutCubic,
                p => rt.anchoredPosition = Vector2.LerpUnclamped(from, to, p));
        }

        /// <summary>Cross-fade a Graphic's colour. Skipped when it is already
        /// there, so calling it from a refresh every frame costs nothing.</summary>
        public static void ColorTo(Graphic g, Color to, float duration = Fast)
        {
            if (g == null) return;
            if (UiMotionRunner.IsRunning(g, ChColor, to)) return;
            Color from = g.color;
            if (from == to) { UiMotionRunner.Cancel(g, ChColor); return; }
            Run(g, ChColor, g.gameObject.activeInHierarchy ? duration : 0f, 0f, OutCubic,
                p => g.color = Color.LerpUnclamped(from, to, p));
            UiMotionRunner.Tag(g, ChColor, to);
        }

        // ---- Composite moves ------------------------------------------------------------

        /// <summary>A panel coming up: fade the whole subtree, and settle the
        /// card (the panel itself unless a card inside a backdrop is given).</summary>
        public static void PanelIn(GameObject panel, Transform card = null, float fromScale = 0.97f)
        {
            if (panel == null) return;
            FadeIn(panel, Base);
            ScaleIn(card != null ? card : panel.transform, fromScale, Slow);
        }

        /// <summary>Content arriving: fade in and rise a few pixels.</summary>
        public static void Reveal(GameObject go, float delay = 0f, float rise = Rise, float duration = Base)
        {
            if (go == null || !go.activeInHierarchy) return;
            FadeIn(go, duration, delay);
            if (go.transform is RectTransform rt) SlideIn(rt, new Vector2(0f, -rise), duration, delay);
        }

        /// <summary>Reveal each active item in order, <see cref="Step"/> apart.</summary>
        public static void Stagger<T>(IList<T> items, float delay = 0f, float rise = Rise,
            float duration = Base, float step = Step) where T : Component
        {
            if (items == null) return;
            int n = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null || !items[i].gameObject.activeInHierarchy) continue;
                Reveal(items[i].gameObject, delay + n * step, rise, duration);
                n++;
            }
        }

        /// <summary>A small success pop: up a few percent and back.</summary>
        public static void Pulse(Transform t, float amount = 0.06f, float duration = Slow)
        {
            if (t == null) return;
            if (Still) { t.localScale = Vector3.one; return; }
            Run(t, ChScale, duration, 0f, x => x,
                p => t.localScale = Vector3.one * (1f + amount * Mathf.Sin(p * Mathf.PI) * (1f - p * 0.35f)));
        }

        /// <summary>A short damped side-to-side shake for a wrong pick. Under
        /// Reduce Motion the colour change alone carries the message.</summary>
        public static void Shake(RectTransform rt, float amplitude = 7f, float duration = 0.28f)
        {
            if (rt == null || Still) return;
            UiMotionRunner.Complete(rt, ChShake);
            UiMotionRunner.Complete(rt, ChMove);
            Vector2 rest = rt.anchoredPosition;
            Run(rt, ChShake, duration, 0f, x => x, p =>
                rt.anchoredPosition = rest + new Vector2(
                    amplitude * Mathf.Sin(p * Mathf.PI * 5f) * (1f - p) * (1f - p), 0f));
        }

        /// <summary>Give a button hover and press feedback. Rows nudge their
        /// label sideways (scaling a row in a tight list overlaps its
        /// neighbours); stand-alone buttons grow a touch and dip when pressed.
        /// Also smooths the tint cross-fade.</summary>
        public static UiPressable AddPress(Button button, bool row = false)
        {
            if (button == null) return null;
            ColorBlock colors = button.colors;
            colors.fadeDuration = Fast * 0.8f;
            button.colors = colors;
            var press = button.GetComponent<UiPressable>();
            if (press == null) press = button.gameObject.AddComponent<UiPressable>();
            press.Row = row;
            return press;
        }
    }
}
