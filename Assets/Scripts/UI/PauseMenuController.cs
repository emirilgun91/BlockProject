using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RogueBlockBlast.Game;
using RogueBlockBlast.Core.Settings;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Oyun sahnesindeki duraklatma menüsü. ESC ile açılır/kapanır.
    ///
    /// Resume · Settings · Main Menu · Quit to Desktop
    ///
    /// Açıkken:
    ///   • Time.timeScale = 0 (oyun donar)
    ///   • GameStateController.LockInput() (RunController tıklamaları yok sayar)
    ///
    /// Ayarlar alt paneli açıkken ESC önce onu kapatır, pause menüsünü değil.
    /// </summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("Açılıp kapanan görsel kap. Bu script'in DURDUĞU obje OLMAMALI — " +
                 "aksi halde menü kapalıyken Update() çalışmaz ve ESC dinlenmez.")]
        [SerializeField] private GameObject _root;            // pause menü içeriği
        [SerializeField] private GameObject _settingsPanel;   // oyun içi ayarlar paneli (opsiyonel)

        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _quitButton;

        [Header("Confirm Box")]
        [Tooltip("Çıkış onayı kutusu — GameSettings.ConfirmQuit açıkken kullanılır.")]
        [SerializeField] private GameObject _confirmBox;
        [SerializeField] private Button _confirmYes;
        [SerializeField] private Button _confirmNo;

        [Header("Scenes")]
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        public bool IsOpen { get; private set; }

        // Onay kutusu hangi eylem için açıldı
        private enum PendingAction { None, MainMenu, QuitGame }
        private PendingAction _pending = PendingAction.None;

        private float _previousTimeScale = 1f;

        private void Awake()
        {
            if (_root == null || _root == gameObject)
            {
                Debug.LogError(
                    "[Pause] _root bu script'in durduğu objeye eşit olamaz — kapandığında Update() " +
                    "çalışmayacağı için ESC dinlenmez. Menüyü ayrı bir alt objeye taşı " +
                    "(Tools ▸ RogueBlockBlast ▸ Pause Menu ▸ Build In Game Scene bunu doğru kurar).", this);
            }

            _resumeButton?.onClick.AddListener(Resume);
            _settingsButton?.onClick.AddListener(OpenSettings);
            _mainMenuButton?.onClick.AddListener(() => Request(PendingAction.MainMenu));
            _quitButton?.onClick.AddListener(() => Request(PendingAction.QuitGame));
            _confirmYes?.onClick.AddListener(ConfirmPending);
            _confirmNo?.onClick.AddListener(CancelPending);

            SetActive(_root, false);
            SetActive(_settingsPanel, false);
            SetActive(_confirmBox, false);
            IsOpen = false;
        }

        private void OnDestroy()
        {
            // Sahne değişirken oyun donmuş kalmasın
            if (IsOpen) Time.timeScale = _previousTimeScale;
        }

        private void Update()
        {
            if (!EscapePressed()) return;

            // Ayarlar açıksa önce onu kapat
            if (_settingsPanel != null && _settingsPanel.activeSelf)
            {
                SetActive(_settingsPanel, false);
                return;
            }

            // Onay kutusu açıksa önce onu kapat
            if (_confirmBox != null && _confirmBox.activeSelf)
            {
                CancelPending();
                return;
            }

            if (IsOpen) Resume();
            else Open();
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        // ── Public API ───────────────────────────────────────────────────────
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            GameStateController.LockInput();

            SetActive(_root, true);
            SetActive(_confirmBox, false);
            _pending = PendingAction.None;
        }

        public void Resume()
        {
            if (!IsOpen) return;
            IsOpen = false;

            Time.timeScale = _previousTimeScale <= 0f ? 1f : _previousTimeScale;
            GameStateController.UnlockInput();

            SetActive(_settingsPanel, false);
            SetActive(_confirmBox, false);
            SetActive(_root, false);
            _pending = PendingAction.None;
        }

        // ── Buttons ──────────────────────────────────────────────────────────
        private void OpenSettings()
        {
            if (_settingsPanel == null)
            {
                Debug.LogWarning("[Pause] Ayarlar paneli bağlanmamış.", this);
                return;
            }
            SetActive(_settingsPanel, true);
        }

        /// <summary>
        /// Çıkış onayı kapalıysa doğrudan uygula, açıksa önce sor.
        /// Run ortasında yanlışlıkla çıkmayı engeller.
        /// </summary>
        private void Request(PendingAction action)
        {
            _pending = action;

            if (!GameSettings.ConfirmQuit || _confirmBox == null)
            {
                ConfirmPending();
                return;
            }
            SetActive(_confirmBox, true);
        }

        private void CancelPending()
        {
            _pending = PendingAction.None;
            SetActive(_confirmBox, false);
        }

        private void ConfirmPending()
        {
            var action = _pending;
            _pending = PendingAction.None;
            SetActive(_confirmBox, false);

            switch (action)
            {
                case PendingAction.MainMenu: GoToMainMenu(); break;
                case PendingAction.QuitGame: QuitToDesktop(); break;
            }
        }

        private void GoToMainMenu()
        {
            // Sahne yüklenmeden önce zamanı ve input kilidini normale döndür
            Time.timeScale = 1f;
            GameStateController.Reset();
            IsOpen = false;

            if (SceneTransition.Instance != null)
                SceneTransition.Instance.LoadScene(_mainMenuSceneName);
            else
                SceneManager.LoadScene(_mainMenuSceneName);
        }

        private void QuitToDesktop()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void SetActive(GameObject go, bool value)
        {
            if (go != null && go.activeSelf != value) go.SetActive(value);
        }
    }
}
