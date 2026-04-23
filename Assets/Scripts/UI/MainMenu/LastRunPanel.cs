using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Son run özetini gösterir — Score, Coins, MaxCombo.
    /// 5 saniye sonra fade out olup PreviewBoard'a geçer.
    ///
    /// PlayerPrefs keyleri:
    ///   LastRun_HasData  (int, 0/1)
    ///   LastRun_Score    (int)
    ///   LastRun_Coins    (int)
    ///   LastRun_MaxCombo (float)
    ///
    /// Hierarchy:
    ///  LastRunPanel (bu script + CanvasGroup)
    ///   ├── TitleText     (TMP — "LAST RUN")
    ///   ├── ScoreValue    (TMP — büyük skor)
    ///   ├── StatsRow
    ///   │    ├── CoinsBlock → CoinsValue + CoinsLabel
    ///   │    └── ComboBlock → ComboValue + ComboLabel
    ///   └── CountdownText (TMP)
    /// </summary>
    public sealed class LastRunPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text    _scoreValueText;
        [SerializeField] private TMP_Text    _coinsValueText;
        [SerializeField] private TMP_Text    _comboValueText;
        [SerializeField] private TMP_Text    _countdownText;

        [Header("Preview Board (sonra aktif olacak)")]
        [SerializeField] private GameObject _previewBoard;

        [Header("Timing")]
        [SerializeField] private float _showDuration = 5f;
        [SerializeField] private float _fadeInTime   = 0.4f;
        [SerializeField] private float _fadeOutTime  = 0.4f;

        // PlayerPrefs keys
        private const string KeyHasData  = "LastRun_HasData";
        private const string KeyScore    = "LastRun_Score";
        private const string KeyCoins    = "LastRun_Coins";
        private const string KeyMaxCombo = "LastRun_MaxCombo";

        private void Start()
        {
            if (PlayerPrefs.GetInt(KeyHasData, 0) == 0)
            {
                HidePanelImmediate();
                ShowPreviewBoard();
                return;
            }

            PopulateTexts();
            StartCoroutine(ShowSequence());
        }

        private void PopulateTexts()
        {
            int   score = PlayerPrefs.GetInt(KeyScore, 0);
            int   coins = PlayerPrefs.GetInt(KeyCoins, 0);
            float combo = PlayerPrefs.GetFloat(KeyMaxCombo, 1f);

            if (_scoreValueText != null) _scoreValueText.text = score.ToString("N0");
            if (_coinsValueText != null) _coinsValueText.text = coins.ToString("N0");
            if (_comboValueText != null) _comboValueText.text = $"x{combo:0.0}";
        }

        private IEnumerator ShowSequence()
        {
            if (_previewBoard != null) _previewBoard.SetActive(false);
            gameObject.SetActive(true);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.DOFade(1f, _fadeInTime).SetUpdate(true);
            }

            float remaining = _showDuration;
            while (remaining > 0f)
            {
                if (_countdownText != null)
                    _countdownText.text = $"{Mathf.CeilToInt(remaining)} Seconds Later...";
                yield return new WaitForSecondsRealtime(0.25f);
                remaining -= 0.25f;
            }

            if (_canvasGroup != null)
                yield return _canvasGroup.DOFade(0f, _fadeOutTime).SetUpdate(true).WaitForCompletion();

            gameObject.SetActive(false);
            ShowPreviewBoard();
        }

        private void HidePanelImmediate()
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void ShowPreviewBoard()
        {
            if (_previewBoard != null) _previewBoard.SetActive(true);
        }

        // ── Static — RunController'dan çağrılır ──────────────────────────────

        /// <summary>Son run verilerini PlayerPrefs'e kaydet.</summary>
        public static void SaveLastRun(int score, int coins, float maxCombo)
        {
            PlayerPrefs.SetInt  (KeyHasData,  1);
            PlayerPrefs.SetInt  (KeyScore,    score);
            PlayerPrefs.SetInt  (KeyCoins,    coins);
            PlayerPrefs.SetFloat(KeyMaxCombo, maxCombo);
            PlayerPrefs.Save();
        }

        /// <summary>Son run verilerini temizle.</summary>
        public static void ClearLastRun()
        {
            PlayerPrefs.DeleteKey(KeyHasData);
            PlayerPrefs.DeleteKey(KeyScore);
            PlayerPrefs.DeleteKey(KeyCoins);
            PlayerPrefs.DeleteKey(KeyMaxCombo);
        }
    }
}