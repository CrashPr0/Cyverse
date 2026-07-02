using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<color=#4CE087><b>Access Granted — Level: Employee</b></color>");
            sb.AppendLine();
            sb.AppendLine($"Final Score:  <b><color=#5BC8FF>{score}</color></b>");
            sb.AppendLine();
            sb.AppendLine($"Knowledge Check:  {quizCorrect} / {quizTotal} correct");
            sb.AppendLine($"Time:  {m}:{s:00}");
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
            rt.sizeDelta = new Vector2(720, 480);
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
            bodyText.fontSize = 27;
            bodyText.alignment = TextAnchor.MiddleCenter;
            bodyText.color = Color.white;
            bodyText.supportRichText = true;
            var brt = body.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(40, 30); brt.offsetMax = new Vector2(-40, -90);

            card.SetActive(false);
        }
    }
}
