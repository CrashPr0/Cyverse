using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Makes the pass-through evidence locker read as a real opening between the
    /// SOC and the forensics lab, the way Super Mario 64's mirror room fakes its
    /// reflection: the levels are separate scenes, so behind the locker's wall we
    /// build a static copy of the OTHER room, offset so the two rooms share that
    /// wall. Looking through the locker from the SOC shows the forensics video
    /// room; from the lab, the SOC task room.
    ///
    /// The copy is built from the same room builders the real levels use, then
    /// stripped to visual components and isolated fill lights (so nothing in
    /// it can be reached, interacted with, or picked up by scene lookups), names
    /// prefixed "Mirror_" so GameObject.Find never returns it. Only the half of the
    /// room the opening faces is kept, and it renders only while the player is
    /// near the locker and looking its way.
    /// </summary>
    public sealed class LockerSightline : MonoBehaviour
    {
        public const string RootName = "LockerMirrorRoom";

        // The other room sits one room-length beyond the shared wall.
        private const float RoomSpan = 40f;

        // Opening through the wall in the locker's frame; matches the chamber liners.
        public const float HoleHalfWidth = 0.48f;
        public const float HoleBottom = 1.04f;
        public const float HoleTop = 1.91f;

        private const float ShowRadius = 10f;

        private GameObject replica;
        private GameObject standIn;
        private float checkTimer;

        /// <summary>True for anything inside a mirror room, so systems that scan
        /// the whole scene (world-text layout, audits) can skip it.</summary>
        public static bool IsReplica(Transform t) => t != null && t.root.name == RootName;

        public static void Build(Transform locker, EvidenceLocker.Side side)
        {
            if (!Application.isPlaying || locker == null) return;
            LockerSightline sightline = locker.GetComponent<LockerSightline>();
            if (sightline != null && sightline.replica != null) return;

            bool socSide = side == EvidenceLocker.Side.Soc;
            GameObject wall = GameObject.Find(socSide ? "Wall_North" : "Wall_South");
            float farFace = 1f;
            if (wall != null)
            {
                Renderer wallRenderer = wall.GetComponent<Renderer>();
                Bounds b = wallRenderer != null ? wallRenderer.bounds : new Bounds(wall.transform.position, wall.transform.lossyScale);
                farFace = Mathf.Abs(locker.InverseTransformPoint(new Vector3(locker.position.x, 1.5f,
                    socSide ? b.max.z : b.min.z)).z);
                CutOpening(wall, b, locker.position.x);
            }
            BuildSleeve(locker, farFace);

            if (sightline == null) sightline = locker.gameObject.AddComponent<LockerSightline>();
            sightline.replica = socSide ? BuildForensicsReplica() : BuildSocReplica();
            ClearTunnelExit(sightline.replica.transform, locker, farFace);
            sightline.replica.SetActive(false);

            // Close the far mouth itself when the replica is culled. A card
            // further behind it can be missed by rays through the sleeve at
            // an oblique angle, exposing the void beyond the room.
            Color tone = socSide ? new Color(0.05f, 0.11f, 0.08f) : new Color(0.12f, 0.06f, 0.06f);
            sightline.standIn = BuildKit.SpawnLocal(PrimitiveType.Quad, "SightlineStandIn", locker,
                new Vector3(0f, (HoleTop + HoleBottom) * 0.5f, farFace + 0.02f), Vector3.zero,
                new Vector3(HoleHalfWidth * 2f + 0.08f, HoleTop - HoleBottom + 0.08f, 1f),
                BuildKit.MakeEmissive(tone, 0.6f), collider: false);
        }

        private void Update()
        {
            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f || replica == null) return;
            checkTimer = 0.2f;

            Camera cam = Camera.main;
            bool show = false;
            if (cam != null)
            {
                Vector3 to = transform.position - cam.transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                Vector3 fwd = cam.transform.forward;
                fwd.y = 0f;
                show = dist < ShowRadius && (dist < 3f ||
                    (fwd.sqrMagnitude > 0.001f && Vector3.Dot(fwd.normalized, to / dist) > 0.25f));
            }
            if (replica.activeSelf != show) replica.SetActive(show);
            if (standIn != null && standIn.activeSelf == show) standIn.SetActive(!show);
        }

        private void OnDestroy()
        {
            if (replica != null) Destroy(replica);
        }

        // ---- The opening ---------------------------------------------------------

        /// <summary>Swap the wall's single mesh for four slabs framing the hole. The
        /// original collider stays, invisible, so the player and interact rays still
        /// stop at the wall.</summary>
        private static void CutOpening(GameObject wall, Bounds b, float holeX)
        {
            Renderer original = wall.GetComponent<Renderer>();
            if (original == null) return;
            Material mat = original.sharedMaterial;
            original.enabled = false;

            var cut = new GameObject(wall.name + "_Cut").transform;
            cut.SetParent(wall.transform.parent, false);
            float x0 = holeX - HoleHalfWidth, x1 = holeX + HoleHalfWidth;
            Slab(cut, "Left", b.min.x, x0, b.min.y, b.max.y, b, mat);
            Slab(cut, "Right", x1, b.max.x, b.min.y, b.max.y, b, mat);
            Slab(cut, "Below", x0, x1, b.min.y, HoleBottom, b, mat);
            Slab(cut, "Above", x0, x1, HoleTop, b.max.y, b, mat);
        }

        private static void Slab(Transform parent, string name, float x0, float x1, float y0, float y1, Bounds b, Material mat)
        {
            if (x1 - x0 < 0.001f || y1 - y0 < 0.001f) return;
            BuildKit.Spawn(PrimitiveType.Cube, name, parent,
                new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, b.center.z),
                new Vector3(x1 - x0, y1 - y0, b.size.z), mat, collider: false);
        }

        /// <summary>Line the hole through the wall so it reads as the locker's
        /// metal sleeve, with the far side's glass and frame at the other face.</summary>
        private static void BuildSleeve(Transform locker, float farFace)
        {
            Material dark = BuildKit.MakeStandard(new Color(0.03f, 0.04f, 0.055f), 0.5f, 0.3f);
            Material frameGlow = BuildKit.MakeEmissive(new Color(0.90f, 0.66f, 0.14f), 1.1f);
            float z0 = -0.13f, z1 = farFace;
            float len = z1 - z0, mid = (z0 + z1) * 0.5f;
            float h = HoleTop - HoleBottom, cy = (HoleTop + HoleBottom) * 0.5f;

            Part(locker, "Sleeve_L", new Vector3(-HoleHalfWidth, cy, mid), new Vector3(0.02f, h, len), dark);
            Part(locker, "Sleeve_R", new Vector3(HoleHalfWidth, cy, mid), new Vector3(0.02f, h, len), dark);
            Part(locker, "Sleeve_Top", new Vector3(0f, HoleTop, mid), new Vector3(HoleHalfWidth * 2f, 0.02f, len), dark);
            Part(locker, "Sleeve_Floor", new Vector3(0f, HoleBottom - 0.01f, mid), new Vector3(HoleHalfWidth * 2f, 0.02f, len), dark);

            // The other locker's window frame, seen from inside: thin gold edges.
            float fz = z1 - 0.02f;
            Part(locker, "FarFrame_L", new Vector3(-HoleHalfWidth + 0.02f, cy, fz), new Vector3(0.012f, h - 0.04f, 0.01f), frameGlow);
            Part(locker, "FarFrame_R", new Vector3(HoleHalfWidth - 0.02f, cy, fz), new Vector3(0.012f, h - 0.04f, 0.01f), frameGlow);
            Part(locker, "FarFrame_Bottom", new Vector3(0f, HoleBottom + 0.02f, fz), new Vector3(HoleHalfWidth * 2f - 0.04f, 0.012f, 0.01f), frameGlow);

        }

        private static void Part(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat) =>
            BuildKit.SpawnLocal(PrimitiveType.Cube, name, parent, localPos, Vector3.zero, scale, mat, collider: false);

        // ---- The replicas --------------------------------------------------------

        /// <summary>The forensics lab's video room, seen from the SOC.</summary>
        private static GameObject BuildForensicsReplica()
        {
            Color green = Level3ForensicsSceneFactory.ForensicGreen;
            GameObject root = Replicate(() =>
            {
                BuildKit.BuildFloor(green, new Color(0.05f, 0.08f, 0.06f));
                BuildKit.BuildWalls(BuildKit.WallColor);
                BuildKit.BuildCeilingPanels();
                BuildKit.BuildWallDetail(green);
                BuildKit.BuildNeonTrim(green);
                PropFactory.BuildFurnishings();
                Level3ForensicsSceneFactory.BuildDivider();
                VideoStation.Build(new Vector3(0f, 0f, -6f), 0f, Forensics.InvestigationCase.BriefingSlides(), green);
                LockedDoor.Build(new Vector3(0f, 0f, 2f), 0f, 3f, "FORENSICS LAB", "", green);
            }, keep: b => b.min.z < 2.6f, sharedWall: "Wall_South", offsetZ: RoomSpan);

            ShowFirstSlide(root, Forensics.InvestigationCase.BriefingSlides());
            ToneFloor(root, new Color(0.020f, 0.035f, 0.028f), new Color(0.08f, 0.38f, 0.20f));
            // Same ceiling tone the lab's own polish pass applies.
            ToneCeiling(root, BuildKit.MakeEmissive(new Color(0.48f, 0.64f, 0.57f), 0.58f));
            AddFill(root, new Color(0.45f, 1f, 0.65f), 1f);
            return root;
        }

        /// <summary>The SOC task room, seen from the forensics lab.</summary>
        private static GameObject BuildSocReplica()
        {
            Color red = Level2SceneFactory.SocRed;
            GameObject root = Replicate(() =>
            {
                BuildKit.BuildFloor(red, new Color(0.07f, 0.05f, 0.05f));
                BuildKit.BuildWalls(BuildKit.WallColor);
                BuildKit.BuildCeilingPanels();
                BuildKit.BuildWallDetail(red);
                BuildKit.BuildNeonTrim(red);
                PropFactory.BuildFurnishings();
                Level2SceneFactory.BuildDivider();
                LockedDoor.Build(new Vector3(0f, 0f, 2f), 0f, 3f, "SOC FLOOR", "", red);
                CertExamStation.Build(new Vector3(8f, 0f, 16f), 0f, Level2Content.ExamQuestions(), red);
            }, keep: b => b.max.z > 1.4f, sharedWall: "Wall_North", offsetZ: -RoomSpan);

            ToneFloor(root, new Color(0.035f, 0.025f, 0.030f), new Color(0.46f, 0.11f, 0.08f));
            ToneCeiling(root, BuildKit.MakeEmissive(new Color(0.52f, 0.60f, 0.70f), 0.62f));
            AddFill(root, new Color(1f, 0.55f, 0.45f), -1f);
            return root;
        }

        /// <summary>Run room builders, gather what they created, strip it to pure
        /// visuals, drop the half the opening can't see and the wall the two rooms
        /// share, then slide it one room-length away.</summary>
        private static GameObject Replicate(System.Action build, System.Func<Bounds, bool> keep,
            string sharedWall, float offsetZ)
        {
            Scene scene = SceneManager.GetActiveScene();
            var before = new HashSet<GameObject>(scene.GetRootGameObjects());
            build();

            var root = new GameObject(RootName);
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (before.Contains(go) || go == root) continue;
                bool isSharedWall = go.name == sharedWall;
                StripToVisuals(go);
                if (isSharedWall) { DestroyImmediate(go); continue; }
                go.transform.SetParent(root.transform, true);
            }

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (r != null && !keep(r.bounds) && r.gameObject != root) DestroyImmediate(r.gameObject);

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root.transform) t.name = "Mirror_" + t.name;

            root.transform.position = new Vector3(0f, 0f, offsetZ);
            return root;
        }

        private static readonly System.Type[] KeptBehaviours = { typeof(TMP_Text), typeof(Billboard), typeof(SignFX) };

        private static void StripToVisuals(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (Light l in go.GetComponentsInChildren<Light>(true)) DestroyImmediate(l);
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                bool keepIt = false;
                foreach (System.Type t in KeptBehaviours)
                    if (t.IsInstanceOfType(mb)) { keepIt = true; break; }
                if (!keepIt) DestroyImmediate(mb);
            }
        }

        /// <summary>The briefing screen's text is normally filled in by its
        /// (now stripped) player; show the idle first slide instead.</summary>
        private static void ShowFirstSlide(GameObject root, VideoStation.Slide[] slides)
        {
            if (slides == null || slides.Length == 0) return;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Mirror_TitleText") text.text = slides[0].title;
                else if (text.name == "Mirror_BodyText") text.text = slides[0].body;
            }
        }

        private static void ToneFloor(GameObject root, Color baseColor, Color lineColor)
        {
            Transform floor = root.transform.Find("Mirror_Floor");
            Material m = floor != null ? floor.GetComponent<Renderer>()?.sharedMaterial : null;
            BuildKit.ToneGridFloor(m, baseColor, lineColor);
        }

        private static void ToneCeiling(GameObject root, Material ceiling)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("Mirror_CeilingPanel_")) r.sharedMaterial = ceiling;
        }

        /// <summary>The other room's own light, so it reads in its colours rather
        /// than this level's. Near fills stop half a metre beyond the shared
        /// wall (|z| = 20), so the view is legible without lighting the real room.</summary>
        private static void AddFill(GameObject root, Color color, float side)
        {
            Vector3[] spots = { new Vector3(-4f, 3.4f, 26.5f), new Vector3(7f, 3.4f, 26.5f), new Vector3(0f, 3.4f, 35f) };
            foreach (Vector3 spot in spots)
            {
                var go = new GameObject("Mirror_Fill");
                go.transform.SetParent(root.transform, true);
                go.transform.position = new Vector3(spot.x, spot.y, spot.z * side);
                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.range = Mathf.Abs(spot.z) < 30f ? 6f : 9f;
                light.intensity = Mathf.Abs(spot.z) < 30f ? 1.6f : 1.1f;
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>Nothing in the copy may sit in the far mouth of the opening.</summary>
        private static void ClearTunnelExit(Transform replica, Transform locker, float farFace)
        {
            Vector3 a = locker.TransformPoint(new Vector3(-HoleHalfWidth, HoleBottom, farFace));
            Vector3 c = locker.TransformPoint(new Vector3(HoleHalfWidth, HoleTop, farFace + 1.2f));
            var mouth = new Bounds((a + c) * 0.5f, new Vector3(Mathf.Abs(c.x - a.x), Mathf.Abs(c.y - a.y), Mathf.Abs(c.z - a.z)));
            foreach (Renderer r in replica.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.name == "Mirror_Floor") continue;
                if (r.bounds.Intersects(mouth)) DestroyImmediate(r.gameObject);
            }
        }
    }
}
