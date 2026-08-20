using System;
using UnityEngine;

namespace RogueBlockBlast.Content
{
    /// <summary>
    /// Seviye başına artan bir değer eğrisi — upgrade kazancı ve maliyeti için.
    ///
    /// İki şekilde kullanılır, ikisi birlikte de çalışır:
    ///
    ///  1) <see cref="Steps"/> ile birebir değer listesi (en net kontrol):
    ///       Steps = [3, 5, 7, 9]        → seviye 1'de 3, 2'de 5, 3'te 7, 4'te 9
    ///       Steps = [100,200,400,600,800]
    ///
    ///  2) Liste bittikten sonra formülle devam:
    ///       sonraki = önceki × Multiplier + Increment
    ///     Multiplier=1, Increment=200 → 800, 1000, 1200, ...
    ///     Multiplier=2, Increment=0   → 800, 1600, 3200, ...
    ///
    /// <see cref="MaxValue"/> tavanı (0 = sınırsız), <see cref="MaxLevel"/> ise
    /// kaç kez yükseltilebileceğini sınırlar (0 = Steps uzunluğu kadar).
    ///
    /// Seviyeler 1 tabanlıdır: <c>GetValue(1)</c> ilk yükseltmenin değeridir.
    /// </summary>
    [Serializable]
    public struct UpgradeCurve
    {
        [Tooltip("Seviye başına birebir değerler. Örn: 3,5,7,9 veya 100,200,400,600,800")]
        public int[] Steps;

        [Tooltip("Steps bittikten sonra her seviyede önceki değerin çarpanı. 1 = sadece Increment uygulanır.")]
        public float Multiplier;

        [Tooltip("Steps bittikten sonra her seviyede eklenen sabit miktar.")]
        public int Increment;

        [Tooltip("Değer tavanı. 0 = sınırsız.")]
        public int MaxValue;

        [Tooltip("Maksimum yükseltme seviyesi. 0 = Steps uzunluğu kadar (Steps de boşsa sınırsız).")]
        public int MaxLevel;

        public bool HasSteps => Steps != null && Steps.Length > 0;

        /// <summary>Kaç kez yükseltilebilir. 0 = sınırsız.</summary>
        public int EffectiveMaxLevel => MaxLevel > 0 ? MaxLevel : (HasSteps ? Steps.Length : 0);

        /// <summary>Bu eğri hiç ayarlanmamış mı? (eski alanlara düşülmesi gerekir)</summary>
        public bool IsEmpty => !HasSteps && Multiplier <= 0f && Increment == 0;

        /// <summary>
        /// 1 tabanlı seviyenin değeri. Steps varsa oradan, bittiyse formülle devam eder,
        /// MaxValue'ya kırpılır.
        /// </summary>
        public int GetValue(int level)
        {
            if (level < 1) return 0;

            int value;
            if (HasSteps)
            {
                if (level <= Steps.Length) value = Steps[level - 1];
                else
                {
                    value = Steps[Steps.Length - 1];
                    for (int i = Steps.Length; i < level; i++)
                        value = Grow(value);
                }
            }
            else
            {
                // Steps yoksa Increment tek başına baz kabul edilir: 1×, 2×, 3× ...
                value = Increment > 0 ? Increment : 0;
                for (int i = 1; i < level; i++) value = Grow(value);
            }

            if (MaxValue > 0) value = Mathf.Min(value, MaxValue);
            return value;
        }

        private int Grow(int value)
        {
            float m = Multiplier > 0f ? Multiplier : 1f;
            return Mathf.RoundToInt(value * m) + Increment;
        }

        /// <summary>
        /// 1..level arası değerlerin toplamı. Puan kazancı için kullanılır —
        /// seviye 3'teki toplam artış 3+5+7 = 15 olur.
        /// </summary>
        public int GetCumulative(int level)
        {
            int sum = 0;
            for (int i = 1; i <= level; i++) sum += GetValue(i);
            return sum;
        }

        /// <summary>Verilen seviyede daha fazla yükseltme yapılabilir mi? (level = mevcut seviye)</summary>
        public bool CanUpgrade(int currentLevel)
        {
            int max = EffectiveMaxLevel;
            return max <= 0 || currentLevel < max;
        }

        public static UpgradeCurve Linear(int start, int increment, int maxLevel, int maxValue = 0) =>
            new UpgradeCurve { Steps = new[] { start }, Multiplier = 1f, Increment = increment, MaxLevel = maxLevel, MaxValue = maxValue };
    }
}
