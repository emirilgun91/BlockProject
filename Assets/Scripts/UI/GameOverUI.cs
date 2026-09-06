using RogueBlockBlast.Game;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{

    public sealed class GameOverUI : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static GameOverUI Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Root")]
        [SerializeField] private GameObject  _root;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Score")]
        [SerializeField] private TMP_Text   _finalScoreText;
        [SerializeField] private TMP_Text   _bestScoreText;
        [SerializeField] private GameObject _newRecordBadge;

        [Header("Stats")]
        [SerializeField] private TMP_Text _linesText;
        [SerializeField] private TMP_Text _piecesText;
        [SerializeField] private TMP_Text _maxComboText;
        [Tooltip("Bu turda kazanılan Blockcoin. Eskiden seçilen kart sayısını gösteriyordu.")]
        [UnityEngine.Serialization.FormerlySerializedAs("_cardsText")]
        [SerializeField] private TMP_Text _coinsEarnedText;

        [Header("Buttons")]
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _mainMenuButton;

        [Header("Scene Names")]
        [SerializeField] private string _gameSceneName    = "Game";
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        [Header("Fade")]
        [SerializeField] private float _fadeSpeed = 5f;

        // ── PlayerPrefs key ──────────────────────────────────────────────────
        private const string BestScoreKey = "BestScore";

        /// <summary>
        /// Rekoru kaydeder; yeni rekorsa true döner.
        /// Statik, çünkü run her zaman bu panelle bitmiyor — demo sonu paneli
        /// Game Over ekranını hiç açmadan run'ı kapatıyor ve skorun yine de
        /// yazılması gerekiyor.
        /// </summary>
        public static bool RecordBestScore(int finalScore)
        {
            int best = PlayerPrefs.GetInt(BestScoreKey, 0);
            if (finalScore <= best) return false;

            PlayerPrefs.SetInt(BestScoreKey, finalScore);
            PlayerPrefs.Save();
            return true;
        }

        // ── Runtime ──────────────────────────────────────────────────────────
        private bool _fadingIn;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            _root.SetActive(false);
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;

            _retryButton.onClick.AddListener(OnRetry);
            _mainMenuButton.onClick.AddListener(OnMainMenu);
        }

        private void Update()
        {
            if (_canvasGroup == null || !_root.activeSelf) return;
            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha,
                _fadingIn ? 1f : 0f,
                
                Time.unscaledDeltaTime * _fadeSpeed
            );

            // Tamamen gizlenince etkileşimi kapat
            bool visible = _canvasGroup.alpha > 0f;
            _canvasGroup.interactable   = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Shows the Game Over overlay.
        /// Pass in the final score from your ScoreSystem.
        /// Stats are read automatically from RunStatsTracker.Instance.
        /// </summary>
        public void Show(int finalScore, int coinsEarned = 0)
        {
            // ── Best score ──────────────────────────────────────────────────
            bool newRecord = RecordBestScore(finalScore);
            int  bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

            // ── Score display ───────────────────────────────────────────────
            _finalScoreText.text = FormatScore(finalScore);
            _bestScoreText.text  = FormatScore(bestScore);
 
            if (_newRecordBadge != null)
                _newRecordBadge.SetActive(newRecord);
 
            // ── Stats ───────────────────────────────────────────────────────
            if (RunStatsTracker.Instance != null)
            {
                var s = RunStatsTracker.Instance;

                _linesText.text    = $"{s.LinesCleared}";
                _piecesText.text   = $"{s.PiecesPlaced}";
                _maxComboText.text = s.MaxCombo > 10
                    ? $"×{s.MaxCombo / 10f:0.0}"
                    : "—";
            }

            // Kazanılan Blockcoin — RunStatsTracker'dan değil, run'ın kendi sayacından
            // gelir (milestone ödülleri + Bounty Hunter + Perfect Clear hepsi orada toplanır).
            if (_coinsEarnedText != null)
                _coinsEarnedText.text = coinsEarned.ToString("N0");
 
            // ── Show ─────────────────────────────────────────────────────────
            Time.timeScale = 0f;
            Game.GameStateController.LockInput();
            _root.SetActive(true);
            _fadingIn = true;
 
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        // ── Buttons ──────────────────────────────────────────────────────────
        private void OnRetry()
        {
            Time.timeScale = 1f;
            Game.GameStateController.Reset();
            SceneManager.LoadScene(_gameSceneName);
        }

        private void OnMainMenu()
        {
            Time.timeScale = 1f;
            Game.GameStateController.Reset();
            SceneTransition.Instance?.LoadScene(_mainMenuSceneName);
        }

        /// <summary>
        /// R tuşuyla NewRun çağrılınca buradan kapat.
        /// gameObject'i kapatmaz — sadece _root'u kapatır.
        /// </summary>
        public void Hide()
        {
            Time.timeScale = 1f;
            Game.GameStateController.UnlockInput();
            _fadingIn = false;
            
        }

        private static string FormatScore(int score) =>
            score.ToString("N0");
    }
}