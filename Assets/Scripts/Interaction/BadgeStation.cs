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
        private Transform badgeCard;
        private TextMesh caption;
        private bool scanning;

        public bool CanInteract => !IsEnrolled;
        public string Prompt => "Enroll — create your ID badge";

        void Awake()
        {
            if (Instance == null) Instance = this;
            NormalizeScreen();
            BuildKit.EnsureAimCollider(gameObject, height: 3.0f, width: 1.8f);
        }

        void OnValidate() => NormalizeScreen();

        /// <summary>The screen's tilt: upright enough that the kiosk reads as
        /// a terminal rather than a lectern, still facing up at the player.</summary>
        private const float ScreenPitch = 22f;

        private void NormalizeScreen()
        {
            Transform body = BuildKit.AlignKioskScreen(transform, "PanelBody", "PanelScreen", ScreenPitch);
            if (scanBar == null)
            {
                Transform panel = transform.Find("PanelScreen");
                if (panel != null) scanBar = panel.Find("ScanBar");
            }
            if (scanBar != null && !scanning)
                scanBar.gameObject.SetActive(false);
            if (screenText == null)
            {
                Transform label = transform.Find("Label_ENROLL\n[E]");
                if (label != null) screenText = label.GetComponent<TextMesh>();
            }
            BuildKit.PlaceOnKioskScreen(screenText != null ? screenText.transform : null,
                body, Vector2.zero, 0.025f);
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
                scanBar.gameObject.SetActive(true);
                scanBar.localPosition = new Vector3(0f, -0.38f, -0.03f);
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

            ShowIssuedBadge();
            if (badgeCard != null) StartCoroutine(IssueCard());

            ScoreSystem.Add(points);
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            BurstFX.SpawnAbove(transform, new Color(0.30f, 1f, 0.45f), 30, minimumHeight: 1.9f);
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast($"ID BADGE ISSUED — {PlayerIdentity.Callsign}  +{points}",
                    new Color(0.90f, 0.66f, 0.14f));

            int newTerms = GlossaryProgress.UnlockTopic(StationSetup.Topic.IAM);
            if (newTerms > 0 && HudUI.Instance != null)
                HudUI.Instance.ShowToast($"+{newTerms} Glossary entries unlocked  [G]",
                    new Color(0.90f, 0.66f, 0.14f));

            Completed?.Invoke();
        }

        /// <summary>The issued ID on the screen: the callsign as large as the
        /// screen allows (callsigns run to 12 characters) over a clearance
        /// line sized to fit, and the caption turns to ENROLLED.</summary>
        private void ShowIssuedBadge()
        {
            if (caption != null)
            {
                caption.text = "ENROLLED";
                caption.color = MfaProps.Verified;
            }
            if (screenText == null) return;
            const float width = 0.54f, perChar = 4.2f; // HUD font: about 4.2 x characterSize per capital
            string callsign = PlayerIdentity.Callsign;
            screenText.characterSize = Mathf.Min(0.02f, width / (Mathf.Max(callsign.Length, 1) * perChar));
            const string clearance = "CLEARANCE: EMPLOYEE";
            int small = Mathf.FloorToInt(screenText.fontSize *
                Mathf.Min(1f, width / (clearance.Length * perChar * screenText.characterSize)));
            screenText.text = $"{callsign}\n<size={small}>{clearance}</size>";
        }

        /// <summary>The new badge slides half out of the printer slot.</summary>
        private IEnumerator IssueCard()
        {
            badgeCard.gameObject.SetActive(true);
            float duration = AccessibilitySettings.ReduceMotion ? 0f : 0.7f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                badgeCard.localPosition = Vector3.Lerp(EnrollmentProps.CardHidden, EnrollmentProps.CardIssued,
                    Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            badgeCard.localPosition = EnrollmentProps.CardIssued;
        }

        // ---- Construction ----------------------------------------------------

        public static BadgeStation Build(Vector3 pos, float rotY, Color accent)
        {
            var root = new GameObject("BadgeStation");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);

            // Positive pitch exposes the screen surface to a standing player.
            BuildKit.SpawnLocal(PrimitiveType.Cube, "PanelBody", root.transform,
                new Vector3(0f, 1.5f, 0f), new Vector3(ScreenPitch, 0f, 0f), new Vector3(0.74f, 0.8f, 0.06f), bodyMat, collider: true);
            var panel = BuildKit.SpawnLocal(PrimitiveType.Quad, "PanelScreen", root.transform,
                new Vector3(0f, 1.5f, -0.042f), new Vector3(ScreenPitch, 0f, 0f), new Vector3(0.64f, 0.7f, 1f),
                BuildKit.MakeHologram(accent), collider: false);

            var bar = BuildKit.SpawnLocal(PrimitiveType.Cube, "ScanBar", panel.transform,
                new Vector3(0f, 0f, -0.03f), Vector3.zero, new Vector3(1f, 0.045f, 0.2f),
                BuildKit.MakeEmissive(new Color(0.4f, 1f, 0.6f), 2.5f), collider: false);

            var station = root.AddComponent<BadgeStation>();
            station.scanBar = bar.transform;
            station.screenText = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.5f, -0.12f),
                "ENROLL\n[E]", new Color(0.95f, 0.98f, 1f), 0.022f);
            station.NormalizeScreen();
            Transform body = root.transform.Find("PanelBody");
            station.badgeCard = EnrollmentProps.BuildKiosk(root.transform, body, panel.transform, accent);
            station.caption = BuildKit.MakeLabel(root.transform, Vector3.zero, "IDENTITY ENROLLMENT",
                new Color(0.62f, 0.78f, 0.90f), 0.0065f, style: FontStyle.Normal);
            BuildKit.PlaceOnKioskScreen(station.caption.transform, body, new Vector2(0f, 0.29f), 0.02f);

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
