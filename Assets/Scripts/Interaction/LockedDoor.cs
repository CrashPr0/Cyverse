using System.Collections;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// A physical sliding door that starts locked. Interacting while locked
    /// explains what's required; <see cref="Unlock"/> slides the panel into
    /// the floor and disables its collider so the player can walk through.
    /// Built procedurally via <see cref="Build"/> (frame + panel + sign).
    /// </summary>
    public class LockedDoor : MonoBehaviour, IInteractable
    {
        [TextArea] public string lockedMessage = "This door is locked.";
        public float slideSeconds = 1.2f;

        public bool IsLocked { get; private set; } = true;

        private Transform panel;
        private Collider panelCollider;
        public float doorwayWidth = 3f;

        public string Prompt => "Door (locked)";
        public bool CanInteract => IsLocked; // once open, no prompt at all

        void Awake()
        {
            ResolveReferences();
            NormalizeGeometry();
        }

        // Start, not Awake: Build() sets doorwayWidth after AddComponent. Saved
        // visual-pass scenes predate the floor track, so it is added here too.
        void Start() => EnsureThreshold();

        void OnValidate()
        {
            ResolveReferences();
            NormalizeGeometry();
        }

        /// <summary>Fits the complete assembly inside the divider opening.
        /// The old posts/header extended 0.4 m into each wall and the panel
        /// touched both the floor and lintel, producing visible z-fighting.</summary>
        private void NormalizeGeometry()
        {
            float width = Mathf.Max(1.5f, doorwayWidth);
            foreach (Transform child in transform)
            {
                if (child.name == "Post")
                {
                    float side = child.localPosition.x < 0f ? -1f : 1f;
                    child.localPosition = new Vector3(side * (width * 0.5f - 0.15f), 1.95f, 0f);
                    child.localScale = new Vector3(0.3f, 3.9f, 0.5f);
                }
                else if (child.name == "Header")
                {
                    child.localPosition = new Vector3(0f, 4.08f, 0f);
                    child.localScale = new Vector3(width, 0.26f, 0.5f);
                }
            }
            if (panel != null)
            {
                panel.localPosition = new Vector3(0f, 2f, 0f);
                panel.localScale = new Vector3(width - 0.2f, 3.8f, 0.16f);
            }
        }

        public void Interact(GameObject interactor)
        {
            if (!IsLocked) return;
            if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast(lockedMessage, new Color(1f, 0.55f, 0.4f));
        }

        public void Unlock()
        {
            if (!IsLocked) return;
            ResolveReferences();
            IsLocked = false;
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            if (panel != null) StartCoroutine(SlideOpen());
        }

        private void ResolveReferences()
        {
            if (panel == null) panel = transform.Find("Panel");
            if (panelCollider == null && panel != null)
                panelCollider = panel.GetComponent<Collider>();
        }

        private IEnumerator SlideOpen()
        {
            if (panelCollider != null) panelCollider.enabled = false;

            // Sink by the panel's full height plus a margin so its top ends
            // below the floor. The old fixed 3.2 m drop left the top 0.7 m of a
            // 3.8 m panel glowing across the open doorway.
            Vector3 start = panel.localPosition;
            float panelTop = start.y + panel.localScale.y * 0.5f;
            Vector3 end = start + Vector3.down * (panelTop + SunkenClearance);
            if (Settings.AccessibilitySettings.ReduceMotion)
            {
                panel.localPosition = end;
                HidePanel();
                yield break;
            }

            float t = 0f;
            while (t < slideSeconds)
            {
                t += Time.deltaTime;
                panel.localPosition = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / slideSeconds));
                yield return null;
            }
            panel.localPosition = end;
            HidePanel();
        }

        private const float SunkenClearance = 0.06f;

        /// <summary>Fully sunk, the panel is pure overdraw under the floor; turn
        /// its renderers off so no camera angle can catch it.</summary>
        private void HidePanel()
        {
            foreach (Renderer r in panel.GetComponentsInChildren<Renderer>()) r.enabled = false;
        }

        /// <summary>A flush floor track the panel drops into, so it visibly
        /// enters a slot instead of clipping through solid floor tiles.</summary>
        private void EnsureThreshold()
        {
            if (transform.Find("Threshold") != null) return;
            float width = Mathf.Max(1.5f, doorwayWidth);
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Threshold";
            plate.transform.SetParent(transform, false);
            plate.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            plate.transform.localScale = new Vector3(width - 0.06f, 0.012f, 0.44f);
            plate.GetComponent<Renderer>().sharedMaterial =
                BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.14f), 0.6f, 0.7f);
            BuildKit.StripCollider(plate);

            var slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slot.name = "Slot";
            slot.transform.SetParent(plate.transform, false);
            // Slightly taller than the plate so its top sits 1 mm above it.
            slot.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            slot.transform.localScale = new Vector3((width - 0.16f) / (width - 0.06f), 1.08f, 0.24f / 0.44f);
            slot.GetComponent<Renderer>().sharedMaterial =
                BuildKit.MakeStandard(new Color(0.008f, 0.009f, 0.012f), 0.2f, 0.1f);
            BuildKit.StripCollider(slot);
        }

        // ---- Construction ----------------------------------------------------

        /// <summary>Door assembly centred at <paramref name="position"/>, panel
        /// spanning <paramref name="width"/> across local X. Edit/play safe.</summary>
        public static LockedDoor Build(Vector3 position, float rotY, float width,
            string signText, string lockedMessage, Color accent)
        {
            var root = new GameObject("LockedDoor_" + signText.Replace(' ', '_'));
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            Material frameMat = BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.55f, 0.35f);

            Post(root.transform, new Vector3(-width * 0.5f - 0.25f, 2f, 0f), frameMat);
            Post(root.transform, new Vector3(width * 0.5f + 0.25f, 2f, 0f), frameMat);

            var header = GameObject.CreatePrimitive(PrimitiveType.Cube);
            header.name = "Header";
            header.transform.SetParent(root.transform, false);
            header.transform.localPosition = new Vector3(0f, 4.08f, 0f);
            header.transform.localScale = new Vector3(width, 0.26f, 0.5f);
            header.GetComponent<Renderer>().sharedMaterial = frameMat;

            var panelGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelGo.name = "Panel";
            panelGo.transform.SetParent(root.transform, false);
            panelGo.transform.localPosition = new Vector3(0f, 2f, 0f);
            panelGo.transform.localScale = new Vector3(width - 0.2f, 3.8f, 0.16f);
            panelGo.GetComponent<Renderer>().sharedMaterial = BuildKit.MakeStandard(
                new Color(0.035f, 0.05f, 0.075f), 0.62f, 0.32f);

            // Keep the door physically legible while preserving the level's
            // holographic language as a shallow luminous face. Parenting the
            // face to the panel makes both pieces slide away together.
            BuildKit.SpawnLocal(PrimitiveType.Quad, "PanelGlow", panelGo.transform,
                new Vector3(0f, 0f, -0.51f), Vector3.zero, new Vector3(0.94f, 0.94f, 1f),
                BuildKit.MakeHologram(accent), collider: false);

            var door = root.AddComponent<LockedDoor>();
            door.lockedMessage = lockedMessage;
            door.doorwayWidth = width;
            door.panel = panelGo.transform;
            door.panelCollider = panelGo.GetComponent<Collider>();
            door.NormalizeGeometry();
            door.EnsureThreshold();

            var glow = new GameObject("DoorLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 3.4f, -1.4f);
            var dl = glow.AddComponent<Light>();
            dl.type = LightType.Point;
            dl.color = accent;
            dl.range = 9f;
            dl.intensity = 1.8f;

            BuildKit.MakeSign(root.transform, position + new Vector3(0f, 4.9f, 0f), signText, accent, 0.035f);
            return door;
        }

        private static void Post(Transform parent, Vector3 localPos, Material mat)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Post";
            post.transform.SetParent(parent, false);
            post.transform.localPosition = localPos;
            post.transform.localScale = new Vector3(0.5f, 4f, 0.6f);
            post.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
