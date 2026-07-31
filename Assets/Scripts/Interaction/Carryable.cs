using System;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// An item the player can pick up (E), walk around with, and either place
    /// on a DropZone (E while looking at it) or put back down (Q). One item at
    /// a time; while carried its colliders are disabled so the interact ray
    /// passes through to drop zones. The optional gate lets a level refuse
    /// pickup until some condition holds (e.g. "enroll your badge first").
    /// </summary>
    public class Carryable : MonoBehaviour, IInteractable
    {
        /// <summary>The item currently in the player's hands, if any.</summary>
        public static Carryable Carried { get; private set; }

        public string itemName = "ITEM";
        public string id = "item";
        public Func<bool> gate;
        public string gateMessage = "You can't take this yet.";

        private Collider[] colliders;
        private static bool hintShown;

        public bool CanInteract => Carried == null;
        public string Prompt => $"Pick up {itemName}";

        void Awake()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            hintShown = false; // static survives disabled domain reload
        }

        void OnDestroy()
        {
            if (Carried == this) Carried = null;
        }

        /// <summary>Drop the static carry reference. Statics survive scene
        /// reloads (and Play-mode restarts with domain reload disabled), so
        /// level managers clear it in Awake or a replay can start "holding"
        /// an object that no longer exists.</summary>
        public static void ClearCarried() => Carried = null;

        public void Interact(GameObject interactor)
        {
            if (Carried != null) return;
            if (gate != null && !gate())
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast(gateMessage, new Color(1f, 0.55f, 0.4f));
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;

            Carried = this;
            foreach (var c in colliders) c.enabled = false;
            var hands = FirstPersonHands.Instance;
            transform.SetParent(hands != null ? hands.CarryAnchor : cam.transform, false);
            transform.localPosition = hands != null
                ? Vector3.zero
                : new Vector3(0.42f, -0.34f, 0.95f);
            transform.localRotation = hands != null
                ? Quaternion.identity
                : Quaternion.Euler(8f, -14f, 0f);
            // In-hand presentation: smaller, and no name tag in your face.
            transform.localScale = Vector3.one * 0.6f;
            SetLabelVisible(false);

            if (!hintShown && HudUI.Instance != null)
            {
                hintShown = true;
                HudUI.Instance.ShowToast(
                    $"Carrying {itemName} — deliver it, or press Q to put it down",
                    new Color(0.70f, 0.85f, 1f));
            }
        }

        void Update()
        {
            if (Carried != this || GameState.Busy) return;
            if (Input.GetKeyDown(KeyCode.Q)) PutDown();
        }

        /// <summary>Set the item down in front of the player, on whatever
        /// surface is underfoot there (items start elevated on tables/racks,
        /// so their original height is not a valid rest height).</summary>
        public void PutDown()
        {
            var cam = Camera.main;
            transform.SetParent(null, true);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            if (cam != null && TryFindDropPosition(cam, out Vector3 dropPosition))
            {
                transform.position = dropPosition;
            }
            else if (cam != null)
            {
                // Never force an object into a wall just because the player is
                // standing in a cramped corner. Keep it in hand until there is
                // a valid resting place.
                var hands = FirstPersonHands.Instance;
                transform.SetParent(hands != null ? hands.CarryAnchor : cam.transform, false);
                transform.localPosition = hands != null ? Vector3.zero : new Vector3(0.42f, -0.34f, 0.95f);
                transform.localRotation = hands != null ? Quaternion.identity : Quaternion.Euler(8f, -14f, 0f);
                transform.localScale = Vector3.one * 0.6f;
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("No clear surface here — move away from the obstacle.",
                        new Color(1f, 0.70f, 0.35f));
                return;
            }

            SetLabelVisible(true);
            foreach (var c in colliders) c.enabled = true;
            if (Carried == this) Carried = null;
        }

        /// <summary>Finds a grounded, non-overlapping location in a small fan
        /// in front of the player. The item colliders stay disabled while this
        /// runs, so its own bounds never make an otherwise safe spot fail.</summary>
        private bool TryFindDropPosition(Camera cam, out Vector3 dropPosition)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float[] lateralOffsets = { 0f, -0.55f, 0.55f, -1.0f, 1.0f };
            float[] distances = { 1.4f, 1.8f, 1.05f };

            foreach (float distance in distances)
            foreach (float lateral in lateralOffsets)
            {
                Vector3 candidate = cam.transform.position + forward * distance + right * lateral;
                Vector3 rayOrigin = candidate + Vector3.up * 0.75f;
                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 7f,
                                     ~0, QueryTriggerInteraction.Ignore))
                    continue;

                candidate.y = hit.point.y;
                transform.position = candidate;
                Bounds bounds = SolidBounds();
                // Rest the true bottom of a token or crate on the surface.
                candidate.y -= bounds.min.y - candidate.y;
                transform.position = candidate;
                bounds = SolidBounds();

                // Shrink very slightly so a crate resting flush on the floor
                // is not rejected due to floating-point contact noise.
                if (!Physics.CheckBox(bounds.center, bounds.extents * 0.96f,
                                      Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    dropPosition = candidate;
                    return true;
                }
            }

            dropPosition = default;
            return false;
        }

        private Bounds SolidBounds()
        {
            bool hasBounds = false;
            Bounds result = new Bounds(transform.position, Vector3.zero);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                // The name tag deliberately floats above the item and should
                // not turn a low shelf into an invalid drop surface.
                if (!renderer.enabled || renderer.GetComponent<TextMesh>() != null) continue;
                if (!hasBounds) { result = renderer.bounds; hasBounds = true; }
                else result.Encapsulate(renderer.bounds);
            }
            return hasBounds ? result : new Bounds(transform.position + Vector3.up * 0.25f,
                Vector3.one * 0.5f);
        }

        private void SetLabelVisible(bool visible)
        {
            var tm = GetComponentInChildren<TextMesh>(true);
            if (tm != null) tm.gameObject.SetActive(visible);
        }

        /// <summary>Delivered: remove from the world (drop zones call this).</summary>
        public void Consume()
        {
            if (Carried == this) Carried = null;
            Destroy(gameObject);
        }

        // ---- Construction ----------------------------------------------------

        /// <summary>A labelled data crate (or, with token=true, a small security
        /// token puck) sitting at the given position.</summary>
        public static Carryable Build(Vector3 pos, string itemName, string id, Color accent,
            bool token = false)
        {
            var root = new GameObject("Carryable_" + id);
            root.transform.position = pos;

            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);
            if (token)
            {
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Puck", root.transform,
                    new Vector3(0f, 0.10f, 0f), Vector3.zero, new Vector3(0.26f, 0.09f, 0.26f), bodyMat, collider: true);
                BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Core", root.transform,
                    new Vector3(0f, 0.20f, 0f), Vector3.zero, new Vector3(0.10f, 0.02f, 0.10f),
                    BuildKit.MakeEmissive(accent, 2.2f), collider: false);
            }
            else
            {
                BuildKit.SpawnLocal(PrimitiveType.Cube, "Box", root.transform,
                    new Vector3(0f, 0.26f, 0f), Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), bodyMat, collider: true);
                BuildKit.SpawnLocal(PrimitiveType.Cube, "Stripe", root.transform,
                    new Vector3(0f, 0.53f, 0f), Vector3.zero, new Vector3(0.52f, 0.05f, 0.52f),
                    BuildKit.MakeEmissive(accent, 1.6f), collider: false);
            }

            BuildKit.MakeLabel(root.transform, new Vector3(0f, token ? 0.55f : 0.85f, 0f),
                itemName, accent, 0.022f, billboard: true);

            // Aim helper, same reason as DropZone. It's captured by the
            // collider list below, so it's disabled while carried and can't
            // block the ray to whatever the player wants to place it on.
            BuildKit.AddAimCollider(root, height: token ? 1.2f : 1.4f, width: 0.9f);

            var carry = root.AddComponent<Carryable>();
            carry.itemName = itemName;
            carry.id = id;
            return carry;
        }
    }
}
