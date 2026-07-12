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

        public static void BuildTaskRoom()
        {
            BuildKit.CreateStation(StationSetup.Topic.IAM, "Inspect: Identification",
                new Vector3(-8f, 0f, 11f), IamBlue, "IDENTIFICATION", "ID",
                Level1IamContent.Identification, Level1IamContent.IdentificationQuiz, Notify);
            BuildKit.CreateStation(StationSetup.Topic.IAM, "Inspect: Authentication",
                new Vector3(-3f, 0f, 14.5f), IamBlue, "AUTHENTICATION", "AUTH",
                Level1IamContent.Authentication, Level1IamContent.AuthenticationQuiz, Notify);
            BuildKit.CreateStation(StationSetup.Topic.IAM, "Inspect: Authorization",
                new Vector3(3f, 0f, 14.5f), IamBlue, "AUTHORIZATION", "AUTHZ",
                Level1IamContent.Authorization, Level1IamContent.AuthorizationQuiz, Notify);
            BuildKit.CreateStation(StationSetup.Topic.IAM, "Inspect: Accountability",
                new Vector3(8f, 0f, 11f), IamBlue, "ACCOUNTABILITY", "ACCT",
                Level1IamContent.Accountability, Level1IamContent.AccountabilityQuiz, Notify);

            // Exit back to the Hub — manager unlocks it when the task is done.
            // x=4 clears the wall column at x=0 and the server racks further west.
            HubDoor.Build(new Vector3(4f, 0f, 19.2f), 0f, "Return to Hub",
                "Hub", 0, new Color(0.90f, 0.66f, 0.14f), HubDoor.Mode.Manual);
        }

        private static void Notify()
        {
            if (Level1IamManager.Instance != null) Level1IamManager.Instance.NotifyStationReviewed();
        }
    }
}
