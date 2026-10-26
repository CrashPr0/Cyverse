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
            public string[] notes;   // why each tile is right or wrong
            public int correct;
        }

        private static readonly PickStep[] Picks =
        {
            new PickStep
            {
                question = "What kind of case is this? Check the SOC alert that sent the phone here.",
                tiles = new[] { "PHISHING / MALWARE", "HR COMPLAINT", "LOST DEVICE", "BILLING FRAUD" },
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
                question = "How much should we copy?\n" +
                    "<size=18><color=#9FC4D6>QUICK copies texts, email, apps and web history.  " +
                    "FULL FILE SYSTEM copies everything, including app databases, deleted records and photo metadata.  " +
                    "LOCK BYPASS breaks into a locked phone.</color></size>",
                tiles = new[] { "QUICK (LOGICAL)", "FULL FILE SYSTEM", "LOCK BYPASS", null },
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

        private GameObject processRoot;
        private TMP_Text stepRail, stepHeader, stepQuestion, processFeedback, runText;
        private RectTransform runFill;
        private Button nextButton;
        private readonly Button[] tiles = new Button[TileCount];

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
                runClock = float.MaxValue;   // Enter skips the animation
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
            int doneCount = Mathf.Min(total, Mathf.FloorToInt(runClock / SecondsPerStage));
            if (doneCount >= total && !runDone)
            {
                runDone = true;
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                SetProcessFeedback("<color=#4CE087><b>PROCESSING COMPLETE.</b></color>  The image hash matches " +
                    "the one on the custody form, so this copy is exactly what came off the phone.", Ink);
            }
            float fraction = runDone ? 1f : Mathf.Clamp01(runClock / (SecondsPerStage * total));
            runFill.anchorMax = new Vector2(fraction, 1f);

            var sb = new StringBuilder();
            for (int i = 0; i < total; i++)
            {
                if (i < doneCount) sb.Append("<color=#4CE087>[OK]</color>  ");
                else if (i == doneCount) sb.Append("<color=#E5A823>[..]</color>  ");
                else sb.Append("<color=#607585>[  ]  ");
                sb.Append(runStages[i]);
                if (i > doneCount) sb.Append("</color>");
                sb.Append('\n');
            }
            if (runDone)
                sb.Append($"\n<b>{phone.artifacts.Count} artifacts</b> recovered" + PassSummary());
            runText.text = sb.ToString();
            nextButton.GetComponentInChildren<TMP_Text>().text = runDone ? "OPEN IN EXPLORER" : "SKIP";
        }

        private void RefreshProcess()
        {
            var rail = new StringBuilder();
            for (int i = 0; i < StepNames.Length; i++)
            {
                string mark = i < processStep || (i == StepRun && runDone) ? "<color=#4CE087>[OK]</color>"
                    : i == processStep ? "<color=#E5A823> >> </color>" : "<color=#607585>[  ]</color>";
                string color = i == processStep ? "#FFFFFF" : i < processStep ? "#B8CCD6" : "#607585";
                rail.Append(mark).Append($"  <color={color}>{i + 1}  {StepNames[i]}</color>\n\n");
            }
            rail.Append("<size=16><color=#607585>then: EXAMINE</color></size>");
            stepRail.text = rail.ToString();

            stepHeader.text = $"STEP {processStep + 1} OF {StepNames.Length}  //  {StepNames[processStep]}";
            bool run = processStep == StepRun;
            runText.transform.parent.gameObject.SetActive(run);

            if (processStep == StepCaseDetails)
                stepQuestion.text = "<size=18><color=#9FC4D6>Case CY-2026-0714  ·  Examiner: you  ·  " +
                    $"Evidence #1: work phone of {Escape(phone.owner)}  ·  Custody form signed</color></size>\n" +
                    Picks[0].question;
            else if (processStep < Picks.Length) stepQuestion.text = Picks[processStep].question;
            else if (processStep == StepOptions)
                stepQuestion.text = "Pick the extra passes to run. Each one adds clues in Examine; none is required.";
            else stepQuestion.text = "";

            for (int i = 0; i < TileCount; i++)
            {
                Button tile = tiles[i];
                string label = processStep < Picks.Length ? Picks[processStep].tiles[i]
                    : processStep == StepOptions ? OptionNames[i] : null;
                tile.gameObject.SetActive(label != null);
                if (label == null) continue;
                bool on = processStep == StepOptions && OptionOn(i);
                string state = processStep == StepOptions
                    ? (on ? "\n<size=18><color=#4CE087>ON</color></size>" : "\n<size=18><color=#607585>OFF</color></size>")
                    : $"\n<size=16><color=#9FC4D6>press {i + 1}</color></size>";
                tile.GetComponentInChildren<TMP_Text>().text = $"<b>{label}</b>{state}";
                tile.GetComponent<Image>().color = on ? new Color(0.10f, 0.36f, 0.20f) : CategoryIdle;
            }

            nextButton.gameObject.SetActive(processStep >= StepOptions);
            if (processStep == StepOptions)
                nextButton.GetComponentInChildren<TMP_Text>().text = "START PROCESSING";
            if (run) TickRun(0f);
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

        private void BuildProcess(Transform cardRoot)
        {
            processRoot = new GameObject("Process", typeof(RectTransform));
            processRoot.transform.SetParent(cardRoot, false);
            Place(processRoot.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f, 0f);
            Transform root = processRoot.transform;

            var railBox = new GameObject("StepRail", typeof(RectTransform), typeof(Image));
            railBox.transform.SetParent(root, false);
            railBox.GetComponent<Image>().color = new Color(0.03f, 0.07f, 0.08f, 1f);
            Place(railBox.GetComponent<RectTransform>(), 0.015f, 0.13f, 0.245f, 0.92f, 2f);
            stepRail = MakeText(railBox.transform, "Steps", 20, TextAlignmentOptions.TopLeft);
            Place(stepRail.rectTransform, 0f, 0f, 1f, 1f, 18f);

            stepHeader = MakeText(root, "StepHeader", 22, TextAlignmentOptions.MidlineLeft);
            stepHeader.fontStyle = FontStyles.Bold;
            stepHeader.color = new Color(0.36f, 0.78f, 1f);
            Place(stepHeader.rectTransform, 0.26f, 0.85f, 0.98f, 0.92f, 4f);

            stepQuestion = MakeText(root, "StepQuestion", 24, TextAlignmentOptions.TopLeft);
            Place(stepQuestion.rectTransform, 0.26f, 0.64f, 0.98f, 0.85f, 4f);

            for (int i = 0; i < TileCount; i++)
            {
                int captured = i;
                Button tile = MakeButton("Tile_" + i, root, "", CategoryIdle);
                float x = 0.26f + i * 0.18f;
                Place(tile.GetComponent<RectTransform>(), x, 0.42f, x + 0.17f, 0.62f, 2f);
                TMP_Text label = tile.GetComponentInChildren<TMP_Text>();
                label.alignment = TextAlignmentOptions.Center;
                label.enableWordWrapping = true;
                label.fontSize = 21;
                tile.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Choose(captured)));
                tiles[i] = tile;
            }

            // The run panel sits over the tiles while processing.
            var runBox = new GameObject("RunPanel", typeof(RectTransform), typeof(Image));
            runBox.transform.SetParent(root, false);
            runBox.GetComponent<Image>().color = new Color(0.03f, 0.07f, 0.08f, 1f);
            Place(runBox.GetComponent<RectTransform>(), 0.26f, 0.22f, 0.98f, 0.85f, 2f);
            var barBack = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            barBack.transform.SetParent(runBox.transform, false);
            barBack.GetComponent<Image>().color = new Color(0.10f, 0.15f, 0.17f, 1f);
            Place(barBack.GetComponent<RectTransform>(), 0.03f, 0.86f, 0.97f, 0.94f, 0f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(barBack.transform, false);
            fill.GetComponent<Image>().color = Green;
            runFill = fill.GetComponent<RectTransform>();
            Place(runFill, 0f, 0f, 0f, 1f, 0f);
            runText = MakeText(runBox.transform, "RunLog", 21, TextAlignmentOptions.TopLeft);
            Place(runText.rectTransform, 0.03f, 0.03f, 0.97f, 0.82f, 4f);
            runBox.SetActive(false);

            nextButton = MakeButton("NextButton", root, "START PROCESSING", new Color(0.10f, 0.36f, 0.20f));
            Place(nextButton.GetComponent<RectTransform>(), 0.74f, 0.13f, 0.98f, 0.205f, 2f);
            nextButton.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
            nextButton.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Submit()));

            processFeedback = MakeText(root, "ProcessFeedback", 21, TextAlignmentOptions.MidlineLeft);
            Place(processFeedback.rectTransform, 0.02f, 0.02f, 0.98f, 0.115f, 4f);

            processRoot.SetActive(false);
        }
    }
}
