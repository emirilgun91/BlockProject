// Line clear partikülleri için unlit shader.
// Vertex rengi (partikülün kendi rengi) × doku × _Intensity.
// ParticleSystem vertex rengini 8-bit tuttuğu için 1'in üstüne çıkamaz;
// bloom'u besleyen HDR parlaklık _Intensity'den gelir.
// Blend modu materyalden seçilir: additive (One One) veya alpha (SrcAlpha OneMinusSrcAlpha).
Shader "RogueBlockBlast/FX/LineClearParticle"
{
    Properties
    {
        _MainTex   ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1
        _Additive  ("Additive (premultiply alpha)", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
            "PreviewType"    = "Plane"
        }

        Blend  [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest  Always
        Cull   Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Intensity;
                float  _Additive;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color      = v.color;
                o.uv         = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 c   = tex * i.color;
                c.rgb    *= _Intensity;
                // Additive modda (One One) alfa rengin içine katlanmalı,
                // alpha modda zaten SrcAlpha ile çarpılıyor — ikisinde de doğru.
                c.rgb    *= lerp(1.0h, c.a, (half)_Additive);
                return c;
            }
            ENDHLSL
        }
    }
}
