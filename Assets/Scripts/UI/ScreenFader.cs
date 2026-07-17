using UnityEngine;
using UnityEngine.UI;
using Cyverse.Settings;

namespace Cyverse.UI
{
    /// <summary>
    /// Full-screen black overlay for fade-from-black on start (and fade-to-black
    /// on level end). Uses unscaled time so it still animates while the game is
    /// paused, and snaps instantly when Reduce Motion is enabled.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }
        public float speed = 1.6f;

        private Image img;
        private float target;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            // Own ROOT canvas GameObject — never add a Canvas to the shared
            // GameSystems object, or the HUD canvas nested under it collapses to
            // screen centre.
            var canvasGo = new GameObject("FadeCanvas", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // above the HUD

            var go = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            img = go.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
        }

        private System.Action pending;

        public void FadeFromBlack() { SetAlpha(1f); target = 0f; }
        public void FadeToBlack() { target = 1f; }

        /// <summary>Fade to black, then run the action (scene loads use this so
        /// transitions don't hard-cut). Under Reduce Motion it fires next frame.</summary>
        public void FadeToBlackThen(System.Action onDone) { target = 1f; pending = onDone; }

        void Update()
        {
            float a = img.color.a;
            if (AccessibilitySettings.ReduceMotion) SetAlpha(target);
            else
            {
                a = Mathf.MoveTowards(a, target, speed * Time.unscaledDeltaTime);
                SetAlpha(a);
            }

            if (pending != null && target >= 1f && img.color.a >= 0.999f)
            {
                var p = pending;
                pending = null;
                p();
            }
        }

        private void SetAlpha(float a)
        {
            var c = img.color;
            c.a = a;
            img.color = c;
        }
    }
}
