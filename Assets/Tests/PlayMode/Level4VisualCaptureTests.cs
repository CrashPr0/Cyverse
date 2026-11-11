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

            // The north-end decor bays behind the station rows.
            (string name, Vector3 from, Vector3 to, float fov)[] decorViews =
            {
                ("north-end-wide", new Vector3(0f, 4.5f, 6f), new Vector3(0f, 1.0f, 19f), 90f),
                ("north-west-decor", new Vector3(-4f, 1.7f, 13.5f), new Vector3(-14f, 1.4f, 18.5f), 60f),
                ("north-centre-decor", new Vector3(0f, 1.7f, 12.5f), new Vector3(-3f, 1.3f, 19f), 60f),
                ("north-east-decor", new Vector3(4f, 1.7f, 13.5f), new Vector3(14f, 1.2f, 18f), 60f),
                // Close views of the room's props, for reviewing their models.
                ("model-foosball", new Vector3(1.7f, 1.65f, 13.7f), new Vector3(0f, 0.85f, 15.6f), 50f),
                ("model-bar-stools", new Vector3(-6.6f, 1.6f, -9.6f), new Vector3(-4.7f, 1.3f, -6f), 60f),
                ("model-beanbags", new Vector3(-11.4f, 1.6f, -5.6f), new Vector3(-14f, 0.3f, -9.2f), 55f),
                ("model-lan-chairs", new Vector3(13.8f, 1.5f, 16.9f), new Vector3(10.6f, 0.9f, 17.9f), 50f),
                ("model-prizes", new Vector3(-4f, 1.7f, 15.4f), new Vector3(-4f, 1.7f, 19f), 55f),
                ("model-prizes-close", new Vector3(-4.6f, 1.7f, 17.25f), new Vector3(-4.6f, 1.5f, 19.3f), 60f),
                ("model-prizes-angle", new Vector3(-2.2f, 1.7f, 17.1f), new Vector3(-4.6f, 1.45f, 19.3f), 55f),
                ("model-ticket-machine", new Vector3(-5.2f, 1.6f, 17.3f), new Vector3(-5.5f, 1.15f, 18.25f), 45f),
                ("model-air-hockey", new Vector3(12.5f, 1.75f, 14.1f), new Vector3(15.2f, 0.95f, 16.2f), 55f),
                ("model-hoops", new Vector3(-15.4f, 1.7f, 14.9f), new Vector3(-17.9f, 1.7f, 18.4f), 60f),
                ("model-snack-counter", new Vector3(-4.7f, 1.65f, -8.1f), new Vector3(-4.7f, 1.3f, -5.2f), 60f),
                ("model-drinks-counter", new Vector3(4.7f, 1.65f, -8.1f), new Vector3(4.7f, 1.3f, -5.2f), 60f),
                ("model-coffee", new Vector3(4.3f, 1.6f, -7.2f), new Vector3(3.9f, 1.3f, -5.95f), 50f),
                ("model-lan-desk", new Vector3(10.6f, 1.55f, 16.0f), new Vector3(10.6f, 0.7f, 18.7f), 60f),
                ("model-console-lounge", new Vector3(-8.4f, 1.6f, 16.3f), new Vector3(-11.6f, 0.6f, 17.9f), 60f),
                ("model-ping-pong", new Vector3(9.6f, 1.6f, -9.4f), new Vector3(12f, 0.8f, -12f), 50f),
            };
            foreach (var view in decorViews)
            {
                PositionPlayerView(camera, view.from, view.to);
                camera.fieldOfView = view.fov;
                yield return null;
                RefreshWorldTextLayout();
                Assert.That(Capture(camera, Path.Combine(output,
                    "Level4_CyberAttack__" + view.name + ".png")), Is.True);
            }

            for (int i = 1; i <= 4; i++)
            {
                Transform station = GameObject.Find("CyberAttackStation_" + i)?.transform;
                Assert.That(station, Is.Not.Null, "Missing Level 4 station " + i + ".");
                PositionStation(camera, station);
                yield return null;
                RefreshWorldTextLayout();
                Assert.That(Capture(camera, Path.Combine(output,
                    "Level4_CyberAttack__station-" + i + ".png")), Is.True);

                // A three-quarter view and a close-up of the machine itself.
                Vector3 ahead = station.forward, side = station.right;
                PositionPlayerView(camera, station.position - ahead * 3.0f + side * 2.6f + Vector3.up * 1.75f,
                    station.position + ahead * 0.2f + Vector3.up * 1.35f);
                camera.fieldOfView = 52f;
                yield return null;
                Assert.That(Capture(camera, Path.Combine(output,
                    "Level4_CyberAttack__model-station-" + i + "-side.png")), Is.True);
                PositionPlayerView(camera, station.position - ahead * 1.9f + Vector3.up * 1.8f,
                    station.position + ahead * 0.35f + Vector3.up * 1.4f);
                yield return null;
                Assert.That(Capture(camera, Path.Combine(output,
                    "Level4_CyberAttack__model-station-" + i + "-close.png")), Is.True);
                PositionPlayerView(camera, station.position + side * 3.9f - ahead * 0.5f + Vector3.up * 1.3f,
                    station.position + side * 1.25f + ahead * 0.35f + Vector3.up * 1.2f);
                yield return null;
                Assert.That(Capture(camera, Path.Combine(output,
                    "Level4_CyberAttack__model-station-" + i + "-profile.png")), Is.True);
                PositionPlayerView(camera, station.position - ahead * 1.1f + Vector3.up * 2.05f,
                    station.position + ahead * 0.15f + Vector3.up * 0.95f);
                yield return null;
                Assert.That(Capture(camera, Path.Combine(output,
                    "Level4_CyberAttack__model-station-" + i + "-top.png")), Is.True);
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
            Assert.That(GameplayActionTestDriver.Scrub(briefing, float.MaxValue), Is.True);
            yield return null;
            HideControlsOverlay();
            yield return null;
            Assert.That(GameplayActionTestDriver.Interact(firstStation), Is.True);
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
