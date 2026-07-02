// Advanced holographic display shader for CyVerse. Built-in RP, transparent +
// additive. Combines a Fresnel edge glow, a procedural grid, fine scanlines, a
// sweeping scan bar, and flicker for a "live hologram" feel.
Shader "Cyverse/Hologram"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.8, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _ScanSpeed ("Scanline Speed", Float) = 2
        _ScanDensity ("Scanline Density", Float) = 60
        _GridDensity ("Grid Density", Float) = 10
        _GridWidth ("Grid Line Width", Range(0, 0.2)) = 0.03
        _BarSpeed ("Scan Bar Speed", Float) = 0.5
        _BarSize ("Scan Bar Size", Range(0.005, 0.4)) = 0.06
        _Alpha ("Base Alpha", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            fixed4 _Color;
            float _RimPower, _ScanSpeed, _ScanDensity;
            float _GridDensity, _GridWidth, _BarSpeed, _BarSize, _Alpha;
            float _CyMotion; // global: 1 = animate, 0 = Reduce Motion

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - wp);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float ndotv = saturate(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                float rim = pow(1.0 - ndotv, _RimPower);

                // procedural grid
                float2 g = abs(frac(i.uv * _GridDensity) - 0.5);
                float grid = 1.0 - smoothstep(0.0, _GridWidth, min(g.x, g.y));

                // fine horizontal scanlines
                float scan = 0.5 + 0.5 * sin(i.uv.y * _ScanDensity - _Time.y * _ScanSpeed * _CyMotion);

                // a bright bar sweeping upward
                float barPos = frac(_Time.y * _BarSpeed * _CyMotion);
                float bar = smoothstep(_BarSize, 0.0, abs(i.uv.y - barPos));

                float flicker = 0.92 + 0.08 * sin(_Time.y * 40.0 * _CyMotion);

                float intensity = (0.18 + rim * 1.4 + grid * 0.5 + scan * 0.18 + bar * 0.9) * flicker;
                fixed4 col = _Color * intensity;
                col.a = saturate((_Alpha + rim + grid * 0.25 + bar * 0.5) * flicker);

                // Soft falloff at the UV borders so panels read as projected
                // light instead of hard-edged rectangles.
                float2 eu = min(i.uv, 1.0 - i.uv);
                float edgeFade = saturate(min(eu.x, eu.y) / 0.10);
                col.a *= edgeFade;
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
