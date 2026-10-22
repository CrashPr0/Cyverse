using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;

namespace Cyverse.UI
{
    /// <summary>
    /// Hard-mode mini-game: a firewall whack-a-mole. A 3x3 grid of live
    /// sessions; the intruder's login keeps popping up in tiles among real
    /// coworkers, and the player kicks it with that tile's number key (1-9,
    /// read left to right, top to bottom). Kick it <see cref="KicksToWin"/>
    /// times before the clock runs out to lock it out. Kicking a coworker
    /// costs a kick. Running out of time restarts the game; Esc gives up and
    /// hands control back.
    ///
    /// Drawn on the HUD canvas over everything else, and timed with unscaled
    /// time because the modal it runs inside may pause the game clock.
    /// </summary>
    public class SessionWhack : MonoBehaviour
    {
        public const int KicksToWin = 8;
        private const float RoundSeconds = 25f;
        private const float IntruderChance = 0.62f;
        private const int PointsPerKick = 20;

        private static readonly Color Intruder = new Color(1f, 0.36f, 0.32f);
        private static readonly Color Coworker = new Color(0.45f, 0.80f, 1f);
        private static readonly Color Empty = new Color(0.07f, 0.09f, 0.13f, 0.95f);

        private enum Tile { Empty, Intruder, Coworker }

        private readonly Tile[] tiles = new Tile[9];
        private readonly float[] tileLeft = new float[9];
        private readonly Image[] tileImages = new Image[9];
        private readonly TMP_Text[] tileTexts = new TMP_Text[9];

        private string intruder, origin;
        private string[] coworkers;
        private Action onWin, onQuit;

        private GameObject panel;
        private TMP_Text statusText, feedbackText;
        private RectTransform timerBar;

        private float timeLeft, untilSpawn, feedbackLeft, restartIn;
        private int kicks;
        private bool running, finished;

        public static bool IsPlaying { get; private set; }

        /// <summary>Starts the game. <paramref name="won"/> runs once the intruder
        /// is locked out; <paramref name="quit"/> if the player presses Esc.</summary>
        public static SessionWhack Play(string intruderUser, string intruderOrigin,
            string[] coworkerNames, Action won, Action quit)
        {
            var game = new GameObject("SessionWhack").AddComponent<SessionWhack>();
            game.intruder = intruderUser;
            game.origin = intruderOrigin;
            game.coworkers = coworkerNames != null && coworkerNames.Length > 0
                ? coworkerNames : new[] { "m.silva", "sysadmin", "intern-02" };
            game.onWin = won;
            game.onQuit = quit;
            game.Build();
            game.Begin();
            IsPlaying = true;
            return game;
        }

        private void Begin()
        {
            for (int i = 0; i < 9; i++) { tiles[i] = Tile.Empty; Paint(i); }
            kicks = 0;
            timeLeft = RoundSeconds;
            untilSpawn = 0.6f;
            restartIn = 0f;
            running = true;
            Feedback($"Kick <color=#FF6A60><b>{intruder}</b></color> every time it shows up. Leave your coworkers alone.", 3f);
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

            timeLeft -= dt;
            if (timerBar != null) timerBar.localScale = new Vector3(Mathf.Clamp01(timeLeft / RoundSeconds), 1f, 1f);
            if (timeLeft <= 0f) { TimeUp(); return; }

            // It speeds up as the clock runs down.
            float pace = 1f - timeLeft / RoundSeconds;
            for (int i = 0; i < 9; i++)
            {
                if (tiles[i] == Tile.Empty) continue;
                if ((tileLeft[i] -= dt) <= 0f) { tiles[i] = Tile.Empty; Paint(i); }
            }
            if ((untilSpawn -= dt) <= 0f)
            {
                Spawn(Mathf.Lerp(1.5f, 0.85f, pace));
                untilSpawn = Mathf.Lerp(0.85f, 0.45f, pace);
            }

            for (int i = 0; i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    Kick(i);
        }

        private void Spawn(float life)
        {
            int start = UnityEngine.Random.Range(0, 9);
            for (int n = 0; n < 9; n++)
            {
                int i = (start + n) % 9;
                if (tiles[i] != Tile.Empty) continue;
                tiles[i] = UnityEngine.Random.value < IntruderChance ? Tile.Intruder : Tile.Coworker;
                tileLeft[i] = life;
                if (tiles[i] == Tile.Coworker)
                    tileTexts[i].text = $"<size=16>{i + 1}</size>\n{coworkers[UnityEngine.Random.Range(0, coworkers.Length)]}";
                Paint(i);
                return;
            }
        }

        private void Kick(int i)
        {
            if (tiles[i] == Tile.Intruder)
            {
                kicks++;
                ScoreSystem.Add(PointsPerKick);
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                tiles[i] = Tile.Empty;
                Paint(i);
                RefreshStatus();
                if (kicks >= KicksToWin) Win();
            }
            else if (tiles[i] == Tile.Coworker)
            {
                kicks = Mathf.Max(0, kicks - 1);
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                Feedback("<color=#FFB070><b>That was a coworker.</b></color>  Kicking real users is a denial of service.", 2f);
                tiles[i] = Tile.Empty;
                Paint(i);
                RefreshStatus();
            }
            else if (Sfx.Instance != null)
            {
                Sfx.Instance.PlayClick();
            }
        }

        private void TimeUp()
        {
            running = false;
            restartIn = 2f;
            for (int i = 0; i < 9; i++) { tiles[i] = Tile.Empty; Paint(i); }
            if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            Feedback($"<color=#FF6A60><b>{intruder} is still logged in.</b></color>  Again…", 2f);
        }

        private void Win()
        {
            running = false;
            for (int i = 0; i < 9; i++) { tiles[i] = Tile.Empty; Paint(i); }
            Feedback("<color=#4CE087><b>LOCKED OUT.</b></color>  Session killed, password reset forced.", 2f);
            if (Sfx.Instance != null) Sfx.Instance.PlayTaskComplete();
            finished = true;
            StartCoroutine(FinishWin());
        }

        // Realtime: the modal this runs inside may have paused the game clock.
        private System.Collections.IEnumerator FinishWin()
        {
            yield return new WaitForSecondsRealtime(1.2f);
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

        // ---- Drawing ------------------------------------------------------------------

        private void Paint(int i)
        {
            if (tileImages[i] == null) return;
            switch (tiles[i])
            {
                case Tile.Intruder:
                    tileImages[i].color = new Color(0.30f, 0.06f, 0.06f, 0.97f);
                    tileTexts[i].text = $"<size=16>{i + 1}</size>\n<b>{intruder}</b>\n<size=15>{origin}</size>";
                    tileTexts[i].color = Intruder;
                    break;
                case Tile.Coworker:
                    tileImages[i].color = new Color(0.06f, 0.14f, 0.22f, 0.97f);
                    tileTexts[i].color = Coworker;
                    break;
                default:
                    tileImages[i].color = Empty;
                    tileTexts[i].text = $"<size=16>{i + 1}</size>";
                    tileTexts[i].color = new Color(0.35f, 0.42f, 0.52f);
                    break;
            }
        }

        private void RefreshStatus()
        {
            if (statusText != null)
                statusText.text = $"KICKED  <b>{kicks}</b> / {KicksToWin}";
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
            Color red = Intruder;

            panel = new GameObject("SessionWhackPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            panel.transform.SetAsLastSibling();
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 600f);
            HudUI.StylePanel(panel, new Color(0.03f, 0.04f, 0.06f, 0.96f), red);

            Text("Title", panel.transform, 26, new Vector2(0f, -14f), 34f).text =
                "<b>FIREWALL: KICK THE INTRUDER</b>";
            Text("Who", panel.transform, 18, new Vector2(0f, -50f), 26f).text =
                $"<color=#FF6A60>{intruder}</color>  ·  {origin}  ·  press the tile's number to kill its session";
            statusText = Text("Status", panel.transform, 20, new Vector2(0f, -80f), 28f);

            const float size = 150f, gap = 12f;
            for (int i = 0; i < 9; i++)
            {
                int col = i % 3, row = i / 3;
                var tile = new GameObject("Tile_" + (i + 1), typeof(RectTransform), typeof(Image));
                tile.transform.SetParent(panel.transform, false);
                var t = tile.GetComponent<RectTransform>();
                t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
                t.pivot = new Vector2(0.5f, 0.5f);
                t.sizeDelta = new Vector2(size, size * 0.62f);
                t.anchoredPosition = new Vector2((col - 1) * (size + gap), 90f - row * (size * 0.62f + gap));
                tileImages[i] = tile.GetComponent<Image>();
                tileImages[i].raycastTarget = false;

                var label = Text("Label", tile.transform, 19, Vector2.zero, size * 0.62f);
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

            var bar = new GameObject("Timer", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(panel.transform, false);
            timerBar = bar.GetComponent<RectTransform>();
            timerBar.anchorMin = new Vector2(0f, 0f);
            timerBar.anchorMax = new Vector2(1f, 0f);
            timerBar.pivot = new Vector2(0f, 0f);
            timerBar.sizeDelta = new Vector2(-24f, 8f);
            timerBar.anchoredPosition = new Vector2(12f, 12f);
            var img = bar.GetComponent<Image>();
            img.color = red;
            img.raycastTarget = false;

            Text("Keys", panel.transform, 15, new Vector2(0f, -540f), 22f).text =
                "<color=#607585>1-9 kick  ·  ESC give up</color>";
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
