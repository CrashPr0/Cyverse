using System;
using TMPro;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.Settings;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The two-way evidence locker: one pass-through cabinet with a glass window
    /// that stands in the SOC's north wall and, as an identical copy, in the
    /// Forensics Lab's south wall. The rooms are separate scenes, so the locker is
    /// not spatially shared — the player is teleported — but the copies are built
    /// from the same code, with a visual copy of the adjacent room beyond each
    /// window, which reads as the far side of one box.
    ///
    ///  - <see cref="Side.Soc"/>: the player locks the SEIZED DEVICE inside.
    ///  - <see cref="Side.Forensics"/>: the player takes it out again. The locker
    ///    always holds a device (a training unit when the SOC deposit never
    ///    happened, e.g. arriving from the Hub), so the lab can never soft-lock.
    ///
    /// The seized device is a <see cref="Carryable"/>; the locker itself is a plain
    /// <see cref="IInteractable"/> so it can explain each refusal.
    /// </summary>
    public sealed class EvidenceLocker : MonoBehaviour, IInteractable
    {
        public enum Side { Soc, Forensics }

        public const string SeizedDeviceId = "seized_device";
        public const string SeizedDeviceName = "SEIZED DEVICE";

        public static EvidenceLocker Instance { get; private set; }

        /// <summary>SOC side: the device was locked inside.</summary>
        public event Action Sealed;
        /// <summary>Forensics side: the device was taken out.</summary>
        public event Action Retrieved;

        // Serialized (the rest of the state is rebuilt) so a locker baked into a scene by
        // the editor's Build Level tools still knows which side it is on.
        [SerializeField] private Side lockerSide;
        public Side side => lockerSide;

        /// <summary>SOC side, this visit: the seized device is in the locker.</summary>
        public bool IsSealed { get; private set; }

        /// <summary>Forensics side, this visit: the device has been taken out.</summary>
        public bool DeviceRetrieved { get; private set; }

        /// <summary>False only in the Forensics Lab before the device has been
        /// retrieved. Evidence Intake refuses to open until this is true.</summary>
        public static bool DeviceReadyForIntake
        {
            get
            {
                EvidenceLocker locker = Instance;
                return locker == null || locker.side != Side.Forensics || locker.DeviceRetrieved;
            }
        }

        public bool CanInteract => true;

        public string Prompt
        {
            get
            {
                if (side == Side.Soc)
                {
                    if (IsSealed) return "Evidence locker — device sealed";
                    Carryable held = Carryable.Carried;
                    return held != null && held.id == SeizedDeviceId
                        ? "Lock the SEIZED DEVICE in the evidence locker"
                        : "Evidence locker — needs the seized device";
                }
                return DeviceRetrieved
                    ? "Evidence locker — device released"
                    : "Retrieve the device from the evidence locker";
            }
        }

        // Evidence gold, identical in both rooms so the two copies read as one object.
        private static readonly Color Gold = new Color(0.90f, 0.66f, 0.14f);
        private static readonly Color Amber = new Color(1f, 0.62f, 0.16f);
        private static readonly Color Secured = new Color(0.95f, 0.30f, 0.22f);
        private static readonly Color Released = new Color(0.30f, 1f, 0.55f);

        private const float ShelfTopY = 1.04f;
        private static readonly Vector3 ShelfDevicePos = new Vector3(0f, ShelfTopY, -0.36f);
        private static readonly Vector3 WindowCenter = new Vector3(0f, 1.46f, -0.5f);

        private Material statusMat;
        private Color statusColor = Amber;
        private float statusIntensity = 1.6f;
        private bool statusPulses;
        private TextMesh tagLabel;
        private GameObject insideDevice;
        private Light innerLight;
        private Carryable retrievedDevice;

        void Awake()
        {
            Instance = this;
            ResolveReferences();
        }

        void Start()
        {
            // A procedural Build already did this; it is what brings a baked copy to life.
            Transform pane = transform.Find("Glass");
            Renderer paneRenderer = pane != null ? pane.GetComponent<Renderer>() : null;
            if (paneRenderer != null && (paneRenderer.sharedMaterial == null ||
                paneRenderer.sharedMaterial.shader.name != "Cyverse/GlassPane"))
                paneRenderer.sharedMaterial = BuildGlassMaterial();
            SetInsideDeviceVisible(side == Side.Forensics ? !DeviceRetrieved : IsSealed);
            Refresh();
            LockerSightline.Build(transform, side);
        }

        /// <summary>Runtime-only fields do not survive being saved into a scene; recover
        /// them by name (same convention as the other saved-scene stations).</summary>
        private void ResolveReferences()
        {
            if (statusMat == null)
            {
                Transform lamp = transform.Find("StatusLamp");
                Renderer lampRenderer = lamp != null ? lamp.GetComponent<Renderer>() : null;
                if (lampRenderer != null) statusMat = lampRenderer.sharedMaterial;
            }
            if (tagLabel == null)
            {
                Transform tag = transform.Find("Label_EVIDENCE_TAG");
                if (tag != null) tagLabel = tag.GetComponent<TextMesh>();
            }
            if (insideDevice == null)
            {
                Transform device = transform.Find("InsideDevice");
                if (device != null) insideDevice = device.gameObject;
            }
            if (innerLight == null)
            {
                Transform lightTransform = transform.Find("ChamberLight");
                if (lightTransform != null) innerLight = lightTransform.GetComponent<Light>();
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // Steady states are applied once by SetStatus; only the "action needed" amber breathes.
            if (statusMat == null || !statusPulses) return;
            ApplyStatus(AccessibilitySettings.ReduceMotion
                ? 1f
                : 0.62f + 0.38f * Mathf.Sin(Time.time * 3.2f));
        }

        // ---- Interaction -------------------------------------------------------

        public void Interact(GameObject interactor)
        {
            if (side == Side.Soc) InteractSoc();
            else InteractForensics(interactor);
        }

        private void InteractSoc()
        {
            if (IsSealed)
            {
                Toast("The device is sealed in the locker — the Forensics Lab can open it from the other side.",
                    Gold);
                return;
            }

            Carryable held = Carryable.Carried;
            if (held == null)
            {
                Deny("Nothing to lock yet — the seized device appears once the SOC alert is resolved.");
                return;
            }
            if (held.id != SeizedDeviceId)
            {
                Deny("That does not belong in the evidence locker — only the seized device.");
                return;
            }

            held.Consume();
            IsSealed = true;
            SocProgress.MarkEvidenceLocked();
            SetInsideDeviceVisible(true);
            Refresh();
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            BurstFX.Spawn(transform.TransformPoint(WindowCenter), Gold, 28);
            Sealed?.Invoke();
        }

        private void InteractForensics(GameObject interactor)
        {
            if (DeviceRetrieved)
            {
                Toast("The device is already in your custody — take it to EVIDENCE INTAKE.", Gold);
                return;
            }
            if (Carryable.Carried != null)
            {
                Deny("Your hands are full — put the item down (Q) before taking the device.");
                return;
            }

            DeviceRetrieved = true;
            SetInsideDeviceVisible(false);

            retrievedDevice = BuildSeizedDevice(transform.TransformPoint(ShelfDevicePos));
            retrievedDevice.Interact(interactor);
            if (Carryable.Carried != retrievedDevice)
            {
                // No player camera to carry it with: leave it on the floor in front.
                retrievedDevice.transform.position = transform.TransformPoint(new Vector3(0f, 0f, -1.3f));
            }

            Refresh();
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            BurstFX.Spawn(transform.TransformPoint(WindowCenter), Released, 28);
            Retrieved?.Invoke();
        }

        /// <summary>
        /// Evidence Intake takes the device out of the player's hands when the dock
        /// animation starts (the phone seats from where the hands were). Removes the
        /// retrieved device wherever it ended up — carried, or put down somewhere —
        /// so there is never a second phone left over once the dock one appears.
        /// </summary>
        public static void ConsumeCarriedDevice()
        {
            EvidenceLocker locker = Instance;
            if (locker == null || locker.side != Side.Forensics || locker.retrievedDevice == null) return;
            Carryable device = locker.retrievedDevice;
            locker.retrievedDevice = null;
            device.Consume();
        }

        private static void Deny(string message)
        {
            if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            Toast(message, new Color(1f, 0.55f, 0.4f));
        }

        private static void Toast(string message, Color color)
        {
            if (HudUI.Instance != null) HudUI.Instance.ShowToast(message, color);
        }

        // ---- State presentation ------------------------------------------------

        private void SetInsideDeviceVisible(bool visible)
        {
            if (insideDevice != null) insideDevice.SetActive(visible);
        }

        private void SetStatus(Color color, float intensity, bool pulses)
        {
            statusColor = color;
            statusIntensity = intensity;
            statusPulses = pulses;
            ApplyStatus(1f);
            if (innerLight != null) innerLight.color = Color.Lerp(color, Color.white, 0.55f);
        }

        private void ApplyStatus(float pulse)
        {
            if (statusMat == null) return;
            statusMat.color = statusColor;
            statusMat.SetColor("_EmissionColor", statusColor * (statusIntensity * pulse));
        }

        private void Refresh()
        {
            bool hasEvidence = SocProgress.TryGetEvidence(out SocEvidenceRecord evidence);
            string source = hasEvidence && !string.IsNullOrEmpty(evidence.computer)
                ? "CASE " + evidence.computer : "TRAINING UNIT";

            if (side == Side.Soc)
            {
                if (IsSealed)
                {
                    SetStatus(Secured, 1.7f, false);
                    SetTag("EVIDENCE TAG\n" + source + "\nSEALED");
                }
                else
                {
                    SetStatus(Amber, 1.6f, true);
                    SetTag("EVIDENCE TAG\nNO DEVICE YET");
                }
                return;
            }

            if (DeviceRetrieved)
            {
                SetStatus(Released, 1.7f, false);
                SetTag("EVIDENCE TAG\n" + source + "\nRELEASED TO DF");
            }
            else
            {
                SetStatus(Amber, 1.6f, true);
                string seal = !hasEvidence ? "NO SOC DEPOSIT"
                    : SocProgress.HasEvidenceLocked ? "SEALED BY SOC" : "SOC SEAL: NONE";
                SetTag("EVIDENCE TAG\n" + source + "\n" + seal);
            }
        }

        private void SetTag(string text)
        {
            if (tagLabel != null) tagLabel.text = text;
        }

        // ---- Construction ------------------------------------------------------

        /// <summary>
        /// Builds a locker whose back sits on a wall. <paramref name="wallPos"/> is a
        /// point on the wall's inner face at the cabinet's centre; local -Z points into
        /// the room, so yaw 0 suits a north wall and yaw 180 a south wall.
        /// </summary>
        public static EvidenceLocker Build(Vector3 wallPos, float rotY, Side side)
        {
            var root = new GameObject(side == Side.Soc ? "EvidenceLocker_SOC" : "EvidenceLocker_DF");
            root.transform.SetPositionAndRotation(wallPos, Quaternion.Euler(0f, rotY, 0f));
            Transform t = root.transform;

            Material steel = BuildKit.MakeStandard(new Color(0.10f, 0.12f, 0.16f), 0.6f, 0.5f);
            Material steelLight = BuildKit.MakeStandard(new Color(0.34f, 0.38f, 0.44f), 0.5f, 0.7f);
            Material dark = BuildKit.MakeStandard(new Color(0.03f, 0.04f, 0.055f), 0.5f, 0.3f);
            Material accentLit = BuildKit.MakeEmissive(Gold, 1.3f);
            Material accentSoft = BuildKit.MakeEmissive(Gold, 0.55f);
            Material paper = BuildKit.MakeEmissive(new Color(0.78f, 0.72f, 0.52f), 0.4f);
            Material status = BuildKit.MakeEmissive(Amber, 1.6f);

            // Housing: plinth + drawer, window cheeks, header band. Visual parts carry no
            // colliders; one blocker on the root keeps the player out of the cabinet.
            Part(t, PrimitiveType.Cube, "Plinth", new Vector3(0f, 0.47f, -0.31f),
                new Vector3(1.4f, 0.94f, 0.62f), steel);
            Part(t, PrimitiveType.Cube, "Cheek_L", new Vector3(-0.65f, 1.46f, -0.31f),
                new Vector3(0.1f, 1.06f, 0.62f), steel);
            Part(t, PrimitiveType.Cube, "Cheek_R", new Vector3(0.65f, 1.46f, -0.31f),
                new Vector3(0.1f, 1.06f, 0.62f), steel);
            Part(t, PrimitiveType.Cube, "Header", new Vector3(0f, 2.2f, -0.31f),
                new Vector3(1.4f, 0.4f, 0.62f), steel);
            Part(t, PrimitiveType.Cube, "HeaderInset", new Vector3(0f, 2.2f, -0.626f),
                new Vector3(1.28f, 0.3f, 0.01f), dark);
            Part(t, PrimitiveType.Cube, "Edge_L", new Vector3(-0.69f, 1.2f, -0.63f),
                new Vector3(0.025f, 2.4f, 0.02f), accentLit);
            Part(t, PrimitiveType.Cube, "Edge_R", new Vector3(0.69f, 1.2f, -0.63f),
                new Vector3(0.025f, 2.4f, 0.02f), accentLit);

            // Drawer on this side of the locker: panel, handle on two posts, keypad.
            Part(t, PrimitiveType.Cube, "DrawerPanel", new Vector3(0f, 0.50f, -0.63f),
                new Vector3(1.26f, 0.76f, 0.02f), BuildKit.MakeStandard(new Color(0.14f, 0.16f, 0.21f), 0.55f, 0.55f));
            Part(t, PrimitiveType.Cube, "DrawerSeam", new Vector3(0f, 0.885f, -0.642f),
                new Vector3(1.26f, 0.012f, 0.004f), accentLit);
            Part(t, PrimitiveType.Cube, "Handle", new Vector3(0f, 0.80f, -0.68f),
                new Vector3(0.56f, 0.045f, 0.05f), steelLight);
            Part(t, PrimitiveType.Cube, "HandlePost_L", new Vector3(-0.22f, 0.80f, -0.65f),
                new Vector3(0.03f, 0.05f, 0.04f), steelLight);
            Part(t, PrimitiveType.Cube, "HandlePost_R", new Vector3(0.22f, 0.80f, -0.65f),
                new Vector3(0.03f, 0.05f, 0.04f), steelLight);
            Part(t, PrimitiveType.Cube, "Keypad", new Vector3(0.40f, 0.43f, -0.645f),
                new Vector3(0.28f, 0.34f, 0.02f), dark);
            Part(t, PrimitiveType.Cube, "KeypadDisplay", new Vector3(0.40f, 0.52f, -0.657f),
                new Vector3(0.22f, 0.07f, 0.004f), accentSoft);
            Part(t, PrimitiveType.Cube, "LockLamp", new Vector3(0.40f, 0.34f, -0.657f),
                new Vector3(0.07f, 0.07f, 0.02f), status);

            // Evidence tag hanging off the handle.
            Part(t, PrimitiveType.Cylinder, "TagString", new Vector3(-0.22f, 0.705f, -0.66f),
                new Vector3(0.008f, 0.075f, 0.008f), dark);
            Part(t, PrimitiveType.Cube, "TagCard", new Vector3(-0.30f, 0.46f, -0.645f),
                new Vector3(0.56f, 0.34f, 0.01f), paper);
            TextMesh tag = BuildKit.MakeLabel(t, new Vector3(-0.30f, 0.46f, -0.655f), "EVIDENCE TAG",
                new Color(0.10f, 0.09f, 0.06f), 0.0095f);

            // Framed glass window onto the chamber.
            Part(t, PrimitiveType.Cube, "Sill", new Vector3(0f, 0.975f, -0.60f),
                new Vector3(1.2f, 0.07f, 0.09f), steel);
            Part(t, PrimitiveType.Cube, "FrameTop", new Vector3(0f, 1.945f, -0.60f),
                new Vector3(1.2f, 0.07f, 0.09f), steel);
            Part(t, PrimitiveType.Cube, "FrameLeft", new Vector3(-0.565f, 1.46f, -0.60f),
                new Vector3(0.07f, 0.97f, 0.09f), steel);
            Part(t, PrimitiveType.Cube, "FrameRight", new Vector3(0.565f, 1.46f, -0.60f),
                new Vector3(0.07f, 0.97f, 0.09f), steel);
            Part(t, PrimitiveType.Cube, "FrameGlow_L", new Vector3(-0.525f, 1.46f, -0.652f),
                new Vector3(0.012f, 0.88f, 0.008f), accentLit);
            Part(t, PrimitiveType.Cube, "FrameGlow_R", new Vector3(0.525f, 1.46f, -0.652f),
                new Vector3(0.012f, 0.88f, 0.008f), accentLit);
            Part(t, PrimitiveType.Cube, "FrameGlow_Bottom", new Vector3(0f, 1.015f, -0.652f),
                new Vector3(1.04f, 0.012f, 0.008f), accentLit);
            // The status bar over the window shares the lamp's material, so the whole
            // locker changes colour when the state does.
            Part(t, PrimitiveType.Cube, "StatusBar", new Vector3(0f, 1.992f, -0.648f),
                new Vector3(1.2f, 0.02f, 0.02f), status);
            Part(t, PrimitiveType.Cube, "StatusLamp", new Vector3(0.54f, 2.2f, -0.648f),
                new Vector3(0.14f, 0.14f, 0.05f), status);

            // Chamber interior: shelf and liners. There is no back hatch: the chamber
            // runs on through the wall into the other room (LockerSightline).
            Part(t, PrimitiveType.Cube, "Shelf", new Vector3(0f, ShelfTopY - 0.015f, -0.36f),
                new Vector3(0.96f, 0.03f, 0.44f), dark);
            Part(t, PrimitiveType.Cube, "ShelfEdge", new Vector3(0f, ShelfTopY + 0.004f, -0.578f),
                new Vector3(0.96f, 0.008f, 0.006f), accentLit);
            Part(t, PrimitiveType.Cube, "Liner_L", new Vector3(-0.48f, 1.47f, -0.35f),
                new Vector3(0.02f, 0.90f, 0.44f), dark);
            Part(t, PrimitiveType.Cube, "Liner_R", new Vector3(0.48f, 1.47f, -0.35f),
                new Vector3(0.02f, 0.90f, 0.44f), dark);
            Part(t, PrimitiveType.Cube, "Liner_Top", new Vector3(0f, 1.91f, -0.35f),
                new Vector3(0.96f, 0.02f, 0.44f), dark);

            var glassGo = BuildKit.SpawnLocal(PrimitiveType.Quad, "Glass", t,
                new Vector3(0f, 1.46f, -0.585f), Vector3.zero, new Vector3(0.95f, 0.84f, 1f),
                BuildGlassMaterial(), collider: false);
            glassGo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = new GameObject("ChamberLight");
            lightGo.transform.SetParent(t, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.8f, -0.45f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.Lerp(Amber, Color.white, 0.55f);
            light.range = 2.4f;
            light.intensity = 0.9f;
            light.shadows = LightShadows.None;

            // The device sits on the shelf. Hidden until something is inside.
            Transform phone = BuildPhoneModel(t, baseCollider: false);
            phone.name = "InsideDevice";
            phone.localPosition = ShelfDevicePos;

            // Header text (TMP auto-sizes so the line never spills past the band).
            var headerGo = new GameObject("LockerHeader", typeof(TextMeshPro));
            headerGo.transform.SetParent(t, false);
            headerGo.transform.localPosition = new Vector3(-0.08f, 2.2f, -0.64f);
            headerGo.transform.localRotation = Quaternion.identity;
            headerGo.transform.localScale = Vector3.one * 0.03f;
            TextMeshPro header = headerGo.GetComponent<TextMeshPro>();
            header.font = TMP_Settings.defaultFontAsset;
            header.text = "<size=62%><color=#E5A823>PASS-THROUGH LOCKER</color></size>\nSOC  /  DF LAB";
            header.alignment = TextAlignmentOptions.Center;
            header.fontStyle = FontStyles.Bold;
            header.color = new Color(0.82f, 0.93f, 1f);
            header.enableWordWrapping = false;
            header.enableAutoSizing = true;
            header.fontSizeMin = 10f;
            header.fontSizeMax = 28f;
            header.overflowMode = TextOverflowModes.Ellipsis;
            header.rectTransform.sizeDelta = new Vector2(34f, 11f);
            WorldTextLayoutIntent.Configure(headerGo, WorldTextLayoutIntent.Mode.Mounted);

            // Floor strips frame the spot to stand on.
            Part(t, PrimitiveType.Cube, "FloorStrip_Front", new Vector3(0f, 0.011f, -0.98f),
                new Vector3(1.4f, 0.006f, 0.03f), accentLit);
            Part(t, PrimitiveType.Cube, "FloorStrip_L", new Vector3(-0.685f, 0.011f, -0.80f),
                new Vector3(0.03f, 0.006f, 0.40f), accentLit);
            Part(t, PrimitiveType.Cube, "FloorStrip_R", new Vector3(0.685f, 0.011f, -0.80f),
                new Vector3(0.03f, 0.006f, 0.40f), accentLit);

            // Aim trigger first (so GetComponent<BoxCollider> finds it, like every other
            // station), then the solid blocker.
            var aim = root.AddComponent<BoxCollider>();
            aim.isTrigger = true;
            aim.center = new Vector3(0f, 1.3f, -0.5f);
            aim.size = new Vector3(1.8f, 2.6f, 1.1f);
            var blocker = root.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, 1.2f, -0.31f);
            blocker.size = new Vector3(1.4f, 2.4f, 0.62f);

            var locker = root.AddComponent<EvidenceLocker>();
            locker.lockerSide = side;
            locker.statusMat = status;
            locker.tagLabel = tag;
            locker.insideDevice = phone.gameObject;
            locker.innerLight = light;
            Instance = locker;

            // SOC copy starts empty; the Forensics copy always starts loaded.
            locker.SetInsideDeviceVisible(side == Side.Forensics);
            locker.Refresh();

            // Raised above the default sign priority: the cert-exam and door signs share this
            // part of the view from the south, and this one is the handoff the player needs.
            GameObject sign = BuildKit.MakeSign(t, root.transform.TransformPoint(new Vector3(0f, 3.0f, -0.3f)),
                "EVIDENCE LOCKER", Gold, 0.03f);
            WorldTextLayoutIntent.Configure(sign, WorldTextLayoutIntent.Mode.Floating, 260);

            // Open the chamber through the wall onto a copy of the other room.
            LockerSightline.Build(root.transform, side);
            return locker;
        }

        /// <summary>The carryable SEIZED DEVICE: a phone leaning in an evidence tray with
        /// an amber tag. Pivot is the bottom of the tray, so it rests on any surface.</summary>
        public static Carryable BuildSeizedDevice(Vector3 position)
        {
            var root = new GameObject("Carryable_" + SeizedDeviceId);
            root.transform.position = position;
            BuildPhoneModel(root.transform, baseCollider: true);

            TextMesh label = BuildKit.MakeLabel(root.transform, new Vector3(0f, 0.55f, 0f),
                SeizedDeviceName, Gold, 0.022f, billboard: true);
            WorldTextLayoutIntent.Configure(label.gameObject,
                WorldTextLayoutIntent.Mode.InteractionCritical, 400);

            // Same aim-helper reasoning as Carryable.Build: the model is small and the
            // interact ray travels level at eye height.
            BuildKit.AddAimCollider(root, height: 1.1f, width: 0.7f);

            var carry = root.AddComponent<Carryable>();
            carry.itemName = SeizedDeviceName;
            carry.id = SeizedDeviceId;
            return carry;
        }

        private static Transform BuildPhoneModel(Transform parent, bool baseCollider)
        {
            var group = new GameObject("PhoneModel").transform;
            group.SetParent(parent, false);

            Material tray = BuildKit.MakeStandard(new Color(0.07f, 0.08f, 0.10f), 0.62f, 0.55f);
            Material shell = BuildKit.MakeStandard(new Color(0.035f, 0.04f, 0.05f), 0.78f, 0.35f);
            Material screen = BuildKit.MakeEmissive(new Color(0.06f, 0.22f, 0.17f), 0.9f);
            Material tagLit = BuildKit.MakeEmissive(Amber, 1.2f);

            BuildKit.SpawnLocal(PrimitiveType.Cube, "Tray", group, new Vector3(0f, 0.0125f, 0f),
                Vector3.zero, new Vector3(0.20f, 0.025f, 0.15f), tray, collider: baseCollider);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "TrayLip", group, new Vector3(0f, 0.032f, -0.055f),
                Vector3.zero, new Vector3(0.14f, 0.02f, 0.012f), tray, collider: false);

            // Leans back 14 degrees; centre = bottom edge + half a phone along the lean.
            const float lean = 14f;
            Quaternion q = Quaternion.Euler(lean, 0f, 0f);
            Vector3 up = q * Vector3.up;
            Vector3 centre = new Vector3(0f, 0.027f, -0.02f) + up * 0.115f;
            Transform phone = BuildKit.SpawnLocal(PrimitiveType.Cube, "Shell", group, centre,
                new Vector3(lean, 0f, 0f), new Vector3(0.12f, 0.23f, 0.018f), shell, collider: false).transform;
            // Quads render on their local -Z face, which is the phone's front.
            BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", phone, new Vector3(0f, 0.002f, -0.0095f),
                Vector3.zero, new Vector3(0.104f, 0.21f, 1f), screen, collider: false);

            // Evidence tag clipped to the tray's front corner.
            BuildKit.SpawnLocal(PrimitiveType.Cube, "EvidenceTag", group, new Vector3(0.085f, 0.04f, -0.085f),
                new Vector3(0f, -12f, 0f), new Vector3(0.05f, 0.075f, 0.004f), tagLit, collider: false);
            return group;
        }

        internal static Material BuildGlassMaterial()
        {
            // The Resources asset keeps the pane shader in release WebGL
            // builds. Its tint, bevel and static sheen leave the sightline clear.
            Shader shader = Resources.Load<Shader>("Shaders/GlassPane");
            return new Material(shader) { name = "EvidenceLockerGlass" };
        }

        private static void Part(Transform parent, PrimitiveType type, string name, Vector3 localPos,
            Vector3 scale, Material mat)
        {
            BuildKit.SpawnLocal(type, name, parent, localPos, Vector3.zero, scale, mat, collider: false);
        }
    }
}
