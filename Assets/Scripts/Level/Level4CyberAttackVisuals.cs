using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Cyverse.Interaction;
using Cyverse.Player;

namespace Cyverse.Level
{
    /// <summary>
    /// Visual foundation for Level 4 — Cyber Attack.
    ///
    /// The mechanics layer owns the four interactables and their progression.
    /// This component deliberately knows only about station names/aliases and
    /// builds the room's visual language around them: one clear task pad, a
    /// mounted title plate, a short affordance line and a state lamp per
    /// station. It can therefore be dropped into a procedural scene or a
    /// future visual-pass scene without serialised references.
    ///
    /// All geometry is primitive, collider-free and cheap for WebGL. Text is
    /// world-space TMP with bounded rectangles and depth-tested materials via
    /// WorldTextLayoutManager, so labels do not turn into giant clipped words
    /// at smaller browser resolutions.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class Level4CyberAttackVisuals : MonoBehaviour
    {
        public const string VisualRootName = "LEVEL4_ATTACK_VISUALS";

        public static readonly Color AttackAmber = new Color(1.00f, 0.43f, 0.12f);
        public static readonly Color AttackRose = new Color(1.00f, 0.20f, 0.35f);
        public static readonly Color AttackCyan = new Color(0.24f, 0.82f, 1.00f);
        public static readonly Color SafeGreen = new Color(0.32f, 1.00f, 0.55f);

        private const float ZoneWidth = 5.8f;
        private const float ZoneDepth = 4.7f;

        private bool applied;
        private Transform visualRoot;
        private Material panelMaterial;
        private Material amberMaterial;
        private Material roseMaterial;
        private Material cyanMaterial;
        private Material whiteMaterial;

        private struct StationSpec
        {
            public string key;
            public string title;
            public string instruction;
            public string[] aliases;
            public Vector3 fallback;
            public Color accent;

            public StationSpec(string key, string title, string instruction,
                string[] aliases, Vector3 fallback, Color accent)
            {
                this.key = key;
                this.title = title;
                this.instruction = instruction;
                this.aliases = aliases;
                this.fallback = fallback;
                this.accent = accent;
            }
        }

        private static readonly StationSpec[] Stations =
        {
            new StationSpec(
                "bypass-mfa",
                "01  BYPASS MFA",
                "TEST THE ENTRY CONTROL  ·  SANDBOX ONLY",
                new[] { "BypassMfa", "Bypass MFA", "InitialAccess", "Initial", "Phishing" },
                new Vector3(-10.5f, 0f, 8f),
                AttackCyan),
            new StationSpec(
                "escalate-privileges",
                "02  ESCALATE PRIVILEGES",
                "TEST THE IDENTITY PIVOT  ·  LEAST PRIVILEGE",
                new[] { "EscalatePrivileges", "Escalate Privileges", "Lateral", "Privilege", "Escalation" },
                new Vector3(10.5f, 0f, 8f),
                AttackAmber),
            new StationSpec(
                "extract-data",
                "03  EXTRACT DATA",
                "CAP THE TRANSFER  ·  MONITORED LAB CHANNEL",
                new[] { "ExtractData", "Extract Data", "Exfil", "Exfiltration", "Data" },
                new Vector3(-10.5f, 0f, 14f),
                AttackRose),
            new StationSpec(
                "cover-tracks",
                "04  COVER TRACKS",
                "TEST THE AUDIT TRAIL  ·  LEAVE EVIDENCE REVIEWABLE",
                new[] { "CoverTracks", "Cover Tracks", "Persistence", "Tracks", "Audit" },
                new Vector3(10.5f, 0f, 14f),
                new Color(1.00f, 0.55f, 0.18f)),
        };

        /// <summary>Gets or adds the visual foundation to a host.</summary>
        public static Level4CyberAttackVisuals Ensure(GameObject host)
        {
            Level4CyberAttackVisuals existing = FindObjectOfType<Level4CyberAttackVisuals>();
            if (existing != null) return existing;

            if (host == null)
                host = new GameObject("Level4CyberAttackVisuals");
            return host.AddComponent<Level4CyberAttackVisuals>();
        }

        private void Start()
        {
            Apply();
        }

        /// <summary>Builds or refreshes the visual language once.</summary>
        public void Apply()
        {
            if (applied) return;
            applied = true;

            ApplyAtmosphere();
            ToneFixtures();

            GameObject existingRoot = GameObject.Find(VisualRootName);
            if (existingRoot != null)
            {
                visualRoot = existingRoot.transform;
            }
            else
            {
                visualRoot = new GameObject(VisualRootName).transform;
                visualRoot.position = Vector3.zero;
            }

            BuildRoomHeader();
            BuildStationZones();
            BuildDataPulse();

            Level4ExfiltrationMeter.Ensure(gameObject);
        }

        private void ApplyAtmosphere()
        {
            // Warm hostile accent against a cool base keeps the room readable
            // in a small WebGL canvas while preserving a cyber-attack mood.
            RenderSettings.ambientLight = new Color(0.24f, 0.255f, 0.31f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.045f, 0.022f, 0.028f);
            RenderSettings.fogDensity = 0.008f;

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.018f, 0.012f, 0.022f);
            }
        }

        private void ToneFixtures()
        {
            Material ceiling = BuildKit.MakeEmissive(new Color(0.50f, 0.56f, 0.68f), 0.62f);
            foreach (Renderer renderer in FindObjectsOfType<Renderer>())
                if (renderer.name.StartsWith("CeilingPanel_")) renderer.sharedMaterial = ceiling;

            foreach (Light light in FindObjectsOfType<Light>())
            {
                if (!light.name.StartsWith("CeilingLight_")) continue;
                light.color = new Color(0.80f, 0.84f, 0.98f);
                light.intensity = 0.88f;
                light.range = 15f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForceVertex;
            }
        }

        private void BuildRoomHeader()
        {
            HideLegacyAttackHeaderSigns();
            ClearNorthHeaderBay();
            // Use the new upper wall band and stand the whole assembly clear
            // of the wall/columns. The previous header shared depth with the
            // centre structural column, so the words appeared embedded in it.
            SpawnCube("L4_HeaderPanel", visualRoot, new Vector3(0f, 5.55f, 18.78f),
                new Vector3(15.5f, 1.35f, 0.12f), PanelMaterial());
            CreateWorldText("L4_Header", new Vector3(0f, 5.76f, 18.69f),
                "CYBER ATTACK  //  OFFENSIVE LAB", new Color(0.90f, 0.94f, 1f),
                0.070f, 170f, 44f, Quaternion.identity);
            SpawnCube("L4_HeaderRail", visualRoot, new Vector3(0f, 4.96f, 18.68f),
                new Vector3(15.5f, 0.045f, 0.05f), AmberMaterial());

            CreateWorldText("L4_HeaderSub", new Vector3(0f, 5.22f, 18.67f),
                "CONTROLLED RED-TEAM SIMULATION", new Color(1f, 0.69f, 0.48f),
                0.045f, 160f, 26f, Quaternion.identity);
        }

        private static void ClearNorthHeaderBay()
        {
            // The centre column has a useful structural role everywhere else,
            // but it bisects the one continuous north-wall header bay.
            GameObject centreColumn = GameObject.Find("Column_N0");
            if (centreColumn != null) centreColumn.SetActive(false);
        }

        private static void HideLegacyAttackHeaderSigns()
        {
            // The first procedural pass placed two billboard signs in this
            // exact wall bay. Keep the authored safety copy out of the same
            // screen-space rectangle as the mounted header below; the mounted
            // version is depth-tested and remains readable at narrow widths.
            foreach (TextMeshPro label in FindObjectsOfType<TextMeshPro>(true))
            {
                if (label == null || string.IsNullOrEmpty(label.text)) continue;
                string text = label.text;
                bool duplicateWorkflow = text.IndexOf("RECON", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    text.IndexOf("EXFIL", StringComparison.OrdinalIgnoreCase) >= 0;
                bool duplicateRules = text.IndexOf("EVERY ACTION IS A FICTIONAL", StringComparison.OrdinalIgnoreCase) >= 0;
                bool duplicateRoute =
                    (text.IndexOf("BYPASS MFA", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     text.IndexOf("ESCALATE PRIVILEGES", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (text.IndexOf("EXTRACT DATA", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     text.IndexOf("COVER TRACKS", StringComparison.OrdinalIgnoreCase) >= 0);
                if (duplicateWorkflow || duplicateRules || duplicateRoute)
                    label.gameObject.SetActive(false);
            }
        }

        private void BuildStationZones()
        {
            HideLegacyStationSigns();
            for (int i = 0; i < Stations.Length; i++)
            {
                StationSpec spec = Stations[i];
                Transform target = ResolveStation(spec, i);
                Vector3 anchor = target != null ? target.position : spec.fallback;
                Vector3 forward = target != null ? Flatten(target.forward) : Vector3.forward;
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                forward.Normalize();

                BuildStationZone(spec, i, target, anchor, forward);
            }
        }

        private void BuildStationZone(StationSpec spec, int index, Transform target,
            Vector3 anchor, Vector3 forward)
        {
            Vector3 floorCenter = new Vector3(anchor.x, 0.025f, anchor.z);
            // Retain a named zone anchor for diagnostics and future authored
            // art, but do not lay an opaque slab, rails, or chevrons over the
            // common floor. Those overlapping layers were what made Level 4
            // read like broken geometry when viewed at ground level.
            GameObject zoneAnchor = new GameObject("L4_ZonePad_" + spec.key);
            zoneAnchor.transform.SetParent(visualRoot, false);
            zoneAnchor.transform.SetPositionAndRotation(floorCenter,
                Quaternion.LookRotation(forward, Vector3.up));

            Material accent = AccentMaterial(spec.accent);

            // Put the panel behind the target and face its readable side back
            // toward the center aisle. For a generated station root, local
            // forward is +Z; text's readable side is local -Z.
            // Mount the backing board high enough that it cannot occlude the
            // monitor or the next station's approach lane. A low, wide board
            // is especially troublesome in the north row: the next camera
            // can pass through its visual bounds even though it has no
            // collider. The raised, compact plate keeps the title readable
            // while preserving a clear sightline to the screen.
            Vector3 boardCenter = anchor + forward * 1.28f + Vector3.up * 3.35f;
            GameObject board = SpawnCube("L4_StationBoard_" + spec.key, visualRoot,
                boardCenter, new Vector3(ZoneWidth - 0.42f, 1.90f, 0.11f), PanelMaterial());
            board.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            SpawnCube("L4_StationRail_" + spec.key, visualRoot,
                boardCenter - forward * 0.075f + Vector3.down * 0.77f,
                new Vector3(ZoneWidth - 0.42f, 0.045f, 0.05f), accent).transform.rotation =
                Quaternion.LookRotation(forward, Vector3.up);

            Vector3 textFront = boardCenter - forward * 0.085f;
            Quaternion textRotation = Quaternion.LookRotation(forward, Vector3.up);
            CreateWorldText("L4_StationTitle_" + spec.key,
                textFront + Vector3.up * 0.42f, spec.title,
                Color.Lerp(spec.accent, Color.white, 0.18f), 0.060f, 78f, 36f, textRotation);
            CreateWorldText("L4_StationInstruction_" + spec.key,
                textFront - Vector3.up * 0.12f, spec.instruction,
                new Color(0.77f, 0.84f, 0.94f), 0.042f, 105f, 23f, textRotation);

            GameObject marker = new GameObject("L4_StationMarker_" + spec.key);
            marker.transform.SetParent(visualRoot, false);
            marker.transform.position = anchor + Vector3.up * 0.10f;
            Level4StationVisual visual = marker.AddComponent<Level4StationVisual>();
            visual.Configure(spec.key, index, target, spec.accent);
        }

        private void BuildDataPulse()
        {
            // A small vertical stack on the north-east wall communicates the
            // end goal without competing with the top-right screen HUD meter.
            Vector3 origin = new Vector3(15.2f, 0.65f, 18.98f);
            SpawnCube("L4_DataPulsePanel", visualRoot, origin + Vector3.up * 1.48f,
                new Vector3(4.8f, 2.9f, 0.08f), PanelMaterial());
            CreateWorldText("L4_DataPulseTitle", origin + Vector3.up * 2.42f,
                "TARGET DATA  //  EXFIL", new Color(1f, 0.72f, 0.42f),
                0.034f, 48f, 22f, Quaternion.identity);
            CreateWorldText("L4_DataPulseBody", origin + Vector3.up * 1.58f,
                "STEAL MORE  ·  GET CAUGHT LESS\nTHE METER IS THE SCORE",
                new Color(0.77f, 0.84f, 0.94f), 0.028f, 46f, 18f, Quaternion.identity);
            for (int i = 0; i < 5; i++)
            {
                SpawnCube("L4_DataPulseBar_" + i, visualRoot,
                    origin + Vector3.up * (0.88f + i * 0.18f),
                    new Vector3(2.6f + i * 0.28f, 0.055f, 0.04f),
                    i < 3 ? CyanMaterial() : AmberMaterial());
            }
        }

        private Transform ResolveStation(StationSpec spec, int stationIndex)
        {
            Transform[] all = FindObjectsOfType<Transform>(true);
            string numberedName = "CyberAttackStation_" + (stationIndex + 1);
            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate != null && string.Equals(candidate.name, numberedName,
                    StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            Transform fallback = null;
            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null || candidate.IsChildOf(visualRoot)) continue;
                if (!candidate.gameObject.scene.IsValid()) continue;

                string name = candidate.name;
                for (int alias = 0; alias < spec.aliases.Length; alias++)
                {
                    if (name.IndexOf(spec.aliases[alias], StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    // Prefer the actual interactable root, then use the first
                    // named visual as a fallback for authored scenes.
                    if (HasInteractable(candidate)) return candidate;
                    if (fallback == null) fallback = candidate;
                    break;
                }
            }
            return fallback;
        }

        private static void HideLegacyStationSigns()
        {
            // CyberAttackStation.Build creates a floating billboard sign for
            // every monitor. The mounted Level 4 board is the authoritative
            // title, so leave only the screen label and state affordance in
            // the station root; otherwise both titles occupy the same pixels
            // from the player approach and during visual QA captures.
            foreach (Transform station in FindObjectsOfType<Transform>(true))
            {
                if (station == null || !station.name.StartsWith("CyberAttackStation_",
                    StringComparison.OrdinalIgnoreCase)) continue;

                foreach (Transform child in station.GetComponentsInChildren<Transform>(true))
                {
                    if (child != null && child.name.StartsWith("Sign_",
                        StringComparison.OrdinalIgnoreCase))
                        child.gameObject.SetActive(false);
                }
            }
        }

        private static bool HasInteractable(Transform target)
        {
            MonoBehaviour[] behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IInteractable) return true;
            MonoBehaviour[] children = target.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < children.Length; i++)
                if (children[i] is IInteractable) return true;
            return false;
        }

        private static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            return direction;
        }

        private TMP_Text CreateWorldText(string name, Vector3 position, string content,
            Color color, float scale, float width, float fontSize, Quaternion rotation)
        {
            GameObject go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(visualRoot, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = Vector3.one * scale;

            TextMeshPro text = go.GetComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = fontSize;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.extraPadding = true;
            text.rectTransform.sizeDelta = new Vector2(width, 4.2f);
            WorldTextLayoutIntent.Configure(go, WorldTextLayoutIntent.Mode.Mounted);
            return text;
        }

        private void SpawnBeam(string name, Transform parent, Vector3 from, Vector3 to, Material material)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < 0.0001f) return;
            GameObject beam = SpawnCube(name, parent, (from + to) * 0.5f,
                new Vector3(0.05f, 0.020f, delta.magnitude), material);
            beam.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        private static GameObject SpawnCube(string name, Transform parent, Vector3 position,
            Vector3 scale, Material material)
        {
            return BuildKit.Spawn(PrimitiveType.Cube, name, parent, position, scale, material, false);
        }

        private Material PanelMaterial()
        {
            if (panelMaterial == null)
                panelMaterial = BuildKit.MakeStandard(new Color(0.055f, 0.062f, 0.095f), 0.66f, 0.52f);
            return panelMaterial;
        }

        private Material AmberMaterial()
        {
            if (amberMaterial == null) amberMaterial = BuildKit.MakeEmissive(AttackAmber, 1.45f);
            return amberMaterial;
        }

        private Material RoseMaterial()
        {
            if (roseMaterial == null) roseMaterial = BuildKit.MakeEmissive(AttackRose, 1.45f);
            return roseMaterial;
        }

        private Material CyanMaterial()
        {
            if (cyanMaterial == null) cyanMaterial = BuildKit.MakeEmissive(AttackCyan, 1.30f);
            return cyanMaterial;
        }

        private Material WhiteMaterial()
        {
            if (whiteMaterial == null) whiteMaterial = BuildKit.MakeEmissive(new Color(0.85f, 0.90f, 1f), 1.20f);
            return whiteMaterial;
        }

        private Material AccentMaterial(Color color)
        {
            if (Approximately(color, AttackRose)) return RoseMaterial();
            if (Approximately(color, AttackCyan)) return CyanMaterial();
            if (Approximately(color, AttackAmber)) return AmberMaterial();
            if (Approximately(color, new Color(1.00f, 0.55f, 0.18f))) return AmberMaterial();
            return WhiteMaterial();
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f &&
                   Mathf.Abs(a.b - b.b) < 0.02f;
        }
    }

    /// <summary>
    /// Lightweight visual state for a station's title plate and hover lamp.
    /// Mechanics can call SetState without taking a dependency on any of the
    /// level-specific UI implementation.
    /// </summary>
    public sealed class Level4StationVisual : MonoBehaviour
    {
        public enum StationState { Ready, Active, Complete, Locked }

        private string stationKey;
        private int stationIndex;
        private Transform interactionTarget;
        private IInteractable interactable;
        private Level4CyberAttackManager manager;
        private CyberAttackStation station;
        private Color accent = Color.cyan;
        private Renderer lampRenderer;
        private Light lamp;
        private TMP_Text stateText;
        private float hover;
        private StationState state = StationState.Ready;

        public string StationKey => stationKey;
        public StationState State => state;

        public void Configure(string key, int index, Transform target, Color color)
        {
            stationKey = key;
            stationIndex = index;
            interactionTarget = target;
            accent = color;
            interactable = FindInteractable(target);
            station = FindStation(target, interactable);
            BindManager();

            GameObject lampObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lampObject.name = "StateLamp";
            BuildKit.StripCollider(lampObject);
            lampObject.transform.SetParent(transform, false);
            lampObject.transform.localPosition = new Vector3(0f, 0.48f, 0f);
            lampObject.transform.localScale = new Vector3(0.34f, 0.08f, 0.08f);
            lampRenderer = lampObject.GetComponent<Renderer>();
            lampRenderer.sharedMaterial = BuildKit.MakeEmissive(accent, 1.7f);

            GameObject lightObject = new GameObject("StateLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.50f, 0f);
            lamp = lightObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = accent;
            lamp.range = 3.4f;
            lamp.intensity = 0.70f;
            lamp.shadows = LightShadows.None;
            lamp.renderMode = LightRenderMode.ForceVertex;

            GameObject textObject = new GameObject("StateText", typeof(TextMeshPro));
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.30f, -0.02f);
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one * 0.026f;
            stateText = textObject.GetComponent<TextMeshPro>();
            stateText.font = TMP_Settings.defaultFontAsset;
            stateText.alignment = TextAlignmentOptions.Center;
            stateText.fontStyle = FontStyles.Bold;
            stateText.enableWordWrapping = false;
            stateText.enableAutoSizing = true;
            stateText.fontSizeMin = 11f;
            stateText.fontSizeMax = 18f;
            stateText.overflowMode = TextOverflowModes.Ellipsis;
            stateText.rectTransform.sizeDelta = new Vector2(24f, 1.2f);
            WorldTextLayoutIntent.Configure(textObject, WorldTextLayoutIntent.Mode.Mounted);

            RefreshStateText();
        }

        private void OnEnable()
        {
            // Configure normally runs immediately after AddComponent, but
            // this also covers a visual marker restored from an authored
            // scene or re-enabled by a capture harness.
            BindManager();
        }

        private void OnDisable()
        {
            if (manager != null) manager.ProgressChanged -= SyncStateFromManager;
            manager = null;
        }

        public void SetState(StationState next)
        {
            state = next;
            RefreshStateText();
        }

        private void Update()
        {
            BindManager();
            SyncStateFromManager();
            bool targeted = interactable != null && ReferenceEquals(PlayerInteractor.CurrentTarget, interactable);
            float target = targeted ? 1f : 0f;
            hover = Mathf.MoveTowards(hover, target, Time.deltaTime * 5f);
            if (lamp != null) lamp.intensity = Mathf.Lerp(0.55f, 1.55f, hover);
            if (lampRenderer != null && lampRenderer.material.HasProperty("_EmissionColor"))
                lampRenderer.material.SetColor("_EmissionColor", accent * (1.2f + hover * 1.8f));
        }

        private void SyncStateFromManager()
        {
            BindManager();
            if (manager == null) return;

            bool complete = manager.IsLevelComplete || manager.CurrentStationIndex > stationIndex ||
                (station != null && manager.IsStationComplete(station));
            StationState next = complete
                ? StationState.Complete
                : !manager.ScenarioStarted || manager.CurrentStationIndex < stationIndex
                    ? StationState.Locked
                    : StationState.Active;
            if (next != state)
            {
                state = next;
                RefreshStateText();
            }
        }

        private void BindManager()
        {
            Level4CyberAttackManager next = Level4CyberAttackManager.Instance;
            if (next == null) next = FindObjectOfType<Level4CyberAttackManager>();
            if (ReferenceEquals(next, manager)) return;

            if (manager != null) manager.ProgressChanged -= SyncStateFromManager;
            manager = next;
            if (manager != null) manager.ProgressChanged += SyncStateFromManager;
        }

        private void RefreshStateText()
        {
            if (stateText == null) return;
            stateText.text = state == StationState.Complete ? "COMPLETE  [OK]" :
                state == StationState.Active ? "ACTIVE  ·  IN PROGRESS" :
                state == StationState.Locked ? "LOCKED  ·  FINISH PRIOR STEP" :
                "READY  ·  PRESS E";
            stateText.color = state == StationState.Complete ? Level4CyberAttackVisuals.SafeGreen :
                state == StationState.Locked ? new Color(0.78f, 0.80f, 0.86f) : accent;
        }

        private static IInteractable FindInteractable(Transform target)
        {
            if (target == null) return null;
            MonoBehaviour[] behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IInteractable interactable) return interactable;
            MonoBehaviour[] children = target.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < children.Length; i++)
                if (children[i] is IInteractable interactable) return interactable;
            return null;
        }

        private static CyberAttackStation FindStation(Transform target, IInteractable candidate)
        {
            CyberAttackStation direct = candidate as CyberAttackStation;
            if (direct != null) return direct;
            if (target == null) return null;

            direct = target.GetComponentInParent<CyberAttackStation>();
            if (direct != null) return direct;
            return target.GetComponentInChildren<CyberAttackStation>(true);
        }
    }
}
