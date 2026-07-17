using UnityEngine;
using Cyverse.Audio;
using Cyverse.Interaction;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Level-agnostic construction helpers shared by every level's scene
    /// factory (SceneFactory for Level 0, Level1SceneFactory for Level 1, and
    /// whatever comes after): the room shell, materials, signage, learning
    /// stations, the player rig, and the common "GameSystems" bundle. Pure
    /// GameObject creation — no play-mode-only calls — so it runs in edit mode
    /// too. Extracted from the original Level-0-only SceneFactory so new
    /// levels build on the same proven foundation instead of re-deriving it.
    /// </summary>
    public static class BuildKit
    {
        public static readonly Color AccentCyan = new Color(0.25f, 0.80f, 1.00f);
        public static readonly Color PanelWhite = new Color(0.90f, 0.95f, 1.00f);
        public static readonly Color WallColor = new Color(0.10f, 0.12f, 0.16f);

        // ---- Room shell (shared 40x40 footprint across levels) --------------

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

        public static void BuildFloor(Color lineColor, Color baseColor)
        {
            var floor = Spawn(PrimitiveType.Plane, "Floor", null,
                Vector3.zero, new Vector3(4f, 1f, 4f), MakeGridFloor(lineColor, baseColor), collider: true);
            floor.isStatic = true;
        }

        public static void BuildWalls(Color wallColor)
        {
            var mat = MakeStandard(wallColor, 0.45f, 0.25f);
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

        public static void BuildWallDetail(Color stripColor)
        {
            var stripMat = MakeEmissive(stripColor, 1.2f);
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

        public static void BuildNeonTrim(Color color)
        {
            var mat = MakeEmissive(color, 2.5f);
            Spawn(PrimitiveType.Cube, "Trim_N", null, new Vector3(0, 0.1f, 19.6f), new Vector3(39.4f, 0.12f, 0.12f), mat, false);
            Spawn(PrimitiveType.Cube, "Trim_S", null, new Vector3(0, 0.1f, -19.6f), new Vector3(39.4f, 0.12f, 0.12f), mat, false);
            Spawn(PrimitiveType.Cube, "Trim_E", null, new Vector3(19.6f, 0.1f, 0f), new Vector3(0.12f, 0.12f, 39.4f), mat, false);
            Spawn(PrimitiveType.Cube, "Trim_W", null, new Vector3(-19.6f, 0.1f, 0f), new Vector3(0.12f, 0.12f, 39.4f), mat, false);
        }

        /// <summary>A rotating holo "core" centerpiece with a counter-rotating
        /// base ring and a mounted nameplate collar — each level's focal
        /// landmark. The title used to be free-floating billboard text that
        /// clipped through the glowing core; it's now a static, double-sided
        /// plate wrapped around the column, which reads as built signage.</summary>
        public static void BuildCenterpiece(Vector3 pos, string title, Color color)
        {
            var core = Spawn(PrimitiveType.Cylinder, "HoloCore", null,
                pos, new Vector3(1.4f, 1.7f, 1.4f), MakeHologram(color), collider: false);
            core.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 18f, 0f);

            var ring = Spawn(PrimitiveType.Cylinder, "HoloCoreRing", null,
                new Vector3(pos.x, 0.06f, pos.z), new Vector3(3.6f, 0.03f, 3.6f), MakeHologram(color), collider: false);
            ring.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, -12f, 0f);

            BuildNameplate(pos + Vector3.up * 0.9f, title, color);

            var glow = new GameObject("HoloCoreLight");
            glow.transform.position = pos;
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = 11f;
            l.intensity = 1.5f;
        }

        /// <summary>Double-sided mounted nameplate: dark plate, accent trim,
        /// white text on the north and south faces. No Billboard, no bobbing —
        /// mounted signage reads as designed where floating text reads as
        /// placeholder.</summary>
        public static GameObject BuildNameplate(Vector3 pos, string title, Color accent)
        {
            var root = new GameObject("Nameplate_" + title.Replace(' ', '_'));
            root.transform.position = pos;

            Spawn(PrimitiveType.Cube, "Plate", root.transform, pos,
                new Vector3(2.9f, 0.62f, 1.5f),
                MakeStandard(new Color(0.045f, 0.05f, 0.075f), 0.55f, 0.5f), collider: false);

            var trim = MakeEmissive(accent, 1.8f);
            Spawn(PrimitiveType.Cube, "Trim_S", root.transform, pos + new Vector3(0f, -0.27f, -0.76f),
                new Vector3(2.9f, 0.05f, 0.02f), trim, false);
            Spawn(PrimitiveType.Cube, "Trim_N", root.transform, pos + new Vector3(0f, -0.27f, 0.76f),
                new Vector3(2.9f, 0.05f, 0.02f), trim, false);

            NameplateFace(root.transform, pos + new Vector3(0f, 0.02f, -0.77f), 0f, title);
            NameplateFace(root.transform, pos + new Vector3(0f, 0.02f, 0.77f), 180f, title);
            return root;
        }

        private static void NameplateFace(Transform parent, Vector3 worldPos, float rotY, string title)
        {
            var go = new GameObject("PlateText");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f); // readable from local -Z

            var font = HudUI.LoadFont();
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = title;
            tm.fontSize = 64;
            tm.characterSize = 0.045f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.96f, 0.98f, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        // ---- Player & shared systems ------------------------------------------

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

        /// <summary>Builds the shared "GameSystems" object (HUD, audio, dialogue,
        /// quiz, results, glossary, title menu, settings) WITHOUT a level
        /// manager — each level adds its own manager component after this.</summary>
        public static GameObject BuildCommonSystems()
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
            return sys;
        }

        // ---- Learning stations -------------------------------------------------

        /// <summary>Generic learning station: desk, hologram panel, light, title
        /// sign, topic glyph, floor ring, and a hidden reviewed-checkmark, wired
        /// to a StationSetup for the given topic. Shared by every level. The
        /// three optional delegates let a level supply its own dialogue, quiz
        /// question, and completion callback; omit them (as Level 0 does) to
        /// use the original Level 0 content/quiz/manager fallback.</summary>
        public static GameObject CreateStation(StationSetup.Topic topic, string prompt,
            Vector3 basePos, Color color, string title, string glyph,
            System.Func<System.Collections.Generic.List<Dialogue.DialogueLine>> contentProvider = null,
            System.Func<Quiz.QuizQuestion> quizProvider = null,
            System.Action onReviewed = null)
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

            MakeSign(root.transform, basePos + new Vector3(0, 3.3f, 0), title, color);
            AddPanelLabel(root.transform, basePos + new Vector3(0, 1.9f, -0.025f), glyph);

            var ring = Spawn(PrimitiveType.Cylinder, "StationRing", root.transform,
                basePos + new Vector3(0, 0.05f, 0), new Vector3(2.4f, 0.02f, 2.4f),
                MakeHologram(color), collider: false);
            ring.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 10f, 0f);

            var mark = BuildCheckmark(root.transform, basePos + new Vector3(0, 2.7f, 0));
            mark.SetActive(false);

            root.AddComponent<InteractableStation>();
            var setup = root.AddComponent<StationSetup>();
            setup.topic = topic;
            setup.prompt = prompt;
            setup.reviewedMark = mark;
            setup.stationLight = l;
            setup.contentProvider = contentProvider;
            setup.quizProvider = quizProvider;
            setup.onReviewed = onReviewed;
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
            StripCollider(bar);
            bar.GetComponent<Renderer>().sharedMaterial = mat;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPos;
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, zRot);
            bar.transform.localScale = scale;
        }

        // ---- Signage ------------------------------------------------------------

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

        /// <summary>Static label floating just in front of a holo panel (on the
        /// approach side), so panels read as "displays" instead of blank glow.</summary>
        public static void AddPanelLabel(Transform parent, Vector3 worldPos, string text)
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

        /// <summary>Spawn a primitive positioned in its parent's LOCAL space —
        /// use this instead of Spawn when the parent is rotated.</summary>
        public static GameObject SpawnLocal(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) StripCollider(go);
            return go;
        }

        /// <summary>Small plain TextMesh label (no halo/underline chrome —
        /// see MakeSign for full signage). Local-space; identity rotation makes
        /// it readable from the parent's -Z side.</summary>
        public static TextMesh MakeLabel(Transform parent, Vector3 localPos, string text,
            Color color, float characterSize, bool billboard = false,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var go = new GameObject("Label_" + text.Replace(' ', '_'));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;

            var font = HudUI.LoadFont();
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = characterSize;
            tm.fontStyle = style;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left
                        : anchor == TextAnchor.MiddleRight ? TextAlignment.Right
                        : TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            if (billboard) go.AddComponent<Billboard>();
            return tm;
        }

        /// <summary>Remove a primitive's collider safely in both play and edit mode.</summary>
        public static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Object.Destroy(c);
            else Object.DestroyImmediate(c);
        }

        // ---- Materials ----------------------------------------------------------

        public static GameObject Spawn(PrimitiveType type, string name, Transform parent,
            Vector3 pos, Vector3 scale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) StripCollider(go);
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

        public static Material MakeGridFloor(Color lineColor, Color baseColor)
        {
            Shader s = Shader.Find("Cyverse/GridFloor");
            if (s != null)
            {
                var m = new Material(s);
                m.SetColor("_LineColor", lineColor);
                m.SetColor("_BaseColor", baseColor);
                return m;
            }
            return MakeStandard(baseColor, 0.8f, 0.3f);
        }
    }
}
