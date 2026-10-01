// Satır tamamlama ipucu çerçevesi — prosedürel, yuvarlatılmış dikdörtgen.
// Quad, hattın kapladığı alanı biraz taşan boyutta çizilir; kenar kalınlığı
// dünya biriminde sabit kalsın diye quad boyutu _Size ile geçilir.
//   _Size       quad'ın dünya boyutu (x, y)
//   _Inset      çerçevenin quad kenarından içeri mesafesi (dünya birimi)
//   _Radius     köşe yuvarlaklığı
//   _Thickness  parlak çizginin kalınlığı
//   _Glow       dış hale genişliği
//   _Sweep      0..1 — hat boyunca akan ışığın konumu (<0 = kapalı)
//   _Fade       genel görünürlük
// Additive çizilir; renk HDR verilir, bloom'u besler.
Shader "RogueBlockBlast/FX/LineHintOutline"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Size      ("Size", Vector) = (1,1,0,0)
        _Inset     ("Inset", Float) = 0.1
        _Radius    ("Radius", Float) = 0.12
        _Thickness ("Thickness", Float) = 0.025
        _Glow      ("Glow", Float) = 0.12
        _Sweep     ("Sweep", Float) = -1
        _Fade      ("Fade", Range(0,1)) = 1
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
                float4 _Size;
                float  _Inset;
                float  _Radius;
                float  _Thickness;
                float  _Glow;
                float  _Sweep;
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

            // Yuvarlatılmış kutunun işaretli uzaklığı (içeride negatif)
            float sdRoundBox (float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - halfSize + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 size = _Size.xy;
                float2 p    = (i.uv - 0.5) * size;
                float  d    = sdRoundBox(p, size * 0.5 - _Inset, _Radius);

                // İnce parlak çizgi + dışa doğru yumuşak hale (içeri hiçbir şey çizilmez)
                float edgeLine = exp(-pow(d / max(_Thickness, 1e-4), 2.0));
                float glow  = exp(-max(d, 0.0) / max(_Glow, 1e-4)) * 0.45 * step(0.0, d);
                float fill  = 0.0;   // iç dolgu yok — bloklar kendi renginde kalsın

                // Hat boyunca akan ışık: uzun eksende ilerleyen bir bant
                float along = size.x >= size.y ? i.uv.x : i.uv.y;
                float sweep = _Sweep >= 0.0
                    ? exp(-pow((along - _Sweep) * 7.0, 2.0)) * 1.6
                    : 0.0;

                float3 tint  = _Color.rgb;
                float3 white = float3(1,1,1) * max(max(tint.r, tint.g), tint.b);

                float3 col = tint * (edgeLine * (1.0 + sweep) + glow * (1.0 + sweep * 0.5) + fill)
                           + white * edgeLine * sweep * 0.5;

                // Quad kenarında sıfıra in (glow kesik görünmesin)
                float2 e = min(i.uv, 1.0 - i.uv) * size;
                float edge = saturate(min(e.x, e.y) / max(_Inset * 0.6, 1e-4));

                return half4(col * edge * _Fade, 1);
            }
            ENDHLSL
        }
    }
}
