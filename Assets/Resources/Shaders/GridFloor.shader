// Advanced glowing tech-grid floor for CyVerse. Built-in RP surface shader so it
// stays glossy/lit. Adds a major + minor grid, distance fade, grazing-angle
// brightening, and a pulse ring travelling outward from the room centre.
Shader "Cyverse/GridFloor"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.05, 0.06, 0.09, 1)
        _LineColor ("Line Color", Color) = (0.2, 0.8, 1, 1)
        _GridScale ("Major Cell Size (m)", Float) = 4
        _LineWidth ("Line Width", Range(0.001, 0.3)) = 0.03
        _MinorEmission ("Minor Grid Emission", Range(0, 2)) = 0.35
        _Smoothness ("Smoothness", Range(0,1)) = 0.88
        _Metallic ("Metallic", Range(0,1)) = 0.35
        _Emission ("Emission Strength", Float) = 1.8
        _FadeDistance ("Fade Distance (m)", Float) = 26
        _PulseStrength ("Pulse Strength", Range(0, 3)) = 0.5
        _PulseSpeed ("Pulse Speed", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
        };

        fixed4 _BaseColor, _LineColor;
        float _GridScale, _LineWidth, _MinorEmission, _Smoothness, _Metallic;
        float _Emission, _FadeDistance, _PulseStrength, _PulseSpeed;
        float _CyMotion; // global: 1 = animate, 0 = Reduce Motion

        // Anti-aliased line mask. coord is in cell units; offset picks where the
        // lines fall (0 = cell edges at half-integers, 0.5 = at integers).
        // Lines never draw thinner than ~1.5 px (thinner ones dim instead of
        // breaking into dashes), and cells under ~2 px fade out to avoid moire.
        float gridLines (float2 coord, float halfWidth, float offset)
        {
            float2 deriv = max(fwidth(coord), 1e-5);
            float2 d = abs(frac(coord + offset) - 0.5);
            float2 w = max(halfWidth.xx, deriv * 0.75);
            float2 l = 1.0 - smoothstep(w - deriv, w + deriv, d);
            l *= saturate(halfWidth / w);
            l *= saturate(1.5 - deriv * 3.0);
            return max(l.x, l.y);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float scale = max(_GridScale, 0.001);
            float2 cell = IN.worldPos.xz / scale;
            // Major lines sit at half-cells (e.g. +-2, +-6 m for 4 m cells) so a
            // doorway on x = 0 is framed by a cell. Minor lines are an exact
            // quarter subdivision of the SAME lattice, so every 4th minor line
            // lands on a major one instead of between them.
            float major = gridLines(cell, _LineWidth * 0.5, 0.0);
            float minor = gridLines(cell * 4.0, _LineWidth * 0.3, 0.5);

            float dist = length(IN.worldPos.xz);
            // Fade with distance from the viewer, like atmosphere, rather than
            // from the room centre (which dimmed every corner regardless of view).
            float viewDist = length(IN.worldPos.xz - _WorldSpaceCameraPos.xz);
            float fade = 1.0 - smoothstep(_FadeDistance * 0.4, _FadeDistance * 1.4, viewDist);

            // pulse ring travelling outward from the centre
            float ring = sin(dist * 0.6 - _Time.y * _PulseSpeed * _CyMotion);
            ring = smoothstep(0.85, 1.0, ring);

            float lines = saturate(major + minor * _MinorEmission);
            float emit = lines * fade + ring * _PulseStrength * fade;

            o.Albedo = _BaseColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Emission = _LineColor.rgb * emit * _Emission;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
