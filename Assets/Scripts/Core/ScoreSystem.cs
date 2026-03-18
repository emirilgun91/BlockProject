using UnityEngine;

namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Skor hesaplar — combo multiplier dışarıdan verilir (ComboSystem'den gelir).
    /// Combo mantığı burada yok, ComboSystem'de.
    /// </summary>
    public sealed class ScoreSystem
    {
        /// <summary>
        /// Placement sonrası skoru hesaplar.
        /// multiplier → ComboSystem.Multiplier
        /// </summary>
        public int ResolveAfterPlacement(int clearedLineCount, float multiplier)
        {
            if (clearedLineCount <= 0) return 0;

            int baseScore  = clearedLineCount * 100;
            int finalScore = Mathf.RoundToInt(baseScore * multiplier);
            return finalScore;
        }
    }
}