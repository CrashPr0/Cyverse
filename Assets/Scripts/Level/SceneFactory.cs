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

        /// <summary>Build the entire level (environment + player + systems + stations).</summary>
        public static void BuildAll()
        {
            BuildLighting();
            BuildFloor();
            BuildWalls();
            BuildCeilingPanels();
            BuildNeonTrim();
            BuildCenterpiece();
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
            var mat = MakeEmissive(PanelWhite, 1.6f);
            for (int z = -14; z <= 14; z += 7)
            {
                Spawn(PrimitiveType.Cube, "CeilingPanel_" + z, null,
                    new Vector3(0, 4.92f, z), new Vector3(34f, 0.12f, 0.9f), mat, collider: false);
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
            sys.AddComponent<Settings.AccessibilitySettings>();
            sys.AddComponent<Level0Manager>();
            return sys;
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
