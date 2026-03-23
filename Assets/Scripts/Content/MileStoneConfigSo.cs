using System.Collections.Generic;
using UnityEngine;

namespace RogueBlockBlast.Content
{
    /// <summary>
    /// Milestone ve pool limit ayarlarının tek kaynağı.
    /// Assets/Content/MilestoneConfig.asset olarak oluştur.
    /// </summary>
    [CreateAssetMenu(menuName = "RogueBlockBlast/MilestoneConfig", fileName = "MilestoneConfig")]
    public sealed class MilestoneConfigSO : ScriptableObject
    {
        [Header("Pool Limit")]
        [Tooltip("Her milestone arasında yerleştirilebilecek maksimum piece sayısı.")]
        public int PoolLimit = 30;

        [Header("Milestones")]
        [Tooltip("Sıralı milestone listesi. Score eşiğine ulaşınca tetiklenir.")]
        public List<MilestoneData> Milestones = new List<MilestoneData>
        {
            new MilestoneData { ScoreThreshold = 1000,  CoinReward = 5,  Label = "I"   },
            new MilestoneData { ScoreThreshold = 2500,  CoinReward = 8,  Label = "II"  },
            new MilestoneData { ScoreThreshold = 5000,  CoinReward = 12, Label = "III" },
            new MilestoneData { ScoreThreshold = 10000, CoinReward = 20, Label = "IV"  },
            new MilestoneData { ScoreThreshold = 20000, CoinReward = 35, Label = "V"   },
        };

        /// <summary>
        /// Sonraki milestone'u döndürür. index = kaçıncı milestone.
        /// Tümü geçildiyse null.
        /// </summary>
        public MilestoneData? GetMilestone(int index)
        {
            if (index < 0 || index >= Milestones.Count) return null;
            return Milestones[index];
        }

        public int TotalMilestones => Milestones.Count;
    }

    [System.Serializable]
    public struct MilestoneData
    {
        [Tooltip("Bu milestone için gereken skor.")]
        public int ScoreThreshold;

        [Tooltip("Milestone'a ulaşınca kazanılan coin.")]
        public int CoinReward;

        [Tooltip("UI'da gösterilecek kısa isim (opsiyonel).")]
        public string Label;
    }
}