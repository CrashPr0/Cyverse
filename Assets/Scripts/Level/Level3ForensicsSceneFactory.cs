using UnityEngine;
using Cyverse.Forensics;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 3 — Digital Forensics ("Case: Spartan Gold"), the in-engine KC7:
    /// the standard two-room template, but the task room is a SOC floor built
    /// around ONE deep interactable — the forensic query terminal — plus
    /// ambient dressing (wall TVs, evidence pinboard sign, server racks come
    /// from the shared furnishings). Palette: analyst green.
    /// </summary>
    public static class Level3ForensicsSceneFactory
    {
        public static readonly Color ForensicGreen = new Color(0.30f, 1f, 0.55f);

        public static void BuildAll()
        {
            BuildKit.BuildLighting();
            BuildKit.BuildFloor(ForensicGreen, new Color(0.05f, 0.08f, 0.06f));
            BuildKit.BuildWalls(BuildKit.WallColor);
            BuildKit.BuildCeilingPanels();
            BuildKit.BuildWallDetail(ForensicGreen);
            BuildKit.BuildNeonTrim(ForensicGreen);
            PropFactory.BuildFurnishings();
            BuildDivider();
            BuildVideoRoom();
            BuildTaskRoom();
            var player = BuildKit.BuildPlayer();
            player.transform.position = new Vector3(0f, 2f, -16f);
            BuildSystems();
        }

        public static GameObject BuildSystems()
        {
            var sys = BuildKit.BuildCommonSystems();
            sys.AddComponent<QueryTerminal>();
            sys.AddComponent<Level3ForensicsManager>();
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

        public static void BuildVideoRoom()
        {
            VideoStation.Build(new Vector3(0f, 0f, -6f), 0f,
                InvestigationCase.BriefingSlides(), ForensicGreen);

            LockedDoor.Build(new Vector3(0f, 0f, 2f), 0f, 3f,
                "SOC FLOOR", "Watch the analyst briefing to unlock this door.", ForensicGreen);
        }

        public static void BuildTaskRoom()
        {
            // The investigation desk, front and centre.
            ForensicsConsole.Build(new Vector3(0f, 0f, 10f), 0f, ForensicGreen);

            // Case-flavoured newsroom on the walls.
            string[] caseNews =
            {
                "ALERT: PHISHING WAVE HITS CYVERSE",
                "6 LURES SENT · SUBJECT: GOLD REWARD",
                "SOC: FOLLOW THE EVIDENCE",
                "TIP: PIVOT DOMAIN → IP → DOMAIN",
            };
            PropFactory.BuildWallTV(null, new Vector3(-19.41f, 2.9f, 12f), -90f, ForensicGreen, caseNews);
            PropFactory.BuildWallTV(null, new Vector3(19.41f, 2.9f, 12f), 90f, ForensicGreen, caseNews, 2);

            // Exit back to the Hub (always unlocked; completion is separate).
            HubDoor.Build(new Vector3(4f, 0f, 19.2f), 0f, "Return to Hub",
                "Hub", 0, new Color(0.90f, 0.66f, 0.14f), HubDoor.Mode.Manual);
        }
    }
}
