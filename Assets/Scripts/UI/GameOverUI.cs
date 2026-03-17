using RogueBlockBlast.Game;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Game Over overlay — shown when no shape in the pool can be placed.
    ///
    /// Hierarchy expected:
    ///  GameOverUI (this)
    ///   ├ Backdrop              (Image — dark semi-transparent)
    ///   ├ Panel
    ///   │  ├ TitleText          (TMP)  "GAME OVER"
    ///   │  ├ ScoreSection
    ///   │  │  ├ FinalScoreLabel (TMP)
    ///   │  │  ├ FinalScoreText  (TMP)  big number
    ///   │  │  ├ BestScoreLabel  (TMP)
    ///   │  │  ├ BestScoreText   (TMP)
    ///   │  │  └ NewRecordBadge  (GameObject — shown only on new record)
    ///   │  ├ StatsSection
    ///   │  │  ├ LinesText       (TMP)
    ///   │  │  ├ PiecesText      (TMP)
    ///   │  │  ├ MaxComboText    (TMP)
    ///   │  │  └ CardsText       (TMP)
    ///   │  ├ RetryButton        (Button)
    ///   │  └ MainMenuButton     (Button)
    /// </summary>
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
        [SerializeField] private TMP_Text _cardsText;

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
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Shows the Game Over overlay.
        /// Pass in the final score from your ScoreSystem.
        /// Stats are read automatically from RunStatsTracker.Instance.
        /// </summary>
        public void Show(int finalScore)
        {
            // ── Best score ──────────────────────────────────────────────────
            int bestScore  = PlayerPrefs.GetInt(BestScoreKey, 0);
            bool newRecord = finalScore > bestScore;

            if (newRecord)
            {
                bestScore = finalScore;
                PlayerPrefs.SetInt(BestScoreKey, bestScore);
                PlayerPrefs.Save();
            }

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
                _cardsText.text    = $"{s.CardsSelected}";
            }

            // ── Show ─────────────────────────────────────────────────────────
            Time.timeScale = 0f;
            _root.SetActive(true);
            _fadingIn = true;

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        // ── Buttons ──────────────────────────────────────────────────────────
        private void OnRetry()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_gameSceneName);
        }

        private void OnMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_mainMenuSceneName);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// Formats 12480 → "12,480"
        private static string FormatScore(int score) =>
            score.ToString("N0");
    }
}