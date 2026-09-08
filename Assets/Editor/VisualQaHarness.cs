#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cyverse.Interaction;

namespace Cyverse.Editor
{
    /// <summary>
    /// Bounded, repeatable scene QA for the CyVerse room set.
    ///
    /// The scanner is deliberately read-only: scenes are opened additively,
    /// inspected, and closed without saving. This matters when the artist has
    /// an unrelated scene edit open in the editor. It reports likely visual
    /// problems as warnings instead of trying to "fix" authored art.
    ///
    /// Menu: CyVerse > Visual QA > Scan All Scenes
    /// CLI:  -executeMethod Cyverse.Editor.VisualQaHarness.RunBatch
    ///
    /// Optional environment variables:
    ///   CYVERSE_VISUAL_QA_OUTPUT  output directory (default Build/VisualQA)
    ///   CYVERSE_VISUAL_QA_SCENES  comma-separated scene names or paths
    ///   CYVERSE_VISUAL_QA_CAPTURE 1 to render a PNG per scene when graphics
    ///                              are available (headless runs skip capture)
    ///   CYVERSE_VISUAL_QA_STRICT  1 to return a failed batch on hard failures
    /// </summary>
    public static class VisualQaHarness
    {
        private const string OutputEnvironmentVariable = "CYVERSE_VISUAL_QA_OUTPUT";
        private const string SceneEnvironmentVariable = "CYVERSE_VISUAL_QA_SCENES";
        private const string CaptureEnvironmentVariable = "CYVERSE_VISUAL_QA_CAPTURE";
        private const string StrictEnvironmentVariable = "CYVERSE_VISUAL_QA_STRICT";

        private const float RoomHalfExtent = 24f;
        private const float RoomMinY = -3f;
        private const float RoomMaxY = 16f;
        private const int MaxIssuesPerScene = 120;
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;

        [Serializable]
        public sealed class Report
        {
            public string generatedAtUtc;
            public string unityVersion;
            public string projectPath;
            public string[] scenes;
            public int sceneCount;
            public int errorCount;
            public int warningCount;
            public int infoCount;
            public bool captureRequested;
            public bool captureAvailable;
            public List<SceneReport> results = new List<SceneReport>();
        }

        [Serializable]
        public sealed class SceneReport
        {
            public string path;
            public string name;
            public string status;
            public int rendererCount;
            public int cameraCount;
            public int lightCount;
            public int worldTextCount;
            public int interactableCount;
            public string contentBounds;
            public string suggestedCameraPosition;
            public string suggestedCameraTarget;
            public string capturePath;
            public List<Issue> issues = new List<Issue>();
        }

        [Serializable]
        public sealed class Issue
        {
            public string severity;
            public string code;
            public string objectName;
            public string message;
            public string position;
        }

        private enum Severity
        {
            Info,
            Warning,
            Error
        }

        private sealed class SceneContext
        {
            public readonly Scene scene;
            public readonly SceneReport report;
            public readonly bool assetLibrary;
            public readonly List<Renderer> renderers = new List<Renderer>();
            public readonly List<Component> worldText = new List<Component>();
            public readonly List<MonoBehaviour> interactables = new List<MonoBehaviour>();
            public Bounds contentBounds;
            public bool hasContent;

            public SceneContext(Scene scene, SceneReport report)
            {
                this.scene = scene;
                this.report = report;
                assetLibrary = IsAssetLibraryScene(scene);
            }
        }

        [MenuItem("CyVerse/Visual QA/Scan All Scenes")]
        public static void RunFromMenu()
        {
            RunBatch(false);
        }

        [MenuItem("CyVerse/Visual QA/Scan All Scenes + Capture")]
        public static void RunFromMenuWithCapture()
        {
            RunBatch(true);
        }

        /// <summary>CI/editor batchmode entry point.</summary>
        public static void RunBatch()
        {
            RunBatch(Environment.GetEnvironmentVariable(CaptureEnvironmentVariable) == "1");
        }

        private static void RunBatch(bool capture)
        {
            string outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            string[] paths = ResolveScenePaths();
            var report = new Report
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                projectPath = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                scenes = paths,
                sceneCount = paths.Length,
                captureRequested = capture
            };

            Debug.Log($"[VISUAL QA] Scanning {paths.Length} scene(s); output={outputDirectory}; " +
                      $"capture={(capture ? "requested" : "off")}");

            foreach (string path in paths)
            {
                SceneReport result;
                try
                {
                    result = ScanScene(path, outputDirectory, capture, report);
                }
                catch (Exception exception)
                {
                    result = new SceneReport
                    {
                        path = path,
                        name = Path.GetFileNameWithoutExtension(path),
                        status = "error"
                    };
                    AddIssue(result, Severity.Error, "SCAN_EXCEPTION", "Scene", exception.Message, Vector3.zero);
                    Debug.LogException(exception);
                }

                report.results.Add(result);
                foreach (Issue issue in result.issues)
                {
                    if (issue.severity == Severity.Error.ToString().ToLowerInvariant()) report.errorCount++;
                    else if (issue.severity == Severity.Warning.ToString().ToLowerInvariant()) report.warningCount++;
                    else report.infoCount++;
                }

                Debug.Log($"[VISUAL QA] {result.name}: {result.status}, " +
                          $"renderers={result.rendererCount}, text={result.worldTextCount}, " +
                          $"interactables={result.interactableCount}, issues={result.issues.Count}");
            }

            report.captureAvailable = report.results.Any(item => !string.IsNullOrEmpty(item.capturePath));
            string jsonPath = Path.Combine(outputDirectory, "visual-qa-report.json");
            string textPath = Path.Combine(outputDirectory, "visual-qa-report.txt");
            File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true));
            File.WriteAllText(textPath, BuildTextReport(report));

            Debug.Log($"[VISUAL QA] Complete: scenes={report.sceneCount}, errors={report.errorCount}, " +
                      $"warnings={report.warningCount}, info={report.infoCount}, report={jsonPath}");

            if (Environment.GetEnvironmentVariable(StrictEnvironmentVariable) == "1" && report.errorCount > 0)
                throw new InvalidOperationException($"Visual QA found {report.errorCount} hard failure(s). See {jsonPath}.");

            // An explicit exit is useful when invoked from a shell and does
            // not affect the menu action because Unity ignores it in GUI mode.
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        private static SceneReport ScanScene(string path, string outputDirectory, bool capture, Report report)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                // Additive loading keeps any dirty scene currently being
                // edited intact. We close only scenes opened by this scan.
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                openedHere = true;
            }

            try
            {
                var result = new SceneReport
                {
                    path = path,
                    name = scene.name,
                    status = "pass"
                };
                var context = new SceneContext(scene, result);
                GatherObjects(context);
                AnalyzeScene(context);

                if (context.hasContent)
                {
                    result.contentBounds = FormatBounds(context.contentBounds);
                    Vector3 target = context.contentBounds.center;
                    Vector3 offset = new Vector3(
                        Mathf.Max(6f, context.contentBounds.extents.x * 0.9f),
                        Mathf.Max(3f, context.contentBounds.extents.y * 0.55f),
                        Mathf.Max(8f, context.contentBounds.extents.z * 0.9f));
                    Vector3 position = target + offset;
                    result.suggestedCameraPosition = FormatVector(position);
                    result.suggestedCameraTarget = FormatVector(target);

                    if (capture)
                    {
                        string capturePath = TryCapture(scene, context, outputDirectory);
                        if (!string.IsNullOrEmpty(capturePath))
                        {
                            result.capturePath = capturePath;
                            report.captureAvailable = true;
                        }
                    }
                }
                else
                {
                    AddIssue(result, HasRuntimeBootstrap(context) ? Severity.Info : Severity.Error,
                        "NO_RENDERERS", "Scene", HasRuntimeBootstrap(context)
                            ? "No serialized renderers; this scene appears to build its room at runtime."
                            : "Scene contains no renderers.", Vector3.zero);
                }

                if (result.issues.Any(issue => issue.severity == "error")) result.status = "error";
                else if (result.issues.Any(issue => issue.severity == "warning")) result.status = "warning";
                return result;
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void GatherObjects(SceneContext context)
        {
            foreach (GameObject root in context.scene.GetRootGameObjects())
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null || !renderer.gameObject.scene.IsValid()) continue;
                    if (renderer.GetComponentInParent<Canvas>() != null) continue;
                    context.renderers.Add(renderer);
                    if (!context.hasContent)
                    {
                        context.contentBounds = renderer.bounds;
                        context.hasContent = true;
                    }
                    else context.contentBounds.Encapsulate(renderer.bounds);
                }

                foreach (TextMesh text in root.GetComponentsInChildren<TextMesh>(true))
                    if (text != null && text.GetComponent<Renderer>() != null)
                        context.worldText.Add(text);
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                    if (text != null && text.GetComponent<Renderer>() != null)
                        context.worldText.Add(text);

                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;
                    if (behaviour is IInteractable) context.interactables.Add(behaviour);
                }
            }

            context.report.rendererCount = context.renderers.Count;
            context.report.worldTextCount = context.worldText.Count;
            context.report.interactableCount = context.interactables.Count;
            context.report.cameraCount = CountComponents<Camera>(context.scene);
            context.report.lightCount = CountComponents<Light>(context.scene);
        }

        private static void AnalyzeScene(SceneContext context)
        {
            AnalyzeCameraAndLighting(context);
            AnalyzeBounds(context);
            AnalyzeRenderers(context);
            AnalyzeWorldText(context);
            AnalyzeInteractables(context);
            AnalyzeAimVolumes(context);
        }

        private static void AnalyzeCameraAndLighting(SceneContext context)
        {
            if (context.assetLibrary)
            {
                AddIssue(context.report, Severity.Info, "ASSET_LIBRARY_SCENE", "Scene",
                    "This is an asset/reference scene, so gameplay camera, lighting and room-envelope checks are informational only.",
                    Vector3.zero);
                return;
            }

            bool runtimeBootstrap = HasRuntimeBootstrap(context);
            if (context.report.cameraCount == 0)
                AddIssue(context.report, runtimeBootstrap ? Severity.Info : Severity.Error,
                    "NO_CAMERA", "Scene", runtimeBootstrap
                        ? "No serialized camera; a runtime bootstrap/factory is expected to create one."
                        : "No camera is serialized and no runtime bootstrap was found.", Vector3.zero);
            else
            {
                int enabled = CountEnabledComponents<Camera>(context.scene);
                if (enabled == 0)
                    AddIssue(context.report, Severity.Error, "CAMERAS_DISABLED", "Scene",
                        "All serialized cameras are disabled.", Vector3.zero);
            }

            if (context.report.lightCount == 0)
                AddIssue(context.report, runtimeBootstrap ? Severity.Info : Severity.Warning,
                    "NO_LIGHT", "Scene", runtimeBootstrap
                        ? "No serialized light; runtime lighting is expected."
                        : "No light is serialized; verify ambient or runtime lighting.", Vector3.zero);
            else if (CountEnabledComponents<Light>(context.scene) == 0)
                AddIssue(context.report, Severity.Warning, "LIGHTS_DISABLED", "Scene",
                    "All serialized lights are disabled.", Vector3.zero);
        }

        private static void AnalyzeBounds(SceneContext context)
        {
            foreach (Renderer renderer in context.renderers)
            {
                Bounds bounds = renderer.bounds;
                bool outside = !context.assetLibrary &&
                               (bounds.min.x < -RoomHalfExtent || bounds.max.x > RoomHalfExtent ||
                               bounds.min.z < -RoomHalfExtent || bounds.max.z > RoomHalfExtent ||
                               bounds.min.y < RoomMinY || bounds.max.y > RoomMaxY);
                if (outside && !IsKnownBackdrop(renderer))
                    AddIssue(context.report, Severity.Warning, "OUTSIDE_ROOM_BOUNDS", renderer.name,
                        $"Renderer bounds {FormatBounds(bounds)} exceed the shared room envelope; verify it is intentional.",
                        bounds.center);

                Vector3 scale = renderer.transform.lossyScale;
                if (!IsFinite(scale) || Mathf.Abs(scale.x) < 0.00001f ||
                    Mathf.Abs(scale.y) < 0.00001f || Mathf.Abs(scale.z) < 0.00001f)
                    AddIssue(context.report, IsRuntimeDrivenFill(renderer) ? Severity.Info : Severity.Warning,
                        IsRuntimeDrivenFill(renderer) ? "RUNTIME_DRIVEN_SCALE" : "DEGENERATE_SCALE", renderer.name,
                        IsRuntimeDrivenFill(renderer)
                            ? $"Renderer has a runtime-driven fill scale {FormatVector(scale)}; verify the first-frame state is intentional."
                            : $"Renderer has a zero/non-finite world scale {FormatVector(scale)}.", renderer.transform.position);
                else if (Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))) > 100f)
                    AddIssue(context.report, Severity.Warning, "EXTREME_SCALE", renderer.name,
                        $"Renderer has an unusually large world scale {FormatVector(scale)}.", renderer.transform.position);
            }
        }

        private static void AnalyzeRenderers(SceneContext context)
        {
            foreach (Renderer renderer in context.renderers)
            {
                if (renderer.sharedMaterial == null)
                    AddIssue(context.report, Severity.Warning, "MISSING_MATERIAL", renderer.name,
                        "Renderer has no shared material and may appear as a default/odd mesh.", renderer.transform.position);

                if (!renderer.enabled && renderer.gameObject.activeInHierarchy)
                    AddIssue(context.report, Severity.Info, "RENDERER_DISABLED", renderer.name,
                        "Active object has a disabled renderer; verify this is an intentional hidden prop.", renderer.transform.position);
            }
        }

        private static void AnalyzeWorldText(SceneContext context)
        {
            for (int i = 0; i < context.worldText.Count; i++)
            {
                Component text = context.worldText[i];
                Renderer renderer = text.GetComponent<Renderer>();
                string value = ReadText(text);
                if (string.IsNullOrWhiteSpace(value))
                    AddIssue(context.report, Severity.Info, "EMPTY_WORLD_TEXT", text.name,
                        "World-text component has no text; it will not help the player read the room.", text.transform.position);

                if (text is TextMesh legacy)
                {
                    if (legacy.characterSize < 0.002f || legacy.characterSize > 0.35f)
                        AddIssue(context.report, Severity.Warning, "TEXT_SCALE_OUTLIER", text.name,
                            $"Legacy TextMesh characterSize={legacy.characterSize:F4} is outside the usual room-label range.",
                            text.transform.position);
                    AddIssue(context.report, Severity.Info, "LEGACY_WORLD_TEXT", text.name,
                        "Legacy TextMesh found; runtime WorldTextLayoutManager will normalize it, but TMP is preferred for new labels.",
                        text.transform.position);
                }
                else if (text is TMP_Text tmp && (tmp.fontSize < 0.001f || tmp.fontSize > 512f))
                    AddIssue(context.report, Severity.Warning, "TMP_SCALE_OUTLIER", text.name,
                        $"TMP fontSize={tmp.fontSize:F2} is unusually small or large for a world label.", text.transform.position);

                if (renderer == null || renderer.sharedMaterial == null)
                    AddIssue(context.report, Severity.Warning, "TEXT_MATERIAL_MISSING", text.name,
                        "World text has no renderer material and may render with an unintended default.", text.transform.position);
                else if (!IsDepthAwareTextMaterial(renderer.sharedMaterial))
                    AddIssue(context.report, Severity.Info, "TEXT_DEPTH_NORMALIZED_AT_RUNTIME", text.name,
                        $"World text uses shader '{renderer.sharedMaterial.shader?.name ?? "<none>"}'; runtime layout manager will apply depth-aware policy.",
                        text.transform.position);

                if (renderer != null && (renderer.bounds.size.x > 30f || renderer.bounds.size.y > 30f || renderer.bounds.size.z > 30f))
                    AddIssue(context.report, Severity.Warning, "TEXT_BOUNDS_HUGE", text.name,
                        $"World-text renderer bounds are unusually large: {FormatBounds(renderer.bounds)}.", text.transform.position);
            }

            // Pairwise 3-D overlap is intentionally a heuristic: surface
            // labels are allowed to sit on panels, so only report text/text
            // intersections and leave the choice to the artist.
            for (int i = 0; i < context.worldText.Count; i++)
            {
                Renderer first = context.worldText[i].GetComponent<Renderer>();
                if (first == null || !first.enabled) continue;
                for (int j = i + 1; j < context.worldText.Count; j++)
                {
                    Renderer second = context.worldText[j].GetComponent<Renderer>();
                    if (second == null || !second.enabled || !first.bounds.Intersects(second.bounds)) continue;
                    float distance = Vector3.Distance(first.bounds.center, second.bounds.center);
                    if (distance > 0.25f && !SameSurfaceFamily(context.worldText[i], context.worldText[j])) continue;
                    if (IsRuntimeManagedTextPair(context.worldText[i], context.worldText[j]))
                    {
                        AddIssue(context.report, Severity.Info, "RUNTIME_MANAGED_TEXT_PAIR",
                            context.worldText[i].name,
                            $"Serialized text intersects '{context.worldText[j].name}', but their station fits and repositions both labels at runtime.",
                            first.bounds.center);
                        continue;
                    }
                    AddIssue(context.report, Severity.Warning, "WORLD_TEXT_OVERLAP", context.worldText[i].name,
                        $"Potential text/text overlap with '{context.worldText[j].name}' ({distance:F2}m apart); verify from the player camera.",
                        first.bounds.center);
                }
            }
        }

        private static void AnalyzeInteractables(SceneContext context)
        {
            foreach (MonoBehaviour behaviour in context.interactables)
            {
                Collider[] colliders = behaviour.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0)
                {
                    AddIssue(context.report, Severity.Warning, "INTERACTABLE_NO_COLLIDER", behaviour.name,
                        "IInteractable has no serialized collider/aim volume. A runtime repair may add one; verify the player can target it.",
                        behaviour.transform.position);
                    continue;
                }

                bool usable = false;
                foreach (Collider collider in colliders)
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    // MeshCollider.bounds is not cooked until the physics
                    // scene has been simulated. A valid shared mesh on an
                    // interactable is still a usable target in the scene
                    // asset, so do not report a false zero-volume warning.
                    if (collider is MeshCollider mesh && mesh.sharedMesh != null)
                    {
                        usable = true;
                        continue;
                    }
                    if (ColliderVolume(collider) <= 0.00001f)
                        AddIssue(context.report, Severity.Warning, "DEGENERATE_COLLIDER", collider.name,
                            "Interactable collider has zero volume and cannot be targeted reliably.", collider.transform.position);
                    else usable = true;
                }

                if (!usable)
                    AddIssue(context.report, Severity.Warning, "INTERACTABLE_COLLIDERS_DISABLED", behaviour.name,
                        "IInteractable has colliders, but none are active and non-degenerate.", behaviour.transform.position);
            }
        }

        private static void AnalyzeAimVolumes(SceneContext context)
        {
            foreach (GameObject root in context.scene.GetRootGameObjects())
            {
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                {
                    if (collider == null) continue;
                    string identity = (collider.name + " " + collider.gameObject.name).ToUpperInvariant();
                    if (!identity.Contains("AIM") && !identity.Contains("INTERACTION")) continue;
                    if (!collider.enabled || !collider.gameObject.activeInHierarchy)
                        AddIssue(context.report, Severity.Warning, "AIM_VOLUME_DISABLED", collider.name,
                            "Named interaction aim volume is disabled or inactive.", collider.transform.position);
                    else if (ColliderVolume(collider) <= 0.00001f)
                        AddIssue(context.report, Severity.Warning, "AIM_VOLUME_DEGENERATE", collider.name,
                            "Named interaction aim volume has zero volume.", collider.transform.position);
                }
            }
        }

        private static bool HasRuntimeBootstrap(SceneContext context)
        {
            foreach (GameObject root in context.scene.GetRootGameObjects())
            {
                string name = root.name.ToUpperInvariant();
                if (name.Contains("BOOTSTRAP") || name.Contains("SCENEFACTORY") ||
                    name.Contains("MANAGER") || name.Contains("LEVEL0") ||
                    name.Contains("LEVEL1") || name.Contains("LEVEL2") || name.Contains("LEVEL3") ||
                    name.Contains("PASSWORDLOCK")) return true;
            }
            return false;
        }

        private static bool IsAssetLibraryScene(Scene scene)
        {
            string name = scene.name.ToUpperInvariant();
            return name.Contains("PROPSANDASSETS") || name.Contains("PROPS_AND_ASSETS") ||
                   name.Contains("ASSETLIBRARY") || name.Contains("ASSET_LIBRARY");
        }

        private static bool IsKnownBackdrop(Renderer renderer)
        {
            string name = renderer.name.ToUpperInvariant();
            return name.Contains("SKY") || name.Contains("BACKDROP") || name.Contains("CEILING") ||
                   name.Contains("FLOOR") || name.Contains("WALL_") || name.Contains("LIGHTGLOW");
        }

        private static bool IsRuntimeDrivenFill(Renderer renderer)
        {
            string name = renderer.name.ToUpperInvariant();
            return name.Contains("BARFILL") || name.Contains("PROGRESSFILL") || name.EndsWith("_FILL");
        }

        private static bool IsDepthAwareTextMaterial(Material material)
        {
            if (material == null || material.shader == null) return false;
            string shader = material.shader.name;
            return shader.Contains("WorldText") || shader.Contains("TextMeshPro");
        }

        private static bool SameSurfaceFamily(Component first, Component second)
        {
            Transform a = first.transform.parent;
            Transform b = second.transform.parent;
            return a != null && b != null && (a == b || a.parent == b.parent);
        }

        private static bool IsRuntimeManagedTextPair(Component first, Component second)
        {
            EndpointStation firstEndpoint = first.GetComponentInParent<EndpointStation>();
            EndpointStation secondEndpoint = second.GetComponentInParent<EndpointStation>();
            if (firstEndpoint != null && firstEndpoint == secondEndpoint)
                return true;

            return IsPlaybookRackText(first) && IsPlaybookRackText(second);
        }

        private static bool IsPlaybookRackText(Component text)
        {
            Carryable card = text.GetComponentInParent<Carryable>();
            if (card != null && card.id != null && card.id.StartsWith("ir_", StringComparison.Ordinal))
                return true;

            string identity = (text.gameObject.name + " " + ReadText(text)).ToUpperInvariant();
            return identity.Contains("RESPONSE_CARDS") || identity.Contains("SHUFFLED_CARDS");
        }

        private static string TryCapture(Scene scene, SceneContext context, string outputDirectory)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                AddIssue(context.report, Severity.Info, "CAPTURE_SKIPPED_HEADLESS", "Scene",
                    "PNG capture was requested but Unity has no graphics device (likely -nographics).", Vector3.zero);
                return null;
            }

            GameObject cameraObject = new GameObject("__CyVerseVisualQaCaptureCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.02f, 0.035f, 1f);
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;

            Bounds bounds = context.contentBounds;
            Vector3 target = bounds.center;
            Vector3 offset = new Vector3(Mathf.Max(6f, bounds.extents.x * 0.9f),
                Mathf.Max(3f, bounds.extents.y * 0.55f),
                Mathf.Max(8f, bounds.extents.z * 0.9f));
            camera.transform.position = target + offset;
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);

            RenderTexture texture = null;
            Texture2D pixels = null;
            try
            {
                texture = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
                texture.Create();
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = texture;
                pixels = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
                pixels.Apply();
                RenderTexture.active = previous;

                string safeName = MakeSafeFileName(scene.name);
                string path = Path.Combine(outputDirectory, "captures", safeName + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                return path;
            }
            catch (Exception exception)
            {
                AddIssue(context.report, Severity.Info, "CAPTURE_FAILED", scene.name,
                    "PNG capture skipped: " + exception.Message, camera.transform.position);
                return null;
            }
            finally
            {
                if (camera != null) camera.targetTexture = null;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (texture != null) texture.Release();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static string[] ResolveScenePaths()
        {
            string filter = Environment.GetEnvironmentVariable(SceneEnvironmentVariable);
            string[] all = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (string.IsNullOrWhiteSpace(filter)) return all;

            string[] requested = filter.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim()).ToArray();
            return all.Where(path => requested.Any(value =>
                string.Equals(value, path, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        private static string ResolveOutputDirectory()
        {
            string configured = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "VisualQA");
        }

        private static int CountComponents<T>(Scene scene) where T : Component
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                count += root.GetComponentsInChildren<T>(true).Length;
            return count;
        }

        private static int CountEnabledComponents<T>(Scene scene) where T : Behaviour
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (T component in root.GetComponentsInChildren<T>(true))
                    if (component != null && component.enabled && component.gameObject.activeInHierarchy) count++;
            return count;
        }

        private static float ColliderVolume(Collider collider)
        {
            if (collider is BoxCollider box)
                return Mathf.Abs(box.size.x * box.size.y * box.size.z * box.transform.lossyScale.x *
                    box.transform.lossyScale.y * box.transform.lossyScale.z);
            if (collider is SphereCollider sphere)
            {
                float radius = Mathf.Abs(sphere.radius * Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.x),
                    Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.y), Mathf.Abs(sphere.transform.lossyScale.z))));
                return 4f * Mathf.PI * radius * radius * radius / 3f;
            }
            if (collider is CapsuleCollider capsule)
            {
                float radius = Mathf.Abs(capsule.radius * Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.x),
                    Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.y), Mathf.Abs(capsule.transform.lossyScale.z))));
                return Mathf.PI * radius * radius * Mathf.Max(radius * 2f, capsule.height);
            }
            Bounds bounds = collider.bounds;
            return Mathf.Abs(bounds.size.x * bounds.size.y * bounds.size.z);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);

        private static string ReadText(Component text)
        {
            if (text is TextMesh legacy) return legacy.text ?? string.Empty;
            if (text is TMP_Text tmp) return tmp.text ?? string.Empty;
            return string.Empty;
        }

        private static void AddIssue(SceneReport report, Severity severity, string code,
            string objectName, string message, Vector3 position)
        {
            if (report.issues.Count >= MaxIssuesPerScene) return;
            report.issues.Add(new Issue
            {
                severity = severity.ToString().ToLowerInvariant(),
                code = code,
                objectName = objectName ?? string.Empty,
                message = message ?? string.Empty,
                position = FormatVector(position)
            });
        }

        private static string BuildTextReport(Report report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("CyVerse Visual QA Report");
            builder.AppendLine($"Generated: {report.generatedAtUtc}");
            builder.AppendLine($"Unity: {report.unityVersion}");
            builder.AppendLine($"Scenes: {report.sceneCount}  Errors: {report.errorCount}  Warnings: {report.warningCount}  Info: {report.infoCount}");
            builder.AppendLine();
            foreach (SceneReport scene in report.results)
            {
                builder.AppendLine($"[{scene.status.ToUpperInvariant()}] {scene.name} ({scene.path})");
                if (!string.IsNullOrEmpty(scene.contentBounds)) builder.AppendLine("  bounds: " + scene.contentBounds);
                if (!string.IsNullOrEmpty(scene.capturePath)) builder.AppendLine("  capture: " + scene.capturePath);
                foreach (Issue issue in scene.issues)
                    builder.AppendLine($"  {issue.severity.ToUpperInvariant()} {issue.code} {issue.objectName}: {issue.message} @ {issue.position}");
                builder.AppendLine();
            }
            return builder.ToString();
        }

        private static string FormatBounds(Bounds bounds) =>
            $"center={FormatVector(bounds.center)} size={FormatVector(bounds.size)}";

        private static string FormatVector(Vector3 value) =>
            $"({value.x.ToString("F2", CultureInfo.InvariantCulture)}," +
            $"{value.y.ToString("F2", CultureInfo.InvariantCulture)}," +
            $"{value.z.ToString("F2", CultureInfo.InvariantCulture)})";

        private static string MakeSafeFileName(string value)
        {
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
                builder.Append(invalid.Contains(character) ? '_' : character);
            return builder.ToString();
        }
    }
}
#endif
