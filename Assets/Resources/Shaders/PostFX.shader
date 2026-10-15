// Cyverse camera post-processing, built for WebGL budgets: a dual-filter
// bloom (half resolution, a few tiny passes) and one composite pass that
// adds the bloom, rolls off highlights with a soft filmic shoulder (mid-tones
// and darks are left exactly as authored), applies a light grade
// (contrast, saturation, split tone), a vignette and a dither that removes
// banding from the dark gradients and fog. Driven by PostFX.cs.
Shader "Hidden/Cyverse/PostFX"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;

    float4 _Threshold;      // x: threshold, y: threshold - knee, z: 2 * knee, w: 0.25 / knee
    float _BloomIntensity;
    float4 _BloomTint;
    float _Exposure;
    float _Contrast;
    float _Saturation;
    float4 _ShadowTint;
    float4 _HighlightTint;
    float _Vignette;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert (appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 Sample (float2 uv) { return tex2D(_MainTex, uv).rgb; }

    // Dual-filter downsample: the centre plus four diagonal taps.
    half3 Down (float2 uv)
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(-1.0, -1.0, 1.0, 1.0);
        half3 s = Sample(uv) * 4.0;
        s += Sample(uv + d.xy);
        s += Sample(uv + d.zy);
        s += Sample(uv + d.xw);
        s += Sample(uv + d.zw);
        return s * (1.0 / 8.0);
    }

    // 9-tap tent upsample.
    half3 Up (float2 uv)
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(1.0, 1.0, -1.0, 0.0);
        half3 s = Sample(uv - d.xy);
        s += Sample(uv - d.wy) * 2.0;
        s += Sample(uv - d.zy);
        s += Sample(uv + d.zw) * 2.0;
        s += Sample(uv) * 4.0;
        s += Sample(uv + d.xw) * 2.0;
        s += Sample(uv + d.zy);
        s += Sample(uv + d.wy) * 2.0;
        s += Sample(uv + d.xy);
        return s * (1.0 / 16.0);
    }

    // Soft-knee threshold: only bright pixels (emissive trim, screens, hot
    // light pools) feed the bloom, with a smooth ramp instead of a hard cut.
    half3 Prefilter (half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half rq = clamp(br - _Threshold.y, 0.0, _Threshold.z);
        rq = _Threshold.w * rq * rq;
        half w = max(rq, br - _Threshold.x) / max(br, 1e-4);
        return c * w;
    }

    // Soft shoulder: identity below the knee, then an exponential roll-off
    // that eases bright HDR values (emissives, light pools, bloom) into white
    // instead of clipping. Darks and mid-tones are untouched, so the scenes
    // keep their authored look.
    half3 Shoulder (half3 x)
    {
        const half knee = 0.72;
        half3 over = max(x - knee, 0.0);
        return min(x, knee) + (1.0 - knee) * (1.0 - exp(-over / (1.0 - knee)));
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: threshold + first downsample (full -> half resolution)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                half3 c = min(Down(i.uv), 64.0); // tame single-pixel fireflies
                return half4(Prefilter(c), 1.0);
            }
            ENDCG
        }

        // 1: downsample
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target { return half4(Down(i.uv), 1.0); }
            ENDCG
        }

        // 2: upsample, added onto the next larger level
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target { return half4(Up(i.uv), 1.0); }
            ENDCG
        }

        // 3: composite
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                half3 c = tex2D(_MainTex, i.uv).rgb;
                c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity * _BloomTint.rgb;

                c = Shoulder(max(c, 0.0) * _Exposure);

                half luma = dot(c, half3(0.2126, 0.7152, 0.0722));
                c = lerp(luma.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                c *= lerp(_ShadowTint.rgb, _HighlightTint.rgb, saturate(luma));

                float2 d = (i.uv - 0.5) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                c *= 1.0 - _Vignette * smoothstep(0.15, 0.75, dot(d, d));

                // Triangular-ish dither, about one 8-bit step.
                float n = frac(sin(dot(i.uv * _ScreenParams.xy, float2(12.9898, 78.233))) * 43758.5453);
                float n2 = frac(n * 7.13 + 0.37);
                c += (n + n2 - 1.0) / 255.0;

                return half4(saturate(c), 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
