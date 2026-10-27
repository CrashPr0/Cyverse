using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cyverse.UI
{
    /// <summary>
    /// Drives the tweens <see cref="UiMotion"/> starts. One hidden object for
    /// the whole game, created on first use and kept across scene loads. It
    /// ticks in LateUpdate on unscaled time, so a tween wins over any code that
    /// set the same value earlier in the frame, and it switches itself off
    /// when nothing is moving.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class UiMotionRunner : MonoBehaviour
    {
        private sealed class Tween
        {
            public UnityEngine.Object owner;
            public int channel;
            public float start, delay, duration;
            public Func<float, float> ease;
            public Action<float> apply;
            public Action done;
            public bool hasTag;
            public Color tag;
            public bool dead;
        }

        private static UiMotionRunner instance;
        private readonly List<Tween> tweens = new List<Tween>();

        private static UiMotionRunner Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("UiMotion") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<UiMotionRunner>();
            return instance;
        }

        internal static void Add(UnityEngine.Object owner, int channel, float duration, float delay,
            Func<float, float> ease, Action<float> apply, Action done)
        {
            UiMotionRunner r = Get();
            r.Remove(owner, channel, complete: false);
            r.tweens.Add(new Tween
            {
                owner = owner, channel = channel, start = Time.unscaledTime,
                delay = delay, duration = duration, ease = ease, apply = apply, done = done,
            });
            r.enabled = true;
        }

        internal static void Cancel(UnityEngine.Object owner, int channel)
        {
            if (instance != null) instance.Remove(owner, channel, complete: false);
        }

        /// <summary>Jump the tween on (owner, channel) to its end; channel 0 means all.</summary>
        internal static void Complete(UnityEngine.Object owner, int channel = 0)
        {
            if (instance != null) instance.Remove(owner, channel, complete: true);
        }

        internal static bool IsRunning(UnityEngine.Object owner, int channel, Color tag)
        {
            if (instance == null) return false;
            foreach (Tween t in instance.tweens)
                if (!t.dead && t.owner == owner && t.channel == channel)
                    return t.hasTag && t.tag == tag;
            return false;
        }

        internal static void Tag(UnityEngine.Object owner, int channel, Color tag)
        {
            if (instance == null) return;
            foreach (Tween t in instance.tweens)
                if (!t.dead && t.owner == owner && t.channel == channel) { t.hasTag = true; t.tag = tag; }
        }

        private void Remove(UnityEngine.Object owner, int channel, bool complete)
        {
            for (int i = 0; i < tweens.Count; i++)
            {
                Tween t = tweens[i];
                if (t.dead || t.owner != owner || (channel != 0 && t.channel != channel)) continue;
                // Marked, not removed: LateUpdate may be walking the list.
                t.dead = true;
                if (complete) Finish(t);
            }
        }

        private static void Finish(Tween t)
        {
            if (t.owner == null) return;
            t.apply(1f);
            t.done?.Invoke();
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            bool snap = UiMotion.Snap;
            // Index loop: a done callback may start new tweens.
            for (int i = 0; i < tweens.Count; i++)
            {
                Tween t = tweens[i];
                if (t.dead) continue;
                if (t.owner == null) { t.dead = true; continue; }
                float elapsed = now - t.start - t.delay;
                if (elapsed < 0f && !snap) continue;
                float p = snap ? 1f : Mathf.Clamp01(elapsed / t.duration);
                if (p >= 1f)
                {
                    t.dead = true;
                    Finish(t);
                }
                else t.apply(t.ease(p));
            }
            tweens.RemoveAll(t => t.dead);
            if (tweens.Count == 0) enabled = false;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
