using UnityEngine;
using Cyverse.Settings;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// The "where do I go now?" marker: a single small cube floating above the
    /// current objective. Levels call PointAt() whenever the next action
    /// changes; the beacon follows that transform, so it works for doors,
    /// kiosks or carried items alike.
    ///
    /// It used to be a full-height light pillar with a distance readout, which
    /// swallowed whatever it was pointing at — the marker has to be findable
    /// without obscuring the thing you walked over to read.
    ///
    /// Design intent: a player should never have to guess what to do next in a
    /// 40x40 room. The beacon answers "where", the HUD task list answers
    /// "what", and the interact prompt answers "how".
    ///
    /// It disappears once the player is within a couple of metres — it has
    /// done its job by then, and the interact prompt takes over.
    /// </summary>
    public class ObjectiveBeacon : MonoBehaviour
    {
        public static ObjectiveBeacon Instance { get; private set; }

        private Transform target;
        private float heightOffset = 2.6f;
        private float markerHeight;

        private Transform cube;
        private Renderer tinted;

        // Hide once the player is basically there; the HUD objective line and
        // the interact prompt take over at that range.
        private const float HideWithin = 2.5f;
        private const float MarkerClearance = 0.45f;
        private const float MarkerHalfExtent = 0.20f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Get (or build) the scene's beacon.</summary>
        public static ObjectiveBeacon Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindObjectOfType<ObjectiveBeacon>();
            if (existing != null) { Instance = existing; return existing; }
            return Build();
        }

        /// <summary>Aim the beacon at a target. Passing null hides it.</summary>
        public void PointAt(Transform newTarget, string action, Color color, float height = 2.9f)
        {
            bool placementChanged = target != newTarget || !Mathf.Approximately(heightOffset, height);
            target = newTarget;
            heightOffset = height;

            if (target == null) { SetVisible(false); return; }

            if (tinted != null)
            {
                var m = tinted.material;
                if (m.HasProperty("_Color")) m.SetColor("_Color", color);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 2f);
            }
            // Guidance refreshes several times per second; the placement only
            // needs a physics query when its destination actually changes.
            if (placementChanged) markerHeight = CalculateMarkerHeight();
            SetVisible(true);
            LateUpdate(); // place it this frame, no one-frame pop at the origin
        }

        public void Hide() => PointAt(null, null, Color.white);

        private void SetVisible(bool visible)
        {
            if (cube != null) cube.gameObject.SetActive(visible);
        }

        void LateUpdate()
        {
            if (target == null) return;

            transform.position = target.position;

            var cam = Camera.main;
            float dist = cam != null
                ? Vector3.Distance(cam.transform.position, target.position)
                : 99f;

            bool showing = dist > HideWithin;
            if (cube != null && cube.gameObject.activeSelf != showing)
                cube.gameObject.SetActive(showing);
            if (!showing || cube == null) return;

            float bob = AccessibilitySettings.ReduceMotion
                ? 0f
                : Mathf.Sin(Time.time * 2f) * 0.16f;
            cube.localPosition = new Vector3(0f, markerHeight + bob, 0f);
            if (!AccessibilitySettings.ReduceMotion)
                cube.Rotate(28f * Time.deltaTime, 46f * Time.deltaTime, 0f, Space.Self);
        }

        /// <summary>Places the marker above the visual target, then keeps it
        /// below any collider directly overhead. A constant offset alone puts
        /// the marker inside tall screens and server racks in the visual-pass
        /// scenes.</summary>
        private float CalculateMarkerHeight()
        {
            if (target == null) return heightOffset;

            float top = target.position.y;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled) top = Mathf.Max(top, renderer.bounds.max.y);
            foreach (var collider in target.GetComponentsInChildren<Collider>(true))
                if (collider.enabled) top = Mathf.Max(top, collider.bounds.max.y);

            float desired = Mathf.Max(heightOffset,
                top - target.position.y + MarkerClearance);
            float riseAboveTop = desired - (top - target.position.y);
            if (riseAboveTop <= 0f) return desired;

            Vector3 rayOrigin = new Vector3(target.position.x, top + 0.02f, target.position.z);
            foreach (var hit in Physics.RaycastAll(rayOrigin, Vector3.up,
                         riseAboveTop + MarkerHalfExtent, ~0, QueryTriggerInteraction.Ignore))
            {
                // The target's own colliders are part of the objective, not a ceiling.
                if (hit.collider.transform.IsChildOf(target)) continue;
                desired = Mathf.Min(desired,
                    hit.point.y - target.position.y - MarkerHalfExtent);
            }

            // Keep a nearby ceiling from pushing a marker down into the target.
            return Mathf.Max(0.65f, desired);
        }

        // ---- Construction ----------------------------------------------------

        private static ObjectiveBeacon Build()
        {
            var root = new GameObject("ObjectiveBeacon");
            var beacon = root.AddComponent<ObjectiveBeacon>();

            var cube = BuildKit.SpawnLocal(PrimitiveType.Cube, "Marker", root.transform,
                new Vector3(0f, 2.9f, 0f), new Vector3(20f, 0f, 20f),
                new Vector3(0.34f, 0.34f, 0.34f),
                BuildKit.MakeHologram(new Color(0.90f, 0.66f, 0.14f)), collider: false);

            beacon.cube = cube.transform;
            beacon.tinted = cube.GetComponent<Renderer>();
            beacon.SetVisible(false);
            return beacon;
        }
    }
}
