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
    /// Behind the station rows, the north end holds hoop shooters, a console
    /// lounge, a prize counter with a bowl of free USB drives, a LAN desk
    /// with a password on a sticky note, and air hockey.
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

            // The north end, behind the station rows. Each piece sits in a bay
            // between the north wall's columns (x = -16, -8, 0, 8, 16) and
            // keeps clear of the machines and the walk to the exit door (x 4).
            BuildHoopShooter(root, new Vector3(-18.55f, 0f, 18.0f), Magenta);
            BuildHoopShooter(root, new Vector3(-17.25f, 0f, 18.0f), Teal);
            BuildConsoleLounge(root, new Vector3(-12.6f, 0f, 17.6f));
            BuildPrizeCounter(root, new Vector3(-4.0f, 0f, 18.2f));
            BuildLanCorner(root, new Vector3(10.6f, 0f, 18.85f));
            BuildAirHockey(root, new Vector3(15.2f, 0f, 16.2f));
            PropFactory.BuildPlant(root, new Vector3(-9.2f, 0f, 18.7f));
            PropFactory.BuildPlant(root, new Vector3(18.6f, 0f, 18.6f));

            // Neon on the side walls, below the wall strip (y 3.4) and
            // between the columns.
            WallNeon(root, "Neon_LevelUp", new Vector3(-19.45f, 2.6f, 4f), -90f,
                "LEVEL UP", Lemon, 4.2f, new Vector2(2.6f, 0.5f));
            WallNeon(root, "Neon_NoRageQuit", new Vector3(19.45f, 2.6f, -4f), 90f,
                "NO RAGE QUITTING", Magenta, 3.0f, new Vector2(3.4f, 0.42f));

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

            // The dividing wall: the snack bar on the south side, the game
            // floor on the north, with the archway as the way through.
            Material wall = Plain(BuildKit.WallColor);
            Material strip = Glow(accent, 1.2f);
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0f ? "L" : "R";
                const float inner = postX + 0.2f, outer = 19.5f, height = 7.2f;
                float cx = side * (inner + outer) * 0.5f, w = outer - inner;
                BlockKit.Slab("DividerWall_" + s, arch, new Vector3(cx, height * 0.5f, 0f),
                    new Vector3(w, height, 0.3f), 0.02f, wall, solid: true);
                foreach (float z in new[] { -0.17f, 0.17f })
                {
                    BlockKit.Box("DividerStrip_" + s, arch, new Vector3(cx, 3.4f, z), new Vector3(w, 0.08f, 0.02f), strip);
                    BlockKit.Box("DividerTrim_" + s, arch, new Vector3(cx, 0.1f, z), new Vector3(w, 0.1f, 0.02f), Glow(accent, 2f));
                }
            }
            BlockKit.Slab("DividerHeader", arch, new Vector3(0f, 6.85f, 0f),
                new Vector3(postX * 2f + 0.4f, 0.7f, 0.3f), 0.02f, wall);

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
                    BlockKit.Box($"StoolFootRing_{s}{k}", bar, stool + Vector3.up * 0.28f, new Vector3(0.3f, 0.02f, 0.3f), Chrome);
                }

                BuildCounterDress(bar, x, side < 0f ? Magenta : Teal);
                BuildCanopy(bar, x, s, side < 0f ? "SNACK BAR" : "FREE DRINKS", side < 0f ? Lemon : Teal);
                if (side < 0f) BuildSnackCounterTop(bar, x);
                else BuildCoffeeCounterTop(bar, x);
            }
        }

        /// <summary>Wood slats and a lit kick plate on the counter front, and
        /// a chrome lip on the top, so the counters read as built joinery.</summary>
        private static void BuildCounterDress(Transform bar, float x, Color accent)
        {
            var slats = new List<CombineInstance>();
            for (int i = 0; i < 11; i++)
                slats.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x - 1.35f + i * 0.27f, 0.52f, -0.415f),
                    Vector3.zero, new Vector3(0.2f, 0.72f, 0.02f)));
            BlockKit.Batch("CounterSlats", bar, Counter, slats);
            BlockKit.Box("KickGlow", bar, new Vector3(x, 0.07f, -0.405f), new Vector3(2.9f, 0.025f, 0.01f), Glow(accent, 0.9f));
            BlockKit.Box("CounterLip", bar, new Vector3(x, 1.06f, -0.5f), new Vector3(3.05f, 0.025f, 0.02f), Chrome);
        }

        /// <summary>A canopy over the counter on two posts, carrying a neon
        /// name sign and three warm pendant lamps.</summary>
        private static void BuildCanopy(Transform bar, float x, string s, string title, Color colour)
        {
            foreach (float px in new[] { -1.45f, 1.45f })
                BlockKit.Box("CanopyPost_" + s, bar, new Vector3(x + px, 1.45f, 1.45f), new Vector3(0.08f, 2.9f, 0.08f), CabinetTrim);
            BlockKit.Slab("Canopy_" + s, bar, new Vector3(x, 2.95f, 0.4f), new Vector3(3.3f, 0.1f, 2.1f), 0.02f, CabinetBody);
            BlockKit.Box("CanopyEdge_" + s, bar, new Vector3(x, 2.89f, -0.65f), new Vector3(3.3f, 0.02f, 0.02f), Glow(colour, 1.2f));
            WallNeon(bar, "Neon_" + s, new Vector3(x, 3.32f, -0.6f), 0f, title, colour, 2.4f, new Vector2(2.4f, 0.32f));

            Color warm = new Color(1f, 0.78f, 0.5f);
            for (int k = -1; k <= 1; k++)
            {
                Vector3 at = new Vector3(x + k * 1.0f, 0f, -0.15f);
                BlockKit.Box("PendantCord", bar, at + new Vector3(0f, 2.62f, 0f), new Vector3(0.012f, 0.56f, 0.012f), CabinetTrim);
                BlockKit.Block("PendantShade", bar, at + new Vector3(0f, 2.18f, 0f), Vector3.zero, 0f, 0.18f,
                    new Vector2(0.16f, 0.16f), new Vector2(0.05f, 0.05f), 0.01f, CabinetTrim);
                var bulb = BuildKit.SpawnLocal(PrimitiveType.Sphere, "PendantBulb", bar, at + new Vector3(0f, 2.17f, 0f),
                    Vector3.zero, Vector3.one * 0.11f, Glow(warm, 1.8f), false);
                bulb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Chip baskets and a fruit bowl on the snack counter.</summary>
        private static void BuildSnackCounterTop(Transform bar, float x)
        {
            Color[] bagColours = { Magenta, Lemon, Teal, Violet };
            var rnd = new System.Random(11);
            for (int b = 0; b < 2; b++)
            {
                Vector3 c = new Vector3(x - 1.0f + b * 0.75f, 1.06f, -0.05f);
                BlockKit.Block("ChipBasket", bar, c, Vector3.zero, 0f, 0.12f,
                    new Vector2(0.24f, 0.16f), new Vector2(0.27f, 0.19f), 0.01f, Cork);
                var bags = new List<CombineInstance>();
                for (int i = 0; i < 5; i++)
                    bags.Add(BlockKit.Piece(BlockKit.CubeMesh,
                        c + new Vector3(-0.18f + i * 0.09f, 0.17f, (float)rnd.NextDouble() * 0.06f - 0.03f),
                        new Vector3(-15f + (float)rnd.NextDouble() * 10f, 0f, (float)rnd.NextDouble() * 16f - 8f),
                        new Vector3(0.08f, 0.2f, 0.03f)));
                BlockKit.Batch("ChipBags", bar, Plain(bagColours[b * 2] * 0.85f), bags);
            }
            Vector3 bowl = new Vector3(x + 0.85f, 1.06f, 0f);
            BlockKit.Block("FruitBowl", bar, bowl, Vector3.zero, 0f, 0.1f,
                new Vector2(0.12f, 0.12f), new Vector2(0.2f, 0.2f), 0.01f, Plain(new Color(0.9f, 0.9f, 0.88f)));
            Color[] fruit = { new Color(0.85f, 0.15f, 0.12f), new Color(0.95f, 0.6f, 0.1f), new Color(0.5f, 0.75f, 0.2f), new Color(0.95f, 0.85f, 0.2f) };
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                var f = BuildKit.SpawnLocal(PrimitiveType.Sphere, "Fruit", bar,
                    bowl + new Vector3(Mathf.Cos(a) * 0.08f, 0.14f + (i == 0 ? 0.05f : 0f), Mathf.Sin(a) * 0.08f),
                    Vector3.zero, Vector3.one * 0.09f, Plain(fruit[i % fruit.Length]), false);
                f.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>A coffee machine, cups and a napkin box on the drinks counter.</summary>
        private static void BuildCoffeeCounterTop(Transform bar, float x)
        {
            Vector3 m = new Vector3(x - 0.8f, 1.06f, 0.05f);
            BlockKit.Slab("CoffeeMachine", bar, m + new Vector3(0f, 0.24f, 0f), new Vector3(0.38f, 0.48f, 0.34f), 0.03f, Chrome);
            BlockKit.Slab("CoffeeMachineFace", bar, m + new Vector3(0f, 0.3f, -0.17f), new Vector3(0.3f, 0.2f, 0.01f), 0.005f, DarkGlass);
            BlockKit.Box("CoffeeMachineLed", bar, m + new Vector3(0.09f, 0.36f, -0.178f), new Vector3(0.05f, 0.03f, 0.004f), Glow(Teal, 1.6f));
            BlockKit.Box("DripTray", bar, m + new Vector3(0f, 0.02f, -0.12f), new Vector3(0.24f, 0.03f, 0.14f), CabinetTrim);
            for (int i = 0; i < 2; i++)
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "CupStack", bar, new Vector3(x - 0.2f + i * 0.14f, 1.2f, -0.05f),
                    Vector3.zero, new Vector3(0.08f, 0.14f, 0.08f), Plain(new Color(0.95f, 0.93f, 0.88f)), false);
            BlockKit.Slab("NapkinBox", bar, new Vector3(x + 0.35f, 1.12f, -0.05f), new Vector3(0.16f, 0.12f, 0.12f), 0.01f, Plain(Teal * 0.6f));
            BlockKit.Slab("SyrupRack", bar, new Vector3(x + 0.9f, 1.09f, 0.1f), new Vector3(0.44f, 0.06f, 0.14f), 0.01f, Counter);
            Color[] syrups = { new Color(0.55f, 0.25f, 0.1f), new Color(0.9f, 0.55f, 0.2f), new Color(0.6f, 0.1f, 0.3f) };
            for (int i = 0; i < 3; i++)
                BlockKit.Block("SyrupBottle", bar, new Vector3(x + 0.76f + i * 0.14f, 1.12f, 0.1f), Vector3.zero, 0f, 0.28f,
                    new Vector2(0.035f, 0.035f), new Vector2(0.015f, 0.015f), 0.005f, Glow(syrups[i], 0.5f));
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

        // ---- North end ----------------------------------------------------------

        private static Transform Group(Transform parent, string name, Vector3 position, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        /// <summary>A basketball shooter: a ramp lane rising from the player's
        /// end (-Z) to a backboard against the wall.</summary>
        private static void BuildHoopShooter(Transform parent, Vector3 position, Color accent)
        {
            var t = Group(parent, "HoopShooter", position, 0f);

            BlockKit.Block("Cabinet", t, new Vector3(0f, 0f, -1.05f), Vector3.zero, 0f, 0.95f,
                new Vector2(0.55f, 0.3f), new Vector2(0.55f, 0.3f), 0.03f, CabinetBody, solid: true);
            BlockKit.Box("CabinetGlow", t, new Vector3(0f, 0.62f, -1.36f), new Vector3(1.0f, 0.12f, 0.01f), Glow(accent, 1.4f));
            BlockKit.MeshPart("Ramp", t, BlockKit.CubeMesh, new Vector3(0f, 1.0f, 0.2f), new Vector3(-8f, 0f, 0f),
                Plain(new Color(0.20f, 0.12f, 0.08f)), solid: true, scale: new Vector3(1.04f, 0.06f, 2.3f));
            foreach (float x in new[] { -0.54f, 0.54f })
            {
                BlockKit.Box("SideNet", t, new Vector3(x, 1.65f, 0.25f), new Vector3(0.03f, 1.2f, 2.3f),
                    Plain(new Color(0.07f, 0.06f, 0.10f)));
                BlockKit.Box("SideRail", t, new Vector3(x, 2.26f, 0.25f), new Vector3(0.05f, 0.04f, 2.3f), Glow(accent, 1.4f));
            }
            for (int i = 0; i < 5; i++)
            {
                var ball = BuildKit.SpawnLocal(PrimitiveType.Sphere, "Ball", t, new Vector3(-0.32f + i * 0.16f, 0.99f, -0.75f),
                    Vector3.zero, Vector3.one * 0.2f, Plain(new Color(0.95f, 0.45f, 0.12f)), false);
                ball.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            BlockKit.Box("Post", t, new Vector3(0f, 1.5f, 1.38f), new Vector3(0.12f, 3.0f, 0.12f), CabinetTrim);
            BlockKit.Slab("Backboard", t, new Vector3(0f, 2.45f, 1.28f), new Vector3(1.0f, 0.7f, 0.04f), 0.01f,
                Plain(new Color(0.88f, 0.90f, 0.94f)));
            var square = new List<CombineInstance>
            {
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, 2.5f, 1.255f), Vector3.zero, new Vector3(0.36f, 0.025f, 0.01f)),
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, 2.24f, 1.255f), Vector3.zero, new Vector3(0.36f, 0.025f, 0.01f)),
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(-0.17f, 2.37f, 1.255f), Vector3.zero, new Vector3(0.025f, 0.28f, 0.01f)),
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0.17f, 2.37f, 1.255f), Vector3.zero, new Vector3(0.025f, 0.28f, 0.01f)),
            };
            BlockKit.Batch("Target", t, Glow(accent, 1.4f), square);

            var ring = new List<CombineInstance>();
            const int segments = 12;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 360f / segments;
                Vector3 at = new Vector3(0f, 2.2f, 1.04f) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.2f;
                ring.Add(BlockKit.Piece(BlockKit.CubeMesh, at, new Vector3(0f, a, 0f), new Vector3(0.11f, 0.02f, 0.02f)));
            }
            BlockKit.Batch("Hoop", t, Glow(new Color(1f, 0.4f, 0.1f), 1.4f), ring);
            BlockKit.Block("Net", t, new Vector3(0f, 1.93f, 1.04f), Vector3.zero, 0f, 0.26f,
                new Vector2(0.12f, 0.12f), new Vector2(0.18f, 0.18f), 0f, WhiteLine);

            BlockKit.Slab("ScoreBox", t, new Vector3(0f, 2.98f, 1.28f), new Vector3(1.0f, 0.3f, 0.1f), 0.01f, DarkGlass);
            MakeText(t, "HoopShot_Text", new Vector3(0f, 2.98f, 1.22f), "HOOP SHOT", accent, 1.3f, 0.9f, 0.24f)
                .fontStyle = FontStyles.Bold;
        }

        /// <summary>A couch facing a big wall TV mid-race, on a rug.</summary>
        private static void BuildConsoleLounge(Transform parent, Vector3 position)
        {
            var t = Group(parent, "ConsoleLounge", position, 0f);

            BlockKit.Box("Rug", t, new Vector3(0f, 0.012f, 0f), new Vector3(4.6f, 0.02f, 2.8f), Plain(new Color(0.16f, 0.07f, 0.24f)));
            BlockKit.Batch("RugBorder", t, Glow(Violet, 0.5f), BlockKit.Outline(new Vector3(0f, 0.026f, 0f), 4.3f, 2.5f, 0.06f));

            // The TV hangs on the north wall (local z 1.9).
            BlockKit.Slab("TvBezel", t, new Vector3(0f, 1.85f, 1.86f), new Vector3(3.0f, 1.7f, 0.08f), 0.01f, DarkGlass);
            BuildKit.SpawnLocal(PrimitiveType.Quad, "TvScreen", t, new Vector3(0f, 1.85f, 1.815f), Vector3.zero,
                new Vector3(2.8f, 1.5f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, Teal, 0.35f)), false);
            MakeText(t, "Tv_Title", new Vector3(0f, 2.35f, 1.81f), "KART CUP  ·  LAP 2 / 3", Lemon, 1.8f, 2.6f, 0.3f)
                .fontStyle = FontStyles.Bold;
            MakeText(t, "Tv_Players", new Vector3(0f, 1.3f, 1.81f), "P1  1st          P2  4th", Color.white, 1.4f, 2.6f, 0.24f);
            BlockKit.Box("Tv_Track", t, new Vector3(0f, 1.72f, 1.81f), new Vector3(2.4f, 0.04f, 0.004f), Glow(WhiteLine.color, 0.8f));
            BlockKit.Box("Tv_KartA", t, new Vector3(0.35f, 1.8f, 1.808f), new Vector3(0.2f, 0.1f, 0.004f), Glow(Magenta, 1.6f));
            BlockKit.Box("Tv_KartB", t, new Vector3(-0.45f, 1.8f, 1.808f), new Vector3(0.2f, 0.1f, 0.004f), Glow(Teal, 1.6f));

            BlockKit.Slab("MediaConsole", t, new Vector3(0f, 0.25f, 1.6f), new Vector3(2.4f, 0.5f, 0.42f), 0.02f, Counter, solid: true);
            BlockKit.Slab("GameConsole", t, new Vector3(-0.6f, 0.55f, 1.6f), new Vector3(0.42f, 0.09f, 0.3f), 0.01f,
                Plain(new Color(0.85f, 0.86f, 0.9f)));
            BlockKit.Box("GameConsoleLed", t, new Vector3(-0.6f, 0.55f, 1.445f), new Vector3(0.3f, 0.012f, 0.005f), Glow(Teal, 1.6f));

            PropFactory.BuildCouch(t, new Vector3(0f, 0f, -0.8f), 0f);
            BlockKit.Slab("ControllerA", t, new Vector3(-0.45f, 0.6f, -0.75f), new Vector3(0.16f, 0.04f, 0.1f), 0.01f,
                Plain(new Color(0.1f, 0.1f, 0.12f)));
            BlockKit.Slab("ControllerB", t, new Vector3(0.5f, 0.6f, -0.7f), new Vector3(0.16f, 0.04f, 0.1f), 0.01f,
                Plain(Magenta * 0.7f));
            BuildBeanbag(t, new Vector3(1.9f, 0f, 0.3f), Violet);
        }

        /// <summary>Ticket prizes on wall shelves behind a counter. The bowl
        /// of free USB drives is the baiting attack, left out in plain sight.</summary>
        private static void BuildPrizeCounter(Transform parent, Vector3 position)
        {
            var t = Group(parent, "PrizeCounter", position, 0f);

            BlockKit.Block("Counter", t, Vector3.zero, Vector3.zero, 0f, 0.95f,
                new Vector2(2.2f, 0.35f), new Vector2(2.2f, 0.35f), 0.03f, CabinetBody, solid: true);
            BlockKit.Slab("CounterTop", t, new Vector3(0f, 0.98f, 0f), new Vector3(4.5f, 0.05f, 0.75f), 0.01f, DarkGlass);
            BlockKit.Box("CounterGlow", t, new Vector3(0f, 0.9f, -0.36f), new Vector3(4.3f, 0.02f, 0.01f), Glow(Magenta, 1.6f));
            BlockKit.Slab("TicketMachine", t, new Vector3(-1.5f, 1.14f, 0.05f), new Vector3(0.36f, 0.28f, 0.28f), 0.02f, CabinetTrim);
            BlockKit.Box("TicketScreen", t, new Vector3(-1.5f, 1.18f, -0.095f), new Vector3(0.26f, 0.12f, 0.005f), Glow(Lemon, 1.2f));

            // Shelves on the wall behind (local z 1.3 is the wall).
            foreach (float x in new[] { -2.3f, 2.3f })
                BlockKit.Box("Upright", t, new Vector3(x, 1.05f, 1.1f), new Vector3(0.06f, 2.1f, 0.4f), CabinetTrim);
            var prizes = new[] { new List<CombineInstance>(), new List<CombineInstance>(), new List<CombineInstance>(), new List<CombineInstance>() };
            var rnd = new System.Random(7);
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float y = 0.9f + shelf * 0.55f;
                BlockKit.Slab("Shelf", t, new Vector3(0f, y, 1.1f), new Vector3(4.6f, 0.04f, 0.4f), 0.005f, Counter);
                if (shelf == 2) continue; // the top shelf holds the plushies
                for (int k = 0; k < 9; k++)
                {
                    float w = 0.2f + (float)rnd.NextDouble() * 0.16f;
                    float h = 0.18f + (float)rnd.NextDouble() * 0.2f;
                    prizes[(k + shelf) % 4].Add(BlockKit.Piece(BlockKit.CubeMesh,
                        new Vector3(-2.0f + k * 0.5f, y + 0.02f + h * 0.5f, 1.1f), Vector3.zero, new Vector3(w, h, 0.26f)));
                }
            }
            Color[] prizeColours = { Magenta, Teal, Lemon, Violet };
            for (int i = 0; i < prizes.Length; i++)
                BlockKit.Batch("Prizes_" + i, t, Plain(prizeColours[i] * 0.8f), prizes[i]);
            for (int k = 0; k < 5; k++)
            {
                var plush = BuildKit.SpawnLocal(PrimitiveType.Sphere, "Plush", t, new Vector3(-1.8f + k * 0.9f, 2.17f, 1.1f),
                    Vector3.zero, new Vector3(0.32f, 0.3f, 0.28f), Plain(prizeColours[k % 4] * 0.85f), false);
                plush.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // The free USB bowl, nearest the room.
            BlockKit.Block("UsbBowl", t, new Vector3(1.4f, 1.005f, -0.1f), Vector3.zero, 0f, 0.1f,
                new Vector2(0.16f, 0.16f), new Vector2(0.22f, 0.22f), 0.01f, Plain(Teal * 0.6f));
            var sticks = new List<CombineInstance>();
            for (int i = 0; i < 9; i++)
                sticks.Add(BlockKit.Piece(BlockKit.CubeMesh,
                    new Vector3(1.4f - 0.12f + (float)rnd.NextDouble() * 0.24f, 1.1f + (float)rnd.NextDouble() * 0.03f,
                        -0.1f - 0.12f + (float)rnd.NextDouble() * 0.24f),
                    new Vector3(0f, (float)rnd.NextDouble() * 180f, 0f), new Vector3(0.07f, 0.018f, 0.025f)));
            BlockKit.Batch("UsbDrives", t, Chrome, sticks);
            BlockKit.Slab("UsbCard", t, new Vector3(1.4f, 1.17f, -0.3f), new Vector3(0.72f, 0.3f, 0.02f), 0.005f, Paper);
            MakeText(t, "UsbCard_Text", new Vector3(1.4f, 1.17f, -0.315f), "FREE USB DRIVES\nTAKE ONE!",
                new Color(0.10f, 0.08f, 0.12f), 0.6f, 0.68f, 0.26f);

            WallNeon(t, "Neon_Prizes", new Vector3(0f, 2.7f, 1.25f), 0f, "PRIZE COUNTER", Lemon, 3.0f, new Vector2(3.2f, 0.42f));
        }

        /// <summary>A three-seat LAN desk against the wall. Someone left their
        /// login on a sticky note on the middle monitor.</summary>
        private static void BuildLanCorner(Transform parent, Vector3 position)
        {
            var t = Group(parent, "LanCorner", position, 0f);

            BlockKit.Slab("Desk", t, new Vector3(0f, 0.75f, 0f), new Vector3(4.2f, 0.06f, 0.8f), 0.01f, CabinetTrim, solid: true);
            foreach (float x in new[] { -2.0f, 0f, 2.0f })
                BlockKit.Box("DeskLeg", t, new Vector3(x, 0.36f, 0f), new Vector3(0.06f, 0.72f, 0.7f), CabinetBody);
            BlockKit.Box("DeskGlow", t, new Vector3(0f, 0.71f, -0.41f), new Vector3(4.1f, 0.02f, 0.01f), Glow(Teal, 1.4f));

            string[] screens = { "VICTORY", "RESPAWN", "GG" };
            Color[] colours = { Teal, Magenta, Lemon };
            var cans = new List<CombineInstance>();
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 1.4f;
                Color c = colours[i];
                BlockKit.Box("MonitorBase", t, new Vector3(x, 0.79f, 0.15f), new Vector3(0.25f, 0.02f, 0.18f), CabinetBody);
                BlockKit.Box("MonitorStand", t, new Vector3(x, 0.95f, 0.17f), new Vector3(0.06f, 0.32f, 0.04f), CabinetBody);
                BlockKit.Slab("MonitorPanel", t, new Vector3(x, 1.3f, 0.13f), new Vector3(0.8f, 0.46f, 0.04f), 0.01f, DarkGlass);
                BuildKit.SpawnLocal(PrimitiveType.Quad, "LanScreen", t, new Vector3(x, 1.3f, 0.105f), Vector3.zero,
                    new Vector3(0.74f, 0.4f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, c, 0.45f)), false);
                MakeText(t, "LanScreen_Text", new Vector3(x, 1.3f, 0.1f), screens[i], Color.Lerp(c, Color.white, 0.4f), 0.95f, 0.7f, 0.2f)
                    .fontStyle = FontStyles.Bold;
                BlockKit.Box("Keyboard", t, new Vector3(x, 0.79f, -0.18f), new Vector3(0.46f, 0.02f, 0.15f), CabinetBody);
                BlockKit.Box("KeyboardGlow", t, new Vector3(x, 0.801f, -0.18f), new Vector3(0.42f, 0.003f, 0.11f), Glow(c, 0.4f));
                BlockKit.Box("Mouse", t, new Vector3(x + 0.34f, 0.79f, -0.18f), new Vector3(0.06f, 0.025f, 0.1f), CabinetBody);
                cans.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x - 0.5f, 0.86f, -0.05f), Vector3.zero, new Vector3(0.07f, 0.16f, 0.07f)));
                BuildGamingChair(t, new Vector3(x, 0f, -1.05f), 180f + (i - 1) * 12f, c);
            }
            BlockKit.Batch("EnergyCans", t, Glow(Lemon, 0.9f), cans);

            BlockKit.Box("StickyNote", t, new Vector3(0.3f, 1.14f, 0.1f), new Vector3(0.18f, 0.14f, 0.004f), Plain(Lemon));
            MakeText(t, "StickyNote_Text", new Vector3(0.3f, 1.14f, 0.097f), "admin\nSummer24!",
                new Color(0.10f, 0.08f, 0.12f), 0.26f, 0.17f, 0.13f);

            WallNeon(t, "Neon_Lan", new Vector3(0f, 2.6f, 0.6f), 0f, "LAN PARTY", Teal, 3.6f, new Vector2(2.6f, 0.5f));
        }

        /// <summary>A gaming chair; the sitter faces local -Z.</summary>
        private static void BuildGamingChair(Transform parent, Vector3 position, float yaw, Color accent)
        {
            var c = Group(parent, "GamingChair", position, yaw);
            Material shell = Plain(new Color(0.07f, 0.06f, 0.09f));
            BlockKit.Box("BaseA", c, new Vector3(0f, 0.05f, 0f), new Vector3(0.6f, 0.04f, 0.08f), CabinetTrim);
            BlockKit.Box("BaseB", c, new Vector3(0f, 0.05f, 0f), new Vector3(0.08f, 0.04f, 0.6f), CabinetTrim);
            BlockKit.Box("Pole", c, new Vector3(0f, 0.27f, 0f), new Vector3(0.06f, 0.42f, 0.06f), Chrome);
            BlockKit.Slab("Seat", c, new Vector3(0f, 0.52f, 0f), new Vector3(0.56f, 0.1f, 0.54f), 0.03f, shell, solid: true);
            BlockKit.Slab("Back", c, new Vector3(0f, 1.0f, 0.27f), new Vector3(0.52f, 0.86f, 0.1f), 0.04f, shell);
            BlockKit.Slab("Headrest", c, new Vector3(0f, 1.5f, 0.27f), new Vector3(0.34f, 0.16f, 0.1f), 0.03f, shell);
            // Racing stripes on the back, which faces the room.
            foreach (float x in new[] { -0.14f, 0.14f })
                BlockKit.Box("Stripe", c, new Vector3(x, 1.0f, 0.322f), new Vector3(0.06f, 0.72f, 0.006f), Plain(accent * 0.85f));
        }

        private static void BuildAirHockey(Transform parent, Vector3 position)
        {
            var t = Group(parent, "AirHockey", position, 0f);

            BlockKit.Block("Body", t, Vector3.zero, Vector3.zero, 0f, 0.78f,
                new Vector2(1.08f, 0.58f), new Vector2(1.14f, 0.62f), 0.03f, CabinetBody, solid: true);
            BlockKit.Box("Surface", t, new Vector3(0f, 0.785f, 0f), new Vector3(2.2f, 0.01f, 1.16f), Plain(new Color(0.82f, 0.88f, 0.94f)));
            BlockKit.Batch("Rail", t, Glow(Teal, 1.4f), BlockKit.Outline(new Vector3(0f, 0.81f, 0f), 2.24f, 1.2f, 0.07f));
            BlockKit.Box("CentreLine", t, new Vector3(0f, 0.792f, 0f), new Vector3(0.015f, 0.004f, 1.1f), Glow(Magenta, 1.2f));
            foreach (float x in new[] { -1.115f, 1.115f })
                BlockKit.Box("Goal", t, new Vector3(x, 0.8f, 0f), new Vector3(0.03f, 0.04f, 0.38f), DarkGlass);
            foreach (float x in new[] { -0.8f, 0.75f })
            {
                Material m = Glow(x < 0f ? Magenta : Lemon, 1.2f);
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Mallet", t, new Vector3(x, 0.805f, x < 0f ? 0.15f : -0.2f),
                    Vector3.zero, new Vector3(0.1f, 0.015f, 0.1f), m, false);
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "MalletKnob", t, new Vector3(x, 0.84f, x < 0f ? 0.15f : -0.2f),
                    Vector3.zero, new Vector3(0.04f, 0.03f, 0.04f), m, false);
            }
            BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Puck", t, new Vector3(0.25f, 0.795f, -0.15f),
                Vector3.zero, new Vector3(0.07f, 0.004f, 0.07f), Plain(new Color(0.08f, 0.08f, 0.1f)), false);
        }

        /// <summary>A neon tube sign on a wall: lit text in a glowing frame on
        /// a dark backer. The text reads from local -Z.</summary>
        private static void WallNeon(Transform parent, string name, Vector3 position, float yaw,
            string text, Color colour, float fontSize, Vector2 size)
        {
            var t = Group(parent, name, position, yaw);
            BlockKit.Slab("Backer", t, new Vector3(0f, 0f, 0.03f), new Vector3(size.x + 0.3f, size.y + 0.24f, 0.04f), 0.01f, DarkGlass);
            float hx = size.x * 0.5f + 0.08f, hy = size.y * 0.5f + 0.06f;
            var tube = new List<CombineInstance>
            {
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, hy, 0f), Vector3.zero, new Vector3(hx * 2f, 0.025f, 0.02f)),
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, -hy, 0f), Vector3.zero, new Vector3(hx * 2f, 0.025f, 0.02f)),
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(-hx, 0f, 0f), Vector3.zero, new Vector3(0.025f, hy * 2f, 0.02f)),
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(hx, 0f, 0f), Vector3.zero, new Vector3(0.025f, hy * 2f, 0.02f)),
            };
            BlockKit.Batch("Tube", t, Glow(colour, 1.6f), tube);
            MakeText(t, "Text", new Vector3(0f, 0f, -0.015f), text, colour, fontSize, size.x, size.y).fontStyle = FontStyles.Bold;
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

        private static Material glass;

        /// <summary>Clear glass for machine fronts (the shared GlassPane
        /// shader); null if the shader is missing, so panes are skipped.</summary>
        private static Material Glass
        {
            get
            {
                if (glass != null) return glass;
                Shader shader = Resources.Load<Shader>("Shaders/GlassPane");
                if (shader == null) return null;
                glass = new Material(shader);
                glass.SetColor("_Color", new Color(0.55f, 0.65f, 0.85f));
                glass.SetFloat("_Alpha", 0.08f);
                glass.SetFloat("_ReflectionStrength", 0.18f);
                glass.SetFloat("_EdgeOpacity", 0.15f);
                return glass;
            }
        }

        /// <summary>A glass pane (quad) readable from its local -Z.</summary>
        private static void Pane(Transform parent, string name, Vector3 pos, float yaw, Vector2 size, float pitch = 0f)
        {
            if (Glass == null) return;
            BlockKit.MeshPart(name, parent, BlockKit.QuadMesh, pos, new Vector3(pitch, yaw, 0f), Glass,
                scale: new Vector3(size.x, size.y, 1f)).GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A backlit marquee: a dark frame, a softly lit panel and
        /// the game's name in bold, all facing local -Z.</summary>
        private static void Marquee(Transform parent, Vector3 center, Vector2 size, string title, Color colour, float fontSize)
        {
            BlockKit.Slab("MarqueeFrame", parent, center + new Vector3(0f, 0f, 0.02f),
                new Vector3(size.x + 0.06f, size.y + 0.06f, 0.04f), 0.01f, CabinetTrim);
            BlockKit.Box("MarqueeLight", parent, center, new Vector3(size.x, size.y, 0.01f),
                Glow(Color.Lerp(colour, Color.black, 0.55f), 1.2f));
            foreach (float y in new[] { -1f, 1f })
                BlockKit.Box("MarqueeEdge", parent, center + new Vector3(0f, y * size.y * 0.5f, -0.008f),
                    new Vector3(size.x, 0.012f, 0.01f), Glow(colour, 2f));
            var text = MakeText(parent, "MarqueeText", center + new Vector3(0f, 0f, -0.012f), title,
                Color.Lerp(colour, Color.white, 0.55f), fontSize, size.x * 0.94f, size.y * 0.9f);
            text.fontStyle = FontStyles.Bold | FontStyles.Italic;
            text.characterSpacing = 2f;
            // Titles differ in length, so shrink a long one to fit the panel.
            text.enableAutoSizing = true;
            text.fontSizeMin = fontSize * 0.5f;
            text.fontSizeMax = fontSize;
        }

        /// <summary>A coin door: a chrome plate with two lit coin slots.</summary>
        private static void CoinDoor(Transform parent, Vector3 center, Vector2 size, Color slot)
        {
            BlockKit.Slab("CoinDoor", parent, center, new Vector3(size.x, size.y, 0.025f), 0.006f, Chrome);
            foreach (float x in new[] { -0.25f, 0.25f })
            {
                Vector3 c = center + new Vector3(x * size.x, size.y * 0.18f, -0.016f);
                BlockKit.Box("CoinSlot", parent, c, new Vector3(0.05f, 0.07f, 0.008f), Glow(slot, 2f));
                BlockKit.Box("CoinReturn", parent, c + new Vector3(0f, -size.y * 0.42f, 0f),
                    new Vector3(0.06f, 0.035f, 0.01f), DarkGlass);
            }
        }

        /// <summary>A soft pool of colour on the carpet under a machine.</summary>
        private static void UnderGlow(Transform parent, Vector3 center, Vector2 size, Color colour) =>
            BlockKit.Box("UnderGlow", parent, center + new Vector3(0f, 0.004f, 0f),
                new Vector3(size.x, 0.004f, size.y), Glow(Color.Lerp(colour, Color.black, 0.6f), 1f));

        /// <summary>Three upright arcade cabinets side by side; the middle one
        /// is the station, the other two run attract mode. Each has the
        /// classic profile: kick plate, coin door, a sloped control panel, a
        /// screen leaning back under a speaker grille, and a lit marquee.
        /// The centre game is about MFA fatigue (push prompts until the
        /// user taps Approve).</summary>
        private static Screen BuildArcadeRow(Transform station)
        {
            Screen screen = default;
            Color[] art = { Teal, Magenta, Violet };
            string[] titles = { "PHISH FRENZY", "PUSH STORM", "TOKEN HEIST" };
            for (int i = -1; i <= 1; i++)
            {
                var cab = new GameObject(i == 0 ? "ArcadeCabinet" : "ArcadeCabinet_" + (i < 0 ? "L" : "R")).transform;
                cab.SetParent(station, false);
                cab.localPosition = new Vector3(i * 1.25f, 0f, 0.35f);
                Color c = art[i + 1];
                Material side = Plain(Color.Lerp(new Color(0.05f, 0.04f, 0.08f), c, 0.22f));
                Material molding = Glow(c, 1.6f);

                BlockKit.Block("Base", cab, Vector3.zero, Vector3.zero, 0f, 1.0f,
                    new Vector2(0.48f, 0.42f), new Vector2(0.48f, 0.42f), 0.02f, CabinetBody, solid: true);
                BlockKit.Block("Deck", cab, new Vector3(0f, 0f, -0.42f), new Vector3(-12f, 0f, 0f), 0.94f, 1.06f,
                    new Vector2(0.47f, 0.2f), new Vector2(0.47f, 0.2f), 0.02f, CabinetTrim);
                BlockKit.Block("Upper", cab, new Vector3(0f, 0f, 0.08f), Vector3.zero, 1.0f, 2.22f,
                    new Vector2(0.48f, 0.34f), new Vector2(0.48f, 0.3f), 0.02f, CabinetBody);

                // Side panels in the cabinet's art colour, proud of the body,
                // traced along the front edge with glowing T-molding.
                foreach (float sx in new[] { -0.5f, 0.5f })
                {
                    BlockKit.Slab("SideLower", cab, new Vector3(sx, 0.5f, 0f), new Vector3(0.04f, 1.0f, 0.86f), 0.01f, side);
                    BlockKit.Slab("SideUpper", cab, new Vector3(sx, 1.62f, 0.08f), new Vector3(0.04f, 1.24f, 0.7f), 0.01f, side);
                    BlockKit.MeshPart("SideDeck", cab, BlockKit.CubeMesh, new Vector3(sx, 1.03f, -0.36f),
                        new Vector3(-38f, 0f, 0f), side, scale: new Vector3(0.04f, 0.05f, 0.24f));
                    BlockKit.Box("Molding", cab, new Vector3(sx, 0.5f, -0.435f), new Vector3(0.05f, 0.96f, 0.012f), molding);
                    BlockKit.Box("Molding", cab, new Vector3(sx, 1.66f, -0.275f), new Vector3(0.05f, 1.12f, 0.012f), molding);
                    BlockKit.Box("Molding", cab, new Vector3(sx, 2.245f, 0.08f), new Vector3(0.05f, 0.012f, 0.7f), molding);
                    // A big stripe of side art, readable from the aisle.
                    BlockKit.MeshPart("SideArt", cab, BlockKit.CubeMesh, new Vector3(sx * 1.04f, 1.3f, 0.1f),
                        new Vector3(-30f, 0f, 0f), Glow(c, 0.8f), scale: new Vector3(0.01f, 0.9f, 0.06f));
                    BlockKit.MeshPart("SideArt", cab, BlockKit.CubeMesh, new Vector3(sx * 1.04f, 1.18f, 0.24f),
                        new Vector3(-30f, 0f, 0f), Glow(Lemon, 0.8f), scale: new Vector3(0.01f, 0.7f, 0.03f));
                }

                BlockKit.Box("KickPlate", cab, new Vector3(0f, 0.07f, -0.425f), new Vector3(0.94f, 0.12f, 0.02f), Chrome);
                CoinDoor(cab, new Vector3(0f, 0.52f, -0.43f), new Vector2(0.34f, 0.4f), new Color(1f, 0.25f, 0.2f));
                UnderGlow(cab, new Vector3(0f, 0f, -0.2f), new Vector2(1.1f, 1.2f), c);

                // Control panel: chrome lip, joystick, six buttons in two rows
                // and a lit 1P START.
                BlockKit.MeshPart("PanelLip", cab, BlockKit.CubeMesh, new Vector3(0f, 0.955f, -0.615f),
                    new Vector3(-12f, 0f, 0f), Chrome, scale: new Vector3(0.94f, 0.03f, 0.02f));
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "StickBase", cab, new Vector3(-0.24f, 1.075f, -0.47f),
                    new Vector3(-12f, 0f, 0f), new Vector3(0.09f, 0.008f, 0.09f), Chrome, false);
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Stick", cab, new Vector3(-0.24f, 1.12f, -0.47f),
                    Vector3.zero, new Vector3(0.022f, 0.05f, 0.022f), Chrome, false);
                BuildKit.SpawnLocal(PrimitiveType.Sphere, "StickBall", cab, new Vector3(-0.24f, 1.18f, -0.47f),
                    Vector3.zero, Vector3.one * 0.075f, Plain(new Color(0.85f, 0.1f, 0.15f)), false);
                Color[] buttons = { Lemon, Magenta, Teal };
                for (int row = 0; row < 2; row++)
                    for (int b = 0; b < 3; b++)
                    {
                        float z = -0.43f - row * 0.09f;
                        BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Button", cab,
                            new Vector3(0.0f + b * 0.11f + row * 0.03f, 1.085f + (z + 0.47f) * 0.21f, z),
                            new Vector3(-12f, 0f, 0f), new Vector3(0.06f, 0.014f, 0.06f), Glow(buttons[b], 1.3f), false);
                    }
                BlockKit.MeshPart("StartButton", cab, BlockKit.CubeMesh, new Vector3(0.36f, 1.08f, -0.44f),
                    new Vector3(-12f, 0f, 0f), Glow(Color.white, 1.4f), scale: new Vector3(0.06f, 0.012f, 0.035f));

                // The screen leans back inside the cabinet's upper half, inside
                // a black bezel with a thin glowing inner edge.
                var bezel = new GameObject("Bezel").transform;
                bezel.SetParent(cab, false);
                bezel.localPosition = new Vector3(0f, 1.52f, -0.24f);
                bezel.localRotation = Quaternion.Euler(10f, 0f, 0f); // leans back, arcade style
                BlockKit.Slab("Frame", bezel, Vector3.zero, new Vector3(0.92f, 0.74f, 0.06f), 0.01f, DarkGlass);
                var edge = new List<CombineInstance>();
                foreach (float y in new[] { -0.325f, 0.325f })
                    edge.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, y, -0.032f), Vector3.zero, new Vector3(0.83f, 0.01f, 0.005f)));
                foreach (float x in new[] { -0.415f, 0.415f })
                    edge.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, 0f, -0.032f), Vector3.zero, new Vector3(0.01f, 0.66f, 0.005f)));
                BlockKit.Batch("BezelEdge", bezel, Glow(c, 1.1f), edge);
                BuildKit.SpawnLocal(PrimitiveType.Quad, "MonitorScreen", bezel, new Vector3(0f, 0f, -0.035f), Vector3.zero,
                    new Vector3(0.8f, 0.62f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, c, i == 0 ? 0.6f : 0.35f)), false);
                Pane(bezel, "ScreenGlass", new Vector3(0f, 0f, -0.045f), 0f, new Vector2(0.86f, 0.68f));
                if (i == 0) screen = new Screen { center = cab.localPosition + bezel.localPosition + new Vector3(0f, 0f, -0.07f), pitch = 10f, size = new Vector2(0.8f, 0.62f) };

                // Speaker grille and marquee above the screen.
                BlockKit.Slab("SpeakerPanel", cab, new Vector3(0f, 1.95f, -0.25f), new Vector3(0.92f, 0.12f, 0.04f), 0.01f, CabinetTrim);
                var slats = new List<CombineInstance>();
                for (int k = 0; k < 9; k++)
                {
                    float x = -0.38f + k * 0.095f;
                    if (Mathf.Abs(x) < 0.12f) continue;
                    slats.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, 1.95f, -0.272f), Vector3.zero, new Vector3(0.04f, 0.07f, 0.005f)));
                }
                BlockKit.Batch("SpeakerSlats", cab, DarkGlass, slats);
                Marquee(cab, new Vector3(0f, 2.12f, -0.29f), new Vector2(0.9f, 0.18f), titles[i + 1], c, 1.05f);
            }
            return screen;
        }

        /// <summary>A claw machine whose prize pile is keycards; the gold one
        /// has far more access than it needs, which is the lesson. Glass on
        /// three sides, chasing bulbs round the canopy, a coin box and a
        /// prize chute with a flap.</summary>
        private static Screen BuildClawMachine(Transform station)
        {
            var m = new GameObject("ClawMachine").transform;
            m.SetParent(station, false);
            m.localPosition = new Vector3(0f, 0f, 0.35f);

            BlockKit.Block("Base", m, Vector3.zero, Vector3.zero, 0f, 0.95f,
                new Vector2(0.75f, 0.65f), new Vector2(0.75f, 0.65f), 0.03f, CabinetBody, solid: true);
            BlockKit.Box("KickPlate", m, new Vector3(0f, 0.06f, -0.655f), new Vector3(1.44f, 0.1f, 0.02f), Chrome);
            BlockKit.Box("BaseGlow", m, new Vector3(0f, 0.93f, -0.66f), new Vector3(1.45f, 0.025f, 0.01f), Glow(Magenta, 1.8f));
            // Big lit stars on the base's sides.
            foreach (float sx in new[] { -0.76f, 0.76f })
                foreach (float a in new[] { 0f, 60f, 120f })
                    BlockKit.MeshPart("SideStar", m, BlockKit.CubeMesh, new Vector3(sx, 0.5f, 0f),
                        new Vector3(a, 0f, 0f), Glow(Lemon, 1.1f), scale: new Vector3(0.01f, 0.36f, 0.05f));
            UnderGlow(m, Vector3.zero, new Vector2(1.7f, 1.5f), Magenta);

            // The glass box: chrome posts with light strips, a floor, a lit
            // back panel and three panes.
            foreach (float x in new[] { -0.72f, 0.72f })
                foreach (float z in new[] { -0.62f, 0.62f })
                {
                    BlockKit.Box("Post", m, new Vector3(x, 1.5f, z), new Vector3(0.05f, 1.1f, 0.05f), Chrome);
                    if (z < 0f)
                        BlockKit.Box("PostLight", m, new Vector3(x - Mathf.Sign(x) * 0.028f, 1.5f, z - 0.02f),
                            new Vector3(0.008f, 1.04f, 0.01f), Glow(Teal, 1.6f));
                }
            BlockKit.Box("Floor", m, new Vector3(0f, 0.96f, 0f), new Vector3(1.4f, 0.02f, 1.2f), Plain(new Color(0.18f, 0.08f, 0.26f)));
            BlockKit.Box("BackPanel", m, new Vector3(0f, 1.5f, 0.6f), new Vector3(1.4f, 1.08f, 0.02f), Glow(Violet, 0.35f));
            var dots = new List<CombineInstance>();
            for (int k = 0; k < 7; k++)
                for (int r = 0; r < 4; r++)
                    dots.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(-0.6f + k * 0.2f, 1.15f + r * 0.24f, 0.585f),
                        new Vector3(0f, 0f, 45f), new Vector3(0.03f, 0.03f, 0.005f)));
            BlockKit.Batch("BackPattern", m, Glow(Magenta, 0.9f), dots);
            Pane(m, "GlassFront", new Vector3(0f, 1.5f, -0.63f), 0f, new Vector2(1.42f, 1.08f));
            Pane(m, "GlassLeft", new Vector3(-0.73f, 1.5f, 0f), 90f, new Vector2(1.22f, 1.08f));
            Pane(m, "GlassRight", new Vector3(0.73f, 1.5f, 0f), -90f, new Vector2(1.22f, 1.08f));

            // Prize chute in the front-right corner, with a flap below.
            BlockKit.Box("Chute", m, new Vector3(0.5f, 1.12f, -0.4f), new Vector3(0.3f, 0.3f, 0.3f), DarkGlass);
            BlockKit.Box("ChuteRim", m, new Vector3(0.5f, 1.275f, -0.4f), new Vector3(0.32f, 0.02f, 0.32f), Glow(Lemon, 1.4f));

            // Keycards in three colours, lanyards and all, plus the gold one.
            var rnd = new System.Random(4);
            var piles = new[] { new List<CombineInstance>(), new List<CombineInstance>(), new List<CombineInstance>() };
            for (int i = 0; i < 34; i++)
            {
                float x = -0.6f + (float)rnd.NextDouble() * 0.9f;
                float z = -0.45f + (float)rnd.NextDouble() * 0.95f;
                float y = 0.99f + (float)rnd.NextDouble() * 0.16f;
                piles[i % 3].Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, y, z),
                    new Vector3((float)rnd.NextDouble() * 50f - 25f, (float)rnd.NextDouble() * 180f, 0f),
                    new Vector3(0.16f, 0.01f, 0.1f)));
            }
            BlockKit.Batch("Keycards", m, Plain(new Color(0.85f, 0.9f, 0.97f)), piles[0]);
            BlockKit.Batch("Keycards_Teal", m, Plain(new Color(0.25f, 0.7f, 0.75f)), piles[1]);
            BlockKit.Batch("Keycards_Pink", m, Plain(new Color(0.85f, 0.35f, 0.6f)), piles[2]);
            BlockKit.Box("AdminKeycard", m, new Vector3(-0.15f, 1.17f, 0.05f), new Vector3(0.2f, 0.012f, 0.13f), Glow(Lemon, 1.8f));
            BlockKit.Box("AdminStripe", m, new Vector3(-0.15f, 1.178f, 0.05f), new Vector3(0.2f, 0.004f, 0.03f), Plain(new Color(0.1f, 0.08f, 0.05f)));

            // Gantry rails, carriage and claw, hovering over the gold card.
            foreach (float z in new[] { -0.55f, 0.55f })
                BlockKit.Box("Rail", m, new Vector3(0f, 2.0f, z), new Vector3(1.4f, 0.03f, 0.03f), Chrome);
            BlockKit.Box("Gantry", m, new Vector3(-0.15f, 1.98f, 0f), new Vector3(0.05f, 0.04f, 1.12f), Chrome);
            BlockKit.Box("Carriage", m, new Vector3(-0.15f, 1.96f, 0.05f), new Vector3(0.12f, 0.06f, 0.12f), CabinetTrim);
            BlockKit.Box("Cable", m, new Vector3(-0.15f, 1.72f, 0.05f), new Vector3(0.012f, 0.44f, 0.012f), Chrome);
            BlockKit.Box("ClawHub", m, new Vector3(-0.15f, 1.5f, 0.05f), new Vector3(0.11f, 0.09f, 0.11f), Chrome);
            foreach (float a in new[] { 0f, 120f, 240f })
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                BlockKit.MeshPart("Prong", m, BlockKit.CubeMesh, new Vector3(-0.15f, 1.4f, 0.05f) + dir * 0.06f,
                    new Vector3(-25f, a, 0f), Chrome, scale: new Vector3(0.02f, 0.16f, 0.02f));
                BlockKit.MeshPart("ProngTip", m, BlockKit.CubeMesh, new Vector3(-0.15f, 1.31f, 0.05f) + dir * 0.09f,
                    new Vector3(25f, a, 0f), Chrome, scale: new Vector3(0.02f, 0.07f, 0.02f));
            }

            // Canopy with a marquee and chasing bulbs round its front.
            BlockKit.Slab("Canopy", m, new Vector3(0f, 2.2f, 0f), new Vector3(1.55f, 0.4f, 1.35f), 0.03f, CabinetBody);
            Marquee(m, new Vector3(0f, 2.2f, -0.69f), new Vector2(1.2f, 0.24f), "GRAB-A-BADGE", Lemon, 1.4f);
            var bulbs = new List<CombineInstance>();
            for (int k = 0; k < 15; k++)
            {
                float x = -0.7f + k * 0.1f;
                bulbs.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, 2.375f, -0.685f), new Vector3(0f, 0f, 45f), Vector3.one * 0.03f));
                bulbs.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, 2.025f, -0.685f), new Vector3(0f, 0f, 45f), Vector3.one * 0.03f));
            }
            BlockKit.Batch("ChaseBulbs", m, Glow(new Color(1f, 0.9f, 0.7f), 2f), bulbs);

            // Controls on the front of the deck: joystick, a big GRAB
            // button and a coin box.
            BlockKit.Slab("ControlDeck", m, new Vector3(-0.1f, 0.98f, -0.6f), new Vector3(0.7f, 0.04f, 0.14f), 0.01f, CabinetTrim);
            BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Stick", m, new Vector3(-0.3f, 1.04f, -0.6f),
                Vector3.zero, new Vector3(0.025f, 0.05f, 0.025f), Chrome, false);
            BuildKit.SpawnLocal(PrimitiveType.Sphere, "StickBall", m, new Vector3(-0.3f, 1.1f, -0.6f),
                Vector3.zero, Vector3.one * 0.075f, Plain(Lemon), false);
            BuildKit.SpawnLocal(PrimitiveType.Cylinder, "GrabButton", m, new Vector3(0.05f, 1.01f, -0.6f),
                Vector3.zero, new Vector3(0.1f, 0.02f, 0.1f), Glow(Magenta, 1.6f), false);
            CoinDoor(m, new Vector3(0.45f, 0.18f, -0.665f), new Vector2(0.4f, 0.2f), Lemon);

            // The station screen sits on the front of the base, under the box.
            BlockKit.Slab("FrontPanel", m, new Vector3(0f, 0.62f, -0.66f), new Vector3(1.2f, 0.42f, 0.03f), 0.01f, DarkGlass);
            BuildKit.SpawnLocal(PrimitiveType.Quad, "MonitorScreen", m, new Vector3(0f, 0.62f, -0.68f), Vector3.zero,
                new Vector3(1.1f, 0.34f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, Magenta, 0.5f)), false);
            return new Screen { center = m.localPosition + new Vector3(0f, 0.62f, -0.7f), pitch = 0f, size = new Vector2(1.1f, 0.34f) };
        }

        /// <summary>Three vending machines; the middle one is the station.
        /// Its glowing delivery slot is the "watched channel" the data leaves
        /// by. Each has a chrome-framed glass window over lit shelves of
        /// mixed snacks with price tags, a keypad column with a card reader,
        /// and its brand on top.</summary>
        private static Screen BuildVendingWall(Transform station)
        {
            Screen screen = default;
            Color[] fronts = { Teal, Magenta, Lemon };
            string[] brands = { "FIZZ", "BYTES", "SNAX" };
            Color[] snackMix = { Magenta, Lemon, Teal, Violet, new Color(1f, 0.5f, 0.2f) };
            for (int i = -1; i <= 1; i++)
            {
                var v = new GameObject(i == 0 ? "VendingMachine" : "VendingMachine_" + (i < 0 ? "L" : "R")).transform;
                v.SetParent(station, false);
                v.localPosition = new Vector3(i * 1.3f, 0f, 0.45f);
                Color c = fronts[i + 1];

                BlockKit.Block("Body", v, Vector3.zero, Vector3.zero, 0f, 2.1f,
                    new Vector2(0.6f, 0.42f), new Vector2(0.6f, 0.42f), 0.03f, CabinetBody, solid: true);
                BlockKit.Box("TopLight", v, new Vector3(0f, 2.105f, -0.3f), new Vector3(1.1f, 0.01f, 0.2f), Glow(c, 0.8f));
                foreach (float sx in new[] { -0.605f, 0.605f })
                    BlockKit.Box("SideStripe", v, new Vector3(sx, 1.05f, -0.3f), new Vector3(0.012f, 1.9f, 0.05f), Glow(c, 1.1f));
                BlockKit.Box("KickPlate", v, new Vector3(0f, 0.07f, -0.425f), new Vector3(1.16f, 0.12f, 0.02f), Chrome);
                UnderGlow(v, new Vector3(0f, 0f, -0.3f), new Vector2(1.25f, 0.9f), c);

                // Window: dark back, lit shelf lips, mixed snacks, price tags,
                // a chrome frame and glass.
                BlockKit.Box("Window", v, new Vector3(-0.12f, 1.2f, -0.425f), new Vector3(0.86f, 1.3f, 0.01f), DarkGlass);
                var lips = new List<CombineInstance>();
                var tags = new List<CombineInstance>();
                var snacks = new List<CombineInstance>[snackMix.Length];
                for (int s = 0; s < snacks.Length; s++) snacks[s] = new List<CombineInstance>();
                var rnd = new System.Random(20 + i);
                for (int shelf = 0; shelf < 5; shelf++)
                {
                    float y = 0.62f + shelf * 0.25f;
                    lips.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(-0.12f, y - 0.02f, -0.45f), Vector3.zero, new Vector3(0.82f, 0.012f, 0.02f)));
                    for (int k = 0; k < 5; k++)
                    {
                        float x = -0.48f + k * 0.18f;
                        float h = 0.1f + (float)rnd.NextDouble() * 0.06f;
                        int kind = (shelf + k * 2 + i + 1) % snackMix.Length;
                        // Two items deep, slightly offset, so rows read as stock.
                        snacks[kind].Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, y + h * 0.5f, -0.44f), Vector3.zero, new Vector3(0.11f, h, 0.025f)));
                        snacks[kind].Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x + 0.01f, y + h * 0.5f, -0.4f), Vector3.zero, new Vector3(0.11f, h, 0.025f)));
                        tags.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, y - 0.045f, -0.452f), Vector3.zero, new Vector3(0.05f, 0.025f, 0.004f)));
                    }
                }
                BlockKit.Batch("ShelfLips", v, Glow(c, 1.2f), lips);
                BlockKit.Batch("PriceTags", v, Paper, tags);
                for (int s = 0; s < snacks.Length; s++)
                    if (snacks[s].Count > 0)
                        BlockKit.Batch("Snacks_" + s, v, Glow(Color.Lerp(snackMix[s], Color.white, 0.15f), 0.55f), snacks[s]);
                var frame = new List<CombineInstance>();
                foreach (float y in new[] { 0.54f, 1.86f })
                    frame.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(-0.12f, y, -0.44f), Vector3.zero, new Vector3(0.92f, 0.035f, 0.03f)));
                foreach (float x in new[] { -0.565f, 0.325f })
                    frame.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, 1.2f, -0.44f), Vector3.zero, new Vector3(0.035f, 1.35f, 0.03f)));
                BlockKit.Batch("WindowFrame", v, Chrome, frame);
                Pane(v, "WindowGlass", new Vector3(-0.12f, 1.2f, -0.462f), 0f, new Vector2(0.86f, 1.3f));

                // Keypad column: brand, display, keys, card reader, coins.
                BlockKit.Box("Keypad", v, new Vector3(0.45f, 1.2f, -0.43f), new Vector3(0.2f, 1.3f, 0.01f), CabinetTrim);
                MakeText(v, "Brand", new Vector3(0.45f, 1.72f, -0.44f), brands[i + 1], c, 0.9f, 0.2f, 0.2f).fontStyle = FontStyles.Bold;
                BlockKit.Box("Display", v, new Vector3(0.45f, 1.5f, -0.438f), new Vector3(0.15f, 0.06f, 0.005f), Glow(new Color(0.3f, 1f, 0.5f), 1.2f));
                var keys = new List<CombineInstance>();
                for (int r = 0; r < 4; r++)
                    for (int k = 0; k < 3; k++)
                        keys.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0.405f + k * 0.045f, 1.4f - r * 0.05f, -0.438f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.008f)));
                BlockKit.Batch("Keys", v, Chrome, keys);
                BlockKit.Box("CardReader", v, new Vector3(0.45f, 1.05f, -0.445f), new Vector3(0.13f, 0.14f, 0.03f), DarkGlass);
                BlockKit.Box("CardLed", v, new Vector3(0.45f, 1.1f, -0.462f), new Vector3(0.06f, 0.012f, 0.005f), Glow(new Color(0.3f, 1f, 0.5f), 2f));
                BlockKit.Box("CoinSlot", v, new Vector3(0.45f, 0.88f, -0.44f), new Vector3(0.02f, 0.06f, 0.01f), Glow(Lemon, 1.5f));
                BlockKit.Box("CoinReturn", v, new Vector3(0.45f, 0.7f, -0.44f), new Vector3(0.1f, 0.06f, 0.015f), Chrome);

                // Delivery flap, chrome framed; the station's is lit.
                BlockKit.Box("SlotFrame", v, new Vector3(-0.12f, 0.3f, -0.428f), new Vector3(0.86f, 0.26f, 0.012f), Chrome);
                BlockKit.Box("Slot", v, new Vector3(-0.12f, 0.3f, -0.436f), new Vector3(0.8f, 0.2f, 0.01f),
                    i == 0 ? Glow(Teal, 1.6f) : DarkGlass);
                MakeText(v, "PushLabel", new Vector3(-0.12f, 0.3f, -0.444f), "PUSH", i == 0 ? new Color(0.05f, 0.2f, 0.2f) : new Color(0.5f, 0.5f, 0.55f), 0.7f, 0.4f, 0.12f);

                // A lit header across the top carries the station's screen.
                BlockKit.Slab("Header", v, new Vector3(0f, 2.0f, -0.43f), new Vector3(1.16f, 0.32f, 0.03f), 0.01f, DarkGlass);
                BuildKit.SpawnLocal(PrimitiveType.Quad, "MonitorScreen", v, new Vector3(0f, 2.0f, -0.45f), Vector3.zero,
                    new Vector3(1.08f, 0.26f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, c, i == 0 ? 0.55f : 0.35f)), false);
                if (i == 0) screen = new Screen { center = v.localPosition + new Vector3(0f, 2.0f, -0.47f), pitch = 0f, size = new Vector2(1.08f, 0.26f) };
            }
            return screen;
        }

        /// <summary>A pinball table with its backbox and, beside it, the high
        /// score board: wiping the board is the "cover tracks" temptation.
        /// The table has chrome rails and lockdown bar, a plunger, a coin
        /// door, slingshots, lit inserts and glass over the playfield.</summary>
        private static Screen BuildPinball(Transform station)
        {
            var p = new GameObject("Pinball").transform;
            p.SetParent(station, false);
            p.localPosition = new Vector3(0f, 0f, 0.1f);
            // Playfield height at local z (the table rises 5° toward the back).
            float Field(float z) => 0.995f + z * 0.087f;

            foreach (float x in new[] { -0.32f, 0.32f })
                foreach (float z in new[] { -0.6f, 0.6f })
                {
                    float h = z < 0f ? 0.72f : 0.82f;
                    BlockKit.Box("Leg", p, new Vector3(x, h * 0.5f, z), new Vector3(0.07f, h, 0.07f), Chrome);
                    BlockKit.Box("LegFoot", p, new Vector3(x, 0.015f, z), new Vector3(0.12f, 0.03f, 0.12f), Chrome);
                    BlockKit.Box("LegBolt", p, new Vector3(x, h - 0.06f, z - 0.04f), new Vector3(0.05f, 0.05f, 0.01f), CabinetTrim);
                }
            BlockKit.Block("Cabinet", p, new Vector3(0f, 0.86f, 0f), new Vector3(-5f, 0f, 0f), -0.24f, 0.12f,
                new Vector2(0.37f, 0.7f), new Vector2(0.37f, 0.7f), 0.02f, CabinetBody, solid: true);
            foreach (float sx in new[] { -0.375f, 0.375f })
            {
                BlockKit.MeshPart("SideArt", p, BlockKit.CubeMesh, new Vector3(sx, 0.82f, 0f), new Vector3(-5f, 0f, 0f),
                    Glow(Magenta, 1f), scale: new Vector3(0.01f, 0.03f, 1.3f));
                BlockKit.MeshPart("SideRail", p, BlockKit.CubeMesh, new Vector3(sx, Field(0f) + 0.03f, 0f), new Vector3(-5f, 0f, 0f),
                    Chrome, scale: new Vector3(0.04f, 0.05f, 1.42f));
            }
            BlockKit.Box("LockdownBar", p, new Vector3(0f, Field(-0.7f) + 0.03f, -0.71f), new Vector3(0.8f, 0.04f, 0.08f), Chrome);
            CoinDoor(p, new Vector3(0f, 0.72f, -0.725f), new Vector2(0.32f, 0.2f), new Color(1f, 0.25f, 0.2f));
            BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Plunger", p, new Vector3(0.3f, 0.92f, -0.76f),
                new Vector3(90f, 0f, 0f), new Vector3(0.025f, 0.06f, 0.025f), Chrome, false);
            BuildKit.SpawnLocal(PrimitiveType.Sphere, "PlungerKnob", p, new Vector3(0.3f, 0.92f, -0.83f),
                Vector3.zero, Vector3.one * 0.05f, Plain(new Color(0.85f, 0.1f, 0.15f)), false);
            UnderGlow(p, new Vector3(0f, 0f, 0f), new Vector2(0.9f, 1.6f), Violet);

            BuildKit.SpawnLocal(PrimitiveType.Quad, "Playfield", p, new Vector3(0f, 0.995f, 0f),
                new Vector3(85f, 0f, 0f), new Vector3(0.66f, 1.32f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, Violet, 0.6f)), false);
            foreach (var bump in new[] { new Vector3(-0.14f, 0f, 0.3f), new Vector3(0.14f, 0f, 0.3f), new Vector3(0f, 0f, 0.45f) })
            {
                Vector3 b = new Vector3(bump.x, Field(bump.z), bump.z);
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "BumperBase", p, b + new Vector3(0f, 0.015f, 0f), Vector3.zero,
                    new Vector3(0.12f, 0.015f, 0.12f), Plain(Color.white), false);
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Bumper", p, b + new Vector3(0f, 0.045f, 0f), Vector3.zero,
                    new Vector3(0.09f, 0.02f, 0.09f), Glow(Magenta, 1.6f), false);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                BlockKit.MeshPart("Flipper", p, BlockKit.CubeMesh, new Vector3(s * 0.09f, Field(-0.5f) + 0.02f, -0.5f),
                    new Vector3(-5f, -s * 25f, 0f), Glow(Lemon, 1.2f), scale: new Vector3(0.14f, 0.025f, 0.03f));
                BlockKit.MeshPart("Slingshot", p, BlockKit.CubeMesh, new Vector3(s * 0.19f, Field(-0.33f) + 0.02f, -0.33f),
                    new Vector3(-5f, s * 20f, 0f), Glow(Teal, 1.4f), scale: new Vector3(0.025f, 0.035f, 0.2f));
                BlockKit.MeshPart("InlaneGuide", p, BlockKit.CubeMesh, new Vector3(s * 0.27f, Field(-0.42f) + 0.02f, -0.42f),
                    new Vector3(-5f, s * 10f, 0f), Chrome, scale: new Vector3(0.012f, 0.03f, 0.22f));
            }
            var inserts = new List<CombineInstance>();
            for (int k = 0; k < 5; k++)
            {
                float z = -0.18f + k * 0.08f;
                inserts.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, Field(z) + 0.003f, z), new Vector3(-5f, 45f, 0f), new Vector3(0.035f, 0.004f, 0.035f)));
                foreach (float s in new[] { -1f, 1f })
                    inserts.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(s * (0.1f + k * 0.03f), Field(z) + 0.003f, z), new Vector3(-5f, 45f, 0f), new Vector3(0.025f, 0.004f, 0.025f)));
            }
            BlockKit.Batch("Inserts", p, Glow(Lemon, 1.4f), inserts);
            Pane(p, "PlayfieldGlass", new Vector3(0f, Field(0f) + 0.07f, 0f), 0f, new Vector2(0.7f, 1.38f), 85f);

            // Backbox on a neck: the station screen, framed with light, and a
            // topper with the game's name.
            BlockKit.Block("Neck", p, new Vector3(0f, 0f, 0.66f), Vector3.zero, 0.9f, 1.05f,
                new Vector2(0.3f, 0.08f), new Vector2(0.3f, 0.08f), 0.01f, CabinetTrim);
            BlockKit.Block("Backbox", p, new Vector3(0f, 0f, 0.66f), Vector3.zero, 1.0f, 1.95f,
                new Vector2(0.4f, 0.09f), new Vector2(0.4f, 0.09f), 0.02f, CabinetBody);
            foreach (float sx in new[] { -0.405f, 0.405f })
                BlockKit.Box("BackboxTrim", p, new Vector3(sx, 1.475f, 0.58f), new Vector3(0.012f, 0.92f, 0.02f), Glow(Teal, 1.4f));
            BlockKit.Box("ScreenSurround", p, new Vector3(0f, 1.52f, 0.568f), new Vector3(0.76f, 0.7f, 0.004f), Glow(Violet, 0.8f));
            BuildKit.SpawnLocal(PrimitiveType.Quad, "MonitorScreen", p, new Vector3(0f, 1.52f, 0.565f), Vector3.zero,
                new Vector3(0.72f, 0.66f, 1f), BuildKit.MakeHologram(Color.Lerp(Color.black, Teal, 0.55f)), false);
            BlockKit.Slab("Topper", p, new Vector3(0f, 2.08f, 0.66f), new Vector3(0.82f, 0.26f, 0.16f), 0.02f, CabinetBody);
            Marquee(p, new Vector3(0f, 2.08f, 0.57f), new Vector2(0.74f, 0.18f), "ROOT ACCESS", Magenta, 1.05f);

            // High-score board on a stand beside the table.
            var board = new GameObject("HighScoreBoard").transform;
            board.SetParent(station, false);
            board.localPosition = new Vector3(1.55f, 0f, 0.55f);
            BlockKit.Block("StandFoot", board, Vector3.zero, Vector3.zero, 0f, 0.04f,
                new Vector2(0.25f, 0.2f), new Vector2(0.24f, 0.19f), 0.01f, Chrome);
            BlockKit.Block("Stand", board, Vector3.zero, Vector3.zero, 0f, 1.1f,
                new Vector2(0.06f, 0.06f), new Vector2(0.05f, 0.05f), 0.01f, Chrome);
            BlockKit.Slab("Panel", board, new Vector3(0f, 1.6f, 0f), new Vector3(1.0f, 1.0f, 0.08f), 0.02f, DarkGlass);
            BlockKit.Box("PanelGlow", board, new Vector3(0f, 2.1f, -0.045f), new Vector3(0.95f, 0.025f, 0.01f), Glow(Teal, 1.6f));
            BlockKit.Box("PanelGlowLow", board, new Vector3(0f, 1.1f, -0.045f), new Vector3(0.95f, 0.025f, 0.01f), Glow(Teal, 1.6f));
            MakeText(board, "HighScore_Text", new Vector3(0f, 1.6f, -0.05f),
                "HIGH SCORES\n1  ACE   98,410\n2  JLEE  87,200\n3  MO    64,050\n4  KAI   51,990",
                Teal, 0.9f, 0.9f, 0.9f);

            return new Screen { center = p.localPosition + new Vector3(0f, 1.52f, 0.55f), pitch = 0f, size = new Vector2(0.72f, 0.66f) };
        }
    }
}
