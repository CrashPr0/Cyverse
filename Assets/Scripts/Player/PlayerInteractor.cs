using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.UI;

namespace Cyverse.Player
{
    /// <summary>
    /// Casts a ray from the camera each frame. When it hits something that
    /// implements <see cref="IInteractable"/>, the HUD shows a prompt and the
    /// interact key activates it.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public float range = 3.5f;
        public KeyCode interactKey = KeyCode.E;

        /// <summary>True while an interactable is under the crosshair (read by FirstPersonHands).</summary>
        public static bool TargetInView { get; private set; }

        private Camera cam;

        void Awake()
        {
            cam = Camera.main;
        }

        void Update()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            if (GameState.Busy)
            {
                TargetInView = false;
                HidePrompt();
                return;
            }

            IInteractable target = null;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, range))
            {
                target = hit.collider.GetComponentInParent<IInteractable>();
            }

            TargetInView = target != null && target.CanInteract;

            if (TargetInView)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.SetInteract(true, target.Prompt, interactKey.ToString());

                if (Input.GetKeyDown(interactKey))
                {
                    if (HudUI.Instance != null) HudUI.Instance.PulseCrosshair();
                    if (Sfx.Instance != null) Sfx.Instance.PlayClick();
                    if (FirstPersonHands.Instance != null) FirstPersonHands.Instance.TriggerInteract();
                    target.Interact(gameObject);
                }
            }
            else
            {
                HidePrompt();
            }
        }

        private void HidePrompt()
        {
            if (HudUI.Instance != null) HudUI.Instance.SetInteract(false);
        }
    }
}
