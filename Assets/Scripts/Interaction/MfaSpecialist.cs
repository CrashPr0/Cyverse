using System;
using System.Collections.Generic;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Dialogue;
using Cyverse.Level;
using Cyverse.Settings;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The MFA Specialist robot beside the Level 1 vault. Talking to her plays
    /// the recorded MFA explainer, and the vault's factor stations stay locked
    /// until the player has. While she speaks her equalizer mouth and chest
    /// core follow the recording's loudness, she gestures, and the hologram at
    /// her side lights each factor (something you know / have / are) as she
    /// reaches it.
    /// </summary>
    public sealed class MfaSpecialist : MonoBehaviour, IInteractable
    {
        public const string DisplayName = "MFA Specialist";

        public static MfaSpecialist Instance { get; private set; }

        /// <summary>True once the player has talked to her — or when the scene
        /// has no specialist, so such a scene never locks the vault.</summary>
        public static bool BriefedInScene => Instance == null || Instance.Briefed;

        public bool Briefed { get; private set; }
        public event Action BriefingStarted;

        /// <summary>Runtime-only: the lines she speaks. Unity does not serialize delegates.</summary>
        [NonSerialized] public Func<List<DialogueLine>> LinesProvider;

        public string Prompt => Briefed ? "Hear the MFA briefing again" : $"Talk to the {DisplayName}";
        public bool CanInteract => !Talking;

        /// <summary>One of her lines is playing.</summary>
        public bool Talking => lineIndex >= 0 && DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying;

        // Factor lines in the recorded briefing: 0-based indices of "something
        // you know / have / are", lighting hologram tiles 0, 1, 2.
        private const int FirstFactorLine = 2;
        private const float EnvelopeRate = 30f;
        private const float FaceRange = 7f;
        private static readonly Vector3 HologramPos = new Vector3(0.58f, 1.22f, 0.18f);

        private static readonly Color IamBlue = new Color(0.30f, 0.62f, 1f);

        private ProceduralRobot.Rig rig;
        private List<DialogueLine> lines;
        private int lineIndex = -1;
        private DialogueManager subscribed;

        private AudioClip lineClip;
        private byte[] envelope;
        private float lineStart;
        private float mouthOpen, envFast, envSlow, nod, browLift;

        private float blinkTimer = 2f, blinkPhase = -1f;
        private Vector2 eyeOffset;
        private float gestureTimer;
        private float gestureHold = 2f;
        private int pointAt = -1;
        private Vector3[] talkPose = RestPose;
        private readonly Vector3[] armNow = (Vector3[])RestPose.Clone();
        private readonly Vector3[] armTarget = (Vector3[])RestPose.Clone();
        private float seed;

        private Transform hologram;
        private readonly Transform[] tiles = new Transform[3];
        private readonly Material[] tilePanels = new Material[3];
        private readonly List<Material>[] tileIcons = { new List<Material>(), new List<Material>(), new List<Material>() };
        private readonly List<TextMesh>[] tileText = { new List<TextMesh>(), new List<TextMesh>(), new List<TextMesh>() };
        private readonly bool[] tileLit = new bool[3];
        private readonly float[] tileGlow = new float[3];
        private readonly float[] tilePop = new float[3];

        // Arm poses for the right arm: shoulder Euler angles (negative x swings
        // the arm forward, positive z out to her side), elbow (x = bend, y =
        // forearm twist; +90 turns the right palm up), wrist Euler angles.
        private static readonly Vector3[] RestPose = { new Vector3(-4f, 0f, 6f), new Vector3(-12f, 0f, 0f), Vector3.zero };
        private static readonly Vector3[][] TalkPoses =
        {
            new[] { new Vector3(-22f, 8f, 10f), new Vector3(-78f, 70f, 0f), new Vector3(0f, 0f, -10f) },   // explain, palm up
            new[] { new Vector3(-16f, 0f, 8f), new Vector3(-95f, 20f, 0f), new Vector3(10f, 0f, 0f) },     // emphasise
            new[] { new Vector3(-30f, -10f, 22f), new Vector3(-60f, 85f, 0f), new Vector3(-10f, 0f, -15f) }, // open hand
            RestPose,
        };
        private static readonly Vector3[] PointPose =
            { new Vector3(-50f, 0f, 38f), new Vector3(-25f, 60f, 0f), new Vector3(0f, 0f, -5f) };
        // Left arm: forearm level and turned palm-up, cradling the tablet; the
        // wrist dips it 14 degrees toward the player.
        private static readonly Vector3[] TabletPose =
            { new Vector3(-10f, 14f, -6f), new Vector3(-88f, -90f, 0f), new Vector3(0f, 0f, -14f) };

        // ---- Construction ----------------------------------------------------

        public static MfaSpecialist Build(Vector3 position, float rotY, Func<List<DialogueLine>> linesProvider)
        {
            var root = new GameObject("MfaSpecialist");
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var specialist = root.AddComponent<MfaSpecialist>();
            specialist.LinesProvider = linesProvider;
            specialist.rig = ProceduralRobot.Build(root.transform, IamBlue);
            specialist.BuildTablet();
            specialist.BuildHologram();
            specialist.BuildLights();

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.86f, 0f);
            col.height = 1.72f;
            col.radius = 0.3f;

            BuildKit.MakeSign(root.transform, position + new Vector3(0f, 2.04f, 0f), "MFA SPECIALIST", IamBlue, 0.022f);
            specialist.ApplyArms(1f);
            return specialist;
        }

        /// <summary>A tablet resting on her upturned left palm, screen up. Built in wrist space: with the forearm
        /// turned palm-up, wrist +X points up and -Y runs along the forearm.</summary>
        private void BuildTablet()
        {
            var tablet = new GameObject("Tablet").transform;
            tablet.SetParent(rig.wristL, false);
            Quaternion lay = Quaternion.LookRotation(Vector3.right, Vector3.down); // screen up, length forward
            // Flat on the palm; the wrist (TabletPose) tips hand and tablet
            // together toward the player so the screen's glow shows.
            tablet.localRotation = lay;
            tablet.localPosition = new Vector3(0.031f, -0.085f, 0.004f);
            Material bezel = BuildKit.MakeStandard(new Color(0.06f, 0.065f, 0.08f), 0.7f, 0.4f);
            Material screen = BuildKit.MakeEmissive(new Color(0.06f, 0.18f, 0.34f), 1.4f);
            Material ui = BuildKit.MakeEmissive(IamBlue, 1.8f);
            Box("Bezel", tablet, Vector3.zero, new Vector3(0.150f, 0.215f, 0.009f), bezel);
            Box("Screen", tablet, new Vector3(0f, 0f, 0.0048f), new Vector3(0.134f, 0.192f, 0.001f), screen);
            for (int i = 0; i < 3; i++)
                Box("Row", tablet, new Vector3(0.012f, 0.055f - i * 0.042f, 0.0055f),
                    new Vector3(0.082f, 0.011f, 0.001f), ui);
            for (int i = 0; i < 3; i++)
                Box("Dot", tablet, new Vector3(-0.047f, 0.055f - i * 0.042f, 0.0055f),
                    new Vector3(0.016f, 0.016f, 0.001f), ui);
        }

        /// <summary>Three stacked holographic tiles at her right side: a
        /// password field, a phone, a fingerprint. The hologram is turned to
        /// face the player, so its contents are authored as seen from its
        /// local -Z (world text reads correctly from that side): +X is the
        /// player's right and "toward the player" is -Z.</summary>
        private void BuildHologram()
        {
            hologram = new GameObject("FactorHologram").transform;
            hologram.SetParent(transform, false);
            hologram.localPosition = HologramPos;
            hologram.localRotation = Quaternion.Euler(0f, 160f, 0f);

            TextMesh title = BuildKit.MakeLabel(hologram, new Vector3(0f, 0.475f, 0f), "MULTI-FACTOR AUTHENTICATION",
                new Color(IamBlue.r, IamBlue.g, IamBlue.b, 0.85f), 0.0042f);
            title.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            string[] words = { "KNOW", "HAVE", "ARE" };
            for (int i = 0; i < 3; i++)
            {
                var tile = new GameObject("Factor_" + words[i]).transform;
                tile.SetParent(hologram, false);
                tile.localPosition = new Vector3(0f, 0.36f - i * 0.165f, 0f);
                tiles[i] = tile;

                var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                panel.name = "Panel";
                BuildKit.StripCollider(panel);
                panel.transform.SetParent(tile, false);
                panel.transform.localScale = new Vector3(0.25f, 0.14f, 1f);
                tilePanels[i] = BuildKit.MakeHologram(IamBlue);
                panel.GetComponent<Renderer>().sharedMaterial = tilePanels[i];

                Material icon = BuildKit.MakeEmissive(IamBlue, 1f);
                tileIcons[i].Add(icon);
                BuildIcon(i, tile, icon);

                tileText[i].Add(BuildKit.MakeLabel(tile, new Vector3(0.045f, 0.022f, -0.004f), "SOMETHING YOU",
                    Color.white, 0.0032f));
                tileText[i].Add(BuildKit.MakeLabel(tile, new Vector3(0.045f, -0.016f, -0.004f), words[i],
                    Color.white, 0.0075f));
            }
            for (int i = 0; i < 3; i++) RefreshTile(i);
        }

        private void BuildIcon(int factor, Transform tile, Material mat)
        {
            var icon = new GameObject("Icon").transform;
            icon.SetParent(tile, false);
            icon.localPosition = new Vector3(-0.075f, 0f, -0.004f);
            switch (factor)
            {
                case 0: // a password field: ****
                    Box("Field", icon, Vector3.zero, new Vector3(0.078f, 0.030f, 0.002f), mat);
                    Box("FieldInner", icon, new Vector3(0f, 0f, -0.0015f),
                        new Vector3(0.072f, 0.024f, 0.001f), BuildKit.MakeStandard(new Color(0.02f, 0.05f, 0.09f), 0.3f, 0f));
                    for (int d = 0; d < 4; d++)
                        Dot(icon, new Vector3(-0.024f + d * 0.016f, 0f, -0.0028f), 0.0085f, mat);
                    break;
                case 1: // a phone showing a one-time code
                    Box("Phone", icon, Vector3.zero, new Vector3(0.036f, 0.064f, 0.004f), mat);
                    Box("PhoneScreen", icon, new Vector3(0f, 0.002f, -0.0025f), new Vector3(0.028f, 0.048f, 0.001f),
                        BuildKit.MakeStandard(new Color(0.02f, 0.05f, 0.09f), 0.3f, 0f));
                    for (int d = 0; d < 3; d++)
                        Box("Code", icon, new Vector3(-0.008f + d * 0.008f, 0.004f, -0.0035f),
                            new Vector3(0.005f, 0.010f, 0.001f), mat);
                    break;
                default: // a fingerprint: nested ridges, open at the bottom
                    for (int r = 0; r < 4; r++)
                    {
                        float inner = 0.006f + r * 0.0072f;
                        var ridge = new GameObject("Ridge", typeof(MeshFilter), typeof(MeshRenderer));
                        ridge.transform.SetParent(icon, false);
                        ridge.GetComponent<MeshFilter>().sharedMesh =
                            CharacterMesh.Arc(inner, inner + 0.0034f, -35f + r * 4f, 215f - r * 4f, 20, "Ridge");
                        ridge.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    }
                    break;
            }
        }

        private void BuildLights()
        {
            // Rooms are lit for stations, not faces: a soft key from the front
            // keeps her features readable, a blue rim separates her from the wall.
            AddLight(new Vector3(0.25f, 1.98f, 1.05f), new Color(1f, 0.93f, 0.86f), 3.2f, 1.25f);
            AddLight(new Vector3(-0.2f, 1.84f, -0.8f), new Color(0.45f, 0.70f, 1f), 2.6f, 0.9f);
        }

        // ---- Interaction -----------------------------------------------------

        public void Interact(GameObject interactor)
        {
            if (Talking || DialogueManager.Instance == null) return;
            lines = LinesProvider != null ? LinesProvider() : new List<DialogueLine>();
            bool first = !Briefed;
            Briefed = true;
            EnsureSubscribed();
            DialogueManager.Instance.Play(lines, OnBriefingFinished);
            if (first) BriefingStarted?.Invoke();
        }

        private void OnBriefingFinished()
        {
            lineIndex = -1;
            for (int i = 0; i < 3; i++) LightTile(i);
        }

        private void EnsureSubscribed()
        {
            DialogueManager dm = DialogueManager.Instance;
            if (dm == subscribed) return;
            if (subscribed != null) subscribed.LineStarted -= OnLineStarted;
            subscribed = dm;
            if (subscribed != null) subscribed.LineStarted += OnLineStarted;
        }

        private void OnLineStarted(DialogueLine line)
        {
            lineIndex = lines != null ? lines.IndexOf(line) : -1;
            if (lineIndex < 0) { lineClip = null; envelope = null; return; }
            lineClip = line.clip;
            envelope = lineClip != null ? Narration.Envelope(lineClip.name) : null;
            lineStart = Time.unscaledTime;
            int factor = lineIndex - FirstFactorLine;
            if (factor >= 0 && factor < 3)
            {
                LightTile(factor);
                pointAt = factor;
                gestureTimer = 0f;
            }
        }

        // ---- Animation -------------------------------------------------------

        private void Awake()
        {
            Instance = this;
            seed = UnityEngine.Random.Range(0f, 100f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (subscribed != null) subscribed.LineStarted -= OnLineStarted;
        }

        private void Update()
        {
            if (rig == null) return;
            EnsureSubscribed();
            if (lineIndex >= 0 && (DialogueManager.Instance == null || !DialogueManager.Instance.IsPlaying))
                OnBriefingFinished(); // stopped or replaced without finishing

            float dt = Time.deltaTime;
            bool calm = AccessibilitySettings.ReduceMotion;
            FacePlayer(dt);
            Look(dt);
            Blink(dt);
            Idle(calm);
            Talk(dt);
            Gesture(dt, calm);
            ApplyArms(1f - Mathf.Exp(-5f * dt));
            AnimateHologram(dt);
        }

        private void FacePlayer(float dt)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 to = cam.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > FaceRange * FaceRange || to.sqrMagnitude < 0.04f) return;
            float delta = Vector3.SignedAngle(transform.forward, to, Vector3.up);
            // Hold still for small offsets (the head covers those); turn the body
            // when the player walks round her, or always while she's talking.
            if (Mathf.Abs(delta) < (Talking ? 6f : 28f)) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to),
                1f - Mathf.Exp(-2.4f * dt));
        }

        private void Look(float dt)
        {
            Camera cam = Camera.main;
            float yaw = 0f, pitch = 0f;
            if (cam != null)
            {
                Vector3 local = transform.InverseTransformPoint(cam.transform.position) - new Vector3(0f, 1.55f, 0f);
                if (local.z > -0.2f && local.magnitude < 9f)
                {
                    yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -55f, 55f);
                    pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -22f, 22f);
                }
            }
            if (pointAt >= 0 && gestureTimer > 0.6f) { yaw = 38f; pitch = 4f; } // glance at the tile she points to

            float k = 1f - Mathf.Exp(-6f * dt);
            float tilt = Mathf.Sin((Time.time + seed) * 0.37f) * 2.5f;
            rig.neck.localRotation = Quaternion.Slerp(rig.neck.localRotation,
                Quaternion.Euler(pitch * 0.35f, yaw * 0.35f, 0f), k);
            rig.head.localRotation = Quaternion.Slerp(rig.head.localRotation,
                Quaternion.Euler(pitch * 0.65f + nod, yaw * 0.65f, tilt), k);

            // The eye lights slide across the visor toward what she looks at.
            Vector2 glance = Vector2.zero;
            if (cam != null)
            {
                Vector3 dir = rig.head.InverseTransformPoint(cam.transform.position) - new Vector3(0f, 0.11f, 0.1f);
                if (dir.z > 0.05f)
                    glance = new Vector2(Mathf.Clamp(dir.x / dir.z, -1f, 1f) * 0.007f,
                                         Mathf.Clamp(dir.y / dir.z, -1f, 1f) * 0.004f);
            }
            eyeOffset = Vector2.Lerp(eyeOffset, glance, k);
            rig.eyeL.localPosition = rig.eyeLRest + (Vector3)eyeOffset;
            rig.eyeR.localPosition = rig.eyeRRest + (Vector3)eyeOffset;
        }

        private void Blink(float dt)
        {
            if (blinkPhase < 0f)
            {
                blinkTimer -= dt;
                if (blinkTimer <= 0f)
                {
                    blinkPhase = 0f;
                    // Now and then a quick double blink.
                    blinkTimer = UnityEngine.Random.value < 0.18f ? 0.22f : UnityEngine.Random.Range(2.4f, 5.6f);
                }
            }
            float closed = 0f;
            if (blinkPhase >= 0f)
            {
                blinkPhase += dt / 0.15f;
                closed = Mathf.Sin(Mathf.Clamp01(blinkPhase) * Mathf.PI);
                if (blinkPhase >= 1f) blinkPhase = -1f;
            }
            Vector3 eye = new Vector3(rig.eyeScale.x, rig.eyeScale.y * Mathf.Lerp(1f, 0.12f, closed), rig.eyeScale.z);
            rig.eyeL.localScale = eye;
            rig.eyeR.localScale = eye;
        }

        private void Idle(bool calm)
        {
            float t = Time.time + seed;
            float breath = Mathf.Sin(t * Mathf.PI * 2f * 0.23f) * (calm ? 0.3f : 1f);
            rig.chest.localScale = new Vector3(1f, 1f + 0.004f * breath, 1f + 0.011f * breath);
            float sway = calm ? 0f : 1f;
            rig.hips.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.31f) * 1.2f * sway, Mathf.Sin(t * 0.23f) * 0.8f * sway);
        }

        private void Talk(float dt)
        {
            float target = 0f;
            bool voiced = Talking && DialogueManager.Instance.SpeakingClip != null &&
                          DialogueManager.Instance.SpeakingClip == lineClip;
            if (voiced && envelope != null && envelope.Length > 1)
            {
                float f = (Time.unscaledTime - lineStart) * EnvelopeRate;
                int i = Mathf.Clamp((int)f, 0, envelope.Length - 2);
                target = Mathf.Lerp(envelope[i], envelope[i + 1], f - i) / 255f;
            }
            else if (Talking && lineClip == null)
            {
                // No recording (text-to-speech fallback): a plausible flap.
                target = Mathf.Clamp01(Mathf.PerlinNoise(Time.time * 9f, seed) * 1.4f - 0.3f);
            }

            mouthOpen += (target - mouthOpen) * (1f - Mathf.Exp((target > mouthOpen ? -30f : -14f) * dt));
            envFast += (target - envFast) * (1f - Mathf.Exp(-12f * dt));
            envSlow += (target - envSlow) * (1f - Mathf.Exp(-1.5f * dt));
            float emphasis = Mathf.Clamp01((envFast - envSlow) * 2.5f);
            nod += (emphasis * 6f - nod) * (1f - Mathf.Exp(-8f * dt));
            browLift += (emphasis * 0.0035f - browLift) * (1f - Mathf.Exp(-10f * dt));

            float open = Mathf.Clamp01(mouthOpen * 1.15f);
            // Equalizer mouth: centre bars tallest, each with its own flicker so
            // the line never moves as one block.
            float t = Time.time;
            for (int i = 0; i < rig.mouthBars.Length; i++)
            {
                float profile = 1f - Mathf.Abs(i - 2) * 0.22f;
                float flicker = 0.7f + 0.3f * Mathf.PerlinNoise(t * 11f + i * 3.1f, seed);
                Vector3 sc = rig.mouthBars[i].localScale;
                sc.y = 0.004f + open * 0.026f * profile * flicker;
                rig.mouthBars[i].localScale = sc;
            }
            rig.browL.localPosition = rig.browLRest + Vector3.up * browLift;
            rig.browR.localPosition = rig.browRRest + Vector3.up * browLift;

            // The chest core breathes slowly at rest and brightens with her voice.
            float idle = 0.5f + 0.5f * Mathf.Sin((t + seed) * 1.6f);
            rig.core.SetColor("_EmissionColor", IamBlue * (1.3f + idle * 0.4f + open * 2.4f));
            rig.antenna.SetColor("_EmissionColor", IamBlue * (Talking ? 1.4f + open * 3f : 0.9f + idle * 0.5f));
        }

        private void Gesture(float dt, bool calm)
        {
            Vector3[] pose = RestPose;
            if (Talking && !calm)
            {
                gestureTimer += dt;
                if (pointAt >= 0)
                {
                    pose = PointPose;
                    if (gestureTimer > 2.0f) { pointAt = -1; gestureTimer = 0f; NextTalkPose(); }
                }
                else
                {
                    if (gestureTimer > gestureHold) NextTalkPose();
                    pose = talkPose;
                }
            }
            else
            {
                pointAt = -1;
            }
            for (int j = 0; j < 3; j++) armTarget[j] = pose[j];
            // Beats: the forearm lifts a little on stressed syllables.
            if (Talking && !calm) armTarget[1] += new Vector3(-envFast * 10f, 0f, 0f);
        }

        private void NextTalkPose()
        {
            gestureTimer = 0f;
            Vector3[] next;
            do next = TalkPoses[UnityEngine.Random.Range(0, TalkPoses.Length)]; while (next == talkPose);
            talkPose = next;
            gestureHold = UnityEngine.Random.Range(1.4f, 2.8f);
        }

        private void ApplyArms(float k)
        {
            if (rig == null) return;
            for (int j = 0; j < 3; j++) armNow[j] = Vector3.Lerp(armNow[j], armTarget[j], k);
            rig.shoulderR.localRotation = Quaternion.Euler(armNow[0]);
            rig.elbowR.localRotation = Elbow(armNow[1]);
            rig.wristR.localRotation = Quaternion.Euler(armNow[2]);
            rig.shoulderL.localRotation = Quaternion.Euler(TabletPose[0]);
            rig.elbowL.localRotation = Elbow(TabletPose[1]);
            rig.wristL.localRotation = Quaternion.Euler(TabletPose[2]);
        }

        /// <summary>Bend the elbow, then twist the forearm about its own axis.</summary>
        private static Quaternion Elbow(Vector3 pose) =>
            Quaternion.Euler(pose.x, 0f, 0f) * Quaternion.Euler(0f, pose.y, 0f);

        // ---- Hologram --------------------------------------------------------

        private void LightTile(int tile)
        {
            if (tile < 0 || tile >= 3 || tileLit[tile]) return;
            tileLit[tile] = true;
            tilePop[tile] = 1f;
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
        }

        private void AnimateHologram(float dt)
        {
            if (hologram == null) return;
            float bob = AccessibilitySettings.ReduceMotion ? 0f : Mathf.Sin((Time.time + seed) * 1.3f) * 0.006f;
            hologram.localPosition = HologramPos + Vector3.up * bob;
            for (int i = 0; i < 3; i++)
            {
                float target = tileLit[i] ? 1f : 0f;
                tileGlow[i] += (target - tileGlow[i]) * (1f - Mathf.Exp(-6f * dt));
                tilePop[i] = Mathf.Max(0f, tilePop[i] - dt * 2.5f);
                tiles[i].localScale = Vector3.one * (1f + 0.14f * Mathf.Sin(tilePop[i] * Mathf.PI));
                RefreshTile(i);
            }
        }

        private void RefreshTile(int i)
        {
            float g = tileGlow[i];
            if (tilePanels[i] != null) tilePanels[i].SetFloat("_Alpha", Mathf.Lerp(0.10f, 0.36f, g));
            foreach (Material m in tileIcons[i])
                m.SetColor("_EmissionColor", IamBlue * Mathf.Lerp(0.35f, 2.4f, g));
            foreach (TextMesh t in tileText[i])
                t.color = new Color(0.85f, 0.93f, 1f, Mathf.Lerp(0.30f, 1f, g));
        }

        // ---- Helpers -----------------------------------------------------------

        private static Transform Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            BuildKit.StripCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private static void Dot(Transform parent, Vector3 pos, float size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Dot";
            BuildKit.StripCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size, size, size * 0.4f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private void AddLight(Vector3 localPos, Color color, float range, float intensity)
        {
            var go = new GameObject("SpecialistLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<UnityEngine.Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }
    }
}
