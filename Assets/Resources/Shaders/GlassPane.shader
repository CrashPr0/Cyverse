// A quiet, clear pane for the evidence locker, not a projected display.
Shader "Cyverse/GlassPane"
{
    Properties
    {
        _Color ("Glass Tint", Color) = (0.62, 0.79, 0.87, 1)
        _Alpha ("Glass Opacity", Range(0, 0.3)) = 0.075
        _ReflectionStrength ("Reflection Strength", Range(0, 0.5)) = 0.24
        _EdgeOpacity ("Beveled Edge", Range(0, 0.5)) = 0.28
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
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
            float _Alpha, _ReflectionStrength, _EdgeOpacity;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float facing = abs(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                float fresnel = pow(1.0 - saturate(facing), 4.0);
                float2 borders = min(i.uv, 1.0 - i.uv);
                float bevel = 1.0 - smoothstep(0.008, 0.025, min(borders.x, borders.y));

                // Two soft, stationary reflection ribbons make the surface
                // readable as glass without obscuring the adjoining room.
                float diagonal = i.uv.x + i.uv.y * 0.58;
                float ribbon = smoothstep(0.55, 0.62, diagonal)
                    * (1.0 - smoothstep(0.76, 0.84, diagonal));
                float fineRibbon = smoothstep(0.89, 0.90, diagonal)
                    * (1.0 - smoothstep(0.92, 0.95, diagonal));
                float reflection = ribbon * 0.55 + fineRibbon * 0.35;
                fixed3 color = lerp(_Color.rgb, fixed3(0.93, 0.98, 1.0),
                    saturate(reflection + bevel * 0.5 + fresnel * 0.4));
                float alpha = _Alpha + reflection * _ReflectionStrength
                    + bevel * _EdgeOpacity + fresnel * 0.12;
                return fixed4(color, saturate(alpha));
            }
            ENDCG
        }
    }
    FallBack Off
}
