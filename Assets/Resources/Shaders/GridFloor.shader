// Glowing tech-grid floor for CyVerse. Built-in Render Pipeline surface shader
// so it still receives lighting and looks glossy/reflective, with emissive grid
// lines driven by world position (independent of UVs / tiling).
Shader "Cyverse/GridFloor"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.05, 0.06, 0.09, 1)
        _LineColor ("Line Color", Color) = (0.2, 0.8, 1, 1)
        _GridScale ("Grid Cell Size (m)", Float) = 2
        _LineWidth ("Line Width", Range(0.001, 0.3)) = 0.04
        _Smoothness ("Smoothness", Range(0,1)) = 0.85
        _Metallic ("Metallic", Range(0,1)) = 0.3
        _Emission ("Emission Strength", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input { float3 worldPos; };

        fixed4 _BaseColor, _LineColor;
        float _GridScale, _LineWidth, _Smoothness, _Metallic, _Emission;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 coord = IN.worldPos.xz / max(_GridScale, 0.001);
            float2 cell = abs(frac(coord) - 0.5);
            float gl = 1.0 - smoothstep(0.0, _LineWidth, min(cell.x, cell.y));

            o.Albedo = _BaseColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Emission = _LineColor.rgb * gl * _Emission;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
