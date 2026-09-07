using UnityEngine;

namespace RogueBlockBlast.UI.FX
{
    /// <summary>
    /// Özel hücre efektlerinin (Neon Cable / Phantom Cell / Safe Zone) ihtiyaç
    /// duyduğu sprite'ları kod içinde üretir.
    ///
    /// Neden asset değil — JuiceGraphics ile aynı gerekçe: bunlar halka, kıvılcım
    /// ve kıymık gibi tamamen matematiksel görseller. Asset olsalardı import
    /// ayarlarına ve dosya kayıplarına bağımlı olurlardı.
    ///
    /// Tüm sprite'lar 128x128 ve PPU 128 — yani dünya boyutları tam 1 birim.
    /// Böylece efekt kodu "0.9 tile büyüklüğünde olsun" derken doğrudan scale
    /// yazabiliyor, sprite boyutuna göre bölme yapmıyor.
    /// </summary>
    public static class OverlayFXGraphics
    {
        private const int Size = 128;
        private const float Ppu = 128f;

        private static Sprite _ring;
        private static Sprite _thickRing;
        private static Sprite _hexShield;
        private static Sprite _shard;
        private static Sprite _spark;
        private static Sprite _softDisc;
        private static Sprite _pixel;

        /// <summary>İnce parlak halka — şok dalgası için.</summary>
        public static Sprite Ring => _ring ??= BuildRing("FXRing", 0.86f, 0.055f);

        /// <summary>Kalın halka — kalkan çerçevesi için.</summary>
        public static Sprite ThickRing => _thickRing ??= BuildRing("FXThickRing", 0.80f, 0.13f);

        /// <summary>Altıgen kalkan çerçevesi — Safe Zone.</summary>
        public static Sprite HexShield => _hexShield ??= BuildHexShield();

        /// <summary>Üçgen kıymık — kalkan kırılması.</summary>
        public static Sprite Shard => _shard ??= BuildShard();

        /// <summary>İki ucu sivri kıvılcım — patlama serpintisi.</summary>
        public static Sprite Spark => _spark ??= BuildSpark();

        /// <summary>Ortası dolu, kenarı yumuşak disk — patlama çekirdeği.</summary>
        public static Sprite SoftDisc => _softDisc ??= BuildSoftDisc();

        /// <summary>Düz beyaz kare — LineRenderer ve çubuk şekilli parçalar.</summary>
        public static Sprite Pixel => _pixel ??= BuildPixel();

        // ── Builders ─────────────────────────────────────────────────────────

        private static Sprite BuildRing(string name, float radius, float thickness)
        {
            var tex = NewTexture(name);
            float half = (Size - 1) * 0.5f;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);

                // Halka merkezinden uzaklaştıkça karesel sönüm — kenar sert kesilmesin.
                float t = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / thickness);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, t * t));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        private static Sprite BuildHexShield()
        {
            var tex = NewTexture("FXHexShield");
            float half = (Size - 1) * 0.5f;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;

                // Altıgen mesafe alanı: altı kenarın normallerine izdüşümün maksimumu.
                float hex = 0f;
                for (int i = 0; i < 6; i++)
                {
                    float a = Mathf.PI / 6f + i * Mathf.PI / 3f;
                    hex = Mathf.Max(hex, dx * Mathf.Cos(a) + dy * Mathf.Sin(a));
                }

                float outline = Mathf.Clamp01(1f - Mathf.Abs(hex - 0.82f) / 0.10f);

                // Çerçevenin içi tamamen boş kalmasın: çok hafif bir cam dolgusu,
                // kalkanın kapladığı alan hücre sınırında okunsun diye.
                float fill = hex < 0.82f ? 0.13f * Mathf.SmoothStep(0f, 1f, hex / 0.82f) : 0f;

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(outline * outline, fill)));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        private static Sprite BuildShard()
        {
            var tex = NewTexture("FXShard");

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // Tabanı altta, tepesi üstte olan ince üçgen.
                float u = x / (float)(Size - 1);
                float v = y / (float)(Size - 1);
                float halfWidth = Mathf.Lerp(0.40f, 0.02f, v);
                float a = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) / Mathf.Max(halfWidth, 0.001f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * Mathf.Lerp(1f, 0.55f, v)));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        private static Sprite BuildSpark()
        {
            var tex = NewTexture("FXSpark");

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)(Size - 1);        // uzunluk ekseni
                float v = y / (float)(Size - 1);

                // Baştan sona incelen mekik: ortada kalın, uçlarda sivri.
                float taper = Mathf.Sin(u * Mathf.PI);
                float halfWidth = 0.24f * taper * taper;
                float a = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) / Mathf.Max(halfWidth, 0.001f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * taper));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        private static Sprite BuildSoftDisc()
        {
            var tex = NewTexture("FXSoftDisc");
            float half = (Size - 1) * 0.5f;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);

                // Merkezde tam opak bir çekirdek, sonra hızlı sönüm.
                float a = d < 0.32f ? 1f : Mathf.Clamp01(1f - (d - 0.32f) / 0.68f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        private static Sprite BuildPixel()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false)
            {
                name       = "FXPixel",
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags  = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, Color.white);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            sprite.name      = tex.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // ── Private ──────────────────────────────────────────────────────────

        private static Texture2D NewTexture(string name) =>
            new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
            {
                name       = name,
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags  = HideFlags.HideAndDontSave
            };

        private static Sprite ToSprite(Texture2D tex)
        {
            var sprite = Sprite.Create(
                tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Ppu);
            sprite.name      = tex.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
