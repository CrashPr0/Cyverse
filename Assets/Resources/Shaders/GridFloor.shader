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

        float gridLines (float2 coord, float width)
        {
            float2 c = abs(frac(coord) - 0.5);
            return 1.0 - smoothstep(0.0, width, min(c.x, c.y));
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float scale = max(_GridScale, 0.001);
            float major = gridLines(IN.worldPos.xz / scale, _LineWidth);
            float minor = gridLines(IN.worldPos.xz / (scale * 0.25), _LineWidth * 0.6);

            float dist = length(IN.worldPos.xz);
            float fade = saturate(1.0 - dist / max(_FadeDistance, 0.001));

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
