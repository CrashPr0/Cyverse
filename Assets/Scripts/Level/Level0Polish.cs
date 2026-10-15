using System.Collections.Generic;
using UnityEngine;
using Cyverse.Interaction;
using static Cyverse.Level.BlockKit;

namespace Cyverse.Level
{
    /// <summary>
    /// Brings the Orientation room (Level 0) into the blocky, chamfered art
    /// direction of the Hub and Level 1 at load time, without touching the
    /// saved scene file: a quieter floor grid, consoles with framed screens for
    /// the three review stations, a scanner booth, a stand for the Security+
    /// kiosk, a robot guard, the crystal centrepiece and a dressed exit door.
    /// Old primitives are hidden with their colliders left in place, so the
    /// stations, scanner and kiosk play exactly as before. Also moves the
    /// Security+ kiosk out from behind the centrepiece. (The RECEPTION label,
    /// saved at the room's origin here and in Level 2, is fixed for every
    /// level by LevelCohesion.)
    /// </summary>
    public class Level0Polish : MonoBehaviour
    {
        private static readonly Color Cyan = BuildKit.AccentCyan;
        private static readonly Color Body = new Color(0.075f, 0.085f, 0.115f);
        private static readonly Color Trim = new Color(0.17f, 0.19f, 0.25f);
        private static readonly Color Recess = new Color(0.035f, 0.04f, 0.06f);

        /// <summary>Where the Security+ kiosk stands: mirroring the scanner on
        /// the other side of the centrepiece, instead of directly behind it.</summary>
        public static readonly Vector3 SecurityPlusPosition = new Vector3(-8f, 0f, 15f);

        public static void Apply(GameObject host)
        {
            if (host.GetComponent<Level0Polish>() != null) return;
            host.AddComponent<Level0Polish>().Run();
        }

        private void Run()
        {
            // Same quiet grid the other levels use, in the orientation's cyan.
            BuildKit.ToneGridFloor(new Color(0.035f, 0.045f, 0.07f), new Color(0.14f, 0.38f, 0.58f));

            foreach (StationSetup station in FindObjectsOfType<StationSetup>()) Station(station.gameObject);
            Scanner();
            SecurityPlus();
            HubPolish.Concierge(new Color(0.95f, 0.75f, 0.25f), "Security Guard", "Sign_SECURITY");
            HubPolish.Core(Cyan, "Sign_CYVERSE");
            LiftCoreSign();
            foreach (HubDoor door in FindObjectsOfType<HubDoor>()) HubPolish.Door(door);
            HubPolish.AmbientDrones();
        }

        // ---- Review stations ----------------------------------------------------

        /// <summary>A console with a framed screen around the station's
        /// hologram (which keeps its hover glow), and an octagonal floor pad
        /// in place of the spinning disc.</summary>
        private static void Station(GameObject station)
        {
            Transform st = station.transform;
            if (st.Find("StationDress") != null) return;
            Color accent = AccentOf(st);
            var dress = new GameObject("StationDress").transform;
            dress.SetParent(st, false);
            Material body = Mat(Body, 0.5f, 0.4f);
            Material trim = Mat(Trim, 0.6f, 0.6f);
            Material glow = BuildKit.MakeEmissive(accent, 1.3f);

            HideRenderer(st.Find("Desk"));
            HideRenderer(st.Find("StationRing"));

            Block("ConsoleBase", dress, Vector3.zero, Vector3.zero, 0f, 0.1f, new Vector2(0.78f, 0.5f), new Vector2(0.76f, 0.48f), 0.03f, trim);
            Block("Console", dress, Vector3.zero, Vector3.zero, 0.1f, 1.02f, new Vector2(0.72f, 0.44f), new Vector2(0.78f, 0.48f), 0.05f, body);
            Block("ConsoleTop", dress, Vector3.zero, Vector3.zero, 1.02f, 1.1f, new Vector2(0.82f, 0.52f), new Vector2(0.82f, 0.52f), 0.025f, trim);
            Box("ConsoleAccent", dress, new Vector3(0f, 0.92f, -0.485f), new Vector3(1.4f, 0.025f, 0.012f), glow);

            // The hologram is a 1.4 x 1.0 m quad centred 1.9 m up; frame it,
            // put a dark plate behind it and stand it on a neck.
            Transform holo = st.Find("Hologram");
            float hz = holo != null ? holo.localPosition.z : 0f;
            Slab("ScreenBack", dress, new Vector3(0f, 1.9f, hz + 0.06f), new Vector3(1.5f, 1.1f, 0.06f), 0.02f, Mat(Recess, 0.5f, 0.2f));
            Slab("Frame_T", dress, new Vector3(0f, 2.47f, hz + 0.03f), new Vector3(1.6f, 0.08f, 0.1f), 0.02f, trim);
            Slab("Frame_B", dress, new Vector3(0f, 1.33f, hz + 0.03f), new Vector3(1.6f, 0.08f, 0.1f), 0.02f, trim);
            Slab("Frame_L", dress, new Vector3(-0.77f, 1.9f, hz + 0.03f), new Vector3(0.08f, 1.22f, 0.1f), 0.02f, trim);
            Slab("Frame_R", dress, new Vector3(0.77f, 1.9f, hz + 0.03f), new Vector3(0.08f, 1.22f, 0.1f), 0.02f, trim);
            Box("FrameEdge", dress, new Vector3(0f, 2.52f, hz), new Vector3(1.4f, 0.02f, 0.012f), glow);
            Block("Neck", dress, new Vector3(0f, 0f, hz + 0.08f), Vector3.zero, 1.1f, 1.3f, new Vector2(0.09f, 0.06f), new Vector2(0.08f, 0.05f), 0.02f, trim);
            // A calmer screen: less of the hologram's grid, scan bar and haze.
            if (holo != null)
            {
                Renderer r = holo.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_GridStrength"))
                {
                    Material m = r.material;
                    m.SetFloat("_GridStrength", 0.35f);
                    m.SetFloat("_BarStrength", 0.35f);
                    m.SetFloat("_Alpha", 0.22f);
                }
            }

            Pad(dress, Vector3.zero, 1.15f, accent);
        }

        /// <summary>An octagonal floor plate with a lit rim, at a station's feet.</summary>
        private static void Pad(Transform parent, Vector3 at, float radius, Color accent)
        {
            MeshPart("Pad", parent, CharacterMesh.Prism(8, radius, radius - 0.05f, 0f, 0.03f, "Pad", 22.5f),
                at, Vector3.zero, Mat(new Color(0.05f, 0.055f, 0.075f), 0.6f, 0.45f));
            MeshPart("PadRim", parent, CharacterMesh.Annulus(8, radius - 0.17f, radius - 0.08f, 0.03f, 0.04f, "PadRim", 22.5f),
                at, Vector3.zero, BuildKit.MakeEmissive(accent, 1.2f));
        }

        /// <summary>The station's colour, read off its floating title.</summary>
        private static Color AccentOf(Transform station)
        {
            foreach (Transform child in station)
            {
                if (!child.name.StartsWith("Sign_")) continue;
                TMPro.TextMeshPro tmp = child.GetComponent<TMPro.TextMeshPro>();
                if (tmp != null) return new Color(tmp.color.r, tmp.color.g, tmp.color.b, 1f);
                TextMesh tm = child.GetComponent<TextMesh>();
                if (tm != null) return new Color(tm.color.r, tm.color.g, tm.color.b, 1f);
            }
            return Cyan;
        }

        // ---- Security scanner ---------------------------------------------------

        /// <summary>A booth framing the scan panel: base pad, two posts with
        /// lit inner edges and a header, over the old pedestal's collider.</summary>
        private static void Scanner()
        {
            FaceScanner scanner = FindObjectOfType<FaceScanner>();
            if (scanner == null || scanner.transform.Find("ScannerDress") != null) return;
            Transform st = scanner.transform;
            var dress = new GameObject("ScannerDress").transform;
            dress.SetParent(st, false);
            Material body = Mat(Body, 0.5f, 0.4f);
            Material trim = Mat(Trim, 0.6f, 0.6f);
            Material glow = BuildKit.MakeEmissive(Cyan, 1.3f);

            HideRenderer(st.Find("ScannerPedestal"));
            Block("Plinth", dress, Vector3.zero, Vector3.zero, 0f, 0.1f, new Vector2(0.95f, 0.6f), new Vector2(0.93f, 0.58f), 0.03f, trim);
            Block("Pedestal", dress, Vector3.zero, Vector3.zero, 0.1f, 1.45f, new Vector2(0.62f, 0.36f), new Vector2(0.66f, 0.4f), 0.05f, body);
            Block("PedestalTop", dress, Vector3.zero, Vector3.zero, 1.45f, 1.52f, new Vector2(0.72f, 0.44f), new Vector2(0.72f, 0.44f), 0.025f, trim);
            // Face-height camera on the pedestal's front.
            Slab("Camera", dress, new Vector3(0f, 1.32f, -0.42f), new Vector3(0.24f, 0.12f, 0.08f), 0.02f, trim);
            MeshPart("Lens", dress, CharacterMesh.Prism(8, 0.04f, 0.035f, 0f, 0.02f, "Lens", 22.5f),
                new Vector3(0f, 1.32f, -0.465f), new Vector3(-90f, 0f, 0f), BuildKit.MakeEmissive(Cyan, 2.2f));

            // Posts either side of the 1.2 x 1.6 panel (centred 2.3 m up).
            foreach (float x in new[] { -0.72f, 0.72f })
            {
                Block("Post", dress, new Vector3(x, 0f, 0f), Vector3.zero, 1.52f, 3.2f, new Vector2(0.08f, 0.12f), new Vector2(0.07f, 0.11f), 0.02f, body);
                Box("PostEdge", dress, new Vector3(x - Mathf.Sign(x) * 0.075f, 2.35f, -0.08f), new Vector3(0.018f, 1.55f, 0.018f), glow);
            }
            Slab("Header", dress, new Vector3(0f, 3.24f, 0f), new Vector3(1.6f, 0.14f, 0.26f), 0.03f, trim);
            Box("HeaderEdge", dress, new Vector3(0f, 3.17f, -0.135f), new Vector3(1.4f, 0.018f, 0.01f), glow);
        }

        // ---- Security+ kiosk ----------------------------------------------------

        private static void SecurityPlus()
        {
            SecurityPlusKiosk kiosk = FindObjectOfType<SecurityPlusKiosk>();
            if (kiosk == null || kiosk.transform.Find("KioskDress") != null) return;
            Transform kt = kiosk.transform;
            // Out from behind the centrepiece, facing the room like the scanner.
            kt.position = SecurityPlusPosition;
            kt.rotation = Quaternion.identity;

            var dress = new GameObject("KioskDress").transform;
            dress.SetParent(kt, false);
            Color accent = new Color(0.62f, 0.45f, 0.95f);
            HideRenderer(kt.Find("Pillar"));
            Material body = Mat(Body, 0.5f, 0.4f);
            Material trim = Mat(Trim, 0.6f, 0.6f);
            Block("Plinth", dress, Vector3.zero, Vector3.zero, 0f, 0.08f, new Vector2(0.4f, 0.32f), new Vector2(0.38f, 0.31f), 0.03f, trim);
            Block("Stand", dress, Vector3.zero, Vector3.zero, 0.08f, 1.3f, new Vector2(0.28f, 0.2f), new Vector2(0.25f, 0.18f), 0.05f, body);
            Box("StandAccent", dress, new Vector3(0f, 0.7f, -0.2f), new Vector3(0.025f, 0.9f, 0.01f), BuildKit.MakeEmissive(accent, 1.3f));
        }

        // ---- Signs and labels ---------------------------------------------------

        /// <summary>The CYVERSE title sat at the same height as the CIA Triad
        /// station's sign in front of it, so the two overlapped from the spawn.
        /// The orientation room is 8.4 m tall: lift it clear.</summary>
        private static void LiftCoreSign()
        {
            GameObject sign = GameObject.Find("Sign_CYVERSE");
            if (sign == null) return;
            Vector3 p = sign.transform.position;
            sign.transform.position = new Vector3(p.x, 5.6f, p.z);
            Rebase(sign.transform);
        }

        private static void Rebase(Transform sign)
        {
            SignFX fx = sign.GetComponent<SignFX>();
            if (fx != null) fx.Rebase();
        }

        private static void HideRenderer(Transform t)
        {
            if (t == null) return;
            Renderer r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
    }
}
