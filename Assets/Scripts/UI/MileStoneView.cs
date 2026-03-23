using DG.Tweening;
using RogueBlockBlast.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Milestone progress bar UI.
    ///
    /// Hierarchy (MilestoneBoard altında):
    ///  MilestoneBoard
    ///   ├── Label          (TMP — "NEXT CARD")
    ///   ├── ProgressBar
    ///   │    ├── BG        (Image — arka plan)
    ///   │    └── Fill      (Image — dolan kısım)
    ///   ├── ThresholdText  (TMP — "15000")
    ///   └── PiecesText     (TMP — "28 left")  ← opsiyonel
    ///
    /// RunController'dan:
    ///   milestoneView.Bind(milestoneSystem);
    /// </summary>
    public sealed class MilestoneView : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Progress Bar")]
        [SerializeField] private Image    _fill;
        [SerializeField] private float    _fillDuration = 0.3f;

        [Header("Text")]
        [SerializeField] private TMP_Text _thresholdText;
        [SerializeField] private TMP_Text _piecesText;       // opsiyonel

        [Header("Colors")]
        [SerializeField] private Color _colorNormal  = new Color(0.91f, 0.64f, 0.19f, 1f); // amber
        [SerializeField] private Color _colorDanger  = new Color(0.75f, 0.24f, 0.17f, 1f); // kırmızı — az kaldı
        [SerializeField] private Color _colorComplete = new Color(0.08f, 0.72f, 0.60f, 1f); // teal — milestone!

        [Header("Danger Threshold")]
        [Tooltip("Pool'un yüzde kaçı dolunca renk kırmızıya döner.")]
        [Range(0f, 1f)]
        [SerializeField] private float _dangerRatio = 0.80f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private MilestoneSystem _system;
        private Sequence        _milestoneSequence;

        // ── Public API ───────────────────────────────────────────────────────
        public void Bind(MilestoneSystem system)
        {
            if (_system != null)
                _system.OnProgressChanged -= HandleProgressChanged;

            _system = system;
            _system.OnProgressChanged += HandleProgressChanged;
        }

        private void OnDestroy()
        {
            if (_system != null)
                _system.OnProgressChanged -= HandleProgressChanged;

            _milestoneSequence?.Kill();
        }

        // ── Handler ──────────────────────────────────────────────────────────
        private void HandleProgressChanged(MilestoneProgressState state)
        {
            UpdateFill(state);
            UpdateTexts(state);
        }

        // ── Fill ─────────────────────────────────────────────────────────────
        private void UpdateFill(MilestoneProgressState state)
        {
            if (_fill == null) return;

            float targetFill = state.PoolFillRatio;

            // Renk — normal / danger / milestone tamamlandı
            Color targetColor;
            if (state.AllCleared)
                targetColor = _colorComplete;
            else if (targetFill >= _dangerRatio)
                targetColor = _colorDanger;
            else
                targetColor = _colorNormal;

            // Fill amount smooth
            _fill.DOFillAmount(targetFill, _fillDuration).SetEase(Ease.OutQuad);
            _fill.DOColor(targetColor, _fillDuration * 0.5f);
        }

        // ── Texts ─────────────────────────────────────────────────────────────
        private void UpdateTexts(MilestoneProgressState state)
        {
            if (_thresholdText != null)
            {
                _thresholdText.text = state.AllCleared
                    ? "MAX"
                    : state.NextThreshold.ToString("N0");
            }

            if (_piecesText != null)
            {
                _piecesText.text = state.AllCleared
                    ? string.Empty
                    : $"{state.PiecesRemaining} left";
            }
        }

        /// <summary>
        /// Milestone'a ulaşıldığında dışarıdan çağrılır — kutlama animasyonu.
        /// </summary>
        public void PlayMilestoneReachedFX()
        {
            if (_fill == null) return;

            _milestoneSequence?.Kill();
            _milestoneSequence = DOTween.Sequence()
                // Teal flash
                .Append(_fill.DOColor(_colorComplete, 0.15f))
                // Fill sıfıra çek (yeni pencere başlıyor)
                .AppendInterval(0.3f)
                .Append(_fill.DOFillAmount(0f, 0.25f).SetEase(Ease.InQuad))
                // Renge geri dön
                .Append(_fill.DOColor(_colorNormal, 0.2f))
                .SetAutoKill(true);
        }
    }
}