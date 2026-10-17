using RogueBlockBlast.Core.Achievements;
using UnityEngine;

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace RogueBlockBlast.Game.Achievements
{
    /// <summary>
    /// Steam'i başlatır, her kare callback'leri çalıştırır ve achievement arka
    /// ucunu <see cref="AchievementService"/>'e bağlar. Sahne kurulumu gerekmez:
    /// ilk sahneden önce kendini yaratır.
    ///
    /// Steamworks.NET paketi yokken (ya da <c>DISABLESTEAMWORKS</c> tanımlıyken)
    /// bu sınıf hiçbir şey yapmaz; oyun Steam'siz derlenir ve çalışır, achievement'lar
    /// yalnızca yerelde tutulur. Paket kurulunca <c>STEAMWORKS_NET</c> define'ı
    /// paketin kendi editör kodu tarafından eklenir.
    ///
    /// Editörde Steam'in açılması için proje kökünde <c>steam_appid.txt</c>
    /// (içinde yalnızca App ID) bulunmalı ve Steam istemcisi açık olmalı.
    /// </summary>
    public sealed class SteamPlatform : MonoBehaviour
    {
        /// <summary>Ana oyunun (Blockflow) Steam App ID'si.</summary>
        public const uint FullGameAppId = 4559890;

        /// <summary>Demonun (Blockflow Demo) Steam App ID'si — ayrı bir Steam uygulaması.</summary>
        public const uint DemoAppId = 5192270;

        /// <summary>
        /// Bu build demo mu? Tam sürüm build'inden önce false yapılmalı
        /// (ve proje kökündeki <c>steam_appid.txt</c> ana oyunun ID'sine çevrilmeli).
        /// RunController'daki <c>_demoBuild</c> demo sınırını açar; bu bayrak yalnızca
        /// Steam tarafını seçer — sahne yüklenmeden çalıştığı için ondan okuyamıyor.
        /// </summary>
        public const bool IsDemo = true;

        /// <summary>
        /// Kullanılan Steamworks App ID. Oyun Steam dışından açılırsa
        /// RestartAppIfNecessary onu bu ID ile Steam üzerinden yeniden başlatır —
        /// yanlış ID, demo yerine ana oyunu açtırıyordu. Editörde aynı ID proje
        /// kökündeki <c>steam_appid.txt</c>'den okunur.
        /// </summary>
        public const uint AppId = IsDemo ? DemoAppId : FullGameAppId;

        public static SteamPlatform Instance { get; private set; }
        public static bool Initialized { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[SteamPlatform]");
            DontDestroyOnLoad(go);
            go.AddComponent<SteamPlatform>();
        }

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
        private SteamAchievementBackend _backend;
        private float _nextSyncTry;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            if (!Packsize.Test() || !DllCheck.Test())
            {
                Debug.LogError("[Steam] Steamworks.NET platform/DLL uyumsuz — Steam devre dışı.");
                return;
            }

#if !UNITY_EDITOR
            if (AppId != 0 && SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
            {
                Application.Quit();
                return;
            }
#endif
            string initError = null;
            try
            {
                Initialized = SteamAPI.InitEx(out initError) == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
            }
            catch (System.DllNotFoundException e)
            {
                initError   = "steam_api DLL bulunamadı: " + e.Message;
                Initialized = false;
            }

            if (!Initialized)
            {
                Debug.LogWarning($"[Steam] Başlatılamadı: {initError}. (Steam açık mı, bu hesapta oyunun " +
                                 "lisansı var mı, editörde steam_appid.txt var mı?) Achievement'lar yerelde tutulacak.");
                return;
            }

            Debug.Log($"[Steam] Bağlandı: {SteamFriends.GetPersonaName()} (App {SteamUtils.GetAppID()})");

            // Eski SDK'larda istatistikler istenmeden gelmez; 1.61+ bu metodu
            // kaldırdı. Reflection ile: hangi Steamworks.NET sürümü kurulursa kurulsun derlenir.
            typeof(SteamUserStats).GetMethod("RequestCurrentStats",
                System.Type.EmptyTypes)?.Invoke(null, null);

            _backend = new SteamAchievementBackend();
        }

        private void Update()
        {
            if (!Initialized) return;
            SteamAPI.RunCallbacks();

            // İstatistikler sunucudan gelene kadar GetAchievement başarısız döner.
            // Gelince bir kez bağlan ve eşitle.
            if (_backend != null && !_backend.IsReady && Time.unscaledTime >= _nextSyncTry)
            {
                _nextSyncTry = Time.unscaledTime + 1f;
                if (_backend.Probe())
                    AchievementService.SetBackend(_backend);
            }
        }

        /// <summary>
        /// Bu Steam hesabındaki tüm achievement ve stat'ları siler, yerel kaydı da
        /// temizler. Yalnızca debug paneli için: yayından önce geliştirici
        /// hesabındaki test açılışlarını sıfırlamaya yarar.
        /// </summary>
        public static void DevResetAllAchievements()
        {
            if (!Initialized) return;
            SteamUserStats.ResetAllStats(true);
            SteamUserStats.StoreStats();
            AchievementService.ResetLocal();
            Debug.Log("[Steam] Bu hesabın tüm achievement'ları sıfırlandı.");
        }

        private void OnApplicationQuit()
        {
            if (!Initialized) return;
            _backend?.Flush();
            SteamAPI.Shutdown();
            Initialized = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private sealed class SteamAchievementBackend : IAchievementBackend
        {
            public bool IsReady { get; private set; }

            /// <summary>İstatistikler yüklendi mi? İlk katalog öğesiyle dener.</summary>
            public bool Probe()
            {
                if (AchievementCatalog.All.Count == 0) return false;
                IsReady = SteamUserStats.GetAchievement(AchievementCatalog.All[0].Id, out _);
                return IsReady;
            }

            public bool IsUnlocked(string id, out bool known)
            {
                known = SteamUserStats.GetAchievement(id, out bool achieved);
                if (!known)
                    Debug.LogWarning($"[Steam] '{id}' Steamworks'te tanımlı değil (ya da yayınlanmadı).");
                return achieved;
            }

            public void Unlock(string id)
            {
                if (IsReady) SteamUserStats.SetAchievement(id);
            }

            public void IndicateProgress(string id, int current, int max)
            {
                if (IsReady) SteamUserStats.IndicateAchievementProgress(id, (uint)current, (uint)max);
            }

            public void Flush()
            {
                if (IsReady) SteamUserStats.StoreStats();
            }
        }
#else
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        public static void DevResetAllAchievements() { }
#endif
    }
}
