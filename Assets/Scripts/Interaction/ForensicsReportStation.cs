using UnityEngine;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Final hand-off point for Level 3. The report desk remains available for
    /// inspection throughout the investigation, but only releases the report
    /// after evidence intake and both case files are complete.
    /// </summary>
    public sealed class ForensicsReportStation : MonoBehaviour, IInteractable
    {
        public bool CanInteract => true;

        public string Prompt
        {
            get
            {
                Level3ForensicsManager manager = Level3ForensicsManager.Instance;
                if (manager == null) return "Forensic report unavailable";
                if (manager.ReportSubmitted) return "Review submitted forensic report";
                return manager.ReportReady
                    ? "Submit final forensic report"
                    : "Forensic report locked — complete the investigation";
            }
        }

        public void Interact(GameObject interactor)
        {
            Level3ForensicsManager manager = Level3ForensicsManager.Instance;
            if (manager == null)
            {
                Deny("Report system unavailable.");
                return;
            }

            if (!manager.ReportReady)
            {
                Deny("The report is locked until custody is accepted and both case files are closed.");
                return;
            }

            if (manager.ReportSubmitted)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("FORENSIC REPORT SUBMITTED — case closed.",
                        new Color(0.30f, 1f, 0.55f));
                return;
            }

            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            manager.SubmitReport();
        }

        private static void Deny(string message)
        {
            if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast(message, new Color(1f, 0.55f, 0.4f));
        }
    }
}
