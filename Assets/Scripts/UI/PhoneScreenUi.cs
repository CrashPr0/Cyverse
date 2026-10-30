using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cyverse.UI
{
    /// <summary>
    /// The Spartan Authenticator phone's screen, shared by the phone beside the
    /// passcode card and the in-world phone's RenderTexture so both read as the
    /// same device: a status bar (clock, signal, battery), a coloured app title
    /// band, the code card with a refresh bar, a status line, and a home-screen
    /// grid of app icons with the authenticator selected.
    ///
    /// Everything is drawn from a few procedural sprites (rounded rect, soft
    /// shadow, ring, gradient) so no texture assets are needed. The screen is
    /// laid out in a fixed 200 x 400 design space; BuildDevice wraps it in a
    /// rounded case with rim, side buttons and a drop shadow that matches the
    /// 3D phone's dark navy shell.
    /// </summary>
    public sealed class PhoneScreenUi
    {
        public static readonly Vector2 ScreenSize = new Vector2(200f, 400f);
        public static readonly Vector2 DeviceSize = new Vector2(220f, 440f);

        /// <summary>The 3D phone shell's colour; the UI case matches it.</summary>
        public static readonly Color CaseColor = new Color(0.05f, 0.06f, 0.09f, 1f);

        private static readonly Color RimColor = new Color(0.20f, 0.23f, 0.30f, 1f);
        private static readonly Color CodeGreen = new Color(0.30f, 1f, 0.45f, 1f);
        private static readonly Color AppTeal = new Color(0.05f, 0.42f, 0.36f, 1f);

        public Text Code { get; private set; }
        public Text Status { get; private set; }
        public Text Clock { get; private set; }

        private static Sprite rounded, softShadow, ring;
        private static Texture2D wallpaper;

        // ---- Public builders -------------------------------------------------

        /// <summary>The full handheld device: drop shadow, side buttons, rim,
        /// case and the screen. Returns the device root (DeviceSize, pivot
        /// centre); the caller anchors and positions it.</summary>
        public static RectTransform BuildDevice(Transform parent, out PhoneScreenUi ui)
        {
            RectTransform device = BuildCase(parent, out RectTransform screen);
            ui = Build(screen);
            return device;
        }

        /// <summary>Drop shadow, side buttons, rim and case, with an empty
        /// ScreenSize rect centred in it.</summary>
        private static RectTransform BuildCase(Transform parent, out RectTransform screen)
        {
            var device = Rect("PhoneDevice", parent);
            device.sizeDelta = DeviceSize;

            // A soft shadow down and to the right lifts the phone off the card.
            var shadow = Img("Shadow", device, SoftShadow(), new Color(0f, 0f, 0f, 0.45f));
            Stretch(shadow.rectTransform, new Vector2(-14f, -26f), new Vector2(22f, 8f));
            shadow.type = Image.Type.Sliced;

            // Side buttons sit behind the case and poke out of its edges.
            SideButton(device, "VolumeUp", -1, 92f, 34f);
            SideButton(device, "VolumeDown", -1, 136f, 34f);
            SideButton(device, "Power", 1, 110f, 50f);

            // Rim (lit metal edge) then the case inset inside it.
            var rim = Img("Rim", device, Rounded(), RimColor);
            Stretch(rim.rectTransform, Vector2.zero, Vector2.zero);
            SetRadius(rim, 34f);

            var body = Img("Case", device, Rounded(), CaseColor);
            Stretch(body.rectTransform, new Vector2(2.5f, 2.5f), new Vector2(-2.5f, -2.5f));
            SetRadius(body, 32f);

            screen = Rect("Screen", body.rectTransform);
            screen.sizeDelta = ScreenSize;
            screen.anchoredPosition = Vector2.zero;
            return device;
        }

        /// <summary>An empty handheld for other phone apps: the same case as
        /// <see cref="BuildDevice"/> (DeviceSize), with the status bar and home
        /// indicator on a rounded, masked glass of ScreenSize for the caller to
        /// fill. Scale the returned device root to size it on screen.</summary>
        public static RectTransform BuildShell(Transform parent, Color glassColor, out RectTransform glass)
        {
            RectTransform device = BuildCase(parent, out RectTransform screen);
            var pane = Img("Glass", screen, Rounded(), glassColor);
            Stretch(pane.rectTransform, Vector2.zero, Vector2.zero);
            SetRadius(pane, 26f);
            pane.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            glass = pane.rectTransform;

            BuildStatusBar(glass, new PhoneScreenUi());
            var home = Img("HomeIndicator", glass, Rounded(), new Color(1f, 1f, 1f, 0.7f));
            var hr = home.rectTransform;
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0f);
            hr.sizeDelta = new Vector2(70f, 4f); hr.anchoredPosition = new Vector2(0f, 6f);
            SetRadius(home, 2f);
            return device;
        }

        /// <summary>A rounded-rectangle image (corner radius in UI units) for
        /// apps drawn inside <see cref="BuildShell"/>.</summary>
        public static Image RoundedImage(string name, Transform parent, Color color, float radius)
        {
            var img = Img(name, parent, Rounded(), color);
            SetRadius(img, radius);
            return img;
        }

        /// <summary>Fill <paramref name="screen"/> (sized ScreenSize) with the
        /// phone's display, clipped to rounded corners.</summary>
        public static PhoneScreenUi Build(RectTransform screen)
        {
            var ui = new PhoneScreenUi();

            // The display glass: a rounded mask holding the wallpaper and all
            // content so nothing pokes past the curved corners.
            var glass = Img("Glass", screen, Rounded(), new Color(0.02f, 0.03f, 0.06f, 1f));
            Stretch(glass.rectTransform, Vector2.zero, Vector2.zero);
            SetRadius(glass, 26f);
            var mask = glass.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            Transform root = glass.transform;

            var wall = new GameObject("Wallpaper", typeof(RectTransform), typeof(RawImage));
            wall.transform.SetParent(root, false);
            var wallImg = wall.GetComponent<RawImage>();
            wallImg.texture = Wallpaper();
            wallImg.raycastTarget = false;
            Stretch(wallImg.rectTransform, Vector2.zero, Vector2.zero);

            BuildStatusBar(root, ui);
            BuildTitleBand(root);
            BuildCodeCard(root, ui);

            ui.Status = MakeText(root, "PhoneStatus", 12, TextAnchor.MiddleCenter);
            ui.Status.color = new Color(0.62f, 0.76f, 0.86f);
            var st = ui.Status.rectTransform;
            TopBand(st, -194f, 24f, 10f);
            ui.Status.text = "";

            BuildAppGrid(root);

            // The home indicator.
            var home = Img("HomeIndicator", root, Rounded(), new Color(1f, 1f, 1f, 0.7f));
            var hr = home.rectTransform;
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0f);
            hr.sizeDelta = new Vector2(70f, 4f); hr.anchoredPosition = new Vector2(0f, 10f);
            SetRadius(home, 2f);

            return ui;
        }

        /// <summary>Refresh the status-bar clock to the current time.</summary>
        public void RefreshClock()
        {
            if (Clock != null) Clock.text = DateTime.Now.ToString("H:mm");
        }

        // ---- Sections --------------------------------------------------------

        private static void BuildStatusBar(Transform root, PhoneScreenUi ui)
        {
            // Speaker pill and front camera, centred in the bar like a notch.
            var pill = Img("Notch", root, Rounded(), new Color(0f, 0f, 0f, 1f));
            var pr = pill.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f); pr.pivot = new Vector2(0.5f, 1f);
            pr.sizeDelta = new Vector2(58f, 16f); pr.anchoredPosition = new Vector2(0f, -6f);
            SetRadius(pill, 8f);
            var cam = Img("Camera", pr, Rounded(), new Color(0.10f, 0.14f, 0.24f, 1f));
            var cr = cam.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 0.5f);
            cr.sizeDelta = new Vector2(7f, 7f); cr.anchoredPosition = new Vector2(-9f, 0f);
            SetRadius(cam, 3.5f);

            ui.Clock = MakeText(root, "Clock", 12, TextAnchor.MiddleLeft);
            ui.Clock.fontStyle = FontStyle.Bold;
            var kr = ui.Clock.rectTransform;
            kr.anchorMin = kr.anchorMax = new Vector2(0f, 1f); kr.pivot = new Vector2(0f, 1f);
            kr.sizeDelta = new Vector2(60f, 22f); kr.anchoredPosition = new Vector2(18f, -3f);
            ui.RefreshClock();

            // Signal bars: four bars rising left to right.
            for (int i = 0; i < 4; i++)
            {
                var bar = Img("Signal" + i, root, Rounded(), Color.white);
                var br = bar.rectTransform;
                br.anchorMin = br.anchorMax = new Vector2(1f, 1f); br.pivot = new Vector2(0.5f, 0f);
                br.sizeDelta = new Vector2(3f, 3f + i * 2.2f);
                br.anchoredPosition = new Vector2(-62f + i * 4.5f, -17f);
                SetRadius(bar, 1f);
            }

            // Battery: outline, fill and nub.
            var batt = Img("Battery", root, Rounded(), new Color(1f, 1f, 1f, 0.5f));
            var ba = batt.rectTransform;
            ba.anchorMin = ba.anchorMax = new Vector2(1f, 1f); ba.pivot = new Vector2(1f, 0.5f);
            ba.sizeDelta = new Vector2(22f, 11f); ba.anchoredPosition = new Vector2(-20f, -13f);
            SetRadius(batt, 3f);
            var inner = Img("BatteryInner", ba, Rounded(), new Color(0.02f, 0.03f, 0.06f, 1f));
            Stretch(inner.rectTransform, new Vector2(1.2f, 1.2f), new Vector2(-1.2f, -1.2f));
            SetRadius(inner, 2f);
            var fill = Img("BatteryFill", ba, Rounded(), Color.white);
            var fr = fill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(0.78f, 1f);
            fr.offsetMin = new Vector2(2.2f, 2.2f); fr.offsetMax = new Vector2(0f, -2.2f);
            SetRadius(fill, 1f);
            var nub = Img("BatteryNub", ba, Rounded(), new Color(1f, 1f, 1f, 0.5f));
            var nr = nub.rectTransform;
            nr.anchorMin = nr.anchorMax = new Vector2(1f, 0.5f); nr.pivot = new Vector2(0f, 0.5f);
            nr.sizeDelta = new Vector2(2f, 5f); nr.anchoredPosition = new Vector2(0.6f, 0f);
            SetRadius(nub, 1f);
        }

        /// <summary>The open app's coloured title band, GTA-phone style.</summary>
        private static void BuildTitleBand(Transform root)
        {
            var band = Img("AppTitle", root, Rounded(), AppTeal);
            var r = band.rectTransform;
            TopBand(r, -30f, 36f, 10f);
            SetRadius(band, 10f);

            var icon = Img("AppIcon", r, Rounded(), new Color(1f, 1f, 1f, 0.16f));
            var ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f); ir.pivot = new Vector2(0f, 0.5f);
            ir.sizeDelta = new Vector2(24f, 24f); ir.anchoredPosition = new Vector2(7f, 0f);
            SetRadius(icon, 7f);
            LockGlyph(ir, 0.55f, Color.white);

            var title = MakeText(r, "AppName", 13, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            var tr = title.rectTransform;
            Stretch(tr, new Vector2(38f, 0f), new Vector2(-4f, 0f));
            title.text = "AUTHENTICATOR";
        }

        private static void BuildCodeCard(Transform root, PhoneScreenUi ui)
        {
            var card = Img("CodeCard", root, Rounded(), new Color(0.03f, 0.08f, 0.12f, 0.92f));
            var r = card.rectTransform;
            TopBand(r, -72f, 116f, 10f);
            SetRadius(card, 12f);
            AddShadow(card, 2f, 0.35f);

            var label = MakeText(r, "OtpLabel", 10, TextAnchor.MiddleCenter);
            label.color = new Color(0.50f, 0.80f, 0.90f);
            TopBand(label.rectTransform, -10f, 18f, 6f);
            label.text = "DAILY VERIFICATION CODE";

            ui.Code = MakeText(r, "OtpCode", 40, TextAnchor.MiddleCenter);
            ui.Code.fontStyle = FontStyle.Bold;
            ui.Code.color = CodeGreen;
            var cr = ui.Code.rectTransform;
            TopBand(cr, -32f, 60f, 4f);
            ui.Code.text = "----";

            // The refresh bar shows the code is short-lived.
            var track = Img("RefreshTrack", r, Rounded(), new Color(1f, 1f, 1f, 0.10f));
            TopBand(track.rectTransform, -100f, 5f, 22f);
            SetRadius(track, 2.5f);
            var fill = Img("RefreshFill", track.rectTransform, Rounded(), CodeGreen);
            var fr = fill.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0.68f, 1f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            SetRadius(fill, 2.5f);
        }

        private enum Glyph { Lock, Mail, Chat, Web, Files, Gear }

        /// <summary>A 3 x 2 home-screen grid with the authenticator selected.</summary>
        private static void BuildAppGrid(Transform root)
        {
            var grid = Rect("AppGrid", root);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 0f); grid.pivot = new Vector2(0.5f, 0f);
            grid.sizeDelta = new Vector2(180f, 130f); grid.anchoredPosition = new Vector2(0f, 38f);

            (string name, Color color, Glyph glyph)[] apps =
            {
                ("AUTH", AppTeal, Glyph.Lock),
                ("MAIL", new Color(0.16f, 0.42f, 0.90f), Glyph.Mail),
                ("CHAT", new Color(0.18f, 0.66f, 0.30f), Glyph.Chat),
                ("WEB", new Color(0.92f, 0.48f, 0.12f), Glyph.Web),
                ("FILES", new Color(0.90f, 0.70f, 0.15f), Glyph.Files),
                ("SETTINGS", new Color(0.38f, 0.42f, 0.50f), Glyph.Gear),
            };
            for (int i = 0; i < apps.Length; i++)
            {
                int col = i % 3, row = i / 3;
                var cell = Rect("App_" + apps[i].name, grid);
                cell.anchorMin = cell.anchorMax = new Vector2(0.5f, 1f); cell.pivot = new Vector2(0.5f, 1f);
                cell.sizeDelta = new Vector2(56f, 62f);
                cell.anchoredPosition = new Vector2((col - 1) * 60f, -row * 66f);

                if (i == 0)
                {
                    // Selection ring, as the GTA phone highlights the chosen app.
                    var sel = Img("Selected", cell, Rounded(), new Color(1f, 1f, 1f, 0.45f));
                    var sr = sel.rectTransform;
                    sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 1f); sr.pivot = new Vector2(0.5f, 1f);
                    sr.sizeDelta = new Vector2(48f, 48f); sr.anchoredPosition = new Vector2(0f, 2f);
                    SetRadius(sel, 14f);
                }

                var icon = Img("Icon", cell, Rounded(), apps[i].color);
                var ir = icon.rectTransform;
                ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 1f); ir.pivot = new Vector2(0.5f, 1f);
                ir.sizeDelta = new Vector2(42f, 42f); ir.anchoredPosition = new Vector2(0f, -1f);
                SetRadius(icon, 12f);

                DrawGlyph(ir, apps[i].glyph);

                var label = MakeText(cell, "Label", 10, TextAnchor.MiddleCenter);
                label.color = new Color(0.86f, 0.92f, 0.98f);
                var lr = label.rectTransform;
                lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0f); lr.pivot = new Vector2(0.5f, 0f);
                lr.sizeDelta = new Vector2(8f, 14f); lr.anchoredPosition = Vector2.zero;
                label.text = apps[i].name;
            }
        }

        // ---- Glyphs (white shapes on the app icons) --------------------------

        private static void DrawGlyph(RectTransform icon, Glyph glyph)
        {
            Color w = Color.white;
            switch (glyph)
            {
                case Glyph.Lock:
                    LockGlyph(icon, 1f, w);
                    break;
                case Glyph.Mail:
                    Shape(icon, "Envelope", new Vector2(0f, 0f), new Vector2(24f, 16f), 0f, w, 2.5f);
                    Shape(icon, "FlapL", new Vector2(-5.5f, 2.5f), new Vector2(14f, 2.5f), -32f, new Color(0.16f, 0.42f, 0.90f), 1f);
                    Shape(icon, "FlapR", new Vector2(5.5f, 2.5f), new Vector2(14f, 2.5f), 32f, new Color(0.16f, 0.42f, 0.90f), 1f);
                    break;
                case Glyph.Chat:
                    Shape(icon, "Bubble", new Vector2(0f, 2f), new Vector2(24f, 17f), 0f, w, 7f);
                    Shape(icon, "Tail", new Vector2(-6f, -6.5f), new Vector2(7f, 7f), 45f, w, 1f);
                    break;
                case Glyph.Web:
                {
                    var globe = Img("Globe", icon, Ring(), w);
                    var gr = globe.rectTransform;
                    gr.sizeDelta = new Vector2(24f, 24f); gr.anchoredPosition = Vector2.zero;
                    Shape(icon, "Meridian", Vector2.zero, new Vector2(2.5f, 22f), 0f, w, 1f);
                    Shape(icon, "Equator", Vector2.zero, new Vector2(22f, 2.5f), 0f, w, 1f);
                    break;
                }
                case Glyph.Files:
                    Shape(icon, "Tab", new Vector2(-5f, 7f), new Vector2(11f, 6f), 0f, w, 2f);
                    Shape(icon, "Folder", new Vector2(0f, -1f), new Vector2(26f, 16f), 0f, w, 3f);
                    break;
                case Glyph.Gear:
                {
                    for (int a = 0; a < 4; a++)
                        Shape(icon, "Tooth" + a, Vector2.zero, new Vector2(5f, 26f), a * 45f, w, 1.5f);
                    var hub = Img("Hub", icon, Ring(), w);
                    var hr = hub.rectTransform;
                    hr.sizeDelta = new Vector2(20f, 20f); hr.anchoredPosition = Vector2.zero;
                    Shape(icon, "HubFill", Vector2.zero, new Vector2(10f, 10f), 0f, w, 5f);
                    Shape(icon, "Hole", Vector2.zero, new Vector2(7f, 7f), 0f, new Color(0.38f, 0.42f, 0.50f), 3.5f);
                    break;
                }
            }
        }

        private static void LockGlyph(RectTransform icon, float scale, Color c)
        {
            var shackle = Img("Shackle", icon, Ring(), c);
            var sr = shackle.rectTransform;
            sr.sizeDelta = new Vector2(16f, 18f) * scale; sr.anchoredPosition = new Vector2(0f, 5f) * scale;
            Shape(icon, "LockBody", new Vector2(0f, -4f) * scale, new Vector2(22f, 15f) * scale, 0f, c, 3f * scale);
            Shape(icon, "Keyhole", new Vector2(0f, -4f) * scale, new Vector2(3f, 6f) * scale, 0f,
                new Color(0.05f, 0.42f, 0.36f), 1f);
        }

        private static void Shape(RectTransform parent, string name, Vector2 pos, Vector2 size,
            float rotation, Color color, float radius)
        {
            var img = Img(name, parent, Rounded(), color);
            var r = img.rectTransform;
            r.sizeDelta = size; r.anchoredPosition = pos;
            r.localRotation = Quaternion.Euler(0f, 0f, rotation);
            SetRadius(img, radius);
        }

        // ---- Small helpers ---------------------------------------------------

        private static void SideButton(RectTransform device, string name, int side, float fromTop, float length)
        {
            var b = Img(name, device, Rounded(), new Color(0.16f, 0.19f, 0.25f, 1f));
            var r = b.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(side < 0 ? 0f : 1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(6f, length);
            r.anchoredPosition = new Vector2(side * 1f, -fromTop);
            SetRadius(b, 2f);
        }

        /// <summary>A full-width band from the top: y offset from the top
        /// edge, height, and horizontal inset on each side.</summary>
        private static void TopBand(RectTransform r, float top, float height, float inset)
        {
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(-inset * 2f, height); r.anchoredPosition = new Vector2(0f, top);
        }

        private static void AddShadow(Graphic g, float distance, float alpha)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, alpha);
            s.effectDistance = new Vector2(distance * 0.5f, -distance);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite == rounded) img.type = Image.Type.Sliced;
            return img;
        }

        private static void Stretch(RectTransform r, Vector2 min, Vector2 max)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = min; r.offsetMax = max;
        }

        /// <summary>Corner radius in UI units for a sliced rounded sprite.</summary>
        private static void SetRadius(Image img, float radius)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = RoundedBorder / Mathf.Max(0.5f, radius);
        }

        private static Text MakeText(Transform parent, string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = HudUI.UIFont != null ? HudUI.UIFont : HudUI.LoadFont();
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ---- Procedural sprites ----------------------------------------------

        private const int RoundedSize = 64;
        private const float RoundedBorder = 24f;

        /// <summary>White rounded rectangle, 9-sliced so any size keeps its
        /// corners; SetRadius scales the corner via pixelsPerUnitMultiplier.</summary>
        private static Sprite Rounded()
        {
            if (rounded != null) return rounded;
            int n = RoundedSize;
            float r = RoundedBorder;
            var tex = NewTex(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f, y + 0.5f, n, n, r);
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            tex.SetPixels(px); tex.Apply();
            rounded = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return rounded;
        }

        /// <summary>A blurred rounded rectangle for drop shadows and glows.</summary>
        private static Sprite SoftShadow()
        {
            if (softShadow != null) return softShadow;
            const int n = 96;
            const float blur = 28f;
            var tex = NewTex(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f - blur, y + 0.5f - blur, n - 2f * blur, n - 2f * blur, 18f);
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + 4f) / blur));
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            softShadow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(40f, 40f, 40f, 40f));
            return softShadow;
        }

        /// <summary>A white antialiased ring (lock shackle, globe, gear).</summary>
        private static Sprite Ring()
        {
            if (ring != null) return ring;
            const int n = 64;
            var tex = NewTex(n, n);
            var px = new Color[n * n];
            float c = n * 0.5f, outer = n * 0.5f - 1f, inner = outer * 0.70f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return ring;
        }

        /// <summary>The wallpaper: a deep navy vertical gradient.</summary>
        private static Texture2D Wallpaper()
        {
            if (wallpaper != null) return wallpaper;
            const int h = 128;
            wallpaper = NewTex(4, h);
            var px = new Color[4 * h];
            Color top = new Color(0.05f, 0.12f, 0.20f), bottom = new Color(0.015f, 0.02f, 0.05f);
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, y / (h - 1f));
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            wallpaper.SetPixels(px); wallpaper.Apply();
            return wallpaper;
        }

        private static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }

        /// <summary>Signed distance from (x, y) to a w x h rounded rect with
        /// corner radius r whose lower-left corner is at the origin.</summary>
        private static float RoundedRectDistance(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }
}
