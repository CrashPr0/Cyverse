using System;
using System.Collections;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Dialogue;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The Security Scanner from the CyVerse Script: once every station is
    /// reviewed, the player authenticates with a face scan to finish onboarding
    /// ("Access Granted — Level: Employee"). A single press starts the scan (a
    /// press-and-wait beat rather than hold-to-charge, which is friendlier for
    /// motor accessibility); progress is captioned, then the completion
    /// dialogue plays and <see cref="Completed"/> fires.
    /// </summary>
    public class FaceScanner : MonoBehaviour, IInteractable
    {
        public float scanSeconds = 2.6f;
        public int scanPoints = 100;

        [Tooltip("Brightened when the scanner activates and pulsed during a scan.")]
        public Light scanLight;

        private bool activated;
        private bool scanning;
        private bool completed;

        /// <summary>Fires once, after the scan and its dialogue have finished.</summary>
        public event Action Completed;

        public bool IsCompleted => completed;

        public string Prompt => "Authenticate — Scan Face";
        public bool CanInteract => activated && !scanning && !completed;

        /// <summary>Called by Level0Manager when all stations are reviewed.</summary>
        public void Activate()
        {
            activated = true;
            if (scanLight != null) scanLight.intensity = 2.5f;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;
            StartCoroutine(ScanRoutine());
        }

        private IEnumerator ScanRoutine()
        {
            scanning = true;
            GameState.DialogueActive = true; // suspend movement during the scan
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();

            float baseIntensity = scanLight != null ? scanLight.intensity : 0f;
            float t = 0f;
            while (t < scanSeconds)
            {
                t += Time.deltaTime;
                int pct = Mathf.Min(Mathf.RoundToInt(t / scanSeconds * 100f), 100);
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowCaption(
                        $"<b><color=#5BC8FF>SECURITY SCANNER</color></b>\nScanning face…  {pct}%");
                if (scanLight != null && !Settings.AccessibilitySettings.ReduceMotion)
                    scanLight.intensity = baseIntensity + Mathf.PingPong(t * 6f, 2f);
                yield return null;
            }

            if (scanLight != null) scanLight.intensity = baseIntensity;
            if (HudUI.Instance != null) HudUI.Instance.HideCaption();
            GameState.DialogueActive = false;

            completed = true;
            ScoreSystem.Add(scanPoints);
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();

            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Play(Level0Content.Complete(), () => Completed?.Invoke());
            else
                Completed?.Invoke();

            scanning = false;
        }
    }
}
