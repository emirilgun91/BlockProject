using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Arayüzün renk tokenları — tek yerde. Kart çerçevelerinden alındı: koyu
    /// lacivert yüzeyler, ince çelik kenar, renk yalnızca vurgu için.
    ///
    /// Builder'lar (pause, ayarlar, HUD) UI'ı kurarken, runtime bileşenleri
    /// (<see cref="KitButton"/>, <see cref="KitSwitch"/>) durum renkleri için okur.
    /// Kart rarity'lerinin kendi paleti var: <c>CardRarityPalette</c>.
    /// </summary>
    public static class UiTheme
    {
        // Yüzeyler, koyudan açığa
        public static readonly Color Scrim        = new Color(0.02f, 0.03f, 0.06f, 0.82f);
        public static readonly Color Sunken       = Hex(0x0B0F1A);
        public static readonly Color Surface      = Hex(0x12131F);
        public static readonly Color Raised       = Hex(0x1B2238);
        public static readonly Color RaisedHover  = Hex(0x243050);

        // Çizgiler
        public static readonly Color Rim          = Hex(0x7A8BA6);
        public static readonly Color RimSoft      = new Color(0.478f, 0.545f, 0.651f, 0.35f);

        // Yazı
        public static readonly Color Text         = Hex(0xE6EAF5);
        public static readonly Color TextDim      = Hex(0x8A93AA);

        // Vurgular
        public static readonly Color Accent       = Hex(0x3DD6F5);   // birincil eylem, odak
        public static readonly Color AccentDeep   = Hex(0x123447);   // birincil buton dolgusu
        public static readonly Color Warm         = Hex(0xFFB547);   // skor, coin
        public static readonly Color Danger       = Hex(0xFF5A6E);
        public static readonly Color DangerDeep   = Hex(0x3A1520);
        public static readonly Color Progress     = Hex(0x8F7CFF);   // milestone ilerlemesi
        public static readonly Color Success      = Hex(0x1FB89A);   // milestone tamam

        /// <summary>Köşe yarıçapı (canvas pikseli), eleman boyutuna göre.</summary>
        public const float RadiusWindow = 24f, RadiusPanel = 18f, RadiusControl = 12f, RadiusChip = 6f;

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        static Color Hex(int rgb) => new Color32(
            (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);
    }
}
