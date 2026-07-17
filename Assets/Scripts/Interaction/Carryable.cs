using System;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
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
            transform.SetParent(cam.transform, false);
            transform.localPosition = new Vector3(0.42f, -0.34f, 0.95f);
            transform.localRotation = Quaternion.Euler(8f, -14f, 0f);

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
            if (cam != null)
            {
                Vector3 fwd = cam.transform.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
                Vector3 pos = cam.transform.position + fwd * 1.4f;

                // Own colliders are still disabled here, so the ray can't hit us.
                pos.y = Physics.Raycast(new Vector3(pos.x, pos.y, pos.z), Vector3.down, out RaycastHit hit, 6f)
                    ? hit.point.y
                    : 0f;
                transform.position = pos;
            }
            transform.rotation = Quaternion.identity;
            foreach (var c in colliders) c.enabled = true;
            if (Carried == this) Carried = null;
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

            var carry = root.AddComponent<Carryable>();
            carry.itemName = itemName;
            carry.id = id;
            return carry;
        }
    }
}
