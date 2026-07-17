using System;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// A pedestal that receives Carryables. Only interactable while the player
    /// is holding something; the owner (MFA vault, sorting station) supplies
    /// the accepts predicate and handles accepted/rejected — this component is
    /// pure plumbing so it stays reusable across levels.
    /// </summary>
    public class DropZone : MonoBehaviour, IInteractable
    {
        public string zoneName = "SLOT";
        public bool active = true;
        public Func<Carryable, bool> accepts;
        public Action<Carryable> onAccepted;
        public Action<Carryable> onRejected;

        public bool CanInteract => active && Carryable.Carried != null;
        public string Prompt => Carryable.Carried != null
            ? $"Place {Carryable.Carried.itemName} — {zoneName}"
            : zoneName;

        public void Interact(GameObject interactor)
        {
            var item = Carryable.Carried;
            if (item == null || !active) return;

            if (accepts == null || accepts(item))
            {
                onAccepted?.Invoke(item);
            }
            else
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                onRejected?.Invoke(item);
            }
        }

        // ---- Construction ----------------------------------------------------

        public static DropZone Build(Vector3 pos, string zoneName, Color accent)
        {
            var root = new GameObject("DropZone_" + zoneName.Replace(' ', '_'));
            root.transform.position = pos;

            BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Pedestal", root.transform,
                new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.55f, 0.5f, 0.55f),
                BuildKit.MakeStandard(new Color(0.09f, 0.10f, 0.14f), 0.5f, 0.4f), collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cylinder, "TopGlow", root.transform,
                new Vector3(0f, 1.01f, 0f), Vector3.zero, new Vector3(0.48f, 0.01f, 0.48f),
                BuildKit.MakeHologram(accent), collider: false);
            BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.55f, 0f),
                zoneName, accent, 0.026f, billboard: true);

            var zone = root.AddComponent<DropZone>();
            zone.zoneName = zoneName;
            return zone;
        }
    }
}
