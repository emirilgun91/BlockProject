using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RogueBlockBlast.Core.Settings;
using SL = Assets.SimpleLocalization.Scripts.LocalizationManager;

namespace RogueBlockBlast.Core.Localization
{
    /// <summary>
    /// Oyunun lokalizasyon cephesi. Altta SimpleLocalization çalışır
    /// (Assets/SimpleLocalization — Google Sheets senkronu ve editör penceresi onun).
    ///
    /// Bu katmanın eklediği şeyler:
    ///   • Seçilen dilin kalıcı saklanması (GameSettings.Language)
    ///   • Sistem dilinin otomatik algılanması
    ///   • Dil listesi + ileri/geri gezinme (Ayarlar paneli için)
    ///   • Anahtar yoksa patlamayan güvenli okuma (Get / GetOr)
    ///
    /// Çeviri eklemek: Assets/SimpleLocalization/Resources/Localization/*.csv
    /// (ya da ◆ Simple Localization penceresinden Google Sheets'ten çek).
    ///
    /// Kullanım:
    ///   Loc.Get("MainMenu.Play")
    ///   Loc.Get("Upgrades.Level", 2, 5)
    ///   Loc.OnChanged += Refresh;
    /// </summary>
    public static class Loc
    {
        private const string DefaultLanguage = "English";

        /// <summary>Dil değişti — görünür metinler yenilenmeli.</summary>
        public static event Action OnChanged;

        public static bool IsInitialized { get; private set; }

        /// <summary>Aktif dil, SimpleLocalization'daki adıyla ("English", "Turkish").</summary>
        public static string CurrentLanguage => SL.Language;

        /// <summary>CSV başlığındaki tüm diller.</summary>
        public static IReadOnlyList<string> AvailableLanguages => _languages;

        private static readonly List<string> _languages = new();
        private static readonly HashSet<string> _warned = new(StringComparer.Ordinal);

        // ── Init ─────────────────────────────────────────────────────────────
        public static void Init()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            SL.Read();

            _languages.Clear();
            _languages.AddRange(SL.Dictionary.Keys);

            if (_languages.Count == 0)
            {
                Debug.LogError("[Loc] Hiç dil yüklenemedi — LocalizationSettings.Sheets boş olabilir.");
                return;
            }

            string saved = GameSettings.Language;
            string pick =
                !string.IsNullOrEmpty(saved) && _languages.Contains(saved) ? saved :
                DetectSystemLanguage();

            SL.Language = pick;
            GameSettings.Language = pick;

            SL.OnLocalizationChanged += () => OnChanged?.Invoke();
        }

        private static string DetectSystemLanguage()
        {
            // Application.systemLanguage zaten SimpleLocalization'ın sütun adlarıyla
            // aynı İngilizce isimleri kullanıyor ("Turkish", "German", ...).
            string native = Application.systemLanguage.ToString();
            if (_languages.Contains(native)) return native;

            // Çince gibi ayrık varyantlar için elle eşleme
            string mapped = Application.systemLanguage switch
            {
                SystemLanguage.ChineseSimplified  => "Chinese (Simplified)",
                SystemLanguage.ChineseTraditional => "Chinese (Traditional)",
                SystemLanguage.Chinese            => "Chinese (Simplified)",
                SystemLanguage.Portuguese         => "Portuguese",
                SystemLanguage.Spanish            => "Castilian Spanish",
                _                                 => null,
            };
            if (mapped != null && _languages.Contains(mapped)) return mapped;

            return _languages.Contains(DefaultLanguage) ? DefaultLanguage : _languages[0];
        }

        // ── Language switching ───────────────────────────────────────────────
        public static void SetLanguage(string language)
        {
            Init();
            if (string.IsNullOrEmpty(language) || !_languages.Contains(language)) return;
            if (language == SL.Language) return;

            SL.Language = language;          // OnLocalizationChanged → OnChanged
            GameSettings.Language = language;
        }

        public static void NextLanguage()     => Cycle(+1);
        public static void PreviousLanguage() => Cycle(-1);

        private static void Cycle(int step)
        {
            Init();
            if (_languages.Count <= 1) return;
            int i = _languages.IndexOf(SL.Language);
            if (i < 0) i = 0;
            SetLanguage(_languages[(i + step + _languages.Count) % _languages.Count]);
        }

        /// <summary>
        /// Dilin kendi dilindeki adı ("Turkish" → "Türkçe").
        /// CSV'de Language.&lt;ad&gt; anahtarı varsa onu kullanır.
        /// </summary>
        public static string GetLanguageDisplayName(string language)
        {
            if (string.IsNullOrEmpty(language)) return string.Empty;
            if (SL.HasKey("Language." + language)) return SL.Localize("Language." + language);
            return language;
        }

        public static string CurrentLanguageDisplayName => GetLanguageDisplayName(CurrentLanguage);

        // ── Lookup ───────────────────────────────────────────────────────────
        public static bool HasKey(string key)
        {
            if (!IsInitialized) Init();
            return !string.IsNullOrEmpty(key) && SL.HasKey(key);
        }

        /// <summary>
        /// Çeviriyi döner. Anahtar yoksa anahtarın kendisini döner ve bir kez uyarır —
        /// SimpleLocalization.Localize'ın aksine exception atmaz, oyun akışını kesmez.
        /// </summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (!IsInitialized) Init();

            if (!SL.HasKey(key))
            {
                if (_warned.Add(key))
                    Debug.LogWarning($"[Loc] Eksik anahtar: \"{key}\" ({CurrentLanguage})");
                return key;
            }
            return SL.Localize(key);
        }

        public static string Get(string key, params object[] args)
        {
            string raw = Get(key);
            if (args == null || args.Length == 0) return raw;
            try { return string.Format(raw, args); }
            catch (FormatException)
            {
                Debug.LogWarning($"[Loc] \"{key}\" format hatası ({CurrentLanguage}): \"{raw}\"");
                return raw;
            }
        }

        /// <summary>Anahtar yoksa verilen metni döner — içerik (kart/upgrade) çevirisi için.</summary>
        public static string GetOr(string key, string fallback)
            => HasKey(key) ? SL.Localize(key) : fallback;


        /// <summary>
        /// Aktif dilin kültürüne göre büyük harfe çevirir.
        /// Türkçe'de "i" → "İ" olmalı; invariant ToUpper bunu "I" yapıyor.
        /// </summary>
        public static string ToUpper(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            try
            {
                string code = CurrentLanguage switch
                {
                    "Turkish" => "tr-TR",
                    "German"  => "de-DE",
                    "French"  => "fr-FR",
                    "Russian" => "ru-RU",
                    _          => "en-US",
                };
                return s.ToUpper(System.Globalization.CultureInfo.GetCultureInfo(code));
            }
            catch { return s.ToUpperInvariant(); }
        }
        /// <summary>CSV'leri diskten yeniden okur. Editörde çeviri düzenlerken kullanışlı.</summary>
        public static void Reload()
        {
            string current = SL.Language;
            SL.Dictionary.Clear();
            IsInitialized = false;
            _warned.Clear();
            Init();
            if (_languages.Contains(current)) SetLanguage(current);
            OnChanged?.Invoke();
        }
    }
}
