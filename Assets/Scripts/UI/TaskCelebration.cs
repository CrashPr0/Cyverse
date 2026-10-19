using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.Settings;

namespace Cyverse.UI
{
    /// <summary>
    /// "TASK COMPLETE" banner under the objective bar, with a rising chime and
    /// a small particle burst, played when TaskListPanel sees a task flip to
    /// done. Banners queue so two quick completions each get their moment.
    /// Under Reduce Motion the banner just appears and fades, with no slide
    /// and no particles; the sound stays.
    /// </summary>
    public class TaskCelebration : MonoBehaviour
    {
        private static TaskCelebration instance;

        private readonly Queue<string> pending = new Queue<string>();
        private bool playing;
        private CanvasGroup group;
        private RectTransform rect;
        private TMP_Text nameText;

        private const float SlideIn = 0.3f;
        private const float Hold = 2f;
        private const float FadeOut = 0.5f;
        private const float RestY = -116f; // just below the objective bar

        /// <summary>Called by TaskListPanel when a task becomes done.</summary>
        public static void Notify(string taskName)
        {
            if (HudUI.Instance == null) return;
            if (instance == null)
            {
                var go = new GameObject("TaskCelebration");
                instance = go.AddComponent<TaskCelebration>();
            }
            instance.pending.Enqueue(taskName);
            if (!instance.playing) instance.StartCoroutine(instance.Run());
        }

        void OnDestroy() { if (instance == this) instance = null; }

        private IEnumerator Run()
        {
            playing = true;
            while (pending.Count > 0)
            {
                if (group == null) Build();
                if (group == null) break; // HUD went away
                yield return Play(pending.Dequeue());
            }
            pending.Clear();
            playing = false;
        }

        private IEnumerator Play(string taskName)
        {
            nameText.text = taskName;
            bool still = AccessibilitySettings.ReduceMotion;

            if (Sfx.Instance != null) Sfx.Instance.PlayTaskComplete();
            if (!still)
            {
                var cam = Camera.main;
                if (cam != null)
                    BurstFX.Spawn(cam.transform.position + cam.transform.forward * 1.6f,
                        new Color(0.30f, 0.88f, 0.53f), 18, 1.6f, 0.7f);
            }

            group.gameObject.SetActive(true);
            float t = 0f;
            while (t < SlideIn)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / SlideIn);
                group.alpha = p;
                rect.anchoredPosition = new Vector2(0f, RestY + (still ? 0f : 30f * (1f - p) * (1f - p)));
                yield return null;
            }
            group.alpha = 1f;
            rect.anchoredPosition = new Vector2(0f, RestY);

            yield return new WaitForSecondsRealtime(Hold);

            t = 0f;
            while (t < FadeOut)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(t / FadeOut);
                yield return null;
            }
            group.alpha = 0f;
            group.gameObject.SetActive(false);
        }

        private void Build()
        {
            if (HudUI.Instance == null) return;

            var panel = new GameObject("TaskCompleteBanner",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, RestY);
            rect.sizeDelta = new Vector2(520f, 74f);
            HudUI.StylePanel(panel, new Color(0.02f, 0.06f, 0.05f, 0.85f), new Color(0.30f, 0.88f, 0.53f));
            panel.GetComponent<Image>().raycastTarget = false;
            group = panel.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var title = MakeText("Title", panel.transform, 24, new Vector2(0f, -8f), 30f);
            title.text = "<b>TASK COMPLETE</b>";
            title.color = new Color(0.30f, 0.88f, 0.53f);
            nameText = MakeText("Name", panel.transform, 20, new Vector2(0f, -38f), 28f);
            nameText.color = Color.white;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            panel.SetActive(false);
        }

        private static TMP_Text MakeText(string name, Transform parent, float size, Vector2 pos, float height)
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
