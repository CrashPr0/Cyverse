using System.Collections.Generic;
using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// The game room's shell and atmosphere, built around the props in
    /// Level4GameRoom. The room used to be a pale floor under a black void:
    /// the walls, columns and ceiling were all near-black, so the machines
    /// had nothing to sit against. This gives the room a visible shell and
    /// some depth without adding anything to walk round:
    ///
    ///  - two-tone walls (a dusky violet band at eye level over a darker
    ///    wainscot with a cap rail), with the upper wall left dark so the
    ///    high ceiling recedes;
    ///  - arcade carpet under each row of machines and under the ping-pong
    ///    table, so the props are grounded and the pale floor reads as the
    ///    aisle between them;
    ///  - strings of warm festoon bulbs across the room below the ceiling;
    ///  - pixel-art light panels (padlock, key, shield, heart) on the game
    ///    floor side of the dividing wall;
    ///  - soft washes of light on the wall behind each neon sign and on the
    ///    floor under the machines and the bar lamps.
    ///
    /// Nothing here has a collider or touches gameplay.
    /// </summary>
    public static class Level4GameRoomAmbience
    {
        public const string RootName = "GameRoomAmbience";

        /// <summary>The wall band at eye level, and the dividing wall.</summary>
        public static readonly Color WallBand = new Color(0.25f, 0.21f, 0.345f);
        /// <summary>The wall above the light strip, and the base wall colour.</summary>
        public static readonly Color WallUpper = new Color(0.115f, 0.10f, 0.165f);
        public static readonly Color Wainscot = new Color(0.135f, 0.11f, 0.20f);
        public static readonly Color Rail = new Color(0.30f, 0.255f, 0.40f);
        public static readonly Color CarpetColour = new Color(0.105f, 0.075f, 0.165f);
        public static readonly Color Warm = new Color(1f, 0.80f, 0.55f);

        public const float WainscotHeight = 1.1f;
        /// <summary>Underside of the wall light strip (BuildKit.BuildWallDetail).</summary>
        private const float BandTop = 3.36f;

        private static Material WallBandMat => Palette.Get("GameWallBand", WallBand, 0.2f, 0f);
        private static Material WainscotMat => Palette.Get("GameWainscot", Wainscot, 0.35f, 0.05f);
        private static Material RailMat => Palette.Get("GameWallRail", Rail, 0.5f, 0.3f);
        private static Material ColumnMat => Palette.Get("GameColumn", new Color(0.15f, 0.125f, 0.215f), 0.45f, 0.2f);
        private static Material CarpetMat => Palette.Get("GameCarpet", CarpetColour, 0.08f, 0f);
        private static Material CableMat => Palette.Get("GameCable", new Color(0.05f, 0.045f, 0.07f), 0.3f, 0.1f);

        private static readonly Dictionary<Color, Material> flecks = new Dictionary<Color, Material>();

        private static Material Fleck(Color c)
        {
            if (flecks.TryGetValue(c, out Material m) && m != null) return m;
            return flecks[c] = BuildKit.MakeStandard(c, 0.1f, 0f);
        }

        public static void Build(Transform parent)
        {
            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);

            Shell(root);
            Carpets(root);
            Festoons(root);
            PixelArt(root);
            BarPools(root);
        }

        // ---- Shell --------------------------------------------------------------

        /// <summary>Bands on the four outer walls. The wall itself stays the
        /// dark upper colour; a thin lighter band covers it from the floor
        /// to the light strip, and the wainscot sits on that.</summary>
        private static void Shell(Transform root)
        {
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
                if (r.name.StartsWith("Column_")) r.sharedMaterial = ColumnMat;

            // The room's inner wall faces are at +-19.5; wall-mounted props
            // (neon backers, the lounge TV, the corkboard) start at 19.46, so
            // the bands stay within 4 cm of the wall.
            const float face = 19.5f, length = 39f;
            var band = new List<CombineInstance>();
            var wainscot = new List<CombineInstance>();
            var rail = new List<CombineInstance>();
            for (int i = 0; i < 4; i++)
            {
                bool alongX = i < 2;
                float sign = i % 2 == 0 ? 1f : -1f;
                Vector3 At(float y, float inset) => alongX
                    ? new Vector3(0f, y, sign * (face - inset))
                    : new Vector3(sign * (face - inset), y, 0f);
                Vector3 Size(float h, float depth) => alongX ? new Vector3(length, h, depth) : new Vector3(depth, h, length);

                band.Add(BlockKit.Piece(BlockKit.CubeMesh, At(BandTop * 0.5f, 0.01f), Vector3.zero, Size(BandTop, 0.02f)));
                wainscot.Add(BlockKit.Piece(BlockKit.CubeMesh, At(WainscotHeight * 0.5f, 0.02f), Vector3.zero, Size(WainscotHeight, 0.03f)));
                rail.Add(BlockKit.Piece(BlockKit.CubeMesh, At(WainscotHeight + 0.025f, 0.03f), Vector3.zero, Size(0.05f, 0.05f)));
            }
            BlockKit.Batch("WallBand", root, WallBandMat, band);
            BlockKit.Batch("Wainscot", root, WainscotMat, wainscot);
            BlockKit.Batch("WainscotRail", root, RailMat, rail);
        }

        /// <summary>The same wainscot and rail on both faces of a free-standing
        /// wall section centred on <paramref name="centre"/> (local to
        /// <paramref name="parent"/>), running along X.</summary>
        public static void WainscotBothFaces(Transform parent, string name, float centreX, float width, float halfThickness)
        {
            var wainscot = new List<CombineInstance>();
            var rail = new List<CombineInstance>();
            foreach (float side in new[] { -1f, 1f })
            {
                wainscot.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(centreX, WainscotHeight * 0.5f, side * (halfThickness + 0.01f)),
                    Vector3.zero, new Vector3(width, WainscotHeight, 0.02f)));
                rail.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(centreX, WainscotHeight + 0.025f, side * (halfThickness + 0.02f)),
                    Vector3.zero, new Vector3(width, 0.05f, 0.04f)));
            }
            BlockKit.Batch(name + "_Wainscot", parent, WainscotMat, wainscot);
            BlockKit.Batch(name + "_Rail", parent, RailMat, rail);
        }

        // ---- Carpet -------------------------------------------------------------

        private static void Carpets(Transform root)
        {
            // One carpet under each row of machines (stations at x +-10.5,
            // z 8 and 14). They stop short of the north-end decor and leave
            // the centre aisle as pale floor.
            Carpet(root, "Carpet_West", new Vector3(-9.7f, 0f, 10.4f), new Vector2(7.0f, 11.2f), Level4GameRoom.Magenta, 31);
            Carpet(root, "Carpet_East", new Vector3(9.7f, 0f, 10.4f), new Vector2(7.0f, 11.2f), Level4GameRoom.Teal, 47);
            Carpet(root, "Carpet_PingPong", new Vector3(12f, 0f, -12f), new Vector2(4.6f, 6.0f), Level4GameRoom.Teal, 59);
        }

        /// <summary>A dark carpet with a thin lit border and the scattered
        /// confetti flecks of an arcade carpet.</summary>
        private static void Carpet(Transform root, string name, Vector3 centre, Vector2 size, Color border, int seed)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = centre;

            BlockKit.Box("Pile", t, new Vector3(0f, 0.011f, 0f), new Vector3(size.x, 0.02f, size.y), CarpetMat)
                .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            BlockKit.Batch("Border", t, Level4GameRoom.GlowMaterial(border, 0.55f),
                BlockKit.Outline(new Vector3(0f, 0.024f, 0f), size.x - 0.3f, size.y - 0.3f, 0.05f));

            Color[] colours = { Level4GameRoom.Magenta, Level4GameRoom.Teal, Level4GameRoom.Lemon, Level4GameRoom.Violet };
            var pieces = new List<CombineInstance>[colours.Length];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = new List<CombineInstance>();
            var rnd = new System.Random(seed);
            int count = Mathf.RoundToInt(size.x * size.y * 0.75f);
            for (int i = 0; i < count; i++)
            {
                float x = ((float)rnd.NextDouble() - 0.5f) * (size.x - 0.9f);
                float z = ((float)rnd.NextDouble() - 0.5f) * (size.y - 0.9f);
                float yaw = (float)rnd.NextDouble() * 180f;
                // Short dashes, with the odd small square.
                Vector3 scale = i % 4 == 0 ? new Vector3(0.09f, 0.002f, 0.09f) : new Vector3(0.24f, 0.002f, 0.045f);
                pieces[i % colours.Length].Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, 0.022f, z), new Vector3(0f, yaw, 0f), scale));
            }
            for (int i = 0; i < pieces.Length; i++)
                BlockKit.Batch("Flecks_" + i, t, Fleck(Color.Lerp(CarpetColour, colours[i], 0.55f)), pieces[i]);
        }

        // ---- Festoon lights -----------------------------------------------------

        /// <summary>Strings of warm bulbs across the room, hung in three swags
        /// from the side walls and two ceiling rods. They sit above every
        /// sign's sightline (the highest, the north header, is read from
        /// below them).</summary>
        private static void Festoons(Transform root)
        {
            const float anchorY = 5.9f, sag = 0.55f, ceiling = Level4CyberAttackSceneFactory.AttackRoomHeight;
            float[] anchors = { -19.45f, -6.5f, 6.5f, 19.45f };
            var bulbs = new List<CombineInstance>();
            var cable = new List<CombineInstance>();
            foreach (float z in new[] { -11f, -3.5f, 6.5f, 12.5f })
            {
                for (int span = 0; span < anchors.Length - 1; span++)
                {
                    float x0 = anchors[span], x1 = anchors[span + 1];
                    int steps = Mathf.RoundToInt((x1 - x0) / 0.72f);
                    Vector3 previous = new Vector3(x0, anchorY, z);
                    for (int i = 1; i <= steps; i++)
                    {
                        float u = i / (float)steps;
                        Vector3 p = new Vector3(Mathf.Lerp(x0, x1, u), anchorY - sag * 4f * u * (1f - u), z);
                        Vector3 d = p - previous;
                        cable.Add(BlockKit.Piece(BlockKit.CubeMesh, (p + previous) * 0.5f,
                            new Vector3(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg), new Vector3(d.magnitude + 0.004f, 0.012f, 0.012f)));
                        if (i < steps)
                        {
                            cable.Add(BlockKit.Piece(BlockKit.CubeMesh, p + Vector3.down * 0.035f, Vector3.zero, new Vector3(0.022f, 0.05f, 0.022f)));
                            bulbs.Add(BlockKit.Piece(BlockKit.CubeMesh, p + Vector3.down * 0.095f, new Vector3(45f, 45f, 0f), Vector3.one * 0.08f));
                        }
                        previous = p;
                    }
                }
                for (int a = 1; a < anchors.Length - 1; a++)
                    cable.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(anchors[a], (anchorY + ceiling) * 0.5f, z),
                        Vector3.zero, new Vector3(0.02f, ceiling - anchorY, 0.02f)));
            }
            BlockKit.Batch("FestoonCable", root, CableMat, cable);
            BlockKit.Batch("FestoonBulbs", root, Level4GameRoom.GlowMaterial(Warm, 1.5f), bulbs);
        }

        // ---- Pixel art ----------------------------------------------------------

        private static readonly string[] Padlock =
        {
            "...####...",
            "..#....#..",
            ".#......#.",
            ".#......#.",
            "##########",
            "##########",
            "####..####",
            "####..####",
            "#####.####",
            "##########",
            "##########",
        };

        private static readonly string[] Key =
        {
            "..####....",
            ".##..##...",
            ".#....#...",
            ".##..##...",
            "..####....",
            "...##.....",
            "...##.....",
            "...####...",
            "...##.....",
            "...####...",
            "...##.....",
        };

        private static readonly string[] Shield =
        {
            "##########",
            "##########",
            "#######.##",
            "######..##",
            "#.###..###",
            "#..#..####",
            "##...#####",
            ".##.#####.",
            ".########.",
            "..######..",
            "....##....",
        };

        private static readonly string[] Heart =
        {
            "..........",
            ".###..###.",
            "##########",
            "##########",
            "##########",
            "##########",
            ".########.",
            "..######..",
            "...####...",
            "....##....",
            "..........",
        };

        /// <summary>Four pixel-art light panels on the game floor side of the
        /// dividing wall (z 2), two each side of the archway.</summary>
        private static void PixelArt(Transform root)
        {
            const float wallFace = 2.15f + 0.03f; // clear of the wall and its wainscot
            Icon(root, "Pixel_Padlock", new Vector3(-14.2f, 2.25f, wallFace), Padlock, Level4GameRoom.Teal);
            Icon(root, "Pixel_Key", new Vector3(-8.6f, 2.25f, wallFace), Key, Level4GameRoom.Lemon);
            Icon(root, "Pixel_Shield", new Vector3(8.6f, 2.25f, wallFace), Shield, Level4GameRoom.Violet);
            Icon(root, "Pixel_Heart", new Vector3(14.2f, 2.25f, wallFace), Heart, Level4GameRoom.Magenta);
        }

        /// <summary>One icon, read from the +Z side: rows top to bottom, each
        /// row left to right as the viewer sees it.</summary>
        private static void Icon(Transform root, string name, Vector3 centre, string[] rows, Color colour)
        {
            const float pixel = 0.16f;
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = centre;

            var pixels = new List<CombineInstance>();
            int columns = rows[0].Length;
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < columns; c++)
                {
                    if (rows[r][c] != '#') continue;
                    // The viewer faces -Z, so their left is +X.
                    float x = ((columns - 1) * 0.5f - c) * pixel;
                    float y = ((rows.Length - 1) * 0.5f - r) * pixel;
                    pixels.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, y, 0f), Vector3.zero,
                        new Vector3(pixel - 0.02f, pixel - 0.02f, 0.03f)));
                }
            BlockKit.Batch("Pixels", t, Level4GameRoom.GlowMaterial(colour, 1.15f), pixels);
            Wash(t, "Wash", new Vector3(0f, 0f, -0.005f), new Vector3(0f, 180f, 0f),
                new Vector2(columns * pixel * 3.6f, rows.Length * pixel * 3.3f), colour, 0.8f);
        }

        // ---- Soft light ---------------------------------------------------------

        /// <summary>Warm light on the floor in front of each bar counter,
        /// under its pendant lamps.</summary>
        private static void BarPools(Transform root)
        {
            foreach (float x in new[] { -4.7f, 4.7f })
                Pool(root, "BarPool", new Vector3(x, 0.02f, -6.9f), new Vector2(6.4f, 4.4f), Warm, 0.6f);
        }

        private static Shader glowShader;
        private static bool glowShaderLooked;

        /// <summary>A soft additive glow on a quad, facing the quad's local
        /// -Z: the stand-in for the light a neon sign or a lamp would throw
        /// on the surface behind or below it. (Every light in this room is a
        /// vertex light, which leaves no trace on the big flat wall and floor
        /// meshes.) Skipped quietly if the shader is missing.</summary>
        public static void Wash(Transform parent, string name, Vector3 localPos, Vector3 euler, Vector2 size, Color colour, float strength)
        {
            if (!glowShaderLooked)
            {
                glowShader = Shader.Find("Cyverse/GlowSprite");
                glowShaderLooked = true;
            }
            if (glowShader == null) return;

            var mat = new Material(glowShader);
            colour.a = Mathf.Clamp01(strength);
            mat.SetColor("_Color", colour);
            mat.SetFloat("_Intensity", 1f);
            MeshRenderer r = BlockKit.MeshPart(name, parent, BlockKit.QuadMesh, localPos, euler, mat,
                scale: new Vector3(size.x, size.y, 1f)).GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>A wash lying flat on the floor (or on a carpet).</summary>
        public static void Pool(Transform parent, string name, Vector3 localPos, Vector2 size, Color colour, float strength) =>
            Wash(parent, name, localPos, new Vector3(90f, 0f, 0f), size, colour, strength);
    }
}
