using UnityEngine;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Procedural Level 4 scene. The south half is a short rules-of-engagement
    /// briefing; the north half is a four-station cyber-attack simulation.
    /// All displays and targets are generated at runtime, which keeps the
    /// level functional in the WebGL build without scene-bound prefab wiring.
    /// </summary>
    public static class Level4CyberAttackSceneFactory
    {
        public static readonly Color AttackOrange = new Color(1f, 0.42f, 0.25f);
        public static readonly Color AttackRed = new Color(0.76f, 0.12f, 0.14f);
        public const float AttackRoomHeight = 7.2f;

        public static void BuildAll()
        {
            BuildKit.BuildLighting();
            BuildKit.BuildFloor(AttackOrange, new Color(0.08f, 0.035f, 0.04f));
            ToneAttackFloor();
            // Level 4's large task boards need a taller volume than the shared
            // five-metre training shell. Keeping the height local to this scene
            // avoids changing the composition of Levels 1-3.
            BuildKit.BuildWalls(BuildKit.WallColor, AttackRoomHeight);
            BuildKit.BuildCeilingPanels(AttackRoomHeight);
            BuildKit.BuildWallDetail(AttackOrange, AttackRoomHeight);
            BuildKit.BuildNeonTrim(AttackOrange);
            PropFactory.BuildFurnishings();
            BuildDivider();
            BuildBriefingRoom();
            BuildAttackFloor();
            GameObject player = BuildKit.BuildPlayer();
            player.transform.position = new Vector3(0f, 2f, -16f);
            BuildSystems();
        }

        private static void ToneAttackFloor()
        {
            // The shared grid shader defaults are intentionally dramatic for
            // the older showcase rooms. Level 4 needs a quieter floor so the
            // station boards and HUD remain the hierarchy of attention. The
            // previous floor competed with gameplay because it combined the
            // shader's pulse/minor grid with a second set of bright route
            // rails. Keep only a quiet, static grid as environmental texture.
            MeshRenderer floor = GameObject.Find("Floor")?.GetComponent<MeshRenderer>();
            Material material = floor != null ? floor.sharedMaterial : null;
            if (material == null || material.shader == null ||
                material.shader.name != "Cyverse/GridFloor") return;

            material.SetColor("_BaseColor", new Color(0.014f, 0.017f, 0.027f, 1f));
            material.SetColor("_LineColor", new Color(0.34f, 0.075f, 0.045f, 1f));
            material.SetFloat("_GridScale", 7.0f);
            material.SetFloat("_LineWidth", 0.012f);
            material.SetFloat("_MinorEmission", 0.025f);
            material.SetFloat("_Emission", 0.28f);
            material.SetFloat("_PulseStrength", 0f);
            material.SetFloat("_FadeDistance", 24f);
            material.SetFloat("_Smoothness", 0.48f);
            material.SetFloat("_Metallic", 0.10f);
        }

        public static GameObject BuildSystems()
        {
            GameObject systems = BuildKit.BuildCommonSystems();
            systems.AddComponent<Level4CyberAttackManager>();
            return systems;
        }

        public static void BuildDivider()
        {
            Material material = BuildKit.MakeStandard(BuildKit.WallColor, 0.45f, 0.25f);
            BuildKit.Spawn(PrimitiveType.Cube, "Divider_W", null,
                new Vector3(-10.8f, AttackRoomHeight * 0.5f, 2f),
                new Vector3(18.4f, AttackRoomHeight, 0.6f), material, true).isStatic = true;
            BuildKit.Spawn(PrimitiveType.Cube, "Divider_E", null,
                new Vector3(10.8f, AttackRoomHeight * 0.5f, 2f),
                new Vector3(18.4f, AttackRoomHeight, 0.6f), material, true).isStatic = true;
        }

        public static void BuildBriefingRoom()
        {
            VideoStation.Build(new Vector3(0f, 0f, -6f), 0f,
                Level4CyberAttackContent.BriefingSlides(), AttackOrange);

            LockedDoor.Build(new Vector3(0f, 0f, 2f), 0f, 3f,
                "ATTACK SIMULATION", "Watch the rules-of-engagement briefing to unlock the simulation floor.", AttackOrange);

            HubDoor spawnExit = HubDoor.Build(new Vector3(4f, 0f, -19.2f), 180f,
                "Return to Hub", "Hub", 0, new Color(0.90f, 0.66f, 0.14f), HubDoor.Mode.Manual);
            spawnExit.SetUnlocked(true);
            BuildKit.MakeSign(null, new Vector3(-7.5f, 3.4f, -19.25f),
                "LEVEL 4  //  RED TEAM LAB", AttackOrange, 0.032f);
            BuildKit.MakeSign(null, new Vector3(7.5f, 3.4f, -19.25f),
                "SYNTHETIC DATA  ·  NO LIVE SYSTEMS", new Color(0.70f, 0.85f, 0.92f), 0.020f);
        }

        public static void BuildAttackFloor()
        {
            BuildKit.MakeSign(null, new Vector3(0f, 4.15f, 19.20f),
                "BYPASS MFA  ›  ESCALATE PRIVILEGES", new Color(1f, 0.72f, 0.55f), 0.045f);
            BuildKit.MakeSign(null, new Vector3(0f, 3.55f, 19.16f),
                "EXTRACT DATA  ›  COVER TRACKS", new Color(0.72f, 0.86f, 0.92f), 0.026f);

            Level4CyberAttackContent.StationKind[] kinds =
            {
                Level4CyberAttackContent.StationKind.BypassMfa,
                Level4CyberAttackContent.StationKind.EscalatePrivileges,
                Level4CyberAttackContent.StationKind.ExtractData,
                Level4CyberAttackContent.StationKind.CoverTracks,
            };
            Vector3[] positions =
            {
                new Vector3(-10.5f, 0f, 8.0f),
                new Vector3(10.5f, 0f, 8.0f),
                new Vector3(-10.5f, 0f, 14.0f),
                new Vector3(10.5f, 0f, 14.0f),
            };
            Vector3 inwardFocus = new Vector3(0f, 0f, 11f);
            for (int i = 0; i < kinds.Length; i++)
            {
                // Monitor quads and their TMP copy are readable from local -Z,
                // so point the station root's +Z away from the shared centre.
                // Deriving this from position keeps every screen genuinely
                // inward-facing if the layout spacing changes later.
                Vector3 outward = positions[i] - inwardFocus;
                outward.y = 0f;
                float yaw = Quaternion.LookRotation(outward.normalized, Vector3.up).eulerAngles.y;
                CyberAttackStation.Build(positions[i], yaw, kinds[i], i, AttackOrange);
            }

            // The room's physical exit is always available. Completing the
            // simulation is persisted separately by the manager, so an
            // interrupted run can never strand a player in WebGL.
            HubDoor.Build(new Vector3(4f, 0f, 19.2f), 0f,
                "Return to Hub", "Hub", 0, new Color(0.90f, 0.66f, 0.14f), HubDoor.Mode.Manual);
        }
    }
}
