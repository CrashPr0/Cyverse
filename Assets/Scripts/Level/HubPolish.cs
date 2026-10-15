using System.Collections.Generic;
using UnityEngine;
using Cyverse.Interaction;
using static Cyverse.Level.BlockKit;

namespace Cyverse.Level
{
    /// <summary>
    /// Brings the hand-saved Hub scene into the blocky, chamfered art direction
    /// of Level 1 at load time, without touching the scene file (which other
    /// people edit): the concierge becomes a service robot like the MFA
    /// Specialist, the holo core becomes a faceted crystal over an octagonal
    /// plinth, the level doors get built-out frames and wall-mounted signs, and
    /// the mission board gets a matching stand. The old primitives are hidden,
    /// their colliders left in place, so nothing about movement or interaction
    /// changes. Safe to run once per scene load; skips anything it can't find.
    /// </summary>
    public class HubPolish : MonoBehaviour
    {
        private static Color Gold => Palette.Accent("Hub");
        private static readonly Color Body = new Color(0.075f, 0.085f, 0.115f);
        private static readonly Color Trim = new Color(0.17f, 0.19f, 0.25f);
        private static readonly Color Chrome = new Color(0.58f, 0.61f, 0.68f);

        public static void Apply(GameObject host)
        {
            if (host.GetComponent<HubPolish>() != null) return;
            host.AddComponent<HubPolish>().Run();
        }

        private void Run()
        {
            Concierge(Gold, "Concierge", "Sign_INFORMATION");
            Core(Gold, "Sign_CYVERSE_HUB");
            Dais();
            foreach (HubDoor door in FindObjectsOfType<HubDoor>()) Door(door);
            MissionBoardStand();
            Wayfinding();
            Banners();
            AmbientDrones();
            StillFloor();
            // The lounges (couches and rugs) are left as they are: they are
            // being reworked separately in the saved scene.
        }

        /// <summary>The Hub floor keeps its gold grid and gloss but loses the
        /// ring that pulsed outward from the centre every few seconds (the
        /// other levels' floors already have it off). The material is
        /// instanced, so the saved scene's floor is never edited.</summary>
        private static void StillFloor()
        {
            Renderer floor = GameObject.Find("Floor") != null ? GameObject.Find("Floor").GetComponent<Renderer>() : null;
            if (floor == null || floor.sharedMaterial == null || !floor.sharedMaterial.HasProperty("_PulseStrength")) return;
            floor.material.SetFloat("_PulseStrength", 0f);
        }

        // ---- Concierge --------------------------------------------------------

        /// <summary>Turns the scene's GuardNPC into a blocky service robot in
        /// <paramref name="accent"/>, talking as <paramref name="speaker"/>.</summary>
        internal static void Concierge(Color accent, string speaker, string signName)
        {
            GuardNPC guard = FindObjectOfType<GuardNPC>();
            if (guard == null || guard.transform.Find("ConciergeRobot") != null) return;
            foreach (Renderer r in guard.GetComponentsInChildren<Renderer>(true))
                if (!r.name.StartsWith("Sign_") && r.transform.parent == guard.transform) r.enabled = false;

            var robot = new GameObject("ConciergeRobot").transform;
            robot.SetParent(guard.transform, false);
            ProceduralRobot.Rig rig = ProceduralRobot.Build(robot, accent);
            robot.gameObject.AddComponent<RobotIdle>().Bind(rig, speaker);

            // The sign floats above the robot's antenna, not its old cap.
            Transform sign = guard.transform.Find(signName);
            if (sign != null) sign.localPosition = new Vector3(0f, 2.25f, 0f);
        }

        // ---- Holo core --------------------------------------------------------

        /// <summary>Replaces BuildKit's holo column with a faceted crystal over
        /// an octagonal plinth, circled by one ring of segments. (A second,
        /// tilted ring and a segmented ring on the ceiling were cut: stacked
        /// round the sign they read as a sunburst.)</summary>
        internal static void Core(Color accent, string signName)
        {
            Color Gold = accent; // local: tints every part below
            GameObject core = GameObject.Find("HoloCore");
            GameObject ring = GameObject.Find("HoloCoreRing");
            if (core == null || GameObject.Find("HubCore") != null) return;
            Vector3 at = new Vector3(core.transform.position.x, 0f, core.transform.position.z);
            Hide(core);
            if (ring != null) Hide(ring);

            // The big glow quad behind the hub sign read as a yellow disc
            // hanging from the ceiling; the sign keeps its plate and underline.
            GameObject sign = GameObject.Find(signName);
            if (sign != null)
            {
                Transform halo = sign.transform.Find("Halo");
                if (halo != null) halo.gameObject.SetActive(false);
            }
            // The saved scene still carries the solid hologram disc the
            // factory replaced long ago (a giant glowing plate on the ceiling).
            // Swap it for a segmented ring.
            Transform furnishings = GameObject.Find("Furnishings") != null ? GameObject.Find("Furnishings").transform : null;
            Transform oldHalo = furnishings != null ? furnishings.Find("CeilingHalo") : null;
            if (oldHalo != null && oldHalo.childCount == 0) Hide(oldHalo.gameObject);

            var root = new GameObject("HubCore").transform;
            root.position = at;
            Material body = Mat(Body, 0.5f, 0.45f);
            Material trim = Mat(Trim, 0.6f, 0.65f);
            Material gold = BuildKit.MakeEmissive(Gold, 1.6f);

            // Octagonal plinth in two steps, lit seams between them.
            MeshPart("PlinthBase", root, CharacterMesh.Prism(8, 1.45f, 1.38f, 0f, 0.18f, "PlinthBase", 22.5f), Vector3.zero, Vector3.zero, trim);
            MeshPart("PlinthTop", root, CharacterMesh.Prism(8, 1.05f, 0.95f, 0.18f, 0.42f, "PlinthTop", 22.5f), Vector3.zero, Vector3.zero, body);
            MeshPart("PlinthSeam", root, CharacterMesh.Annulus(8, 1.05f, 1.12f, 0.18f, 0.2f, "PlinthSeam", 22.5f), Vector3.zero, Vector3.zero, gold);
            MeshPart("Emitter", root, CharacterMesh.Annulus(8, 0.32f, 0.5f, 0.42f, 0.47f, "Emitter", 22.5f), Vector3.zero, Vector3.zero, gold);

            // A faceted crystal (two eight-sided pyramids) turning slowly,
            // with a ring of segments orbiting it.
            var spin = new GameObject("Crystal").transform;
            spin.SetParent(root, false);
            spin.localPosition = new Vector3(0f, 2.55f, 0f);
            Material crystal = BuildKit.MakeEmissive(Color.Lerp(accent, Color.white, 0.3f), 1.3f);
            MeshPart("CrystalTop", spin, CharacterMesh.Prism(8, 0.55f, 0.02f, 0f, 0.95f, "CrystalTop", 22.5f), Vector3.zero, Vector3.zero, crystal);
            MeshPart("CrystalBottom", spin, CharacterMesh.Prism(8, 0.02f, 0.55f, -0.7f, 0f, "CrystalBottom", 22.5f), Vector3.zero, Vector3.zero, crystal);
            spin.gameObject.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 14f, 0f);

            Mesh segment = CharacterMesh.BevelBlock(-0.02f, 0.02f, new Vector2(0.11f, 0.03f), new Vector2(0.11f, 0.03f), 0.01f, 0f, "OrbitSegment");
            var orbit = new GameObject("Orbit").transform;
            orbit.SetParent(root, false);
            orbit.localPosition = new Vector3(0f, 2.55f, 0f);
            var parts = new List<CombineInstance>();
            const int count = 20;
            for (int i = 0; i < count; i++)
            {
                if (i % 5 == 4) continue; // gaps, so the ring reads as turning
                float a = i * 360f / count;
                Quaternion q = Quaternion.Euler(0f, a, 0f);
                parts.Add(Piece(segment, q * new Vector3(0f, 0f, 1.0f), new Vector3(0f, a, 0f), Vector3.one));
            }
            Batch("OrbitMesh", orbit, gold, parts);
            orbit.gameObject.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 10f, 0f);
        }

        /// <summary>The round two-step dais under the core becomes octagonal
        /// steps, chamfered, matching the core's plinth.</summary>
        private static void Dais()
        {
            GameObject step = GameObject.Find("DaisStep");
            GameObject top = GameObject.Find("Dais");
            if (step == null || GameObject.Find("HubDais") != null) return;
            Vector3 at = new Vector3(step.transform.position.x, 0f, step.transform.position.z);
            Hide(step);
            if (top != null) Hide(top);

            var root = new GameObject("HubDais").transform;
            root.position = at;
            MeshPart("DaisLower", root, CharacterMesh.Prism(8, 4.4f, 4.3f, 0f, 0.05f, "DaisLower", 22.5f),
                Vector3.zero, Vector3.zero, Mat(new Color(0.06f, 0.065f, 0.085f), 0.55f, 0.4f));
            MeshPart("DaisUpper", root, CharacterMesh.Prism(8, 3.55f, 3.45f, 0.05f, 0.1f, "DaisUpper", 22.5f),
                Vector3.zero, Vector3.zero, Mat(new Color(0.045f, 0.05f, 0.07f), 0.7f, 0.5f));
            MeshPart("DaisEdge", root, CharacterMesh.Annulus(8, 3.55f, 3.62f, 0.05f, 0.065f, "DaisEdge", 22.5f),
                Vector3.zero, Vector3.zero, BuildKit.MakeEmissive(Gold, 0.9f));
        }

        // ---- Doors ------------------------------------------------------------

        internal static void Door(HubDoor door)
        {
            Transform t = door.transform;
            if (t.Find("DoorDress") != null) return;
            var dress = new GameObject("DoorDress").transform;
            dress.SetParent(t, false);

            Color accent = AccentOf(door);
            Material body = Mat(Body, 0.5f, 0.45f);
            Material trim = Mat(Trim, 0.6f, 0.65f);
            Material chrome = Mat(Chrome, 0.75f, 0.9f);
            Material glow = BuildKit.MakeEmissive(accent, 1.4f);

            // Built-out frame over the old cube jambs (their colliders stay).
            foreach (Transform child in t)
                if (child.name == "Frame" && child.GetComponent<Renderer>() != null) child.GetComponent<Renderer>().enabled = false;
            Slab("Jamb_L", dress, new Vector3(-1.5f, 2.0f, 0f), new Vector3(0.42f, 4.0f, 0.56f), 0.05f, body);
            Slab("Jamb_R", dress, new Vector3(1.5f, 2.0f, 0f), new Vector3(0.42f, 4.0f, 0.56f), 0.05f, body);
            Slab("Header", dress, new Vector3(0f, 4.15f, 0f), new Vector3(3.44f, 0.42f, 0.56f), 0.05f, body);
            Slab("Plinth_L", dress, new Vector3(-1.5f, 0.1f, -0.02f), new Vector3(0.52f, 0.2f, 0.62f), 0.03f, trim);
            Slab("Plinth_R", dress, new Vector3(1.5f, 0.1f, -0.02f), new Vector3(0.52f, 0.2f, 0.62f), 0.03f, trim);
            // A lit inner edge round the opening, in the level's colour.
            Box("Edge_L", dress, new Vector3(-1.27f, 2.0f, -0.29f), new Vector3(0.03f, 3.8f, 0.02f), glow);
            Box("Edge_R", dress, new Vector3(1.27f, 2.0f, -0.29f), new Vector3(0.03f, 3.8f, 0.02f), glow);
            Box("Edge_T", dress, new Vector3(0f, 3.92f, -0.29f), new Vector3(2.57f, 0.03f, 0.02f), glow);
            var bolts = new List<CombineInstance>();
            Mesh bolt = CharacterMesh.Prism(6, 0.03f, 0.026f, 0f, 0.025f, "Bolt");
            foreach (float x in new[] { -1.5f, 1.5f })
                foreach (float y in new[] { 0.6f, 3.6f })
                    bolts.Add(Piece(bolt, new Vector3(x, y, -0.28f), new Vector3(-90f, 0f, 0f), Vector3.one));
            Batch("Bolts", dress, chrome, bolts);

            // The name sign: mounted flat on a plate above the door instead of
            // billboarding, which sank half of it into the wall at an angle.
            Transform sign = null;
            foreach (Transform child in t) if (child.name.StartsWith("Sign_")) { sign = child; break; }
            if (sign != null)
            {
                foreach (var b in sign.GetComponents<Billboard>()) Destroy(b);
                Transform halo = sign.Find("Halo");
                if (halo != null) halo.gameObject.SetActive(false);
                sign.localRotation = Quaternion.identity;
                sign.localPosition = new Vector3(0f, 4.78f, -0.34f);
                Slab("SignPlate", dress, new Vector3(0f, 4.76f, -0.27f), new Vector3(3.3f, 0.62f, 0.08f), 0.02f, body);
                Box("SignEdge", dress, new Vector3(0f, 5.06f, -0.315f), new Vector3(3.1f, 0.02f, 0.012f), glow);
            }
        }

        /// <summary>The door's colour, read off its portal (set from its level).</summary>
        private static Color AccentOf(HubDoor door)
        {
            Transform sign = null;
            foreach (Transform child in door.transform) if (child.name.StartsWith("Sign_")) { sign = child; break; }
            TextMesh tm = sign != null ? sign.GetComponent<TextMesh>() : null;
            return tm != null ? new Color(tm.color.r, tm.color.g, tm.color.b, 1f) : Gold;
        }

        // ---- Wayfinding ---------------------------------------------------------

        /// <summary>Each door's spinning hologram disc and its dashes become
        /// an octagonal landing pad with a lit rim and chevrons on the floor
        /// leading to it, in the level's colour. The pad carries no arrow of
        /// its own: the chevrons already point the way.</summary>
        private static void Wayfinding()
        {
            GameObject root = GameObject.Find("Wayfinding");
            if (root == null || root.transform.Find("PadDress_Done") != null) return;
            new GameObject("PadDress_Done").transform.SetParent(root.transform, false);

            Mesh chevron = ChevronMesh();
            foreach (Transform pad in root.transform)
            {
                if (!pad.name.StartsWith("LandingPad_")) continue;
                string level = pad.name.Substring("LandingPad_".Length);
                Renderer padRenderer = pad.GetComponent<Renderer>();
                Color accent = padRenderer != null && padRenderer.sharedMaterial != null &&
                               padRenderer.sharedMaterial.HasProperty("_Color")
                    ? padRenderer.sharedMaterial.GetColor("_Color") : Gold;
                accent.a = 1f;
                if (padRenderer != null) padRenderer.enabled = false;

                // The door this pad serves: the dashes run from the pad away
                // from the door, so the door lies the other way.
                Transform dash0 = root.transform.Find($"GuideDash_{level}_0");
                Vector3 towardDoor = dash0 != null ? (pad.position - dash0.position) : Vector3.right;
                towardDoor.y = 0f;
                towardDoor.Normalize();
                Quaternion face = Quaternion.LookRotation(towardDoor, Vector3.up);

                var dress = new GameObject("Pad_" + level).transform;
                dress.SetParent(root.transform, false);
                dress.SetPositionAndRotation(new Vector3(pad.position.x, 0f, pad.position.z), face);
                MeshPart("PadPlate", dress, CharacterMesh.Prism(8, 1.1f, 1.05f, 0f, 0.035f, "PadPlate", 22.5f),
                    Vector3.zero, Vector3.zero, Mat(new Color(0.05f, 0.055f, 0.075f), 0.6f, 0.45f));
                MeshPart("PadRim", dress, CharacterMesh.Annulus(8, 0.9f, 1.0f, 0.035f, 0.045f, "PadRim", 22.5f),
                    Vector3.zero, Vector3.zero, BuildKit.MakeEmissive(accent, 1.5f));

                // A short, quiet lead-in: the three dashes nearest the door
                // become small chevrons; the far two (which reach the lounges)
                // are dropped, and any chevron landing on furniture is skipped.
                for (int k = 0; k < 5; k++)
                {
                    Transform dash = root.transform.Find($"GuideDash_{level}_{k}");
                    if (dash == null) continue;
                    if (dash.GetComponent<Renderer>() != null) dash.GetComponent<Renderer>().enabled = false;
                    Vector3 at = new Vector3(dash.position.x, 0.02f, dash.position.z);
                    if (k >= 3 || OnFurniture(at)) continue;
                    var c = MeshPart("Chevron_" + level + "_" + k, root.transform, chevron, at, Vector3.zero,
                        BuildKit.MakeEmissive(accent, 0.75f - k * 0.18f), scale: new Vector3(0.75f, 1f, 0.75f));
                    c.rotation = face;
                }
            }
        }

        /// <summary>True when a floor point sits under the lounges' couches,
        /// tables or rugs.</summary>
        private static bool OnFurniture(Vector3 floorPoint)
        {
            GameObject furnishings = GameObject.Find("Furnishings");
            if (furnishings == null) return false;
            foreach (Transform group in furnishings.transform)
            {
                if (!group.name.StartsWith("Lounge") && !group.name.StartsWith("Seat")) continue;
                foreach (Renderer r in group.GetComponentsInChildren<Renderer>())
                {
                    Bounds b = r.bounds;
                    b.Expand(new Vector3(0.6f, 0f, 0.6f));
                    if (floorPoint.x > b.min.x && floorPoint.x < b.max.x && floorPoint.z > b.min.z && floorPoint.z < b.max.z)
                        return true;
                }
            }
            return false;
        }

        /// <summary>A flat "^" pointing along +Z, about 0.6 m wide.</summary>
        private static Mesh ChevronMesh()
        {
            Mesh bar = CharacterMesh.BevelBlock(0f, 0.02f, new Vector2(0.06f, 0.24f), new Vector2(0.06f, 0.24f), 0.01f, 0f, "ChevronBar");
            var parts = new List<CombineInstance>
            {
                Piece(bar, new Vector3(-0.15f, 0f, -0.05f), new Vector3(0f, 40f, 0f), Vector3.one),
                Piece(bar, new Vector3(0.15f, 0f, -0.05f), new Vector3(0f, -40f, 0f), Vector3.one),
            };
            var mesh = new Mesh { name = "Chevron" };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            return mesh;
        }

        // ---- Banners ------------------------------------------------------------

        /// <summary>The flat glowing slabs on the north wall become hanging
        /// banners: a chrome rod, a matte panel in SJSU blue or gold with a
        /// notched tail, and a security icon.</summary>
        private static void Banners()
        {
            GameObject furnishings = GameObject.Find("Furnishings");
            if (furnishings == null || furnishings.transform.Find("BannerDress_0") != null) return;
            Material chrome = Mat(Chrome, 0.75f, 0.9f);
            for (int i = 0; i < 8; i++)
            {
                Transform panel = furnishings.transform.Find("Banner_" + i);
                Transform frame = furnishings.transform.Find("BannerFrame_" + i);
                if (panel == null) break;
                Color colour = panel.GetComponent<Renderer>() != null ? panel.GetComponent<Renderer>().sharedMaterial.color : Gold;
                Hide(panel.gameObject);
                if (frame != null) Hide(frame.gameObject);

                var b = new GameObject("BannerDress_" + i).transform;
                b.SetParent(furnishings.transform, false);
                // Local -Z (the readable side) already faces into the room.
                b.position = new Vector3(panel.position.x, 3.1f, 19.32f);

                Box("Rod", b, new Vector3(0f, 1.32f, 0f), new Vector3(1.75f, 0.05f, 0.05f), chrome);
                Box("RodCap_L", b, new Vector3(-0.9f, 1.32f, 0f), new Vector3(0.08f, 0.08f, 0.08f), chrome);
                Box("RodCap_R", b, new Vector3(0.9f, 1.32f, 0f), new Vector3(0.08f, 0.08f, 0.08f), chrome);
                Material cloth = Mat(colour * 0.8f, 0.3f, 0f);
                Slab("Cloth", b, new Vector3(0f, 0.2f, 0f), new Vector3(1.45f, 2.2f, 0.04f), 0.015f, cloth);
                // Notched tail: two angled flaps under the panel.
                Box("Tail_L", b, new Vector3(-0.36f, -1.0f, 0f), new Vector3(0.73f, 0.32f, 0.04f), cloth).localRotation = Quaternion.Euler(0f, 0f, -12f);
                Box("Tail_R", b, new Vector3(0.36f, -1.0f, 0f), new Vector3(0.73f, 0.32f, 0.04f), cloth).localRotation = Quaternion.Euler(0f, 0f, 12f);
                Box("Stripe", b, new Vector3(0f, 1.05f, -0.025f), new Vector3(1.45f, 0.06f, 0.01f), BuildKit.MakeEmissive(Color.white, 0.6f));

                var icon = BannerIcon(i % 4);
                Transform glyph = Batch("Icon", b, BuildKit.MakeEmissive(new Color(0.95f, 0.97f, 1f), 0.9f), icon);
                glyph.localPosition = new Vector3(0f, 0.35f, -0.03f);
                TextMesh word = BuildKit.MakeLabel(b, new Vector3(0f, -0.42f, -0.03f), BannerWords[i % 4],
                    new Color(0.95f, 0.97f, 1f), 0.027f);
                word.transform.localRotation = Quaternion.identity;
            }
        }

        private static readonly string[] BannerWords = { "IDENTIFY", "PROTECT", "DETECT", "RESPOND" };

        /// <summary>An icon about 0.6 m tall in its X/Y plane, facing -Z: a lock,
        /// a shield, an eye, a megaphone-ish alert.</summary>
        private static List<CombineInstance> BannerIcon(int kind)
        {
            const float t = 0.02f;
            var p = new List<CombineInstance>();
            Vector3 face = new Vector3(-90f, 0f, 0f);
            switch (kind)
            {
                case 0: // lock
                    p.Add(Piece(CubeMesh, new Vector3(0f, -0.08f, 0f), Vector3.zero, new Vector3(0.42f, 0.34f, t)));
                    p.Add(Piece(CharacterMesh.Annulus(16, 0.12f, 0.17f, 0f, t, "Shackle"), new Vector3(0f, 0.12f, 0f), face, Vector3.one));
                    break;
                case 1: // shield: a square top over a diamond that comes to a point
                    p.Add(Piece(CubeMesh, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(0.42f, 0.24f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0f, -0.04f, 0f), new Vector3(0f, 0f, 45f), new Vector3(0.297f, 0.297f, t)));
                    break;
                case 2: // eye
                    p.Add(Piece(CharacterMesh.Annulus(16, 0.2f, 0.25f, 0f, t, "EyeOuter"), Vector3.zero, face, new Vector3(1f, 1f, 0.55f)));
                    p.Add(Piece(CharacterMesh.Prism(12, 0.09f, 0.09f, 0f, t, "Pupil"), Vector3.zero, face, Vector3.one));
                    break;
                default: // alert: triangle-ish bars and an exclamation mark
                    p.Add(Piece(CubeMesh, new Vector3(-0.13f, 0f, 0f), new Vector3(0f, 0f, -28f), new Vector3(0.05f, 0.5f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0.13f, 0f, 0f), new Vector3(0f, 0f, 28f), new Vector3(0.05f, 0.5f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0f, -0.22f, 0f), Vector3.zero, new Vector3(0.5f, 0.05f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0f, 0.0f, 0f), Vector3.zero, new Vector3(0.05f, 0.18f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0f, -0.14f, 0f), Vector3.zero, new Vector3(0.05f, 0.05f, t)));
                    break;
            }
            return p;
        }

        // ---- Ambient drones -----------------------------------------------------

        /// <summary>The realistic quad drones become small blocky service
        /// drones (same hover and patrol; their rotors keep spinning).</summary>
        internal static void AmbientDrones()
        {
            foreach (Hoverer drone in FindObjectsOfType<Hoverer>())
            {
                if (drone.transform.Find("BlockDrone") != null) continue;
                foreach (Renderer r in drone.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

                var body = new GameObject("BlockDrone").transform;
                body.SetParent(drone.transform, false);
                Material shell = Mat(new Color(0.84f, 0.87f, 0.91f), 0.45f, 0.1f);
                Material joint = Mat(new Color(0.13f, 0.14f, 0.17f), 0.4f, 0.3f);
                Block("Shell", body, Vector3.zero, Vector3.zero, -0.09f, 0.09f, new Vector2(0.15f, 0.15f), new Vector2(0.13f, 0.13f), 0.035f, shell);
                Slab("Lens", body, new Vector3(0f, -0.1f, 0f), new Vector3(0.12f, 0.03f, 0.12f), 0.01f, joint);
                Box("LensLight", body, new Vector3(0f, -0.118f, 0f), new Vector3(0.06f, 0.008f, 0.06f), BuildKit.MakeEmissive(new Color(0.3f, 0.78f, 1f), 2.4f));
                var rotors = new List<Transform>();
                Mesh ring = CharacterMesh.Annulus(8, 0.11f, 0.14f, 0f, 0.015f, "RotorGuard", 22.5f);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = Quaternion.Euler(0f, 45f + i * 90f, 0f) * new Vector3(0f, 0.04f, 0.34f);
                    Box("Arm_" + i, body, corner * 0.5f + new Vector3(0f, 0.02f, 0f), new Vector3(0.04f, 0.025f, 0.34f), joint)
                        .localRotation = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                    MeshPart("Guard_" + i, body, ring, corner, Vector3.zero, joint);
                    var rotor = new GameObject("Rotor").transform;
                    rotor.SetParent(body, false);
                    rotor.localPosition = corner + new Vector3(0f, 0.012f, 0f);
                    Box("Blade", rotor, Vector3.zero, new Vector3(0.22f, 0.005f, 0.025f), joint);
                    rotors.Add(rotor);
                }
                drone.rotors = rotors.ToArray();
            }
        }

        // ---- Mission board ----------------------------------------------------

        private static void MissionBoardStand()
        {
            MissionBoard board = FindObjectOfType<MissionBoard>();
            if (board == null || board.transform.Find("BoardDress") != null) return;
            Transform t = board.transform;
            var dress = new GameObject("BoardDress").transform;
            dress.SetParent(t, false);
            Material body = Mat(Body, 0.5f, 0.45f);
            Material trim = Mat(Trim, 0.6f, 0.65f);

            foreach (Transform child in t)
                if ((child.name == "Post" || child.name == "Panel") && child.GetComponent<Renderer>() != null)
                    child.GetComponent<Renderer>().enabled = false;

            Slab("Frame", dress, new Vector3(0f, 2.05f, 0.07f), new Vector3(3.72f, 2.92f, 0.1f), 0.04f, body);
            Box("TopEdge", dress, new Vector3(0f, 3.48f, -0.005f), new Vector3(3.5f, 0.022f, 0.012f), BuildKit.MakeEmissive(Gold, 1.4f));
            foreach (float x in new[] { -1.55f, 1.55f })
            {
                Block("Leg", dress, new Vector3(x, 0f, 0.1f), Vector3.zero, 0f, 0.62f,
                    new Vector2(0.05f, 0.05f), new Vector2(0.045f, 0.045f), 0.015f, trim);
                Slab("Foot", dress, new Vector3(x, 0.03f, 0.1f), new Vector3(0.16f, 0.06f, 0.5f), 0.02f, trim);
            }
        }

        internal static void Hide(GameObject go)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
    }
}
