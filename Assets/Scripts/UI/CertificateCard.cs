using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Core;

namespace Cyverse.UI
{
    /// <summary>
    /// Prototype certificate of completion, opened from the results card of
    /// any level ([C] or the button). Names the player's callsign, the level,
    /// the score and rating, and today's date. Drawn on the HUD canvas like the
    /// results card; nothing is saved or printed yet.
    /// </summary>
    public sealed class CertificateCard
    {
        private static readonly Color Paper = new Color(0.97f, 0.95f, 0.89f, 1f);
        private static readonly Color InkDark = new Color(0.10f, 0.13f, 0.18f, 1f);
        private static readonly Color InkSoft = new Color(0.34f, 0.38f, 0.44f, 1f);
        private static readonly Color Gold = new Color(0.78f, 0.58f, 0.16f, 1f);
        private static readonly Color Navy = new Color(0.08f, 0.17f, 0.32f, 1f);

        private GameObject root;
        private RectTransform sheet;
        private Text recipient, levelLine, scoreLine, dateLine;

        public bool IsOpen => root != null && root.activeSelf;

        /// <summary>"LEVEL 3 COMPLETE" or "LEVEL 3 HARD — COMPLETE" → the
        /// level's full name, as the Hub doors call it.</summary>
        public static string LevelName(string headerText)
        {
            Match m = Regex.Match(headerText ?? "", @"LEVEL\s+(\d)");
            if (!m.Success) return "CyVerse Training";
            string name;
            switch (m.Groups[1].Value)
            {
                case "0": name = "Level 0: Orientation"; break;
                case "1": name = "Level 1: Identity & Access Management"; break;
                case "2": name = "Level 2: Cyber Defense (SOC)"; break;
                case "3": name = "Level 3: Digital Forensics"; break;
                case "4": name = "Level 4: Cyber Attack"; break;
                default: name = "Level " + m.Groups[1].Value; break;
            }
            return headerText.Contains("HARD") ? name + "  (Hard)" : name;
        }

        public void Show(Transform canvas, string headerText, int score, int parScore, string grade, string rank)
        {
            if (root == null) Build(canvas);
            recipient.text = PlayerIdentity.Callsign;
            levelLine.text = LevelName(headerText);
            scoreLine.text = $"Score  <b>{score:N0}</b>  /  {parScore:N0} par      Rating  <b>{grade}</b>  ·  {rank}";
            dateLine.text = DateTime.Now.ToString("MMMM d, yyyy");
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            UiMotion.PanelIn(root, sheet, 0.95f);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void Build(Transform canvas)
        {
            ModalSession.EnsureEventSystem();
            root = new GameObject("CertificateOverlay", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas, false);
            var rrt = (RectTransform)root.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0.01f, 0.02f, 0.80f);

            sheet = Box("Certificate", root.transform, Gold, Vector2.zero, new Vector2(1120f, 760f));
            sheet.anchoredPosition = new Vector2(0f, 20f);
            Box("Paper", sheet, Paper, Vector2.zero, new Vector2(1104f, 744f));
            Frame(sheet, new Vector2(1060f, 700f), Navy, 3f);
            Frame(sheet, new Vector2(1044f, 684f), Gold, 1.5f);

            Label(sheet, "Academy", "C Y V E R S E   S E C U R I T Y   A C A D E M Y", 20, Gold, 300f, FontStyle.Bold);
            Label(sheet, "Title", "Certificate of Completion", 62, Navy, 225f, FontStyle.Bold);
            Box("TitleRule", sheet, Gold, new Vector2(0f, 178f), new Vector2(420f, 3f));
            Label(sheet, "Certifies", "This certifies that", 24, InkSoft, 128f, FontStyle.Italic);
            recipient = Label(sheet, "Recipient", "", 54, InkDark, 66f, FontStyle.Bold);
            Box("NameRule", sheet, InkSoft, new Vector2(0f, 30f), new Vector2(560f, 1.5f));
            Label(sheet, "Completed", "has successfully completed", 24, InkSoft, -6f, FontStyle.Italic);
            levelLine = Label(sheet, "Level", "", 38, Navy, -58f, FontStyle.Bold);
            scoreLine = Label(sheet, "Score", "", 22, InkDark, -112f, FontStyle.Normal);

            // Date on the left, signature on the right, seal between them.
            dateLine = Signature(sheet, "Date", -300f, "Date");
            Signature(sheet, "Signed", 300f, "CyVerse Training Lead").text = "<i>CyVerse Training</i>";
            Image seal = PhoneScreenUi.RoundedImage("Seal", sheet, Gold, 60f);
            seal.rectTransform.sizeDelta = new Vector2(120f, 120f);
            seal.rectTransform.anchoredPosition = new Vector2(0f, -240f);
            Image sealInner = PhoneScreenUi.RoundedImage("SealInner", seal.rectTransform, Navy, 52f);
            sealInner.rectTransform.sizeDelta = new Vector2(104f, 104f);
            Text sealText = Label(seal.rectTransform, "SealText", "CYVERSE\n<size=12>PROTOTYPE</size>", 17, Gold, 0f, FontStyle.Bold);
            sealText.rectTransform.sizeDelta = new Vector2(104f, 60f);

            Label(sheet, "Prototype",
                "Prototype certificate  ·  not an official credential", 15, InkSoft, -322f, FontStyle.Italic);

            Button close = MakeButton(root.transform, "Close", "CLOSE   [C] / Esc");
            var crt = (RectTransform)close.transform;
            crt.anchoredPosition = new Vector2(0f, -400f);
            close.onClick.AddListener(Hide);
            UiMotion.AddPress(close);

            root.SetActive(false);
        }

        private static RectTransform Box(string name, Transform parent, Color color, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        /// <summary>A thin rectangular rule of the given size (four bars).</summary>
        private static void Frame(Transform parent, Vector2 size, Color color, float width)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f;
            Box("FrameTop", parent, color, new Vector2(0f, hy), new Vector2(size.x, width));
            Box("FrameBottom", parent, color, new Vector2(0f, -hy), new Vector2(size.x, width));
            Box("FrameLeft", parent, color, new Vector2(-hx, 0f), new Vector2(width, size.y));
            Box("FrameRight", parent, color, new Vector2(hx, 0f), new Vector2(width, size.y));
        }

        private static Text Label(Transform parent, string name, string text, int size, Color color, float y, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = HudUI.UIFont != null ? HudUI.UIFont : HudUI.LoadFont();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.text = text;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(960f, size * 1.4f);
            rt.anchoredPosition = new Vector2(0f, y);
            return t;
        }

        /// <summary>A value over a rule with a caption under it.</summary>
        private static Text Signature(Transform parent, string name, float x, string caption)
        {
            Text value = Label(parent, name, "", 24, InkDark, -222f, FontStyle.Normal);
            value.rectTransform.sizeDelta = new Vector2(300f, 34f);
            value.rectTransform.anchoredPosition = new Vector2(x, -222f);
            Box(name + "Rule", parent, InkSoft, new Vector2(x, -244f), new Vector2(260f, 1.5f));
            Text cap = Label(parent, name + "Caption", caption, 16, InkSoft, -264f, FontStyle.Normal);
            cap.rectTransform.sizeDelta = new Vector2(300f, 24f);
            cap.rectTransform.anchoredPosition = new Vector2(x, -264f);
            return value;
        }

        private static Button MakeButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.09f, 0.14f, 0.16f, 1f);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(240f, 46f);
            Text t = Label(go.transform, "Label", label, 20, Color.white, 0f, FontStyle.Bold);
            t.rectTransform.sizeDelta = new Vector2(240f, 46f);
            return go.GetComponent<Button>();
        }
    }
}
