using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Dialogue;
using Cyverse.Interaction;
using Cyverse.Player;
using Cyverse.Settings;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Assembles a runnable, stylised Level 0 at Play time: a dark high-tech
    /// lobby with a glowing grid floor, ceiling light panels, neon trim, a
    /// rotating holographic centerpiece, and three holographic learning
    /// stations (I/AM, CIA Triad, NICE Roles). The look is driven by two custom
    /// shaders (Cyverse/GridFloor and Cyverse/Hologram) plus emissive materials.
    ///
    /// This is a procedural placeholder for the art team's concept (futuristic,
    /// colorful, lived-in working space). Replace primitives with real prefabs
    /// incrementally — see SETUP.md. The scene only needs this one component.
    /// </summary>
    public class Level0Bootstrap : MonoBehaviour
    {
        // ---- Palette --------------------------------------------------------
        private static readonly Color AccentCyan = new Color(0.25f, 0.80f, 1.00f);
        private static readonly Color PanelWhite = new Color(0.90f, 0.95f, 1.00f);
        private static readonly Color WallColor = new Color(0.10f, 0.12f, 0.16f);
        private static readonly Color IamColor = new Color(0.25f, 0.65f, 1.00f);
        private static readonly Color CiaColor = new Color(0.30f, 1.00f, 0.65f);
        private static readonly Color NiceColor = new Color(1.00f, 0.72f, 0.25f);

        void Awake()
        {
            GameState.Reset();

            EnsureLighting();
            BuildFloor();
            BuildWalls();
            BuildCeilingPanels();
            BuildNeonTrim();
            BuildCenterpiece();
            BuildPlayer();
            BuildSystems();   // HUD + Dialogue + Settings + Level0Manager
            BuildStations();

            Level0Manager.Instance.Begin();
        }

        // ---- Lighting -------------------------------------------------------

        private void EnsureLighting()
        {
            if (FindObjectOfType<Light>() == null)
            {
                var go = new GameObject("Directional Light");
                var light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(0.70f, 0.80f, 1.00f); // cool key light
                light.intensity = 0.8f;
                light.shadows = LightShadows.Soft;
                go.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            }
            // Low ambient so the emissive grid, panels and holograms read strongly.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.16f, 0.22f);
        }

        // ---- Environment ----------------------------------------------------

        private void BuildFloor()
        {
            var floor = Spawn(PrimitiveType.Plane, "Floor", null,
                Vector3.zero, new Vector3(4f, 1f, 4f), MakeGridFloor(), collider: true);
            floor.isStatic = true;
        }

        private void BuildWalls()
        {
            var mat = MakeStandard(WallColor, smoothness: 0.45f, metallic: 0.25f);
            CreateWall("Wall_North", new Vector3(0, 2.5f, 20), new Vector3(40, 5, 1), mat);
            CreateWall("Wall_South", new Vector3(0, 2.5f, -20), new Vector3(40, 5, 1), mat);
            CreateWall("Wall_East", new Vector3(20, 2.5f, 0), new Vector3(1, 5, 40), mat);
            CreateWall("Wall_West", new Vector3(-20, 2.5f, 0), new Vector3(1, 5, 40), mat);
        }

        private void CreateWall(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var wall = Spawn(PrimitiveType.Cube, name, null, pos, scale, mat, collider: true);
            wall.isStatic = true;
        }

        private void BuildCeilingPanels()
        {
            // Rows of bright emissive bars, like recessed ceiling lights.
            var mat = MakeEmissive(PanelWhite, 1.6f);
            for (int z = -14; z <= 14; z += 7)
            {
                Spawn(PrimitiveType.Cube, "CeilingPanel_" + z, null,
                    new Vector3(0, 4.92f, z), new Vector3(34f, 0.12f, 0.9f), mat, collider: false);
            }
        }

        private void BuildNeonTrim()
        {
            var mat = MakeEmissive(AccentCyan, 2.5f);
            // Glowing skirting where the walls meet the floor.
            Spawn(PrimitiveType.Cube, "Trim_N", null, new Vector3(0, 0.1f, 19.6f), new Vector3(39.4f, 0.12f, 0.12f), mat, false);
            Spawn(PrimitiveType.Cube, "Trim_S", null, new Vector3(0, 0.1f, -19.6f), new Vector3(39.4f, 0.12f, 0.12f), mat, false);
            Spawn(PrimitiveType.Cube, "Trim_E", null, new Vector3(19.6f, 0.1f, 0f), new Vector3(0.12f, 0.12f, 39.4f), mat, false);
            Spawn(PrimitiveType.Cube, "Trim_W", null, new Vector3(-19.6f, 0.1f, 0f), new Vector3(0.12f, 0.12f, 39.4f), mat, false);
        }

        private void BuildCenterpiece()
        {
            // A tall rotating hologram "server core" as a focal point at the back.
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

        private void BuildPlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 2f, -8f);

            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 1f, 0);
            cc.height = 2f;
            cc.radius = 0.4f;

            // Camera must exist and be tagged before the controllers cache it.
            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.7f, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";

            player.AddComponent<FirstPersonController>();
            player.AddComponent<PlayerInteractor>();
        }

        private void BuildSystems()
        {
            var sys = new GameObject("GameSystems");
            // Order matters: HUD builds the canvas the others draw into.
            sys.AddComponent<HudUI>();
            sys.AddComponent<DialogueManager>();
            sys.AddComponent<AccessibilitySettings>();
            sys.AddComponent<Level0Manager>();
        }

        // ---- Stations -------------------------------------------------------

        private void BuildStations()
        {
            // A gentle arc facing the player as they enter from the south.
            CreateStation("iam", "Inspect the I/AM Kiosk",
                new Vector3(-5, 0, 3), IamColor, Level0Content.IAM());

            CreateStation("cia", "Inspect the CIA Triad Hologram",
                new Vector3(0, 0, 7), CiaColor, Level0Content.CIA());

            CreateStation("nice", "Inspect the NICE Roles Board",
                new Vector3(5, 0, 3), NiceColor, Level0Content.Nice());
        }

        private void CreateStation(string id, string prompt, Vector3 basePos, Color color, List<DialogueLine> lines)
        {
            // Root carries the InteractableStation; the desk child carries the
            // collider the interactor ray hits (GetComponentInParent finds the root).
            var root = new GameObject("Station_" + id);
            root.transform.position = basePos;

            Spawn(PrimitiveType.Cube, "Desk", root.transform,
                basePos + new Vector3(0, 0.55f, 0), new Vector3(1.6f, 1.1f, 1.0f),
                MakeStandard(new Color(0.12f, 0.14f, 0.18f), 0.6f, 0.4f), collider: true);

            var holo = Spawn(PrimitiveType.Quad, "Hologram", root.transform,
                basePos + new Vector3(0, 1.9f, 0), new Vector3(1.4f, 1.0f, 1f),
                MakeHologram(color), collider: false);
            // Flat panel faces the player rather than spinning — a rotating quad
            // would disappear edge-on every 90°. The centerpiece cylinder spins.
            holo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var glow = new GameObject("StationLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.position = basePos + new Vector3(0, 1.8f, 0);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = 7f;
            l.intensity = 2.5f;

            var station = root.AddComponent<InteractableStation>();
            station.Configure(id, prompt, lines);
            Level0Manager.Instance.Register(station);
        }

        // ---- Helpers --------------------------------------------------------

        private static GameObject Spawn(PrimitiveType type, string name, Transform parent,
            Vector3 pos, Vector3 scale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().material = mat;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            return go;
        }

        private static Material MakeStandard(Color c, float smoothness, float metallic)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = c;
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        private static Material MakeEmissive(Color c, float strength)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = c;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * strength);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        private static Material MakeHologram(Color c)
        {
            Shader s = Shader.Find("Cyverse/Hologram");
            if (s != null)
            {
                var m = new Material(s);
                m.SetColor("_Color", c);
                return m;
            }
            return MakeEmissive(c, 2.5f); // fallback if the shader didn't compile
        }

        private static Material MakeGridFloor()
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
