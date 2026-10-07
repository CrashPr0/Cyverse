using System.Text;
using TMPro;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Forensics;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The SOC investigation desk: a three-monitor console that opens the
    /// forensic query terminal. Owns the level's LogDatabase and
    /// InvestigationCase instances, so terminal progress survives stepping
    /// away and re-opening.
    /// </summary>
    public class ForensicsConsole : MonoBehaviour, IInteractable
    {
        public LogDatabase Database { get; private set; }
        public InvestigationCase[] Cases { get; private set; }

        // The center monitor (MonScreen_0) is a live diegetic readout instead of
        // a static emissive quad: a DiegeticScreen RenderTexture surface that
        // shows current case / question / progress / SOC handoff / score. This
        // is presentation only — the interactive QueryTerminal modal (keyboard
        // KQL entry, `answer` submission) still opens on the HUD via
        // QueryTerminal.Open(...); the readout mirrors the state the terminal
        // reads from these same Cases. See Docs/FORENSICS_REWORK_PLAN.md §"MIDDLE".
        private DiegeticScreen centerScreen;
        private TMP_Text readoutText;
        private bool readoutSubscribed;

        /// <summary>The first unsolved case (or the last one, once all done).</summary>
        public InvestigationCase ActiveCase
        {
            get
            {
                if (Cases == null || Cases.Length == 0) return null;
                foreach (var c in Cases) if (c != null && !c.IsComplete) return c;
                for (int i = Cases.Length - 1; i >= 0; i--)
                    if (Cases[i] != null) return Cases[i];
                return null;
            }
        }

        public bool AllComplete
        {
            get
            {
                if (Cases == null) return false;
                foreach (var c in Cases) if (c == null || !c.IsComplete) return false;
                return true;
            }
        }

        public int TotalQuestions
        {
            get
            {
                int n = 0;
                if (Cases != null)
                    foreach (var c in Cases)
                        if (c != null && c.questions != null) n += c.questions.Length;
                return n;
            }
        }

        public int TotalAnswered
        {
            get
            {
                int n = 0;
                if (Cases != null)
                    foreach (var c in Cases)
                        if (c != null) n += c.AnsweredCount;
                return n;
            }
        }

        void Awake()
        {
            // Plain C# objects don't serialize into saved scenes — rebuild
            // them on load so editor-saved copies of the level still work.
            if (Database == null) Database = LogDatabase.Build();
            if (Cases == null)
                Cases = new[] { InvestigationCase.SpartanGold(), InvestigationCase.MidnightExfil() };
        }

        private bool CustodyReady => ChainOfCustodyForm.Instance == null || ChainOfCustodyForm.Instance.IsComplete;

        private static bool AcquisitionPending =>
            PlugInStation.Instance != null && !PlugInStation.Instance.UploadStarted;
        private static bool AcquisitionRunning =>
            PlugInStation.Instance != null && PlugInStation.Instance.UploadInProgress;

        public bool CanInteract => true;
        public string Prompt => !CustodyReady
            ? "Complete the chain-of-custody form first"
            : AcquisitionPending
                ? "Dock the evidence device — start the upload"
            : AcquisitionRunning
                ? "Imaging the evidence device — E to skip to the terminal"
            : AllComplete
                ? "Review the case logs"
                : $"Work {(ActiveCase != null ? ActiveCase.title : "the case")} — Forensic Terminal";

        public void Interact(GameObject interactor)
        {
            if (!CustodyReady)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Analysis is locked until Evidence Intake accepts the custody record.",
                        new Color(0.90f, 0.66f, 0.14f));
                return;
            }
            // First E after custody docks the device and starts the upload; the
            // next E opens the terminal (finishing a running upload instantly,
            // so it never blocks analysis).
            PlugInStation upload = PlugInStation.Instance;
            if (upload != null && upload.StartUpload()) return;
            if (upload != null) upload.CompleteNow();
            if (QueryTerminal.Instance == null)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Terminal offline — no QueryTerminal in scene.", new Color(1f, 0.55f, 0.4f));
                return;
            }
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();

            var current = ActiveCase;
            bool lastCase = current == null || current == Cases[Cases.Length - 1];
            string closedNote = lastCase
                ? null // default "results are waiting" close-out
                : "<b>CASE CLOSED.</b> A new case file just hit your desk — Esc, then open the terminal again.";
            QueryTerminal.Instance.Open(Database, current, closedNote,
                guidedStart: Cases != null && Cases.Length > 0 && current == Cases[0]);

            // Repaint the diegetic monitor on engage: the QuestionAnswered event
            // covers mid-session progress, and this catches score/case-advance
            // that settle as the analyst opens the terminal.
            RefreshReadout();
        }

        // ---- Construction ----------------------------------------------------

        public static ForensicsConsole Build(Vector3 pos, float rotY, Color accent)
        {
            var root = new GameObject("ForensicsConsole");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Desk", root.transform,
                new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(3.2f, 1.0f, 1.1f), bodyMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "DeskTrim", root.transform,
                new Vector3(0f, 1.02f, -0.56f), Vector3.zero, new Vector3(3.2f, 0.04f, 0.02f),
                BuildKit.MakeEmissive(accent, 1.5f), collider: false);

            // Three angled monitors, KC7-appropriately wall-of-data green.
            // The center (i==0) is left as a body-only mount here; its screen is
            // a live DiegeticScreen built after the console component exists
            // (below). The side monitors are static emissive quads; the LEFT one
            // is swapped for the evidence-acquisition screen by PlugInStation.
            for (int i = -1; i <= 1; i++)
            {
                float yaw = i * 24f;
                var monitorPos = new Vector3(i * 1.05f, 1.65f, 0.18f);
                BuildKit.SpawnLocal(PrimitiveType.Cube, "MonBody_" + i, root.transform,
                    monitorPos, new Vector3(-8f, yaw, 0f),
                    new Vector3(1.0f, 0.65f, 0.05f), bodyMat, collider: i == 0);
                BuildMonitorStand(root.transform, i, monitorPos, Quaternion.Euler(-8f, yaw, 0f), 1.0f, bodyMat);
                if (i == 0) continue; // center screen is diegetic; built below
                BuildKit.SpawnLocal(PrimitiveType.Quad, "MonScreen_" + i, root.transform,
                    new Vector3(i * 1.05f, 1.65f, 0.14f), new Vector3(-8f, yaw, 0f),
                    new Vector3(0.9f, 0.55f, 1f),
                    BuildKit.MakeEmissive(new Color(0.05f, 0.22f, 0.10f), 0.8f), collider: false);
            }
            // NOTE: a large world-space "FORENSIC TERMINAL" TextMesh used to sit
            // here at local (0,1.62,0.1) — directly in front of MonScreen_0
            // (0,1.65,0.14) on the viewer's -Z side — and drew a room-sized label
            // straight across the live diegetic readout. It was redundant: the RT
            // readout already prints "CYVERSE FORENSIC TERMINAL" as its header,
            // and the station carries a mounted room sign above the console
            // (built below as "INVESTIGATION DESK", re-labelled by
            // Level3ForensicsPolish to "02 ANALYZE EVIDENCE"). Removed so the
            // centre monitor shows exactly one legible readout.

            BuildKit.SpawnLocal(PrimitiveType.Cube, "Keyboard", root.transform,
                new Vector3(0f, 1.03f, -0.25f), Vector3.zero, new Vector3(0.7f, 0.03f, 0.22f),
                BuildKit.MakeStandard(new Color(0.07f, 0.08f, 0.11f), 0.4f, 0.2f), collider: false);

            BuildKit.MakeSign(root.transform, pos + new Vector3(0f, 2.9f, 0f), "INVESTIGATION DESK", accent, 0.032f);

            var glow = new GameObject("ConsoleLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.2f, -1.2f);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.45f, 1f, 0.60f);
            l.range = 6f;
            l.intensity = 1.8f;

            var console = root.AddComponent<ForensicsConsole>();
            console.Database = LogDatabase.Build();
            console.Cases = new[]
            {
                InvestigationCase.SpartanGold(),
                InvestigationCase.MidnightExfil(),
            };

            // Build the live diegetic center monitor and paint its first frame.
            console.BuildCenterScreen(root.transform);
            return console;
        }

        /// <summary>VESA mount, neck and foot for one monitor. The monitors used to
        /// float 0.33 m above the desk, which reads as unfinished once props
        /// (the evidence cradle) sit beneath them. Collider-free so the console's
        /// interact ray is unaffected.</summary>
        private static void BuildMonitorStand(Transform root, int index, Vector3 monitorPos,
            Quaternion monitorRot, float deskTop, Material mat)
        {
            Vector3 back = monitorRot * Vector3.forward;      // out of the monitor's rear face
            Vector3 rear = monitorPos + back * 0.025f;        // body is 0.05 m deep
            Vector3 flatBack = new Vector3(back.x, 0f, back.z).normalized;
            float yaw = monitorRot.eulerAngles.y;

            BuildKit.SpawnLocal(PrimitiveType.Cube, "MonArm_Mount_" + index, root,
                rear + back * 0.02f, monitorRot.eulerAngles, new Vector3(0.14f, 0.14f, 0.04f), mat, collider: false);

            // The neck stands behind the mount plate and is hidden by the screen
            // above the desk gap.
            Vector3 neck = rear + back * 0.04f + flatBack * 0.02f;
            BuildKit.SpawnLocal(PrimitiveType.Cube, "MonArm_Neck_" + index, root,
                new Vector3(neck.x, (deskTop + rear.y) * 0.5f, neck.z), new Vector3(0f, yaw, 0f),
                new Vector3(0.055f, rear.y - deskTop, 0.04f), mat, collider: false);

            // Foot reaches forward under the screen, as on a real stand.
            Vector3 foot = neck - flatBack * 0.04f;
            BuildKit.SpawnLocal(PrimitiveType.Cube, "MonArm_Foot_" + index, root,
                new Vector3(foot.x, deskTop + 0.008f, foot.z), new Vector3(0f, yaw, 0f),
                new Vector3(0.26f, 0.016f, 0.18f), mat, collider: false);
        }

        // ---- Diegetic center monitor ----------------------------------------

        // The center screen quad matches the old MonScreen_0: 0.9 x 0.55 world
        // metres, sat at the same local pose on the console (which is why the
        // DiegeticScreen is re-parented in rather than placed by Create's world
        // args). Keep the RT aspect matched to the 0.9 x 0.55 surface so the
        // stacked readout is not stretched after it is mapped onto the quad.
        private static readonly Vector3 CenterScreenLocalPos = new Vector3(0f, 1.65f, 0.14f);
        private static readonly Vector3 CenterScreenLocalEuler = new Vector3(-8f, 0f, 0f);
        private static readonly Vector2 CenterScreenWorldSize = new Vector2(0.9f, 0.55f);

        /// <summary>Build the DiegeticScreen surface for MonScreen_0 and attach a
        /// TMP readout under its offscreen canvas, then render the first frame.
        /// Called once from <see cref="Build"/> after the console exists.</summary>
        private void BuildCenterScreen(Transform consoleRoot)
        {
            // Create at origin/identity; we re-parent the screen quad onto the
            // console mount so it inherits the console's placement and the -8°
            // monitor tilt. DiegeticScreen keeps its own offscreen canvas/camera
            // unparented far away, so re-parenting the quad is safe.
            centerScreen = DiegeticScreen.Create(Vector3.zero, 0f, CenterScreenWorldSize,
                rtWidth: 432, rtHeight: 264, name: "MonScreen_0");
            // Parent the DiegeticScreen component's own root under the console at
            // the exact old MonScreen_0 pose. The screen quad is a CHILD of that
            // root (see DiegeticScreen.BuildScreenQuad) at local origin, so it
            // follows automatically — no separate quad reparent. This also means
            // the screen's OnDestroy cleanup (RT + offscreen canvas/camera) tears
            // down with the level. The offscreen canvas/camera are held UNPARENTED
            // far away by the helper regardless, so this only moves the
            // lightweight screen root, not the isolation.
            var screenRoot = centerScreen.transform;
            screenRoot.SetParent(consoleRoot, false);
            screenRoot.localPosition = CenterScreenLocalPos;
            screenRoot.localRotation = Quaternion.Euler(CenterScreenLocalEuler);

            BuildReadout(centerScreen.CanvasRoot);
            SubscribeCaseEvents();
            RefreshReadout();
        }

        /// <summary>Lay out the readout widget(s) under the DiegeticScreen canvas.
        /// One rich-text TMP block fills the canvas; content is composed in
        /// <see cref="RefreshReadout"/>.</summary>
        private void BuildReadout(RectTransform canvasRoot)
        {
            if (canvasRoot == null) return;

            var go = new GameObject("Readout", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            // Inset a small margin so text isn't flush to the bezel.
            rt.offsetMin = new Vector2(14f, 12f);
            rt.offsetMax = new Vector2(-14f, -12f);

            readoutText = go.GetComponent<TextMeshProUGUI>();
            readoutText.font = TMP_Settings.defaultFontAsset;
            readoutText.fontSize = 15;
            readoutText.alignment = TextAlignmentOptions.TopLeft;
            readoutText.color = new Color(0.60f, 1f, 0.72f);
            readoutText.richText = true;
            readoutText.enableWordWrapping = true;
            readoutText.overflowMode = TextOverflowModes.Truncate;
            readoutText.raycastTarget = false;
        }

        /// <summary>Subscribe to each case's answered event so the diegetic
        /// readout re-renders as the analyst makes progress in the HUD terminal
        /// — without polling per frame (WebGL-friendly, matching DiegeticScreen's
        /// on-demand render). Idempotent.</summary>
        private void SubscribeCaseEvents()
        {
            if (readoutSubscribed || Cases == null) return;
            foreach (var c in Cases)
                if (c != null) c.QuestionAnswered += RefreshReadout;
            readoutSubscribed = true;
        }

        private void OnDestroy()
        {
            if (readoutSubscribed && Cases != null)
                foreach (var c in Cases)
                    if (c != null) c.QuestionAnswered -= RefreshReadout;
            readoutSubscribed = false;
        }

        /// <summary>Compose the live readout from this console's own case state
        /// (the same objects QueryTerminal reads) and render one RT frame.</summary>
        public void RefreshReadout()
        {
            if (readoutText == null || centerScreen == null) return;

            var sb = new StringBuilder();
            sb.Append("<b><color=#4CFF8C>CYVERSE FORENSIC TERMINAL</color></b>\n");

            if (SocProgress.TryGetEvidence(out var evidence))
            {
                sb.Append("<size=12><color=#4CE087>SOC HANDOFF [OK] VERIFIED</color>\n")
                  .Append(Escape(evidence.computer)).Append("  \u00b7  ").Append(Escape(evidence.user)).Append('\n')
                  .Append(Escape(evidence.alertTitle)).Append("</size>\n\n");
            }
            else
            {
                sb.Append("<size=12><color=#FF8866>SOC HANDOFF MISSING</color></size>\n\n");
            }

            var current = ActiveCase;
            if (AllComplete)
            {
                sb.Append("<color=#E5A823><b>ALL CASES CLOSED [OK]</b></color>\n\n");
            }
            else if (current != null)
            {
                sb.Append("<b>").Append(Escape(current.title)).Append("</b>\n");
                sb.Append("<size=13><color=#8FB8CC>")
                  .Append(current.AnsweredCount).Append('/').Append(current.questions.Length)
                  .Append(" solved</color></size>\n\n");

                var q = current.Current;
                if (q != null)
                {
                    sb.Append("<size=13><color=#5BC8FF><b>Q")
                      .Append(current.CurrentIndex + 1).Append(":</b></color> ")
                      .Append(Escape(q.prompt)).Append("</size>\n\n");
                }

                // Progress dots for the active case.
                sb.Append("<color=#8FB8CC><size=16>");
                for (int i = 0; i < current.questions.Length; i++)
                    sb.Append(current.questions[i].Answered
                        ? "<color=#4CE087>\u25a0</color>" : "\u25a1").Append(' ');
                sb.Append("</size></color>\n");
            }

            sb.Append("\n<size=12><color=#8FB8CC>CASE FILES ")
              .Append(TotalAnswered).Append('/').Append(TotalQuestions)
              .Append("   \u00b7   SCORE ").Append(ScoreSystem.Score).Append("</color></size>\n");
            sb.Append("<size=11><color=#607585>Press E \u2014 open terminal</color></size>");

            readoutText.text = sb.ToString();
            centerScreen.RenderNow();
        }

        /// <summary>Neutralize angle-bracket sequences in dynamic content so they
        /// don't collide with TMP rich-text tags (same convention as
        /// QueryTerminal.Escape).</summary>
        private static string Escape(string s) => string.IsNullOrEmpty(s)
            ? "" : s.Replace("<", "\u2039").Replace(">", "\u203a");
    }
}
