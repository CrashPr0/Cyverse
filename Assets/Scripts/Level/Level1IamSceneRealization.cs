using System;
using System.Collections.Generic;
using UnityEngine;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Turns either Level 1 scene adapter (procedural or authored) into the
    /// same ready-to-run mission graph. All runtime-only delegate/content
    /// wiring and scene discovery stays behind this seam.
    /// </summary>
    internal static class Level1IamSceneRealization
    {
        internal const string BadgeGateMessage =
            "BADGE REQUIRED — enroll at the ID kiosk first.";

        internal static Func<bool> BadgeGate => () => BadgeStation.EnrolledInScene;

        internal sealed class Bindings
        {
            public BadgeStation badge;
            public MfaGauntlet gauntlet;
            public DropZone mfaSlot;
            public SortingStation sorting;
            public AuditStation audit;
            public CertExamStation exam;
            public VideoStation briefing;
            public LockedDoor taskDoor;
            public HubDoor exitDoor;
            public readonly List<StationSetup> legacyStations = new List<StationSetup>();
        }

        public static Bindings Realize(GameObject host)
        {
            ReconcileTaskRoom();
            Level1EndFlowDiagnostics.Install(host);

            var result = new Bindings
            {
                badge = UnityEngine.Object.FindObjectOfType<BadgeStation>(),
                gauntlet = UnityEngine.Object.FindObjectOfType<MfaGauntlet>(),
                sorting = UnityEngine.Object.FindObjectOfType<SortingStation>(),
                audit = UnityEngine.Object.FindObjectOfType<AuditStation>(),
                exam = UnityEngine.Object.FindObjectOfType<CertExamStation>(),
                briefing = UnityEngine.Object.FindObjectOfType<VideoStation>(),
                taskDoor = UnityEngine.Object.FindObjectOfType<LockedDoor>(),
            };

            foreach (DropZone zone in UnityEngine.Object.FindObjectsOfType<DropZone>())
                if (zone.zoneName == "TOKEN SLOT") { result.mfaSlot = zone; break; }

            int taskCount = (result.badge != null ? 1 : 0) +
                (result.gauntlet != null ? 1 : 0) +
                (result.sorting != null ? 1 : 0) +
                (result.audit != null ? 1 : 0);
            if (taskCount == 0)
                result.legacyStations.AddRange(
                    UnityEngine.Object.FindObjectsOfType<StationSetup>());

            HubDoor.EnsureReachableExit(2f, new Color(0.90f, 0.66f, 0.14f));
            result.exitDoor = LevelSceneLookup.NearestExit();
            return result;
        }

        public static void ReconcileTaskRoom()
        {
            MfaGauntlet gauntlet = UnityEngine.Object.FindObjectOfType<MfaGauntlet>();
            if (gauntlet != null)
                gauntlet.Configure(Level1IamSceneFactory.IamBlue,
                    Level1IamContent.DailyPasscode, BadgeGate, BadgeGateMessage);

            SortingStation sorting = UnityEngine.Object.FindObjectOfType<SortingStation>();
            if (sorting != null)
                sorting.Configure(Level1IamContent.SortingCrates(), BadgeGate, BadgeGateMessage);

            AuditStation audit = UnityEngine.Object.FindObjectOfType<AuditStation>();
            if (audit != null)
                audit.Configure(Level1IamContent.AuditRounds(), Level1IamSceneFactory.IamBlue,
                    BadgeGate, BadgeGateMessage);

            CertExamStation exam = UnityEngine.Object.FindObjectOfType<CertExamStation>();
            if (exam != null) exam.Configure(Level1IamContent.ExamQuestions());
        }
    }
}
