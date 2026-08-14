using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>Data-driven SOC alert board. The player flags one of four
    /// event rows, walks to that computer, compares the alert with its live
    /// activity, and chooses a benign/possible-true-positive verdict.</summary>
    public class SiemConsole : MonoBehaviour, IInteractable
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

        private TextMesh worldHeader, worldBody, worldHint;
        private Renderer screenRenderer;

        public int ScenarioIndex => scenarioIndex;
        public int ScenarioCount => scenarios != null ? scenarios.Length : 0;
        public int CompletedScenarios => IsComplete ? ScenarioCount : scenarioIndex;
        public int FlaggedRowIndex => flaggedRow;
        public Level2Content.SocScenario ActiveScenario =>
            scenarios != null && scenarioIndex >= 0 && scenarioIndex < scenarios.Length
                ? scenarios[scenarioIndex] : null;

        public bool CanInteract => !IsComplete && mode == PanelMode.Closed;
        public string Prompt => flaggedRow >= 0 && ActiveScenario != null
            ? $"Review Alert Board  (flagged {ActiveScenario.rows[flaggedRow].computer})"
            : "Review the active SOC alert";

        public void Configure(Level2Content.SocScenario[] content)
        {
            scenarios = content;
            scenarioIndex = Mathf.Clamp(scenarioIndex, 0, Mathf.Max(0, ScenarioCount - 1));
            ResolveWorldReferences();
            RefreshWorldDisplay();
            RefreshWorkstations();
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
            if (flaggedRow < 0)
            {
                Toast("Flag a row on the Alert Board first.", false);
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
                {
                    selectedRow = (selectedRow + ActiveScenario.rows.Length - 1) % ActiveScenario.rows.Length;
                    RenderAlertBoard(feedbackText.text);
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    selectedRow = (selectedRow + 1) % ActiveScenario.rows.Length;
                    RenderAlertBoard(feedbackText.text);
                }
                else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
                {
                    flaggedRow = selectedRow;
                    string computer = ActiveScenario.rows[flaggedRow].computer;
                    ClosePanel();
                    Toast($"ROW FLAGGED — go investigate {computer}.", true);
                    RefreshWorldDisplay();
                }
                else if (Input.GetKeyDown(KeyCode.Escape)) ClosePanel();
            }
            else if (mode == PanelMode.Verification)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                    ResolveVerdict(Level2Content.SocVerdict.MatchBenignPositive);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                    ResolveVerdict(Level2Content.SocVerdict.NoMatchPossibleTruePositive);
                else if (Input.GetKeyDown(KeyCode.Escape)) ClosePanel();
            }
            else if (mode == PanelMode.ChainOfCustody &&
                     (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) ||
                      Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)))
            {
                ConfirmAndCollect();
            }
        }

        private void OpenAlertBoard(string hint = "")
        {
            EnsurePanel();
            mode = PanelMode.AlertBoard;
            openedFrame = Time.frameCount;
            GameState.SocInvestigationOpen = true;
            FirstPersonController.LockCursor(false);
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
                string cursor = i == selectedRow ? "<color=#E5A823>▶</color>" : " ";
                string flag = i == flaggedRow ? "<color=#FF8A78>⚑</color>" : " ";
                string computer = wrongRowAttempts >= 2 && i == scenario.triggerRowIndex
                    ? $"<color=#E5A823><b>{row.computer}</b></color>" : row.computer;
                sb.AppendLine($"{cursor}{flag}  {row.time,-7} {computer,-10} {row.user,-12} {row.activity}");
            }
            bodyText.text = sb.ToString();
            feedbackText.text = string.IsNullOrEmpty(hint)
                ? "Only one row can be flagged at a time."
                : "<color=#FFB347>" + hint + "</color>";
            controlsText.text = "↑ / ↓ SELECT ROW     ·     E / ENTER FLAG & GO INVESTIGATE     ·     ESC CLOSE";
        }

        private void OpenVerification()
        {
            EnsurePanel();
            mode = PanelMode.Verification;
            openedFrame = Time.frameCount;
            GameState.SocInvestigationOpen = true;
            FirstPersonController.LockCursor(false);
            panel.SetActive(true);

            var row = ActiveScenario.rows[flaggedRow];
            var view = ActiveScenario.Workstation(row.computer);
            headerText.text = "WORKSTATION VERIFICATION  ·  " + row.computer;
            bodyText.text =
                "<color=#5BD9FF><b>FLAGGED ALERT ROW</b></color>\n" +
                $"TIME       {row.time}\nCOMPUTER   {row.computer}\nUSER       {row.user}\nACTIVITY   {row.activity}\n\n" +
                "<color=#5BD9FF><b>LIVE WORKSTATION ACTIVITY</b></color>\n" +
                $"> {view.lines[0]}\n> {view.lines[1]}";
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
                analystName = PlayerPrefs.GetString("cv_analyst_name", "SOC Analyst"),
                inventoryItem = "Evidence: WS-03 disk image + chain-of-custody record",
            };
        }

        private void OpenChainOfCustody()
        {
            mode = PanelMode.ChainOfCustody;
            openedFrame = Time.frameCount;
            headerText.text = "CHAIN OF CUSTODY  ·  AUTO-GENERATED";
            bodyText.text =
                $"<color=#5BD9FF><b>ALERT</b></color>        {pendingEvidence.alertTitle}\n" +
                $"<color=#5BD9FF><b>TIME</b></color>         {pendingEvidence.time}\n" +
                $"<color=#5BD9FF><b>COMPUTER</b></color>     {pendingEvidence.computer}\n" +
                $"<color=#5BD9FF><b>USER</b></color>         {pendingEvidence.user}\n" +
                $"<color=#5BD9FF><b>ACTIVITY</b></color>     {pendingEvidence.activity}\n" +
                $"<color=#5BD9FF><b>VERIFICATION</b></color> {pendingEvidence.verificationResult}\n" +
                $"<color=#5BD9FF><b>COLLECTED</b></color>    {pendingEvidence.collectedAtUtc}\n" +
                $"<color=#5BD9FF><b>ANALYST</b></color>      {pendingEvidence.analystName}";
            feedbackText.text = "<color=#E5A823>WS-03 will be powered down and collected as evidence.</color>";
            controlsText.text = "[ E / ENTER ]  CONFIRM & COLLECT";
        }

        private void ConfirmAndCollect()
        {
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
            GameState.SocInvestigationOpen = false;
            if (panel != null) panel.SetActive(false);
            FirstPersonController.LockCursor(true);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Runs the same scenario resolution/state transitions used by
        /// gameplay while leaving spatial travel to CampaignTasPlayback.</summary>
        public void CompleteForAutomation()
        {
            if (IsComplete || scenarios == null) return;
            while (!IsComplete)
            {
                flaggedRow = ActiveScenario.triggerRowIndex;
                ResolveVerdict(ActiveScenario.correctVerdict);
                if (mode == PanelMode.ChainOfCustody) ConfirmAndCollect();
            }
        }
#endif

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
