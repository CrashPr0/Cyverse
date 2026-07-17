using UnityEngine;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 1 — I/AM, in the new two-room level template:
    ///   south half = VIDEO ROOM: spawn (entered from the Hub), a briefing
    ///     screen (scrubbable/repeatable), and a locked door in the divider
    ///   north half = TASK ROOM: four I/AM stations (Identification,
    ///     Authentication, Authorization, Accountability) and the exit door
    ///     back to the Hub, which unlocks when the task is complete.
    /// Built on BuildKit like every level. Positions dodge the shared
    /// furnishing set (work pods z≈-3.5, server wall z=18.5, plants ±8,8,
    /// columns every ±8 along walls).
    /// </summary>
    public static class Level1IamSceneFactory
    {
        public static readonly Color IamBlue = new Color(0.25f, 0.65f, 1f);

        public static void BuildAll()
        {
            BuildKit.BuildLighting();
            BuildKit.BuildFloor(IamBlue, new Color(0.05f, 0.06f, 0.09f));
            BuildKit.BuildWalls(BuildKit.WallColor);
            BuildKit.BuildCeilingPanels();
            BuildKit.BuildWallDetail(IamBlue);
            BuildKit.BuildNeonTrim(IamBlue);
            PropFactory.BuildFurnishings();
            BuildDivider();
            BuildVideoRoom();
            BuildTaskRoom();
            var player = BuildKit.BuildPlayer();
            player.transform.position = new Vector3(0f, 2f, -16f); // enter from the Hub side
            BuildSystems();
        }

        public static GameObject BuildSystems()
        {
            var sys = BuildKit.BuildCommonSystems();
            sys.AddComponent<Level1IamManager>();
            return sys;
        }

        /// <summary>Wall splitting video room (south) from task room (north),
        /// with a 3.2m doorway gap at x=0 for the locked door.</summary>
        public static void BuildDivider()
        {
            var mat = BuildKit.MakeStandard(BuildKit.WallColor, 0.45f, 0.25f);
            BuildKit.Spawn(PrimitiveType.Cube, "Divider_W", null,
                new Vector3(-10.8f, 2.5f, 2f), new Vector3(18.4f, 5f, 0.6f), mat, collider: true).isStatic = true;
            BuildKit.Spawn(PrimitiveType.Cube, "Divider_E", null,
                new Vector3(10.8f, 2.5f, 2f), new Vector3(18.4f, 5f, 0.6f), mat, collider: true).isStatic = true;
        }

        public static void BuildVideoRoom()
        {
            // Briefing screen mid-room, facing the spawn (its screen faces -Z).
            VideoStation.Build(new Vector3(0f, 0f, -6f), 0f,
                Level1IamContent.BriefingSlides(), IamBlue);

            LockedDoor.Build(new Vector3(0f, 0f, 2f), 0f, 3f,
                "TASK ROOM", "Complete the security briefing to unlock this door.", IamBlue);
        }

        /// <summary>The gamified task room: four hands-on tasks (one per "A")
        /// plus the Certification Exam. Every task except enrollment gates on
        /// the badge — identification comes first, enforced by the game rules.
        /// Positions dodge the shared furnishings: server racks x −6.5..−2.9
        /// and 13.6/14.8 at z=18.5, plants (±8,8), wall columns every ±8.</summary>
        public static void BuildTaskRoom()
        {
            System.Func<bool> badgeGate = () => BadgeStation.EnrolledInScene;
            const string gateMsg = "BADGE REQUIRED — enroll at the ID kiosk first.";

            // Task 1 — IDENTIFICATION: first thing seen through the divider door.
            BadgeStation.Build(new Vector3(-4.5f, 0f, 6f), 0f, IamBlue);

            // Task 2 — AUTHENTICATION: vault on the west wall; its token
            // deliberately charges on the far (east) side of the room.
            MfaGauntlet.Build(
                vaultPos: new Vector3(-16.5f, 0f, 12f), vaultRotY: -90f,
                terminalPos: new Vector3(-13f, 0f, 8f),
                padPos: new Vector3(-13f, 0f, 15f),
                tokenRackPos: new Vector3(8f, 0f, 5f),
                slotPos: new Vector3(-15.2f, 0f, 9.2f),
                accent: IamBlue,
                passcode: Level1IamContent.DailyPasscode,
                gate: badgeGate, gateMessage: gateMsg);

            // Task 3 — AUTHORIZATION: intake table + role pedestals, east side.
            SortingStation.Build(new Vector3(13f, 0f, 8f),
                Level1IamContent.SortingCrates(),
                new[]
                {
                    ("INTERN", new Vector3(10.5f, 0f, 14f)),
                    ("HR MANAGER", new Vector3(13.5f, 0f, 15.5f)),
                    ("SYSADMIN", new Vector3(16.5f, 0f, 14f)),
                },
                IamBlue, badgeGate, gateMsg);

            // Task 4 — ACCOUNTABILITY: audit log board, north-west.
            AuditStation.Build(new Vector3(-9.5f, 0f, 17.6f), 0f,
                Level1IamContent.AuditRounds(), IamBlue, badgeGate, gateMsg);

            // Boss check — activated by the manager once all tasks are done.
            CertExamStation.Build(new Vector3(0f, 0f, 16f), 0f,
                Level1IamContent.ExamQuestions(), IamBlue);

            // Exit back to the Hub — manager unlocks it when the level is done.
            // x=4 clears the wall column at x=0 and the server racks further west.
            HubDoor.Build(new Vector3(4f, 0f, 19.2f), 0f, "Return to Hub",
                "Hub", 0, new Color(0.90f, 0.66f, 0.14f), HubDoor.Mode.Manual);
        }
    }
}
