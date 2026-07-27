using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cyverse.Core;

namespace Cyverse.UI
{
    /// <summary>
    /// End-of-level results card: score, knowledge-check accuracy, and time,
    /// with an optional [R] replay (when the scene can be reloaded — i.e. it's
    /// a saved scene in Build Settings, not an untitled editor scene).
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        public static ResultsScreen Instance { get; private set; }

        private GameObject card;
        private Text bodyText;
        private bool shown;
        private bool canReload;
        private bool canHub;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        /// <summary>
        /// Shows the results card. The trailing string params let a level
        /// customize the header, opening line, next-mission tease, and replay
        /// label; omitting them keeps Level 0's original text unchanged.
        /// </summary>
        public void Show(int score, int quizCorrect, int quizTotal, float seconds,
            string headerText = "LEVEL 0 COMPLETE",
            string grantedLine = "Access Granted — Level: Employee",
            string nextMissionText = "Level 1 — Cyber Defense  (in development)",
            string replaySuffix = "Level 0",
            int parScore = 550)
        {
            if (card == null) Build(headerText);

            var scene = SceneManager.GetActiveScene();
            canReload = scene.IsValid() && !string.IsNullOrEmpty(scene.name);
            string hub = Core.SceneCatalog.Preferred("Hub");
            canHub = Application.CanStreamedLevelBeLoaded(hub) && scene.name != hub;

            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);

            // Persistent best score (gamification: something to beat on replay).
            int best = PlayerPrefs.GetInt("cv_best", 0);
            bool newBest = score > best;
            if (newBest)
            {
                best = score;
                PlayerPrefs.SetInt("cv_best", best);
                PlayerPrefs.Save();
            }

            // Grade on a FRACTION of the level's par score, not an absolute
            // number. The thresholds used to be hardcoded to Level 0's 550,
            // so richer levels (I/AM pars 1100, Forensics 1800) handed out an
            // S for any run at all and the grade stopped meaning anything.
            float par = Mathf.Max(1, parScore);
            float pct = score / par;

            string grade = pct >= 0.95f ? "S" : pct >= 0.82f ? "A" : pct >= 0.68f ? "B" : "C";

            // Rank title — a second, more human-readable layer of feedback.
            string rank = pct >= 0.90f ? "Senior Security Agent"
                        : pct >= 0.72f ? "Security Specialist"
                        : pct >= 0.55f ? "Security Analyst"
                        : "Security Recruit";

            int percentile = PercentileFor(score, par);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=#4CE087><b>{grantedLine}</b></color>");
            sb.AppendLine($"Employee ID:  <b>{PlayerIdentity.Callsign}</b>");
            sb.AppendLine();
            sb.AppendLine($"Security Clearance Rating:  <color=#E5A823><b>{grade}</b></color>   Rank:  <color=#E5A823><b>{rank}</b></color>");
            sb.AppendLine();
            sb.AppendLine($"Final Score:  <b><color=#5BC8FF>{score}</color></b>  <size=20><color=#8FB8CC>/ {parScore} par</color></size>");
            sb.AppendLine($"Best Score:  {best}" + (newBest ? "  <color=#E5A823><b>NEW BEST!</b></color>" : ""));
            sb.AppendLine($"Best Streak:  {ScoreSystem.BestStreak} correct in a row");
            sb.AppendLine();
            sb.AppendLine($"Knowledge Check:  {quizCorrect} / {quizTotal} correct");
            sb.AppendLine($"Time:  {m}:{s:00}");
            sb.AppendLine($"<size=22>You scored better than <color=#5BC8FF><b>{percentile}%</b></color> of recruits</size>");
            if (!string.IsNullOrEmpty(nextMissionText))
            {
                sb.AppendLine();
                sb.AppendLine($"<size=20><color=#5BC8FF>NEXT MISSION:</color> <color=#8FB8CC>{nextMissionText}</color></size>");
            }
            if (canReload || canHub)
            {
                sb.AppendLine();
                string keys = "";
                if (canReload) keys += $"[R]  Replay {replaySuffix}";
                if (canReload && canHub) keys += "      ";
                if (canHub) keys += "[H]  Return to Hub";
                sb.AppendLine($"<size=20><color=#8FB8CC>{keys}</color></size>");
            }
            bodyText.text = sb.ToString();

            card.SetActive(true);
            shown = true;
        }

        void Update()
        {
            if (!shown) return;
            if (canReload && Input.GetKeyDown(KeyCode.R))
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            else if (canHub && Input.GetKeyDown(KeyCode.H))
            {
                if (ScreenFader.Instance != null)
                    ScreenFader.Instance.FadeToBlackThen(() => SceneManager.LoadScene(Core.SceneCatalog.Preferred("Hub")));
                else
                    SceneManager.LoadScene(Core.SceneCatalog.Preferred("Hub"));
            }
        }

        /// <summary>
        /// Serverless "you scored better than X% of recruits": models a
        /// bell-curve of scores relative to the level's par rather than
        /// calling out to a leaderboard. Clamped to
        /// 1–99 so the message always reads as encouragement, never as "worst
        /// of everyone."
        /// </summary>
        private static int PercentileFor(int score, float par)
        {
            // Curve scales with the level: an average recruit lands near 55%
            // of par, one standard deviation is ~17% of it.
            float mean = par * 0.55f, stdDev = Mathf.Max(1f, par * 0.17f);
            float z = (score - mean) / stdDev;
            float cdf = NormalCdf(z);
            return Mathf.Clamp(Mathf.RoundToInt(cdf * 100f), 1, 99);
        }

        /// <summary>Abramowitz-Stegun approximation of the standard normal CDF.</summary>
        private static float NormalCdf(float z)
        {
            float t = 1f / (1f + 0.2316419f * Mathf.Abs(z));
            float d = 0.3989423f * Mathf.Exp(-z * z / 2f);
            float prob = d * t * (0.3193815f + t * (-0.3565638f + t * (1.781478f + t * (-1.821256f + t * 1.330274f))));
            return z > 0f ? 1f - prob : prob;
        }

        private void Build(string headerText)
        {
            var canvas = HudUI.Instance != null ? HudUI.Instance.Canvas.transform : null;
            card = new GameObject("ResultsCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(canvas, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 30);
            rt.sizeDelta = new Vector2(780, 700);
            HudUI.StylePanel(card, new Color(0.02f, 0.04f, 0.07f, 0.95f), HudUI.Accent);

            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(card.transform, false);
            var ht = header.AddComponent<Text>();
            ht.font = HudUI.UIFont;
            ht.fontSize = 40;
            ht.fontStyle = FontStyle.Bold;
            ht.alignment = TextAnchor.UpperCenter;
            ht.color = HudUI.Accent;
            ht.text = headerText;
            var hrt = header.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1);
            hrt.sizeDelta = new Vector2(0, 60); hrt.anchoredPosition = new Vector2(0, -28);

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(card.transform, false);
            bodyText = body.AddComponent<Text>();
            bodyText.font = HudUI.UIFont;
            bodyText.fontSize = 24;
            bodyText.alignment = TextAnchor.MiddleCenter;
            bodyText.color = Color.white;
            bodyText.supportRichText = true;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            var brt = body.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(40, 30); brt.offsetMax = new Vector2(-40, -90);

            card.SetActive(false);
        }
    }
}
