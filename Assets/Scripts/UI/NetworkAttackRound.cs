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
    ///
    /// The full office network map (the one on the pinball backglass) opens
    /// when play starts and any time after with M or the NETWORK MAP button,
    /// so players can see where devices, routers and the cloud sit.
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
        private GameObject map;

        protected override string MachineName => "NETWORK ATTACK";
        protected override string Tagline =>
            "Six attacks are heading for the fake company's network.\n" +
            "Click where each one lands: <color=#40CCFF><b>DEVICES</b></color>, the <color=#FFB840><b>ROUTER</b></color> or the <color=#B285FF><b>CLOUD</b></color>.\n" +
            "<color=#9FB4C4>Press <b>M</b> any time to see the full network map.</color>";
        protected override string PlayKeys => "CLICK A TARGET  ·  OR PRESS 1 / 2 / 3  ·  M NETWORK MAP";
        protected override Color Accent => new Color(1f, 0.55f, 0.18f);
        protected override Vector2 PanelSize => new Vector2(1060f, 700f);

        /// <summary>Test views.</summary>
        public int Index => index;
        public int Correct => correct;
        public bool Revealing => step == Step.Reveal;
        public Target CurrentTarget => Attacks[Mathf.Clamp(index, 0, Attacks.Length - 1)].target;
        public bool MapOpen => map != null && map.activeSelf;

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
            TMP_Text officeLabel = Label(office, "Label", "OFFICE NETWORK", 15f, new Color(0.66f, 0.74f, 0.82f), TextAlignmentOptions.TopLeft);
            officeLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
            officeLabel.rectTransform.offsetMax = new Vector2(0f, -6f);
            officeLabel.characterSpacing = 3f;

            // Links: devices to router, router out to the internet and the cloud.
            Color wire = new Color(0.55f, 0.66f, 0.78f, 0.7f);
            Box(field, "Link_DevicesRouter", wire, new Vector2(-190f, -62f), new Vector2(68f, 4f));
            Box(field, "Link_RouterCloud", wire, new Vector2(150f, -62f), new Vector2(148f, 4f));
            TMP_Text internet = Label(field, "Internet", "INTERNET", 15f, new Color(0.66f, 0.74f, 0.82f));
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

            // Button for the full network map, in the open space above the cloud.
            RectTransform mapButton = Box(field, "MapButton", new Color(0.10f, 0.13f, 0.19f, 1f), new Vector2(372f, 92f), new Vector2(220f, 40f));
            mapButton.GetComponent<Image>().raycastTarget = true;
            var mapEdge = mapButton.gameObject.AddComponent<Outline>();
            mapEdge.effectColor = new Color(0.62f, 0.72f, 0.82f, 0.6f);
            mapEdge.effectDistance = new Vector2(1.5f, 1.5f);
            var mapClick = mapButton.gameObject.AddComponent<Button>();
            mapClick.onClick.AddListener(() => ShowMap(true));
            UiMotion.AddPress(mapClick);
            TMP_Text mapLabel = Label(mapButton, "Label", "<b>NETWORK MAP</b>  <color=#9FB4C4>[M]</color>", 16f, Ink);
            mapLabel.characterSpacing = 2f;
            mapLabel.enableWordWrapping = false;

            BuildMap(field.parent);

            ShowAttack(0);
        }

        /// <summary>The full network map, drawn over the whole round so its
        /// labels can be read. Hidden until opened.</summary>
        private void BuildMap(Transform panel)
        {
            map = new GameObject("NetworkMap", typeof(RectTransform), typeof(Image), typeof(Button));
            map.transform.SetParent(panel, false);
            var rt = map.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1780f, 960f);
            rt.anchoredPosition = Vector2.zero;
            HudUI.StylePanel(map, new Color(0.035f, 0.025f, 0.05f, 0.99f), Accent);
            map.GetComponent<Image>().raycastTarget = true;
            map.GetComponent<Button>().onClick.AddListener(() => ShowMap(false));

            TMP_Text title = Label(map.transform, "Title", "<b>THE COMPANY NETWORK</b>", 30f, Accent);
            Place(title.rectTransform, new Vector2(0f, 438f), new Vector2(1700f, 44f));
            title.characterSpacing = 6f;
            title.enableWordWrapping = false;

            // Drawn from code (NetworkDiagramArt), shared with the pinball backglass.
            Texture2D picture = NetworkDiagram.Texture;
            float width = 1720f;
            float height = width / NetworkDiagram.Aspect;
            var imageGo = new GameObject("Diagram", typeof(RectTransform), typeof(RawImage));
            imageGo.transform.SetParent(map.transform, false);
            var raw = imageGo.GetComponent<RawImage>();
            raw.texture = picture;
            raw.color = picture != null ? Color.white : new Color(0.1f, 0.1f, 0.12f);
            raw.raycastTarget = false;
            Place(raw.rectTransform, new Vector2(0f, 6f), new Vector2(width, height));

            TMP_Text key = Label(map.transform, "Key",
                "<color=#40CCFF><b>DEVICES</b></color> = PCs, laptops, printers and IoT on the LAN     " +
                "<color=#FFB840><b>ROUTER</b></color> = the LAN gateway and WAN router     " +
                "<color=#B285FF><b>CLOUD</b></color> = the cloud network and the databases",
                19f, Ink);
            Place(key.rectTransform, new Vector2(0f, -412f), new Vector2(1720f, 30f));
            key.enableWordWrapping = false;
            TMP_Text close = Label(map.transform, "Close", "M, SPACE OR CLICK TO CLOSE", 16f, Dim);
            Place(close.rectTransform, new Vector2(0f, -448f), new Vector2(1720f, 24f));
            close.characterSpacing = 3f;

            map.SetActive(false);
        }

        private void ShowMap(bool show)
        {
            if (map == null || map.activeSelf == show) return;
            if (show && Phase != State.Playing) return;
            map.SetActive(show);
            map.transform.SetAsLastSibling();
            if (show) UiMotion.PanelIn(map, null, 0.98f);
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
        }

        protected override bool HandleCancel()
        {
            if (!MapOpen) return false;
            ShowMap(false);
            return true;
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
            TMP_Text sub = Label(node, "Sub", subtitle, 15f, new Color(0.66f, 0.74f, 0.82f));
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
            // Start on the full map so players see the network before the attacks.
            ShowMap(true);
        }

        protected override void TickPlay(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);
            if (MapOpen)
            {
                if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Space) ||
                    Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    ShowMap(false);
                return;
            }
            if (Input.GetKeyDown(KeyCode.M) && step != Step.Packet) { ShowMap(true); return; }
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
            ShowMap(false);
            picked = target;
            bool right = target == (int)Attacks[index].target;
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
            results[index] = right;
            if (right) correct++;
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
            ShowMap(false);
            Flash(string.Empty, 0.01f);
            explainText.text = string.Empty;
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
            int answered = index + (step == Step.Reveal ? 1 : 0);
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

        /// <summary>Test hook: open or close the network map.</summary>
        public void ShowMapForTest(bool show) => ShowMap(show);
    }
}
