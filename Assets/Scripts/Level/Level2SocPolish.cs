using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Runtime finish pass for the Cyber Defense task room. The Level 2 visual
    /// scene predates the final SOC flow, so this layer gives saved and freshly
    /// generated scenes the same lighting, zoning and workstation treatment.
    /// Everything is decorative, collider-free and inexpensive enough for WebGL.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class Level2SocPolish : MonoBehaviour
    {
        private const string PolishRootName = "SOC_Polish";

        private Material panelMaterial;
        private Material furnitureMaterial;
        private Material floorInsetMaterial;
        private Material accentMaterial;
        private Material coolAccentMaterial;

        public static Level2SocPolish Ensure(GameObject host)
        {
            Level2SocPolish polish = Object.FindObjectOfType<Level2SocPolish>();
            if (polish == null) polish = host.AddComponent<Level2SocPolish>();
            return polish;
        }

        private void Start() => Apply();

        public void Apply()
        {
            ApplyAtmosphere();
            ToneCeilingAndFixtures();
            PolishWorkstations();
            BuildDecor();
        }

        private void ApplyAtmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.275f, 0.31f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.052f, 0.027f, 0.038f);
            RenderSettings.fogDensity = 0.010f;

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.022f, 0.016f, 0.026f);
            }
        }

        private void ToneCeilingAndFixtures()
        {
            Material ceiling = BuildKit.MakeEmissive(new Color(0.52f, 0.60f, 0.70f), 0.62f);
            foreach (Renderer renderer in FindObjectsOfType<Renderer>())
                if (renderer.name.StartsWith("CeilingPanel_")) renderer.sharedMaterial = ceiling;

            foreach (Light light in FindObjectsOfType<Light>())
            {
                if (!light.name.StartsWith("CeilingLight_") || light.transform.position.z < 1f) continue;
                light.color = new Color(0.78f, 0.84f, 0.95f);
                light.intensity = 0.78f;
                light.range = 12f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForceVertex;
            }
        }

        private void PolishWorkstations()
        {
            Material furniture = FurnitureMaterial();
            foreach (EndpointStation endpoint in FindObjectsOfType<EndpointStation>())
            {
                if (!endpoint.gameObject.activeInHierarchy) continue;
                SetMaterial(endpoint.transform.Find("Desk"), furniture);
                SetMaterial(endpoint.transform.Find("MonStand"), furniture);
                SetMaterial(endpoint.transform.Find("MonBody"), furniture);
            }
        }

        private void BuildDecor()
        {
            if (GameObject.Find(PolishRootName) != null) return;

            GameObject root = new GameObject(PolishRootName);
            BuildNavigationSpine(root.transform);
            BuildWorkstationWall(root.transform);
            BuildCommandZone(root.transform);
            BuildZoneLighting(root.transform);
            BuildMountedHeader(root.transform);
        }

        private void BuildNavigationSpine(Transform root)
        {
            SpawnCube("SOC_AisleInset", root, new Vector3(0f, 0.022f, 9.2f),
                new Vector3(3.4f, 0.028f, 13.2f), FloorInsetMaterial());
            SpawnCube("SOC_AisleRail_L", root, new Vector3(-1.66f, 0.043f, 9.2f),
                new Vector3(0.035f, 0.018f, 13.2f), AccentMaterial());
            SpawnCube("SOC_AisleRail_R", root, new Vector3(1.66f, 0.043f, 9.2f),
                new Vector3(0.035f, 0.018f, 13.2f), AccentMaterial());

            for (int z = 4; z <= 14; z += 5)
                SpawnCube("SOC_AisleMarker_" + z, root, new Vector3(0f, 0.046f, z),
                    new Vector3(0.78f, 0.018f, 0.045f), CoolAccentMaterial());
        }

        private void BuildWorkstationWall(Transform root)
        {
            for (int i = 0; i < 4; i++)
            {
                float z = 4.5f + i * 3f;
                Transform bay = new GameObject("SOC_WorkstationBay_" + (i + 1)).transform;
                bay.SetParent(root, false);

                SpawnCube("Backdrop", bay, new Vector3(-19.34f, 1.68f, z),
                    new Vector3(0.13f, 2.95f, 2.55f), PanelMaterial());
                SpawnCube("TopRail", bay, new Vector3(-19.24f, 3.12f, z),
                    new Vector3(0.08f, 0.055f, 2.28f), AccentMaterial());
                SpawnCube("FloorPad", bay, new Vector3(-17.38f, 0.028f, z),
                    new Vector3(3.55f, 0.035f, 2.55f), FloorInsetMaterial());
                SpawnCube("DeskGlow", bay, new Vector3(-15.66f, 0.075f, z),
                    new Vector3(0.035f, 0.055f, 1.65f), CoolAccentMaterial());
            }
        }

        private void BuildCommandZone(Transform root)
        {
            SpawnCube("SOC_CommandPad", root, new Vector3(-12.8f, 0.025f, 7f),
                new Vector3(5.2f, 0.032f, 4.8f), FloorInsetMaterial());
            SpawnCube("SOC_CommandRail_N", root, new Vector3(-12.8f, 0.052f, 9.38f),
                new Vector3(5.2f, 0.026f, 0.045f), AccentMaterial());
            SpawnCube("SOC_CommandRail_S", root, new Vector3(-12.8f, 0.052f, 4.62f),
                new Vector3(5.2f, 0.026f, 0.045f), AccentMaterial());
        }

        private void BuildZoneLighting(Transform root)
        {
            AddFillLight(root, "SOC_WorkstationFill", new Vector3(-15.8f, 3.6f, 9f),
                new Color(0.36f, 0.66f, 1f), 0.72f, 10f);
            AddFillLight(root, "SOC_CommandFill", new Vector3(-10.5f, 3.6f, 7f),
                new Color(1f, 0.38f, 0.28f), 0.62f, 9f);
            AddFillLight(root, "SOC_ResponseFill", new Vector3(0f, 3.7f, 13.5f),
                new Color(1f, 0.46f, 0.34f), 0.55f, 10f);
        }

        private void BuildMountedHeader(Transform root)
        {
            GameObject header = new GameObject("SOC_OperationsHeader", typeof(TextMeshPro));
            header.transform.SetParent(root, false);
            // Keep the plate inside one architectural bay. A long title across
            // the entire wall reads as broken text wherever a support column
            // correctly occludes it.
            header.transform.position = new Vector3(-19.17f, 3.82f, 6f);
            header.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            header.transform.localScale = Vector3.one * 0.070f;

            TextMeshPro text = header.GetComponent<TextMeshPro>();
            text.text = "ANALYST PODS";
            text.color = new Color(0.78f, 0.88f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = false;
            text.fontSize = 34f;
            text.rectTransform.sizeDelta = new Vector2(8f, 1.4f);
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

        private static GameObject SpawnCube(string name, Transform parent, Vector3 position,
            Vector3 scale, Material material)
        {
            return BuildKit.Spawn(PrimitiveType.Cube, name, parent, position, scale, material, collider: false);
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
                panelMaterial = BuildKit.MakeStandard(new Color(0.075f, 0.085f, 0.12f), 0.62f, 0.48f);
            return panelMaterial;
        }

        private Material FurnitureMaterial()
        {
            if (furnitureMaterial == null)
                furnitureMaterial = BuildKit.MakeStandard(new Color(0.095f, 0.11f, 0.155f), 0.68f, 0.52f);
            return furnitureMaterial;
        }

        private Material FloorInsetMaterial()
        {
            if (floorInsetMaterial == null)
                floorInsetMaterial = BuildKit.MakeStandard(new Color(0.035f, 0.038f, 0.055f), 0.42f, 0.36f);
            return floorInsetMaterial;
        }

        private Material AccentMaterial()
        {
            if (accentMaterial == null)
                accentMaterial = BuildKit.MakeEmissive(new Color(0.95f, 0.32f, 0.24f), 1.35f);
            return accentMaterial;
        }

        private Material CoolAccentMaterial()
        {
            if (coolAccentMaterial == null)
                coolAccentMaterial = BuildKit.MakeEmissive(new Color(0.28f, 0.72f, 1f), 1.05f);
            return coolAccentMaterial;
        }
    }
}
