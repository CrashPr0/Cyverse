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
    ///   KNOW — type the daily passcode (MfaFactor → TypingChallenge)
    ///   HAVE — fetch the security token from its rack and slot it (Carryable
    ///          → DropZone; the walk across the room IS the lesson)
    ///   ARE  — biometric pad scan (MfaFactor)
    /// Indicator lights fill as factors clear; all three slide the vault
    /// panel open. Factor order is free — like real MFA.
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
        private Renderer[] lights;
        private Material litMat;
        private Transform vaultPanel;

        public void FactorCleared(int index)
        {
            if (IsComplete || index < 0 || index > 2 || cleared[index]) return;
            cleared[index] = true;

            if (lights != null && lights[index] != null && litMat != null)
                lights[index].sharedMaterial = litMat;

            int n = 0;
            foreach (bool c in cleared) if (c) n++;

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
                Vector3 from = vaultPanel.localPosition;
                Vector3 to = from + Vector3.down * 2.4f;
                if (AccessibilitySettings.ReduceMotion)
                    vaultPanel.localPosition = to;
                else
                {
                    for (float t = 0f; t < 1.1f; t += Time.deltaTime)
                    {
                        vaultPanel.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 1.1f));
                        yield return null;
                    }
                    vaultPanel.localPosition = to;
                }
            }

            ScoreSystem.Add(completionPoints);
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            BurstFX.Spawn(transform.position + Vector3.up * 2.2f, new Color(0.90f, 0.66f, 0.14f), 40);
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast($"MULTI-FACTOR VERIFIED — VAULT OPEN  +{completionPoints}",
                    new Color(0.90f, 0.66f, 0.14f));

            Completed?.Invoke();
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

            var frameMat = BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.55f, 0.35f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Frame_L", root.transform,
                new Vector3(-1.3f, 1.7f, 0f), Vector3.zero, new Vector3(0.35f, 3.4f, 0.5f), frameMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Frame_R", root.transform,
                new Vector3(1.3f, 1.7f, 0f), Vector3.zero, new Vector3(0.35f, 3.4f, 0.5f), frameMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Lintel", root.transform,
                new Vector3(0f, 3.55f, 0f), Vector3.zero, new Vector3(2.95f, 0.35f, 0.5f), frameMat, collider: true);

            var panel = BuildKit.SpawnLocal(PrimitiveType.Cube, "VaultPanel", root.transform,
                new Vector3(0f, 1.7f, 0.05f), Vector3.zero, new Vector3(2.25f, 3.3f, 0.18f),
                BuildKit.MakeStandard(new Color(0.13f, 0.14f, 0.19f), 0.7f, 0.6f), collider: true);

            var gauntlet = root.AddComponent<MfaGauntlet>();
            gauntlet.vaultPanel = panel.transform;
            gauntlet.litMat = BuildKit.MakeEmissive(new Color(0.30f, 1f, 0.45f), 2.4f);
            var offMat = BuildKit.MakeStandard(new Color(0.16f, 0.18f, 0.24f), 0.4f, 0.2f);
            gauntlet.lights = new Renderer[3];
            for (int i = 0; i < 3; i++)
            {
                var lamp = BuildKit.SpawnLocal(PrimitiveType.Sphere, "FactorLight_" + i, root.transform,
                    new Vector3((i - 1) * 0.55f, 3.55f, -0.3f), Vector3.zero,
                    new Vector3(0.22f, 0.22f, 0.22f), offMat, collider: false);
                gauntlet.lights[i] = lamp.GetComponent<Renderer>();
            }

            BuildKit.MakeSign(root.transform, vaultPos + new Vector3(0f, 4.3f, 0f), "MFA VAULT", accent, 0.032f);

            // KNOW — passcode terminal, with the memo plaque beside it.
            var know = MfaFactor.Build(terminalPos, 0f, MfaFactor.Kind.Knowledge, gauntlet, accent);
            know.passcode = passcode;
            know.gate = gate;
            know.gateMessage = gateMessage;
            BuildMemo(terminalPos + new Vector3(0f, 0f, 1.6f), passcode, accent);

            // ARE — biometric pad.
            var are = MfaFactor.Build(padPos, 0f, MfaFactor.Kind.Biometric, gauntlet, accent);
            are.gate = gate;
            are.gateMessage = gateMessage;

            // HAVE — token on a rack across the room, slotted by the vault.
            var rack = new GameObject("TokenRack");
            rack.transform.position = tokenRackPos;
            BuildKit.SpawnLocal(PrimitiveType.Cube, "RackTop", rack.transform,
                new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.8f, 1.0f, 0.8f),
                BuildKit.MakeStandard(new Color(0.09f, 0.10f, 0.14f), 0.5f, 0.4f), collider: true);
            BuildKit.MakeLabel(rack.transform, new Vector3(0f, 1.9f, 0f), "TOKEN CHARGER", accent, 0.024f, billboard: true);
            var token = Carryable.Build(tokenRackPos + Vector3.up * 1.0f, "SECURITY TOKEN", "mfa_token", accent, token: true);
            token.gate = gate;
            token.gateMessage = gateMessage;

            var slot = DropZone.Build(slotPos, "TOKEN SLOT", accent);
            slot.accepts = item => item.id == "mfa_token";
            slot.onAccepted = item =>
            {
                item.Consume();
                BurstFX.Spawn(slot.transform.position + Vector3.up * 1.2f, accent, 20);
                gauntlet.FactorCleared(1);
            };
            slot.onRejected = item =>
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("This slot only takes the SECURITY TOKEN.", new Color(1f, 0.55f, 0.4f));
            };

            return gauntlet;
        }

        private static void BuildMemo(Vector3 pos, string passcode, Color accent)
        {
            var memo = new GameObject("PasscodeMemo");
            memo.transform.position = pos;

            BuildKit.SpawnLocal(PrimitiveType.Cube, "Post", memo.transform,
                new Vector3(0f, 0.8f, 0f), Vector3.zero, new Vector3(0.08f, 1.6f, 0.08f),
                BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.5f, 0.4f), collider: false);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Plaque", memo.transform,
                new Vector3(0f, 1.85f, 0.03f), Vector3.zero, new Vector3(1.9f, 0.8f, 0.06f),
                BuildKit.MakeStandard(new Color(0.05f, 0.055f, 0.08f), 0.5f, 0.4f), collider: false);
            BuildKit.MakeLabel(memo.transform, new Vector3(0f, 1.85f, -0.01f),
                $"DAILY PASSCODE\n<size=42>{passcode}</size>\n<size=22>rotates every 24h — do not share</size>",
                accent, 0.020f);
        }
    }
}
