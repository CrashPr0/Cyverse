using UnityEngine;
using Cyverse.Settings;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// The "where do I go now?" marker: a light pillar with a spinning diamond
    /// and a floating label above the current objective, plus live distance.
    /// Levels call PointAt() whenever the next action changes; the beacon
    /// follows that transform, so it works for doors, kiosks, or carried
    /// items alike.
    ///
    /// Design intent: a player should never have to guess what to do next in a
    /// 40x40 room. The beacon answers "where", the HUD task list answers
    /// "what", and the interact prompt answers "how".
    ///
    /// It fades out as the player closes in (it has done its job by then, and
    /// a pillar in your face obscures the thing you walked to).
    /// </summary>
    public class ObjectiveBeacon : MonoBehaviour
    {
        public static ObjectiveBeacon Instance { get; private set; }

        private Transform target;
        private float heightOffset = 2.6f;

        private Transform pillar, diamond;
        private TextMesh label;
        private Renderer[] tinted;
        private string actionText = "";

        private const float FadeNear = 3.5f;  // fully faded
        private const float FadeFar = 7f;     // fully visible

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
        public void PointAt(Transform newTarget, string action, Color color, float height = 2.6f)
        {
            target = newTarget;
            actionText = action;
            heightOffset = height;

            if (target == null) { SetVisible(false); return; }

            foreach (var r in tinted)
            {
                if (r == null) continue;
                var m = r.material;
                if (m.HasProperty("_Color")) m.SetColor("_Color", color);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 2f);
            }
            if (label != null) label.color = color;
            SetVisible(true);
            LateUpdate(); // place it this frame, no one-frame pop at the origin
        }

        public void Hide() => PointAt(null, null, Color.white);

        private void SetVisible(bool visible)
        {
            if (pillar != null) pillar.gameObject.SetActive(visible);
            if (diamond != null) diamond.gameObject.SetActive(visible);
            if (label != null) label.gameObject.SetActive(visible);
        }

        void LateUpdate()
        {
            if (target == null) return;

            transform.position = target.position;

            var cam = Camera.main;
            float dist = cam != null
                ? Vector3.Distance(cam.transform.position, target.position)
                : 99f;

            // Fade as the player arrives.
            float alpha = Mathf.Clamp01((dist - FadeNear) / (FadeFar - FadeNear));
            bool showing = alpha > 0.02f;
            if (pillar != null && pillar.gameObject.activeSelf != showing)
                pillar.gameObject.SetActive(showing);
            if (diamond != null && diamond.gameObject.activeSelf != showing)
                diamond.gameObject.SetActive(showing);

            if (label != null)
            {
                label.gameObject.SetActive(showing);
                if (showing)
                {
                    label.text = $"{actionText}\n{Mathf.RoundToInt(dist)}m";
                    Color c = label.color;
                    c.a = alpha;
                    label.color = c;
                    label.transform.position = target.position + Vector3.up * (heightOffset + 1.5f);
                }
            }

            if (!showing) return;

            if (pillar != null)
                pillar.localPosition = new Vector3(0f, heightOffset * 0.5f, 0f);

            if (diamond != null)
            {
                float bob = AccessibilitySettings.ReduceMotion
                    ? 0f
                    : Mathf.Sin(Time.time * 2f) * 0.18f;
                diamond.localPosition = new Vector3(0f, heightOffset + bob, 0f);
                if (!AccessibilitySettings.ReduceMotion)
                    diamond.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
            }
        }

        // ---- Construction ----------------------------------------------------

        private static ObjectiveBeacon Build()
        {
            var root = new GameObject("ObjectiveBeacon");
            var beacon = root.AddComponent<ObjectiveBeacon>();

            var mat = BuildKit.MakeHologram(new Color(0.30f, 1f, 0.55f));

            var pillar = BuildKit.SpawnLocal(PrimitiveType.Cylinder, "Pillar", root.transform,
                new Vector3(0f, 1.3f, 0f), Vector3.zero, new Vector3(0.28f, 1.3f, 0.28f), mat, collider: false);

            // A cube on its corner reads as a diamond/waypoint gem.
            var diamond = BuildKit.SpawnLocal(PrimitiveType.Cube, "Diamond", root.transform,
                new Vector3(0f, 2.6f, 0f), new Vector3(45f, 0f, 45f), new Vector3(0.42f, 0.42f, 0.42f),
                mat, collider: false);

            beacon.pillar = pillar.transform;
            beacon.diamond = diamond.transform;
            beacon.tinted = new[] { pillar.GetComponent<Renderer>(), diamond.GetComponent<Renderer>() };

            beacon.label = BuildKit.MakeLabel(root.transform, new Vector3(0f, 4.1f, 0f),
                "", new Color(0.30f, 1f, 0.55f), 0.03f, billboard: true);

            beacon.SetVisible(false);
            return beacon;
        }
    }
}
