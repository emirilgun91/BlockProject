using UnityEngine;

namespace RogueBlockBlast.UI.FX
{
    /// <summary>
    /// Runtime'da üretilen yardımcı FX sprite'ları.
    /// Yeni art asset gerektirmez — hepsi prosedürel, statik olarak cache'lenir.
    ///
    /// - <see cref="OuterGlow"/>  : içi boş yumuşak halo (kartın dışına taşan glow için)
    /// - <see cref="RadialGlow"/> : merkezden dışa sönen dolu glow (satın alma patlaması için)
    /// - <see cref="ShineBand"/>  : yatay şeffaf→beyaz→şeffaf bant (metalik parlama süpürmesi için)
    /// </summary>
    public static class UIFXSprites
    {
        private static Sprite _outerGlow;
        private static Sprite _radialGlow;
        private static Sprite _shineBand;

        public static Sprite OuterGlow  => _outerGlow  ??= BuildOuterGlow();
        public static Sprite RadialGlow => _radialGlow ??= BuildRadialGlow();
        public static Sprite ShineBand  => _shineBand  ??= BuildShineBand();

        // ── Builders ─────────────────────────────────────────────────────────

        /// <summary>
        /// İçi boş halo. Alfa merkezde 0, kenara yakın tepe yapar, dış kenarda 0'a döner.
        /// Kart boyutunun ~1.25 katına ölçeklenerek "dış glow" izlenimi verir.
        /// </summary>
        private static Sprite BuildOuterGlow()
        {
            const int size = 128;
            var tex = NewTexture(size);
            var px  = new Color[size * size];

            // Kart, sprite'ın iç %80'lik kısmını kaplar → tepe noktası 0.80 civarı.
            const float inner = 0.72f; // buradan itibaren yükselmeye başlar
            const float peak  = 0.82f; // kart kenarı — en parlak
            const float outer = 1.00f; // sprite kenarı — tamamen şeffaf

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Merkeze göre normalize edilmiş "yuvarlatılmış kare" mesafesi
                float nx = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                float ny = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                float d  = RoundedBoxDistance(nx, ny);

                float a = d <= peak
                    ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, peak, d))
                    : Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(peak, outer, d));

                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }

            return Finish(tex, px, size);
        }

        /// <summary>Merkezde dolu, dışa doğru yumuşak sönen radyal glow.</summary>
        private static Sprite BuildRadialGlow()
        {
            const int size = 128;
            var tex = NewTexture(size);
            var px  = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float d  = Mathf.Sqrt(nx * nx + ny * ny);

                // Merkezde 1, kenarda 0 — kare alarak daha yumuşak bir düşüş
                float a = Mathf.Clamp01(1f - d);
                a *= a;

                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            return Finish(tex, px, size);
        }

        /// <summary>
        /// Yatay şeffaf→beyaz→şeffaf bant. Eğik yerleştirilip kart üzerinde
        /// süpürüldüğünde metalik parlama hissi verir.
        /// </summary>
        private static Sprite BuildShineBand()
        {
            const int w = 64;
            const int h = 8;
            var tex = NewTexture(w, h);
            var px  = new Color[w * h];

            for (int x = 0; x < w; x++)
            {
                float t = (x + 0.5f) / w;              // 0..1
                float a = Mathf.Sin(t * Mathf.PI);      // uçlarda 0, ortada 1
                a = Mathf.Pow(a, 2.2f);                 // dar ve keskin bir çizgi

                var c = new Color(1f, 1f, 1f, a);
                for (int y = 0; y < h; y++) px[y * w + x] = c;
            }

            return Finish(tex, px, w, h);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Süperelips ("yuvarlatılmış kare") mesafe alanı — merkezde 0, kenarda ~1.
        /// Köşeleri yumuşattığı için halo kart köşelerinde sivrilmez.
        /// </summary>
        private static float RoundedBoxDistance(float nx, float ny)
        {
            float x4 = nx * nx * nx * nx;
            float y4 = ny * ny * ny * ny;
            return Mathf.Pow(x4 + y4, 0.25f);
        }

        private static Texture2D NewTexture(int size) => NewTexture(size, size);

        private static Texture2D NewTexture(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags  = HideFlags.HideAndDontSave
            };

        private static Sprite Finish(Texture2D tex, Color[] px, int size) => Finish(tex, px, size, size);

        private static Sprite Finish(Texture2D tex, Color[] px, int w, int h)
        {
            tex.SetPixels(px);
            tex.Apply(false, true);

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
