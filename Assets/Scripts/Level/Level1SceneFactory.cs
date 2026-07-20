using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Dialogue;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 1 (Cyber Defense / SOC Analyst) construction: the SIEM/EDR/Incident
    /// Response stations, the Threat Response Console completion gate, the SOC
    /// Lead NPC, and the "SOC CORE" centerpiece. Room shell, materials, signage,
    /// player rig, and shared systems come from <see cref="BuildKit"/> — the
    /// same foundation Level 0 uses — so this is a blockout built ON that base,
    /// not a rewrite of it. Room layout deliberately reuses Level 0's exact
    /// coordinates (spawn, station arc, core, gate) since they're already
    /// playtested for clear sightlines; only the palette and content differ.
    /// Used by both Level1Bootstrap (runtime) and the editor menu
    /// (Level1SceneBuilder) so the procedural and hand-tweaked scenes match.
    /// </summary>
    public static class Level1SceneFactory
    {
        // "SOC Red" alert theme for the room shell/centerpiece — visually
        // distinct from Level 0's cyan onboarding lobby.
        public static readonly Color AlertAccent = new Color(0.95f, 0.35f, 0.25f);

        public static readonly Color SiemColor = new Color(0.25f, 0.85f, 0.90f);
        public static readonly Color EdrColor = new Color(1.00f, 0.60f, 0.15f);
        public static readonly Color IncidentColor = new Color(0.75f, 0.35f, 1.00f);

        /// <summary>Build the entire level (environment + player + systems + stations + gate).</summary>
        public static void BuildAll()
        {
            BuildKit.BuildLighting();
            BuildKit.BuildFloor(AlertAccent, new Color(0.06f, 0.05f, 0.06f));
            BuildKit.BuildWalls(BuildKit.WallColor);
            BuildKit.BuildCeilingPanels();
            BuildKit.BuildWallDetail(AlertAccent);
            BuildKit.BuildNeonTrim(AlertAccent);
            BuildKit.BuildCenterpiece(new Vector3(0, 2.6f, 13f), "SOC CORE", AlertAccent);
            BuildGate();
            PropFactory.BuildFurnishings();
            BuildGuard();
            BuildKit.BuildPlayer();
            BuildSystems();
            BuildStations();
        }

        public static GameObject BuildSystems()
        {
            var sys = BuildKit.BuildCommonSystems();
            sys.AddComponent<Level1Manager>();
            return sys;
        }

        // ---- SOC Lead NPC -----------------------------------------------------

        public static GameObject BuildGuard()
        {
            return GuardNPC.Build(new Vector3(2.2f, 0f, -5.5f), 180f,
                displayName: "SOC Lead", signText: "SOC LEAD",
                dialogueProfile: GuardNPC.DialogueProfile.CyberDefenseLead);
        }

        public static List<DialogueLine> SocLeadLines()
        {
            var phase = Level1Manager.Instance != null
                ? Level1Manager.Instance.CurrentPhase
                : Level1Manager.Phase.Review;

            switch (phase)
            {
                case Level1Manager.Phase.Certify:
                    return new List<DialogueLine>
                    {
                        new DialogueLine("SOC Lead",
                            "All stations reviewed — solid work. Head to the Threat Response Console and certify to finish this rotation.", null, 4f),
                    };
                case Level1Manager.Phase.Complete:
                    return new List<DialogueLine>
                    {
                        new DialogueLine("SOC Lead",
                            $"Nice work out there, {PlayerIdentity.Callsign}. That's the core of cyber defense.", null, 3.5f),
                    };
                default:
                    return new List<DialogueLine>
                    {
                        new DialogueLine("SOC Lead",
                            "Check the SIEM Console, the EDR Terminal, and the Incident Response Board. Walk up to each glowing station and press E.", null, 4.5f),
                        new DialogueLine("SOC Lead",
                            "Answer the knowledge checks to earn points — and press G any time to open the glossary.", null, 4f),
                    };
            }
        }

        // ---- Threat Response Console ------------------------------------------

        public static GameObject BuildGate()
        {
            var root = new GameObject("ThreatResponseConsole");
            var basePos = new Vector3(8f, 0f, 15f);
            root.transform.position = basePos;

            BuildKit.Spawn(PrimitiveType.Cube, "GatePedestal", root.transform,
                basePos + new Vector3(0, 0.75f, 0), new Vector3(1.4f, 1.5f, 0.8f),
                BuildKit.MakeStandard(new Color(0.12f, 0.10f, 0.10f), 0.6f, 0.4f), collider: true);

            var panel = BuildKit.Spawn(PrimitiveType.Quad, "GatePanel", root.transform,
                basePos + new Vector3(0, 2.3f, 0), new Vector3(1.2f, 1.6f, 1f),
                BuildKit.MakeHologram(AlertAccent), collider: false);
            panel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var glow = new GameObject("GateLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.position = basePos + new Vector3(0, 2.2f, 0);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = AlertAccent;
            l.range = 8f;
            l.intensity = 0.8f; // dim until Level1Manager activates it

            BuildKit.MakeSign(root.transform, basePos + new Vector3(0, 3.6f, 0), "THREAT RESPONSE CONSOLE", AlertAccent);
            BuildKit.AddPanelLabel(root.transform, basePos + new Vector3(0, 2.3f, -0.025f), "CERTIFY");

            var gate = root.AddComponent<Level1Gate>();
            gate.gateLight = l;
            return root;
        }

        // ---- Stations -------------------------------------------------------

        public static void BuildStations()
        {
            BuildKit.CreateStation(StationSetup.Topic.SIEM, "Inspect the SIEM Console",
                new Vector3(-5, 0, 3), SiemColor, "SIEM CONSOLE", "SIEM",
                Level1Content.Siem, () => Level1Quiz.For(StationSetup.Topic.SIEM),
                NotifyLevel1Manager);

            BuildKit.CreateStation(StationSetup.Topic.EDR, "Inspect the EDR Terminal",
                new Vector3(0, 0, 7), EdrColor, "EDR TERMINAL", "EDR",
                Level1Content.Edr, () => Level1Quiz.For(StationSetup.Topic.EDR),
                NotifyLevel1Manager);

            BuildKit.CreateStation(StationSetup.Topic.INCIDENT, "Inspect the Incident Response Board",
                new Vector3(5, 0, 3), IncidentColor, "INCIDENT RESPONSE", "IR",
                Level1Content.Incident, () => Level1Quiz.For(StationSetup.Topic.INCIDENT),
                NotifyLevel1Manager);
        }

        private static void NotifyLevel1Manager()
        {
            if (Level1Manager.Instance != null) Level1Manager.Instance.NotifyStationReviewed();
        }
    }
}
