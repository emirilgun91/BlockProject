using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RogueBlockBlast.Core.Settings;

namespace RogueBlockBlast.UI.FX
{
    /// <summary>
    /// Shape shop'ta bir şeklin taban puanı yükseltildiğinde oynatılan efekt.
    ///
    /// Yazı korunur — puan metni zaten <c>"15 -> 18"</c> biçiminde ve
    /// <see cref="ShapeShopCardView.Refresh"/> tarafından yazılıyor. Bu bileşen
    /// ona dokunmaz; üstüne katman ekler:
    ///
    ///   1. Metnin arkasında genişleyip sönen radyal patlama
    ///   2. Metinde altın rengine gidip geri dönen renk flaşı + punch
    ///   3. Yukarı süzülen "+N" yazısı — asıl "kazandım" sinyali
    ///   4. Kartın etrafında kısa bir hale
    ///
    /// <b>Ölçüler sabit piksel değil</b>, puan metninin kendi yüksekliğinden
    /// türetilir. Kart düzeni ya da yazı tipi büyüklüğü değişirse efekt de
    /// onunla birlikte ölçeklenir.
    ///
    /// Sayı sayacı (roll-up) bilinçli olarak yapılmadı: metin tek bir sayı
    /// değil, "mevcut -> sonraki" biçiminde. Sayacı ona uydurmak metnin
    /// biçimini efekte bağımlı kılardı.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShapeScoreUpgradeFX : MonoBehaviour
    {
        // ── Ölçü katsayıları (puan metninin yüksekliğine göre) ───────────────
        private const float BurstSizeFactor  = 3.4f;
        private const float GlowPadding      = -10f;
        private const float FloaterRise      = 1.9f;   // metin yüksekliğinin katı
        private const float FloaterFontRatio = 1.15f;  // puan yazısına göre

        // ── Zamanlama ────────────────────────────────────────────────────────
        private const float BurstDuration   = 0.45f;
        private const float PunchDuration   = 0.40f;
        private const float FloaterDuration = 0.95f;
        private const float GlowDuration    = 0.55f;

        // ── Renkler ──────────────────────────────────────────────────────────
        private static readonly Color Gold  = new Color(1f, 0.82f, 0.36f);
        private static readonly Color Green = new Color(0.13f, 0.87f, 0.40f);

        [Header("Bağlantılar")]
        [Tooltip("Puan değeri yazısı. Efektler buna göre konumlanır ve ölçeklenir.")]
        [SerializeField] private TMP_Text _scoreText;

        [Tooltip("Halenin çizileceği kart kökü. Boşsa bu nesnenin RectTransform'u.")]
        [SerializeField] private RectTransform _cardRect;

        [Header("Ayarlar")]
        [SerializeField] private bool _showCardGlow = true;

        private Image    _burst;
        private Image    _glow;
        private TMP_Text _floater;

        private Color   _scoreBaseColor;
        private Vector3 _scoreBaseScale = Vector3.one;
        private bool    _built;

        // ── Public ───────────────────────────────────────────────────────────

        /// <summary>
        /// Hedefleri koddan bağlar — prefaba elle bileşen eklemek gerekmesin diye.
        /// Zaten kurulmuşsa hiçbir şey yapmaz.
        /// </summary>
        public void Bind(TMP_Text scoreText, RectTransform cardRect)
        {
            if (_built) return;
            _scoreText = scoreText;
            _cardRect  = cardRect;
        }

        /// <summary>
        /// Efekti oynatır. <paramref name="gained"/> bu yükseltmeyle eklenen
        /// puan; 0 veya altıysa "+N" yazısı gösterilmez.
        /// </summary>
        public void Play(float gained)
        {
            if (_scoreText == null) return;

            float vfx = Mathf.Clamp01(GameSettings.VfxIntensity);
            if (vfx <= 0.001f) return;

            EnsureBuilt();

            PlayBurst(vfx);
            PlayTextPunch(vfx);
            if (gained > 0f) PlayFloater(gained, vfx);
            if (_showCardGlow) PlayCardGlow(vfx);
        }

        // ── Kurulum ──────────────────────────────────────────────────────────

        /// <summary>
        /// Katmanlar ilk kullanımda kurulur. Awake'te kurmak güvenilmez:
        /// layout henüz oturmamış olabilir ve metin yüksekliği 0 okunur.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            if (_cardRect == null) _cardRect = transform as RectTransform;

            _scoreBaseColor = _scoreText.color;
            _scoreBaseScale = _scoreText.transform.localScale;

            var textRect = _scoreText.rectTransform;
            var parent   = textRect.parent as RectTransform;
            if (parent == null) return;

            float unit = TextUnit();

            // ── Patlama: metnin arkasında, kardeş sırasında önce ──
            _burst = UIFXOverlay.CreateSized(
                parent, "FX_ScoreBurst", UIFXSprites.RadialGlow,
                Vector2.one * (unit * BurstSizeFactor));

            // Anchor'lar metinden KOPYALANMAZ. Metin stretch anchor kullanıyorsa
            // sizeDelta'nın anlamı değişir ve patlama yanlış boyutta çıkar.
            // Kendi merkez anchor'ında kalıp konumu localPosition ile eşitliyoruz —
            // bu anchor düzeninden bağımsız çalışır.
            var burstRect = _burst.rectTransform;
            burstRect.anchorMin     = new Vector2(0.5f, 0.5f);
            burstRect.anchorMax     = new Vector2(0.5f, 0.5f);
            burstRect.pivot         = new Vector2(0.5f, 0.5f);
            burstRect.localPosition = textRect.localPosition;
            burstRect.SetSiblingIndex(textRect.GetSiblingIndex());

            _burst.color         = new Color(Gold.r, Gold.g, Gold.b, 0f);
            _burst.raycastTarget = false;

            // ── "+N" yazısı: metnin üstünde süzülür ──
            var floaterGo = new GameObject("FX_ScoreFloater", typeof(RectTransform));
            floaterGo.transform.SetParent(parent, worldPositionStays: false);
            floaterGo.transform.SetAsLastSibling();

            var floaterRect = floaterGo.GetComponent<RectTransform>();
            floaterRect.anchorMin     = new Vector2(0.5f, 0.5f);
            floaterRect.anchorMax     = new Vector2(0.5f, 0.5f);
            floaterRect.pivot         = new Vector2(0.5f, 0.5f);
            floaterRect.localPosition = textRect.localPosition;
            // Genişlik metnin gerçek genişliğinden; yükseklik bir satırlık.
            floaterRect.sizeDelta     = new Vector2(
                Mathf.Max(60f, textRect.rect.width), unit * 1.4f);

            floaterGo.AddComponent<LayoutElement>().ignoreLayout = true;

            _floater = floaterGo.AddComponent<TextMeshProUGUI>();
            _floater.font             = _scoreText.font;
            _floater.fontSize         = _scoreText.fontSize * FloaterFontRatio;
            _floater.fontStyle        = FontStyles.Bold;
            _floater.alignment        = TextAlignmentOptions.Center;
            _floater.raycastTarget    = false;
            _floater.textWrappingMode = TextWrappingModes.NoWrap;
            _floater.color            = new Color(Green.r, Green.g, Green.b, 0f);

            // ── Kart halesi ──
            if (_showCardGlow && _cardRect != null)
            {
                _glow = UIFXOverlay.CreateStretched(
                    _cardRect, "FX_ScoreCardGlow", UIFXSprites.OuterGlow, GlowPadding);
                _glow.color         = new Color(Gold.r, Gold.g, Gold.b, 0f);
                _glow.raycastTarget = false;
                _glow.rectTransform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// Tüm ölçülerin dayandığı birim: puan metninin satır yüksekliği.
        /// Rect okunamazsa font boyutuna düşülür.
        /// </summary>
        private float TextUnit()
        {
            float h = _scoreText.rectTransform.rect.height;
            if (h < 1f) h = _scoreText.fontSize * 1.2f;
            return Mathf.Max(8f, h);
        }

        // ── Parçalar ─────────────────────────────────────────────────────────

        private void PlayBurst(float vfx)
        {
            if (_burst == null) return;

            var rect = _burst.rectTransform;
            _burst.DOKill();
            rect.DOKill();

            rect.localScale = Vector3.one * 0.55f;
            _burst.color    = new Color(Gold.r, Gold.g, Gold.b, 0.55f * vfx);

            rect.DOScale(1.25f, BurstDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            _burst.DOFade(0f, BurstDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void PlayTextPunch(float vfx)
        {
            var tr = _scoreText.transform;
            tr.DOKill();
            _scoreText.DOKill();

            tr.localScale = _scoreBaseScale;

            if (!GameSettings.ReduceMotion)
            {
                tr.DOPunchScale(Vector3.one * (0.30f * vfx), PunchDuration, 7, 0.7f)
                  .SetUpdate(true)
                  .OnComplete(() => tr.localScale = _scoreBaseScale);
            }

            // Altına gidip taban rengine dönen flaş.
            _scoreText.color = Gold;
            _scoreText.DOColor(_scoreBaseColor, PunchDuration * 1.4f)
                      .SetEase(Ease.OutCubic)
                      .SetUpdate(true);
        }

        private void PlayFloater(float gained, float vfx)
        {
            if (_floater == null) return;

            var rect = _floater.rectTransform;
            _floater.DOKill();
            rect.DOKill();

            _floater.text  = $"+{gained:0.##}";
            _floater.color = new Color(Green.r, Green.g, Green.b, vfx);

            float   unit  = TextUnit();
            Vector3 start = _scoreText.rectTransform.localPosition;

            rect.localPosition = start;
            rect.localScale    = Vector3.one * 0.75f;

            rect.DOLocalMoveY(start.y + unit * FloaterRise, FloaterDuration)
                .SetEase(Ease.OutCubic).SetUpdate(true);

            rect.DOScale(1f, FloaterDuration * 0.35f)
                .SetEase(Ease.OutBack).SetUpdate(true);

            // Yükselişin sonuna doğru sönsün — hemen kaybolursa okunmuyor.
            _floater.DOFade(0f, FloaterDuration * 0.45f)
                    .SetDelay(FloaterDuration * 0.55f)
                    .SetEase(Ease.InQuad).SetUpdate(true);
        }

        private void PlayCardGlow(float vfx)
        {
            if (_glow == null) return;

            _glow.DOKill();
            _glow.color = new Color(Gold.r, Gold.g, Gold.b, 0.38f * vfx);
            _glow.DOFade(0f, GlowDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void OnDisable()
        {
            // Kart havuzda yeniden kullanılabilir; yarım kalan tween bırakma.
            if (_scoreText != null)
            {
                _scoreText.DOKill();
                _scoreText.transform.DOKill();
                _scoreText.color              = _scoreBaseColor;
                _scoreText.transform.localScale = _scoreBaseScale;
            }

            if (_burst != null)   { _burst.DOKill();   _burst.rectTransform.DOKill(); }
            if (_glow != null)    { _glow.DOKill(); }
            if (_floater != null) { _floater.DOKill(); _floater.rectTransform.DOKill(); }
        }
    }
}
