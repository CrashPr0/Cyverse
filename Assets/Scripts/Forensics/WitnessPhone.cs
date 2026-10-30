using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.UI;

namespace Cyverse.Forensics
{
    /// <summary>
    /// A coworker's phone on a stand in the lab, shared by consent. Pressing E
    /// opens it full screen as a NovaOS messages app: a contact list, and
    /// tapping a contact opens the conversation, which scrolls (mouse wheel,
    /// drag, or Up/Down). Optional: it backs up Case 2 from a witness's side
    /// and is not needed to finish the level.
    ///
    /// Actions: Interact opens · Choose(i) opens contact i · Navigate(±1)
    /// scrolls · Cancel goes back to the list, then closes.
    /// </summary>
    public sealed class WitnessPhone : MonoBehaviour, IInteractable, IGameplayActionTarget
    {
        // Design space is the shared phone screen (200 x 400); the device is
        // scaled up to read comfortably on the 1920 x 1080 HUD canvas.
        private const float DeviceScale = 1.85f;
        private const float HeaderTop = -26f, HeaderHeight = 34f;
        private const float BubbleMax = 132f, BubblePad = 6f, BubbleFont = 9f;
        private const float ScrollStep = 60f;

        private static readonly Color Glass = new Color(0.035f, 0.045f, 0.075f, 1f);
        private static readonly Color HeaderColor = new Color(0.06f, 0.08f, 0.13f, 1f);
        private static readonly Color Nova = new Color(0.36f, 0.56f, 1f);
        private static readonly Color OutBubble = new Color(0.22f, 0.42f, 0.95f, 1f);
        private static readonly Color InBubble = new Color(0.15f, 0.18f, 0.25f, 1f);
        private static readonly Color Ink = new Color(0.92f, 0.95f, 1f);
        private static readonly Color Muted = new Color(0.58f, 0.66f, 0.78f);
        private static readonly Color RowIdle = new Color(0.05f, 0.065f, 0.10f, 1f);
        private static readonly Color[] AvatarColors =
        {
            new Color(0.85f, 0.45f, 0.25f), new Color(0.25f, 0.65f, 0.45f), new Color(0.70f, 0.30f, 0.35f),
            new Color(0.55f, 0.40f, 0.80f), new Color(0.25f, 0.55f, 0.75f),
        };

        private List<WitnessContact> contacts;
        private int openContact = -1;
        private bool open;
        private ModalSession.Lease modal;

        private GameObject overlay;
        private RectTransform holder, glass, listRoot, chatRoot, chatContent;
        private ScrollRect chatScroll;
        private TMP_Text headerTitle, headerDetail;
        private Button backButton;
        private readonly List<Button> contactRows = new List<Button>();

        public bool IsOpen => open;
        public int OpenContact => openContact;
        public int ContactCount => Contacts.Count;
        public IReadOnlyList<WitnessContact> Contacts => contacts ?? (contacts = WitnessPhoneContent.Build());
        /// <summary>1 at the top of the conversation, 0 at the bottom.</summary>
        public float ScrollPosition => chatScroll != null ? chatScroll.verticalNormalizedPosition : 1f;
        /// <summary>How far the open conversation can scroll, in design units.</summary>
        public float ScrollRange => chatScroll == null ? 0f
            : Mathf.Max(0f, chatContent.rect.height - ((RectTransform)chatScroll.viewport).rect.height);

        public bool CanInteract => !open;
        public string Prompt => "Read witness phone  (" + WitnessPhoneContent.OwnerName + ")";

        public void Interact(GameObject interactor) => Open();

        private void OnDestroy() => modal?.Close();

        public bool TryApply(GameplayAction action, GameObject actor)
        {
            switch (action.Kind)
            {
                case GameplayActionKind.Choose:
                case GameplayActionKind.Select:
                    if (!open || action.Index < 0 || action.Index >= ContactCount) return false;
                    ShowConversation(action.Index);
                    return true;
                case GameplayActionKind.Navigate:
                    if (!open || openContact < 0) return false;
                    Scroll(action.Index);
                    return true;
                case GameplayActionKind.Cancel:
                    if (!open) return false;
                    if (openContact >= 0) ShowContacts();
                    else Close();
                    return true;
                default:
                    return false;
            }
        }

        private void Open()
        {
            if (open) return;
            if (overlay == null) Build();
            if (overlay == null) return;
            if (!ModalSession.TryOpen(this, ModalSession.Channel.Quiz, out modal, releaseCursor: true)) return;
            open = true;
            overlay.transform.SetAsLastSibling();
            overlay.SetActive(true);
            UiMotion.PanelIn(overlay, holder, 0.96f);
            ShowContacts();
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
        }

        private void Close()
        {
            open = false;
            openContact = -1;
            if (overlay != null) overlay.SetActive(false);
            modal?.Close();
            modal = null;
        }

        void Update()
        {
            if (!open || Time.frameCount == GameState.MenuTransitionFrame) return;
            if (GameInput.CancelDown) { GameplayActions.TryApply(this, GameplayAction.Cancel()); return; }
            if (openContact >= 0)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow)) GameplayActions.TryApply(this, GameplayAction.Navigate(-1));
                else if (Input.GetKeyDown(KeyCode.DownArrow)) GameplayActions.TryApply(this, GameplayAction.Navigate(1));
                return;
            }
            for (int i = 0; i < ContactCount && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    GameplayActions.TryApply(this, GameplayAction.Choose(i));
                    return;
                }
        }

        // ---- Screens -----------------------------------------------------------

        private void ShowContacts()
        {
            openContact = -1;
            chatRoot.gameObject.SetActive(false);
            listRoot.gameObject.SetActive(true);
            backButton.gameObject.SetActive(false);
            headerTitle.text = "<b>Messages</b>";
            headerDetail.text = "NovaOS";
            UiMotion.Stagger(contactRows, 0.02f, UiMotion.Rise * 0.4f);
        }

        private void ShowConversation(int index)
        {
            openContact = index;
            WitnessContact contact = Contacts[index];
            listRoot.gameObject.SetActive(false);
            chatRoot.gameObject.SetActive(true);
            backButton.gameObject.SetActive(true);
            headerTitle.text = "<b>" + contact.name + "</b>";
            headerDetail.text = contact.detail;
            FillConversation(contact);
            // Start at the beginning so the story reads in order.
            chatScroll.verticalNormalizedPosition = 1f;
            UiMotion.FadeIn(chatRoot.gameObject, UiMotion.Fast);
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
        }

        private void Scroll(int direction)
        {
            float range = ScrollRange;
            if (range <= 0f) return;
            float step = ScrollStep / range;
            chatScroll.verticalNormalizedPosition =
                Mathf.Clamp01(chatScroll.verticalNormalizedPosition - direction * step);
        }

        // ---- Construction ------------------------------------------------------

        private void Build()
        {
            if (HudUI.Instance == null) return;
            ModalSession.EnsureEventSystem();

            overlay = new GameObject("WitnessPhoneOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            Stretch((RectTransform)overlay.transform, 0f, 0f, 1f, 1f);
            overlay.GetComponent<Image>().color = new Color(0f, 0.01f, 0.02f, 0.82f);

            // PanelIn settles the holder to scale 1, so the size lives one level down.
            holder = Rect("PhoneHolder", overlay.transform);
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.anchoredPosition = new Vector2(-150f, -10f);
            RectTransform device = PhoneScreenUi.BuildShell(holder, Glass, out glass);
            device.localScale = Vector3.one * DeviceScale;

            BuildCaption(overlay.transform);
            BuildHeader();
            BuildContactList();
            BuildConversation();
            overlay.SetActive(false);
        }

        private void BuildCaption(Transform parent)
        {
            TMP_Text caption = MakeText(parent, "Caption", 22, TextAlignmentOptions.TopLeft);
            var rt = caption.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(430f, 420f);
            rt.anchoredPosition = new Vector2(110f, 0f);
            caption.text =
                "<size=15><color=#607585><b><cspace=3>WITNESS PHONE</cspace></b></color></size>\n" +
                $"<size=30><b>{WitnessPhoneContent.OwnerName}</b></size>\n" +
                "<color=#8FB8CC>Works with the seized phone's owner. Shared by consent; read only.</color>\n\n" +
                "Tap a contact to open the conversation.\n" +
                "Scroll with the mouse wheel, drag, or <b>Up/Down</b>.\n" +
                "<b>Esc</b> goes back, then closes.";
            caption.enableWordWrapping = true;

            Button close = MakeButton(parent, "Close", "<b>ESC</b>   CLOSE", new Color(0.09f, 0.14f, 0.16f), 18);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.sizeDelta = new Vector2(180f, 48f);
            crt.anchoredPosition = new Vector2(110f, -250f);
            close.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
            close.onClick.AddListener(Close);
            UiMotion.AddPress(close);
        }

        private void BuildHeader()
        {
            Image header = Plain("Header", glass, HeaderColor);
            var hr = header.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
            hr.sizeDelta = new Vector2(0f, HeaderHeight); hr.anchoredPosition = new Vector2(0f, HeaderTop);

            headerTitle = MakeText(hr, "Title", 12, TextAlignmentOptions.Center);
            headerTitle.enableWordWrapping = false;
            headerTitle.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(headerTitle.rectTransform, 0f, 0.42f, 1f, 1f, 34f, 0f, 34f, 0f);
            headerDetail = MakeText(hr, "Detail", 7, TextAlignmentOptions.Center);
            headerDetail.color = Muted;
            headerDetail.enableWordWrapping = false;
            headerDetail.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(headerDetail.rectTransform, 0f, 0f, 1f, 0.46f, 30f, 2f, 30f, 0f);

            backButton = MakeButton(hr, "Back", "<b>‹</b> Back", HeaderColor, 9);
            var br = (RectTransform)backButton.transform;
            br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(0f, 1f); br.pivot = new Vector2(0f, 0.5f);
            br.sizeDelta = new Vector2(38f, 0f); br.anchoredPosition = new Vector2(2f, 0f);
            backButton.GetComponentInChildren<TMP_Text>().color = Nova;
            backButton.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Cancel()));
            UiMotion.AddPress(backButton);
        }

        private RectTransform BodyArea(string name)
        {
            var rt = Rect(name, glass);
            Stretch(rt, 0f, 0f, 1f, 1f, 0f, 16f, 0f, -HeaderTop + HeaderHeight + 1f);
            return rt;
        }

        private void BuildContactList()
        {
            listRoot = BodyArea("ContactList");
            const float rowHeight = 40f;
            for (int i = 0; i < ContactCount; i++)
            {
                WitnessContact c = Contacts[i];
                Button row = MakeButton(listRoot, "Contact_" + i, "", RowIdle, 9);
                var rr = (RectTransform)row.transform;
                rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(0.5f, 1f);
                rr.sizeDelta = new Vector2(0f, rowHeight - 1f);
                rr.anchoredPosition = new Vector2(0f, -i * rowHeight);
                int index = i;
                row.onClick.AddListener(() => GameplayActions.TryApply(this, GameplayAction.Choose(index)));
                UiMotion.AddPress(row, row: true);

                Image avatar = PhoneScreenUi.RoundedImage("Avatar", rr, AvatarColors[i % AvatarColors.Length], 13f);
                var ar = avatar.rectTransform;
                ar.anchorMin = ar.anchorMax = new Vector2(0f, 0.5f); ar.pivot = new Vector2(0f, 0.5f);
                ar.sizeDelta = new Vector2(26f, 26f); ar.anchoredPosition = new Vector2(8f, 0f);
                TMP_Text initials = MakeText(ar, "Initials", 9, TextAlignmentOptions.Center);
                initials.text = "<b>" + Initials(c.name) + "</b>";
                Stretch(initials.rectTransform, 0f, 0f, 1f, 1f);

                TMP_Text label = row.GetComponentInChildren<TMP_Text>();
                label.enableWordWrapping = true;
                label.overflowMode = TextOverflowModes.Truncate;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(40f, 2f);
                label.rectTransform.offsetMax = new Vector2(-6f, -2f);
                WitnessMessage last = c.Last;
                string preview = last.IsSystem ? last.text : (last.outgoing ? "You: " : "") + last.text;
                if (preview.Length > 38) preview = preview.Substring(0, 36) + "...";
                label.text = $"<b>{c.name}</b><pos=78%><size=7><color=#8FA0B8>{last.time.Substring(0, 5)}</color></size>\n" +
                    $"<size=8><color=#8FA0B8>{Escape(preview)}</color></size>";
                contactRows.Add(row);
            }

            TMP_Text footer = MakeText(listRoot, "Footer", 7, TextAlignmentOptions.Center);
            footer.color = Muted;
            footer.text = "Shared with the forensics lab by consent";
            var fr = footer.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(1f, 0f); fr.pivot = new Vector2(0.5f, 0f);
            fr.sizeDelta = new Vector2(0f, 14f); fr.anchoredPosition = new Vector2(0f, 2f);
        }

        private void BuildConversation()
        {
            chatRoot = BodyArea("Conversation");

            var viewport = Rect("Viewport", chatRoot);
            Stretch(viewport, 0f, 0f, 1f, 1f, 0f, 0f, 5f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            // A clear graphic so the wheel and drag land on the scroll view
            // anywhere, not only on the bubbles.
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            chatContent = Rect("Content", viewport);
            chatContent.anchorMin = new Vector2(0f, 1f); chatContent.anchorMax = new Vector2(1f, 1f);
            chatContent.pivot = new Vector2(0.5f, 1f);
            chatContent.sizeDelta = new Vector2(0f, 0f);

            Image track = Plain("ScrollTrack", chatRoot, new Color(1f, 1f, 1f, 0.05f));
            var tr = track.rectTransform;
            tr.anchorMin = new Vector2(1f, 0f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(1f, 0.5f);
            tr.sizeDelta = new Vector2(3f, -8f); tr.anchoredPosition = new Vector2(-1f, 0f);
            var bar = tr.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            var handleArea = Rect("Area", tr);
            Stretch(handleArea, 0f, 0f, 1f, 1f);
            Image handle = PhoneScreenUi.RoundedImage("Handle", handleArea, new Color(1f, 1f, 1f, 0.35f), 1.5f);
            Stretch(handle.rectTransform, 0f, 0f, 1f, 1f);
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;

            chatScroll = chatRoot.gameObject.AddComponent<ScrollRect>();
            chatScroll.viewport = viewport;
            chatScroll.content = chatContent;
            chatScroll.horizontal = false;
            chatScroll.vertical = true;
            chatScroll.movementType = ScrollRect.MovementType.Clamped;
            chatScroll.scrollSensitivity = 18f;
            chatScroll.verticalScrollbar = bar;
            chatScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        /// <summary>Lays the bubbles out by hand (measured with TMP) so the
        /// height is exact without nested layout groups.</summary>
        private void FillConversation(WitnessContact contact)
        {
            for (int i = chatContent.childCount - 1; i >= 0; i--)
                Destroy(chatContent.GetChild(i).gameObject);

            float width = ((RectTransform)chatScroll.viewport).rect.width;
            if (width <= 0f) width = PhoneScreenUi.ScreenSize.x - 5f;
            float y = -6f;
            string day = null;
            foreach (WitnessMessage m in contact.messages)
            {
                string date = m.time.Substring(0, 5);
                if (date != day)
                {
                    day = date;
                    y -= 2f;
                    TMP_Text stamp = MakeText(chatContent, "Day", 7, TextAlignmentOptions.Center);
                    stamp.color = Muted;
                    stamp.text = "<b>" + DayLabel(date) + "</b>";
                    Place(stamp.rectTransform, 0f, y, width, 12f);
                    y -= 14f;
                }

                if (m.IsSystem)
                {
                    TMP_Text note = MakeText(chatContent, "System", 7.5f, TextAlignmentOptions.Center);
                    note.color = new Color(1f, 0.62f, 0.45f);
                    note.text = "<i>" + m.text + "</i>";
                    Place(note.rectTransform, 0f, y, width, 12f);
                    y -= 16f;
                    continue;
                }

                bool showSender = contact.group && !m.outgoing && !string.IsNullOrEmpty(m.sender);
                if (showSender)
                {
                    TMP_Text who = MakeText(chatContent, "Sender", 7, TextAlignmentOptions.Left);
                    who.color = Nova;
                    who.text = "<b>" + m.sender + "</b>";
                    Place(who.rectTransform, 10f, y, BubbleMax, 10f);
                    y -= 10f;
                }

                TMP_Text body = MakeText(chatContent, "Text", BubbleFont, TextAlignmentOptions.TopLeft);
                body.text = Escape(m.text);
                body.color = Color.white;
                Vector2 pref = body.GetPreferredValues(body.text, BubbleMax, 0f);
                float textW = Mathf.Min(BubbleMax, Mathf.Ceil(pref.x) + 1f);
                float textH = Mathf.Ceil(body.GetPreferredValues(body.text, textW, 0f).y);
                float bubbleW = textW + BubblePad * 2f, bubbleH = textH + BubblePad * 1.4f;
                float x = m.outgoing ? width - 8f - bubbleW : 8f;

                Image bubble = PhoneScreenUi.RoundedImage("Bubble", chatContent, m.outgoing ? OutBubble : InBubble, 8f);
                Place(bubble.rectTransform, x, y, bubbleW, bubbleH);
                body.transform.SetParent(bubble.transform, false);
                Stretch(body.rectTransform, 0f, 0f, 1f, 1f, BubblePad, BubblePad * 0.7f, BubblePad, BubblePad * 0.7f);
                y -= bubbleH + 1.5f;

                TMP_Text time = MakeText(chatContent, "Time", 6, m.outgoing ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
                time.color = new Color(0.50f, 0.57f, 0.68f);
                time.text = m.time.Substring(6);
                Place(time.rectTransform, m.outgoing ? width - 8f - 60f : 10f, y, 60f, 8f);
                y -= 12f;
            }
            chatContent.sizeDelta = new Vector2(0f, -y + 6f);
        }

        // ---- Helpers -------------------------------------------------------------

        private static string DayLabel(string monthDay)
        {
            switch (monthDay)
            {
                case "07-13": return "SUN 07-13";
                case "07-14": return "MON 07-14";
                case "07-15": return "TUE 07-15";
                case "07-16": return "WED 07-16";
                default: return monthDay;
            }
        }

        private static string Initials(string name)
        {
            string result = "";
            foreach (string part in name.Split(' '))
                if (part.Length > 0 && char.IsLetter(part[0]) && result.Length < 2) result += char.ToUpperInvariant(part[0]);
            return result.Length > 0 ? result : "?";
        }

        private static string Escape(string s) => s.Replace("<", "‹").Replace(">", "›");

        /// <summary>Top-anchored rect at x, y (y negative, down from the top).</summary>
        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image Plain(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax,
            float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static Button MakeButton(Transform parent, string name, string label, Color background, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = background;
            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.80f, 0.88f, 1f);
            colors.pressedColor = new Color(0.62f, 0.72f, 0.92f);
            button.colors = colors;

            TMP_Text text = MakeText(go.transform, "Label", size, TextAlignmentOptions.MidlineLeft);
            text.text = label;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(text.rectTransform, 0f, 0f, 1f, 1f, 6f, 0f, 4f, 0f);
            return button;
        }

        private static TMP_Text MakeText(Transform parent, string name, float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = size;
            t.alignment = alignment;
            t.color = Ink;
            t.richText = true;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.margin = Vector4.zero;
            return t;
        }
    }
}
