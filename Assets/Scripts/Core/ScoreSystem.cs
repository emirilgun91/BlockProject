using UnityEngine;

namespace RogueBlockBlast.Core
{
    public class ScoreSystem
    {
        private int _placementsDoneInCycle = 0;
        private float _comboBonus = 0f;

        public int ResolveAfterPlacement(int clearedLineCount)
        {
            bool anyClear = clearedLineCount > 0;

            if (anyClear && _placementsDoneInCycle <= 1)
            {
                _comboBonus += 0.1f;
            }

            int baseScore = clearedLineCount * 100;
            float multiplier = 1f + _comboBonus;

            int finalScore = Mathf.RoundToInt(baseScore * multiplier);

            _placementsDoneInCycle++;

            if (_placementsDoneInCycle >= 3)
            {
                _placementsDoneInCycle = 0;
                _comboBonus = 0f;
            }

            return finalScore;
        }
    }
}