using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cyverse.UI
{
    /// <summary>
    /// Hover and press feedback for a uGUI button, added by
    /// <see cref="UiMotion.AddPress"/>. A stand-alone button grows 2% on hover
    /// and dips to 97% while held; a list row slides its label a few pixels
    /// instead. Event-driven, so it costs nothing while the pointer is away.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiPressable : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.02f;
        private const float PressScale = 0.97f;

        public bool Row { get; set; }

        private Button button;
        private RectTransform label;
        private Vector2 labelRest;
        private bool hasRest, hovered, held;

        void Awake()
        {
            button = GetComponent<Button>();
        }

        private bool Live => button == null || button.IsInteractable();

        public void OnPointerEnter(PointerEventData e) { hovered = true; Apply(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; held = false; Apply(); }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { held = true; Apply(); } }
        public void OnPointerUp(PointerEventData e) { held = false; Apply(); }

        private void Apply()
        {
            bool live = Live;
            if (Row)
            {
                RectTransform l = Label();
                if (l == null) return;
                float x = live && (hovered || held) ? UiMotion.Nudge : 0f;
                UiMotion.MoveTo(l, labelRest + new Vector2(x, 0f), UiMotion.Fast);
                return;
            }
            float s = !live ? 1f : held ? PressScale : hovered ? HoverScale : 1f;
            UiMotion.ScaleTo(transform, s, held ? UiMotion.Fast * 0.6f : UiMotion.Fast);
        }

        /// <summary>The first text child, whose resting spot is read on first
        /// hover: builders often re-inset the label after adding it.</summary>
        private RectTransform Label()
        {
            if (label == null)
            {
                Graphic g = null;
                foreach (Graphic c in GetComponentsInChildren<Graphic>(true))
                    if (c.gameObject != gameObject && c.GetType().Name.Contains("Text")) { g = c; break; }
                if (g == null) return null;
                label = g.rectTransform;
            }
            if (!hasRest) { labelRest = label.anchoredPosition; hasRest = true; }
            return label;
        }

        void OnDisable()
        {
            // A panel closing under the pointer must not reopen with a stuck
            // hover: put everything back at rest, at once.
            hovered = held = false;
            UiMotion.Finish(transform);
            transform.localScale = Vector3.one;
            if (label != null && hasRest)
            {
                UiMotion.Finish(label);
                label.anchoredPosition = labelRest;
            }
        }
    }
}
