using DG.Tweening;
using TMPro;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Place FX")]
        [SerializeField] private float _placeScalePunch  = 0.28f;
        [SerializeField] private float _placeDuration    = 0.18f;
        [SerializeField] private int   _placeVibrato     = 1;

        [Header("Clear FX")]
        [SerializeField] private float _clearScaleUp     = 1.18f;
        [SerializeField] private float _clearFadeOut     = 0f;
        [SerializeField] private float _clearDuration    = 0.22f;

        [Header("Restore")]
        [SerializeField] private float _restoreDelay     = 0.22f;

        [Header("Debug")]
        [SerializeField] private TMP_Text _scoreText;  // TilePrefab altındaki ScoreText

        // ── Private ──────────────────────────────────────────────────────────
        private SpriteRenderer _sr;
        private Vector3        _baseScale;
        private Tweener        _colorTween;
        private Sequence       _fxSequence;

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            // Fallback — Build() Init'i çağırmadıysa buradan yakala
            if (_baseScale == Vector3.zero)
                _baseScale = transform.localScale;
        }

        /// <summary>
        /// BoardView.Build() tile'ı oluşturduktan hemen sonra bu metodu çağırır.
        /// Scale o an ne ise onu base olarak kilitler.
        /// </summary>
        public void Init()
        {
            _baseScale = transform.localScale;
        }

        private void OnDestroy()
        {
            _fxSequence?.Kill();
            _colorTween?.Kill();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Rengi doğrudan ata (animasyon yok).</summary>
        public void SetColor(Color color)
        {
            _colorTween?.Kill();
            _sr.color = color;

            // Boş hücreye dönünce score text'i temizle
            if (_scoreText != null)
                _scoreText.text = string.Empty;
        }

        /// <summary>
        /// Tile value'yu gösterir.
        /// BoardView.Render() içinde dolu tile'lara çağrılır.
        /// </summary>
        public void SetTileValue(float value)
        {
            if (_scoreText == null) return;
            _scoreText.text = value > 0f ? value.ToString("0") : string.Empty;
        }

        /// <summary>
        /// Yerleştirme FX — scale punch.
        /// Renk animasyonu yok — Render() her frame rengi yönetiyor, çakışma olmasın.
        /// </summary>
        public void PlayPlaceFX()
        {
            _fxSequence?.Kill();
            Vector3 current = _baseScale != Vector3.zero ? _baseScale : transform.localScale;

            _fxSequence = DOTween.Sequence()
                .Append(transform
                    .DOScale(current * (1f + _placeScalePunch), _placeDuration * 0.35f)
                    .SetEase(Ease.OutQuad))
                .Append(transform
                    .DOScale(current, _placeDuration * 0.65f)
                    .SetEase(Ease.OutBack))
                .SetAutoKill(true)
                .OnKill(() => transform.localScale = current);
        }

        public void PlayRippleFX(float strength = 0.07f, float duration = 0.18f)
        {
            if (_fxSequence != null && _fxSequence.IsActive()) return;
            Vector3 current = _baseScale != Vector3.zero ? _baseScale : transform.localScale;

            DOTween.Sequence()
                .Append(transform
                    .DOScale(current * (1f + strength), duration * 0.4f)
                    .SetEase(Ease.OutQuad))
                .Append(transform
                    .DOScale(current, duration * 0.6f)
                    .SetEase(Ease.OutQuad))
                .SetAutoKill(true)
                .OnKill(() => transform.localScale = current);
        }

        /// <summary>
        /// Line clear FX — temizlenen hücreler üzerinde çalışır.
        /// Önce hafif büyür, sonra scale+alpha ile solar.
        /// delay: dalga efekti için dışarıdan verilir (x veya y index * offset).
        /// </summary>
        /// <param name="delay">Dalga efekti için gecikme.</param>
        /// <param name="flashColor">Parlama rengi — null ise tile'ın mevcut rengi kullanılır.</param>
        /// <param name="emptyColor">Animasyon sonunda dönülecek boş hücre rengi.</param>
        public void PlayClearFX(float delay = 0f, Color? flashColor = null, Color? emptyColor = null)
        {
            _fxSequence?.Kill();

            Color tileColor  = _sr.color;                        // yerleştirilen parçanın rengi
            Color flash      = flashColor ?? tileColor;          // flash → parça rengi veya override
            Color restoreTo  = emptyColor ?? new Color(0x1c / 255f, 0x21 / 255f, 0x32 / 255f, 1f);

            _fxSequence = DOTween.Sequence();
            _fxSequence
                // 1. flash rengi — parlama
                .AppendCallback(() => _sr.color = flash)
                // 2. büyü
                .Append(transform
                    .DOScale(_baseScale * _clearScaleUp, _clearDuration * 0.35f)
                    .SetEase(Ease.OutCubic))
                // 3. parça rengine geri dön + küçül
                .Append(DOTween.Sequence()
                    .Join(_sr.DOColor(tileColor, _clearDuration * 0.30f)
                        .SetEase(Ease.InQuad))
                    .Join(transform
                        .DOScale(_baseScale * 0.85f, _clearDuration * 0.30f)
                        .SetEase(Ease.InQuad)))
                // 4. boş renge fade + scale normale dön
                .Append(DOTween.Sequence()
                    .Join(_sr.DOColor(restoreTo, _clearDuration * 0.35f)
                        .SetEase(Ease.OutQuad))
                    .Join(transform
                        .DOScale(_baseScale, _clearDuration * 0.35f)
                        .SetEase(Ease.OutBack)))
                .SetDelay(delay)
                .SetAutoKill(true)
                .OnKill(() =>
                {
                    transform.localScale = _baseScale;
                    _sr.color = restoreTo;
                })
                .SetUpdate(false);
        }

        /// <summary>Clear FX sonrası hücreyi boş renge döndürür.</summary>
        public void RestoreEmpty(Color emptyColor)
        {
            _fxSequence?.Kill();
            transform.localScale = _baseScale;

            _colorTween?.Kill();
            _colorTween = _sr
                .DOColor(emptyColor, 0.12f)
                .SetEase(Ease.OutQuad);
        }
    }
}