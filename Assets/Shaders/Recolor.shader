Shader "Hidden/Dearlife/Recolor"
{
    // Turns a green leaf card texture into blossom pink or autumn colours, keeping its shape (alpha) and its light and dark detail.
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Dark ("Dark", Color) = (0.4, 0.1, 0.1, 1)
        _Light ("Light", Color) = (1, 0.7, 0.3, 1)
        _Alt ("Second colour", Color) = (0.8, 0.3, 0.1, 1)
        _Mix ("Second colour amount", Range(0, 1)) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Dark, _Light, _Alt;
            float _Mix;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float lum = saturate(dot(c.rgb, float3(0.3, 0.59, 0.11)) * 2.4);
                // a patchwork of two colours, so a tree is not one flat shade
                float2 cell = floor(i.uv * 22.0);
                float n = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                float3 light = lerp(_Light.rgb, _Alt.rgb, step(1.0 - _Mix, n));
                float3 rgb = lerp(_Dark.rgb, light, lum);
                return float4(rgb, c.a);
            }
            ENDCG
        }
    }
}
