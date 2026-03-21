// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
//
// Additive blit shader for sky layer compositing.
// Used by SkyRenderer GL immediate-mode calls to composite
// .properties-driven sky layers into the sky RenderTexture.
//
// output.rgb = tex.rgb * tex.a * vertexColor.a
// Blend One One (additive accumulation from black)
//
// Unlike Sprites/Default, does NOT premultiply twice —
// texture alpha and vertex alpha (layer fade) are applied once.

Shader "Hidden/PopVuj/SkyBlit"
{
    Properties
    {
        _MainTex ("", 2D) = "black" {}
    }

    SubShader
    {
        Blend One One
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                half   alpha : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = v.uv;
                o.alpha = v.color.a;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                return half4(tex.rgb * tex.a * i.alpha, 0);
            }
            ENDCG
        }
    }
}
