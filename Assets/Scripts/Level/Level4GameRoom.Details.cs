using System.Collections.Generic;
using UnityEngine;
using Ring = Cyverse.Level.CharacterMesh.Ring;

namespace Cyverse.Level
{
    /// <summary>
    /// The second modelling pass over the game room: the props that were
    /// still flat panels and plain boxes. Vending and cooler stock becomes
    /// bottles, cans, bags and cups; the air hockey table, coffee machine,
    /// ticket dispenser and beanbags are rebuilt; and the pinball table, hoop
    /// shooters, ping-pong table and LAN desk get the secondary detail
    /// (feet, frames, seams, wire) that makes a model read as built.
    /// </summary>
    public static partial class Level4GameRoom
    {
        // ---- Shared pieces ------------------------------------------------------

        private static readonly Color Steel = new Color(0.62f, 0.65f, 0.72f);

        /// <summary>A bar that narrows from 1 to 0.5 across its height.</summary>
        private static CombineInstance Taper(Vector3 at, float diameter, float height) =>
            BlockKit.Piece(Flare, at + Vector3.up * height, new Vector3(180f, 0f, 0f), new Vector3(diameter, height, diameter));

        /// <summary>A round bar lying along <paramref name="axisEuler"/>
        /// (its length runs along the rotated Y axis), centred on <paramref name="at"/>.</summary>
        private static CombineInstance Spindle(Vector3 at, Vector3 axisEuler, float diameter, float length)
        {
            Quaternion q = Quaternion.Euler(axisEuler);
            return new CombineInstance
            {
                mesh = Round,
                transform = Matrix4x4.TRS(at - q * Vector3.up * (length * 0.5f), q, new Vector3(diameter, length, diameter)),
            };
        }

        /// <summary>A flat ring facing local -Z (a grille, a fan, a coil).</summary>
        private static CombineInstance FacingRing(Vector3 at, float diameter, float depth) =>
            BlockKit.Piece(Hoop, at, new Vector3(-90f, 0f, 0f), new Vector3(diameter, depth, diameter));

        /// <summary>Closed loop of square bars round a superellipse of
        /// half-widths <paramref name="rx"/> and <paramref name="rz"/>.</summary>
        private static List<CombineInstance> RoundedLoop(float y, float rx, float rz, float n, float thickness, int segments = 44)
        {
            var bars = new List<CombineInstance>();
            var ring = new Ring(y, rx, rz, 0f, 0f, n);
            Vector3 last = CharacterMesh.RingPoint(ring, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = CharacterMesh.RingPoint(ring, i * Mathf.PI * 2f / segments, 0f);
                bars.Add(Strut(last, next, thickness));
                last = next;
            }
            return bars;
        }

        // ---- Stock: bottles, cans, bags and cups --------------------------------

        private enum Snack { Bottle, Can, Bag, Cup }

        /// <summary>One item of stock standing on <paramref name="at"/>.
        /// <paramref name="depth"/> squashes it front to back, for shelves
        /// too shallow for a full round.</summary>
        private static void AddSnack(Snack kind, Vector3 at, float h, float depth,
            List<CombineInstance> body, List<CombineInstance> trim)
        {
            int bodyFrom = body.Count, trimFrom = trim.Count;
            switch (kind)
            {
                case Snack.Bottle:
                    body.Add(Part(Round, at, 0.062f, h * 0.58f));
                    body.Add(Taper(at + Vector3.up * h * 0.58f, 0.062f, h * 0.2f));
                    body.Add(Part(Round, at + Vector3.up * h * 0.76f, 0.03f, h * 0.14f));
                    trim.Add(Part(Round, at + Vector3.up * h * 0.88f, 0.036f, h * 0.12f));
                    trim.Add(Part(Round, at + Vector3.up * h * 0.22f, 0.064f, h * 0.2f));
                    break;
                case Snack.Can:
                    body.Add(Part(Round, at, 0.066f, h * 0.94f));
                    trim.Add(Part(Round, at + Vector3.up * h * 0.94f, 0.056f, h * 0.06f));
                    trim.Add(Part(Round, at + Vector3.up * h * 0.38f, 0.068f, h * 0.16f));
                    break;
                case Snack.Bag:
                    body.Add(Ball(at + Vector3.up * h * 0.5f, new Vector3(0.105f, h * 0.9f, 0.05f)));
                    body.Add(BlockKit.Piece(BlockKit.CubeMesh, at + Vector3.up * (h - 0.008f), Vector3.zero, new Vector3(0.094f, 0.016f, 0.008f)));
                    body.Add(BlockKit.Piece(BlockKit.CubeMesh, at + Vector3.up * 0.008f, Vector3.zero, new Vector3(0.094f, 0.016f, 0.008f)));
                    trim.Add(Ball(at + new Vector3(0f, h * 0.5f, -0.004f), new Vector3(0.06f, h * 0.36f, 0.05f)));
                    break;
                default:
                    body.Add(Part(Flare, at, 0.078f, h * 0.86f));
                    trim.Add(Part(Round, at + Vector3.up * h * 0.86f, 0.082f, h * 0.1f));
                    break;
            }
            if (Mathf.Approximately(depth, 1f)) return;
            Matrix4x4 squash = Matrix4x4.Translate(at) * Matrix4x4.Scale(new Vector3(1f, 1f, depth)) * Matrix4x4.Translate(-at);
            for (int i = bodyFrom; i < body.Count; i++) { CombineInstance p = body[i]; p.transform = squash * p.transform; body[i] = p; }
            for (int i = trimFrom; i < trim.Count; i++) { CombineInstance p = trim[i]; p.transform = squash * p.transform; trim[i] = p; }
        }

        /// <summary>The stock behind one vending machine's window: five
        /// shelves of five, each kind of item in its own colour, on wire
        /// coils. <paramref name="machine"/> picks the mix (drinks, snacks,
        /// both).</summary>
        private static void BuildVendingStock(Transform v, int machine, Color[] mix)
        {
            var bodies = new List<CombineInstance>[mix.Length];
            for (int s = 0; s < bodies.Length; s++) bodies[s] = new List<CombineInstance>();
            var trim = new List<CombineInstance>();
            var coils = new List<CombineInstance>();
            var rnd = new System.Random(20 + machine);
            for (int shelf = 0; shelf < 5; shelf++)
            {
                float y = 0.62f + shelf * 0.25f;
                for (int k = 0; k < 5; k++)
                {
                    float x = -0.48f + k * 0.18f;
                    int colour = (shelf + k * 2 + machine + 1) % mix.Length;
                    Snack kind;
                    if (machine < 0) kind = shelf % 2 == 0 ? Snack.Bottle : Snack.Can;                  // drinks
                    else if (machine == 0) kind = shelf % 2 == 0 ? Snack.Bag : Snack.Cup;               // snacks
                    else kind = (Snack)((shelf + k) % 4);                                               // a bit of everything
                    float h = kind == Snack.Bottle ? 0.17f : kind == Snack.Bag ? 0.15f + (float)rnd.NextDouble() * 0.02f : 0.12f;
                    AddSnack(kind, new Vector3(x, y, -0.443f), h, 0.42f, bodies[colour], trim);
                    coils.Add(FacingRing(new Vector3(x, y + 0.03f, -0.452f), 0.07f, 0.004f));
                }
            }
            for (int s = 0; s < bodies.Length; s++)
                if (bodies[s].Count > 0)
                    BlockKit.Batch("Snacks_" + s, v, Glow(Color.Lerp(mix[s], Color.white, 0.15f), 0.55f), bodies[s]);
            BlockKit.Batch("SnackLabels", v, Glow(new Color(0.95f, 0.95f, 1f), 0.5f), trim);
            BlockKit.Batch("Coils", v, BrightSteel, coils);
            // A pull bar on the delivery flap and levelling feet under the body.
            var metal = new List<CombineInstance>
            {
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(-0.12f, 0.418f, -0.442f), Vector3.zero, new Vector3(0.5f, 0.014f, 0.02f)),
            };
            foreach (float x in new[] { -0.5f, 0.5f })
                metal.Add(Part(Round, new Vector3(x, -0.001f, -0.34f), 0.07f, 0.03f));
            BlockKit.Batch("Fittings", v, BrightSteel, metal);
        }

        /// <summary>A drinks cooler's front: a lit interior with four shelves
        /// of cans and bottles behind a framed glass door with a handle.</summary>
        private static void BuildCoolerFront(Transform bar, string name, Vector3 c, Color drinks)
        {
            var bodies = new List<CombineInstance>();
            var trim = new List<CombineInstance>();
            var shelves = new List<CombineInstance>();
            for (int shelf = 0; shelf < 4; shelf++)
            {
                float y = 0.42f + shelf * 0.38f;
                shelves.Add(BlockKit.Piece(BlockKit.CubeMesh, c + new Vector3(0f, y - 0.008f, -0.395f), Vector3.zero, new Vector3(0.48f, 0.012f, 0.03f)));
                for (int k = 0; k < 4; k++)
                    AddSnack(shelf % 2 == 0 ? Snack.Can : Snack.Bottle, c + new Vector3(-0.165f + k * 0.11f, y, -0.397f),
                        shelf % 2 == 0 ? 0.14f : 0.2f, 0.5f, bodies, trim);
            }
            BlockKit.Batch(name + "_Cans", bar, Glow(drinks, 0.9f), bodies);
            BlockKit.Batch(name + "_Labels", bar, Glow(new Color(0.95f, 0.95f, 1f), 0.6f), trim);
            BlockKit.Batch(name + "_Shelves", bar, BrightSteel, shelves);

            // Door: frame, glass and a long pull handle on standoffs.
            var door = new List<CombineInstance>();
            foreach (float y in new[] { 0.27f, 1.89f })
                door.Add(BlockKit.Piece(BlockKit.CubeMesh, c + new Vector3(0f, y, -0.418f), Vector3.zero, new Vector3(0.54f, 0.03f, 0.025f)));
            foreach (float x in new[] { -0.26f, 0.26f })
                door.Add(BlockKit.Piece(BlockKit.CubeMesh, c + new Vector3(x, 1.08f, -0.418f), Vector3.zero, new Vector3(0.03f, 1.65f, 0.025f)));
            door.Add(Spindle(c + new Vector3(0.21f, 1.08f, -0.46f), Vector3.zero, 0.022f, 0.6f));
            foreach (float y in new[] { 0.84f, 1.32f })
                door.Add(Spindle(c + new Vector3(0.21f, y, -0.445f), new Vector3(90f, 0f, 0f), 0.016f, 0.04f));
            BlockKit.Batch(name + "_Door", bar, BrightSteel, door);
            Pane(bar, name + "_Pane", c + new Vector3(0f, 1.08f, -0.42f), 0f, new Vector2(0.5f, 1.6f));
            // Brand band across the top and a vent grille at the foot.
            BlockKit.Box(name + "_Band", bar, c + new Vector3(0f, 1.97f, -0.365f), new Vector3(0.52f, 0.1f, 0.01f), Glow(drinks, 1.1f));
            var vents = new List<CombineInstance>();
            for (int i = 0; i < 5; i++)
                vents.Add(BlockKit.Piece(BlockKit.CubeMesh, c + new Vector3(0f, 0.07f + i * 0.035f, -0.364f), Vector3.zero, new Vector3(0.44f, 0.012f, 0.01f)));
            BlockKit.Batch(name + "_Vents", bar, DarkGlass, vents);
        }

        /// <summary>Crinkled bags standing in a basket at <paramref name="c"/>.</summary>
        private static void BuildChipBags(Transform bar, Vector3 c, Color colour, System.Random rnd)
        {
            var bags = new List<CombineInstance>();
            var labels = new List<CombineInstance>();
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = c + new Vector3(-0.18f + i * 0.09f, 0.17f, (float)rnd.NextDouble() * 0.06f - 0.03f);
                Quaternion lean = Quaternion.Euler(-15f + (float)rnd.NextDouble() * 10f, 0f, (float)rnd.NextDouble() * 16f - 8f);
                bags.Add(new CombineInstance { mesh = SphereMesh, transform = Matrix4x4.TRS(at, lean, new Vector3(0.088f, 0.2f, 0.04f)) });
                foreach (float end in new[] { -1f, 1f })
                    bags.Add(new CombineInstance
                    {
                        mesh = BlockKit.CubeMesh,
                        transform = Matrix4x4.TRS(at + lean * new Vector3(0f, end * 0.098f, 0f), lean, new Vector3(0.08f, 0.018f, 0.008f)),
                    });
                labels.Add(new CombineInstance
                {
                    mesh = SphereMesh,
                    transform = Matrix4x4.TRS(at + lean * new Vector3(0f, 0.01f, -0.006f), lean, new Vector3(0.05f, 0.07f, 0.036f)),
                });
            }
            BlockKit.Batch("ChipBags", bar, Plain(colour * 0.85f), bags);
            BlockKit.Batch("ChipBagLabels", bar, Plain(PrizeWhite), labels);
        }

        // ---- Coffee machine -----------------------------------------------------

        /// <summary>An espresso machine at <paramref name="m"/> on the counter:
        /// a steel body with a cup rail, two group heads with portafilters, a
        /// steam wand, a drip tray and two cups waiting under the spouts.</summary>
        private static void BuildEspressoMachine(Transform bar, Vector3 m)
        {
            Material dark = Plain(PrizeDark);
            BlockKit.Slab("CoffeeMachineBase", bar, m + new Vector3(0f, 0.03f, 0f), new Vector3(0.44f, 0.06f, 0.36f), 0.012f, CabinetTrim);
            BlockKit.Slab("CoffeeMachineBack", bar, m + new Vector3(0f, 0.2f, 0.09f), new Vector3(0.42f, 0.3f, 0.16f), 0.015f, BrightSteel);
            BlockKit.Slab("CoffeeMachine", bar, m + new Vector3(0f, 0.39f, 0f), new Vector3(0.44f, 0.13f, 0.36f), 0.025f, BrightSteel);
            BlockKit.Slab("CoffeeMachineFace", bar, m + new Vector3(0f, 0.39f, -0.181f), new Vector3(0.36f, 0.08f, 0.008f), 0.003f, DarkGlass);
            BlockKit.Box("CoffeeMachineLed", bar, m + new Vector3(0.12f, 0.39f, -0.187f), new Vector3(0.05f, 0.025f, 0.004f), Glow(Teal, 1.6f));
            BlockKit.Batch("CupRail", bar, BrightSteel, BlockKit.Outline(m + new Vector3(0f, 0.475f, 0f), 0.4f, 0.32f, 0.014f));
            BlockKit.Slab("DripTray", bar, m + new Vector3(0f, 0.066f, -0.07f), new Vector3(0.38f, 0.014f, 0.18f), 0.004f, dark);

            var steel = new List<CombineInstance>();
            var black = new List<CombineInstance>();
            var cups = new List<CombineInstance>();
            foreach (float x in new[] { -0.1f, 0.1f })
            {
                steel.Add(Part(Round, m + new Vector3(x, 0.285f, -0.07f), 0.075f, 0.04f));                       // group head
                steel.Add(Part(Round, m + new Vector3(x, 0.255f, -0.07f), 0.062f, 0.03f));                       // portafilter basket
                black.Add(Strut(m + new Vector3(x, 0.27f, -0.1f), m + new Vector3(x, 0.255f, -0.24f), 0.024f));   // its handle
                steel.Add(Part(Round, m + new Vector3(x, 0.235f, -0.07f), 0.016f, 0.02f));                       // spout
                cups.Add(Part(Flare, m + new Vector3(x, 0.073f, -0.07f), 0.07f, 0.06f));
                cups.Add(FacingRing(m + new Vector3(x + 0.045f, 0.105f, -0.066f), 0.04f, 0.008f));               // cup handle
            }
            steel.Add(Strut(m + new Vector3(0.2f, 0.33f, -0.1f), m + new Vector3(0.235f, 0.14f, -0.15f), 0.012f)); // steam wand
            black.Add(Ball(m + new Vector3(0.2f, 0.34f, -0.1f), Vector3.one * 0.035f));
            for (int k = 0; k < 3; k++)                                                                           // knobs
                black.Add(Spindle(m + new Vector3(-0.14f + k * 0.07f, 0.39f, -0.19f), new Vector3(90f, 0f, 0f), 0.028f, 0.02f));
            // Cups warming on the rail.
            for (int k = 0; k < 6; k++)
                cups.Add(BlockKit.Piece(Flare, m + new Vector3(-0.13f + (k % 3) * 0.13f, 0.52f, -0.06f + (k / 3) * 0.13f),
                    new Vector3(180f, 0f, 0f), new Vector3(0.07f, 0.06f, 0.07f)));
            BlockKit.Batch("CoffeeSteel", bar, BrightSteel, steel);
            BlockKit.Batch("CoffeeBlack", bar, dark, black);
            BlockKit.Batch("CoffeeCups", bar, Plain(new Color(0.95f, 0.93f, 0.88f)), cups);
        }

        /// <summary>Two stacks of paper cups, nested rim over rim.</summary>
        private static void BuildCupStacks(Transform bar, Vector3 at)
        {
            var cups = new List<CombineInstance>();
            var rims = new List<CombineInstance>();
            for (int stack = 0; stack < 2; stack++)
                for (int k = 0; k < 6 - stack * 2; k++)
                {
                    Vector3 p = at + new Vector3(stack * 0.13f, k * 0.022f, 0f);
                    cups.Add(BlockKit.Piece(Flare, p + Vector3.up * 0.1f, new Vector3(180f, 0f, 0f), new Vector3(0.085f, 0.1f, 0.085f)));
                    rims.Add(Part(Round, p, 0.089f, 0.008f));
                }
            BlockKit.Batch("CupStack", bar, Plain(new Color(0.95f, 0.93f, 0.88f)), cups);
            BlockKit.Batch("CupStackRims", bar, Plain(Teal * 0.7f), rims);
        }

        // ---- Ticket dispenser ---------------------------------------------------

        /// <summary>The prize counter's ticket dispenser: a rounded housing
        /// with a lit screen, a beacon on top and a strip of tickets feeding
        /// out of its slot onto the counter.</summary>
        private static void BuildTicketDispenser(Transform t, Vector3 at)
        {
            BlockKit.MeshPart("TicketMachine", t, Shared("ticketBody", () => CharacterMesh.Loft(new[]
            {
                new Ring(0f, 0.17f, 0.13f, 0f, 0f, 4f), new Ring(0.02f, 0.185f, 0.145f, 0f, 0f, 4f),
                new Ring(0.22f, 0.185f, 0.145f, 0f, 0f, 4f), new Ring(0.3f, 0.16f, 0.12f, 0.01f, 0f, 4f),
                new Ring(0.33f, 0.1f, 0.07f, 0.02f, 0f, 4f),
            }, 28, 3, "TicketBody")), at, Vector3.zero, CabinetTrim);
            BlockKit.Slab("TicketBezel", t, at + new Vector3(0f, 0.19f, -0.143f), new Vector3(0.28f, 0.13f, 0.012f), 0.004f, DarkGlass);
            BlockKit.Box("TicketScreen", t, at + new Vector3(0f, 0.19f, -0.151f), new Vector3(0.24f, 0.09f, 0.004f), Glow(Lemon, 1.2f));
            BlockKit.Box("TicketSlot", t, at + new Vector3(0f, 0.085f, -0.146f), new Vector3(0.2f, 0.016f, 0.01f), DarkGlass);
            BlockKit.MeshPart("TicketBeacon", t, SphereMesh, at + new Vector3(0f, 0.325f, 0.02f), Vector3.zero, Glow(Magenta, 1.6f),
                scale: new Vector3(0.09f, 0.09f, 0.09f));
            BlockKit.MeshPart("TicketBeaconCollar", t, Round, at + new Vector3(0f, 0.315f, 0.02f), Vector3.zero, BrightSteel,
                scale: new Vector3(0.1f, 0.015f, 0.1f));

            // The strip: out of the slot, over the counter edge of the housing
            // and along the top, perforated into tickets.
            var strip = new List<CombineInstance>();
            Vector3 a = at + new Vector3(0f, 0.085f, -0.15f), b = at + new Vector3(0.01f, 0.004f, -0.2f), c = at + new Vector3(0.2f, 0.004f, -0.26f);
            strip.Add(EdgeRibbon(a, b, 0.17f));
            strip.Add(EdgeRibbon(b, c, 0.17f));
            BlockKit.Batch("TicketStrip", t, Plain(new Color(1f, 0.8f, 0.3f)), strip);
            var marks = new List<CombineInstance>();
            for (int i = 1; i < 4; i++)
                marks.Add(BlockKit.Piece(BlockKit.CubeMesh, Vector3.Lerp(b, c, i / 4f) + Vector3.up * 0.002f,
                    new Vector3(0f, Mathf.Atan2(c.x - b.x, c.z - b.z) * Mathf.Rad2Deg, 0f), new Vector3(0.17f, 0.002f, 0.004f)));
            BlockKit.Batch("TicketPerforations", t, Plain(PrizeDark), marks);
        }

        /// <summary>A thin flat ribbon <paramref name="width"/> wide from
        /// <paramref name="a"/> to <paramref name="b"/>.</summary>
        private static CombineInstance EdgeRibbon(Vector3 a, Vector3 b, float width)
        {
            Vector3 d = b - a;
            Vector3 side = Vector3.Cross(Vector3.up, d).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            Quaternion q = Quaternion.LookRotation(d.normalized, Vector3.Cross(d.normalized, side));
            return new CombineInstance { mesh = BlockKit.CubeMesh, transform = Matrix4x4.TRS((a + b) * 0.5f, q, new Vector3(width, 0.003f, d.magnitude)) };
        }

        // ---- Beanbag ------------------------------------------------------------

        /// <summary>A sat-in beanbag: a sack bulging over its footprint, with
        /// a hollow where someone sat and the filling pushed up behind it.
        /// The sitter faces -Z.</summary>
        private static Mesh SackMesh()
        {
            const int rings = 16, segments = 28;
            const float sweep = 0.76f * Mathf.PI;
            float floor = Mathf.Cos(sweep);
            var verts = new List<Vector3>();
            for (int i = 0; i <= rings; i++)
            {
                float a = sweep * i / rings;
                float spread = Mathf.Sin(a);
                float lift = Mathf.Pow(Mathf.Max(0f, (Mathf.Cos(a) - floor) / (1f - floor)), 0.8f);
                for (int j = 0; j < segments; j++)
                {
                    float phi = j * Mathf.PI * 2f / segments;
                    float lumpy = 1f + 0.03f * Mathf.Sin(phi * 5f + 1f) * (1f - lift);
                    float x = spread * Mathf.Sin(phi) * 0.56f * lumpy;
                    float z = spread * Mathf.Cos(phi) * 0.53f * lumpy;
                    float back = Mathf.Exp(-((z - 0.27f) * (z - 0.27f) / 0.05f + x * x / 0.13f));
                    float seat = Mathf.Exp(-((z + 0.1f) * (z + 0.1f) / 0.05f + x * x / 0.08f));
                    float height = 0.43f + 0.36f * back - 0.15f * seat;
                    verts.Add(new Vector3(x, lift * height, z + 0.05f * back * lift));
                }
            }
            var tris = new List<int>();
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < segments; j++)
                {
                    int a = i * segments + j, b = i * segments + (j + 1) % segments;
                    int c = a + segments, d = b + segments;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            // Make sure the faces point outward, whichever way the grid wound.
            Vector3 p0 = verts[tris[tris.Count - 3]], p1 = verts[tris[tris.Count - 2]], p2 = verts[tris[tris.Count - 1]];
            Vector3 centre = (p0 + p1 + p2) / 3f;
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), new Vector3(centre.x, 0f, centre.z)) < 0f)
                for (int k = 0; k < tris.Count; k += 3) { int swap = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = swap; }

            var mesh = new Mesh { name = "Beanbag" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- Air hockey ---------------------------------------------------------

        /// <summary>An air hockey table: a rounded tub on two pedestal legs,
        /// a raised rail round a white surface with painted lines, mallets
        /// and a puck, and a lit scoreboard on an arch over the centre.</summary>
        private static void BuildAirHockeyModel(Transform parent, Vector3 position)
        {
            var t = Group(parent, "AirHockey", position, 0f);
            // The same footprint the old slab-sided table blocked.
            var body = t.gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 0.39f, 0f);
            body.size = new Vector3(2.28f, 0.78f, 1.24f);

            const float top = 0.8f, n = 5f;
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                BlockKit.Block("Leg", t, new Vector3(x, 0f, 0f), Vector3.zero, 0.03f, 0.5f,
                    new Vector2(0.14f, 0.46f), new Vector2(0.2f, 0.52f), 0.03f, CabinetBody);
                BlockKit.Slab("LegFoot", t, new Vector3(x, 0.02f, 0f), new Vector3(0.36f, 0.04f, 1.08f), 0.012f, CabinetTrim);
                BlockKit.Box("LegGlow", t, new Vector3(x, 0.26f, -0.5f), new Vector3(0.2f, 0.3f, 0.012f), Glow(Teal, 0.5f));
            }
            BlockKit.Slab("Stretcher", t, new Vector3(0f, 0.2f, 0f), new Vector3(1.3f, 0.1f, 0.12f), 0.02f, CabinetTrim);

            BlockKit.MeshPart("Body", t, Shared("airHockeyTub", () => CharacterMesh.Loft(new[]
            {
                new Ring(0.46f, 0.98f, 0.48f, 0f, 0f, n), new Ring(0.52f, 1.1f, 0.58f, 0f, 0f, n),
                new Ring(0.74f, 1.14f, 0.62f, 0f, 0f, n), new Ring(top, 1.14f, 0.62f, 0f, 0f, n),
            }, 48, 2, "AirHockeyTub")), Vector3.zero, Vector3.zero, CabinetBody);
            BlockKit.MeshPart("Stripe", t, Shared("airHockeyStripe", () => CharacterMesh.Loft(new[]
            {
                new Ring(0.6f, 1.129f, 0.609f, 0f, 0f, n), new Ring(0.64f, 1.136f, 0.616f, 0f, 0f, n),
            }, 48, 1, "AirHockeyStripe")), Vector3.zero, Vector3.zero, Glow(Magenta, 1f));
            BlockKit.MeshPart("Surface", t, Shared("airHockeySurface", () => CharacterMesh.Loft(new[]
            {
                new Ring(top - 0.01f, 1.1f, 0.58f, 0f, 0f, n), new Ring(top + 0.006f, 1.1f, 0.58f, 0f, 0f, n),
            }, 48, 1, "AirHockeySurface")), Vector3.zero, Vector3.zero, Plain(new Color(0.86f, 0.91f, 0.96f)));
            BlockKit.Batch("Rail", t, CabinetTrim, RoundedLoop(top + 0.03f, 1.115f, 0.595f, n, 0.06f));
            BlockKit.Batch("RailGlow", t, Glow(Teal, 1.4f), RoundedLoop(top + 0.064f, 1.115f, 0.595f, n, 0.014f));

            // Painted ice: centre line and circle, a crease at each goal.
            float ice = top + 0.008f;
            var lines = new List<CombineInstance>
            {
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, ice, 0f), Vector3.zero, new Vector3(0.018f, 0.002f, 1.1f)),
                Part(Shared("airHockeyCircle", () => CharacterMesh.Annulus(28, 0.16f, 0.178f, 0f, 1f, "AirHockeyCircle")),
                    new Vector3(0f, ice - 0.001f, 0f), 1f, 0.002f),
                Part(Round, new Vector3(0f, ice - 0.001f, 0f), 0.05f, 0.0025f),
            };
            BlockKit.Batch("CentreLine", t, Glow(Magenta, 1.2f), lines);
            Mesh crease = Shared("airHockeyCrease", () => CharacterMesh.Arc(0.2f, 0.218f, -90f, 90f, 20, "AirHockeyCrease"));
            var creases = new List<CombineInstance>
            {
                BlockKit.Piece(crease, new Vector3(-1.06f, ice, 0f), new Vector3(90f, 0f, 0f), Vector3.one),
                BlockKit.Piece(crease, new Vector3(1.06f, ice, 0f), new Vector3(90f, 180f, 0f), Vector3.one),
            };
            foreach (float x in new[] { -0.55f, 0.55f })
                creases.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, ice, 0f), Vector3.zero, new Vector3(0.012f, 0.002f, 1.1f)));
            BlockKit.Batch("Creases", t, Glow(Teal, 1.0f), creases);
            foreach (float x in new[] { -1f, 1f })
            {
                BlockKit.Box("Goal", t, new Vector3(x * 1.083f, top + 0.028f, 0f), new Vector3(0.012f, 0.04f, 0.36f), DarkGlass);
                BlockKit.Slab("PuckReturn", t, new Vector3(x * 1.13f, 0.62f, 0f), new Vector3(0.03f, 0.07f, 0.3f), 0.008f, DarkGlass);
            }

            // Mallets and puck.
            foreach (float x in new[] { -0.8f, 0.75f })
            {
                Vector3 at = new Vector3(x, ice, x < 0f ? 0.15f : -0.2f);
                var mallet = new List<CombineInstance>
                {
                    Part(Round, at, 0.11f, 0.022f), Part(Round, at + Vector3.up * 0.02f, 0.05f, 0.05f),
                    Ball(at + Vector3.up * 0.075f, Vector3.one * 0.058f),
                };
                BlockKit.Batch("Mallet", t, Glow(x < 0f ? Magenta : Lemon, 1.2f), mallet);
            }
            BlockKit.MeshPart("Puck", t, Round, new Vector3(0.25f, ice, -0.15f), Vector3.zero, Plain(PrizeRed), scale: new Vector3(0.075f, 0.01f, 0.075f));

            // Scoreboard arch over the centre line.
            var arch = new List<CombineInstance>();
            foreach (float z in new[] { -0.64f, 0.64f })
            {
                arch.Add(Spindle(new Vector3(0f, 1.2f, z), Vector3.zero, 0.035f, 1.0f));
                arch.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, 0.7f, z), Vector3.zero, new Vector3(0.1f, 0.14f, 0.05f)));
            }
            arch.Add(Spindle(new Vector3(0f, 1.7f, 0f), new Vector3(90f, 0f, 0f), 0.035f, 1.315f));
            BlockKit.Batch("Arch", t, BrightSteel, arch);
            BlockKit.Slab("ScoreBox", t, new Vector3(0f, 1.7f, 0f), new Vector3(0.14f, 0.2f, 0.56f), 0.02f, CabinetBody);
            foreach (float face in new[] { -1f, 1f })
            {
                BlockKit.Box("ScoreHome", t, new Vector3(face * 0.072f, 1.7f, -0.13f), new Vector3(0.004f, 0.12f, 0.16f), Glow(Magenta, 1.4f));
                BlockKit.Box("ScoreAway", t, new Vector3(face * 0.072f, 1.7f, 0.13f), new Vector3(0.004f, 0.12f, 0.16f), Glow(Lemon, 1.4f));
            }
            BlockKit.Box("ArchLight", t, new Vector3(0f, 1.595f, 0f), new Vector3(0.1f, 0.012f, 0.5f), Glow(new Color(0.9f, 0.95f, 1f), 1.6f));
        }

        // ---- Hoop shooter -------------------------------------------------------

        /// <summary>The hoop itself: a round rim on a bracket and a net of
        /// cords narrowing below it.</summary>
        private static void BuildHoop(Transform t, Vector3 rimCentre, float backboardZ)
        {
            BlockKit.MeshPart("Hoop", t, Shared("hoopRim", () => CharacterMesh.Annulus(24, 0.19f, 0.215f, -0.012f, 0.012f, "HoopRim")),
                rimCentre, Vector3.zero, Glow(new Color(1f, 0.4f, 0.1f), 1.4f));
            BlockKit.Box("HoopBracket", t, new Vector3(rimCentre.x, rimCentre.y - 0.02f, (rimCentre.z + 0.2f + backboardZ) * 0.5f),
                new Vector3(0.1f, 0.04f, backboardZ - rimCentre.z - 0.2f), Plain(new Color(0.8f, 0.3f, 0.08f)));

            var net = new List<CombineInstance>();
            const int cords = 12;
            for (int i = 0; i < cords; i++)
            {
                float a0 = i * Mathf.PI * 2f / cords, a1 = (i + 1) * Mathf.PI * 2f / cords;
                Vector3 top0 = rimCentre + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * 0.2f;
                Vector3 top1 = rimCentre + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * 0.2f;
                Vector3 mid = rimCentre + new Vector3(Mathf.Sin((a0 + a1) * 0.5f), 0f, Mathf.Cos((a0 + a1) * 0.5f)) * 0.15f + Vector3.down * 0.13f;
                Vector3 low = rimCentre + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * 0.115f + Vector3.down * 0.27f;
                Vector3 lowNext = rimCentre + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * 0.115f + Vector3.down * 0.27f;
                net.Add(Strut(top0, mid, 0.008f));
                net.Add(Strut(top1, mid, 0.008f));
                net.Add(Strut(mid, low, 0.008f));
                net.Add(Strut(mid, lowNext, 0.008f));
            }
            BlockKit.Batch("Net", t, WhiteLine, net);
        }

        /// <summary>A side of the shooter's cage: a tube frame strung with a
        /// diamond net, in place of a solid panel.</summary>
        private static void BuildCageSide(Transform t, float x, float y0, float y1, float z0, float z1)
        {
            var frame = new List<CombineInstance>
            {
                Strut(new Vector3(x, y0, z0), new Vector3(x, y1, z0), 0.03f), Strut(new Vector3(x, y0, z1), new Vector3(x, y1, z1), 0.03f),
                Strut(new Vector3(x, y0, z0), new Vector3(x, y0, z1), 0.03f),
            };
            BlockKit.Batch("CageFrame", t, CabinetTrim, frame);

            var net = new List<CombineInstance>();
            float height = y1 - y0, length = z1 - z0;
            const float pitch = 0.2f;
            for (float s = -height; s < length; s += pitch)
            {
                // One rising and one falling cord per step, clipped to the panel.
                float za = Mathf.Max(s, 0f), zb = Mathf.Min(s + height, length);
                if (zb - za < 0.02f) continue;
                net.Add(Strut(new Vector3(x, y0 + (za - s), z0 + za), new Vector3(x, y0 + (zb - s), z0 + zb), 0.007f));
                net.Add(Strut(new Vector3(x, y1 - (za - s), z0 + za), new Vector3(x, y1 - (zb - s), z0 + zb), 0.007f));
            }
            BlockKit.Batch("SideNet", t, Plain(new Color(0.42f, 0.42f, 0.5f)), net);
        }

        /// <summary>The two seams of a basketball of <paramref name="diameter"/> at <paramref name="at"/>.</summary>
        private static void AddBallSeams(List<CombineInstance> into, Vector3 at, float diameter)
        {
            Mesh seam = Shared("ballSeam", () => CharacterMesh.Annulus(20, 0.485f, 0.507f, -0.03f, 0.03f, "BallSeam"));
            into.Add(BlockKit.Piece(seam, at, Vector3.zero, Vector3.one * diameter));
            into.Add(BlockKit.Piece(seam, at, new Vector3(90f, 20f, 0f), Vector3.one * diameter));
            into.Add(BlockKit.Piece(seam, at, new Vector3(0f, 0f, 90f), Vector3.one * diameter));
        }

        // ---- Ping-pong ----------------------------------------------------------

        /// <summary>What holds a ping-pong table up and strings its net: a
        /// folding steel undercarriage on casters, an apron under the top,
        /// and clamp posts carrying a net with a white tape.</summary>
        private static void BuildPingPongFrame(Transform t)
        {
            var frame = new List<CombineInstance>();
            var wheels = new List<CombineInstance>();
            foreach (float z in new[] { -0.95f, 0.95f })
            {
                foreach (float x in new[] { -0.56f, 0.56f })
                {
                    frame.Add(Strut(new Vector3(x, 0.09f, z), new Vector3(x, 0.72f, z), 0.04f));
                    frame.Add(Strut(new Vector3(x, 0.4f, z), new Vector3(x, 0.72f, z - Mathf.Sign(z) * 0.36f), 0.03f));   // brace
                    wheels.Add(Spindle(new Vector3(x, 0.045f, z), new Vector3(0f, 0f, 90f), 0.09f, 0.04f));
                }
                frame.Add(Strut(new Vector3(-0.56f, 0.14f, z), new Vector3(0.56f, 0.14f, z), 0.04f));
            }
            frame.Add(Strut(new Vector3(0f, 0.14f, -0.95f), new Vector3(0f, 0.14f, 0.95f), 0.04f));
            frame.AddRange(BlockKit.Outline(new Vector3(0f, 0.715f, 0f), 1.44f, 2.66f, 0.05f));
            BlockKit.Batch("Frame", t, CabinetTrim, frame);
            BlockKit.Batch("Casters", t, Plain(PrizeDark), wheels);

            var posts = new List<CombineInstance>();
            foreach (float x in new[] { -0.81f, 0.81f })
            {
                posts.Add(Spindle(new Vector3(x, 0.85f, 0f), Vector3.zero, 0.02f, 0.18f));
                posts.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x * 0.975f, 0.76f, 0f), Vector3.zero, new Vector3(0.07f, 0.06f, 0.05f)));
            }
            BlockKit.Batch("NetPosts", t, BrightSteel, posts);
            BlockKit.Box("NetTape", t, new Vector3(0f, 0.928f, 0f), new Vector3(1.62f, 0.016f, 0.014f), WhiteLine);
        }

        // ---- Pinball ------------------------------------------------------------

        /// <summary>A tapered steel leg on a round levelling foot.</summary>
        private static void BuildPinballLeg(Transform p, Vector3 at, float height)
        {
            BlockKit.Block("Leg", p, at, Vector3.zero, 0.03f, height, new Vector2(0.022f, 0.022f), new Vector2(0.04f, 0.04f), 0.008f, BrightSteel);
            BlockKit.MeshPart("LegFoot", p, Round, at, Vector3.zero, Plain(PrizeDark), scale: new Vector3(0.085f, 0.03f, 0.085f));
            BlockKit.Box("LegBolt", p, at + new Vector3(0f, height - 0.06f, -0.042f), new Vector3(0.03f, 0.03f, 0.008f), CabinetTrim);
            BlockKit.Box("LegBolt", p, at + new Vector3(0f, height - 0.13f, -0.042f), new Vector3(0.03f, 0.03f, 0.008f), CabinetTrim);
        }

        /// <summary>A pop bumper: a white skirt, a lit body and a domed cap.</summary>
        private static void BuildBumper(Transform p, Vector3 at)
        {
            BlockKit.MeshPart("BumperBase", p, Round, at, Vector3.zero, Plain(PrizeWhite), scale: new Vector3(0.115f, 0.018f, 0.115f));
            BlockKit.MeshPart("Bumper", p, Round, at + Vector3.up * 0.018f, Vector3.zero, Glow(Magenta, 1.6f), scale: new Vector3(0.072f, 0.03f, 0.072f));
            BlockKit.MeshPart("BumperCap", p, SphereMesh, at + Vector3.up * 0.048f, Vector3.zero, Plain(PrizeRed), scale: new Vector3(0.105f, 0.03f, 0.105f));
            BlockKit.MeshPart("BumperStar", p, Round, at + Vector3.up * 0.061f, Vector3.zero, Glow(Lemon, 1.2f), scale: new Vector3(0.04f, 0.004f, 0.04f));
        }

        /// <summary>The playfield furniture that makes a table look played:
        /// a wire ramp arcing over the middle, an apron with its cards at
        /// the front, flipper pivots, lane posts with rubber rings, flipper
        /// buttons on the cabinet sides, and speakers in the backbox.</summary>
        private static void BuildPinballDetail(Transform p, System.Func<float, float> field)
        {
            // Wire ramp: two rails from the left lane up and over to the right.
            var wire = new List<CombineInstance>();
            const int spans = 14;
            Vector3 Rail(float s, float offset)
            {
                float x = -0.25f + 0.5f * s;
                float z = 0.02f + 0.2f * Mathf.Sin(Mathf.PI * s);
                return new Vector3(x, field(z) + 0.012f + 0.042f * Mathf.Sin(Mathf.PI * s), z + offset);
            }
            for (int i = 0; i < spans; i++)
            {
                float s0 = i / (float)spans, s1 = (i + 1) / (float)spans;
                foreach (float offset in new[] { -0.022f, 0.022f })
                    wire.Add(Strut(Rail(s0, offset), Rail(s1, offset), 0.007f));
                if (i % 3 == 1)
                {
                    Vector3 mid = Rail(s0, 0f);
                    wire.Add(Strut(Rail(s0, -0.022f), Rail(s0, 0.022f), 0.006f));
                    wire.Add(Strut(mid, new Vector3(mid.x, field(mid.z), mid.z), 0.006f));
                }
            }
            // Lane posts either side of the inserts, and the flipper pivots.
            var rubber = new List<CombineInstance>();
            foreach (float x in new[] { -0.24f, 0.24f })
                foreach (float z in new[] { -0.12f, 0.12f })
                {
                    wire.Add(Part(Round, new Vector3(x, field(z), z), 0.016f, 0.04f));
                    rubber.Add(Part(Hoop, new Vector3(x, field(z) + 0.012f, z), 0.034f, 0.012f));
                }
            BlockKit.Batch("WireRamp", p, BrightSteel, wire);
            BlockKit.Batch("PostRubbers", p, Plain(PrizeRed), rubber);
            var pivots = new List<CombineInstance>();
            foreach (float s in new[] { -1f, 1f })
                pivots.Add(Part(Round, new Vector3(s * 0.15f, field(-0.47f) + 0.006f, -0.47f), 0.045f, 0.032f));
            BlockKit.Batch("FlipperPivots", p, Glow(Lemon, 1.2f), pivots);

            // Apron and its two instruction cards.
            float apronZ = -0.61f;
            BlockKit.MeshPart("Apron", p, BlockKit.CubeMesh, new Vector3(0f, field(apronZ) + 0.022f, apronZ), new Vector3(-5f, 0f, 0f),
                CabinetTrim, scale: new Vector3(0.66f, 0.03f, 0.14f));
            foreach (float x in new[] { -0.19f, 0.19f })
                BlockKit.MeshPart("ApronCard", p, BlockKit.CubeMesh, new Vector3(x, field(apronZ) + 0.039f, apronZ), new Vector3(-5f, 0f, 0f),
                    Paper, scale: new Vector3(0.2f, 0.002f, 0.09f));
            BlockKit.MeshPart("ApronDrain", p, BlockKit.CubeMesh, new Vector3(0f, field(apronZ) + 0.039f, apronZ + 0.03f), new Vector3(-5f, 0f, 0f),
                DarkGlass, scale: new Vector3(0.08f, 0.002f, 0.05f));

            // Flipper buttons and the cabinet's front corner guards.
            var buttons = new List<CombineInstance>();
            foreach (float sx in new[] { -1f, 1f })
                buttons.Add(Spindle(new Vector3(sx * 0.378f, 0.9f, -0.6f), new Vector3(0f, 0f, 90f), 0.045f, 0.03f));
            BlockKit.Batch("FlipperButtons", p, Plain(PrizeWhite), buttons);

            // Speakers under the backbox screen.
            var grilles = new List<CombineInstance>();
            foreach (float x in new[] { -0.22f, 0.22f })
            {
                grilles.Add(FacingRing(new Vector3(x, 1.09f, 0.569f), 0.11f, 0.006f));
                grilles.Add(FacingRing(new Vector3(x, 1.09f, 0.569f), 0.05f, 0.006f));
            }
            BlockKit.Batch("Speakers", p, BrightSteel, grilles);
        }

        // ---- LAN desk -----------------------------------------------------------

        /// <summary>A gaming PC under the desk: a dark tower with a lit side
        /// strip and a glowing fan ring on its front.</summary>
        private static void BuildPcTower(Transform t, Vector3 at, Color c)
        {
            BlockKit.Slab("PcTower", t, at + new Vector3(0f, 0.25f, 0f), new Vector3(0.2f, 0.46f, 0.44f), 0.015f, CabinetBody);
            BlockKit.Box("PcFront", t, at + new Vector3(0f, 0.25f, -0.222f), new Vector3(0.16f, 0.4f, 0.006f), DarkGlass);
            var lit = new List<CombineInstance>();
            for (int k = 0; k < 2; k++)
                lit.Add(FacingRing(at + new Vector3(0f, 0.16f + k * 0.17f, -0.224f), 0.13f, 0.008f));
            lit.Add(BlockKit.Piece(BlockKit.CubeMesh, at + new Vector3(-0.101f, 0.25f, 0f), Vector3.zero, new Vector3(0.004f, 0.36f, 0.02f)));
            BlockKit.Batch("PcGlow", t, Glow(c, 1.3f), lit);
            var feet = new List<CombineInstance>();
            foreach (float z in new[] { -0.16f, 0.16f })
                feet.Add(BlockKit.Piece(BlockKit.CubeMesh, at + new Vector3(0f, 0.01f, z), Vector3.zero, new Vector3(0.18f, 0.02f, 0.04f)));
            BlockKit.Batch("PcFeet", t, Plain(PrizeDark), feet);
        }

        /// <summary>A headset left on the desk, standing on its ear cups.</summary>
        private static void BuildHeadset(Transform t, Vector3 at, Color c)
        {
            var dark = new List<CombineInstance>();
            foreach (float x in new[] { -0.085f, 0.085f })
                dark.Add(Ball(at + new Vector3(x, 0.045f, 0f), new Vector3(0.045f, 0.09f, 0.08f)));
            dark.Add(BlockKit.Piece(Shared("headsetBand", () => CharacterMesh.Arc(0.086f, 0.1f, 0f, 180f, 16, "HeadsetBand")),
                at + new Vector3(0f, 0.07f, 0f), Vector3.zero, new Vector3(1f, 1f, 1f)));
            BlockKit.Batch("Headset", t, Plain(PrizeDark), dark);
            var lit = new List<CombineInstance>();
            foreach (float x in new[] { -1f, 1f })
                lit.Add(Spindle(at + new Vector3(x * 0.108f, 0.045f, 0f), new Vector3(0f, 0f, 90f), 0.04f, 0.006f));
            BlockKit.Batch("HeadsetGlow", t, Glow(c, 1.2f), lit);
        }
    }
}
