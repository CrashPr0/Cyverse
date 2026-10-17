// Full-screen colour inversion for the Invert Colors setting. Drawn as the
// last UI layer, it writes white with a "one minus destination" blend, so every
// pixel already on screen (the room, the HUD, menus) becomes its opposite:
// out = 1 - dst. One quad, no render textures, so it costs nothing on WebGL.
// Alpha is left alone, or the browser would composite the canvas see-through.
Shader "Hidden/Cyverse/InvertOverlay"
{
    Properties
    {
        _MainTex ("Unused (UI)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend OneMinusDstColor Zero
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert (float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag () : SV_Target
            {
                return fixed4(1, 1, 1, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
