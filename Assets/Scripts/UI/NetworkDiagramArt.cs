using UnityEngine;

namespace Cyverse.UI
{
    /// <summary>What <see cref="NetworkDiagramArt"/> draws with. Coordinates
    /// are pixels of the original 1199 x 535 diagram, x right and y down.</summary>
    public interface IDiagramPainter
    {
        /// <summary>A filled axis-aligned rectangle.</summary>
        void Rect(float x, float y, float w, float h, Color32 c);

        /// <summary>A rectangle outline of thickness <paramref name="t"/>,
        /// centred on the edges of (x, y, w, h).</summary>
        void Frame(float x, float y, float w, float h, float t, Color32 c);

        /// <summary>A polyline of thickness <paramref name="t"/> through
        /// x0, y0, x1, y1, ... with square ends.</summary>
        void Line(float t, Color32 c, params float[] pts);

        /// <summary>A filled convex polygon through x0, y0, x1, y1, ...</summary>
        void Poly(Color32 c, params float[] pts);

        /// <summary>A filled ellipse.</summary>
        void Ellipse(float cx, float cy, float rx, float ry, Color32 c);

        /// <summary>One line of text centred on <paramref name="cx"/> with its
        /// baseline at <paramref name="baseline"/>. Weight 0 regular, 1 semibold, 2 bold.</summary>
        void Text(float cx, float baseline, string text, float size, int weight, Color32 c);
    }

    /// <summary>
    /// Dr Tonia's office network diagram (LAN, WAN and database network),
    /// drawn from code so it stays sharp at any size. It reproduces the
    /// original Lucid export pixel for pixel as closely as shapes allow:
    /// every position below is measured from that 1199 x 535 image.
    ///
    /// Keep it in this simple form (painter calls, for loops, locals): a
    /// script in the review tooling reads this file to preview and diff it.
    /// </summary>
    public static class NetworkDiagramArt
    {
        public const float Width = 1199f;
        public const float Height = 535f;

        public static readonly Color32 Paper = new Color32(255, 255, 255, 255);
        public static readonly Color32 GridMinor = new Color32(244, 244, 244, 255);
        public static readonly Color32 GridMajor = new Color32(233, 233, 233, 255);
        public static readonly Color32 Blue = new Color32(84, 167, 220, 255);
        public static readonly Color32 BlueLight = new Color32(114, 186, 226, 255);
        public static readonly Color32 BluePale = new Color32(167, 214, 237, 255);
        public static readonly Color32 BlueLine = new Color32(121, 190, 228, 255);
        public static readonly Color32 Steel = new Color32(112, 168, 202, 255);
        public static readonly Color32 White = new Color32(255, 255, 255, 255);
        public static readonly Color32 Wire = new Color32(62, 67, 76, 255);
        public static readonly Color32 WireLight = new Color32(214, 216, 219, 255);
        public static readonly Color32 CloudEdge = new Color32(120, 124, 130, 255);
        public static readonly Color32 Ink = new Color32(51, 51, 51, 255);
        public static readonly Color32 InkLabel = new Color32(60, 62, 66, 255);
        public static readonly Color32 Orange = new Color32(242, 180, 120, 255);
        public static readonly Color32 OrangeTag = new Color32(238, 153, 74, 255);
        public static readonly Color32 Teal = new Color32(94, 197, 177, 255);
        public static readonly Color32 TealTag = new Color32(87, 191, 168, 255);
        public static readonly Color32 Yellow = new Color32(245, 214, 92, 255);
        public static readonly Color32 YellowTag = new Color32(245, 208, 74, 255);
        public static readonly Color32 Gray = new Color32(128, 128, 128, 255);
        public static readonly Color32 GrayBody = new Color32(153, 153, 153, 255);
        public static readonly Color32 GrayLight = new Color32(200, 200, 200, 255);

        public const float Regular = 8.7f;
        public const float Small = 7.1f;
        public const float TagSize = 5.6f;

        public static void Paint(IDiagramPainter p)
        {
            p.Rect(0f, 0f, Width, Height, Paper);
            Grid(p);
            Zones(p);
            Devices(p);
            Wires(p);
            Labels(p);
        }

        // ---- Background grid --------------------------------------------------------

        private static void Grid(IDiagramPainter p)
        {
            for (int i = 0; i < 77; i++)
            {
                float gx = 11.5f + i * 15.77f;
                Color32 cx = i % 4 == 0 ? GridMajor : GridMinor;
                p.Rect(gx, 0f, 1f, Height, cx);
            }
            for (int j = 0; j < 34; j++)
            {
                float gy = 9.9f + j * 15.77f;
                Color32 cy = j % 4 == 2 ? GridMajor : GridMinor;
                p.Rect(0f, gy, Width, 1f, cy);
            }
        }

        // ---- The three network zones --------------------------------------------------

        private static void Zones(IDiagramPainter p)
        {
            p.Frame(30f, 67f, 358f, 439f, 2f, Orange);
            p.Rect(29f, 47f, 21f, 16f, OrangeTag);
            p.Text(39.5f, 57.4f, "LAN", TagSize, 2, Ink);

            p.Frame(435f, 63f, 376f, 130.5f, 1.6f, Teal);
            p.Rect(434f, 43f, 24f, 16f, TealTag);
            p.Text(446f, 53.4f, "WAN", TagSize, 2, Ink);

            p.Frame(858f, 49f, 311f, 264f, 2f, Yellow);
            p.Rect(857f, 29f, 66f, 16f, YellowTag);
            p.Text(890f, 39.4f, "Database Network", TagSize, 2, Ink);
        }

        // ---- Connectors -------------------------------------------------------------

        private static void Wires(IDiagramPainter p)
        {
            float t = 1.6f;
            // LAN: printers, PCs, laptops and IoT into the access switch.
            p.Line(t, Wire, 131f, 122.5f, 218.8f, 122.5f, 218.8f, 222f);
            ArrowDown(p, 218.8f, 228.5f);
            p.Line(t, Wire, 140.5f, 236.6f, 188f, 236.6f);
            ArrowRight(p, 194.5f, 236.6f);
            p.Line(t, Wire, 210.8f, 246f, 210.8f, 353.2f, 128f, 353.2f);
            p.Line(t, Wire, 227.2f, 246f, 227.2f, 450.3f, 124.5f, 450.3f);
            // Access switch to the LAN gateway.
            p.Line(t, Wire, 244.5f, 236.6f, 294f, 236.6f);
            ArrowRight(p, 300.5f, 236.6f);
            // Gateway out over the WAN link, up to the WAN router.
            p.Line(t, Wire, 326.6f, 238.8f, 326.6f, 254.6f, 456.6f, 254.6f, 456.6f, 123.2f, 465f, 123.2f);
            ArrowRight(p, 471.5f, 123.2f);
            // Remote employee down to the internet, internet to the router.
            p.Line(2f, WireLight, 380f, 65f, 380f, 100f);
            ArrowDown(p, 380f, 106f);
            p.Line(1.6f, WireLight, 411f, 125.6f, 455.8f, 125.6f);
            // WAN router to firewall, firewall to the cloud network.
            p.Line(t, Wire, 526.5f, 123.2f, 568f, 123.2f);
            ArrowRight(p, 574.5f, 123.2f);
            p.Line(t, Wire, 653f, 123.2f, 694f, 123.2f);
            ArrowRight(p, 700.5f, 123.2f);
            p.Line(1.6f, BlueLine, 700f, 124.1f, 725f, 124.1f);
            p.Line(1.6f, BlueLine, 752f, 124.1f, 778f, 124.1f);
            p.Line(t, Wire, 778f, 123.2f, 884f, 123.2f);
            ArrowRight(p, 890.5f, 123.2f);
            // Database network: firewall to primary, replication, backups.
            p.Line(t, Wire, 969f, 108.8f, 1017f, 108.8f);
            ArrowRight(p, 1023.5f, 108.8f);
            p.Line(t, Wire, 1051f, 101.6f, 1101f, 101.6f);
            ArrowRight(p, 1107.5f, 101.6f);
            p.Line(t, Wire, 1051f, 116.6f, 1080.6f, 116.6f, 1080.6f, 232.6f, 1105f, 232.6f);
            ArrowRight(p, 1111.5f, 232.6f);
        }

        private static void ArrowRight(IDiagramPainter p, float tipX, float y)
        {
            p.Poly(Wire, tipX, y, tipX - 7.2f, y - 3.1f, tipX - 7.2f, y + 3.1f);
        }

        private static void ArrowDown(IDiagramPainter p, float x, float tipY)
        {
            p.Poly(Wire, x, tipY, x + 3.1f, tipY - 7.2f, x - 3.1f, tipY - 7.2f);
        }

        // ---- Devices ----------------------------------------------------------------

        private static void Devices(IDiagramPainter p)
        {
            Printer(p);
            Computer(p);
            Laptop(p);
            Iot(p);
            Switch(p);
            Router(p, 0f, 0f);
            Router(p, 170.3f, -113.2f);
            Person(p);
            InternetCloud(p);
            Firewall(p, 574f, 95f);
            Firewall(p, 890f, 81f);
            Bolt(p);
            Server(p, 1023f);
            Server(p, 1107f);
            Backup(p);
        }

        private static void Printer(IDiagramPainter p)
        {
            p.Rect(84.3f, 98.2f, 32.4f, 21f, Blue);
            p.Rect(71.2f, 121f, 59.3f, 19f, Blue);
            p.Rect(72.6f, 119.4f, 56.5f, 2f, Blue);
            p.Rect(80.6f, 133.6f, 40f, 13.4f, White);
            p.Rect(81.6f, 134.6f, 38f, 11.6f, Blue);
            p.Rect(88.6f, 140f, 24.6f, 1.2f, BlueLine);
            for (int i = 0; i < 3; i++)
            {
                float dx = 107.2f + i * 8.3f;
                p.Ellipse(dx, 127.2f, 2.1f, 2.1f, BluePale);
                p.Ellipse(dx, 127.2f, 0.9f, 0.9f, BlueLight);
            }
        }

        private static void Computer(IDiagramPainter p)
        {
            p.Rect(115f, 203.6f, 25.6f, 60f, Blue);
            p.Rect(132f, 207f, 6f, 3.2f, BluePale);
            p.Rect(60f, 212.2f, 70.6f, 45.6f, White);
            p.Rect(61f, 213.2f, 68.6f, 43.6f, Blue);
            p.Rect(64.2f, 216.2f, 61.4f, 37.6f, White);
            p.Rect(92f, 256.8f, 6f, 3.6f, Blue);
            p.Poly(White, 63.2f, 259f, 126.4f, 259f, 131f, 271.4f, 61f, 271.4f);
            p.Poly(Blue, 64.2f, 260f, 125.4f, 260f, 129.9f, 270.5f, 62f, 270.5f);
            for (int i = 0; i < 14; i++)
            {
                p.Rect(72.2f + i * 3.15f, 261.9f, 2.1f, 1.1f, BluePale);
            }
            for (int k = 0; k < 10; k++)
            {
                p.Rect(79f + k * 3.15f, 264.4f, 2.1f, 1.1f, BluePale);
            }
            p.Rect(85f, 268.2f, 21f, 2.3f, White);
        }

        private static void Laptop(IDiagramPainter p)
        {
            p.Rect(73.4f, 330f, 54f, 34.2f, Blue);
            p.Rect(77f, 333.4f, 46.8f, 24.6f, GrayLight);
            p.Rect(78f, 334.4f, 44.8f, 22.6f, Gray);
            p.Poly(Blue, 73.4f, 364.6f, 127.4f, 364.6f, 131f, 377.8f, 70.4f, 377.8f);
            p.Poly(White, 75.6f, 366.6f, 125.2f, 366.6f, 126.2f, 370.6f, 74.6f, 370.6f);
        }

        private static void Iot(IDiagramPainter p)
        {
            p.Rect(76.8f, 436f, 3.8f, 28.6f, Blue);
            p.Rect(120.8f, 436f, 3.8f, 28.6f, Blue);
            p.Rect(80.6f, 437f, 40.2f, 26.4f, GrayBody);
            p.Rect(85.6f, 438f, 31f, 24f, GrayLight);
            p.Rect(86.6f, 439f, 14.6f, 6.2f, White);
            p.Rect(102.6f, 439f, 13.4f, 6.2f, White);
            p.Rect(86.6f, 447f, 14.6f, 6.2f, White);
            p.Rect(102.6f, 447f, 13.4f, 6.2f, White);
            p.Rect(86.6f, 455f, 14.6f, 6.4f, White);
            p.Rect(102.6f, 455f, 13.4f, 6.4f, White);
            p.Rect(92f, 446.6f, 1.6f, 7f, GrayLight);
        }

        private static void Switch(IDiagramPainter p)
        {
            p.Rect(194f, 228f, 50.6f, 15.2f, Blue);
            p.Rect(231f, 229f, 12.4f, 13.2f, BluePale);
            p.Rect(232f, 230f, 10.4f, 11.2f, BlueLight);
            p.Line(1f, BluePale, 232.6f, 231.4f, 241.4f, 239.8f);
            p.Line(1f, BluePale, 241.4f, 231.4f, 232.6f, 239.8f);
            for (int r = 0; r < 2; r++)
            {
                for (int i = 0; i < 4; i++)
                {
                    float px = 201.6f + i * 8f;
                    float py = 231.4f + r * 5f;
                    p.Poly(White, px - 2.2f, py + 0.4f, px + 2.2f, py + 0.4f, px + 2.2f, py + 1.5f, px + 1f, py + 2.8f, px - 1f, py + 2.8f, px - 2.2f, py + 1.5f);
                }
            }
            p.Rect(197.2f, 245.6f, 44f, 1.4f, Steel);
        }

        private static void Router(IDiagramPainter p, float ox, float oy)
        {
            p.Poly(Blue, ox + 299.2f, oy + 213.4f, ox + 302.2f, oy + 213.4f, ox + 307.6f, oy + 239.4f, ox + 302.6f, oy + 239.4f);
            p.Poly(Blue, ox + 350.4f, oy + 213.6f, ox + 353.6f, oy + 213.6f, ox + 350.2f, oy + 239.4f, ox + 345.2f, oy + 239.4f);
            p.Rect(ox + 296.6f, oy + 239f, 59.8f, 18f, Blue);
            p.Rect(ox + 338f, oy + 240.8f, 17.2f, 13.4f, BluePale);
            p.Rect(ox + 339f, oy + 241.8f, 15.2f, 11.4f, BlueLight);
            float cx = ox + 346.6f;
            float cy = oy + 247.5f;
            p.Poly(White, cx, cy - 4.8f, cx + 1.6f, cy - 2.8f, cx - 1.6f, cy - 2.8f);
            p.Poly(White, cx, cy + 4.8f, cx - 1.6f, cy + 2.8f, cx + 1.6f, cy + 2.8f);
            p.Poly(White, cx - 5.2f, cy, cx - 3.2f, cy - 1.6f, cx - 3.2f, cy + 1.6f);
            p.Poly(White, cx + 5.2f, cy, cx + 3.2f, cy + 1.6f, cx + 3.2f, cy - 1.6f);
            p.Rect(cx - 0.5f, cy - 3f, 1f, 6f, White);
            p.Rect(cx - 3.4f, cy - 0.5f, 6.8f, 1f, White);
            for (int i = 0; i < 4; i++)
            {
                float px = ox + 302.6f + i * 9f;
                float py = oy + 245.6f;
                p.Poly(White, px - 2.7f, py, px + 2.7f, py, px + 2.7f, py + 1.7f, px + 1.2f, py + 3.9f, px - 1.2f, py + 3.9f, px - 2.7f, py + 1.7f);
            }
            p.Rect(ox + 299.4f, oy + 259.4f, 54.4f, 1.8f, Steel);
        }

        private static void Person(IDiagramPainter p)
        {
            p.Rect(372f, 0f, 16f, 17f, Blue);
            p.Rect(372f, 16f, 7.4f, 17f, Blue);
            p.Rect(380.6f, 16f, 7.4f, 17f, Blue);
            p.Rect(365f, 0f, 5.6f, 10.8f, Blue);
            p.Rect(389.4f, 0f, 5.6f, 10.8f, Blue);
        }

        private static void InternetCloud(IDiagramPainter p)
        {
            float e = 1.2f;
            CloudLobes(p, e, CloudEdge);
            CloudLobes(p, 0f, White);
        }

        private static void CloudLobes(IDiagramPainter p, float e, Color32 c)
        {
            p.Ellipse(366.6f, 118.2f, 10.6f + e, 10.4f + e, c);
            p.Ellipse(385.4f, 116.4f, 10.6f + e, 10.6f + e, c);
            p.Ellipse(400.6f, 123.4f, 10.2f + e, 10.2f + e, c);
            p.Ellipse(358f, 129.2f, 9.6f + e, 9.2f + e, c);
            p.Ellipse(402.4f, 136.2f, 8.8f + e, 9.2f + e, c);
            p.Ellipse(369.6f, 141.2f, 11f + e, 8.4f + e, c);
            p.Ellipse(386.4f, 142.8f, 10.2f + e, 9.6f + e, c);
            p.Ellipse(399.4f, 143f, 8.6f + e, 6.2f + e, c);
            p.Ellipse(380f, 129f, 20f + e, 15f + e, c);
        }

        private static void Firewall(IDiagramPainter p, float x, float y)
        {
            for (int r = 0; r < 7; r++)
            {
                float by = y + r * 8.4f;
                if (r % 2 == 0)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        p.Rect(x + i * 20f, by, 18.6f, 6.9f, Blue);
                    }
                }
                else
                {
                    p.Rect(x, by, 9.4f, 6.9f, Blue);
                    for (int k = 0; k < 3; k++)
                    {
                        p.Rect(x + 11f + k * 20.5f, by, 18.6f, 6.9f, Blue);
                    }
                    p.Rect(x + 72.4f, by, 6.6f, 6.9f, Blue);
                }
            }
        }

        private static void Bolt(IDiagramPainter p)
        {
            p.Poly(Blue, 723f, 128.6f, 739.6f, 114.8f, 740.2f, 121.6f, 726.4f, 128.4f);
            p.Poly(Blue, 739.6f, 114.8f, 740.2f, 121.6f, 741.8f, 131.8f, 738.8f, 131.8f);
            p.Poly(Blue, 740.2f, 123.4f, 763f, 115.6f, 744f, 129f, 741.2f, 131f);
        }

        private static void Server(IDiagramPainter p, float x)
        {
            p.Rect(x, 85f, 28.2f, 48f, Blue);
            p.Ellipse(x + 21.4f, 108.4f, 1.7f, 1.7f, BluePale);
            p.Rect(x + 10f, 118f, 15.6f, 1.2f, BluePale);
            p.Rect(x + 10f, 120.8f, 15.6f, 1.2f, White);
            p.Rect(x + 10f, 123.7f, 15.6f, 1.2f, White);
        }

        private static void Backup(IDiagramPainter p)
        {
            p.Rect(1111f, 209f, 20.4f, 47.6f, Blue);
            p.Frame(1114.6f, 211.6f, 13.2f, 10.6f, 1f, BluePale);
            p.Rect(1116.4f, 213.2f, 3.8f, 3.2f, White);
            p.Rect(1120.2f, 214.4f, 4.6f, 1f, White);
            p.Rect(1116.4f, 217.6f, 3.8f, 2.8f, White);
            p.Rect(1122.2f, 219f, 2.2f, 1.6f, White);
            p.Frame(1124.6f, 239.6f, 2.6f, 12.2f, 1f, BluePale);
        }

        // ---- Text -------------------------------------------------------------------

        private static void Labels(IDiagramPainter p)
        {
            // Device names.
            p.Text(380.2f, 41.9f, "Remote", Regular, 0, Ink);
            p.Text(380.5f, 54.8f, "Employee", Regular, 0, Ink);
            p.Text(379.6f, 131.8f, "Internet", Regular, 0, Ink);
            p.Text(101.3f, 155.3f, "Printers", Regular, 0, Ink);
            p.Rect(190f, 247.8f, 58f, 8.6f, White);
            p.Text(219f, 255.2f, "Access Switch", Regular, 0, Ink);
            p.Text(327.0f, 269.7f, "LAN Gateway", Regular, 0, Ink);
            p.Text(101.1f, 279.9f, "Employee PCs", Regular, 0, Ink);
            p.Text(101.5f, 386.4f, "Employee Laptops", Regular, 0, Ink);
            p.Text(101.8f, 473.2f, "IoT Devices", Regular, 0, Ink);
            p.Text(497.0f, 156.5f, "WAN Router", Regular, 0, Ink);
            p.Text(614.5f, 161.3f, "Firewall", Regular, 0, Ink);
            p.Poly(Ink, 703.2f, 141.8f, 703.8f, 140.1f, 705.4f, 138.9f, 707.6f, 138.4f, 709.8f, 138.9f, 711.4f, 140.1f, 712f, 141.8f);
            p.Text(745.6f, 142.4f, "Cloud Network", Regular, 0, Ink);
            p.Text(929.5f, 146.5f, "Database Firewall /", Regular, 0, Ink);
            p.Text(929.7f, 159.2f, "Security Group", Regular, 0, Ink);
            p.Text(1037.5f, 141.3f, "Primary", Regular, 0, Ink);
            p.Text(1037.1f, 154.3f, "Database", Regular, 0, Ink);
            p.Text(1121.4f, 141.6f, "Read", Regular, 0, Ink);
            p.Text(1121.3f, 154.4f, "Replica", Regular, 0, Ink);
            p.Text(1120.4f, 265.5f, "Backup", Regular, 0, Ink);
            p.Text(1121.8f, 278.1f, "Storage", Regular, 0, Ink);

            // Connector labels sit on a white patch that breaks the line.
            p.Rect(156.5f, 229f, 21f, 18f, White);
            p.Rect(424.5f, 116f, 27.5f, 19f, White);
            p.Rect(658f, 114f, 36f, 19f, White);
            p.Rect(976f, 99f, 38f, 19f, White);
            Tag(p, 379.9f, 63.4f, "Remote access", 54f);
            Tag(p, 219.0f, 133.6f, "LAN traffic", 38f);
            Tag(p, 167.3f, 234.3f, "LAN", 16f);
            Tag(p, 167.0f, 244.6f, "traffic", 22f);
            Tag(p, 274.2f, 239.6f, "LAN traffic", 38f);
            Tag(p, 211.1f, 344.2f, "LAN traffic", 38f);
            Tag(p, 227.1f, 402.2f, "LAN traffic", 38f);
            Tag(p, 455.4f, 256.8f, "WAN link", 32f);
            Tag(p, 438.9f, 123.7f, "Internet", 30f);
            Tag(p, 439.2f, 133.8f, "access", 26f);
            Tag(p, 548.2f, 126.2f, "WAN link", 32f);
            Tag(p, 677.5f, 120.8f, "Encrypted", 36f);
            Tag(p, 675.8f, 131.2f, "VPN", 16f);
            Tag(p, 833.4f, 126.2f, "Private database traffic", 80f);
            Tag(p, 996.5f, 106.7f, "Database", 34f);
            Tag(p, 996.1f, 116.8f, "traffic", 22f);
            Tag(p, 1081.5f, 103.2f, "Replication", 40f);
            Tag(p, 1081.6f, 178.1f, "Backups", 30f);
        }

        private static void Tag(IDiagramPainter p, float cx, float baseline, string text, float w)
        {
            p.Rect(cx - w * 0.5f, baseline - 7f, w, 8.6f, White);
            p.Text(cx, baseline, text, Small, 2, InkLabel);
        }
    }
}
