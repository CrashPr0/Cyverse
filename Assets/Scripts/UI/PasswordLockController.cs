using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        private TextMesh inputText, feedbackText;
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
            BuildWorld();
            RefreshInput();
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
            inputText.text = $"PASSCODE:  <color=#5BC8FF>{shown}{(blink ? "_" : " ")}</color>";
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

            BuildKit.MakeLabel(monitor, new Vector3(0f, 0.45f, -0.06f), "CYVERSE", Cyan, 0.05f);
            BuildKit.MakeLabel(monitor, new Vector3(0f, 0.28f, -0.06f),
                revealPassword ? "SECURE TERMINAL — EMPLOYEE ACCESS" : "SECURE TERMINAL — BETA ACCESS",
                new Color(0.75f, 0.85f, 0.95f), 0.018f, billboard: false,
                anchor: TextAnchor.MiddleCenter, style: FontStyle.Normal);

            inputText = BuildKit.MakeLabel(monitor, new Vector3(-0.85f, 0.02f, -0.06f), "",
                Color.white, 0.030f, billboard: false, anchor: TextAnchor.MiddleLeft);
            feedbackText = BuildKit.MakeLabel(monitor, new Vector3(0f, -0.24f, -0.06f), "",
                Color.white, 0.019f, billboard: false, anchor: TextAnchor.MiddleCenter, style: FontStyle.Normal);
            BuildKit.MakeLabel(monitor, new Vector3(0f, -0.47f, -0.06f),
                "TYPE the code  ·  ENTER submit  ·  TAB show/hide",
                new Color(0.42f, 0.50f, 0.62f), 0.015f, billboard: false,
                anchor: TextAnchor.MiddleCenter, style: FontStyle.Normal);

            BuildMemoPlaque();

            // Camera rig: fixed, framing terminal + gate. No controller.
            var camGo = new GameObject("SceneCamera");
            camGo.transform.position = new Vector3(0f, 1.75f, -4.6f);
            camGo.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
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

            BuildKit.MakeLabel(null, new Vector3(0f, 0.35f, -1.48f),
                "San José State University  ·  CyberVerse Project",
                new Color(0.40f, 0.48f, 0.60f), 0.012f, billboard: false,
                anchor: TextAnchor.MiddleCenter, style: FontStyle.Normal);
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

            string text = revealPassword
                ? "SECURITY MEMO — KEEP PRIVATE\n\n" +
                  $"Temporary password:\n<size=52><color=#E5A823>{password}</color></size>\n\n" +
                  "<size=26>Why it's strong: 11 chars mixing\nupper/lower, digits and symbols —\nbuilt from a passphrase.</size>"
                : "CLOSED BETA\n\n" +
                  "Enter the access code you\nreceived from the CyVerse team.\n\n" +
                  "<size=26>Codes are case-sensitive —\ntype exactly, incl. symbols.\nNo code? Contact the team.</size>";
            BuildKit.MakeLabel(memo.transform, new Vector3(0f, 1.88f, -0.01f),
                text, new Color(0.88f, 0.93f, 1f), 0.016f, billboard: false,
                anchor: TextAnchor.MiddleCenter, style: FontStyle.Normal);
        }
    }
}
