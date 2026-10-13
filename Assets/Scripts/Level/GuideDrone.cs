using System.Collections.Generic;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Settings;
using static Cyverse.Level.BlockKit;

namespace Cyverse.Level
{
    /// <summary>
    /// A small companion drone that shows the way when the objective marker
    /// (ObjectiveBeacon's cube) is out of sight — off screen, behind the
    /// player, or behind a wall. It flies in at the edge of the view on the
    /// side the player should turn toward and points at the objective, then
    /// flies off again once the marker is back in view. Purely visual: no
    /// colliders, so it never blocks movement or the interact ray.
    /// </summary>
    public class GuideDrone : MonoBehaviour
    {
        public static GuideDrone Instance { get; private set; }

        // Hover spot in camera space: ahead, toward the side to turn, a little high.
        private const float Ahead = 1.35f, Side = 0.72f, Lift = 0.14f;
        // The marker must be out of sight this long before the drone comes in
        // (it's a last resort for a player who has lost the objective, not a
        // constant companion — the floor trail is the gentler first hint),
        // and back in sight this long before it leaves.
        public const float ShowAfter = 30f;
        private const float HideAfter = 0.7f;

        /// <summary>How long the objective marker has been out of sight.</summary>
        public float MarkerHiddenFor => hiddenFor;
        private static readonly Color Guide = new Color(0.90f, 0.66f, 0.14f);

        private Transform body, pointer, bladesL, bladesR;
        private float hiddenFor, seenFor, presence, side = 1f;
        private Vector3 velocity;
        private bool pointing;

        /// <summary>True while the drone is out, pointing the way.</summary>
        public bool IsPointing => pointing;

        public static GuideDrone Ensure()
        {
            if (Instance != null) return Instance;
            var found = FindObjectOfType<GuideDrone>();
            if (found != null) return Instance = found;
            var drone = new GameObject("GuideDrone").AddComponent<GuideDrone>();
            drone.BuildModel();
            GuideTrail.Ensure();
            return drone;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            if (body == null) return;
            Camera cam = Camera.main;
            ObjectiveBeacon beacon = ObjectiveBeacon.Instance;
            Vector3 marker = Vector3.zero;

            // Menus and full-screen panels have the player's attention; so do
            // markers close enough to have hidden themselves.
            bool menuUp = GameState.Busy && !GameState.DialogueActive;
            if (cam != null && beacon != null && beacon.MarkerShown && !menuUp)
            {
                marker = beacon.MarkerPosition;
                bool inView = MarkerInView(cam, marker, beacon.Target);
                // Behind a wall, point the way to walk (along the floor trail's
                // route) rather than straight through the wall at the marker.
                if (!inView && GuideTrail.Instance != null &&
                    !LineClear(cam.transform.position, marker, beacon.Target) &&
                    GuideTrail.Instance.TryGetHeading(cam.transform.position, out Vector3 heading))
                    marker = heading;
                hiddenFor = inView ? 0f : hiddenFor + Time.deltaTime;
                seenFor = inView ? seenFor + Time.deltaTime : 0f;
                pointing = pointing ? seenFor < HideAfter : hiddenFor > ShowAfter;
            }
            else
            {
                hiddenFor = seenFor = 0f;
                pointing = false;
            }

            float fade = AccessibilitySettings.ReduceMotion ? 0.05f : 0.35f;
            float before = presence;
            presence = Mathf.MoveTowards(presence, pointing ? 1f : 0f, Time.deltaTime / fade);
            body.gameObject.SetActive(presence > 0f);
            if (presence <= 0f || cam == null) return;

            // Which side to turn: decided by where the objective is relative to
            // the view, with some slack so a target straight behind doesn't
            // make the drone swap sides every frame.
            Vector3 local = cam.transform.InverseTransformPoint(marker);
            if (Mathf.Abs(local.x) > 0.6f || (local.z > 0f && Mathf.Abs(local.x) > 0.2f))
                side = Mathf.Sign(local.x);

            // Flies in from (and out to) beyond the edge of the view.
            float outward = 1f - Smooth(presence);
            Vector3 spot = new Vector3(side * (Side + outward * 0.9f), Lift, Ahead);
            Vector3 desired = cam.transform.TransformPoint(spot);
            desired = KeepOutOfWalls(cam.transform.position, desired);
            if (!AccessibilitySettings.ReduceMotion)
                desired += cam.transform.up * (Mathf.Sin(Time.time * 3.1f) * 0.018f);

            if (before <= 0f) transform.position = desired;
            else transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.10f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Smooth(presence));

            // Face the objective. A target behind the player is pointed at
            // sideways — an arrow pointing straight away from the camera
            // can't be read.
            Vector3 toward = cam.transform.InverseTransformDirection((marker - transform.position).normalized);
            if (toward.z < 0.25f)
            {
                toward.z = 0.25f;
                toward.x = side * Mathf.Max(Mathf.Abs(toward.x), 0.7f);
            }
            Vector3 look = cam.transform.TransformDirection(toward.normalized);
            Quaternion aim = Quaternion.LookRotation(look, Vector3.up);
            body.rotation = before <= 0f ? aim : Quaternion.Slerp(body.rotation, aim, Time.deltaTime * 10f);

            if (!AccessibilitySettings.ReduceMotion)
            {
                bladesL.Rotate(0f, 1400f * Time.deltaTime, 0f, Space.Self);
                bladesR.Rotate(0f, -1400f * Time.deltaTime, 0f, Space.Self);
                float pulse = 1f + Mathf.Sin(Time.time * 7f) * 0.12f;
                pointer.localScale = new Vector3(1f, 1f, pulse);
            }
        }

        /// <summary>The marker is on screen and nothing solid stands between
        /// the camera and it.</summary>
        private static bool MarkerInView(Camera cam, Vector3 marker, Transform target)
        {
            Vector3 v = cam.WorldToViewportPoint(marker);
            if (v.z <= 0f || v.x < 0.04f || v.x > 0.96f || v.y < 0.04f || v.y > 0.96f) return false;

            Vector3 from = cam.transform.position;
            Vector3 to = marker - from;
            foreach (RaycastHit hit in Physics.RaycastAll(from, to.normalized, Mathf.Max(0f, to.magnitude - 0.3f),
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (target != null && hit.collider.transform.IsChildOf(target)) continue;
                if (hit.collider.transform.IsChildOf(cam.transform.root)) continue;
                return false;
            }
            return true;
        }

        private static bool LineClear(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 d = to - from;
            foreach (RaycastHit hit in Physics.RaycastAll(from, d.normalized, Mathf.Max(0f, d.magnitude - 0.3f),
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (target != null && hit.collider.transform.IsChildOf(target)) continue;
                if (hit.collider is CharacterController) continue;
                return false;
            }
            return true;
        }

        /// <summary>Pulls the hover spot in front of a wall the player is
        /// standing against, so the drone never sinks into it.</summary>
        private static Vector3 KeepOutOfWalls(Vector3 eye, Vector3 desired)
        {
            Vector3 to = desired - eye;
            if (Physics.Raycast(eye, to.normalized, out RaycastHit hit, to.magnitude + 0.2f,
                    ~0, QueryTriggerInteraction.Ignore))
                return hit.point - to.normalized * 0.22f;
            return desired;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        // ---- Model ------------------------------------------------------------

        /// <summary>A little blocky drone in the MFA Specialist's style: white
        /// shell, dark visor with two square eyes, a rotor each side, an
        /// antenna, and the pointer (beam and chevron) out front along +Z.</summary>
        private void BuildModel()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            Material shell = Mat(new Color(0.84f, 0.87f, 0.91f), 0.45f, 0.1f);
            Material joint = Mat(new Color(0.13f, 0.14f, 0.17f), 0.4f, 0.3f);
            Material visor = Mat(new Color(0.025f, 0.04f, 0.075f), 0.7f, 0.1f);
            Material eyes = BuildKit.MakeEmissive(new Color(0.30f, 0.78f, 1f), 2.4f);
            Material gold = BuildKit.MakeEmissive(Guide, 2.2f);

            Block("DroneShell", body, Vector3.zero, Vector3.zero, -0.055f, 0.055f,
                new Vector2(0.085f, 0.07f), new Vector2(0.075f, 0.062f), 0.02f, shell);
            Slab("DroneVisor", body, new Vector3(0f, 0.004f, 0.066f), new Vector3(0.12f, 0.07f, 0.012f), 0.008f, visor);
            Batch("DroneEyes", body, eyes, new List<CombineInstance>
            {
                Piece(CubeMesh, new Vector3(-0.027f, 0.008f, 0.0735f), Vector3.zero, new Vector3(0.022f, 0.024f, 0.004f)),
                Piece(CubeMesh, new Vector3(0.027f, 0.008f, 0.0735f), Vector3.zero, new Vector3(0.022f, 0.024f, 0.004f)),
            });

            // Arms, rotor rings and the antenna mast, as one dark mesh.
            Mesh ring = CharacterMesh.Annulus(8, 0.034f, 0.048f, 0f, 0.008f, "RotorRing", 22.5f);
            Batch("DroneFrame", body, joint, new List<CombineInstance>
            {
                Piece(CubeMesh, new Vector3(-0.11f, 0.004f, 0f), Vector3.zero, new Vector3(0.05f, 0.014f, 0.02f)),
                Piece(CubeMesh, new Vector3(0.11f, 0.004f, 0f), Vector3.zero, new Vector3(0.05f, 0.014f, 0.02f)),
                Piece(ring, new Vector3(-0.15f, 0.008f, 0f), Vector3.zero, Vector3.one),
                Piece(ring, new Vector3(0.15f, 0.008f, 0f), Vector3.zero, Vector3.one),
                Piece(CubeMesh, new Vector3(0f, 0.075f, -0.02f), Vector3.zero, new Vector3(0.008f, 0.04f, 0.008f)),
            });
            bladesL = Blades("BladesL", new Vector3(-0.15f, 0.014f, 0f), joint);
            bladesR = Blades("BladesR", new Vector3(0.15f, 0.014f, 0f), joint);
            Box("AntennaTip", body, new Vector3(0f, 0.1f, -0.02f), new Vector3(0.02f, 0.02f, 0.02f), gold);

            // The pointer: a beam and a chevron, out front.
            pointer = new GameObject("Pointer").transform;
            pointer.SetParent(body, false);
            pointer.localPosition = new Vector3(0f, 0f, 0.08f);
            Batch("PointerMesh", pointer, gold, new List<CombineInstance>
            {
                Piece(CubeMesh, new Vector3(0f, 0f, 0.13f), Vector3.zero, new Vector3(0.012f, 0.012f, 0.24f)),
                Piece(CubeMesh, new Vector3(-0.0225f, 0f, 0.223f), new Vector3(0f, 40f, 0f), new Vector3(0.012f, 0.012f, 0.07f)),
                Piece(CubeMesh, new Vector3(0.0225f, 0f, 0.223f), new Vector3(0f, -40f, 0f), new Vector3(0.012f, 0.012f, 0.07f)),
            });

            BuildStream();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            body.gameObject.SetActive(false);
        }

        /// <summary>Gold motes streaming off the pointer's tip toward the
        /// objective; they stop with the drone (it lives on the body).</summary>
        private void BuildStream()
        {
            var go = new GameObject("PointerStream");
            go.transform.SetParent(pointer, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.startColor = Guide;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = AccessibilitySettings.ReduceMotion ? 10f : 32f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone; // emits along the pointer (+Z)
            shape.angle = 6f;
            shape.radius = 0.008f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            go.GetComponent<ParticleSystemRenderer>().material = GuideTrail.GlowMaterial();
            ps.Play();
        }

        private Transform Blades(string name, Vector3 at, Material mat)
        {
            var hub = new GameObject(name).transform;
            hub.SetParent(body, false);
            hub.localPosition = at;
            Batch(name + "Mesh", hub, mat, new List<CombineInstance>
            {
                Piece(CubeMesh, Vector3.zero, Vector3.zero, new Vector3(0.07f, 0.003f, 0.01f)),
                Piece(CubeMesh, Vector3.zero, new Vector3(0f, 90f, 0f), new Vector3(0.07f, 0.003f, 0.01f)),
            });
            return hub;
        }
    }
}
