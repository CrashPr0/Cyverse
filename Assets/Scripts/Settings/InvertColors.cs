using UnityEngine;
using UnityEngine.UI;

namespace Cyverse.Settings
{
    /// <summary>
    /// The Invert Colors setting: a full-screen layer on top of everything
    /// (including the HUD, menus and screen fades) that turns each colour into
    /// its opposite, the way an operating system's invert-colours mode does.
    /// It lives across scene loads and is restored from PlayerPrefs at startup,
    /// so the title screen is inverted too.
    /// </summary>
    public static class InvertColors
    {
        private const string PrefKey = "cv_invert";

        private static GameObject overlay;

        public static bool Enabled { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Restore() => Set(PlayerPrefs.GetInt(PrefKey, 0) == 1, save: false);

        public static void Set(bool on, bool save = true)
        {
            Enabled = on;
            if (save)
            {
                PlayerPrefs.SetInt(PrefKey, on ? 1 : 0);
                PlayerPrefs.Save();
            }
            if (on) Ensure();
            if (overlay != null) overlay.SetActive(on);
        }

        private static void Ensure()
        {
            if (overlay != null) return;
            Shader shader = Resources.Load<Shader>("Shaders/InvertOverlay");
            if (shader == null || !shader.isSupported) return;

            overlay = new GameObject("InvertColorsOverlay", typeof(Canvas));
            Object.DontDestroyOnLoad(overlay);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue; // above the HUD (0) and screen fades (1000)

            var layer = new GameObject("Invert", typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(overlay.transform, false);
            var rt = layer.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var image = layer.GetComponent<Image>();
            image.material = new Material(shader);
            image.raycastTarget = false;
        }
    }
}
