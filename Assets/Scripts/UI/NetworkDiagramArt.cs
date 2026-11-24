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
        public const float Height = 567f;

        /// <summary>Paper added above the original export, whose top edge cut
        /// off the remote employee's head. Coordinates below stay those of the
        /// original (y = 0 at its top edge); painters shift them down by this.</summary>
        public const float Top = 32f;

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
            p.Rect(0f, -Top, Width, Height, Paper);
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
                float gx = 11.5f + i * 15.805f;
                Color32 cx = i % 4 == 0 ? GridMajor : GridMinor;
                p.Rect(gx, -Top, 1f, Height, cx);
            }
            for (int j = -2; j < 34; j++)
            {
                float gy = 10.57f + j * 15.803f;
                Color32 cy = (j + 4) % 4 == 2 ? GridMajor : GridMinor;
                p.Rect(0f, gy, Width, 1f, cy);
            }
        }

        // ---- The three network zones --------------------------------------------------

        private static void Zones(IDiagramPainter p)
        {
            p.Frame(30f, 67f, 358f, 439f, 2.4f, Orange);
            p.Rect(29.4f, 47f, 21f, 16f, OrangeTag);
            p.Text(39.7f, 57.2f, "LAN", TagSize, 2, Ink);

            p.Frame(435.2f, 63.4f, 375.5f, 130.3f, 1.9f, Teal);
            p.Rect(434.4f, 43.4f, 24f, 16.2f, TealTag);
            p.Text(446.1f, 53.4f, "WAN", TagSize, 2, Ink);

            p.Frame(858.1f, 49f, 311.4f, 264f, 1.9f, Yellow);
            p.Rect(857.4f, 29f, 66f, 16f, YellowTag);
            p.Text(890.1f, 39.2f, "Database Network", TagSize, 2, Ink);
        }

        // ---- Connectors -------------------------------------------------------------

        private static void Wires(IDiagramPainter p)
        {
            float t = 1.2f;
            // LAN: printers, PCs, laptops and IoT into the access switch.
            p.Line(t, Wire, 131f, 122.3f, 219f, 122.3f, 219f, 222f);
            ArrowDown(p, 219.1f, 227.3f);
            p.Line(t, Wire, 141.3f, 237f, 188f, 237f);
            ArrowRight(p, 193.3f, 236.9f);
            p.Line(t, Wire, 211f, 247.2f, 211f, 353.2f, 128f, 353.2f);
            p.Line(t, Wire, 227.1f, 247.2f, 227.1f, 450.1f, 125.3f, 450.1f);
            // Access switch to the LAN gateway.
            p.Line(t, Wire, 244.7f, 237f, 294f, 237f);
            ArrowRight(p, 301.3f, 236.9f);
            // Gateway out over the WAN link, up to the WAN router.
            p.Line(t, Wire, 326.8f, 239.4f, 326.8f, 254.2f, 456.8f, 254.2f, 456.8f, 123.4f, 465f, 123.4f);
            ArrowRight(p, 471.5f, 123.8f);
            // Remote employee down to the internet, internet to the router.
            p.Line(1.6f, WireLight, 379.8f, 65f, 379.8f, 100f);
            ArrowDown(p, 380f, 106.6f);
            p.Line(2f, WireLight, 410f, 126f, 454.6f, 126f);
            // WAN router to firewall, firewall to the cloud network.
            p.Line(t, Wire, 525.3f, 123.4f, 568f, 123.4f);
            ArrowRight(p, 573.3f, 123.6f);
            p.Line(t, Wire, 653.2f, 123.8f, 694f, 123.8f);
            ArrowRight(p, 699.3f, 123.6f);
            p.Line(2f, BlueLine, 700.6f, 124.1f, 724.2f, 124.1f);
            p.Line(1.6f, BlueLine, 753.2f, 124.1f, 779.4f, 124.1f);
            p.Line(t, Wire, 779f, 123.4f, 884f, 123.4f);
            ArrowRight(p, 889.3f, 123.5f);
            // Database network: firewall to primary, replication, backups.
            p.Line(t, Wire, 969.8f, 109f, 1017f, 109f);
            ArrowRight(p, 1022.3f, 109f);
            p.Line(t, Wire, 1052f, 101f, 1101f, 101f);
            ArrowRight(p, 1106.7f, 101.2f);
            p.Line(t, Wire, 1051.8f, 117f, 1081f, 117f, 1081f, 233f, 1105f, 233f);
            ArrowRight(p, 1110.3f, 233f);
        }

        private static void ArrowRight(IDiagramPainter p, float tipX, float y)
        {
            p.Poly(Wire, tipX, y, tipX - 7.6f, y - 3.1f, tipX - 7.6f, y + 3.1f);
        }

        private static void ArrowDown(IDiagramPainter p, float x, float tipY)
        {
            p.Poly(Wire, x, tipY, x + 2.7f, tipY - 7.6f, x - 2.7f, tipY - 7.6f);
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
            Router(p, 169.9f, -113.6f);
            Person(p);
            InternetCloud(p);
            Firewall(p, 574.2f, 95.2f);
            Firewall(p, 890.7f, 80.7f);
            Bolt(p);
            Server(p, 1023.2f);
            Server(p, 1107.2f);
            Backup(p);
        }

        private static void Printer(IDiagramPainter p)
        {
            p.Rect(84.3f, 98.6f, 32.6f, 20.2f, Blue);
            p.Rect(71.2f, 121f, 59.7f, 19f, Blue);
            p.Rect(73f, 119.4f, 56.1f, 2f, Blue);
            p.Rect(81.3f, 134f, 39.6f, 12.2f, White);
            p.Rect(81.8f, 134.6f, 38f, 11.8f, Blue);
            p.Rect(88.6f, 139.6f, 24.6f, 1.4f, BlueLine);
            for (int i = 0; i < 3; i++)
            {
                float dx = 107.1f + i * 8.7f;
                p.Ellipse(dx, 127.2f, 2.3f, 2.3f, BluePale);
                p.Ellipse(dx, 127.2f, 0.6f, 0.6f, BlueLight);
            }
        }

        private static void Computer(IDiagramPainter p)
        {
            p.Rect(115f, 203.4f, 25.6f, 61f, Blue);
            p.Rect(132.6f, 206.2f, 5.8f, 4f, BluePale);
            p.Rect(59.6f, 211.8f, 69.4f, 44.4f, White);
            p.Rect(61.8f, 212.8f, 68.6f, 42.8f, Blue);
            p.Rect(64.6f, 216f, 60.6f, 37.6f, White);
            p.Rect(91.9f, 256f, 6.4f, 3.6f, Blue);
            p.Poly(White, 63.2f, 259.4f, 125.6f, 259.4f, 130.6f, 270.4f, 61f, 270.4f);
            p.Poly(Blue, 64.4f, 260f, 125.4f, 260f, 130.3f, 270.5f, 61.6f, 270.5f);
            for (int i = 0; i < 14; i++)
            {
                p.Rect(72.6f + i * 2.75f, 261.9f, 2.5f, 1.1f, BluePale);
            }
            for (int k = 0; k < 10; k++)
            {
                p.Rect(78.9f + k * 2.95f, 264.4f, 2.6f, 1.1f, BluePale);
            }
            p.Rect(84.9f, 267.8f, 21.6f, 2.5f, White);
        }

        private static void Laptop(IDiagramPainter p)
        {
            p.Rect(73.6f, 330f, 53.8f, 33.8f, Blue);
            p.Rect(77.2f, 333.2f, 47.2f, 25.8f, GrayLight);
            p.Rect(78.2f, 334.7f, 45.2f, 23.4f, Gray);
            p.Poly(Blue, 72.8f, 365.2f, 128.8f, 365.2f, 130.8f, 377f, 70.8f, 377f);
            p.Poly(White, 75.2f, 366.4f, 126.2f, 366.4f, 127f, 370.8f, 74.6f, 370.8f);
        }

        private static void Iot(IDiagramPainter p)
        {
            p.Rect(77.4f, 436.8f, 3f, 27.2f, Blue);
            p.Rect(120.8f, 436.8f, 3.8f, 27.2f, Blue);
            p.Rect(81f, 436.8f, 40.2f, 27.2f, GrayBody);
            p.Rect(86.2f, 438.6f, 30.2f, 22.6f, GrayLight);
            p.Rect(85.8f, 439.4f, 14.2f, 5.8f, White);
            p.Rect(102.6f, 439f, 13.4f, 6.2f, White);
            p.Rect(85.8f, 447f, 13.8f, 5.4f, White);
            p.Rect(102.9f, 447f, 13.4f, 5.4f, White);
            p.Rect(86.2f, 454.6f, 13.6f, 6.2f, White);
            p.Rect(102.6f, 454.6f, 13.4f, 6.2f, White);
            p.Rect(92.4f, 446.6f, 2f, 7f, GrayLight);
        }

        private static void Switch(IDiagramPainter p)
        {
            p.Rect(193.8f, 228f, 50.4f, 15.2f, Blue);
            p.Rect(230.2f, 228.2f, 13.2f, 14.4f, BluePale);
            p.Rect(230.8f, 230.6f, 9.4f, 10f, BlueLight);
            p.Line(1.3f, BluePale, 231.4f, 231.3f, 240f, 240.2f);
            p.Line(1.3f, BluePale, 240f, 230.3f, 231.4f, 239.7f);
            for (int r = 0; r < 2; r++)
            {
                for (int i = 0; i < 4; i++)
                {
                    float px = 200.3f + i * 7.4f;
                    float py = 231f + r * 6.2f;
                    p.Poly(White, px - 2.4f, py + 0.2f, px + 2.4f, py + 0.2f, px + 2.4f, py + 1.7f, px + 0.8f, py + 2.6f, px - 0.8f, py + 2.6f, px - 2.4f, py + 1.7f);
                }
            }
            p.Rect(196.6f, 245f, 45.2f, 1.4f, Steel);
        }

        private static void Router(IDiagramPainter p, float ox, float oy)
        {
            p.Poly(Blue, ox + 299.4f, oy + 213.6f, ox + 301.6f, oy + 213.6f, ox + 308f, oy + 240.3f, ox + 302.8f, oy + 240.3f);
            p.Poly(Blue, ox + 351.6f, oy + 214.6f, ox + 353.6f, oy + 214.6f, ox + 350.4f, oy + 240f, ox + 345.2f, oy + 240f);
            p.Rect(ox + 296.6f, oy + 239f, 59.8f, 18.4f, Blue);
            p.Rect(ox + 338.3f, oy + 240.4f, 16.9f, 14.8f, BluePale);
            p.Rect(ox + 339.3f, oy + 241.7f, 15f, 12.4f, BlueLight);
            float cx = ox + 346.2f;
            float cy = oy + 247.7f;
            p.Poly(White, cx, cy - 6f, cx + 1.5f, cy - 3.4f, cx - 1.5f, cy - 3.4f);
            p.Poly(White, cx, cy + 6f, cx - 1.4f, cy + 3.2f, cx + 1.4f, cy + 3.2f);
            p.Poly(White, cx - 6.2f, cy, cx - 2.8f, cy - 1.5f, cx - 2.8f, cy + 1.5f);
            p.Poly(White, cx + 5.6f, cy, cx + 2.4f, cy + 1.7f, cx + 2.4f, cy - 1.7f);
            p.Rect(cx - 0.5f, cy - 3.2f, 0.8f, 6.4f, White);
            p.Rect(cx - 3.4f, cy - 0.3f, 6.8f, 1f, White);
            for (int i = 0; i < 4; i++)
            {
                float px = ox + 302.1f + i * 8.9f;
                float py = oy + 245.8f;
                p.Poly(White, px - 2.7f, py, px + 2.7f, py, px + 2.7f, py + 1.6f, px + 1f, py + 4.1f, px - 1f, py + 4.1f, px - 2.7f, py + 1.6f);
            }
            p.Rect(ox + 300f, oy + 259.2f, 53.6f, 2f, Steel);
        }

        private static void Person(IDiagramPainter p)
        {
            // The original export cut the figure off at y = 0: the head,
            // shoulders and upper arms above it are completed here.
            p.Ellipse(379.8f, -19.8f, 7f, 7f, Blue);
            p.Rect(372f, -11f, 15.6f, 27.4f, Blue);
            p.Rect(372f, 16.4f, 7f, 17f, Blue);
            p.Rect(380.3f, 16.4f, 7.4f, 17f, Blue);
            p.Rect(365f, -11f, 5.4f, 22.2f, Blue);
            p.Rect(389.4f, -11f, 5.4f, 22.4f, Blue);
        }

        private static void InternetCloud(IDiagramPainter p)
        {
            float e = 1f;
            CloudLobes(p, e, CloudEdge);
            CloudLobes(p, 0f, White);
        }

        private static void CloudLobes(IDiagramPainter p, float e, Color32 c)
        {
            p.Ellipse(367.2f, 118.2f, 11.6f + e, 10.2f + e, c);
            p.Ellipse(385.8f, 116.1f, 10.6f + e, 10.6f + e, c);
            p.Ellipse(399.6f, 122.6f, 10.2f + e, 10.2f + e, c);
            p.Ellipse(358.8f, 128.5f, 9.9f + e, 9.2f + e, c);
            p.Ellipse(401.6f, 137.7f, 8.6f + e, 10.4f + e, c);
            p.Ellipse(370f, 140.6f, 11.2f + e, 8.4f + e, c);
            p.Ellipse(386.4f, 142f, 10.8f + e, 9.6f + e, c);
            p.Ellipse(399.8f, 141.8f, 7.8f + e, 5.8f + e, c);
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
                        p.Rect(x + i * 20f, by, 18.2f, 7f, Blue);
                    }
                }
                else
                {
                    p.Rect(x, by, 8.6f, 7.1f, Blue);
                    for (int k = 0; k < 3; k++)
                    {
                        p.Rect(x + 10.6f + k * 20.5f, by, 18.6f, 6.7f, Blue);
                    }
                    p.Rect(x + 71.8f, by, 6.6f, 7.1f, Blue);
                }
            }
        }

        private static void Bolt(IDiagramPainter p)
        {
            p.Poly(Blue, 722.5f, 128f, 739.6f, 114.8f, 741f, 120.8f, 725.2f, 127f);
            p.Poly(Blue, 739.6f, 114.4f, 741f, 121.2f, 740.8f, 131.8f, 738.6f, 131.8f);
            p.Poly(Blue, 741.6f, 124.8f, 762.4f, 116f, 742.8f, 129f, 740.2f, 131f);
        }

        private static void Server(IDiagramPainter p, float x)
        {
            p.Rect(x, 85.4f, 28.2f, 47.8f, Blue);
            p.Ellipse(x + 21.4f, 108.4f, 2f, 2f, BluePale);
            p.Rect(x + 10.6f, 118f, 14.8f, 1.2f, BluePale);
            p.Rect(x + 10.4f, 121.2f, 15.6f, 1.2f, White);
            p.Rect(x + 10.4f, 123.9f, 15f, 1.2f, White);
        }

        private static void Backup(IDiagramPainter p)
        {
            p.Rect(1111.6f, 209.3f, 19.6f, 47.6f, Blue);
            p.Frame(1115f, 211.9f, 12.4f, 10.2f, 1.2f, BluePale);
            p.Rect(1117.2f, 213.6f, 4.6f, 3f, White);
            p.Rect(1121.4f, 214.8f, 4.6f, 1.2f, White);
            p.Rect(1117.2f, 217.6f, 4.6f, 3f, White);
            p.Rect(1122.9f, 218.9f, 2.6f, 1.6f, White);
            p.Frame(1125.4f, 239.8f, 2f, 12f, 1.2f, BluePale);
        }

        // ---- Text -------------------------------------------------------------------

        private static void Labels(IDiagramPainter p)
        {
            // Device names.
            p.Text(380.1f, 41.9f, "Remote", Regular, 0, Ink);
            p.Text(380f, 54.8f, "Employee", Regular, 0, Ink);
            p.Text(379.4f, 131.8f, "Internet", Regular, 0, Ink);
            p.Text(100.8f, 155.3f, "Printers", Regular, 0, Ink);
            p.Rect(190f, 247f, 57.2f, 8.6f, White);
            p.Text(219f, 255f, "Access Switch", Regular, 0, Ink);
            p.Text(326.7f, 269.7f, "LAN Gateway", Regular, 0, Ink);
            p.Text(101.1f, 279.7f, "Employee PCs", Regular, 0, Ink);
            p.Text(100.8f, 386.2f, "Employee Laptops", Regular, 0, Ink);
            p.Text(101f, 473.2f, "IoT Devices", Regular, 0, Ink);
            p.Text(496.6f, 156.5f, "WAN Router", Regular, 0, Ink);
            p.Text(613.2f, 161.5f, "Firewall", Regular, 0, Ink);
            p.Poly(Ink, 703.2f, 141.4f, 704.6f, 140.9f, 705f, 138.9f, 708.4f, 138.4f, 709.8f, 138.9f, 711f, 140.9f, 711.6f, 141.4f);
            p.Text(745.1f, 141.6f, "Cloud Network", Regular, 0, Ink);
            p.Text(929.6f, 146.5f, "Database Firewall /", Regular, 0, Ink);
            p.Text(929.6f, 159.2f, "Security Group", Regular, 0, Ink);
            p.Text(1037.4f, 141.5f, "Primary", Regular, 0, Ink);
            p.Text(1037.3f, 154.3f, "Database", Regular, 0, Ink);
            p.Text(1121.3f, 141.6f, "Read", Regular, 0, Ink);
            p.Text(1121.2f, 154.2f, "Replica", Regular, 0, Ink);
            p.Text(1121.3f, 265.5f, "Backup", Regular, 0, Ink);
            p.Text(1121.3f, 278.3f, "Storage", Regular, 0, Ink);

            // Connector labels sit on a white patch that breaks the line.
            p.Rect(156.9f, 229f, 21f, 18f, White);
            p.Rect(425.3f, 116.6f, 26.7f, 18.6f, White);
            p.Rect(658f, 113.8f, 36f, 18.6f, White);
            p.Rect(976.8f, 99f, 36.6f, 19f, White);
            Tag(p, 379.9f, 63.6f, "Remote access", 53.6f);
            Tag(p, 219.1f, 133.6f, "LAN traffic", 38f);
            Tag(p, 167.5f, 234.5f, "LAN", 16f);
            Tag(p, 167.3f, 244.8f, "traffic", 22.8f);
            Tag(p, 273.4f, 239.4f, "LAN traffic", 37.2f);
            Tag(p, 210.9f, 344f, "LAN traffic", 38f);
            Tag(p, 227.4f, 402f, "LAN traffic", 38f);
            Tag(p, 456.7f, 256.7f, "WAN link", 31.6f);
            Tag(p, 438.8f, 123.5f, "Internet", 30f);
            Tag(p, 438.8f, 133.8f, "access", 26f);
            Tag(p, 548.6f, 126.3f, "WAN link", 31.2f);
            Tag(p, 676.2f, 121.2f, "Encrypted", 36.1f);
            Tag(p, 676.2f, 131.2f, "VPN", 16f);
            Tag(p, 834.6f, 126.3f, "Private database traffic", 80.2f);
            Tag(p, 996.2f, 106.5f, "Database", 34f);
            Tag(p, 996.1f, 116.8f, "traffic", 22f);
            Tag(p, 1081.5f, 103.8f, "Replication", 38.8f);
            Tag(p, 1080.9f, 178.1f, "Backups", 30f);
        }

        private static void Tag(IDiagramPainter p, float cx, float baseline, string text, float w)
        {
            p.Rect(cx - w * 0.5f, baseline - 6.8f, w, 8.2f, White);
            p.Text(cx, baseline, text, Small, 2, InkLabel);
        }
    }
}
