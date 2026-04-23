using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// PreviewBoard'daki tek hücre.
    ///
    /// 3 durum:
    /// - Empty: koyu, boş hücre
    /// - Filled: renkli blok
    /// - Corner: statik köşe dekorasyonu (hiç değişmez, breathe animasyonu)
    ///
    /// Animasyonlar:
    /// - PopIn:  scale 0 → 1.1 → 1 (overshoot bounce)
    /// - PopOut: scale 1 → 0 + rotate
    /// - Breathe: brightness 1 → 1.25 → 1 (corner cell)
    /// </summary>
    public sealed class PreviewCellView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _image;
        [SerializeField] private Image _highlightOverlay; // üstteki parlak şerit, opsiyonel

        [Header("Colors")]
        [SerializeField] private Color _emptyColor = new Color(0.04f, 0.08f, 0.13f);

        [Header("Animation")]
        [SerializeField] private float _popInDuration  = 0.45f;
        [SerializeField] private float _popOutDuration = 0.30f;
        [SerializeField] private float _breatheDuration = 3.6f;

        // Runtime
        private Tween _currentTween;
        private Tween _breatheTween;
        private bool  _isCornerCell;

        private void Awake()
        {
            if (_image == null) _image = GetComponent<Image>();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Hücreyi boşalt — emptyColor'a döner.</summary>
        public void SetEmpty(bool instant = false)
        {
            KillTween();
            transform.localRotation = Quaternion.identity;

            if (instant)
            {
                transform.localScale = Vector3.one;
                _image.color = _emptyColor;
                if (_highlightOverlay != null) _highlightOverlay.enabled = false;
                return;
            }

            // Kapanış animasyonu yoksa direkt boşalt
            transform.localScale = Vector3.one;
            _image.color = _emptyColor;
            if (_highlightOverlay != null) _highlightOverlay.enabled = false;
        }

        /// <summary>Renkli bloğu pop animasyonuyla yerleştir.</summary>
        public void PopIn(Color color)
        {
            KillTween();
            transform.localRotation = Quaternion.Euler(0, 0, -8f);
            transform.localScale    = Vector3.zero;

            _image.color = color;
            if (_highlightOverlay != null) _highlightOverlay.enabled = true;

            _currentTween = DOTween.Sequence()
                .Append(transform.DOScale(Vector3.one, _popInDuration)
                         .SetEase(Ease.OutBack, 1.8f))
                .Join(transform.DOLocalRotate(Vector3.zero, _popInDuration)
                         .SetEase(Ease.OutBack));
        }

        /// <summary>Bloğu kapat — scale'i küçülterek.</summary>
        public void PopOut()
        {
            KillTween();

            _currentTween = DOTween.Sequence()
                .Append(transform.DOScale(Vector3.zero, _popOutDuration)
                         .SetEase(Ease.InBack))
                .Join(transform.DOLocalRotate(new Vector3(0, 0, 8f), _popOutDuration))
                .OnComplete(() => SetEmpty(instant: true));
        }

        /// <summary>
        /// Köşe dekorasyon hücresi olarak sabitle.
        /// Renk set edilir, asla değiştirilmez, breathe animasyonu başlar.
        /// </summary>
        public void SetAsCorner(Color color)
        {
            _isCornerCell = true;
            KillTween();

            transform.localScale    = Vector3.one;
            transform.localRotation = Quaternion.identity;
            _image.color = color;
            if (_highlightOverlay != null) _highlightOverlay.enabled = true;

            // Breathe — brightness oscillation via color
            _breatheTween = _image
                .DOColor(color * 1.25f, _breatheDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        public bool IsCornerCell => _isCornerCell;

        // ── Private ──────────────────────────────────────────────────────────

        private void KillTween()
        {
            _currentTween?.Kill();
            _currentTween = null;
        }

        private void OnDestroy()
        {
            _currentTween?.Kill();
            _breatheTween?.Kill();
        }
    }
}