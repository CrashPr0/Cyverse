using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Interaction;
using Cyverse.Level;
using Cyverse.Settings;

namespace Cyverse.UI
{
    /// <summary>
    /// The game's entry scene, now DIEGETIC: a security vestibule. The player
    /// stands before a physical terminal; the login prompt renders on the
    /// terminal's monitor, the memo is a plaque beside it, and the gate to
    /// the Hub is a real door that slides open on success before the camera
    /// fades through. No player movement — it's a typing scene; the camera
    /// idles with a subtle sway (still under Reduce Motion).
    ///
    /// Two modes (same rules as before):
    ///  - Gate mode (default): a real beta-access gate — the code is NOT
    ///    shown; repeated failures trigger a cooldown.
    ///  - revealPassword: educational demo — the plaque gives the password
    ///    and the passphrase lesson.
    /// SECURITY NOTE: the check runs client-side and the string ships in the
    /// build and repo — a speed bump for casual visitors, never protection
    /// for anything sensitive. Real gating belongs on the host.
    /// </summary>
    public class PasswordLockController : MonoBehaviour
    {
        public string password = "C1@scg2laC!";
        public string nextScene = "Hub";
        public int maxLength = 24;

        [Tooltip("Print the password on the memo plaque (educational demo " +
                 "mode). Leave off for real gate use, e.g. website beta tests.")]
        public bool revealPassword = false;
        public int lockoutAfterAttempts = 5;
        public float lockoutSeconds = 30f;

        private static readonly Color Gold = new Color(0.90f, 0.66f, 0.14f);
        private static readonly Color Cyan = new Color(0.35f, 0.85f, 1f);

        private Text inputText, feedbackText;
        private Transform monitor;
        private Transform camRig;
        private Vector3 camBasePos;
        private Quaternion camBaseRot;
        private LockedDoor gate;

        private string typed = "";
        private bool masked = true;
        private int attempts;
        private bool unlocked;
        private float lockedUntil;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false; // typing-only scene, no pointer needed
            AccessibilitySettings.ReduceMotion = PlayerPrefs.GetInt("cv_reducemotion", 0) == 1;
            Shader.SetGlobalFloat("_CyMotion", AccessibilitySettings.ReduceMotion ? 0f : 1f);
            BuildWorld();
            RefreshInput();
            SetFeedback("<color=#6F8296>SYSTEM READY  /  AWAITING CREDENTIALS</color>");
            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();
        }

        void Update()
        {
            if (unlocked) return;

            if (Time.unscaledTime < lockedUntil)
            {
                int remain = Mathf.CeilToInt(lockedUntil - Time.unscaledTime);
                SetFeedback($"<color=#FF8866><b>LOCKED</b></color>  Too many attempts — retry in {remain}s.");
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                masked = !masked;
                RefreshInput();
            }

            bool clipboardModifier = Input.GetKey(KeyCode.LeftControl) ||
                                     Input.GetKey(KeyCode.RightControl) ||
                                     Input.GetKey(KeyCode.LeftCommand) ||
                                     Input.GetKey(KeyCode.RightCommand);
            if (clipboardModifier && Input.GetKeyDown(KeyCode.V))
            {
                PasteFromClipboard();
                return;
            }
            if (clipboardModifier && Input.GetKeyDown(KeyCode.C))
            {
                CopyToClipboard();
                return;
            }

            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
                }
                else if (c == '\n' || c == '\r')
                {
                    Submit();
                    return;
                }
                else if (!char.IsControl(c) && typed.Length < maxLength)
                {
                    typed += c;
                }
            }
            RefreshInput();
        }

        private void PasteFromClipboard()
        {
            string clipboard = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clipboard))
            {
                SetFeedback("<color=#6F8296>CLIPBOARD IS EMPTY</color>");
                return;
            }

            int before = typed.Length;
            foreach (char c in clipboard)
            {
                if (!char.IsControl(c) && typed.Length < maxLength)
                    typed += c;
            }

            RefreshInput();
            SetFeedback(typed.Length > before
                ? "<color=#5BC8FF>PASTED FROM CLIPBOARD</color>"
                : "<color=#6F8296>NO VALID CHARACTERS TO PASTE</color>");
        }

        private void CopyToClipboard()
        {
            if (typed.Length == 0)
            {
                SetFeedback("<color=#6F8296>NOTHING TO COPY</color>");
                return;
            }

            GUIUtility.systemCopyBuffer = typed;
            SetFeedback("<color=#5BC8FF>ENTRY COPIED TO CLIPBOARD</color>");
        }

        void LateUpdate()
        {
            if (!unlocked) RefreshInput(); // keep the cursor blinking

            // Idle camera sway — the cheap trick that makes a static scene
            // feel inhabited. Frozen under Reduce Motion.
            if (camRig == null || AccessibilitySettings.ReduceMotion) return;
            float t = Time.time;
            camRig.position = camBasePos + new Vector3(
                Mathf.Sin(t * 0.35f) * 0.035f, Mathf.Sin(t * 0.9f) * 0.02f, 0f);
            camRig.rotation = camBaseRot * Quaternion.Euler(
                Mathf.Sin(t * 0.5f) * 0.4f, Mathf.Sin(t * 0.27f) * 0.6f, 0f);
        }

        private void Submit()
        {
            if (typed == password)
            {
                unlocked = true;
                RefreshInput();
                SetFeedback("<color=#4CE087><b>ACCESS GRANTED</b></color>  Welcome to CyberVerse.");
                StartCoroutine(EnterTheHub());
            }
            else
            {
                attempts++;
                typed = "";
                RefreshInput();
                if (lockoutAfterAttempts > 0 && attempts % lockoutAfterAttempts == 0)
                {
                    lockedUntil = Time.unscaledTime + lockoutSeconds;
                    SetFeedback($"<color=#FF8866><b>LOCKED</b></color>  Too many attempts — retry in {Mathf.CeilToInt(lockoutSeconds)}s.");
                }
                else if (revealPassword && attempts >= 2)
                    SetFeedback("<color=#FF8866><b>ACCESS DENIED</b></color>  Check the memo — type it exactly, incl. symbols.");
                else
                    SetFeedback("<color=#FF8866><b>ACCESS DENIED</b></color>  Incorrect access code.");
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                StartCoroutine(ShakeMonitor());
            }
        }

        private IEnumerator EnterTheHub()
        {
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            if (gate != null) gate.Unlock(); // the doors part for you
            BurstFX.Spawn(new Vector3(0f, 2.6f, 2.8f), Gold, 40, 3f, 1.2f);
            yield return new WaitForSeconds(1.8f);

            if (Application.CanStreamedLevelBeLoaded(nextScene))
            {
                string target = nextScene;
                if (ScreenFader.Instance != null)
                    ScreenFader.Instance.FadeToBlackThen(() => SceneManager.LoadScene(target));
                else
                    SceneManager.LoadScene(target);
            }
            else
            {
                SetFeedback($"<color=#FF8866>Scene '{nextScene}' missing — run CyVerse > Add Scenes To Build Settings.</color>");
            }
        }

        private IEnumerator ShakeMonitor()
        {
            if (monitor == null) yield break;
            Vector3 basePos = monitor.localPosition;
            if (!AccessibilitySettings.ReduceMotion)
            {
                for (float t = 0f; t < 0.35f; t += Time.deltaTime)
                {
                    monitor.localPosition = basePos + Vector3.right *
                        (Mathf.Sin(t * 70f) * 0.05f * (1f - t / 0.35f));
                    yield return null;
                }
            }
            monitor.localPosition = basePos;
        }

        private void RefreshInput()
        {
            if (inputText == null) return;
            string shown = masked ? new string('•', typed.Length) : typed;
            bool blink = !unlocked && Mathf.Sin(Time.unscaledTime * 6f) > 0f;
            inputText.text = $"<color=#5BC8FF>{shown}{(blink ? "_" : " ")}</color>";
        }

        private void SetFeedback(string message)
        {
            if (feedbackText != null) feedbackText.text = message;
        }

        // ---- Construction ----------------------------------------------------

        private void BuildWorld()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.10f, 0.14f);

            // Vestibule shell: 12 x 16, gate on the north wall.
            var floorMat = BuildKit.MakeGridFloor(Gold * 0.9f, new Color(0.05f, 0.05f, 0.05f));
            floorMat.SetFloat("_GridScale", 6f);
            floorMat.SetFloat("_Emission", 0.9f);
            floorMat.SetFloat("_PulseStrength", 0.2f);
            BuildKit.Spawn(PrimitiveType.Plane, "Floor", null,
                Vector3.zero, new Vector3(1.2f, 1f, 1.6f), floorMat, collider: false);

            var wallMat = BuildKit.MakeStandard(BuildKit.WallColor, 0.45f, 0.25f);
            BuildKit.Spawn(PrimitiveType.Cube, "Wall_N", null, new Vector3(0, 2.5f, 8f), new Vector3(12f, 5f, 0.6f), wallMat, false);
            BuildKit.Spawn(PrimitiveType.Cube, "Wall_S", null, new Vector3(0, 2.5f, -8f), new Vector3(12f, 5f, 0.6f), wallMat, false);
            BuildKit.Spawn(PrimitiveType.Cube, "Wall_E", null, new Vector3(6f, 2.5f, 0), new Vector3(0.6f, 5f, 16f), wallMat, false);
            BuildKit.Spawn(PrimitiveType.Cube, "Wall_W", null, new Vector3(-6f, 2.5f, 0), new Vector3(0.6f, 5f, 16f), wallMat, false);
            BuildKit.Spawn(PrimitiveType.Cube, "Ceiling", null, new Vector3(0, 5.1f, 0), new Vector3(12f, 0.3f, 16f),
                BuildKit.MakeStandard(new Color(0.06f, 0.07f, 0.10f), 0.3f, 0.1f), false);
            BuildKit.Spawn(PrimitiveType.Cube, "LightBar", null, new Vector3(0, 4.9f, -2f), new Vector3(8f, 0.1f, 0.7f),
                BuildKit.MakeEmissive(BuildKit.PanelWhite, 1.3f), false);

            // Floor trim + SJSU banners so the walls aren't slabs.
            var trim = BuildKit.MakeEmissive(Gold, 2f);
            BuildKit.Spawn(PrimitiveType.Cube, "Trim_E", null, new Vector3(5.6f, 0.08f, 0), new Vector3(0.1f, 0.1f, 15.4f), trim, false);
            BuildKit.Spawn(PrimitiveType.Cube, "Trim_W", null, new Vector3(-5.6f, 0.08f, 0), new Vector3(0.1f, 0.1f, 15.4f), trim, false);
            BuildKit.Spawn(PrimitiveType.Cube, "Banner_E", null, new Vector3(5.6f, 2.9f, -1f), new Vector3(0.08f, 2.2f, 1.3f),
                BuildKit.MakeEmissive(new Color(0f, 0.33f, 0.64f), 0.4f), false);
            BuildKit.Spawn(PrimitiveType.Cube, "Banner_W", null, new Vector3(-5.6f, 2.9f, -1f), new Vector3(0.08f, 2.2f, 1.3f),
                BuildKit.MakeEmissive(Gold, 0.35f), false);

            // The gate to the Hub — a real LockedDoor that opens on success.
            gate = LockedDoor.Build(new Vector3(0f, 0f, 3f), 0f, 3f,
                "CYVERSE HUB", "Authenticate at the terminal.", Gold);

            // The terminal: pedestal + tilted monitor carrying all the text.
            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);
            BuildKit.Spawn(PrimitiveType.Cube, "Pedestal", null,
                new Vector3(0f, 0.65f, -1.2f), new Vector3(0.7f, 1.3f, 0.5f), bodyMat, false);
            BuildKit.Spawn(PrimitiveType.Cube, "PedestalTrim", null,
                new Vector3(0f, 1.31f, -1.45f), new Vector3(0.7f, 0.03f, 0.02f),
                BuildKit.MakeEmissive(Cyan, 1.6f), false);

            var monitorGo = new GameObject("Monitor");
            monitorGo.transform.position = new Vector3(0f, 1.9f, -1.15f);
            monitorGo.transform.rotation = Quaternion.Euler(-10f, 0f, 0f);
            monitor = monitorGo.transform;

            BuildKit.SpawnLocal(PrimitiveType.Cube, "Body", monitor,
                Vector3.zero, Vector3.zero, new Vector3(2.1f, 1.35f, 0.08f), bodyMat, collider: false);
            BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", monitor,
                new Vector3(0f, 0f, -0.045f), Vector3.zero, new Vector3(1.98f, 1.23f, 1f),
                BuildKit.MakeEmissive(new Color(0.015f, 0.04f, 0.07f), 0.6f), collider: false);

            BuildTerminalCanvas();

            BuildMemoPlaque();

            // Camera rig: fixed, framing terminal + gate. No controller.
            var camGo = new GameObject("SceneCamera");
            camGo.transform.position = new Vector3(0f, 1.75f, -4.6f);
            camGo.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            cam.allowMSAA = true;
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            camRig = camGo.transform;
            camBasePos = camGo.transform.position;
            camBaseRot = camGo.transform.rotation;

            // Lighting: cool key over the terminal, warm glow at the gate.
            var key = new GameObject("TerminalLight");
            key.transform.position = new Vector3(0f, 3.4f, -2.6f);
            var kl = key.AddComponent<Light>();
            kl.type = LightType.Point; kl.color = Cyan; kl.range = 7f; kl.intensity = 1.7f;

            var gateGlow = new GameObject("GateLight");
            gateGlow.transform.position = new Vector3(0f, 3.2f, 2.2f);
            var gl = gateGlow.AddComponent<Light>();
            gl.type = LightType.Point; gl.color = Gold; gl.range = 8f; gl.intensity = 1.5f;

            // Sound + fade, self-contained (no GameSystems in this scene).
            gameObject.AddComponent<Sfx>();
            gameObject.AddComponent<AmbientHum>();
            gameObject.AddComponent<ScreenFader>();

        }

        private void BuildTerminalCanvas()
        {
            var canvas = MakeWorldCanvas("TerminalUI", monitor,
                new Vector3(0f, 0f, -0.065f), new Vector2(1000f, 620f), 0.0019f);
            Transform root = canvas.transform;

            MakeUiText(root, "Brand", new Vector2(0f, 235f), new Vector2(900f, 105f),
                "CYVERSE", 82, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            MakeUiText(root, "TerminalMode", new Vector2(0f, 163f), new Vector2(880f, 52f),
                revealPassword ? "SECURE TERMINAL  /  EMPLOYEE ACCESS" : "SECURE TERMINAL  /  BETA ACCESS",
                28, new Color(0.72f, 0.84f, 0.96f), TextAnchor.MiddleCenter, FontStyle.Normal);
            MakeUiImage(root, "HeaderRule", new Vector2(0f, 126f), new Vector2(780f, 3f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f));

            MakeUiText(root, "PasscodeLabel", new Vector2(-350f, 48f), new Vector2(210f, 62f),
                "PASSCODE", 26, new Color(0.70f, 0.78f, 0.88f), TextAnchor.MiddleLeft, FontStyle.Bold);
            MakeUiImage(root, "InputField", new Vector2(70f, 48f), new Vector2(620f, 82f),
                new Color(0.02f, 0.11f, 0.17f, 0.95f));
            MakeUiImage(root, "InputAccent", new Vector2(70f, 6f), new Vector2(620f, 3f), Cyan);
            inputText = MakeUiText(root, "Input", new Vector2(70f, 48f), new Vector2(580f, 70f),
                "", 54, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            inputText.resizeTextForBestFit = true;
            inputText.resizeTextMinSize = 28;
            inputText.resizeTextMaxSize = 54;

            feedbackText = MakeUiText(root, "Feedback", new Vector2(0f, -63f), new Vector2(850f, 74f),
                "", 27, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);
            feedbackText.resizeTextForBestFit = true;
            feedbackText.resizeTextMinSize = 17;
            feedbackText.resizeTextMaxSize = 27;

            MakeUiText(root, "Controls", new Vector2(0f, -168f), new Vector2(880f, 48f),
                "ENTER  SUBMIT     TAB  SHOW / HIDE     CTRL/CMD+C/V  COPY / PASTE",
                21, new Color(0.48f, 0.58f, 0.70f), TextAnchor.MiddleCenter, FontStyle.Normal);

            AddCrtOverlay(root);
        }

        private static void AddCrtOverlay(Transform root)
        {
            Shader shader = Resources.Load<Shader>("Shaders/CRTOverlay");
            if (shader == null) shader = Shader.Find("Cyverse/CRTOverlay");
            if (shader == null)
            {
                Debug.LogWarning("CRT overlay shader was not found; terminal will render without it.");
                return;
            }

            var overlay = MakeUiImage(root, "CRTOverlay", Vector2.zero,
                new Vector2(970f, 590f), Color.white);
            overlay.material = new Material(shader)
            {
                name = "Terminal CRT Overlay"
            };
            overlay.transform.SetAsLastSibling();
        }

        private void BuildMemoPlaque()
        {
            var memo = new GameObject("MemoPlaque");
            memo.transform.position = new Vector3(2.3f, 0f, -1.7f);
            // +24° swings the readable (-Z) face left-and-back, toward the
            // camera at centre — -24° would angle it away.
            memo.transform.rotation = Quaternion.Euler(0f, 24f, 0f);

            var frameMat = BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.5f, 0.4f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Post", memo.transform,
                new Vector3(0f, 0.75f, 0f), Vector3.zero, new Vector3(0.08f, 1.5f, 0.08f), frameMat, collider: false);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Plaque", memo.transform,
                new Vector3(0f, 1.85f, 0.03f), Vector3.zero, new Vector3(1.7f, 1.15f, 0.06f),
                BuildKit.MakeStandard(new Color(0.05f, 0.055f, 0.08f), 0.5f, 0.4f), collider: false);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "PlaqueTrim", memo.transform,
                new Vector3(0f, 1.24f, -0.005f), Vector3.zero, new Vector3(1.7f, 0.04f, 0.02f),
                BuildKit.MakeEmissive(Gold, 1.6f), collider: false);

            var canvas = MakeWorldCanvas("MemoUI", memo.transform,
                new Vector3(0f, 1.88f, -0.012f), new Vector2(720f, 500f), 0.00215f);
            Transform root = canvas.transform;

            MakeUiText(root, "MemoHeader", new Vector2(0f, 182f), new Vector2(640f, 62f),
                revealPassword ? "SECURITY MEMO" : "CLOSED BETA",
                38, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            MakeUiImage(root, "MemoRule", new Vector2(0f, 140f), new Vector2(560f, 3f),
                new Color(Gold.r, Gold.g, Gold.b, 0.75f));

            string body = revealPassword
                ? $"Temporary password:\n<color=#E5A823><b>{password}</b></color>"
                : "Enter the access code you received\nfrom the CyVerse team.";
            Text bodyText = MakeUiText(root, "MemoBody", new Vector2(0f, 48f), new Vector2(620f, 150f),
                body, 30, new Color(0.90f, 0.95f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
            bodyText.resizeTextForBestFit = true;
            bodyText.resizeTextMinSize = 22;
            bodyText.resizeTextMaxSize = 30;

            string note = revealPassword
                ? "Use a long, memorable passphrase.\nMix upper/lowercase, digits, and symbols."
                : "Codes are case-sensitive.\nType every symbol exactly.\n\nNo code? Contact the CyVerse team.";
            MakeUiText(root, "MemoNote", new Vector2(0f, -118f), new Vector2(600f, 145f),
                note, 20, new Color(0.66f, 0.74f, 0.84f), TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        private static Canvas MakeWorldCanvas(string name, Transform parent,
            Vector3 localPosition, Vector2 pixelSize, float worldScale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localPosition = localPosition;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * worldScale;
            rt.sizeDelta = pixelSize;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.dynamicPixelsPerUnit = 4f;
            return canvas;
        }

        private static Text MakeUiText(Transform parent, string name, Vector2 position,
            Vector2 size, string content, int fontSize, Color color,
            TextAnchor alignment, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = HudUI.LoadFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1f;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static Image MakeUiImage(Transform parent, string name, Vector2 position,
            Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
