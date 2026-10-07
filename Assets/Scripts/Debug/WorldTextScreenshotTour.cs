#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Cyverse.Interaction;
using Cyverse.Forensics;
using Cyverse.Level;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Testing
{
    /// <summary>
    /// Development-build-only camera placement for repeatable visual QA.
    /// Enable with ?textLayoutPreview=1&amp;view=alert|soc|workstations|workstations_north|playbook|briefing|iam_tasks|iam_badge|iam_mfa|iam_authorization|iam_audit|iam_cert|iam_exit|custody|upload|forensics|report|locker|locker_angle|locker_far.
    /// It never ships in a non-development WebGL build.
    /// </summary>
    public sealed class WorldTextScreenshotTour : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);

            Camera camera = Camera.main;
            if (camera == null) yield break;

            foreach (FirstPersonController controller in FindObjectsOfType<FirstPersonController>())
                controller.enabled = false;
            foreach (PlayerInteractor interactor in FindObjectsOfType<PlayerInteractor>())
                interactor.enabled = false;
            foreach (ControlsOverlay controls in FindObjectsOfType<ControlsOverlay>())
                Destroy(controls);
            GameObject controlsCard = GameObject.Find("ControlsOverlay");
            if (controlsCard != null) controlsCard.SetActive(false);

            // The camera is detached so a CharacterController or saved player
            // transform cannot pull it away from the deterministic QA view.
            camera.transform.SetParent(null, true);
            HideFirstPersonGeometry(camera.transform);

            string url = Application.absoluteURL;
            if (url.Contains("view=locker_angle")) PositionLockerAngle(camera);
            else if (url.Contains("view=locker_far")) PositionLockerFar(camera);
            else if (url.Contains("view=locker")) PositionLocker(camera);
            else if (url.Contains("view=custody")) PositionCustody(camera);
            else if (url.Contains("view=report")) PositionForensicsReport(camera);
            else if (url.Contains("view=upload")) PositionForensicsUpload(camera);
            else if (url.Contains("view=forensics")) PositionForensicsOverview(camera);
            else if (url.Contains("view=playbook")) PositionPlaybook(camera);
            else if (url.Contains("view=workstations_north")) PositionWorkstationsNorth(camera);
            else if (url.Contains("view=workstations")) PositionWorkstations(camera);
            else if (url.Contains("view=soc")) PositionSocOverview(camera);
            else if (url.Contains("view=iam_tasks")) PositionIamTasks(camera);
            else if (url.Contains("view=iam_badge")) PositionIamBadge(camera);
            else if (url.Contains("view=iam_mfa")) PositionIamMfa(camera);
            else if (url.Contains("view=iam_authorization")) PositionIamAuthorization(camera);
            else if (url.Contains("view=iam_audit")) PositionIamAudit(camera);
            else if (url.Contains("view=iam_exit")) PositionIamExit(camera);
            else if (url.Contains("view=iam_cert")) PositionCertification(camera);
            else if (url.Contains("view=briefing")) PositionBriefing(camera);
            else PositionAlertBoard(camera);

            if (WorldTextLayoutManager.Instance != null)
            {
                WorldTextLayoutManager.Instance.RefreshNow();
                WorldTextLayoutManager.Instance.EvaluateNow();
            }
        }

        private static void PositionAlertBoard(Camera camera)
        {
            SiemConsole board = FindObjectOfType<SiemConsole>();
            if (board == null) return;
            Vector3 target = board.transform.TransformPoint(0f, 2.45f, 0f);
            Vector3 position = target - board.transform.forward * 6.2f + Vector3.up * 0.15f;
            Place(camera, position, target);
        }

        private static void PositionSocOverview(Camera camera)
        {
            Place(camera, new Vector3(0f, 3.0f, 3.8f), new Vector3(0f, 2.0f, 12f));
        }

        private static void PositionWorkstations(Camera camera)
        {
            // The SIEM desk sits between the aisle camera and the workstation
            // wall. Two opposing review angles cover the complete row without
            // pretending a single occluded shot can show all four screens.
            Place(camera, new Vector3(-9.5f, 2.7f, 3.3f), new Vector3(-17f, 1.35f, 8f));
        }

        private static void PositionWorkstationsNorth(Camera camera)
        {
            Place(camera, new Vector3(-9.5f, 2.7f, 14.7f), new Vector3(-17f, 1.35f, 10f));
        }

        private static void PositionPlaybook(Camera camera)
        {
            PlaybookStation playbook = FindObjectOfType<PlaybookStation>();
            if (playbook == null) return;
            Vector3 target = playbook.transform.TransformPoint(new Vector3(0f, 1.75f, 0f));
            Vector3 readableSide = playbook.transform.TransformDirection(Vector3.back);
            // The six slots span the full board width. Keep the whole puzzle
            // in frame at the repeatable QA angle, including the two edge
            // labels, instead of producing a misleading clipped screenshot.
            Place(camera, target + readableSide * 13.2f + Vector3.up * 1.25f, target);
        }

        private static void PositionBriefing(Camera camera)
        {
            VideoStation briefing = FindObjectOfType<VideoStation>();
            if (briefing == null) return;
            Vector3 target = briefing.transform.TransformPoint(0f, 2.0f, 0f);
            Vector3 position = target - briefing.transform.forward * 6.5f;
            Place(camera, position, target);
        }

        private static void PositionIamTasks(Camera camera)
        {
            Place(camera, new Vector3(0f, 4.1f, 3.5f), new Vector3(0f, 1.6f, 13f));
        }

        private static void PositionIamBadge(Camera camera)
        {
            // Stay north of the z=2 divider; a longer lens position captures
            // the wall rather than the kiosk itself.
            PositionNamedFront(camera, "BadgeStation", 2.3f, 1.45f);
        }

        private static void PositionIamMfa(Camera camera)
        {
            // From the playable aisle this includes the west-wall vault, both
            // fixed factors, phone dock and token slot in one diagnostic view.
            Place(camera, new Vector3(-7.4f, 3.4f, 8.0f), new Vector3(-14.6f, 1.55f, 11.8f));
        }

        private static void PositionIamAuthorization(Camera camera)
        {
            Place(camera, new Vector3(7.2f, 3.8f, 4.4f), new Vector3(13f, 1.25f, 11f));
        }

        private static void PositionIamAudit(Camera camera)
        {
            PositionNamedFront(camera, "AuditStation", 5.4f, 2.0f);
        }

        private static void PositionIamExit(Camera camera)
        {
            HubDoor selected = null;
            foreach (HubDoor door in FindObjectsOfType<HubDoor>())
                if (selected == null || door.transform.position.z > selected.transform.position.z)
                    selected = door;
            if (selected == null) return;

            Vector3 target = selected.transform.position + Vector3.up * 2.15f;
            Vector3 position = target - selected.transform.forward * 5.2f + Vector3.up * 0.25f;
            Place(camera, position, target);
        }

        private static void PositionCertification(Camera camera)
        {
            PositionNamedFront(camera, "CertExamStation", 4.8f, 1.5f);
        }

        private static void PositionLocker(Camera camera)
        {
            EvidenceLocker locker = FindObjectOfType<EvidenceLocker>();
            if (locker == null) return;
            camera.fieldOfView = 55f;
            Place(camera, locker.transform.TransformPoint(new Vector3(0f, 1.5f, -2.6f)),
                locker.transform.TransformPoint(new Vector3(0f, 1.46f, 0.5f)));
        }

        private static void PositionLockerAngle(Camera camera)
        {
            EvidenceLocker locker = FindObjectOfType<EvidenceLocker>();
            if (locker == null) return;
            camera.fieldOfView = 55f;
            Place(camera, locker.transform.TransformPoint(new Vector3(0.65f, 1.65f, -1.35f)),
                locker.transform.TransformPoint(new Vector3(-0.08f, 1.47f, 0.55f)));
        }

        private static void PositionLockerFar(Camera camera)
        {
            EvidenceLocker locker = FindObjectOfType<EvidenceLocker>();
            if (locker == null) return;
            Vector3 entry = locker.transform.TransformPoint(new Vector3(-0.4709f, 1.46f, -0.585f));
            Vector3 exit = locker.transform.TransformPoint(new Vector3(0.46f, 1.46f, 1.02f));
            camera.fieldOfView = 35f;
            Place(camera, entry - (exit - entry).normalized * 15f, exit);
        }

        private static void PositionNamedFront(Camera camera, string objectName,
            float distance, float targetHeight)
        {
            GameObject targetObject = GameObject.Find(objectName);
            if (targetObject == null) return;
            Vector3 target = targetObject.transform.position + Vector3.up * targetHeight;
            Vector3 position = target - targetObject.transform.forward * distance + Vector3.up * 0.35f;
            Place(camera, position, target);
        }

        private static void PositionCustody(Camera camera)
        {
            ChainOfCustodyStation station = FindObjectOfType<ChainOfCustodyStation>();
            if (station != null)
            {
                Vector3 target = station.transform.position + Vector3.up * 1.25f;
                Place(camera, target + new Vector3(0f, 1.1f, -4.8f), target);
            }
            if (ChainOfCustodyForm.Instance != null)
                ChainOfCustodyForm.Instance.Open();
        }

        private static void PositionForensicsOverview(Camera camera)
        {
            ForensicsConsole console = FindObjectOfType<ForensicsConsole>();
            if (console == null) return;
            Vector3 target = console.transform.position + Vector3.up * 1.25f;
            // The monitor faces the same -forward side as the other stations;
            // approach from the playable aisle so the console screens, not
            // their backs, are what the deterministic QA frame evaluates.
            Place(camera, target - console.transform.forward * 6.8f + Vector3.up * 1.1f, target);
        }

        private static void PositionForensicsUpload(Camera camera)
        {
            PlugInStation station = PlugInStation.Instance;
            if (station == null) { PositionForensicsOverview(camera); return; }
            // Frame the finished state: cradle, write blocker and LEFT monitor
            // from where an analyst stands at the console.
            station.CompleteNow();
            ForensicsConsole console = station.GetComponentInParent<ForensicsConsole>();
            Transform desk = console != null ? console.transform : station.transform;
            Vector3 target = station.transform.position + Vector3.up * 0.42f;
            Place(camera, desk.position - desk.forward * 2.2f + Vector3.up * 1.7f, target);
        }

        private static void PositionForensicsReport(Camera camera)
        {
            GameObject report = GameObject.Find("DF_ReportMonitor");
            if (report == null) { PositionForensicsOverview(camera); return; }
            Vector3 target = report.transform.position + Vector3.up * 0.08f;
            // This is a monitor-readability check, so keep foreground plants
            // and the desk edge from consuming most of the deterministic frame.
            Place(camera, target + new Vector3(0f, 0.30f, -3.7f), target);
        }

        private static void Place(Camera camera, Vector3 position, Vector3 target)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);
        }

        private static void HideFirstPersonGeometry(Transform camera)
        {
            foreach (Renderer renderer in camera.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
        }
    }
}
#endif
