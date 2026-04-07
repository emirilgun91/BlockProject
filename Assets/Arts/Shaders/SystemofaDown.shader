Shader "RogueBlockBlast/SystemofaDown"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color         ("Tint",            Color) = (1,1,1,1)
        _GlowColor     ("Glow Color",      Color) = (1,1,1,1)
        _GlowIntensity ("Glow Intensity", Float) = 0
        _GlowPulseSpeed("Pulse Speed",    Float) = 0
        _RainbowActive ("Rainbow Active", Float) = 0
        _RainbowSpeed  ("Rainbow Speed",  Float) = 1
        _RainbowSaturation("Rainbow Sat", Float) = 1
        _ShockwaveT    ("Shockwave T",    Float) = 0
        _ShockwaveColor("Shockwave Color",Color) = (0,1,0.5,1)
        _ShockwaveWidth("Shockwave Width",Float) = 0.08
        _NoiseAmount   ("Noise Amount",   Float) = 0
        _Alpha         ("Alpha",          Float) = 1
    }
    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "RenderPipeline"    = "UniversalPipeline"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _GlowColor;
                float  _GlowIntensity;
                float  _GlowPulseSpeed;
                float  _RainbowActive;
                float  _RainbowSpeed;
                float  _RainbowSaturation;
                float  _ShockwaveT;
                float4 _ShockwaveColor;
                float  _ShockwaveWidth;
                float  _NoiseAmount;
                float  _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                
                // DÜZELTME 1: Sprite'lar için TRANSFORM_TEX kullanmak Atlaslama yaparken bozar. 
                // UV'yi doğrudan alıyoruz.
                OUT.uv         = IN.uv; 
                OUT.color      = IN.color * _Color;
                return OUT;
            }

            float3 HsvToRgb(float h, float s, float v)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(h + K.xyz) * 6.0 - K.www);
                return v * lerp(K.xxx, saturate(p - K.xxx), s);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float4 col = tex * IN.color;

                float pulse = (_GlowPulseSpeed > 0.001) 
                    ? 0.65 + 0.35 * sin(_Time.y * _GlowPulseSpeed * 6.28318) 
                    : 1.0;

                float3 glowCol = _GlowColor.rgb;

                if (_RainbowActive > 0.5)
                {
                    float hue = frac(IN.uv.x + IN.uv.y - _Time.y * _RainbowSpeed * 0.5);
                    glowCol   = HsvToRgb(hue, _RainbowSaturation, 1.0);
                }

                // DÜZELTME 2: tex.a ile çarparak glow efektinin sadece sprite'ın dolu kısımlarında olmasını sağladık.
                // Aksi takdirde şeffaf kısımlar da parlayıp objeyi bembeyaz bir kareye çevirir.
                col.rgb += glowCol * _GlowIntensity * pulse * tex.a;

                if (_ShockwaveT > 0.001 && _ShockwaveT < 0.999)
                {
                    float2 c    = IN.uv - 0.5;
                    float  dist = length(c) * 2.0;
                    float  ring = 1.0 - saturate(abs(dist - _ShockwaveT) / _ShockwaveWidth);
                    float  fade = 1.0 - _ShockwaveT;

                    col.rgb = lerp(col.rgb, _ShockwaveColor.rgb * 2.0, ring * fade);
                    col.a   = max(col.a, ring * fade * 0.8);
                }

                col.a *= _Alpha;
                return col;
            }
            ENDHLSL
        }
    }
}