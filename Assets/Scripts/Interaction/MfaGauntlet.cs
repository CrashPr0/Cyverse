using System;
using System.Collections;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.Settings;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Task 2 — AUTHENTICATION: the MFA Vault. Three factors must be cleared,
    /// each a different mechanic so the taxonomy is *felt*, not memorised:
    ///   KNOW — read the daily OTP on the Spartan Authenticator phone and
    ///          type it into the terminal (MfaFactor → TypingChallenge)
    ///   HAVE — fetch the security token from its rack and slot it (Carryable
    ///          → DropZone; the walk across the room IS the lesson)
    ///   ARE  — biometric scanner (MfaFactor)
    /// Each verified factor draws its own locking bar and lights its lamp;
    /// with all three drawn the wheel turns and the door swings open. Factor
    /// order is free — like real MFA.
    /// </summary>
    public class MfaGauntlet : MonoBehaviour
    {
        public static readonly string[] FactorNames =
            { "SOMETHING YOU KNOW", "SOMETHING YOU HAVE", "SOMETHING YOU ARE" };

        public event Action Completed;
        public bool IsComplete { get; private set; }

        public int factorPoints = 50;
        public int completionPoints = 75;

        private readonly bool[] cleared = new bool[3];

        /// <summary>How many of the three factors are verified (for the HUD).</summary>
        public int ClearedCount
        {
            get { int n = 0; foreach (bool c in cleared) if (c) n++; return n; }
        }

        /// <summary>True once the token has been slotted (the "have" factor).</summary>
        public bool TokenDelivered => cleared[1];
        private Renderer[] lights;
        private Transform[] lockBars;
        private Material litMat;
        private Transform vaultPanel;
        private TextMesh statusLabel;
        private Light chamberLight;
        private GameObject legacyPasscodeMemo;

        // ---- Wiring ----------------------------------------------------------

        /// <summary>Installs the runtime-only state: the indicator/panel
        /// references, the factor gates, and the TOKEN SLOT delegates. None of
        /// that survives being saved into a scene file, so a visual-pass scene
        /// loads with a slot that swallows the token without ever clearing the
        /// "something you have" factor. Called by Build and again by the level
        /// factory on load; safe either way.</summary>
        public void Configure(Color accent, string passcode, Func<bool> gate, string gateMessage,
            Func<string> gateMessageProvider = null)
        {
            if (vaultPanel == null) vaultPanel = transform.Find("VaultPanel");
            if (litMat == null) litMat = BuildKit.MakeEmissive(MfaProps.Verified, 1.6f);
            HideLegacyPasscodeMemo();
            if (lights == null) lights = new Renderer[3];
            if (lockBars == null) lockBars = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                if (lights[i] == null)
                {
                    var lamp = transform.Find("FactorLight_" + i);
                    if (lamp != null) lights[i] = lamp.GetComponent<Renderer>();
                }
                if (lockBars[i] == null && vaultPanel != null) lockBars[i] = vaultPanel.Find("LockBar_" + i);
            }
            // Absent from the older saved visual-pass vault; everything that
            // uses these checks for null.
            if (statusLabel == null)
            {
                var status = transform.Find("VaultStatus");
                if (status != null) statusLabel = status.GetComponent<TextMesh>();
            }
            if (chamberLight == null)
            {
                var light = transform.Find("VaultChamber/ChamberLight");
                if (light != null) chamberLight = light.GetComponent<Light>();
            }

            foreach (var factor in FindObjectsOfType<MfaFactor>())
            {
                if (factor.gauntlet != this) continue;
                factor.gate = gate;
                factor.gateMessage = gateMessage;
                factor.gateMessageProvider = gateMessageProvider;
                if (factor.kind == MfaFactor.Kind.Knowledge) factor.passcode = passcode;
            }

            foreach (var item in FindObjectsOfType<Carryable>())
            {
                if (item.id != "mfa_token") continue;
                item.gate = gate;
                item.gateMessage = gateMessage;
                item.gateMessageProvider = gateMessageProvider;
            }

            foreach (var candidate in FindObjectsOfType<DropZone>())
            {
                if (candidate.zoneName != "TOKEN SLOT") continue;
                var slot = candidate;
                slot.accepts = item => item.id == "mfa_token";
                slot.onAccepted = item =>
                {
                    item.Consume();
                    ShowSeatedToken(slot.transform);
                    BurstFX.SpawnAbove(slot.transform, accent, 20, minimumHeight: 1.2f);
                    FactorCleared(1);
                };
                slot.onRejected = item =>
                {
                    if (HudUI.Instance != null)
                        HudUI.Instance.ShowToast("This slot only takes the SECURITY TOKEN.",
                            new Color(1f, 0.55f, 0.4f));
                };
            }
        }

        public void FactorCleared(int index)
        {
            if (IsComplete || index < 0 || index > 2 || cleared[index]) return;
            cleared[index] = true;

            if (lights != null && lights[index] != null && litMat != null)
                lights[index].sharedMaterial = litMat;
            if (lockBars != null && lockBars[index] != null)
                StartCoroutine(RetractBar(lockBars[index]));

            int n = 0;
            foreach (bool c in cleared) if (c) n++;
            if (statusLabel != null)
            {
                statusLabel.text = MfaProps.StatusText(n);
                if (n == 3) statusLabel.color = MfaProps.Verified;
            }

            ScoreSystem.Add(factorPoints);
            if (Sfx.Instance != null) Sfx.Instance.PlayStreak(n);
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast($"FACTOR VERIFIED ({n}/3): {FactorNames[index]}  +{factorPoints}",
                    new Color(0.30f, 1f, 0.45f));

            if (n == 3) StartCoroutine(OpenVault());
        }

        private IEnumerator OpenVault()
        {
            IsComplete = true;
            yield return new WaitForSeconds(0.5f);

            if (vaultPanel != null)
            {
                Transform wheel = vaultPanel.Find("Wheel");
                if (wheel != null) yield return SwingDoor(wheel);
                else yield return SlidePanel();
            }

            ScoreSystem.Add(completionPoints);
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            BurstFX.SpawnAbove(transform, new Color(0.90f, 0.66f, 0.14f), 40, minimumHeight: 2.2f);
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast($"MULTI-FACTOR VERIFIED — VAULT OPEN  +{completionPoints}",
                    new Color(0.90f, 0.66f, 0.14f));

            Completed?.Invoke();
        }

        // Each factor draws one bolt back into the door.
        private IEnumerator RetractBar(Transform bar)
        {
            Vector3 from = bar.localPosition;
            Vector3 to = from + Vector3.right * MfaProps.LockBarTravel;
            float duration = AccessibilitySettings.ReduceMotion ? 0f : 0.35f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                bar.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            bar.localPosition = to;
        }

        // The wheel turns, then the door swings out on its hinge and the
        // chamber behind it lights up.
        private IEnumerator SwingDoor(Transform wheel)
        {
            Quaternion shut = vaultPanel.localRotation;
            Quaternion open = shut * Quaternion.Euler(0f, MfaProps.DoorSwingDegrees, 0f);
            const float chamberIntensity = 1.6f;
            if (AccessibilitySettings.ReduceMotion)
            {
                vaultPanel.localRotation = open;
                if (chamberLight != null) chamberLight.intensity = chamberIntensity;
                yield break;
            }

            Quaternion wheelStart = wheel.localRotation;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                wheel.localRotation = wheelStart * Quaternion.Euler(0f, 0f, -200f * Mathf.SmoothStep(0f, 1f, t / 0.9f));
                yield return null;
            }
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.6f);
                vaultPanel.localRotation = Quaternion.Slerp(shut, open, k);
                if (chamberLight != null) chamberLight.intensity = chamberIntensity * k;
                yield return null;
            }
            vaultPanel.localRotation = open;
            if (chamberLight != null) chamberLight.intensity = chamberIntensity;
        }

        // The older saved visual-pass vault: a plain panel that sinks into the floor.
        private IEnumerator SlidePanel()
        {
            Vector3 from = vaultPanel.localPosition;
            Vector3 to = from + Vector3.down * 2.4f;
            if (!AccessibilitySettings.ReduceMotion)
            {
                for (float t = 0f; t < 1.1f; t += Time.deltaTime)
                {
                    vaultPanel.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 1.1f));
                    yield return null;
                }
            }
            vaultPanel.localPosition = to;
        }

        // The token stays visible in the reader, and the reader's rim turns green.
        private void ShowSeatedToken(Transform slot)
        {
            Transform seated = slot.Find("ReaderHead/SeatedToken");
            if (seated != null) seated.gameObject.SetActive(true);
            Transform rim = slot.Find("ReaderHead/ReaderRim");
            if (rim != null && litMat != null) rim.GetComponent<Renderer>().sharedMaterial = litMat;
        }

        // ---- Construction ----------------------------------------------------

        /// <summary>Builds the vault plus its three factor stations. All world
        /// positions; the vault's readable face points along local -Z.</summary>
        public static MfaGauntlet Build(Vector3 vaultPos, float vaultRotY,
            Vector3 terminalPos, Vector3 padPos, Vector3 tokenRackPos, Vector3 slotPos,
            Color accent, string passcode, Func<bool> gate, string gateMessage)
        {
            var root = new GameObject("MfaVault");
            root.transform.position = vaultPos;
            root.transform.rotation = Quaternion.Euler(0f, vaultRotY, 0f);

            // Each factor's lamp glows amber while its bar is locked.
            var lockedMat = BuildKit.MakeEmissive(new Color(1f, 0.55f, 0.12f), 1.1f);
            MfaProps.BuildVault(root.transform, accent, lockedMat);
            var gauntlet = root.AddComponent<MfaGauntlet>();

            BuildKit.MakeSign(root.transform, vaultPos + new Vector3(0f, 4.3f, 0f), "MFA VAULT", accent, 0.032f);

            var vaultGlow = new GameObject("VaultLight");
            vaultGlow.transform.SetParent(root.transform, false);
            vaultGlow.transform.localPosition = new Vector3(0f, 2.6f, -2f);
            var vl = vaultGlow.AddComponent<Light>();
            vl.type = LightType.Point;
            vl.color = accent;
            vl.range = 10f;
            vl.intensity = 1.8f;

            // KNOW — passcode terminal, with an in-world reminder that the
            // actual daily code arrives on the player's phone.
            MfaFactor.Build(terminalPos, 0f, MfaFactor.Kind.Knowledge, gauntlet, accent);
            BuildPhoneReminder(terminalPos + new Vector3(0f, 0f, 1.6f), accent);

            // The in-world "Spartan Authenticator" phone sits on a dock just to
            // the side of the passcode terminal. TypingChallenge finds it via
            // DiegeticPhone.Active and lights its RenderTexture screen with the
            // OTP when the KNOW factor is interacted with; if none is built the
            // challenge falls back to its old screen-space HUD panel.
            DiegeticPhone.Build(terminalPos + new Vector3(-0.9f, 0f, -0.2f), 0f, accent);

            // ARE — biometric scanner.
            MfaFactor.Build(padPos, 0f, MfaFactor.Kind.Biometric, gauntlet, accent);

            // HAVE — token on a rack along the west wall, a short carry from the vault's slot.
            var rack = new GameObject("TokenRack");
            rack.transform.SetPositionAndRotation(tokenRackPos, Quaternion.Euler(0f, vaultRotY, 0f));
            MfaProps.BuildTokenCharger(rack.transform, accent);
            BuildKit.MakeLabel(rack.transform, new Vector3(0f, 1.9f, 0f), "TOKEN CHARGER", accent, 0.024f, billboard: true);
            Carryable.Build(tokenRackPos + Vector3.up * 1.0f, "SECURITY TOKEN", "mfa_token", accent, token: true);
            // The reader faces into the room, the same way as the vault.
            DropZone slot = DropZone.Build(slotPos, "TOKEN SLOT", accent);
            slot.transform.rotation = Quaternion.Euler(0f, vaultRotY, 0f);
            MfaProps.DressTokenReader(slot.gameObject, accent);

            // Factors, token and slot exist now, so the shared wiring path can
            // find and hook them — the same one a saved scene goes through.
            gauntlet.Configure(accent, passcode, gate, gateMessage);
            return gauntlet;
        }

        private void HideLegacyPasscodeMemo()
        {
            if (legacyPasscodeMemo == null)
                legacyPasscodeMemo = GameObject.Find("PasscodeMemo");
            if (legacyPasscodeMemo != null)
                legacyPasscodeMemo.SetActive(false);
        }

        private static void BuildPhoneReminder(Vector3 pos, Color accent)
        {
            var memo = new GameObject("PhoneOtpReminder");
            memo.transform.position = pos;

            MfaProps.BuildSignStand(memo.transform, new Vector3(0f, 1.85f, 0.03f), new Vector2(1.9f, 0.8f), accent);
            BuildKit.MakeLabel(memo.transform, new Vector3(0f, 1.85f, -0.01f),
                "DAILY OTP\n<size=30>CHECK YOUR PHONE</size>\n<size=22>code rotates every 24h</size>",
                accent, 0.020f);
        }
    }
}
