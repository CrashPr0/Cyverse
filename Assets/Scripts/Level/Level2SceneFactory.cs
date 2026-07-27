using UnityEngine;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 2 — Cyber Defense, rebuilt on the two-room template with hands-on
    /// tasks instead of review stations:
    ///   south = BRIEFING ROOM (spawn, scrubbable briefing, locked divider)
    ///   north = SOC FLOOR: the SIEM alert desk, the EDR endpoint row, the
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
            SiemConsole.Build(new Vector3(-13f, 0f, 7f), 60f, Level2Content.Alerts(), SocRed);

            // Task 2 — EDR: a row of workstations along the east side.
            var fleetGo = new GameObject("EdrFleet");
            fleetGo.transform.position = new Vector3(14f, 0f, 10f);
            var fleet = fleetGo.AddComponent<EdrFleet>();
            var defs = Level2Content.Endpoints();
            for (int i = 0; i < defs.Length; i++)
            {
                var e = EndpointStation.Build(
                    new Vector3(14f, 0f, 4f + i * 2.6f), -90f, defs[i], fleet, SocRed);
                fleet.Register(e);
            }
            BuildKit.MakeSign(fleetGo.transform, new Vector3(14f, 3.2f, 10f), "ENDPOINTS", SocRed, 0.032f);

            // Task 3 — INCIDENT RESPONSE: sequence board on the north wall,
            // card rack a few metres south of it.
            PlaybookStation.Build(
                // z=16 keeps the backboard (board z + 0.6) clear of the
                // server-rack wall at z=18.5.
                boardPos: new Vector3(-1f, 0f, 16f),
                rackPos: new Vector3(-1f, 0f, 12f),
                accent: SocRed, gate: null, gateMessage: null);

            // Boss check — the manager activates it once all tasks are done.
            // x=8 clears the rightmost playbook slot (x=4) and the wall column.
            CertExamStation.Build(new Vector3(8f, 0f, 16f), 0f,
                Level2Content.ExamQuestions(), SocRed);

            HubDoor.Build(new Vector3(4f, 0f, 19.2f), 0f, "Return to Hub",
                "Hub", 0, new Color(0.90f, 0.66f, 0.14f), HubDoor.Mode.Manual);
        }
    }
}
