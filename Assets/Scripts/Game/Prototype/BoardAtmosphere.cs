using RogueBlockBlast.Core;
using RogueBlockBlast.Core.Settings;
using RogueBlockBlast.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RogueBlockBlast.Game.Prototype
{
    /// <summary>
    /// Sahnenin tamamını oyun durumuna bağlayan tek nokta.
    ///
    /// Fikir şu: atmosfer parlaklık değil <b>tepki</b>. Karanlık bir sahne,
    /// oyuncuya cevap veriyorsa canlıdır. Bu bileşen komboyu tek bir "ısı"
    /// değerine indirger ve o ısıyla anahtar ışığı, bloom'u, vignette'i ve
    /// tahtanın boş hücrelerini birlikte sürer — kombo yükseldikçe oda ısınır.
    ///
    /// Taban değerler <see cref="Start"/>'ta sahnenin kendisinden okunur;
    /// sabit sayı gömülmez. Böylece ışık/post-fx ayarlarını elle değiştirmek
    /// bu bileşeni bozmaz, yeni taban olur.
    ///
    /// <see cref="GameSettings.ReduceMotion"/> açıkken nabız durur ve ısı
    /// yalnızca renk olarak uygulanır; <see cref="GameSettings.VfxIntensity"/>
    /// tüm etkiyi ölçekler.
    /// </summary>
    public sealed class BoardAtmosphere : MonoBehaviour
    {
        [Header("Bağlantılar")]
        [SerializeField] private RunController _runController;
        [SerializeField] private BoardView     _boardView;
        [SerializeField] private Light2D       _keyLight;
        [SerializeField] private Volume        _volume;

        [Header("Isı eğrisi")]
        [Tooltip("Çarpanın ısıya katkısının yumuşaklığı. Küçük = çarpan hızla etki eder.\n\n" +
                 "ComboSystem'de çarpanın tavanı YOK — her temizlemede sınırsız büyüyor. " +
                 "Bu yüzden sabit bir 'tam ısı çarpanı' tanımlanamaz: doyuma ulaşmayan " +
                 "üstel bir eğri kullanılır, böylece üst uçta da ifade kalır.")]
        [SerializeField] private float _multiplierSoftness = 1.6f;

        [Tooltip("Isının hedefe yaklaşma hızı. Düşük = daha tembel, ağır bir geçiş.")]
        [SerializeField] private float _heatDamping = 3.5f;

        [Header("Anahtar ışık")]
        [Tooltip("Isı 1'de ışığın alacağı renk. Taban renk sahneden okunur.")]
        [SerializeField] private Color _hotLightColor = new Color(1f, 0.45f, 0.28f, 1f);

        [Tooltip("Isı 1'de ışık şiddetinin taban değere oranı.")]
        [SerializeField] private float _hotIntensityFactor = 1.55f;

        [Header("Post-processing")]
        [SerializeField] private float _hotBloomFactor    = 1.7f;
        [SerializeField] private float _hotVignetteAdd    = 0.10f;

        [Header("Boş hücre nabzı")]
        [Tooltip("Isı 0'da dalga şiddeti — tahta hiç ölmesin diye sıfır değil.")]
        [SerializeField] private float _pulseAmountCold = 0.16f;
        [SerializeField] private float _pulseAmountHot  = 0.50f;
        [SerializeField] private float _pulseSpeedCold  = 0.8f;
        [SerializeField] private float _pulseSpeedHot   = 2.2f;

        [SerializeField] private Color _pulseColorCold = new Color32(0x2a, 0x35, 0x52, 0xff);
        [SerializeField] private Color _pulseColorHot  = new Color32(0x4a, 0x33, 0x3c, 0xff);

        // ── Taban değerler (sahneden okunur) ─────────────────────────────────
        private Color _baseLightColor;
        private float _baseLightIntensity;
        private float _baseBloomIntensity;
        private float _baseVignetteIntensity;

        private Bloom    _bloom;
        private Vignette _vignette;

        private float _heat;
        private float _heatTarget;
        private bool  _bound;

        /// <summary>
        /// Yumuşatılmış kombo ısısı, 0..1. Arka plan gibi diğer sahne
        /// bileşenleri komboya ayrı ayrı abone olmak yerine bunu okur —
        /// tek sinyal, tek yumuşatma.
        /// </summary>
        public float Heat => _heat;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Start()
        {
            if (_runController == null) _runController = FindFirstObjectByType<RunController>();
            if (_boardView     == null) _boardView     = FindFirstObjectByType<BoardView>();
            if (_volume        == null) _volume        = FindFirstObjectByType<Volume>();

            CaptureBaseline();
            Bind();
        }

        private void OnDestroy()
        {
            if (_bound && _runController != null && _runController.Combo != null)
                _runController.Combo.OnStateChanged -= HandleComboChanged;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _heat = Mathf.Lerp(_heat, _heatTarget, 1f - Mathf.Exp(-_heatDamping * dt));

            float vfx = Mathf.Clamp01(GameSettings.VfxIntensity);
            Apply(_heat * vfx);
        }

        // ── Kurulum ──────────────────────────────────────────────────────────

        private void Bind()
        {
            if (_runController == null)
            {
                Debug.LogWarning("[Atmosphere] RunController bulunamadı — atmosfer komboya bağlanamadı.");
                return;
            }

            var combo = _runController.Combo;
            if (combo == null)
            {
                Debug.LogWarning("[Atmosphere] ComboSystem null — atmosfer bağlanamadı.");
                return;
            }

            combo.OnStateChanged += HandleComboChanged;
            _bound = true;
        }

        /// <summary>
        /// Taban değerleri sahnenin mevcut hâlinden okur. Sabit sayı gömmek,
        /// ışık veya post-fx ayarını elle değiştirdiğinizde bu bileşenin onu
        /// ezmesine yol açardı.
        /// </summary>
        private void CaptureBaseline()
        {
            if (_keyLight != null)
            {
                _baseLightColor     = _keyLight.color;
                _baseLightIntensity = _keyLight.intensity;
            }

            if (_volume == null || _volume.profile == null) return;

            // .profile getter'ı asset'in runtime kopyasını verir — asset
            // kirlenmez, editörde ayarlar bozulmaz.
            if (_volume.profile.TryGet(out _bloom))
                _baseBloomIntensity = _bloom.intensity.value;

            if (_volume.profile.TryGet(out _vignette))
                _baseVignetteIntensity = _vignette.intensity.value;
        }

        // ── Isı ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Kombo durumunu tek bir 0..1 "ısı" değerine indirger.
        ///
        /// İki sinyal birleştirilir:
        ///
        /// <b>Şarj doluluğu</b> — kısa vadeli, her temizlemede oynar.
        /// <c>Charges / MaxCharge</c> olarak hesaplanır, yani combo barı 3 de
        /// olsa 5 de olsa (<c>upgrade_combo_bar</c>) kendini ayarlar. 5 barlı
        /// kurulumda tam ısıya ulaşmak daha çok temizleme ister — doğrusu bu.
        ///
        /// <b>Çarpan</b> — uzun vadeli, run'ın gidişatı. Tavanı olmadığı için
        /// doyuma ulaşmayan üstel bir eğriyle 0..1'e taşınır; 1'e asla tam
        /// varmaz, böylece çarpan büyüdükçe biraz daha ısı gelmeye devam eder.
        ///
        /// Büyük olan kazanır. Bunun yan faydası: oyuncu şarjı kaybettiğinde
        /// (temizlemesiz yerleştirme) ısı tamamen çökmez, çünkü çarpan durur —
        /// "hâlâ iyi bir run'dasın" bilgisi ekranda kalır.
        /// </summary>
        private void HandleComboChanged(ComboState state)
        {
            float chargeRatio = state.MaxCharge > 0
                ? Mathf.Clamp01((float)state.Charges / state.MaxCharge)
                : 0f;

            float over      = Mathf.Max(0f, state.Multiplier - 1f);
            float softness  = Mathf.Max(0.05f, _multiplierSoftness);
            float multRatio = 1f - Mathf.Exp(-over / softness);

            _heatTarget = Mathf.Clamp01(Mathf.Max(chargeRatio, multRatio));
        }

        // ── Uygulama ─────────────────────────────────────────────────────────

        private void Apply(float heat)
        {
            bool reduceMotion = GameSettings.ReduceMotion;

            if (_keyLight != null)
            {
                _keyLight.color     = Color.Lerp(_baseLightColor, _hotLightColor, heat);
                _keyLight.intensity = Mathf.Lerp(
                    _baseLightIntensity, _baseLightIntensity * _hotIntensityFactor, heat);
            }

            if (_bloom != null)
                _bloom.intensity.value = Mathf.Lerp(
                    _baseBloomIntensity, _baseBloomIntensity * _hotBloomFactor, heat);

            if (_vignette != null)
                _vignette.intensity.value = Mathf.Lerp(
                    _baseVignetteIntensity, _baseVignetteIntensity + _hotVignetteAdd, heat);

            if (_boardView != null)
            {
                _boardView.EmptyPulseColor = Color.Lerp(_pulseColorCold, _pulseColorHot, heat);

                // ReduceMotion: dalga durur ama renk kayması kalır — bilgi
                // kaybolmasın, yalnızca hareket kalksın.
                _boardView.EmptyPulseAmount = reduceMotion
                    ? Mathf.Lerp(_pulseAmountCold, _pulseAmountHot, heat) * 0.5f
                    : Mathf.Lerp(_pulseAmountCold, _pulseAmountHot, heat);

                _boardView.EmptyPulseSpeed = reduceMotion
                    ? 0f
                    : Mathf.Lerp(_pulseSpeedCold, _pulseSpeedHot, heat);
            }
        }
    }
}
