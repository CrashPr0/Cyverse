using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Dialogue;
using Cyverse.Level;
using Cyverse.Settings;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The security guard from the CyVerse Script, standing near the spawn.
    /// Built from primitives (navy uniform, Spartan-gold belt and badge, cyan
    /// visor). Turns to face the player when they're near, breathes subtly,
    /// and when talked to gives directions appropriate to the current phase
    /// (review stations → go authenticate → welcome aboard).
    /// </summary>
    public class GuardNPC : MonoBehaviour, IInteractable
    {
        public float faceRange = 8f;
        public float turnSpeed = 3f;

        private Transform head;
        private float headBaseY;
        private float seed;

        public string Prompt => "Talk to the Security Guard";
        public bool CanInteract => true;

        void Start()
        {
            var h = transform.Find("Head");
            if (h != null) { head = h; headBaseY = h.localPosition.y; }
            seed = Random.Range(0f, 10f);
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 to = cam.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < faceRange * faceRange && to.sqrMagnitude > 0.01f)
                {
                    var look = Quaternion.LookRotation(to);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look,
                        1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
                }
            }

            if (head != null && !AccessibilitySettings.ReduceMotion)
            {
                var p = head.localPosition;
                p.y = headBaseY + Mathf.Sin((Time.time + seed) * 1.4f) * 0.006f;
                head.localPosition = p;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (DialogueManager.Instance == null) return;
            DialogueManager.Instance.Play(LinesForPhase());
        }

        private List<DialogueLine> LinesForPhase()
        {
            var phase = Level0Manager.Instance != null
                ? Level0Manager.Instance.CurrentPhase
                : Level0Manager.Phase.Review;

            switch (phase)
            {
                case Level0Manager.Phase.Authenticate:
                    return new List<DialogueLine>
                    {
                        new DialogueLine("Security Guard",
                            "All stations reviewed — nice work. Head to the Security Scanner and authenticate with a face scan to finish your onboarding.", null, 4f),
                    };
                case Level0Manager.Phase.Complete:
                    return new List<DialogueLine>
                    {
                        new DialogueLine("Security Guard",
                            "Welcome aboard, Cy95192. CyberVerse is glad to have you.", null, 3.5f),
                    };
                default:
                    return new List<DialogueLine>
                    {
                        new DialogueLine("Security Guard",
                            "Make your rounds, recruit: the I/AM Kiosk, the CIA Triad hologram, and the NICE Roles board. Walk up to each glowing station and press E.", null, 4.5f),
                        new DialogueLine("Security Guard",
                            "Answer the knowledge checks to earn points — and press G any time to open the glossary.", null, 4f),
                    };
            }
        }

        // ---- Construction ----------------------------------------------------

        /// <summary>Build the guard (edit- and play-mode safe). Used by
        /// SceneFactory.BuildAll and the editor menu.</summary>
        public static GameObject Build(Vector3 position, float rotY)
        {
            var root = new GameObject("GuardNPC");
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            Material uniform = SceneFactory.MakeStandard(new Color(0.09f, 0.12f, 0.20f), 0.35f, 0.1f);
            Material trim = SceneFactory.MakeStandard(new Color(0.06f, 0.07f, 0.10f), 0.4f, 0.2f);
            Material skin = SceneFactory.MakeStandard(new Color(0.72f, 0.55f, 0.44f), 0.25f, 0f);
            Material gold = SceneFactory.MakeEmissive(new Color(0.90f, 0.66f, 0.14f), 1.1f);
            Material visor = SceneFactory.MakeEmissive(new Color(0.25f, 0.8f, 1f), 1.8f);

            Part(root.transform, PrimitiveType.Cube, "LegL", new Vector3(-0.11f, 0.45f, 0f), new Vector3(0.16f, 0.9f, 0.18f), trim);
            Part(root.transform, PrimitiveType.Cube, "LegR", new Vector3(0.11f, 0.45f, 0f), new Vector3(0.16f, 0.9f, 0.18f), trim);
            Part(root.transform, PrimitiveType.Cube, "Belt", new Vector3(0f, 0.93f, 0f), new Vector3(0.38f, 0.07f, 0.24f), gold);
            Part(root.transform, PrimitiveType.Cube, "Torso", new Vector3(0f, 1.22f, 0f), new Vector3(0.40f, 0.52f, 0.24f), uniform);
            Part(root.transform, PrimitiveType.Cube, "Badge", new Vector3(0.12f, 1.32f, 0.125f), new Vector3(0.07f, 0.09f, 0.02f), gold);
            Part(root.transform, PrimitiveType.Cube, "ShoulderL", new Vector3(-0.27f, 1.42f, 0f), new Vector3(0.14f, 0.12f, 0.20f), uniform);
            Part(root.transform, PrimitiveType.Cube, "ShoulderR", new Vector3(0.27f, 1.42f, 0f), new Vector3(0.14f, 0.12f, 0.20f), uniform);
            Part(root.transform, PrimitiveType.Cube, "ArmL", new Vector3(-0.27f, 1.10f, 0f), new Vector3(0.11f, 0.44f, 0.14f), uniform);
            Part(root.transform, PrimitiveType.Cube, "ArmR", new Vector3(0.27f, 1.10f, 0f), new Vector3(0.11f, 0.44f, 0.14f), uniform);
            Part(root.transform, PrimitiveType.Cube, "Neck", new Vector3(0f, 1.51f, 0f), new Vector3(0.12f, 0.07f, 0.12f), skin);
            Part(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.66f, 0f), new Vector3(0.24f, 0.24f, 0.24f), skin);
            Part(root.transform, PrimitiveType.Cube, "Visor", new Vector3(0f, 1.68f, 0.105f), new Vector3(0.22f, 0.05f, 0.05f), visor);
            Part(root.transform, PrimitiveType.Cylinder, "Cap", new Vector3(0f, 1.80f, 0f), new Vector3(0.30f, 0.03f, 0.30f), uniform);
            Part(root.transform, PrimitiveType.Cube, "CapBrim", new Vector3(0f, 1.76f, 0.15f), new Vector3(0.24f, 0.02f, 0.12f), uniform);

            // One capsule collider on the root for the interact raycast.
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.95f, 0f);
            col.height = 1.9f;
            col.radius = 0.32f;

            root.AddComponent<GuardNPC>();

            SceneFactory.MakeSign(root.transform, position + new Vector3(0f, 2.25f, 0f),
                "SECURITY", new Color(0.90f, 0.66f, 0.14f), 0.022f);
            return root;
        }

        private static void Part(Transform parent, PrimitiveType type, string name,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var c = go.GetComponent<Collider>();
            if (c != null)
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
