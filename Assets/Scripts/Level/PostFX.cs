using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Camera post-processing for the built-in pipeline (see PostFX.shader):
    /// a half-resolution dual-filter bloom fed by an HDR camera, a soft filmic
    /// shoulder on the highlights, a subtle grade, a vignette and a dither. About six small passes,
    /// so it stays cheap enough for WebGL. LightingDirector turns it on and off
    /// with the graphics setting.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        public bool bloom = true;
        [Tooltip("Brightness where bloom starts (HDR camera). Emissive trim and screens sit above it.")]
        public float bloomThreshold = 0.95f;
        [Range(0f, 1f)] public float bloomKnee = 0.6f;
        public float bloomIntensity = 0.45f;
        public Color bloomTint = new Color(1f, 0.98f, 0.96f);

        [Tooltip("Exposure before the highlight shoulder; 1 keeps the authored brightness.")]
        public float exposure = 1.0f;
        public float contrast = 1.0f;
        public float saturation = 1.04f;
        public Color shadowTint = new Color(0.98f, 1.0f, 1.03f);   // slightly cool shadows
        public Color highlightTint = new Color(1.02f, 1.0f, 0.98f); // slightly warm highlights
        [Range(0f, 1f)] public float vignette = 0.25f;

        private const int PassPrefilter = 0, PassDown = 1, PassUp = 2, PassComposite = 3;
        private const int MaxLevels = 6;

        private Material material;
        private readonly RenderTexture[] levels = new RenderTexture[MaxLevels];
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        private static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private static readonly int BloomTintId = Shader.PropertyToID("_BloomTint");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int ShadowTintId = Shader.PropertyToID("_ShadowTint");
        private static readonly int HighlightTintId = Shader.PropertyToID("_HighlightTint");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");

        private bool EnsureMaterial()
        {
            if (material != null) return true;
            Shader shader = Shader.Find("Hidden/Cyverse/PostFX");
            if (shader == null || !shader.isSupported) return false;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!EnsureMaterial())
            {
                Graphics.Blit(source, destination);
                enabled = false;
                return;
            }

            bool hdr = source.format == RenderTextureFormat.ARGBHalf ||
                       source.format == RenderTextureFormat.ARGBFloat ||
                       source.format == RenderTextureFormat.DefaultHDR ||
                       source.format == RenderTextureFormat.RGB111110Float;
            RenderTexture bloomResult = null;
            if (bloom && bloomIntensity > 0f)
                bloomResult = RenderBloom(source, hdr);

            material.SetTexture(BloomTexId, bloomResult != null ? (Texture)bloomResult : Texture2D.blackTexture);
            material.SetFloat(BloomIntensityId, bloomResult != null ? bloomIntensity : 0f);
            material.SetColor(BloomTintId, bloomTint);
            material.SetFloat(ExposureId, exposure);
            material.SetFloat(ContrastId, contrast);
            material.SetFloat(SaturationId, saturation);
            material.SetColor(ShadowTintId, shadowTint);
            material.SetColor(HighlightTintId, highlightTint);
            material.SetFloat(VignetteId, vignette);
            Graphics.Blit(source, destination, material, PassComposite);

            for (int i = 0; i < MaxLevels; i++)
            {
                if (levels[i] == null) continue;
                RenderTexture.ReleaseTemporary(levels[i]);
                levels[i] = null;
            }
        }

        private RenderTexture RenderBloom(RenderTexture source, bool hdr)
        {
            // Without an HDR buffer nothing exceeds 1, so start a little lower.
            float threshold = hdr ? bloomThreshold : Mathf.Min(bloomThreshold, 0.8f);
            float knee = Mathf.Max(threshold * bloomKnee, 1e-4f);
            material.SetVector(ThresholdId, new Vector4(threshold, threshold - knee, knee * 2f, 0.25f / knee));

            RenderTextureFormat format = hdr ? source.format : RenderTextureFormat.Default;
            int width = Mathf.Max(1, source.width / 2), height = Mathf.Max(1, source.height / 2);
            int count = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Min(width, height), 2f)) - 3, 1, MaxLevels);

            levels[0] = RenderTexture.GetTemporary(width, height, 0, format);
            levels[0].filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, levels[0], material, PassPrefilter);
            for (int i = 1; i < count; i++)
            {
                width = Mathf.Max(1, width / 2);
                height = Mathf.Max(1, height / 2);
                levels[i] = RenderTexture.GetTemporary(width, height, 0, format);
                levels[i].filterMode = FilterMode.Bilinear;
                Graphics.Blit(levels[i - 1], levels[i], material, PassDown);
            }
            for (int i = count - 2; i >= 0; i--)
                Graphics.Blit(levels[i + 1], levels[i], material, PassUp);
            return levels[0];
        }
    }
}
