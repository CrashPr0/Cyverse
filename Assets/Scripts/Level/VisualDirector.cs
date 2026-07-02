using UnityEngine;
using UnityEngine.UI;
using Cyverse.Settings;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Runtime "cinematography" layer, added by Level0Manager if missing so it
    /// upgrades hand-built scenes and fresh builds alike:
    ///  - dark solid-colour sky (no daylight skybox), exponential fog, 4x MSAA
    ///  - fake bloom: an additive radial glow sprite on every point light
    ///  - drifting cyan dust motes (skipped under Reduce Motion)
    ///  - a subtle screen-space vignette behind the HUD
    /// Everything is generated in code; nothing here needs assets or a scene
    /// reference, and each step is idempotent.
    /// </summary>
    public class VisualDirector : MonoBehaviour
    {
        public static VisualDirector Instance { get; private set; }

        public Color fogColor = new Color(0.03f, 0.045f, 0.07f);
        public float fogDensity = 0.014f;
        public Color skyColor = new Color(0.015f, 0.02f, 0.035f);
        public float vignetteStrength = 0.45f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            ApplyAtmosphere();
            AttachLightGlows();
            BuildDust();
            BuildVignette();
            AnimateSigns();
        }

        /// <summary>Give bob/pulse/glitch motion to any signage that predates
        /// SignFX (scenes saved before it existed).</summary>
        private void AnimateSigns()
        {
            foreach (TextMesh tm in FindObjectsOfType<TextMesh>())
                if (tm.GetComponent<SignFX>() == null)
                    tm.gameObject.AddComponent<SignFX>();
        }

        private void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.skybox = null; // ambient comes from the flat colour set at build

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = skyColor;
            }

            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 60f);
        }

        private void AttachLightGlows()
        {
            Shader glowShader = Shader.Find("Cyverse/GlowSprite");
            if (glowShader == null) return;

            foreach (Light l in FindObjectsOfType<Light>())
            {
                if (l.type != LightType.Point) continue;
                if (l.transform.Find("LightGlow") != null) continue; // idempotent

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "LightGlow";
                var col = quad.GetComponent<Collider>();
                if (col != null) Destroy(col);

                quad.transform.SetParent(l.transform, false);
                quad.transform.localPosition = Vector3.zero;
                float size = Mathf.Clamp(l.range * 0.5f, 2f, 6f);
                quad.transform.localScale = new Vector3(size, size, 1f);

                var mat = new Material(glowShader);
                Color c = l.color;
                c.a = 0.35f;
                mat.SetColor("_Color", c);
                mat.SetFloat("_Intensity", 0.7f);
                quad.GetComponent<Renderer>().material = mat;

                quad.AddComponent<Billboard>();
            }
        }

        private void BuildDust()
        {
            if (AccessibilitySettings.ReduceMotion) return;
            if (GameObject.Find("DustMotes") != null) return;

            Shader glowShader = Shader.Find("Cyverse/GlowSprite");
            if (glowShader == null) return;

            try
            {
                var go = new GameObject("DustMotes");
                go.transform.position = new Vector3(0f, 2.5f, 0f);

                var ps = go.AddComponent<ParticleSystem>();
                // A fresh ParticleSystem is already playing (playOnAwake); some
                // module values may not be modified while playing, so stop and
                // clear before configuring, then start it again.
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = ps.main;
                main.loop = true;
                main.startLifetime = 14f;
                main.startSpeed = 0f;
                main.startSize = MinMax(0.02f, 0.07f);
                main.startColor = new Color(0.65f, 0.9f, 1f, 0.30f);
                main.maxParticles = 500;
                main.prewarm = true; // room is already dusty on arrival

                var emission = ps.emission;
                emission.rateOverTime = 22f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(34f, 4.6f, 34f);

                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = MinMax(-0.05f, 0.05f);
                vel.y = MinMax(-0.07f, 0.01f);
                vel.z = MinMax(-0.05f, 0.05f);

                var psRenderer = go.GetComponent<ParticleSystemRenderer>();
                var mat = new Material(glowShader);
                mat.SetColor("_Color", Color.white);
                mat.SetFloat("_Intensity", 1f);
                psRenderer.material = mat;

                ps.Play();
            }
            catch (System.Exception e)
            {
                // Atmosphere must never break gameplay — log and carry on.
                Debug.LogWarning("VisualDirector: dust motes disabled — " + e.Message);
            }
        }

        /// <summary>Random-between-two-constants curve, built via properties so
        /// it can't hit any constructor-overload ambiguity.</summary>
        private static ParticleSystem.MinMaxCurve MinMax(float min, float max)
        {
            var curve = new ParticleSystem.MinMaxCurve();
            curve.mode = ParticleSystemCurveMode.TwoConstants;
            curve.constantMin = min;
            curve.constantMax = max;
            return curve;
        }

        private void BuildVignette()
        {
            if (HudUI.Instance == null) return;
            if (HudUI.Instance.Canvas.transform.Find("Vignette") != null) return;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x / (float)(size - 1)) - 0.5f;
                    float dy = (y / (float)(size - 1)) - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.7071f; // 0 centre → 1 corner
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.45f) / 0.55f));
                    pixels[y * size + x] = new Color(0f, 0f, 0f, a * vignetteStrength);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            var go = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(HudUI.Instance.Canvas.transform, false);
            go.transform.SetAsFirstSibling(); // behind all other HUD elements
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            img.color = Color.white;
            img.raycastTarget = false;
        }
    }
}
