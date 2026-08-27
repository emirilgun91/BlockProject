using UnityEngine;

namespace RogueBlockBlast.UI.Juice
{
    /// <summary>
    /// Juice efektlerinin ihtiyaç duyduğu basit sprite'ları kod içinde üretir.
    ///
    /// Neden asset değil: bunlar radyal glow, yumuşak parlama şeridi ve dalga
    /// gibi tamamen matematiksel görseller. Asset olarak tutulsalar import
    /// ayarlarına, palet değişikliklerine ve dosya kayıplarına bağımlı olurdu.
    /// Üretilenler statik olarak önbelleğe alınır — sahnede kaç bileşen olursa
    /// olsun her sprite bir kez yaratılır.
    /// </summary>
    public static class JuiceGraphics
    {
        private static Sprite _radialGlow;
        private static Sprite _streak;
        private static Sprite _wave;

        /// <summary>Merkezden dışa solan yumuşak daire — glow halesi için.</summary>
        public static Sprite RadialGlow
        {
            get
            {
                if (_radialGlow != null) return _radialGlow;

                const int size = 128;
                var tex = NewTexture(size, size, "JuiceRadialGlow");

                float half = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);

                    // Kenarda sert kesim yerine kareli sönüm — hale daha yumuşak okunur.
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                tex.Apply();

                _radialGlow = ToSprite(tex);
                return _radialGlow;
            }
        }

        /// <summary>Saydam → beyaz → saydam yatay şerit — parlama süpürmesi için.</summary>
        public static Sprite Streak
        {
            get
            {
                if (_streak != null) return _streak;

                const int w = 64;
                var tex = NewTexture(w, 4, "JuiceStreak");

                for (int x = 0; x < w; x++)
                {
                    float t = x / (float)(w - 1);
                    // Ortada tepe yapan yumuşak eğri.
                    float a = Mathf.Sin(t * Mathf.PI);
                    a = a * a * a;

                    for (int y = 0; y < 4; y++)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                tex.Apply();

                _streak = ToSprite(tex);
                return _streak;
            }
        }

        /// <summary>
        /// Yatayda tekrar eden yumuşak dalga — dolan barın "akan su" hissi için.
        /// Kenarları birbirine uyar (seamless), böylece kaydırıldığında dikiş görünmez.
        /// </summary>
        public static Sprite Wave
        {
            get
            {
                if (_wave != null) return _wave;

                const int w = 128;
                const int h = 32;
                var tex = NewTexture(w, h, "JuiceWave");
                tex.wrapMode = TextureWrapMode.Repeat;

                for (int x = 0; x < w; x++)
                {
                    float t = x / (float)w;

                    // İki farklı frekans üst üste — tek sinüsten daha organik.
                    float a = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f
                            + Mathf.Sin(t * Mathf.PI * 6f + 1.3f) * 0.25f;

                    a = Mathf.Clamp01(a * 0.5f + 0.5f);

                    for (int y = 0; y < h; y++)
                    {
                        // Üstte biraz daha parlak — ışığın yüzeyde kırılması gibi.
                        float vertical = Mathf.Lerp(0.55f, 1f, y / (float)(h - 1));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * vertical));
                    }
                }
                tex.Apply();

                _wave = ToSprite(tex);
                return _wave;
            }
        }

        // ── Private ──────────────────────────────────────────────────────────

        private static Texture2D NewTexture(int w, int h, string name) =>
            new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
            {
                name       = name,
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags  = HideFlags.HideAndDontSave
            };

        private static Sprite ToSprite(Texture2D tex)
        {
            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);

            sprite.name      = tex.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
