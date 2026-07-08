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

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public void Show(int score, int quizCorrect, int quizTotal, float seconds)
        {
            if (card == null) Build();

            var scene = SceneManager.GetActiveScene();
            canReload = scene.IsValid() && !string.IsNullOrEmpty(scene.name);

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

            // Themed letter grade — score-driven, generous at the top end so a
            // perfect run (550) earns the S.
            string grade = score >= 520 ? "S" : score >= 450 ? "A" : score >= 350 ? "B" : "C";

            // Rank title — a second, more human-readable layer of feedback.
            string rank = score >= 500 ? "Senior Security Agent"
                        : score >= 400 ? "Security Specialist"
                        : score >= 300 ? "Security Analyst"
                        : "Security Recruit";

            int percentile = PercentileFor(score);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<color=#4CE087><b>Access Granted — Level: Employee</b></color>");
            sb.AppendLine($"Employee ID:  <b>{PlayerIdentity.Callsign}</b>");
            sb.AppendLine();
            sb.AppendLine($"Security Clearance Rating:  <color=#E5A823><b>{grade}</b></color>   Rank:  <color=#E5A823><b>{rank}</b></color>");
            sb.AppendLine();
            sb.AppendLine($"Final Score:  <b><color=#5BC8FF>{score}</color></b>");
            sb.AppendLine($"Best Score:  {best}" + (newBest ? "  <color=#E5A823><b>NEW BEST!</b></color>" : ""));
            sb.AppendLine($"Best Streak:  {ScoreSystem.BestStreak} correct in a row");
            sb.AppendLine();
            sb.AppendLine($"Knowledge Check:  {quizCorrect} / {quizTotal} correct");
            sb.AppendLine($"Time:  {m}:{s:00}");
            sb.AppendLine($"<size=22>You scored better than <color=#5BC8FF><b>{percentile}%</b></color> of recruits</size>");
            sb.AppendLine();
            sb.AppendLine("<size=20><color=#5BC8FF>NEXT MISSION:</color> <color=#8FB8CC>Level 1 — Cyber Defense  (in development)</color></size>");
            if (canReload)
            {
                sb.AppendLine();
                sb.AppendLine("<size=20><color=#8FB8CC>[R]  Replay Level 0</color></size>");
            }
            bodyText.text = sb.ToString();

            card.SetActive(true);
            shown = true;
        }

        void Update()
        {
            if (!shown || !canReload) return;
            if (Input.GetKeyDown(KeyCode.R))
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Serverless "you scored better than X% of recruits": models a fixed
        /// bell-curve distribution of scores (mean 300, std-dev 95, over the
        /// 0–550 range) rather than calling out to a leaderboard. Clamped to
        /// 1–99 so the message always reads as encouragement, never as "worst
        /// of everyone."
        /// </summary>
        private static int PercentileFor(int score)
        {
            const float mean = 300f, stdDev = 95f;
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

        private void Build()
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
            ht.text = "LEVEL 0 COMPLETE";
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
