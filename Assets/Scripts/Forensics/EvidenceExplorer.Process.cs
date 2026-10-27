using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Interaction;

namespace Cyverse.Forensics
{
    /// <summary>
    /// The Process side of the desk, the step before Examine. It follows the
    /// AXIOM Process order: case details, evidence source, acquisition method,
    /// processing options, then the processing run. Steps 1 to 3 are picks with
    /// one right answer. A wrong pick explains itself and costs nothing.
    /// Step 4 switches the optional passes on or off; each one adds badges
    /// in Examine. The run verifies the image hash and then opens Examine.
    ///
    /// Actions while processing: Choose(i) picks or toggles tile i · Submit
    /// starts the run, skips it, and then opens Examine · Cancel steps away
    /// (the progress is kept).
    /// </summary>
    public sealed partial class EvidenceExplorer
    {
        private const int StepCaseDetails = 0, StepSource = 1, StepMethod = 2, StepOptions = 3, StepRun = 4;
        private const int TileCount = 4;
        private static readonly string[] StepNames =
            { "CASE DETAILS", "EVIDENCE SOURCE", "ACQUISITION", "PROCESSING OPTIONS", "PROCESS" };

        private sealed class PickStep
        {
            public string question;
            public string[] tiles;
            public string[] blurbs;  // one line under each tile's name
            public string[] notes;   // why each tile is right or wrong
            public string summary;   // the case-summary label this step fills in
            public int correct;
        }

        private static readonly PickStep[] Picks =
        {
            new PickStep
            {
                question = "What kind of case is this? Check the SOC alert that sent the phone here.",
                tiles = new[] { "PHISHING / MALWARE", "HR COMPLAINT", "LOST DEVICE", "BILLING FRAUD" },
                blurbs = new[]
                {
                    "Scam links, bad apps, strange network traffic",
                    "A coworker reported workplace conduct",
                    "A phone that was found or turned in",
                    "Fake invoices or changed payment details",
                },
                summary = "CASE TYPE",
                notes = new[]
                {
                    "Phishing / malware: the SOC flagged a scam link and strange app traffic.",
                    "No one filed a complaint. The SOC alert was a scam link and malware traffic.",
                    "The phone wasn't lost; it was handed over with a custody form.",
                    "Nothing here is about billing. Re-read the SOC alert: a scam link, then malware traffic.",
                },
                correct = 0,
            },
            new PickStep
            {
                question = "What's in the cradle? The tool has to know the platform to read it.",
                tiles = new[] { "COMPUTER", "ANDROID PHONE", "IPHONE", "SIM CARD ONLY" },
                blurbs = new[]
                {
                    "A laptop or desktop drive",
                    "A phone running Google Android",
                    "A phone running Apple iOS",
                    "Just the SIM: contacts and the number",
                },
                summary = "PLATFORM",
                notes = new[]
                {
                    "That's a phone in the cradle, not a computer.",
                    "Android: the docked work phone. The tool now knows where each app keeps its data.",
                    "Look again: the work phone is an Android. iPhones store data differently.",
                    "A SIM only holds contacts and the number. The data we need is on the phone itself.",
                },
                correct = 1,
            },
            new PickStep
            {
                question = "How much should we copy? Pick the acquisition that keeps what this case needs.",
                tiles = new[] { "QUICK (LOGICAL)", "FULL FILE SYSTEM", "LOCK BYPASS", null },
                blurbs = new[]
                {
                    "Texts, email, apps and web history. Fast.",
                    "Everything: app databases, deleted records, photo metadata. Slower.",
                    "Breaks into a locked phone. Needs legal authority.",
                    null,
                },
                summary = "ACQUISITION",
                notes = new[]
                {
                    "Quick is fast, but it skips chat databases, deleted chats and photo GPS. This case needs them.",
                    "Full file system: slower, but nothing is left behind, not even deleted records.",
                    "Not needed and not allowed here: the owner gave the passcode, and a bypass needs legal authority.",
                    null,
                },
                correct = 1,
            },
        };

        private static readonly string[] OptionNames =
            { "KEYWORD SEARCH", "HASH MATCH", "TEXT IN PHOTOS (OCR)", "AI TRIAGE" };
        private static readonly string[] OptionNotes =
        {
            "Searches for the SOC alert terms (spartan-rewards, " + PhoneImage.AttackerIp + ") and badges every hit.",
            "Compares every file against a list of known-bad hashes.",
            "Reads the words in photos so the filter can find them.",
            "AI flags strange chats and photos. Flags are leads, not proof. Check them.",
        };

        private int processStep;
        private bool runDone;
        private float runClock;
        private string[] runStages;

        private const string CaseNumber = "CY-2026-0714";

        private GameObject processRoot;
        private TMP_Text stepHeader, stepQuestion, processFeedback, runText, runPercent, evidenceCard;
        private RectTransform runFill;
        private Button nextButton;
        private readonly Button[] tiles = new Button[TileCount];
        private readonly TMP_Text[] tileKeys = new TMP_Text[TileCount];
        private readonly TMP_Text[] tileBlurbs = new TMP_Text[TileCount];
        private readonly TMP_Text[] tileStates = new TMP_Text[TileCount];
        private readonly Image[] tileStripes = new Image[TileCount];
        private readonly Image[] railRows = new Image[StepNames.Length];
        private readonly Image[] railMarks = new Image[StepNames.Length];
        private readonly Image[] railBadges = new Image[StepNames.Length];
        private readonly TMP_Text[] railNumbers = new TMP_Text[StepNames.Length];
        private readonly TMP_Text[] railNames = new TMP_Text[StepNames.Length];
        private readonly TMP_Text[] railStates = new TMP_Text[StepNames.Length];
        private readonly Image[] progressSegments = new Image[StepNames.Length];
        private readonly TMP_Text[] summaryValues = new TMP_Text[4];

        public bool InProcess => open && processRoot != null && processRoot.activeSelf;
        public int ProcessStep => processStep;
        public bool ProcessRunDone => runDone;
        /// <summary>The right tile on a pick step; -1 on the options and run steps.</summary>
        public int ProcessCorrectChoice => processStep < Picks.Length ? Picks[processStep].correct : -1;
        public string ProcessFeedback => processFeedback != null ? processFeedback.text : "";

        private void BeginProcess()
        {
            examineRoot.SetActive(false);
            processRoot.SetActive(true);
            titleText.text = $"PROCESS EVIDENCE  //  work phone of {Escape(phone.owner)}";
            if (processStep > StepRun) processStep = 0;
            SetProcessFeedback(processStep == 0
                ? "Set up the case before you touch any data. Start with the case type."
                : "Picking up where you left off.", Ink);
            RefreshProcess();
        }

        private bool TryApplyProcess(GameplayAction action)
        {
            switch (action.Kind)
            {
                case GameplayActionKind.Choose:
                    return ChooseTile(action.Index);
                case GameplayActionKind.Submit:
                    return ProcessNext();
                case GameplayActionKind.Cancel:
                    Close();
                    return true;
                default:
                    return false;
            }
        }

        private bool ChooseTile(int index)
        {
            if (index < 0 || index >= TileCount) return false;
            if (processStep < Picks.Length)
            {
                PickStep pick = Picks[processStep];
                if (pick.tiles[index] == null) return false;
                if (index == pick.correct)
                {
                    if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                    SetProcessFeedback($"<color=#4CE087><b>OK</b></color>  {pick.notes[index]}", Ink);
                    processStep++;
                    if (processStep == StepOptions) phone.KeywordPass = phone.HashPass = phone.OcrPass = phone.AiPass = false;
                }
                else
                {
                    if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                    SetProcessFeedback($"<color=#FF8866><b>Not that one.</b></color>  {pick.notes[index]}", Ink);
                }
                RefreshProcess();
                return true;
            }
            if (processStep == StepOptions)
            {
                switch (index)
                {
                    case 0: phone.KeywordPass = !phone.KeywordPass; break;
                    case 1: phone.HashPass = !phone.HashPass; break;
                    case 2: phone.OcrPass = !phone.OcrPass; break;
                    case 3: phone.AiPass = !phone.AiPass; break;
                }
                if (Sfx.Instance != null) Sfx.Instance.PlayClick();
                SetProcessFeedback(OptionNotes[index], Ink);
                RefreshProcess();
                return true;
            }
            return false;
        }

        private bool ProcessNext()
        {
            if (processStep < Picks.Length)
            {
                SetProcessFeedback("Pick one of the options above first.", Gold);
                return true;
            }
            if (processStep == StepOptions)
            {
                processStep = StepRun;
                runDone = false;
                runClock = 0f;
                runStages = BuildStages();
                SetProcessFeedback("Processing. Leave the phone in the cradle.", Ink);
                RefreshProcess();
                return true;
            }
            if (!runDone)
            {
                // Enter skips the animation. Jump to the end exactly: a huge
                // clock would overflow the int stage count and never finish.
                runClock = SecondsPerStage * runStages.Length;
                TickRun(0f);
                return true;
            }
            phone.Processed = true;
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            ShowExamine();
            return true;
        }

        private string[] BuildStages()
        {
            var stages = new System.Collections.Generic.List<string>
            {
                "Connecting to the phone", "Copying the full file system", "Verifying the image hash",
                "Parsing app databases", "Carving deleted records",
            };
            if (phone.KeywordPass) stages.Add("Searching for keywords");
            if (phone.HashPass) stages.Add("Matching file hashes");
            if (phone.OcrPass) stages.Add("Reading text in photos");
            if (phone.AiPass) stages.Add("Running AI triage");
            return stages.ToArray();
        }

        private const float SecondsPerStage = 0.55f;

        private void UpdateProcess()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                GameplayActions.TryApply(this, GameplayAction.Submit());
            for (int i = 0; i < TileCount; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    GameplayActions.TryApply(this, GameplayAction.Choose(i));
            if (processStep == StepRun && !runDone) TickRun(Time.unscaledDeltaTime);
        }

        private void TickRun(float dt)
        {
            runClock += dt;
            int total = runStages.Length;
            int doneCount = runClock >= SecondsPerStage * total
                ? total
                : Mathf.Clamp(Mathf.FloorToInt(runClock / SecondsPerStage), 0, total);
            if (doneCount >= total && !runDone)
            {
                runDone = true;
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                SetProcessFeedback("<color=#4CE087><b>PROCESSING COMPLETE.</b></color>  The image hash matches " +
                    "the one on the custody form, so this copy is exactly what came off the phone.", Ink);
            }
            float fraction = runDone ? 1f : Mathf.Clamp01(runClock / (SecondsPerStage * total));
            runFill.anchorMax = new Vector2(fraction, 1f);
            runPercent.text = runDone ? "<color=#4CE087><b>100%  COMPLETE</b></color>"
                : $"<b>{Mathf.FloorToInt(fraction * 100f)}%</b>  <color=#8FB8CC>{runStages[Mathf.Min(doneCount, total - 1)].ToUpperInvariant()}</color>";

            var sb = new StringBuilder();
            for (int i = 0; i < total; i++)
            {
                if (i < doneCount) sb.Append("<color=#4CE087><b>DONE</b></color><pos=12%>");
                else if (i == doneCount) sb.Append("<color=#E5A823><b>RUNNING</b></color><pos=12%>");
                else sb.Append("<color=#607585>QUEUED<pos=12%>");
                sb.Append(runStages[i]);
                if (i > doneCount) sb.Append("</color>");
                sb.Append('\n');
            }
            if (runDone)
                sb.Append($"<b>{phone.artifacts.Count} artifacts</b> recovered" + PassSummary());
            runText.text = sb.ToString();
            nextButton.GetComponentInChildren<TMP_Text>().text = runDone
                ? "<b>OPEN IN EXPLORER</b>   <size=15><color=#9FD8B4>ENTER</color></size>"
                : "<b>SKIP</b>   <size=15><color=#9FD8B4>ENTER</color></size>";
        }

        private void RefreshProcess()
        {
            bool run = processStep == StepRun;
            for (int i = 0; i < StepNames.Length; i++)
            {
                bool done = i < processStep || (i == StepRun && runDone);
                bool current = i == processStep && !done;
                railRows[i].color = current ? new Color(0.07f, 0.16f, 0.18f, 1f) : CategoryIdle;
                railMarks[i].enabled = current;
                railBadges[i].color = done ? new Color(0.10f, 0.42f, 0.22f) : current ? Gold : new Color(0.10f, 0.16f, 0.19f);
                railNumbers[i].text = done ? "OK" : (i + 1).ToString();
                railNumbers[i].color = current ? new Color(0.05f, 0.05f, 0.03f) : done ? Green : Muted;
                railNames[i].color = current ? Color.white : done ? new Color(0.78f, 0.88f, 0.92f) : new Color(0.42f, 0.52f, 0.58f);
                railStates[i].text = done ? "<color=#4CE087>DONE</color>" + (i < Picks.Length ? "  ·  " + Answer(i) : "")
                    : current ? "<color=#E5A823>IN PROGRESS</color>" : "PENDING";
                progressSegments[i].color = done ? Green : current ? Gold : new Color(0.12f, 0.19f, 0.22f);
            }
            evidenceCard.text =
                "<size=15><color=#607585><b><cspace=3>EVIDENCE ITEM</cspace></b></color></size>\n" +
                $"<b>#1  Work phone</b>\n<color=#8FB8CC>Owner</color><indent=38%>{Escape(phone.owner)}</indent>\n" +
                $"<color=#8FB8CC>Case</color><indent=38%>{CaseNumber}</indent>\n" +
                "<color=#8FB8CC>Examiner</color><indent=38%>You</indent>\n" +
                "<color=#8FB8CC>Custody</color><indent=38%><color=#4CE087>Form signed</color></indent>";

            stepHeader.text = $"STEP {processStep + 1} OF {StepNames.Length}   <color=#607585>//</color>   {StepNames[processStep]}";
            runText.transform.parent.gameObject.SetActive(run);

            if (processStep < Picks.Length) stepQuestion.text = Picks[processStep].question;
            else if (processStep == StepOptions)
                stepQuestion.text = "Pick the extra passes to run. Each one adds clues in Examine; none is required.";
            else stepQuestion.text = runDone
                ? "The image is ready. Open it in the Evidence Explorer to examine it."
                : "Processing the image. The tool copies the phone, checks the copy, then runs your passes.";

            for (int i = 0; i < Picks.Length; i++)
                summaryValues[i].text = i < processStep
                    ? $"<color=#FFFFFF>{Answer(i)}</color>"
                    : i == processStep ? "<color=#E5A823>choosing...</color>" : "<color=#607585>not set</color>";
            summaryValues[3].text = processStep > StepOptions ? $"<color=#FFFFFF>{PassList()}</color>"
                : processStep == StepOptions ? "<color=#E5A823>choosing...</color>" : "<color=#607585>not set</color>";

            for (int i = 0; i < TileCount; i++)
            {
                Button tile = tiles[i];
                string label = processStep < Picks.Length ? Picks[processStep].tiles[i]
                    : processStep == StepOptions ? OptionNames[i] : null;
                tile.gameObject.SetActive(label != null && !run);
                if (label == null) continue;
                bool options = processStep == StepOptions;
                bool on = options && OptionOn(i);
                tile.GetComponentInChildren<TMP_Text>().text = label;
                tileKeys[i].text = (i + 1).ToString();
                tileBlurbs[i].text = options ? OptionNotes[i] : Picks[processStep].blurbs[i];
                tileStates[i].text = !options ? ""
                    : on ? "<color=#4CE087><b>ON</b></color>" : "<color=#607585><b>OFF</b></color>";
                tileStripes[i].color = on ? Green : Line;
                tile.GetComponent<Image>().color = on ? new Color(0.06f, 0.24f, 0.15f) : TileIdle;
            }

            nextButton.gameObject.SetActive(processStep >= StepOptions);
            if (processStep == StepOptions)
                nextButton.GetComponentInChildren<TMP_Text>().text =
                    "<b>START PROCESSING</b>   <size=15><color=#9FD8B4>ENTER</color></size>";
            if (run) TickRun(0f);
        }

        /// <summary>The right pick on setup step <paramref name="i"/>, in
        /// sentence case for the summary and the step rail.</summary>
        private static string Answer(int i)
        {
            string s = Picks[i].tiles[Picks[i].correct].ToLowerInvariant();
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private string PassList()
        {
            var on = new System.Collections.Generic.List<string>();
            if (phone.KeywordPass) on.Add("Keyword");
            if (phone.HashPass) on.Add("Hash");
            if (phone.OcrPass) on.Add("OCR");
            if (phone.AiPass) on.Add("AI");
            return on.Count == 0 ? "None" : string.Join(", ", on);
        }

        private bool OptionOn(int i) =>
            i == 0 ? phone.KeywordPass : i == 1 ? phone.HashPass : i == 2 ? phone.OcrPass : phone.AiPass;

        private void SetProcessFeedback(string text, Color color)
        {
            if (processFeedback == null) return;
            processFeedback.text = text;
            processFeedback.color = color;
        }

        // ---- Examine-side helpers for what the passes found -------------------

        private string PassSummary()
        {
            if (phone == null) return "";
            int hits = 0, bad = 0, ai = 0;
            foreach (PhoneArtifact a in phone.artifacts)
            {
                if (phone.IsKeywordHit(a)) hits++;
                if (phone.HashPass && a.KnownBad != null) bad++;
                if (phone.AiPass && a.AiFlag != null) ai++;
            }
            var sb = new StringBuilder();
            if (phone.KeywordPass) sb.Append($"  ·  <color=#5BC8FF>{hits} keyword hits</color>");
            if (phone.HashPass) sb.Append($"  ·  <color=#FF6B6B>{bad} known bad</color>");
            if (phone.AiPass) sb.Append($"  ·  <color=#C59BFF>{ai} AI flags</color>");
            return sb.ToString();
        }

        private string Badges(PhoneArtifact a)
        {
            var sb = new StringBuilder();
            if (phone.HashPass && a.KnownBad != null) sb.Append("<color=#FF6B6B>[BAD]</color> ");
            if (phone.IsKeywordHit(a)) sb.Append("<color=#5BC8FF>[HIT]</color> ");
            if (phone.AiPass && a.AiFlag != null) sb.Append("<color=#C59BFF>[AI]</color> ");
            return sb.ToString();
        }

        private void AppendPassFindings(StringBuilder sb, PhoneArtifact a)
        {
            if (phone.HashPass && a.KnownBad != null)
                sb.Append("<color=#FF6B6B>KNOWN BAD:</color>  ").Append(Escape(a.KnownBad)).Append('\n');
            if (phone.IsKeywordHit(a))
                sb.Append("<color=#5BC8FF>KEYWORD HIT:</color>  matches a term from the SOC alert\n");
            if (!string.IsNullOrEmpty(a.PhotoText))
                sb.Append("<color=#5BC8FF>TEXT IN PHOTO:</color>  ")
                  .Append(phone.OcrPass ? Escape(a.PhotoText) : "<color=#607585>not read (OCR was off)</color>")
                  .Append('\n');
            if (phone.AiPass && a.AiFlag != null)
                sb.Append("<color=#C59BFF>AI FLAG (verify):</color>  ").Append(Escape(a.AiFlag)).Append('\n');
        }

        // ---- Construction ------------------------------------------------------

        private static readonly Color TileIdle = new Color(0.04f, 0.085f, 0.10f, 1f);

        private void BuildProcess(Transform cardRoot)
        {
            processRoot = new GameObject("Process", typeof(RectTransform));
            processRoot.transform.SetParent(cardRoot, false);
            Place(processRoot.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f, 0f);
            Transform root = processRoot.transform;

            // Left rail: the five setup steps, then the evidence item.
            const float railX0 = 0.012f, railX1 = 0.245f, railTop = 0.925f;
            Band(root, "StepRail", Pane, railX0, ColumnsBottom, railX1, railTop, true);
            PaneTitle(root, "StepRailHeader", railX0, railTop - PaneHeaderH, railX1, railTop).text = "CASE SETUP";
            for (int i = 0; i < StepNames.Length; i++)
            {
                float top = railTop - PaneHeaderH - 0.012f - i * 0.083f;
                Image row = Band(root, "Step_" + i, CategoryIdle, railX0 + 0.006f, top - 0.075f, railX1 - 0.006f, top, false);
                railRows[i] = row;
                railMarks[i] = Edge(row.transform, Gold, 5f);

                Image badgeBox = Band(row.transform, "Badge", Line, 0f, 0f, 0f, 0f, false);
                RectTransform brt = badgeBox.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
                brt.pivot = new Vector2(0f, 0.5f);
                brt.sizeDelta = new Vector2(40f, 40f);
                brt.anchoredPosition = new Vector2(18f, 0f);
                railBadges[i] = badgeBox;
                railNumbers[i] = MakeText(badgeBox.transform, "Number", 18, TextAlignmentOptions.Center);
                railNumbers[i].fontStyle = FontStyles.Bold;
                railNumbers[i].enableWordWrapping = false;
                Place(railNumbers[i].rectTransform, 0f, 0f, 1f, 1f, 0f);

                railNames[i] = MakeText(row.transform, "Name", 19, TextAlignmentOptions.BottomLeft);
                railNames[i].text = StepNames[i];
                railNames[i].fontStyle = FontStyles.Bold;
                railNames[i].enableWordWrapping = false;
                railNames[i].overflowMode = TextOverflowModes.Ellipsis;
                Place(railNames[i].rectTransform, 0f, 0.5f, 1f, 0.92f, 0f);
                Inset(railNames[i].rectTransform, 72f, 8f);

                railStates[i] = MakeText(row.transform, "State", 15, TextAlignmentOptions.TopLeft);
                railStates[i].color = new Color(0.42f, 0.52f, 0.58f);
                railStates[i].enableWordWrapping = false;
                railStates[i].overflowMode = TextOverflowModes.Ellipsis;
                Place(railStates[i].rectTransform, 0f, 0.08f, 1f, 0.48f, 0f);
                Inset(railStates[i].rectTransform, 72f, 8f);
            }
            TMP_Text then = MakeText(root, "Then", 15, TextAlignmentOptions.MidlineLeft);
            then.text = "<cspace=3><b>NEXT  >  EXAMINE</b></cspace>";
            then.color = new Color(0.42f, 0.52f, 0.58f);
            float thenTop = railTop - PaneHeaderH - 0.012f - StepNames.Length * 0.083f;
            Place(then.rectTransform, railX0, thenTop - 0.04f, railX1, thenTop, 0f);
            Inset(then.rectTransform, 22f, 8f);

            Image evidenceBox = Band(root, "EvidenceCard", new Color(0.035f, 0.07f, 0.085f, 1f),
                railX0 + 0.006f, ColumnsBottom + 0.008f, railX1 - 0.006f, thenTop - 0.05f, false);
            Edge(evidenceBox.transform, Cyan, 4f);
            evidenceCard = MakeText(evidenceBox.transform, "Evidence", 18, TextAlignmentOptions.TopLeft);
            evidenceCard.lineSpacing = 8f;
            Place(evidenceCard.rectTransform, 0f, 0f, 1f, 1f, 12f);
            Inset(evidenceCard.rectTransform, 20f, 12f);

            // Main area: progress, the step, a running case summary, the tiles.
            const float mainX0 = 0.262f, mainX1 = 0.988f, gap = 0.006f;
            float segment = (mainX1 - mainX0 - gap * (StepNames.Length - 1)) / StepNames.Length;
            for (int i = 0; i < StepNames.Length; i++)
            {
                float x = mainX0 + i * (segment + gap);
                progressSegments[i] = Band(root, "Progress_" + i, Line, x, 0.912f, x + segment, 0.92f, false);
            }

            stepHeader = MakeText(root, "StepHeader", 18, TextAlignmentOptions.MidlineLeft);
            stepHeader.fontStyle = FontStyles.Bold;
            stepHeader.characterSpacing = 4f;
            stepHeader.color = Cyan;
            stepHeader.enableWordWrapping = false;
            Place(stepHeader.rectTransform, mainX0, 0.86f, mainX1, 0.905f, 0f);

            stepQuestion = MakeText(root, "StepQuestion", 31, TextAlignmentOptions.TopLeft);
            stepQuestion.fontStyle = FontStyles.Bold;
            stepQuestion.color = Color.white;
            Place(stepQuestion.rectTransform, mainX0, 0.735f, mainX1, 0.86f, 0f);

            Band(root, "CaseSummary", Pane, mainX0, 0.635f, mainX1, 0.725f, true);
            string[] summaryLabels = { Picks[0].summary, Picks[1].summary, Picks[2].summary, "EXTRA PASSES" };
            float cell = (mainX1 - mainX0) / summaryLabels.Length;
            for (int i = 0; i < summaryLabels.Length; i++)
            {
                float x = mainX0 + i * cell;
                if (i > 0) Band(root, "SummaryRule_" + i, Line, x, 0.648f, x + 0.0012f, 0.712f, false);
                TMP_Text label = MakeText(root, "SummaryLabel_" + i, 14, TextAlignmentOptions.BottomLeft);
                label.text = $"<b><cspace=3>{summaryLabels[i]}</cspace></b>";
                label.color = new Color(0.42f, 0.52f, 0.58f);
                label.enableWordWrapping = false;
                Place(label.rectTransform, x, 0.682f, x + cell, 0.718f, 0f);
                Inset(label.rectTransform, 20f, 8f);
                summaryValues[i] = MakeText(root, "SummaryValue_" + i, 20, TextAlignmentOptions.TopLeft);
                summaryValues[i].fontStyle = FontStyles.Bold;
                summaryValues[i].enableWordWrapping = false;
                summaryValues[i].overflowMode = TextOverflowModes.Ellipsis;
                Place(summaryValues[i].rectTransform, x, 0.64f, x + cell, 0.68f, 0f);
                Inset(summaryValues[i].rectTransform, 20f, 8f);
            }

            float tileW = (mainX1 - mainX0 - 0.012f * (TileCount - 1)) / TileCount;
            for (int i = 0; i < TileCount; i++)
            {
                int captured = i;
                Button tile = MakeButton("Tile_" + i, root, "", TileIdle);
                float x = mainX0 + i * (tileW + 0.012f);
                Place(tile.GetComponent<RectTransform>(), x, 0.225f, x + tileW, 0.612f, 0f);
                AddBorder(tile.gameObject);

                TMP_Text label = tile.GetComponentInChildren<TMP_Text>();
                label.alignment = TextAlignmentOptions.BottomLeft;
                label.enableWordWrapping = true;
                label.fontSize = 25;
                label.fontStyle = FontStyles.Bold;
                Place(label.rectTransform, 0f, 0.42f, 1f, 0.72f, 0f);
                Inset(label.rectTransform, 22f, 18f);

                Image stripe = Band(tile.transform, "Stripe", Line, 0f, 0.985f, 1f, 1f, false);
                tileStripes[i] = stripe;

                Image keyBox = Band(tile.transform, "Key", new Color(0.09f, 0.15f, 0.18f), 0f, 0f, 0f, 0f, true);
                RectTransform krt = keyBox.rectTransform;
                krt.anchorMin = krt.anchorMax = new Vector2(0f, 1f);
                krt.pivot = new Vector2(0f, 1f);
                krt.sizeDelta = new Vector2(38f, 38f);
                krt.anchoredPosition = new Vector2(22f, -22f);
                tileKeys[i] = MakeText(keyBox.transform, "Number", 19, TextAlignmentOptions.Center);
                tileKeys[i].fontStyle = FontStyles.Bold;
                tileKeys[i].color = Gold;
                Place(tileKeys[i].rectTransform, 0f, 0f, 1f, 1f, 0f);

                tileStates[i] = MakeText(tile.transform, "State", 18, TextAlignmentOptions.TopRight);
                tileStates[i].enableWordWrapping = false;
                tileStates[i].characterSpacing = 3f;
                Place(tileStates[i].rectTransform, 0.5f, 0.8f, 1f, 0.95f, 0f);
                Inset(tileStates[i].rectTransform, 0f, 22f);

                tileBlurbs[i] = MakeText(tile.transform, "Blurb", 18, TextAlignmentOptions.TopLeft);
                tileBlurbs[i].color = Muted;
                tileBlurbs[i].lineSpacing = 4f;
                Place(tileBlurbs[i].rectTransform, 0f, 0.05f, 1f, 0.40f, 0f);
                Inset(tileBlurbs[i].rectTransform, 22f, 18f);

                tile.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Choose(captured)));
                tiles[i] = tile;
            }

            // The run panel takes the tiles' place while processing.
            Image runBox = Band(root, "RunPanel", Pane, mainX0, 0.225f, mainX1, 0.612f, true);
            runPercent = MakeText(runBox.transform, "Percent", 19, TextAlignmentOptions.MidlineLeft);
            runPercent.enableWordWrapping = false;
            runPercent.overflowMode = TextOverflowModes.Ellipsis;
            Place(runPercent.rectTransform, 0.025f, 0.87f, 0.975f, 0.97f, 0f);
            Image barBack = Band(runBox.transform, "Bar", new Color(0.08f, 0.13f, 0.15f, 1f), 0.025f, 0.80f, 0.975f, 0.855f, false);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(barBack.transform, false);
            fill.GetComponent<Image>().color = Green;
            runFill = fill.GetComponent<RectTransform>();
            Place(runFill, 0f, 0f, 0f, 1f, 0f);
            runText = MakeText(runBox.transform, "RunLog", 19, TextAlignmentOptions.TopLeft);
            Place(runText.rectTransform, 0.025f, 0.03f, 0.975f, 0.76f, 0f);
            runBox.gameObject.SetActive(false);

            nextButton = MakeButton("NextButton", root, "START PROCESSING", new Color(0.10f, 0.40f, 0.22f));
            Place(nextButton.GetComponent<RectTransform>(), 0.74f, ColumnsBottom + 0.008f, mainX1, 0.212f, 0f);
            nextButton.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
            Edge(nextButton.transform, Green, 4f);
            nextButton.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Submit()));

            processFeedback = MakeStatusBar(root, "ProcessFeedback");

            processRoot.SetActive(false);
        }
    }
}
