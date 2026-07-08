using UnityEngine;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 0-specific construction: onboarding stations (I/AM, CIA, NICE),
    /// the Security Scanner completion gate, and the "CYVERSE" centerpiece.
    /// The room shell, materials, signage, player rig, and shared systems come
    /// from <see cref="BuildKit"/> so other levels reuse the same foundation.
    /// Used by both Level0Bootstrap (runtime) and the editor menu
    /// (Level0SceneBuilder) so the procedural scene and a hand-tweakable
    /// editor-built scene are identical.
    /// </summary>
    public static class SceneFactory
    {
        public static readonly Color IamColor = new Color(0.25f, 0.65f, 1.00f);
        public static readonly Color CiaColor = new Color(0.30f, 1.00f, 0.65f);
        public static readonly Color NiceColor = new Color(1.00f, 0.72f, 0.25f);

        /// <summary>Build the entire level (environment + player + systems + stations + scanner).</summary>
        public static void BuildAll()
        {
            BuildKit.BuildLighting();
            BuildKit.BuildFloor(BuildKit.AccentCyan, new Color(0.05f, 0.06f, 0.09f));
            BuildKit.BuildWalls(BuildKit.WallColor);
            BuildKit.BuildCeilingPanels();
            BuildKit.BuildWallDetail(new Color(0.55f, 0.80f, 1f));
            BuildKit.BuildNeonTrim(BuildKit.AccentCyan);
            BuildKit.BuildCenterpiece(new Vector3(0, 2.6f, 13f), "CYVERSE", BuildKit.AccentCyan);
            BuildScanner();
            PropFactory.BuildFurnishings();
            GuardNPC.Build(new Vector3(2.2f, 0f, -5.5f), 180f);
            BuildKit.BuildPlayer();
            BuildSystems();
            BuildStations();
        }

        public static GameObject BuildSystems()
        {
            var sys = BuildKit.BuildCommonSystems();
            sys.AddComponent<Level0Manager>();
            return sys;
        }

        // ---- Security scanner -------------------------------------------------

        public static GameObject BuildScanner()
        {
            var root = new GameObject("SecurityScanner");
            var basePos = new Vector3(8f, 0f, 15f);
            root.transform.position = basePos;

            BuildKit.Spawn(PrimitiveType.Cube, "ScannerPedestal", root.transform,
                basePos + new Vector3(0, 0.75f, 0), new Vector3(1.4f, 1.5f, 0.8f),
                BuildKit.MakeStandard(new Color(0.12f, 0.14f, 0.18f), 0.6f, 0.4f), collider: true);

            var panel = BuildKit.Spawn(PrimitiveType.Quad, "ScannerPanel", root.transform,
                basePos + new Vector3(0, 2.3f, 0), new Vector3(1.2f, 1.6f, 1f),
                BuildKit.MakeHologram(BuildKit.AccentCyan), collider: false);
            panel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var glow = new GameObject("ScannerLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.position = basePos + new Vector3(0, 2.2f, 0);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = BuildKit.AccentCyan;
            l.range = 8f;
            l.intensity = 0.8f; // dim until Level0Manager activates it

            BuildKit.MakeSign(root.transform, basePos + new Vector3(0, 3.6f, 0), "SECURITY SCANNER", BuildKit.AccentCyan);
            BuildKit.AddPanelLabel(root.transform, basePos + new Vector3(0, 2.3f, -0.025f), "SCAN");

            var scanner = root.AddComponent<FaceScanner>();
            scanner.scanLight = l;
            return root;
        }

        // ---- Stations -------------------------------------------------------

        public static void BuildStations()
        {
            BuildKit.CreateStation(StationSetup.Topic.IAM, "Inspect the I/AM Kiosk",
                new Vector3(-5, 0, 3), IamColor, "I/AM KIOSK", "I/AM");
            BuildKit.CreateStation(StationSetup.Topic.CIA, "Inspect the CIA Triad Hologram",
                new Vector3(0, 0, 7), CiaColor, "CIA TRIAD", "CIA");
            BuildKit.CreateStation(StationSetup.Topic.NICE, "Inspect the NICE Roles Board",
                new Vector3(5, 0, 3), NiceColor, "NICE ROLES", "NICE");
        }
    }
}
