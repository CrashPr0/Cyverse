using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Forensics;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The RIGHT-hand Forensics station: the diegetic "plug-in / upload" beat.
    ///
    /// After the player finishes the chain of custody at the LEFT station, they
    /// walk up here and the evidence phone "plugs in" and uploads to the
    /// station. This is presentation only — a scripted DOCKING ANIMATION with
    /// NO literal connect/tap interaction:
    ///
    ///   1. A phone prop (built here, reusing DiegeticPhone.BuildProp geometry —
    ///      replicated locally because that builder is private and to keep this
    ///      a FRESH build that never touches the I/AM MFA vault phone at
    ///      MfaGauntlet.cs:211) starts in a raised "approach" pose above the desk.
    ///   2. On the "after chain of custody" moment (ChainOfCustodyForm.Completed),
    ///      a coroutine SmoothStep-lerps the phone's world transform down into a
    ///      dock slot on the desk.
    ///   3. Then an "UPLOADING…" progress readout plays on the station's diegetic
    ///      screen (a DiegeticScreen RT surface): a progress bar + percent painted
    ///      onto the screen's CanvasRoot, RenderNow()'d as it advances, ending on
    ///      "UPLOAD COMPLETE".
    ///
    /// The report-submission flow (ForensicsReportStation.Interact ->
    /// manager.SubmitReport()) is untouched; this station only animates.
    ///
    /// Build/wiring is done by Level3ForensicsPolish.BuildReportingZone(): it
    /// creates the DiegeticScreen and this component and calls
    /// <see cref="Configure"/> with the desk anchor + screen reference.
    /// </summary>
    public sealed class PlugInStation : MonoBehaviour
    {
        // Docking animation timing (seconds).
        private const float DockDuration = 1.4f;
        private const float UploadDuration = 3.2f;

        // Accent used for the "uploading" readout — the report zone's gold.
        private static readonly Color Accent = new Color(0.92f, 0.72f, 0.30f);
        private static readonly Color Green = new Color(0.30f, 1f, 0.55f);

        private DiegeticScreen screen;

        // The phone prop and its two poses.
        private Transform phone;
        private Vector3 approachPos;   // raised "just walked up" pose
        private Quaternion approachRot;
        private Vector3 dockPos;       // seated in the dock slot
        private Quaternion dockRot;
        private Light phoneGlow;

        // Screen UI widgets (built lazily onto the DiegeticScreen canvas).
        private Text screenTitle;
        private Text screenPercent;
        private Text screenStatus;
        private RectTransform progressFill;
        private float progressTrackWidth;

        private bool played;
        private ChainOfCustodyForm subscribedForm;

        /// <summary>
        /// Wire the station. <paramref name="deskTop"/> is the world position of
        /// the desk surface the dock sits on (~(9.2, deskTopY, 11)).
        /// <paramref name="diegeticScreen"/> is the RT screen built by
        /// Level3ForensicsPolish for this desk.
        /// </summary>
        public void Configure(Vector3 deskTop, DiegeticScreen diegeticScreen)
        {
            screen = diegeticScreen;
            BuildDockAndPhone(deskTop);
            BuildScreenReadout();
            // Custody may not exist yet (built by another station), and the
            // event fires exactly once when custody is accepted — poll for the
            // instance, then subscribe.
            StartCoroutine(SubscribeWhenReady());
        }

        // ---- Prop construction ----------------------------------------------

        /// <summary>
        /// A small dock slot on the desk plus the phone prop. Geometry mirrors
        /// DiegeticPhone.BuildProp() (private there): a low dock pad, an upright
        /// shell body, an emissive screen face, and a dim glow light. Built as a
        /// child of this station so it moves/cleans up with it, and entirely
        /// independent of the MFA vault phone.
        /// </summary>
        private void BuildDockAndPhone(Vector3 deskTop)
        {
            var caseMat = BuildKit.MakeStandard(new Color(0.05f, 0.06f, 0.09f), 0.35f, 0.25f);
            var dockMat = BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.5f, 0.4f);

            // The dock slot sits on the desk, offset toward the player (-Z) so
            // the phone lands in front of the monitor rather than under it.
            Vector3 slotCenter = deskTop + new Vector3(0f, 0.03f, -0.42f);
            var dock = BuildKit.Spawn(PrimitiveType.Cube, "DF_PhoneDock", transform,
                slotCenter, new Vector3(0.42f, 0.06f, 0.30f), dockMat, collider: false);
            // A shallow cradle lip so the slot reads as a place the phone seats.
            BuildKit.SpawnLocal(PrimitiveType.Cube, "DockLip", dock.transform,
                new Vector3(0f, 0.5f, -0.5f), Vector3.zero, new Vector3(1f, 1.0f, 0.12f),
                dockMat, collider: false);

            // The docked pose: phone standing just above the dock pad, upright,
            // screen facing the player on -Z (identity — Quad renders on -Z).
            dockPos = slotCenter + new Vector3(0f, 0.20f, 0f);
            dockRot = Quaternion.identity;

            // The approach pose: raised and tilted, as if just brought up to the
            // desk before it settles into the dock.
            approachPos = dockPos + new Vector3(0f, 0.55f, -0.30f);
            approachRot = Quaternion.Euler(24f, 0f, 0f);

            // ---- The phone prop (replicated DiegeticPhone.BuildProp geometry).
            var phoneRoot = new GameObject("DF_EvidencePhone");
            phoneRoot.transform.SetParent(transform, false);
            phoneRoot.transform.SetPositionAndRotation(approachPos, approachRot);
            phone = phoneRoot.transform;

            // Upright body.
            var body = new GameObject("Body");
            body.transform.SetParent(phone, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;

            BuildKit.SpawnLocal(PrimitiveType.Cube, "Shell", body.transform,
                Vector3.zero, Vector3.zero, new Vector3(0.20f, 0.40f, 0.025f), caseMat, collider: false);

            // Emissive screen face (own small readout; the *station* screen shows
            // the upload progress — this is just the "device is on" tell).
            var faceMat = BuildKit.MakeEmissive(new Color(0.10f, 0.42f, 0.30f), 1.4f);
            BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", body.transform,
                new Vector3(0f, 0f, -0.014f), Vector3.zero, new Vector3(0.176f, 0.36f, 1f),
                faceMat, collider: false);

            // A dim glow so the phone reads as a lit device; brightens on dock.
            var glow = new GameObject("PhoneGlow");
            glow.transform.SetParent(body.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0f, -0.20f);
            phoneGlow = glow.AddComponent<Light>();
            phoneGlow.type = LightType.Point;
            phoneGlow.color = Green;
            phoneGlow.range = 1.4f;
            phoneGlow.intensity = 0.35f;
            phoneGlow.shadows = LightShadows.None;
            phoneGlow.renderMode = LightRenderMode.ForceVertex;
        }

        // ---- Screen readout -------------------------------------------------

        /// <summary>Build the UPLOADING readout widgets onto the DiegeticScreen's
        /// WorldSpace canvas. Laid out against CanvasSize; RenderNow() re-applies
        /// the isolation layer so these draw only into the RT.</summary>
        private void BuildScreenReadout()
        {
            if (screen == null || screen.CanvasRoot == null) return;
            RectTransform canvas = screen.CanvasRoot;
            Vector2 size = screen.CanvasSize;

            // Background fill so the screen doesn't show the camera clear color
            // through gaps.
            var bg = new GameObject("UploadBg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvas, false);
            var bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.015f, 0.05f, 0.038f, 1f);

            screenTitle = MakeText(canvas, "UploadTitle", 30, TextAnchor.MiddleCenter);
            screenTitle.fontStyle = FontStyle.Bold;
            screenTitle.color = Accent;
            var tr = screenTitle.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-16f, 64f); tr.anchoredPosition = new Vector2(0f, -22f);
            screenTitle.text = "EVIDENCE UPLOAD";

            // Progress track (background) + fill (foreground). The fill's width
            // is lerped 0..trackWidth during the upload.
            var track = new GameObject("ProgressTrack", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(canvas, false);
            var trackRt = (RectTransform)track.transform;
            progressTrackWidth = size.x - 60f;
            trackRt.anchorMin = new Vector2(0.5f, 0.5f); trackRt.anchorMax = new Vector2(0.5f, 0.5f);
            trackRt.pivot = new Vector2(0.5f, 0.5f);
            trackRt.sizeDelta = new Vector2(progressTrackWidth, 34f);
            trackRt.anchoredPosition = new Vector2(0f, -6f);
            track.GetComponent<Image>().color = new Color(0.04f, 0.12f, 0.09f, 1f);

            var fill = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            progressFill = (RectTransform)fill.transform;
            progressFill.anchorMin = new Vector2(0f, 0f); progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.pivot = new Vector2(0f, 0.5f);
            progressFill.offsetMin = Vector2.zero; progressFill.offsetMax = Vector2.zero;
            progressFill.sizeDelta = new Vector2(0f, 0f); // width driven at runtime
            fill.GetComponent<Image>().color = Green;

            screenPercent = MakeText(canvas, "UploadPercent", 44, TextAnchor.MiddleCenter);
            screenPercent.fontStyle = FontStyle.Bold;
            screenPercent.color = Green;
            var pr = screenPercent.rectTransform;
            pr.anchorMin = new Vector2(0.5f, 0.5f); pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(size.x - 20f, 60f); pr.anchoredPosition = new Vector2(0f, 58f);
            screenPercent.text = "";

            screenStatus = MakeText(canvas, "UploadStatus", 20, TextAnchor.MiddleCenter);
            screenStatus.color = new Color(0.60f, 0.86f, 0.78f);
            var st = screenStatus.rectTransform;
            st.anchorMin = new Vector2(0f, 0f); st.anchorMax = new Vector2(1f, 0f); st.pivot = new Vector2(0.5f, 0f);
            st.sizeDelta = new Vector2(-16f, 70f); st.anchoredPosition = new Vector2(0f, 18f);
            screenStatus.text = "AWAITING CUSTODY ACCEPTANCE";

            screen.RenderNow();
        }

        private Text MakeText(Transform parent, string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = HudUI.UIFont != null ? HudUI.UIFont : HudUI.LoadFont();
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ---- Trigger --------------------------------------------------------

        /// <summary>Poll for the custody form (built by the LEFT station, which
        /// may not exist yet at build time) and subscribe to its Completed event
        /// once. Non-destructive: if custody was already complete before we
        /// subscribed, play immediately.</summary>
        private IEnumerator SubscribeWhenReady()
        {
            // Cap the wait so a scene that never builds custody doesn't spin
            // forever; ~20s at 4 checks/sec is plenty for scene bring-up.
            for (int i = 0; i < 80 && subscribedForm == null; i++)
            {
                ChainOfCustodyForm form = ChainOfCustodyForm.Instance;
                if (form != null)
                {
                    subscribedForm = form;
                    subscribedForm.Completed += OnCustodyCompleted;
                    // Custody could have been accepted before we bound.
                    if (form.IsComplete) OnCustodyCompleted();
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        private void OnCustodyCompleted()
        {
            if (played) return;
            played = true;
            StartCoroutine(DockAndUpload());
        }

        // ---- Animation ------------------------------------------------------

        private IEnumerator DockAndUpload()
        {
            // Phase 1: dock the phone (SmoothStep world-transform lerp).
            if (phone != null)
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / DockDuration;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                    phone.SetPositionAndRotation(
                        Vector3.Lerp(approachPos, dockPos, k),
                        Quaternion.Slerp(approachRot, dockRot, k));
                    yield return null;
                }
                phone.SetPositionAndRotation(dockPos, dockRot);
                if (phoneGlow != null) phoneGlow.intensity = 1.0f;
            }

            if (Cyverse.Audio.Sfx.Instance != null) Cyverse.Audio.Sfx.Instance.PlayConfirm();

            // Phase 2: upload progress on the diegetic screen.
            if (screen != null)
            {
                if (screenStatus != null) screenStatus.text = "PHONE DOCKED · UPLOADING TO STATION";
                float t = 0f;
                int lastShown = -1;
                while (t < 1f)
                {
                    t += Time.deltaTime / UploadDuration;
                    float k = Mathf.Clamp01(t);
                    int pct = Mathf.RoundToInt(k * 100f);
                    if (progressFill != null)
                        progressFill.sizeDelta = new Vector2(progressTrackWidth * k, 0f);
                    if (pct != lastShown)
                    {
                        lastShown = pct;
                        if (screenPercent != null) screenPercent.text = pct + "%";
                        screen.RenderNow();
                    }
                    yield return null;
                }
                if (progressFill != null) progressFill.sizeDelta = new Vector2(progressTrackWidth, 0f);
                if (screenPercent != null) screenPercent.text = "100%";
                if (screenTitle != null) screenTitle.text = "UPLOAD COMPLETE";
                if (screenTitle != null) screenTitle.color = Green;
                if (screenStatus != null) screenStatus.text = "EVIDENCE IMAGE RECEIVED · READY FOR ANALYSIS";
                screen.RenderNow();
            }
        }

        private void OnDestroy()
        {
            if (subscribedForm != null)
                subscribedForm.Completed -= OnCustodyCompleted;
        }
    }
}
