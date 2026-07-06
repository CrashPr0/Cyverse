using UnityEngine;
using Cyverse.Audio;
using Cyverse.Interaction;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Shared construction for Level 0, used by both <see cref="Level0Bootstrap"/>
    /// (runtime) and the editor menu (Level0SceneBuilder) so the procedural scene
    /// and a hand-tweakable editor-built scene are identical. Pure GameObject
    /// creation — no play-mode-only calls — so it runs in edit mode too.
    /// </summary>
    public static class SceneFactory
    {
        public static readonly Color AccentCyan = new Color(0.25f, 0.80f, 1.00f);
        public static readonly Color PanelWhite = new Color(0.90f, 0.95f, 1.00f);
        public static readonly Color WallColor = new Color(0.10f, 0.12f, 0.16f);
        public static readonly Color IamColor = new Color(0.25f, 0.65f, 1.00f);
        public static readonly Color CiaColor = new Color(0.30f, 1.00f, 0.65f);
        public static readonly Color NiceColor = new Color(1.00f, 0.72f, 0.25f);

        /// <summary>Build the entire level (environment + player + systems + stations + scanner).</summary>
        public static void BuildAll()
        {
            BuildLighting();
            BuildFloor();
            BuildWalls();
            BuildCeilingPanels();
            BuildWallDetail();
            BuildNeonTrim();
            BuildCenterpiece();
            BuildScanner();
            PropFactory.BuildFurnishings();
            Interaction.GuardNPC.Build(new Vector3(2.2f, 0f, -5.5f), 180f);
            BuildPlayer();
            BuildSystems();
            BuildStations();
        }

        // ---- Environment ----------------------------------------------------

        public static void BuildLighting()
        {
            if (Object.FindObjectOfType<Light>() == null)
            {
                var go = new GameObject("Directional Light");
                var light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(0.70f, 0.80f, 1.00f);
                light.intensity = 0.8f;
                light.shadows = LightShadows.Soft;
                go.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.16f, 0.22f);
        }

        public static void BuildFloor()
        {
            var floor = Spawn(PrimitiveType.Plane, "Floor", null,
                Vector3.zero, new Vector3(4f, 1f, 4f), MakeGridFloor(), collider: true);
            floor.isStatic = true;
        }

        public static void BuildWalls()
        {
            var mat = MakeStandard(WallColor, 0.45f, 0.25f);
            CreateWall("Wall_North", new Vector3(0, 2.5f, 20), new Vector3(40, 5, 1), mat);
            CreateWall("Wall_South", new Vector3(0, 2.5f, -20), new Vector3(40, 5, 1), mat);
            CreateWall("Wall_East", new Vector3(20, 2.5f, 0), new Vector3(1, 5, 40), mat);
            CreateWall("Wall_West", new Vector3(-20, 2.5f, 0), new Vector3(1, 5, 40), mat);
        }

        private static void CreateWall(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var wall = Spawn(PrimitiveType.Cube, name, null, pos, scale, mat, collider: true);
            wall.isStatic = true;
        }

        public static void BuildCeilingPanels()
        {
            // Solid dark ceiling slab so the room reads as an interior (and the
            // sky never shows), with recessed light bars just below it.
            var slabMat = MakeStandard(new Color(0.06f, 0.07f, 0.10f), 0.3f, 0.1f);
            var slab = Spawn(PrimitiveType.Cube, "CeilingSlab", null,
                new Vector3(0, 5.15f, 0), new Vector3(40f, 0.3f, 40f), slabMat, collider: false);
            slab.isStatic = true;

            var mat = MakeEmissive(PanelWhite, 1.6f);
            for (int z = -14; z <= 14; z += 7)
            {
                Spawn(PrimitiveType.Cube, "CeilingPanel_" + z, null,
                    new Vector3(0, 4.92f, z), new Vector3(34f, 0.12f, 0.9f), mat, collider: false);
            }
        }

        public static void BuildWallDetail()
        {
            // Mid-height accent strip on each wall.
            var stripMat = MakeEmissive(new Color(0.55f, 0.80f, 1f), 1.2f);
            Spawn(PrimitiveType.Cube, "Strip_N", null, new Vector3(0, 3.4f, 19.45f), new Vector3(39f, 0.08f, 0.08f), stripMat, false);
            Spawn(PrimitiveType.Cube, "Strip_S", null, new Vector3(0, 3.4f, -19.45f), new Vector3(39f, 0.08f, 0.08f), stripMat, false);
            Spawn(PrimitiveType.Cube, "Strip_E", null, new Vector3(19.45f, 3.4f, 0), new Vector3(0.08f, 0.08f, 39f), stripMat, false);
            Spawn(PrimitiveType.Cube, "Strip_W", null, new Vector3(-19.45f, 3.4f, 0), new Vector3(0.08f, 0.08f, 39f), stripMat, false);

            // Structural columns break up the flat walls.
            var colMat = MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.55f, 0.35f);
            for (int i = -16; i <= 16; i += 8)
            {
                Spawn(PrimitiveType.Cube, "Column_N" + i, null, new Vector3(i, 2.5f, 19.2f), new Vector3(0.5f, 5f, 0.5f), colMat, true).isStatic = true;
                Spawn(PrimitiveType.Cube, "Column_S" + i, null, new Vector3(i, 2.5f, -19.2f), new Vector3(0.5f, 5f, 0.5f), colMat, true).isStatic = true;
                Spawn(PrimitiveType.Cube, "Column_E" + i, null, new Vector3(19.2f, 2.5f, i), new Vector3(0.5f, 5f, 0.5f), colMat, true).isStatic = true;
                Spawn(PrimitiveType.Cube, "Column_W" + i, null, new Vector3(-19.2f, 2.5f, i), new Vector3(0.5f, 5f, 0.5f), colMat, true).isStatic = true;
            }
        }

        public static void BuildNeonTrim()
        {
            var mat = MakeEmissive(AccentCyan, 2.5f);
            Spawn(PrimitiveType.Cube, "Trim_N", null, new Vector3(0, 0.1f, 19.6f), new Vector3(39.4f, 0.12f, 0.12f), mat, false);
            Spawn(PrimitiveType.Cube, "Trim_S", null, new Vector3(0, 0.1f, -19.6f), new Vector3(39.4f, 0.12f, 0.12f), mat, false);
            Spawn(PrimitiveType.Cube, "Trim_E", null, new Vector3(19.6f, 0.1f, 0f), new Vector3(0.12f, 0.12f, 39.4f), mat, false);
            Spawn(PrimitiveType.Cube, "Trim_W", null, new Vector3(-19.6f, 0.1f, 0f), new Vector3(0.12f, 0.12f, 39.4f), mat, false);
        }

        public static void BuildCenterpiece()
        {
            var core = Spawn(PrimitiveType.Cylinder, "HoloCore", null,
                new Vector3(0, 2.6f, 13f), new Vector3(1.6f, 2.2f, 1.6f), MakeHologram(AccentCyan), collider: false);
            core.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 18f, 0f);

            // Counter-rotating base ring grounds the core visually.
            var ring = Spawn(PrimitiveType.Cylinder, "HoloCoreRing", null,
                new Vector3(0, 0.06f, 13f), new Vector3(3.6f, 0.03f, 3.6f), MakeHologram(AccentCyan), collider: false);
            ring.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, -12f, 0f);

            MakeSign(null, new Vector3(0, 4.55f, 13f), "CYVERSE", new Color(0.75f, 0.92f, 1f), 0.10f);

            var glow = new GameObject("HoloCoreLight");
            glow.transform.position = new Vector3(0, 2.6f, 13f);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = AccentCyan;
            l.range = 12f;
            l.intensity = 2.0f;
        }

        // ---- Player & systems ----------------------------------------------

        public static GameObject BuildPlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 2f, -8f);

            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 1f, 0);
            cc.height = 2f;
            cc.radius = 0.4f;

            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.7f, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";

            player.AddComponent<FirstPersonController>();
            player.AddComponent<PlayerInteractor>();
            camGo.AddComponent<FirstPersonHands>();
            return player;
        }

        public static GameObject BuildSystems()
        {
            var sys = new GameObject("GameSystems");
            sys.AddComponent<HudUI>();
            sys.AddComponent<Sfx>();
            sys.AddComponent<ScreenFader>();
            sys.AddComponent<ControlsOverlay>();
            sys.AddComponent<Dialogue.DialogueManager>();
            sys.AddComponent<Quiz.QuizSystem>();
            sys.AddComponent<ResultsScreen>();
            sys.AddComponent<GlossaryPanel>();
            sys.AddComponent<MainMenu>();
            sys.AddComponent<Audio.AmbientHum>();
            sys.AddComponent<Settings.AccessibilitySettings>();
            sys.AddComponent<Level0Manager>();
            return sys;
        }

        // ---- Security scanner -------------------------------------------------

        public static GameObject BuildScanner()
        {
            var root = new GameObject("SecurityScanner");
            var basePos = new Vector3(8f, 0f, 15f);
            root.transform.position = basePos;

            Spawn(PrimitiveType.Cube, "ScannerPedestal", root.transform,
                basePos + new Vector3(0, 0.75f, 0), new Vector3(1.4f, 1.5f, 0.8f),
                MakeStandard(new Color(0.12f, 0.14f, 0.18f), 0.6f, 0.4f), collider: true);

            var panel = Spawn(PrimitiveType.Quad, "ScannerPanel", root.transform,
                basePos + new Vector3(0, 2.3f, 0), new Vector3(1.2f, 1.6f, 1f),
                MakeHologram(AccentCyan), collider: false);
            panel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var glow = new GameObject("ScannerLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.position = basePos + new Vector3(0, 2.2f, 0);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = AccentCyan;
            l.range = 8f;
            l.intensity = 0.8f; // dim until Level0Manager activates it

            MakeSign(root.transform, basePos + new Vector3(0, 3.6f, 0), "SECURITY SCANNER", AccentCyan);
            AddPanelLabel(root.transform, basePos + new Vector3(0, 2.3f, -0.025f), "SCAN");

            var scanner = root.AddComponent<Interaction.FaceScanner>();
            scanner.scanLight = l;
            return root;
        }

        // ---- Signage ----------------------------------------------------------

        public static GameObject MakeSign(Transform parent, Vector3 worldPos, string text, Color color,
            float characterSize = 0.045f)
        {
            var go = new GameObject("Sign_" + text.Replace(' ', '_'));
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = worldPos;

            var font = HudUI.LoadFont();
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = characterSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            // TextMesh renders with its font's material, which must be set explicitly.
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            go.AddComponent<Billboard>();
            go.AddComponent<SignFX>();

            // Holo chrome: a glowing underline and a soft glow halo behind the
            // glyphs, so signs read as projected holograms rather than bare text.
            float h = characterSize * tm.fontSize * 0.1f;            // approx glyph height
            float w = Mathf.Max(0.5f, text.Length * h * 0.62f);       // approx text width

            var underline = GameObject.CreatePrimitive(PrimitiveType.Cube);
            underline.name = "Underline";
            StripCollider(underline);
            underline.transform.SetParent(go.transform, false);
            underline.transform.localPosition = new Vector3(0f, -h * 0.75f, 0.005f);
            underline.transform.localScale = new Vector3(w, h * 0.06f, 0.01f);
            underline.GetComponent<Renderer>().sharedMaterial = MakeEmissive(color, 2f);

            Shader glowShader = Shader.Find("Cyverse/GlowSprite");
            if (glowShader != null)
            {
                var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                halo.name = "Halo";
                StripCollider(halo);
                halo.transform.SetParent(go.transform, false);
                halo.transform.localPosition = new Vector3(0f, 0f, 0.03f);
                halo.transform.localScale = new Vector3(w * 1.5f, h * 2.6f, 1f);
                var haloMat = new Material(glowShader);
                Color hc = color;
                hc.a = 0.30f;
                haloMat.SetColor("_Color", hc);
                haloMat.SetFloat("_Intensity", 0.55f);
                halo.GetComponent<Renderer>().sharedMaterial = haloMat;
            }

            return go;
        }

        /// <summary>Remove a primitive's collider safely in both play and edit mode.</summary>
        private static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Object.Destroy(c);
            else Object.DestroyImmediate(c);
        }

        private static string SignTitleFor(StationSetup.Topic topic)
        {
            switch (topic)
            {
                case StationSetup.Topic.CIA: return "CIA TRIAD";
                case StationSetup.Topic.NICE: return "NICE ROLES";
                default: return "I/AM KIOSK";
            }
        }

        private static string GlyphFor(StationSetup.Topic topic)
        {
            switch (topic)
            {
                case StationSetup.Topic.CIA: return "CIA";
                case StationSetup.Topic.NICE: return "NICE";
                default: return "I/AM";
            }
        }

        /// <summary>Static label floating just in front of a holo panel (on the
        /// approach side), so panels read as "displays" instead of blank glow.</summary>
        private static void AddPanelLabel(Transform parent, Vector3 worldPos, string text)
        {
            var go = new GameObject("PanelLabel");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.identity; // readable from the south approach

            var font = HudUI.LoadFont();
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.045f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 1f, 1f, 0.92f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        // ---- Stations -------------------------------------------------------

        public static void BuildStations()
        {
            CreateStation(StationSetup.Topic.IAM, "Inspect the I/AM Kiosk",
                new Vector3(-5, 0, 3), IamColor);
            CreateStation(StationSetup.Topic.CIA, "Inspect the CIA Triad Hologram",
                new Vector3(0, 0, 7), CiaColor);
            CreateStation(StationSetup.Topic.NICE, "Inspect the NICE Roles Board",
                new Vector3(5, 0, 3), NiceColor);
        }

        public static GameObject CreateStation(StationSetup.Topic topic, string prompt, Vector3 basePos, Color color)
        {
            var root = new GameObject("Station_" + topic.ToString().ToLower());
            root.transform.position = basePos;

            Spawn(PrimitiveType.Cube, "Desk", root.transform,
                basePos + new Vector3(0, 0.55f, 0), new Vector3(1.6f, 1.1f, 1.0f),
                MakeStandard(new Color(0.12f, 0.14f, 0.18f), 0.6f, 0.4f), collider: true);

            var holo = Spawn(PrimitiveType.Quad, "Hologram", root.transform,
                basePos + new Vector3(0, 1.9f, 0), new Vector3(1.4f, 1.0f, 1f),
                MakeHologram(color), collider: false);
            holo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var glow = new GameObject("StationLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.position = basePos + new Vector3(0, 1.8f, 0);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = 7f;
            l.intensity = 2.5f;

            MakeSign(root.transform, basePos + new Vector3(0, 3.3f, 0), SignTitleFor(topic), color);
            AddPanelLabel(root.transform, basePos + new Vector3(0, 1.9f, -0.025f), GlyphFor(topic));

            // Slow-turning holo ring marks the interaction zone on the floor.
            var ring = Spawn(PrimitiveType.Cylinder, "StationRing", root.transform,
                basePos + new Vector3(0, 0.05f, 0), new Vector3(2.4f, 0.02f, 2.4f),
                MakeHologram(color), collider: false);
            ring.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 10f, 0f);

            var mark = BuildCheckmark(root.transform, basePos + new Vector3(0, 2.7f, 0));
            mark.SetActive(false);

            var station = root.AddComponent<InteractableStation>();
            var setup = root.AddComponent<StationSetup>();
            setup.topic = topic;
            setup.prompt = prompt;
            setup.reviewedMark = mark;
            setup.stationLight = l;
            return root;
        }

        public static GameObject BuildCheckmark(Transform parent, Vector3 worldPos)
        {
            var mark = new GameObject("ReviewedMark");
            mark.transform.SetParent(parent, false);
            mark.transform.position = worldPos;
            mark.transform.localRotation = Quaternion.identity;

            var mat = MakeEmissive(new Color(0.30f, 1f, 0.45f), 3f);
            CreateTickBar(mark.transform, new Vector3(-0.14f, -0.02f, 0f), 45f, new Vector3(0.30f, 0.10f, 0.10f), mat);
            CreateTickBar(mark.transform, new Vector3(0.10f, 0.10f, 0f), -45f, new Vector3(0.60f, 0.10f, 0.10f), mat);
            return mark;
        }

        private static void CreateTickBar(Transform parent, Vector3 localPos, float zRot, Vector3 scale, Material mat)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "TickBar";
            var col = bar.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            bar.GetComponent<Renderer>().sharedMaterial = mat;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPos;
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, zRot);
            bar.transform.localScale = scale;
        }

        // ---- Helpers --------------------------------------------------------

        public static GameObject Spawn(PrimitiveType type, string name, Transform parent,
            Vector3 pos, Vector3 scale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            return go;
        }

        public static Material MakeStandard(Color c, float smoothness, float metallic)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = c;
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static Material MakeEmissive(Color c, float strength)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = c;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * strength);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        public static Material MakeHologram(Color c)
        {
            Shader s = Shader.Find("Cyverse/Hologram");
            if (s != null)
            {
                var m = new Material(s);
                m.SetColor("_Color", c);
                return m;
            }
            return MakeEmissive(c, 2.5f);
        }

        public static Material MakeGridFloor()
        {
            Shader s = Shader.Find("Cyverse/GridFloor");
            if (s != null)
            {
                var m = new Material(s);
                m.SetColor("_LineColor", AccentCyan);
                m.SetColor("_BaseColor", new Color(0.05f, 0.06f, 0.09f));
                return m;
            }
            return MakeStandard(new Color(0.08f, 0.09f, 0.12f), 0.8f, 0.3f);
        }
    }
}
