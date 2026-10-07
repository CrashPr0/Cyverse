using System.Collections.Generic;
using UnityEngine;

namespace Cyverse.Level
{
    public enum PedestalStyle
    {
        /// <summary>Lathed gunmetal pedestal, flat top with an inset glow ring.</summary>
        Plain,
        /// <summary>Same body, topped by a lectern-style angled display.</summary>
        Screen,
        /// <summary>Same body, topped by a large round push-button with a "Pressed" blend shape.</summary>
        Button,
    }

    /// <summary>
    /// Code-generated pedestals in three variants (see <see cref="PedestalStyle"/>),
    /// all ~1 m tall with a ~0.6 m footprint, in the game's style: dark
    /// gunmetal body plus a thin emissive accent in a caller-supplied colour.
    ///
    /// Bodies are lathed surfaces of revolution (base plinth, slightly tapered
    /// column with an accent neck band, ogee collar, bevelled top cap) built
    /// once per variant, cached in memory and flagged DontUnloadUnusedAsset so
    /// scene loads don't destroy the shared copies. Normals are smooth around
    /// every ring and along curves, and split at hard edges; triangles are
    /// wound to face their vertex normals. Each body is ONE mesh with two
    /// submeshes (0 = gunmetal, shared material; 1 = accent, per-pedestal
    /// emissive material), so a pedestal is one draw call plus its top part.
    ///
    /// Front convention: a pedestal's front is local -Z (the Screen faces it,
    /// tilted up); turn it with the yaw argument.
    ///
    /// <code>
    /// var ped = PedestalFactory.Create(room, new Vector3(2f, 0f, 3f), 180f,
    ///                                  PedestalStyle.Button, BuildKit.AccentCyan);
    /// ped.Button.Press();                       // animate the cap
    /// var screen = PedestalFactory.Create(room, p, 0f, PedestalStyle.Screen, accent);
    /// screen.ScreenRenderer.sharedMaterial = myMat;   // or parent a DiegeticScreen quad
    /// </code>
    /// </summary>
    public static class PedestalFactory
    {
        /// <summary>Height of the flat top of Plain / Button pedestals.</summary>
        public const float SurfaceHeight = 1.0f;

        /// <summary>How far the Screen's display plane is inclined from horizontal
        /// (rising away from the viewer on local -Z).</summary>
        public const float ScreenTiltDegrees = 30f;

        /// <summary>Local height of the Screen's display centre.</summary>
        public const float ScreenCenterY = 0.98f;

        /// <summary>Display size (width, height along the slope) in metres.</summary>
        public static readonly Vector2 ScreenSize = new Vector2(0.36f, 0.28f);

        /// <summary>Emission multiplier of the accent lines.</summary>
        public const float AccentEmission = 2.2f;

        /// <summary>Resting emission multiplier of the button cap.</summary>
        public const float ButtonRestEmission = 0.9f;

        private const float CollisionRadius = 0.285f;
        private const int Around = 48;

        // Lathe radii (metres).
        private const float PlinthR = 0.300f;
        private const float CollarR = 0.282f;
        private const float TopBevelR = 0.266f;
        private const float ColumnTopR = 0.190f;
        private const float ColumnBaseR = 0.224f;
        private const float ColumnBaseY = 0.094f;
        private const float CollarHeight = 0.085f;
        private const float PlainColumnTopY = 0.840f;
        // The Screen head sits lower so the display centre lands near 1 m.
        private const float ScreenColumnTopY = 0.625f;

        // Button cap (pedestal-local metres).
        private const float CapRadius = 0.184f;
        private const float CapRestTopY = 1.036f;
        private const float CapSkirtY = 0.975f;
        private const float CapTravel = 0.015f;
        private const float CapBulge = 0.004f;

        private const int SubBody = 0;
        private const int SubAccent = 1;

        private static Mesh _plain, _screen, _buttonBody, _buttonCap, _panel;
        private static Material _bodyMaterial;

        // ------------------------------------------------------------ public

        /// <summary>
        /// Builds a pedestal and returns its root GameObject (which carries a
        /// <see cref="Pedestal"/> handle, a single solid collider and, for the
        /// Button style, a <see cref="PedestalButton"/>). Same as
        /// <see cref="Create"/> but returns the GameObject.
        /// </summary>
        public static GameObject Build(Transform parent, Vector3 localPos, float yawDegrees,
            PedestalStyle style, Color accent)
        {
            return Create(parent, localPos, yawDegrees, style, accent).gameObject;
        }

        /// <summary>
        /// Builds a pedestal at <paramref name="localPos"/> (its floor point)
        /// under <paramref name="parent"/>, turned by <paramref name="yawDegrees"/>
        /// (front = local -Z). The root holds one CapsuleCollider for the body
        /// (a capsule rather than a box so BuildKit.AddAimCollider, which reuses
        /// a root BoxCollider, can still add its own trigger to the same root);
        /// every visual part is collider-free.
        /// </summary>
        public static Pedestal Create(Transform parent, Vector3 localPos, float yawDegrees,
            PedestalStyle style, Color accent)
        {
            EnsureShared();

            var root = new GameObject("Pedestal_" + style);
            Transform rt = root.transform;
            rt.SetParent(parent, false);
            rt.localPosition = localPos;
            rt.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            var pedestal = root.AddComponent<Pedestal>();
            pedestal.Style = style;
            pedestal.Accent = accent;
            pedestal.SurfaceY = style == PedestalStyle.Screen ? ScreenCenterY : SurfaceHeight;

            Material accentMaterial = BuildKit.MakeEmissive(accent, AccentEmission);
            accentMaterial.name = "PedestalAccent";
            pedestal.AccentMaterial = accentMaterial;

            var body = new GameObject("Body");
            body.transform.SetParent(rt, false);
            body.AddComponent<MeshFilter>().sharedMesh = BodyMesh(style);
            var bodyRenderer = body.AddComponent<MeshRenderer>();
            bodyRenderer.sharedMaterials = new[] { _bodyMaterial, accentMaterial };
            pedestal.BodyRenderer = bodyRenderer;

            float height = style == PedestalStyle.Screen ? ScreenBodyHeight : SurfaceHeight;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = CollisionRadius;
            collider.height = height;
            collider.center = new Vector3(0f, height * 0.5f, 0f);

            if (style == PedestalStyle.Screen) AddScreen(pedestal, rt, accent);
            else if (style == PedestalStyle.Button) AddButton(pedestal, rt, accent);
            return pedestal;
        }

        internal static void TintScreenMaterial(Material m, Color accent)
        {
            Color dark = Color.Lerp(Color.black, accent, 0.14f);
            dark.a = 1f;
            m.color = dark;
            m.SetColor("_EmissionColor", accent * 0.45f);
        }

        internal static void TintCapMaterial(Material m, Color accent, float restEmission)
        {
            if (m == null) return;
            Color c = accent * 0.55f;
            c.a = 1f;
            m.color = c;
            m.SetColor("_EmissionColor", accent * restEmission);
        }

        // ------------------------------------------------------ top parts

        private static void AddScreen(Pedestal pedestal, Transform root, Color accent)
        {
            float tilt = ScreenTiltDegrees * Mathf.Deg2Rad;
            var normal = new Vector3(0f, Mathf.Cos(tilt), -Mathf.Sin(tilt));   // out of the display
            var upSlope = new Vector3(0f, Mathf.Sin(tilt), Mathf.Cos(tilt));

            var go = new GameObject("Screen");
            go.transform.SetParent(root, false);
            // Sits just above the aperture floor, below the bezel plane. A Quad's
            // visible face is its local -Z, so the transform looks "into" the pedestal.
            go.transform.localPosition = new Vector3(0f, ScreenCenterY, 0f) - normal * (ScreenRecess - ScreenPanelGap);
            go.transform.localRotation = Quaternion.LookRotation(-normal, upSlope);
            go.transform.localScale = new Vector3(ScreenSize.x, ScreenSize.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = _panel;
            var renderer = go.AddComponent<MeshRenderer>();

            Material material = BuildKit.MakeEmissive(accent, 0.45f);
            material.name = "PedestalScreen";
            material.SetFloat("_Glossiness", 0.85f);
            TintScreenMaterial(material, accent);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            pedestal.ScreenTransform = go.transform;
            pedestal.ScreenRenderer = renderer;
            pedestal.ScreenSize = ScreenSize;
            pedestal.DefaultScreenMaterial = material;
        }

        private static void AddButton(Pedestal pedestal, Transform root, Color accent)
        {
            var go = new GameObject("ButtonCap");
            go.transform.SetParent(root, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = _buttonCap;
            Bounds bounds = _buttonCap.bounds;
            bounds.Expand(0.02f);
            smr.localBounds = bounds;
            smr.updateWhenOffscreen = false;

            Material material = BuildKit.MakeEmissive(accent, ButtonRestEmission);
            material.name = "PedestalButtonCap";
            material.SetFloat("_Glossiness", 0.7f);
            TintCapMaterial(material, accent, ButtonRestEmission);
            smr.sharedMaterial = material;

            var button = root.gameObject.AddComponent<PedestalButton>();
            button.restEmission = ButtonRestEmission;
            button.Configure(smr, accent);

            pedestal.Button = button;
            pedestal.ButtonCap = smr;
        }

        // -------------------------------------------------- shared assets

        private static void EnsureShared()
        {
            // Unity's null check also catches assets destroyed by an unload.
            if (_bodyMaterial == null)
            {
                _bodyMaterial = Keep(BuildKit.MakeStandard(new Color(0.12f, 0.13f, 0.165f), 0.55f, 0.55f), "PedestalGunmetal");
            }
            if (_panel == null) _panel = Keep(BuildPanelMesh(), "PedestalScreenPanel");
        }

        private static Mesh BodyMesh(PedestalStyle style)
        {
            switch (style)
            {
                case PedestalStyle.Screen:
                    if (_screen == null) _screen = Keep(BuildBody(PedestalStyle.Screen, "PedestalScreenBody"), "PedestalScreenBody");
                    return _screen;
                case PedestalStyle.Button:
                    if (_buttonBody == null) _buttonBody = Keep(BuildBody(PedestalStyle.Button, "PedestalButtonBody"), "PedestalButtonBody");
                    if (_buttonCap == null) _buttonCap = Keep(BuildButtonCap(), "PedestalButtonCap");
                    return _buttonBody;
                default:
                    if (_plain == null) _plain = Keep(BuildBody(PedestalStyle.Plain, "PedestalPlainBody"), "PedestalPlainBody");
                    return _plain;
            }
        }

        private static T Keep<T>(T asset, string name) where T : Object
        {
            asset.name = name;
            asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return asset;
        }

        // --------------------------------------------------- body (lathe)

        private struct ProfilePoint
        {
            public float R, Y;
            /// <summary>Submesh of the profile segment that LEAVES this point.</summary>
            public int Sub;
            /// <summary>Average the normals of the two adjacent segments (curved
            /// surface); otherwise the ring is split into a hard edge.</summary>
            public bool Smooth;

            public ProfilePoint(float r, float y, int sub = SubBody, bool smooth = false)
            {
                R = r; Y = y; Sub = sub; Smooth = smooth;
            }
        }

        private static float ColumnRadius(float y, float columnTopY) =>
            Mathf.Lerp(ColumnBaseR, ColumnTopR, (y - ColumnBaseY) / (columnTopY - ColumnBaseY));

        /// <summary>Highest point of the Screen head: the back edge of the display plane.</summary>
        private static float ScreenBodyHeight =>
            ScreenCenterY + CollarR * Mathf.Tan(ScreenTiltDegrees * Mathf.Deg2Rad);

        /// <summary>Plinth, tapered column with its accent neck band, and the
        /// ogee collar. Returns the y where the collar reaches its full radius
        /// (the profile ends there).</summary>
        private static float AddLowerProfile(List<ProfilePoint> p, float columnTopY)
        {
            // Base plinth: a short drum, a chamfer, and a ledge that steps in to the column.
            p.Add(new ProfilePoint(0.296f, 0.000f));
            p.Add(new ProfilePoint(PlinthR, 0.004f));
            p.Add(new ProfilePoint(PlinthR, 0.052f));
            p.Add(new ProfilePoint(0.284f, 0.072f));
            p.Add(new ProfilePoint(0.236f, 0.080f));
            p.Add(new ProfilePoint(ColumnBaseR, ColumnBaseY));

            // Column (slight taper) with a thin raised, chamfered accent band below the collar.
            float bandMid = columnTopY - 0.035f;
            float y1 = bandMid - 0.013f, y2 = bandMid + 0.013f;
            p.Add(new ProfilePoint(ColumnRadius(y1, columnTopY), y1, SubAccent));
            p.Add(new ProfilePoint(ColumnRadius(y1, columnTopY) + 0.007f, y1 + 0.005f, SubAccent));
            p.Add(new ProfilePoint(ColumnRadius(y2, columnTopY) + 0.007f, y2 - 0.005f, SubAccent));
            p.Add(new ProfilePoint(ColumnRadius(y2, columnTopY), y2));

            // Ogee collar: a cove flaring into a convex lip (smooth along the curve).
            var p0 = new Vector2(ColumnTopR, columnTopY);
            var p1 = new Vector2(ColumnTopR, columnTopY + 0.045f);
            var p2 = new Vector2(CollarR, columnTopY + 0.040f);
            var p3 = new Vector2(CollarR, columnTopY + CollarHeight);
            const int steps = 10;
            p.Add(new ProfilePoint(p0.x, p0.y));
            for (int i = 1; i < steps; i++)
            {
                Vector2 q = Bezier(p0, p1, p2, p3, (float)i / steps);
                p.Add(new ProfilePoint(q.x, q.y, SubBody, smooth: true));
            }
            p.Add(new ProfilePoint(p3.x, p3.y));
            return p3.y;
        }

        private static Mesh BuildBody(PedestalStyle style, string name) => BodyGeo(style).ToMesh(name);

        private static Geo BodyGeo(PedestalStyle style)
        {
            var g = new Geo(2);
            var profile = new List<ProfilePoint>();

            if (style == PedestalStyle.Screen)
            {
                float wallBase = AddLowerProfile(profile, ScreenColumnTopY);
                float[] angles = ScreenAngles();
                Lathe(g, profile, angles);
                AddScreenHead(g, angles, wallBase);
                return g;
            }

            AddLowerProfile(profile, PlainColumnTopY);

            // Rim wall, then a 45 degree bevel onto the flat top.
            profile.Add(new ProfilePoint(CollarR, 0.984f));
            profile.Add(new ProfilePoint(TopBevelR, SurfaceHeight));
            if (style == PedestalStyle.Plain)
            {
                // Flat top (objects sit on it) with a shallow inset accent ring.
                profile.Add(new ProfilePoint(0.236f, SurfaceHeight));
                profile.Add(new ProfilePoint(0.236f, 0.997f, SubAccent));
                profile.Add(new ProfilePoint(0.218f, 0.997f));
                profile.Add(new ProfilePoint(0.218f, SurfaceHeight));
                profile.Add(new ProfilePoint(0f, SurfaceHeight));
            }
            else
            {
                // Flat bezel ring, a chamfered lip, then a well for the button cap
                // whose floor glows through the gap around the cap.
                profile.Add(new ProfilePoint(0.208f, SurfaceHeight));
                profile.Add(new ProfilePoint(0.194f, 0.986f));
                profile.Add(new ProfilePoint(0.194f, 0.950f, SubAccent));
                profile.Add(new ProfilePoint(0f, 0.950f));
            }
            Lathe(g, profile, UniformAngles(Around));
            return g;
        }

        /// <summary>
        /// Revolves a (radius, height) profile, bottom to top then in over the
        /// top, around the Y axis. Segment normals come from the profile, so
        /// they point outward when the profile is traversed that way; smooth
        /// points get the average of their two segments, others are split.
        /// </summary>
        private static void Lathe(Geo g, List<ProfilePoint> pts, float[] angles)
        {
            int n = pts.Count;
            var segmentNormal = new Vector2[n - 1];
            var along = new float[n];
            for (int i = 0; i < n - 1; i++)
            {
                var d = new Vector2(pts[i + 1].R - pts[i].R, pts[i + 1].Y - pts[i].Y);
                segmentNormal[i] = new Vector2(d.y, -d.x).normalized;   // outward in (r, y)
                along[i + 1] = along[i] + d.magnitude;
            }

            var rings = new List<(int point, Vector2 normal)>();
            for (int i = 0; i < n; i++)
            {
                if (i == 0) rings.Add((i, segmentNormal[0]));
                else if (i == n - 1) rings.Add((i, segmentNormal[n - 2]));
                else if (pts[i].Smooth) rings.Add((i, (segmentNormal[i - 1] + segmentNormal[i]).normalized));
                else
                {
                    rings.Add((i, segmentNormal[i - 1]));
                    rings.Add((i, segmentNormal[i]));
                }
            }

            int m = angles.Length;
            var ringStart = new int[rings.Count];
            for (int k = 0; k < rings.Count; k++)
            {
                ringStart[k] = g.Count;
                ProfilePoint p = pts[rings[k].point];
                Vector2 rn = rings[k].normal;
                for (int j = 0; j < m; j++)
                {
                    float a = angles[j];
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 normal = p.R < 1e-5f
                        ? new Vector3(0f, Mathf.Sign(rn.y), 0f)          // on the axis
                        : (dir * rn.x + Vector3.up * rn.y).normalized;
                    g.Add(dir * p.R + Vector3.up * p.Y, normal,
                          new Vector2(a / (Mathf.PI * 2f), along[rings[k].point]));
                }
            }

            for (int k = 0; k < rings.Count - 1; k++)
            {
                if (rings[k].point == rings[k + 1].point) continue;   // two halves of a split corner
                int sub = pts[rings[k].point].Sub;
                for (int j = 0; j < m - 1; j++)
                {
                    g.Quad(sub, ringStart[k] + j, ringStart[k] + j + 1,
                           ringStart[k + 1] + j + 1, ringStart[k + 1] + j);
                }
            }
        }

        // ------------------------------------------------ screen head

        private const float ScreenRecess = 0.005f;      // depth of the display aperture
        private const float ScreenPanelGap = 0.002f;    // panel sits this far above the recess floor
        private const float ScreenFrameOuter = 0.020f;  // accent frame: outer / inner offset from the aperture
        private const float ScreenFrameInner = 0.012f;
        private const float EdgeDrop = 0.012f;          // bevel: drop down the wall ...
        private const float EdgeInset = 0.012f;         // ... and in along the display plane

        /// <summary>Lathe angles for the Screen head: uniform, plus the four
        /// angles at which the accent frame's corners fall, so those corners are
        /// real vertices of every ring.</summary>
        private static float[] ScreenAngles()
        {
            float tilt = ScreenTiltDegrees * Mathf.Deg2Rad;
            float hx = ScreenSize.x * 0.5f + ScreenFrameOuter;
            float hy = ScreenSize.y * 0.5f + ScreenFrameOuter;
            float corner = Mathf.Atan2(hy * Mathf.Cos(tilt), hx);
            return MakeAngles(Around, corner, Mathf.PI - corner, Mathf.PI + corner, 2f * Mathf.PI - corner);
        }

        /// <summary>
        /// A drum of the collar's radius cut by the inclined display plane:
        /// vertical wall, bevel, a flat bezel annulus, a thin accent frame and
        /// a recessed rectangular aperture (floor + four walls) for the screen
        /// panel. Everything on the plane shares one flat normal.
        /// </summary>
        private static void AddScreenHead(Geo g, float[] angles, float wallBase)
        {
            float tilt = ScreenTiltDegrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(tilt), s = Mathf.Sin(tilt);
            var normal = new Vector3(0f, c, -s);
            var center = new Vector3(0f, ScreenCenterY, 0f);
            int m = angles.Length;

            // On the display plane, (x, v): x across, v up the slope.
            Vector3 OnPlane(float x, float v, float offset) =>
                center + new Vector3(x, v * s, v * c) + normal * offset;

            // Wall + bevel rings.
            var wallLow = new int[m];
            var wallHigh = new int[m];
            var bevelLow = new int[m];
            var bevelHigh = new int[m];
            var plane = new int[m];
            var edgeXV = new Vector2[m];
            for (int j = 0; j < m; j++)
            {
                float a = angles[j];
                float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                var radial = new Vector3(cx, 0f, cz);
                var xv = new Vector2(CollarR * cx, CollarR * cz / c);       // where the wall meets the plane
                edgeXV[j] = xv;
                Vector3 edge = OnPlane(xv.x, xv.y, 0f);
                Vector3 lowEdge = edge - Vector3.up * EdgeDrop;
                float k = 1f - EdgeInset / xv.magnitude;
                Vector3 inner = OnPlane(xv.x * k, xv.y * k, 0f);
                float u = a / (Mathf.PI * 2f);

                wallLow[j] = g.Add(new Vector3(lowEdge.x, wallBase, lowEdge.z), radial, new Vector2(u, 0f));
                wallHigh[j] = g.Add(lowEdge, radial, new Vector2(u, 1f));

                // Bevel normal from the ruled surface between the two rings.
                Vector3 across = inner - lowEdge;
                Vector3 around = BevelRing(a + 0.002f, c, s, center) - BevelRing(a - 0.002f, c, s, center);
                Vector3 bn = Vector3.Cross(across, around).normalized;
                if (Vector3.Dot(bn, radial + Vector3.up) < 0f) bn = -bn;
                bevelLow[j] = g.Add(lowEdge, bn, new Vector2(u, 2f));
                bevelHigh[j] = g.Add(inner, bn, new Vector2(u, 3f));
                plane[j] = g.Add(inner, normal, new Vector2(inner.x, inner.z));
            }
            for (int j = 0; j < m - 1; j++)
            {
                g.Quad(SubBody, wallLow[j], wallLow[j + 1], wallHigh[j + 1], wallHigh[j]);
                g.Quad(SubBody, bevelLow[j], bevelLow[j + 1], bevelHigh[j + 1], bevelHigh[j]);
            }

            // Bezel annulus: the bevel's inner ring out to the accent frame's outer
            // rectangle, found by casting each ring angle's ray onto the rectangle.
            float ohx = ScreenSize.x * 0.5f + ScreenFrameOuter;
            float ohy = ScreenSize.y * 0.5f + ScreenFrameOuter;
            var outerPts = new Vector2[m];
            var outerRing = new int[m];
            for (int j = 0; j < m; j++)
            {
                float a = angles[j];
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) / c);       // ray in plane coordinates
                float tx = Mathf.Abs(dir.x) > 1e-6f ? ohx / Mathf.Abs(dir.x) : float.MaxValue;
                float ty = Mathf.Abs(dir.y) > 1e-6f ? ohy / Mathf.Abs(dir.y) : float.MaxValue;
                outerPts[j] = dir * Mathf.Min(tx, ty);
                outerRing[j] = g.Add(OnPlane(outerPts[j].x, outerPts[j].y, 0f), normal, outerPts[j]);
            }
            for (int j = 0; j < m - 1; j++)
            {
                g.Quad(SubBody, plane[j], plane[j + 1], outerRing[j + 1], outerRing[j]);
            }

            // The inner rectangles reuse the outer one's vertex subdivision (each
            // point slid inward), so no ring has a T-junction against the next.
            // Frame (accent) between the outer and middle loops, then a thin body
            // margin down to the aperture loop.
            Vector2 Inset(Vector2 pt, float d)
            {
                if (Mathf.Abs(Mathf.Abs(pt.x) - ohx) < 1e-4f) pt.x -= Mathf.Sign(pt.x) * d;
                if (Mathf.Abs(Mathf.Abs(pt.y) - ohy) < 1e-4f) pt.y -= Mathf.Sign(pt.y) * d;
                return pt;
            }
            var frameRing = new int[m];
            var apertureRing = new int[m];
            var aperturePts = new Vector2[m];
            for (int j = 0; j < m; j++)
            {
                Vector2 fp = Inset(outerPts[j], ScreenFrameOuter - ScreenFrameInner);
                frameRing[j] = g.Add(OnPlane(fp.x, fp.y, 0f), normal, fp);
                aperturePts[j] = Inset(outerPts[j], ScreenFrameOuter);
                apertureRing[j] = g.Add(OnPlane(aperturePts[j].x, aperturePts[j].y, 0f), normal, aperturePts[j]);
            }
            for (int j = 0; j < m - 1; j++)
            {
                g.Quad(SubAccent, outerRing[j], outerRing[j + 1], frameRing[j + 1], frameRing[j]);
                g.Quad(SubBody, frameRing[j], frameRing[j + 1], apertureRing[j + 1], apertureRing[j]);
            }

            // Aperture: walls and a floor below the panel. Each wall quad has its own
            // vertices so it keeps a flat, inward-facing normal.
            float ahx = ScreenSize.x * 0.5f;
            var up = new Vector3(0f, s, c);
            for (int j = 0; j < m - 1; j++)
            {
                Vector2 p0 = aperturePts[j], p1 = aperturePts[j + 1];
                Vector2 mid = (p0 + p1) * 0.5f;
                Vector3 inward = Mathf.Abs(Mathf.Abs(mid.x) - ahx) < 1e-3f
                    ? Vector3.right * -Mathf.Sign(mid.x)      // left / right side
                    : up * -Mathf.Sign(mid.y);                // lower / upper side
                int a0 = g.Add(OnPlane(p0.x, p0.y, 0f), inward, p0);
                int a1 = g.Add(OnPlane(p1.x, p1.y, 0f), inward, p1);
                int b1 = g.Add(OnPlane(p1.x, p1.y, -ScreenRecess), inward, p1);
                int b0 = g.Add(OnPlane(p0.x, p0.y, -ScreenRecess), inward, p0);
                g.Quad(SubBody, a0, a1, b1, b0);
            }
            float ahy = ScreenSize.y * 0.5f;
            var floor = new Vector2[] { new Vector2(-ahx, -ahy), new Vector2(ahx, -ahy), new Vector2(ahx, ahy), new Vector2(-ahx, ahy) };
            var floorIds = new int[4];
            for (int k = 0; k < 4; k++) floorIds[k] = g.Add(OnPlane(floor[k].x, floor[k].y, -ScreenRecess), normal, floor[k]);
            g.Quad(SubBody, floorIds[0], floorIds[1], floorIds[2], floorIds[3]);
        }

        /// <summary>Point on the bevel's inner ring at lathe angle <paramref name="a"/>
        /// (used only to take a tangent for the bevel normal).</summary>
        private static Vector3 BevelRing(float a, float c, float s, Vector3 center)
        {
            float x = CollarR * Mathf.Cos(a);
            float v = CollarR * Mathf.Sin(a) / c;
            float k = 1f - EdgeInset / new Vector2(x, v).magnitude;
            return center + new Vector3(x * k, v * k * s, v * k * c);
        }

        // ---------------------------------------------------- button cap

        private static float Smooth01(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static float CapDomeY(float r)
        {
            float t = r / (CapRadius - 0.022f);
            return CapRestTopY - 0.0045f * t * t;
        }

        /// <summary>The cap's profile (skirt bottom up the wall, round the
        /// fillet, over the dome to the axis). With <paramref name="pressed"/>
        /// every point is pushed down <see cref="CapTravel"/>, and the visible
        /// rim bulges out <see cref="CapBulge"/>, fading to nothing at the
        /// skirt and the dome's centre.</summary>
        private static List<ProfilePoint> CapProfile(bool pressed)
        {
            const float fillet = 0.022f;
            float domeEdge = CapRadius - fillet;
            float filletCenterY = CapDomeY(domeEdge) - fillet;

            var raw = new List<(float r, float y, bool smooth)>();
            raw.Add((CapRadius, CapSkirtY, false));
            for (int k = 0; k <= 6; k++)
            {
                float phi = (90f - k * 15f) * Mathf.Deg2Rad;
                raw.Add((domeEdge + fillet * Mathf.Sin(phi), filletCenterY + fillet * Mathf.Cos(phi), true));
            }
            foreach (float r in new[] { 0.12f, 0.08f, 0.04f, 0f })
                raw.Add((r, CapDomeY(r), true));

            var list = new List<ProfilePoint>(raw.Count);
            foreach (var point in raw)
            {
                float r = point.r, y = point.y;
                if (pressed)
                {
                    float bulge = CapBulge * Smooth01(0.55f, 0.95f, point.r / CapRadius)
                                           * Smooth01(CapSkirtY + 0.003f, SurfaceHeight, point.y);
                    r += bulge;
                    y -= CapTravel;
                }
                list.Add(new ProfilePoint(r, y, SubBody, point.smooth));
            }
            return list;
        }

        /// <summary>
        /// The button cap: a rubbery domed mesh with ONE blend shape,
        /// "Pressed" (weight 0..100). Rest and pressed profiles are lathed with
        /// identical topology, so the shape's vertex AND normal deltas are exact
        /// differences. No bones: the SkinnedMeshRenderer exists purely to
        /// evaluate the blend shape.
        /// </summary>
        private static Mesh BuildButtonCap()
        {
            CapGeos(out Geo rest, out Geo pressed);
            Mesh mesh = rest.ToMesh("PedestalButtonCap");
            int count = rest.Count;
            var dv = new Vector3[count];
            var dn = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                dv[i] = pressed.V[i] - rest.V[i];
                dn[i] = pressed.N[i] - rest.N[i];
            }
            mesh.AddBlendShapeFrame(PedestalButton.BlendShapeName, 100f, dv, dn, null);
            return mesh;
        }

        private static void CapGeos(out Geo rest, out Geo pressed)
        {
            float[] angles = UniformAngles(32);
            rest = new Geo(1);
            pressed = new Geo(1);
            Lathe(rest, CapProfile(false), angles);
            Lathe(pressed, CapProfile(true), angles);
        }

        /// <summary>Unit quad exactly like Unity's built-in Quad: visible from -Z,
        /// UV (0,0) at its bottom-left as seen from the front.</summary>
        private static Mesh BuildPanelMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------- helpers

        private static float[] UniformAngles(int segments) => MakeAngles(segments);

        /// <summary>Ring angles 0..2pi (last = 2pi, a seam duplicate): uniform
        /// steps, plus the given extra angles (uniform ones too close to an extra
        /// are dropped so no sliver quads appear).</summary>
        private static float[] MakeAngles(int segments, params float[] extra)
        {
            const float tau = Mathf.PI * 2f;
            var list = new List<float>();
            for (int i = 0; i < segments; i++) list.Add(i * tau / segments);
            foreach (float e in extra)
            {
                float a = Mathf.Repeat(e, tau);
                list.RemoveAll(x => x > 0f && Mathf.Abs(x - a) < 0.05f);
                list.Add(a);
            }
            list.Sort();
            list.Add(tau);
            return list.ToArray();
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        /// <summary>Vertex / triangle lists with per-submesh, outward-winding triangle helpers.</summary>
        private sealed class Geo
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            private readonly List<int>[] _triangles;

            public Geo(int submeshes)
            {
                _triangles = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++) _triangles[i] = new List<int>();
            }

            public int Count => V.Count;

            public int Add(Vector3 position, Vector3 normal, Vector2 uv)
            {
                V.Add(position);
                N.Add(normal);
                UV.Add(uv);
                return V.Count - 1;
            }

            /// <summary>Triangle wound (Unity: clockwise = front) so it faces along
            /// its vertices' normals; zero-area triangles (lathe poles) are skipped.</summary>
            public void Tri(int sub, int a, int b, int c)
            {
                Vector3 face = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                if (face.sqrMagnitude < 1e-14f) return;
                if (Vector3.Dot(face, N[a] + N[b] + N[c]) < 0f)
                {
                    int t = b; b = c; c = t;
                }
                _triangles[sub].Add(a);
                _triangles[sub].Add(b);
                _triangles[sub].Add(c);
            }

            public void Quad(int sub, int a, int b, int c, int d)
            {
                Tri(sub, a, b, c);
                Tri(sub, a, c, d);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(V);
                mesh.SetNormals(N);
                mesh.SetUVs(0, UV);
                mesh.subMeshCount = _triangles.Length;
                for (int i = 0; i < _triangles.Length; i++) mesh.SetTriangles(_triangles[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
