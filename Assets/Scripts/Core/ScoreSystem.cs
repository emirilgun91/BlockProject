using UnityEngine;

namespace RogueBlockBlast.Core
{
    public class ScoreSystem
    {
        private int   _placementsDoneInCycle = 0;
        private float _comboBonus            = 0f;

        /// <summary>
        /// Şu anki combo multiplier — RunStatsTracker'a beslemek için kullanılır.
        /// Örnek: comboBonus 0.3 ise CurrentMultiplier = 1.3
        /// </summary>
        public float CurrentMultiplier => 1f + _comboBonus;

        public int ResolveAfterPlacement(int clearedLineCount)
        {
            bool anyClear = clearedLineCount > 0;

            if (anyClear && _placementsDoneInCycle <= 1)
                _comboBonus += 0.1f;

            int   baseScore  = clearedLineCount * 100;
            float multiplier = CurrentMultiplier;

            int finalScore = Mathf.RoundToInt(baseScore * multiplier);

            _placementsDoneInCycle++;

            if (_placementsDoneInCycle >= 3)
            {
                _placementsDoneInCycle = 0;
                _comboBonus            = 0f;
            }

            return finalScore;
        }
    }
}