using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    /// <summary>
    /// Opt-in Level 4 visual tour. It captures the room overview plus one
    /// bounded view per station, so layout regressions can be reviewed without
    /// manually walking every station. Enable with
    /// CYVERSE_LEVEL4_VISUAL_CAPTURE=1.
    /// </summary>
    public sealed class Level4VisualCaptureTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CapturesOverviewAndFourStationViews()
        {
            if (Environment.GetEnvironmentVariable("CYVERSE_LEVEL4_VISUAL_CAPTURE") != "1")
                yield break;

            if (!Application.CanStreamedLevelBeLoaded("Level4_CyberAttack"))
                Assert.Ignore("Level4_CyberAttack is not in Build Settings.");

            SceneManager.LoadScene("Level4_CyberAttack", LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "Level 4 did not create a MainCamera.");
            DisablePlayerControl();
            HideFirstPersonGeometry(camera.transform);

            string output = Environment.GetEnvironmentVariable("CYVERSE_LEVEL4_VISUAL_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
            {
                string project = Directory.GetParent(Application.dataPath).FullName;
                output = Path.Combine(project, "Build", "VisualQA", "level4-captures");
            }
            Directory.CreateDirectory(output);

            PositionOverview(camera);
            RefreshWorldTextLayout();
            Assert.That(Capture(camera, Path.Combine(output, "Level4_CyberAttack__overview.png")), Is.True);

            PositionPlayerView(camera, new Vector3(0f, 1.70f, -10.2f), new Vector3(0f, 2.05f, -6f));
            yield return null;
            RefreshWorldTextLayout();
            Assert.That(Capture(camera, Path.Combine(output,
                "Level4_CyberAttack__briefing-player.png")), Is.True);

            PositionPlayerView(camera, new Vector3(0f, 1.70f, 11.2f), new Vector3(0f, 5.45f, 19f));
            yield return null;
            RefreshWorldTextLayout();
            Assert.That(Capture(camera, Path.Combine(output,
                "Level4_CyberAttack__north-header-player.png")), Is.True);

            PositionPlayerView(camera, new Vector3(0f, 3.10f, 4.8f), new Vector3(0f, 2.15f, 11f));
            camera.fieldOfView = 90f;
            yield return null;
            RefreshWorldTextLayout();
            Assert.That(Capture(camera, Path.Combine(output,
                "Level4_CyberAttack__inward-north-pair.png")), Is.True);

            PositionPlayerView(camera, new Vector3(0f, 3.10f, 17.2f), new Vector3(0f, 2.15f, 11f));
            camera.fieldOfView = 90f;
            yield return null;
            RefreshWorldTextLayout();
            Assert.That(Capture(camera, Path.Combine(output,
                "Level4_CyberAttack__inward-south-pair.png")), Is.True);

            for (int i = 1; i <= 4; i++)
            {
                Transform station = GameObject.Find("CyberAttackStation_" + i)?.transform;
                Assert.That(station, Is.Not.Null, "Missing Level 4 station " + i + ".");
                PositionStation(camera, station);
                yield return null;
                RefreshWorldTextLayout();
                Assert.That(Capture(camera, Path.Combine(output,
                    "Level4_CyberAttack__station-" + i + ".png")), Is.True);
            }

            // Include one normal gameplay frame with the overlay canvases so
            // checklist leading and narrow-WebGL HUD spacing are reviewable.
            PositionPlayerView(camera, new Vector3(0f, 1.70f, -10.2f), new Vector3(0f, 2.05f, -6f));
            yield return null;
            HideControlsOverlay();
            yield return null;
            RefreshWorldTextLayout();
            CaptureOverlayCanvases(camera);
            Assert.That(Capture(camera, Path.Combine(output,
                "Level4_CyberAttack__hud-layout.png")), Is.True);

            // The attack choices are an important visual state, not just a
            // mechanical one. Convert the overlay canvases for this capture
            // only so Camera.Render includes the modal, then leave the scene
            // to unload normally at the end of the test.
            Type videoType = FindType("Cyverse.Interaction.VideoStation");
            Type stationType = FindType("Cyverse.Interaction.CyberAttackStation");
            Component briefing = UnityEngine.Object.FindObjectOfType(videoType) as Component;
            PropertyInfo stationIndex = stationType.GetProperty("StationIndex");
            Component firstStation = null;
            foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsOfType(stationType))
            {
                if ((int)stationIndex.GetValue(candidate) != 0) continue;
                firstStation = candidate as Component;
                break;
            }
            Assert.That(briefing, Is.Not.Null);
            Assert.That(firstStation, Is.Not.Null);
            videoType.GetMethod("CompleteForAutomation")?.Invoke(briefing, null);
            yield return null;
            HideControlsOverlay();
            yield return null;
            stationType.GetMethod("Interact")?.Invoke(firstStation, new object[] { null });
            yield return null;
            CaptureOverlayCanvases(camera);
            Assert.That(Capture(camera, Path.Combine(output,
                "Level4_CyberAttack__choice-modal.png")), Is.True);
        }

        private static void CaptureOverlayCanvases(Camera camera)
        {
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
        }

        private static void HideControlsOverlay()
        {
            Type controlsType = FindType("Cyverse.UI.ControlsOverlay");
            if (controlsType != null)
            {
                foreach (UnityEngine.Object controls in UnityEngine.Object.FindObjectsOfType(controlsType))
                    UnityEngine.Object.Destroy(controls);
            }

            GameObject card = GameObject.Find("ControlsOverlay");
            if (card != null) UnityEngine.Object.Destroy(card);
        }

        private static void PositionOverview(Camera camera)
        {
            camera.fieldOfView = 65f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            // Start behind the briefing kiosk. The old position was only a
            // couple of metres from its world-space slide title, which made
            // that title dominate the overview and clip at the bottom.
            Vector3 target = new Vector3(0f, 1.7f, 11f);
            Vector3 position = new Vector3(0f, 3.8f, -17.5f);
            camera.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(target - position, Vector3.up));
        }

        private static void PositionStation(Camera camera, Transform station)
        {
            Vector3 forward = station.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 target = station.position + Vector3.up * 2.35f;
            // Aim from a slight outward offset so the wide board behind the
            // previous row does not fill the sightline for station 3 or 4.
            Vector3 outward = station.position.x < 0f ? Vector3.left : Vector3.right;
            float outwardOffset = station.position.z > 11f ? 3.6f : 2.1f;
            Vector3 position = station.position - forward * 6.4f + outward * outwardOffset + Vector3.up * 2.35f;
            camera.fieldOfView = 52f;
            camera.nearClipPlane = 0.05f;
            camera.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(target - position, Vector3.up));
        }

        private static void PositionPlayerView(Camera camera, Vector3 position, Vector3 target)
        {
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            camera.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(target - position, Vector3.up));
        }

        private static void RefreshWorldTextLayout()
        {
            Type managerType = FindType("Cyverse.Level.WorldTextLayoutManager");
            if (managerType == null) return;
            object manager = UnityEngine.Object.FindObjectOfType(managerType);
            if (manager == null) return;
            managerType.GetMethod("RefreshNow")?.Invoke(manager, null);
            managerType.GetMethod("EvaluateNow")?.Invoke(manager, null);
        }

        private static void DisablePlayerControl()
        {
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                string name = behaviour != null ? behaviour.GetType().Name : string.Empty;
                if (name == "FirstPersonController") behaviour.enabled = false;
            }
        }

        private static void HideFirstPersonGeometry(Transform camera)
        {
            foreach (Renderer renderer in camera.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
        }

        private static bool Capture(Camera camera, string path)
        {
            const int width = 1280;
            const int height = 720;
            RenderTexture target = null;
            Texture2D pixels = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                return File.Exists(path) && new FileInfo(path).Length > 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LEVEL4 VISUAL QA] Capture failed: " + exception.Message);
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
                if (target != null) target.Release();
            }
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
