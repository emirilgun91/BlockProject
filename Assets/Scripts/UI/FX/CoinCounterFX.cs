using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.FX
{
    /// <summary>
    /// Coin sayacı için yumuşak sayı artışı ve altın parlama süpürmesi.
    ///
    /// - Değer değişince sayı anında değil, kısa bir count-up ile ilerler
    /// - Değer <b>arttığında</b> sayacın üzerinden kısa bir altın shine geçer
    /// - Değer azaldığında (harcama) sadece sayı yumuşakça iner, shine oynamaz
    ///
    /// Sayacın TMP_Text'ine ekle veya <see cref="Attach"/> ile runtime'da bağla.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoinCounterFX : MonoBehaviour
    {
        [Header("Count-up")]
        [SerializeField] private float  _countDuration = 0.45f;
        [SerializeField] private string _numberFormat  = "N0";

        [Header("Shine")]
        [SerializeField] private Color _shineColor    = new Color(1f, 0.85f, 0.45f);
        [SerializeField] private float _shineAlpha    = 0.5f;
        [SerializeField] private float _shineDuration = 0.55f;

        private TMP_Text      _label;
        private RectTransform _shineRect;
        private Image         _shine;

        private int   _displayed;
        private float _tweenValue;
        private Tween _countTween;
        private bool  _initialized;

        /// <summary>Runtime'da bir TMP_Text'e FX iliştirir.</summary>
        public static CoinCounterFX Attach(TMP_Text label)
        {
            if (label == null) return null;
            return label.GetComponent<CoinCounterFX>() ?? label.gameObject.AddComponent<CoinCounterFX>();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Animasyonsuz kurulum — panel açılışında mevcut bakiyeyi yazar.</summary>
        public void SetImmediate(int value)
        {
            EnsureSetup();
            _countTween?.Kill();
            _displayed  = value;
            _tweenValue = value;
            Write(value);
            _initialized = true;
        }

        /// <summary>Yeni bakiyeye yumuşakça sayar; artışta altın shine oynatır.</summary>
        public void SetValue(int value)
        {
            EnsureSetup();

            if (!_initialized) { SetImmediate(value); return; }
            if (value == _displayed) return;

            bool increased = value > _displayed;
            int  from      = _displayed;
            _displayed     = value;

            _countTween?.Kill();
            _tweenValue = from;
            _countTween = DOTween.To(() => _tweenValue, v =>
                          {
                              _tweenValue = v;
                              Write(Mathf.RoundToInt(v));
                          }, value, _countDuration)
                          .SetEase(Ease.OutCubic)
                          .SetUpdate(true)
                          .SetLink(gameObject)
                          .OnComplete(() => Write(value));

            if (increased) PlayShine();
        }

        // ── Internals ────────────────────────────────────────────────────────

        private void EnsureSetup()
        {
            if (_label != null) return;

            _label = GetComponent<TMP_Text>();

            var host = (RectTransform)transform;
            var mask = UIFXOverlay.CreateMaskedContainer(host, "FX_CoinShineMask");

            float height = Mathf.Max(host.rect.height, 24f) * 2f;
            _shine     = UIFXOverlay.CreateSized(mask, "FX_CoinShine", UIFXSprites.ShineBand, new Vector2(26f, height));
            _shineRect = (RectTransform)_shine.transform;
            _shineRect.localRotation = Quaternion.Euler(0f, 0f, 18f);
            _shine.color = new Color(_shineColor.r, _shineColor.g, _shineColor.b, 0f);
        }

        private void PlayShine()
        {
            if (_shine == null) return;

            var host  = (RectTransform)transform;
            float w   = Mathf.Max(host.rect.width, 80f);
            float from = -w * 0.7f;
            float to   =  w * 0.7f;

            _shineRect.DOKill();
            _shine.DOKill();

            _shineRect.anchoredPosition = new Vector2(from, 0f);
            _shine.color = new Color(_shineColor.r, _shineColor.g, _shineColor.b, _shineAlpha);

            _shineRect.DOAnchorPosX(to, _shineDuration).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(gameObject);
            _shine.DOFade(0f, _shineDuration).SetEase(Ease.InQuad).SetUpdate(true).SetLink(gameObject);
        }

        private void Write(int value)
        {
            if (_label != null) _label.text = value.ToString(_numberFormat);
        }

        private void OnDisable()
        {
            _countTween?.Kill();
            _countTween = null;
            _initialized = false;

            if (_shine != null)
            {
                _shine.DOKill();
                _shineRect.DOKill();
                _shine.color = new Color(_shineColor.r, _shineColor.g, _shineColor.b, 0f);
            }
        }
    }
}
