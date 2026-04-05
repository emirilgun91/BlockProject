Shader "Custom/RetrowaveGrid"
{
    Properties
    {
        _GridColor   ("Grid Color",   Color)  = (0, 0.8, 1, 1)
        _BgColor     ("Background Color", Color) = (0.02, 0.05, 0.12, 1)
        _GridScale   ("Grid Scale",   Float)  = 20.0
        _LineWidth   ("Line Width",   Float)  = 0.03
        _ScrollSpeed ("Scroll Speed", Float)  = 0.5
        _FadeStart   ("Fade Start",   Float)  = 0.3
        _GlowStr     ("Glow Strength",Float)  = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            float4 _GridColor;
            float4 _BgColor;
            float  _GridScale;
            float  _LineWidth;
            float  _ScrollSpeed;
            float  _FadeStart;
            float  _GlowStr;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UV'yi scroll et (sadece dikey eksen)
                float2 uv = i.uv;
                uv.y = frac(uv.y + _Time.y * _ScrollSpeed);

                // Grid koordinatları
                float2 scaled = uv * _GridScale;
                float2 gridUV = frac(scaled);

                // Çizgi kalınlığı (her iki eksen)
                float lx = step(gridUV.x, _LineWidth) + step(1.0 - _LineWidth, gridUV.x);
                float ly = step(gridUV.y, _LineWidth) + step(1.0 - _LineWidth, gridUV.y);
                float fline = saturate(lx + ly);

                // Yatay çizgilere glow ekle (dikey gradyan)
                float glowY = ly * exp(-gridUV.y * _GlowStr);
                fline = saturate(fline + glowY * 0.4);

                // Uzaklık fade (üst tarafa doğru silikleşme)
                float fade = smoothstep(0.0, _FadeStart, i.uv.y);
                fline *= fade;

                // Sonuç rengi
                fixed4 col = lerp(_BgColor, _GridColor, fline);
                return col;
            }
            ENDCG
        }
    }
}






/*1. Assets → Create → Material → "RetrowaveGridMat"
2. Shader alanını "Custom/RetrowaveGrid" yap
3. Sahneye bir Plane ekle (GameObject → 3D Object → Plane)
4. Plane'e bu materyali sürükle
5. Plane'i kameraya göre konumlandır:
   - Position:  (0, -2, 5)
   - Scale:     (5, 1, 10)
   - Rotation:  (0, 0, 0)
```

---

## 3. Perspektif Efekti İçin Kamera Ayarı

Görseldeki "zemin uzaklığa kaçıyor" hissini vermek için:
```
Main Camera:
  - Projection: Perspective
  - Field of View: 60
  - Position: (0, 1, 0)
  - Rotation: (15, 0, 0)   ← hafif aşağı baktır*/