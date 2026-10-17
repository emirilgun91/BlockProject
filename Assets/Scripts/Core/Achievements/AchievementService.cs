using System;
using RogueBlockBlast.Core.Save;
using UnityEngine;

namespace RogueBlockBlast.Core.Achievements
{
    /// <summary>
    /// Achievement'ları platforma iletecek arka uç. Steam dışında (editör, Steam
    /// açık değil, ileride başka mağaza) <see cref="NullAchievementBackend"/> kullanılır.
    /// </summary>
    public interface IAchievementBackend
    {
        bool IsReady { get; }

        /// <summary>Platformda açık mı? Bilinmiyorsa false + <paramref name="known"/> = false.</summary>
        bool IsUnlocked(string id, out bool known);

        void Unlock(string id);

        /// <summary>Sayaçlı achievement ilerlemesini overlay'de gösterir (ör. 500/1000).</summary>
        void IndicateProgress(string id, int current, int max);

        /// <summary>Bekleyen değişiklikleri sunucuya yollar (Steam: StoreStats).</summary>
        void Flush();
    }

    public sealed class NullAchievementBackend : IAchievementBackend
    {
        public bool IsReady => false;
        public bool IsUnlocked(string id, out bool known) { known = false; return false; }
        public void Unlock(string id) { }
        public void IndicateProgress(string id, int current, int max) { }
        public void Flush() { }
    }

    /// <summary>
    /// Achievement'ların tek giriş noktası. Kod yalnızca "şu açıldı" / "sayaç
    /// şu kadar arttı" der; platformun hazır olup olmadığıyla uğraşmaz.
    ///
    /// Durum önce yerelde (SaveStore / save.json) tutulur, sonra arka uca iletilir. Bu
    /// sayede Steam kapalıyken (çevrimdışı, editör) açılanlar kaybolmaz: arka uç
    /// bağlandığında <see cref="SetBackend"/> eksikleri Steam'e yollar; Steam'de
    /// açık olup yerelde olmayanları da (ilerleme sıfırlandıysa) yerele geri yazar.
    /// </summary>
    public static class AchievementService
    {
        private const string UnlockPrefix = "Ach_Unlocked_";
        private const string StatPrefix   = "Ach_Stat_";

        private static IAchievementBackend _backend = new NullAchievementBackend();

        /// <summary>Yeni açılan achievement. Steam zaten kendi bildirimini gösteriyor; bu oyun içi tepki / debug için.</summary>
        public static event Action<AchievementDef> OnUnlocked;

        public static IAchievementBackend Backend => _backend;

        // ── Arka uç ──────────────────────────────────────────────────────────

        public static void SetBackend(IAchievementBackend backend)
        {
            _backend = backend ?? new NullAchievementBackend();
            Sync();
        }

        /// <summary>Yerel ve platform durumunu iki yönlü eşitler.</summary>
        public static void Sync()
        {
            if (!_backend.IsReady) return;

            bool pushed = false;
            foreach (var def in AchievementCatalog.All)
            {
                bool local  = IsUnlocked(def.Id);
                bool remote = _backend.IsUnlocked(def.Id, out bool known);
                if (!known) continue;

                if (local && !remote)      { _backend.Unlock(def.Id); pushed = true; }
                else if (remote && !local) SaveStore.SetInt(UnlockPrefix + def.Id, 1);
            }

            if (pushed) _backend.Flush();
        }

        // ── Achievement ──────────────────────────────────────────────────────

        public static bool IsUnlocked(string id) =>
            SaveStore.GetInt(UnlockPrefix + id, 0) == 1;

        public static void Unlock(string id)
        {
            if (string.IsNullOrEmpty(id) || IsUnlocked(id)) return;

            var def = AchievementCatalog.Get(id);
            if (def == null)
            {
                Debug.LogWarning($"[Achievements] Katalogda olmayan id: {id}");
                return;
            }

            SaveStore.SetInt(UnlockPrefix + id, 1);
            SaveStore.Save();

            _backend.Unlock(id);
            _backend.Flush();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Achievements] AÇILDI: {def.DevName} ({id})");
#endif
            OnUnlocked?.Invoke(def);
        }

        // ── Sayaçlar ─────────────────────────────────────────────────────────

        public static int GetStat(string stat) => SaveStore.GetInt(StatPrefix + stat, 0);

        /// <summary>
        /// Ömür boyu sayacı artırır, eşiğe ulaşan achievement'ları açar.
        /// Her yerleştirmede çağrılabilir: SaveStore.SetInt bellekte kalır, diske
        /// en geç <see cref="SaveStore.DebounceSeconds"/> sonra ya da bir sonraki <see cref="Unlock"/>'ta yazılır.
        /// </summary>
        public static void AddStat(string stat, int delta)
        {
            if (delta <= 0) return;

            int before = GetStat(stat);
            int after  = before + delta;
            SaveStore.SetInt(StatPrefix + stat, after);

            foreach (var def in AchievementCatalog.All)
            {
                if (def.Stat != stat || IsUnlocked(def.Id)) continue;

                if (after >= def.Target)
                {
                    Unlock(def.Id);
                    continue;
                }

                // Overlay ilerlemesi yalnızca %25'lik eşikleri geçerken — her
                // yerleştirmede bildirim çıkarsa oyuncu boğulur.
                int stepBefore = before * 4 / def.Target;
                int stepAfter  = after  * 4 / def.Target;
                if (stepAfter > stepBefore)
                    _backend.IndicateProgress(def.Id, after, def.Target);
            }
        }

        /// <summary>
        /// Yerel achievement + sayaç kayıtlarını siler (debug). Steam'deki durumu
        /// silmez — bir sonraki <see cref="Sync"/> açık olanları geri getirir.
        /// </summary>
        public static void ResetLocal()
        {
            foreach (var def in AchievementCatalog.All)
                SaveStore.DeleteKey(UnlockPrefix + def.Id);

            SaveStore.DeleteKey(StatPrefix + AchievementStats.LinesCleared);
            SaveStore.DeleteKey(StatPrefix + AchievementStats.PiecesPlaced);
            SaveStore.DeleteKey(StatPrefix + AchievementStats.CoinsEarned);
            SaveStore.DeleteKey(StatPrefix + AchievementStats.RunsPlayed);
            SaveStore.Save();
        }
    }
}
