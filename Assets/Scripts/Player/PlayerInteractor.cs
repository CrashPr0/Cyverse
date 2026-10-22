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
    ///
    /// While carrying an item there is one action, on E (Q does the same):
    /// aimed at a slot or station it places the item there; aimed at nothing
    /// it puts the item down. Playtesters were confused by being told "Q to
    /// drop" in one place and "E to place" in another.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public float range = 3.5f;
        public KeyCode interactKey = KeyCode.E;

        /// <summary>True while an interactable is under the crosshair (read by FirstPersonHands).</summary>
        public static bool TargetInView { get; private set; }

        /// <summary>The interactable under the crosshair, if any (read by stations for hover glow).</summary>
        public static IInteractable CurrentTarget { get; private set; }

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
                CurrentTarget = null;
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
            CurrentTarget = TargetInView ? target : null;

            Carryable carried = Carryable.Carried;
            bool actPressed = Input.GetKeyDown(interactKey) ||
                              (carried != null && Input.GetKeyDown(KeyCode.Q));

            if (TargetInView)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.SetInteract(true, target.Prompt, interactKey.ToString());

                if (actPressed)
                {
                    string targetType = target.GetType().Name;
                    string targetPrompt = target.Prompt;
                    if (GameplayActions.TryApply(target, GameplayAction.Interact(), gameObject))
                    {
                        if (HudUI.Instance != null) HudUI.Instance.PulseCrosshair();
                        if (Sfx.Instance != null) Sfx.Instance.PlayClick();
                        if (FirstPersonHands.Instance != null) FirstPersonHands.Instance.TriggerInteract();
                        PlaytestMetrics.RecordInteraction(targetType, targetPrompt);
                    }
                }
            }
            else if (carried != null && !carried.CanPutDown)
            {
                // Evidence in custody has no "put down" prompt: only the station
                // that takes it can. E still answers, naming where it goes.
                HidePrompt();
                if (actPressed) carried.PutDown();
            }
            else if (carried != null)
            {
                // Carrying, aimed at nothing that takes it: the same key sets
                // it down. The badge says so, so there is never a separate
                // "drop" key to remember.
                if (HudUI.Instance != null)
                    HudUI.Instance.SetInteract(true, $"Put down {carried.itemName}", interactKey.ToString());
                if (actPressed)
                {
                    carried.PutDown();
                    if (Sfx.Instance != null) Sfx.Instance.PlayClick();
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
