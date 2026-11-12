using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;

namespace Cyverse.UI
{
    /// <summary>
    /// NETWORK ATTACK's round (Level 4): a simple map of the fake company's
    /// network with three places an attack can land: endpoint devices, the
    /// router and the cloud. Six attacks come in one at a time and the player
    /// clicks (or presses 1, 2 or 3 for) the spot each one hits. Then the map
    /// shows the hit and the defense that stops it. Limited on purpose to
    /// those three attack points.
    /// </summary>
    public sealed class NetworkAttackRound : ArcadeRound
    {
        public enum Target { Endpoint = 0, Router = 1, Cloud = 2 }

        public const int CorrectToWin = 4;
        private const int TicketsPerCorrect = 4;
        private const int PerfectBonus = 12;
        private const float PacketSeconds = 0.5f;

        public sealed class Attack
        {
            public readonly string name;
            public readonly string what;
            public readonly Target target;
            public readonly string defense;

            public Attack(string name, string what, Target target, string defense)
            {
                this.name = name;
                this.what = what;
                this.target = target;
                this.defense = defense;
            }
        }

        public static readonly Attack[] Attacks =
        {
            new Attack("PHISHING",
                "A trick email gets an employee to type their password into a fake sign-in page.",
                Target.Endpoint,
                "MFA that can't be phished, a spam filter, and staff who report odd emails."),
            new Attack("DNS HIJACKING",
                "The attacker changes the network's address book, so typing the company's website opens a fake copy.",
                Target.Router,
                "Change the router's default admin password, keep it updated, and lock its DNS settings."),
            new Attack("THIRD-PARTY BREACH",
                "The company that rents out the storage gets hacked, and customer files leak with it.",
                Target.Cloud,
                "Check each vendor's security, share only the data they need, and encrypt what you store."),
            new Attack("MALWARE",
                "A \"free tool\" download quietly installs software that spies on the laptop or locks its files for ransom.",
                Target.Endpoint,
                "Malware protection, automatic updates, and installing only approved apps."),
            new Attack("DENIAL OF SERVICE",
                "A flood of junk traffic hits the office connection until nobody can get online.",
                Target.Router,
                "Traffic filtering from the internet provider and a backup connection."),
            new Attack("THIRD-PARTY VULNERABILITY",
                "A flaw in a cloud app the company uses lets outsiders open shared folders.",
                Target.Cloud,
                "Patch fast when a vendor warns you, review sharing settings, and watch for odd access."),
        };

        private static readonly string[] TargetNames = { "ENDPOINT DEVICES", "ROUTER", "CLOUD SERVICES" };
        private static readonly Color[] TargetColors =
        {
            new Color(0.25f, 0.80f, 1f),
            new Color(1f, 0.72f, 0.25f),
            new Color(0.70f, 0.52f, 1f),
        };
        private static readonly Vector2[] NodePositions =
        {
            new Vector2(-340f, -62f),
            new Vector2(-40f, -62f),
            new Vector2(340f, -62f),
        };
        private static readonly Vector2 NodeSize = new Vector2(232f, 168f);
        private static readonly Vector2 AttackerPosition = new Vector2(0f, 120f);

        private enum Step { Ask, Packet, Reveal }

        private int index;
        private int correct;
        private int picked = -1;
        private Step step;
        private float stepTime;
        private readonly bool[] results = new bool[Attacks.Length];

        private RectTransform[] nodes;
        private Image[] nodeFaces;
        private Outline[] nodeEdges;
        private TMP_Text attackTitle, attackText, explainText;
        private RectTransform packet;

        protected override string MachineName => "NETWORK ATTACK";
        protected override string Tagline =>
            "Six attacks are heading for the fake company's network.\n" +
            "Click where each one lands: <color=#40CCFF><b>DEVICES</b></color>, the <color=#FFB840><b>ROUTER</b></color> or the <color=#B285FF><b>CLOUD</b></color>.";
        protected override string PlayKeys => "CLICK A TARGET  ·  OR PRESS 1 / 2 / 3";
        protected override Color Accent => new Color(1f, 0.55f, 0.18f);
        protected override Vector2 PanelSize => new Vector2(1060f, 700f);

        /// <summary>Test views.</summary>
        public int Index => index;
        public int Correct => correct;
        public bool Revealing => step == Step.Reveal;
        public Target CurrentTarget => Attacks[Mathf.Clamp(index, 0, Attacks.Length - 1)].target;

        // ---- Build ------------------------------------------------------------------

        protected override void BuildPlayfield(RectTransform field)
        {
            // A faint grid, like a network diagram on graph paper.
            for (int i = -9; i <= 9; i++)
                Box(field, "GridV" + i, new Color(1f, 0.55f, 0.18f, 0.04f), new Vector2(i * 56f, 0f), new Vector2(2f, 500f));
            for (int i = -4; i <= 4; i++)
                Box(field, "GridH" + i, new Color(1f, 0.55f, 0.18f, 0.04f), new Vector2(0f, i * 56f), new Vector2(1020f, 2f));

            // The attack card along the top.
            RectTransform cardRt = Box(field, "AttackCard", new Color(0.16f, 0.05f, 0.05f, 1f), new Vector2(0f, 188f), new Vector2(900f, 96f));
            var cardEdge = cardRt.gameObject.AddComponent<Outline>();
            cardEdge.effectColor = new Color(1f, 0.38f, 0.30f, 0.6f);
            cardEdge.effectDistance = new Vector2(2f, 2f);
            attackTitle = Label(cardRt, "Title", string.Empty, 26f, new Color(1f, 0.55f, 0.48f));
            attackTitle.rectTransform.offsetMin = new Vector2(20f, 50f);
            attackTitle.rectTransform.offsetMax = new Vector2(-20f, -6f);
            attackTitle.characterSpacing = 4f;
            attackTitle.enableWordWrapping = false;
            attackText = Label(cardRt, "What", string.Empty, 17f, Ink);
            attackText.rectTransform.offsetMin = new Vector2(24f, 6f);
            attackText.rectTransform.offsetMax = new Vector2(-24f, -46f);

            // Office boundary around the devices and the router.
            RectTransform office = Box(field, "Office", new Color(1f, 1f, 1f, 0.025f), new Vector2(-190f, -52f), new Vector2(580f, 236f));
            var officeEdge = office.gameObject.AddComponent<Outline>();
            officeEdge.effectColor = new Color(0.62f, 0.72f, 0.82f, 0.35f);
            officeEdge.effectDistance = new Vector2(1.5f, 1.5f);
            TMP_Text officeLabel = Label(office, "Label", "OFFICE NETWORK", 13f, Dim, TextAlignmentOptions.TopLeft);
            officeLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
            officeLabel.rectTransform.offsetMax = new Vector2(0f, -6f);
            officeLabel.characterSpacing = 3f;

            // Links: devices to router, router out to the internet and the cloud.
            Color wire = new Color(0.55f, 0.66f, 0.78f, 0.7f);
            Box(field, "Link_DevicesRouter", wire, new Vector2(-190f, -62f), new Vector2(68f, 4f));
            Box(field, "Link_RouterCloud", wire, new Vector2(150f, -62f), new Vector2(148f, 4f));
            TMP_Text internet = Label(field, "Internet", "INTERNET", 13f, Dim);
            Place(internet.rectTransform, new Vector2(150f, -42f), new Vector2(140f, 22f));
            internet.characterSpacing = 3f;

            // The attacker's launch point, under the attack card.
            RectTransform attacker = Box(field, "Attacker", new Color(0.85f, 0.22f, 0.18f, 1f), AttackerPosition, new Vector2(22f, 22f));
            attacker.localRotation = Quaternion.Euler(0f, 0f, 45f);

            nodes = new RectTransform[3];
            nodeFaces = new Image[3];
            nodeEdges = new Outline[3];
            string[] subtitles = { "Staff laptops and phones", "Links the office to the internet", "Rented storage and apps" };
            for (int i = 0; i < 3; i++) BuildNode(field, i, subtitles[i]);

            packet = Box(field, "Packet", new Color(1f, 0.30f, 0.24f, 1f), AttackerPosition, new Vector2(16f, 16f));
            packet.gameObject.SetActive(false);

            explainText = Label(field, "Explain", string.Empty, 17f, Ink);
            Place(explainText.rectTransform, new Vector2(0f, -205f), new Vector2(940f, 60f));

            ShowAttack(0);
        }

        private void BuildNode(RectTransform field, int i, string subtitle)
        {
            Color color = TargetColors[i];
            RectTransform node = Box(field, "Node_" + TargetNames[i], new Color(0.05f, 0.07f, 0.11f, 1f), NodePositions[i], NodeSize);
            Image face = node.GetComponent<Image>();
            face.raycastTarget = true;
            var edge = node.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(color.r, color.g, color.b, 0.75f);
            edge.effectDistance = new Vector2(2f, 2f);
            var button = node.gameObject.AddComponent<Button>();
            int captured = i;
            button.onClick.AddListener(() => Pick(captured));
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            button.colors = colors;
            UiMotion.AddPress(button);

            // Number badge.
            RectTransform badge = Box(node, "Badge", color, new Vector2(-NodeSize.x * 0.5f + 20f, NodeSize.y * 0.5f - 20f), new Vector2(28f, 28f));
            TMP_Text num = Label(badge, "Num", "<b>" + (i + 1) + "</b>", 18f, new Color(0.04f, 0.05f, 0.08f));
            num.enableWordWrapping = false;

            BuildIcon(node, (Target)i, color);

            TMP_Text title = Label(node, "Title", "<b>" + TargetNames[i] + "</b>", 17f, color);
            Place(title.rectTransform, new Vector2(0f, -38f), new Vector2(NodeSize.x - 16f, 24f));
            title.characterSpacing = 2f;
            title.enableWordWrapping = false;
            TMP_Text sub = Label(node, "Sub", subtitle, 13f, Dim);
            Place(sub.rectTransform, new Vector2(0f, -62f), new Vector2(NodeSize.x - 16f, 22f));

            nodes[i] = node;
            nodeFaces[i] = face;
            nodeEdges[i] = edge;
        }

        /// <summary>A small flat icon made of boxes.</summary>
        private static void BuildIcon(RectTransform node, Target target, Color color)
        {
            Color dark = new Color(0.04f, 0.05f, 0.08f, 1f);
            Vector2 c = new Vector2(0f, 26f);
            switch (target)
            {
                case Target.Endpoint:
                    // Laptop and phone.
                    Box(node, "LaptopLid", color, c + new Vector2(-18f, 10f), new Vector2(70f, 46f));
                    Box(node, "LaptopScreen", dark, c + new Vector2(-18f, 12f), new Vector2(60f, 36f));
                    Box(node, "LaptopBase", color, c + new Vector2(-18f, -18f), new Vector2(88f, 8f));
                    Box(node, "Phone", color, c + new Vector2(44f, -2f), new Vector2(24f, 42f));
                    Box(node, "PhoneScreen", dark, c + new Vector2(44f, 0f), new Vector2(18f, 32f));
                    break;
                case Target.Router:
                    Box(node, "Body", color, c + new Vector2(0f, -10f), new Vector2(96f, 30f));
                    for (int a = -1; a <= 1; a++)
                        Box(node, "Antenna" + a, color, c + new Vector2(a * 34f, 18f), new Vector2(5f, 30f))
                            .localRotation = Quaternion.Euler(0f, 0f, -a * 12f);
                    for (int l = 0; l < 4; l++)
                        Box(node, "Light" + l, l == 0 ? new Color(0.30f, 0.95f, 0.55f) : dark, c + new Vector2(-30f + l * 16f, -10f), new Vector2(8f, 8f));
                    break;
                default:
                    // A blocky cloud over a little server stack.
                    Box(node, "CloudA", color, c + new Vector2(-24f, 12f), new Vector2(46f, 30f));
                    Box(node, "CloudB", color, c + new Vector2(8f, 20f), new Vector2(50f, 42f));
                    Box(node, "CloudC", color, c + new Vector2(36f, 8f), new Vector2(38f, 24f));
                    Box(node, "CloudBase", color, c + new Vector2(4f, 2f), new Vector2(104f, 16f));
                    Box(node, "Rack1", new Color(0.55f, 0.62f, 0.72f, 1f), c + new Vector2(4f, -22f), new Vector2(60f, 10f));
                    Box(node, "Rack2", new Color(0.55f, 0.62f, 0.72f, 1f), c + new Vector2(4f, -35f), new Vector2(60f, 10f));
                    break;
            }
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
            ShowAttack(0);
            Flash("Read the attack, then click the part of the network it hits.", 4f);
        }

        protected override void TickPlay(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);
            stepTime += dt;
            switch (step)
            {
                case Step.Ask:
                    if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Pick(0);
                    else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Pick(1);
                    else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Pick(2);
                    break;
                case Step.Packet:
                    // The attack travels from the attacker to where it lands.
                    float t = Mathf.Clamp01(stepTime / PacketSeconds);
                    Vector2 to = NodePositions[(int)Attacks[index].target] + new Vector2(0f, NodeSize.y * 0.5f);
                    packet.anchoredPosition = Vector2.Lerp(AttackerPosition, to, UiMotion.InOutCubic(t));
                    if (t >= 1f) Land();
                    break;
                case Step.Reveal:
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                        Input.GetKeyDown(KeyCode.KeypadEnter)) Next();
                    break;
            }
        }

        private void Pick(int target)
        {
            if (Phase != State.Playing || step != Step.Ask || target < 0 || target > 2) return;
            picked = target;
            bool right = target == (int)Attacks[index].target;
            results[index] = right;
            if (right) correct++;
            if (!right)
            {
                UiMotion.Shake(nodes[target], 7f);
                nodeFaces[target].color = new Color(0.16f, 0.17f, 0.20f, 1f);
            }
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            step = Step.Packet;
            stepTime = 0f;
            packet.gameObject.SetActive(true);
            packet.anchoredPosition = AttackerPosition;
            RefreshStatus();
        }

        private void Land()
        {
            Attack a = Attacks[index];
            int hit = (int)a.target;
            bool right = picked == hit;
            packet.gameObject.SetActive(false);
            step = Step.Reveal;
            stepTime = 0f;
            nodeFaces[hit].color = new Color(0.36f, 0.09f, 0.07f, 1f);
            nodeEdges[hit].effectColor = new Color(1f, 0.35f, 0.28f, 1f);
            nodeEdges[hit].effectDistance = new Vector2(4f, 4f);
            UiMotion.Pulse(nodes[hit], 0.08f);
            if (Sfx.Instance != null)
            {
                if (right) Sfx.Instance.PlayConfirm();
                else Sfx.Instance.PlayDeny();
            }
            string lead = right
                ? "<color=#4CE087><b>RIGHT!</b></color>  "
                : "<color=#FF8A6A><b>NOT QUITE.</b></color>  ";
            explainText.text = lead + "<b>" + a.name + "</b> lands on the <b>" + TargetNames[hit] + "</b>.\n" +
                "<color=#FFC838>Defense:</color> " + a.defense;
            Flash("<color=#9FB4C4>SPACE NEXT</color>", 60f);
            RefreshStatus();
        }

        private void Next()
        {
            if (step != Step.Reveal) return;
            if (index + 1 >= Attacks.Length) { End(); return; }
            ShowAttack(index + 1);
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
        }

        private void End()
        {
            if (Phase != State.Playing) return;
            int tickets = correct * TicketsPerCorrect + (correct == Attacks.Length ? PerfectBonus : 0);
            bool won = correct >= CorrectToWin;
            Finish(won, tickets, won ? "NETWORK MAPPED" : "KEEP PRACTICING",
                $"You placed {correct} of {Attacks.Length} attacks.\n" +
                "Real attacks chain together: a phished laptop leads to more systems, then to the data in the cloud.\n" +
                "<color=#FFC838>Each point needs its own defense: protected devices, a locked-down router, and vendors you've checked.</color>");
        }

        // ---- Drawing ----------------------------------------------------------------

        private void ShowAttack(int i)
        {
            index = Mathf.Clamp(i, 0, Attacks.Length - 1);
            step = Step.Ask;
            stepTime = 0f;
            picked = -1;
            Attack a = Attacks[index];
            attackTitle.text = "<b>INCOMING:  " + a.name + "</b>";
            attackText.text = a.what;
            explainText.text = "<color=#9FB4C4>Where does this attack land?</color>";
            for (int n = 0; n < 3; n++)
            {
                Color color = TargetColors[n];
                nodeFaces[n].color = new Color(0.05f, 0.07f, 0.11f, 1f);
                nodeEdges[n].effectColor = new Color(color.r, color.g, color.b, 0.75f);
                nodeEdges[n].effectDistance = new Vector2(2f, 2f);
            }
            if (packet != null) packet.gameObject.SetActive(false);
            Flash(string.Empty, 0.01f);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            var dots = new System.Text.StringBuilder();
            int answered = index + (step == Step.Ask ? 0 : 1);
            for (int i = 0; i < Attacks.Length; i++)
                dots.Append(i >= answered ? "<color=#3A4654>●</color> "
                    : results[i] ? "<color=#4CE087>●</color> " : "<color=#FF6A60>●</color> ");
            SetStatus($"ATTACK {index + 1} OF {Attacks.Length}   ·   {dots}  ·   RIGHT  {correct}");
        }

        // ---- Test hooks -----------------------------------------------------------------

        protected override void ResolveForTest(bool win)
        {
            correct = win ? Attacks.Length : 0;
            index = Attacks.Length - 1;
            step = Step.Reveal;
            End();
        }

        /// <summary>Test hook: click a target (0 devices, 1 router, 2 cloud).</summary>
        public void PickForTest(int target) => Pick(target);

        /// <summary>Test hook: skip the packet's flight.</summary>
        public void LandForTest()
        {
            if (step == Step.Packet) Land();
        }

        /// <summary>Test hook: move on to the next attack.</summary>
        public void NextForTest() => Next();
    }
}
