using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Settings;

namespace Cyverse.Player
{
    /// <summary>
    /// Desktop / WebGL first-person movement: WASD to move, mouse to look.
    /// No jumping or VR — matches the Level 0 demo requirements. Movement is
    /// suspended while a dialogue beat or the settings menu is active.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 4.5f;
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
            // WASD only — deliberately NOT the "Horizontal"/"Vertical" axes,
            // which also map the arrow keys. Arrows belong to stations (video
            // scrubbing, audit-log navigation); reading axes made every arrow
            // press strafe the player away from the screen they were using.
            float h = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float v = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);

            Vector3 move = transform.right * h + transform.forward * v;
            move = Vector3.ClampMagnitude(move, 1f) * moveSpeed;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);

            // Footsteps: play one every stepInterval metres of ground travel.
            if (controller.isGrounded)
            {
                Vector3 horiz = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
                stepDistance += horiz.magnitude * Time.deltaTime;
                if (stepDistance >= stepInterval)
                {
                    stepDistance = 0f;
                    if (Sfx.Instance != null) Sfx.Instance.PlayFootstep();
                }
            }
        }

        private void ApplyGravityOnly()
        {
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
        }

        /// <summary>Lock + hide the cursor for play, or release it for menus.</summary>
        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
