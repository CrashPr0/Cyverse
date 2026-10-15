using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Cyverse.Core;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// The lighting and camera look for every room. In every tier:
    ///
    ///  - soft pools of light on the floor under each lamp, in its colour (the
    ///    forward renderer only lets two lights touch the big floor mesh, so
    ///    most lamps used to leave no trace on it);
    ///  - a top-down (trilight) ambient that gives shapes form;
    ///  - a box-projected room reflection probe, so the glossy floor and metal
    ///    props reflect the room's lights, screens and trim.
    ///
    /// High and Balanced add the HDR camera with bloom and a soft filmic
    /// shoulder (PostFX); High uses 4x MSAA and a sharper probe. Real-time
    /// shadows are deliberately left out: on these near-black floors they were
    /// invisible yet doubled the draw calls.
    ///
    /// The tier comes from Settings > Graphics. Until the player picks one, it
    /// starts at High and steps down on its own if the frame rate stays under
    /// 40 fps, remembering the result for the next scene.
    /// </summary>
    public class LightingDirector : MonoBehaviour
    {
        public enum Tier { Performance = 0, Balanced = 1, High = 2 }

        public static LightingDirector Instance { get; private set; }

        private const string ChosenKey = "cv_gfx";
        private const string AutoKey = "cv_gfx_auto";

        /// <summary>The tier in effect.</summary>
        public static Tier Current { get; private set; } = Tier.High;

        /// <summary>True when the player picked the tier (no auto stepping).</summary>
        public static bool Chosen => PlayerPrefs.HasKey(ChosenKey);

        private ReflectionProbe probe;

        public static LightingDirector Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            return host.AddComponent<LightingDirector>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Current = (Tier)Mathf.Clamp(PlayerPrefs.GetInt(ChosenKey, PlayerPrefs.GetInt(AutoKey, (int)Tier.High)), 0, 2);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            Ambient();
            NoSunShadows();
            Apply();
            StartCoroutine(CaptureReflections());
            if (!Chosen) StartCoroutine(WatchFrameRate());
        }

        /// <summary>The player chose a tier in Settings.</summary>
        public static void Choose(Tier tier)
        {
            PlayerPrefs.SetInt(ChosenKey, (int)tier);
            PlayerPrefs.Save();
            Current = tier;
            if (Instance != null) Instance.Apply();
        }

        /// <summary>Back to automatic (Settings cycles through "Auto").</summary>
        public static void ChooseAuto()
        {
            PlayerPrefs.DeleteKey(ChosenKey);
            PlayerPrefs.Save();
            Current = (Tier)Mathf.Clamp(PlayerPrefs.GetInt(AutoKey, (int)Tier.High), 0, 2);
            if (Instance != null) Instance.Apply();
        }

        // ---- Tier ---------------------------------------------------------------

        private void Apply()
        {
            Camera cam = Camera.main;
            bool post = Current >= Tier.Balanced;

            if (cam != null)
            {
                cam.allowHDR = post && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
                PostFX fx = cam.GetComponent<PostFX>();
                if (fx == null && post) fx = cam.gameObject.AddComponent<PostFX>();
                if (fx != null) fx.enabled = post;
            }

            QualitySettings.antiAliasing = Current == Tier.High ? 4 : 2;
            QualitySettings.realtimeReflectionProbes = true;
            if (probe != null)
            {
                int resolution = Current == Tier.High ? 256 : 128;
                if (probe.resolution != resolution) { probe.resolution = resolution; probe.RenderProbe(); }
            }
            HudVignette(!post);
            LightHalos(post);
        }

        /// <summary>No real-time shadows from the room's directional light
        /// (see the class notes): the ceiling slab would block it anyway.</summary>
        private static void NoSunShadows()
        {
            foreach (Light l in FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) l.shadows = LightShadows.None;
        }

        /// <summary>Top-down ambient (brighter from above, darker from below)
        /// around the room's existing ambient level, so shapes read as solid
        /// instead of evenly flat.</summary>
        private static void Ambient()
        {
            if (RenderSettings.ambientMode == AmbientMode.Trilight) return;
            Color a = RenderSettings.ambientLight;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(a.r * 1.18f, a.g * 1.22f, a.b * 1.3f);
            RenderSettings.ambientEquatorColor = a * 0.95f;
            RenderSettings.ambientGroundColor = new Color(a.r * 0.55f, a.g * 0.52f, a.b * 0.5f);
        }

        /// <summary>One box-projected reflection probe filling the room,
        /// rendered once the room's props exist (and once more after the
        /// level's own polish passes have finished building).</summary>
        private IEnumerator CaptureReflections()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            Bounds room = RoomBounds();
            var go = new GameObject("RoomReflections");
            go.transform.position = room.center;
            probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.resolution = Current == Tier.High ? 256 : 128;
            probe.hdr = true;
            probe.boxProjection = true;
            probe.size = room.size;
            probe.importance = 1;
            probe.intensity = 0.8f; // glossy, not mirror-like
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = RenderSettings.fogColor;
            probe.shadowDistance = 0f;
            probe.RenderProbe();
            SoftenGlossyFloors();
            LightPools();
            yield return new WaitForSecondsRealtime(3f);
            // The level's own polish passes may have added lamps and props.
            LightPools();
            if (probe != null) probe.RenderProbe();
        }

        // ---- Floor light pools ----------------------------------------------------

        private static Transform poolRoot;
        private const float MaxPoolRadius = 4.2f;

        /// <summary>A soft pool of light on the surface under each lamp, in the
        /// lamp's colour, following its brightness (scanners and stations that
        /// light up when active). Fill lights in vertex mode and broad fill
        /// spots are skipped, and no pool is wider than MaxPoolRadius.</summary>
        private static void LightPools()
        {
            Shader glow = Shader.Find("Cyverse/GlowSprite");
            if (glow == null) return;
            if (poolRoot == null) poolRoot = new GameObject("LightPools").transform;

            foreach (Light l in FindObjectsOfType<Light>())
            {
                if (l.type == LightType.Directional || l.type == LightType.Area) continue;
                if (l.renderMode == LightRenderMode.ForceVertex || !l.enabled) continue;
                if (l.GetComponent<LightPool>() != null) continue;
                // The diegetic screens' and phone's private glows sit right at
                // their glass; a pool would just be a smudge on the wall.
                if (l.range < 2.5f) continue;
                // A broad spot is a room fill, not a lamp: its "pool" would
                // be tens of metres wide and just haze the whole floor.
                if (l.type == LightType.Spot && l.spotAngle >= 120f) continue;

                Vector3 dir = l.type == LightType.Spot ? l.transform.forward : Vector3.down;
                if (!Physics.Raycast(l.transform.position, dir, out RaycastHit hit, l.range,
                        ~0, QueryTriggerInteraction.Ignore))
                    continue;
                if (hit.normal.y < 0.7f) continue; // floors and table tops only
                if (CoveredByProp(hit.point)) continue;

                float height = hit.distance;
                float radius = l.type == LightType.Spot
                    ? Mathf.Clamp(Mathf.Tan(l.spotAngle * 0.5f * Mathf.Deg2Rad) * height * 0.9f, 0.6f, MaxPoolRadius)
                    : Mathf.Clamp(l.range * 0.42f, 1.1f, MaxPoolRadius);
                float falloff = Mathf.Clamp01(1f - height / l.range);

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Pool_" + l.name;
                Object.Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(poolRoot, false);
                quad.transform.position = hit.point + hit.normal * 0.012f;
                quad.transform.rotation = Quaternion.LookRotation(-hit.normal);
                quad.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
                var mat = new Material(glow);
                Renderer r = quad.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                l.gameObject.AddComponent<LightPool>().Bind(l, mat, falloff);
            }
        }

        /// <summary>True when a low, collider-free prop (a dais, a plinth)
        /// stands over this floor point. The pool would sit under it and
        /// show only as a ring round its edge; the lamp lights the prop anyway.</summary>
        private static bool CoveredByProp(Vector3 floorPoint)
        {
            foreach (Renderer r in FindObjectsOfType<Renderer>())
            {
                if (!r.enabled || r.name.StartsWith("Pool_") || r.name == "LightGlow") continue;
                Bounds b = r.bounds;
                if (b.max.y < floorPoint.y + 0.02f || b.max.y > floorPoint.y + 1.0f) continue; // decals, tall props
                if (b.min.y > floorPoint.y + 0.05f) continue;                                     // not standing on it
                if (floorPoint.x < b.min.x || floorPoint.x > b.max.x ||
                    floorPoint.z < b.min.z || floorPoint.z > b.max.z) continue;
                return true;
            }
            return false;
        }

        /// <summary>The grid floors were tuned near-mirror (0.88 to 0.93
        /// smoothness) when they only reflected the sky. Against the room's
        /// own reflection that turns every wall light strip into a long
        /// blurred band across the floor, so the gloss is eased to a sheen.</summary>
        private static void SoftenGlossyFloors()
        {
            const float maxGloss = 0.7f;
            foreach (Renderer r in FindObjectsOfType<Renderer>())
            {
                Material m = r.sharedMaterial;
                if (m == null || m.shader == null || m.shader.name != "Cyverse/GridFloor") continue;
                if (!m.HasProperty("_Smoothness") || m.GetFloat("_Smoothness") <= maxGloss) continue;
                r.material.SetFloat("_Smoothness", maxGloss); // an instance: never edits a saved material
            }
        }

        /// <summary>The room shell's extent (the shared 40 x 40 m footprint and
        /// the height of its ceiling slab).</summary>
        private static Bounds RoomBounds()
        {
            float height = 5f;
            GameObject slab = GameObject.Find("CeilingSlab");
            if (slab != null && slab.GetComponent<Renderer>() != null)
                height = slab.GetComponent<Renderer>().bounds.min.y;
            return new Bounds(new Vector3(0f, height * 0.5f, 0f), new Vector3(40f, height, 40f));
        }

        private static void HudVignette(bool visible)
        {
            if (HudUI.Instance == null) return;
            Transform v = HudUI.Instance.Canvas.transform.Find("Vignette");
            if (v != null) v.gameObject.SetActive(visible);
        }

        /// <summary>The fake-bloom halos on point lights are halved while real
        /// bloom is on, so the two don't stack into haze.</summary>
        private static void LightHalos(bool realBloom)
        {
            foreach (Light l in FindObjectsOfType<Light>())
            {
                Transform glow = l.transform.Find("LightGlow");
                if (glow == null) continue;
                Renderer r = glow.GetComponent<Renderer>();
                if (r != null && r.material.HasProperty("_Intensity"))
                    r.material.SetFloat("_Intensity", realBloom ? 0.35f : 0.7f);
            }
        }

        // ---- Automatic tier -----------------------------------------------------

        /// <summary>Steps down one tier at a time while play runs under 40 fps
        /// (menus and loading excluded), and remembers where it settled.</summary>
        private IEnumerator WatchFrameRate()
        {
            yield return new WaitForSecondsRealtime(4f);
            while (!Chosen && Current > Tier.Performance)
            {
                float elapsed = 0f;
                int frames = 0;
                while (elapsed < 3f)
                {
                    if (!GameState.Busy) { elapsed += Time.unscaledDeltaTime; frames++; }
                    yield return null;
                }
                if (frames / elapsed >= 40f) yield break;
                if (Chosen) yield break;
                Current = Current - 1;
                PlayerPrefs.SetInt(AutoKey, (int)Current);
                PlayerPrefs.Save();
                Apply();
            }
        }
    }
}
