using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Cyverse.Tests
{
    /// <summary>
    /// The settings menu builds its grouped rows on the HUD, fits the screen,
    /// and the Invert Colors setting puts a working full-screen inversion layer
    /// on top of everything. Set CYVERSE_SETTINGS_CAPTURE_DIR to also save
    /// PNGs of the menu, plain and inverted.
    /// </summary>
    public sealed class SettingsMenuPlayModeTests
    {
        private static readonly string[] Sections = { "Audio", "Display", "Controls", "Accessibility" };
        private static readonly string[] Rows =
        {
            "Master_Volume", "Voice_Volume", "Sound_Effects", "Voiceover", "Graphics", "Field_of_View",
            "Look_Sensitivity", "Move_With", "Caption_Size", "Reduce_Motion", "Invert_Colors", "Restore_Defaults"
        };

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator SettingsMenu_GroupsRowsAndInvertsColors()
        {
            SceneManager.LoadScene("Level1_IAM", LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            Type settingsType = FindType("Cyverse.Settings.AccessibilitySettings");
            Type invertType = FindType("Cyverse.Settings.InvertColors");
            Assert.That(settingsType, Is.Not.Null);
            Assert.That(invertType, Is.Not.Null);
            object settings = settingsType.GetProperty("Instance").GetValue(null);
            Assert.That(settings, Is.Not.Null, "Level 1 should have a settings menu.");
            MethodInfo setOpen = settingsType.GetMethod("SetMenuOpen");
            MethodInfo setInvert = invertType.GetMethod("Set");
            bool invertWas = (bool)invertType.GetProperty("Enabled").GetValue(null);

            try
            {
                setOpen.Invoke(settings, new object[] { true });
                yield return null;

                GameObject card = GameObject.Find("SettingsCard");
                Assert.That(card, Is.Not.Null, "Opening settings should show the settings card.");
                foreach (string section in Sections)
                    Assert.That(card.transform.Find("Section_" + section), Is.Not.Null, $"Missing the {section} section.");
                foreach (string row in Rows)
                    Assert.That(card.transform.Find("Row_" + row), Is.Not.Null, $"Missing the {row} row.");

                // The card must fit the 1080-pixel reference height, clear of
                // the objective bar at the top of the HUD.
                var rect = card.GetComponent<RectTransform>();
                float top = rect.anchoredPosition.y + rect.sizeDelta.y * 0.5f;
                Assert.That(rect.sizeDelta.y, Is.LessThanOrEqualTo(900f), "The settings card is taller than the screen allows.");
                Assert.That(top, Is.LessThanOrEqualTo(540f - 100f), "The settings card overlaps the objective bar.");

                yield return Capture("settings");

                setInvert.Invoke(null, new object[] { true, false });
                yield return null;
                GameObject overlay = GameObject.Find("InvertColorsOverlay");
                Assert.That(overlay, Is.Not.Null, "Invert Colors should add its overlay.");
                Assert.That(overlay.activeInHierarchy, Is.True);
                Canvas canvas = overlay.GetComponent<Canvas>();
                Assert.That(canvas.sortingOrder, Is.GreaterThan(1000), "The inversion must draw above the HUD and screen fades.");
                Image layer = overlay.GetComponentInChildren<Image>();
                Assert.That(layer.material.shader.name, Is.EqualTo("Hidden/Cyverse/InvertOverlay"));
                Assert.That(layer.raycastTarget, Is.False);

                yield return Capture("settings_inverted");

                setInvert.Invoke(null, new object[] { false, false });
                yield return null;
                Assert.That(overlay.activeInHierarchy, Is.False, "Turning Invert Colors off should hide the overlay.");
            }
            finally
            {
                setInvert.Invoke(null, new object[] { invertWas, false });
                setOpen.Invoke(settings, new object[] { false });
            }
        }

        /// <summary>Renders the main camera, the HUD and the inversion layer to
        /// a PNG when CYVERSE_SETTINGS_CAPTURE_DIR is set and a GPU is present.</summary>
        private static IEnumerator Capture(string name)
        {
            string dir = Environment.GetEnvironmentVariable("CYVERSE_SETTINGS_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir) ||
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                yield break;
            yield return null; // WaitForEndOfFrame never fires in batch mode
            Directory.CreateDirectory(dir);

            Camera main = Camera.main;
            var go = new GameObject("CaptureCamera");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            var rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;

            Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            var modes = new RenderMode[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                if (!canvases[i].isRootCanvas || canvases[i].renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = cam;
                // Higher sorting order nearer the camera, so the inversion is last.
                canvases[i].planeDistance = canvases[i].sortingOrder > 1000 ? 0.3f : 0.5f;
            }
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].renderMode == modes[i]) continue;
                canvases[i].renderMode = modes[i];
                canvases[i].worldCamera = null;
            }
            cam.targetTexture = null;
            UnityEngine.Object.Destroy(go);
            UnityEngine.Object.Destroy(tex);
            rt.Release();
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
