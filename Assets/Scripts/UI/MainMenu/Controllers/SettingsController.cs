using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Ana menü Settings paneli.
    ///
    /// Hierarchy:
    ///  SettingsPanel (CanvasGroup)
    ///   └── Window
    ///        ├── Header
    ///        │    └── CloseButton (Button)
    ///        ├── Content
    ///        │    ├── Row_Master
    ///        │    │    └── MasterSlider (Slider)
    ///        │    │    └── MasterValueText (TMP)
    ///        │    ├── Row_Music
    ///        │    │    └── MusicSlider (Slider)
    ///        │    │    └── MusicValueText (TMP)
    ///        │    ├── Row_SFX
    ///        │    │    └── SFXSlider (Slider)
    ///        │    │    └── SFXValueText (TMP)
    ///        │    ├── Row_Language  ← Lokalizasyon sisteminle bağla
    ///        │    │    ├── LangPrevButton (Button)
    ///        │    │    ├── LangLabel (TMP)
    ///        │    │    └── LangNextButton (Button)
    ///        │    └── Row_Fullscreen
    ///        │         └── FullscreenToggle (Toggle)
    ///        └── Footer
    ///             ├── ApplyButton (Button)
    ///             └── CancelButton (Button) [opsiyonel]
    ///
    /// Kullanım:
    ///   PanelManager.OpenPanel("Settings") veya
    ///   SettingsController'ı PanelManager'a bağla.
    /// </summary>
    public sealed class SettingsController : MonoBehaviour
    {
        // ── Audio ────────────────────────────────────────────────────────────
        [Header("Audio Sliders")]
        [SerializeField] private Slider   _masterSlider;
        [SerializeField] private TMP_Text _masterValueText;

        [SerializeField] private Slider   _musicSlider;
        [SerializeField] private TMP_Text _musicValueText;

        [SerializeField] private Slider   _sfxSlider;
        [SerializeField] private TMP_Text _sfxValueText;

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

        // ── Footer ───────────────────────────────────────────────────────────
        [Header("Footer")]
        [SerializeField] private Button   _applyButton;
        [SerializeField] private Button   _cancelButton;
        [SerializeField] private Button   _closeButton;

        // ── PlayerPrefs keys (AudioManager ile eşleşmeli) ────────────────────
        private const string MASTER_KEY = "MasterVolume";
        private const string MUSIC_KEY  = "MusicVolume";
        private const string SFX_KEY    = "SFXVolume";
        private const string FULLSCREEN_KEY = "Fullscreen";

        // Panel açılınca önceki değerleri sakla — Cancel için
        private float _prevMaster;
        private float _prevMusic;
        private float _prevSfx;
        private bool  _prevFullscreen;
        private bool  _desiredFullscreen;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            
            Debug.Log($"[Settings] Awake | ApplyBtn null: {_applyButton == null}");
            // Slider event'leri
            _masterSlider?.onValueChanged.AddListener(OnMasterChanged);
            _musicSlider?.onValueChanged.AddListener(OnMusicChanged);
            _sfxSlider?.onValueChanged.AddListener(OnSFXChanged);

            // Language butonları — lokalizasyon sisteminle doldur
            _langPrevButton?.onClick.AddListener(OnLangPrev);
            _langNextButton?.onClick.AddListener(OnLangNext);

            // Fullscreen
            _fullscreenToggle?.onValueChanged.AddListener(OnFullscreenChanged);

            // Footer
            _applyButton?.onClick.AddListener(OnApply);
            _cancelButton?.onClick.AddListener(OnCancel);
            _closeButton?.onClick.AddListener(OnClose);
        }

        private void OnEnable()
        {
            LoadSettings();

            // Listener'ları burada ekle ama duplicate olmasın
            _applyButton?.onClick.RemoveListener(OnApply);
            _applyButton?.onClick.AddListener(OnApply);
    
            _cancelButton?.onClick.RemoveListener(OnCancel);
            _cancelButton?.onClick.AddListener(OnCancel);
    
            _closeButton?.onClick.RemoveListener(OnClose);
            _closeButton?.onClick.AddListener(OnClose);
    
            _langPrevButton?.onClick.RemoveListener(OnLangPrev);
            _langPrevButton?.onClick.AddListener(OnLangPrev);
    
            _langNextButton?.onClick.RemoveListener(OnLangNext);
            _langNextButton?.onClick.AddListener(OnLangNext);
    
            _masterSlider?.onValueChanged.RemoveListener(OnMasterChanged);
            _masterSlider?.onValueChanged.AddListener(OnMasterChanged);
    
            _musicSlider?.onValueChanged.RemoveListener(OnMusicChanged);
            _musicSlider?.onValueChanged.AddListener(OnMusicChanged);
    
            _sfxSlider?.onValueChanged.RemoveListener(OnSFXChanged);
            _sfxSlider?.onValueChanged.AddListener(OnSFXChanged);
    
            _fullscreenToggle?.onValueChanged.RemoveListener(OnFullscreenChanged);
            _fullscreenToggle?.onValueChanged.AddListener(OnFullscreenChanged);
        }
        
        private void OnDisable()
        {
            _applyButton?.onClick.RemoveListener(OnApply);
            _cancelButton?.onClick.RemoveListener(OnCancel);
            _closeButton?.onClick.RemoveListener(OnClose);
            _langPrevButton?.onClick.RemoveListener(OnLangPrev);
            _langNextButton?.onClick.RemoveListener(OnLangNext);
            _masterSlider?.onValueChanged.RemoveListener(OnMasterChanged);
            _musicSlider?.onValueChanged.RemoveListener(OnMusicChanged);
            _sfxSlider?.onValueChanged.RemoveListener(OnSFXChanged);
            _fullscreenToggle?.onValueChanged.RemoveListener(OnFullscreenChanged);
        }

        // ── Load ─────────────────────────────────────────────────────────────
        private void LoadSettings()
        {
            // Kayıtlı değerleri oku (AudioManager'daki default 0.75f ile eşleşiyor)
            float master = PlayerPrefs.GetFloat(MASTER_KEY, 0.75f);
            float music  = PlayerPrefs.GetFloat(MUSIC_KEY,  0.75f);
            float sfx    = PlayerPrefs.GetFloat(SFX_KEY,    0.75f);
            bool  fs     = PlayerPrefs.GetInt(FULLSCREEN_KEY, Screen.fullScreen ? 1 : 0) == 1;

            // Önceki değerleri sakla (Cancel için)
            _prevMaster     = master;
            _prevMusic      = music;
            _prevSfx        = sfx;
            _prevFullscreen    = fs;
            _desiredFullscreen = fs;

            // Slider'ları güncelle (event tetiklemeden)
            SetSliderSilent(_masterSlider, master);
            SetSliderSilent(_musicSlider,  music);
            SetSliderSilent(_sfxSlider,    sfx);

            // Etiketleri güncelle
            RefreshValueText(_masterValueText, master);
            RefreshValueText(_musicValueText,  music);
            RefreshValueText(_sfxValueText,    sfx);

            // Fullscreen toggle — SetValueWithoutNotify prevents triggering Screen.fullScreen on panel open
            if (_fullscreenToggle != null)
                _fullscreenToggle.SetIsOnWithoutNotify(fs);
            RefreshFullscreenIcon(fs);

            // Dil etiketi — kendi lokalizasyon sisteminle güncelle
            // Örnek: if (_langLabel != null) _langLabel.text = LocalizationManager.CurrentLanguageName;
            RefreshLangLabel();
        }

        // ── Slider callbacks ─────────────────────────────────────────────────
        private void OnMasterChanged(float value)
        {
            AudioManager.Instance?.SetMasterVolume(value);
            RefreshValueText(_masterValueText, value);
        }

        private void OnMusicChanged(float value)
        {
            AudioManager.Instance?.SetMusicVolume(value);
            RefreshValueText(_musicValueText, value);
        }

        private void OnSFXChanged(float value)
        {
            AudioManager.Instance?.SetSFXVolume(value);
            RefreshValueText(_sfxValueText, value);
        }

        // ── Language ─────────────────────────────────────────────────────────
        private void OnLangPrev()
        {
            // Kendi lokalizasyon sisteminle değiştir:
            // LocalizationManager.PreviousLanguage();
            // RefreshLangLabel();
            Debug.Log("[Settings] Önceki dil — lokalizasyon sistemine bağla");
            RefreshLangLabel();
        }

        private void OnLangNext()
        {
            // Kendi lokalizasyon sisteminle değiştir:
            // LocalizationManager.NextLanguage();
            // RefreshLangLabel();
            Debug.Log("[Settings] Sonraki dil — lokalizasyon sistemine bağla");
            RefreshLangLabel();
        }

        private void RefreshLangLabel()
        {
            if (_langLabel == null) return;
            // Lokalizasyon sisteminle değiştir:
            // _langLabel.text = LocalizationManager.CurrentLanguageName;
            _langLabel.text = "TÜRKÇE"; // placeholder
        }

        // ── Fullscreen ───────────────────────────────────────────────────────
        private void OnFullscreenChanged(bool value)
        {
            _desiredFullscreen = value;
            RefreshFullscreenIcon(value);
        }

        private void RefreshFullscreenIcon(bool isOn)
        {
            if (_fullscreenIcon == null) return;
            _fullscreenIcon.sprite = isOn ? _iconChecked : _iconUnchecked;
        }

        // ── Footer ───────────────────────────────────────────────────────────

        /// <summary>
        /// Apply: ses ayarlarını PlayerPrefs'e kalıcı yazar, paneli kapatır.
        /// AudioManager zaten her slider değişiminde PlayerPrefs'e yazıyor
        /// ama fullscreen'i burada da kaydediyoruz.
        /// </summary>
        private void OnApply()
        {
            Screen.fullScreen = _desiredFullscreen;
            PlayerPrefs.SetInt(FULLSCREEN_KEY, _desiredFullscreen ? 1 : 0);
            PlayerPrefs.Save();

            ClosePanel();
        }

        /// <summary>
        /// Cancel: değişiklikleri geri al, paneli kapat.
        /// </summary>
        private void OnCancel()
        {
            // Sesleri eski haline döndür
            AudioManager.Instance?.SetMasterVolume(_prevMaster);
            AudioManager.Instance?.SetMusicVolume(_prevMusic);
            AudioManager.Instance?.SetSFXVolume(_prevSfx);

            // Fullscreen: nothing was applied yet, just reset the saved pref
            PlayerPrefs.SetFloat(MASTER_KEY,   _prevMaster);
            PlayerPrefs.SetFloat(MUSIC_KEY,    _prevMusic);
            PlayerPrefs.SetFloat(SFX_KEY,      _prevSfx);
            PlayerPrefs.SetInt(FULLSCREEN_KEY, _prevFullscreen ? 1 : 0);
            PlayerPrefs.Save();

            ClosePanel();
        }

        private void OnClose()
        {
            // Kapat = Cancel davranışı (değişiklikler kaybolur)
            OnCancel();
        }

        private void ClosePanel()
        {
            PanelManager.Instance?.CloseCurrentPanel();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Slider'ı event tetiklemeden belirli değere ayarla.
        /// </summary>
        private static void SetSliderSilent(Slider slider, float value)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(value);
        }

        /// <summary>
        /// Slider değerini 0-100 arası yüzde olarak göster.
        /// </summary>
        private static void RefreshValueText(TMP_Text label, float value)
        {
            if (label == null) return;
            label.text = Mathf.RoundToInt(value * 100f).ToString();
        }
    }
}