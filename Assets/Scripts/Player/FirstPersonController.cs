using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.Settings;

namespace Cyverse.Player
{
    /// <summary>
    /// Desktop / WebGL first-person movement: WASD to move, mouse to look.
    /// No jumping or VR — matches the Level 0 demo requirements. Movement is
    /// suspended while a dialogue beat or the settings menu is active.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour, IGameplayActionTarget
    {
        [Header("Movement")]
        public float moveSpeed = 5.2f;
        public float sprintMultiplier = 1.6f; // while Shift is held
        public float gravity = -9.81f;

        [Header("Look")]
        public float lookSensitivity = 2f;
        public float maxPitch = 85f;

        [Header("Footsteps")]
        public float stepInterval = 2.0f; // metres travelled per footstep

        private CharacterController controller;
        private Transform cam;
        private float pitch;
        private float verticalVelocity;
        private float stepDistance;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (Camera.main != null) cam = Camera.main.transform;
        }

        void Start()
        {
            LockCursor(true);
        }

        void Update()
        {
            // Still apply gravity while "busy" so the player doesn't hover,
            // but ignore movement and look input.
            if (GameState.Busy)
            {
                ApplyGravityOnly();
                return;
            }

            // Re-acquire pointer lock after a focus loss or (for WebGL) the
            // first click — browsers/editor only grant lock after a user gesture,
            // so until then the OS cursor is visible and roams over the HUD.
            if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
                LockCursor(true);

            HandleLook();
            HandleMove();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && !GameState.Busy) LockCursor(true);
        }

        private void HandleLook()
        {
            float userScale = AccessibilitySettings.Instance != null
                ? AccessibilitySettings.Instance.MouseSensitivity
                : 1f;
            float sens = lookSensitivity * userScale;

            float mouseX = Input.GetAxis("Mouse X") * sens;
            float mouseY = Input.GetAxis("Mouse Y") * sens;

            transform.Rotate(Vector3.up * mouseX);

            pitch = Mathf.Clamp(pitch - mouseY, -maxPitch, maxPitch);
            if (cam != null) cam.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        private void HandleMove()
        {
            // WASD, plus the arrow keys unless the player turned that off in
            // settings. Read key by key, not through the "Horizontal" /
            // "Vertical" axes, so the setting is respected. While the player
            // looks at a briefing screen, left/right arrows scrub it instead
            // of strafing away from it. Full-screen panels pause movement (Busy).
            bool arrows = AccessibilitySettings.ArrowKeysMove;
            bool arrowsStrafe = arrows && !Interaction.VideoStation.BeingWatched;
            bool right = Input.GetKey(KeyCode.D) || (arrowsStrafe && Input.GetKey(KeyCode.RightArrow));
            bool left = Input.GetKey(KeyCode.A) || (arrowsStrafe && Input.GetKey(KeyCode.LeftArrow));
            bool forward = Input.GetKey(KeyCode.W) || (arrows && Input.GetKey(KeyCode.UpArrow));
            bool back = Input.GetKey(KeyCode.S) || (arrows && Input.GetKey(KeyCode.DownArrow));
            float h = (right ? 1f : 0f) - (left ? 1f : 0f);
            float v = (forward ? 1f : 0f) - (back ? 1f : 0f);

            // Shift sprints. Busy (menus, dialogue) never reaches this method,
            // and a disabled controller can't sprint either. Footsteps are
            // timed by distance, so they quicken on their own.
            bool sprint = controller != null && controller.enabled &&
                          (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            float speed = sprint ? moveSpeed * sprintMultiplier : moveSpeed;

            Vector3 velocity = Vector3.ClampMagnitude(
                transform.right * h + transform.forward * v, 1f) * speed;
            GameplayActions.TryApply(this,
                GameplayAction.Move(velocity, Time.deltaTime), gameObject);
        }

        public bool TryApply(GameplayAction action, GameObject actor)
        {
            if (action.Kind != GameplayActionKind.Move || controller == null ||
                !controller.enabled || action.Amount <= 0f || float.IsNaN(action.Amount) ||
                float.IsInfinity(action.Amount) || !IsFinite(action.Vector))
                return false;

            ApplyMovement(action.Vector, action.Amount);
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private void ApplyMovement(Vector3 horizontalVelocity, float deltaSeconds)
        {
            Vector3 move = horizontalVelocity;
            move.y = 0f;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * deltaSeconds;
            move.y = verticalVelocity;

            controller.Move(move * deltaSeconds);

            // Footsteps: play one every stepInterval metres of ground travel.
            if (controller.isGrounded)
            {
                Vector3 horiz = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
                stepDistance += horiz.magnitude * deltaSeconds;
                if (stepDistance >= stepInterval)
                {
                    stepDistance = 0f;
                    if (Sfx.Instance != null) Sfx.Instance.PlayFootstep();
                }
            }
        }

        private void ApplyGravityOnly()
        {
            ApplyMovement(Vector3.zero, Time.deltaTime);
        }

        /// <summary>Lock + hide the cursor for play, or release it for menus.</summary>
        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
