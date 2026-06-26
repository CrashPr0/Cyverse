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
    /// Assembles a runnable Level 0 at Play time — lighting, room, player,
    /// HUD/managers, and the three learning stations — so the team has a working
    /// vertical slice without hand-wiring a scene. Everything here is intended
    /// to be replaced piece by piece with real art and a hand-built scene as
    /// the project grows (see SETUP.md). The only thing the scene needs is one
    /// GameObject carrying this component.
    /// </summary>
    public class Level0Bootstrap : MonoBehaviour
    {
        void Awake()
        {
            GameState.Reset();

            EnsureLighting();
            BuildRoom();
            BuildPlayer();
            BuildSystems();   // HUD + Dialogue + Settings + Level0Manager
            BuildStations();

            Level0Manager.Instance.Begin();
        }

        private void EnsureLighting()
        {
            if (FindObjectOfType<Light>() == null)
            {
                var go = new GameObject("Directional Light");
                var light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(0.95f, 0.97f, 1f);
                light.intensity = 1.1f;
                go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.45f);
        }

        private void BuildRoom()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(4f, 1f, 4f); // ~40 x 40 m
            Tint(floor, new Color(0.18f, 0.20f, 0.25f));

            CreateWall("Wall_North", new Vector3(0, 2.5f, 20), new Vector3(40, 5, 1));
            CreateWall("Wall_South", new Vector3(0, 2.5f, -20), new Vector3(40, 5, 1));
            CreateWall("Wall_East", new Vector3(20, 2.5f, 0), new Vector3(1, 5, 40));
            CreateWall("Wall_West", new Vector3(-20, 2.5f, 0), new Vector3(1, 5, 40));
        }

        private void CreateWall(string name, Vector3 pos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            Tint(wall, new Color(0.25f, 0.28f, 0.34f));
        }

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
            camGo.AddComponent<Camera>();
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

        private void BuildStations()
        {
            CreateStation("iam", "Inspect the I/AM Kiosk",
                new Vector3(-5, 1, 3), new Color(0.20f, 0.60f, 1f), Level0Content.IAM());

            CreateStation("cia", "Inspect the CIA Triad Hologram",
                new Vector3(0, 1.2f, 7), new Color(0.20f, 1f, 0.60f), Level0Content.CIA());

            CreateStation("nice", "Inspect the NICE Roles Board",
                new Vector3(5, 1, 3), new Color(1f, 0.70f, 0.20f), Level0Content.Nice());
        }

        private void CreateStation(string id, string prompt, Vector3 pos, Color color, List<DialogueLine> lines)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Station_" + id;
            cube.transform.position = pos;
            cube.transform.localScale = new Vector3(1.2f, 2f, 1.2f);
            Tint(cube, color, emissive: true);

            var station = cube.AddComponent<InteractableStation>();
            station.Configure(id, prompt, lines);
            Level0Manager.Instance.Register(station);
        }

        private static void Tint(GameObject go, Color color, bool emissive = false)
        {
            var mat = go.GetComponent<Renderer>().material;
            mat.color = color;
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.5f);
            }
        }
    }
}
