// The objective marker as seen THROUGH level geometry. ObjectiveBeacon draws
// its cube twice: the normal hologram pass (depth tested, so walls, signs and
// props hide it) and this pass, which only draws the pixels that something
// solid is covering. The result: in the open the marker looks as it always
// did; behind a sign or a wall it shows as a flat outlined cube, so the
// player can still see where to go.
//
// Alpha blended rather than additive so it stays readable over bright
// surfaces (light walls, lit signs), and drawn after everything else so glow
// sprites and other transparent decor never cover it.
Shader "Cyverse/ObjectiveMarkerGhost"
{
    Properties
    {
        _Color ("Color", Color) = (0.90, 0.66, 0.14, 1)
        _Fill ("Fill Alpha", Range(0, 1)) = 0.32
        _EdgeAlpha ("Edge Alpha", Range(0, 1)) = 0.95
        _Edge ("Edge Width", Range(0.01, 0.5)) = 0.16
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Greater
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _Fill, _EdgeAlpha, _Edge;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Each cube face has 0..1 UVs: a bright frame around a soft fill.
                float2 eu = min(i.uv, 1.0 - i.uv);
                float edge = 1.0 - smoothstep(_Edge * 0.6, _Edge, min(eu.x, eu.y));
                fixed4 col;
                col.rgb = _Color.rgb;
                col.a = lerp(_Fill, _EdgeAlpha, edge);
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
