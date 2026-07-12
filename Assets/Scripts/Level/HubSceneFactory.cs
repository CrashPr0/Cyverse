using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Dialogue;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// The Main Hub: the room every level is entered from. Four level doorways
    /// along the north wall (Level 1 I/AM playable; Level 2 Cyber Defense
    /// unlocks after it; Levels 3-4 in development) plus an Orientation door
    /// for the original Level 0 demo. Built on BuildKit like every level;
    /// palette is a warm gold "atrium" so the hub reads differently from the
    /// levels themselves.
    /// </summary>
    public static class HubSceneFactory
    {
        public static readonly Color HubGold = new Color(0.90f, 0.66f, 0.14f);

        public static void BuildAll()
        {
            BuildKit.BuildLighting();
            BuildKit.BuildFloor(HubGold, new Color(0.07f, 0.06f, 0.05f));
            BuildKit.BuildWalls(BuildKit.WallColor);
            BuildKit.BuildCeilingPanels();
            BuildKit.BuildWallDetail(HubGold);
            BuildKit.BuildNeonTrim(HubGold);
            BuildKit.BuildCenterpiece(new Vector3(0, 2.6f, 6f), "CYVERSE HUB", HubGold);
            PropFactory.BuildFurnishings();
            BuildDoors();
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
            // Story doors along the EAST wall (the north wall hosts the shared
            // server racks, and wall columns sit at every ±8 — these z slots
            // are chosen to clear both). rotY=90 points each door's readable
            // face west, into the room.
            HubDoor.Build(new Vector3(19.2f, 0f, 12f), 90f, "Level 1 — I/AM",
                "Level1_IAM", 1, new Color(0.25f, 0.65f, 1f));
            HubDoor.Build(new Vector3(19.2f, 0f, 4f), 90f, "Level 2 — Cyber Defense",
                "Level1", 2, new Color(0.95f, 0.35f, 0.25f));
            HubDoor.Build(new Vector3(19.2f, 0f, -4f), 90f, "Level 3 — Digital Forensics",
                "", 3, new Color(0.75f, 0.35f, 1f));
            HubDoor.Build(new Vector3(19.2f, 0f, -12f), 90f, "Level 4 — Cyber Attack",
                "", 4, new Color(1f, 0.60f, 0.15f));

            // Orientation demo on the west wall (facing east, into the room).
            HubDoor.Build(new Vector3(-19.2f, 0f, -12f), -90f, "Orientation (Demo)",
                "Level0", 0, new Color(0.30f, 1f, 0.65f));
        }

        private static List<DialogueLine> ConciergeLines()
        {
            int done = LevelProgress.CompletedStoryLevels();
            var lines = new List<DialogueLine>();
            if (done == 0)
                lines.Add(new DialogueLine("Concierge",
                    $"Welcome to the CyVerse Hub, {PlayerIdentity.Callsign}. Start with Level 1 — I/AM, the blue door on the east wall. Each level: watch the briefing, then complete the task.", null, 5f));
            else
                lines.Add(new DialogueLine("Concierge",
                    $"Good to see you, {PlayerIdentity.Callsign} — {done} level{(done == 1 ? "" : "s")} complete. Finished levels stay open if you want a better score.", null, 4.5f));
            lines.Add(new DialogueLine("Concierge",
                "The Orientation door on the west wall replays the original onboarding demo any time.", null, 3.5f));
            return lines;
        }
    }
}
