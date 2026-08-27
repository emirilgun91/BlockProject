using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.FX
{
    /// <summary>
    /// Upgrade kartının mikro-etkileşimleri. Layout / renk / işlevsellik değişmez —
    /// yalnızca üstte ek görsel geri bildirim katmanları çalışır.
    ///
    /// - Hover: hafif ölçek (1.04x) + 3px yukarı kalkma + mavi dış glow
    /// - Idle : çok yavaş, ara sıra geçen metalik parlama süpürmesi
    /// - Satın alma: yumuşak altın flaş + kısa radyal glow
    ///
    /// FX katmanları runtime'da enjekte edilir (bkz. <see cref="UIFXOverlay"/>),
    /// bu yüzden prefab'da hiçbir kurulum gerektirmez.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeSlotFX : MonoBehaviour
    {
        // ── Hover ────────────────────────────────────────────────────────────
        private const float HoverScale     = 1.04f;
        private const float HoverLiftY     = 3f;
        private const float HoverDuration  = 0.16f;
        private const float IconLiftY      = 2.5f;

        // ── Glow ─────────────────────────────────────────────────────────────
        // Palet sarısı (#F0BB4C) — satın alma flash'ıyla aynı aileden.
        private static readonly Color GlowColor = new Color(0.94f, 0.73f, 0.30f);
        private const float GlowAlpha    = 0.42f;
        private const float GlowPadding  = -14f;   // kart rect'inin dışına taşar

        // Hale, slot boyutundan bağımsız olarak %10 küçültülür. Padding'i
        // kısmak yerine ölçek kullanmak, farklı slot ölçülerinde de tam %10 verir.
        private const float GlowScale    = 0.9f;

        // ── Shine ────────────────────────────────────────────────────────────
        private const float ShineInterval = 7f;    // ortalama bekleme (çok yavaş)
        private const float ShineJitter   = 4f;    // rastgelelik — kartlar senkron süpürmesin
        private const float ShineDuration = 0.9f;
        private const float ShineAlpha    = 0.13f;

        // ── Purchase ─────────────────────────────────────────────────────────
        private static readonly Color GoldFlash = new Color(1f, 0.82f, 0.36f);

        // Runtime
        private RectTransform _rect;
        private RectTransform _iconRect;
        private Image         _glow;
        private Image         _shine;
        private Image         _flash;
        private Image         _burst;

        private Vector3   _baseScale;
        private Vector2   _basePos;
        private Vector2   _iconBasePos;
        private Sequence  _shineLoop;
        private bool      _hovered;
        private bool      _restCached;

        // ── Setup ────────────────────────────────────────────────────────────

        /// <summary>
        /// <paramref name="iconRect"/> hover'da hafifçe yüzen ikon (opsiyonel).
        /// </summary>
        public void Initialize(RectTransform iconRect)
        {
            _rect      = (RectTransform)transform;
            _iconRect  = iconRect;
            _baseScale = _rect.localScale;

            // Dinlenme pozisyonları ilk hover'da okunur — layout (GridLayoutGroup)
            // Awake sırasında kartı henüz yerleştirmemiş olabilir.
            BuildLayers();
            StartShineLoop();
        }

        private void BuildLayers()
        {
            // Dış mavi glow — kartın dışına taşar, içi boş halo olduğu için kartı yıkamaz.
            _glow = UIFXOverlay.CreateStretched(_rect, "FX_HoverGlow", UIFXSprites.OuterGlow, GlowPadding);
            _glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0f);
            _glow.rectTransform.localScale = Vector3.one * GlowScale;
            _glow.transform.SetAsFirstSibling();

            // Metalik parlama — kart sınırları içinde kırpılır.
            var shineMask = UIFXOverlay.CreateMaskedContainer(_rect, "FX_ShineMask");
            _shine = UIFXOverlay.CreateSized(shineMask, "FX_Shine", UIFXSprites.ShineBand, new Vector2(40f, 400f));
            _shine.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            _shine.color = new Color(1f, 1f, 1f, 0f);

            // Satın alma flaşı ve radyal patlama.
            _flash = UIFXOverlay.CreateStretched(_rect, "FX_PurchaseFlash", UIFXSprites.RadialGlow);
            _flash.color = new Color(GoldFlash.r, GoldFlash.g, GoldFlash.b, 0f);

            _burst = UIFXOverlay.CreateStretched(_rect, "FX_PurchaseBurst", UIFXSprites.OuterGlow, -8f);
            _burst.color = new Color(GoldFlash.r, GoldFlash.g, GoldFlash.b, 0f);
        }

        // ── Hover ────────────────────────────────────────────────────────────

        public void OnHoverEnter()
        {
            if (_rect == null || _hovered) return;
            _hovered = true;

            CacheRestPositions();

            _rect.DOKill();
            _rect.DOScale(_baseScale * HoverScale, HoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            _rect.DOAnchorPosY(_basePos.y + HoverLiftY, HoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);

            if (_glow != null)
            {
                _glow.DOKill();
                _glow.DOFade(GlowAlpha, HoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            if (_iconRect != null)
            {
                _iconRect.DOKill();
                _iconRect.DOAnchorPosY(_iconBasePos.y + IconLiftY, HoverDuration)
                         .SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        public void OnHoverExit()
        {
            if (_rect == null || !_hovered) return;
            _hovered = false;

            _rect.DOKill();
            _rect.DOScale(_baseScale, HoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            _rect.DOAnchorPosY(_basePos.y, HoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);

            if (_glow != null)
            {
                _glow.DOKill();
                _glow.DOFade(0f, HoverDuration * 1.5f).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            if (_iconRect != null)
            {
                _iconRect.DOKill();
                _iconRect.DOAnchorPosY(_iconBasePos.y, HoverDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        /// <summary>Dinlenme pozisyonlarını ilk hover'da (layout oturduktan sonra) okur.</summary>
        private void CacheRestPositions()
        {
            if (_restCached) return;
            _restCached = true;

            _basePos = _rect.anchoredPosition;
            if (_iconRect != null)
                _iconBasePos = _iconRect.anchoredPosition;
        }

        // ── Shine ────────────────────────────────────────────────────────────

        private void StartShineLoop()
        {
            if (_shine == null) return;

            _shineLoop?.Kill();
            _shineLoop = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

            // Kartlar aynı anda parlamasın diye rastgele bir faz kaydırması.
            _shineLoop.AppendInterval(Random.Range(0f, ShineInterval));
            _shineLoop.AppendCallback(() =>
            {
                // Genişliği süpürme anında oku — layout Awake'te henüz oturmamış olabilir.
                float width = Mathf.Max(_rect.rect.width, 120f);
                float from  = -width * 0.75f;
                float to    =  width * 0.75f;

                var rect = (RectTransform)_shine.transform;
                rect.anchoredPosition = new Vector2(from, 0f);
                rect.DOKill();
                _shine.DOKill();

                rect.DOAnchorPosX(to, ShineDuration).SetEase(Ease.InOutSine).SetUpdate(true);
                _shine.DOFade(ShineAlpha, ShineDuration * 0.35f).SetUpdate(true)
                      .OnComplete(() => _shine.DOFade(0f, ShineDuration * 0.65f).SetUpdate(true));
            });
            _shineLoop.AppendInterval(ShineDuration);
            _shineLoop.AppendInterval(ShineInterval + Random.Range(0f, ShineJitter));
            _shineLoop.SetLoops(-1);
        }

        // ── Purchase ─────────────────────────────────────────────────────────

        /// <summary>Satın alma sonrası kısa altın flaş + radyal glow.</summary>
        public void PlayPurchaseFeedback()
        {
            if (_flash == null || _burst == null) return;

            _flash.DOKill();
            _flash.color = new Color(GoldFlash.r, GoldFlash.g, GoldFlash.b, 0f);
            _flash.DOFade(0.30f, 0.09f).SetEase(Ease.OutQuad).SetUpdate(true)
                  .OnComplete(() => _flash.DOFade(0f, 0.35f).SetEase(Ease.InQuad).SetUpdate(true));

            var burstRect = (RectTransform)_burst.transform;
            burstRect.DOKill();
            _burst.DOKill();
            burstRect.localScale = Vector3.one * 0.9f;
            _burst.color = new Color(GoldFlash.r, GoldFlash.g, GoldFlash.b, 0.55f);

            burstRect.DOScale(1.22f, 0.42f).SetEase(Ease.OutCubic).SetUpdate(true);
            _burst.DOFade(0f, 0.42f).SetEase(Ease.OutQuad).SetUpdate(true);

            // Kartın kendisine çok hafif bir "nefes" — abartısız.
            _rect.DOKill();
            _rect.DOScale(_baseScale * (_hovered ? HoverScale + 0.02f : 1.03f), 0.08f)
                 .SetEase(Ease.OutQuad).SetUpdate(true)
                 .OnComplete(() => _rect.DOScale(_hovered ? _baseScale * HoverScale : _baseScale, 0.22f)
                                        .SetEase(Ease.OutBack).SetUpdate(true));
        }

        // ── Unity ────────────────────────────────────────────────────────────

        private void OnDisable()
        {
            _shineLoop?.Kill();
            _shineLoop = null;

            if (_rect != null)
            {
                _rect.DOKill();
                _rect.localScale = _baseScale;
                if (_restCached) _rect.anchoredPosition = _basePos;
            }

            if (_iconRect != null)
            {
                _iconRect.DOKill();
                if (_restCached) _iconRect.anchoredPosition = _iconBasePos;
            }

            if (_glow  != null) { _glow.DOKill();  _glow.color  = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0f); }
            if (_shine != null) { _shine.DOKill(); _shine.color = new Color(1f, 1f, 1f, 0f); }
            if (_flash != null) { _flash.DOKill(); _flash.color = new Color(GoldFlash.r, GoldFlash.g, GoldFlash.b, 0f); }
            if (_burst != null) { _burst.DOKill(); _burst.color = new Color(GoldFlash.r, GoldFlash.g, GoldFlash.b, 0f); }

            _hovered = false;
        }

        private void OnEnable()
        {
            if (_shine != null && _shineLoop == null)
                StartShineLoop();
        }
    }
}
