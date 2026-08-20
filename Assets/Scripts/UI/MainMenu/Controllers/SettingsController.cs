using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RogueBlockBlast.Core.Settings;
using RogueBlockBlast.Core.Localization;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Ayarlar paneli. Tüm durum <see cref="GameSettings"/> içinde tutulur —
    /// bu sınıf sadece görünüm ve bağlama yapar.
    ///
    /// Alanların HEPSİ opsiyoneldir. Sadece prefab'da bağladıkların çalışır;
    /// bağlamadıkların sessizce atlanır. Yani yeni kontrolleri istediğin sırada
    /// ekleyebilirsin, panel arada bozulmaz.
    ///
    /// Hierarchy örneği:
    ///  SettingsPanel (CanvasGroup)
    ///   └── Window
    ///        ├── Header    → CloseButton
    ///        ├── Content
    ///        │    ├── Audio        → Master / Music / SFX slider + Mute toggle
    ///        │    ├── Display      → Fullscreen / Resolution / VSync / FPS
    ///        │    ├── Gameplay     → Ghost / Highlight / Grid / Haptics
    ///        │    ├── Accessibility→ ReduceMotion / Shake / VFX / Colorblind / UIScale
    ///        │    ├── Language     → Prev / Label / Next
    ///        │    └── Data         → ResetSettings / ResetProgress
    ///        └── Footer    → Apply / Cancel
    /// </summary>
    public sealed class SettingsController : MonoBehaviour
    {
        // ── Audio ────────────────────────────────────────────────────────────
        [Header("Audio")]
        [SerializeField] private Slider   _masterSlider;
        [SerializeField] private TMP_Text _masterValueText;
        [SerializeField] private Slider   _musicSlider;
        [SerializeField] private TMP_Text _musicValueText;
        [SerializeField] private Slider   _sfxSlider;
        [SerializeField] private TMP_Text _sfxValueText;
        [SerializeField] private Toggle   _muteToggle;
        [SerializeField] private Toggle   _muteUnfocusedToggle;

        // ── Language ─────────────────────────────────────────────────────────
        [Header("Language")]
        [SerializeField] private Button   _langPrevButton;
        [SerializeField] private Button   _langNextButton;
        [SerializeField] private TMP_Text _langLabel;

        // ── Display ──────────────────────────────────────────────────────────
        [Header("Display")]
        [SerializeField] private Toggle   _fullscreenToggle;
        [SerializeField] private Image    _fullscreenIcon;
        [SerializeField] private Sprite   _iconChecked;
        [SerializeField] private Sprite   _iconUnchecked;

        [SerializeField] private Button   _resPrevButton;
        [SerializeField] private Button   _resNextButton;
        [SerializeField] private TMP_Text _resLabel;

        [SerializeField] private Button   _vsyncButton;
        [SerializeField] private TMP_Text _vsyncLabel;

        [SerializeField] private Button   _fpsButton;
        [SerializeField] private TMP_Text _fpsLabel;

        [SerializeField] private Toggle   _showFpsToggle;

        // ── Gameplay ─────────────────────────────────────────────────────────
        [Header("Gameplay")]
        [SerializeField] private Toggle   _ghostToggle;
        [SerializeField] private Toggle   _highlightToggle;
        [SerializeField] private Toggle   _gridToggle;
        [SerializeField] private Toggle   _confirmQuitToggle;
        [SerializeField] private Toggle   _hapticsToggle;
        [SerializeField] private Slider   _dragOffsetSlider;

        // ── Accessibility ────────────────────────────────────────────────────
        [Header("Accessibility")]
        [SerializeField] private Toggle   _reduceMotionToggle;
        [SerializeField] private Toggle   _reduceFlashingToggle;
        [SerializeField] private Slider   _screenShakeSlider;
        [SerializeField] private TMP_Text _screenShakeValueText;
        [SerializeField] private Slider   _vfxSlider;
        [SerializeField] private TMP_Text _vfxValueText;
        [SerializeField] private Button   _colorblindButton;
        [SerializeField] private TMP_Text _colorblindLabel;
        [SerializeField] private Slider   _uiScaleSlider;
        [SerializeField] private TMP_Text _uiScaleValueText;
        [SerializeField] private Toggle   _largeTextToggle;

        // ── Data ─────────────────────────────────────────────────────────────
        [Header("Data")]
        [SerializeField] private Button   _resetSettingsButton;
        [SerializeField] private Button   _resetProgressButton;
        [Tooltip("İlerleme sıfırlama onay kutusu. İlk tıklamada açılır.")]
        [SerializeField] private GameObject _resetProgressConfirm;
        [SerializeField] private Button   _resetProgressConfirmYes;
        [SerializeField] private Button   _resetProgressConfirmNo;

        // ── Footer ───────────────────────────────────────────────────────────
        [Header("Footer")]
        [SerializeField] private Button   _applyButton;
        [SerializeField] private Button   _cancelButton;
        [SerializeField] private Button   _closeButton;

        // Cancel için panel açılışındaki anlık görüntü
        private struct Snapshot
        {
            public float Master, Music, Sfx, Shake, Vfx, UiScale, DragOffset;
            public bool  Muted, MuteUnfocused, Fullscreen, ShowFps;
            public bool  Ghost, Highlight, Grid, ConfirmQuit, Haptics;
            public bool  ReduceMotion, ReduceFlashing, LargeText;
            public int   ResolutionIdx, VSync, TargetFps;
            public ColorblindMode Colorblind;
            public string Language;
        }

        private Snapshot _snapshot;
        private bool _suppressCallbacks;      // UI'ı programatik doldururken event yazmasın
        private Resolution[] _resolutions;
        private int _resIndex;

        private static readonly int[] FpsOptions = { -1, 30, 60, 90, 120, 144 };

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            GameSettings.EnsureLoaded();
            Loc.Init();
            BuildResolutionList();
        }

        private void OnEnable()
        {
            TakeSnapshot();
            Bind(true);
            RefreshAll();
            Loc.OnChanged += RefreshAll;
        }

        private void OnDisable()
        {
            Loc.OnChanged -= RefreshAll;
            Bind(false);
        }

        // ── Binding ──────────────────────────────────────────────────────────
        /// <summary>Tüm listener'ları tek yerden ekler/çıkarır — duplicate imkânsız.</summary>
        private void Bind(bool on)
        {
            BindSlider(_masterSlider,     OnMasterChanged,      on);
            BindSlider(_musicSlider,      OnMusicChanged,       on);
            BindSlider(_sfxSlider,        OnSfxChanged,         on);
            BindSlider(_screenShakeSlider,OnScreenShakeChanged, on);
            BindSlider(_vfxSlider,        OnVfxChanged,         on);
            BindSlider(_uiScaleSlider,    OnUiScaleChanged,     on);
            BindSlider(_dragOffsetSlider, OnDragOffsetChanged,  on);

            BindToggle(_muteToggle,           v => { GameSettings.Muted = v; RefreshAudio(); }, on);
            BindToggle(_muteUnfocusedToggle,  v => GameSettings.MuteWhenUnfocused = v,          on);
            BindToggle(_fullscreenToggle,     OnFullscreenChanged,                              on);
            BindToggle(_showFpsToggle,        v => GameSettings.ShowFps = v,                    on);
            BindToggle(_ghostToggle,          v => GameSettings.GhostPreview = v,               on);
            BindToggle(_highlightToggle,      v => GameSettings.HighlightClearingLines = v,     on);
            BindToggle(_gridToggle,           v => GameSettings.GridLines = v,                  on);
            BindToggle(_confirmQuitToggle,    v => GameSettings.ConfirmQuit = v,                on);
            BindToggle(_hapticsToggle,        v => GameSettings.Haptics = v,                    on);
            BindToggle(_reduceFlashingToggle, v => GameSettings.ReduceFlashing = v,             on);
            BindToggle(_largeTextToggle,      v => GameSettings.LargeText = v,                  on);
            BindToggle(_reduceMotionToggle,   OnReduceMotionChanged,                            on);

            BindButton(_langPrevButton,  () => { Loc.PreviousLanguage(); RefreshLanguage(); }, on);
            BindButton(_langNextButton,  () => { Loc.NextLanguage();     RefreshLanguage(); }, on);
            BindButton(_resPrevButton,   () => CycleResolution(-1), on);
            BindButton(_resNextButton,   () => CycleResolution(+1), on);
            BindButton(_vsyncButton,     CycleVSync,                on);
            BindButton(_fpsButton,       CycleFps,                  on);
            BindButton(_colorblindButton,CycleColorblind,           on);

            BindButton(_resetSettingsButton, OnResetSettings,        on);
            BindButton(_resetProgressButton, OnResetProgressAsk,     on);
            BindButton(_resetProgressConfirmYes, OnResetProgressConfirm, on);
            BindButton(_resetProgressConfirmNo,  () => ShowResetConfirm(false), on);

            BindButton(_applyButton,  OnApply,  on);
            BindButton(_cancelButton, OnCancel, on);
            BindButton(_closeButton,  OnCancel, on);
        }

        private void BindSlider(Slider s, UnityEngine.Events.UnityAction<float> fn, bool on)
        {
            if (s == null) return;
            s.onValueChanged.RemoveListener(fn);
            if (on) s.onValueChanged.AddListener(fn);
        }

        private void BindToggle(Toggle t, Action<bool> fn, bool on)
        {
            if (t == null) return;
            UnityEngine.Events.UnityAction<bool> wrapped = v => { if (!_suppressCallbacks) fn(v); };
            // RemoveAllListeners: lambda referansı tutulamadığı için tek güvenli yol.
            t.onValueChanged.RemoveAllListeners();
            if (on) t.onValueChanged.AddListener(wrapped);
        }

        private void BindButton(Button b, Action fn, bool on)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            if (on) b.onClick.AddListener(() => fn());
        }

        // ── Slider callbacks ─────────────────────────────────────────────────
        private void OnMasterChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettings.MasterVolume = v;
            SetPercent(_masterValueText, v);
        }

        private void OnMusicChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettings.MusicVolume = v;
            SetPercent(_musicValueText, v);
        }

        private void OnSfxChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettings.SfxVolume = v;
            SetPercent(_sfxValueText, v);
        }

        private void OnScreenShakeChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettings.ScreenShake = v;
            SetPercent(_screenShakeValueText, v);
        }

        private void OnVfxChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettings.VfxIntensity = v;
            SetPercent(_vfxValueText, v);
        }

        private void OnUiScaleChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettings.UiScale = v;
            if (_uiScaleValueText != null) _uiScaleValueText.text = $"{v:0.00}x";
        }

        private void OnDragOffsetChanged(float v)
        {
            if (_suppressCallbacks) return;
            GameSettings.DragOffsetY = v;
        }

        // ── Toggle callbacks ─────────────────────────────────────────────────
        private void OnFullscreenChanged(bool v)
        {
            GameSettings.Fullscreen = v;
            RefreshFullscreenIcon(v);
        }

        /// <summary>ReduceMotion shake/VFX değerlerini ezdiği için o slider'ları da yeniler.</summary>
        private void OnReduceMotionChanged(bool v)
        {
            GameSettings.ReduceMotion = v;
            RefreshAccessibility();
        }

        // ── Cycling controls ─────────────────────────────────────────────────
        private void BuildResolutionList()
        {
            var all = Screen.resolutions;
            var unique = new List<Resolution>();
            for (int i = 0; i < all.Length; i++)
            {
                // Aynı genişlik/yükseklik farklı tazeleme hızlarıyla tekrarlanıyor — en yükseğini tut.
                bool dup = false;
                for (int j = 0; j < unique.Count; j++)
                    if (unique[j].width == all[i].width && unique[j].height == all[i].height) { dup = true; break; }
                if (!dup) unique.Add(all[i]);
            }
            _resolutions = unique.ToArray();

            int saved = GameSettings.ResolutionIndex;
            _resIndex = saved >= 0 && saved < _resolutions.Length ? saved : CurrentResolutionIndex();
        }

        private int CurrentResolutionIndex()
        {
            for (int i = 0; i < _resolutions.Length; i++)
                if (_resolutions[i].width == Screen.width && _resolutions[i].height == Screen.height)
                    return i;
            return Mathf.Max(0, _resolutions.Length - 1);
        }

        private void CycleResolution(int step)
        {
            if (_resolutions == null || _resolutions.Length == 0) return;
            _resIndex = (_resIndex + step + _resolutions.Length) % _resolutions.Length;
            GameSettings.ResolutionIndex = _resIndex;
            RefreshDisplay();
        }

        private void CycleVSync()
        {
            GameSettings.VSync = (GameSettings.VSync + 1) % 3;
            RefreshDisplay();
        }

        private void CycleFps()
        {
            int cur = Array.IndexOf(FpsOptions, GameSettings.TargetFrameRate);
            int next = (cur + 1 + FpsOptions.Length) % FpsOptions.Length;
            GameSettings.TargetFrameRate = FpsOptions[next];
            RefreshDisplay();
        }

        private void CycleColorblind()
        {
            int next = ((int)GameSettings.Colorblind + 1) % 5;
            GameSettings.Colorblind = (ColorblindMode)next;
            RefreshAccessibility();
        }

        // ── Refresh ──────────────────────────────────────────────────────────
        private void RefreshAll()
        {
            _suppressCallbacks = true;
            RefreshAudio();
            RefreshDisplay();
            RefreshGameplay();
            RefreshAccessibility();
            RefreshLanguage();
            ShowResetConfirm(false);
            _suppressCallbacks = false;
        }

        private void RefreshAudio()
        {
            bool prev = _suppressCallbacks; _suppressCallbacks = true;

            SetSlider(_masterSlider, GameSettings.MasterVolume);
            SetSlider(_musicSlider,  GameSettings.MusicVolume);
            SetSlider(_sfxSlider,    GameSettings.SfxVolume);
            SetPercent(_masterValueText, GameSettings.MasterVolume);
            SetPercent(_musicValueText,  GameSettings.MusicVolume);
            SetPercent(_sfxValueText,    GameSettings.SfxVolume);
            SetToggle(_muteToggle,          GameSettings.Muted);
            SetToggle(_muteUnfocusedToggle, GameSettings.MuteWhenUnfocused);

            // Sustur açıkken ses slider'ları anlamsız — soluklaştır
            SetInteractable(_masterSlider, !GameSettings.Muted);
            SetInteractable(_musicSlider,  !GameSettings.Muted);
            SetInteractable(_sfxSlider,    !GameSettings.Muted);

            _suppressCallbacks = prev;
        }

        private void RefreshDisplay()
        {
            bool prev = _suppressCallbacks; _suppressCallbacks = true;

            SetToggle(_fullscreenToggle, GameSettings.Fullscreen);
            RefreshFullscreenIcon(GameSettings.Fullscreen);
            SetToggle(_showFpsToggle, GameSettings.ShowFps);

            if (_resLabel != null && _resolutions != null && _resolutions.Length > 0)
            {
                int i = Mathf.Clamp(_resIndex, 0, _resolutions.Length - 1);
                _resLabel.text = $"{_resolutions[i].width} × {_resolutions[i].height}";
            }

            if (_vsyncLabel != null)
            {
                _vsyncLabel.text = GameSettings.VSync switch
                {
                    0 => Loc.Get("Settings.Display.VSync.Off"),
                    1 => Loc.Get("Settings.Display.VSync.Every"),
                    _ => Loc.Get("Settings.Display.VSync.Half"),
                };
            }

            if (_fpsLabel != null)
            {
                int fps = GameSettings.TargetFrameRate;
                _fpsLabel.text = fps <= 0 ? Loc.Get("Settings.Unlimited") : fps.ToString();
            }

            _suppressCallbacks = prev;
        }

        private void RefreshGameplay()
        {
            bool prev = _suppressCallbacks; _suppressCallbacks = true;

            SetToggle(_ghostToggle,       GameSettings.GhostPreview);
            SetToggle(_highlightToggle,   GameSettings.HighlightClearingLines);
            SetToggle(_gridToggle,        GameSettings.GridLines);
            SetToggle(_confirmQuitToggle, GameSettings.ConfirmQuit);
            SetToggle(_hapticsToggle,     GameSettings.Haptics);
            SetSlider(_dragOffsetSlider,  GameSettings.DragOffsetY);

            _suppressCallbacks = prev;
        }

        private void RefreshAccessibility()
        {
            bool prev = _suppressCallbacks; _suppressCallbacks = true;

            SetToggle(_reduceMotionToggle,   GameSettings.ReduceMotion);
            SetToggle(_reduceFlashingToggle, GameSettings.ReduceFlashing);
            SetToggle(_largeTextToggle,      GameSettings.LargeText);

            SetSlider(_screenShakeSlider, GameSettings.ScreenShakeRaw);
            SetSlider(_vfxSlider,         GameSettings.VfxIntensityRaw);
            SetSlider(_uiScaleSlider,     GameSettings.UiScale);
            SetPercent(_screenShakeValueText, GameSettings.ScreenShakeRaw);
            SetPercent(_vfxValueText,         GameSettings.VfxIntensityRaw);
            if (_uiScaleValueText != null) _uiScaleValueText.text = $"{GameSettings.UiScale:0.00}x";

            // ReduceMotion bu ikisini zaten eziyor — kilitli göster
            SetInteractable(_screenShakeSlider, !GameSettings.ReduceMotion);
            SetInteractable(_vfxSlider,         !GameSettings.ReduceMotion);

            if (_colorblindLabel != null)
            {
                _colorblindLabel.text = GameSettings.Colorblind switch
                {
                    ColorblindMode.Protanopia    => Loc.Get("Settings.Access.Colorblind.Protanopia"),
                    ColorblindMode.Deuteranopia  => Loc.Get("Settings.Access.Colorblind.Deuteranopia"),
                    ColorblindMode.Tritanopia    => Loc.Get("Settings.Access.Colorblind.Tritanopia"),
                    ColorblindMode.HighContrast  => Loc.Get("Settings.Access.Colorblind.HighContrast"),
                    _                            => Loc.Get("Settings.Access.Colorblind.None"),
                };
            }

            _suppressCallbacks = prev;
        }

        private void RefreshLanguage()
        {
            if (_langLabel != null)
                _langLabel.text = Loc.CurrentLanguageDisplayName;

            bool multiple = Loc.AvailableLanguages.Count > 1;
            if (_langPrevButton != null) _langPrevButton.interactable = multiple;
            if (_langNextButton != null) _langNextButton.interactable = multiple;
        }

        private void RefreshFullscreenIcon(bool isOn)
        {
            if (_fullscreenIcon == null) return;
            _fullscreenIcon.sprite = isOn ? _iconChecked : _iconUnchecked;
        }

        // ── Data ─────────────────────────────────────────────────────────────
        private void OnResetSettings()
        {
            GameSettings.ResetToDefaults();
            BuildResolutionList();
            RefreshAll();
        }

        private void OnResetProgressAsk() => ShowResetConfirm(true);

        private void ShowResetConfirm(bool visible)
        {
            if (_resetProgressConfirm != null) _resetProgressConfirm.SetActive(visible);
        }

        /// <summary>
        /// Coin, upgrade ve unlock ilerlemesini siler. Ayarlar ve dil korunur.
        /// Geri alınamaz — bu yüzden onay kutusundan geçer.
        /// </summary>
        private void OnResetProgressConfirm()
        {
            SaveDataService.ResetProgress();
            ShowResetConfirm(false);
            RefreshAll();
        }

        // ── Footer ───────────────────────────────────────────────────────────
        private void OnApply()
        {
            GameSettings.ApplyAll();
            ClosePanel();
        }

        /// <summary>Panel açıldığı andaki değerlere geri döner.</summary>
        private void OnCancel()
        {
            var s = _snapshot;

            GameSettings.MasterVolume = s.Master;
            GameSettings.MusicVolume  = s.Music;
            GameSettings.SfxVolume    = s.Sfx;
            GameSettings.Muted        = s.Muted;
            GameSettings.MuteWhenUnfocused = s.MuteUnfocused;

            GameSettings.Fullscreen      = s.Fullscreen;
            GameSettings.ResolutionIndex = s.ResolutionIdx;
            GameSettings.VSync           = s.VSync;
            GameSettings.TargetFrameRate = s.TargetFps;
            GameSettings.ShowFps         = s.ShowFps;

            GameSettings.GhostPreview           = s.Ghost;
            GameSettings.HighlightClearingLines = s.Highlight;
            GameSettings.GridLines              = s.Grid;
            GameSettings.ConfirmQuit            = s.ConfirmQuit;
            GameSettings.Haptics                = s.Haptics;
            GameSettings.DragOffsetY            = s.DragOffset;

            GameSettings.ReduceMotion   = s.ReduceMotion;
            GameSettings.ReduceFlashing = s.ReduceFlashing;
            GameSettings.ScreenShake    = s.Shake;
            GameSettings.VfxIntensity   = s.Vfx;
            GameSettings.Colorblind     = s.Colorblind;
            GameSettings.UiScale        = s.UiScale;
            GameSettings.LargeText      = s.LargeText;

            Loc.SetLanguage(s.Language);

            GameSettings.ApplyAll();
            ClosePanel();
        }

        private void TakeSnapshot()
        {
            _snapshot = new Snapshot
            {
                Master        = GameSettings.MasterVolume,
                Music         = GameSettings.MusicVolume,
                Sfx           = GameSettings.SfxVolume,
                Muted         = GameSettings.Muted,
                MuteUnfocused = GameSettings.MuteWhenUnfocused,

                Fullscreen    = GameSettings.Fullscreen,
                ResolutionIdx = GameSettings.ResolutionIndex,
                VSync         = GameSettings.VSync,
                TargetFps     = GameSettings.TargetFrameRate,
                ShowFps       = GameSettings.ShowFps,

                Ghost         = GameSettings.GhostPreview,
                Highlight     = GameSettings.HighlightClearingLines,
                Grid          = GameSettings.GridLines,
                ConfirmQuit   = GameSettings.ConfirmQuit,
                Haptics       = GameSettings.Haptics,
                DragOffset    = GameSettings.DragOffsetY,

                ReduceMotion  = GameSettings.ReduceMotion,
                ReduceFlashing= GameSettings.ReduceFlashing,
                Shake         = GameSettings.ScreenShakeRaw,
                Vfx           = GameSettings.VfxIntensityRaw,
                Colorblind    = GameSettings.Colorblind,
                UiScale       = GameSettings.UiScale,
                LargeText     = GameSettings.LargeText,

                Language      = Loc.CurrentLanguage,
            };
        }

        /// <summary>
        /// Ana menüde PanelManager kapatır. Oyun sahnesinde (pause menüsü içinde)
        /// PanelManager yok — o zaman panel kendini kapatır.
        /// </summary>
        private void ClosePanel()
        {
            if (PanelManager.Instance != null) PanelManager.Instance.CloseCurrentPanel();
            else gameObject.SetActive(false);
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static void SetSlider(Slider s, float v) { if (s != null) s.SetValueWithoutNotify(v); }
        private static void SetToggle(Toggle t, bool v)  { if (t != null) t.SetIsOnWithoutNotify(v); }
        private static void SetPercent(TMP_Text l, float v) { if (l != null) l.text = Mathf.RoundToInt(v * 100f).ToString(); }

        private static void SetInteractable(Selectable s, bool on)
        {
            if (s == null) return;
            s.interactable = on;
            var cg = s.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = on ? 1f : 0.45f;
        }
    }
}
