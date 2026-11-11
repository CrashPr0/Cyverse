using System;
using System.Collections.Generic;
using UnityEngine;
using Ring = Cyverse.Level.CharacterMesh.Ring;

namespace Cyverse.Level
{
    /// <summary>
    /// The game room's shaped props: the pieces that were plain boxes and
    /// stretched spheres, rebuilt with a real silhouette. Arcade cabinets get
    /// the classic side profile, the claw machine a proper claw and railed
    /// prize chute, and the foosball table, bar stools, pendant lamps,
    /// beanbags, gaming chairs and plush prizes are modelled as what they
    /// are. Round forms are lofted or turned (CharacterMesh), profiles are
    /// extruded, and small parts are batched so each prop stays a handful of
    /// renderers.
    /// </summary>
    public static partial class Level4GameRoom
    {
        // ---- Mesh helpers -------------------------------------------------------

        private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        /// <summary>A mesh built once and shared by every copy of a prop.</summary>
        private static Mesh Shared(string key, Func<Mesh> build)
        {
            if (meshes.TryGetValue(key, out Mesh m) && m != null) return m;
            return meshes[key] = build();
        }

        private static Mesh sphereMesh;

        private static Mesh SphereMesh
        {
            get
            {
                if (sphereMesh != null) return sphereMesh;
                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereMesh = probe.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.DestroyImmediate(probe);
                return sphereMesh;
            }
        }

        private static Material brightSteel;

        /// <summary>A pale steel that stays readable in the dark room, where
        /// mirror chrome only reflects the black ceiling.</summary>
        private static Material BrightSteel => brightSteel != null
            ? brightSteel
            : (brightSteel = BuildKit.MakeStandard(new Color(0.74f, 0.77f, 0.84f), 0.55f, 0.25f));

        private static CombineInstance Ball(Vector3 at, Vector3 size) => BlockKit.Piece(SphereMesh, at, Vector3.zero, size);

        /// <summary>A square bar from <paramref name="a"/> to <paramref name="b"/>.</summary>
        private static CombineInstance Strut(Vector3 a, Vector3 b, float thickness)
        {
            Vector3 d = b - a;
            return new CombineInstance
            {
                mesh = BlockKit.CubeMesh,
                transform = Matrix4x4.TRS((a + b) * 0.5f, Quaternion.LookRotation(d.normalized),
                    new Vector3(thickness, thickness, d.magnitude)),
            };
        }

        /// <summary>A flat strip <paramref name="width"/> wide (along X) lying
        /// on the line from <paramref name="a"/> to <paramref name="b"/>,
        /// which must run in the YZ plane.</summary>
        private static CombineInstance EdgeStrip(Vector3 a, Vector3 b, float width, float thickness)
        {
            Vector3 d = (b - a).normalized;
            return new CombineInstance
            {
                mesh = BlockKit.CubeMesh,
                transform = Matrix4x4.TRS((a + b) * 0.5f, Quaternion.LookRotation(d, Vector3.Cross(d, Vector3.right)),
                    new Vector3(width, thickness, (b - a).magnitude + thickness)),
            };
        }

        /// <summary>A flat-shaded panel: the outline, given as (z, y) points,
        /// extruded <paramref name="thickness"/> along X about x = 0. The
        /// outline may be concave.</summary>
        private static Mesh ExtrudeProfile(Vector2[] outline, float thickness, string name)
        {
            int n = outline.Length;
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % n];
                area += p.x * q.y - q.x * p.y;
            }
            var poly = new List<Vector2>(outline);
            if (area < 0f) poly.Reverse(); // counter-clockwise in (z, y)

            var verts = new List<Vector3>();
            var tris = new List<int>();
            float h = thickness * 0.5f;

            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f) { Vector3 t = b; b = c; c = t; }
                int at = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(at); tris.Add(at + 1); tris.Add(at + 2);
            }

            // Ear clipping for the two faces.
            var index = new List<int>();
            for (int i = 0; i < n; i++) index.Add(i);
            int guard = 0;
            while (index.Count > 2 && guard++ < n * n)
            {
                bool clipped = false;
                for (int i = 0; i < index.Count; i++)
                {
                    int ia = index[(i + index.Count - 1) % index.Count], ib = index[i], ic = index[(i + 1) % index.Count];
                    Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                    if (Cross(b - a, c - b) <= 1e-7f) continue; // reflex or straight
                    bool empty = true;
                    foreach (int k in index)
                    {
                        if (k == ia || k == ib || k == ic) continue;
                        Vector2 p = poly[k];
                        if (Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f) { empty = false; break; }
                    }
                    if (!empty) continue;
                    foreach (float x in new[] { -h, h })
                        Tri(new Vector3(x, a.y, a.x), new Vector3(x, b.y, b.x), new Vector3(x, c.y, c.x), new Vector3(x, 0f, 0f));
                    index.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }

            // The rim.
            for (int i = 0; i < n; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % n];
                Vector2 d = q - p;
                Vector3 outward = new Vector3(0f, -d.x, d.y); // (z, y) edge normal for a CCW outline
                Vector3 p0 = new Vector3(-h, p.y, p.x), p1 = new Vector3(h, p.y, p.x);
                Vector3 q0 = new Vector3(-h, q.y, q.x), q1 = new Vector3(h, q.y, q.x);
                Tri(p0, p1, q1, outward);
                Tri(p0, q1, q0, outward);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        // ---- Arcade cabinet -----------------------------------------------------

        /// <summary>The cabinet's side profile as (z, y), front toward -Z: kick
        /// plate, the control panel's nose, the screen leaning back, the
        /// marquee overhanging it, and a straight back.</summary>
        private static readonly Vector2[] ArcadeProfile =
        {
            new Vector2(-0.44f, 0f), new Vector2(-0.44f, 0.86f), new Vector2(-0.75f, 0.915f), new Vector2(-0.75f, 1.045f),
            new Vector2(-0.38f, 1.125f), new Vector2(-0.24f, 1.90f), new Vector2(-0.33f, 1.97f), new Vector2(-0.33f, 2.25f),
            new Vector2(0.42f, 2.25f), new Vector2(0.42f, 0f),
        };

        /// <summary>Both side panels, cut to the classic arcade silhouette,
        /// with glowing T-molding along the front and top edges.</summary>
        private static void BuildArcadeSides(Transform cab, Material side, Material molding)
        {
            Mesh panel = Shared("arcadeSide", () => ExtrudeProfile(ArcadeProfile, 0.04f, "ArcadeSide"));
            var strips = new List<CombineInstance>();
            foreach (float sx in new[] { -0.5f, 0.5f })
            {
                BlockKit.MeshPart("SidePanel", cab, panel, new Vector3(sx, 0f, 0f), Vector3.zero, side);
                for (int i = 0; i < 8; i++)
                {
                    Vector2 a = ArcadeProfile[i], b = ArcadeProfile[i + 1];
                    strips.Add(EdgeStrip(new Vector3(sx, a.y, a.x), new Vector3(sx, b.y, b.x), 0.05f, 0.014f));
                }
            }
            BlockKit.Batch("Molding", cab, molding, strips);
        }

        // ---- Claw machine -------------------------------------------------------

        /// <summary>A three-finger claw on its cable, hanging open over
        /// <paramref name="over"/> from the gantry at <paramref name="gantryY"/>.</summary>
        private static void BuildClaw(Transform m, Vector3 over, float gantryY)
        {
            float hubTop = over.y + 0.5f, hubBottom = hubTop - 0.1f;
            BlockKit.MeshPart("Cable", m, Shared("clawCable", () => CharacterMesh.Prism(8, 0.008f, 0.008f, 0f, 1f, "Cable")),
                new Vector3(over.x, hubTop, over.z), Vector3.zero, BrightSteel, scale: new Vector3(1f, gantryY - hubTop, 1f));
            BlockKit.MeshPart("ClawHub", m, Shared("clawHub", () => CharacterMesh.Prism(12, 0.07f, 0.05f, 0f, 0.1f, "ClawHub")),
                new Vector3(over.x, hubBottom, over.z), Vector3.zero, BrightSteel);
            BlockKit.MeshPart("ClawBand", m, Shared("clawBand", () => CharacterMesh.Annulus(12, 0.058f, 0.072f, 0f, 0.02f, "ClawBand")),
                new Vector3(over.x, hubBottom + 0.05f, over.z), Vector3.zero, Glow(Magenta, 1.6f));

            var fingers = new List<CombineInstance>();
            Vector3 hub = new Vector3(over.x, hubBottom + 0.02f, over.z);
            for (int i = 0; i < 3; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 120f + 30f, 0f) * Vector3.forward;
                Vector3 shoulder = hub + dir * 0.055f;
                Vector3 elbow = hub + dir * 0.19f + Vector3.down * 0.13f;
                Vector3 tip = hub + dir * 0.09f + Vector3.down * 0.31f;
                fingers.Add(Strut(shoulder, elbow, 0.026f));
                fingers.Add(Strut(elbow, tip, 0.024f));
                fingers.Add(Ball(elbow, Vector3.one * 0.04f));
                fingers.Add(Strut(tip, tip - dir * 0.045f + Vector3.down * 0.01f, 0.03f));
            }
            BlockKit.Batch("ClawFingers", m, BrightSteel, fingers);
        }

        /// <summary>The prize chute: a low bin with a dark mouth and a lit
        /// rim, guarded from the prize pile by two clear acrylic walls.</summary>
        private static void BuildChute(Transform m, Vector3 at)
        {
            const float size = 0.32f, top = 0.15f;
            BlockKit.Slab("Chute", m, at + new Vector3(0f, top * 0.5f, 0f), new Vector3(size, top, size), 0.01f, CabinetTrim);
            BlockKit.Box("ChuteMouth", m, at + new Vector3(0f, top + 0.002f, 0f), new Vector3(size - 0.07f, 0.004f, size - 0.07f), DarkGlass);
            BlockKit.Batch("ChuteRim", m, Glow(Lemon, 1.4f), BlockKit.Outline(at + new Vector3(0f, top + 0.006f, 0f), size - 0.03f, size - 0.03f, 0.03f));
            // Guards on the two sides that face the prizes.
            Pane(m, "ChuteGuardSide", at + new Vector3(-size * 0.5f, 0.21f, 0f), 90f, new Vector2(size, 0.42f));
            Pane(m, "ChuteGuardBack", at + new Vector3(0f, 0.21f, size * 0.5f), 0f, new Vector2(size, 0.42f));
            // Lit edges, so the clear walls read in the dark box.
            var edges = new List<CombineInstance>
            {
                BlockKit.Piece(BlockKit.CubeMesh, at + new Vector3(-size * 0.5f, 0.42f, 0f), Vector3.zero, new Vector3(0.012f, 0.012f, size)),
                BlockKit.Piece(BlockKit.CubeMesh, at + new Vector3(0f, 0.42f, size * 0.5f), Vector3.zero, new Vector3(size, 0.012f, 0.012f)),
            };
            foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
                edges.Add(BlockKit.Piece(BlockKit.CubeMesh, at + new Vector3(corner.x * size * 0.5f, 0.21f, corner.y * size * 0.5f),
                    Vector3.zero, new Vector3(0.012f, 0.42f, 0.012f)));
            BlockKit.Batch("ChuteGuardEdges", m, Glow(Teal, 1.1f), edges);
        }

        // ---- Bar ----------------------------------------------------------------

        /// <summary>A bar stool: a round cushioned seat on a chrome pedestal
        /// with a foot ring.</summary>
        private static void BuildStool(Transform bar, string id, Vector3 at, Color seat)
        {
            var s = Group(bar, "Stool_" + id, at, 0f);
            BlockKit.MeshPart("Base", s, Shared("stoolBase", () => CharacterMesh.Prism(20, 0.21f, 0.17f, 0f, 0.035f, "StoolBase")),
                new Vector3(0f, 0.006f, 0f), Vector3.zero, BrightSteel);
            BlockKit.MeshPart("Pole", s, Shared("stoolPole", () => CharacterMesh.Prism(12, 0.036f, 0.03f, 0.035f, 0.69f, "StoolPole")),
                Vector3.zero, Vector3.zero, BrightSteel);
            BlockKit.MeshPart("FootRing", s, Shared("stoolRing", () => CharacterMesh.Annulus(20, 0.135f, 0.16f, 0f, 0.022f, "StoolRing")),
                new Vector3(0f, 0.27f, 0f), Vector3.zero, BrightSteel);
            BlockKit.Batch("FootRingArms", s, BrightSteel, new List<CombineInstance>
            {
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, 0.281f, 0f), Vector3.zero, new Vector3(0.28f, 0.016f, 0.02f)),
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, 0.281f, 0f), Vector3.zero, new Vector3(0.02f, 0.016f, 0.28f)),
            });
            BlockKit.MeshPart("Seat", s, Shared("stoolSeat", () => CharacterMesh.Loft(new[]
            {
                new Ring(0.69f, 0.16f, 0.16f), new Ring(0.705f, 0.215f, 0.215f), new Ring(0.75f, 0.228f, 0.228f),
                new Ring(0.785f, 0.2f, 0.2f), new Ring(0.798f, 0.12f, 0.12f),
            }, 20, 2, "StoolSeat")), Vector3.zero, Vector3.zero, Plain(seat));
        }

        /// <summary>A round metal pendant shade.</summary>
        private static void BuildPendantShade(Transform bar, Vector3 at) =>
            BlockKit.MeshPart("PendantShade", bar, Shared("pendantShade", () => CharacterMesh.Loft(new[]
            {
                new Ring(0f, 0.18f, 0.18f), new Ring(0.05f, 0.165f, 0.165f), new Ring(0.13f, 0.09f, 0.09f),
                new Ring(0.19f, 0.045f, 0.045f), new Ring(0.22f, 0.03f, 0.03f),
            }, 18, 2, "PendantShade")), at, Vector3.zero, CabinetTrim);

        // ---- Soft furnishings ---------------------------------------------------

        /// <summary>A slumped beanbag: wide and low in front where you sit, rising
        /// to a lump at the back.</summary>
        private static Transform BuildBeanbagModel(Transform parent, Vector3 position, float yaw, Color colour)
        {
            Transform bag = BlockKit.MeshPart("Beanbag", parent, Shared("beanbag", () => CharacterMesh.Loft(new[]
            {
                new Ring(0f, 0.42f, 0.40f), new Ring(0.06f, 0.56f, 0.52f), new Ring(0.22f, 0.58f, 0.54f),
                new Ring(0.34f, 0.50f, 0.46f, 0.06f), new Ring(0.44f, 0.37f, 0.31f, 0.2f), new Ring(0.56f, 0.28f, 0.2f, 0.3f),
                new Ring(0.66f, 0.2f, 0.14f, 0.34f), new Ring(0.71f, 0.1f, 0.07f, 0.35f),
            }, 22, 3, "Beanbag")), position, new Vector3(0f, yaw, 0f), BuildKit.MakeStandard(colour * 0.75f, 0.15f, 0f));
            // The same footprint the old round bag blocked.
            var body = bag.gameObject.AddComponent<SphereCollider>();
            body.center = new Vector3(0f, 0.3f, 0f);
            body.radius = 0.525f;
            bag.gameObject.isStatic = true;
            return bag;
        }

        /// <summary>A gaming chair; the sitter faces local -Z. A bucket seat
        /// and a tall winged back on a five-star base, with armrests, pillows
        /// in the accent colour and racing stripes down the back.</summary>
        private static void BuildGamingChairModel(Transform parent, Vector3 position, float yaw, Color accent)
        {
            var c = Group(parent, "GamingChair", position, yaw);
            Material shell = Plain(new Color(0.09f, 0.08f, 0.115f));
            Material trim = Plain(accent * 0.85f);

            var baseParts = new List<CombineInstance>();
            for (int i = 0; i < 5; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 72f, 0f) * Vector3.forward;
                baseParts.Add(BlockKit.Piece(BlockKit.CubeMesh, dir * 0.16f + Vector3.up * 0.075f, new Vector3(0f, i * 72f, 0f),
                    new Vector3(0.05f, 0.03f, 0.32f)));
                baseParts.Add(Ball(dir * 0.3f + Vector3.up * 0.035f, Vector3.one * 0.07f));
            }
            BlockKit.Batch("Base", c, CabinetTrim, baseParts);
            BlockKit.MeshPart("Pole", c, Shared("chairPole", () => CharacterMesh.Prism(10, 0.032f, 0.032f, 0.08f, 0.46f, "ChairPole")),
                Vector3.zero, Vector3.zero, Chrome);

            BlockKit.MeshPart("Seat", c, Shared("chairSeat", () => CharacterMesh.Loft(new[]
            {
                new Ring(0.45f, 0.2f, 0.2f, 0f, 0f, 4f), new Ring(0.49f, 0.275f, 0.265f, 0f, 0f, 4f),
                new Ring(0.56f, 0.285f, 0.275f, 0f, 0f, 4f), new Ring(0.59f, 0.24f, 0.23f, 0f, 0f, 4f),
            }, 20, 2, "ChairSeat")), Vector3.zero, Vector3.zero, shell, solid: true);
            BlockKit.MeshPart("Back", c, Shared("chairBack", () => CharacterMesh.Loft(new[]
            {
                new Ring(0.55f, 0.2f, 0.05f, 0.25f, 0f, 4f), new Ring(0.74f, 0.25f, 0.06f, 0.27f, 0f, 4f),
                new Ring(1.05f, 0.275f, 0.062f, 0.30f, 0f, 4f), new Ring(1.25f, 0.25f, 0.058f, 0.32f, 0f, 4f),
                new Ring(1.4f, 0.18f, 0.052f, 0.335f, 0f, 4f), new Ring(1.5f, 0.13f, 0.045f, 0.345f, 0f, 4f),
            }, 20, 3, "ChairBack")), Vector3.zero, Vector3.zero, shell);

            BlockKit.MeshPart("HeadPillow", c, SphereMesh, new Vector3(0f, 1.36f, 0.27f), Vector3.zero, trim, scale: new Vector3(0.25f, 0.13f, 0.09f));
            BlockKit.MeshPart("LumbarPillow", c, SphereMesh, new Vector3(0f, 0.8f, 0.21f), Vector3.zero, trim, scale: new Vector3(0.33f, 0.16f, 0.11f));
            var arms = new List<CombineInstance>();
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                arms.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, 0.66f, 0.04f), Vector3.zero, new Vector3(0.035f, 0.2f, 0.05f)));
                arms.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, 0.775f, 0f), Vector3.zero, new Vector3(0.075f, 0.03f, 0.26f)));
            }
            BlockKit.Batch("Armrests", c, CabinetTrim, arms);
            // Racing stripes on the back, which faces the room.
            var stripes = new List<CombineInstance>();
            foreach (float x in new[] { -0.13f, 0.13f })
                stripes.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, 1.0f, 0.358f), new Vector3(5.5f, 0f, 0f), new Vector3(0.05f, 0.62f, 0.008f)));
            BlockKit.Batch("Stripes", c, trim, stripes);
        }

        // ---- Foosball -----------------------------------------------------------

        /// <summary>A foosball table: a wooden cabinet on splayed legs, a
        /// walled pitch with goals, eight rods with grips and two teams of
        /// little players, and score beads at each end.</summary>
        private static void BuildFoosballModel(Transform parent, Vector3 position, float yaw)
        {
            var t = Group(parent, "Foosball", position, yaw);
            const float floorY = 0.8f, wallTop = 0.93f, halfX = 0.72f, halfZ = 0.42f, rodY = 0.895f;
            Material wood = Plain(new Color(0.52f, 0.33f, 0.18f));
            Material pitch = Plain(new Color(0.10f, 0.48f, 0.22f));

            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                {
                    BlockKit.Block("Leg", t, new Vector3(x * 0.6f, 0f, z * 0.33f), new Vector3(-z * 7f, 0f, x * 7f), 0.02f, 0.62f,
                        new Vector2(0.035f, 0.035f), new Vector2(0.055f, 0.055f), 0.008f, wood);
                    BlockKit.MeshPart("Foot", t, Shared("foosFoot", () => CharacterMesh.Prism(12, 0.045f, 0.035f, 0f, 0.022f, "FoosFoot")),
                        new Vector3(x * 0.6f, 0f, z * 0.33f), Vector3.zero, CabinetTrim);
                }
            BlockKit.Block("Box", t, Vector3.zero, Vector3.zero, 0.56f, floorY,
                new Vector2(halfX - 0.05f, halfZ - 0.05f), new Vector2(halfX + 0.02f, halfZ + 0.02f), 0.015f, wood, solid: true);
            BlockKit.Batch("Apron", t, Glow(Teal, 0.9f), BlockKit.Outline(new Vector3(0f, 0.575f, 0f), 1.36f, 0.76f, 0.02f));

            var walls = new List<CombineInstance>();
            foreach (float z in new[] { -halfZ, halfZ })
                walls.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, (floorY + wallTop) * 0.5f, z), Vector3.zero,
                    new Vector3(halfX * 2f + 0.04f, wallTop - floorY, 0.04f)));
            foreach (float x in new[] { -halfX, halfX })
                walls.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, (floorY + wallTop) * 0.5f, 0f), Vector3.zero,
                    new Vector3(0.04f, wallTop - floorY, halfZ * 2f - 0.04f)));
            BlockKit.Batch("Walls", t, wood, walls);
            BlockKit.Batch("WallCap", t, CabinetTrim, BlockKit.Outline(new Vector3(0f, wallTop + 0.004f, 0f), halfX * 2f, halfZ * 2f, 0.05f));

            BlockKit.Box("Pitch", t, new Vector3(0f, floorY + 0.004f, 0f), new Vector3(halfX * 2f - 0.04f, 0.008f, halfZ * 2f - 0.04f), pitch);
            var lines = new List<CombineInstance>
            {
                BlockKit.Piece(BlockKit.CubeMesh, new Vector3(0f, floorY + 0.009f, 0f), Vector3.zero, new Vector3(0.012f, 0.002f, halfZ * 2f - 0.05f)),
                BlockKit.Piece(Shared("foosCircle", () => CharacterMesh.Annulus(24, 0.1f, 0.112f, 0f, 0.002f, "FoosCircle")),
                    new Vector3(0f, floorY + 0.009f, 0f), Vector3.zero, Vector3.one),
            };
            foreach (float x in new[] { -1f, 1f })
                lines.AddRange(BlockKit.Outline(new Vector3(x * (halfX - 0.14f), floorY + 0.0095f, 0f), 0.2f, 0.36f, 0.012f));
            BlockKit.Batch("Lines", t, WhiteLine, lines);
            foreach (float x in new[] { -1f, 1f })
                BlockKit.Box("Goal", t, new Vector3(x * (halfX - 0.022f), floorY + 0.045f, 0f), new Vector3(0.012f, 0.07f, 0.22f), DarkGlass);

            Mesh rod = Shared("foosRod", () => CharacterMesh.Prism(8, 0.009f, 0.009f, -0.5f, 0.5f, "FoosRod"));
            Mesh grip = Shared("foosGrip", () => CharacterMesh.Prism(10, 0.024f, 0.02f, -0.5f, 0.5f, "FoosGrip"));
            var rods = new List<CombineInstance>();
            var grips = new List<CombineInstance>();
            var red = new List<CombineInstance>();
            var blue = new List<CombineInstance>();
            int[] line = { 1, 2, 3, 5, 5, 3, 2, 1 };
            for (int i = 0; i < 8; i++)
            {
                float x = -0.56f + i * 0.16f;
                // Each team's handles are on its own side of the table.
                bool redRod = i == 0 || i == 1 || i == 3 || i == 5;
                float handle = redRod ? -1f : 1f;
                rods.Add(BlockKit.Piece(rod, new Vector3(x, rodY, handle * 0.04f), new Vector3(90f, 0f, 0f), new Vector3(1f, 1.2f, 1f)));
                grips.Add(BlockKit.Piece(grip, new Vector3(x, rodY, handle * 0.62f), new Vector3(90f, 0f, 0f), new Vector3(1f, 0.13f, 1f)));
                rods.Add(Ball(new Vector3(x, rodY, -handle * 0.56f), Vector3.one * 0.03f));

                var team = redRod ? red : blue;
                int players = line[i];
                for (int p = 0; p < players; p++)
                {
                    float z = players == 1 ? 0f : -0.3f + p * (0.6f / (players - 1));
                    team.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, rodY - 0.005f, z), Vector3.zero, new Vector3(0.036f, 0.06f, 0.03f)));
                    team.Add(Ball(new Vector3(x, rodY + 0.042f, z), Vector3.one * 0.032f));
                    team.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x, rodY - 0.055f, z), Vector3.zero, new Vector3(0.03f, 0.045f, 0.018f)));
                }
            }
            BlockKit.Batch("Rods", t, BrightSteel, rods);
            BlockKit.Batch("Grips", t, Plain(new Color(0.06f, 0.06f, 0.07f)), grips);
            BlockKit.Batch("RedTeam", t, Plain(new Color(0.85f, 0.15f, 0.2f)), red);
            BlockKit.Batch("BlueTeam", t, Plain(new Color(0.15f, 0.35f, 0.9f)), blue);

            // Score beads on a wire over each goal end.
            var wires = new List<CombineInstance>();
            var beadsRed = new List<CombineInstance>();
            var beadsBlue = new List<CombineInstance>();
            foreach (float x in new[] { -1f, 1f })
            {
                float wx = x * halfX;
                wires.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(wx, wallTop + 0.05f, 0f), Vector3.zero, new Vector3(0.005f, 0.005f, 0.4f)));
                foreach (float z in new[] { -0.2f, 0.2f })
                    wires.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(wx, wallTop + 0.03f, z), Vector3.zero, new Vector3(0.008f, 0.05f, 0.008f)));
                for (int b = 0; b < 5; b++)
                    (x < 0f ? beadsRed : beadsBlue).Add(Ball(new Vector3(wx, wallTop + 0.05f, -0.17f + b * 0.03f + (b > 2 ? 0.2f : 0f)),
                        Vector3.one * 0.026f));
            }
            BlockKit.Batch("ScoreWires", t, BrightSteel, wires);
            BlockKit.Batch("ScoreBeadsRed", t, Plain(new Color(0.85f, 0.15f, 0.2f)), beadsRed);
            BlockKit.Batch("ScoreBeadsBlue", t, Plain(new Color(0.15f, 0.35f, 0.9f)), beadsBlue);

            var ball = BuildKit.SpawnLocal(PrimitiveType.Sphere, "Ball", t, new Vector3(0.21f, floorY + 0.025f, -0.12f),
                Vector3.zero, Vector3.one * 0.034f, Plain(new Color(0.95f, 0.95f, 0.92f)), false);
            ball.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The badge someone left behind (the corkboard's "found" note).
            BlockKit.Box("ForgottenBadge", t, new Vector3(0.5f, wallTop + 0.02f, 0.42f), new Vector3(0.13f, 0.006f, 0.085f), Glow(Lemon, 0.8f));
        }

        /// <summary>A ping-pong paddle lying flat: a round blade and a handle.</summary>
        private static void BuildPaddle(Transform table, string name, Vector3 at, float yaw, Color rubber)
        {
            var p = Group(table, name, at, yaw);
            BlockKit.MeshPart("Blade", p, Shared("paddleBlade", () => CharacterMesh.Prism(18, 0.082f, 0.082f, 0f, 0.014f, "PaddleBlade")),
                Vector3.zero, Vector3.zero, Plain(rubber));
            BlockKit.Box("Handle", p, new Vector3(0f, 0.009f, -0.125f), new Vector3(0.036f, 0.022f, 0.11f), Plain(new Color(0.62f, 0.42f, 0.24f)));
        }

        // ---- Prizes -------------------------------------------------------------

        /// <summary>The pieces of one plush bear sitting at <paramref name="at"/>.</summary>
        private static void AddPlushBear(List<CombineInstance> into, Vector3 at)
        {
            into.Add(Ball(at + new Vector3(0f, 0.12f, 0f), new Vector3(0.25f, 0.24f, 0.21f)));        // body
            into.Add(Ball(at + new Vector3(0f, 0.29f, -0.01f), new Vector3(0.19f, 0.18f, 0.17f)));     // head
            foreach (float x in new[] { -1f, 1f })
            {
                into.Add(Ball(at + new Vector3(x * 0.075f, 0.375f, 0f), new Vector3(0.07f, 0.07f, 0.04f)));   // ears
                into.Add(Ball(at + new Vector3(x * 0.12f, 0.16f, -0.05f), new Vector3(0.08f, 0.13f, 0.08f))); // arms
                into.Add(Ball(at + new Vector3(x * 0.075f, 0.04f, -0.09f), new Vector3(0.095f, 0.08f, 0.13f))); // feet
            }
        }

        /// <summary>The lighter muzzle of the bear at <paramref name="at"/>.</summary>
        private static CombineInstance PlushMuzzle(Vector3 at) =>
            Ball(at + new Vector3(0f, 0.265f, -0.08f), new Vector3(0.085f, 0.065f, 0.05f));

        /// <summary>The eyes and nose of the bear at <paramref name="at"/>.</summary>
        private static void AddPlushFace(List<CombineInstance> into, Vector3 at)
        {
            foreach (float x in new[] { -0.042f, 0.042f })
                into.Add(Ball(at + new Vector3(x, 0.318f, -0.083f), Vector3.one * 0.024f));
            into.Add(Ball(at + new Vector3(0f, 0.278f, -0.106f), new Vector3(0.03f, 0.022f, 0.02f)));
        }
    }
}
