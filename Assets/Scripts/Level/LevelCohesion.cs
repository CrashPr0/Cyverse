using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cyverse.Interaction;
using static Cyverse.Level.BlockKit;

namespace Cyverse.Level
{
    /// <summary>
    /// The last finishing pass in every level, so all rooms read as one game:
    ///
    ///  - server racks and locker banks saved into older scenes as plain boxes
    ///    are rebuilt as CabinetProps (racks bolted into rows under a cable
    ///    tray, locker banks closed up and numbered);
    ///  - the Level 2 alert board's console gets a chamfered desk and frame;
    ///  - ceiling light bars become a narrow diffuser in a dark housing;
    ///  - station and door signs are mounted on plates instead of floating
    ///    and turning to the camera (characters' name tags stay floating);
    ///  - bought furniture (work pods, lounge sofas and their desk props) is
    ///    moved onto the game's palette in place of wood, chrome and leather.
    ///
    /// It runs one frame after load, once every level's own polish pass has
    /// built its props. Nothing is saved: scene objects are rebuilt and
    /// materials replaced in memory only.
    /// </summary>
    public sealed class LevelCohesion : MonoBehaviour
    {
        private static readonly Color BodyColor = new Color(0.075f, 0.085f, 0.115f);
        private static readonly Color TrimColor = new Color(0.17f, 0.19f, 0.25f);
        private static readonly Color RecessColor = new Color(0.035f, 0.04f, 0.06f);
        private static readonly Color GunmetalColor = new Color(0.30f, 0.33f, 0.40f);

        public static void Ensure(GameObject host)
        {
            if (host.GetComponent<LevelCohesion>() == null) host.AddComponent<LevelCohesion>();
        }

        private IEnumerator Start()
        {
            yield return null; // after every level polish pass's Start
            UpgradeSavedCabinets();
            DressAlertConsole();
            CeilingFixtures();
            foreach (HubDoor door in FindObjectsOfType<HubDoor>()) HubPolish.Door(door);
            ReceptionSigns();
            MountStationSigns();
            FurniturePalette();
        }

        // ---- Saved cabinets -----------------------------------------------------

        /// <summary>Racks and lockers that a scene saved as primitives (their
        /// "Led0" / "Seam" children give them away) are rebuilt in place.</summary>
        private static void UpgradeSavedCabinets()
        {
            var racks = new List<Transform>();
            var lockers = new List<Transform>();
            foreach (Transform t in FindObjectsOfType<Transform>())
            {
                if (t.name == "ServerRack" && t.Find("Led0") != null && t.Find("Servers") == null) racks.Add(t);
                else if (t.name == "Lockers" && t.Find("Seam") != null && t.Find("Doors") == null) lockers.Add(t);
            }

            foreach (List<Transform> row in Rows(racks, 1.5f))
            {
                Repack(row, CabinetProps.RackPitch);
                foreach (Transform rack in row)
                {
                    ClearChildren(rack);
                    CabinetProps.ServerRack(rack, Seed(rack.position));
                }
                Transform first = row[0];
                var tray = new GameObject("ServerRow").transform;
                tray.SetParent(first.parent, false);
                tray.position = Centre(row);
                tray.rotation = first.rotation;
                CabinetProps.CableTray(tray, row.Count);
            }

            foreach (List<Transform> row in Rows(lockers, 2.6f))
            {
                Repack(row, 2.05f);
                // Numbered left to right as seen from the front, where the
                // bank's local +X is on the viewer's left.
                row.Sort((a, b) => Along(b, row[0]).CompareTo(Along(a, row[0])));
                for (int i = 0; i < row.Count; i++)
                {
                    ClearChildren(row[i]);
                    CabinetProps.LockerBank(row[i], Seed(row[i].position), 1 + i * 4);
                }
            }
        }

        /// <summary>Groups cabinets standing side by side (same parent and
        /// facing, in line, gaps under <paramref name="maxStep"/>) into rows,
        /// each sorted along the row.</summary>
        private static List<List<Transform>> Rows(List<Transform> items, float maxStep)
        {
            var rows = new List<List<Transform>>();
            var left = new List<Transform>(items);
            while (left.Count > 0)
            {
                Transform seed = left[0];
                left.RemoveAt(0);
                var row = new List<Transform> { seed };
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    for (int i = left.Count - 1; i >= 0; i--)
                    {
                        Transform c = left[i];
                        if (c.parent != seed.parent || Quaternion.Angle(c.rotation, seed.rotation) > 2f) continue;
                        Vector3 local = seed.InverseTransformPoint(c.position);
                        if (Mathf.Abs(local.z) > 0.2f) continue;
                        bool adjacent = false;
                        foreach (Transform r in row)
                            if (Mathf.Abs(Along(c, seed) - Along(r, seed)) < maxStep) { adjacent = true; break; }
                        if (!adjacent) continue;
                        row.Add(c);
                        left.RemoveAt(i);
                        grew = true;
                    }
                }
                row.Sort((a, b) => Along(a, seed).CompareTo(Along(b, seed)));
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>Position along <paramref name="frame"/>'s local X axis.</summary>
        private static float Along(Transform t, Transform frame) => frame.InverseTransformPoint(t.position).x;

        private static Vector3 Centre(List<Transform> row)
        {
            Vector3 sum = Vector3.zero;
            foreach (Transform t in row) sum += t.position;
            return sum / row.Count;
        }

        /// <summary>Closes the gaps: spaces the row evenly at
        /// <paramref name="pitch"/> about its centre, along its own X axis.</summary>
        private static void Repack(List<Transform> row, float pitch)
        {
            Vector3 centre = Centre(row);
            Vector3 axis = row[0].right;
            for (int i = 0; i < row.Count; i++)
                row[i].position = centre + axis * ((i - (row.Count - 1) * 0.5f) * pitch);
        }

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject child = t.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static int Seed(Vector3 p) => Mathf.RoundToInt(p.x * 10f) * 7919 + Mathf.RoundToInt(p.z * 10f);

        // ---- Level 2 alert board ------------------------------------------------

        /// <summary>The alert board stood on a bare black box: the console gets
        /// a chamfered desk with a lit edge, and the screen a framed housing.
        /// The old boxes keep their colliders.</summary>
        private static void DressAlertConsole()
        {
            SiemConsole console = FindObjectOfType<SiemConsole>();
            if (console == null || console.transform.Find("ConsoleDress") != null) return;
            Transform root = console.transform;
            var dress = new GameObject("ConsoleDress").transform;
            dress.SetParent(root, false);
            Material body = Mat(BodyColor, 0.45f, 0.4f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Material glow = BuildKit.MakeEmissive(new Color(0.95f, 0.42f, 0.30f), 1.1f);

            Transform desk = root.Find("Desk");
            if (desk != null)
            {
                Hide(desk);
                Vector3 s = desk.localScale, p = desk.localPosition;
                float top = p.y + s.y * 0.5f, hx = s.x * 0.5f, hz = s.z * 0.5f;
                Block("DeskKick", dress, new Vector3(p.x, 0f, p.z + 0.05f), Vector3.zero, 0f, 0.08f,
                    new Vector2(hx - 0.08f, hz - 0.1f), new Vector2(hx - 0.08f, hz - 0.1f), 0.02f, Mat(RecessColor, 0.3f, 0.2f));
                Block("DeskBody", dress, new Vector3(p.x, 0f, p.z), Vector3.zero, 0.08f, top - 0.05f,
                    new Vector2(hx - 0.02f, hz - 0.02f), new Vector2(hx, hz), 0.05f, body);
                Block("DeskTop", dress, new Vector3(p.x, 0f, p.z), Vector3.zero, top - 0.05f, top,
                    new Vector2(hx + 0.03f, hz + 0.03f), new Vector2(hx + 0.03f, hz + 0.03f), 0.02f, trim);
                Box("DeskGlow", dress, new Vector3(p.x, top - 0.08f, p.z - hz - 0.004f), new Vector3(s.x - 0.3f, 0.012f, 0.006f), glow);
                for (int i = -1; i <= 1; i++)
                    Slab("DeskPanel_" + (i + 1), dress, new Vector3(p.x + i * s.x / 3f, top * 0.48f, p.z - hz - 0.002f),
                        new Vector3(s.x / 3f - 0.12f, top * 0.55f, 0.02f), 0.008f, Mat(RecessColor, 0.3f, 0.2f));
            }

            Transform screen = root.Find("ScreenBody");
            if (screen != null)
            {
                Hide(screen);
                Vector3 s = screen.localScale, p = screen.localPosition;
                float hx = s.x * 0.5f, hy = s.y * 0.5f;
                Slab("ScreenHousing", dress, p + new Vector3(0f, 0f, 0.03f), new Vector3(s.x + 0.1f, s.y + 0.1f, s.z + 0.04f), 0.04f, body);
                float faceZ = p.z - s.z * 0.5f - 0.01f;
                Batch("ScreenBezel", dress, trim, new List<CombineInstance>
                {
                    Piece(CubeMesh, new Vector3(p.x - hx, p.y, faceZ), Vector3.zero, new Vector3(0.1f, s.y + 0.1f, 0.03f)),
                    Piece(CubeMesh, new Vector3(p.x + hx, p.y, faceZ), Vector3.zero, new Vector3(0.1f, s.y + 0.1f, 0.03f)),
                    Piece(CubeMesh, new Vector3(p.x, p.y + hy, faceZ), Vector3.zero, new Vector3(s.x + 0.1f, 0.1f, 0.03f)),
                    Piece(CubeMesh, new Vector3(p.x, p.y - hy, faceZ), Vector3.zero, new Vector3(s.x + 0.1f, 0.1f, 0.03f)),
                });
                Box("ScreenGlow", dress, new Vector3(p.x, p.y + hy + 0.035f, faceZ - 0.016f), new Vector3(s.x - 0.6f, 0.012f, 0.006f), glow);
                // A post at each side from the desk up into the housing.
                float deskTop = desk != null ? desk.localPosition.y + desk.localScale.y * 0.5f : 1f;
                for (int side = -1; side <= 1; side += 2)
                    Block(side < 0 ? "Post_L" : "Post_R", dress, new Vector3(p.x + side * (hx - 0.4f), 0f, p.z + 0.08f), Vector3.zero,
                        deskTop, p.y - hy, new Vector2(0.06f, 0.05f), new Vector2(0.05f, 0.04f), 0.015f, trim);
            }
        }

        private static void Hide(Transform t)
        {
            Renderer r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }

        // ---- Ceiling fixtures ---------------------------------------------------

        /// <summary>The ceiling bars were full-width glowing slabs, the
        /// brightest large shapes in every room. Each becomes a dark housing
        /// with a narrow recessed diffuser, split into modules. The diffuser
        /// keeps its CeilingPanel_ name, so each level's own tint still lands
        /// on it.</summary>
        private static void CeilingFixtures()
        {
            Material housing = Palette.Get("CeilingHousing", new Color(0.11f, 0.12f, 0.16f), 0.5f, 0.5f);
            Material divider = Palette.Get("CeilingDivider", new Color(0.06f, 0.065f, 0.085f), 0.4f, 0.3f);
            foreach (Renderer r in FindObjectsOfType<Renderer>())
            {
                Transform panel = r.transform;
                if (!panel.name.StartsWith("CeilingPanel_") || panel.Find("Housing") != null) continue;
                if (panel.GetComponent<MeshFilter>() == null) continue;
                Bounds b = r.bounds;
                if (b.size.x < 4f && b.size.z < 4f) continue; // only the long bars
                bool alongX = b.size.x >= b.size.z;
                float length = alongX ? b.size.x : b.size.z, width = alongX ? b.size.z : b.size.x;
                Quaternion axis = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

                var fixture = new GameObject("CeilingFixture_" + panel.name.Substring("CeilingPanel_".Length)).transform;
                fixture.SetPositionAndRotation(new Vector3(b.center.x, b.max.y, b.center.z), axis);
                Slab("CeilingHousing", fixture, new Vector3(0f, -0.07f, 0f), new Vector3(length + 0.3f, 0.14f, width + 0.1f), 0.03f, housing);

                // The bar itself becomes the diffuser strip under the housing.
                Vector3 lossy = panel.lossyScale;
                Vector3 local = panel.localScale;
                float sx = local.x / Mathf.Max(lossy.x, 1e-4f), sy = local.y / Mathf.Max(lossy.y, 1e-4f), sz = local.z / Mathf.Max(lossy.z, 1e-4f);
                panel.position = new Vector3(b.center.x, b.max.y - 0.15f, b.center.z);
                Vector3 world = alongX ? new Vector3(length - 0.5f, 0.02f, 0.22f) : new Vector3(0.22f, 0.02f, length - 0.5f);
                panel.localScale = new Vector3(world.x * sx, world.y * sy, world.z * sz);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var bars = new List<CombineInstance>();
                for (float x = -length * 0.5f + 2.4f; x < length * 0.5f - 1.2f; x += 2.4f)
                    bars.Add(Piece(CubeMesh, new Vector3(x, -0.155f, 0f), Vector3.zero, new Vector3(0.05f, 0.025f, 0.3f)));
                Batch("CeilingDividers", fixture, divider, bars);
                new GameObject("Housing").transform.SetParent(panel, false); // marks the bar as done
            }
        }

        // ---- Signs --------------------------------------------------------------

        /// <summary>Station and door signs mount flat on a plate facing the
        /// station's front, like the Hub doors and the enrollment kiosk,
        /// instead of floating and turning to the camera. Skipped: signs with
        /// no owner, signs already mounted, characters' name tags and the
        /// centrepiece signs, which are seen from every side. Door signs are
        /// already on their plates (HubPolish.Door) and are only marked mounted.</summary>
        private static void MountStationSigns()
        {
            foreach (Transform sign in FindObjectsOfType<Transform>())
            {
                if (!sign.name.StartsWith("Sign_") || sign.name.EndsWith("_Plate")) continue;
                if (sign.GetComponent<TMPro.TMP_Text>() == null && sign.GetComponent<TextMesh>() == null) continue;
                Transform owner = sign.parent;
                if (owner == null || sign.name.StartsWith("Sign_CYVERSE") || IsCharacter(owner)) continue;
                WorldTextLayoutIntent intent = sign.GetComponent<WorldTextLayoutIntent>();
                if (intent != null && !intent.IsFloating) continue;
                if (owner.GetComponent<HubDoor>() != null) MarkMounted(sign);
                else Mount(sign, owner);
            }
            // Re-read the new intents now, before the layout pass fades them
            // as floating signs again.
            WorldTextLayoutManager layout = FindObjectOfType<WorldTextLayoutManager>();
            if (layout != null) layout.RefreshNow();
        }

        /// <summary>The reception counter's sign stands over the counter,
        /// facing the visitors' side (local +Z, where its lit edge is; staff sit
        /// on the -Z side). Some saved scenes left it at the room's origin, in
        /// the doorway between the lobby and the task room.</summary>
        private static void ReceptionSigns()
        {
            foreach (Transform reception in FindObjectsOfType<Transform>())
            {
                if (reception.name != "Reception") continue;
                Transform sign = null;
                foreach (Transform child in reception)
                    if (child.name.StartsWith("Sign_RECEPTION")) { sign = child; break; }
                if (sign == null) continue;
                sign.position = reception.TransformPoint(new Vector3(0f, 1.75f, 0.05f));
                foreach (SignFX fx in sign.GetComponents<SignFX>()) fx.Rebase();
                Mount(sign, reception, -reception.forward);
            }
        }

        private static bool IsCharacter(Transform owner) =>
            owner.GetComponentInParent<GuardNPC>() != null || owner.GetComponentInParent<MfaSpecialist>() != null ||
            owner.GetComponentInChildren<RobotIdle>() != null;

        /// <summary>Stops a sign floating, bobbing or fading, and undoes any
        /// fade the floating-sign layout had already applied.</summary>
        private static void MarkMounted(Transform sign)
        {
            foreach (Billboard b in sign.GetComponents<Billboard>()) Destroy(b);
            foreach (SignFX fx in sign.GetComponents<SignFX>()) fx.enabled = false;
            Transform halo = sign.Find("Halo");
            if (halo != null) halo.gameObject.SetActive(false);
            WorldTextLayoutIntent.Configure(sign.gameObject, WorldTextLayoutIntent.Mode.Mounted, 200);
            var tmp = sign.GetComponent<TMPro.TMP_Text>();
            if (tmp != null) { Color c = tmp.color; c.a = 1f; tmp.color = c; }
            var mesh = sign.GetComponent<TextMesh>();
            if (mesh != null) { Color c = mesh.color; c.a = 1f; mesh.color = c; }
            foreach (Renderer chrome in sign.GetComponentsInChildren<Renderer>(true))
                if (chrome.transform != sign && chrome.name != "Halo") chrome.enabled = true;
        }

        private static void Mount(Transform sign, Transform owner) => Mount(sign, owner, owner.forward);

        /// <summary>Mounts the sign on a plate, its text read by someone looking
        /// along <paramref name="facing"/> (stations face their local -Z, so by
        /// default that is the owner's forward).</summary>
        private static void Mount(Transform sign, Transform owner, Vector3 facing)
        {
            MarkMounted(sign);
            // The plate's lit edge replaces the underline, which on some saved
            // signs sits through the middle of the word.
            Transform underline = sign.Find("Underline");
            if (underline != null) underline.gameObject.SetActive(false);

            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
            sign.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);

            Renderer text = sign.GetComponent<Renderer>();
            if (text == null) return;
            Bounds local = text.localBounds;
            Vector3 scale = sign.lossyScale;
            float w = local.size.x * Mathf.Abs(scale.x), h = local.size.y * Mathf.Abs(scale.y);
            if (w <= 0.01f || h <= 0.01f) return;
            Vector3 centre = sign.TransformPoint(local.center);

            Color colour = SignColour(sign);
            var plate = new GameObject(sign.name + "_Plate").transform;
            plate.SetParent(owner, true);
            plate.SetPositionAndRotation(centre + sign.forward * 0.05f, sign.rotation);
            Slab("Plate", plate, Vector3.zero, new Vector3(w + 0.36f, h + 0.2f, 0.05f), 0.02f, Mat(BodyColor, 0.45f, 0.4f));
            Box("PlateEdge", plate, new Vector3(0f, (h + 0.2f) * 0.5f - 0.02f, -0.027f), new Vector3(w + 0.16f, 0.012f, 0.006f),
                BuildKit.MakeEmissive(colour, 1.1f));
            Support(plate, w + 0.36f, h + 0.2f);
        }

        /// <summary>A plate with nothing behind it (no wall, board or housing
        /// within half a metre) needs something to hold it up. A low sign over
        /// a counter or kiosk stands on two short posts from that surface; a
        /// sign up near the ceiling hangs from it on two thin rods.</summary>
        private static void Support(Transform plate, float width, float height)
        {
            if (Physics.Raycast(plate.position, plate.forward, 0.6f, ~0, QueryTriggerInteraction.Ignore)) return;
            Material metal = Mat(TrimColor, 0.55f, 0.6f);
            float bottom = plate.position.y - height * 0.5f, top = plate.position.y + height * 0.5f;
            float postX = Mathf.Min(width * 0.5f - 0.15f, 0.6f);

            if (Physics.Raycast(new Vector3(plate.position.x, bottom - 0.02f, plate.position.z), Vector3.down,
                    out RaycastHit below, 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                float length = bottom - below.point.y;
                var posts = new List<CombineInstance>();
                for (int side = -1; side <= 1; side += 2)
                    posts.Add(Piece(CubeMesh, new Vector3(side * postX, -height * 0.5f - length * 0.5f, 0.01f),
                        Vector3.zero, new Vector3(0.03f, length, 0.03f)));
                Batch("SignPosts", plate, metal, posts);
                return;
            }

            Renderer slab = GameObject.Find("CeilingSlab") != null ? GameObject.Find("CeilingSlab").GetComponent<Renderer>() : null;
            float ceiling = slab != null ? slab.bounds.min.y : 5f;
            if (ceiling - top < 0.1f) return;
            var rods = new List<CombineInstance>();
            for (int side = -1; side <= 1; side += 2)
                rods.Add(Piece(CubeMesh, new Vector3(side * (width * 0.5f - 0.15f), height * 0.5f + (ceiling - top) * 0.5f, 0.01f),
                    Vector3.zero, new Vector3(0.014f, ceiling - top, 0.014f)));
            Batch("CeilingRods", plate, metal, rods);
        }

        private static Color SignColour(Transform sign)
        {
            var tmp = sign.GetComponent<TMPro.TMP_Text>();
            Color c = tmp != null ? tmp.color : sign.GetComponent<TextMesh>() != null ? sign.GetComponent<TextMesh>().color : BuildKit.AccentCyan;
            c.a = 1f;
            return c;
        }

        // ---- Furniture palette --------------------------------------------------

        private enum Finish { Body, Recess, Trim, Gunmetal }

        /// <summary>The bought furniture's materials, by name, and the finish
        /// each becomes. Screens, lamps, the partitions (already in palette)
        /// and the rug are left alone.</summary>
        private static readonly Dictionary<string, Finish> Furniture = new Dictionary<string, Finish>
        {
            { "Adler Dark Wood", Finish.Trim }, { "Ebony Wood", Finish.Trim }, { "BA_Wood 03", Finish.Trim },
            { "Brushed Metal_Chrome", Finish.Gunmetal }, { "Brushed Chrome", Finish.Gunmetal },
            { "BA_Leather_01", Finish.Body }, { "Plastic_01", Finish.Body }, { "GameConsole_01", Finish.Body },
            { "Controller_01_A", Finish.Body }, { "keyboard_01", Finish.Body }, { "Keyboard", Finish.Body },
            { "Mouse", Finish.Body }, { "Phone", Finish.Body }, { "Router", Finish.Body }, { "Chessboard", Finish.Body },
            { "Vase_Apt_01", Finish.Body }, { "BA_Concrete_01", Finish.Body }, { "Building Floor Tile", Finish.Body },
            { "BA_Color_Black_01", Finish.Recess }, { "DarkGlass", Finish.Recess },
        };

        /// <summary>The reception counter and coffee table tops that older
        /// scenes saved with their own light wood material.</summary>
        private static bool IsSavedWoodTop(Renderer r)
        {
            if (!r.name.EndsWith("Top") || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_Color")) return false;
            Color c = r.sharedMaterial.color;
            return c.r > 0.3f && c.r > c.g + 0.06f && c.g > c.b + 0.05f; // warm brown
        }

        private static void FurniturePalette()
        {
            var finishes = new Dictionary<Finish, Material>
            {
                { Finish.Body, Palette.Get("FurnitureFabric", BodyColor, 0.35f, 0.3f) },
                { Finish.Recess, Palette.Get("FurnitureDark", RecessColor, 0.4f, 0.2f) },
                { Finish.Trim, Palette.Get("FurnitureTop", TrimColor, 0.55f, 0.5f) },
                { Finish.Gunmetal, Palette.Get("FurnitureMetal", GunmetalColor, 0.62f, 0.85f) },
            };
            foreach (Renderer r in FindObjectsOfType<Renderer>(true))
            {
                if (IsSavedWoodTop(r)) { r.sharedMaterial = finishes[Finish.Trim]; continue; }
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string name = mats[i].name.Replace(" (Instance)", "");
                    if (!Furniture.TryGetValue(name, out Finish finish)) continue;
                    mats[i] = finishes[finish];
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
