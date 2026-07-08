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
    /// Level 1's completion gate: the Threat Response Console. Once every
    /// station is reviewed, the player certifies to finish the Cyber Defense
    /// rotation. Mirrors FaceScanner's press-and-wait pattern (friendlier for
    /// motor accessibility than hold-to-charge) but re-themed — a separate
    /// class rather than a generalized FaceScanner so Level 0's proven,
    /// working gate is never put at risk by a shared abstraction.
    /// </summary>
    public class Level1Gate : MonoBehaviour, IInteractable
    {
        public float certifySeconds = 2.6f;
        public int certifyPoints = 100;

        [Tooltip("Brightened when the console activates and pulsed while certifying.")]
        public Light gateLight;

        private bool activated;
        private bool certifying;
        private bool completed;
        private Renderer panelRenderer;

        void Start()
        {
            var panel = transform.Find("GatePanel");
            if (panel != null) panelRenderer = panel.GetComponent<Renderer>();
        }

        /// <summary>Fires once, after certification and its dialogue have finished.</summary>
        public event Action Completed;

        public bool IsCompleted => completed;

        public string Prompt => "Certify — Threat Response Console";
        public bool CanInteract => activated && !certifying && !completed;

        /// <summary>Called by Level1Manager when all stations are reviewed.</summary>
        public void Activate()
        {
            activated = true;
            if (gateLight != null) gateLight.intensity = 2.5f;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;
            StartCoroutine(CertifyRoutine());
        }

        private IEnumerator CertifyRoutine()
        {
            certifying = true;
            GameState.DialogueActive = true; // suspend movement during certification
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();

            float baseIntensity = gateLight != null ? gateLight.intensity : 0f;

            // Drive the hologram's sweep bar hard while certifying — the panel
            // visibly "works," then settles back.
            Material panelMat = panelRenderer != null ? panelRenderer.material : null;
            bool hasBar = panelMat != null && panelMat.HasProperty("_BarSpeed");
            float oldBarSpeed = 0f, oldBarSize = 0f;
            if (hasBar)
            {
                oldBarSpeed = panelMat.GetFloat("_BarSpeed");
                oldBarSize = panelMat.GetFloat("_BarSize");
                panelMat.SetFloat("_BarSpeed", 3.5f);
                panelMat.SetFloat("_BarSize", 0.20f);
            }

            float t = 0f;
            while (t < certifySeconds)
            {
                t += Time.deltaTime;
                int pct = Mathf.Min(Mathf.RoundToInt(t / certifySeconds * 100f), 100);
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowCaption(
                        $"<b><color=#5BC8FF>THREAT RESPONSE CONSOLE</color></b>\nCertifying {PlayerIdentity.Callsign}…  {pct}%");
                if (gateLight != null && !Settings.AccessibilitySettings.ReduceMotion)
                    gateLight.intensity = baseIntensity + Mathf.PingPong(t * 6f, 2f);
                yield return null;
            }

            if (gateLight != null) gateLight.intensity = baseIntensity;
            if (hasBar)
            {
                panelMat.SetFloat("_BarSpeed", oldBarSpeed);
                panelMat.SetFloat("_BarSize", oldBarSize);
            }
            if (HudUI.Instance != null) HudUI.Instance.HideCaption();
            GameState.DialogueActive = false;

            completed = true;
            ScoreSystem.Add(certifyPoints);
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            BurstFX.Spawn(transform.position + Vector3.up * 2.3f, new Color(0.25f, 0.8f, 1f), 40);

            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Play(Level1Content.Complete(), () => Completed?.Invoke());
            else
                Completed?.Invoke();

            certifying = false;
        }
    }
}
