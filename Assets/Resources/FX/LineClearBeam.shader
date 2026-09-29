// Temizlenen satır/sütun boyunca açılan enerji ışını — tamamen prosedürel.
// Quad'ın U ekseni hattın uzunluğu, V ekseni kalınlığı.
//   _Reveal : 0→1, ışın merkezden iki uca doğru açılır; önde parlak bir "kafa" koşar
//   _Fade   : genel sönüm
//   _Core   : ince beyaz çekirdeğin keskinliği (yüksek = daha ince)
// Additive çizilir; renk HDR verilir, bloom'u besler.
Shader "RogueBlockBlast/FX/LineClearBeam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Reveal ("Reveal", Range(0,1.2)) = 1
        _Fade   ("Fade",   Range(0,1))   = 1
        _Core   ("Core Sharpness", Float) = 60
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
                float  _Reveal;
                float  _Fade;
                float  _Core;
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

            float hash (float n) { return frac(sin(n) * 43758.5453); }

            half4 frag (Varyings i) : SV_Target
            {
                float along  = abs(i.uv.x - 0.5) * 2.0;   // 0 merkez, 1 uç
                float across = abs(i.uv.y - 0.5) * 2.0;   // 0 çekirdek, 1 kenar

                // Kesit: ince beyaz çekirdek + geniş yumuşak hale
                float core = exp(-across * across * _Core);
                float halo = exp(-across * 5.5) * 0.3;
                float rim  = saturate(1.0 - across);       // kenarda sıfıra in (quad kenarı görünmesin)

                // Merkezden açılma
                float open  = smoothstep(_Reveal + 0.02, _Reveal - 0.10, along);
                // Önde koşan parlak kafa
                float head  = exp(-pow((along - _Reveal) * 9.0, 2.0)) * step(_Reveal, 1.05);
                // Uçlarda incel
                float taper = smoothstep(1.0, 0.80, along);

                // Çekirdek boyunca hafif titreşen enerji (bantlar)
                float bands = 0.85 + 0.15 * sin(i.uv.x * 90.0 + _Reveal * 40.0);

                float3 tint  = _Color.rgb;
                float3 white = lerp(tint, float3(1,1,1) * max(max(tint.r, tint.g), tint.b), 0.65);

                float3 col = tint  * (halo * rim * 1.4) * open
                           + white * (core * 1.2 * bands) * open
                           + white * head * (core * 2.5 + halo * 1.2) * rim;

                col *= taper * _Fade;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
