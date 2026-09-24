using UnityEngine;
using UnityEngine.UI;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The in-world "Spartan Authenticator" phone for the MFA KNOWLEDGE factor.
    ///
    /// Approach (b): instead of a screen-space HUD overlay, the phone is a real
    /// prop in the 3D scene. Its screen is a RenderTexture: a dedicated
    /// world-space Canvas (the same header / code / status widgets the old HUD
    /// panel had) is rendered by a private orthographic camera into a small RT,
    /// which is bound to the phone's screen quad via an unlit material so it
    /// reads as a lit device display.
    ///
    /// The offscreen UI (canvas + camera) lives at a remote world offset
    /// (OffscreenOrigin) so the render camera only ever sees this phone's own
    /// canvas — no scene layer / TagManager change is needed. The RT is
    /// rendered ON DEMAND (camera.enabled == false; Render() is called only when
    /// the code or status text changes), so it costs nothing per frame — chosen
    /// deliberately because Cyverse ships to WebGL.
    ///
    /// TypingChallenge drives it: ShowCode() lights the screen with the OTP,
    /// SetStatus() updates the status line during the auto-type animation, and
    /// Hide() darkens/hides it when the challenge closes. Display only — there
    /// is no in-world tap interaction; the terminal still receives the
    /// auto-typed code exactly as before.
    /// </summary>
    public class DiegeticPhone : MonoBehaviour
    {
        // The most recently built phone, so the shared TypingChallenge singleton
        // can find its diegetic screen without a serialized reference. Rebuilt
        // per scene load; null on levels that have no MFA phone (the HUD panel
        // fallback covers those).
        public static DiegeticPhone Active { get; private set; }

        // Far from ALL real geometry (thousands of units on every axis) so the
        // main game camera's frustum can never contain the offscreen UI canvas,
        // even before the culling-mask isolation below takes effect.
        private static readonly Vector3 OffscreenOrigin = new Vector3(10000f, -1000f, 10000f);

        // A dedicated layer for the offscreen phone-UI canvas. The RT camera
        // renders ONLY this layer; the main game camera has it REMOVED from its
        // culling mask, so a WorldSpace canvas (which otherwise draws into every
        // camera whose mask includes its layer) never appears in the game view.
        // Chosen at runtime from a free builtin slot so no TagManager edit is
        // needed; falls back to the UI layer only if none is free.
        private static int phoneUiLayer = -1;

        private const int RtWidth = 256;
        private const int RtHeight = 384;

        private Camera uiCamera;
        private RenderTexture rt;
        private Canvas screenCanvas;
        private GameObject offscreenRoot; // canvas + camera live here, unparented from the phone
        private Renderer screenRenderer;
        private Light screenGlow;

        private Text headerText, otpLabelText, codeText, statusText;
        private Color accent;
        private bool built;

        // ---- Construction ----------------------------------------------------

        /// <summary>Builds the phone prop at <paramref name="pos"/> (facing
        /// +yaw), its offscreen render canvas + camera, and the RT wiring.
        /// Registers itself as the Active phone.</summary>
        public static DiegeticPhone Build(Vector3 pos, float rotY, Color accent)
        {
            var root = new GameObject("SpartanAuthenticatorPhone");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            var phone = root.AddComponent<DiegeticPhone>();
            phone.accent = accent;
            phone.BuildProp();
            phone.BuildOffscreenUi();
            phone.Hide();
            phone.built = true;
            Active = phone;
            return phone;
        }

        /// <summary>The physical phone: a small stand, an angled body, and the
        /// screen quad the RT is painted onto. Placed on a low dock beside the
        /// passcode terminal so the player reads it as a phone sitting there.</summary>
        private void BuildProp()
        {
            var caseMat = BuildKit.MakeStandard(new Color(0.05f, 0.06f, 0.09f), 0.35f, 0.25f);

            // A short pedestal / dock so the phone sits at a readable height
            // rather than lying on the floor.
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Dock", transform,
                new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.32f, 1.0f, 0.28f),
                BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.5f, 0.4f), collider: true);

            // The phone body stands UPRIGHT on its dock, facing the player who
            // approaches from -Z. The screen quad below renders on its local -Z
            // face (VideoStation convention), so identity rotation already faces
            // the viewer; the previous -20 deg pitch made the whole device lean
            // back and read as tilted/skewed. No pitch/roll — vertical device.
            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 1.12f, 0.02f);
            body.transform.localRotation = Quaternion.identity;

            BuildKit.SpawnLocal(PrimitiveType.Cube, "Shell", body.transform,
                Vector3.zero, Vector3.zero, new Vector3(0.20f, 0.40f, 0.025f), caseMat, collider: false);

            // The screen: a quad slightly proud of the shell front (-Z of body,
            // which is where the player stands), wearing the RenderTexture
            // through an unlit material. Unity's Quad renders on its LOCAL -Z
            // face, so identity rotation already faces a viewer on -Z; a 180°
            // yaw (used before) backface-culled it away from the player.
            var screen = BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", body.transform,
                new Vector3(0f, 0f, -0.014f), new Vector3(0f, 0f, 0f),
                new Vector3(0.176f, 0.36f, 1f), null, collider: false);
            screenRenderer = screen.GetComponent<Renderer>();

            // A dim point light so the screen appears to cast its own glow — the
            // "diegetic" tell that it is a lit device in the room.
            var glow = new GameObject("ScreenGlow");
            glow.transform.SetParent(body.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0f, -0.20f);
            screenGlow = glow.AddComponent<Light>();
            screenGlow.type = LightType.Point;
            screenGlow.color = new Color(0.30f, 1f, 0.45f);
            screenGlow.range = 1.6f;
            screenGlow.intensity = 0f; // lit only while showing a code

            BuildKit.MakeLabel(transform, new Vector3(0f, 1.9f, 0f),
                "AUTHENTICATOR", accent, 0.018f, billboard: true);
        }

        /// <summary>The offscreen render pipeline: a world-space Canvas holding
        /// the phone's UI, a private orthographic camera framing exactly that
        /// canvas, and the RT they render into. All parented under this phone
        /// but translated to OffscreenOrigin so nothing else is in shot.</summary>
        private void BuildOffscreenUi()
        {
            rt = new RenderTexture(RtWidth, RtHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "SpartanAuthenticatorRT",
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

            // Resolve a dedicated layer for the offscreen UI, then keep it OUT
            // of the main camera's culling mask. Without this, a WorldSpace
            // Canvas draws into EVERY camera whose mask includes its layer — so
            // the main game camera was rendering the full 2.2 x 3.3 m canvas
            // into the room (the "room-sized floating blue panel" bug). The RT
            // camera below is restricted to ONLY this layer, so it captures the
            // canvas and nothing else; the main camera never sees it.
            int layer = ResolvePhoneUiLayer();
            ExcludeLayerFromMainCamera(layer);

            // The offscreen canvas + camera live UNPARENTED (scene root), pinned
            // to an absolute far-away world position. They must NOT be children
            // of the phone at the terminal: parenting them there and then setting
            // .position fights the terminal transform and can leave them near the
            // room. Unparented + absolute position guarantees the pair sits
            // thousands of units away where no other geometry (and no main-camera
            // frustum) can reach them.
            offscreenRoot = new GameObject("SpartanAuthenticatorOffscreen");
            offscreenRoot.transform.position = OffscreenOrigin;

            // ---- The offscreen canvas ----
            var canvasGo = new GameObject("PhoneUiCanvas", typeof(Canvas));
            canvasGo.transform.SetParent(offscreenRoot.transform, false);
            canvasGo.transform.localPosition = Vector3.zero;      // == OffscreenOrigin in world
            canvasGo.transform.localRotation = Quaternion.identity;
            screenCanvas = canvasGo.GetComponent<Canvas>();
            screenCanvas.renderMode = RenderMode.WorldSpace;
            var canvasRt = (RectTransform)canvasGo.transform;
            // 1 canvas unit == 1 world unit at scale 0.01; the RectTransform is
            // 220 x 330 units -> 2.2 x 3.3 world units, matched by the camera.
            canvasRt.sizeDelta = new Vector2(220f, 330f);
            canvasRt.localScale = Vector3.one * 0.01f;

            BuildScreenContents(canvasGo.transform);

            // Every renderer under the canvas goes on the dedicated layer so the
            // RT camera sees them and the main camera does not.
            ApplyLayerRecursively(offscreenRoot, layer);

            // ---- The private render camera ----
            var camGo = new GameObject("PhoneUiCamera", typeof(Camera));
            camGo.transform.SetParent(offscreenRoot.transform, false);
            // Sit in front of the canvas (WorldSpace canvas faces +Z), looking
            // toward it along +Z. Local offset from the offscreen root.
            camGo.transform.localPosition = new Vector3(0f, 0f, -3f);
            camGo.transform.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            uiCamera = camGo.GetComponent<Camera>();
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = 1.65f;      // half of the 3.3-unit canvas height
            uiCamera.aspect = (float)RtWidth / RtHeight;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 6f;             // brackets the canvas, excludes the world
            uiCamera.cullingMask = 1 << layer;      // renders ONLY the phone canvas
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

        /// <summary>Pick a layer for the offscreen phone UI. Prefer a free
        /// builtin slot (unnamed layers 3, 6..31 in a stock project) so no
        /// TagManager edit is required; if somehow none is free, fall back to
        /// the UI layer (5). Resolved once and cached across phones.</summary>
        private static int ResolvePhoneUiLayer()
        {
            if (phoneUiLayer >= 0) return phoneUiLayer;
            // Builtin-reserved layers that must never be repurposed.
            for (int i = 8; i <= 31; i++) // user layers first
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    phoneUiLayer = i;
                    return phoneUiLayer;
                }
            }
            for (int i = 3; i <= 7; i++) // then any free low slot (3 is unnamed in stock)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    phoneUiLayer = i;
                    return phoneUiLayer;
                }
            }
            phoneUiLayer = 5; // UI — last resort; main camera will drop UI on it
            return phoneUiLayer;
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

        /// <summary>Remove the phone-UI layer from the main camera's culling
        /// mask so the WorldSpace canvas is never drawn into the game view.
        /// Idempotent (bit-clear), safe to call once per phone build.</summary>
        private static void ExcludeLayerFromMainCamera(int layer)
        {
            var main = Camera.main;
            if (main != null)
                main.cullingMask &= ~(1 << layer);
        }

        /// <summary>The phone UI itself — the same header / OTP label / big code
        /// / status line the old screen-space panel drew, now laid out on the
        /// world-space canvas.</summary>
        private void BuildScreenContents(Transform canvas)
        {
            var face = new GameObject("Face", typeof(RectTransform), typeof(Image));
            face.transform.SetParent(canvas, false);
            var faceRt = (RectTransform)face.transform;
            faceRt.anchorMin = Vector2.zero; faceRt.anchorMax = Vector2.one;
            faceRt.offsetMin = Vector2.zero; faceRt.offsetMax = Vector2.zero;
            face.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.045f, 1f);

            headerText = MakeText(canvas, "PhoneHeader", 20, TextAnchor.MiddleCenter);
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = new Color(0.70f, 0.86f, 0.96f);
            var hr = headerText.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
            hr.sizeDelta = new Vector2(-16f, 40f); hr.anchoredPosition = new Vector2(0f, -14f);
            headerText.text = "SPARTAN AUTHENTICATOR";

            var screen = new GameObject("Screen", typeof(RectTransform), typeof(Image));
            screen.transform.SetParent(canvas, false);
            var sr = (RectTransform)screen.transform;
            sr.anchorMin = new Vector2(0.5f, 0.5f); sr.anchorMax = new Vector2(0.5f, 0.5f); sr.pivot = new Vector2(0.5f, 0.5f);
            sr.sizeDelta = new Vector2(196f, 210f); sr.anchoredPosition = new Vector2(0f, -6f);
            screen.GetComponent<Image>().color = new Color(0.02f, 0.10f, 0.14f, 1f);

            otpLabelText = MakeText(screen.transform, "OtpLabel", 16, TextAnchor.MiddleCenter);
            otpLabelText.color = new Color(0.45f, 0.78f, 0.90f);
            var lr = otpLabelText.rectTransform;
            lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(0.5f, 1f);
            lr.sizeDelta = new Vector2(-12f, 30f); lr.anchoredPosition = new Vector2(0f, -14f);
            otpLabelText.text = "DAILY VERIFICATION CODE";

            codeText = MakeText(screen.transform, "OtpCode", 52, TextAnchor.MiddleCenter);
            codeText.fontStyle = FontStyle.Bold;
            codeText.color = new Color(0.30f, 1f, 0.45f);
            var cr = codeText.rectTransform;
            cr.anchorMin = new Vector2(0f, 0.5f); cr.anchorMax = new Vector2(1f, 0.5f); cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(-10f, 72f); cr.anchoredPosition = Vector2.zero;
            codeText.text = "----";

            statusText = MakeText(canvas, "PhoneStatus", 15, TextAnchor.MiddleCenter);
            statusText.color = new Color(0.55f, 0.70f, 0.80f);
            var st = statusText.rectTransform;
            st.anchorMin = new Vector2(0f, 0f); st.anchorMax = new Vector2(1f, 0f); st.pivot = new Vector2(0.5f, 0f);
            st.sizeDelta = new Vector2(-14f, 48f); st.anchoredPosition = new Vector2(0f, 10f);
            statusText.text = "";
        }

        private Text MakeText(Transform parent, string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = HudUI.UIFont != null ? HudUI.UIFont : HudUI.LoadFont();
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ---- Drive API (called by TypingChallenge) ---------------------------

        /// <summary>Wake the phone: show the OTP code and light the screen.</summary>
        public void ShowCode(string code)
        {
            if (!built) return;
            SetVisible(true);
            if (codeText != null) codeText.text = string.IsNullOrEmpty(code) ? "----" : code;
            if (screenGlow != null) screenGlow.intensity = 1.1f;
            RenderNow();
        }

        /// <summary>Update the big code readout (used while auto-typing).</summary>
        public void SetCode(string code)
        {
            if (!built || codeText == null) return;
            codeText.text = string.IsNullOrEmpty(code) ? "----" : code;
            RenderNow();
        }

        /// <summary>Update the status line at the foot of the screen.</summary>
        public void SetStatus(string richText)
        {
            if (!built || statusText == null) return;
            statusText.text = richText ?? "";
            RenderNow();
        }

        public void SetVisible(bool visible)
        {
            if (!built) return;
            if (screenRenderer != null) screenRenderer.enabled = visible;
            if (screenGlow != null && !visible) screenGlow.intensity = 0f;
        }

        /// <summary>Darken and hide the screen (challenge closed/cancelled).</summary>
        public void Hide()
        {
            if (statusText != null) statusText.text = "";
            if (codeText != null) codeText.text = "----";
            if (screenGlow != null) screenGlow.intensity = 0f;
            SetVisible(false);
            RenderNow();
        }

        /// <summary>Render the canvas into the RT exactly once. Cheap because
        /// the camera is otherwise disabled — nothing renders per frame.</summary>
        private void RenderNow()
        {
            if (uiCamera != null && rt != null)
                uiCamera.Render();
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            if (uiCamera != null) uiCamera.targetTexture = null;
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }
            // The offscreen canvas + camera are unparented from the phone, so
            // they are NOT destroyed with the phone root — tear them down here
            // to avoid leaking a canvas/camera per scene reload.
            if (offscreenRoot != null) Destroy(offscreenRoot);
        }
    }
}
