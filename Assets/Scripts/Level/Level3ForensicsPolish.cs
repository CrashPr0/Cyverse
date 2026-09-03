using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Cyverse.Forensics;
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

        private Material panelMaterial;
        private Material furnitureMaterial;
        private Material floorMaterial;
        private Material greenMaterial;
        private Material goldMaterial;
        private TMP_Text reportStatus;
        private ForensicsConsole console;

        private void Start()
        {
            ApplyAtmosphere();
            ToneFixtures();
            BuildLab();
            BindProgress();
        }

        private void OnDestroy()
        {
            if (console == null || console.Cases == null) return;
            foreach (InvestigationCase investigation in console.Cases)
            {
                investigation.QuestionAnswered -= RefreshReport;
                investigation.CaseCompleted -= RefreshReport;
            }
        }

        private void ApplyAtmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.31f, 0.285f);
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

        private void BuildLab()
        {
            if (GameObject.Find(RootName) != null) return;
            GameObject root = new GameObject(RootName);

            BuildWorkflowHeader(root.transform);
            BuildAnalysisZone(root.transform);
            BuildAcquisitionZone(root.transform);
            BuildEvidenceZone(root.transform);
            BuildReportingZone(root.transform);
            BuildWorkflowPath(root.transform);
            BuildFillLights(root.transform);
            PolishInvestigationDesk();
        }

        private void BuildWorkflowHeader(Transform root)
        {
            CreateWorldText(root, "DF_WorkflowHeader", new Vector3(0f, 4.05f, 19.18f),
                "ACQUIRE   ›   ANALYZE   ›   CORRELATE   ›   REPORT",
                new Color(0.72f, 1f, 0.82f), 0.064f, 20f, 34f);
            SpawnCube("DF_HeaderRail", root, new Vector3(0f, 3.62f, 19.28f),
                new Vector3(17f, 0.045f, 0.055f), GreenMaterial());
        }

        private void BuildAnalysisZone(Transform root)
        {
            SpawnCube("DF_AnalysisPad", root, new Vector3(0f, 0.026f, 10f),
                new Vector3(7.4f, 0.035f, 6.2f), FloorMaterial());
            SpawnCube("DF_AnalysisRail_L", root, new Vector3(-3.66f, 0.052f, 10f),
                new Vector3(0.04f, 0.022f, 6.0f), GreenMaterial());
            SpawnCube("DF_AnalysisRail_R", root, new Vector3(3.66f, 0.052f, 10f),
                new Vector3(0.04f, 0.022f, 6.0f), GreenMaterial());
        }

        private void BuildAcquisitionZone(Transform root)
        {
            SpawnCube("DF_AcquisitionPad", root, new Vector3(-11.5f, 0.026f, 8f),
                new Vector3(5.2f, 0.035f, 4.6f), FloorMaterial());
            SpawnCube("DF_IntakePlinth", root, new Vector3(-11.5f, 0.48f, 8.15f),
                new Vector3(2.6f, 0.92f, 1.65f), FurnitureMaterial());
            SpawnCube("DF_EvidenceCase", root, new Vector3(-11.5f, 1.04f, 8.1f),
                new Vector3(1.75f, 0.20f, 1.0f), PanelMaterial());
            SpawnCube("DF_EvidenceSeal", root, new Vector3(-11.5f, 1.16f, 7.58f),
                new Vector3(1.20f, 0.045f, 0.035f), GoldMaterial());

            string evidenceText = SocProgress.TryGetEvidence(out var evidence)
                ? $"01  ACQUISITION\n{evidence.computer} DISK IMAGE  ·  CUSTODY VERIFIED"
                : "01  ACQUISITION\nTRAINING IMAGE  ·  NO CAMPAIGN HANDOFF";
            CreateWorldText(root, "DF_IntakeStatus", new Vector3(-11.5f, 1.55f, 7.28f),
                evidenceText, new Color(0.78f, 0.92f, 1f), 0.046f, 10f, 27f);
        }

        private void BuildEvidenceZone(Transform root)
        {
            SpawnCube("DF_EvidencePad", root, new Vector3(-8f, 0.025f, 15f),
                new Vector3(6.2f, 0.034f, 4.2f), FloorMaterial());
            SpawnCube("DF_EvidenceRail", root, new Vector3(-8f, 0.052f, 12.92f),
                new Vector3(5.6f, 0.022f, 0.045f), GoldMaterial());
            CreateWorldText(root, "DF_CorrelationLabel", new Vector3(-8f, 3.55f, 14.82f),
                "03  CORRELATE FINDINGS", new Color(0.92f, 0.72f, 0.30f), 0.048f, 10f, 28f);
        }

        private void BuildReportingZone(Transform root)
        {
            SpawnCube("DF_ReportingPad", root, new Vector3(9.2f, 0.026f, 10.5f),
                new Vector3(5.4f, 0.035f, 5.0f), FloorMaterial());
            SpawnCube("DF_ReportDesk", root, new Vector3(9.2f, 0.50f, 11f),
                new Vector3(3.0f, 1.0f, 1.25f), FurnitureMaterial());
            SpawnCube("DF_ReportMonitor", root, new Vector3(9.2f, 1.52f, 11.15f),
                new Vector3(2.7f, 1.25f, 0.08f), PanelMaterial());
            SpawnCube("DF_ReportScreen", root, new Vector3(9.2f, 1.52f, 11.09f),
                new Vector3(2.45f, 1.04f, 0.025f), BuildKit.MakeEmissive(new Color(0.04f, 0.18f, 0.11f), 0.72f));
            CreateWorldText(root, "DF_ReportHeader", new Vector3(9.2f, 2.34f, 11.02f),
                "04  FORENSIC REPORT", new Color(0.72f, 1f, 0.82f), 0.045f, 9f, 28f);
            reportStatus = CreateWorldText(root, "DF_ReportStatus", new Vector3(9.2f, 1.53f, 11.00f),
                "CASEWORK  0 / 14\nREPORT LOCKED", new Color(0.82f, 0.92f, 1f), 0.037f, 8f, 25f);
        }

        private void BuildWorkflowPath(Transform root)
        {
            SpawnBeam("DF_Path_AcquireToAnalyze", root, new Vector3(-9.3f, 0.055f, 8f),
                new Vector3(-3.8f, 0.055f, 9.2f), GreenMaterial());
            SpawnBeam("DF_Path_AnalyzeToEvidence", root, new Vector3(-2.4f, 0.055f, 12.5f),
                new Vector3(-5.4f, 0.055f, 13.8f), GoldMaterial());
            SpawnBeam("DF_Path_EvidenceToReport", root, new Vector3(-4.9f, 0.055f, 15f),
                new Vector3(6.5f, 0.055f, 11.8f), GreenMaterial());
        }

        private void BuildFillLights(Transform root)
        {
            AddFillLight(root, "DF_IntakeFill", new Vector3(-11.5f, 3.2f, 8f),
                new Color(0.42f, 0.72f, 1f), 0.58f, 8f);
            AddFillLight(root, "DF_AnalysisFill", new Vector3(0f, 3.5f, 10f),
                new Color(0.30f, 1f, 0.55f), 0.68f, 10f);
            AddFillLight(root, "DF_ReportFill", new Vector3(9.2f, 3.2f, 10.5f),
                new Color(0.88f, 0.68f, 0.28f), 0.52f, 8f);
        }

        private void PolishInvestigationDesk()
        {
            ForensicsConsole found = FindObjectOfType<ForensicsConsole>();
            if (found == null) return;
            SetMaterial(found.transform.Find("Desk"), FurnitureMaterial());
            foreach (Renderer renderer in found.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.StartsWith("MonBody_")) renderer.sharedMaterial = PanelMaterial();
        }

        private void BindProgress()
        {
            console = FindObjectOfType<ForensicsConsole>();
            if (console != null && console.Cases != null)
            {
                foreach (InvestigationCase investigation in console.Cases)
                {
                    investigation.QuestionAnswered += RefreshReport;
                    investigation.CaseCompleted += RefreshReport;
                }
            }
            RefreshReport();
        }

        private void RefreshReport()
        {
            if (reportStatus == null) return;
            int done = console != null ? console.TotalAnswered : 0;
            int total = console != null ? console.TotalQuestions : 14;
            bool complete = console != null && console.AllComplete;
            reportStatus.text = complete
                ? $"CASEWORK  {total} / {total}\nREPORT READY FOR RELEASE"
                : $"CASEWORK  {done} / {total}\nREPORT LOCKED — ANALYSIS IN PROGRESS";
            reportStatus.color = complete ? new Color(0.35f, 1f, 0.55f) : new Color(0.82f, 0.92f, 1f);
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
                new Vector3(0.05f, 0.020f, delta.magnitude), material);
            beam.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
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
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;
            TextMeshPro text = go.GetComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.fontSize = fontSize;
            text.rectTransform.sizeDelta = new Vector2(width, 2.4f);
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
                panelMaterial = BuildKit.MakeStandard(new Color(0.055f, 0.075f, 0.075f), 0.62f, 0.54f);
            return panelMaterial;
        }

        private Material FurnitureMaterial()
        {
            if (furnitureMaterial == null)
                furnitureMaterial = BuildKit.MakeStandard(new Color(0.075f, 0.11f, 0.105f), 0.66f, 0.48f);
            return furnitureMaterial;
        }

        private Material FloorMaterial()
        {
            if (floorMaterial == null)
                floorMaterial = BuildKit.MakeStandard(new Color(0.022f, 0.046f, 0.038f), 0.40f, 0.34f);
            return floorMaterial;
        }

        private Material GreenMaterial()
        {
            if (greenMaterial == null)
                greenMaterial = BuildKit.MakeEmissive(new Color(0.24f, 0.92f, 0.50f), 1.18f);
            return greenMaterial;
        }

        private Material GoldMaterial()
        {
            if (goldMaterial == null)
                goldMaterial = BuildKit.MakeEmissive(new Color(0.90f, 0.64f, 0.22f), 1.08f);
            return goldMaterial;
        }
    }
}
