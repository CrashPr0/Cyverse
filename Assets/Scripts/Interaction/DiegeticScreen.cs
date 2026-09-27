using UnityEngine;
using Cyverse.Level;

namespace Cyverse.Interaction
{
    /// <summary>
    /// A reusable diegetic (in-world) screen surface.
    ///
    /// This generalizes the proven RenderTexture-screen recipe from
    /// <see cref="DiegeticPhone"/> so the Forensics station reworks
    /// (LEFT custody form, MIDDLE terminal, RIGHT plug-in/upload) don't each
    /// re-implement the fiddly offscreen-render isolation. It is the shared
    /// prerequisite named in Docs/FORENSICS_REWORK_PLAN.md §3.
    ///
    /// What it builds:
    ///   - A screen <b>Quad</b> wearing an <c>Unlit/Texture</c> material bound to
    ///     a <see cref="RenderTexture"/> (configurable size; default 256x384 like
    ///     the phone), so it reads as a self-lit device display.
    ///   - An offscreen <b>WorldSpace Canvas</b> plus a <b>private orthographic
    ///     Camera</b>, both parented under an <c>offscreenRoot</c> that is
    ///     <b>unparented</b> from any caller transform and pinned thousands of
    ///     units away (OffscreenOrigin ~(10000,-1000,10000)), so no other
    ///     geometry is ever in the render camera's shot.
    ///   - A <b>dedicated isolation layer</b> auto-picked from a free slot (no
    ///     TagManager edit): the RT camera culls ONLY that layer, and the layer
    ///     is REMOVED from the main camera's culling mask so the WorldSpace
    ///     canvas never draws into the game view.
    ///   - <c>uiCamera.enabled = false</c> — content renders <b>on demand</b> via
    ///     <see cref="RenderNow"/>, called only when the caller changes content.
    ///     Zero per-frame cost, chosen deliberately because Cyverse ships WebGL.
    ///
    /// Caller API:
    ///   - <see cref="Create"/> to build one at a position/rotation/size.
    ///   - <see cref="ScreenTransform"/> — the screen quad's Transform, to place
    ///     / re-parent the physical surface.
    ///   - <see cref="CanvasRoot"/> — the WorldSpace Canvas' RectTransform; attach
    ///     your own UI widgets (Text/Image/etc.) under it.
    ///   - <see cref="CanvasSize"/> — the canvas' RectTransform size in canvas
    ///     units, so callers can lay widgets out against known bounds.
    ///   - <see cref="RenderNow"/> after changing content.
    ///   - <see cref="SetVisible"/> to show/hide the physical screen.
    ///   - <see cref="OnDestroy"/> releases the RT and destroys the unparented
    ///     offscreen root (no per-reload leak).
    ///
    /// Quad-facing convention (matches VideoStation / DiegeticPhone): Unity's
    /// Quad renders on its LOCAL -Z face, so the screen quad is kept at identity
    /// rotation and already faces a viewer standing on -Z. A 180 deg yaw would
    /// backface-cull the surface (invisible screen).
    /// </summary>
    public class DiegeticScreen : MonoBehaviour
    {
        // Far from ALL real geometry (thousands of units on every axis) so the
        // main game camera's frustum can never contain the offscreen UI canvas,
        // even before the culling-mask isolation below takes effect.
        private static readonly Vector3 OffscreenOrigin = new Vector3(10000f, -1000f, 10000f);

        // A dedicated layer for the offscreen screen-UI canvas. The RT camera
        // renders ONLY this layer; the main game camera has it REMOVED from its
        // culling mask, so a WorldSpace canvas (which otherwise draws into every
        // camera whose mask includes its layer) never appears in the game view.
        // Chosen once at runtime from a free builtin slot so no TagManager edit
        // is needed; falls back to the UI layer only if none is free. Shared by
        // all DiegeticScreens (and safe to share with DiegeticPhone's own layer:
        // every screen's canvas + RT camera live at their own OffscreenOrigin far
        // apart in world space, so the layer only needs to be excluded from the
        // main camera once).
        private static int screenUiLayer = -1;

        private const int DefaultRtWidth = 256;
        private const int DefaultRtHeight = 384;

        // Canvas units per world unit at the canvas' localScale. The canvas is
        // built this many units tall/wide, then scaled down; the ortho camera's
        // size is matched to it so the quad shows the whole canvas.
        private const float CanvasUnitsPerWorldUnit = 100f;

        private int rtWidth = DefaultRtWidth;
        private int rtHeight = DefaultRtHeight;

        private Camera uiCamera;
        private RenderTexture rt;
        private Canvas screenCanvas;
        private RectTransform canvasRoot;
        private GameObject offscreenRoot; // canvas + camera live here, unparented from the caller
        private Renderer screenRenderer;
        private Transform screenQuad;
        private bool built;

        // ---- Public API ------------------------------------------------------

        /// <summary>The screen quad's Transform — re-parent / position it to
        /// place the surface where the caller wants it. It renders on its local
        /// -Z face (faces a viewer on -Z at identity rotation).</summary>
        public Transform ScreenTransform => screenQuad;

        /// <summary>The WorldSpace Canvas' RectTransform. Attach caller UI
        /// widgets under this; they are put on the isolation layer automatically
        /// by <see cref="RegisterContent"/> — call that after adding widgets, or
        /// just call <see cref="RenderNow"/> which re-applies the layer.</summary>
        public RectTransform CanvasRoot => canvasRoot;

        /// <summary>Size of the canvas RectTransform in canvas units, so callers
        /// can lay out widgets against known bounds.</summary>
        public Vector2 CanvasSize => canvasRoot != null ? canvasRoot.sizeDelta : Vector2.zero;

        /// <summary>The Renderer of the screen quad, in case a caller wants to
        /// tweak the material or toggle it directly.</summary>
        public Renderer ScreenRenderer => screenRenderer;

        /// <summary>The RenderTexture the screen shows (already bound to the
        /// quad's material).</summary>
        public RenderTexture Texture => rt;

        /// <summary>
        /// Build a diegetic screen.
        /// </summary>
        /// <param name="pos">World position of the screen root.</param>
        /// <param name="rotY">Yaw in degrees. Keep the approach on the surface's
        /// -Z for the quad to face the viewer.</param>
        /// <param name="worldSize">Screen quad size in world metres (X x Y).</param>
        /// <param name="rtWidth">RenderTexture width (default 256).</param>
        /// <param name="rtHeight">RenderTexture height (default 384).</param>
        /// <param name="name">GameObject name for the screen root.</param>
        public static DiegeticScreen Create(Vector3 pos, float rotY, Vector2 worldSize,
            int rtWidth = DefaultRtWidth, int rtHeight = DefaultRtHeight, string name = "DiegeticScreen")
        {
            var root = new GameObject(name);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            var screen = root.AddComponent<DiegeticScreen>();
            screen.rtWidth = Mathf.Max(8, rtWidth);
            screen.rtHeight = Mathf.Max(8, rtHeight);
            screen.BuildScreenQuad(worldSize);
            screen.BuildOffscreenUi();
            screen.built = true;
            return screen;
        }

        /// <summary>Show/hide the physical screen surface (renderer toggle).</summary>
        public void SetVisible(bool visible)
        {
            if (screenRenderer != null) screenRenderer.enabled = visible;
        }

        /// <summary>Render the canvas into the RT exactly once. Cheap because the
        /// camera is otherwise disabled — nothing renders per frame. Re-applies
        /// the isolation layer to all canvas descendants first, so widgets a
        /// caller attached after Create() are captured and kept out of the main
        /// camera.</summary>
        public void RenderNow()
        {
            if (!built) return;
            if (offscreenRoot != null) ApplyLayerRecursively(offscreenRoot, screenUiLayer);
            if (uiCamera != null && rt != null) uiCamera.Render();
        }

        /// <summary>Explicitly put a freshly-attached widget subtree on the
        /// isolation layer. Optional — <see cref="RenderNow"/> already re-applies
        /// the layer to the whole canvas — but available for callers that add
        /// widgets outside the canvas hierarchy or want to be explicit.</summary>
        public void RegisterContent(GameObject content)
        {
            if (content != null) ApplyLayerRecursively(content, screenUiLayer);
        }

        // ---- Construction ----------------------------------------------------

        /// <summary>The screen quad: a single Quad wearing the RT material,
        /// slightly forward on the root's -Z (viewer side). Identity rotation so
        /// its local -Z face points at the viewer.</summary>
        private void BuildScreenQuad(Vector2 worldSize)
        {
            var quad = BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", transform,
                Vector3.zero, Vector3.zero, new Vector3(worldSize.x, worldSize.y, 1f),
                null, collider: false);
            screenQuad = quad.transform;
            screenRenderer = quad.GetComponent<Renderer>();
        }

        /// <summary>The offscreen render pipeline: a world-space Canvas, a
        /// private orthographic camera framing exactly that canvas, and the RT
        /// they render into. Both live under an unparented root pinned to
        /// OffscreenOrigin so nothing else is ever in shot.</summary>
        private void BuildOffscreenUi()
        {
            rt = new RenderTexture(rtWidth, rtHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "DiegeticScreenRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
            };
            rt.Create();

            // Unlit so the screen reads as self-lit regardless of scene lights.
            Shader unlit = Shader.Find("Unlit/Texture");
            var screenMat = unlit != null ? new Material(unlit) : BuildKit.MakeEmissive(Color.white, 1f);
            screenMat.mainTexture = rt;
            if (screenRenderer != null) screenRenderer.sharedMaterial = screenMat;

            // Resolve a dedicated layer for the offscreen UI, then keep it OUT of
            // the main camera's culling mask. Without this, a WorldSpace Canvas
            // draws into EVERY camera whose mask includes its layer — the main
            // game camera would render the canvas into the room. The RT camera
            // below is restricted to ONLY this layer, so it captures the canvas
            // and nothing else; the main camera never sees it.
            int layer = ResolveScreenUiLayer();
            ExcludeLayerFromMainCamera(layer);

            // The offscreen canvas + camera live UNPARENTED (scene root), pinned
            // to an absolute far-away world position. They must NOT be children
            // of the caller's placed screen: parenting them there and then
            // setting a local position fights the caller transform and can leave
            // them near the room. Unparented + absolute position guarantees the
            // pair sits thousands of units away where no other geometry (and no
            // main-camera frustum) can reach them.
            offscreenRoot = new GameObject("DiegeticScreenOffscreen");
            offscreenRoot.transform.position = OffscreenOrigin;

            // ---- The offscreen canvas ----
            // Canvas dimensions in canvas units, matched to the RT aspect so the
            // camera shows the whole canvas without letterboxing. Height is a
            // round 330 units -> 3.3 world units at scale 0.01; width follows the
            // RT aspect.
            float canvasHeightUnits = 330f;
            float canvasWidthUnits = canvasHeightUnits * ((float)rtWidth / rtHeight);
            float canvasWorldScale = 1f / CanvasUnitsPerWorldUnit;

            var canvasGo = new GameObject("ScreenUiCanvas", typeof(Canvas));
            canvasGo.transform.SetParent(offscreenRoot.transform, false);
            canvasGo.transform.localPosition = Vector3.zero;      // == OffscreenOrigin in world
            canvasGo.transform.localRotation = Quaternion.identity;
            screenCanvas = canvasGo.GetComponent<Canvas>();
            screenCanvas.renderMode = RenderMode.WorldSpace;
            canvasRoot = (RectTransform)canvasGo.transform;
            canvasRoot.sizeDelta = new Vector2(canvasWidthUnits, canvasHeightUnits);
            canvasRoot.localScale = Vector3.one * canvasWorldScale;

            // Everything already under the canvas goes on the dedicated layer so
            // the RT camera sees it and the main camera does not. Callers attach
            // their widgets after Create(); RenderNow() re-applies the layer to
            // catch those.
            ApplyLayerRecursively(offscreenRoot, layer);

            // ---- The private render camera ----
            var camGo = new GameObject("ScreenUiCamera", typeof(Camera));
            camGo.transform.SetParent(offscreenRoot.transform, false);
            // Sit in front of the canvas (WorldSpace canvas faces +Z), looking
            // toward it along +Z. Local offset from the offscreen root.
            camGo.transform.localPosition = new Vector3(0f, 0f, -3f);
            camGo.transform.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            uiCamera = camGo.GetComponent<Camera>();
            uiCamera.orthographic = true;
            // Half of the canvas height in world units.
            uiCamera.orthographicSize = (canvasHeightUnits * canvasWorldScale) * 0.5f;
            uiCamera.aspect = (float)rtWidth / rtHeight;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 6f;             // brackets the canvas, excludes the world
            uiCamera.cullingMask = 1 << layer;      // renders ONLY the screen canvas
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = new Color(0.015f, 0.025f, 0.045f, 1f);
            uiCamera.targetTexture = rt;
            uiCamera.allowHDR = false;
            uiCamera.allowMSAA = false;
            uiCamera.useOcclusionCulling = false;
            uiCamera.enabled = false; // render on demand only

            screenCanvas.worldCamera = uiCamera;
        }

        // ---- Layer isolation helpers -----------------------------------------

        /// <summary>Pick a layer for the offscreen screen UI. Prefer a free
        /// builtin slot (unnamed user layers 8..31, then any free low slot) so no
        /// TagManager edit is required; if somehow none is free, fall back to the
        /// UI layer (5). Resolved once and cached across all screens.</summary>
        private static int ResolveScreenUiLayer()
        {
            if (screenUiLayer >= 0) return screenUiLayer;
            for (int i = 8; i <= 31; i++) // user layers first
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    screenUiLayer = i;
                    return screenUiLayer;
                }
            }
            for (int i = 3; i <= 7; i++) // then any free low slot (3 is unnamed in stock)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    screenUiLayer = i;
                    return screenUiLayer;
                }
            }
            screenUiLayer = 5; // UI — last resort; main camera will drop UI on it
            return screenUiLayer;
        }

        /// <summary>Set <paramref name="layer"/> on <paramref name="go"/> and
        /// every descendant, so all canvas child renderers render only into the
        /// RT camera.</summary>
        private static void ApplyLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
                ApplyLayerRecursively(child.gameObject, layer);
        }

        /// <summary>Remove the screen-UI layer from the main camera's culling
        /// mask so the WorldSpace canvas is never drawn into the game view.
        /// Idempotent (bit-clear), safe to call once per screen build.</summary>
        private static void ExcludeLayerFromMainCamera(int layer)
        {
            var main = Camera.main;
            if (main != null)
                main.cullingMask &= ~(1 << layer);
        }

        private void OnDestroy()
        {
            if (uiCamera != null) uiCamera.targetTexture = null;
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }
            // The offscreen canvas + camera are unparented from this screen, so
            // they are NOT destroyed with the screen root — tear them down here
            // to avoid leaking a canvas/camera per scene reload.
            if (offscreenRoot != null) Destroy(offscreenRoot);
        }
    }
}
