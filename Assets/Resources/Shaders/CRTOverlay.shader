// Subtle, legibility-first CRT treatment for the password terminal UI.
// This is an overlay rather than a screen-warping post effect: it adds fine
// scanlines, a soft edge vignette, restrained phosphor noise, and a slow sweep
// without resampling or distorting the text beneath it.
Shader "Cyverse/CRTOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Tint ("Phosphor Tint", Color) = (0.25, 0.82, 1, 1)
        _ScanlineDensity ("Scanline Density", Range(80, 480)) = 210
        _ScanlineStrength ("Scanline Strength", Range(0, 0.25)) = 0.055
        _NoiseStrength ("Noise Strength", Range(0, 0.15)) = 0.008
        _SweepStrength ("Sweep Strength", Range(0, 0.15)) = 0.025
        _VignetteStrength ("Vignette Strength", Range(0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+200"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed4 _Tint;
            float _ScanlineDensity;
            float _ScanlineStrength;
            float _NoiseStrength;
            float _SweepStrength;
            float _VignetteStrength;
            float _CyMotion; // global: 1 = animate, 0 = Reduce Motion

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float Hash(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float motion = saturate(_CyMotion);

                // Narrow, static scanlines preserve glyph edges instead of
                // blurring or displacing the world-space canvas underneath.
                float scanWave = 0.5 + 0.5 * sin(
                    input.uv.y * _ScanlineDensity * 6.2831853);
                float scanline = pow(scanWave, 6.0) * _ScanlineStrength;

                // Rounded edge falloff gives the flat panel a little CRT depth.
                float2 edge = abs(input.uv - 0.5) * 2.0;
                float vignette = saturate(
                    (pow(edge.x, 3.0) + pow(edge.y, 3.0)) * 0.55);
                vignette *= _VignetteStrength;

                // Quantized noise reads like phosphor grain and remains stable
                // when Reduce Motion is enabled.
                float timeStep = floor(_Time.y * 24.0 * motion);
                float noise = (Hash(floor(input.uv * float2(640.0, 360.0)) + timeStep)
                    - 0.5) * 2.0 * _NoiseStrength * motion;

                float sweepPosition = frac(_Time.y * 0.07 * motion);
                float sweep = pow(saturate(
                    1.0 - abs(input.uv.y - sweepPosition) * 18.0), 3.0);
                sweep *= _SweepStrength * motion;

                float dark = saturate(scanline + vignette + max(-noise, 0.0));
                float glow = saturate(sweep + max(noise, 0.0));
                float alpha = saturate(dark + glow) * input.color.a;
                float glowMix = glow / max(dark + glow, 0.0001);
                float3 color = lerp(float3(0.003, 0.008, 0.012), _Tint.rgb, glowMix);

                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
    FallBack Off
}
