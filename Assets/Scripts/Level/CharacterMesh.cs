using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Smooth procedural meshes for characters. A body part is a loft: a stack
    /// of superellipse rings along local +Y whose size and offset are given at
    /// a few keys and interpolated with Catmull-Rom, so a dozen numbers make an
    /// organic shape (a head, a forearm, a shoe). Rings share their seam vertex,
    /// so RecalculateNormals shades the part without a visible split.
    /// </summary>
    public static class CharacterMesh
    {
        /// <summary>One cross-section: half-widths <see cref="rx"/> (X) and
        /// <see cref="rz"/> (Z) centred at (<see cref="ox"/>, <see cref="y"/>,
        /// <see cref="oz"/>). <see cref="n"/> is the superellipse exponent:
        /// 2 is an ellipse, higher is boxier (tailored fabric).</summary>
        public struct Ring
        {
            public float y, rx, rz, ox, oz, n;

            public Ring(float y, float rx, float rz, float oz = 0f, float ox = 0f, float n = 2f)
            {
                this.y = y; this.rx = rx; this.rz = rz; this.ox = ox; this.oz = oz; this.n = n;
            }
        }

        /// <summary>A closed tube through <paramref name="keys"/> (ascending y),
        /// capped at both ends. <paramref name="steps"/> rings per key span.</summary>
        public static Mesh Loft(IList<Ring> keys, int segments = 24, int steps = 4, string name = "Loft")
        {
            List<Ring> rings = Resample(keys, steps);
            int m = segments;
            var verts = new List<Vector3>(rings.Count * m + 2);
            foreach (Ring r in rings)
                for (int j = 0; j < m; j++)
                    verts.Add(RingPoint(r, j * Mathf.PI * 2f / m, 0f));

            var tris = new List<int>(rings.Count * m * 6 + m * 6);
            for (int i = 0; i < rings.Count - 1; i++)
                for (int j = 0; j < m; j++)
                {
                    int a = i * m + j, b = i * m + (j + 1) % m;
                    int c = a + m, d = b + m;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }

            Ring first = rings[0], last = rings[rings.Count - 1];
            int bottom = verts.Count;
            verts.Add(new Vector3(first.ox, first.y, first.oz));
            int top = verts.Count;
            verts.Add(new Vector3(last.ox, last.y, last.oz));
            int lastRing = (rings.Count - 1) * m;
            for (int j = 0; j < m; j++)
            {
                int jn = (j + 1) % m;
                tris.Add(bottom); tris.Add(jn); tris.Add(j);
                tris.Add(top); tris.Add(lastRing + j); tris.Add(lastRing + jn);
            }
            return Finish(name, verts, tris);
        }

        /// <summary>An open, two-sided shell (hair, a hood): each ring covers
        /// only the arc <paramref name="arcAt"/>(y) returns, in radians measured
        /// from +Z (the face) through +X. <paramref name="groove"/> ripples the
        /// surface into strands; <paramref name="thickness"/> sets the gap
        /// between the outer and inner sides.</summary>
        public static Mesh Shell(IList<Ring> keys, Func<float, Vector2> arcAt, int segments, int steps,
            float thickness, float groove = 0f, int grooveCount = 0, string name = "Shell")
        {
            List<Ring> rings = Resample(keys, steps);
            int m = segments + 1; // open arcs carry both end vertices
            var verts = new List<Vector3>(rings.Count * m * 2);
            for (int side = 0; side < 2; side++)
                foreach (Ring r in rings)
                {
                    Vector2 arc = arcAt(r.y);
                    for (int j = 0; j < m; j++)
                    {
                        float phi = Mathf.Lerp(arc.x, arc.y, j / (float)segments);
                        float ripple = grooveCount > 0 ? groove * Mathf.Sin(phi * grooveCount) : 0f;
                        verts.Add(RingPoint(r, phi, ripple - (side == 1 ? thickness : 0f)));
                    }
                }

            int inner = rings.Count * m;
            var tris = new List<int>(rings.Count * m * 12);
            for (int i = 0; i < rings.Count - 1; i++)
                for (int j = 0; j < m - 1; j++)
                {
                    int a = i * m + j, b = a + 1, c = a + m, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                    // Inner side faces inward: same quad, reversed winding.
                    tris.Add(inner + a); tris.Add(inner + c); tris.Add(inner + b);
                    tris.Add(inner + b); tris.Add(inner + c); tris.Add(inner + d);
                }
            return Finish(name, verts, tris);
        }

        /// <summary>A spherical cap of radius 0.5 around +Y, from the pole down
        /// to <paramref name="maxPolarDeg"/>; two-sided. Eyelids are caps that
        /// rotate over the eyeball.</summary>
        public static Mesh Cap(float maxPolarDeg, int segments = 18, int bands = 8, string name = "Cap")
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            verts.Add(new Vector3(0f, 0.5f, 0f));
            for (int i = 1; i <= bands; i++)
            {
                float theta = maxPolarDeg * Mathf.Deg2Rad * i / bands;
                for (int j = 0; j < segments; j++)
                {
                    float phi = j * Mathf.PI * 2f / segments;
                    verts.Add(0.5f * new Vector3(Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta),
                        Mathf.Sin(theta) * Mathf.Cos(phi)));
                }
            }
            for (int j = 0; j < segments; j++)
            {
                int jn = (j + 1) % segments;
                tris.Add(0); tris.Add(1 + j); tris.Add(1 + jn);
            }
            for (int i = 0; i < bands - 1; i++)
                for (int j = 0; j < segments; j++)
                {
                    int jn = (j + 1) % segments;
                    int a = 1 + i * segments + j, b = 1 + i * segments + jn;
                    int c = a + segments, d = b + segments;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            return TwoSided(Finish(name, verts, tris));
        }

        /// <summary>A flat arc band in the XY plane (radii in local units),
        /// two-sided — fingerprint ridges, ring icons.</summary>
        public static Mesh Arc(float innerRadius, float outerRadius, float fromDeg, float toDeg,
            int segments = 24, string name = "Arc")
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int j = 0; j <= segments; j++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, j / (float)segments) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts.Add(dir * innerRadius);
                verts.Add(dir * outerRadius);
                if (j == 0) continue;
                int i0 = (j - 1) * 2, i1 = i0 + 1, i2 = j * 2, i3 = i2 + 1;
                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }
            return TwoSided(Finish(name, verts, tris));
        }

        /// <summary>A flat-shaded block from <paramref name="y0"/> to
        /// <paramref name="y1"/> with chamfered edges: half-sizes (x, z)
        /// <paramref name="bottom"/> at its base and <paramref name="top"/> at
        /// its top (so it can taper), centred at <paramref name="oz"/> in Z.
        /// Every face keeps its own vertices, so edges stay crisp.</summary>
        public static Mesh BevelBlock(float y0, float y1, Vector2 bottom, Vector2 top, float bevel,
            float oz = 0f, string name = "Block")
        {
            float b = Mathf.Min(bevel, (y1 - y0) * 0.45f);
            var rings = new[]
            {
                Chamfered(y0, bottom.x - b, bottom.y - b, b, oz),
                Chamfered(y0 + b, bottom.x, bottom.y, b, oz),
                Chamfered(y1 - b, top.x, top.y, b, oz),
                Chamfered(y1, top.x - b, top.y - b, b, oz),
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 bb, Vector3 c, Vector3 d) // a,b on the lower ring; c,d above
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(bb); verts.Add(c); verts.Add(d);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i + 1); tris.Add(i + 3); tris.Add(i + 2);
            }
            int m = rings[0].Length;
            for (int r = 0; r < rings.Length - 1; r++)
                for (int j = 0; j < m; j++)
                {
                    int jn = (j + 1) % m;
                    Quad(rings[r][j], rings[r][jn], rings[r + 1][j], rings[r + 1][jn]);
                }
            Vector3 low = new Vector3(0f, y0, oz), high = new Vector3(0f, y1, oz);
            for (int j = 0; j < m; j++)
            {
                int jn = (j + 1) % m, i = verts.Count;
                verts.Add(low); verts.Add(rings[0][jn]); verts.Add(rings[0][j]);
                verts.Add(high); verts.Add(rings[3][j]); verts.Add(rings[3][jn]);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i + 3); tris.Add(i + 4); tris.Add(i + 5);
            }
            return Finish(name, verts, tris);
        }

        /// <summary>A rectangle with cut corners, in the same order a loft ring
        /// runs: from the front (+Z) round through +X.</summary>
        private static Vector3[] Chamfered(float y, float hx, float hz, float cut, float oz)
        {
            float c = Mathf.Min(cut, Mathf.Min(hx, hz) * 0.9f);
            return new[]
            {
                new Vector3(hx - c, y, oz + hz), new Vector3(hx, y, oz + hz - c),
                new Vector3(hx, y, oz - hz + c), new Vector3(hx - c, y, oz - hz),
                new Vector3(-hx + c, y, oz - hz), new Vector3(-hx, y, oz - hz + c),
                new Vector3(-hx, y, oz + hz - c), new Vector3(-hx + c, y, oz + hz),
            };
        }

        /// <summary>A thin panel lying on the front of a loft between
        /// <paramref name="yMin"/> and <paramref name="yMax"/>, spanning the
        /// x-range <paramref name="span"/> returns at each height (kept within
        /// <paramref name="maxSpan"/> of the loft's half-width, where its front
        /// is still facing forward), raised by <paramref name="lift"/>.</summary>
        public static Mesh ConformPatch(IList<Ring> keys, float yMin, float yMax, Func<float, Vector2> span,
            float lift, int cols, int rows, float maxSpan = 0.86f, string name = "Patch")
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int r = 0; r <= rows; r++)
            {
                float y = Mathf.Lerp(yMin, yMax, r / (float)rows);
                Vector2 xr = span(y);
                float limit = Sample(keys, y).rx * maxSpan;
                xr = new Vector2(Mathf.Clamp(xr.x, -limit, limit), Mathf.Clamp(xr.y, -limit, limit));
                for (int c = 0; c <= cols; c++)
                {
                    float x = Mathf.Lerp(xr.x, xr.y, c / (float)cols);
                    verts.Add(new Vector3(x, y, FrontZ(keys, y, x) + lift));
                }
            }
            int w = cols + 1;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int a = r * w + c, b = a + 1, d = a + w, e = d + 1;
                    tris.Add(a); tris.Add(b); tris.Add(d);
                    tris.Add(b); tris.Add(e); tris.Add(d);
                }
            return Finish(name, verts, tris);
        }

        /// <summary>Surface point of ring <paramref name="r"/> at angle
        /// <paramref name="phi"/> (0 = +Z), pushed out by <paramref name="offset"/>.</summary>
        public static Vector3 RingPoint(Ring r, float phi, float offset)
        {
            float e = 2f / Mathf.Max(0.5f, r.n);
            float s = Mathf.Sin(phi), c = Mathf.Cos(phi);
            float x = (r.rx + offset) * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e);
            float z = (r.rz + offset) * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
            return new Vector3(r.ox + x, r.y, r.oz + z);
        }

        /// <summary>Front-surface Z of a lofted part at height y and lateral x,
        /// for placing features (eyes, lapels) flush on it.</summary>
        public static float FrontZ(IList<Ring> keys, float y, float x)
        {
            Ring r = Sample(keys, y);
            float u = Mathf.Clamp01(Mathf.Abs(x - r.ox) / Mathf.Max(1e-4f, r.rx));
            float e = Mathf.Max(0.5f, r.n);
            return r.oz + r.rz * Mathf.Pow(1f - Mathf.Pow(u, e), 1f / e);
        }

        /// <summary>The interpolated ring at height y.</summary>
        public static Ring Sample(IList<Ring> keys, float y)
        {
            if (y <= keys[0].y) return keys[0];
            for (int i = 0; i < keys.Count - 1; i++)
                if (y <= keys[i + 1].y)
                {
                    float t = (y - keys[i].y) / Mathf.Max(1e-5f, keys[i + 1].y - keys[i].y);
                    return Interpolate(keys, i, t);
                }
            return keys[keys.Count - 1];
        }

        private static List<Ring> Resample(IList<Ring> keys, int steps)
        {
            var rings = new List<Ring>();
            for (int i = 0; i < keys.Count - 1; i++)
                for (int s = 0; s < steps; s++)
                    rings.Add(Interpolate(keys, i, s / (float)steps));
            rings.Add(keys[keys.Count - 1]);
            return rings;
        }

        private static Ring Interpolate(IList<Ring> k, int i, float t)
        {
            Ring p0 = k[Mathf.Max(0, i - 1)], p1 = k[i], p2 = k[i + 1], p3 = k[Mathf.Min(k.Count - 1, i + 2)];
            return new Ring(
                Mathf.Lerp(p1.y, p2.y, t),
                Mathf.Max(0f, CatmullRom(p0.rx, p1.rx, p2.rx, p3.rx, t)),
                Mathf.Max(0f, CatmullRom(p0.rz, p1.rz, p2.rz, p3.rz, t)),
                CatmullRom(p0.oz, p1.oz, p2.oz, p3.oz, t),
                CatmullRom(p0.ox, p1.ox, p2.ox, p3.ox, t),
                Mathf.Lerp(p1.n, p2.n, t));
        }

        private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        private static Mesh TwoSided(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            var verts = new List<Vector3>(v);
            verts.AddRange(v);
            var tris = new List<int>(t);
            for (int i = 0; i < t.Length; i += 3)
            {
                tris.Add(t[i] + v.Length); tris.Add(t[i + 2] + v.Length); tris.Add(t[i + 1] + v.Length);
            }
            return Finish(mesh.name, verts, tris);
        }

        private static Mesh Finish(string name, List<Vector3> verts, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
