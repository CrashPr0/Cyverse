using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Handle on a pedestal built by <see cref="PedestalFactory"/>: which
    /// variant it is and the parts callers may want to touch. Lives on the
    /// pedestal root next to its collider (and, for the Button variant, a
    /// <see cref="PedestalButton"/>).
    /// </summary>
    public sealed class Pedestal : MonoBehaviour
    {
        public PedestalStyle Style { get; internal set; }
        public Color Accent { get; internal set; }

        /// <summary>Local height of the surface things rest on: the flat top
        /// of Plain / Button (the cap protrudes a little above it), or the
        /// centre of the display for Screen.</summary>
        public float SurfaceY { get; internal set; }

        /// <summary>The lathed body (gunmetal + accent submeshes).</summary>
        public MeshRenderer BodyRenderer { get; internal set; }

        /// <summary>This pedestal's own emissive accent material (body submesh 1).</summary>
        public Material AccentMaterial { get; internal set; }

        // ---- Screen variant -----------------------------------------------

        /// <summary>
        /// Screen variant only (else null): the display surface, built like a
        /// Unity Quad — unit-sized mesh scaled to <see cref="ScreenSize"/>,
        /// local +X right, local +Y up the slope, visible from its local -Z
        /// side (which faces the viewer, tilted up). To use a
        /// <c>DiegeticScreen</c>, parent its <c>ScreenTransform</c> here with
        /// zero position/identity rotation and scale (ScreenSize, 1), and hide
        /// <see cref="ScreenRenderer"/>.
        /// </summary>
        public Transform ScreenTransform { get; internal set; }
        public MeshRenderer ScreenRenderer { get; internal set; }

        /// <summary>Display size in metres (width, height along the slope).</summary>
        public Vector2 ScreenSize { get; internal set; }

        // ---- Button variant -----------------------------------------------

        /// <summary>Button variant only (else null).</summary>
        public PedestalButton Button { get; internal set; }
        public SkinnedMeshRenderer ButtonCap { get; internal set; }

        internal Material DefaultScreenMaterial;

        /// <summary>Recolors the accent (glow lines, default screen tint, button
        /// cap and flash) of this pedestal only.</summary>
        public void SetAccent(Color accent)
        {
            Accent = accent;
            if (AccentMaterial != null)
            {
                AccentMaterial.color = accent;
                AccentMaterial.SetColor("_EmissionColor", accent * PedestalFactory.AccentEmission);
            }
            if (DefaultScreenMaterial != null) PedestalFactory.TintScreenMaterial(DefaultScreenMaterial, accent);
            if (Button != null)
            {
                if (ButtonCap != null) PedestalFactory.TintCapMaterial(ButtonCap.sharedMaterial, accent, Button.restEmission);
                Button.SetAccent(accent);
            }
        }
    }
}
