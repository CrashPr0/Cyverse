// Holographic display shader for CyVerse station panels and the centerpiece.
// Built-in Render Pipeline. Transparent + additive so it reads as glowing
// light: animated scanlines, a Fresnel rim, and a subtle flicker.
Shader "Cyverse/Hologram"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.8, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _ScanSpeed ("Scan Speed", Float) = 2
        _ScanDensity ("Scan Density", Float) = 40
        _Alpha ("Base Alpha", Range(0,1)) = 0.45
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
            float _RimPower, _ScanSpeed, _ScanDensity, _Alpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float ndotv = saturate(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                float rim = pow(1.0 - ndotv, _RimPower);
                float scan = 0.5 + 0.5 * sin((i.uv.y * _ScanDensity) - _Time.y * _ScanSpeed);
                float flicker = 0.92 + 0.08 * sin(_Time.y * 45.0);

                float intensity = (0.30 + rim + scan * 0.25) * flicker;
                fixed4 col = _Color * intensity;
                col.a = saturate((_Alpha + rim) * flicker);
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
