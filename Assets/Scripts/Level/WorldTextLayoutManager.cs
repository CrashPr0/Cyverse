using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cyverse.Level
{
    /// <summary>
    /// Central runtime policy for world-space text.
    ///
    /// Surface labels are depth-tested and remain fixed to their authored
    /// panel. Floating signs may billboard/animate, and are deconflicted in
    /// screen space so distant room labels do not stack into an unreadable
    /// knot. The scan also heals TextMesh objects serialized by visual-pass
    /// scenes before the depth-aware world-text shader existed.
    /// </summary>
    public sealed class WorldTextLayoutManager : MonoBehaviour
    {
        private sealed class Entry
        {
            public int id;
            public Component text;
            public Renderer renderer;
            public bool floating;
            public int priority;
            public float distance;
            public Rect screenRect;
            public float targetVisibility = 1f;
            public float visibility = 1f;
            public float lastAppliedAlpha = 1f;
        }

        public static WorldTextLayoutManager Instance { get; private set; }
        public int LastOverlapCount { get; private set; }

        [SerializeField] private float layoutInterval = 0.12f;
        [SerializeField] private float rescanInterval = 2f;
        [SerializeField] private float overlapPaddingPixels = 10f;
        [SerializeField] private float viewportSafePaddingPixels = 18f;
        [SerializeField, Range(0.05f, 0.5f)] private float obscuredAlpha = 0.08f;
        [SerializeField] private float fadeSpeed = 8f;

        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<int, Entry> byId = new Dictionary<int, Entry>();
        private readonly List<Entry> candidates = new List<Entry>();
        private readonly List<Rect> acceptedRects = new List<Rect>();
        private readonly Dictionary<int, Material> tmpMaterials = new Dictionary<int, Material>();
        private readonly HashSet<int> normalizedTmpMaterialIds = new HashSet<int>();
        private float nextLayout;
        private float nextRescan;

        public static WorldTextLayoutManager Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            var existing = FindObjectOfType<WorldTextLayoutManager>();
            return existing != null ? existing : host.AddComponent<WorldTextLayoutManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            RefreshNow();
#if DEVELOPMENT_BUILD
            if (Application.absoluteURL.Contains("textLayoutPreview=1") &&
                GetComponent<Cyverse.Testing.WorldTextScreenshotTour>() == null)
                gameObject.AddComponent<Cyverse.Testing.WorldTextScreenshotTour>();
#endif
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (Material material in tmpMaterials.Values)
                if (material != null) Destroy(material);
            tmpMaterials.Clear();
            normalizedTmpMaterialIds.Clear();
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (now >= nextRescan)
            {
                nextRescan = now + rescanInterval;
                RefreshNow();
            }
            if (now >= nextLayout)
            {
                nextLayout = now + layoutInterval;
                EvaluateNow();
            }

            float step = fadeSpeed * Time.unscaledDeltaTime;
            foreach (Entry entry in entries)
            {
                if (!IsUsable(entry) || !entry.floating) continue;
                entry.visibility = Mathf.MoveTowards(entry.visibility, entry.targetVisibility, step);
                ApplyVisibility(entry);
            }
        }

        /// <summary>Idempotently discovers and normalizes world text.</summary>
        public void RefreshNow()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].text != null) continue;
                byId.Remove(entries[i].id);
                entries.RemoveAt(i);
            }

            foreach (TextMesh text in FindObjectsOfType<TextMesh>(true))
            {
                if (!text.gameObject.scene.IsValid()) continue;
                Renderer renderer = text.GetComponent<Renderer>();
                if (renderer == null) continue;
                renderer.sharedMaterial = BuildKit.TextMaterial();
                Register(text, renderer);
            }

            foreach (TextMeshPro text in FindObjectsOfType<TextMeshPro>(true))
            {
                if (!text.gameObject.scene.IsValid()) continue;
                NormalizeTmpMaterial(text);
                Renderer renderer = text.GetComponent<Renderer>();
                if (renderer != null) Register(text, renderer);
            }
        }

        /// <summary>Runs one deterministic layout pass; exposed for tests.</summary>
        public void EvaluateNow()
        {
            Camera camera = Camera.main;
            candidates.Clear();
            acceptedRects.Clear();
            LastOverlapCount = 0;

            // Fixed panel/slot labels reserve their authored screen space.
            // They never move or fade, but floating labels must yield when a
            // camera angle projects them across a fixed readout.
            foreach (Entry entry in entries)
            {
                if (!IsUsable(entry) || entry.floating || camera == null) continue;
                if (TryProject(camera, entry.renderer.bounds, out Rect rect, out _))
                    acceptedRects.Add(Expand(rect, overlapPaddingPixels));
            }

            foreach (Entry entry in entries)
            {
                entry.targetVisibility = 1f;
                if (!IsUsable(entry) || !entry.floating || camera == null) continue;
                if (!TryProject(camera, entry.renderer.bounds, out Rect rect, out float distance))
                    continue;
                entry.screenRect = rect;
                entry.distance = distance;
                candidates.Add(entry);
            }

            candidates.Sort((a, b) =>
            {
                int priority = b.priority.CompareTo(a.priority);
                return priority != 0 ? priority : a.distance.CompareTo(b.distance);
            });

            foreach (Entry entry in candidates)
            {
                // A clipped billboard reads as a giant partial word at the
                // edge of the screen. Fade it until the player turns far
                // enough for the complete label to enter the safe frame.
                if (entry.screenRect.xMin < viewportSafePaddingPixels ||
                    entry.screenRect.yMin < viewportSafePaddingPixels ||
                    entry.screenRect.xMax > Screen.width - viewportSafePaddingPixels ||
                    entry.screenRect.yMax > Screen.height - viewportSafePaddingPixels)
                {
                    entry.targetVisibility = 0f;
                    LastOverlapCount++;
                    continue;
                }

                Rect padded = Expand(entry.screenRect, overlapPaddingPixels);
                bool overlaps = false;
                foreach (Rect accepted in acceptedRects)
                {
                    if (!padded.Overlaps(accepted)) continue;
                    overlaps = true;
                    break;
                }

                if (overlaps)
                {
                    entry.targetVisibility = obscuredAlpha;
                    LastOverlapCount++;
                }
                else
                {
                    acceptedRects.Add(padded);
                }
            }

            // Apply the decision in the same pass. SignFX updates earlier in
            // the frame and may restore its base alpha, so a deterministic
            // late reapplication is required in addition to the smooth path.
            foreach (Entry entry in candidates)
            {
                entry.visibility = entry.targetVisibility;
                ApplyVisibility(entry);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Current/target alpha pair used by Play Mode diagnostics.</summary>
        public Vector2 DebugVisibility(Component text)
        {
            return text != null && byId.TryGetValue(text.GetInstanceID(), out Entry entry)
                ? new Vector2(entry.visibility, entry.targetVisibility)
                : new Vector2(-1f, -1f);
        }
#endif

        private void Register(Component text, Renderer renderer)
        {
            int id = text.GetInstanceID();
            ResolveIntent(text, out bool floating, out int priority);
            if (byId.TryGetValue(id, out Entry existing))
            {
                existing.renderer = renderer;
                existing.floating = floating;
                existing.priority = priority;
                ConfigureMotion(text, floating);
                return;
            }

            var entry = new Entry
            {
                id = id,
                text = text,
                renderer = renderer,
                floating = floating,
                priority = priority
            };
            entries.Add(entry);
            byId[id] = entry;
            ConfigureMotion(text, floating);
        }

        private static void ResolveIntent(Component text, out bool floating, out int priority)
        {
            WorldTextLayoutIntent intent = text.GetComponent<WorldTextLayoutIntent>();
            if (intent != null)
            {
                floating = intent.IsFloating;
                priority = intent.LayoutPriority;
                return;
            }

            // Compatibility for text serialized before layout intent existed.
            // New text creators attach an explicit contract; this fallback can
            // be removed once the remaining authored scenes are re-saved.
            if (text.GetComponentInParent<Cyverse.Interaction.Carryable>() != null ||
                text.GetComponentInParent<Cyverse.Interaction.DropZone>() != null)
            {
                floating = false;
                priority = 400;
                return;
            }

            Billboard billboard = text.GetComponent<Billboard>();
            floating = billboard != null
                ? billboard.enabled
                : text.gameObject.name.StartsWith("Sign_") ||
                  text.gameObject.name.Contains("FloatingSign");
            priority = text.gameObject.name.StartsWith("Sign_") ? 200 : 100;
        }

        private static void ConfigureMotion(Component text, bool floating)
        {
            SignFX fx = text.GetComponent<SignFX>();
            if (floating)
            {
                if (fx == null) text.gameObject.AddComponent<SignFX>();
            }
            else if (fx != null)
            {
                // Readouts should never drift into their containing panel.
                fx.enabled = false;
            }
        }

        private void NormalizeTmpMaterial(TextMeshPro text)
        {
            Material source = text.fontSharedMaterial;
            if (source == null) return;
            if (normalizedTmpMaterialIds.Contains(source.GetInstanceID())) return;
            int id = source.GetInstanceID();
            if (!tmpMaterials.TryGetValue(id, out Material material) || material == null)
            {
                material = new Material(source) { name = source.name + " (World Depth)" };
                int zTestMode = Shader.PropertyToID("_ZTestMode");
                if (material.HasProperty(zTestMode))
                    material.SetFloat(zTestMode, (float)CompareFunction.LessEqual);
                material.renderQueue = (int)RenderQueue.Transparent;
                tmpMaterials[id] = material;
                normalizedTmpMaterialIds.Add(material.GetInstanceID());
            }
            text.fontSharedMaterial = material;
            // TMP updates its renderer bounds lazily. Force the mesh once when
            // registered so the first overlap pass is correct even in a
            // headless test or immediately after a scene transition.
            text.ForceMeshUpdate();
        }

        private static bool IsUsable(Entry entry) =>
            entry.text != null && entry.renderer != null &&
            entry.text.gameObject.activeInHierarchy && entry.renderer.enabled;

        private static bool TryProject(Camera camera, Bounds bounds, out Rect rect, out float distance)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float xMin = float.MaxValue, yMin = float.MaxValue;
            float xMax = float.MinValue, yMax = float.MinValue;
            bool visible = false;
            Accumulate(camera, new Vector3(min.x, min.y, min.z), ref visible, ref xMin, ref yMin, ref xMax, ref yMax);
            Accumulate(camera, new Vector3(max.x, min.y, min.z), ref visible, ref xMin, ref yMin, ref xMax, ref yMax);
            Accumulate(camera, new Vector3(min.x, max.y, min.z), ref visible, ref xMin, ref yMin, ref xMax, ref yMax);
            Accumulate(camera, new Vector3(max.x, max.y, min.z), ref visible, ref xMin, ref yMin, ref xMax, ref yMax);
            Accumulate(camera, new Vector3(min.x, min.y, max.z), ref visible, ref xMin, ref yMin, ref xMax, ref yMax);
            Accumulate(camera, new Vector3(max.x, min.y, max.z), ref visible, ref xMin, ref yMin, ref xMax, ref yMax);
            Accumulate(camera, new Vector3(min.x, max.y, max.z), ref visible, ref xMin, ref yMin, ref xMax, ref yMax);
            Accumulate(camera, new Vector3(max.x, max.y, max.z), ref visible, ref xMin, ref yMin, ref xMax, ref yMax);

            distance = Vector3.Distance(camera.transform.position, bounds.center);
            rect = visible ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : default;
            if (!visible || rect.width < 1f || rect.height < 1f) return false;
            return rect.xMax >= 0f && rect.yMax >= 0f && rect.xMin <= Screen.width && rect.yMin <= Screen.height;
        }

        private static void Accumulate(Camera camera, Vector3 corner, ref bool visible,
            ref float xMin, ref float yMin, ref float xMax, ref float yMax)
        {
            Vector3 point = camera.WorldToScreenPoint(corner);
            if (point.z <= 0f) return;
            visible = true;
            xMin = Mathf.Min(xMin, point.x);
            yMin = Mathf.Min(yMin, point.y);
            xMax = Mathf.Max(xMax, point.x);
            yMax = Mathf.Max(yMax, point.y);
        }

        private static Rect Expand(Rect rect, float amount) =>
            Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount,
                rect.xMax + amount, rect.yMax + amount);

        private static Color GetColor(Component text)
        {
            if (text is TextMesh legacy) return legacy.color;
            if (text is TMP_Text tmp) return tmp.color;
            return Color.white;
        }

        private static void SetColor(Component text, Color color)
        {
            if (text is TextMesh legacy) legacy.color = color;
            else if (text is TMP_Text tmp) tmp.color = color;
        }

        private static void ApplyVisibility(Entry entry)
        {
            Color color = GetColor(entry.text);
            float unmodifiedAlpha = entry.lastAppliedAlpha > 0.01f
                ? Mathf.Clamp01(color.a / entry.lastAppliedAlpha)
                : 1f;
            color.a = unmodifiedAlpha * entry.visibility;
            SetColor(entry.text, color);
            // MakeSign chrome lives on child renderers. Leaving its underline
            // and halo visible after the glyphs fade creates unexplained bars
            // at screen edges, so treat the sign as one visual unit.
            bool showChrome = entry.visibility > 0.5f;
            foreach (Renderer child in entry.text.GetComponentsInChildren<Renderer>(true))
                if (child != entry.renderer) child.enabled = showChrome;
            entry.lastAppliedAlpha = entry.visibility;
        }
    }
}
