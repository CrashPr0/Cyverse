using UnityEngine;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.Settings;

namespace Cyverse.Player
{
    /// <summary>
    /// Procedural first-person hands: stylised gloved hands built from
    /// primitives (no art assets) and animated entirely in code —
    ///  - idle sway and speed-scaled walk bob (opposite phase per hand)
    ///  - the right hand lifts into a "ready" pose when an interactable is in view
    ///  - a quick reach-forward when the player presses interact
    ///  - hands tuck away while the menu or results screen is up
    /// Lives on the player camera. All motion respects Reduce Motion.
    /// </summary>
    public class FirstPersonHands : MonoBehaviour
    {
        public static FirstPersonHands Instance { get; private set; }

        [Header("Pose")]
        public Vector3 rightRestPosition = new Vector3(0.26f, -0.30f, 0.52f);

        [Header("Motion")]
        public float idleSway = 0.004f;
        public float walkBob = 0.013f;
        public float interactDuration = 0.35f;

        private Transform handL, handR;
        private CharacterController body;
        private float bobTime;
        private float interactT = 1f; // >= 1 means the reach animation is done
        private Vector3 hoverCur;
        private Vector3 hideCur;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            // Hands sit close to the lens; make sure they don't clip.
            var cam = GetComponent<Camera>();
            if (cam != null && cam.nearClipPlane > 0.1f) cam.nearClipPlane = 0.1f;

            handR = BuildHand(left: false);
            handL = BuildHand(left: true);
        }

        void Start()
        {
            body = GetComponentInParent<CharacterController>();
        }

        /// <summary>Called by PlayerInteractor when the player activates something.</summary>
        public void TriggerInteract()
        {
            if (interactT >= 1f) interactT = 0f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-10f * dt);
            bool reduce = AccessibilitySettings.ReduceMotion;

            float speed = 0f;
            if (body != null)
            {
                Vector3 v = body.velocity;
                v.y = 0f;
                speed = v.magnitude;
            }
            float speed01 = Mathf.Clamp01(speed / 4.5f);

            if (!reduce) bobTime += dt * Mathf.Lerp(1.6f, 7.5f, speed01);
            float amp = reduce ? 0f : Mathf.Lerp(idleSway, walkBob, speed01);

            // Classic figure-eight-ish bob, opposite phase per hand.
            Vector3 bobR = new Vector3(Mathf.Cos(bobTime * 0.5f), -Mathf.Abs(Mathf.Sin(bobTime)), 0f) * amp;
            Vector3 bobL = new Vector3(-Mathf.Cos(bobTime * 0.5f), -Mathf.Abs(Mathf.Sin(bobTime + Mathf.PI * 0.5f)), 0f) * amp;

            // Reach animation: 0 -> 1 -> 0 over interactDuration.
            if (interactT < 1f) interactT = Mathf.Min(1f, interactT + dt / interactDuration);
            float reach = Mathf.Sin(Mathf.PI * interactT);
            Vector3 reachOff = new Vector3(-0.04f, 0.05f, 0.15f) * reach;

            bool hover = PlayerInteractor.TargetInView && !GameState.Busy;
            hoverCur = Vector3.Lerp(hoverCur, hover ? new Vector3(-0.02f, 0.03f, 0.05f) : Vector3.zero, k);

            bool hidden = GameState.MenuOpen || GameState.LevelComplete;
            hideCur = Vector3.Lerp(hideCur, hidden ? new Vector3(0f, -0.4f, -0.1f) : Vector3.zero, k);

            Vector3 baseR = rightRestPosition;
            Vector3 baseL = Vector3.Scale(rightRestPosition, new Vector3(-1f, 1f, 1f));

            handR.localPosition = Vector3.Lerp(handR.localPosition,
                baseR + bobR + hoverCur + reachOff + hideCur, k * 1.6f);
            handL.localPosition = Vector3.Lerp(handL.localPosition,
                baseL + bobL + hideCur, k);

            float rollR = (reduce ? 0f : Mathf.Sin(bobTime * 0.5f) * 2f) - reach * 12f;
            handR.localRotation = Quaternion.Slerp(handR.localRotation,
                Quaternion.Euler(-6f - reach * 18f, -8f, rollR), k * 1.6f);
            handL.localRotation = Quaternion.Slerp(handL.localRotation,
                Quaternion.Euler(-6f, 8f, reduce ? 0f : -Mathf.Sin(bobTime * 0.5f) * 2f), k);
        }

        // ---- Construction ----------------------------------------------------

        private Transform BuildHand(bool left)
        {
            float m = left ? -1f : 1f;
            var root = new GameObject(left ? "HandL" : "HandR").transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.Scale(rightRestPosition, new Vector3(m, 1f, 1f));

            Material glove = SceneFactory.MakeStandard(new Color(0.09f, 0.10f, 0.14f), 0.45f, 0.2f);
            Material accent = SceneFactory.MakeEmissive(new Color(0.25f, 0.8f, 1f), 1.6f);

            Part(root, "Forearm", new Vector3(0f, -0.015f, -0.12f), new Vector3(6f, 0f, 0f),
                new Vector3(0.055f, 0.05f, 0.17f), glove);
            Part(root, "Palm", Vector3.zero, Vector3.zero,
                new Vector3(0.085f, 0.03f, 0.10f), glove);

            for (int i = 0; i < 4; i++)
            {
                float x = (-0.031f + i * 0.021f) * m;
                Part(root, "Finger" + i, new Vector3(x, 0.002f, 0.075f), new Vector3(22f, 0f, 0f),
                    new Vector3(0.017f, 0.017f, 0.06f), glove);
            }
            Part(root, "Thumb", new Vector3(-0.055f * m, -0.004f, 0.025f), new Vector3(12f, -38f * m, 0f),
                new Vector3(0.018f, 0.018f, 0.052f), glove);

            // CyberVerse glove chrome: glowing knuckle strip + wrist cuff.
            Part(root, "Knuckles", new Vector3(0f, 0.019f, 0.032f), Vector3.zero,
                new Vector3(0.07f, 0.006f, 0.012f), accent);
            Part(root, "WristCuff", new Vector3(0f, 0f, -0.05f), Vector3.zero,
                new Vector3(0.092f, 0.052f, 0.014f), accent);

            return root;
        }

        private static void Part(Transform parent, string name, Vector3 localPos,
            Vector3 localEuler, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c); // runtime-only construction
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
