using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Core;
using Cyverse.Level;

namespace Cyverse.UI
{
    /// <summary>Small persistent HUD inventory card for the SOC evidence item.
    /// It reconstructs itself in every scene from SocProgress's JSON record.</summary>
    public sealed class EvidenceInventoryPanel : MonoBehaviour
    {
        public static EvidenceInventoryPanel Instance { get; private set; }

        private GameObject panel;
        private TextMeshProUGUI body;
        private string lastJson;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public static EvidenceInventoryPanel Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            var found = FindObjectOfType<EvidenceInventoryPanel>();
            return found != null ? found : host.AddComponent<EvidenceInventoryPanel>();
        }

        public void RefreshNow()
        {
            string json = PlayerPrefs.GetString(SocProgress.EvidenceJsonKey, "");
            lastJson = json;
            if (string.IsNullOrEmpty(json) || !SocProgress.TryGetEvidence(out var evidence))
            {
                if (panel != null) panel.SetActive(false);
                return;
            }

            if (panel == null) Build();
            body.text = "<color=#5BD9FF><b>EVIDENCE INVENTORY</b></color>\n" +
                        "<color=#E5A823>■</color>  " + evidence.inventoryItem + "\n" +
                        $"<size=18><color=#AFC4D4>{evidence.computer} · collected {evidence.collectedAtUtc}</color></size>";
            panel.SetActive(!GameState.AnyMenuOpen);
        }

        void LateUpdate()
        {
            string json = PlayerPrefs.GetString(SocProgress.EvidenceJsonKey, "");
            if (json != lastJson) RefreshNow();
            if (panel != null && !string.IsNullOrEmpty(json))
                panel.SetActive(!GameState.AnyMenuOpen);
        }

        private void Build()
        {
            if (HudUI.Instance == null) return;
            panel = new GameObject("EvidenceInventory", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-24f, 24f);
            rt.sizeDelta = new Vector2(510f, 120f);
            HudUI.StylePanel(panel, new Color(0.02f, 0.04f, 0.07f, 0.90f), HudUI.Accent);

            var textGo = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(panel.transform, false);
            body = textGo.GetComponent<TextMeshProUGUI>();
            body.fontSize = 22f;
            body.color = Color.white;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.enableWordWrapping = true;
            var trt = body.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(14f, 8f);
            trt.offsetMax = new Vector2(-12f, -12f);
        }
    }
}
