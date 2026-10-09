using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Forensics;
using Cyverse.Level;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Evidence acquisition at the Investigation Desk — the step directly after
    /// chain of custody. Once custody is accepted, the analyst's first E press
    /// at the desk sets the seized phone into a forensic cradle, images it through
    /// a hardware write blocker, and hash-verified, with the readout on the
    /// console's LEFT monitor.
    ///
    /// The rig is parented to the console and laid out in its own frame:
    /// origin on the desk top under the cradle, +Z toward the monitor, yawed to
    /// match the monitor so cradle, phone face and screen all point at a player
    /// standing at the console.
    ///
    /// The console drives it: first E starts the upload (<see cref="StartUpload"/>),
    /// the next E opens the terminal — finishing the upload instantly via
    /// <see cref="CompleteNow"/> if it is still running, so it never blocks.
    /// </summary>
    public sealed class PlugInStation : MonoBehaviour
    {
        public static PlugInStation Instance { get; private set; }

        public bool UploadStarted => started;
        public bool UploadComplete => complete;
        public bool UploadInProgress => started && !complete;

        private const float DockArcDuration = 1.05f;
        private const float SeatDuration = 0.30f;
        private const float MountPause = 0.35f;
        private const float ImagingDuration = 2.8f;
        private const float VerifyDuration = 1.1f;
        private const float DeviceCapacityGb = 64f;

        private const float PhoneWidth = 0.12f;
        private const float PhoneHeight = 0.24f;
        private const float PhoneDepth = 0.016f;
        private const float CradleLean = 35f;   // back from vertical: low enough to clear the monitor
        private const float BaseHeight = 0.025f;
        private const float BackrestHeight = 0.18f;
        private const float BackrestDepth = 0.022f;
        private const float CableDiameter = 0.009f;

        private static readonly Vector3 BlockerCenter = new Vector3(-0.255f, 0.031f, 0.06f);
        private static readonly Vector3 BlockerSize = new Vector3(0.20f, 0.056f, 0.135f);

        // Matches the 0.9 x 0.55 m monitor (aspect 1.636) so text isn't stretched.
        private const int ScreenRtWidth = 576;
        private const int ScreenRtHeight = 352;

        private static readonly Color Green = new Color(0.30f, 1f, 0.55f);
        private static readonly Color Gold = new Color(0.92f, 0.72f, 0.30f);
        private static readonly Color Amber = new Color(1f, 0.60f, 0.16f);
        private static readonly Color Dim = new Color(0.60f, 0.78f, 0.80f);
        private static readonly Color Muted = new Color(0.36f, 0.50f, 0.50f);

        private Transform consoleRoot;
        private DiegeticScreen screen;
        // The RIGHT monitor: blank while the device is imaged, then shows the
        // working copy the analysis runs on.
        private DiegeticScreen imageScreen;
        private TMP_Text imageTitleText;
        private TMP_Text imageDetailText;
        private TMP_Text imageNoteText;
        private Image imageAccent;

        private Transform phone;
        private Vector3 dockLocalPos;
        private Vector3 preDockLocalPos;
        private Quaternion dockLocalRot;
        private Material phoneFaceMat;
        private Transform phoneBarFill;

        private Material statusStripMat;
        private Material powerLedMat;
        private Material blockLedMat;
        private Material activityLedMat;
        private Vector3[] deviceCable;
        private Vector3[] hostCable;
        private Transform deviceBead;
        private Transform hostBead;

        private TMP_Text titleText;
        private TMP_Text tagText;
        private TMP_Text deviceText;
        private TMP_Text percentText;
        private TMP_Text bytesText;
        private TMP_Text hashText;
        private TMP_Text statusText;
        private Image headerAccent;
        private RectTransform progressFill;
        private float progressTrackWidth;

        private string hashDisplay;
        private bool armed;
        private bool started;
        private bool complete;
        private Coroutine sequence;
        private ChainOfCustodyForm subscribedForm;

        // ---- Construction ----------------------------------------------------

        /// <summary>Install the rig on <paramref name="console"/>: the left
        /// monitor becomes the acquisition screen and the cradle, write blocker
        /// and cabling go on the desk beneath it.</summary>
        public static PlugInStation Build(ForensicsConsole console)
        {
            if (console == null) return null;
            Transform consoleRoot = console.transform;

            // Take the left monitor's pose from the console itself so the
            // screen stays on the monitor if the console layout changes.
            Vector3 monitorPos = new Vector3(-1.05f, 1.65f, 0.14f);
            Quaternion monitorRot = Quaternion.Euler(-8f, -24f, 0f);
            Vector2 monitorSize = new Vector2(0.9f, 0.55f);
            Transform staticScreen = consoleRoot.Find("MonScreen_-1");
            if (staticScreen != null)
            {
                monitorPos = staticScreen.localPosition;
                monitorRot = staticScreen.localRotation;
                monitorSize = new Vector2(staticScreen.localScale.x, staticScreen.localScale.y);
                // Same pose as the RT quad: hide it now, since Destroy waits a
                // frame and the two would z-fight until then.
                Renderer staticRenderer = staticScreen.GetComponent<Renderer>();
                if (staticRenderer != null) staticRenderer.enabled = false;
                Destroy(staticScreen.gameObject);
            }

            float deskTop = 1.0f;
            Transform desk = consoleRoot.Find("Desk");
            if (desk != null) deskTop = desk.localPosition.y + desk.localScale.y * 0.5f;

            var host = new GameObject("DF_PlugInStation");
            host.transform.SetParent(consoleRoot, false);
            // In front of the monitor, nudged toward the console centre so it
            // reads as part of the analyst's workspace rather than its edge.
            host.transform.localPosition = new Vector3(monitorPos.x * 0.93f, deskTop, -0.22f);
            host.transform.localRotation = Quaternion.Euler(0f, monitorRot.eulerAngles.y, 0f);

            var station = host.AddComponent<PlugInStation>();
            station.consoleRoot = consoleRoot;
            station.hashDisplay = ShortHash(EvidenceHash());
            station.BuildScreen(monitorPos, monitorRot, monitorSize);
            station.BuildCradle();
            station.BuildWriteBlocker();
            station.BuildCables(deskTop);
            station.BuildPhone();
            station.BuildReadout();
            station.BuildImageScreen();
            station.ShowAwaitingCustody();
            station.StartCoroutine(station.SubscribeWhenReady());
            return station;
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (subscribedForm != null) subscribedForm.Completed -= OnCustodyCompleted;
        }

        private void BuildScreen(Vector3 localPos, Quaternion localRot, Vector2 size)
        {
            screen = DiegeticScreen.Create(Vector3.zero, 0f, size,
                ScreenRtWidth, ScreenRtHeight, "DF_UploadScreen");
            screen.transform.SetParent(consoleRoot, false);
            screen.transform.localPosition = localPos;
            screen.transform.localRotation = localRot;
        }

        /// <summary>A leaning forensic cradle. All parts derive from one lean
        /// frame so the phone seats flush on the backrest and behind the lip by
        /// construction.</summary>
        private void BuildCradle()
        {
            Material dockMat = BuildKit.MakeStandard(new Color(0.07f, 0.08f, 0.10f), 0.62f, 0.55f);
            Material portMat = BuildKit.MakeStandard(new Color(0.015f, 0.018f, 0.02f), 0.3f, 0.2f);

            Quaternion lean = Quaternion.Euler(CradleLean, 0f, 0f);
            Vector3 up = lean * Vector3.up;        // phone's long axis, tipped toward the monitor
            Vector3 back = lean * Vector3.forward; // out of the phone's back
            // Where the phone's bottom edge rests (mid-thickness).
            Vector3 seat = new Vector3(0f, BaseHeight + 0.006f, -0.034f);

            dockLocalRot = lean;
            dockLocalPos = seat + up * (PhoneHeight * 0.5f);
            preDockLocalPos = dockLocalPos + up * 0.075f;

            Transform cradle = new GameObject("DF_PhoneDock").transform;
            cradle.SetParent(transform, false);

            Part(PrimitiveType.Cube, "Base", cradle, new Vector3(0f, BaseHeight * 0.5f, 0.005f),
                Quaternion.identity, new Vector3(0.17f, BaseHeight, 0.18f), dockMat);

            Vector3 backrest = seat + up * (BackrestHeight * 0.5f - 0.004f)
                + back * (PhoneDepth * 0.5f + 0.0015f + BackrestDepth * 0.5f);
            Part(PrimitiveType.Cube, "Backrest", cradle, backrest, lean,
                new Vector3(0.15f, BackrestHeight, BackrestDepth), dockMat);
            // Kickstand meeting the backrest's rear face just below its middle.
            Part(PrimitiveType.Cube, "Strut", cradle,
                new Vector3(0f, BaseHeight + 0.03f, backrest.z + 0.026f),
                Quaternion.identity, new Vector3(0.09f, 0.06f, 0.03f), dockMat);

            float phoneFrontZ = (seat - back * (PhoneDepth * 0.5f)).z;
            Part(PrimitiveType.Cube, "Lip", cradle,
                new Vector3(0f, BaseHeight + 0.01f, phoneFrontZ - 0.012f),
                Quaternion.identity, new Vector3(0.15f, 0.02f, 0.016f), dockMat);

            statusStripMat = BuildKit.MakeEmissive(Amber, 0.5f);
            Part(PrimitiveType.Cube, "StatusStrip", cradle, new Vector3(0f, BaseHeight * 0.5f, -0.0865f),
                Quaternion.identity, new Vector3(0.13f, 0.008f, 0.003f), statusStripMat);
            Part(PrimitiveType.Cube, "Port", cradle, new Vector3(0f, 0.0125f, 0.097f),
                Quaternion.identity, new Vector3(0.03f, 0.012f, 0.004f), portMat);
        }

        /// <summary>A small hardware write blocker beside the cradle, with
        /// power / write-block / activity LEDs on its faceplate.</summary>
        private void BuildWriteBlocker()
        {
            Material chassisMat = BuildKit.MakeStandard(new Color(0.40f, 0.43f, 0.46f), 0.55f, 0.8f);
            Material faceMat = BuildKit.MakeStandard(new Color(0.03f, 0.035f, 0.04f), 0.5f, 0.3f);
            Material portMat = BuildKit.MakeStandard(new Color(0.015f, 0.018f, 0.02f), 0.3f, 0.2f);
            Material labelMat = BuildKit.MakeStandard(new Color(0.78f, 0.80f, 0.76f), 0.15f, 0f);

            Transform blocker = new GameObject("DF_WriteBlocker").transform;
            blocker.SetParent(transform, false);
            blocker.localPosition = BlockerCenter;

            Vector3 half = BlockerSize * 0.5f;
            Part(PrimitiveType.Cube, "Chassis", blocker, Vector3.zero, Quaternion.identity, BlockerSize, chassisMat);
            Part(PrimitiveType.Cube, "Faceplate", blocker, new Vector3(0f, -0.002f, -half.z - 0.0012f),
                Quaternion.identity, new Vector3(BlockerSize.x - 0.02f, BlockerSize.y - 0.018f, 0.0024f), faceMat);
            Part(PrimitiveType.Cube, "TopStripe", blocker, new Vector3(0f, half.y + 0.0012f, -half.z + 0.012f),
                Quaternion.identity, new Vector3(BlockerSize.x - 0.03f, 0.0024f, 0.006f),
                BuildKit.MakeEmissive(Gold, 0.6f));
            Part(PrimitiveType.Cube, "AssetLabel", blocker, new Vector3(0.045f, -0.002f, -half.z - 0.0026f),
                Quaternion.identity, new Vector3(0.05f, 0.016f, 0.001f), labelMat);

            powerLedMat = Led(blocker, "Led_Power", -0.062f, Green, 1.6f);
            blockLedMat = Led(blocker, "Led_WriteBlock", -0.040f, Amber, 0.08f);
            activityLedMat = Led(blocker, "Led_Activity", -0.018f, Green, 0.05f);

            // Device port faces the cradle; host port exits the rear toward the workstation.
            Part(PrimitiveType.Cube, "Port_Device", blocker, new Vector3(half.x + 0.002f, -0.006f, 0.03f),
                Quaternion.identity, new Vector3(0.004f, 0.012f, 0.026f), portMat);
            Part(PrimitiveType.Cube, "Port_Host", blocker, new Vector3(0.04f, -0.006f, half.z + 0.002f),
                Quaternion.identity, new Vector3(0.026f, 0.012f, 0.004f), portMat);

            // Rubber feet so the chassis rests on the desk instead of hovering.
            for (int i = 0; i < 4; i++)
            {
                float fx = (i % 2 == 0 ? -1f : 1f) * (half.x - 0.02f);
                float fz = (i < 2 ? -1f : 1f) * (half.z - 0.02f);
                Part(PrimitiveType.Cube, "Foot_" + i, blocker, new Vector3(fx, -half.y - 0.0015f, fz),
                    Quaternion.identity, new Vector3(0.02f, 0.003f, 0.02f), portMat);
            }
        }

        private Material Led(Transform blocker, string name, float x, Color color, float intensity)
        {
            Material mat = BuildKit.MakeEmissive(color, intensity);
            Part(PrimitiveType.Cube, name, blocker, new Vector3(x, 0.004f, -BlockerSize.z * 0.5f - 0.0026f),
                Quaternion.identity, new Vector3(0.011f, 0.011f, 0.003f), mat);
            return mat;
        }

        private void BuildCables(float deskTop)
        {
            Material cableMat = BuildKit.MakeStandard(new Color(0.025f, 0.028f, 0.032f), 0.35f, 0.1f);
            Material plugMat = BuildKit.MakeStandard(new Color(0.06f, 0.065f, 0.07f), 0.5f, 0.4f);
            float lie = CableDiameter * 0.5f + 0.0005f; // resting on the desk top

            // Cradle rear port -> write blocker device port, looping behind the cradle.
            deviceCable = new[]
            {
                new Vector3(0f, 0.0125f, 0.099f),
                new Vector3(0f, lie + 0.003f, 0.125f),
                new Vector3(-0.05f, lie, 0.142f),
                new Vector3(-0.11f, lie, 0.125f),
                new Vector3(-0.142f, 0.012f, 0.098f),
                new Vector3(-0.152f, 0.025f, 0.09f),
            };

            // Write blocker host port -> back of the desk, routed outside the
            // monitor foot and draped over the rear edge. The far half is laid
            // out in console space so it follows the desk, not the rig's yaw.
            float hostPortZ = BlockerCenter.z + BlockerSize.z * 0.5f + 0.004f;
            hostCable = new[]
            {
                new Vector3(-0.215f, 0.025f, hostPortZ),
                new Vector3(-0.215f, lie + 0.004f, hostPortZ + 0.025f),
                new Vector3(-0.24f, lie, 0.20f),
                FromConsole(new Vector3(-1.31f, deskTop + lie, 0.30f)),
                FromConsole(new Vector3(-1.33f, deskTop + lie, 0.50f)),
                FromConsole(new Vector3(-1.335f, deskTop + lie, 0.553f)),
                FromConsole(new Vector3(-1.335f, deskTop - 0.012f, 0.562f)),
                FromConsole(new Vector3(-1.335f, deskTop - 0.10f, 0.566f)),
            };

            BuildCable("DF_Cable_Device", deviceCable, cableMat, plugMat, plugAtEnd: true);
            BuildCable("DF_Cable_Host", hostCable, cableMat, plugMat, plugAtEnd: false);

            // Data pulses that run along both cables while the image transfers.
            Material beadMat = BuildKit.MakeEmissive(Green, 2.2f);
            deviceBead = Part(PrimitiveType.Sphere, "DF_DataPulse_Device", transform, deviceCable[0],
                Quaternion.identity, Vector3.one * CableDiameter * 1.5f, beadMat).transform;
            hostBead = Part(PrimitiveType.Sphere, "DF_DataPulse_Host", transform, hostCable[0],
                Quaternion.identity, Vector3.one * CableDiameter * 1.5f, beadMat).transform;
            deviceBead.gameObject.SetActive(false);
            hostBead.gameObject.SetActive(false);
        }

        private Vector3 FromConsole(Vector3 consoleLocal) =>
            transform.InverseTransformPoint(consoleRoot.TransformPoint(consoleLocal));

        /// <summary>Cylinder segments with sphere joints, so bends read as one
        /// continuous cable instead of a chain of sticks.</summary>
        private void BuildCable(string name, Vector3[] points, Material cableMat, Material plugMat, bool plugAtEnd)
        {
            Transform cable = new GameObject(name).transform;
            cable.SetParent(transform, false);
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                Vector3 delta = b - a;
                Part(PrimitiveType.Cylinder, "Segment_" + i, cable, (a + b) * 0.5f,
                    Quaternion.FromToRotation(Vector3.up, delta.normalized),
                    new Vector3(CableDiameter, delta.magnitude * 0.5f, CableDiameter), cableMat);
                if (i > 0)
                    Part(PrimitiveType.Sphere, "Joint_" + i, cable, a, Quaternion.identity,
                        Vector3.one * CableDiameter, cableMat);
            }

            // Connector boots where the cable meets a port.
            AddPlug(cable, points[0], points[1], plugMat);
            if (plugAtEnd)
                AddPlug(cable, points[points.Length - 1], points[points.Length - 2], plugMat);
        }

        private void AddPlug(Transform cable, Vector3 port, Vector3 toward, Material mat)
        {
            Vector3 dir = (toward - port).normalized;
            Part(PrimitiveType.Cube, "Plug", cable, port + dir * 0.009f, Quaternion.LookRotation(dir),
                new Vector3(0.014f, 0.010f, 0.018f), mat);
        }

        private void BuildPhone()
        {
            Material caseMat = BuildKit.MakeStandard(new Color(0.035f, 0.04f, 0.05f), 0.78f, 0.35f);
            Material slotMat = BuildKit.MakeStandard(new Color(0.01f, 0.012f, 0.014f), 0.3f, 0.2f);
            phoneFaceMat = BuildKit.MakeEmissive(new Color(0.06f, 0.22f, 0.17f), 0.9f);

            var root = new GameObject("DF_EvidencePhone");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = dockLocalPos;
            root.transform.localRotation = dockLocalRot;
            phone = root.transform;

            float face = -PhoneDepth * 0.5f;
            Part(PrimitiveType.Cube, "Shell", phone, Vector3.zero, Quaternion.identity,
                new Vector3(PhoneWidth, PhoneHeight, PhoneDepth), caseMat);
            // Quads render on their local -Z face, which is the phone's front.
            // 1.5 mm layer steps keep the stack clear of 16-bit WebGL depth.
            Part(PrimitiveType.Quad, "Screen", phone, new Vector3(0f, 0.002f, face - 0.0015f),
                Quaternion.identity, new Vector3(0.106f, 0.214f, 1f), phoneFaceMat);
            Part(PrimitiveType.Cube, "Speaker", phone, new Vector3(0f, PhoneHeight * 0.5f - 0.006f, face - 0.0005f),
                Quaternion.identity, new Vector3(0.03f, 0.003f, 0.001f), slotMat);
            // The phone mirrors the transfer on its own screen.
            Part(PrimitiveType.Quad, "TransferTrack", phone, new Vector3(0f, -0.06f, face - 0.003f),
                Quaternion.identity, new Vector3(0.082f, 0.007f, 1f),
                BuildKit.MakeEmissive(new Color(0.03f, 0.10f, 0.08f), 0.6f));
            phoneBarFill = Part(PrimitiveType.Quad, "TransferFill", phone, new Vector3(-0.041f, -0.06f, face - 0.0045f),
                Quaternion.identity, new Vector3(0.0001f, 0.007f, 1f), BuildKit.MakeEmissive(Green, 1.8f)).transform;

            // The device is still at intake until custody clears it.
            root.SetActive(false);
        }

        private static GameObject Part(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Quaternion localRot, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            BuildKit.StripCollider(go); // decorative; must never catch the console's interact ray
            return go;
        }

        // ---- Screen readout ---------------------------------------------------

        private void BuildReadout()
        {
            if (screen == null || screen.CanvasRoot == null) return;
            Vector2 size = screen.CanvasSize; // ~540 x 330 canvas units
            const float margin = 18f;
            float content = size.x - margin * 2f;

            Image bg = Panel("UploadBg", Vector2.zero, size, new Color(0.012f, 0.035f, 0.028f));
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;

            Panel("HeaderBand", Vector2.zero, new Vector2(size.x, 54f), new Color(0.03f, 0.09f, 0.065f));
            headerAccent = Panel("HeaderAccent", new Vector2(0f, 54f), new Vector2(size.x, 3f), Gold);

            titleText = Label("UploadTitle", new Vector2(margin, 0f), new Vector2(330f, 54f), 24f,
                TextAlignmentOptions.MidlineLeft, Gold, bold: true);
            tagText = Label("UploadTag", new Vector2(size.x - margin - 170f, 0f), new Vector2(170f, 54f), 15f,
                TextAlignmentOptions.MidlineRight, Muted);
            deviceText = Label("UploadDevice", new Vector2(margin, 66f), new Vector2(content, 30f), 17f,
                TextAlignmentOptions.MidlineLeft, Dim);
            percentText = Label("UploadPercent", new Vector2(margin, 98f), new Vector2(220f, 62f), 46f,
                TextAlignmentOptions.MidlineLeft, Muted, bold: true);
            bytesText = Label("UploadBytes", new Vector2(margin + 220f, 98f), new Vector2(content - 220f, 62f), 17f,
                TextAlignmentOptions.MidlineRight, Dim);

            Image track = Panel("ProgressTrack", new Vector2(margin, 168f), new Vector2(content, 20f),
                new Color(0.04f, 0.12f, 0.09f));
            progressTrackWidth = content;
            var fill = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            progressFill = (RectTransform)fill.transform;
            progressFill.anchorMin = new Vector2(0f, 0f);
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.pivot = new Vector2(0f, 0.5f);
            progressFill.anchoredPosition = Vector2.zero;
            progressFill.sizeDelta = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = Green;
            fillImage.raycastTarget = false;

            hashText = Label("UploadHash", new Vector2(margin, 198f), new Vector2(content, 28f), 16f,
                TextAlignmentOptions.MidlineLeft, Muted);
            Panel("Divider", new Vector2(margin, 236f), new Vector2(content, 2f), new Color(0.10f, 0.25f, 0.18f));
            statusText = Label("UploadStatus", new Vector2(margin, 246f), new Vector2(content, 68f), 20f,
                TextAlignmentOptions.Center, Amber, bold: true);
        }

        /// <summary>Top-left anchored text box; <paramref name="pos"/> is measured
        /// down from the canvas' top-left corner.</summary>
        private TMP_Text Label(string name, Vector2 pos, Vector2 box, float size,
            TextAlignmentOptions alignment, Color color, bool bold = false, RectTransform canvas = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas != null ? canvas : screen.CanvasRoot, false);
            PlaceTopLeft((RectTransform)go.transform, pos, box);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            // Rows carry deliberate line breaks; shrink to fit instead of wrapping into a neighbour.
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 11f;
            text.fontSizeMax = size;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private Image Panel(string name, Vector2 pos, Vector2 box, Color color, RectTransform canvas = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas != null ? canvas : screen.CanvasRoot, false);
            PlaceTopLeft((RectTransform)go.transform, pos, box);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void PlaceTopLeft(RectTransform rt, Vector2 pos, Vector2 box)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = new Vector2(Mathf.Max(1f, box.x), Mathf.Max(1f, box.y));
        }

        /// <summary>Take over the console's RIGHT monitor the same way the left
        /// one is: a render-texture screen at the static quad's pose.</summary>
        private void BuildImageScreen()
        {
            Transform staticScreen = consoleRoot.Find("MonScreen_1");
            if (staticScreen == null) return;
            Vector2 size = new Vector2(staticScreen.localScale.x, staticScreen.localScale.y);
            imageScreen = DiegeticScreen.Create(Vector3.zero, 0f, size,
                ScreenRtWidth, ScreenRtHeight, "DF_ImageScreen");
            imageScreen.transform.SetParent(consoleRoot, false);
            imageScreen.transform.localPosition = staticScreen.localPosition;
            imageScreen.transform.localRotation = staticScreen.localRotation;
            Renderer staticRenderer = staticScreen.GetComponent<Renderer>();
            if (staticRenderer != null) staticRenderer.enabled = false;
            Destroy(staticScreen.gameObject);

            RectTransform canvas = imageScreen.CanvasRoot;
            if (canvas == null) return;
            Vector2 canvasSize = imageScreen.CanvasSize;
            const float margin = 18f;
            float content = canvasSize.x - margin * 2f;

            Image bg = Panel("ImageBg", Vector2.zero, canvasSize, new Color(0.012f, 0.035f, 0.028f), canvas);
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;

            imageTitleText = Label("ImageTitle", new Vector2(margin, 92f), new Vector2(content, 76f), 54f,
                TextAlignmentOptions.Center, Green, bold: true, canvas: canvas);
            imageAccent = Panel("ImageAccent", new Vector2(canvasSize.x * 0.5f - 120f, 176f),
                new Vector2(240f, 3f), Green, canvas);
            imageDetailText = Label("ImageDetail", new Vector2(margin, 190f), new Vector2(content, 30f), 18f,
                TextAlignmentOptions.Center, Dim, canvas: canvas);
            imageNoteText = Label("ImageNote", new Vector2(margin, 224f), new Vector2(content, 28f), 15f,
                TextAlignmentOptions.Center, Muted, canvas: canvas);
            ShowImageEmpty();
        }

        private void ShowImageEmpty()
        {
            if (imageScreen == null || imageTitleText == null) return;
            imageTitleText.text = "";
            imageDetailText.text = "";
            imageNoteText.text = "";
            imageAccent.enabled = false;
            imageScreen.RenderNow();
        }

        private void ShowImageCopied()
        {
            if (imageScreen == null || imageTitleText == null) return;
            imageTitleText.text = "IMAGE COPIED";
            imageAccent.enabled = true;
            imageDetailText.text = $"{ImageFileName()}   ·   {DeviceCapacityGb:0.0} GB   ·   SHA-256 match";
            imageNoteText.text = "Analysis runs on this copy; the original stays sealed.";
            imageScreen.RenderNow();
        }

        private static string ImageFileName()
        {
            string source = SocProgress.TryGetEvidence(out var evidence) && !string.IsNullOrEmpty(evidence.computer)
                ? evidence.computer : "training";
            return source + "_phone.E01";
        }

        private void ShowAwaitingCustody()
        {
            if (titleText == null) return;
            titleText.text = "EVIDENCE ACQUISITION";
            tagText.text = "WRITE-BLOCK STANDBY";
            tagText.color = Muted;
            deviceText.text = "DEVICE   none docked";
            percentText.text = "--";
            percentText.color = Muted;
            bytesText.text = "awaiting device";
            hashText.text = "SHA-256   ----";
            statusText.text = "COMPLETE CHAIN OF CUSTODY\nAT EVIDENCE INTAKE FIRST";
            statusText.color = Amber;
            SetProgress(0f);
            screen.RenderNow();
        }

        private void ShowReadyToDock()
        {
            if (titleText == null) return;
            statusText.text = "CUSTODY ACCEPTED\nPRESS E TO DOCK THE DEVICE";
            statusText.color = Green;
            screen.RenderNow();
        }

        private void ShowDocking()
        {
            if (titleText == null) return;
            tagText.text = "WRITE-BLOCK ON";
            tagText.color = Amber;
            deviceText.text = "DEVICE   " + DeviceLabel();
            percentText.text = "0%";
            percentText.color = Green;
            bytesText.text = $"0.0 / {DeviceCapacityGb:0.0} GB";
            statusText.text = "DEVICE DETECTED\nMOUNTING READ-ONLY";
            statusText.color = Gold;
            screen.RenderNow();
        }

        private void ShowImaging(float k)
        {
            int pct = Mathf.RoundToInt(k * 100f);
            percentText.text = pct + "%";
            // A believable, slightly unsteady transfer rate.
            int rate = 380 + Mathf.RoundToInt(Mathf.PerlinNoise(Time.time * 3f, 0.3f) * 90f);
            bytesText.text = $"{k * DeviceCapacityGb:0.0} / {DeviceCapacityGb:0.0} GB   ·   {rate} MB/s";
            statusText.text = "IMAGING DEVICE\nREAD-ONLY  ·  NO WRITES REACH EVIDENCE";
            statusText.color = Gold;
            SetProgress(k);
            screen.RenderNow();
        }

        private void ShowVerifying(int revealed)
        {
            string shown = hashDisplay.Substring(0, revealed) + new string('-', hashDisplay.Length - revealed);
            hashText.text = "SHA-256   " + shown;
            hashText.color = Dim;
            statusText.text = "VERIFYING\nSHA-256 HASH VALUES";
            statusText.color = Gold;
            screen.RenderNow();
        }

        private void ShowComplete()
        {
            titleText.text = "ACQUISITION COMPLETE";
            titleText.color = Green;
            headerAccent.color = Green;
            tagText.text = "WRITE-BLOCK ON";
            tagText.color = Amber;
            deviceText.text = "DEVICE   " + DeviceLabel();
            percentText.text = "100%";
            percentText.color = Green;
            bytesText.text = $"{DeviceCapacityGb:0.0} / {DeviceCapacityGb:0.0} GB   ·   image sealed";
            hashText.text = $"SHA-256   {hashDisplay}   <color=#4CE087>[MATCH]</color>";
            hashText.color = Dim;
            statusText.text = "HASH VALUES MATCH\nREADY FOR ANALYSIS";
            statusText.color = Green;
            SetProgress(1f);
            screen.RenderNow();
        }

        private void SetProgress(float k)
        {
            if (progressFill != null) progressFill.sizeDelta = new Vector2(progressTrackWidth * k, 0f);
            if (phoneBarFill != null)
            {
                float w = Mathf.Max(0.0001f, 0.082f * k);
                phoneBarFill.localScale = new Vector3(w, 0.007f, 1f);
                phoneBarFill.localPosition = new Vector3(-0.041f + w * 0.5f, -0.06f, phoneBarFill.localPosition.z);
            }
        }

        private static string DeviceLabel()
        {
            string caseName = SocProgress.TryGetEvidence(out var evidence) && !string.IsNullOrEmpty(evidence.computer)
                ? "case " + evidence.computer
                : "training case";
            return $"seized phone  ·  {DeviceCapacityGb:0} GB  ·  {caseName}";
        }

        /// <summary>Deterministic 64-hex digest seeded from the SOC handoff, so the
        /// same case always shows the same "evidence hash".</summary>
        private static string EvidenceHash()
        {
            string seed = SocProgress.TryGetEvidence(out var e)
                ? e.computer + "|" + e.collectedAtUtc + "|" + e.user
                : "CYVERSE-TRAINING-IMAGE";
            uint x = 2166136261u;
            foreach (char c in seed) { x ^= c; x *= 16777619u; }
            if (x == 0) x = 0x9E3779B9u;
            var sb = new StringBuilder(64);
            const string hex = "0123456789abcdef";
            for (int i = 0; i < 64; i++)
            {
                x ^= x << 13; x ^= x >> 17; x ^= x << 5;
                sb.Append(hex[(int)(x & 15u)]);
            }
            return sb.ToString();
        }

        private static string ShortHash(string full) => full.Substring(0, 16) + "..." + full.Substring(56, 8);

        // ---- Trigger -----------------------------------------------------------

        private IEnumerator SubscribeWhenReady()
        {
            // Custody is built by another station and may not exist yet.
            for (int i = 0; i < 80 && subscribedForm == null; i++)
            {
                ChainOfCustodyForm form = ChainOfCustodyForm.Instance;
                if (form != null)
                {
                    subscribedForm = form;
                    subscribedForm.Completed += OnCustodyCompleted;
                    if (form.IsComplete) OnCustodyCompleted();
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
            }
            // No custody step in this scene: ready to dock straight away.
            if (subscribedForm == null) OnCustodyCompleted();
        }

        private void OnCustodyCompleted()
        {
            if (armed || started || complete) return;
            armed = true;
            ShowReadyToDock();
        }

        /// <summary>Begin docking + imaging. Called by the console on the first
        /// E press after custody. Returns false if it already started.</summary>
        public bool StartUpload()
        {
            if (started || complete) return false;
            started = true;
            armed = false;
            // The device the analyst took from the evidence locker leaves their hands
            // here: DockPhone starts this rig's phone from the held position, so remove
            // the carried copy instead of leaving two phones. No-op when none was taken.
            EvidenceLocker.ConsumeCarriedDevice();
            sequence = StartCoroutine(AcquisitionSequence());
            return true;
        }

        /// <summary>Jump straight to the verified, docked state. Called by the
        /// console on interact so acquisition never stands between the analyst
        /// and the terminal.</summary>
        public void CompleteNow()
        {
            if (complete) return;
            if (sequence != null) StopCoroutine(sequence);
            started = true;
            armed = false;
            if (phone != null)
            {
                phone.gameObject.SetActive(true);
                phone.localPosition = dockLocalPos;
                phone.localRotation = dockLocalRot;
                phone.localScale = Vector3.one;
            }
            Finish(playSound: false);
        }

        // ---- Sequence ----------------------------------------------------------

        private IEnumerator AcquisitionSequence()
        {
            ShowDocking();
            yield return DockPhone();

            SetEmission(statusStripMat, Green, 1.4f);
            SetEmission(blockLedMat, Amber, 1.8f);
            SetEmission(phoneFaceMat, new Color(0.10f, 0.42f, 0.30f), 1.4f);
            if (Cyverse.Audio.Sfx.Instance != null) Cyverse.Audio.Sfx.Instance.PlayClick();
            yield return new WaitForSeconds(MountPause);

            deviceBead.gameObject.SetActive(true);
            hostBead.gameObject.SetActive(true);
            float t = 0f;
            int lastPct = -1;
            while (t < 1f)
            {
                t += Time.deltaTime / ImagingDuration;
                float k = Mathf.Clamp01(t);
                float time = Time.time;
                bool blink = Mathf.PerlinNoise(time * 22f, 0.7f) > 0.42f;
                SetEmission(activityLedMat, Green, blink ? 2.4f : 0.15f);
                SetEmission(phoneFaceMat, new Color(0.10f, 0.42f, 0.30f), 1.2f + 0.35f * Mathf.Sin(time * 9f));
                deviceBead.localPosition = Sample(deviceCable, Mathf.Repeat(time * 1.6f, 1f));
                hostBead.localPosition = Sample(hostCable, Mathf.Repeat(time * 1.1f + 0.4f, 1f));

                int pct = Mathf.RoundToInt(k * 100f);
                if (pct != lastPct)
                {
                    lastPct = pct;
                    ShowImaging(k);
                }
                yield return null;
            }
            deviceBead.gameObject.SetActive(false);
            hostBead.gameObject.SetActive(false);
            SetEmission(activityLedMat, Green, 0.05f);

            t = 0f;
            int lastShown = -1;
            while (t < 1f)
            {
                t += Time.deltaTime / VerifyDuration;
                int revealed = Mathf.RoundToInt(Mathf.Clamp01(t) * hashDisplay.Length);
                if (revealed != lastShown)
                {
                    lastShown = revealed;
                    ShowVerifying(revealed);
                }
                yield return null;
            }

            Finish(playSound: true);
        }

        /// <summary>The phone comes up from the analyst's hands in an arc, aligns
        /// above the cradle, then slides down the backrest into the seat.</summary>
        private IEnumerator DockPhone()
        {
            phone.gameObject.SetActive(true);

            Vector3 startPos;
            Quaternion startRot;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Transform view = cam.transform;
                Vector3 held = view.position + view.forward * 0.55f + view.right * 0.20f - view.up * 0.24f;
                startPos = transform.InverseTransformPoint(held);
                startRot = Quaternion.Inverse(transform.rotation) * (view.rotation * Quaternion.Euler(-10f, -6f, 3f));
            }
            else
            {
                startPos = preDockLocalPos + new Vector3(0f, 0.30f, -0.20f);
                startRot = Quaternion.Euler(10f, 0f, 0f);
            }
            // The rig only yaws, so local up is world up.
            Vector3 control = (startPos + preDockLocalPos) * 0.5f + Vector3.up * 0.22f;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / DockArcDuration;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                float u = 1f - k;
                phone.localPosition = u * u * startPos + 2f * u * k * control + k * k * preDockLocalPos;
                // Settle the orientation a little before arriving so the final
                // slide is a straight, aligned insertion.
                phone.localRotation = Quaternion.Slerp(startRot, dockLocalRot,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.25f)));
                phone.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(t * 5f));
                yield return null;
            }

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / SeatDuration;
                float k = Mathf.Clamp01(t);
                phone.localPosition = Vector3.Lerp(preDockLocalPos, dockLocalPos, k * k);
                yield return null;
            }
            phone.localPosition = dockLocalPos;
            phone.localRotation = dockLocalRot;
            phone.localScale = Vector3.one;
        }

        private void Finish(bool playSound)
        {
            complete = true;
            sequence = null;
            if (deviceBead != null) deviceBead.gameObject.SetActive(false);
            if (hostBead != null) hostBead.gameObject.SetActive(false);
            SetEmission(statusStripMat, Green, 1.4f);
            SetEmission(blockLedMat, Amber, 1.8f);
            SetEmission(activityLedMat, Green, 0.05f);
            SetEmission(phoneFaceMat, new Color(0.10f, 0.42f, 0.30f), 1.4f);
            if (titleText != null) ShowComplete();
            ShowImageCopied();
            if (playSound && Cyverse.Audio.Sfx.Instance != null) Cyverse.Audio.Sfx.Instance.PlayConfirm();
        }

        private static void SetEmission(Material mat, Color color, float intensity)
        {
            if (mat == null) return;
            mat.color = color;
            mat.SetColor("_EmissionColor", color * intensity);
        }

        /// <summary>Point at fraction <paramref name="t"/> of a polyline's length.</summary>
        private static Vector3 Sample(Vector3[] points, float t)
        {
            float total = 0f;
            for (int i = 0; i < points.Length - 1; i++) total += Vector3.Distance(points[i], points[i + 1]);
            float target = t * total;
            for (int i = 0; i < points.Length - 1; i++)
            {
                float length = Vector3.Distance(points[i], points[i + 1]);
                if (target <= length || i == points.Length - 2)
                    return Vector3.Lerp(points[i], points[i + 1], length > 0f ? Mathf.Clamp01(target / length) : 0f);
                target -= length;
            }
            return points[points.Length - 1];
        }
    }
}
