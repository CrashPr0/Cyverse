using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Dialogue;
using Cyverse.Interaction;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// The Main Hub: the room every level is entered from. Four level doorways
    /// along the east wall (Level 1 I/AM playable; Level 2 Cyber Defense
    /// unlocks after it; Levels 3-4 in development) plus an Orientation door
    /// on the west wall for the original Level 0 demo. Built on BuildKit like
    /// every level, but furnished as an ATRIUM, not an office: a central dais
    /// under the holo core, lounge seating, SJSU banners, a live mission-status
    /// board by the spawn, and colour-coded wayfinding paths on the floor
    /// leading to each door. Palette is a warm gold so the hub reads
    /// differently from the levels themselves.
    /// </summary>
    public static class HubSceneFactory
    {
        public static readonly Color HubGold = new Color(0.90f, 0.66f, 0.14f);
        public static readonly Color SjsuBlue = new Color(0.00f, 0.33f, 0.64f);

        // One source of truth for the level doors; wayfinding and the mission
        // board are generated from this same table so they can't drift apart.
        private struct DoorSpec
        {
            public string name; public string scene; public int level;
            public Color accent; public Vector3 pos; public float rotY;
            public DoorSpec(string name, string scene, int level, Color accent, Vector3 pos, float rotY)
            { this.name = name; this.scene = scene; this.level = level; this.accent = accent; this.pos = pos; this.rotY = rotY; }
        }

        private static DoorSpec[] Doors() => new[]
        {
            // Story doors along the EAST wall (the z slots clear the wall
            // columns at every ±8). rotY=90 points each door's readable face
            // west, into the room.
            new DoorSpec("Level 1 — I/AM", "Level1_IAM", 1, new Color(0.25f, 0.65f, 1f), new Vector3(19.2f, 0f, 12f), 90f),
            new DoorSpec("Level 2 — Cyber Defense", "Level1", 2, new Color(0.95f, 0.35f, 0.25f), new Vector3(19.2f, 0f, 4f), 90f),
            new DoorSpec("Level 3 — Digital Forensics", "", 3, new Color(0.75f, 0.35f, 1f), new Vector3(19.2f, 0f, -4f), 90f),
            new DoorSpec("Level 4 — Cyber Attack", "", 4, new Color(1f, 0.60f, 0.15f), new Vector3(19.2f, 0f, -12f), 90f),
            // Orientation demo on the west wall (facing east, into the room).
            new DoorSpec("Orientation (Demo)", "Level0", 0, new Color(0.30f, 1f, 0.65f), new Vector3(-19.2f, 0f, -12f), -90f),
        };

        public static void BuildAll()
        {
            BuildKit.BuildLighting();
            BuildKit.BuildFloor(HubGold, new Color(0.07f, 0.06f, 0.05f));
            BuildKit.BuildWalls(BuildKit.WallColor);
            BuildKit.BuildCeilingPanels();
            BuildKit.BuildWallDetail(HubGold);
            BuildKit.BuildNeonTrim(HubGold);
            BuildKit.BuildCenterpiece(new Vector3(0, 2.6f, 6f), "CYVERSE HUB", HubGold);
            BuildAtrium();
            BuildDoors();
            BuildMissionBoard();
            GuardNPC.Build(new Vector3(2.2f, 0f, -5.5f), 180f,
                displayName: "Concierge", signText: "INFORMATION", linesProvider: ConciergeLines);
            BuildKit.BuildPlayer();
            BuildSystems();
        }

        public static GameObject BuildSystems()
        {
            var sys = BuildKit.BuildCommonSystems();
            sys.AddComponent<HubManager>();
            return sys;
        }

        public static void BuildDoors()
        {
            var wayfinding = new GameObject("Wayfinding");
            foreach (var d in Doors())
            {
                HubDoor.Build(d.pos, d.rotY, d.name, d.scene, d.level, d.accent);

                // The door's local -Z points into the room (see HubDoor.Build);
                // pads, guide strips, and accent light all extend that way.
                Vector3 inward = Quaternion.Euler(0f, d.rotY, 0f) * Vector3.back;

                var pad = BuildKit.Spawn(PrimitiveType.Cylinder, "LandingPad_" + d.level, wayfinding.transform,
                    d.pos + inward * 1.8f + Vector3.up * 0.025f, new Vector3(2.2f, 0.012f, 2.2f),
                    BuildKit.MakeHologram(d.accent), collider: false);
                pad.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 8f, 0f);

                var strip = BuildKit.Spawn(PrimitiveType.Cube, "GuideStrip_" + d.level, wayfinding.transform,
                    d.pos + inward * 7.2f + Vector3.up * 0.015f, new Vector3(0.18f, 0.03f, 8f),
                    BuildKit.MakeEmissive(d.accent, 1.4f), collider: false);
                strip.transform.rotation = Quaternion.Euler(0f, d.rotY, 0f);

                var glow = new GameObject("DoorLight_" + d.level);
                glow.transform.SetParent(wayfinding.transform, false);
                glow.transform.position = d.pos + inward * 0.8f + Vector3.up * 3.6f;
                var l = glow.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = d.accent;
                l.range = 7f;
                l.intensity = 1.6f;
            }
        }

        /// <summary>Everything that makes the hub read as an atrium/lobby:
        /// central dais + ceiling halo, lounge seating, plants framing the
        /// doors, SJSU banners on the (otherwise bare) north wall, drones.</summary>
        public static void BuildAtrium()
        {
            var root = new GameObject("Furnishings");

            // Dais under the holo core, with a slow gold halo on the ceiling
            // above it — the room's vertical anchor.
            BuildKit.Spawn(PrimitiveType.Cylinder, "Dais", root.transform,
                new Vector3(0f, 0.02f, 6f), new Vector3(7f, 0.02f, 7f),
                BuildKit.MakeStandard(new Color(0.05f, 0.06f, 0.09f), 0.75f, 0.45f), collider: false);
            var halo = BuildKit.Spawn(PrimitiveType.Cylinder, "CeilingHalo", root.transform,
                new Vector3(0f, 4.8f, 6f), new Vector3(8f, 0.015f, 8f),
                BuildKit.MakeHologram(HubGold), collider: false);
            halo.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 6f, 0f);

            // Lounge, north-west (clear of the west door at z=-12).
            PropFactory.BuildRug(root.transform, new Vector3(-12f, 0f, 14f), new Vector3(4.8f, 0.02f, 3.6f));
            PropFactory.BuildCouch(root.transform, new Vector3(-12f, 0f, 15.3f), 180f);
            PropFactory.BuildCouch(root.transform, new Vector3(-14.4f, 0f, 13.4f), 90f);
            PropFactory.BuildCoffeeTable(root.transform, new Vector3(-11.6f, 0f, 13.5f));
            PropFactory.BuildPlant(root.transform, new Vector3(-9.5f, 0f, 16.5f));

            // Smaller seat near the spawn, south-east.
            PropFactory.BuildRug(root.transform, new Vector3(10f, 0f, -14f), new Vector3(3.6f, 0.02f, 3.0f));
            PropFactory.BuildCouch(root.transform, new Vector3(10f, 0f, -15.3f), 0f);
            PropFactory.BuildPlant(root.transform, new Vector3(12.4f, 0f, -15.8f));

            // Plants framing the door bays (between the pads, against the walls).
            PropFactory.BuildPlant(root.transform, new Vector3(18.3f, 0f, 8f));
            PropFactory.BuildPlant(root.transform, new Vector3(18.3f, 0f, 0f));
            PropFactory.BuildPlant(root.transform, new Vector3(18.3f, 0f, -8f));
            PropFactory.BuildPlant(root.transform, new Vector3(-18.3f, 0f, -8f));
            PropFactory.BuildPlant(root.transform, new Vector3(-18.3f, 0f, -16f));

            // SJSU banners along the north wall so it isn't a bare slab.
            for (int i = 0; i < 4; i++)
            {
                float x = -12f + i * 8f;
                Color c = i % 2 == 0 ? SjsuBlue : HubGold;
                BuildKit.Spawn(PrimitiveType.Cube, "Banner_" + i, root.transform,
                    new Vector3(x, 3.1f, 19.3f), new Vector3(1.4f, 2.4f, 0.08f),
                    BuildKit.MakeEmissive(c, 0.9f), collider: false);
            }

            // Ambient drones.
            PropFactory.BuildDrone(root.transform, new Vector3(-10f, 3.6f, 8f));
            PropFactory.BuildDrone(root.transform, new Vector3(10f, 3.8f, -6f));
        }

        /// <summary>Free-standing status board angled toward the spawn point:
        /// one row per door, refreshed live by the MissionBoard component.</summary>
        public static void BuildMissionBoard()
        {
            var root = new GameObject("MissionBoard");
            // Player spawns at (0,-8) looking north; the board sits ahead-left,
            // rotated so its readable face (local -Z) points at the spawn.
            root.transform.position = new Vector3(-4.5f, 0f, -3.5f);
            root.transform.rotation = Quaternion.Euler(0f, -45f, 0f);

            var frameMat = BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.55f, 0.35f);
            Post(root.transform, new Vector3(-1.7f, 1.7f, 0.06f), frameMat);
            Post(root.transform, new Vector3(1.7f, 1.7f, 0.06f), frameMat);

            // BuildKit.Spawn parents with worldPositionStays, so localPosition
            // AND localRotation must be set explicitly to follow the rotated root.
            var backing = BuildKit.Spawn(PrimitiveType.Cube, "Panel", root.transform, Vector3.zero,
                new Vector3(3.6f, 2.8f, 0.08f), frameMat, collider: true);
            backing.transform.localPosition = new Vector3(0f, 2.05f, 0.06f);
            backing.transform.localRotation = Quaternion.identity;

            var surface = BuildKit.Spawn(PrimitiveType.Quad, "Surface", root.transform, Vector3.zero,
                new Vector3(3.45f, 2.65f, 1f), BuildKit.MakeHologram(new Color(0.12f, 0.20f, 0.30f)), collider: false);
            surface.transform.localPosition = new Vector3(0f, 2.05f, 0.0f);
            surface.transform.localRotation = Quaternion.identity;

            var board = root.AddComponent<MissionBoard>();

            MakeBoardText(root.transform, new Vector3(0f, 3.12f, -0.06f), "MISSION STATUS",
                HubGold, 0.032f, TextAnchor.MiddleCenter, FontStyle.Bold);
            board.headerText = MakeBoardText(root.transform, new Vector3(0f, 2.76f, -0.06f), "",
                new Color(0.85f, 0.92f, 1f), 0.020f, TextAnchor.MiddleCenter, FontStyle.Normal);

            var doors = Doors();
            var statuses = new TextMesh[doors.Length];
            var levels = new int[doors.Length];
            var inDev = new bool[doors.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                float y = 2.34f - i * 0.34f;
                MakeBoardText(root.transform, new Vector3(-1.55f, y, -0.06f),
                    doors[i].name.ToUpperInvariant(), doors[i].accent, 0.022f,
                    TextAnchor.MiddleLeft, FontStyle.Normal);
                statuses[i] = MakeBoardText(root.transform, new Vector3(1.55f, y, -0.06f), "",
                    Color.white, 0.022f, TextAnchor.MiddleRight, FontStyle.Bold);
                levels[i] = doors[i].level;
                inDev[i] = string.IsNullOrEmpty(doors[i].scene);
            }
            board.statusTexts = statuses;
            board.levels = levels;
            board.inDevelopment = inDev;

            var glow = new GameObject("BoardLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.2f, -1.6f);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.55f, 0.75f, 1f);
            l.range = 6f;
            l.intensity = 1.4f;
        }

        private static void Post(Transform parent, Vector3 localPos, Material mat)
        {
            var post = BuildKit.Spawn(PrimitiveType.Cube, "Post", parent, Vector3.zero,
                new Vector3(0.12f, 3.4f, 0.12f), mat, collider: true);
            post.transform.localPosition = localPos;
            post.transform.localRotation = Quaternion.identity;
        }

        private static TextMesh MakeBoardText(Transform parent, Vector3 localPos, string text,
            Color color, float characterSize, TextAnchor anchor, FontStyle style)
        {
            var go = new GameObject("BoardText");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity; // readable from local -Z

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
            return tm;
        }

        private static List<DialogueLine> ConciergeLines()
        {
            int done = LevelProgress.CompletedStoryLevels();
            var lines = new List<DialogueLine>();
            if (done == 0)
                lines.Add(new DialogueLine("Concierge",
                    $"Welcome to the CyVerse Hub, {PlayerIdentity.Callsign}. Start with Level 1 — I/AM: follow the blue path to the east wall. Each level: watch the briefing, then complete the task.", null, 5f));
            else
                lines.Add(new DialogueLine("Concierge",
                    $"Good to see you, {PlayerIdentity.Callsign} — {done} level{(done == 1 ? "" : "s")} complete. Finished levels stay open if you want a better score.", null, 4.5f));
            lines.Add(new DialogueLine("Concierge",
                "The mission board behind me tracks your clearance. The Orientation door on the west wall replays the original onboarding demo any time.", null, 4f));
            return lines;
        }
    }
}
