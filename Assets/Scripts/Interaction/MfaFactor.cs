using System;
using System.Collections;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.Settings;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// One factor station of the MFA vault: Knowledge (opens the phone OTP
    /// challenge) or Biometric (a short in-place scan). The HAVE factor is a
    /// Carryable + DropZone, so it needs no component here.
    /// </summary>
    public class MfaFactor : MonoBehaviour, IInteractable
    {
        public enum Kind { Knowledge, Biometric }

        public Kind kind;
        public MfaGauntlet gauntlet;
        public string passcode;
        public Func<bool> gate;
        public string gateMessage = "You can't use this yet.";
        /// <summary>Runtime-only: overrides <see cref="gateMessage"/> when the
        /// reason depends on what is still missing.</summary>
        [System.NonSerialized] public System.Func<string> gateMessageProvider;

        private bool cleared, busy;
        private Light scanLight;
        private Transform scanBar;

        public bool CanInteract => !cleared;
        public string Prompt => kind == Kind.Knowledge
            ? "Passcode terminal — something you KNOW"
            : "Biometric scanner — something you ARE";

        void Awake()
        {
            NormalizeScreen();
        }

        void OnValidate() => NormalizeScreen();

        private void NormalizeScreen()
        {
            if (kind != Kind.Knowledge) return;

            Transform body = BuildKit.AlignKioskScreen(transform, "ScreenBody", "Screen");
            // The code boxes and hint are only on the rebuilt kiosk.
            BuildKit.PlaceOnKioskScreen(transform.Find("CodeSlots"), body, new Vector2(0f, -0.01f), 0.02f);
            BuildKit.PlaceOnKioskScreen(transform.Find("ScreenHint"), body, new Vector2(0f, -0.125f), 0.022f);
            Transform label = transform.Find("Label_PASSCODE");
            if (label == null) return;

            Billboard billboard = label.GetComponent<Billboard>();
            if (billboard != null)
            {
                if (Application.isPlaying) Destroy(billboard);
                else DestroyImmediate(billboard);
            }
            BuildKit.PlaceOnKioskScreen(label, body, new Vector2(0f, 0.115f), 0.024f);
        }

        public void Interact(GameObject interactor)
        {
            if (cleared || busy) return;
            if (gate != null && !gate())
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast(gateMessageProvider != null ? gateMessageProvider() : gateMessage,
                        new Color(1f, 0.55f, 0.4f));
                return;
            }

            if (kind == Kind.Knowledge)
            {
                if (TypingChallenge.Instance == null) { Clear(); return; } // no UI — don't soft-lock
                busy = true;
                TypingChallenge.Instance.ShowPhoneOtp(
                    "SOMETHING YOU KNOW",
                    "Your Spartan Authenticator phone shows today's verification code. Type it in, then press ENTER.",
                    passcode,
                    ok => { busy = false; if (ok) Clear(); });
            }
            else
            {
                StartCoroutine(Scan());
            }
        }

        private IEnumerator Scan()
        {
            busy = true;
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("SCANNING — hold still…", new Color(0.55f, 0.85f, 1f));

            float duration = AccessibilitySettings.ReduceMotion ? 0.2f : 1.5f;
            Vector3 barRest = scanBar != null ? scanBar.localPosition : Vector3.zero;
            if (scanBar != null) scanBar.gameObject.SetActive(true);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (scanLight != null)
                    scanLight.intensity = 1.5f + Mathf.PingPong(t * 6f, 2.2f);
                // The bar sweeps the palm plate, front edge to back and back.
                if (scanBar != null)
                    scanBar.localPosition = barRest + Vector3.forward * (0.26f * Mathf.PingPong(t * 2.2f, 1f));
                yield return null;
            }
            if (scanLight != null) scanLight.intensity = 1.2f;
            if (scanBar != null)
            {
                scanBar.localPosition = barRest;
                scanBar.gameObject.SetActive(false);
            }

            busy = false;
            Clear();
        }

        private void Clear()
        {
            if (cleared) return;
            cleared = true;
            if (gauntlet != null) gauntlet.FactorCleared(kind == Kind.Knowledge ? 0 : 2);
        }

        // ---- Construction ----------------------------------------------------

        public static MfaFactor Build(Vector3 pos, float rotY, Kind kind, MfaGauntlet gauntlet, Color accent)
        {
            var root = new GameObject("MfaFactor_" + kind);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);
            Transform scanBar = null;
            if (kind == Kind.Knowledge)
            {
                MfaProps.BuildPasscodeKiosk(root.transform, accent);
                BuildKit.SpawnLocal(PrimitiveType.Cube, "ScreenBody", root.transform,
                    new Vector3(0f, 1.3f, 0.02f), new Vector3(35f, 0f, 0f), new Vector3(0.75f, 0.5f, 0.05f), bodyMat, collider: true);
                BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", root.transform,
                    new Vector3(0f, 1.280f, -0.009f), new Vector3(35f, 0f, 0f), new Vector3(0.66f, 0.42f, 1f),
                    BuildKit.MakeHologram(accent), collider: false);
                BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.3f, -0.08f),
                    "PASSCODE", new Color(0.95f, 0.98f, 1f), 0.012f);
                MfaProps.BuildCodeSlots(root.transform, accent);
                TextMesh hint = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.2f, -0.08f),
                    "CODE FROM YOUR PHONE", new Color(0.70f, 0.82f, 0.90f), 0.0055f, style: FontStyle.Normal);
                hint.gameObject.name = "ScreenHint";
            }
            else
            {
                // The player stands on the pad and faces the scanner behind it.
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Pad", root.transform,
                    new Vector3(0f, 0.02f, -0.1f), Vector3.zero, new Vector3(0.85f, 0.02f, 0.85f),
                    BuildKit.MakeHologram(accent), collider: true);
                scanBar = MfaProps.BuildBioScanner(root.transform, new Vector3(0f, 0f, 0.5f), accent);
                BuildKit.MakeLabel(root.transform, new Vector3(0f, 2.0f, 0f), "BIOMETRIC", accent, 0.026f, billboard: true);
            }

            // The biometric pad is a disc on the floor; without an aim volume
            // the player has to stare at their feet to trigger it.
            BuildKit.AddAimCollider(root, height: kind == Kind.Biometric ? 2.0f : 2.2f, width: 1.3f);

            var factor = root.AddComponent<MfaFactor>();
            factor.kind = kind;
            factor.gauntlet = gauntlet;
            factor.scanBar = scanBar;
            factor.NormalizeScreen();

            var glow = new GameObject("FactorLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1.8f, -0.5f);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = accent;
            l.range = 4.5f;
            l.intensity = 1.2f;
            factor.scanLight = l;

            return factor;
        }
    }
}
