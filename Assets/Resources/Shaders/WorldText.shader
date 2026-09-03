// Depth-tested 3D text for CyVerse.
//
// Unity's built-in "GUI/Text Shader" — the material a Font hands out via
// font.material, which every TextMesh uses by default — is declared with
// ZTest Always. That's correct for screen overlays and completely wrong in a
// 3D world: every sign, label and station readout draws straight through
// walls, floors and props.
//
// This is the same shader with ZTest LEqual, so text is occluded by geometry
// in front of it. Still unlit, still alpha-blended from the font atlas, still
// no depth writes (so it never occludes anything itself).
Shader "Cyverse/WorldText"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Text Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        CGINCLUDE
        #include "UnityCG.cginc"

        struct appdata_t
        {
            float4 vertex   : POSITION;
            fixed4 color    : COLOR;
            float2 texcoord : TEXCOORD0;
        };

        struct v2f
        {
            float4 vertex   : SV_POSITION;
            fixed4 color    : COLOR;
            float2 texcoord : TEXCOORD0;
        };

        sampler2D _MainTex;
        float4 _MainTex_ST;
        fixed4 _Color;

        v2f vert(appdata_t v)
        {
            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.color = v.color * _Color;
            o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
            return o;
        }

        fixed GlyphAlpha(v2f i)
        {
            return tex2D(_MainTex, i.texcoord).a * i.color.a;
        }
        ENDCG

        // An alpha-clipped depth prepass lets a nearer label occlude a farther
        // one. The prior ZWrite-Off-only shader fixed text-vs-wall ordering but
        // could not resolve text-vs-text intersections.
        Pass
        {
            Lighting Off
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth

            fixed4 fragDepth(v2f i) : SV_Target
            {
                clip(GlyphAlpha(i) - 0.12);
                return 0;
            }
            ENDCG
        }

        Pass
        {
            Lighting Off
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragColor

            fixed4 fragColor(v2f i) : SV_Target
            {
                fixed4 col = i.color;
                col.a = GlyphAlpha(i);
                return col;
            }
            ENDCG
        }
    }

    FallBack "GUI/Text Shader"
}
