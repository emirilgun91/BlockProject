using System;
using RogueBlockBlast.Core.Save;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Run'lar arası kalıcı coin yönetimi.
    /// SaveStore (save.json) ile saklanır.
    /// DontDestroyOnLoad — sahneler arası yaşar.
    ///
    /// Kullanım:
    ///   CoinWallet.Instance.Balance
    ///   CoinWallet.Instance.Earn(amount)
    ///   CoinWallet.Instance.Spend(amount)  → bool
    /// </summary>
    public sealed class CoinWallet : MonoBehaviour
    {
        public static CoinWallet Instance { get; private set; }

        private const string SaveKey = "CoinWallet_Balance";

        public event Action<int> OnBalanceChanged;  // UI güncellemesi için

        public int Balance { get; private set; }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Coin kazanır.</summary>
        public void Earn(int amount)
        {
            if (amount <= 0) return;
            Balance += amount;
            Save();
            OnBalanceChanged?.Invoke(Balance);
            Achievements.AchievementService.AddStat(Achievements.AchievementStats.CoinsEarned, amount);
        }

        /// <summary>
        /// Coin harcar.
        /// Yeterli coin yoksa false döner, harcama yapılmaz.
        /// </summary>
        public bool Spend(int amount)
        {
            if (amount <= 0) return true;
            if (Balance < amount) return false;

            Balance -= amount;
            Save();
            OnBalanceChanged?.Invoke(Balance);
            return true;
        }

        public bool CanAfford(int amount) => Balance >= amount;

        // ── Persistence ──────────────────────────────────────────────────────

        // Coin değişimi seyrek ve değerli (satın alma, milestone) — debounce beklemeden yaz.
        private void Save()
        {
            SaveStore.SetInt(SaveKey, Balance);
            SaveStore.Save();
        }

        private void Load() => Balance = SaveStore.GetInt(SaveKey, 0);

        /// <summary>Debug / test için sıfırla.</summary>
        public void Reset()
        {
            Balance = 0;
            Save();
            OnBalanceChanged?.Invoke(Balance);
        }
    }
}