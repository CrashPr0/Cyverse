using UnityEngine;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Produces one ready SOC mission from either the procedural scene or the
    /// saved visual-pass scene. It owns compatibility repair, task content,
    /// workstation placement, obsolete presentation removal, and final polish.
    /// </summary>
    internal static class Level2SceneRealization
    {
        internal sealed class Bindings
        {
            public SiemConsole siem;
            public PlaybookStation playbook;
            public CertExamStation exam;
            public VideoStation briefing;
            public LockedDoor taskDoor;
            public HubDoor exitDoor;
        }

        public static Bindings Realize(GameObject host)
        {
            SiemConsole siem = Object.FindObjectOfType<SiemConsole>();
            if (siem != null) siem.Configure(Level2Content.SocScenarios());

            EndpointStation[] endpoints = Object.FindObjectsOfType<EndpointStation>();
            System.Array.Sort(endpoints, (a, b) =>
                a.transform.position.z.CompareTo(b.transform.position.z));
            Level2Content.EndpointDef[] definitions = Level2Content.Endpoints();
            for (int i = 0; i < endpoints.Length; i++)
            {
                if (i < definitions.Length)
                {
                    endpoints[i].transform.SetPositionAndRotation(
                        new Vector3(-17f, 0f, 4.5f + i * 3f),
                        Quaternion.Euler(0f, -90f, 0f));
                    endpoints[i].ConfigureSoc(definitions[i], siem);
                }
                else endpoints[i].gameObject.SetActive(false);
            }

            EdrFleet fleet = Object.FindObjectOfType<EdrFleet>();
            if (fleet != null) fleet.enabled = false;

            PlaybookStation playbook = Object.FindObjectOfType<PlaybookStation>();
            if (playbook != null)
            {
                playbook.Place(new Vector3(17f, 0f, 12f), 90f);
                playbook.Configure();
            }

            LevelSceneLookup.HideTextObjectsNamed("Sign_ENDPOINTS");

            CertExamStation exam = Object.FindObjectOfType<CertExamStation>();
            if (exam != null) exam.Configure(Level2Content.ExamQuestions());

            GameObject runtimeHost = host != null ? host : new GameObject("Level2Runtime");
            Level2SocPolish.Ensure(runtimeHost).Apply();

            HubDoor.EnsureReachableExit(2f, new Color(0.90f, 0.66f, 0.14f));
            return new Bindings
            {
                siem = siem,
                playbook = playbook,
                exam = exam,
                briefing = Object.FindObjectOfType<VideoStation>(),
                taskDoor = Object.FindObjectOfType<LockedDoor>(),
                exitDoor = LevelSceneLookup.NearestExit(),
            };
        }
    }
}
