using UnityEngine;

namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Tile değeri tabanlı skor hesabı.
    /// tileValueSum × comboMultiplier × globalMultiplier
    /// </summary>
    public sealed class ScoreSystem
    {
        /// <summary>
        /// Placement sonrası skoru hesaplar.
        /// tileValueSum     : temizlenen tile'ların toplam değeri (LineClearSystem'den gelir)
        /// comboMultiplier  : ComboSystem.Multiplier
        /// globalMultiplier : kart efektlerinden gelen çarpan (varsayılan 1.0)
        /// </summary>
        public int ResolveAfterPlacement(
            float tileValueSum,
            float comboMultiplier,
            float globalMultiplier = 1f)
        {
            if (tileValueSum <= 0f) return 0;

            float raw    = tileValueSum * comboMultiplier * globalMultiplier;
            return Mathf.RoundToInt(raw);
        }
    }
}