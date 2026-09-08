using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    /// <summary>
    /// Optional runtime half of VisualQaHarness. The editor scanner can inspect
    /// every serialized scene, but procedural bootstraps only exist after the
    /// scene starts. Set CYVERSE_VISUAL_QA_RUNTIME_CAPTURE=1 and filter this
    /// test to render representative post-bootstrap frames to PNG.
    ///
    /// The test is a no-op by default, so the normal CI play-mode suite does
    /// not pay the capture cost. In a headless runner it still validates that
    /// each requested scene reaches a usable camera and reports capture as
    /// skipped when the graphics device is null.
    /// </summary>
    public sealed class VisualQaRuntimeCaptureTests
    {
        private static readonly string[] DefaultScenes =
        {
            "PasswordLock",
            "Hub",
            "Level0 Visual Pass",
            "Level0",
            "Level1_IAM_VisualPass",
            "Level1_IAM",
            "Level2_CyberDefense_VisualPass",
            "Level2_CyberDefense",
            "Level3_Forensics",
            "Level4_CyberAttack"
        };

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CaptureBootstrappedRepresentativeScenes()
        {
            if (Environment.GetEnvironmentVariable("CYVERSE_VISUAL_QA_RUNTIME_CAPTURE") != "1")
                yield break;

            string[] scenes = ResolveScenes();
            string outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);
            bool headless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;

            foreach (string sceneName in scenes)
            {
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                // A bootstrap may construct the camera, room and HUD across
                // several Start frames. Let those systems settle before
                // taking the invariant snapshot.
                for (int i = 0; i < 6; i++) yield return null;

                Camera camera = Camera.main;
                Assert.That(camera, Is.Not.Null, $"Runtime scene '{sceneName}' did not expose a MainCamera.");

                DisablePlayerControl();
                HideFirstPersonGeometry(camera.transform);
                Bounds bounds;
                bool hasBounds = TryGetVisibleBounds(out bounds);
                if (!hasBounds)
                {
                    Debug.Log($"[VISUAL QA RUNTIME] {sceneName}: no post-bootstrap renderer bounds; no frame written.");
                    continue;
                }

                Position(camera, bounds);
                if (headless)
                {
                    Debug.Log($"[VISUAL QA RUNTIME] {sceneName}: camera ready; PNG skipped because graphics device is null.");
                    continue;
                }

                string path = Path.Combine(outputDirectory, MakeSafeFileName(sceneName) + ".png");
                Assert.That(Capture(camera, path), Is.True, $"Could not capture runtime frame for '{sceneName}'.");
                Debug.Log($"[VISUAL QA RUNTIME] {sceneName}: captured {path}");

                // A room-wide bounds camera is useful for missing geometry,
                // but it is too distant to judge monitor text or puzzle
                // spacing. Reuse the same deterministic views as the WebGL
                // screenshot tour so a capture run produces actionable close
                // views without a person walking to every station.
                foreach (string view in FocusedViews(sceneName))
                {
                    if (!TryPositionFocusedView(camera, view)) continue;
                    yield return null; // allow billboards/layout to face the new camera
                    RefreshWorldTextLayout();
                    string focusedPath = Path.Combine(outputDirectory,
                        MakeSafeFileName(sceneName) + "__" + view + ".png");
                    Assert.That(Capture(camera, focusedPath), Is.True,
                        $"Could not capture '{view}' view for '{sceneName}'.");
                    Debug.Log($"[VISUAL QA RUNTIME] {sceneName}/{view}: captured {focusedPath}");
                }
            }
        }

        private static string[] FocusedViews(string sceneName)
        {
            if (sceneName.StartsWith("Level2_CyberDefense", StringComparison.OrdinalIgnoreCase))
                return new[] { "alert", "workstations_south", "workstations_north", "playbook", "soc" };
            if (sceneName.StartsWith("Level3_Forensics", StringComparison.OrdinalIgnoreCase))
                return new[] { "forensics", "report", "custody" };
            if (sceneName.StartsWith("Level4_CyberAttack", StringComparison.OrdinalIgnoreCase))
                return new[] { "level4_overview" };
            if (sceneName.StartsWith("Level1_IAM", StringComparison.OrdinalIgnoreCase))
                return new[] { "briefing" };
            return Array.Empty<string>();
        }

        private static bool TryPositionFocusedView(Camera camera, string view)
        {
            string methodName;
            switch (view)
            {
                case "alert": methodName = "PositionAlertBoard"; break;
                case "workstations_south": methodName = "PositionWorkstations"; break;
                case "workstations_north": methodName = "PositionWorkstationsNorth"; break;
                case "playbook": methodName = "PositionPlaybook"; break;
                case "soc": methodName = "PositionSocOverview"; break;
                case "briefing": methodName = "PositionBriefing"; break;
                case "custody": methodName = "PositionCustody"; break;
                case "forensics": methodName = "PositionForensicsOverview"; break;
                case "report": methodName = "PositionForensicsReport"; break;
                case "level4_overview": methodName = "PositionOverview"; break;
                default: return false;
            }

            Type tourType = view == "level4_overview"
                ? FindType("Cyverse.Tests.Level4VisualCaptureTests")
                : FindType("Cyverse.Testing.WorldTextScreenshotTour");
            if (tourType == null) return false;

            MethodInfo method = tourType?.GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) return false;
            camera.fieldOfView = 60f;
            method.Invoke(null, new object[] { camera });
            return true;
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

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        private static string[] ResolveScenes()
        {
            string value = Environment.GetEnvironmentVariable("CYVERSE_VISUAL_QA_RUNTIME_SCENES");
            if (string.IsNullOrWhiteSpace(value)) return DefaultScenes;
            var requested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string item in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                requested.Add(item.Trim());

            var selected = new List<string>();
            foreach (string scene in DefaultScenes)
                if (requested.Contains(scene) || requested.Contains(scene.Replace(" ", "_"))) selected.Add(scene);
            return selected.ToArray();
        }

        private static string ResolveOutputDirectory()
        {
            string value = Environment.GetEnvironmentVariable("CYVERSE_VISUAL_QA_RUNTIME_OUTPUT");
            if (!string.IsNullOrWhiteSpace(value)) return Path.GetFullPath(value);
            string project = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(project, "Build", "VisualQA", "runtime-captures");
        }

        private static void DisablePlayerControl()
        {
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                string name = behaviour.GetType().Name;
                if (name == "FirstPersonController" || name == "PlayerInteractor") behaviour.enabled = false;
            }
        }

        private static void HideFirstPersonGeometry(Transform camera)
        {
            foreach (Renderer renderer in camera.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
        }

        private static bool TryGetVisibleBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsOfType<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (renderer.GetComponentInParent<Canvas>() != null) continue;
                if (renderer.GetComponentInParent<Camera>() != null) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found && bounds.size.sqrMagnitude > 0.0001f;
        }

        private static void Position(Camera camera, Bounds bounds)
        {
            Vector3 target = bounds.center;
            Vector3 offset = new Vector3(Mathf.Max(6f, bounds.extents.x * 0.9f),
                Mathf.Max(3f, bounds.extents.y * 0.55f),
                Mathf.Max(8f, bounds.extents.z * 0.9f));
            camera.transform.SetPositionAndRotation(target + offset,
                Quaternion.LookRotation(target - (target + offset), Vector3.up));
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = Mathf.Max(200f, bounds.size.magnitude * 8f);
        }

        private static bool Capture(Camera camera, string path)
        {
            RenderTexture target = null;
            Texture2D pixels = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                const int width = 1280;
                const int height = 720;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                pixels.Apply();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                return File.Exists(path) && new FileInfo(path).Length > 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VISUAL QA RUNTIME] Capture failed: {exception.Message}");
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

        private static string MakeSafeFileName(string value)
        {
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            var result = new System.Text.StringBuilder(value.Length);
            foreach (char character in value) result.Append(invalid.Contains(character) ? '_' : character);
            return result.ToString();
        }
    }
}
