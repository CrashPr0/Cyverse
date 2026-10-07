using System.Collections.Generic;
using UnityEngine;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Keeps a planter off gameplay geometry. Furnishing spots are shared by
    /// every room, so a planter can land on a station another level put there
    /// (Level 2's playbook pedestals run along the east wall through the
    /// (17.3, 17.3) corner spot). Once the level has finished building, the
    /// planter checks its footprint against colliders — stations, interaction
    /// triggers, walls — and steps to the nearest clear spot.
    ///
    /// Runs late (after Level*Polish passes, which build in Start at order 200)
    /// so it sees every station, and only once: planters never move after the
    /// first frame.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class PlanterClearance : MonoBehaviour
    {
        // Pot + crown envelope, a little generous so the crown never brushes a
        // station's sign or aim volume.
        private static readonly Vector3 HalfExtents = new Vector3(0.34f, 0.72f, 0.34f);
        private const float EnvelopeCenterY = 0.8f;
        private const float RoomHalfExtent = 18.6f;
        // Standing room kept around anything the player interacts with, so a
        // planter never crowds the spot a player stands in to use a station.
        private const float InteractableClearance = 2.0f;

        // Small nudges only: a larger jump lands planters in open floor that
        // players walk through, which is worse than no planter at all.
        private static readonly Vector3[] Offsets =
        {
            new Vector3(0f, 0f, -1.2f), new Vector3(0f, 0f, 1.2f),
            new Vector3(-1.2f, 0f, 0f), new Vector3(1.2f, 0f, 0f),
            new Vector3(-1.2f, 0f, -1.2f), new Vector3(1.2f, 0f, -1.2f),
            new Vector3(-1.2f, 0f, 1.2f), new Vector3(1.2f, 0f, 1.2f),
        };

        private readonly Collider[] hits = new Collider[16];
        private readonly List<Vector3> interactables = new List<Vector3>();

        private void Start()
        {
            Physics.SyncTransforms();
            foreach (MonoBehaviour behaviour in FindObjectsOfType<MonoBehaviour>())
                if (behaviour is IInteractable && !behaviour.transform.IsChildOf(transform))
                    interactables.Add(behaviour.transform.position);

            Vector3 origin = transform.position;
            if (!Blocked(origin)) return;

            foreach (Vector3 offset in Offsets)
            {
                Vector3 candidate = origin + offset;
                if (Mathf.Abs(candidate.x) > RoomHalfExtent || Mathf.Abs(candidate.z) > RoomHalfExtent) continue;
                if (Blocked(candidate)) continue;
                transform.position = candidate;
                return;
            }
            // Nowhere clear nearby: a hidden plant beats one standing in a station.
            gameObject.SetActive(false);
        }

        private bool Blocked(Vector3 floorPos)
        {
            foreach (Vector3 spot in interactables)
            {
                Vector3 d = spot - floorPos;
                d.y = 0f;
                if (d.sqrMagnitude < InteractableClearance * InteractableClearance) return true;
            }

            int count = Physics.OverlapBoxNonAlloc(floorPos + Vector3.up * EnvelopeCenterY, HalfExtents,
                hits, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit.transform.IsChildOf(transform)) continue;
                // Room-scale trigger volumes (zones, music areas) aren't props.
                if (hit.isTrigger && (hit.bounds.size.x > 4f || hit.bounds.size.z > 4f)) continue;
                if (hit is CharacterController) continue;
                return true;
            }
            return false;
        }
    }
}
