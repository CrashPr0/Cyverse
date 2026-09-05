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
    /// Enable with ?textLayoutPreview=1&amp;view=alert|soc|workstations|playbook|briefing|custody.
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
            if (url.Contains("view=custody")) PositionCustody(camera);
            else if (url.Contains("view=playbook")) PositionPlaybook(camera);
            else if (url.Contains("view=workstations")) PositionWorkstations(camera);
            else if (url.Contains("view=soc")) PositionSocOverview(camera);
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
            Place(camera, new Vector3(-9.5f, 2.7f, 3.3f), new Vector3(-17f, 1.35f, 9f));
        }

        private static void PositionPlaybook(Camera camera)
        {
            PlaybookStation playbook = FindObjectOfType<PlaybookStation>();
            if (playbook == null) return;
            Vector3 target = playbook.transform.TransformPoint(new Vector3(0f, 1.75f, 0f));
            Vector3 readableSide = playbook.transform.TransformDirection(Vector3.back);
            Place(camera, target + readableSide * 9.4f + Vector3.up * 1.25f, target);
        }

        private static void PositionBriefing(Camera camera)
        {
            VideoStation briefing = FindObjectOfType<VideoStation>();
            if (briefing == null) return;
            Vector3 target = briefing.transform.TransformPoint(0f, 2.0f, 0f);
            Vector3 position = target - briefing.transform.forward * 6.5f;
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
