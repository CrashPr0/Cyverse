using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Cyverse.Forensics;
using Cyverse.UI;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// WebGL-conscious finish layer for the procedurally generated Forensics
    /// Lab. It turns the single-terminal room into a visible workflow:
    /// acquire evidence, analyze it, correlate findings, then report.
    /// Decorative geometry is collider-free and safe to rebuild per scene.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class Level3ForensicsPolish : MonoBehaviour
    {
        private const string RootName = "FORENSICS_LAB_POLISH";

        // Floor dressing is inlaid, not stacked: zone pads 4-12 mm proud (no
        // visible side faces, clear of the floor's depth), rails/paths just
        // above them. The old 3.5 cm slabs floated 9 mm off the floor and the
        // rails stood 6 cm tall, which read as debris on the grid.
        private const float PadY = 0.008f;
        private const float PadHeight = 0.008f;
        private const float InlayY = 0.0145f;
        private const float InlayHeight = 0.005f;

        /// <summary>The lab floor is a light gray with a faint green grid, so
        /// the green stations and signs stand out against it. Zone pads sit a
        /// shade darker so the three work areas still read from the door.</summary>
        public static readonly Color FloorBase = new Color(0.66f, 0.68f, 0.68f);
        public static readonly Color FloorLine = new Color(0.20f, 0.46f, 0.30f);
        private static readonly Color PadBase = new Color(0.54f, 0.57f, 0.57f);

        private Material panelMaterial;
        private Material furnitureMaterial;
        private Material floorMaterial;
        private Material greenMaterial;
        private Material goldMaterial;
        private TMP_Text reportStatus;
        private TMP_Text intakeStatus;
        private ForensicsConsole console;
        private ChainOfCustodyForm custodyForm;
        private Level3ForensicsManager manager;
        private bool applied;

        private void Start() => Apply();

        /// <summary>Realizes the complete lab presentation synchronously so
        /// gameplay can bind to the intake and report stations immediately.</summary>
        public void Apply()
        {
            if (applied) return;
            applied = true;
            ApplyAtmosphere();
            ToneLabFloor(GameObject.Find("Floor")?.GetComponent<Renderer>()?.sharedMaterial);
            ToneFixtures();
            MovePlanterOffIntake();
            BuildLab();
            BindProgress();
        }

        private void OnDestroy()
        {
            if (console != null && console.Cases != null)
            {
                foreach (InvestigationCase investigation in console.Cases)
                {
                    investigation.QuestionAnswered -= RefreshReport;
                    investigation.CaseCompleted -= RefreshReport;
                }
            }
            if (custodyForm != null)
            {
                custodyForm.Changed -= RefreshIntake;
                custodyForm.Completed -= RefreshIntake;
            }
            if (manager != null) manager.ReportFiled -= RefreshReport;
        }

        private void ApplyAtmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            // Preserve the green lab mood without letting the furniture collapse
            // into black silhouettes in a smaller WebGL canvas.
            RenderSettings.ambientLight = new Color(0.30f, 0.36f, 0.33f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.018f, 0.052f, 0.035f);
            RenderSettings.fogDensity = 0.009f;

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.012f, 0.028f, 0.022f);
            }
        }

        private void ToneFixtures()
        {
            Material ceiling = BuildKit.MakeEmissive(new Color(0.48f, 0.64f, 0.57f), 0.58f);
            foreach (Renderer renderer in FindObjectsOfType<Renderer>())
                if (renderer.name.StartsWith("CeilingPanel_")) renderer.sharedMaterial = ceiling;

            foreach (Light light in FindObjectsOfType<Light>())
            {
                if (!light.name.StartsWith("CeilingLight_") || light.transform.position.z < 1f) continue;
                light.color = new Color(0.72f, 0.92f, 0.82f);
                light.intensity = 0.76f;
                light.range = 12f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForceVertex;
            }
        }

        // The shared furnishing planter at (-8, 8) stands between the lab floor
        // and Evidence Intake (-11.5, 8.15), half hiding the desk on approach.
        // Tuck it beside the intake plinth on the wall side instead; runs before
        // PlanterClearance (order 1000), which still checks the new spot.
        private static void MovePlanterOffIntake()
        {
            GameObject furnishings = GameObject.Find("Furnishings");
            if (furnishings == null) return;
            foreach (Transform child in furnishings.transform)
            {
                Vector3 p = child.position;
                if (child.name == "Plant" && Near(p, -8f, 8f))
                    child.position = new Vector3(-14.2f, p.y, 8.2f);
                // The east planter stood on the report pad's corner, in front
                // of the report monitor.
                else if (child.name == "Plant" && Near(p, 8f, 8f))
                    child.position = new Vector3(12.8f, p.y, 8f);
                // Reception sat between the evidence locker and the briefing
                // screen, across the first walk of the level.
                else if (child.name == "Reception" && Near(p, 5f, -12f))
                    child.position = new Vector3(11.5f, p.y, -12f);
            }
        }

        private static bool Near(Vector3 p, float x, float z) =>
            Mathf.Abs(p.x - x) < 0.1f && Mathf.Abs(p.z - z) < 0.1f;

        private void BuildLab()
        {
            if (GameObject.Find(RootName) != null) return;
            GameObject root = new GameObject(RootName);

            BuildWorkflowHeader(root.transform);
            BuildAnalysisZone(root.transform);
            BuildAcquisitionZone(root.transform);
            BuildReportingZone(root.transform);
            BuildWorkflowPath(root.transform);
            BuildWitnessPhone(root.transform);
            BuildFillLights(root.transform);
            PolishInvestigationDesk();
        }

        private void BuildWorkflowHeader(Transform root)
        {
            // Centred in the wall bay between Column_N-16 and Column_N-8: on
            // the centre line the x=0 column and the exit door's housing
            // covered the words, and the next bay has the server row's tray
            // rods in front of it.
            CreateWorldText(root, "DF_WorkflowHeader", new Vector3(-12f, 4.05f, 19.18f),
                "ACQUIRE   ›   ANALYZE   ›   REPORT",
                Palette.Tint("Level3", new Color(0.72f, 1f, 0.82f)), 0.055f, 120f, 34f);
            SpawnCube("DF_HeaderRail", root, new Vector3(-12f, 3.62f, 19.28f),
                new Vector3(5.2f, 0.045f, 0.055f), GreenMaterial());
        }

        private void BuildAnalysisZone(Transform root)
        {
            SpawnCube("DF_AnalysisPad", root, new Vector3(0f, PadY, 10f),
                new Vector3(7.4f, PadHeight, 6.2f), FloorMaterial());
            SpawnCube("DF_AnalysisRail_L", root, new Vector3(-3.66f, InlayY, 10f),
                new Vector3(0.04f, InlayHeight, 6.0f), GreenMaterial());
            SpawnCube("DF_AnalysisRail_R", root, new Vector3(3.66f, InlayY, 10f),
                new Vector3(0.04f, InlayHeight, 6.0f), GreenMaterial());
        }

        private void BuildAcquisitionZone(Transform root)
        {
            SpawnCube("DF_AcquisitionPad", root, new Vector3(-11.5f, PadY, 8f),
                new Vector3(5.2f, PadHeight, 4.6f), FloorMaterial());

            string evidenceText = SocProgress.TryGetEvidence(out var evidence)
                ? $"{evidence.computer} IMAGE  ·  CUSTODY REQUIRED"
                : "TRAINING IMAGE  ·  CUSTODY REQUIRED";
            // Keep the evidence identity as a compact caption on the plinth's
            // front face. Its old y=1.55 position sat directly in front of the
            // new custody RT and painted a second set of words across it.
            SpawnCube("DF_IntakeStatusPanel", root, new Vector3(-11.5f, 0.60f, 7.31f),
                new Vector3(2.45f, 0.42f, 0.04f), PanelMaterial());
            intakeStatus = CreateWorldText(root, "DF_IntakeStatus", new Vector3(-11.5f, 0.60f, 7.28f),
                evidenceText, new Color(0.78f, 0.92f, 1f), 0.026f, 88f, 22f);
        }

        private void BuildReportingZone(Transform root)
        {
            SpawnCube("DF_ReportingPad", root, new Vector3(9.2f, PadY, 10.5f),
                new Vector3(5.4f, PadHeight, 5.0f), FloorMaterial());
            // Create this primitive with its collider intact. SpawnCube strips
            // colliders with deferred Destroy during play mode, so immediately
            // reusing that component as an aim trigger would lose it at the end
            // of the frame.
            GameObject reportDesk = BuildKit.Spawn(PrimitiveType.Cube, "DF_ReportDesk", root,
                new Vector3(9.2f, 0.50f, 11f), new Vector3(3.0f, 1.0f, 1.25f),
                FurnitureMaterial(), collider: true);
            BuildKit.EnsureAimCollider(reportDesk, 2.6f, 3.2f);
            // EnsureAimCollider turns the desk's own box into a trigger, which
            // left the desk walk-through. A child box keeps it solid; the
            // interact ray still finds the station through GetComponentInParent.
            var solid = new GameObject("DF_ReportDeskSolid");
            solid.transform.SetParent(reportDesk.transform, false);
            solid.AddComponent<BoxCollider>();
            reportDesk.AddComponent<ForensicsReportStation>();
            // The monitor stands on a neck above the desk (its housing used to
            // sink into the worktop); the heading panel rides above it.
            GameObject reportMonitor = SpawnCube("DF_ReportMonitor", root, new Vector3(9.2f, 1.66f, 11.15f),
                new Vector3(2.7f, 1.25f, 0.08f), PanelMaterial());
            SpawnCube("DF_ReportScreen", root, new Vector3(9.2f, 1.66f, 11.09f),
                new Vector3(2.45f, 1.04f, 0.025f), BuildKit.MakeEmissive(new Color(0.04f, 0.18f, 0.11f), 0.72f));
            SpawnCube("DF_ReportHeaderPanel", root, new Vector3(9.2f, 2.60f, 11.10f),
                new Vector3(3.35f, 0.50f, 0.04f), PanelMaterial());
            SpawnHangers("DF_ReportHeaderPanel", root, new Vector3(9.2f, 2.60f, 11.10f), 3.35f, 0.50f);
            CreateWorldText(root, "DF_ReportHeader", new Vector3(9.2f, 2.60f, 11.02f),
                "03  FORENSIC REPORT", Palette.Tint("Level3", new Color(0.72f, 1f, 0.82f)), 0.045f, 52f, 28f);
            // Evidence acquisition now happens at the Investigation Desk, so the
            // report monitor is free to carry the casework status again. 1.75 cm
            // proud of the screen face (z 11.0775) to stay out of its depth.
            reportStatus = CreateWorldText(root, "DF_ReportStatus", new Vector3(9.2f, 1.66f, 11.06f),
                "CASEWORK  0 / 11  ·  REPORT LOCKED", new Color(0.82f, 0.92f, 1f), 0.040f, 58f, 30f);
            ForensicsStationProps.DressReport(reportDesk.transform, reportMonitor.transform, root,
                ForensicsStationProps.MakeFinish(FurnitureMaterial(), new Color(0.90f, 0.66f, 0.22f)));
        }

        /// <summary>Optional side station east of the report desk: a coworker's
        /// phone lying on a reading stand, tilted toward the player. E opens it
        /// as a messages app (<see cref="WitnessPhone"/>).</summary>
        private void BuildWitnessPhone(Transform root)
        {
            Vector3 at = new Vector3(12.4f, 0f, 11.0f);
            var stand = new GameObject("DF_WitnessPhone");
            stand.transform.SetParent(root, false);
            stand.transform.position = at;
            // One solid box: blocks walking and catches the interact ray.
            var box = stand.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(0.62f, 1.2f, 0.5f);
            stand.AddComponent<WitnessPhone>();

            SpawnCube("DF_WitnessStandBase", stand.transform, at + new Vector3(0f, 0.03f, 0f),
                new Vector3(0.56f, 0.06f, 0.46f), FurnitureMaterial());
            SpawnCube("DF_WitnessStandPost", stand.transform, at + new Vector3(0f, 0.5f, 0.04f),
                new Vector3(0.12f, 0.9f, 0.12f), FurnitureMaterial());
            // Tilted 20 degrees so the top faces the player approaching from -z.
            Quaternion tilt = Quaternion.Euler(-20f, 0f, 0f);
            GameObject top = SpawnCube("DF_WitnessStandTop", stand.transform, at + new Vector3(0f, 0.98f, 0f),
                new Vector3(0.48f, 0.035f, 0.36f), PanelMaterial());
            top.transform.rotation = tilt;
            Vector3 up = tilt * Vector3.up;
            GameObject body = SpawnCube("DF_WitnessPhoneBody", stand.transform, top.transform.position + up * 0.024f,
                new Vector3(0.10f, 0.012f, 0.19f), BuildKit.MakeStandard(PhoneScreenUi.CaseColor, 0.35f, 0.25f));
            body.transform.rotation = tilt;
            GameObject screen = SpawnCube("DF_WitnessPhoneScreen", stand.transform, body.transform.position + up * 0.0065f,
                new Vector3(0.088f, 0.002f, 0.172f), BuildKit.MakeEmissive(new Color(0.22f, 0.40f, 0.95f), 0.9f));
            screen.transform.rotation = tilt;

            // A name plate on the post's front face, approached from the south.
            SpawnCube("DF_WitnessPlate", stand.transform, at + new Vector3(0f, 0.66f, -0.03f),
                new Vector3(0.46f, 0.15f, 0.02f), PanelMaterial());
            CreateWorldText(stand.transform, "DF_WitnessLabel", at + new Vector3(0f, 0.66f, -0.043f),
                "WITNESS PHONE", Palette.Tint("Level3", new Color(0.72f, 1f, 0.82f)), 0.025f, 17f, 28f);
        }

        private void BuildWorkflowPath(Transform root)
        {
            // z = 7, not 8: the shared furnishing planter spot is (-8, 8) (this
            // level moves it, but other rooms keep it there).
            SpawnOrthogonalPath("DF_Path_AcquireToAnalyze", root, new Vector3(-9.3f, InlayY, 7f),
                new Vector3(-3.8f, InlayY, 9.2f), GreenMaterial());
            // Each leg starts where the last one ended, on the side of the
            // station the player works from: the analyze desk's front edge
            // straight across to the report desk's front (it faces south,
            // toward z 9).
            SpawnBeam("DF_Path_AnalyzeToReport", root, new Vector3(3.8f, InlayY, 9.4f),
                new Vector3(8.4f, InlayY, 9.4f), GreenMaterial());
        }

        private void BuildFillLights(Transform root)
        {
            AddFillLight(root, "DF_IntakeFill", new Vector3(-11.5f, 3.2f, 8f),
                new Color(0.42f, 0.72f, 1f), 0.58f, 8f);
            AddFillLight(root, "DF_AnalysisFill", new Vector3(0f, 3.5f, 10f),
                Palette.Tint("Level3", new Color(0.30f, 1f, 0.55f)), 0.68f, 10f);
            AddFillLight(root, "DF_ReportFill", new Vector3(9.2f, 3.2f, 10.5f),
                new Color(0.88f, 0.68f, 0.28f), 0.52f, 8f);
        }

        private void PolishInvestigationDesk()
        {
            ForensicsConsole found = FindObjectOfType<ForensicsConsole>();
            if (found == null) return;
            SetMaterial(found.transform.Find("Desk"), FurnitureMaterial());
            ForensicsStationProps.Finish finish = ForensicsStationProps.MakeFinish(FurnitureMaterial(),
                Palette.Tint("Level3", new Color(0.24f, 0.92f, 0.50f)));
            ForensicsStationProps.DressAnalyze(found.transform, finish);
            foreach (Renderer renderer in found.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.StartsWith("MonBody_") || renderer.name.StartsWith("MonArm_"))
                    renderer.sharedMaterial = PanelMaterial();

            // Evidence acquisition is the step right after custody, so it lives
            // on this desk: cradle + write blocker under the LEFT monitor, which
            // becomes the imaging readout.
            PlugInStation.Build(found);

            // The generated floating title sat directly on the room's neon
            // wall rail. Mount it to a quiet panel above the console so the
            // next actionable station reads as a single visual unit.
            ConfigureStationHeading(found.transform, "02  ANALYZE EVIDENCE",
                new Vector3(0f, 3.10f, 9.58f));

            ChainOfCustodyStation intake = FindObjectOfType<ChainOfCustodyStation>();
            if (intake != null)
                ForensicsStationProps.DressIntake(intake.transform, finish);
            if (intake != null)
                ConfigureStationHeading(intake.transform, "01  EVIDENCE INTAKE",
                    // z 7.47 keeps its hanger rods clear of CeilingPanel_7's
                    // light strip (z 6.55..7.45).
                    new Vector3(-11.5f, 2.30f, 7.47f));
        }

        private void ConfigureStationHeading(Transform station, string content, Vector3 position)
        {
            TextMeshPro heading = null;
            foreach (TextMeshPro candidate in station.GetComponentsInChildren<TextMeshPro>(true))
            {
                if (!candidate.gameObject.name.StartsWith("Sign_")) continue;
                heading = candidate;
                break;
            }
            if (heading == null) return;

            heading.text = content;
            heading.color = Palette.Tint("Level3", new Color(0.72f, 1f, 0.82f));
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 18f;
            heading.fontSizeMax = 38f;
            heading.overflowMode = TextOverflowModes.Ellipsis;
            heading.rectTransform.sizeDelta = new Vector2(48f, 3.8f);
            heading.transform.SetPositionAndRotation(position, Quaternion.identity);
            heading.transform.localScale = Vector3.one * 0.042f;

            Billboard billboard = heading.GetComponent<Billboard>();
            if (billboard != null) billboard.enabled = false;
            WorldTextLayoutIntent.Configure(heading.gameObject,
                WorldTextLayoutIntent.Mode.Mounted, 200);
            SignFX signFx = heading.GetComponent<SignFX>();
            if (signFx != null) signFx.enabled = false;

            // Sized to the ~2 m heading (a 4.8 m slab read as a black wall from
            // the side and hid the wall TV) and hung from the ceiling.
            Vector3 panel = position + Vector3.forward * 0.08f;
            SpawnCube("DF_HeadingPanel_" + station.name, station.parent,
                panel, new Vector3(2.6f, 0.62f, 0.06f), PanelMaterial());
            SpawnHangers("DF_HeadingPanel_" + station.name, station.parent, panel, 2.6f, 0.62f);
        }

        /// <summary>Two thin rods from the top of a hanging panel to the ceiling.</summary>
        private void SpawnHangers(string name, Transform root, Vector3 panelCenter, float width, float height)
        {
            const float Ceiling = 5f;
            float top = panelCenter.y + height * 0.5f;
            if (top >= Ceiling) return;
            float length = Ceiling - top;
            foreach (float side in new[] { -1f, 1f })
                SpawnCube(name + "_Hanger" + (side < 0 ? "L" : "R"), root,
                    new Vector3(panelCenter.x + side * (width * 0.5f - 0.15f), top + length * 0.5f, panelCenter.z),
                    new Vector3(0.025f, length, 0.025f), FurnitureMaterial());
        }

        private void BindProgress()
        {
            console = FindObjectOfType<ForensicsConsole>();
            custodyForm = ChainOfCustodyForm.Instance;
            if (console != null && console.Cases != null)
            {
                foreach (InvestigationCase investigation in console.Cases)
                {
                    investigation.QuestionAnswered += RefreshReport;
                    investigation.CaseCompleted += RefreshReport;
                }
            }
            if (custodyForm != null)
            {
                custodyForm.Changed += RefreshIntake;
                custodyForm.Completed += RefreshIntake;
            }
            // The report monitor kept saying PRESS E TO SUBMIT after the report
            // was filed; repaint it when that happens.
            manager = Level3ForensicsManager.Instance;
            if (manager != null) manager.ReportFiled += RefreshReport;
            RefreshIntake();
            RefreshReport();
        }

        private void RefreshIntake()
        {
            if (intakeStatus == null) return;
            string source = SocProgress.TryGetEvidence(out var evidence) ? evidence.computer : "TRAINING IMAGE";
            bool complete = custodyForm != null && custodyForm.IsComplete;
            intakeStatus.text = complete
                ? $"{source} IMAGE  ·  CUSTODY ACCEPTED  [OK]"
                : $"{source} IMAGE  ·  CUSTODY  {custodyForm?.SelectedCount ?? 0} / {custodyForm?.FieldCount ?? 4}";
            intakeStatus.color = complete ? new Color(0.35f, 1f, 0.55f) : new Color(0.78f, 0.92f, 1f);
        }

        private void RefreshReport()
        {
            if (reportStatus == null) return;
            int done = console != null ? console.TotalAnswered : 0;
            int total = console != null ? console.TotalQuestions : 11;
            bool ready = console != null && console.AllComplete;
            bool submitted = Level3ForensicsManager.Instance != null &&
                Level3ForensicsManager.Instance.ReportSubmitted;
            reportStatus.text = submitted
                ? $"CASEWORK  {total} / {total}  ·  REPORT SUBMITTED  [OK]"
                : ready
                    ? $"CASEWORK  {total} / {total}  ·  PRESS E TO SUBMIT"
                    : $"CASEWORK  {done} / {total}  ·  REPORT LOCKED";
            reportStatus.color = ready ? new Color(0.35f, 1f, 0.55f) : new Color(0.82f, 0.92f, 1f);
        }

        private static void AddFillLight(Transform root, string name, Vector3 position,
            Color color, float intensity, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForceVertex;
        }

        private static void SpawnBeam(string name, Transform root, Vector3 from, Vector3 to, Material material)
        {
            Vector3 delta = to - from;
            GameObject beam = SpawnCube(name, root, (from + to) * 0.5f,
                new Vector3(0.05f, InlayHeight, delta.magnitude), material);
            beam.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        private static void SpawnOrthogonalPath(string name, Transform root,
            Vector3 from, Vector3 to, Material material)
        {
            // Route along the room's X/Z grid instead of drawing a direct
            // diagonal across it. The right-angle turn reads as intentional
            // wayfinding and no longer creates malformed-looking crossings.
            Vector3 corner = new Vector3(to.x, from.y, from.z);
            SpawnBeam(name + "_X", root, from, corner, material);
            SpawnBeam(name + "_Z", root, corner, to, material);
        }

        private static GameObject SpawnCube(string name, Transform root, Vector3 position,
            Vector3 scale, Material material)
        {
            return BuildKit.Spawn(PrimitiveType.Cube, name, root, position, scale, material, collider: false);
        }

        private static TMP_Text CreateWorldText(Transform root, string name, Vector3 position,
            string content, Color color, float scale, float width, float fontSize)
        {
            GameObject go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(root, false);
            go.transform.position = position;
            // These panels are all approached from the south (-Z); identity is
            // the readable side for a world TextMeshPro object in this project.
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;
            TextMeshPro text = go.GetComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = fontSize;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.fontSize = fontSize;
            text.rectTransform.sizeDelta = new Vector2(width, 4.8f);
            WorldTextLayoutIntent.Configure(go, WorldTextLayoutIntent.Mode.Mounted);
            return text;
        }

        private static void SetMaterial(Transform target, Material material)
        {
            if (target == null) return;
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private Material PanelMaterial()
        {
            if (panelMaterial == null)
                panelMaterial = BuildKit.MakeStandard(new Color(0.075f, 0.105f, 0.095f), 0.62f, 0.54f);
            return panelMaterial;
        }

        private Material FurnitureMaterial()
        {
            if (furnitureMaterial == null)
                furnitureMaterial = BuildKit.MakeStandard(new Color(0.095f, 0.135f, 0.125f), 0.66f, 0.48f);
            return furnitureMaterial;
        }

        /// <summary>Light gray lab floor with a quiet grid. Shared with the
        /// Level 2 locker sightline so the view through the wall matches.</summary>
        public static void ToneLabFloor(Material material) => ToneLabFloor(material, FloorBase);

        private static void ToneLabFloor(Material material, Color baseColor)
        {
            BuildKit.ToneGridFloor(material, baseColor, Palette.Tint("Level3", FloorLine));
            if (material == null || material.shader == null || material.shader.name != "Cyverse/GridFloor") return;
            // A bright floor needs less line glow and less shine than the old
            // dark one, or the grid and the ceiling reflections wash it out.
            material.SetFloat("_MinorEmission", 0.04f);
            material.SetFloat("_Emission", 0.30f);
            material.SetFloat("_Smoothness", 0.30f);
            material.SetFloat("_Metallic", 0.02f);
        }

        /// <summary>Zone inlays reuse the floor's world-space grid so lines run
        /// unbroken through each station zone; a lifted base and brighter lines
        /// mark the zone instead of a dark slab that blanked the grid.</summary>
        private Material FloorMaterial()
        {
            if (floorMaterial != null) return floorMaterial;
            floorMaterial = BuildKit.MakeGridFloor(FloorLine, PadBase);
            ToneLabFloor(floorMaterial, PadBase);
            return floorMaterial;
        }

        private Material GreenMaterial()
        {
            if (greenMaterial == null)
                greenMaterial = BuildKit.MakeEmissive(Palette.Tint("Level3", new Color(0.24f, 0.92f, 0.50f)), 0.85f);
            return greenMaterial;
        }

        private Material GoldMaterial()
        {
            if (goldMaterial == null)
                goldMaterial = BuildKit.MakeEmissive(new Color(0.90f, 0.64f, 0.22f), 0.8f);
            return goldMaterial;
        }
    }
}
