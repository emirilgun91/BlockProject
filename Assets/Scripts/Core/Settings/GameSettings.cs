using System;
using UnityEngine;

namespace RogueBlockBlast.Core.Settings
{
    public enum ColorblindMode { None = 0, Protanopia = 1, Deuteranopia = 2, Tritanopia = 3, HighContrast = 4 }

    /// <summary>
    /// Tüm oyun ayarlarının tek kaynağı. PlayerPrefs ile kalıcı.
    ///
    /// Kullanım:
    ///   GameSettings.MusicVolume = 0.5f;          // yazar + uygular + event fırlatır
    ///   if (GameSettings.ReduceMotion) { ... }    // okur (cache'ten, PlayerPrefs'e gitmez)
    ///   GameSettings.OnChanged += key => ...;     // herhangi bir ayar değişti
    ///
    /// Yeni ayar eklerken: alan + property + Load() içine bir satır, hepsi bu.
    /// </summary>
    public static class GameSettings
    {
        // ── Keys ─────────────────────────────────────────────────────────────
        public const string K_Master        = "MasterVolume";   // AudioManager ile ortak
        public const string K_Music         = "MusicVolume";    // AudioManager ile ortak
        public const string K_Sfx           = "SFXVolume";      // AudioManager ile ortak
        public const string K_Muted         = "Set_Muted";
        public const string K_MuteUnfocused = "Set_MuteUnfocused";

        public const string K_Fullscreen    = "Fullscreen";     // eski SettingsController ile ortak
        public const string K_ResolutionIdx = "Set_ResolutionIdx";
        public const string K_VSync         = "Set_VSync";
        public const string K_TargetFps     = "Set_TargetFps";

        public const string K_ScreenShake   = "Set_ScreenShake";
        public const string K_VfxIntensity  = "Set_VfxIntensity";
        public const string K_ReduceMotion  = "Set_ReduceMotion";
        public const string K_ReduceFlashing= "Set_ReduceFlashing";

        public const string K_GhostPreview  = "Set_GhostPreview";
        public const string K_HighlightLines= "Set_HighlightLines";
        public const string K_ConfirmQuit   = "Set_ConfirmQuit";
        public const string K_ShowFps       = "Set_ShowFps";
        public const string K_GridLines     = "Set_GridLines";

        public const string K_Haptics       = "Set_Haptics";
        public const string K_DragOffsetY   = "Set_DragOffsetY";

        public const string K_Colorblind    = "Set_Colorblind";
        public const string K_UiScale       = "Set_UiScale";
        public const string K_LargeText     = "Set_LargeText";

        public const string K_Language      = "Set_Language";

        // ── Events ───────────────────────────────────────────────────────────
        /// <summary>Herhangi bir ayar değiştiğinde, değişen key ile tetiklenir.</summary>
        public static event Action<string> OnChanged;
        /// <summary>Ses ayarlarından biri değişti (master/music/sfx/mute).</summary>
        public static event Action OnAudioChanged;
        /// <summary>Ekran ayarlarından biri değişti (fullscreen/res/vsync/fps).</summary>
        public static event Action OnDisplayChanged;
        /// <summary>Erişilebilirlik ayarlarından biri değişti.</summary>
        public static event Action OnAccessibilityChanged;

        // ── Backing fields ───────────────────────────────────────────────────
        private static bool _loaded;

        private static float _master, _music, _sfx;
        private static bool  _muted, _muteUnfocused;

        private static bool  _fullscreen;
        private static int   _resolutionIdx, _vsync, _targetFps;

        private static float _screenShake, _vfxIntensity;
        private static bool  _reduceMotion, _reduceFlashing;

        private static bool  _ghostPreview, _highlightLines, _confirmQuit, _showFps, _gridLines;

        private static bool  _haptics;
        private static float _dragOffsetY;

        private static ColorblindMode _colorblind;
        private static float _uiScale;
        private static bool  _largeText;

        private static string _language;

        // ── Audio ────────────────────────────────────────────────────────────
        public static float MasterVolume
        {
            get { EnsureLoaded(); return _master; }
            set { EnsureLoaded(); if (Set(ref _master, Mathf.Clamp01(value), K_Master)) { ApplyAudio(); OnAudioChanged?.Invoke(); } }
        }

        public static float MusicVolume
        {
            get { EnsureLoaded(); return _music; }
            set { EnsureLoaded(); if (Set(ref _music, Mathf.Clamp01(value), K_Music)) { ApplyAudio(); OnAudioChanged?.Invoke(); } }
        }

        public static float SfxVolume
        {
            get { EnsureLoaded(); return _sfx; }
            set { EnsureLoaded(); if (Set(ref _sfx, Mathf.Clamp01(value), K_Sfx)) { ApplyAudio(); OnAudioChanged?.Invoke(); } }
        }

        /// <summary>Tüm sesi kapatır. Slider değerlerini bozmaz — geri açınca eski seviyeler döner.</summary>
        public static bool Muted
        {
            get { EnsureLoaded(); return _muted; }
            set { EnsureLoaded(); if (Set(ref _muted, value, K_Muted)) { ApplyAudio(); OnAudioChanged?.Invoke(); } }
        }

        /// <summary>Oyun arka plana düştüğünde sesi kıs.</summary>
        public static bool MuteWhenUnfocused
        {
            get { EnsureLoaded(); return _muteUnfocused; }
            set { EnsureLoaded(); if (Set(ref _muteUnfocused, value, K_MuteUnfocused)) OnAudioChanged?.Invoke(); }
        }

        /// <summary>Efektif master seviyesi — mute durumu dahil. AudioManager bunu kullanmalı.</summary>
        public static float EffectiveMaster => Muted ? 0f : MasterVolume;

        // ── Display ──────────────────────────────────────────────────────────
        public static bool Fullscreen
        {
            get { EnsureLoaded(); return _fullscreen; }
            set { EnsureLoaded(); if (Set(ref _fullscreen, value, K_Fullscreen)) { ApplyDisplay(); OnDisplayChanged?.Invoke(); } }
        }

        /// <summary>Screen.resolutions içindeki index. -1 = dokunma (native).</summary>
        public static int ResolutionIndex
        {
            get { EnsureLoaded(); return _resolutionIdx; }
            set { EnsureLoaded(); if (Set(ref _resolutionIdx, value, K_ResolutionIdx)) { ApplyDisplay(); OnDisplayChanged?.Invoke(); } }
        }

        /// <summary>0 = kapalı, 1 = her frame, 2 = iki frame'de bir.</summary>
        public static int VSync
        {
            get { EnsureLoaded(); return _vsync; }
            set { EnsureLoaded(); if (Set(ref _vsync, Mathf.Clamp(value, 0, 2), K_VSync)) { ApplyDisplay(); OnDisplayChanged?.Invoke(); } }
        }

        /// <summary>Hedef FPS. -1 = sınırsız (platform varsayılanı).</summary>
        public static int TargetFrameRate
        {
            get { EnsureLoaded(); return _targetFps; }
            set { EnsureLoaded(); if (Set(ref _targetFps, value, K_TargetFps)) { ApplyDisplay(); OnDisplayChanged?.Invoke(); } }
        }

        // ── Visual / Motion ──────────────────────────────────────────────────
        /// <summary>0..1 — ekran sarsıntısı çarpanı. ReduceMotion açıkken 0 döner.</summary>
        public static float ScreenShake
        {
            get { EnsureLoaded(); return _reduceMotion ? 0f : _screenShake; }
            set { EnsureLoaded(); if (Set(ref _screenShake, Mathf.Clamp01(value), K_ScreenShake)) OnAccessibilityChanged?.Invoke(); }
        }

        /// <summary>Ham sarsıntı değeri — ayar UI'ı slider'ı bununla doldurmalı.</summary>
        public static float ScreenShakeRaw { get { EnsureLoaded(); return _screenShake; } }

        /// <summary>0..1 — partikül / line clear VFX yoğunluğu.</summary>
        public static float VfxIntensity
        {
            get { EnsureLoaded(); return _reduceMotion ? Mathf.Min(_vfxIntensity, 0.35f) : _vfxIntensity; }
            set { EnsureLoaded(); if (Set(ref _vfxIntensity, Mathf.Clamp01(value), K_VfxIntensity)) OnAccessibilityChanged?.Invoke(); }
        }

        public static float VfxIntensityRaw { get { EnsureLoaded(); return _vfxIntensity; } }

        /// <summary>Açıkken sarsıntı kapanır, VFX kısılır, arka plan animasyonları yavaşlar.</summary>
        public static bool ReduceMotion
        {
            get { EnsureLoaded(); return _reduceMotion; }
            set { EnsureLoaded(); if (Set(ref _reduceMotion, value, K_ReduceMotion)) OnAccessibilityChanged?.Invoke(); }
        }

        /// <summary>Hızlı yanıp sönen efektleri kapatır (fotosensitivite).</summary>
        public static bool ReduceFlashing
        {
            get { EnsureLoaded(); return _reduceFlashing; }
            set { EnsureLoaded(); if (Set(ref _reduceFlashing, value, K_ReduceFlashing)) OnAccessibilityChanged?.Invoke(); }
        }

        // ── Gameplay ─────────────────────────────────────────────────────────
        /// <summary>Parça sürüklerken hayalet önizleme göster.</summary>
        public static bool GhostPreview
        {
            get { EnsureLoaded(); return _ghostPreview; }
            set { EnsureLoaded(); Set(ref _ghostPreview, value, K_GhostPreview); }
        }

        /// <summary>Bu yerleştirmeyle temizlenecek satır/sütunları vurgula.</summary>
        public static bool HighlightClearingLines
        {
            get { EnsureLoaded(); return _highlightLines; }
            set { EnsureLoaded(); Set(ref _highlightLines, value, K_HighlightLines); }
        }

        /// <summary>Run sırasında çıkışta onay iste.</summary>
        public static bool ConfirmQuit
        {
            get { EnsureLoaded(); return _confirmQuit; }
            set { EnsureLoaded(); Set(ref _confirmQuit, value, K_ConfirmQuit); }
        }

        public static bool ShowFps
        {
            get { EnsureLoaded(); return _showFps; }
            set { EnsureLoaded(); Set(ref _showFps, value, K_ShowFps); }
        }

        /// <summary>Tahta ızgara çizgilerini göster.</summary>
        public static bool GridLines
        {
            get { EnsureLoaded(); return _gridLines; }
            set { EnsureLoaded(); Set(ref _gridLines, value, K_GridLines); }
        }

        // ── Touch / Haptics ──────────────────────────────────────────────────
        public static bool Haptics
        {
            get { EnsureLoaded(); return _haptics; }
            set { EnsureLoaded(); Set(ref _haptics, value, K_Haptics); }
        }

        /// <summary>Dokunmatikte parçayı parmağın kaç hücre üstünde tutacağı (0..2).</summary>
        public static float DragOffsetY
        {
            get { EnsureLoaded(); return _dragOffsetY; }
            set { EnsureLoaded(); Set(ref _dragOffsetY, Mathf.Clamp(value, 0f, 2f), K_DragOffsetY); }
        }

        // ── Accessibility ────────────────────────────────────────────────────
        public static ColorblindMode Colorblind
        {
            get { EnsureLoaded(); return _colorblind; }
            set
            {
                EnsureLoaded();
                int v = (int)value;
                int cur = (int)_colorblind;
                if (cur == v) return;
                _colorblind = value;
                PlayerPrefs.SetInt(K_Colorblind, v);
                Save();
                OnChanged?.Invoke(K_Colorblind);
                OnAccessibilityChanged?.Invoke();
            }
        }

        /// <summary>0.8 .. 1.4 — CanvasScaler scaleFactor çarpanı.</summary>
        public static float UiScale
        {
            get { EnsureLoaded(); return _uiScale; }
            set { EnsureLoaded(); if (Set(ref _uiScale, Mathf.Clamp(value, 0.8f, 1.4f), K_UiScale)) OnAccessibilityChanged?.Invoke(); }
        }

        public static bool LargeText
        {
            get { EnsureLoaded(); return _largeText; }
            set { EnsureLoaded(); if (Set(ref _largeText, value, K_LargeText)) OnAccessibilityChanged?.Invoke(); }
        }

        // ── Language ─────────────────────────────────────────────────────────
        /// <summary>ISO kodu ("en", "tr"). LocalizationManager bunu okur/yazar.</summary>
        public static string Language
        {
            get { EnsureLoaded(); return _language; }
            set
            {
                EnsureLoaded();
                if (string.IsNullOrEmpty(value) || _language == value) return;
                _language = value;
                PlayerPrefs.SetString(K_Language, value);
                Save();
                OnChanged?.Invoke(K_Language);
            }
        }

        // ── Load / Apply / Reset ─────────────────────────────────────────────
        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;   // reentrancy guard — property getter'ları Load içinde çağrılmasın
            Load();
        }

        private static void Load()
        {
            _master        = PlayerPrefs.GetFloat(K_Master, 0.75f);
            _music         = PlayerPrefs.GetFloat(K_Music,  0.75f);
            _sfx           = PlayerPrefs.GetFloat(K_Sfx,    0.75f);
            _muted         = PlayerPrefs.GetInt(K_Muted, 0) == 1;
            _muteUnfocused = PlayerPrefs.GetInt(K_MuteUnfocused, 1) == 1;

            _fullscreen    = PlayerPrefs.GetInt(K_Fullscreen, Screen.fullScreen ? 1 : 0) == 1;
            _resolutionIdx = PlayerPrefs.GetInt(K_ResolutionIdx, -1);
            _vsync         = PlayerPrefs.GetInt(K_VSync, 1);
            _targetFps     = PlayerPrefs.GetInt(K_TargetFps, -1);

            _screenShake   = PlayerPrefs.GetFloat(K_ScreenShake, 1f);
            _vfxIntensity  = PlayerPrefs.GetFloat(K_VfxIntensity, 1f);
            _reduceMotion  = PlayerPrefs.GetInt(K_ReduceMotion, 0) == 1;
            _reduceFlashing= PlayerPrefs.GetInt(K_ReduceFlashing, 0) == 1;

            _ghostPreview  = PlayerPrefs.GetInt(K_GhostPreview, 1) == 1;
            _highlightLines= PlayerPrefs.GetInt(K_HighlightLines, 1) == 1;
            _confirmQuit   = PlayerPrefs.GetInt(K_ConfirmQuit, 1) == 1;
            _showFps       = PlayerPrefs.GetInt(K_ShowFps, 0) == 1;
            _gridLines     = PlayerPrefs.GetInt(K_GridLines, 1) == 1;

            _haptics       = PlayerPrefs.GetInt(K_Haptics, 1) == 1;
            _dragOffsetY   = PlayerPrefs.GetFloat(K_DragOffsetY, 0f);

            _colorblind    = (ColorblindMode)PlayerPrefs.GetInt(K_Colorblind, 0);
            _uiScale       = PlayerPrefs.GetFloat(K_UiScale, 1f);
            _largeText     = PlayerPrefs.GetInt(K_LargeText, 0) == 1;

            _language      = PlayerPrefs.GetString(K_Language, string.Empty);
        }

        /// <summary>Kaydedilmiş ses seviyelerini AudioManager'a uygular.</summary>
        public static void ApplyAudio()
        {
            var am = AudioManager.Instance;
            if (am == null) return;
            am.SetMasterVolume(EffectiveMaster);
            am.SetMusicVolume(MusicVolume);
            am.SetSFXVolume(SfxVolume);
        }

        /// <summary>Ekran / kalite ayarlarını uygular.</summary>
        public static void ApplyDisplay()
        {
            EnsureLoaded();

            QualitySettings.vSyncCount = _vsync;
            Application.targetFrameRate = _targetFps;

            var resolutions = Screen.resolutions;
            if (_resolutionIdx >= 0 && _resolutionIdx < resolutions.Length)
            {
                var r = resolutions[_resolutionIdx];
                Screen.SetResolution(r.width, r.height, _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed,
                                     r.refreshRateRatio);
            }
            else if (Screen.fullScreen != _fullscreen)
            {
                Screen.fullScreen = _fullscreen;
            }
        }

        /// <summary>Ses + ekran ayarlarının hepsini uygular. Oyun açılışında çağrılır.</summary>
        public static void ApplyAll()
        {
            EnsureLoaded();
            ApplyDisplay();
            ApplyAudio();
        }

        /// <summary>
        /// Sadece ayarları varsayılana döndürür. İlerleme (coin, upgrade, unlock) etkilenmez.
        /// </summary>
        public static void ResetToDefaults()
        {
            string[] keys =
            {
                K_Master, K_Music, K_Sfx, K_Muted, K_MuteUnfocused,
                K_Fullscreen, K_ResolutionIdx, K_VSync, K_TargetFps,
                K_ScreenShake, K_VfxIntensity, K_ReduceMotion, K_ReduceFlashing,
                K_GhostPreview, K_HighlightLines, K_ConfirmQuit, K_ShowFps, K_GridLines,
                K_Haptics, K_DragOffsetY,
                K_Colorblind, K_UiScale, K_LargeText,
                // dil bilinçli olarak korunuyor — sıfırlama sonrası menü okunabilir kalsın
            };
            foreach (var k in keys) PlayerPrefs.DeleteKey(k);
            Save();

            Load();
            ApplyAll();
            OnChanged?.Invoke(string.Empty);
            OnAudioChanged?.Invoke();
            OnDisplayChanged?.Invoke();
            OnAccessibilityChanged?.Invoke();
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static bool Set(ref float field, float value, string key)
        {
            if (Mathf.Approximately(field, value)) return false;
            field = value;
            PlayerPrefs.SetFloat(key, value);
            Save();
            OnChanged?.Invoke(key);
            return true;
        }

        private static bool Set(ref int field, int value, string key)
        {
            if (field == value) return false;
            field = value;
            PlayerPrefs.SetInt(key, value);
            Save();
            OnChanged?.Invoke(key);
            return true;
        }

        private static bool Set(ref bool field, bool value, string key)
        {
            if (field == value) return false;
            field = value;
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            Save();
            OnChanged?.Invoke(key);
            return true;
        }

        private static void Save() => PlayerPrefs.Save();
    }
}
