using System.Collections.Generic;
using RogueBlockBlast.Content;

//using UnityEditor.Overlays;

using UnityEngine;

namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Upgrade seviyelerini PlayerPrefs ile kalıcı saklar.
    /// DontDestroyOnLoad — sahneler arası yaşar.
    ///
    /// Kullanım:
    ///   UpgradeRegistry.Instance.GetLevel("upgrade_pool_capacity")
    ///   UpgradeRegistry.Instance.Upgrade(upgradeSO)
    ///   UpgradeRegistry.Instance.Sell(upgradeSO)
    ///   UpgradeRegistry.Instance.IsUnlocked(upgradeSO)
    /// </summary>
    public sealed class UpgradeRegistry : MonoBehaviour
    {
        public static UpgradeRegistry Instance { get; private set; }

        private const string KeyPrefix = "Upgrade_Level_";

        // Runtime cache
        private readonly Dictionary<string, int> _levels = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Init ─────────────────────────────────────────────────────────────

        /// <summary>Tüm upgrade'leri kütüphaneden yükle.</summary>
        public void Init(UpgradeLibrarySO library)
        {
            if (library == null) return;
            _levels.Clear();

            foreach (var u in library.Upgrades)
            {
                if (u == null) continue;
                int saved = PlayerPrefs.GetInt(KeyPrefix + u.Id, 0);
                Debug.Log($"[UpgradeRegistry] Load → Key:{KeyPrefix + u.Id} | Saved:{saved}");
                if (saved > 0) _levels[u.Id] = saved;
            }
        }

        /// <summary>
        /// Runtime cache'i boşaltır — PlayerPrefs dışarıdan silindiğinde
        /// (ör. ilerleme sıfırlama) çağrılır. Diskten okumaz; sonrasında
        /// <see cref="Init"/> ile yeniden yüklenmeli.
        /// </summary>
        public void ClearRuntimeCache() => _levels.Clear();

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Mevcut seviyeyi döndürür (0 = sahip değil).</summary>
        public int GetLevel(string id) =>
            _levels.TryGetValue(id, out int l) ? l : 0;

        /// <summary>
        /// Bu upgrade unlock edilmiş mi?
        /// Milestone kontrolü PlayerPrefs'ten yapılır.
        /// </summary>
        public bool IsUnlocked(UpgradeSO upgrade)
        {
            if (upgrade == null) return false;
            if (upgrade.UnlockStageIndex <= 0) return true;
            int maxReached = PlayerPrefs.GetInt("MaxMilestoneReached", 0);
            return maxReached >= upgrade.UnlockStageIndex;
        }

        /// <summary>Bir sonraki seviyeye yükseltebilir mi?</summary>
        public bool CanUpgrade(UpgradeSO upgrade)
        {
            if (!CanUpgradeIgnoringCoins(upgrade)) return false;
            return CoinWallet.Instance?.CanAfford(GetNextCost(upgrade)) ?? false;
        }

        /// <summary>
        /// Coin durumuna BAKMADAN yükseltilebilir mi (kilit açık + maks seviyede değil).
        ///
        /// UI butonları bunu kullanmalı: buton açık kalır, tıklayınca "yetersiz coin"
        /// uyarısı gösterilir. Butonu tamamen kapatınca oyuncu neden tıklayamadığını
        /// anlamıyordu.
        /// </summary>
        public bool CanUpgradeIgnoringCoins(UpgradeSO upgrade)
        {
            if (upgrade == null) return false;
            if (!IsUnlocked(upgrade)) return false;
            return GetLevel(upgrade.Id) < upgrade.MaxLevel;
        }

        /// <summary>Bir sonraki seviyenin maliyeti.</summary>
        public int GetNextCost(UpgradeSO upgrade)
        {
            if (upgrade == null) return 0;
            return upgrade.GetCostForLevel(GetLevel(upgrade.Id) + 1);
        }

        /// <summary>Oyuncunun bir sonraki seviyeye parası yetiyor mu?</summary>
        public bool CanAffordUpgrade(UpgradeSO upgrade)
            => CoinWallet.Instance?.CanAfford(GetNextCost(upgrade)) ?? false;

        /// <summary>Son seviyeyi satabilir mi?</summary>
        public bool CanSell(UpgradeSO upgrade)
        {
            if (upgrade == null) return false;
            return GetLevel(upgrade.Id) > 0;
        }

        /// <summary>
        /// Upgrade yapar — coin harcayarak seviye artırır.
        /// Başarılıysa true döner.
        /// </summary>
        public bool Upgrade(UpgradeSO upgrade)
        {
            if (!CanUpgrade(upgrade)) return false;

            int current = GetLevel(upgrade.Id);
            int cost    = upgrade.GetCostForLevel(current + 1);

            if (!CoinWallet.Instance.Spend(cost)) return false;

            int newLevel = current + 1;
            _levels[upgrade.Id] = newLevel;
            PlayerPrefs.SetInt(KeyPrefix + upgrade.Id, newLevel);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// Son seviyeyi iade eder — coin geri verir.
        /// Sell fiyatı: o seviyenin maliyetinin yarısı.
        /// </summary>
        public bool Sell(UpgradeSO upgrade)
        {
            if (!CanSell(upgrade)) return false;

            int current   = GetLevel(upgrade.Id);
            int sellPrice = upgrade.GetCostForLevel(current) / 2;

            CoinWallet.Instance?.Earn(sellPrice);

            int newLevel = current - 1;
            if (newLevel <= 0)
                _levels.Remove(upgrade.Id);
            else
                _levels[upgrade.Id] = newLevel;

            PlayerPrefs.SetInt(KeyPrefix + upgrade.Id, newLevel);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Efekt değerini döndürür (GetTotalEffect shortcut).</summary>
        public float GetEffect(UpgradeSO upgrade) =>
            upgrade?.GetTotalEffect(GetLevel(upgrade.Id)) ?? 0f;

        /// <summary>Debug/test — tüm verileri siler.</summary>
        public void ResetAll(UpgradeLibrarySO library)
        {
            if (library == null) return;
            foreach (var u in library.Upgrades)
            {
                if (u == null) continue;
                PlayerPrefs.DeleteKey(KeyPrefix + u.Id);
            }
            _levels.Clear();
        }
    }
}