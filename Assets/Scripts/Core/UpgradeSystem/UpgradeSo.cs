using UnityEngine;

namespace RogueBlockBlast.Content
{
    public enum UpgradeEffectType
    {
        PoolCapacity,       // Havuz kapasitesini artır
        PoolReroll,         // Pool yenileme hakkı
        CardReroll,         // Kart yenileme hakkı
        ComboGainBoost,     // Combo kazanım artışı
        DeadPoolRevive,     // Dead pool kurtarma
        ComboBarExpansion,  // Combo bar sayısı artışı
        CoinGainBoost,      // Coin kazanım artışı
        StartingCombo,      // Başlangıç combo çarpanı
    }

    /// <summary>
    /// Tek bir pasif upgrade'i tanımlar.
    /// Inspector'da tüm değerler düzenlenebilir.
    ///
    /// Lokalizasyon: NameKey ve DescriptionKey'i
    /// kendi lokalizasyon sistemine bağla.
    ///
    /// Assets/Content/Upgrades/ klasörüne oluştur.
    /// </summary>
    [CreateAssetMenu(menuName = "RogueBlockBlast/Upgrade", fileName = "Upgrade_")]
    public sealed class UpgradeSO : ScriptableObject
    {
        [Header("Identity")]
        public string Id;

        [Header("Localization Keys — kendi sistemine bağla")]
        public string NameKey;
        public string DescriptionKey;

        [Header("Visual")]
        public Sprite Icon;

        [Header("Unlock")]
        [Tooltip("Kaçıncı milestone stage'i geçilince açılır.")]
        public int UnlockStageIndex = 0;

        [Header("Level")]
        [Tooltip("Bu upgrade'in maksimum seviyesi.")]
        public int MaxLevel = 3;

        [Tooltip("Her seviyenin coin maliyeti. MaxLevel kadar eleman girmeli.")]
        public int[] CostPerLevel;

        [Header("Effect")]
        public UpgradeEffectType EffectType;

        [Tooltip("Her seviyede eklenen etki miktarı. (PoolCapacity için 3, ComboGain için 0.03 vb.)")]
        public float EffectValuePerLevel = 1f;

        // ── Runtime helpers ──────────────────────────────────────────────────

        /// <summary>Belirtilen seviyenin maliyeti. Level 1-based (1..MaxLevel).</summary>
        public int GetCostForLevel(int level)
        {
            if (CostPerLevel == null || CostPerLevel.Length == 0) return 0;
            int idx = Mathf.Clamp(level - 1, 0, CostPerLevel.Length - 1);
            return CostPerLevel[idx];
        }

        /// <summary>Toplam efekt değeri (currentLevel × EffectValuePerLevel).</summary>
        public float GetTotalEffect(int currentLevel) =>
            currentLevel * EffectValuePerLevel;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;

            // CostPerLevel dizisi MaxLevel ile uyumlu olsun
            if (CostPerLevel == null || CostPerLevel.Length != MaxLevel)
            {
                var newArr = new int[MaxLevel];
                if (CostPerLevel != null)
                    for (int i = 0; i < Mathf.Min(CostPerLevel.Length, MaxLevel); i++)
                        newArr[i] = CostPerLevel[i];
                CostPerLevel = newArr;
            }
        }
    }
}