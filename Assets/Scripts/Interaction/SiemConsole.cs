using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Forensics;
using Cyverse.Level;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>Data-driven SOC alert board. The player flags one of four
    /// event rows, walks to that computer, compares the alert with its live
    /// activity, and chooses a benign/possible-true-positive verdict.</summary>
    public class SiemConsole : MonoBehaviour, IInteractable, IGameplayActionTarget
    {
        public event Action Completed;
        public bool IsComplete { get; private set; }
        public int pointsPerScenario = 120;

        private Level2Content.SocScenario[] scenarios;
        private int scenarioIndex;
        private int selectedRow;
        private int flaggedRow = -1;
        private int wrongRowAttempts;

        private enum PanelMode { Closed, AlertBoard, Verification, ChainOfCustody }
        private PanelMode mode;
        private GameObject panel;
        private TextMeshProUGUI headerText, bodyText, feedbackText, controlsText;
        private int openedFrame;
        private SocEvidenceRecord pendingEvidence;
        private ModalSession.Lease modal;

        private TextMesh worldHeader, worldBody, worldHint;
        private Renderer screenRenderer;

        public int ScenarioIndex => scenarioIndex;
        public int ScenarioCount => scenarios != null ? scenarios.Length : 0;
        public int CompletedScenarios => IsComplete ? ScenarioCount : scenarioIndex;
        public int FlaggedRowIndex => flaggedRow;
        public int SelectedRowIndex => selectedRow;
        public Level2Content.SocScenario ActiveScenario =>
            scenarios != null && scenarioIndex >= 0 && scenarioIndex < scenarios.Length
                ? scenarios[scenarioIndex] : null;

        public bool CanInteract => !IsComplete && mode == PanelMode.Closed;
        public string Prompt => flaggedRow >= 0 && ActiveScenario != null &&
            ActiveScenario.rows != null && flaggedRow < ActiveScenario.rows.Length
            ? $"Review Alert Board  (flagged {ActiveScenario.rows[flaggedRow].computer})"
            : "Review the active SOC alert";

        public void Configure(Level2Content.SocScenario[] content)
        {
            scenarios = content ?? new Level2Content.SocScenario[0];
            scenarioIndex = Mathf.Clamp(scenarioIndex, 0, Mathf.Max(0, ScenarioCount - 1));
            EnsureInteractionCollider();
            ResolveWorldReferences();
            RefreshWorldDisplay();
            RefreshWorkstations();
        }

        /// <summary>
        /// The saved visual-pass board lost the collider on its ScreenBody.
        /// Its remaining desk collider ends below the player's eye-level ray,
        /// making the board look usable while never producing an E prompt.
        /// Keep a non-blocking target over the visible screen itself so both
        /// saved and runtime-built variants are reliably interactable.
        /// </summary>
        private void EnsureInteractionCollider()
        {
            var target = GetComponent<BoxCollider>();
            if (target == null) target = gameObject.AddComponent<BoxCollider>();
            target.isTrigger = true;
            target.center = new Vector3(0f, 2.5f, 0.15f);
            target.size = new Vector3(4.8f, 2.8f, 0.8f);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract || ActiveScenario == null) return;
            OpenAlertBoard();
        }

        /// <summary>Called by a physical WS-01..WS-04 station.</summary>
        public void Investigate(string computer)
        {
            if (IsComplete || ActiveScenario == null) return;
            if (ActiveScenario.rows == null || flaggedRow < 0)
            {
                Toast("Flag a row on the Alert Board first.", false);
                return;
            }
            if (flaggedRow >= ActiveScenario.rows.Length)
            {
                flaggedRow = -1;
                Toast("The alert board was refreshed. Flag a row again.", false);
                return;
            }

            var flagged = ActiveScenario.rows[flaggedRow];
            if (flagged.computer != computer)
            {
                Toast($"Your flag points to {flagged.computer}, not {computer}.", false);
                return;
            }

            if (flaggedRow != ActiveScenario.triggerRowIndex)
            {
                wrongRowAttempts++;
                string hint = wrongRowAttempts == 1
                    ? "That activity looks routine. Which row does the alert description actually point at?"
                    : $"Second hint: focus on the COMPUTER tied to the suspicious activity — {ActiveScenario.rows[ActiveScenario.triggerRowIndex].computer}.";
                OpenAlertBoard(hint);
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                return;
            }

            OpenVerification();
        }

        void Update()
        {
            if (mode == PanelMode.Closed || Time.frameCount == openedFrame) return;

            if (mode == PanelMode.AlertBoard)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                    GameplayActions.TryApply(this, GameplayAction.Navigate(-1));
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                    GameplayActions.TryApply(this, GameplayAction.Navigate(1));
                else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
                    GameplayActions.TryApply(this, GameplayAction.Submit());
                else if (Input.GetKeyDown(KeyCode.Escape))
                    GameplayActions.TryApply(this, GameplayAction.Cancel());
            }
            else if (mode == PanelMode.Verification)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                    GameplayActions.TryApply(this, GameplayAction.Choose(0));
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                    GameplayActions.TryApply(this, GameplayAction.Choose(1));
                else if (Input.GetKeyDown(KeyCode.Escape))
                    GameplayActions.TryApply(this, GameplayAction.Cancel());
            }
            else if (mode == PanelMode.ChainOfCustody &&
                     (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) ||
                      Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)))
            {
                GameplayActions.TryApply(this, GameplayAction.Submit());
            }
        }

        public bool TryApply(GameplayAction action, GameObject actor)
        {
            if (mode == PanelMode.Closed || ActiveScenario == null) return false;

            if (action.Kind == GameplayActionKind.Cancel)
            {
                ClosePanel();
                return true;
            }

            if (mode == PanelMode.AlertBoard)
            {
                if (action.Kind == GameplayActionKind.Navigate && ActiveScenario.rows.Length > 0)
                {
                    int count = ActiveScenario.rows.Length;
                    int delta = action.Index % count;
                    selectedRow = (selectedRow + delta + count) % count;
                    RenderAlertBoard(feedbackText.text);
                    return true;
                }
                if (action.Kind == GameplayActionKind.Submit)
                {
                    flaggedRow = selectedRow;
                    string computer = ActiveScenario.rows[flaggedRow].computer;
                    ClosePanel();
                    Toast($"ROW FLAGGED — go investigate {computer}.", true);
                    RefreshWorldDisplay();
                    return true;
                }
                return false;
            }

            if (mode == PanelMode.Verification && action.Kind == GameplayActionKind.Choose)
            {
                if (action.Index < 0 || action.Index > 1) return false;
                ResolveVerdict(action.Index == 0
                    ? Level2Content.SocVerdict.MatchBenignPositive
                    : Level2Content.SocVerdict.NoMatchPossibleTruePositive);
                return true;
            }

            if (mode == PanelMode.ChainOfCustody && action.Kind == GameplayActionKind.Submit)
            {
                ConfirmAndCollect();
                return true;
            }

            return false;
        }

        private void OpenAlertBoard(string hint = "")
        {
            EnsurePanel();
            if (!ModalSession.TryOpen(this, ModalSession.Channel.SocInvestigation,
                out modal, releaseCursor: true)) return;
            mode = PanelMode.AlertBoard;
            openedFrame = Time.frameCount;
            panel.SetActive(true);
            RenderAlertBoard(hint);
        }

        private void RenderAlertBoard(string hint)
        {
            var scenario = ActiveScenario;
            headerText.text = $"ALERT BOARD  ·  SCENARIO {scenarioIndex + 1}/{ScenarioCount}\n" +
                              $"<color=#FF8A78>{scenario.title}</color>";

            var sb = new StringBuilder();
            sb.AppendLine(scenario.description);
            sb.AppendLine();
            sb.AppendLine("<color=#5BD9FF><b>      TIME    COMPUTER   USER         ACTIVITY</b></color>");
            for (int i = 0; i < scenario.rows.Length; i++)
            {
                var row = scenario.rows[i];
                // Keep selection markers in the shipped font's ASCII range;
                // triangle/flag glyphs otherwise become missing-glyph boxes.
                string cursor = i == selectedRow ? "<color=#E5A823>></color>" : " ";
                string flag = i == flaggedRow ? "<color=#FF8A78>*</color>" : " ";
                string computer = wrongRowAttempts >= 2 && i == scenario.triggerRowIndex
                    ? $"<color=#E5A823><b>{row.computer}</b></color>" : row.computer;
                sb.AppendLine($"{cursor}{flag}  {row.time,-7} {computer,-10} {row.user,-12} {row.activity}");
            }
            bodyText.text = sb.ToString();
            feedbackText.text = string.IsNullOrEmpty(hint)
                ? "Only one row can be flagged at a time."
                : "<color=#FFB347>" + hint + "</color>";
            controlsText.text = "UP / DOWN SELECT ROW     ·     E / ENTER FLAG & GO INVESTIGATE     ·     ESC CLOSE";
        }

        private void OpenVerification()
        {
            EnsurePanel();
            if (!ModalSession.TryOpen(this, ModalSession.Channel.SocInvestigation,
                out modal, releaseCursor: true)) return;
            mode = PanelMode.Verification;
            openedFrame = Time.frameCount;
            panel.SetActive(true);

            var row = ActiveScenario.rows[flaggedRow];
            var view = ActiveScenario.Workstation(row.computer);
            string[] lines = view != null && view.lines != null && view.lines.Length >= 2
                ? view.lines
                : new[] { "No live workstation details available", "Recheck the flagged computer" };
            headerText.text = "WORKSTATION VERIFICATION  ·  " + row.computer;
            bodyText.text =
                "<color=#5BD9FF><b>FLAGGED ALERT ROW</b></color>\n" +
                $"TIME       {row.time}\nCOMPUTER   {row.computer}\nUSER       {row.user}\nACTIVITY   {row.activity}\n\n" +
                "<color=#5BD9FF><b>LIVE WORKSTATION ACTIVITY</b></color>\n" +
                $"> {lines[0]}\n> {lines[1]}";
            feedbackText.text = "Does the flagged activity match what is visibly running here?";
            controlsText.text = "[1] MATCH — BENIGN POSITIVE     ·     [2] NO MATCH — POSSIBLE TRUE POSITIVE     ·     ESC CLOSE";
        }

        private void ResolveVerdict(Level2Content.SocVerdict verdict)
        {
            if (ActiveScenario == null || flaggedRow != ActiveScenario.triggerRowIndex) return;
            if (verdict != ActiveScenario.correctVerdict)
            {
                feedbackText.text = "<color=#FFB347>Look again — compare the ACTIVITY text against what is on this screen.</color>";
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                return;
            }

            ScoreSystem.Add(pointsPerScenario);
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();

            if (scenarioIndex < ScenarioCount - 1)
            {
                string resolved = ActiveScenario.resolution;
                scenarioIndex++;
                selectedRow = 0;
                flaggedRow = -1;
                wrongRowAttempts = 0;
                ClosePanel();
                RefreshWorldDisplay();
                RefreshWorkstations();
                Toast(resolved + "  Next alert loaded.", true);
                return;
            }

            SocProgress.MarkCompromisedComputer();
            BuildPendingEvidence();
            if (mode == PanelMode.Closed) ConfirmAndCollect();
            else OpenChainOfCustody();
        }

        private void BuildPendingEvidence()
        {
            var row = ActiveScenario.rows[ActiveScenario.triggerRowIndex];
            pendingEvidence = new SocEvidenceRecord
            {
                alertTitle = ActiveScenario.title,
                time = row.time,
                computer = row.computer,
                user = row.user,
                activity = row.activity,
                verificationResult = "machine locked/idle — activity unexplained",
                collectedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"),
                analystName = CustodyLog.SocAnalystName,
                inventoryItem = "Evidence: WS-03 disk image + chain-of-custody record",
            };
        }

        private void OpenChainOfCustody()
        {
            mode = PanelMode.ChainOfCustody;
            openedFrame = Time.frameCount;
            headerText.text = "CHAIN OF CUSTODY FORM  ·  ENTRY 1";
            // Short text-based intro to the form; the lab repeats this same
            // record and has the player write entry 2 (see CustodyLog).
            const string Key = "<color=#5BD9FF><b>";
            const string EndKey = "</b></color>";
            bodyText.text =
                "<color=#E5A823><b>WHAT IS A CHAIN-OF-CUSTODY FORM?</b></color> " +
                "A record of every time evidence changes hands: WHO released it, WHO received it, WHEN, and WHY. " +
                "Every handoff gets its own row, even inside one department, or the evidence may not hold up in court.\n\n" +
                $"<size=88%>{Key}CASE #{EndKey} {CustodyLog.CaseNumber(pendingEvidence)}" +
                $"      {Key}CLIENT ITEM {CustodyLog.ItemNumber}{EndKey} {CustodyLog.ItemDescription(pendingEvidence)}" +
                $"      {Key}SERIAL{EndKey} {CustodyLog.Serial(pendingEvidence)}\n" +
                $"{Key}ALERT{EndKey} {pendingEvidence.alertTitle}  ·  {pendingEvidence.time}  ·  user {pendingEvidence.user}  ·  " +
                $"{pendingEvidence.verificationResult}</size>\n\n" +
                "<b>CHAIN OF CUSTODY</b>\n" +
                "<size=86%><color=#7FA8C8><b>ITEM<pos=7%>DATE / TIME<pos=25%>RELEASED BY<pos=47%>RECEIVED BY<pos=72%>REASON</b></color>\n" +
                // Two lines per entry, like the paper form: name, then role/time.
                $"{CustodyLog.ItemNumber}<pos=7%>{DatePart(pendingEvidence.collectedAtUtc, 0)}<pos=25%>{CustodyLog.ClientName}" +
                $"<pos=47%>{CustodyLog.SocAnalystName}<pos=72%>{CustodyLog.CollectionReason}\n" +
                $"<color=#9FB4C0><pos=7%>{DatePart(pendingEvidence.collectedAtUtc, 1)}<pos=25%>{CustodyLog.ClientRole}" +
                $"<pos=47%>{CustodyLog.SocRole}</color>\n" +
                $"<line-height=150%> </line-height>\n" +
                $"<color=#6F8796>{CustodyLog.ItemNumber}<pos=7%>NEXT ENTRY<pos=25%>Logged by Digital Forensics when the device reaches the lab.</color></size>";
            feedbackText.text = "<color=#E5A823>Entry 1: the client releases the device to SOC analyst L. Torres. Lock it in the evidence locker; Digital Forensics adds entry 2.</color>";
            controlsText.text = "[ E / ENTER ]  CONFIRM & COLLECT";
        }

        /// <summary>"2026-10-05 14:02 UTC" -> part 0 "2026-10-05", part 1 "14:02 UTC".</summary>
        private static string DatePart(string stamp, int part)
        {
            if (string.IsNullOrEmpty(stamp)) return "";
            int split = stamp.IndexOf(' ');
            if (split < 0) return part == 0 ? stamp : "";
            return part == 0 ? stamp.Substring(0, split) : stamp.Substring(split + 1);
        }

        private void ConfirmAndCollect()
        {
            if (pendingEvidence == null)
            {
                ClosePanel();
                Toast("Evidence record unavailable — review the alert again.", false);
                return;
            }
            SocProgress.StoreEvidence(pendingEvidence);
            IsComplete = true;
            ClosePanel();
            RefreshWorldDisplay();
            if (screenRenderer != null)
                screenRenderer.sharedMaterial = BuildKit.MakeHologram(new Color(0.30f, 1f, 0.45f));

            var ws03 = FindWorkstation("WS-03");
            if (ws03 != null) ws03.MarkCollectedAsEvidence();
            EvidenceInventoryPanel.Ensure(gameObject)?.RefreshNow();
            BurstFX.SpawnAbove(ws03 != null ? ws03.transform : transform,
                new Color(0.90f, 0.66f, 0.14f), 36, minimumHeight: 2f);
            Toast("EVIDENCE COLLECTED — deliver it to Digital Forensics.", true);
            Completed?.Invoke();
        }

        private void ClosePanel()
        {
            mode = PanelMode.Closed;
            if (panel != null) panel.SetActive(false);
            modal?.Close();
            modal = null;
        }

        private void OnDestroy()
        {
            modal?.Close();
        }

        private void EnsurePanel()
        {
            if (panel != null) return;
            Transform canvas = HudUI.Instance != null ? HudUI.Instance.Canvas.transform : null;
            panel = new GameObject("SocInvestigationPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1500f, 820f);
            HudUI.StylePanel(panel, new Color(0.015f, 0.025f, 0.05f, 0.98f), Level2SceneFactory.SocRed);

            headerText = MakeText(panel.transform, "Header", 38f, TextAlignmentOptions.TopLeft,
                new Vector2(50f, 650f), new Vector2(-50f, -30f));
            bodyText = MakeText(panel.transform, "Body", 27f, TextAlignmentOptions.TopLeft,
                new Vector2(55f, 130f), new Vector2(-55f, -175f));
            bodyText.font = TMP_Settings.defaultFontAsset;
            feedbackText = MakeText(panel.transform, "Feedback", 25f, TextAlignmentOptions.Center,
                new Vector2(50f, 70f), new Vector2(-50f, -670f));
            controlsText = MakeText(panel.transform, "Controls", 22f, TextAlignmentOptions.Center,
                new Vector2(40f, 22f), new Vector2(-40f, -745f));
            controlsText.color = new Color(0.70f, 0.85f, 1f);
            panel.SetActive(false);
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, float size,
            TextAlignmentOptions alignment, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.richText = true;
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return text;
        }

        private void ResolveWorldReferences()
        {
            if (screenRenderer == null)
            {
                var screen = transform.Find("Screen");
                if (screen != null) screenRenderer = screen.GetComponent<Renderer>();
            }

            // The canonical Level 2 scene has stable label names. Resolve
            // those first so a body label containing "ALERT" cannot be
            // mistaken for the body and leave the actual heading unmapped.
            if (worldHeader == null)
            {
                Transform header = transform.Find("Label_ALERT_BOARD");
                if (header != null) worldHeader = header.GetComponent<TextMesh>();
            }
            if (worldBody == null)
            {
                Transform body = transform.Find("Label_Active_SOC_scenario");
                if (body != null) worldBody = body.GetComponent<TextMesh>();
            }
            if (worldHint == null)
            {
                Transform hint = transform.Find("Label_[E]_REVIEW_&_FLAG");
                if (hint != null) worldHint = hint.GetComponent<TextMesh>();
            }

            // Retain content-based fallbacks for procedural and older scene
            // variants whose labels predate the canonical names above.
            foreach (var label in GetComponentsInChildren<TextMesh>(true))
            {
                if (worldHeader == null && (label.text.Contains("SIEM") || label.text.Contains("SHIFT"))) worldHeader = label;
                else if (worldBody == null && (label.text.Contains("Press E") || label.text.Contains("Triaged") || label.text.Contains("ALERT"))) worldBody = label;
                else if (worldHint == null && label.text.Contains("[")) worldHint = label;
            }
        }

        private void RefreshWorldDisplay()
        {
            if (worldHeader != null) worldHeader.text = IsComplete ? "SOC HANDOFF COMPLETE" : $"ALERT BOARD  {scenarioIndex + 1}/{Mathf.Max(1, ScenarioCount)}";
            if (worldBody != null) worldBody.text = IsComplete ? "WS-03 EVIDENCE COLLECTED" : ActiveScenario != null ? ActiveScenario.title : "NO ACTIVE ALERT";
            if (worldHint != null) worldHint.text = IsComplete ? "DELIVER TO DIGITAL FORENSICS" : flaggedRow >= 0 ? $"FLAGGED: {ActiveScenario.rows[flaggedRow].computer}" : "[E] REVIEW & FLAG";
        }

        private void RefreshWorkstations()
        {
            if (ActiveScenario == null) return;
            foreach (var station in FindObjectsOfType<EndpointStation>())
            {
                if (station.def == null) continue;
                var view = ActiveScenario.Workstation(station.def.hostname);
                if (view != null) station.SetSocActivity(view.lines);
            }
        }

        private EndpointStation FindWorkstation(string computer)
        {
            foreach (var station in FindObjectsOfType<EndpointStation>())
                if (station.def != null && station.def.hostname == computer) return station;
            return null;
        }

        private static void Toast(string message, bool positive)
        {
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast(message, positive
                    ? new Color(0.30f, 1f, 0.45f)
                    : new Color(1f, 0.55f, 0.4f));
        }

        // ---- Construction --------------------------------------------------

        public static SiemConsole Build(Vector3 pos, float rotY,
            Level2Content.SocScenario[] content, Color accent)
        {
            var root = new GameObject("SiemConsole");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Desk", root.transform,
                new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(3.0f, 1.0f, 1.0f), bodyMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "ScreenBody", root.transform,
                new Vector3(0f, 2.5f, 0.2f), Vector3.zero, new Vector3(4.5f, 2.4f, 0.1f), bodyMat, collider: true);
            var screen = BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", root.transform,
                new Vector3(0f, 2.5f, 0.14f), Vector3.zero, new Vector3(4.35f, 2.25f, 1f),
                BuildKit.MakeHologram(accent), collider: false);

            var console = root.AddComponent<SiemConsole>();
            console.screenRenderer = screen.GetComponent<Renderer>();
            console.worldHeader = BuildKit.MakeLabel(root.transform, new Vector3(0f, 3.25f, 0.1f),
                "ALERT BOARD", accent, 0.024f);
            console.worldBody = BuildKit.MakeLabel(root.transform, new Vector3(0f, 2.52f, 0.1f),
                "Active SOC scenario", Color.white, 0.025f);
            console.worldHint = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.85f, 0.1f),
                "[E] REVIEW & FLAG", new Color(0.60f, 0.72f, 0.85f), 0.020f);
            console.Configure(content);
            return console;
        }
    }
}
