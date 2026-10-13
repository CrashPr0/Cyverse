using System.Collections.Generic;
using UnityEngine;
using Cyverse.Level;
using static Cyverse.Level.BlockKit;

namespace Cyverse.Interaction
{
    /// <summary>
    /// What Data Triage looks like, in the blocky style of the MFA area: the
    /// intake desk with a marked bay per crate, the data crates as hard cases
    /// with an icon for what is inside, and each role's desk with an inbox
    /// tray and a hologram bust of the person it stands for. SortingStation
    /// keeps the rules; these only build what the player sees, plus solid
    /// colliders where the old primitives had them.
    /// </summary>
    public static class TriageProps
    {
        public enum CrateIcon { Info, Money, Key, Medical }

        private static readonly Color BodyColor = new Color(0.075f, 0.085f, 0.115f);
        private static readonly Color TrimColor = new Color(0.17f, 0.19f, 0.25f);
        private static readonly Color CaseColor = new Color(0.13f, 0.14f, 0.19f);
        private static readonly Color ChromeColor = new Color(0.58f, 0.61f, 0.68f);
        private static readonly Color IconColor = new Color(0.85f, 0.95f, 1f);

        /// <summary>The icon on a crate, from its id: public info, payroll,
        /// admin keys or medical records.</summary>
        public static CrateIcon IconFor(string crateId)
        {
            switch (crateId)
            {
                case "payroll": return CrateIcon.Money;
                case "rootkeys": return CrateIcon.Key;
                case "medical": return CrateIcon.Medical;
                default: return CrateIcon.Info;
            }
        }

        // ---- Intake desk ------------------------------------------------------

        /// <summary>The intake desk, top at y=0.9, front toward -Z, with a lit
        /// bay at each crate slot.</summary>
        public static void BuildIntakeDesk(Transform root, Color accent, IList<Vector3> crateSlots)
        {
            Material body = Mat(BodyColor, 0.45f, 0.4f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Material recess = Mat(new Color(0.035f, 0.04f, 0.06f), 0.3f, 0.2f);
            Material glow = BuildKit.MakeEmissive(accent, 1.4f);

            Block("DeskPlinth", root, Vector3.zero, Vector3.zero, 0f, 0.08f, new Vector2(1.8f, 0.8f), new Vector2(1.8f, 0.8f), 0.02f, recess);
            Block("DeskBody", root, Vector3.zero, Vector3.zero, 0.08f, 0.84f, new Vector2(1.88f, 0.88f), new Vector2(1.9f, 0.9f), 0.05f, body, solid: true);
            Block("DeskTop", root, Vector3.zero, Vector3.zero, 0.84f, 0.9f, new Vector2(1.95f, 0.95f), new Vector2(1.95f, 0.95f), 0.025f, trim, solid: true);
            Box("DeskTrim", root, new Vector3(0f, 0.87f, -0.955f), new Vector3(3.8f, 0.03f, 0.012f), glow);

            // Three recessed panels down the front, the middle one labelled.
            for (int i = -1; i <= 1; i++)
                Slab("DeskPanel_" + (i + 1), root, new Vector3(i * 1.24f, 0.46f, -0.905f), new Vector3(1.1f, 0.6f, 0.02f), 0.01f, recess);
            BuildKit.MakeLabel(root, new Vector3(0f, 0.6f, -0.92f), "DATA INTAKE", accent, 0.014f);
            BuildKit.MakeLabel(root, new Vector3(0f, 0.45f, -0.92f), "deliver each crate to the role that needs it",
                new Color(0.62f, 0.72f, 0.80f), 0.0075f, style: FontStyle.Normal);

            var bays = new List<CombineInstance>();
            foreach (Vector3 slot in crateSlots)
                bays.AddRange(Outline(new Vector3(slot.x, 0.903f, slot.z), 0.66f, 0.54f, 0.014f));
            Batch("IntakeBays", root, BuildKit.MakeEmissive(accent, 0.9f), bays);
        }

        // ---- Crates -----------------------------------------------------------

        /// <summary>Restyles a data crate as a hard case with the icon for
        /// what it holds, replacing the generic box and its collider.</summary>
        public static void DressCrate(GameObject crate, string crateId, Color accent)
        {
            Remove(crate.transform, "Box", "Stripe");
            BuildCrateCase(crate.transform, "Case", Vector3.zero, IconFor(crateId), accent, 1f, solid: true);
        }

        /// <summary>A hard case, bottom at its origin: body, lid with a lit
        /// seam, handle, latches and the icon on its front and back.</summary>
        public static Transform BuildCrateCase(Transform parent, string name, Vector3 localPos, CrateIcon icon,
            Color accent, float scale, bool solid)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localScale = Vector3.one * scale;
            Material chrome = Mat(ChromeColor, 0.75f, 0.9f);

            Block("CaseBody", root, Vector3.zero, Vector3.zero, 0f, 0.30f, new Vector2(0.26f, 0.21f), new Vector2(0.26f, 0.21f),
                0.03f, Mat(CaseColor, 0.45f, 0.35f), solid);
            Block("CaseLid", root, Vector3.zero, Vector3.zero, 0.30f, 0.40f, new Vector2(0.265f, 0.215f), new Vector2(0.25f, 0.20f),
                0.025f, Mat(TrimColor, 0.55f, 0.6f));
            Batch("CaseSeam", root, BuildKit.MakeEmissive(accent, 1.6f), Outline(new Vector3(0f, 0.30f, 0f), 0.535f, 0.435f, 0.012f));

            var hardware = new List<CombineInstance>
            {
                Piece(CubeMesh, new Vector3(-0.08f, 0.43f, 0f), Vector3.zero, new Vector3(0.025f, 0.06f, 0.025f)),
                Piece(CubeMesh, new Vector3(0.08f, 0.43f, 0f), Vector3.zero, new Vector3(0.025f, 0.06f, 0.025f)),
                Piece(CubeMesh, new Vector3(0f, 0.465f, 0f), Vector3.zero, new Vector3(0.2f, 0.025f, 0.035f)),
                Piece(CubeMesh, new Vector3(-0.14f, 0.30f, -0.217f), Vector3.zero, new Vector3(0.05f, 0.06f, 0.016f)),
                Piece(CubeMesh, new Vector3(0.14f, 0.30f, -0.217f), Vector3.zero, new Vector3(0.05f, 0.06f, 0.016f)),
            };
            Batch("CaseHardware", root, chrome, hardware);

            var glyph = new List<CombineInstance>();
            Matrix4x4 front = Matrix4x4.Translate(new Vector3(0f, 0.15f, -0.214f));
            Matrix4x4 back = Matrix4x4.TRS(new Vector3(0f, 0.15f, 0.214f), Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            foreach (CombineInstance piece in IconPieces(icon))
            {
                glyph.Add(new CombineInstance { mesh = piece.mesh, transform = front * piece.transform });
                glyph.Add(new CombineInstance { mesh = piece.mesh, transform = back * piece.transform });
            }
            Batch("CaseIcon", root, BuildKit.MakeEmissive(IconColor, 1.3f), glyph);
            return root;
        }

        /// <summary>An icon about 0.15 m across, drawn in the X/Y plane facing -Z.</summary>
        private static List<CombineInstance> IconPieces(CrateIcon icon)
        {
            const float t = 0.006f; // depth
            Vector3 faceForward = new Vector3(-90f, 0f, 0f);
            var p = new List<CombineInstance>();
            switch (icon)
            {
                case CrateIcon.Info: // (i)
                    p.Add(Piece(CharacterMesh.Annulus(16, 0.068f, 0.082f, 0f, t, "InfoRing"), Vector3.zero, faceForward, Vector3.one));
                    p.Add(Piece(CubeMesh, new Vector3(0f, 0.035f, 0f), Vector3.zero, new Vector3(0.024f, 0.024f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0f, -0.018f, 0f), Vector3.zero, new Vector3(0.024f, 0.06f, t)));
                    break;
                case CrateIcon.Money: // $ as a segmented S with a stroke through it
                    const float w = 0.075f, h = 0.12f, s = 0.02f;
                    p.Add(Piece(CubeMesh, new Vector3(0f, h * 0.5f, 0f), Vector3.zero, new Vector3(w, s, t)));
                    p.Add(Piece(CubeMesh, new Vector3(-w * 0.5f, h * 0.25f, 0f), Vector3.zero, new Vector3(s, h * 0.5f, t)));
                    p.Add(Piece(CubeMesh, Vector3.zero, Vector3.zero, new Vector3(w, s, t)));
                    p.Add(Piece(CubeMesh, new Vector3(w * 0.5f, -h * 0.25f, 0f), Vector3.zero, new Vector3(s, h * 0.5f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0f, -h * 0.5f, 0f), Vector3.zero, new Vector3(w, s, t)));
                    p.Add(Piece(CubeMesh, Vector3.zero, Vector3.zero, new Vector3(s * 0.6f, h + 0.05f, t)));
                    break;
                case CrateIcon.Key: // ring, shaft and two teeth
                    p.Add(Piece(CharacterMesh.Annulus(12, 0.022f, 0.042f, 0f, t, "KeyRing"), new Vector3(-0.055f, 0f, 0f), faceForward, Vector3.one));
                    p.Add(Piece(CubeMesh, new Vector3(0.03f, 0f, 0f), Vector3.zero, new Vector3(0.13f, 0.02f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0.065f, -0.02f, 0f), Vector3.zero, new Vector3(0.016f, 0.03f, t)));
                    p.Add(Piece(CubeMesh, new Vector3(0.088f, -0.024f, 0f), Vector3.zero, new Vector3(0.016f, 0.038f, t)));
                    break;
                default: // medical cross
                    p.Add(Piece(CubeMesh, Vector3.zero, Vector3.zero, new Vector3(0.12f, 0.04f, t)));
                    p.Add(Piece(CubeMesh, Vector3.zero, Vector3.zero, new Vector3(0.04f, 0.12f, t)));
                    break;
            }
            return p;
        }

        // ---- Role desks -------------------------------------------------------

        /// <summary>Height at which a role desk's inbox takes delivered crates.</summary>
        public const float InboxHeight = 1.0f;
        public const float DeliveredScale = 0.62f;
        public const float DeliveredSpacing = 0.29f;

        /// <summary>Restyles a role drop zone as that role's desk: an inbox
        /// tray with one lit bay per crate the role takes (so HR's desk is
        /// visibly built for two), and a hologram bust of the person behind
        /// it, with the role label lifted above the bust.</summary>
        public static void DressRoleDesk(GameObject zone, string role, Color accent, int bays = 1)
        {
            Transform zt = zone.transform;
            Remove(zt, "Pedestal", "TopGlow");
            bays = Mathf.Max(1, bays);

            Material body = Mat(BodyColor, 0.45f, 0.4f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Material glow = BuildKit.MakeEmissive(accent, 1.4f);
            // A single bay keeps the original square desk; each extra bay
            // widens the inbox, and the column and base widen with it.
            float half = bays > 1 ? 0.22f + bays * 0.2f : 0.34f;
            float column = half - 0.12f;
            Block("DeskBase", zt, Vector3.zero, Vector3.zero, 0f, 0.1f, new Vector2(half + 0.02f, 0.30f), new Vector2(half, 0.28f), 0.03f, trim, solid: true);
            Block("DeskColumn", zt, Vector3.zero, Vector3.zero, 0.1f, 0.9f, new Vector2(column + 0.02f, 0.20f), new Vector2(column, 0.18f), 0.04f, body, solid: true);
            Block("Inbox", zt, Vector3.zero, Vector3.zero, 0.9f, InboxHeight, new Vector2(half, 0.30f), new Vector2(half, 0.30f), 0.03f, trim, solid: true);
            var rim = new List<CombineInstance>
            {
                Piece(CubeMesh, new Vector3(0f, InboxHeight + 0.04f, 0.28f), Vector3.zero, new Vector3(half * 2f, 0.08f, 0.04f)),
                Piece(CubeMesh, new Vector3(-half + 0.02f, InboxHeight + 0.04f, 0f), Vector3.zero, new Vector3(0.04f, 0.08f, 0.6f)),
                Piece(CubeMesh, new Vector3(half - 0.02f, InboxHeight + 0.04f, 0f), Vector3.zero, new Vector3(0.04f, 0.08f, 0.6f)),
            };
            // Dividers between bays.
            for (int i = 1; i < bays; i++)
                rim.Add(Piece(CubeMesh, new Vector3((BayX(i - 1, bays) + BayX(i, bays)) * 0.5f, InboxHeight + 0.025f, -0.01f),
                    Vector3.zero, new Vector3(0.025f, 0.05f, 0.5f)));
            Batch("InboxRim", zt, trim, rim);
            var beds = new List<CombineInstance>();
            float bedWidth = bays > 1 ? 0.34f : 0.5f;
            for (int i = 0; i < bays; i++)
                beds.AddRange(Outline(new Vector3(BayX(i, bays), InboxHeight + 0.003f, -0.01f), bedWidth, 0.44f, 0.012f));
            Batch("InboxBed", zt, BuildKit.MakeEmissive(accent, 0.9f), beds);
            Box("DeskAccent", zt, new Vector3(0f, 0.5f, -0.205f), new Vector3(0.025f, 0.6f, 0.01f), glow);

            // Projector behind the inbox, and the person it shows. The bust
            // and role name float above head height, a layer above the crate
            // names on the intake desk, so the two never draw over each other
            // from the desk.
            Box("ProjectorPost", zt, new Vector3(0f, 1.55f, 0.22f), new Vector3(0.05f, 1.1f, 0.05f), body);
            MeshPart("Projector", zt, CharacterMesh.Prism(8, 0.07f, 0.06f, 0f, 0.04f, "Projector", 22.5f),
                new Vector3(0f, 2.1f, 0.22f), Vector3.zero, trim);
            MeshPart("ProjectorLens", zt, CharacterMesh.Annulus(8, 0.03f, 0.055f, 0f, 0.006f, "ProjectorLens", 22.5f),
                new Vector3(0f, 2.14f, 0.22f), Vector3.zero, BuildKit.MakeEmissive(accent, 2.2f));
            BuildRoleBust(zt, role, new Vector3(0f, 2.2f, 0.22f), accent);

            TextMesh label = zt.GetComponentInChildren<TextMesh>(true);
            if (label != null) label.transform.localPosition = new Vector3(0f, 3.0f, 0f);
        }

        /// <summary>A delivered crate, filed in the next free bay of a role
        /// desk's inbox (stacking only if it ever runs out of bays).</summary>
        public static Transform FileCrate(Transform zone, string crateId, int alreadyFiled, int bays, Color accent)
        {
            bays = Mathf.Max(1, bays);
            return BuildCrateCase(zone, "Delivered_" + crateId,
                new Vector3(BayX(alreadyFiled % bays, bays), InboxHeight + alreadyFiled / bays * DeliveredSpacing, 0f),
                IconFor(crateId), accent, DeliveredScale, solid: false);
        }

        /// <summary>Centre of bay <paramref name="i"/> of <paramref name="bays"/>, side by side along X.</summary>
        private static float BayX(int i, int bays) => bays <= 1 ? 0f : (i - (bays - 1) * 0.5f) * 0.4f;

        /// <summary>A hologram head and shoulders for a role, each with its own
        /// tell: the intern's lanyard badge, the HR manager's tie, the
        /// sysadmin's headset.</summary>
        private static void BuildRoleBust(Transform parent, string role, Vector3 pos, Color accent)
        {
            var bust = new GameObject("RoleBust").transform;
            bust.SetParent(parent, false);
            bust.localPosition = pos;
            bust.localScale = Vector3.one * 1.4f; // readable from the intake desk

            var parts = new List<CombineInstance>
            {
                Piece(CharacterMesh.BevelBlock(0f, 0.14f, new Vector2(0.17f, 0.08f), new Vector2(0.13f, 0.07f), 0.03f, 0f, "Shoulders"),
                    Vector3.zero, Vector3.zero, Vector3.one),
                Piece(CubeMesh, new Vector3(0f, 0.155f, 0f), Vector3.zero, new Vector3(0.06f, 0.04f, 0.06f)),
                Piece(CharacterMesh.BevelBlock(0.17f, 0.36f, new Vector2(0.075f, 0.075f), new Vector2(0.075f, 0.075f), 0.025f, 0f, "Head"),
                    Vector3.zero, Vector3.zero, Vector3.one),
            };
            switch (role)
            {
                case "INTERN":
                    parts.Add(Piece(CubeMesh, new Vector3(-0.03f, 0.105f, -0.078f), new Vector3(0f, 0f, -25f), new Vector3(0.012f, 0.09f, 0.01f)));
                    parts.Add(Piece(CubeMesh, new Vector3(0.03f, 0.105f, -0.078f), new Vector3(0f, 0f, 25f), new Vector3(0.012f, 0.09f, 0.01f)));
                    parts.Add(Piece(CubeMesh, new Vector3(0f, 0.035f, -0.084f), Vector3.zero, new Vector3(0.05f, 0.065f, 0.01f)));
                    break;
                case "HR MANAGER":
                    parts.Add(Piece(CubeMesh, new Vector3(0f, 0.125f, -0.076f), Vector3.zero, new Vector3(0.03f, 0.025f, 0.01f)));
                    parts.Add(Piece(CharacterMesh.BevelBlock(0f, 0.1f, new Vector2(0.006f, 0.004f), new Vector2(0.016f, 0.004f), 0.003f, 0f, "Tie"),
                        new Vector3(0f, 0.11f, -0.078f), new Vector3(180f, 0f, 0f), Vector3.one));
                    break;
                case "SYSADMIN":
                    parts.Add(Piece(CubeMesh, new Vector3(0f, 0.375f, 0f), Vector3.zero, new Vector3(0.15f, 0.02f, 0.03f)));
                    parts.Add(Piece(CubeMesh, new Vector3(-0.085f, 0.325f, 0f), Vector3.zero, new Vector3(0.02f, 0.09f, 0.03f)));
                    parts.Add(Piece(CubeMesh, new Vector3(0.085f, 0.325f, 0f), Vector3.zero, new Vector3(0.02f, 0.09f, 0.03f)));
                    parts.Add(Piece(CubeMesh, new Vector3(-0.09f, 0.265f, 0f), Vector3.zero, new Vector3(0.035f, 0.065f, 0.065f)));
                    parts.Add(Piece(CubeMesh, new Vector3(0.09f, 0.265f, 0f), Vector3.zero, new Vector3(0.035f, 0.065f, 0.065f)));
                    // Mic boom from the left ear cup round to the mouth.
                    parts.Add(Piece(CubeMesh, new Vector3(-0.045f, 0.235f, -0.045f), new Vector3(0f, -45f, 0f), new Vector3(0.012f, 0.012f, 0.12f)));
                    break;
            }
            Transform mesh = Batch("BustMesh", bust, BuildKit.MakeHologram(accent), parts);
            PlanarUVs(mesh.GetComponent<MeshFilter>().sharedMesh);
        }
    }
}
