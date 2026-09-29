// Şok dalgası halkası — prosedürel. Quad'ın merkezinden dışa doğru genişler.
//   _Radius : 0→1 halka yarıçapı (quad yarı-genişliği = 1)
//   _Width  : halka kalınlığı
//   _Fade   : genel sönüm
// Halkanın içinde, yarıçap büyüdükçe sönen hafif bir dolgu parlaması var —
// "patlamanın merkezi" okunur.
Shader "RogueBlockBlast/FX/LineClearRing"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Radius ("Radius", Range(0,1)) = 0.5
        _Width  ("Width",  Range(0.001,0.5)) = 0.08
        _Fade   ("Fade",   Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Blend  One One
        ZWrite Off
        ZTest  Always
        Cull   Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Radius;
                float  _Width;
                float  _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv         = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2.0;

                float d     = (r - _Radius) / _Width;
                // Dış kenar keskin, iç kenar uzun kuyruklu — hareket yönü okunur
                float ring  = d > 0 ? exp(-d * d * 3.0) : exp(-d * d * 0.9);
                float inner = saturate(1.0 - r / max(_Radius, 0.001)) * (1.0 - _Radius) * 0.04;
                float edge  = smoothstep(1.0, 0.92, r);

                float3 white = float3(1,1,1) * max(max(_Color.r, _Color.g), _Color.b);
                float3 col   = lerp(_Color.rgb, white, exp(-d * d * 8.0) * 0.6) * ring
                             + _Color.rgb * inner;

                return half4(col * edge * _Fade, 1);
            }
            ENDHLSL
        }
    }
}
