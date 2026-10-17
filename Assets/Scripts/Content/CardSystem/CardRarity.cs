using UnityEngine;

namespace RogueBlockBlast.Content
{
    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic
    }

    /// <summary>
    /// Rarity renkleri — kart çerçevesi, envanter slotu ve debug paneli aynı
    /// paleti okur. Rare ile Epic eskiden ikisi de mordu; 76px'lik slotta
    /// ayırt edilmiyordu. Ton farkı artık belirgin: gri → teal → mavi → magenta.
    /// </summary>
    public static class CardRarityPalette
    {
        public static readonly Color Common   = Hex(0x7A8BA6);
        public static readonly Color Uncommon = Hex(0x1FB89A);
        public static readonly Color Rare     = Hex(0x3D7BFF);
        public static readonly Color Epic     = Hex(0xC042E8);

        public static Color Get(CardRarity rarity) => rarity switch
        {
            CardRarity.Common   => Common,
            CardRarity.Uncommon => Uncommon,
            CardRarity.Rare     => Rare,
            CardRarity.Epic     => Epic,
            _                   => Common,
        };

        private static Color Hex(int rgb) => new Color32(
            (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);
    }
}
