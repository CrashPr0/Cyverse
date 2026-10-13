using System.Collections.Generic;
using UnityEngine;
using Cyverse.Level;
using static Cyverse.Level.BlockKit;

namespace Cyverse.Interaction
{
    /// <summary>
    /// What the IR Playbook looks like, in the blocky, chamfered style of Level
    /// 1: a framed wall board, one wall panel per step with arrows showing the
    /// left-to-right order, a dock per step whose lamp marks the next one to
    /// fill, binder cards with an icon for each response step, and a rack desk
    /// with a lit bay per card. PlaybookStation keeps the rules; these build
    /// the meshes. Everything is idempotent, so a saved scene that is rewired
    /// twice is dressed once.
    /// </summary>
    public static class PlaybookProps
    {
        private static readonly Color BodyColor = new Color(0.075f, 0.085f, 0.115f);
        private static readonly Color TrimColor = new Color(0.17f, 0.19f, 0.25f);
        private static readonly Color BinderColor = new Color(0.13f, 0.14f, 0.19f);
        private static readonly Color IconColor = new Color(0.92f, 0.96f, 1f);
        public static readonly Color Filled = new Color(0.30f, 1f, 0.45f);

        // ---- Wall board -------------------------------------------------------

        /// <summary>A framed panel behind the board's title, instruction and
        /// sequence rows (it also hides the room's wall-light strip behind the
        /// text). <paramref name="width"/> spans all the slots.</summary>
        public static void BuildBoard(Transform board, float width, Color accent)
        {
            if (board.Find("BoardDress") != null) return;
            var dress = new GameObject("BoardDress").transform;
            dress.SetParent(board, false);
            Material body = Mat(BodyColor, 0.5f, 0.4f);
            Material trim = Mat(TrimColor, 0.6f, 0.6f);
            Material glow = BuildKit.MakeEmissive(accent, 1.4f);

            Slab("Panel", dress, new Vector3(0f, 2.8f, 0.7f), new Vector3(width, 2.9f, 0.08f), 0.03f, body);
            Slab("Frame_Top", dress, new Vector3(0f, 4.27f, 0.66f), new Vector3(width + 0.2f, 0.14f, 0.14f), 0.03f, trim);
            Slab("Frame_Bottom", dress, new Vector3(0f, 1.33f, 0.66f), new Vector3(width + 0.2f, 0.14f, 0.14f), 0.03f, trim);
            Slab("Frame_L", dress, new Vector3(-width * 0.5f - 0.05f, 2.8f, 0.66f), new Vector3(0.14f, 3.0f, 0.14f), 0.03f, trim);
            Slab("Frame_R", dress, new Vector3(width * 0.5f + 0.05f, 2.8f, 0.66f), new Vector3(0.14f, 3.0f, 0.14f), 0.03f, trim);
            Box("TitleRule", dress, new Vector3(0f, 3.42f, 0.64f), new Vector3(width * 0.6f, 0.025f, 0.01f), glow);
        }

        /// <summary>One wall panel per step, behind and above its dock, holding
        /// the step's label; an arrow on the right edge points to the next.</summary>
        public static Transform BuildStepPanel(Transform zone, bool last, Color accent)
        {
            Transform existing = zone.Find("StepPanel");
            if (existing != null) return existing;
            var panel = new GameObject("StepPanel").transform;
            panel.SetParent(zone, false);
            Material body = Mat(new Color(0.045f, 0.05f, 0.07f), 0.55f, 0.3f);
            Slab("Plate", panel, new Vector3(0f, 1.82f, 0.6f), new Vector3(1.85f, 0.5f, 0.05f), 0.02f, body);
            Box("Edge", panel, new Vector3(0f, 2.07f, 0.57f), new Vector3(1.7f, 0.018f, 0.01f), BuildKit.MakeEmissive(accent, 1.2f));
            if (!last)
            {
                // A chevron in the gap to the next panel (+X), the reading order.
                var arrow = new List<CombineInstance>
                {
                    Piece(CubeMesh, new Vector3(1.11f, 1.87f, 0.58f), new Vector3(0f, 0f, 45f), new Vector3(0.03f, 0.14f, 0.02f)),
                    Piece(CubeMesh, new Vector3(1.11f, 1.77f, 0.58f), new Vector3(0f, 0f, -45f), new Vector3(0.03f, 0.14f, 0.02f)),
                };
                Batch("NextArrow", panel, BuildKit.MakeEmissive(accent, 1.6f), arrow);
            }
            return panel;
        }

        // ---- Docks --------------------------------------------------------------

        /// <summary>Restyles a STEP drop zone as a dock: a stand with the step
        /// number on its front, a shelf with a lit card outline, a hidden
        /// binder (SeatedCard) shown once the right card is placed, and a lamp
        /// (DockLamp) PlaybookStation colours by state. Replaces the generic
        /// pedestal and its collider.</summary>
        public static void DressDock(GameObject zone, int stepNumber, string step, Color accent)
        {
            Transform zt = zone.transform;
            if (zt.Find("Dock") != null) return;
            Remove(zt, "Pedestal", "TopGlow");
            var dock = new GameObject("Dock").transform;
            dock.SetParent(zt, false);

            Material body = Mat(BodyColor, 0.45f, 0.4f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Block("Base", dock, Vector3.zero, Vector3.zero, 0f, 0.08f, new Vector2(0.34f, 0.28f), new Vector2(0.32f, 0.27f), 0.03f, trim, solid: true);
            Block("Column", dock, Vector3.zero, Vector3.zero, 0.08f, 0.82f, new Vector2(0.24f, 0.19f), new Vector2(0.22f, 0.18f), 0.04f, body, solid: true);
            Block("Shelf", dock, Vector3.zero, Vector3.zero, 0.82f, 0.9f, new Vector2(0.32f, 0.22f), new Vector2(0.32f, 0.22f), 0.025f, trim, solid: true);
            Slab("Back", dock, new Vector3(0f, 1.2f, 0.18f), new Vector3(0.6f, 0.6f, 0.06f), 0.02f, body);
            // The silhouette of the card that belongs here, lit until filled.
            var outline = new List<CombineInstance>();
            foreach (CombineInstance edge in Outline(Vector3.zero, 0.48f, 0.58f, 0.014f))
            {
                CombineInstance upright = edge;
                upright.transform = Matrix4x4.TRS(new Vector3(0f, 1.2f, 0.145f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one) * edge.transform;
                outline.Add(upright);
            }
            Batch("CardOutline", dock, BuildKit.MakeEmissive(accent, 1.1f), outline);

            TextMesh number = BuildKit.MakeLabel(dock, new Vector3(0f, 0.5f, -0.195f), stepNumber.ToString(),
                IconColor, 0.05f);
            number.gameObject.name = "DockNumber";
            Slab("DockLamp", dock, new Vector3(0f, 0.74f, -0.19f), new Vector3(0.26f, 0.05f, 0.02f), 0.01f,
                BuildKit.MakeEmissive(new Color(0.3f, 0.32f, 0.38f), 0.4f));

            Transform seated = BuildBinder(dock, "SeatedCard", new Vector3(0f, 0.9f, 0.04f), step, accent, solid: false);
            seated.gameObject.SetActive(false);
        }

        // ---- Cards ----------------------------------------------------------------

        /// <summary>Restyles a playbook card as a binder standing upright, its
        /// step printed on the front with an icon, replacing the generic box.</summary>
        public static void DressCard(Carryable card, string step, Color accent)
        {
            Transform ct = card.transform;
            if (ct.Find("Binder") != null) return;
            Remove(ct, "Box", "Stripe");
            BuildBinder(ct, "Binder", Vector3.zero, step, accent, solid: true);

            // The floating name tag becomes the printed title on the cover:
            // floating, it drew across the step labels on the wall behind.
            TextMesh label = null;
            foreach (TextMesh tm in card.GetComponentsInChildren<TextMesh>(true))
                if (tm.transform.parent == ct) { label = tm; break; }
            if (label != null)
            {
                foreach (Billboard b in label.GetComponents<Billboard>()) Object.Destroy(b);
                label.transform.localPosition = new Vector3(0f, 0.16f, -0.078f);
                label.transform.localRotation = Quaternion.identity;
                label.color = IconColor;
                FitToWidth(label, 0.4f, 0.014f);
                WorldTextLayoutIntent.Configure(label.gameObject, WorldTextLayoutIntent.Mode.Mounted);
            }
        }

        /// <summary>A binder about 0.46 x 0.56 m, bottom at its origin, cover
        /// facing -Z: dark cover, accent spine on the left, rings, and the
        /// step's icon.</summary>
        private static Transform BuildBinder(Transform parent, string name, Vector3 pos, string step, Color accent, bool solid)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            Block("Cover", root, Vector3.zero, Vector3.zero, 0f, 0.56f, new Vector2(0.23f, 0.07f), new Vector2(0.23f, 0.07f), 0.02f,
                Mat(BinderColor, 0.45f, 0.3f), solid);
            Block("Spine", root, new Vector3(-0.215f, 0f, 0f), Vector3.zero, 0.005f, 0.555f, new Vector2(0.025f, 0.072f), new Vector2(0.025f, 0.072f), 0.008f,
                BuildKit.MakeEmissive(accent, 0.9f));
            var rings = new List<CombineInstance>();
            foreach (float y in new[] { 0.12f, 0.44f })
                rings.Add(Piece(CharacterMesh.Annulus(8, 0.018f, 0.03f, 0f, 0.012f, "Ring", 22.5f), new Vector3(-0.17f, y, -0.072f), new Vector3(-90f, 0f, 0f), Vector3.one));
            Batch("Rings", root, Mat(new Color(0.58f, 0.61f, 0.68f), 0.75f, 0.9f), rings);
            Transform icon = Batch("Icon", root, BuildKit.MakeEmissive(IconColor, 1.2f), IconPieces(step));
            icon.localPosition = new Vector3(0.02f, 0.36f, -0.075f);
            return root;
        }

        /// <summary>An icon ~0.16 m across in the X/Y plane, facing -Z.</summary>
        private static List<CombineInstance> IconPieces(string step)
        {
            const float t = 0.006f;
            Vector3 face = new Vector3(-90f, 0f, 0f);
            var p = new List<CombineInstance>();
            switch (step)
            {
                case "PREPARATION": // clipboard with a tick
                    foreach (CombineInstance e in Outline(Vector3.zero, 0.12f, 0.15f, 0.014f))
                    {
                        CombineInstance up = e;
                        up.transform = Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f)) * e.transform;
                        p.Add(up);
                    }
                    p.Add(Piece(CubeMesh, new Vector3(0f, 0.085f, 0f), Vector3.zero, new Vector3(0.05f, 0.025f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(-0.022f, -0.01f, 0f), new Vector3(0f, 0f, 45f), new Vector3(0.012f, 0.045f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0.016f, 0.005f, 0f), new Vector3(0f, 0f, -35f), new Vector3(0.012f, 0.085f, t)));
                    break;
                case "DETECTION": // magnifier
                    p.Add(Piece(CharacterMesh.Annulus(14, 0.04f, 0.056f, 0f, t, "Lens"), new Vector3(-0.015f, 0.015f, 0f), face, Vector3.one));
                    p.Add(Piece(CubeMesh, new Vector3(0.045f, -0.045f, 0f), new Vector3(0f, 0f, 45f), new Vector3(0.018f, 0.075f, t)));
                    break;
                case "CONTAINMENT": // a threat held inside a wall
                    foreach (CombineInstance e in Outline(Vector3.zero, 0.15f, 0.15f, 0.016f))
                    {
                        CombineInstance up = e;
                        up.transform = Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f)) * e.transform;
                        p.Add(up);
                    }
                    p.Add(Piece(CharacterMesh.Prism(8, 0.03f, 0.03f, 0f, t, "Threat", 22.5f), Vector3.zero, face, Vector3.one));
                    break;
                case "ERADICATION": // X
                    p.Add(Piece(CubeMesh, Vector3.zero, new Vector3(0f, 0f, 45f), new Vector3(0.022f, 0.17f, t)));
                    p.Add(Piece(CubeMesh, Vector3.zero, new Vector3(0f, 0f, -45f), new Vector3(0.022f, 0.17f, t)));
                    break;
                case "RECOVERY": // circular arrow
                    p.Add(Piece(CharacterMesh.Annulus(14, 0.05f, 0.066f, 0f, t, "Loop"), Vector3.zero, face, Vector3.one));
                    p.Add(Piece(CubeMesh, new Vector3(0.07f, 0.03f, 0f), new Vector3(0f, 0f, 30f), new Vector3(0.016f, 0.05f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0.045f, 0.035f, 0f), new Vector3(0f, 0f, -60f), new Vector3(0.016f, 0.05f, t)));
                    break;
                default: // LESSONS LEARNED: a light bulb
                    p.Add(Piece(CharacterMesh.Prism(12, 0.05f, 0.05f, 0f, t, "Bulb"), new Vector3(0f, 0.02f, 0f), face, Vector3.one));
                    p.Add(Piece(CubeMesh, new Vector3(0f, -0.05f, 0f), Vector3.zero, new Vector3(0.05f, 0.03f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0f, -0.075f, 0f), Vector3.zero, new Vector3(0.036f, 0.012f, t)));
                    break;
            }
            return p;
        }

        // ---- Rack -------------------------------------------------------------------

        /// <summary>The card rack as a desk (top at y = 0.9) with a lit bay per
        /// card and its instruction printed on the front instead of floating.</summary>
        public static void DressRack(GameObject rack, float width, IList<float> bayX, Color accent)
        {
            Transform rt = rack.transform;
            if (rt.Find("RackDesk") != null) return;
            Transform table = rt.Find("Table");
            if (table != null && table.GetComponent<Renderer>() != null) table.GetComponent<Renderer>().enabled = false;

            var desk = new GameObject("RackDesk").transform;
            desk.SetParent(rt, false);
            float half = width * 0.5f;
            Material body = Mat(BodyColor, 0.45f, 0.4f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Material recess = Mat(new Color(0.035f, 0.04f, 0.06f), 0.3f, 0.2f);
            Block("Plinth", desk, Vector3.zero, Vector3.zero, 0f, 0.08f, new Vector2(half - 0.1f, 0.6f), new Vector2(half - 0.1f, 0.6f), 0.02f, recess);
            Block("Body", desk, Vector3.zero, Vector3.zero, 0.08f, 0.84f, new Vector2(half - 0.03f, 0.68f), new Vector2(half, 0.7f), 0.05f, body);
            Block("Top", desk, Vector3.zero, Vector3.zero, 0.84f, 0.9f, new Vector2(half + 0.04f, 0.74f), new Vector2(half + 0.04f, 0.74f), 0.025f, trim);
            Box("FrontTrim", desk, new Vector3(0f, 0.87f, -0.745f), new Vector3(width, 0.025f, 0.012f), BuildKit.MakeEmissive(accent, 1.3f));
            var bays = new List<CombineInstance>();
            foreach (float x in bayX) bays.AddRange(Outline(new Vector3(x, 0.903f, 0f), 0.62f, 0.3f, 0.014f));
            Batch("CardBays", desk, BuildKit.MakeEmissive(accent, 0.8f), bays);

            foreach (TextMesh label in rt.GetComponentsInChildren<TextMesh>(true))
            {
                if (label.transform.parent != rt) continue;
                foreach (Billboard b in label.GetComponents<Billboard>()) Object.Destroy(b);
                label.text = "SHUFFLED CARDS  —  E TO PICK UP";
                label.characterSize = 0.022f;
                label.transform.localPosition = new Vector3(0f, 0.5f, -0.72f);
                label.transform.localRotation = Quaternion.identity;
                WorldTextLayoutIntent.Configure(label.gameObject, WorldTextLayoutIntent.Mode.Mounted);
                Slab("LabelPlate", desk, new Vector3(0f, 0.5f, -0.705f), new Vector3(4.6f, 0.22f, 0.02f), 0.01f, recess);
                break;
            }
        }

        // ---- Helpers ------------------------------------------------------------------

        public static void FitToWidth(TextMesh label, float allowedWidth, float preferredSize)
        {
            label.characterSize = preferredSize;
            Renderer r = label.GetComponent<Renderer>();
            if (r == null) return;
            Vector3 right = label.transform.right;
            Vector3 axis = new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z));
            float width = 2f * Vector3.Dot(r.bounds.extents, axis);
            if (width > allowedWidth && width > 0.001f) label.characterSize *= allowedWidth / width;
        }
    }
}
