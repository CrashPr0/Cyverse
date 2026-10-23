using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 4 dressed as a big-tech employee game room: the place staff go
    /// to blow off steam, which the red team turns into its attack surface.
    /// Each of the four attack stations is a game machine (arcade cabinet,
    /// claw machine, vending wall, pinball), and the rest of the room is
    /// decoration around them: a snack bar where the briefing plays, a staff
    /// corkboard full of recon clues, foosball, ping-pong, beanbags, and a
    /// scrolling "GAME ROOM" LED sign on an archway over the centre aisle.
    ///
    /// Everything is built from BlockKit pieces at load time. Machine bodies
    /// are built in station-local space with their playable face toward
    /// local -Z, the side the player reads every station from.
    /// </summary>
    public static class Level4GameRoom
    {
        public const string RootName = "GameRoom";
        public const string ArchName = "GameRoomArch";

        public static readonly Color Magenta = new Color(1f, 0.22f, 0.62f);
        public static readonly Color Teal = new Color(0.18f, 0.88f, 0.86f);
        public static readonly Color Lemon = new Color(1f, 0.86f, 0.22f);
        public static readonly Color Violet = new Color(0.56f, 0.36f, 1f);

        // ---- Materials (palette files, so they can be recoloured) ----------

        private static Material CabinetBody => Palette.Get("GameCabinetBody", new Color(0.09f, 0.06f, 0.14f), 0.45f, 0.2f);
        private static Material CabinetTrim => Palette.Get("GameCabinetTrim", new Color(0.20f, 0.17f, 0.26f), 0.6f, 0.5f);
        private static Material Chrome => Palette.Get("GameChrome", new Color(0.62f, 0.64f, 0.70f), 0.85f, 0.9f);
        private static Material DarkGlass => Palette.Get("GameDarkGlass", new Color(0.02f, 0.025f, 0.04f), 0.9f, 0.1f);
        private static Material Felt => Palette.Get("GameFelt", new Color(0.05f, 0.30f, 0.26f), 0.1f, 0f);
        private static Material TableBlue => Palette.Get("GameTableBlue", new Color(0.06f, 0.16f, 0.36f), 0.35f, 0f);
        private static Material WhiteLine => Palette.Get("GameWhiteLine", new Color(0.92f, 0.92f, 0.95f), 0.3f, 0f);
        private static Material Counter => Palette.Get("GameCounter", new Color(0.30f, 0.20f, 0.14f), 0.55f, 0.1f);
        private static Material Cork => Palette.Get("GameCork", new Color(0.55f, 0.40f, 0.24f), 0.1f, 0f);
        private static Material Paper => Palette.Get("GamePaper", new Color(0.93f, 0.91f, 0.84f), 0.1f, 0f);

        private static readonly Dictionary<Color, Material> glows = new Dictionary<Color, Material>();
        private static readonly Dictionary<Color, Material> plains = new Dictionary<Color, Material>();

        private static Material Glow(Color c, float strength = 1.4f)
        {
            Color key = new Color(c.r, c.g, c.b, strength / 10f);
            if (glows.TryGetValue(key, out Material m) && m != null) return m;
            return glows[key] = BuildKit.MakeEmissive(c, strength);
        }

        private static Material Plain(Color c)
        {
            if (plains.TryGetValue(c, out Material m) && m != null) return m;
            return plains[c] = BuildKit.MakeStandard(c, 0.35f, 0.05f);
        }

        // ---- Room ---------------------------------------------------------------

        /// <summary>Builds the non-interactive game room around the stations.</summary>
        public static GameObject BuildRoom(Color accent)
        {
            var root = new GameObject(RootName).transform;

            BuildArch(root, accent);
            BuildSnackBar(root, new Vector3(0f, 0f, -6f));
            BuildCorkboard(root, new Vector3(-19.35f, 0f, -4f), -90f);
            // North of the shared station aisle (z 11) so the walk between
            // machines stays clear.
            BuildFoosball(root, new Vector3(0f, 0f, 15.6f), 0f);
            BuildPingPong(root, new Vector3(12f, 0f, -12f), 0f);

            PropFactory.BuildLounge(root);
            BuildBeanbag(root, new Vector3(-12.2f, 0f, -9.4f), Magenta);
            BuildBeanbag(root, new Vector3(-14.0f, 0f, -8.6f), Teal);
            BuildBeanbag(root, new Vector3(-15.8f, 0f, -9.6f), Lemon);

            PropFactory.BuildPlant(root, new Vector3(-17.3f, 0f, 17.3f));
            PropFactory.BuildPlant(root, new Vector3(17.3f, 0f, -17.3f));
            PropFactory.BuildPlant(root, new Vector3(-7.4f, 0f, -17.4f));
            PropFactory.BuildPlant(root, new Vector3(7.6f, 0f, 3.2f));
            PropFactory.BuildPlant(root, new Vector3(-7.6f, 0f, 3.2f));
            return root.gameObject;
        }

        /// <summary>A two-post archway over the centre aisle, where the old
        /// dividing wall stood, carrying the scrolling GAME ROOM sign. It
        /// faces the spawn, so it is the first thing the player sees.</summary>
        public static void BuildArch(Transform parent, Color accent)
        {
            var arch = new GameObject(ArchName).transform;
            arch.SetParent(parent, false);
            arch.position = new Vector3(0f, 0f, 2f);

            const float postX = 3.9f, beamY = 4.9f;
            foreach (float x in new[] { -postX, postX })
            {
                string side = x < 0f ? "L" : "R";
                BlockKit.Block("ArchPost_" + side, arch, new Vector3(x, 0f, 0f), Vector3.zero, 0f, beamY,
                    new Vector2(0.2f, 0.2f), new Vector2(0.16f, 0.16f), 0.03f, CabinetTrim, solid: true);
                BlockKit.Box("ArchPostGlow_" + side, arch, new Vector3(x, beamY * 0.5f, -0.17f),
                    new Vector3(0.03f, beamY - 0.4f, 0.012f), Glow(Magenta, 1.6f));
                BlockKit.Slab("ArchFoot_" + side, arch, new Vector3(x, 0.06f, 0f),
                    new Vector3(0.6f, 0.12f, 0.6f), 0.02f, CabinetBody);
            }
            BlockKit.Slab("ArchBeam", arch, new Vector3(0f, beamY + 0.12f, 0f),
                new Vector3(postX * 2f + 0.4f, 0.24f, 0.36f), 0.03f, CabinetTrim);

            LedMarquee.Build(arch, "GameRoomSign", arch.position + new Vector3(0f, beamY + 0.82f, 0f), 0f,
                7.2f, 0.9f, "GAME ROOM", Color.Lerp(accent, Lemon, 0.25f), twoSided: true);
        }

        private static void BuildSnackBar(Transform parent, Vector3 tvPosition)
        {
            // The briefing screen stands at tvPosition; the bar wraps round it
            // so the screen reads as the bar's TV rather than a lone kiosk.
            var bar = new GameObject("SnackBar").transform;
            bar.SetParent(parent, false);
            bar.position = tvPosition;

            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0f ? "L" : "R";
                float x = side * 4.7f;
                BlockKit.Block("Counter_" + s, bar, new Vector3(x, 0f, 0f), Vector3.zero, 0f, 1.0f,
                    new Vector2(1.45f, 0.4f), new Vector2(1.45f, 0.4f), 0.03f, CabinetBody, solid: true);
                BlockKit.Slab("CounterTop_" + s, bar, new Vector3(x, 1.03f, -0.03f),
                    new Vector3(3.05f, 0.06f, 0.92f), 0.02f, Counter);
                BlockKit.Box("CounterGlow_" + s, bar, new Vector3(x, 0.95f, -0.41f),
                    new Vector3(2.9f, 0.02f, 0.01f), Glow(Teal, 1.6f));

                // Drink coolers behind the counter, glass fronts toward the room.
                for (int i = 0; i < 2; i++)
                {
                    Vector3 c = new Vector3(x + (i == 0 ? -0.6f : 0.6f), 0f, 0.95f);
                    string n = $"Cooler_{s}{i}";
                    BlockKit.Block(n, bar, c, Vector3.zero, 0f, 2.05f,
                        new Vector2(0.29f, 0.36f), new Vector2(0.29f, 0.36f), 0.02f, CabinetTrim, solid: true);
                    BlockKit.Slab(n + "_Glass", bar, c + new Vector3(0f, 1.08f, -0.37f),
                        new Vector3(0.5f, 1.6f, 0.02f), 0.005f, Glow(new Color(0.55f, 0.75f, 0.95f), 0.35f));
                    var cans = new List<CombineInstance>();
                    for (int shelf = 0; shelf < 4; shelf++)
                        for (int k = 0; k < 3; k++)
                            cans.Add(BlockKit.Piece(BlockKit.CubeMesh,
                                c + new Vector3(-0.15f + k * 0.15f, 0.48f + shelf * 0.38f, -0.4f),
                                Vector3.zero, new Vector3(0.08f, 0.16f, 0.04f)));
                    Color drinks = side < 0f ? (i == 0 ? Magenta : Teal) : (i == 0 ? Lemon : Violet);
                    BlockKit.Batch(n + "_Cans", bar, Glow(drinks, 0.9f), cans);
                }

                // Stools on the room side.
                for (int k = 0; k < 3; k++)
                {
                    Vector3 stool = new Vector3(x - 1.0f + k * 1.0f, 0f, -0.95f);
                    BlockKit.Block($"Stool_{s}{k}", bar, stool, Vector3.zero, 0f, 0.7f,
                        new Vector2(0.06f, 0.06f), new Vector2(0.05f, 0.05f), 0.01f, Chrome);
                    BlockKit.Slab($"StoolSeat_{s}{k}", bar, stool + Vector3.up * 0.74f,
                        new Vector3(0.42f, 0.08f, 0.42f), 0.03f, Plain(side < 0f ? Magenta * 0.7f : Teal * 0.6f));
                }
            }

            BuildKit.MakeSign(bar, tvPosition + new Vector3(-4.7f, 2.9f, 0.95f), "SNACK BAR", Lemon, 0.026f);
            BuildKit.MakeSign(bar, tvPosition + new Vector3(4.7f, 2.9f, 0.95f), "FREE DRINKS", Teal, 0.026f);
        }

        /// <summary>The staff corkboard: the public details an attacker
        /// collects during recon, pinned up where anyone can read them.</summary>
        private static void BuildCorkboard(Transform parent, Vector3 position, float yaw)
        {
            var board = new GameObject("StaffCorkboard").transform;
            board.SetParent(parent, false);
            board.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            BlockKit.Slab("Frame", board, new Vector3(0f, 1.9f, 0.04f), new Vector3(3.0f, 1.6f, 0.08f), 0.02f, Counter);
            BlockKit.Slab("Cork", board, new Vector3(0f, 1.9f, -0.01f), new Vector3(2.8f, 1.4f, 0.04f), 0.01f, Cork);

            Note(board, "Note_Wifi", new Vector3(-0.85f, 2.25f), "GUEST WI-FI\nGameRoom2024!", Lemon);
            Note(board, "Note_Admin", new Vector3(0.05f, 2.3f), "WELCOME JORDAN LEE\nnew IT admin!", Paper.color);
            Note(board, "Note_Badge", new Vector3(0.95f, 2.2f), "FOUND: badge on\nfoosball table", Magenta);
            Note(board, "Note_OrgChart", new Vector3(-0.6f, 1.55f), "ORG CHART\nFinance > Vault team", Teal);
            Note(board, "Note_Tourney", new Vector3(0.6f, 1.5f), "PING-PONG CUP\nsign up: first.last", Paper.color);

            var title = MakeText(board, "Corkboard_Title", new Vector3(0f, 2.86f, -0.02f),
                "STAFF BOARD", Lemon, 2.4f, 2.6f, 0.4f);
            title.fontStyle = FontStyles.Bold;
        }

        private static void Note(Transform board, string name, Vector2 at, string text, Color paper)
        {
            BlockKit.Box(name, board, new Vector3(at.x, at.y, -0.035f), new Vector3(0.78f, 0.44f, 0.006f), Plain(paper));
            BlockKit.Box(name + "_Pin", board, new Vector3(at.x, at.y + 0.17f, -0.045f), new Vector3(0.04f, 0.04f, 0.02f), Plain(Color.red));
            MakeText(board, name + "_Text", new Vector3(at.x, at.y - 0.02f, -0.042f), text,
                new Color(0.10f, 0.08f, 0.12f), 0.6f, 0.74f, 0.36f);
        }

        private static TextMeshPro MakeText(Transform parent, string name, Vector3 localPos, string text,
            Color color, float fontSize, float width, float height)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.GetComponent<TextMeshPro>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.rectTransform.sizeDelta = new Vector2(width, height);
            WorldTextLayoutIntent.Configure(go, WorldTextLayoutIntent.Mode.Mounted);
            return tmp;
        }

        private static void BuildFoosball(Transform parent, Vector3 position, float yaw)
        {
            var t = new GameObject("Foosball").transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            foreach (float x in new[] { -0.6f, 0.6f })
                foreach (float z in new[] { -0.35f, 0.35f })
                    BlockKit.Box("Leg", t, new Vector3(x, 0.4f, z), new Vector3(0.08f, 0.8f, 0.08f), CabinetTrim);
            BlockKit.Block("Box", t, Vector3.zero, Vector3.zero, 0.8f, 1.0f,
                new Vector2(0.72f, 0.42f), new Vector2(0.72f, 0.42f), 0.02f, CabinetBody, solid: true);
            BlockKit.Box("Pitch", t, new Vector3(0f, 0.86f, 0f), new Vector3(1.36f, 0.01f, 0.76f), Felt);
            BlockKit.Box("Halfway", t, new Vector3(0f, 0.867f, 0f), new Vector3(0.015f, 0.004f, 0.76f), WhiteLine);

            var rods = new List<CombineInstance>();
            var red = new List<CombineInstance>();
            var blue = new List<CombineInstance>();
            for (int i = 0; i < 8; i++)
            {
                float x = -0.56f + i * 0.16f;
                rods.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, 0.96f, 0f), Vector3.zero, new Vector3(0.018f, 0.018f, 1.2f)));
                var team = i % 2 == 0 ? red : blue;
                int players = i == 0 || i == 7 ? 1 : i % 3 == 1 ? 2 : 3;
                for (int p = 0; p < players; p++)
                {
                    float z = players == 1 ? 0f : -0.22f + p * (0.44f / (players - 1));
                    team.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, 0.93f, z), Vector3.zero, new Vector3(0.04f, 0.13f, 0.03f)));
                }
            }
            BlockKit.Batch("Rods", t, Chrome, rods);
            BlockKit.Batch("RedTeam", t, Plain(new Color(0.85f, 0.15f, 0.2f)), red);
            BlockKit.Batch("BlueTeam", t, Plain(new Color(0.15f, 0.35f, 0.9f)), blue);
            // The badge someone left behind (the corkboard's "found" note).
            BlockKit.Box("ForgottenBadge", t, new Vector3(0.62f, 1.005f, 0.3f), new Vector3(0.09f, 0.006f, 0.13f), Glow(Lemon, 0.8f));
        }

        private static void BuildPingPong(Transform parent, Vector3 position, float yaw)
        {
            var t = new GameObject("PingPong").transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            foreach (float x in new[] { -0.6f, 0.6f })
                foreach (float z in new[] { -1.1f, 1.1f })
                    BlockKit.Box("Leg", t, new Vector3(x, 0.37f, z), new Vector3(0.06f, 0.74f, 0.06f), CabinetTrim);
            BlockKit.Block("Top", t, Vector3.zero, Vector3.zero, 0.74f, 0.77f,
                new Vector2(0.76f, 1.37f), new Vector2(0.76f, 1.37f), 0.01f, TableBlue, solid: true);
            var lines = new List<CombineInstance>
            {
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, 0.773f, 0f), Vector3.zero, new Vector3(0.012f, 0.004f, 2.72f)),
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(-0.745f, 0.773f, 0f), Vector3.zero, new Vector3(0.02f, 0.004f, 2.74f)),
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0.745f, 0.773f, 0f), Vector3.zero, new Vector3(0.02f, 0.004f, 2.74f)),
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, 0.773f, -1.36f), Vector3.zero, new Vector3(1.5f, 0.004f, 0.02f)),
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, 0.773f, 1.36f), Vector3.zero, new Vector3(1.5f, 0.004f, 0.02f)),
            };
            BlockKit.Batch("Lines", t, WhiteLine, lines);
            BlockKit.Box("Net", t, new Vector3(0f, 0.85f, 0f), new Vector3(1.66f, 0.15f, 0.01f), Plain(new Color(0.12f, 0.12f, 0.14f)));
            BlockKit.Box("PaddleA", t, new Vector3(0.35f, 0.785f, -0.9f), new Vector3(0.16f, 0.015f, 0.26f), Plain(new Color(0.8f, 0.12f, 0.15f)));
            BlockKit.Box("PaddleB", t, new Vector3(-0.3f, 0.785f, 0.95f), new Vector3(0.16f, 0.015f, 0.26f), Plain(new Color(0.1f, 0.1f, 0.12f)));
            var ball = BuildKit.SpawnLocal(PrimitiveType.Sphere, "Ball", t, new Vector3(0.1f, 0.79f, -0.4f),
                Vector3.zero, Vector3.one * 0.04f, Plain(new Color(1f, 0.6f, 0.2f)), false);
            ball.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void BuildBeanbag(Transform parent, Vector3 position, Color colour)
        {
            var bag = BuildKit.SpawnLocal(PrimitiveType.Sphere, "Beanbag", parent, position + Vector3.up * 0.3f,
                new Vector3(0f, Random.Range(0f, 360f), 0f), new Vector3(1.05f, 0.62f, 0.95f),
                BuildKit.MakeStandard(colour * 0.75f, 0.15f, 0f), true);
            bag.isStatic = true;
        }

        // ---- Machines (station-local; playable face toward -Z) ----------------

        /// <summary>Where a machine's screen is and how far it leans back, so
        /// the station can mount its label on it.</summary>
        public struct Screen
        {
            public Vector3 center;
            public float pitch;
            public Vector2 size;
        }

        public static Screen BuildMachine(Transform station, Level4CyberAttackContent.StationKind kind, Color accent)
        {
            switch (kind)
            {
                case Level4CyberAttackContent.StationKind.BypassMfa: return BuildArcadeRow(station);
                case Level4CyberAttackContent.StationKind.EscalatePrivileges: return BuildClawMachine(station);
                case Level4CyberAttackContent.StationKind.ExtractData: return BuildVendingWall(station);
                default: return BuildPinball(station);
            }
        }

        /// <summary>Three arcade cabinets side by side; the middle one is the
        /// station, the other two are attract-mode decoration.</summary>
        private static Screen BuildArcadeRow(Transform station)
        {
            Screen screen = default;
            Color[] art = { Teal, Magenta, Violet };
            for (int i = -1; i <= 1; i++)
            {
                var cab = new GameObject(i == 0 ? "ArcadeCabinet" : "ArcadeCabinet_" + (i < 0 ? "L" : "R")).transform;
                cab.SetParent(station, false);
                cab.localPosition = new Vector3(i * 1.25f, 0f, 0.35f);
                Color c = art[i + 1];

                BlockKit.Block("Base", cab, Vector3.zero, Vector3.zero, 0f, 1.0f,
                    new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), 0.03f, CabinetBody, solid: true);
                BlockKit.Block("Deck", cab, new Vector3(0f, 0f, -0.42f), new Vector3(-12f, 0f, 0f), 0.94f, 1.06f,
                    new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), 0.02f, CabinetTrim);
                BlockKit.Block("Upper", cab, new Vector3(0f, 0f, 0.08f), Vector3.zero, 1.0f, 2.0f,
                    new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.3f), 0.03f, CabinetBody);
                BlockKit.Slab("Header", cab, new Vector3(0f, 2.06f, -0.12f), new Vector3(1.0f, 0.26f, 0.2f), 0.02f, Glow(c, 1.5f));
                // A thin neon stripe down each side, not a lit panel.
                foreach (float x in new[] { -0.505f, 0.505f })
                    BlockKit.Box("SideArt", cab, new Vector3(x, 1.2f, -0.3f), new Vector3(0.012f, 1.6f, 0.04f), Glow(c, 0.9f));

                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Stick", cab, new Vector3(-0.2f, 1.12f, -0.47f),
                    Vector3.zero, new Vector3(0.03f, 0.06f, 0.03f), Chrome, false);
                BuildKit.SpawnLocal(PrimitiveType.Sphere, "StickBall", cab, new Vector3(-0.2f, 1.2f, -0.47f),
                    Vector3.zero, Vector3.one * 0.07f, Plain(new Color(0.85f, 0.1f, 0.15f)), false);
                for (int b = 0; b < 3; b++)
                    BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Button", cab, new Vector3(0.02f + b * 0.12f, 1.09f, -0.45f),
                        new Vector3(-12f, 0f, 0f), new Vector3(0.06f, 0.012f, 0.06f), Glow(b == 0 ? Lemon : b == 1 ? Magenta : Teal, 1.2f), false);

                // The screen leans back inside the cabinet's upper half.
                var bezel = new GameObject("Bezel").transform;
                bezel.SetParent(cab, false);
                bezel.localPosition = new Vector3(0f, 1.52f, -0.24f);
                bezel.localRotation = Quaternion.Euler(10f, 0f, 0f); // leans back, arcade style
                BlockKit.Slab("Frame", bezel, Vector3.zero, new Vector3(0.9f, 0.72f, 0.06f), 0.01f, DarkGlass);
                BuildKit.SpawnLocal(PrimitiveType.Quad, "MonitorScreen", bezel, new Vector3(0f, 0f, -0.035f), Vector3.zero,
                    new Vector3(0.8f, 0.62f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, c, i == 0 ? 0.6f : 0.35f)), false);
                if (i == 0) screen = new Screen { center = cab.localPosition + bezel.localPosition + new Vector3(0f, 0f, -0.07f), pitch = 10f, size = new Vector2(0.8f, 0.62f) };
            }
            return screen;
        }

        /// <summary>A claw machine whose prize pile is keycards; the gold one
        /// has far more access than it needs, which is the lesson.</summary>
        private static Screen BuildClawMachine(Transform station)
        {
            var m = new GameObject("ClawMachine").transform;
            m.SetParent(station, false);
            m.localPosition = new Vector3(0f, 0f, 0.35f);

            BlockKit.Block("Base", m, Vector3.zero, Vector3.zero, 0f, 0.95f,
                new Vector2(0.75f, 0.65f), new Vector2(0.75f, 0.65f), 0.03f, CabinetBody, solid: true);
            BlockKit.Box("BaseGlow", m, new Vector3(0f, 0.93f, -0.66f), new Vector3(1.45f, 0.025f, 0.01f), Glow(Magenta, 1.8f));
            // Open glass box: corner posts, a floor and the prize chute.
            foreach (float x in new[] { -0.72f, 0.72f })
                foreach (float z in new[] { -0.62f, 0.62f })
                    BlockKit.Box("Post", m, new Vector3(x, 1.5f, z), new Vector3(0.05f, 1.1f, 0.05f), Chrome);
            BlockKit.Box("Floor", m, new Vector3(0f, 0.96f, 0f), new Vector3(1.4f, 0.02f, 1.2f), Plain(new Color(0.18f, 0.08f, 0.26f)));
            BlockKit.Box("BackPanel", m, new Vector3(0f, 1.5f, 0.6f), new Vector3(1.4f, 1.08f, 0.02f), Glow(Violet, 0.35f));
            BlockKit.Box("Chute", m, new Vector3(0.5f, 1.12f, -0.4f), new Vector3(0.3f, 0.3f, 0.3f), DarkGlass);

            var cards = new List<CombineInstance>();
            var rnd = new System.Random(4);
            for (int i = 0; i < 26; i++)
            {
                float x = -0.55f + (float)rnd.NextDouble() * 0.85f;
                float z = -0.45f + (float)rnd.NextDouble() * 0.95f;
                float y = 0.99f + (float)rnd.NextDouble() * 0.16f;
                cards.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, y, z),
                    new Vector3((float)rnd.NextDouble() * 50f - 25f, (float)rnd.NextDouble() * 180f, 0f),
                    new Vector3(0.16f, 0.01f, 0.1f)));
            }
            BlockKit.Batch("Keycards", m, Plain(new Color(0.75f, 0.85f, 0.95f)), cards);
            BlockKit.Box("AdminKeycard", m, new Vector3(-0.15f, 1.17f, 0.05f), new Vector3(0.2f, 0.012f, 0.13f), Glow(Lemon, 1.8f));

            // Gantry and claw, hovering over the gold card.
            BlockKit.Box("Gantry", m, new Vector3(0f, 1.98f, 0.05f), new Vector3(1.4f, 0.04f, 0.06f), Chrome);
            BlockKit.Box("Cable", m, new Vector3(-0.15f, 1.75f, 0.05f), new Vector3(0.012f, 0.44f, 0.012f), Chrome);
            BlockKit.Box("ClawHub", m, new Vector3(-0.15f, 1.52f, 0.05f), new Vector3(0.1f, 0.08f, 0.1f), Chrome);
            foreach (float a in new[] { 0f, 120f, 240f })
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                BlockKit.MeshPart("Prong", m, BlockKit.CubeMesh, new Vector3(-0.15f, 1.42f, 0.05f) + dir * 0.06f,
                    new Vector3(-25f, a, 0f), Chrome, scale: new Vector3(0.02f, 0.16f, 0.02f));
            }
            BlockKit.Slab("Canopy", m, new Vector3(0f, 2.12f, 0f), new Vector3(1.55f, 0.24f, 1.35f), 0.03f, CabinetBody);
            BlockKit.Box("CanopyGlow", m, new Vector3(0f, 2.12f, -0.68f), new Vector3(1.45f, 0.14f, 0.01f), Glow(Lemon, 1.4f));

            // The station screen sits on the front of the base, under the box.
            BlockKit.Slab("FrontPanel", m, new Vector3(0f, 0.62f, -0.66f), new Vector3(1.2f, 0.42f, 0.03f), 0.01f, DarkGlass);
            BuildKit.SpawnLocal(PrimitiveType.Quad, "MonitorScreen", m, new Vector3(0f, 0.62f, -0.68f), Vector3.zero,
                new Vector3(1.1f, 0.34f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, Magenta, 0.5f)), false);
            BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Stick", m, new Vector3(-0.3f, 1.0f, -0.6f),
                Vector3.zero, new Vector3(0.03f, 0.06f, 0.03f), Chrome, false);
            BuildKit.SpawnLocal(PrimitiveType.Sphere, "StickBall", m, new Vector3(-0.3f, 1.08f, -0.6f),
                Vector3.zero, Vector3.one * 0.07f, Plain(Lemon), false);
            return new Screen { center = m.localPosition + new Vector3(0f, 0.62f, -0.7f), pitch = 0f, size = new Vector2(1.1f, 0.34f) };
        }

        /// <summary>Three vending machines; the middle one is the station.
        /// Its glowing delivery slot is the "watched channel" the data leaves by.</summary>
        private static Screen BuildVendingWall(Transform station)
        {
            Screen screen = default;
            Color[] fronts = { Teal, Magenta, Lemon };
            for (int i = -1; i <= 1; i++)
            {
                var v = new GameObject(i == 0 ? "VendingMachine" : "VendingMachine_" + (i < 0 ? "L" : "R")).transform;
                v.SetParent(station, false);
                v.localPosition = new Vector3(i * 1.3f, 0f, 0.45f);
                Color c = fronts[i + 1];

                BlockKit.Block("Body", v, Vector3.zero, Vector3.zero, 0f, 2.1f,
                    new Vector2(0.6f, 0.42f), new Vector2(0.6f, 0.42f), 0.03f, CabinetBody, solid: true);
                BlockKit.Box("Window", v, new Vector3(-0.12f, 1.2f, -0.425f), new Vector3(0.86f, 1.3f, 0.01f), DarkGlass);
                var items = new List<CombineInstance>();
                for (int shelf = 0; shelf < 5; shelf++)
                    for (int k = 0; k < 5; k++)
                        items.Add(BlockKit.Piece(BlockKit.CubeMesh,
                            new Vector3(-0.48f + k * 0.18f, 0.66f + shelf * 0.25f, -0.44f),
                            Vector3.zero, new Vector3(0.08f, 0.12f, 0.02f)));
                BlockKit.Batch("Snacks", v, Glow(c, 0.4f), items);
                BlockKit.Box("Keypad", v, new Vector3(0.45f, 1.25f, -0.43f), new Vector3(0.16f, 0.26f, 0.01f), CabinetTrim);
                BlockKit.Box("Slot", v, new Vector3(-0.12f, 0.3f, -0.43f), new Vector3(0.8f, 0.2f, 0.01f),
                    i == 0 ? Glow(Teal, 1.6f) : DarkGlass);

                // A lit header across the top carries the station's screen.
                BlockKit.Slab("Header", v, new Vector3(0f, 2.0f, -0.43f), new Vector3(1.16f, 0.32f, 0.03f), 0.01f, DarkGlass);
                BuildKit.SpawnLocal(PrimitiveType.Quad, "MonitorScreen", v, new Vector3(0f, 2.0f, -0.45f), Vector3.zero,
                    new Vector3(1.08f, 0.26f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, c, i == 0 ? 0.55f : 0.35f)), false);
                if (i == 0) screen = new Screen { center = v.localPosition + new Vector3(0f, 2.0f, -0.47f), pitch = 0f, size = new Vector2(1.08f, 0.26f) };
            }
            return screen;
        }

        /// <summary>A pinball table with its backbox and, beside it, the high
        /// score board: wiping the board is the "cover tracks" temptation.</summary>
        private static Screen BuildPinball(Transform station)
        {
            var p = new GameObject("Pinball").transform;
            p.SetParent(station, false);
            p.localPosition = new Vector3(0f, 0f, 0.1f);

            foreach (float x in new[] { -0.3f, 0.3f })
                foreach (float z in new[] { -0.6f, 0.6f })
                    BlockKit.Box("Leg", p, new Vector3(x, 0.4f, z), new Vector3(0.06f, z < 0f ? 0.78f : 0.88f, 0.06f), Chrome);
            BlockKit.Block("Cabinet", p, new Vector3(0f, 0.86f, 0f), new Vector3(-5f, 0f, 0f), -0.06f, 0.12f,
                new Vector2(0.37f, 0.7f), new Vector2(0.37f, 0.7f), 0.02f, CabinetBody, solid: true);
            BuildKit.SpawnLocal(PrimitiveType.Quad, "Playfield", p, new Vector3(0f, 0.995f, 0f),
                new Vector3(85f, 0f, 0f), new Vector3(0.66f, 1.32f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, Violet, 0.6f)), false);
            foreach (var bump in new[] { new Vector3(-0.14f, 1.03f, 0.3f), new Vector3(0.14f, 1.03f, 0.3f), new Vector3(0f, 1.04f, 0.45f) })
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Bumper", p, bump, Vector3.zero,
                    new Vector3(0.1f, 0.03f, 0.1f), Glow(Magenta, 1.6f), false);
            BlockKit.Box("FlipperL", p, new Vector3(-0.1f, 0.96f, -0.5f), new Vector3(0.14f, 0.025f, 0.03f), Glow(Lemon, 1.2f));
            BlockKit.Box("FlipperR", p, new Vector3(0.1f, 0.96f, -0.5f), new Vector3(0.14f, 0.025f, 0.03f), Glow(Lemon, 1.2f));

            // Backbox: the station screen.
            BlockKit.Block("Backbox", p, new Vector3(0f, 0f, 0.66f), Vector3.zero, 1.0f, 1.95f,
                new Vector2(0.4f, 0.09f), new Vector2(0.4f, 0.09f), 0.02f, CabinetBody);
            BuildKit.SpawnLocal(PrimitiveType.Quad, "MonitorScreen", p, new Vector3(0f, 1.52f, 0.565f), Vector3.zero,
                new Vector3(0.72f, 0.66f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, Teal, 0.55f)), false);

            // High-score board on a stand beside the table.
            var board = new GameObject("HighScoreBoard").transform;
            board.SetParent(station, false);
            board.localPosition = new Vector3(1.55f, 0f, 0.55f);
            BlockKit.Block("Stand", board, Vector3.zero, Vector3.zero, 0f, 1.1f,
                new Vector2(0.06f, 0.06f), new Vector2(0.05f, 0.05f), 0.01f, Chrome);
            BlockKit.Slab("Panel", board, new Vector3(0f, 1.6f, 0f), new Vector3(1.0f, 1.0f, 0.08f), 0.02f, DarkGlass);
            BlockKit.Box("PanelGlow", board, new Vector3(0f, 2.1f, -0.045f), new Vector3(0.95f, 0.025f, 0.01f), Glow(Teal, 1.6f));
            MakeText(board, "HighScore_Text", new Vector3(0f, 1.6f, -0.05f),
                "HIGH SCORES\n1  ACE   98,410\n2  JLEE  87,200\n3  MO    64,050\n4  KAI   51,990",
                Teal, 0.9f, 0.9f, 0.9f);

            return new Screen { center = p.localPosition + new Vector3(0f, 1.52f, 0.55f), pitch = 0f, size = new Vector2(0.72f, 0.66f) };
        }
    }
}
