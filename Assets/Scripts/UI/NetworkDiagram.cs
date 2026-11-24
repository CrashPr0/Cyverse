using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Cyverse.UI
{
    /// <summary>
    /// Renders <see cref="NetworkDiagramArt"/> once into a texture that the
    /// Level 4 pinball backglass and the Network Attack round's map share.
    ///
    /// The art is laid out as UI on an off-screen canvas (shapes as meshes,
    /// labels in Inter) and drawn by a private camera at <see cref="Scale"/>
    /// times the original size with MSAA, then copied into a mipmapped
    /// texture so it stays smooth both full screen and on the far glass.
    /// </summary>
    public static class NetworkDiagram
    {
        /// <summary>Pixels per diagram pixel in the cached texture.</summary>
        public const int Scale = 2;

        /// <summary>Line-box centre to baseline, in ems, for the legacy text
        /// renderer with Inter (hhea ascent 0.96875, height 1.2109).</summary>
        public static float BaselineFromCentre = 0.3638f;

        private const string FontFolder = "Fonts/Diagram/Inter-";
        private static readonly string[] Weights = { "Regular", "SemiBold", "Bold" };
        private static Texture2D cached;

        public static float Aspect => NetworkDiagramArt.Width / NetworkDiagramArt.Height;

        /// <summary>The diagram texture, rendered on first use.</summary>
        public static Texture2D Texture
        {
            get
            {
                if (cached == null) cached = Render(Scale);
                return cached;
            }
        }

        /// <summary>Renders the diagram at <paramref name="scale"/> times its
        /// original 1199 x 535 size. Without a graphics device (headless test
        /// runs) it returns a small blank texture instead.</summary>
        public static Texture2D Render(int scale)
        {
            int w = Mathf.RoundToInt(NetworkDiagramArt.Width * scale);
            int h = Mathf.RoundToInt(NetworkDiagramArt.Height * scale);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                var blank = new Texture2D(4, 4, TextureFormat.RGB24, false) { name = "NetworkDiagram (no GPU)" };
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = NetworkDiagramArt.Paper;
                blank.SetPixels32(pixels);
                blank.Apply();
                return blank;
            }

            // Far below the level so no gameplay camera ever sees it.
            var origin = new Vector3(0f, -5000f, 0f);
            int layer = LayerMask.NameToLayer("UI");
            if (layer < 0) layer = 5;

            var root = new GameObject("NetworkDiagramCanvas", typeof(RectTransform), typeof(Canvas));
            root.layer = layer;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = root.GetComponent<RectTransform>();
            rt.position = origin;
            rt.sizeDelta = new Vector2(w, h);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var painter = new UiPainter(rt, scale, layer);
            NetworkDiagramArt.Paint(painter);
            painter.Flush();

            var camGo = new GameObject("NetworkDiagramCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = h * 0.5f;
            cam.transform.position = origin + new Vector3(0f, 0f, -10f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;
            cam.cullingMask = 1 << layer;
            cam.allowHDR = false;
            cam.allowMSAA = true;

            var target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                name = "NetworkDiagramRT",
            };
            target.Create();
            cam.targetTexture = target;
            cam.aspect = (float)w / h;
            Canvas.ForceUpdateCanvases();
            cam.Render();

            // Resolve the multisampled target before reading it back.
            var resolved = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(target, resolved);
            var previous = RenderTexture.active;
            RenderTexture.active = resolved;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, true)
            {
                name = "NetworkDiagram",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            tex.Apply(true, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(resolved);

            cam.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(camGo);
            Object.Destroy(root);
            return tex;
        }

        // ---- Painter --------------------------------------------------------------------

        /// <summary>Builds the art as UI: runs of shapes share one mesh, and a
        /// label starts a new run so later shapes still draw on top of it.</summary>
        private sealed class UiPainter : IDiagramPainter
        {
            private readonly RectTransform canvas;
            private readonly float s;
            private readonly int layer;
            private readonly Font[] fonts = new Font[3];
            private DiagramShapeMesh run;
            private int count;

            public UiPainter(RectTransform canvas, float scale, int layer)
            {
                this.canvas = canvas;
                s = scale;
                this.layer = layer;
                for (int i = 0; i < 3; i++)
                {
                    fonts[i] = Resources.Load<Font>(FontFolder + Weights[i]);
                    if (fonts[i] == null) fonts[i] = HudUI.LoadFont();
                }
            }

            private Vector2 P(float x, float y) => new Vector2(x * s, -y * s);

            private DiagramShapeMesh Run()
            {
                if (run != null) return run;
                var go = new GameObject("Shapes" + count++, typeof(RectTransform), typeof(CanvasRenderer));
                go.layer = layer;
                go.transform.SetParent(canvas, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0f, 1f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                run = go.AddComponent<DiagramShapeMesh>();
                run.raycastTarget = false;
                return run;
            }

            public void Flush()
            {
                if (run != null) run.SetVerticesDirty();
                run = null;
            }

            public void Rect(float x, float y, float w, float h, Color32 c)
            {
                Run().Quad(P(x, y), P(x + w, y), P(x + w, y + h), P(x, y + h), c);
            }

            public void Frame(float x, float y, float w, float h, float t, Color32 c)
            {
                float t2 = t * 0.5f;
                Rect(x - t2, y - t2, w + t, t, c);
                Rect(x - t2, y + h - t2, w + t, t, c);
                Rect(x - t2, y - t2, t, h + t, c);
                Rect(x + w - t2, y - t2, t, h + t, c);
            }

            public void Line(float t, Color32 c, params float[] pts)
            {
                for (int i = 0; i + 3 < pts.Length; i += 2)
                {
                    var a = new Vector2(pts[i], pts[i + 1]);
                    var b = new Vector2(pts[i + 2], pts[i + 3]);
                    Vector2 u = (b - a).normalized;
                    Vector2 n = new Vector2(-u.y, u.x) * (t * 0.5f);
                    Vector2 e = u * (t * 0.5f);
                    Vector2 q0 = a - e + n, q1 = b + e + n, q2 = b + e - n, q3 = a - e - n;
                    Run().Quad(P(q0.x, q0.y), P(q1.x, q1.y), P(q2.x, q2.y), P(q3.x, q3.y), c);
                }
            }

            public void Poly(Color32 c, params float[] pts)
            {
                var v = new List<Vector2>(pts.Length / 2);
                for (int i = 0; i + 1 < pts.Length; i += 2) v.Add(P(pts[i], pts[i + 1]));
                Run().Fan(v, c);
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
            {
                int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(rx, ry) * s * 1.6f), 16, 96);
                var v = new List<Vector2>(n);
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    v.Add(P(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
                }
                Run().Fan(v, c);
            }

            public void Text(float cx, float baseline, string text, float size, int weight, Color32 c)
            {
                Flush();
                var go = new GameObject("Label_" + text, typeof(RectTransform));
                go.layer = layer;
                go.transform.SetParent(canvas, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                float px = size * s;
                int fontSize = Mathf.Max(1, Mathf.RoundToInt(px));
                float k = px / fontSize;
                rt.localScale = new Vector3(k, k, 1f);
                rt.sizeDelta = new Vector2(text.Length * fontSize + 20f, fontSize * 2f);
                rt.anchoredPosition = P(cx, baseline - BaselineFromCentre * size);
                var label = go.AddComponent<Text>();
                label.font = fonts[Mathf.Clamp(weight, 0, 2)];
                label.fontSize = fontSize;
                label.fontStyle = FontStyle.Normal;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.supportRichText = false;
                label.raycastTarget = false;
                label.color = c;
                label.text = text;
            }
        }
    }
}
