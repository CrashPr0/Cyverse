using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// A stylized dot-matrix LED sign that scrolls one message, like the
    /// "GAME ROOM" sign over the game room door at a big-tech campus. The
    /// dots are one small runtime texture on an emissive Standard quad,
    /// redrawn a column at a time, so the whole sign is one texture and one
    /// material with no imported art. Unlit dots stay faintly visible so it
    /// reads as a real LED board rather than floating text.
    /// </summary>
    public sealed class LedMarquee : MonoBehaviour
    {
        private const int GlyphRows = 7;
        private const int PadRows = 1;
        private const int Rows = GlyphRows + PadRows * 2;
        // Each dot is DotPixels square in the texture: a rounded 4x4 lamp and
        // a one-pixel dark gap, so the grid survives bilinear filtering.
        private const int DotPixels = 5;

        public string message = "GAME ROOM";
        public Color litColor = new Color(1f, 0.45f, 0.18f);
        public float stepSeconds = 0.075f;
        /// <summary>True moves the words left to right across the board.</summary>
        public bool scrollRight = true;

        private int columns;
        private byte[] strip;
        private int offset;
        private float timer;
        private Texture2D texture;
        private Color32[] pixels;

        public Texture2D Texture => texture;

        /// <summary>Builds a framed sign <paramref name="width"/> by
        /// <paramref name="height"/> metres. The lit face reads from local -Z,
        /// like the other mounted signs; <paramref name="twoSided"/> adds a
        /// second face on +Z for players walking back the other way.</summary>
        public static LedMarquee Build(Transform parent, string name, Vector3 position, float rotY,
            float width, float height, string message, Color litColor, bool twoSided)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotY, 0f));

            Material housing = Palette.Get("LedHousing", new Color(0.035f, 0.035f, 0.045f), 0.5f, 0.4f);
            Material trim = BuildKit.MakeEmissive(litColor, 0.9f);
            const float depth = 0.22f;
            BlockKit.Slab("Housing", root.transform, Vector3.zero,
                new Vector3(width + 0.28f, height + 0.28f, depth), 0.04f, housing);
            foreach (float side in twoSided ? new[] { -1f, 1f } : new[] { -1f })
            {
                float z = side * (depth * 0.5f + 0.004f);
                BlockKit.Box(side < 0f ? "TrimTop" : "TrimTop_Back", root.transform,
                    new Vector3(0f, height * 0.5f + 0.1f, z), new Vector3(width + 0.2f, 0.025f, 0.008f), trim);
                BlockKit.Box(side < 0f ? "TrimBottom" : "TrimBottom_Back", root.transform,
                    new Vector3(0f, -height * 0.5f - 0.1f, z), new Vector3(width + 0.2f, 0.025f, 0.008f), trim);
            }

            var marquee = root.AddComponent<LedMarquee>();
            marquee.message = message;
            marquee.litColor = litColor;
            float pitch = height / Rows;
            marquee.columns = Mathf.Max(GlyphColumns(message) + 4, Mathf.RoundToInt(width / pitch));
            marquee.Prepare();

            var material = new Material(Shader.Find("Standard")) { name = BuildKit.GlowMaterialName };
            material.color = Color.black;
            material.SetFloat("_Glossiness", 0.35f);
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", marquee.texture);
            material.SetColor("_EmissionColor", Color.white * (1.8f * BuildKit.GlowLevel));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            BuildKit.SpawnLocal(PrimitiveType.Quad, "LedFace", root.transform,
                new Vector3(0f, 0f, -depth * 0.5f - 0.006f), Vector3.zero,
                new Vector3(width, height, 1f), material, false);
            // Turned half a turn, the back face's +X runs the other way in
            // the world, so the same texture still reads left to right.
            if (twoSided)
                BuildKit.SpawnLocal(PrimitiveType.Quad, "LedFace_Back", root.transform,
                    new Vector3(0f, 0f, depth * 0.5f + 0.006f), new Vector3(0f, 180f, 0f),
                    new Vector3(width, height, 1f), material, false);
            return marquee;
        }

        private void Start()
        {
            // Build prepares the board; this covers a sign added by hand.
            if (texture != null) return;
            if (columns <= 0) columns = GlyphColumns(message) + 24;
            Prepare();
        }

        private void Prepare()
        {
            strip = Render(message, columns);
            offset = 0;
            texture = new Texture2D(columns * DotPixels, Rows * DotPixels, TextureFormat.RGBA32, false)
            {
                name = "LedMarquee_" + message,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[texture.width * texture.height];
            Redraw();
        }

        private void Update()
        {
            if (strip == null || strip.Length == 0) return;
            timer += Time.deltaTime;
            if (timer < stepSeconds) return;
            timer -= stepSeconds;
            if (timer > stepSeconds) timer = 0f; // a long frame skips, not races
            offset = (offset + 1) % strip.Length;
            Redraw();
        }

        private void Redraw()
        {
            Color32 lit = litColor;
            Color32 dim = Color.Lerp(Color.black, litColor, 0.09f);
            Color32 gap = Color.black;
            int width = columns * DotPixels;
            for (int c = 0; c < columns; c++)
            {
                // Scrolling right: the board shows the strip shifted right by
                // offset, so each column reads from offset columns back.
                int source = scrollRight ? c - offset : c + offset;
                source = ((source % strip.Length) + strip.Length) % strip.Length;
                byte bits = strip[source];
                for (int r = 0; r < Rows; r++)
                {
                    int glyphRow = r - PadRows;
                    bool on = glyphRow >= 0 && glyphRow < GlyphRows && (bits & (1 << glyphRow)) != 0;
                    Color32 lamp = on ? lit : dim;
                    // Texture rows run bottom-up; glyph rows run top-down.
                    int y0 = (Rows - 1 - r) * DotPixels;
                    int x0 = c * DotPixels;
                    for (int py = 0; py < DotPixels; py++)
                    {
                        for (int px = 0; px < DotPixels; px++)
                        {
                            bool edge = px == DotPixels - 1 || py == DotPixels - 1;
                            bool corner = (px == 0 || px == DotPixels - 2) && (py == 0 || py == DotPixels - 2);
                            pixels[(y0 + py) * width + x0 + px] = edge || corner ? gap : lamp;
                        }
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        /// <summary>The message as one byte per column (bit 0 = top row),
        /// with a blank run after it so one copy crosses the board at a time.</summary>
        private static byte[] Render(string text, int boardColumns)
        {
            var columnsOut = new System.Collections.Generic.List<byte>();
            foreach (char ch in text.ToUpperInvariant())
            {
                string[] glyph = Glyph(ch);
                int glyphWidth = glyph[0].Length;
                for (int x = 0; x < glyphWidth; x++)
                {
                    byte bits = 0;
                    for (int y = 0; y < GlyphRows; y++)
                        if (glyph[y][x] == '#') bits |= (byte)(1 << y);
                    columnsOut.Add(bits);
                }
                columnsOut.Add(0);
            }
            while (columnsOut.Count < boardColumns) columnsOut.Add(0);
            return columnsOut.ToArray();
        }

        private static int GlyphColumns(string text)
        {
            int count = 0;
            foreach (char ch in text.ToUpperInvariant()) count += Glyph(ch)[0].Length + 1;
            return count;
        }

        // 5x7 capitals. Only the letters the signs use are drawn; anything
        // else is a space.
        private static string[] Glyph(char ch)
        {
            switch (ch)
            {
                case 'A': return new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" };
                case 'E': return new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" };
                case 'G': return new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###." };
                case 'M': return new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" };
                case 'O': return new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." };
                case 'R': return new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" };
                case '*': return new[] { ".....", "..#..", ".###.", "#####", ".###.", "..#..", "....." };
                default: return new[] { "...", "...", "...", "...", "...", "...", "..." };
            }
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }
    }
}
