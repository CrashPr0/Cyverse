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

        Lighting Off
        Cull Off
        ZWrite Off
        ZTest LEqual            // <- the fix
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Font atlases carry the glyph in alpha; colour comes from the
                // TextMesh's vertex colour.
                fixed4 col = i.color;
                col.a *= tex2D(_MainTex, i.texcoord).a;
                return col;
            }
            ENDCG
        }
    }

    FallBack "GUI/Text Shader"
}
