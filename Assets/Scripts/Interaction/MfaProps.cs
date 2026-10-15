using System.Collections.Generic;
using UnityEngine;
using Cyverse.Level;
using static Cyverse.Level.BlockKit;

namespace Cyverse.Interaction
{
    /// <summary>
    /// What the MFA area looks like, in the same blocky, chamfered style as the
    /// MFA Specialist robot: the vault housing and its swing door (one locking
    /// bar per factor), the security token fob, its charging dock and reader,
    /// the biometric scanner, the passcode kiosk and the phone dock. Behaviour
    /// stays on MfaGauntlet, MfaFactor, Carryable and DropZone; these build the
    /// meshes, plus solid colliders where the old primitives had them.
    /// </summary>
    public static class MfaProps
    {
        // Vault layout, in the vault's local space (its face looks along -Z).
        public const float DoorHalfWidth = 1.15f;
        public const float DoorTop = 3.35f;
        public const float HousingTop = 4.0f;
        public const float HingeZ = -0.42f;
        public const float DoorSwingDegrees = -105f;
        public const float LockBarTravel = 0.36f;
        /// <summary>Heights of the KNOW, HAVE and ARE locking bars (factor order).</summary>
        public static readonly float[] LockBarHeights = { 2.85f, 1.75f, 0.65f };
        private static readonly string[] FactorWords = { "KNOW", "HAVE", "ARE" };
        private static readonly string[] FactorHints = { "passcode", "token", "biometric" };

        private static readonly Color HousingColor = new Color(0.075f, 0.085f, 0.115f);
        private static readonly Color TrimColor = new Color(0.17f, 0.19f, 0.25f);
        private static readonly Color RecessColor = new Color(0.035f, 0.04f, 0.06f);
        private static readonly Color DoorColor = new Color(0.24f, 0.26f, 0.32f);
        private static readonly Color ChromeColor = new Color(0.58f, 0.61f, 0.68f);
        private static readonly Color FobColor = new Color(0.12f, 0.13f, 0.17f);
        public static readonly Color Verified = new Color(0.30f, 1f, 0.45f);

        // ---- Vault ------------------------------------------------------------

        /// <summary>The vault: a housing built out from the wall, a swing door
        /// (VaultPanel is its hinge pivot) with a handle wheel, three locking
        /// bars that retract as their factors clear, the factor lamps
        /// (FactorLight_0..2) beside them, a status line (VaultStatus) and a
        /// small chamber behind the door, lit by ChamberLight once it opens.</summary>
        public static void BuildVault(Transform root, Color accent, Material lampOff)
        {
            Material housing = Mat(HousingColor, 0.4f, 0.35f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Material recess = Mat(RecessColor, 0.3f, 0.2f);
            Material chrome = Mat(ChromeColor, 0.75f, 0.9f);
            Material glow = BuildKit.MakeEmissive(accent, 1.6f);

            const float w = DoorHalfWidth, outer = 3.2f;
            float pierX = (w + outer) * 0.5f, pierWidth = outer - w;

            // Housing: piers and a header round the opening, with returns back
            // to the room wall so the vault reads as built in, not standing free.
            Slab("Housing_L", root, new Vector3(-pierX, HousingTop * 0.5f, 0f), new Vector3(pierWidth, HousingTop, 0.6f), 0.06f, housing, solid: true);
            Slab("Housing_R", root, new Vector3(pierX, HousingTop * 0.5f, 0f), new Vector3(pierWidth, HousingTop, 0.6f), 0.06f, housing, solid: true);
            Slab("Housing_Top", root, new Vector3(0f, (DoorTop + HousingTop) * 0.5f, 0f), new Vector3(2f * w + 0.02f, HousingTop - DoorTop, 0.6f), 0.04f, housing, solid: true);
            Slab("Housing_SideL", root, new Vector3(-outer + 0.15f, HousingTop * 0.5f, 1.5f), new Vector3(0.3f, HousingTop, 3.0f), 0.04f, housing, solid: true);
            Slab("Housing_SideR", root, new Vector3(outer - 0.15f, HousingTop * 0.5f, 1.5f), new Vector3(0.3f, HousingTop, 3.0f), 0.04f, housing, solid: true);

            // Front: a raised frame round the opening with a lit seam outside
            // it, inset panels, kick plates and bolt heads.
            const float trimZ = -0.33f;
            Slab("Jamb_L", root, new Vector3(-w - 0.09f, DoorTop * 0.5f + 0.045f, trimZ), new Vector3(0.18f, DoorTop + 0.09f, 0.12f), 0.03f, trim);
            Slab("Jamb_R", root, new Vector3(w + 0.09f, DoorTop * 0.5f + 0.045f, trimZ), new Vector3(0.18f, DoorTop + 0.09f, 0.12f), 0.03f, trim);
            Slab("Jamb_Head", root, new Vector3(0f, DoorTop + 0.09f, trimZ), new Vector3(2f * w + 0.36f, 0.18f, 0.12f), 0.03f, trim);
            Box("Seam_L", root, new Vector3(-w - 0.21f, DoorTop * 0.5f, -0.315f), new Vector3(0.025f, DoorTop - 0.1f, 0.02f), glow);
            Box("Seam_R", root, new Vector3(w + 0.21f, DoorTop * 0.5f, -0.315f), new Vector3(0.025f, DoorTop - 0.1f, 0.02f), glow);
            Box("Seam_Top", root, new Vector3(0f, DoorTop + 0.21f, -0.315f), new Vector3(2f * w + 0.42f, 0.025f, 0.02f), glow);
            Box("Threshold", root, new Vector3(0f, 0.025f, -0.05f), new Vector3(2f * w, 0.05f, 0.7f), trim);

            Mesh bolt = CharacterMesh.Prism(6, 0.035f, 0.03f, 0f, 0.03f, "Bolt");
            var bolts = new List<CombineInstance>();
            float insetW = pierWidth - 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "L" : "R";
                Slab("Inset_" + s, root, new Vector3(side * pierX, 2.05f, -0.305f), new Vector3(insetW, 3.1f, 0.02f), 0.01f, recess);
                Box("Kick_" + s, root, new Vector3(side * pierX, 0.11f, -0.32f), new Vector3(pierWidth, 0.22f, 0.06f), trim);
                for (int cx = -1; cx <= 1; cx += 2)
                    for (int cy = -1; cy <= 1; cy += 2)
                        bolts.Add(Piece(bolt, new Vector3(side * pierX + cx * (insetW * 0.5f + 0.12f), 2.05f + cy * 1.62f, -0.30f),
                            new Vector3(-90f, 0f, 0f), Vector3.one));
            }
            Batch("Bolts", root, chrome, bolts);

            // Hinges on the right-hand jamb: barrels on the door's pivot line,
            // leaves fixed to the housing.
            Mesh barrel = CharacterMesh.Prism(8, 0.07f, 0.07f, 0f, 0.6f, "HingeBarrel", 22.5f);
            var hinges = new List<CombineInstance>();
            foreach (float y in new[] { 0.35f, 2.35f })
            {
                hinges.Add(Piece(barrel, new Vector3(w + 0.01f, y, HingeZ), Vector3.zero, Vector3.one));
                hinges.Add(Piece(CubeMesh, new Vector3(w + 0.17f, y + 0.3f, -0.415f), Vector3.zero, new Vector3(0.32f, 0.5f, 0.03f)));
            }
            Batch("Hinges", root, chrome, hinges);

            // One receiver per locking bar on the left jamb, each with the lamp
            // MfaGauntlet turns green and the factor it is waiting for.
            for (int i = 0; i < 3; i++)
            {
                float y = LockBarHeights[i];
                Slab("Receiver_" + i, root, new Vector3(-w - 0.17f, y, -0.45f), new Vector3(0.30f, 0.24f, 0.16f), 0.03f, trim);
                Slab("FactorLight_" + i, root, new Vector3(-w - 0.52f, y, -0.33f), new Vector3(0.14f, 0.14f, 0.05f), 0.02f, lampOff);
                BuildKit.MakeLabel(root, new Vector3(-w - 0.66f, y + 0.035f, -0.32f), FactorWords[i],
                    accent, 0.016f, anchor: TextAnchor.MiddleRight);
                BuildKit.MakeLabel(root, new Vector3(-w - 0.66f, y - 0.075f, -0.32f), FactorHints[i],
                    new Color(0.62f, 0.72f, 0.80f), 0.010f, anchor: TextAnchor.MiddleRight, style: FontStyle.Normal);
            }

            TextMesh status = BuildKit.MakeLabel(root, new Vector3(0f, 3.78f, -0.31f), StatusText(0), accent, 0.013f);
            status.gameObject.name = "VaultStatus";

            BuildVaultDoor(root, accent, trim, recess, chrome);
            BuildVaultChamber(root, accent, housing, trim, chrome);
        }

        /// <summary>The vault's header line for <paramref name="cleared"/> of 3 factors.</summary>
        public static string StatusText(int cleared) =>
            cleared >= 3 ? "ACCESS GRANTED" : $"LOCKED  ·  {cleared} OF 3 FACTORS";

        private static void BuildVaultDoor(Transform root, Color accent, Material trim, Material recess, Material chrome)
        {
            const float w = DoorHalfWidth;
            var panel = new GameObject("VaultPanel").transform;
            panel.SetParent(root, false);
            panel.localPosition = new Vector3(w + 0.01f, 0f, HingeZ);

            // Positions below are relative to the hinge. The leaf fills the
            // opening; its face sits 0.1 m behind the housing front.
            const float leafX = -1.14f, faceZ = 0.22f, bossY = 1.75f;
            Slab("DoorLeaf", panel, new Vector3(leafX, 1.685f, faceZ + 0.15f), new Vector3(2.24f, 3.25f, 0.30f), 0.05f,
                Mat(DoorColor, 0.62f, 0.75f), solid: true);
            Slab("DoorInset_Top", panel, new Vector3(leafX, 2.92f, faceZ - 0.005f), new Vector3(1.86f, 0.5f, 0.02f), 0.01f, recess);
            Slab("DoorInset_Bottom", panel, new Vector3(leafX, 0.5f, faceZ - 0.005f), new Vector3(1.86f, 0.55f, 0.02f), 0.01f, recess);
            MeshPart("DoorBoss", panel, CharacterMesh.Prism(8, 0.62f, 0.57f, 0f, 0.07f, "DoorBoss", 22.5f),
                new Vector3(leafX, bossY, faceZ), new Vector3(-90f, 0f, 0f), trim);
            MeshPart("BossGroove", panel, CharacterMesh.Annulus(8, 0.47f, 0.51f, 0f, 0.012f, "BossGroove", 22.5f),
                new Vector3(leafX, bossY, faceZ - 0.07f), new Vector3(-90f, 0f, 0f), BuildKit.MakeEmissive(accent, 0.9f));

            // Handle wheel: hub, rim, three spokes and grips in one mesh, spun
            // about its local Z by MfaGauntlet as the door unlocks.
            var wheel = new GameObject("Wheel").transform;
            wheel.SetParent(panel, false);
            wheel.localPosition = new Vector3(leafX, bossY, faceZ - 0.07f);
            Mesh grip = CharacterMesh.Prism(6, 0.04f, 0.035f, 0f, 0.12f, "Grip");
            var wheelParts = new List<CombineInstance>
            {
                Piece(CharacterMesh.Prism(8, 0.12f, 0.10f, 0f, 0.14f, "Hub", 22.5f), Vector3.zero, new Vector3(-90f, 0f, 0f), Vector3.one),
                Piece(CharacterMesh.Annulus(12, 0.32f, 0.40f, 0f, 0.06f, "Rim", 15f), new Vector3(0f, 0f, -0.10f), new Vector3(-90f, 0f, 0f), Vector3.one),
            };
            for (int k = 0; k < 3; k++)
            {
                Quaternion turn = Quaternion.Euler(0f, 0f, k * 120f);
                wheelParts.Add(Piece(CubeMesh, turn * new Vector3(0f, 0.21f, 0f) + new Vector3(0f, 0f, -0.11f),
                    new Vector3(0f, 0f, k * 120f), new Vector3(0.06f, 0.30f, 0.05f)));
                wheelParts.Add(Piece(grip, turn * new Vector3(0f, 0.36f, 0f) + new Vector3(0f, 0f, -0.13f),
                    new Vector3(-90f, 0f, 0f), Vector3.one));
            }
            Batch("WheelMesh", wheel, chrome, wheelParts);

            // Locking bars ride in guides proud of the door and shoot into the
            // receivers on the left jamb; each retracts by LockBarTravel.
            var guides = new List<CombineInstance>();
            for (int i = 0; i < 3; i++)
            {
                float y = LockBarHeights[i];
                Slab("LockBar_" + i, panel, new Vector3(-2.29f, y, -0.03f), new Vector3(0.60f, 0.11f, 0.09f), 0.025f, chrome);
                guides.Add(Piece(CubeMesh, new Vector3(-2.11f, y, 0.095f), Vector3.zero, new Vector3(0.10f, 0.20f, 0.25f)));
            }
            Batch("BarGuides", panel, trim, guides);
        }

        private static void BuildVaultChamber(Transform root, Color accent, Material housing, Material trim, Material chrome)
        {
            const float w = DoorHalfWidth;
            var chamber = new GameObject("VaultChamber").transform;
            chamber.SetParent(root, false);
            Slab("ChamberBack", chamber, new Vector3(0f, 1.7f, 2.95f), new Vector3(2.4f, 3.4f, 0.1f), 0.02f, housing, solid: true);
            Slab("ChamberWall_L", chamber, new Vector3(-w - 0.05f, 1.7f, 1.6f), new Vector3(0.1f, 3.4f, 2.7f), 0.02f, housing, solid: true);
            Slab("ChamberWall_R", chamber, new Vector3(w + 0.05f, 1.7f, 1.6f), new Vector3(0.1f, 3.4f, 2.7f), 0.02f, housing, solid: true);
            Slab("ChamberCeiling", chamber, new Vector3(0f, DoorTop + 0.05f, 1.6f), new Vector3(2.4f, 0.1f, 2.7f), 0.02f, housing);
            Slab("ChamberFloor", chamber, new Vector3(0f, 0.02f, 1.6f), new Vector3(2.3f, 0.04f, 2.7f), 0.01f, trim);

            // Safe-deposit drawers down both walls, as two meshes.
            var drawers = new List<CombineInstance>();
            var handles = new List<CombineInstance>();
            for (int side = -1; side <= 1; side += 2)
                for (int col = 0; col < 4; col++)
                    for (int row = 0; row < 6; row++)
                    {
                        float z = 0.6f + col * 0.55f, y = 0.6f + row * 0.5f;
                        drawers.Add(Piece(CubeMesh, new Vector3(side * (w - 0.015f), y, z), Vector3.zero, new Vector3(0.03f, 0.44f, 0.5f)));
                        handles.Add(Piece(CubeMesh, new Vector3(side * (w - 0.045f), y, z), Vector3.zero, new Vector3(0.03f, 0.04f, 0.16f)));
                    }
            Batch("Drawers", chamber, trim, drawers);
            Batch("DrawerHandles", chamber, chrome, handles);

            Slab("CorePlinth", chamber, new Vector3(0f, 0.45f, 2.2f), new Vector3(0.5f, 0.9f, 0.5f), 0.04f, trim);
            Transform core = MeshPart("CredentialCore", chamber, CharacterMesh.Prism(6, 0.11f, 0.11f, -0.15f, 0.15f, "CredentialCore"),
                new Vector3(0f, 1.15f, 2.2f), Vector3.zero, BuildKit.MakeEmissive(accent, 2.4f));
            core.gameObject.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 35f, 0f);
            BuildKit.MakeLabel(chamber, new Vector3(0f, 2.55f, 2.88f), "PROTECTED DATA", accent, 0.014f);

            var light = new GameObject("ChamberLight").AddComponent<Light>();
            light.transform.SetParent(chamber, false);
            light.transform.localPosition = new Vector3(0f, 2.6f, 1.4f);
            light.type = LightType.Point;
            light.color = accent;
            light.range = 3.4f;
            light.intensity = 0f; // lights cast through the shut door, so it stays off until open
        }

        // ---- Security token ---------------------------------------------------

        /// <summary>A hardware security token: a chamfered key fob with a code
        /// display, a button and a key-ring loop, lying flat with its bottom at
        /// y=0 and its long side along X.</summary>
        public static Transform BuildTokenFob(Transform parent, string name, Vector3 localPos, Color accent, bool solid)
        {
            var fob = new GameObject(name).transform;
            fob.SetParent(parent, false);
            fob.localPosition = localPos;
            Material chrome = Mat(ChromeColor, 0.75f, 0.9f);
            Block("FobBody", fob, Vector3.zero, Vector3.zero, 0f, 0.07f, new Vector2(0.15f, 0.085f), new Vector2(0.14f, 0.078f),
                0.025f, Mat(FobColor, 0.5f, 0.3f), solid);
            Slab("FobDisplay", fob, new Vector3(-0.02f, 0.071f, 0f), new Vector3(0.14f, 0.006f, 0.07f), 0.003f, Mat(RecessColor, 0.6f, 0.1f));
            var digits = new List<CombineInstance>();
            for (int i = 0; i < 6; i++)
                digits.Add(Piece(CubeMesh, new Vector3(-0.07f + i * 0.02f, 0.075f, 0f), Vector3.zero, new Vector3(0.012f, 0.003f, 0.03f)));
            Batch("FobDigits", fob, BuildKit.MakeEmissive(accent, 2.2f), digits);
            Block("FobButton", fob, new Vector3(0.095f, 0.07f, 0f), Vector3.zero, 0f, 0.012f,
                new Vector2(0.022f, 0.022f), new Vector2(0.018f, 0.018f), 0.006f, chrome);
            MeshPart("FobLoop", fob, CharacterMesh.Annulus(8, 0.032f, 0.05f, 0f, 0.025f, "FobLoop", 22.5f),
                new Vector3(-0.185f, 0.022f, 0f), Vector3.zero, chrome);
            return fob;
        }

        /// <summary>The token's charging dock. Its cradle top is at y=1.0, where
        /// the token rests; a charge meter runs down the front (-Z).</summary>
        public static void BuildTokenCharger(Transform rack, Color accent)
        {
            Material body = Mat(HousingColor, 0.45f, 0.4f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Block("ChargerBase", rack, Vector3.zero, Vector3.zero, 0f, 0.12f, new Vector2(0.42f, 0.36f), new Vector2(0.40f, 0.34f), 0.04f, trim, solid: true);
            Block("ChargerBody", rack, Vector3.zero, Vector3.zero, 0.12f, 0.92f, new Vector2(0.30f, 0.26f), new Vector2(0.34f, 0.29f), 0.05f, body, solid: true);
            Block("Cradle", rack, Vector3.zero, Vector3.zero, 0.92f, 1.0f, new Vector2(0.36f, 0.31f), new Vector2(0.36f, 0.31f), 0.03f, trim, solid: true);
            Slab("CradleBed", rack, new Vector3(0f, 1.0f, 0f), new Vector3(0.36f, 0.006f, 0.22f), 0.003f, Mat(RecessColor, 0.3f, 0.2f));
            Batch("CradleRim", rack, BuildKit.MakeEmissive(accent, 1.8f), Outline(new Vector3(0f, 1.005f, 0f), 0.40f, 0.26f, 0.018f));

            // Charge meter: five lit segments following the front's lean.
            var meter = new List<CombineInstance>();
            for (int i = 0; i < 5; i++)
            {
                float y = 0.30f + i * 0.11f;
                float front = 0.26f + 0.03f * (y - 0.12f) / 0.8f;
                meter.Add(Piece(CubeMesh, new Vector3(0f, y, -front - 0.008f), Vector3.zero, new Vector3(0.18f, 0.06f, 0.02f)));
            }
            Batch("ChargeMeter", rack, BuildKit.MakeEmissive(Verified, 1.1f), meter);
            BuildKit.MakeLabel(rack, new Vector3(0f, 0.86f, -0.30f), "CHARGED", Verified, 0.009f);

            Material cable = Mat(new Color(0.03f, 0.03f, 0.04f), 0.35f, 0f);
            Box("Cable_Drop", rack, new Vector3(0f, 0.2f, 0.32f), new Vector3(0.05f, 0.4f, 0.05f), cable);
            Box("Cable_Floor", rack, new Vector3(0f, 0.02f, 0.62f), new Vector3(0.05f, 0.04f, 0.6f), cable);
        }

        /// <summary>Restyles a TOKEN SLOT drop zone as a token reader: a post
        /// with an angled head whose fob-shaped bed glows (ReaderRim) until
        /// the token is seated (SeatedToken, shown by MfaGauntlet). Replaces
        /// the generic pedestal and its collider.</summary>
        public static void DressTokenReader(GameObject zone, Color accent)
        {
            Transform zt = zone.transform;
            Remove(zt, "Pedestal", "TopGlow");

            Material body = Mat(HousingColor, 0.45f, 0.4f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Block("ReaderBase", zt, Vector3.zero, Vector3.zero, 0f, 0.1f, new Vector2(0.30f, 0.26f), new Vector2(0.28f, 0.24f), 0.03f, trim, solid: true);
            Block("ReaderPost", zt, Vector3.zero, Vector3.zero, 0.1f, 0.92f, new Vector2(0.16f, 0.13f), new Vector2(0.14f, 0.12f), 0.04f, body, solid: true);

            // The head tips its face toward the player on the -Z side.
            var head = new GameObject("ReaderHead").transform;
            head.SetParent(zt, false);
            head.localPosition = new Vector3(0f, 0.95f, 0f);
            head.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            Slab("ReaderBody", head, new Vector3(0f, 0.05f, 0f), new Vector3(0.42f, 0.10f, 0.32f), 0.03f, trim, solid: true);
            Slab("ReaderBed", head, new Vector3(0f, 0.101f, 0f), new Vector3(0.32f, 0.004f, 0.19f), 0.002f, Mat(RecessColor, 0.3f, 0.2f));
            Batch("ReaderRim", head, BuildKit.MakeEmissive(accent, 1.8f), Outline(new Vector3(0f, 0.104f, 0f), 0.35f, 0.22f, 0.014f));
            TextMesh insert = BuildKit.MakeLabel(head, new Vector3(0f, 0.05f, -0.165f), "INSERT TOKEN", accent, 0.008f);
            insert.gameObject.name = "ReaderCaption";

            // The zone's floating name sits just above the reader, between the
            // vault's HAVE and ARE rows behind it rather than across them.
            foreach (TextMesh label in zone.GetComponentsInChildren<TextMesh>(true))
            {
                if (label == insert || label.transform.parent != zt) continue;
                label.transform.localPosition = new Vector3(0f, 1.34f, 0f);
                label.characterSize = 0.02f;
            }
            BuildTokenFob(head, "SeatedToken", new Vector3(0f, 0.103f, 0f), accent, solid: false).gameObject.SetActive(false);
        }

        // ---- Factor stations --------------------------------------------------

        /// <summary>The biometric scanner the player faces from its pad: a post
        /// with a palm plate tipped toward them and an iris camera above.
        /// Returns the scan bar (inactive) that sweeps the plate while scanning.</summary>
        public static Transform BuildBioScanner(Transform parent, Vector3 localPos, Color accent)
        {
            var root = new GameObject("BioScanner").transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            Material body = Mat(HousingColor, 0.45f, 0.4f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);

            Block("ScannerBase", root, Vector3.zero, Vector3.zero, 0f, 0.08f, new Vector2(0.28f, 0.22f), new Vector2(0.27f, 0.21f), 0.03f, trim, solid: true);
            Block("ScannerPost", root, Vector3.zero, Vector3.zero, 0.08f, 1.0f, new Vector2(0.16f, 0.12f), new Vector2(0.14f, 0.11f), 0.04f, body, solid: true);

            var head = new GameObject("ScanHead").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0f, 1.02f, 0f);
            head.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            Slab("PlateBody", head, new Vector3(0f, 0.04f, 0f), new Vector3(0.46f, 0.08f, 0.38f), 0.03f, trim, solid: true);
            Slab("PalmGlass", head, new Vector3(0f, 0.083f, 0f), new Vector3(0.36f, 0.006f, 0.30f), 0.003f, BuildKit.MakeHologram(accent));

            Batch("PalmPrint", head, BuildKit.MakeEmissive(accent, 1.3f), HandPrint(new Vector3(0f, 0.088f, -0.055f)));

            Transform scanBar = Box("ScanBar", head, new Vector3(0f, 0.09f, -0.13f), new Vector3(0.36f, 0.004f, 0.012f),
                BuildKit.MakeEmissive(new Color(0.65f, 0.95f, 1f), 3f));
            scanBar.gameObject.SetActive(false);

            // Iris camera on a stalk behind the plate.
            Box("IrisStalk", root, new Vector3(0f, 1.32f, 0.17f), new Vector3(0.05f, 0.4f, 0.05f), body);
            Slab("IrisHousing", root, new Vector3(0f, 1.55f, 0.16f), new Vector3(0.22f, 0.12f, 0.12f), 0.03f, trim);
            MeshPart("IrisLens", root, CharacterMesh.Prism(8, 0.035f, 0.03f, 0f, 0.02f, "IrisLens", 22.5f),
                new Vector3(0f, 1.55f, 0.10f), new Vector3(-90f, 0f, 0f), BuildKit.MakeEmissive(accent, 2.6f));
            return scanBar;
        }

        /// <summary>A stylised right hand lying palm down, palm centred on
        /// <paramref name="palm"/>, fingers pointing away from the player (+Z),
        /// so it reads as "put your hand here".</summary>
        public static List<CombineInstance> HandPrint(Vector3 palm)
        {
            var hand = new List<CombineInstance>
            {
                Piece(CubeMesh, palm, Vector3.zero, new Vector3(0.11f, 0.003f, 0.10f)),
                // Thumb: its base overlaps the palm so the hand reads as one shape.
                Piece(CubeMesh, palm + new Vector3(-0.072f, 0f, -0.005f), new Vector3(0f, -35f, 0f), new Vector3(0.024f, 0.003f, 0.08f)),
            };
            float[] fingerX = { -0.042f, -0.014f, 0.014f, 0.042f };   // index to little finger
            float[] fingerLen = { 0.08f, 0.09f, 0.085f, 0.065f };
            for (int f = 0; f < 4; f++)
                hand.Add(Piece(CubeMesh, palm + new Vector3(fingerX[f], 0f, 0.055f + fingerLen[f] * 0.5f), Vector3.zero,
                    new Vector3(0.02f, 0.003f, fingerLen[f])));
            return hand;
        }

        /// <summary>The passcode kiosk's body: plinth, tapered stand and a
        /// keypad ledge under the screen. The screen itself (ScreenBody /
        /// Screen) stays on MfaFactor so its kiosk alignment keeps working.</summary>
        public static void BuildPasscodeKiosk(Transform root, Color accent)
        {
            Material body = Mat(HousingColor, 0.45f, 0.4f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Block("Plinth", root, Vector3.zero, Vector3.zero, 0f, 0.08f, new Vector2(0.36f, 0.28f), new Vector2(0.34f, 0.27f), 0.03f, trim, solid: true);
            Block("Stand", root, Vector3.zero, Vector3.zero, 0.08f, 1.12f, new Vector2(0.27f, 0.21f), new Vector2(0.24f, 0.19f), 0.05f, body, solid: true);
            Box("StandAccent", root, new Vector3(0f, 0.6f, -0.212f), new Vector3(0.025f, 0.8f, 0.01f), BuildKit.MakeEmissive(accent, 1.4f));

            var ledge = new GameObject("Keypad").transform;
            ledge.SetParent(root, false);
            ledge.localPosition = new Vector3(0f, 1.0f, -0.29f);
            ledge.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            Slab("KeypadLedge", ledge, new Vector3(0f, 0.03f, 0f), new Vector3(0.40f, 0.06f, 0.2f), 0.02f, trim);
            Mesh key = CharacterMesh.BevelBlock(0f, 0.014f, new Vector2(0.022f, 0.016f), new Vector2(0.019f, 0.013f), 0.006f, 0f, "Key");
            var keys = new List<CombineInstance>();
            for (int col = 0; col < 3; col++)
                for (int row = 0; row < 4; row++)
                    keys.Add(Piece(key, new Vector3(-0.06f + col * 0.06f, 0.06f, -0.065f + row * 0.044f), Vector3.zero, Vector3.one));
            Batch("Keys", ledge, Mat(new Color(0.30f, 0.33f, 0.40f), 0.5f, 0.3f), keys);
            Box("EnterKey", ledge, new Vector3(0.13f, 0.067f, -0.02f), new Vector3(0.05f, 0.014f, 0.1f), BuildKit.MakeEmissive(Verified, 1.4f));
        }

        /// <summary>One code box per digit of the daily code, in their own X/Y
        /// plane (facing -Z), for the passcode screen.</summary>
        public static Transform BuildCodeSlots(Transform parent, Color accent, int digits = 4)
        {
            var slots = new List<CombineInstance>();
            for (int i = 0; i < digits; i++)
            {
                float x = (i - (digits - 1) * 0.5f) * 0.09f;
                foreach (CombineInstance edge in Outline(new Vector3(x, 0f, 0f), 0.06f, 0.075f, 0.006f))
                {
                    CombineInstance flat = edge;
                    flat.transform = Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f)) * edge.transform;
                    slots.Add(flat);
                }
            }
            return Batch("CodeSlots", parent, BuildKit.MakeEmissive(accent, 1.6f), slots);
        }

        /// <summary>A chamfered dock for the authenticator phone, with a lip
        /// the phone stands in. Its top is at y=1.0.</summary>
        public static void BuildPhoneDock(Transform root)
        {
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Block("Dock", root, Vector3.zero, Vector3.zero, 0f, 1.0f, new Vector2(0.18f, 0.16f), new Vector2(0.15f, 0.13f), 0.04f,
                Mat(HousingColor, 0.45f, 0.4f), solid: true);
            Box("DockLip", root, new Vector3(0f, 1.012f, -0.05f), new Vector3(0.26f, 0.025f, 0.03f), trim);
            Box("DockBack", root, new Vector3(0f, 1.05f, 0.07f), new Vector3(0.26f, 0.12f, 0.03f), trim);
        }

        /// <summary>A free-standing sign: a chamfered plaque on two legs with
        /// a lit top edge, its face toward -Z.</summary>
        public static void BuildSignStand(Transform root, Vector3 plaqueCenter, Vector2 plaqueSize, Color accent)
        {
            Material body = Mat(HousingColor, 0.45f, 0.4f);
            Material trim = Mat(TrimColor, 0.55f, 0.6f);
            Slab("Plaque", root, plaqueCenter, new Vector3(plaqueSize.x, plaqueSize.y, 0.06f), 0.02f, body);
            Box("PlaqueEdge", root, plaqueCenter + new Vector3(0f, plaqueSize.y * 0.5f - 0.02f, -0.032f),
                new Vector3(plaqueSize.x - 0.1f, 0.012f, 0.01f), BuildKit.MakeEmissive(accent, 1.4f));
            float legX = plaqueSize.x * 0.5f - 0.2f;
            float legTop = plaqueCenter.y - plaqueSize.y * 0.5f + 0.05f;
            for (int side = -1; side <= 1; side += 2)
            {
                Block(side < 0 ? "Leg_L" : "Leg_R", root, new Vector3(side * legX, 0f, plaqueCenter.z + 0.04f), Vector3.zero,
                    0f, legTop, new Vector2(0.035f, 0.035f), new Vector2(0.03f, 0.03f), 0.012f, trim);
                Slab(side < 0 ? "Foot_L" : "Foot_R", root, new Vector3(side * legX, 0.025f, plaqueCenter.z + 0.04f),
                    new Vector3(0.12f, 0.05f, 0.32f), 0.015f, trim);
            }
        }
    }
}
