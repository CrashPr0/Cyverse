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
    /// One factor station of the MFA vault: Knowledge (opens the typed
    /// passcode challenge) or Biometric (a short in-place scan). The HAVE
    /// factor is a Carryable + DropZone, so it needs no component here.
    /// </summary>
    public class MfaFactor : MonoBehaviour, IInteractable
    {
        public enum Kind { Knowledge, Biometric }

        public Kind kind;
        public MfaGauntlet gauntlet;
        public string passcode;
        public Func<bool> gate;
        public string gateMessage = "You can't use this yet.";

        private bool cleared, busy;
        private Light scanLight;

        public bool CanInteract => !cleared;
        public string Prompt => kind == Kind.Knowledge
            ? "Passcode terminal — something you KNOW"
            : "Biometric pad — something you ARE";

        public void Interact(GameObject interactor)
        {
            if (cleared || busy) return;
            if (gate != null && !gate())
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast(gateMessage, new Color(1f, 0.55f, 0.4f));
                return;
            }

            if (kind == Kind.Knowledge)
            {
                if (TypingChallenge.Instance == null) { Clear(); return; } // no UI — don't soft-lock
                busy = true;
                TypingChallenge.Instance.Show(
                    "SOMETHING YOU KNOW",
                    "Enter the daily passcode.\n\n<size=20><color=#8FB8CC>It's posted on the memo beside this terminal. (Noticed how easy that made it? Real attackers look for notes like that too — a lesson for later.)</color></size>",
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
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (scanLight != null)
                    scanLight.intensity = 1.5f + Mathf.PingPong(t * 6f, 2.2f);
                yield return null;
            }
            if (scanLight != null) scanLight.intensity = 1.2f;

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
            if (kind == Kind.Knowledge)
            {
                BuildKit.SpawnLocal(PrimitiveType.Cube, "Stand", root.transform,
                    new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.5f, 1.1f, 0.4f), bodyMat, collider: true);
                BuildKit.SpawnLocal(PrimitiveType.Cube, "ScreenBody", root.transform,
                    new Vector3(0f, 1.3f, 0.02f), new Vector3(-20f, 0f, 0f), new Vector3(0.75f, 0.5f, 0.05f), bodyMat, collider: true);
                BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", root.transform,
                    new Vector3(0f, 1.3f, -0.017f), new Vector3(-20f, 0f, 0f), new Vector3(0.66f, 0.42f, 1f),
                    BuildKit.MakeHologram(accent), collider: false);
                BuildKit.MakeLabel(root.transform, new Vector3(0f, 2.0f, 0f), "PASSCODE", accent, 0.026f, billboard: true);
            }
            else
            {
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Pad", root.transform,
                    new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(0.9f, 0.04f, 0.9f),
                    BuildKit.MakeHologram(accent), collider: true);
                BuildKit.SpawnLocal(PrimitiveType.Cube, "Pillar", root.transform,
                    new Vector3(0.7f, 0.8f, 0f), Vector3.zero, new Vector3(0.18f, 1.6f, 0.18f), bodyMat, collider: true);
                BuildKit.SpawnLocal(PrimitiveType.Sphere, "Eye", root.transform,
                    new Vector3(0.7f, 1.7f, 0f), Vector3.zero, new Vector3(0.16f, 0.16f, 0.16f),
                    BuildKit.MakeEmissive(accent, 2f), collider: false);
                BuildKit.MakeLabel(root.transform, new Vector3(0f, 2.0f, 0f), "BIOMETRIC", accent, 0.026f, billboard: true);
            }

            var factor = root.AddComponent<MfaFactor>();
            factor.kind = kind;
            factor.gauntlet = gauntlet;

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
