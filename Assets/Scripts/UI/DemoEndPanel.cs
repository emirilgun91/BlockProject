using RogueBlockBlast.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Demo bitiş paneli. Oyuncu demonun son aşamasına ulaştığında açılır;
    /// teşekkür eder, wishlist ve geri bildirim bağlantılarını sunar.
    ///
    /// Panel açıldığında run <b>biter</b> — kart seçimi gösterilmez, oyun
    /// duraklatılır ve girdi kilitlenir. Buradan çıkışın tek yolu ana menü.
    ///
    /// Bağlantı adresleri <see cref="ExternalLinkButton"/> üzerinden verilir.
    /// URL boş bırakılırsa o buton kendini devre dışı bırakır — adres gelene
    /// kadar panel yine de düzgün çalışır.
    /// </summary>
    public sealed class DemoEndPanel : MonoBehaviour
    {
        public static DemoEndPanel Instance { get; private set; }

        [Header("Root")]
        [Tooltip("Panelin gövdesi. Bu bileşen kapatılan nesnenin üstünde OLMAMALI — " +
                 "yoksa kendi Awake/Show'unu kaybeder.")]
        [SerializeField] private GameObject  _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float       _fadeDuration = 0.35f;

        [Header("Buttons")]
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _wishlistButton;
        [SerializeField] private Button _feedbackButton;

        [Header("Scene")]
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        [Header("Audio")]
        [SerializeField] private AudioClip _openSfx;

        private bool  _isOpen;
        private float _fadeElapsed;

        /// <summary>Panel açıksa true — RunController buna bakıp girdiyi kapatır.</summary>
        public bool IsOpen => _isOpen;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = 0f;
                _canvasGroup.interactable   = false;
                _canvasGroup.blocksRaycasts = false;
            }

            _root?.SetActive(false);

            _mainMenuButton?.onClick.AddListener(GoToMainMenu);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_isOpen || _canvasGroup == null) return;

            // Panel timeScale = 0 iken açılıyor; unscaled zaman şart.
            _fadeElapsed += Time.unscaledDeltaTime;

            float t = _fadeDuration <= 0f ? 1f : Mathf.Clamp01(_fadeElapsed / _fadeDuration);
            _canvasGroup.alpha = t;

            if (t >= 1f)
            {
                _canvasGroup.interactable   = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Paneli açar ve oyunu durdurur.</summary>
        public void Show()
        {
            if (_isOpen) return;
            _isOpen      = true;
            _fadeElapsed = 0f;

            _root?.SetActive(true);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = 0f;
                _canvasGroup.blocksRaycasts = true;   // arkadaki tıklamaları hemen kes
                _canvasGroup.interactable   = false;  // fade bitince açılır
            }

            RefreshLinkButtons();

            Time.timeScale = 0f;
            GameStateController.LockInput();

            if (_openSfx != null) AudioManager.Instance?.PlaySFX(_openSfx);
        }

        // ── Private ──────────────────────────────────────────────────────────

        /// <summary>
        /// URL'si olmayan bağlantı butonlarını gizler. Adresler sonradan
        /// gireceği için panelin şimdiden eksiksiz görünmesi gerekiyor —
        /// boş bir butonu göstermek yerine hiç göstermemek daha temiz.
        /// </summary>
        private void RefreshLinkButtons()
        {
            ApplyLink(_wishlistButton);
            ApplyLink(_feedbackButton);
        }

        private static void ApplyLink(Button button)
        {
            if (button == null) return;

            var link = button.GetComponent<ExternalLinkButton>();
            bool hasUrl = link != null && !string.IsNullOrWhiteSpace(link.Url);

            button.gameObject.SetActive(hasUrl);
        }

        private void GoToMainMenu()
        {
            // Sahne yüklenmeden önce zaman ve girdi kilidi normale döner —
            // PauseMenuController ile aynı sıra.
            Time.timeScale = 1f;
            GameStateController.Reset();
            _isOpen = false;

            if (SceneTransition.Instance != null)
                SceneTransition.Instance.LoadScene(_mainMenuSceneName);
            else
                SceneManager.LoadScene(_mainMenuSceneName);
        }
    }
}
