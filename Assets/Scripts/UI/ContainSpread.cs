using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;

namespace Cyverse.UI
{
    /// <summary>
    /// Level 2 Hard mini-game: contain the spread. A 3x3 map of the office
    /// network, wired to its neighbours left, right, up and down. Malware
    /// starts on the compromised workstation and, every few seconds, hops to
    /// one clean machine next to an infected one. The player isolates a
    /// machine with its number key (1-9, left to right, top to bottom).
    /// Isolate every infected machine to contain it. If it reaches the file
    /// server the map resets. Isolating a clean machine takes it offline for
    /// nothing and costs points. Esc gives up and hands control back.
    ///
    /// Paced gently on purpose: one hop at a time, with time to read the map.
    /// Drawn on the HUD canvas, timed with unscaled time (the modal it runs
    /// inside may pause the game clock).
    /// </summary>
    public class ContainSpread : MonoBehaviour
    {
        public const int FileServer = 8;      // key 9, bottom right
        public const int PatientZero = 4;     // key 5, centre: WS-03
        /// <summary>Where it has already reached when the player catches it:
        /// WS-03 itself, WS-04 and WS-02.</summary>
        public static readonly int[] StartInfected = { PatientZero, 3, 1 };

        public const float FirstHopSeconds = 3f;
        public const float HopSeconds = 2.8f;
        /// <summary>Cutting a machine off takes a moment, so the order matters:
        /// isolate whatever is next to the file server first.</summary>
        public const float IsolateLockSeconds = 1f;
        public const int PointsPerIsolate = 15;
        public const int CleanIsolatePenalty = 10;

        public static readonly string[] MachineNames =
        {
            "WS-01", "WS-02", "PRINTER",
            "WS-04", "WS-03", "MAIL SERVER",
            "WS-05", "WS-06", "FILE SERVER",
        };

        private enum Node { Clean, Infected, Isolated, IsolatedInfected }

        private readonly Node[] nodes = new Node[9];
        private readonly Image[] tileImages = new Image[9];
        private readonly TMP_Text[] tileTexts = new TMP_Text[9];

        private Action onWin, onQuit;
        private GameObject panel;
        private TMP_Text statusText, feedbackText;
        private RectTransform hopBar;

        private float untilHop, lockLeft, feedbackLeft, restartIn;
        private bool running, finished;

        public static bool IsPlaying { get; private set; }
        public bool Won { get; private set; }
        public bool FileServerHit { get; private set; }

        public static ContainSpread Play(Action won, Action quit)
        {
            var game = new GameObject("ContainSpread").AddComponent<ContainSpread>();
            game.onWin = won;
            game.onQuit = quit;
            game.Build();
            game.Begin();
            IsPlaying = true;
            return game;
        }

        // ---- Rules (public so tests can drive them without key presses) ----------

        public bool IsInfected(int i) => nodes[i] == Node.Infected;
        public bool IsIsolated(int i) => nodes[i] == Node.Isolated || nodes[i] == Node.IsolatedInfected;

        public int ActiveInfections
        {
            get { int n = 0; for (int i = 0; i < 9; i++) if (nodes[i] == Node.Infected) n++; return n; }
        }

        public static bool AreNeighbours(int a, int b)
        {
            int ar = a / 3, ac = a % 3, br = b / 3, bc = b % 3;
            return Mathf.Abs(ar - br) + Mathf.Abs(ac - bc) == 1;
        }

        /// <summary>Takes machine <paramref name="i"/> off the network.</summary>
        public void Isolate(int i)
        {
            if (!running || finished || i < 0 || i >= 9) return;
            if (i == FileServer)
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                Feedback("<color=#FFB070><b>The file server runs the business.</b></color>  It stays online: stop the malware before it gets there.", 2.5f);
                return;
            }
            if (IsIsolated(i)) { if (Sfx.Instance != null) Sfx.Instance.PlayClick(); return; }

            lockLeft = IsolateLockSeconds;
            if (nodes[i] == Node.Infected)
            {
                nodes[i] = Node.IsolatedInfected;
                ScoreSystem.Add(PointsPerIsolate);
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            }
            else
            {
                nodes[i] = Node.Isolated;
                ScoreSystem.Add(-CleanIsolatePenalty);
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                Feedback($"<color=#FFB070><b>{MachineNames[i]} was clean.</b></color>  Its user just lost their connection for nothing.  −{CleanIsolatePenalty}", 2.5f);
            }
            Paint(i);
            RefreshStatus();
            if (ActiveInfections == 0) Win();
        }

        /// <summary>One hop: a random infected, connected machine infects one
        /// clean, connected neighbour. Returns the machine it reached, or -1.</summary>
        public int Hop()
        {
            int choices = 0, pick = -1;
            for (int from = 0; from < 9; from++)
            {
                if (nodes[from] != Node.Infected) continue;
                for (int to = 0; to < 9; to++)
                {
                    if (nodes[to] != Node.Clean || !AreNeighbours(from, to)) continue;
                    // Reservoir pick: every open link equally likely.
                    choices++;
                    if (UnityEngine.Random.Range(0, choices) == 0) pick = to;
                }
            }
            if (pick < 0) return -1;
            nodes[pick] = Node.Infected;
            Paint(pick);
            RefreshStatus();
            if (pick == FileServer) ServerLost();
            return pick;
        }

        // ---- Flow ----------------------------------------------------------------

        private void Begin()
        {
            for (int i = 0; i < 9; i++) nodes[i] = Node.Clean;
            foreach (int i in StartInfected) nodes[i] = Node.Infected;
            for (int i = 0; i < 9; i++) Paint(i);
            untilHop = FirstHopSeconds;
            lockLeft = 0f;
            running = true;
            FileServerHit = false;
            Feedback("Isolate every <color=#FF6A60><b>infected</b></color> machine before it reaches the <b>FILE SERVER</b>. Start with the ones closest to it.", 4f);
            RefreshStatus();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (feedbackLeft > 0f && (feedbackLeft -= dt) <= 0f && feedbackText != null) feedbackText.text = "";

            if (finished) return;
            if (GameInput.CancelDown) { End(false); return; }

            if (!running)
            {
                if ((restartIn -= dt) <= 0f) Begin();
                return;
            }

            untilHop -= dt;
            if (hopBar != null) hopBar.localScale = new Vector3(Mathf.Clamp01(untilHop / HopSeconds), 1f, 1f);
            if (untilHop <= 0f)
            {
                untilHop = HopSeconds;
                Hop();
                if (!running) return;
            }

            if (lockLeft > 0f)
            {
                if ((lockLeft -= dt) <= 0f) RefreshStatus();
                return;
            }
            for (int i = 0; i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    Isolate(i);
                    break;
                }
        }

        private void ServerLost()
        {
            running = false;
            FileServerHit = true;
            restartIn = 2.5f;
            if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            Feedback("<color=#FF6A60><b>The file server is encrypted.</b></color>  Restoring the network… try again.", 2.5f);
        }

        private void Win()
        {
            running = false;
            finished = true;
            Won = true;
            Feedback("<color=#4CE087><b>CONTAINED.</b></color>  Every infected machine is off the network.", 2f);
            if (Sfx.Instance != null) Sfx.Instance.PlayTaskComplete();
            if (isActiveAndEnabled) StartCoroutine(FinishWin());
            else End(true);
        }

        // Realtime: the modal this runs inside may have paused the game clock.
        private System.Collections.IEnumerator FinishWin()
        {
            yield return new WaitForSecondsRealtime(1.4f);
            End(true);
        }

        private void End(bool won)
        {
            finished = true;
            IsPlaying = false;
            var callback = won ? onWin : onQuit;
            if (panel != null) Destroy(panel);
            Destroy(gameObject);
            callback?.Invoke();
        }

        void OnDestroy()
        {
            if (panel != null) Destroy(panel);
            IsPlaying = false;
        }

        // ---- Drawing ----------------------------------------------------------------

        private void Paint(int i)
        {
            if (tileImages[i] == null) return;
            string name = MachineNames[i];
            string key = $"<size=16>{i + 1}</size>";
            switch (nodes[i])
            {
                case Node.Infected:
                    tileImages[i].color = new Color(0.32f, 0.06f, 0.06f, 0.97f);
                    tileTexts[i].color = new Color(1f, 0.42f, 0.38f);
                    tileTexts[i].text = $"{key}\n<b>{name}</b>\n<size=14>INFECTED</size>";
                    break;
                case Node.IsolatedInfected:
                    tileImages[i].color = new Color(0.10f, 0.10f, 0.12f, 0.97f);
                    tileTexts[i].color = new Color(0.55f, 0.85f, 0.60f);
                    tileTexts[i].text = $"{key}\n<b>{name}</b>\n<size=14>ISOLATED</size>";
                    break;
                case Node.Isolated:
                    tileImages[i].color = new Color(0.10f, 0.10f, 0.12f, 0.97f);
                    tileTexts[i].color = new Color(0.50f, 0.55f, 0.62f);
                    tileTexts[i].text = $"{key}\n<b>{name}</b>\n<size=14>OFFLINE (was clean)</size>";
                    break;
                default:
                    bool server = i == FileServer;
                    tileImages[i].color = server ? new Color(0.14f, 0.12f, 0.04f, 0.97f)
                                                 : new Color(0.06f, 0.14f, 0.22f, 0.97f);
                    tileTexts[i].color = server ? new Color(0.95f, 0.78f, 0.30f) : new Color(0.45f, 0.80f, 1f);
                    tileTexts[i].text = $"{key}\n<b>{name}</b>\n<size=14>{(server ? "PROTECT" : "clean")}</size>";
                    break;
            }
        }

        private void RefreshStatus()
        {
            if (statusText != null)
                statusText.text = lockLeft > 0f
                    ? "<color=#8FA3B5>ISOLATING…</color>"
                    : $"STILL SPREADING  <b>{ActiveInfections}</b>";
        }

        private void Feedback(string text, float seconds)
        {
            if (feedbackText == null) return;
            feedbackText.text = text;
            feedbackLeft = seconds;
        }

        private void Build()
        {
            if (HudUI.Instance == null) return;
            Color red = new Color(1f, 0.36f, 0.32f);

            panel = new GameObject("ContainSpreadPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            panel.transform.SetAsLastSibling();
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 600f);
            HudUI.StylePanel(panel, new Color(0.03f, 0.04f, 0.06f, 0.96f), red);

            Text("Title", panel.transform, 26, new Vector2(0f, -14f), 34f).text =
                "<b>CONTAINMENT: STOP THE SPREAD</b>";
            Text("How", panel.transform, 18, new Vector2(0f, -50f), 26f).text =
                "Press a machine's number to cut it off the network";
            statusText = Text("Status", panel.transform, 20, new Vector2(0f, -80f), 28f);

            const float size = 150f, gap = 12f;
            float h = size * 0.62f;

            // Network links between neighbours, drawn under the tiles.
            for (int i = 0; i < 9; i++)
            {
                int col = i % 3, row = i / 3;
                Vector2 at = TilePos(col, row, size, gap, h);
                if (col < 2) Link(at + new Vector2((size + gap) * 0.5f, 0f), new Vector2(gap + 8f, 4f));
                if (row < 2) Link(at - new Vector2(0f, (h + gap) * 0.5f), new Vector2(4f, gap + 8f));
            }

            for (int i = 0; i < 9; i++)
            {
                int col = i % 3, row = i / 3;
                var tile = new GameObject("Node_" + (i + 1), typeof(RectTransform), typeof(Image));
                tile.transform.SetParent(panel.transform, false);
                var t = tile.GetComponent<RectTransform>();
                t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 0.5f);
                t.sizeDelta = new Vector2(size, h);
                t.anchoredPosition = TilePos(col, row, size, gap, h);
                tileImages[i] = tile.GetComponent<Image>();
                tileImages[i].raycastTarget = false;

                var label = Text("Label", tile.transform, 18, Vector2.zero, h);
                var lr = label.rectTransform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = Vector2.zero;
                lr.anchoredPosition = Vector2.zero;
                label.alignment = TextAlignmentOptions.Center;
                label.enableWordWrapping = true;
                tileTexts[i] = label;
                Paint(i);
            }

            feedbackText = Text("Feedback", panel.transform, 18, new Vector2(0f, -470f), 50f);
            feedbackText.enableWordWrapping = true;

            var bar = new GameObject("NextHop", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(panel.transform, false);
            hopBar = bar.GetComponent<RectTransform>();
            hopBar.anchorMin = new Vector2(0f, 0f);
            hopBar.anchorMax = new Vector2(1f, 0f);
            hopBar.pivot = new Vector2(0f, 0f);
            hopBar.sizeDelta = new Vector2(-24f, 8f);
            hopBar.anchoredPosition = new Vector2(12f, 12f);
            var img = bar.GetComponent<Image>();
            img.color = red;
            img.raycastTarget = false;

            Text("Keys", panel.transform, 15, new Vector2(0f, -540f), 22f).text =
                "<color=#607585>1-9 isolate  ·  bar = next hop  ·  ESC give up</color>";
        }

        private static Vector2 TilePos(int col, int row, float size, float gap, float h) =>
            new Vector2((col - 1) * (size + gap), 90f - row * (h + gap));

        private void Link(Vector2 at, Vector2 sizeDelta)
        {
            var go = new GameObject("Link", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(panel.transform, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = sizeDelta;
            r.anchoredPosition = at;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.25f, 0.35f, 0.45f, 0.9f);
            img.raycastTarget = false;
        }

        private static TMP_Text Text(string name, Transform parent, float size, Vector2 pos, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.richText = true;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            t.color = Color.white;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(-24f, height);
            return t;
        }
    }
}
