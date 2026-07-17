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
            TuneAtmosphere();
            BuildFloor();
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

        /// <summary>What makes procedural rooms look fake is flat, even light
        /// and uniform glow. The hub instead gets a warm key light against a
        /// cooler, darker ambient (classic warm/cool contrast), so surfaces
        /// shade differently depending on orientation.</summary>
        private static void TuneAtmosphere()
        {
            RenderSettings.ambientLight = new Color(0.10f, 0.11f, 0.15f);
            var sun = GameObject.Find("Directional Light");
            if (sun != null)
            {
                var dl = sun.GetComponent<Light>();
                if (dl != null)
                {
                    dl.color = new Color(1.00f, 0.91f, 0.78f); // warm key
                    dl.intensity = 0.55f;
                    sun.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
                }
            }
        }

        /// <summary>The hub floor is tuned quieter than the levels': larger,
        /// dimmer grid cells and a glossier surface, so it reads as polished
        /// lobby stone with inlays rather than a glowing tron grid.</summary>
        private static void BuildFloor()
        {
            var mat = BuildKit.MakeGridFloor(HubGold * 0.9f, new Color(0.055f, 0.052f, 0.048f));
            // No-ops harmlessly if the custom shader fell back to Standard.
            mat.SetFloat("_GridScale", 8f);
            mat.SetFloat("_LineWidth", 0.018f);
            mat.SetFloat("_MinorEmission", 0.12f);
            mat.SetFloat("_Emission", 1.0f);
            mat.SetFloat("_PulseStrength", 0.22f);
            mat.SetFloat("_Smoothness", 0.93f);
            var floor = BuildKit.Spawn(PrimitiveType.Plane, "Floor", null,
                Vector3.zero, new Vector3(4f, 1f, 4f), mat, collider: true);
            floor.isStatic = true;

            // Baseboard skirting where walls meet the floor — small physical
            // detail that procedural rooms always lack.
            var baseMat = BuildKit.MakeStandard(new Color(0.05f, 0.055f, 0.075f), 0.6f, 0.5f);
            BuildKit.Spawn(PrimitiveType.Cube, "Base_N", null, new Vector3(0, 0.175f, 19.4f), new Vector3(39.2f, 0.35f, 0.16f), baseMat, false).isStatic = true;
            BuildKit.Spawn(PrimitiveType.Cube, "Base_S", null, new Vector3(0, 0.175f, -19.4f), new Vector3(39.2f, 0.35f, 0.16f), baseMat, false).isStatic = true;
            BuildKit.Spawn(PrimitiveType.Cube, "Base_E", null, new Vector3(19.4f, 0.175f, 0), new Vector3(0.16f, 0.35f, 39.2f), baseMat, false).isStatic = true;
            BuildKit.Spawn(PrimitiveType.Cube, "Base_W", null, new Vector3(-19.4f, 0.175f, 0), new Vector3(0.16f, 0.35f, 39.2f), baseMat, false).isStatic = true;
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
                // pads, guide dashes, and lighting all extend that way.
                Quaternion facing = Quaternion.Euler(0f, d.rotY, 0f);
                Vector3 inward = facing * Vector3.back;

                var pad = BuildKit.Spawn(PrimitiveType.Cylinder, "LandingPad_" + d.level, wayfinding.transform,
                    d.pos + inward * 1.8f + Vector3.up * 0.025f, new Vector3(2.2f, 0.012f, 2.2f),
                    BuildKit.MakeHologram(d.accent), collider: false);
                pad.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 8f, 0f);

                // Dashed guide path (airport-style wayfinding) — dashes read as
                // designed signage where a solid 8m glow bar reads as a laser.
                var dashMat = BuildKit.MakeEmissive(d.accent, 1.5f);
                for (int k = 0; k < 5; k++)
                {
                    var dash = BuildKit.Spawn(PrimitiveType.Cube, $"GuideDash_{d.level}_{k}", wayfinding.transform,
                        d.pos + inward * (3.4f + k * 1.75f) + Vector3.up * 0.015f,
                        new Vector3(0.18f, 0.03f, 1.1f), dashMat, collider: false);
                    dash.transform.rotation = facing;
                }

                // Recessed threshold + dark backing panel give the doorway
                // physical depth instead of a glowing sticker on a flat wall.
                var plateMat = BuildKit.MakeStandard(new Color(0.05f, 0.055f, 0.075f), 0.65f, 0.6f);
                var plate = BuildKit.Spawn(PrimitiveType.Cube, "Threshold_" + d.level, wayfinding.transform,
                    d.pos + inward * 0.45f + Vector3.up * 0.01f, new Vector3(3.2f, 0.02f, 0.9f), plateMat, collider: false);
                plate.transform.rotation = facing;

                // 0.14 tucks the quad just behind the baseboard face (0.32 from
                // the wall centre-line) so the two never render coplanar.
                var backing = BuildKit.Spawn(PrimitiveType.Quad, "DoorBacking_" + d.level, wayfinding.transform,
                    d.pos - inward * 0.14f + Vector3.up * 2f, new Vector3(3.0f, 4.05f, 1f),
                    BuildKit.MakeStandard(new Color(0.02f, 0.025f, 0.04f), 0.2f, 0.1f), collider: false);
                backing.transform.rotation = facing;

                // A downward spot pool at each door: pools of light with dark
                // gaps between them model how real lobbies are actually lit.
                var glow = new GameObject("DoorLight_" + d.level);
                glow.transform.SetParent(wayfinding.transform, false);
                glow.transform.position = d.pos + inward * 1.6f + Vector3.up * 4.4f;
                glow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var l = glow.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = 62f;
                l.color = Color.Lerp(d.accent, Color.white, 0.35f);
                l.range = 9f;
                l.intensity = 2.6f;
            }
        }

        /// <summary>Everything that makes the hub read as an atrium/lobby:
        /// central dais + ceiling halo, lounge seating, plants framing the
        /// doors, SJSU banners on the (otherwise bare) north wall, drones.</summary>
        public static void BuildAtrium()
        {
            var root = new GameObject("Furnishings");

            // Two-step dais under the holo core, with a slow gold halo on the
            // ceiling above it — the room's vertical anchor.
            BuildKit.Spawn(PrimitiveType.Cylinder, "DaisStep", root.transform,
                new Vector3(0f, 0.012f, 6f), new Vector3(8.6f, 0.012f, 8.6f),
                BuildKit.MakeStandard(new Color(0.08f, 0.085f, 0.11f), 0.6f, 0.35f), collider: false);
            BuildKit.Spawn(PrimitiveType.Cylinder, "Dais", root.transform,
                new Vector3(0f, 0.028f, 6f), new Vector3(7f, 0.02f, 7f),
                BuildKit.MakeStandard(new Color(0.05f, 0.06f, 0.09f), 0.78f, 0.5f), collider: false);
            // Segmented ceiling ring (a solid hologram cylinder here rendered
            // as a giant glowing disc — very much the thing to avoid).
            var halo = new GameObject("CeilingHalo");
            halo.transform.SetParent(root.transform, false);
            halo.transform.position = new Vector3(0f, 4.8f, 6f);
            var haloMat = BuildKit.MakeEmissive(HubGold, 1.4f);
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                var seg = BuildKit.Spawn(PrimitiveType.Cube, "HaloSeg_" + i, halo.transform,
                    halo.transform.position + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 4f),
                    new Vector3(0.6f, 0.05f, 0.16f), haloMat, collider: false);
                seg.transform.rotation = Quaternion.Euler(0f, a, 0f);
            }
            halo.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 6f, 0f);

            // Lounges sit in slightly-rotated groups: nothing in a real room is
            // perfectly axis-aligned, and the few degrees of tilt do more to
            // break the "generated" look than any material could.
            var loungeNw = Pivot(root.transform, "Lounge_NW", new Vector3(-12f, 0f, 14f), 7f);
            PropFactory.BuildRug(loungeNw, Vector3.zero, new Vector3(4.8f, 0.02f, 3.6f));
            PropFactory.BuildCouch(loungeNw, new Vector3(0f, 0f, 1.3f), 180f);
            PropFactory.BuildCouch(loungeNw, new Vector3(-2.4f, 0f, -0.6f), 90f);
            PropFactory.BuildCoffeeTable(loungeNw, new Vector3(0.4f, 0f, -0.5f));
            PropFactory.BuildPlant(loungeNw, new Vector3(2.5f, 0f, 2.5f));

            var seatSe = Pivot(root.transform, "Seat_SE", new Vector3(10f, 0f, -14f), -5f);
            PropFactory.BuildRug(seatSe, Vector3.zero, new Vector3(3.6f, 0.02f, 3.0f));
            PropFactory.BuildCouch(seatSe, new Vector3(0f, 0f, -1.3f), 0f);
            PropFactory.BuildPlant(seatSe, new Vector3(2.4f, 0f, -1.8f));

            // Plants framing the door bays (between the pads, against the walls).
            PropFactory.BuildPlant(root.transform, new Vector3(18.3f, 0f, 8f));
            PropFactory.BuildPlant(root.transform, new Vector3(18.3f, 0f, 0f));
            PropFactory.BuildPlant(root.transform, new Vector3(18.3f, 0f, -8f));
            PropFactory.BuildPlant(root.transform, new Vector3(-18.3f, 0f, -8f));
            PropFactory.BuildPlant(root.transform, new Vector3(-18.3f, 0f, -16f));

            // SJSU banners along the north wall so it isn't a bare slab. Framed
            // and only faintly emissive — they should look LIT, not glowing.
            var frameMat = BuildKit.MakeStandard(new Color(0.06f, 0.065f, 0.09f), 0.5f, 0.55f);
            for (int i = 0; i < 4; i++)
            {
                float x = -12f + i * 8f;
                Color c = i % 2 == 0 ? SjsuBlue : HubGold;
                BuildKit.Spawn(PrimitiveType.Cube, "BannerFrame_" + i, root.transform,
                    new Vector3(x, 3.1f, 19.38f), new Vector3(1.6f, 2.6f, 0.06f), frameMat, collider: false);
                BuildKit.Spawn(PrimitiveType.Cube, "Banner_" + i, root.transform,
                    new Vector3(x, 3.1f, 19.3f), new Vector3(1.4f, 2.4f, 0.08f),
                    BuildKit.MakeEmissive(c, 0.35f), collider: false);
            }

            // Wall TVs with live tickers + charts (west wall pair, one by the
            // spawn on the south wall) so the walls carry motion and lore.
            string[] newsA =
            {
                "SOC STATUS: ALL CLEAR",
                "MFA BLOCKS 99% OF ATTACKS",
                "PHISHING DRILL THURSDAY",
                "PATCH COMPLIANCE: 96%",
            };
            string[] newsB =
            {
                "LOCK YOUR WORKSTATION",
                "REPORT SUSPICIOUS EMAILS",
                "VERIFY, THEN TRUST",
                "BACKUPS TESTED WEEKLY",
            };
            PropFactory.BuildWallTV(root.transform, new Vector3(-19.41f, 2.9f, 12f), -90f, BuildKit.AccentCyan, newsA);
            PropFactory.BuildWallTV(root.transform, new Vector3(-19.41f, 2.9f, 4f), -90f, HubGold, newsB, 1);
            PropFactory.BuildWallTV(root.transform, new Vector3(4f, 2.9f, -19.41f), 180f, BuildKit.AccentCyan, newsA, 2);

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

        /// <summary>Rotated group parent, so a furniture cluster can be laid
        /// out in easy local coordinates and tilted a few degrees as one.</summary>
        private static Transform Pivot(Transform parent, string name, Vector3 pos, float rotY)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            return go.transform;
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
