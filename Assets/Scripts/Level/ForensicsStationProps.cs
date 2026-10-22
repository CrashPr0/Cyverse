using System.Collections.Generic;
using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Furniture and desk-top props for the three chain-of-custody stations in
    /// the Forensics Lab (Evidence Intake, Analyze Evidence, Forensic Report).
    /// Each station was a bare box; its box keeps its collider and transform
    /// (the dock and tests measure the desk top from it) with the renderer
    /// hidden, and a bevelled desk with a lit edge is built in its place.
    /// Everything here is collider-free unless noted.
    /// </summary>
    public static class ForensicsStationProps
    {
        public struct Finish
        {
            public Material body, top, recess, glow, metal, screen;
        }

        public static Finish MakeFinish(Material body, Color glow)
        {
            return new Finish
            {
                body = body,
                top = BlockKit.Mat(new Color(0.15f, 0.19f, 0.18f), 0.62f, 0.55f),
                recess = BlockKit.Mat(new Color(0.030f, 0.042f, 0.040f), 0.30f, 0.20f),
                glow = BuildKit.MakeEmissive(glow, 1.1f),
                metal = BlockKit.Mat(new Color(0.36f, 0.40f, 0.41f), 0.58f, 0.80f),
                screen = BuildKit.MakeEmissive(new Color(0.04f, 0.20f, 0.12f), 0.75f),
            };
        }

        /// <summary>Restyle a box desk in place. <paramref name="box"/> is the old
        /// cube (any parent); the dress goes under <paramref name="dressParent"/>,
        /// which must share the box's parent space.</summary>
        public static Transform DressDesk(Transform box, Transform dressParent, string prefix, Finish f,
            bool frontPanels = true)
        {
            if (box == null) return null;
            Renderer old = box.GetComponent<Renderer>();
            if (old != null) old.enabled = false;

            var dress = new GameObject(prefix + "_Dress").transform;
            dress.SetParent(dressParent, false);

            Vector3 s = box.localScale, p = box.localPosition;
            float top = p.y + s.y * 0.5f, hx = s.x * 0.5f, hz = s.z * 0.5f;
            Vector3 foot = new Vector3(p.x, p.y - s.y * 0.5f, p.z);

            // Recessed toe kick, body, and a worktop that overhangs the front.
            BlockKit.Block("Kick", dress, foot + new Vector3(0f, 0f, 0.06f), Vector3.zero, 0f, 0.09f,
                new Vector2(hx - 0.06f, hz - 0.12f), new Vector2(hx - 0.06f, hz - 0.12f), 0.02f, f.recess);
            BlockKit.Block("Body", dress, foot, Vector3.zero, 0.09f, s.y - 0.055f,
                new Vector2(hx - 0.03f, hz - 0.03f), new Vector2(hx - 0.01f, hz - 0.01f), 0.04f, f.body);
            BlockKit.Block("Worktop", dress, foot, Vector3.zero, s.y - 0.055f, s.y,
                new Vector2(hx + 0.03f, hz + 0.04f), new Vector2(hx + 0.04f, hz + 0.05f), 0.018f, f.top);

            // Lit strip under the worktop lip, and a floor wash from the kick.
            BlockKit.Box("EdgeGlow", dress, new Vector3(p.x, top - 0.075f, p.z - hz - 0.004f),
                new Vector3(s.x - 0.24f, 0.012f, 0.008f), f.glow);
            BlockKit.Box("KickGlow", dress, new Vector3(p.x, foot.y + 0.02f, p.z - hz + 0.055f),
                new Vector3(s.x - 0.3f, 0.008f, 0.01f), f.glow);

            // End caps proud of the body give the desk a frame.
            for (int side = -1; side <= 1; side += 2)
                BlockKit.Slab(side < 0 ? "EndCap_L" : "EndCap_R", dress,
                    new Vector3(p.x + side * (hx - 0.015f), foot.y + (s.y - 0.055f) * 0.5f, p.z),
                    new Vector3(0.05f, s.y - 0.055f, s.z + 0.02f), 0.012f, f.top);

            if (frontPanels)
            {
                // Three recessed doors with pulls across the analyst's side.
                float doorW = (s.x - 0.16f) / 3f;
                for (int i = -1; i <= 1; i++)
                {
                    float x = p.x + i * doorW;
                    BlockKit.Slab("Door_" + (i + 1), dress,
                        new Vector3(x, foot.y + 0.09f + (s.y - 0.27f) * 0.5f + 0.02f, p.z - hz - 0.004f),
                        new Vector3(doorW - 0.06f, s.y - 0.27f, 0.02f), 0.008f, f.recess);
                    BlockKit.Box("Pull_" + (i + 1), dress,
                        new Vector3(x, top - 0.2f, p.z - hz - 0.02f), new Vector3(doorW * 0.4f, 0.016f, 0.014f), f.metal);
                }
            }
            return dress;
        }

        /// <summary>A bezel and back stand for a floating screen quad (facing -Z
        /// in <paramref name="parent"/> space), standing on the desk top.</summary>
        public static void MonitorHousing(Transform parent, string name, Vector3 screenCenter, Vector2 screenSize,
            float deskTop, Finish f, float yaw = 0f)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = screenCenter;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float bottom = screenSize.y * 0.5f;
            BlockKit.Slab("Bezel", root, new Vector3(0f, 0f, 0.035f),
                new Vector3(screenSize.x + 0.08f, screenSize.y + 0.08f, 0.06f), 0.015f, f.body);
            BlockKit.Box("BezelGlow", root, new Vector3(0f, -bottom - 0.03f, -0.002f),
                new Vector3(0.10f, 0.008f, 0.004f), f.glow);
            float neckTop = -0.05f, neckBottom = deskTop - screenCenter.y;
            BlockKit.Box("Neck", root, new Vector3(0f, (neckTop + neckBottom) * 0.5f, 0.10f),
                new Vector3(0.07f, neckTop - neckBottom, 0.05f), f.metal);
            BlockKit.Slab("Foot", root, new Vector3(0f, neckBottom + 0.01f, 0.04f),
                new Vector3(0.36f, 0.02f, 0.24f), 0.008f, f.metal);
        }

        // ---- 01 Evidence Intake ------------------------------------------------

        /// <summary>Evidence Intake: a sealed evidence bag in a rimmed tray, the
        /// custody tablet on a stand, a barcode scanner in its dock, and a
        /// housing for the custody readout.</summary>
        public static void DressIntake(Transform station, Finish f)
        {
            if (station == null || station.Find("Intake_Dress") != null) return;
            Transform plinth = station.Find("IntakePlinth");
            // Its front face carries the evidence caption panel (Polish), so no doors.
            Transform dress = DressDesk(plinth, station, "Intake", f, frontPanels: false);
            if (dress == null) return;
            float top = plinth.localPosition.y + plinth.localScale.y * 0.5f;

            BlockKit.Remove(station, "EvidenceCase", "CustodyTablet");

            // Rimmed evidence tray.
            Vector3 tray = new Vector3(-0.62f, top, 0.0f);
            BlockKit.Slab("Tray", dress, tray + new Vector3(0f, 0.012f, 0f), new Vector3(1.0f, 0.024f, 0.62f), 0.006f, f.recess);
            BlockKit.Batch("TrayRim", dress, f.metal, BlockKit.Outline(tray + new Vector3(0f, 0.03f, 0f), 1.0f, 0.62f, 0.03f));

            // Sealed evidence bag: clear-ish pouch, red tamper tape, white label.
            Material bag = BlockKit.Mat(new Color(0.55f, 0.66f, 0.62f), 0.85f, 0.05f);
            Material tape = BuildKit.MakeEmissive(new Color(0.85f, 0.12f, 0.10f), 0.6f);
            Material paper = BlockKit.Mat(new Color(0.86f, 0.87f, 0.82f), 0.15f, 0f);
            Material ink = BlockKit.Mat(new Color(0.05f, 0.05f, 0.05f), 0.1f, 0f);
            Vector3 bagPos = tray + new Vector3(-0.05f, 0.032f, 0.02f);
            BlockKit.Block("EvidenceBag", dress, bagPos, new Vector3(0f, -8f, 0f), 0f, 0.035f,
                new Vector2(0.30f, 0.21f), new Vector2(0.28f, 0.19f), 0.012f, bag);
            BlockKit.Box("TamperTape", dress, bagPos + Quaternion.Euler(0f, -8f, 0f) * new Vector3(0f, 0.036f, 0.16f),
                new Vector3(0.58f, 0.004f, 0.045f), tape).localRotation = Quaternion.Euler(0f, -8f, 0f);
            Transform label = BlockKit.Box("BagLabel", dress,
                bagPos + Quaternion.Euler(0f, -8f, 0f) * new Vector3(0.10f, 0.037f, -0.04f),
                new Vector3(0.22f, 0.003f, 0.14f), paper);
            label.localRotation = Quaternion.Euler(0f, -8f, 0f);
            var bars = new List<CombineInstance>();
            float[] widths = { 0.006f, 0.003f, 0.008f, 0.003f, 0.005f, 0.003f, 0.009f, 0.004f, 0.003f, 0.007f };
            float x = -0.065f;
            foreach (float w in widths)
            {
                bars.Add(BlockKit.Piece(BlockKit.CubeMesh, new Vector3(x + w * 0.5f, 0f, 0f), Vector3.zero,
                    new Vector3(w, 0.002f, 0.05f)));
                x += w + 0.008f;
            }
            Transform barcode = BlockKit.Batch("Barcode", dress, ink, bars);
            barcode.localPosition = bagPos + Quaternion.Euler(0f, -8f, 0f) * new Vector3(0.10f, 0.0395f, -0.06f);
            barcode.localRotation = Quaternion.Euler(0f, -8f, 0f);

            // Custody tablet on a low stand, screen tilted up at the player.
            Vector3 tablet = new Vector3(0.62f, top, -0.18f);
            BlockKit.Block("TabletStand", dress, tablet + new Vector3(0f, 0f, 0.10f), Vector3.zero, 0f, 0.05f,
                new Vector2(0.20f, 0.12f), new Vector2(0.16f, 0.06f), 0.01f, f.metal);
            Transform slate = new GameObject("CustodyTablet").transform;
            slate.SetParent(dress, false);
            slate.localPosition = tablet + new Vector3(0f, 0.17f, 0.06f);
            slate.localRotation = Quaternion.Euler(58f, 0f, 0f);
            BlockKit.Slab("Shell", slate, Vector3.zero, new Vector3(0.82f, 0.56f, 0.03f), 0.012f, f.body);
            BlockKit.MeshPart("Screen", slate, BlockKit.QuadMesh, new Vector3(0f, 0f, -0.0165f), Vector3.zero,
                f.screen, scale: new Vector3(0.74f, 0.48f, 1f));

            // Handheld barcode scanner standing in its dock at the right end.
            Vector3 dock = new Vector3(1.12f, top, 0.30f);
            BlockKit.Block("ScannerDock", dress, dock, Vector3.zero, 0f, 0.05f,
                new Vector2(0.07f, 0.09f), new Vector2(0.06f, 0.08f), 0.01f, f.body);
            BlockKit.Block("ScannerGrip", dress, dock + new Vector3(0f, 0.04f, 0.01f), new Vector3(-12f, 0f, 0f),
                0f, 0.15f, new Vector2(0.025f, 0.03f), new Vector2(0.028f, 0.035f), 0.01f, f.recess);
            BlockKit.Block("ScannerHead", dress, dock + new Vector3(0f, 0.18f, -0.03f), new Vector3(-12f, 0f, 0f),
                0f, 0.05f, new Vector2(0.04f, 0.07f), new Vector2(0.045f, 0.075f), 0.012f, f.recess);
            BlockKit.Box("ScannerWindow", dress, dock + new Vector3(0f, 0.205f, -0.105f),
                new Vector3(0.06f, 0.03f, 0.004f), tape);

            // Housing for the live custody readout (see ChainOfCustodyStation.Build).
            Transform readout = station.Find("CustodyReadoutScreen");
            if (readout != null)
            {
                Vector3 c = station.InverseTransformPoint(readout.position);
                MonitorHousing(dress, "ReadoutHousing", c, new Vector2(1.2f, 0.9f), top, f);
            }
        }

        // ---- 02 Analyze Evidence -----------------------------------------------

        /// <summary>Analyze Evidence: the desk dress plus a forensic workstation
        /// tower (drive bays, status LEDs) at the right end.</summary>
        public static void DressAnalyze(Transform console, Finish f)
        {
            if (console == null || console.Find("Analyze_Dress") != null) return;
            Transform desk = console.Find("Desk");
            Transform dress = DressDesk(desk, console, "Analyze", f);
            if (dress == null) return;
            BlockKit.Remove(console, "DeskTrim");

            Vector3 tower = new Vector3(desk.localPosition.x + desk.localScale.x * 0.5f + 0.24f, 0f, 0.05f);
            Transform body = BlockKit.Block("Tower", dress, tower, Vector3.zero, 0f, 0.78f,
                new Vector2(0.15f, 0.30f), new Vector2(0.15f, 0.30f), 0.02f, f.body, solid: true);
            BlockKit.Slab("TowerFace", dress, tower + new Vector3(0f, 0.40f, -0.30f),
                new Vector3(0.24f, 0.70f, 0.02f), 0.006f, f.recess);
            Material bayMat = BlockKit.Mat(new Color(0.22f, 0.25f, 0.26f), 0.5f, 0.7f);
            Material ledGreen = BuildKit.MakeEmissive(new Color(0.30f, 1f, 0.55f), 1.6f);
            Material ledAmber = BuildKit.MakeEmissive(new Color(1f, 0.65f, 0.18f), 1.6f);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.66f - i * 0.075f;
                BlockKit.Box("Bay_" + i, dress, tower + new Vector3(-0.015f, y, -0.315f),
                    new Vector3(0.17f, 0.05f, 0.012f), bayMat);
                BlockKit.Box("BayLed_" + i, dress, tower + new Vector3(0.088f, y, -0.318f),
                    new Vector3(0.014f, 0.014f, 0.006f), i == 1 ? ledAmber : ledGreen);
            }
            BlockKit.Box("TowerVent", dress, tower + new Vector3(0f, 0.18f, -0.315f),
                new Vector3(0.18f, 0.16f, 0.006f), f.top);
            BlockKit.Box("TowerGlow", dress, tower + new Vector3(0f, 0.785f, -0.24f),
                new Vector3(0.22f, 0.006f, 0.06f), f.glow);
        }

        // ---- 04 Forensic Report ------------------------------------------------

        /// <summary>Forensic Report: desk dress, a stand under the report
        /// monitor, a keyboard, a stack of case folders and a report printer.</summary>
        public static void DressReport(Transform desk, Transform monitor, Transform root, Finish f)
        {
            if (desk == null || root.Find("Report_Dress") != null) return;
            Transform dress = DressDesk(desk, root, "Report", f);
            float top = desk.localPosition.y + desk.localScale.y * 0.5f;
            Vector3 c = desk.localPosition;

            if (monitor != null)
            {
                Vector3 m = monitor.localPosition;
                float bottom = m.y - monitor.localScale.y * 0.5f;
                BlockKit.Box("MonitorNeck", dress, new Vector3(m.x, (bottom + top) * 0.5f + 0.02f, m.z + 0.09f),
                    new Vector3(0.12f, bottom - top + 0.04f, 0.06f), f.metal);
                BlockKit.Slab("MonitorFoot", dress, new Vector3(m.x, top + 0.01f, m.z + 0.02f),
                    new Vector3(0.6f, 0.02f, 0.3f), 0.008f, f.metal);
            }

            BlockKit.Slab("ReportKeyboard", dress, new Vector3(c.x - 0.15f, top + 0.012f, c.z - 0.35f),
                new Vector3(0.75f, 0.024f, 0.22f), 0.008f, f.recess);
            var keys = new List<CombineInstance>();
            for (int row = 0; row < 4; row++)
            for (int col = 0; col < 14; col++)
                keys.Add(BlockKit.Piece(BlockKit.CubeMesh,
                    new Vector3(c.x - 0.15f - 0.31f + col * 0.0477f, top + 0.027f, c.z - 0.35f - 0.07f + row * 0.0467f),
                    Vector3.zero, new Vector3(0.038f, 0.008f, 0.036f)));
            BlockKit.Batch("ReportKeys", dress, f.top, keys);

            // Manila case folders, slightly fanned.
            Material manila = BlockKit.Mat(new Color(0.78f, 0.64f, 0.38f), 0.2f, 0f);
            Material redTab = BuildKit.MakeEmissive(new Color(0.85f, 0.14f, 0.10f), 0.5f);
            Vector3 stack = new Vector3(c.x - 1.05f, top, c.z - 0.25f);
            for (int i = 0; i < 4; i++)
            {
                float yaw = (i - 1.5f) * 4f;
                Transform folder = BlockKit.Slab("Folder_" + i, dress, stack + new Vector3(0f, 0.008f + i * 0.016f, 0f),
                    new Vector3(0.34f, 0.014f, 0.44f), 0.003f, manila);
                folder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
            BlockKit.Box("FolderTab", dress, stack + new Vector3(0.12f, 0.066f, -0.2f),
                new Vector3(0.06f, 0.004f, 0.05f), redTab);

            // Report printer with a page in the output tray.
            Vector3 printer = new Vector3(c.x + 1.0f, top, c.z - 0.05f);
            BlockKit.Block("Printer", dress, printer, Vector3.zero, 0f, 0.20f,
                new Vector2(0.24f, 0.20f), new Vector2(0.22f, 0.18f), 0.02f, f.body);
            BlockKit.Box("PrinterSlot", dress, printer + new Vector3(0f, 0.12f, -0.201f),
                new Vector3(0.34f, 0.02f, 0.006f), f.recess);
            Transform page = BlockKit.Box("PrinterPage", dress, printer + new Vector3(0f, 0.13f, -0.30f),
                new Vector3(0.28f, 0.003f, 0.22f), BlockKit.Mat(new Color(0.88f, 0.89f, 0.85f), 0.15f, 0f));
            page.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            BlockKit.Box("PrinterLed", dress, printer + new Vector3(0.17f, 0.17f, -0.185f),
                new Vector3(0.02f, 0.012f, 0.006f), f.glow);
        }
    }
}
