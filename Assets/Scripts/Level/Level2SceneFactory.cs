using UnityEngine;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 2 — Cyber Defense, rebuilt on the two-room template with hands-on
    /// tasks instead of review stations:
    ///   south = BRIEFING ROOM (spawn, scrubbable briefing, locked divider)
    ///   north = SOC FLOOR: the Alert Board, WS-01..WS-04 verification desks,
    ///           incident-response playbook board, and the certification exam.
    /// Palette is SOC alert-red so it reads apart from I/AM's blue.
    /// </summary>
    public static class Level2SceneFactory
    {
        public static readonly Color SocRed = new Color(0.95f, 0.42f, 0.32f);

        public static void BuildAll()
        {
            BuildKit.BuildLighting();
            BuildKit.BuildFloor(SocRed, new Color(0.07f, 0.05f, 0.05f));
            BuildKit.BuildWalls(BuildKit.WallColor);
            BuildKit.BuildCeilingPanels();
            BuildKit.BuildWallDetail(SocRed);
            BuildKit.BuildNeonTrim(SocRed);
            PropFactory.BuildFurnishings();
            BuildDivider();
            BuildBriefingRoom();
            BuildSocFloor();
            var player = BuildKit.BuildPlayer();
            player.transform.position = new Vector3(0f, 2f, -16f);
            BuildSystems();
        }

        public static GameObject BuildSystems()
        {
            var sys = BuildKit.BuildCommonSystems();
            sys.AddComponent<Level2Manager>();
            return sys;
        }

        /// <summary>Reinstalls runtime-only task content and delegates after a
        /// visual-pass scene is deserialized.</summary>
        public static void WireTaskRoom()
        {
            var siem = Object.FindObjectOfType<SiemConsole>();
            if (siem != null) siem.Configure(Level2Content.SocScenarios());

            // Visual-pass scenes may still contain the original five EDR
            // desks. Reuse the first four as WS-01..WS-04 and hide the spare.
            var endpoints = Object.FindObjectsOfType<EndpointStation>();
            System.Array.Sort(endpoints, (a, b) =>
                a.transform.position.z.CompareTo(b.transform.position.z));
            var definitions = Level2Content.Endpoints();
            for (int i = 0; i < endpoints.Length; i++)
            {
                if (i < definitions.Length)
                {
                    // Keep verification near the Alert Board without putting
                    // a desk barricade across the center of the SOC floor.
                    endpoints[i].transform.SetPositionAndRotation(
                        new Vector3(-17f, 0f, 4.5f + i * 3f),
                        Quaternion.Euler(0f, -90f, 0f));
                    endpoints[i].ConfigureSoc(definitions[i], siem);
                }
                else endpoints[i].gameObject.SetActive(false);
            }
            if (siem != null) siem.Configure(Level2Content.SocScenarios());

            var fleet = Object.FindObjectOfType<EdrFleet>();
            if (fleet != null) fleet.enabled = false;

            var playbook = Object.FindObjectOfType<PlaybookStation>();
            if (playbook != null)
            {
                // Keep the entrance sightline open: mount the complete puzzle
                // on the east/right wall and face its readable side west.
                playbook.Place(new Vector3(17f, 0f, 12f), 90f);
                playbook.Configure();
            }

            var exam = Object.FindObjectOfType<CertExamStation>();
            if (exam != null) exam.Configure(Level2Content.ExamQuestions());

            GameObject systems = GameObject.Find("GameSystems");
            if (systems == null) systems = new GameObject("Level2RuntimePolish");
            Level2SocPolish.Ensure(systems);
        }

        public static void BuildDivider()
        {
            var mat = BuildKit.MakeStandard(BuildKit.WallColor, 0.45f, 0.25f);
            BuildKit.Spawn(PrimitiveType.Cube, "Divider_W", null,
                new Vector3(-10.8f, 2.5f, 2f), new Vector3(18.4f, 5f, 0.6f), mat, collider: true).isStatic = true;
            BuildKit.Spawn(PrimitiveType.Cube, "Divider_E", null,
                new Vector3(10.8f, 2.5f, 2f), new Vector3(18.4f, 5f, 0.6f), mat, collider: true).isStatic = true;
        }

        public static void BuildBriefingRoom()
        {
            VideoStation.Build(new Vector3(0f, 0f, -6f), 0f,
                Level2Content.BriefingSlides(), SocRed);

            LockedDoor.Build(new Vector3(0f, 0f, 2f), 0f, 3f,
                "SOC FLOOR", "Watch the defense briefing to unlock this door.", SocRed);

            // Exit in the room the player spawns in (the task room has its own).
            var spawnExit = HubDoor.Build(new Vector3(4f, 0f, -19.2f), 180f, "Return to Hub",
                "Hub", 0, new Color(0.90f, 0.66f, 0.14f), HubDoor.Mode.Manual);
            spawnExit.SetUnlocked(true);
        }

        /// <summary>Task room. Positions dodge the shared furnishings: server
        /// racks x −6.5..−2.9 and 13.6/14.8 at z=18.5, plants (±8,8), wall
        /// columns every ±8.</summary>
        public static void BuildSocFloor()
        {
            // Task 1 — SIEM: the alert desk faces you as you come through.
            var siem = SiemConsole.Build(new Vector3(-13f, 0f, 7f), 60f, Level2Content.SocScenarios(), SocRed);

            // Task 2 — verification workstations along the west wall beside
            // the Alert Board. The center remains a clear route to the rest
            // of the SOC floor.
            var fleetGo = new GameObject("SocWorkstations");
            fleetGo.transform.position = new Vector3(-17f, 0f, 9f);
            var defs = Level2Content.Endpoints();
            for (int i = 0; i < defs.Length; i++)
            {
                var e = EndpointStation.Build(
                    new Vector3(-17f, 0f, 4.5f + i * 3f), -90f, defs[i], null, SocRed);
                e.ConfigureSoc(defs[i], siem);
            }
            BuildKit.MakeSign(fleetGo.transform, new Vector3(-17f, 3.2f, 9f), "SOC WORKSTATIONS", SocRed, 0.032f);

            // Task 3 — INCIDENT RESPONSE: sequence board on the east/right
            // wall. Its readable side faces west into the SOC, leaving the
            // entrance's direct sightline open.
            PlaybookStation.Build(
                boardPos: new Vector3(17f, 0f, 12f),
                rackPos: new Vector3(12f, 0f, 12f),
                accent: SocRed, gate: null, gateMessage: null,
                yawDegrees: 90f);

            // Boss check — the manager activates it once all tasks are done.
            // The certification station remains on the north wall, separated
            // from the playbook rack's west edge.
            CertExamStation.Build(new Vector3(8f, 0f, 16f), 0f,
                Level2Content.ExamQuestions(), SocRed);

            HubDoor.Build(new Vector3(4f, 0f, 19.2f), 0f, "Return to Hub",
                "Hub", 0, new Color(0.90f, 0.66f, 0.14f), HubDoor.Mode.Manual);
        }
    }
}
