Shader "Hidden/Dearlife/SkinLayers"
{
    // The character creator's details, blended onto a person's skin texture once (PersonLook): skin tone, lipstick,
    // blush, eyeshadow, eyeliner, freckles, a beard in the hair colour and tattoos. The masks come from
    // tools/blender_skin_layers.py. Pass 1 recolours an iris texture for the eye colour.
    Properties
    {
        _MainTex ("Skin", 2D) = "white" {}
        _LayersA ("Lips, blush, eyeshadow, liner", 2D) = "black" {}
        _LayersB ("Freckles, beard, goatee, stubble", 2D) = "black" {}
        _LayersC ("Tattoos, scalp", 2D) = "black" {}
        _Skin ("Skin tone", Color) = (1, 1, 1, 1)
        _Lip ("Lipstick (a = amount)", Color) = (0.6, 0.1, 0.15, 0)
        _Blush ("Blush (a = amount)", Color) = (0.9, 0.5, 0.5, 0)
        _Shadow ("Eyeshadow (a = amount)", Color) = (0.4, 0.3, 0.3, 0)
        _Liner ("Eyeliner (a = amount)", Color) = (0.02, 0.02, 0.02, 0)
        _Freckles ("Freckles (a = amount)", Color) = (0.62, 0.45, 0.35, 0)
        _Beard ("Beard colour", Color) = (0.05, 0.04, 0.03, 1)
        _BeardStyle ("Full, goatee, stubble", Vector) = (0, 0, 0, 0)
        _Tattoo ("Band, rose, star", Vector) = (0, 0, 0, 0)
        _Ink ("Ink", Color) = (0.06, 0.08, 0.11, 1)
        _Iris ("Iris colour", Color) = (0.3, 0.2, 0.1, 1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _LayersA, _LayersB, _LayersC;
        float4 _Skin, _Lip, _Blush, _Shadow, _Liner, _Freckles, _Beard, _BeardStyle, _Tattoo, _Ink, _Iris;
        struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
        v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
        float Lum(float3 c) { return dot(c, float3(0.3, 0.59, 0.11)); }
        ENDCG

        Pass    // 0: the skin
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                float3 src = tex2D(_MainTex, i.uv).rgb;
                float3 c = src * _Skin.rgb;
                float4 A = tex2D(_LayersA, i.uv), B = tex2D(_LayersB, i.uv), C = tex2D(_LayersC, i.uv);
                // the hair painted on the scalp takes the person's hair colour, keeping its strands
                c = lerp(c, _Beard.rgb * (0.45 + 6.0 * Lum(src)), C.a);
                float l = Lum(c);
                // lipstick keeps the light and dark of the lips underneath
                c = lerp(c, _Lip.rgb * (0.55 + 1.2 * saturate(l * 1.8)), A.r * _Lip.a);
                // blush tints, it never paints over
                c = lerp(c, c * lerp(float3(1, 1, 1), _Blush.rgb * 1.35, 0.7), A.g * _Blush.a);
                c = lerp(c, _Shadow.rgb * (0.6 + 1.0 * saturate(l * 1.8)), A.b * _Shadow.a * 0.85);
                c = lerp(c, _Liner.rgb, A.a * _Liner.a);
                c = lerp(c, c * _Freckles.rgb, B.r * _Freckles.a);
                float beard = max(max(B.g * _BeardStyle.x, B.b * _BeardStyle.y), B.a * _BeardStyle.z);
                c = lerp(c, _Beard.rgb, saturate(beard));
                float ink = max(max(C.r * _Tattoo.x, C.g * _Tattoo.y), C.b * _Tattoo.z);
                c = lerp(c, _Ink.rgb * (0.75 + 0.8 * l), ink * 0.88);
                return float4(c, 1);
            }
            ENDCG
        }

        Pass    // 1: an iris in another colour, keeping its fibres
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                float4 t = tex2D(_MainTex, i.uv);
                float l = saturate(Lum(t.rgb) * 3.2);
                float3 c = _Iris.rgb * (0.35 + 1.3 * l) + l * l * 0.05;
                return float4(c, t.a);
            }
            ENDCG
        }
    }
}
