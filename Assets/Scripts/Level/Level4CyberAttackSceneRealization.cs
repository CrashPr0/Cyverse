using UnityEngine;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Makes Level 4 mechanically and visually ready through one entry point.
    /// Both scene adapters receive the same station ordering, content wiring,
    /// presentation reconciliation, safe exit, and resolved mission bindings.
    /// </summary>
    internal static class Level4CyberAttackSceneRealization
    {
        internal sealed class Bindings
        {
            public CyberAttackStation[] stations;
            public VideoStation briefing;
            public LockedDoor taskDoor;
            public HubDoor exitDoor;
        }

        public static Bindings Realize(GameObject host,
            Level4CyberAttackManager manager,
            Level4CyberAttackContent.StationScenario[] scenarios)
        {
            GameObject runtimeHost = host != null ? host : new GameObject("Level4Runtime");
            Level4CyberAttackVisuals visuals = Level4CyberAttackVisuals.Ensure(runtimeHost);
            visuals.Apply();

            CyberAttackStation[] stations = Object.FindObjectsOfType<CyberAttackStation>();
            System.Array.Sort(stations, (a, b) => a.StationIndex.CompareTo(b.StationIndex));
            for (int i = 0; i < stations.Length; i++)
            {
                int scenarioIndex = Mathf.Clamp(i, 0, scenarios.Length - 1);
                stations[i].Configure(manager, i, scenarios[scenarioIndex].kind);
            }

            HubDoor.EnsureReachableExit(2f, new Color(0.90f, 0.66f, 0.14f));
            return new Bindings
            {
                stations = stations,
                briefing = Object.FindObjectOfType<VideoStation>(),
                taskDoor = Object.FindObjectOfType<LockedDoor>(),
                exitDoor = LevelSceneLookup.NearestExit(),
            };
        }
    }
}
