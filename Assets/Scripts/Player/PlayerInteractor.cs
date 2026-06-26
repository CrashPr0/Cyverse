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
                HidePrompt();
                return;
            }

            IInteractable target = null;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, range))
            {
                target = hit.collider.GetComponentInParent<IInteractable>();
            }

            if (target != null && target.CanInteract)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.SetInteract(true, target.Prompt, interactKey.ToString());

                if (Input.GetKeyDown(interactKey))
                {
                    if (HudUI.Instance != null) HudUI.Instance.PulseCrosshair();
                    if (Sfx.Instance != null) Sfx.Instance.PlayClick();
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
