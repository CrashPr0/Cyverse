using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Animates the push-button cap of a <see cref="PedestalStyle.Button"/>
    /// pedestal. The cap is a skinned mesh with one blend shape, "Pressed"
    /// (cap pushed down ~1.5 cm with a slight rubbery bulge of its rim);
    /// <see cref="Press"/> drives that shape's weight 0 -> 100 -> 0 over
    /// <see cref="duration"/> seconds and, optionally, pulses the cap's
    /// emission in step with it.
    ///
    /// Runs on unscaled time (works while Time.timeScale is 0, e.g. behind a
    /// pause or quiz overlay) and only uses runtime APIs, so it is safe in a
    /// WebGL build. Update is enabled only while an animation plays, so an
    /// idle button costs nothing.
    ///
    /// The flash edits the cap renderer's *shared* material directly (no
    /// per-frame instancing); <see cref="PedestalFactory"/> gives every cap
    /// its own material, so pedestals never flash each other. If you wire this
    /// up by hand, give the cap a unique material or turn <see cref="flash"/>
    /// off.
    /// </summary>
    public sealed class PedestalButton : MonoBehaviour
    {
        public const string BlendShapeName = "Pressed";

        [Tooltip("The skinned cap carrying the \"Pressed\" blend shape.")]
        public SkinnedMeshRenderer capRenderer;

        [Tooltip("Total press-and-release time in seconds (unscaled).")]
        public float duration = 0.25f;

        [Range(0.1f, 0.9f), Tooltip("Share of the duration spent going down; the rest is the release.")]
        public float downFraction = 0.4f;

        [Tooltip("Pulse the cap's emission while pressed.")]
        public bool flash = true;
        public Color flashColor = Color.white;
        [Tooltip("Emission multiplier at rest / at full press.")]
        public float restEmission = 0.9f;
        public float flashEmission = 4f;

        private int _shape = -1;
        private Material _material;
        private float _weight;          // current blend-shape weight, 0..100
        private float _time;
        private float _startWeight;
        private bool _animating;
        private bool _warned;

        /// <summary>True while a press is animating.</summary>
        public bool IsPressing => _animating;

        /// <summary>Current "Pressed" weight, 0 (up) .. 100 (fully down).</summary>
        public float Weight => _weight;

        /// <summary>Hooks the button to its cap renderer and sets the flash
        /// color (the pedestal's accent). Safe to call again to re-target.</summary>
        public void Configure(SkinnedMeshRenderer renderer, Color accent)
        {
            capRenderer = renderer;
            flashColor = accent;
            Resolve();
            ApplyWeight(0f);
        }

        /// <summary>Recolors the flash and the resting glow.</summary>
        public void SetAccent(Color accent)
        {
            flashColor = accent;
            ApplyEmission(_weight);
        }

        /// <summary>Plays one press-and-release. Calling it mid-press restarts
        /// the push from the cap's current depth (no visual jump).</summary>
        public void Press()
        {
            if (capRenderer == null) return;
            if (_shape < 0 && _material == null) Resolve();
            _startWeight = _weight;
            _time = 0f;
            _animating = true;
            enabled = true;
        }

        /// <summary>Holds the cap at an explicit depth (0..100), cancelling any
        /// animation. Handy for a latched or disabled-looking button.</summary>
        public void SetWeight(float weight)
        {
            _animating = false;
            enabled = false;
            ApplyWeight(Mathf.Clamp(weight, 0f, 100f));
        }

        private void Awake()
        {
            Resolve();
            enabled = false;   // Update only runs during a press
        }

        private void Update()
        {
            // Clamp so a hitch (a WebGL tab regaining focus) can't skip the animation.
            _time += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float down = Mathf.Max(0.01f, duration * downFraction);
            float total = Mathf.Max(down + 0.01f, duration);

            float weight;
            if (_time < down)
            {
                float t = _time / down;
                weight = Mathf.Lerp(_startWeight, 100f, 1f - (1f - t) * (1f - t));   // ease-out: snaps down
            }
            else if (_time < total)
            {
                float t = (_time - down) / (total - down);
                weight = Mathf.Lerp(100f, 0f, t * t * (3f - 2f * t));                // smooth release
            }
            else
            {
                weight = 0f;
                _animating = false;
                enabled = false;
            }
            ApplyWeight(weight);
        }

        private void Resolve()
        {
            _shape = -1;
            _material = null;
            if (capRenderer == null) return;

            Mesh mesh = capRenderer.sharedMesh;
            if (mesh != null) _shape = mesh.GetBlendShapeIndex(BlendShapeName);
            if (_shape < 0 && !_warned)
            {
                _warned = true;
                Debug.LogWarning($"[PedestalButton] '{name}': cap mesh has no \"{BlendShapeName}\" blend shape; press will only flash.");
            }
            _material = capRenderer.sharedMaterial;
        }

        private void ApplyWeight(float weight)
        {
            _weight = weight;
            if (capRenderer != null && _shape >= 0) capRenderer.SetBlendShapeWeight(_shape, weight);
            ApplyEmission(weight);
        }

        private void ApplyEmission(float weight)
        {
            if (!flash || _material == null || !_material.HasProperty("_EmissionColor")) return;
            float k = Mathf.Lerp(restEmission, flashEmission, Mathf.Clamp01(weight / 100f));
            Color c = flashColor * k;
            c.a = 1f;
            _material.SetColor("_EmissionColor", c);
        }
    }
}
