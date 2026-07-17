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
    /// Task 1 — IDENTIFICATION. An enrollment kiosk: one interaction runs a
    /// short biometric-capture sweep and issues the player's ID badge under
    /// their callsign. Every other task station gates on EnrolledInScene,
    /// which teaches the core idea structurally: identification comes first.
    /// </summary>
    public class BadgeStation : MonoBehaviour, IInteractable
    {
        public static BadgeStation Instance { get; private set; }

        /// <summary>Gate used by the other stations. A scene with no kiosk
        /// doesn't gate (avoids soft-locking hand-built scenes).</summary>
        public static bool EnrolledInScene => Instance == null || Instance.IsEnrolled;

        public bool IsEnrolled { get; private set; }
        public event Action Completed;

        public int points = 75;

        private TextMesh screenText;
        private Transform scanBar;
        private bool scanning;

        public bool CanInteract => !IsEnrolled;
        public string Prompt => "Enroll — create your ID badge";

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Interact(GameObject interactor)
        {
            if (IsEnrolled || scanning) return;
            StartCoroutine(Enroll());
        }

        private IEnumerator Enroll()
        {
            scanning = true;
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("BIOMETRIC CAPTURE — hold still…", new Color(0.55f, 0.85f, 1f));

            // Scan bar sweeps the panel twice (snaps under Reduce Motion).
            if (scanBar != null && !AccessibilitySettings.ReduceMotion)
            {
                for (float t = 0f; t < 1.6f; t += Time.deltaTime)
                {
                    float y = Mathf.PingPong(t * 1.25f, 1f);
                    scanBar.localPosition = new Vector3(0f, Mathf.Lerp(-0.38f, 0.38f, y), -0.03f);
                    yield return null;
                }
            }
            if (scanBar != null) scanBar.gameObject.SetActive(false);

            IsEnrolled = true;
            scanning = false;

            if (screenText != null)
                screenText.text = $"ID: {PlayerIdentity.Callsign}\nCLEARANCE: EMPLOYEE";

            ScoreSystem.Add(points);
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            BurstFX.Spawn(transform.position + Vector3.up * 1.9f, new Color(0.30f, 1f, 0.45f), 30);
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast($"ID BADGE ISSUED — {PlayerIdentity.Callsign}  +{points}",
                    new Color(0.90f, 0.66f, 0.14f));

            int newTerms = GlossaryProgress.UnlockTopic(StationSetup.Topic.IAM);
            if (newTerms > 0 && HudUI.Instance != null)
                HudUI.Instance.ShowToast($"+{newTerms} Glossary entries unlocked  [G]",
                    new Color(0.90f, 0.66f, 0.14f));

            Completed?.Invoke();
        }

        // ---- Construction ----------------------------------------------------

        public static BadgeStation Build(Vector3 pos, float rotY, Color accent)
        {
            var root = new GameObject("BadgeStation");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Pillar", root.transform,
                new Vector3(0f, 0.65f, 0.1f), Vector3.zero, new Vector3(0.55f, 1.3f, 0.35f), bodyMat, collider: true);

            // Angled capture panel; its face looks down local -Z at the player.
            BuildKit.SpawnLocal(PrimitiveType.Cube, "PanelBody", root.transform,
                new Vector3(0f, 1.55f, 0f), new Vector3(-18f, 0f, 0f), new Vector3(0.9f, 1.0f, 0.06f), bodyMat, collider: true);
            var panel = BuildKit.SpawnLocal(PrimitiveType.Quad, "PanelScreen", root.transform,
                new Vector3(0f, 1.55f, -0.041f), new Vector3(-18f, 0f, 0f), new Vector3(0.8f, 0.88f, 1f),
                BuildKit.MakeHologram(accent), collider: false);

            var bar = BuildKit.SpawnLocal(PrimitiveType.Cube, "ScanBar", panel.transform,
                new Vector3(0f, 0f, -0.03f), Vector3.zero, new Vector3(1f, 0.045f, 0.2f),
                BuildKit.MakeEmissive(new Color(0.4f, 1f, 0.6f), 2.5f), collider: false);

            var station = root.AddComponent<BadgeStation>();
            station.scanBar = bar.transform;
            station.screenText = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.55f, -0.12f),
                "ENROLL\n[E]", new Color(0.95f, 0.98f, 1f), 0.028f);
            station.screenText.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);

            BuildKit.MakeSign(root.transform, pos + new Vector3(0f, 2.6f, 0f), "ENROLLMENT", accent, 0.032f);

            var glow = new GameObject("KioskLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.1f, -0.8f);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = accent;
            l.range = 5f;
            l.intensity = 1.8f;

            return station;
        }
    }
}
