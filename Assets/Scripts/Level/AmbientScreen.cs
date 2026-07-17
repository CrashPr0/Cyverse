using UnityEngine;
using Cyverse.Settings;

namespace Cyverse.Level
{
    /// <summary>
    /// Drives a wall TV built by PropFactory.BuildWallTV: cycles a headline
    /// ticker and animates a small bar chart so screens read as live feeds
    /// rather than static glowing rectangles. Under Reduce Motion the bars
    /// freeze; the headline still rotates (slow, informational, no flashing).
    /// </summary>
    public class AmbientScreen : MonoBehaviour
    {
        public TextMesh headline;
        public Transform[] bars;          // bottom-pivoted: scaling Y grows upward
        public string[] messages;
        public int firstMessage;
        public float messageSeconds = 6f;
        public float maxBarHeight = 0.5f;

        private int index;
        private float msgTimer;
        private float barTimer;
        private float[] targets;

        void Start()
        {
            index = firstMessage;
            if (bars != null)
            {
                targets = new float[bars.Length];
                for (int i = 0; i < bars.Length; i++)
                    targets[i] = Random.Range(0.25f, 1f) * maxBarHeight;
            }
            ApplyMessage();
        }

        void Update()
        {
            if (messages != null && messages.Length > 0)
            {
                msgTimer += Time.deltaTime;
                if (msgTimer >= messageSeconds)
                {
                    msgTimer = 0f;
                    index = (index + 1) % messages.Length;
                    ApplyMessage();
                }
            }

            if (bars == null || targets == null || AccessibilitySettings.ReduceMotion) return;

            barTimer += Time.deltaTime;
            if (barTimer >= 1.7f)
            {
                barTimer = 0f;
                for (int i = 0; i < targets.Length; i++)
                    targets[i] = Random.Range(0.2f, 1f) * maxBarHeight;
            }
            for (int i = 0; i < bars.Length; i++)
            {
                if (bars[i] == null) continue;
                var s = bars[i].localScale;
                s.y = Mathf.Lerp(s.y, targets[i], Time.deltaTime * 3f);
                bars[i].localScale = s;
            }
        }

        private void ApplyMessage()
        {
            if (headline != null && messages != null && messages.Length > 0)
                headline.text = messages[index % messages.Length];
        }
    }
}
