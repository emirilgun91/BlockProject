using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.FX
{
    /// <summary>
    /// Buy butonuna premium his katan mikro-etkileşimler.
    /// Layout, renk paleti ve tıklama işlevi değişmez.
    ///
    /// - Idle  : yavaş "nefes alan" glow (yalnızca interactable iken)
    /// - Hover : hafif parlaklık artışı + glow yoğunlaşması
    /// - Click : hızlı küçülme, ardından yaylı geri sıçrama
    ///
    /// Butona ekle (veya <see cref="Attach"/> ile runtime'da bağla) — başka kurulum gerekmez.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PremiumButtonFX : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Glow")]
        [SerializeField] private Color _glowColor    = new Color(0.34f, 0.66f, 1f);
        [SerializeField] private float _idleGlowMin  = 0.10f;
        [SerializeField] private float _idleGlowMax  = 0.24f;
        [SerializeField] private float _hoverGlow    = 0.45f;
        [SerializeField] private float _breathPeriod = 1.9f;

        [Header("Hover")]
        [Tooltip("Hover'da grafiğe uygulanan parlaklık çarpanı.")]
        [SerializeField] private float _hoverBrightness = 1.10f;
        [SerializeField] private float _hoverDuration   = 0.14f;

        [Header("Click")]
        [SerializeField] private float _pressScale = 0.95f;

        private Button        _button;
        private Graphic       _graphic;
        private RectTransform _rect;
        private Image         _glow;

        private Vector3  _baseScale;
        private Color    _baseColor;
        private Sequence _breath;
        private bool     _hovered;

        /// <summary>Runtime'da bir butona FX iliştirir (zaten varsa mevcut olanı döndürür).</summary>
        public static PremiumButtonFX Attach(Button button)
        {
            if (button == null) return null;
            return button.GetComponent<PremiumButtonFX>() ?? button.gameObject.AddComponent<PremiumButtonFX>();
        }

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            _button    = GetComponent<Button>();
            _rect      = (RectTransform)transform;
            _graphic   = _button != null && _button.targetGraphic != null
                       ? _button.targetGraphic
                       : GetComponent<Graphic>();

            _baseScale = _rect.localScale;
            _baseColor = _graphic != null ? _graphic.color : Color.white;

            _glow = UIFXOverlay.CreateStretched(_rect, "FX_ButtonGlow", UIFXSprites.OuterGlow, -12f);
            _glow.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, 0f);
            _glow.transform.SetAsFirstSibling();
        }

        private void OnEnable()
        {
            _hovered = false;
            StartBreathing();
        }

        private void OnDisable()
        {
            _breath?.Kill();
            _breath = null;

            _rect.DOKill();
            _rect.localScale = _baseScale;

            if (_graphic != null) { _graphic.DOKill(); _graphic.color = _baseColor; }
            if (_glow    != null) { _glow.DOKill();    _glow.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, 0f); }
        }

        private void Update()
        {
            // Buton devre dışıyken nefes almasın — yanlış bir "tıklanabilir" sinyali vermemeli.
            bool active = _button == null || _button.interactable;
            if (active && _breath == null && !_hovered) StartBreathing();
            else if (!active && _breath != null)
            {
                _breath.Kill();
                _breath = null;
                if (_glow != null) _glow.DOFade(0f, 0.2f).SetUpdate(true);
            }
        }

        // ── Idle breathing ───────────────────────────────────────────────────

        private void StartBreathing()
        {
            if (_glow == null) return;
            if (_button != null && !_button.interactable) return;

            _breath?.Kill();
            _glow.DOKill();
            _glow.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, _idleGlowMin);

            _breath = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _breath.Append(_glow.DOFade(_idleGlowMax, _breathPeriod * 0.5f).SetEase(Ease.InOutSine));
            _breath.Append(_glow.DOFade(_idleGlowMin, _breathPeriod * 0.5f).SetEase(Ease.InOutSine));
            _breath.SetLoops(-1);
        }

        // ── Pointer ──────────────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData e)
        {
            if (_button != null && !_button.interactable) return;
            _hovered = true;

            _breath?.Kill();
            _breath = null;

            if (_glow != null)
            {
                _glow.DOKill();
                _glow.DOFade(_hoverGlow, _hoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            if (_graphic != null)
            {
                _graphic.DOKill();
                _graphic.DOColor(Brighten(_baseColor, _hoverBrightness), _hoverDuration)
                        .SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hovered = false;

            if (_graphic != null)
            {
                _graphic.DOKill();
                _graphic.DOColor(_baseColor, _hoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            _rect.DOKill();
            _rect.DOScale(_baseScale, _hoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);

            StartBreathing();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_button != null && !_button.interactable) return;

            _rect.DOKill();
            _rect.DOScale(_baseScale * _pressScale, 0.07f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData e)
        {
            _rect.DOKill();
            _rect.DOScale(_baseScale, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Color Brighten(Color c, float factor) => new Color(
            Mathf.Clamp01(c.r * factor),
            Mathf.Clamp01(c.g * factor),
            Mathf.Clamp01(c.b * factor),
            c.a);
    }
}
