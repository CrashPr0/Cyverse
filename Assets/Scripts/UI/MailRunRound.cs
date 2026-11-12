using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;

namespace Cyverse.UI
{
    /// <summary>
    /// PHISH FRENZY's round (Level 4, bypass MFA): mailboxes slide along the
    /// top of the cabinet, and only the green TEST MAILBOX is approved for the
    /// exercise. Slide the launcher with the arrows and send practice phishing
    /// emails up with Space. A sweeping SPAM FILTER eats emails it touches,
    /// and hitting a real employee's inbox breaks the rules of engagement.
    /// Land five before time runs out and the test user's phone shows why it
    /// worked (a push prompt they tapped Approve on) and what would have
    /// stopped it (a security key that can't be phished).
    /// </summary>
    public sealed class MailRunRound : ArcadeRound
    {
        public const int HitsToWin = 5;
        public const int StrikesAllowed = 3;
        public const float RoundSeconds = 35f;
        private const int WinTickets = 20;
        private const int ConsolationTickets = 5;
        private const int TicketsPerHit = 3;

        // Cabinet coordinates (centre origin, inside the cabinet box).
        private const float CabWidth = 620f, CabHeight = 430f;
        private const float BoxRowY = 150f, FilterY = 30f, LauncherY = -178f;
        private const float BoxSpacing = 118f, BoxWidth = 92f, BoxHeight = 64f;
        private const float MinX = -270f, MaxX = 270f;
        private const float LauncherSpeed = 330f, LauncherAccel = 2200f;
        private const float EmailSpeed = 560f, SendCooldown = 0.32f;
        private const float FilterWidth = 140f;

        private static readonly string[] Inboxes =
            { "A. RIVERA", "HR DESK", "PAYROLL", "K. OSEI", "IT HELP", "D. NGUYEN" };

        private sealed class Mailbox
        {
            public RectTransform rt;
            public Image body;
            public TMP_Text label;
            public bool test;
            public float offset;
            public float flash;
            public Color baseColor;
        }

        private sealed class Email
        {
            public RectTransform rt;
            public bool live;
        }

        private enum Step { Play, Phone }

        private Mailbox[] boxes;
        private readonly Email[] emails = new Email[3];
        private RectTransform cab, launcher, filter;
        private float launcherX, launcherVel, cooldown, conveyor;
        private float filterX, filterDir = 1f, filterSpeed;
        private float timeLeft, stateTime, launcherKick;
        private int hits, strikes;
        private bool hazardsPaused;
        private Step step;

        private RectTransform phone;
        private Image phoneScreen, approveButton;
        private TMP_Text phoneText, phoneCaption, approveLabel;
        private int phoneStage;

        protected override string MachineName => "PHISH FRENZY";
        protected override string Tagline =>
            "Land practice phishing emails in the <color=#4CE087><b>TEST MAILBOX</b></color> only.\n" +
            "Real employees' inboxes are out of bounds, and the spam filter eats what it touches.";
        protected override string PlayKeys => "← / → MOVE     ·     SPACE SEND";
        protected override Color Accent => new Color(0.22f, 0.82f, 1f);

        /// <summary>Test views.</summary>
        public int Hits => hits;
        public int Strikes => strikes;

        // ---- Build ------------------------------------------------------------------

        protected override void BuildPlayfield(RectTransform field)
        {
            cab = Box(field, "Cabinet", new Color(0.04f, 0.06f, 0.11f, 1f), new Vector2(-142f, 0f),
                new Vector2(CabWidth, CabHeight));
            // Mailboxes scroll past the edges, so clip them to the screen.
            cab.gameObject.AddComponent<RectMask2D>();
            // A faint grid gives the screen its arcade look.
            for (int i = -5; i <= 5; i++)
                Box(cab, "GridV" + i, new Color(0.22f, 0.82f, 1f, 0.05f), new Vector2(i * 56f, 0f), new Vector2(2f, CabHeight));
            for (int i = -3; i <= 3; i++)
                Box(cab, "GridH" + i, new Color(0.22f, 0.82f, 1f, 0.05f), new Vector2(0f, i * 60f), new Vector2(CabWidth, 2f));
            Box(cab, "Shelf", new Color(0.20f, 0.26f, 0.34f, 1f), new Vector2(0f, BoxRowY - BoxHeight * 0.5f - 6f), new Vector2(CabWidth, 6f));
            Box(cab, "Track", new Color(0.20f, 0.26f, 0.34f, 1f), new Vector2(0f, LauncherY - 22f), new Vector2(CabWidth - 30f, 4f));

            BuildMailboxes();

            filter = Box(cab, "SpamFilter", new Color(1f, 0.42f, 0.36f, 0.85f), new Vector2(0f, FilterY), new Vector2(FilterWidth, 16f));
            Box(filter, "Glow", new Color(1f, 0.42f, 0.36f, 0.18f), Vector2.zero, new Vector2(FilterWidth + 26f, 40f)).SetAsFirstSibling();
            TMP_Text filterLabel = Label(filter, "Label", "<b>SPAM FILTER</b>", 12f, new Color(0.12f, 0.02f, 0.02f));
            filterLabel.characterSpacing = 2f;
            filterLabel.enableWordWrapping = false;

            launcher = Box(cab, "Launcher", new Color(0.22f, 0.82f, 1f, 1f), new Vector2(0f, LauncherY), new Vector2(64f, 20f));
            Box(launcher, "Nozzle", new Color(0.70f, 0.95f, 1f, 1f), new Vector2(0f, 15f), new Vector2(18f, 12f));
            Box(launcher, "Base", new Color(0.10f, 0.40f, 0.52f, 1f), new Vector2(0f, -10f), new Vector2(80f, 6f));

            for (int i = 0; i < emails.Length; i++)
            {
                RectTransform env = Box(cab, "Email" + i, new Color(0.96f, 0.96f, 0.90f, 1f), Vector2.zero, new Vector2(34f, 22f));
                // A flap drawn as two tilted strips.
                RectTransform l = Box(env, "FlapL", new Color(0.55f, 0.58f, 0.62f, 1f), new Vector2(-8f, 3f), new Vector2(20f, 2.5f));
                l.localRotation = Quaternion.Euler(0f, 0f, -32f);
                RectTransform r = Box(env, "FlapR", new Color(0.55f, 0.58f, 0.62f, 1f), new Vector2(8f, 3f), new Vector2(20f, 2.5f));
                r.localRotation = Quaternion.Euler(0f, 0f, 32f);
                env.gameObject.SetActive(false);
                emails[i] = new Email { rt = env };
            }

            BuildPhone(field);
            launcherX = 0f;
            filterX = -200f;
            StepBoxes(0f, false);
            Layout();
        }

        private void BuildMailboxes()
        {
            int testIndex = Random.Range(1, Inboxes.Length - 1);
            boxes = new Mailbox[Inboxes.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                bool test = i == testIndex;
                var box = new Mailbox { test = test, offset = i * BoxSpacing };
                box.baseColor = test ? new Color(0.12f, 0.55f, 0.30f, 1f) : new Color(0.20f, 0.24f, 0.30f, 1f);
                box.rt = Box(cab, "Mailbox_" + i, box.baseColor, Vector2.zero, new Vector2(BoxWidth, BoxHeight));
                box.body = box.rt.GetComponent<Image>();
                Box(box.rt, "Slot", new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, -18f), new Vector2(BoxWidth - 30f, 6f));
                box.label = Label(box.rt, "Name", test ? "<b>TEST\nMAILBOX</b>" : Inboxes[i], 12f,
                    test ? Color.white : new Color(0.72f, 0.78f, 0.86f));
                box.label.rectTransform.offsetMin = new Vector2(4f, 10f);
                box.label.rectTransform.offsetMax = new Vector2(-4f, -4f);
                if (test)
                {
                    var edge = box.rt.gameObject.AddComponent<Outline>();
                    edge.effectColor = new Color(0.30f, 0.95f, 0.55f, 0.9f);
                    edge.effectDistance = new Vector2(2f, 2f);
                }
                boxes[i] = box;
            }
        }

        private void BuildPhone(RectTransform field)
        {
            RectTransform side = Box(field, "PhoneSide", new Color(0.03f, 0.05f, 0.08f, 1f), new Vector2(318f, 0f), new Vector2(276f, CabHeight));
            TMP_Text heading = Label(side, "Heading", "<b>TEST USER'S PHONE</b>", 16f, Dim);
            heading.rectTransform.anchorMin = new Vector2(0f, 1f);
            heading.rectTransform.anchorMax = new Vector2(1f, 1f);
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);
            heading.rectTransform.sizeDelta = new Vector2(0f, 30f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            heading.characterSpacing = 3f;

            phone = Box(side, "Phone", new Color(0.14f, 0.15f, 0.18f, 1f), new Vector2(0f, 26f), new Vector2(150f, 260f));
            phoneScreen = Box(phone, "Screen", new Color(0.05f, 0.08f, 0.12f, 1f), new Vector2(0f, 4f), new Vector2(134f, 226f)).GetComponent<Image>();
            Box(phone, "Speaker", new Color(0.05f, 0.05f, 0.06f, 1f), new Vector2(0f, 121f), new Vector2(34f, 4f));
            phoneText = Label(phoneScreen.transform, "Text", string.Empty, 15f, Ink);
            phoneText.rectTransform.offsetMin = new Vector2(8f, 56f);
            phoneText.rectTransform.offsetMax = new Vector2(-8f, -10f);

            approveButton = Box(phoneScreen.transform, "Approve", new Color(0.20f, 0.60f, 0.95f, 1f), new Vector2(0f, -78f), new Vector2(104f, 34f)).GetComponent<Image>();
            approveLabel = Label(approveButton.transform, "Label", "<b>APPROVE</b>", 14f, Color.white);
            approveButton.gameObject.SetActive(false);

            phoneCaption = Label(side, "Caption", string.Empty, 15f, Ink);
            phoneCaption.rectTransform.anchorMin = new Vector2(0f, 0f);
            phoneCaption.rectTransform.anchorMax = new Vector2(1f, 0f);
            phoneCaption.rectTransform.pivot = new Vector2(0.5f, 0f);
            phoneCaption.rectTransform.sizeDelta = new Vector2(-24f, 76f);
            phoneCaption.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            RefreshPhone();
        }

        // ---- Play -------------------------------------------------------------------

        protected override void BeginPlay()
        {
            hits = 0;
            strikes = 0;
            timeLeft = RoundSeconds;
            filterSpeed = 150f;
            filterX = -200f;
            cooldown = 0.25f;
            step = Step.Play;
            RefreshStatus();
            RefreshPhone();
            Flash("Line up under the <color=#4CE087><b>green TEST MAILBOX</b></color> and press <b>Space</b>.", 4f);
        }

        protected override void TickPlay(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);
            stateTime += dt;
            if (step == Step.Phone) { TickPhone(); StepBoxes(dt, false); Layout(); return; }

            timeLeft -= dt;
            float pace = 1f - Mathf.Clamp01(timeLeft / RoundSeconds);
            if (!hazardsPaused)
            {
                conveyor += Mathf.Lerp(62f, 108f, pace) * dt;
                filterSpeed = Mathf.Lerp(150f, 250f, pace);
                filterX += filterDir * filterSpeed * dt;
                if (filterX > MaxX - FilterWidth * 0.5f + 30f) { filterX = MaxX - FilterWidth * 0.5f + 30f; filterDir = -1f; }
                if (filterX < MinX + FilterWidth * 0.5f - 30f) { filterX = MinX + FilterWidth * 0.5f - 30f; filterDir = 1f; }
            }

            float input = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) input -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) input += 1f;
            launcherVel = Mathf.MoveTowards(launcherVel, input * LauncherSpeed, LauncherAccel * dt);
            float x = launcherX + launcherVel * dt;
            launcherX = Mathf.Clamp(x, MinX, MaxX);
            if (!Mathf.Approximately(x, launcherX)) launcherVel = 0f;

            cooldown -= dt;
            if (Input.GetKeyDown(KeyCode.Space)) Send();

            StepEmails(dt);
            StepBoxes(dt, true);
            launcherKick = Mathf.MoveTowards(launcherKick, 0f, dt * 5f);
            RefreshStatus();
            Layout();

            if (Phase != State.Playing || step != Step.Play) return;
            if (timeLeft <= 0f)
                Lose("TIME'S UP", $"{hits} of {HitsToWin} practice emails reached the test mailbox.\n" +
                    "In a real test, staff who report odd emails and a good spam filter make this hard on purpose.");
        }

        private bool Send()
        {
            if (cooldown > 0f) return false;
            foreach (Email email in emails)
            {
                if (email.live) continue;
                email.live = true;
                email.rt.gameObject.SetActive(true);
                email.rt.anchoredPosition = new Vector2(launcherX, LauncherY + 22f);
                email.rt.localRotation = Quaternion.identity;
                email.rt.localScale = Vector3.one;
                cooldown = SendCooldown;
                launcherKick = 1f;
                if (Sfx.Instance != null) Sfx.Instance.PlayClick();
                return true;
            }
            return false;
        }

        private void StepEmails(float dt)
        {
            foreach (Email email in emails)
            {
                if (!email.live) continue;
                Vector2 p = email.rt.anchoredPosition;
                float before = p.y;
                p.y += EmailSpeed * dt;
                email.rt.anchoredPosition = p;
                // A small flutter as it flies.
                email.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(p.y * 0.05f) * 6f);

                if (before < FilterY && p.y >= FilterY - 10f && Mathf.Abs(p.x - filterX) < FilterWidth * 0.5f + 14f)
                {
                    Kill(email);
                    Flash("<color=#FF8A6A><b>Caught by the spam filter.</b></color> Time it for the gap.", 1.8f);
                    if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                    UiMotion.Pulse(filter, 0.12f);
                    continue;
                }

                if (p.y >= BoxRowY - BoxHeight * 0.5f)
                {
                    Mailbox hit = null;
                    foreach (Mailbox box in boxes)
                        if (Mathf.Abs(box.rt.anchoredPosition.x - p.x) < BoxWidth * 0.5f + 6f) hit = box;
                    if (hit != null) { Kill(email); Land(hit); }
                    else if (p.y > CabHeight * 0.5f) Kill(email);
                }
            }
        }

        private void Land(Mailbox box)
        {
            box.flash = 1f;
            UiMotion.Pulse(box.rt, 0.14f);
            if (box.test)
            {
                hits++;
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                Flash($"<color=#4CE087><b>Landed!</b></color>  {hits} of {HitsToWin} in the test mailbox.", 1.6f);
                RefreshPhone();
                if (hits >= HitsToWin) StartPhone();
            }
            else
            {
                strikes++;
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                UiMotion.Shake(box.rt, 6f);
                Flash($"<color=#FF8A6A><b>That's {box.label.text}'s real inbox.</b></color> Out of bounds for this test.", 2.2f);
                if (strikes >= StrikesAllowed)
                    Lose("OUT OF BOUNDS", "Three emails went to real employees' inboxes.\n" +
                        "The rules of engagement only allow the approved test mailbox.");
            }
            RefreshStatus();
        }

        private void Kill(Email email)
        {
            email.live = false;
            email.rt.gameObject.SetActive(false);
        }

        private void StepBoxes(float dt, bool moving)
        {
            float span = BoxSpacing * boxes.Length;
            foreach (Mailbox box in boxes)
            {
                float x = Mathf.Repeat(box.offset + conveyor, span) - span * 0.5f;
                box.rt.anchoredPosition = new Vector2(x, BoxRowY);
                box.flash = Mathf.MoveTowards(box.flash, 0f, dt * 2.5f);
                Color hot = box.test ? new Color(0.40f, 1f, 0.60f, 1f) : new Color(0.95f, 0.30f, 0.26f, 1f);
                box.body.color = Color.Lerp(box.baseColor, hot, box.flash);
            }
        }

        // ---- The phone: why it worked, and what stops it ----------------------------

        private void StartPhone()
        {
            step = Step.Phone;
            stateTime = 0f;
            phoneStage = 0;
            foreach (Email email in emails) Kill(email);
            Flash(string.Empty, 0.01f);
        }

        private void TickPhone()
        {
            if (phoneStage == 0)
            {
                phoneStage = 1;
                phoneScreen.color = new Color(0.05f, 0.08f, 0.12f, 1f);
                phoneText.text = "<size=13><color=#9FB4C4>SIGN-IN REQUEST</color></size>\n\n<b>Is this you\nsigning in?</b>";
                approveButton.gameObject.SetActive(true);
                approveButton.color = new Color(0.20f, 0.60f, 0.95f, 1f);
                approveLabel.text = "<b>APPROVE</b>";
                phoneCaption.text = "The test user clicked the practice link, and a push prompt pops up.";
                UiMotion.Pulse(phone, 0.06f);
            }
            else if (phoneStage == 1 && stateTime > 1.3f)
            {
                phoneStage = 2;
                approveButton.color = new Color(0.30f, 0.88f, 0.53f, 1f);
                approveLabel.text = "<b>APPROVED</b>";
                phoneText.text = "<color=#4CE087><b>SIGNED IN</b></color>\n\n<size=13>Test account opened</size>";
                phoneCaption.text = "<color=#4CE087><b>They tapped Approve.</b></color> A push prompt can be phished.";
                UiMotion.Pulse(approveButton.transform, 0.12f);
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            }
            else if (phoneStage == 2 && stateTime > 3.0f)
            {
                phoneStage = 3;
                phoneScreen.color = new Color(0.20f, 0.05f, 0.05f, 1f);
                approveButton.gameObject.SetActive(false);
                phoneText.text = "<size=13><color=#9FB4C4>SECURITY KEY REQUIRED</color></size>\n\n<color=#FF6A60><b>BLOCKED</b></color>\n\n<size=13>Wrong website.\nKey refused.</size>";
                phoneCaption.text = "<color=#FFC838><b>With a security key, the same trick fails.</b></color> The key checks the real website.";
                UiMotion.Pulse(phone, 0.08f);
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            }
            else if (phoneStage == 3 && stateTime > 5.0f)
            {
                phoneStage = 4;
                Win();
            }
        }

        private void Win()
        {
            if (Phase != State.Playing) return;
            int tickets = WinTickets + Mathf.Clamp(Mathf.RoundToInt(timeLeft), 0, 20) + (StrikesAllowed - strikes) * 2;
            Finish(true, tickets, "PHISH LANDED",
                "Five practice emails reached the approved test mailbox, and a push prompt let one in.\n" +
                "<color=#FFC838>A security key that can't be phished, a spam filter and staff who report odd emails stop this.</color>");
        }

        private void Lose(string headline, string detail)
        {
            if (Phase != State.Playing) return;
            foreach (Email email in emails) Kill(email);
            Finish(false, ConsolationTickets + TicketsPerHit * hits, headline, detail);
        }

        protected override void ResolveForTest(bool win)
        {
            if (win)
            {
                hits = HitsToWin;
                Win();
            }
            else
            {
                strikes = StrikesAllowed;
                Lose("OUT OF BOUNDS", "Test loss.");
            }
        }

        /// <summary>Test hook: freeze the mailboxes and park the filter and
        /// launcher so a send under the test mailbox lands.</summary>
        public void LineUpForTest()
        {
            hazardsPaused = true;
            filterX = MaxX + 200f;
            float span = BoxSpacing * boxes.Length;
            foreach (Mailbox box in boxes)
                if (box.test) conveyor = Mathf.Repeat(span * 0.5f - box.offset, span);
            StepBoxes(0f, false);
            launcherX = 0f;
            launcherVel = 0f;
            Layout();
        }

        /// <summary>Test hook: press Send without a key.</summary>
        public bool SendForTest()
        {
            cooldown = 0f;
            return Send();
        }

        /// <summary>Test hook: the phone sequence is playing.</summary>
        public bool ShowingPhone => step == Step.Phone;

        // ---- Drawing ----------------------------------------------------------------

        private void Layout()
        {
            if (launcher == null) return;
            launcher.anchoredPosition = new Vector2(launcherX, LauncherY - launcherKick * 4f);
            launcher.localRotation = Quaternion.Euler(0f, 0f, -launcherVel * 0.012f);
            if (filter != null) filter.anchoredPosition = new Vector2(Mathf.Clamp(filterX, -CabWidth, CabWidth), FilterY);
        }

        private void RefreshStatus()
        {
            var landed = new System.Text.StringBuilder();
            for (int i = 0; i < HitsToWin; i++)
                landed.Append(i < hits ? "<color=#4CE087>●</color> " : "<color=#3A4654>●</color> ");
            var strikesText = new System.Text.StringBuilder();
            for (int i = 0; i < StrikesAllowed; i++)
                strikesText.Append(i < strikes ? "<color=#FF6A60>×</color> " : "<color=#3A4654>×</color> ");
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
            SetStatus($"LANDED  {landed}   ·   OUT OF BOUNDS  {strikesText}  ·   0:{seconds:00}");
        }

        private void RefreshPhone()
        {
            if (phoneText == null) return;
            phoneScreen.color = new Color(0.05f, 0.08f, 0.12f, 1f);
            approveButton.gameObject.SetActive(false);
            phoneText.text = $"<size=13><color=#9FB4C4>TEST INBOX</color></size>\n\n<size=40><b>{hits}</b></size>\n<size=13>practice emails</size>";
            phoneCaption.text = hits == 0
                ? "Only the approved test account takes part in this exercise."
                : "Each landed email is a chance the test user clicks.";
        }
    }
}
