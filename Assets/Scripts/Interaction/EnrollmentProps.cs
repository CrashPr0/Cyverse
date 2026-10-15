using System.Collections.Generic;
using UnityEngine;
using Cyverse.Level;
using static Cyverse.Level.BlockKit;

namespace Cyverse.Interaction
{
    /// <summary>
    /// What the enrollment kiosk looks like, in the blocky, chamfered style of
    /// the MFA area: a floor bay with shoe prints where the player stands, a
    /// pedestal with a ledge carrying a palm reader and the badge printer's
    /// slot, the screen set in a deep bezel between two lit fins under a
    /// face-capture camera, and an ENROLLMENT header plate above it.
    /// BadgeStation keeps the behaviour and its pitched screen (PanelBody /
    /// PanelScreen / ScanBar); PanelBody's box is hidden inside the new head
    /// but keeps its collider.
    /// </summary>
    public static class EnrollmentProps
    {
        /// <summary>The badge card inside the printer, and where it rests
        /// once issued, half out of the slot (kiosk local space).</summary>
        public static readonly Vector3 CardHidden = new Vector3(-0.2f, 0.892f, -0.30f);
        public static readonly Vector3 CardIssued = new Vector3(-0.2f, 0.892f, -0.385f);

        private static readonly Color BodyColor = new Color(0.075f, 0.085f, 0.115f);
        private static readonly Color TrimColor = new Color(0.17f, 0.19f, 0.25f);
        private static readonly Color RecessColor = new Color(0.02f, 0.025f, 0.04f);
        private static readonly Color BayColor = new Color(0.05f, 0.06f, 0.085f);
        private static readonly Color CardColor = new Color(0.86f, 0.90f, 0.95f);

        /// <summary>Builds the kiosk around <paramref name="panelBody"/> and
        /// <paramref name="panelScreen"/>, the pitched screen, with the front
        /// toward -Z. Returns the badge card, inactive inside the printer
        /// until BadgeStation issues it.</summary>
        public static Transform BuildKiosk(Transform root, Transform panelBody, Transform panelScreen, Color accent)
        {
            Material body = Mat(BodyColor, 0.45f, 0.4f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Material recess = Mat(RecessColor, 0.3f, 0.2f);
            Material glow = BuildKit.MakeEmissive(accent, 1.1f);

            BuildBay(root, accent);

            // Pedestal, the ledge it carries and a spine up to the head.
            Block("Plinth", root, new Vector3(0f, 0f, -0.02f), Vector3.zero, 0f, 0.06f,
                new Vector2(0.40f, 0.32f), new Vector2(0.39f, 0.31f), 0.02f, trim, solid: true);
            Block("Pedestal", root, new Vector3(0f, 0f, -0.02f), Vector3.zero, 0.06f, 0.86f,
                new Vector2(0.27f, 0.17f), new Vector2(0.22f, 0.17f), 0.05f, body, solid: true);
            Box("PedestalGlow", root, new Vector3(0f, 0.42f, -0.192f), new Vector3(0.022f, 0.55f, 0.006f), glow);
            Block("Ledge", root, new Vector3(0f, 0f, -0.11f), Vector3.zero, 0.86f, 0.92f,
                new Vector2(0.40f, 0.22f), new Vector2(0.42f, 0.23f), 0.02f, trim, solid: true);
            Box("LedgeGlow", root, new Vector3(0.06f, 0.866f, -0.334f), new Vector3(0.36f, 0.008f, 0.006f), glow);
            Block("Spine", root, new Vector3(0f, 0f, 0.12f), Vector3.zero, 0.92f, 1.42f,
                new Vector2(0.17f, 0.09f), new Vector2(0.15f, 0.08f), 0.03f, body);

            // Palm reader on the right of the ledge, fingers pointing away.
            Slab("PalmPlate", root, new Vector3(0.2f, 0.928f, -0.13f), new Vector3(0.26f, 0.016f, 0.31f), 0.01f, trim);
            Slab("PalmGlass", root, new Vector3(0.2f, 0.937f, -0.13f), new Vector3(0.22f, 0.004f, 0.27f), 0.002f,
                BuildKit.MakeHologram(accent));
            Batch("PalmPrint", root, BuildKit.MakeEmissive(accent, 1.2f), MfaProps.HandPrint(new Vector3(0.215f, 0.94f, -0.15f)));

            // Badge printer slot on the left of the ledge front, and its card.
            Slab("CardSlot", root, new Vector3(-0.2f, 0.892f, -0.345f), new Vector3(0.16f, 0.034f, 0.016f), 0.006f, trim);
            Box("CardSlotMouth", root, new Vector3(-0.2f, 0.892f, -0.354f), new Vector3(0.13f, 0.007f, 0.004f), recess);
            Transform card = BuildCard(root, accent);

            Vector2 screenHalf = new Vector2(panelScreen.localScale.x, panelScreen.localScale.y) * 0.5f;
            BuildHead(root, panelBody, screenHalf, body, trim, glow);
            BuildHeader(root, accent, body, trim, glow);
            return card;
        }

        /// <summary>A dark floor plate with a dim outline and a pair of shoe
        /// prints about a metre in front of the screen: "stand here".</summary>
        private static void BuildBay(Transform root, Color accent)
        {
            Slab("Bay", root, new Vector3(0f, 0.004f, -0.5f), new Vector3(1.4f, 0.008f, 2.0f), 0.004f,
                Mat(BayColor, 0.65f, 0.3f));
            List<CombineInstance> marks = Outline(new Vector3(0f, 0.0105f, -0.5f), 1.34f, 1.94f, 0.01f);

            // Each print is one silhouette: a broad forefoot, a waist narrowed
            // on the arch side and a round heel, toes turned slightly out.
            Mesh disc = CharacterMesh.Prism(16, 1f, 1f, 0f, 1f, "PrintDisc", 11.25f);
            for (int side = -1; side <= 1; side += 2)
            {
                Quaternion yaw = Quaternion.Euler(0f, side * 6f, 0f);
                Vector3 foot = new Vector3(side * 0.13f, 0.008f, -1.05f);
                Vector3 euler = yaw.eulerAngles;
                marks.Add(Piece(disc, foot + yaw * new Vector3(0f, 0f, 0.055f), euler, new Vector3(0.052f, 0.003f, 0.085f)));
                marks.Add(Piece(CubeMesh, foot + yaw * new Vector3(side * 0.008f, 0.0015f, -0.035f), euler, new Vector3(0.07f, 0.003f, 0.1f)));
                marks.Add(Piece(disc, foot + yaw * new Vector3(side * 0.004f, 0f, -0.095f), euler, new Vector3(0.042f, 0.003f, 0.05f)));
            }
            Batch("BayMarks", root, BuildKit.MakeEmissive(accent, 0.3f), marks);
        }

        private static Transform BuildCard(Transform root, Color accent)
        {
            var card = new GameObject("BadgeCard").transform;
            card.SetParent(root, false);
            card.localPosition = CardHidden;
            Box("CardBody", card, Vector3.zero, new Vector3(0.10f, 0.003f, 0.065f), Mat(CardColor, 0.5f, 0f));
            Box("CardStripe", card, new Vector3(0f, 0.0004f, -0.022f), new Vector3(0.10f, 0.0035f, 0.014f), Mat(accent, 0.5f, 0.1f));
            Box("CardPhoto", card, new Vector3(0.028f, 0.0004f, 0.006f), new Vector3(0.024f, 0.0035f, 0.028f),
                Mat(new Color(0.12f, 0.14f, 0.2f), 0.4f, 0f));
            Box("CardChip", card, new Vector3(-0.03f, 0.0004f, 0.008f), new Vector3(0.012f, 0.0035f, 0.01f),
                Mat(new Color(0.85f, 0.68f, 0.3f), 0.7f, 0.9f));
            card.gameObject.SetActive(false);
            return card;
        }

        /// <summary>The head, in the screen's pitched frame (screen surface at
        /// z = -0.042, <paramref name="half"/> its half size): a deep shell, a
        /// bevelled bezel, lit side fins and the camera pod on top.</summary>
        private static void BuildHead(Transform root, Transform panelBody, Vector2 half,
            Material body, Material trim, Material glow)
        {
            var head = new GameObject("Head").transform;
            head.SetParent(root, false);
            head.localPosition = panelBody.localPosition;
            head.localRotation = panelBody.localRotation;
            Renderer backing = panelBody.GetComponent<Renderer>();
            if (backing != null) backing.enabled = false;

            float sw = half.x, sh = half.y;
            Slab("HeadShell", head, new Vector3(0f, 0f, 0.05f), new Vector3(2f * sw + 0.18f, 2f * sh + 0.18f, 0.16f), 0.04f, body);
            Slab("HeadBack", head, new Vector3(0f, -0.06f, 0.18f), new Vector3(0.5f, 0.5f, 0.14f), 0.04f, body);

            // Bezel bars overlap the screen edge slightly to hide the hologram's rim.
            Mesh stile = CharacterMesh.BevelBlock(-(sh + 0.09f), sh + 0.09f, new Vector2(0.05f, 0.015f), new Vector2(0.05f, 0.015f), 0.012f, 0f, "BezelStile");
            Mesh rail = CharacterMesh.BevelBlock(-0.05f, 0.05f, new Vector2(sw - 0.007f, 0.015f), new Vector2(sw - 0.007f, 0.015f), 0.012f, 0f, "BezelRail");
            Batch("Bezel", head, trim, new List<CombineInstance>
            {
                Piece(stile, new Vector3(-(sw + 0.043f), 0f, -0.045f), Vector3.zero, Vector3.one),
                Piece(stile, new Vector3(sw + 0.043f, 0f, -0.045f), Vector3.zero, Vector3.one),
                Piece(rail, new Vector3(0f, sh + 0.043f, -0.045f), Vector3.zero, Vector3.one),
                Piece(rail, new Vector3(0f, -(sh + 0.043f), -0.045f), Vector3.zero, Vector3.one),
            });

            float finX = sw + 0.12f;
            Slab("Fin_L", head, new Vector3(-finX, 0f, -0.04f), new Vector3(0.04f, 2f * sh + 0.12f, 0.2f), 0.012f, trim);
            Slab("Fin_R", head, new Vector3(finX, 0f, -0.04f), new Vector3(0.04f, 2f * sh + 0.12f, 0.2f), 0.012f, trim);
            Batch("FinGlow", head, glow, new List<CombineInstance>
            {
                Piece(CubeMesh, new Vector3(-finX, 0f, -0.142f), Vector3.zero, new Vector3(0.012f, 2f * sh, 0.006f)),
                Piece(CubeMesh, new Vector3(finX, 0f, -0.142f), Vector3.zero, new Vector3(0.012f, 2f * sh, 0.006f)),
            });

            // Face-capture camera: a dark lens in a ring light, with a tally lamp.
            float camY = sh + 0.145f;
            Slab("CameraPod", head, new Vector3(0f, camY, 0f), new Vector3(0.24f, 0.1f, 0.12f), 0.025f, trim);
            MeshPart("CameraLens", head, CharacterMesh.Prism(8, 0.028f, 0.024f, 0f, 0.02f, "CameraLens", 22.5f),
                new Vector3(0f, camY, -0.06f), new Vector3(-90f, 0f, 0f), Mat(new Color(0.02f, 0.025f, 0.035f), 0.95f, 0.2f));
            MeshPart("CameraRing", head, CharacterMesh.Annulus(16, 0.034f, 0.044f, 0f, 0.006f, "CameraRing"),
                new Vector3(0f, camY, -0.062f), new Vector3(-90f, 0f, 0f), glow);
            Box("CameraTally", head, new Vector3(0.085f, camY, -0.062f), new Vector3(0.01f, 0.01f, 0.004f),
                BuildKit.MakeEmissive(new Color(1f, 0.3f, 0.25f), 1.4f));
        }

        /// <summary>The kiosk's name on a plate standing on a bracket at the
        /// back of the head, just above the camera. It is mounted text, so it
        /// always shows, where a floating sign fades whenever another label
        /// crosses it on screen.</summary>
        private static void BuildHeader(Transform root, Color accent, Material body, Material trim, Material glow)
        {
            const float z = 0.31f, plateY = 2.14f;
            Slab("HeaderBracket", root, new Vector3(0f, 1.93f, z - 0.01f), new Vector3(0.24f, 0.22f, 0.05f), 0.015f, trim);
            Slab("HeaderPlate", root, new Vector3(0f, plateY, z), new Vector3(0.92f, 0.22f, 0.05f), 0.02f, body);
            Box("HeaderGlow", root, new Vector3(0f, plateY + 0.095f, z - 0.027f), new Vector3(0.8f, 0.01f, 0.006f), glow);
            TextMesh title = BuildKit.MakeLabel(root, new Vector3(0f, plateY - 0.01f, z - 0.03f), "ENROLLMENT", accent, 0.0165f);
            title.gameObject.name = "HeaderTitle";
        }
    }
}
