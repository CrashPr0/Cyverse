using System.Collections.Generic;
using UnityEngine;
using static Cyverse.Level.BlockKit;

namespace Cyverse.Level
{
    /// <summary>
    /// The server cabinets and locker banks that furnish the rooms, in the
    /// blocky, chamfered style of the Level 1 stations: a framed rack cabinet
    /// with a glass door over a stack of servers whose activity lights
    /// flicker (RackLights), and a bank of steel lockers, each door with vents,
    /// a pull and a badge reader. Fronts face +Z. Small parts are batched per
    /// material (flat details are quads), so a cabinet stays about a dozen
    /// renderers on WebGL.
    /// </summary>
    public static class CabinetProps
    {
        private static readonly Color BodyColor = new Color(0.06f, 0.07f, 0.095f);
        private static readonly Color TrimColor = new Color(0.17f, 0.19f, 0.25f);
        private static readonly Color RecessColor = new Color(0.02f, 0.025f, 0.04f);
        private static readonly Color BladeColor = new Color(0.13f, 0.145f, 0.19f);
        private static readonly Color BlankColor = new Color(0.075f, 0.085f, 0.11f);
        private static readonly Color DoorColor = new Color(0.21f, 0.24f, 0.30f);
        private static readonly Color PlateColor = new Color(0.50f, 0.55f, 0.63f);
        private static readonly Color ChromeColor = new Color(0.58f, 0.61f, 0.68f);
        // Saturated, and driven only just past white, so green, cyan and
        // amber stay distinguishable instead of all washing out to pale cyan.
        private static readonly Color Green = new Color(0.15f, 1f, 0.30f);
        private static readonly Color Cyan = new Color(0.20f, 0.70f, 1f);
        private static readonly Color Amber = new Color(1f, 0.60f, 0.15f);
        private static readonly Color Red = new Color(1f, 0.22f, 0.20f);
        private static readonly Color CableColor = new Color(0.03f, 0.035f, 0.05f);
        private static readonly Color PatchColor = new Color(0.10f, 0.25f, 0.55f);
        private static readonly Color NumberColor = new Color(0.06f, 0.08f, 0.11f);

        // Palette files (Resources/Palette), shared across every cabinet and level.
        private static Material Body => Palette.Get("CabinetBody", BodyColor, 0.45f, 0.4f);
        private static Material Trim => Mat(TrimColor, 0.55f, 0.6f);
        private static Material Recess => Mat(RecessColor, 0.3f, 0.2f);
        private static Material Blade => Palette.Get("ServerBlade", BladeColor, 0.55f, 0.55f);
        private static Material Blank => Palette.Get("ServerBlank", BlankColor, 0.4f, 0.4f);
        private static Material Door => Palette.Get("LockerDoor", DoorColor, 0.58f, 0.35f);
        private static Material Plate => Palette.Get("LockerPlate", PlateColor, 0.6f, 0.5f);
        private static Material Chrome => Mat(ChromeColor, 0.75f, 0.9f);
        private static Material Accent => Palette.Glow("CabinetGlow", BuildKit.AccentCyan, 1.1f);
        private static Material Cable => Palette.Get("Cable", CableColor, 0.35f, 0.1f);
        private static Material Patch => Palette.Get("PatchCable", PatchColor, 0.4f, 0.1f);
        private static Material LedGreen => Palette.Glow("LedGreen", Green, 1.5f);
        private static Material LedCyan => Palette.Glow("LedCyan", Cyan, 1.5f);
        private static Material LedAmber => Palette.Glow("LedAmber", Amber, 1.5f);
        private static Material LedRed => Palette.Glow("LedRed", Red, 1.5f);

        private static Material glass;

        private static Material Glass
        {
            get
            {
                if (glass != null) return glass;
                Shader shader = Resources.Load<Shader>("Shaders/GlassPane");
                if (shader == null) return null;
                glass = new Material(shader);
                glass.SetColor("_Color", new Color(0.30f, 0.45f, 0.58f));
                glass.SetFloat("_Alpha", 0.10f);
                glass.SetFloat("_ReflectionStrength", 0.10f);
                glass.SetFloat("_EdgeOpacity", 0.12f);
                return glass;
            }
        }

        /// <summary>A flat detail (drive bay, vent, LED) facing +Z.</summary>
        private static void Flat(List<CombineInstance> list, float x, float y, float z, float w, float h) =>
            list.Add(Piece(QuadMesh, new Vector3(x, y, z), new Vector3(0f, 180f, 0f), new Vector3(w, h, 1f)));

        // ---- Server cabinet -----------------------------------------------------

        private const float RackHalfWidth = 0.4f, RackHalfDepth = 0.36f, RackTop = 2.08f;

        /// <summary>Centre-to-centre spacing of cabinets bolted into a row.</summary>
        public const float RackPitch = 0.82f;

        // How far the glass reflection streak repeats along a row, in pane widths.
        private const float GlassStreakPeriod = 2.8f;
        private const float BayBottom = 0.24f, BayTop = 1.95f;
        private const float RackUnit = 0.0445f;

        /// <summary>A server cabinet (0.8 x 2.14 x 0.72 m, footprint centred on
        /// the origin): plinth, body and cap, a framed front with a lit status
        /// strip and a handle, side vents, and behind the glass a stack of
        /// 1U, 2U and 4U servers and blanking panels, varied by
        /// <paramref name="seed"/> so neighbouring racks differ.</summary>
        public static void ServerRack(Transform rack, int seed)
        {
            var rng = new System.Random(seed);
            const float hw = RackHalfWidth, hd = RackHalfDepth, top = RackTop;
            const float face = hd;                 // the body's front face
            const float plate = face + 0.022f;     // server faceplates' front
            const float detail = plate + 0.002f;   // flat details on them

            Block("Plinth", rack, Vector3.zero, Vector3.zero, 0f, 0.07f,
                new Vector2(hw - 0.03f, hd - 0.03f), new Vector2(hw - 0.03f, hd - 0.03f), 0.015f, Recess);
            Block("Body", rack, Vector3.zero, Vector3.zero, 0.07f, top,
                new Vector2(hw, hd), new Vector2(hw, hd), 0.03f, Body, solid: true);
            Block("Cap", rack, Vector3.zero, Vector3.zero, top, top + 0.06f,
                new Vector2(hw + 0.012f, hd + 0.012f), new Vector2(hw, hd), 0.02f, Trim);

            // Door frame: stiles and rails standing proud of the body.
            const float frameZ = face + 0.02f;
            var frame = new List<CombineInstance>
            {
                Piece(CubeMesh, new Vector3(-hw + 0.035f, 1.08f, frameZ), Vector3.zero, new Vector3(0.07f, 1.96f, 0.05f)),
                Piece(CubeMesh, new Vector3(hw - 0.035f, 1.08f, frameZ), Vector3.zero, new Vector3(0.07f, 1.96f, 0.05f)),
                Piece(CubeMesh, new Vector3(0f, top - 0.07f, frameZ), Vector3.zero, new Vector3(2f * hw, 0.10f, 0.05f)),
                Piece(CubeMesh, new Vector3(0f, 0.16f, frameZ), Vector3.zero, new Vector3(2f * hw, 0.12f, 0.05f)),
            };
            Batch("Frame", rack, Trim, frame);
            Box("StatusStrip", rack, new Vector3(0f, top - 0.07f, frameZ + 0.027f), new Vector3(0.34f, 0.012f, 0.006f), Accent);

            var blades = new List<CombineInstance>();
            var blanks = new List<CombineInstance>();
            var recess = new List<CombineInstance>();
            var chrome = new List<CombineInstance>();
            var green = new List<CombineInstance>();
            var cyan = new List<CombineInstance>();
            var amber = new List<CombineInstance>();
            var activity = new[] { new List<CombineInstance>(), new List<CombineInstance>(), new List<CombineInstance>() };

            void LedAt(float x, float y, bool active)
            {
                List<CombineInstance> set = active ? activity[rng.Next(3)]
                    : rng.Next(10) == 0 ? amber : rng.Next(2) == 0 ? green : cyan;
                Flat(set, x, y, detail + 0.001f, 0.012f, 0.008f);
            }

            float y = BayBottom;
            while (y < BayTop - RackUnit * 0.5f)
            {
                int roll = rng.Next(100);
                int units = roll < 28 ? 1 : roll < 58 ? 2 : roll < 76 ? 4 : roll < 90 ? 1 : 2;
                bool blank = roll >= 76;
                if (y + units * RackUnit > BayTop) { units = 1; blank = true; }
                float h = units * RackUnit, cy = y + h * 0.5f;
                y += h;

                (blank ? blanks : blades).Add(Piece(CubeMesh, new Vector3(0f, cy, face + 0.012f), Vector3.zero,
                    new Vector3(0.64f, h - 0.005f, 0.02f)));
                chrome.Add(Piece(CubeMesh, new Vector3(-0.315f, cy, face + 0.016f), Vector3.zero, new Vector3(0.014f, h - 0.012f, 0.02f)));
                chrome.Add(Piece(CubeMesh, new Vector3(0.315f, cy, face + 0.016f), Vector3.zero, new Vector3(0.014f, h - 0.012f, 0.02f)));

                if (blank)
                {
                    for (int s = 0; s < units; s++)
                        Flat(recess, 0f, y - h + (s + 0.5f) * RackUnit, detail, 0.5f, 0.006f);
                    continue;
                }

                LedAt(0.27f, cy + h * 0.25f, active: false);  // power
                if (units == 1)
                {
                    for (int k = 0; k < 4; k++) Flat(recess, -0.26f + k * 0.085f, cy, detail, 0.075f, h * 0.55f);
                    Flat(recess, 0.12f, cy, detail, 0.15f, h * 0.35f);
                    LedAt(0.245f, cy - h * 0.15f, active: true);
                }
                else if (units == 2)
                {
                    for (int row = -1; row <= 1; row += 2)
                        for (int k = 0; k < 6; k++)
                            Flat(recess, -0.27f + k * 0.07f, cy + row * h * 0.22f, detail, 0.06f, h * 0.36f);
                    for (int k = 0; k < 6; k++) LedAt(-0.248f + k * 0.07f, cy + h * 0.22f, active: rng.Next(3) > 0);
                    Flat(cyan, 0.2f, cy - h * 0.2f, detail + 0.001f, 0.05f, 0.006f);
                }
                else
                {
                    for (int row = -1; row <= 1; row++)
                        for (int k = 0; k < 4; k++)
                        {
                            float x = -0.24f + k * 0.12f, ry = cy + row * h * 0.3f;
                            Flat(recess, x, ry, detail, 0.105f, h * 0.26f);
                            LedAt(x + 0.04f, ry, active: rng.Next(4) > 0);
                        }
                }
            }

            // Side vents, high on both sides.
            for (int s = 0; s < 10; s++)
            {
                float vy = 1.55f + s * 0.035f;
                recess.Add(Piece(QuadMesh, new Vector3(hw + 0.002f, vy, 0f), new Vector3(0f, -90f, 0f), new Vector3(0.4f, 0.012f, 1f)));
                recess.Add(Piece(QuadMesh, new Vector3(-hw - 0.002f, vy, 0f), new Vector3(0f, 90f, 0f), new Vector3(0.4f, 0.012f, 1f)));
            }

            // Handle on the opening stile.
            chrome.Add(Piece(CubeMesh, new Vector3(hw - 0.035f, 1.2f, frameZ + 0.05f), Vector3.zero, new Vector3(0.018f, 0.3f, 0.016f)));
            chrome.Add(Piece(CubeMesh, new Vector3(hw - 0.035f, 1.07f, frameZ + 0.035f), Vector3.zero, new Vector3(0.014f, 0.014f, 0.03f)));
            chrome.Add(Piece(CubeMesh, new Vector3(hw - 0.035f, 1.33f, frameZ + 0.035f), Vector3.zero, new Vector3(0.014f, 0.014f, 0.03f)));

            Batch("Servers", rack, Blade, blades);
            if (blanks.Count > 0) Batch("Blanks", rack, Blank, blanks);
            Batch("ServerDetail", rack, Recess, recess);
            Batch("Chrome", rack, Chrome, chrome);
            if (green.Count > 0) Batch("LedsGreen", rack, LedGreen, green);
            if (cyan.Count > 0) Batch("LedsCyan", rack, LedCyan, cyan);
            if (amber.Count > 0) Batch("LedsAmber", rack, LedAmber, amber);

            var blinking = new List<Renderer>();
            for (int i = 0; i < activity.Length; i++)
            {
                if (activity[i].Count == 0) continue;
                blinking.Add(Batch("LedsActivity" + i, rack, i == 1 ? LedCyan : LedGreen, activity[i])
                    .GetComponent<Renderer>());
            }
            rack.gameObject.AddComponent<RackLights>().sets = blinking.ToArray();

            if (Glass != null)
            {
                float paneWidth = 2f * hw - 0.12f;
                Transform glass = MeshPart("Glass", rack, QuadMesh, new Vector3(0f, (BayBottom + BayTop) * 0.5f + 0.005f, face + 0.04f),
                    new Vector3(0f, 180f, 0f), Glass, scale: new Vector3(paneWidth, BayTop - BayBottom + 0.07f, 1f));
                var glassRenderer = glass.GetComponent<MeshRenderer>();
                glassRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Offset the streak by where the pane sits along its row, so
                // side-by-side cabinets share one continuous reflection.
                var block = new MaterialPropertyBlock();
                block.SetFloat("_RibbonOffset", Vector3.Dot(glass.position, glass.right) / paneWidth);
                block.SetFloat("_RibbonPeriod", GlassStreakPeriod);
                glassRenderer.SetPropertyBlock(block);
            }
        }

        /// <summary>The overhead ladder tray along a row of
        /// <paramref name="count"/> cabinets (centred on the row's origin,
        /// fronts toward +Z): side rails and rungs hung from the ceiling on
        /// threaded rods, a cable bundle and a run of blue patch cords in it,
        /// and a drop of cables into the back of each cabinet's roof.</summary>
        public static void CableTray(Transform row, int count)
        {
            const float y = 2.55f, hz = 0.15f, dropZ = -0.17f;
            float length = count * RackPitch + 0.1f, half = length * 0.5f;
            float ceiling = 5f;
            GameObject slab = GameObject.Find("CeilingSlab");
            if (slab != null && slab.GetComponent<Renderer>() != null)
                ceiling = slab.GetComponent<Renderer>().bounds.min.y;

            var frame = new List<CombineInstance>
            {
                Piece(CubeMesh, new Vector3(0f, y, -hz), Vector3.zero, new Vector3(length, 0.07f, 0.02f)),
                Piece(CubeMesh, new Vector3(0f, y, hz), Vector3.zero, new Vector3(length, 0.07f, 0.02f)),
            };
            for (float x = -half + 0.1f; x < half - 0.05f; x += 0.25f)
                frame.Add(Piece(CubeMesh, new Vector3(x, y - 0.03f, 0f), Vector3.zero, new Vector3(0.025f, 0.012f, 2f * hz)));
            var rods = new List<CombineInstance>();
            foreach (float x in new[] { -half + 0.15f, half - 0.15f })
            {
                frame.Add(Piece(CubeMesh, new Vector3(x, y - 0.045f, 0f), Vector3.zero, new Vector3(0.04f, 0.02f, 2f * hz + 0.08f)));
                for (int side = -1; side <= 1; side += 2)
                    rods.Add(Piece(CubeMesh, new Vector3(x, (y + ceiling) * 0.5f, side * (hz + 0.03f)), Vector3.zero,
                        new Vector3(0.014f, ceiling - y, 0.014f)));
            }
            Batch("TrayFrame", row, Trim, frame);
            Batch("CeilingRods", row, Trim, rods); // hangers, up to however high the ceiling is

            Mesh drop = CharacterMesh.Prism(8, 0.035f, 0.035f, RackTop + 0.06f, y - 0.03f, "CableDrop", 22.5f);
            Mesh patchDrop = CharacterMesh.Prism(6, 0.016f, 0.016f, RackTop + 0.06f, y - 0.03f, "PatchDrop");
            var cables = new List<CombineInstance>
            {
                Piece(CubeMesh, new Vector3(0f, y - 0.005f, -0.03f), Vector3.zero, new Vector3(length - 0.06f, 0.05f, 0.18f)),
            };
            var patches = new List<CombineInstance>
            {
                Piece(CubeMesh, new Vector3(0f, y + 0.012f, 0.08f), Vector3.zero, new Vector3(length - 0.12f, 0.03f, 0.05f)),
            };
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * RackPitch;
                cables.Add(Piece(drop, new Vector3(x - 0.05f, 0f, dropZ), Vector3.zero, Vector3.one));
                patches.Add(Piece(patchDrop, new Vector3(x + 0.04f, 0f, dropZ + 0.04f), Vector3.zero, Vector3.one));
            }
            Batch("Cables", row, Cable, cables);
            Batch("PatchCables", row, Patch, patches);
        }

        // ---- Locker bank ----------------------------------------------------------

        /// <summary>A bank of four steel lockers (2.0 x 1.92 x 0.5 m, footprint
        /// centred on the origin): recessed kick plate, body and crown with a
        /// lit strip under it, and per door top and bottom vents, a number
        /// plate (numbered from <paramref name="firstNumber"/>), a pull, hinges
        /// and a badge reader whose lamp shows locked
        /// (red) or open (green, one door per bank, chosen by
        /// <paramref name="seed"/>), plus a soft fill light.</summary>
        public static void LockerBank(Transform bank, int seed, int firstNumber = 1)
        {
            var rng = new System.Random(seed);
            const float hw = 1.0f, hd = 0.25f, top = 1.86f;
            const float doorFront = hd + 0.025f, detail = doorFront + 0.002f;

            Block("Kick", bank, new Vector3(0f, 0f, -0.02f), Vector3.zero, 0f, 0.1f,
                new Vector2(hw - 0.02f, hd - 0.03f), new Vector2(hw - 0.02f, hd - 0.03f), 0.01f, Recess);
            Block("Body", bank, Vector3.zero, Vector3.zero, 0.1f, top,
                new Vector2(hw, hd), new Vector2(hw, hd), 0.02f, Body, solid: true);
            Block("Crown", bank, Vector3.zero, Vector3.zero, top, top + 0.06f,
                new Vector2(hw + 0.02f, hd + 0.02f), new Vector2(hw + 0.01f, hd + 0.01f), 0.02f, Trim);
            Box("CrownGlow", bank, new Vector3(0f, top - 0.018f, hd + 0.006f), new Vector3(2f * hw - 0.1f, 0.01f, 0.006f), Accent);

            Mesh doorMesh = CharacterMesh.BevelBlock(-0.84f, 0.84f, new Vector2(0.235f, 0.0125f),
                new Vector2(0.235f, 0.0125f), 0.01f, 0f, "LockerDoor");
            var doors = new List<CombineInstance>();
            var vents = new List<CombineInstance>();
            var plates = new List<CombineInstance>();
            var chrome = new List<CombineInstance>();
            var readers = new List<CombineInstance>();
            var red = new List<CombineInstance>();
            var green = new List<CombineInstance>();
            int open = rng.Next(4);

            for (int i = 0; i < 4; i++)
            {
                float x = -0.75f + i * 0.5f;
                doors.Add(Piece(doorMesh, new Vector3(x, 0.98f, hd + 0.0125f), Vector3.zero, Vector3.one));
                for (int s = 0; s < 5; s++) Flat(vents, x, 1.58f + s * 0.04f, detail, 0.28f, 0.014f);
                for (int s = 0; s < 3; s++) Flat(vents, x, 0.26f + s * 0.04f, detail, 0.28f, 0.014f);
                Flat(plates, x, 1.45f, detail, 0.09f, 0.035f);
                // Numbered left to right as seen from the front, where local +X is on the left.
                TextMesh number = BuildKit.MakeLabel(bank, new Vector3(x, 1.448f, detail + 0.002f),
                    (firstNumber + 3 - i).ToString("00"), NumberColor, 0.0045f);
                number.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // read from the front (+Z)

                float side = x + 0.16f;
                chrome.Add(Piece(CubeMesh, new Vector3(side, 1.0f, doorFront + 0.018f), Vector3.zero, new Vector3(0.022f, 0.18f, 0.012f)));
                chrome.Add(Piece(CubeMesh, new Vector3(side, 0.93f, doorFront + 0.008f), Vector3.zero, new Vector3(0.015f, 0.015f, 0.016f)));
                chrome.Add(Piece(CubeMesh, new Vector3(side, 1.07f, doorFront + 0.008f), Vector3.zero, new Vector3(0.015f, 0.015f, 0.016f)));
                foreach (float hy in new[] { 0.4f, 1.55f })
                    chrome.Add(Piece(CubeMesh, new Vector3(x - 0.232f, hy, doorFront - 0.004f), Vector3.zero, new Vector3(0.012f, 0.07f, 0.014f)));

                readers.Add(Piece(CubeMesh, new Vector3(side, 1.22f, doorFront + 0.01f), Vector3.zero, new Vector3(0.055f, 0.085f, 0.02f)));
                Flat(i == open ? green : red, side, 1.25f, doorFront + 0.0215f, 0.03f, 0.006f);
            }

            Batch("Doors", bank, Door, doors);
            Batch("Vents", bank, Recess, vents);
            Batch("Plates", bank, Plate, plates);
            Batch("Chrome", bank, Chrome, chrome);
            Batch("Readers", bank, Trim, readers);
            Batch("LampsLocked", bank, LedRed, red);
            Batch("LampsOpen", bank, LedGreen, green);

            // Lockers usually stand along a wall the ceiling lights miss. A
            // per-vertex fill washes their doors without taking one of the
            // two pixel-light slots, and adds nothing to the big floor and
            // wall meshes (whose few vertices sit far outside its range).
            var fill = new GameObject("LockerFill").AddComponent<Light>();
            fill.transform.SetParent(bank, false);
            fill.transform.localPosition = new Vector3(0f, 2.2f, 1.1f);
            fill.type = LightType.Point;
            fill.renderMode = LightRenderMode.ForceVertex;
            fill.color = new Color(0.62f, 0.78f, 1f);
            fill.range = 3.2f;
            fill.intensity = 1.4f;
            fill.shadows = LightShadows.None;
        }
    }
}
