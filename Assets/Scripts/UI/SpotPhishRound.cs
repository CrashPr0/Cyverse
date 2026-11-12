using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;

namespace Cyverse.UI
{
    /// <summary>
    /// SPOT THE PHISH's round (Level 4, social engineering): a scam-or-legit
    /// quiz in the style of the well-known phishing quizzes. Five messages
    /// come up one at a time, leading with a fake job post on a jobs site and
    /// a DM from a friend's hacked account. The player calls each one SCAM or
    /// LEGIT; then the red flags (or the signs it's real) light up in the
    /// message and are listed beside it. Every name, site and address is
    /// invented.
    /// </summary>
    public sealed class SpotPhishRound : ArcadeRound
    {
        public const int CorrectToWin = 4;
        private const int TicketsPerCorrect = 4;
        private const int PerfectBonus = 10;

        /// <summary>One message on the screen. Text inside [[ ]] is a clue:
        /// plain while the player decides, highlighted once they answer.</summary>
        public sealed class Message
        {
            public readonly string app;
            public readonly Color appColor;
            public readonly string sender;
            public readonly string senderDetail;
            public readonly string body;
            public readonly bool scam;
            public readonly string verdict;
            public readonly string[] clues;

            public Message(string app, Color appColor, string sender, string senderDetail,
                string body, bool scam, string verdict, params string[] clues)
            {
                this.app = app;
                this.appColor = appColor;
                this.sender = sender;
                this.senderDetail = senderDetail;
                this.body = body;
                this.scam = scam;
                this.verdict = verdict;
                this.clues = clues;
            }
        }

        private static readonly Color JobsBlue = new Color(0.26f, 0.56f, 0.96f);
        private static readonly Color ChatViolet = new Color(0.64f, 0.44f, 1f);
        private static readonly Color MailTeal = new Color(0.20f, 0.74f, 0.70f);

        /// <summary>The messages, in order. The first two lead on purpose: a
        /// fake job post and a friend's hijacked account.</summary>
        public static readonly Message[] Messages =
        {
            new Message("HIRELINE  ·  JOB POST", JobsBlue,
                "Nexora Global  ·  Remote Data Analyst",
                "Posted by Dana Wells, Recruiter  ·  [[joined 3 days ago · 2 connections]]",
                "<b>Remote Data Analyst</b>   [[$95/hr · no experience needed!]]\n\n" +
                "We're hiring fast. [[Text our hiring manager at (555) 014-2290]] to start today, no interview needed.\n\n" +
                "Every new hire buys a [[$60 starter kit]] (laptop software), refunded with your first paycheck.\n\n" +
                "[[Only 3 spots left. Apply in the next hour!]]",
                true,
                "Fake job posts are built to take your money, your sign-in and your personal details.",
                "Pay far too good for \"no experience\"",
                "Pulls you off the site into a private text chat",
                "Asks you to pay first. Real jobs pay you.",
                "Brand-new recruiter with almost no connections",
                "Rush: \"only 3 spots, next hour\""),

            new Message("PINGLY  ·  DIRECT MESSAGE", ChatViolet,
                "Maya Torres",
                "Your friend from class  ·  Active now",
                "<color=#B9A6FF>Maya:</color>  hey!! u there?\n" +
                "<color=#B9A6FF>Maya:</color>  [[i'm locked out and the reset code went to YOUR number by mistake]]\n" +
                "<color=#B9A6FF>Maya:</color>  [[can u send me the 6-digit code u just got?? need it asap]]\n\n" +
                "<color=#9FB4C4><size=85%>TEXT MESSAGE ON YOUR PHONE</size></color>\n" +
                "<i>Your Pingly sign-in code is 482 913. [[Never share this code with anyone.]]</i>",
                true,
                "Maya's account was taken over, and the attacker is now using it on her friends.",
                "Someone else is typing from Maya's hacked account",
                "A code sent to YOUR phone unlocks YOUR account",
                "Hurry: \"need it asap\"",
                "The code's own text says never share it",
                "Check by calling Maya, not by replying"),

            new Message("MAIL  ·  INBOX", MailTeal,
                "IT Help Desk",
                "[[it@brightway.co]]  ·  your company's own address",
                "<b>Wi-Fi maintenance Saturday, 2 to 4 AM</b>\n\n" +
                "Hi team, the office Wi-Fi will be offline this Saturday from 2 to 4 AM while we update the router.\n\n" +
                "[[There's nothing you need to do.]] Questions? [[Use the help desk page you already have bookmarked.]]\n\n" +
                "Thanks,\nBrightway IT",
                false,
                "Not every message is a trap. Real ones don't rush you or ask for secrets.",
                "Sent from the company's real address",
                "No link, no sign-in, no payment asked",
                "Points you to a page you already use",
                "Calm tone, no deadline or threat"),

            new Message("MAIL  ·  INBOX", MailTeal,
                "Payroll Team",
                "[[payroll@brightway-hr-portal.co]]",
                "<b>ACTION REQUIRED: Paycheck on hold</b>\n\n" +
                "[[Dear employee,]]\n\n" +
                "We could not process your direct deposit. [[Confirm your bank details within 24 hours]] or this month's pay will be held.\n\n" +
                "[[CONFIRM DETAILS  >  brightway-hr-portal.co/verify]]",
                true,
                "Go to the payroll page you know, or call HR, instead of using the link.",
                "Look-alike address, not brightway.co",
                "\"Dear employee\" instead of your name",
                "A threat plus a 24-hour deadline",
                "Wants bank details through a link"),

            new Message("HIRELINE  ·  MESSAGES", JobsBlue,
                "Priya Shah",
                "Recruiter at Harbor & Pine Bank  ·  [[about the job you applied to on Monday]]",
                "Hi! Thanks for applying to our IT Support Intern role.\n\n" +
                "[[Interview times are on your application page here on HireLine]], so pick whichever slot works for you.\n\n" +
                "[[We never ask applicants for money, passwords or codes.]]\n\n" +
                "Priya",
                false,
                "A real recruiter. Compare it with the first job post.",
                "Matches a job you actually applied to",
                "Keeps you on the site, no outside chat",
                "Asks for nothing: no money, passwords or codes"),
        };

        private int index;
        private int correct;
        private bool revealing;
        private bool lastRight;
        private readonly bool[] results = new bool[Messages.Length];

        private RectTransform card;
        private Image appBar;
        private TMP_Text appText, senderText, detailText, bodyText;
        private TMP_Text columnHeading, columnText;
        private Button scamButton, legitButton;

        protected override string MachineName => "SPOT THE PHISH";
        protected override string Tagline =>
            "Five messages, one question each: <color=#FF8A6A><b>SCAM</b></color> or <color=#4CE087><b>LEGIT</b></color>?\n" +
            "After each call, the clues light up.";
        protected override string PlayKeys => "1 SCAM     ·     2 LEGIT     ·     OR CLICK";
        protected override Color Accent => new Color(1f, 0.44f, 0.56f);
        protected override Vector2 PanelSize => new Vector2(1060f, 700f);

        /// <summary>Test views.</summary>
        public int Index => index;
        public int Correct => correct;
        public bool Revealing => revealing;
        public bool CurrentIsScam => index < Messages.Length && Messages[index].scam;

        // ---- Build ------------------------------------------------------------------

        protected override void BuildPlayfield(RectTransform field)
        {
            // The message, drawn as an app window.
            card = Box(field, "MessageCard", new Color(0.93f, 0.95f, 0.97f, 1f), new Vector2(-172f, 0f), new Vector2(640f, 456f));
            appBar = Box(card, "AppBar", JobsBlue, new Vector2(0f, 206f), new Vector2(640f, 44f)).GetComponent<Image>();
            appText = Label(appBar.transform, "App", string.Empty, 17f, Color.white, TextAlignmentOptions.MidlineLeft);
            appText.rectTransform.offsetMin = new Vector2(18f, 0f);
            appText.characterSpacing = 3f;
            appText.enableWordWrapping = false;

            RectTransform avatar = Box(card, "Avatar", new Color(0.70f, 0.76f, 0.84f, 1f), new Vector2(-284f, 148f), new Vector2(44f, 44f));
            Box(avatar, "Head", new Color(0.93f, 0.95f, 0.97f, 1f), new Vector2(0f, 6f), new Vector2(16f, 16f));
            Box(avatar, "Shoulders", new Color(0.93f, 0.95f, 0.97f, 1f), new Vector2(0f, -14f), new Vector2(30f, 12f));

            senderText = Label(card, "Sender", string.Empty, 20f, new Color(0.08f, 0.10f, 0.14f), TextAlignmentOptions.TopLeft);
            Place(senderText.rectTransform, new Vector2(30f, 162f), new Vector2(560f, 28f));
            senderText.enableWordWrapping = false;
            senderText.overflowMode = TextOverflowModes.Ellipsis;
            detailText = Label(card, "SenderDetail", string.Empty, 15f, new Color(0.36f, 0.42f, 0.50f), TextAlignmentOptions.TopLeft);
            Place(detailText.rectTransform, new Vector2(30f, 134f), new Vector2(560f, 24f));
            detailText.enableWordWrapping = false;
            Box(card, "Rule", new Color(0.80f, 0.84f, 0.89f, 1f), new Vector2(0f, 114f), new Vector2(600f, 2f));

            bodyText = Label(card, "Body", string.Empty, 17f, new Color(0.10f, 0.12f, 0.16f), TextAlignmentOptions.TopLeft);
            Place(bodyText.rectTransform, new Vector2(0f, -56f), new Vector2(592f, 316f));
            bodyText.lineSpacing = 6f;

            // The right column: the two calls, then what to look for.
            RectTransform column = Box(field, "Column", new Color(0.03f, 0.04f, 0.07f, 1f), new Vector2(334f, 0f), new Vector2(316f, 456f));
            columnHeading = Label(column, "Heading", string.Empty, 22f, Ink);
            Place(columnHeading.rectTransform, new Vector2(0f, 196f), new Vector2(296f, 48f));

            scamButton = MakeCall(column, "Scam", "<b>[1]  SCAM</b>", new Color(0.74f, 0.20f, 0.17f, 1f), new Vector2(0f, 128f), () => Answer(true));
            legitButton = MakeCall(column, "Legit", "<b>[2]  LEGIT</b>", new Color(0.13f, 0.55f, 0.31f, 1f), new Vector2(0f, 52f), () => Answer(false));

            columnText = Label(column, "Clues", string.Empty, 15f, Ink, TextAlignmentOptions.TopLeft);
            Place(columnText.rectTransform, new Vector2(0f, -110f), new Vector2(284f, 226f));
            columnText.lineSpacing = 4f;

            ShowMessage(0);
        }

        private Button MakeCall(Transform parent, string name, string label, Color color, Vector2 position, Action onClick)
        {
            RectTransform rt = Box(parent, name + "Button", color, position, new Vector2(270f, 60f));
            var image = rt.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.92f, 0.86f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            UiMotion.AddPress(button);
            TMP_Text text = Label(rt, "Label", label, 24f, Color.white);
            text.characterSpacing = 4f;
            text.enableWordWrapping = false;
            return button;
        }

        private static void Place(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        // ---- Play -------------------------------------------------------------------

        protected override void BeginPlay()
        {
            index = 0;
            correct = 0;
            ShowMessage(0);
            Flash("Look at who sent it, what they want, and how fast they want it.", 4f);
        }

        protected override void TickPlay(float dt)
        {
            if (!revealing)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Answer(true);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Answer(false);
            }
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                     Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Next();
            }
        }

        private void Answer(bool sayScam)
        {
            if (Phase != State.Playing || revealing || index >= Messages.Length) return;
            Message m = Messages[index];
            lastRight = sayScam == m.scam;
            results[index] = lastRight;
            if (lastRight) correct++;
            revealing = true;
            if (Sfx.Instance != null)
            {
                if (lastRight) Sfx.Instance.PlayConfirm();
                else Sfx.Instance.PlayDeny();
            }
            Reveal(m);
            if (lastRight) UiMotion.Pulse(card, 0.025f);
            else UiMotion.Shake(card, 6f);
            RefreshStatus();
        }

        private void Next()
        {
            if (!revealing) return;
            if (index + 1 >= Messages.Length) { End(); return; }
            ShowMessage(index + 1);
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
        }

        private void End()
        {
            if (Phase != State.Playing) return;
            int tickets = correct * TicketsPerCorrect + (correct == Messages.Length ? PerfectBonus : 0);
            bool won = correct >= CorrectToWin;
            Finish(won, tickets, won ? "SHARP EYES" : "KEEP PRACTICING",
                $"You called {correct} of {Messages.Length} messages right.\n" +
                "<color=#FFC838>Slow down, check the sender another way, and never pay, sign in or share a code because a message told you to.</color>");
        }

        // ---- Drawing ----------------------------------------------------------------

        private void ShowMessage(int i)
        {
            index = Mathf.Clamp(i, 0, Messages.Length - 1);
            revealing = false;
            Message m = Messages[index];
            appBar.color = m.appColor;
            appText.text = "<b>" + m.app + "</b>";
            senderText.text = "<b>" + m.sender + "</b>";
            detailText.text = Clues(m.senderDetail, null);
            bodyText.text = Clues(m.body, null);
            columnHeading.text = "<b>SCAM OR LEGIT?</b>";
            columnHeading.color = Ink;
            scamButton.gameObject.SetActive(true);
            legitButton.gameObject.SetActive(true);
            columnText.text = "<color=#9FB4C4>Things to check:</color>\n" +
                "•  Who is it really from?\n" +
                "•  What do they want you to do?\n" +
                "•  Are they rushing or scaring you?\n" +
                "•  Money, a sign-in or a code?";
            Flash(string.Empty, 0.01f);
            if (card != null) UiMotion.Pulse(card, 0.015f);
            RefreshStatus();
        }

        private void Reveal(Message m)
        {
            string mark = m.scam ? "#FF5A4A50" : "#4CE08748";
            detailText.text = Clues(m.senderDetail, mark);
            bodyText.text = Clues(m.body, mark);
            scamButton.gameObject.SetActive(false);
            legitButton.gameObject.SetActive(false);

            string kind = m.scam ? "<color=#FF8A6A>SCAM</color>" : "<color=#4CE087>LEGIT</color>";
            columnHeading.text = (lastRight ? "<color=#4CE087><b>RIGHT!</b></color>" : "<color=#FF8A6A><b>NOT QUITE</b></color>") +
                "\n<size=17>It's " + kind + ".</size>";

            var list = new System.Text.StringBuilder();
            list.Append(m.scam ? "<color=#FF8A6A><b>RED FLAGS</b></color>\n" : "<color=#4CE087><b>WHY IT'S REAL</b></color>\n");
            foreach (string clue in m.clues) list.Append("•  ").Append(clue).Append('\n');
            columnText.text = list.ToString();
            columnText.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            columnText.rectTransform.sizeDelta = new Vector2(284f, 330f);
            Flash(m.verdict + "   <color=#9FB4C4>SPACE NEXT</color>", 60f);
        }

        /// <summary>Turns [[clue]] marks into highlights, or strips them.</summary>
        private static string Clues(string text, string markColor)
        {
            if (markColor == null) return text.Replace("[[", string.Empty).Replace("]]", string.Empty);
            return text.Replace("[[", "<mark=" + markColor + ">").Replace("]]", "</mark>");
        }

        private void RefreshStatus()
        {
            var dots = new System.Text.StringBuilder();
            int answered = index + (revealing ? 1 : 0);
            for (int i = 0; i < Messages.Length; i++)
                dots.Append(i >= answered ? "<color=#3A4654>●</color> "
                    : results[i] ? "<color=#4CE087>●</color> " : "<color=#FF6A60>●</color> ");
            SetStatus($"MESSAGE {index + 1} OF {Messages.Length}   ·   {dots}  ·   RIGHT  {correct}");
            if (columnText != null && !revealing)
            {
                columnText.rectTransform.anchoredPosition = new Vector2(0f, -110f);
                columnText.rectTransform.sizeDelta = new Vector2(284f, 226f);
            }
        }

        // ---- Test hooks -----------------------------------------------------------------

        protected override void ResolveForTest(bool win)
        {
            correct = win ? Messages.Length : 0;
            index = Messages.Length - 1;
            revealing = true;
            End();
        }

        /// <summary>Test hook: answer the message on screen.</summary>
        public void AnswerForTest(bool sayScam) => Answer(sayScam);

        /// <summary>Test hook: move on from the clues.</summary>
        public void NextForTest() => Next();
    }
}
