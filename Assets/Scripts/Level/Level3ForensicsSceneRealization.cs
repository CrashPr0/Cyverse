using UnityEngine;
using Cyverse.Forensics;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Realizes the forensics workflow before the mission manager subscribes
    /// to it. Gameplay-critical intake/report objects and presentation are
    /// therefore ready together instead of appearing in later Start calls.
    /// </summary>
    internal static class Level3ForensicsSceneRealization
    {
        internal sealed class Bindings
        {
            public ForensicsConsole console;
            public VideoStation briefing;
            public LockedDoor taskDoor;
            public HubDoor exitDoor;
            public ChainOfCustodyForm custodyForm;
            public ChainOfCustodyStation custodyStation;
        }

        public static Bindings Realize(GameObject host)
        {
            if (QueryTerminal.Instance == null) host.AddComponent<QueryTerminal>();
            ChainOfCustodyForm form = ChainOfCustodyForm.Ensure(host);
            ChainOfCustodyStation station = ChainOfCustodyStation.Ensure();

            Level3ForensicsPolish polish = Object.FindObjectOfType<Level3ForensicsPolish>();
            if (polish == null) polish = host.AddComponent<Level3ForensicsPolish>();
            polish.Apply();

            HubDoor.EnsureReachableExit(2f, new Color(0.90f, 0.66f, 0.14f));
            foreach (HubDoor door in Object.FindObjectsOfType<HubDoor>())
                door.SetUnlocked(true);

            return new Bindings
            {
                console = Object.FindObjectOfType<ForensicsConsole>(),
                briefing = Object.FindObjectOfType<VideoStation>(),
                taskDoor = Object.FindObjectOfType<LockedDoor>(),
                exitDoor = LevelSceneLookup.NearestExit(),
                custodyForm = form,
                custodyStation = station,
            };
        }
    }
}
