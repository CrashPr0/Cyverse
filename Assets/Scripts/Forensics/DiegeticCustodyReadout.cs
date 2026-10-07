using TMPro;
using UnityEngine;
using Cyverse.Interaction;

namespace Cyverse.Forensics
{
    /// <summary>
    /// A world-space (diegetic) readout of chain-of-custody progress, drawn onto
    /// a <see cref="DiegeticScreen"/> mounted at the Evidence Intake plinth.
    ///
    /// The interactive custody form itself stays a mouse-driven HUD modal — a
    /// WorldSpace canvas rendered to an offscreen RenderTexture (as DiegeticScreen
    /// builds it) is display-only and cannot receive the dropdown clicks the form
    /// needs. So this mirrors the form's live state (blanks completed, pending vs
    /// accepted) onto the station screen, which is the plan's blessed
    /// "drive a diegetic status/summary readout onto the station screen" path.
    ///
    /// Content is re-rendered on demand (DiegeticScreen renders only when
    /// <see cref="DiegeticScreen.RenderNow"/> is called), so there is zero
    /// per-frame cost — matching the WebGL constraint the screen was built for.
    /// </summary>
    public sealed class DiegeticCustodyReadout : MonoBehaviour
    {
        private static readonly Color Green = new Color(0.30f, 1f, 0.55f);
        private static readonly Color Gold = new Color(0.90f, 0.66f, 0.14f);
        private static readonly Color Dim = new Color(0.62f, 0.78f, 0.82f);

        private DiegeticScreen screen;
        private TMP_Text headerText;
        private TMP_Text statusText;
        private TMP_Text detailText;

        /// <summary>Build a readout bound to <paramref name="screen"/>. Lays out a
        /// header / status / detail stack under the screen's CanvasRoot and does
        /// an initial render.</summary>
        public static DiegeticCustodyReadout Attach(DiegeticScreen screen)
        {
            if (screen == null || screen.CanvasRoot == null) return null;
            var readout = screen.CanvasRoot.gameObject.AddComponent<DiegeticCustodyReadout>();
            readout.screen = screen;
            readout.BuildUi();
            readout.SetState(0, 4, false);
            return readout;
        }

        private void BuildUi()
        {
            Vector2 size = screen.CanvasSize; // canvas units (e.g. ~220 x 330)
            RectTransform root = screen.CanvasRoot;
            float contentWidth = Mathf.Max(1f, size.x - 20f);

            headerText = MakeText("CustodyHeader", root, 22f, TextAlignmentOptions.Top);
            headerText.text = "CHAIN OF CUSTODY";
            headerText.color = Green;
            headerText.fontStyle = FontStyles.Bold;
            Place(headerText.rectTransform, new Vector2(0f, 1f),
                new Vector2(10f, -14f), new Vector2(contentWidth, 46f));

            statusText = MakeText("CustodyStatus", root, 30f, TextAlignmentOptions.Center);
            statusText.color = Gold;
            statusText.fontStyle = FontStyles.Bold;
            Place(statusText.rectTransform, new Vector2(0f, 1f),
                new Vector2(10f, -92f), new Vector2(contentWidth, 74f));

            detailText = MakeText("CustodyDetail", root, 16f, TextAlignmentOptions.Bottom);
            detailText.color = Dim;
            Place(detailText.rectTransform, new Vector2(0f, 0f),
                new Vector2(10f, 16f), new Vector2(contentWidth, 115f));

            // Everything we just added is on the canvas; RenderNow re-applies the
            // isolation layer to these new widgets and draws them once.
            screen.RenderNow();
        }

        /// <summary>Update the readout to reflect current form state and render.</summary>
        public void SetState(int selected, int total, bool complete)
        {
            if (statusText == null) return;
            if (complete)
            {
                statusText.text = "CUSTODY\nACCEPTED";
                statusText.color = Green;
                detailText.text = "Entry 2 signed and logged:\nSOC to Digital Forensics.\nTake the device to the\nINVESTIGATION DESK.";
                detailText.color = Green;
            }
            else
            {
                statusText.text = $"{selected}/{total}\nLOGGED";
                statusText.color = Gold;
                // Second custody form of the run (the SOC logged the first), so
                // spell out why: a new handoff always gets its own entry.
                detailText.text = "Write entry 2 on the custody record:\nSOC to Digital Forensics.\nEvery handoff gets its own row,\neven inside one department.";
                detailText.color = Dim;
            }
            if (screen != null) screen.RenderNow();
        }

        // ---- UI helpers (WorldSpace canvas widgets) --------------------------

        private static TMP_Text MakeText(string name, RectTransform parent, float size,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            // The copy has deliberate line breaks. Auto-size each complete row
            // into its box instead of introducing surprise wraps that collide
            // with neighbouring rows.
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = size;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        /// <summary>Anchor a widget to a corner of the canvas and offset it in
        /// canvas units. <paramref name="cornerAnchor"/> picks the corner; offset
        /// is measured from it, size is the widget box.</summary>
        private static void Place(RectTransform rt, Vector2 cornerAnchor,
            Vector2 offset, Vector2 boxSize)
        {
            rt.anchorMin = cornerAnchor;
            rt.anchorMax = cornerAnchor;
            rt.pivot = cornerAnchor;
            rt.sizeDelta = new Vector2(Mathf.Max(1f, boxSize.x), Mathf.Max(1f, boxSize.y));
            rt.anchoredPosition = offset;
        }
    }
}
