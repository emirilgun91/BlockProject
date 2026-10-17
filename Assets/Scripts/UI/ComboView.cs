using DG.Tweening;
using RogueBlockBlast.Core.Localization;
using RogueBlockBlast.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Combo UI — 3 charge bar + multiplier text.
    ///
    /// Hierarchy:
    ///  ComboBoard
    ///   ├── ComboText          (TMP — "COMBO")
    ///   ├── ComboNumText       (TMP — "x1.3")
    ///   ├── ChargeContainer
    ///   │    ├── Charge_1      (Image)
    ///   │    ├── Charge_2      (Image)
    ///   │    └── Charge_3      (Image)
    ///   └── ComboX             (GameObject — opsiyonel dekoratif)
    ///
    /// Bind() metodunu RunController'dan çağır:
    ///   comboView.Bind(comboSystem);
    /// </summary>
    public sealed class ComboView : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Charge Bars")]
        [SerializeField] private Image[] _chargeBars;

        [Header("Reset FX")]
        [SerializeField] private RectTransform _comboPanel;    // ComboBoard RectTransform — shake için
        [SerializeField] private Color _colorResetFlash = new Color(0.85f, 0.22f, 0.22f, 1f); // kırmızı
        [SerializeField] private Color _colorShieldFlash   = new Color(0.35f, 0.85f, 1.00f, 1f); // kalkan: camgöbeği
        [SerializeField] private Color _colorSoftLandFlash = new Color(0.45f, 0.70f, 1.00f, 1f); // fren: mavi

        [Header("Text")]
        [SerializeField] private TMP_Text _multiplierText;

        [Header("Colors")]
        [SerializeField] private Color _colorEmpty   = new Color(0.25f, 0.29f, 0.40f, 1f); // #404966
        [SerializeField] private Color _colorFilled  = new Color(0.91f, 0.64f, 0.19f, 1f); // #E8A230
        [SerializeField] private Color _colorMaxGlow = new Color(1.00f, 0.80f, 0.20f, 1f); // parlak amber

        [Header("Animation")]
        [SerializeField] private float _fillDuration  = 0.18f;
        [SerializeField] private float _emptyDuration = 0.14f;
        [SerializeField] private float _glowPunchScale = 1.22f;   // max'a ulaşınca ilk vuruş
        [SerializeField] private float _maxPulseScale  = 1.12f;   // sonraki sürekli nabız
        [SerializeField] private float _textPunchScale = 1.15f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private ComboSystem _system;
        private int         _lastCharges    = -1;
        private float       _lastMultiplier = -1f;
        private Sequence    _glowSequence;
        private int         _glowBarIndex   = -1;   // pulse eden bar; -1 = yok
        private Transform   _glowRoot;              // pulse eden bar'ın çerçevesi (ölçeklenen)
        private Vector3     _glowRootScale  = Vector3.one;
        private bool        _pendingMaxGlow;        // OnMaxCharge, OnStateChanged'den ÖNCE gelir
        private int         _visibleBars    = -1;
        private RogueBlockBlast.UI.FX.ComboRescueFX _rescueFX;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>ComboSystem'e abone olur. RunController.Start()'ta çağır.</summary>
        private Vector3 _multiplierBaseScale;

        public void Bind(ComboSystem system)
        {
            if (_system != null)
            {
                _system.OnStateChanged    -= HandleStateChanged;
                _system.OnMaxCharge       -= HandleMaxCharge;
                _system.OnComboReset      -= HandleReset;
                _system.OnComboShieldUsed -= HandleShieldUsed;
                _system.OnSoftLanding     -= HandleSoftLanding;
            }

            _system = system;
            _system.OnStateChanged    += HandleStateChanged;
            _system.OnMaxCharge       += HandleMaxCharge;
            _system.OnComboReset      += HandleReset;
            _system.OnComboShieldUsed += HandleShieldUsed;
            _system.OnSoftLanding     += HandleSoftLanding;

            EnsureRescueFX();

            // Orijinal scale'leri kaydet — world space canvas'ta Vector3.one değil
            if (_multiplierText != null)
                _multiplierBaseScale = _multiplierText.transform.localScale;

            // Sabit 3 değil: combo-bar yükseltmesi MaxCharge'ı 4-5 yapabilir.
            ApplyState(new ComboState(system.Charges, system.MaxCharge, system.Multiplier,
                                      system.IsMaxCharge), animate: false);
        }

        // ── Handlers ─────────────────────────────────────────────────────────
        private void HandleStateChanged(ComboState state)
        {
            // Max charge düştüyse glow'u durdur — bar'lar güncellenmeden ÖNCE,
            // yoksa pulse sequence yeni renk tween'iyle çakışır.
            if (!state.IsMaxCharge)
                StopMaxGlow();

            ApplyState(state, animate: true);

            // ComboSystem OnMaxCharge'ı OnStateChanged'den önce ateşliyor. Glow
            // burada, bar'lar son hâline tween'lendikten sonra başlatılır;
            // aksi hâlde UpdateBars'ın DOColor'ı pulse'un üstüne yazıyordu.
            if (_pendingMaxGlow && state.IsMaxCharge)
                PlayMaxGlow(state.MaxCharge - 1);
            _pendingMaxGlow = false;
        }

        private void HandleMaxCharge() => _pendingMaxGlow = true;

        private void HandleReset()
        {
            // Erişilebilirlik: "Hareketi Azalt" açıkken bu 0 döner ve sarsıntı atlanır.
            float shake = RogueBlockBlast.Core.Settings.GameSettings.ScreenShake;

            // 1. Panel shake
            if (_comboPanel != null && shake > 0f)
            {
                _comboPanel.DOKill();
                _comboPanel.DOShakeAnchorPos(0.3f, strength: new Vector2(5f, 2f) * shake, vibrato: 8, randomness: 45f)
                           .SetEase(Ease.OutQuad);
            }

            // 2. Multiplier text shake
            if (_multiplierText != null && shake > 0f)
            {
                _multiplierText.transform.DOKill();
                _multiplierText.transform
                    .DOShakeScale(0.25f, 0.18f * shake, 5, 45f)
                    .OnComplete(() => _multiplierText.transform.localScale = _multiplierBaseScale);
            }

            // 3. Bar'lar: kırmızı flash → boş renge dön
            if (_chargeBars == null) return;
            foreach (var bar in _chargeBars)
            {
                if (bar == null) continue;
                bar.DOKill();
                DOTween.Sequence()
                    .Append(bar.DOColor(_colorResetFlash, 0.08f).SetEase(Ease.OutQuad))
                    .Append(bar.DOColor(_colorEmpty,      0.25f).SetEase(Ease.OutQuad))
                    .SetAutoKill(true);
            }
        }

        // ── Kurtarma anları ──────────────────────────────────────────────────

        /// <summary>
        /// Combo Shield reset'i soğurdu. Bar'lar burada KIRMIZI yerine mavi
        /// parlıyor: aynı kırmızı flash kullanılsaydı oyuncunun refleksi
        /// "combo'm gitti" olurdu — oysa tam tersi oldu.
        /// </summary>
        private void HandleShieldUsed()
        {
            EnsureRescueFX();
            _rescueFX?.PlayShieldSave(Loc.GetOr("Combo.ShieldHeld", "SHIELD HELD"));

            if (_chargeBars == null) return;
            foreach (var bar in _chargeBars)
            {
                if (bar == null) continue;
                bar.DOKill();
                DOTween.Sequence()
                    .Append(bar.DOColor(_colorShieldFlash, 0.08f).SetEase(Ease.OutQuad))
                    .Append(bar.DOColor(_colorEmpty,       0.30f).SetEase(Ease.OutQuad))
                    .SetAutoKill(true);
            }
        }

        /// <summary>
        /// Soft Landing: çarpan tabana çakılmak yerine yarılandı. Sarsıntı yok —
        /// sarsıntı "kaza" demek, buradaki jest frenleme olmalı.
        /// </summary>
        private void HandleSoftLanding(float before, float after)
        {
            EnsureRescueFX();
            _rescueFX?.PlaySoftLanding(
                Loc.GetOr("Combo.SoftLanding", "SOFT LANDING") + "  x" + after.ToString("0.##"),
                _multiplierText != null ? _multiplierText.rectTransform : null);

            if (_chargeBars == null) return;
            foreach (var bar in _chargeBars)
            {
                if (bar == null) continue;
                bar.DOKill();
                DOTween.Sequence()
                    .Append(bar.DOColor(_colorSoftLandFlash, 0.10f).SetEase(Ease.OutQuad))
                    .Append(bar.DOColor(_colorEmpty,         0.30f).SetEase(Ease.OutQuad))
                    .SetAutoKill(true);
            }
        }

        private void EnsureRescueFX()
        {
            if (_rescueFX != null) return;
            _rescueFX = gameObject.AddComponent<RogueBlockBlast.UI.FX.ComboRescueFX>();
            _rescueFX.Setup(_comboPanel != null ? _comboPanel : transform as RectTransform,
                            _multiplierText != null ? _multiplierText.font : null);
        }

        private void OnDestroy()
        {
            if (_system != null)
            {
                _system.OnStateChanged    -= HandleStateChanged;
                _system.OnMaxCharge       -= HandleMaxCharge;
                _system.OnComboReset      -= HandleReset;
                _system.OnComboShieldUsed -= HandleShieldUsed;
                _system.OnSoftLanding     -= HandleSoftLanding;
            }
            _glowSequence?.Kill();
        }

        // ── Core render ──────────────────────────────────────────────────────
        private void ApplyState(ComboState state, bool animate)
        {
            UpdateBars(state, animate);
            UpdateMultiplierText(state, animate);

            _lastCharges    = state.Charges;
            _lastMultiplier = state.Multiplier;
        }

        private void UpdateBars(ComboState state, bool animate)
        {
            if (_chargeBars == null) return;

            SetVisibleBarCount(state.MaxCharge);

            for (int i = 0; i < _chargeBars.Length; i++)
            {
                if (_chargeBars[i] == null) continue;

                // Pulse eden bar'ın rengi glow sequence'a ait.
                if (i == _glowBarIndex) continue;

                bool shouldBeFilled = i < state.Charges;

                // Max'ta da dolu renk: _colorMaxGlow (turuncu) mavi bar sprite'ını
                // çarpınca kahverengiye dönüyor, barlar "söndü" gibi okunuyordu.
                // Max'ı son bar'ın nabzı anlatır.
                Color targetColor = shouldBeFilled ? _colorFilled : _colorEmpty;

                if (!animate)
                {
                    _chargeBars[i].color      = targetColor;
                    _chargeBars[i].fillAmount = shouldBeFilled ? 1f : 0f;
                    continue;
                }

                float duration = shouldBeFilled ? _fillDuration : _emptyDuration;

                _chargeBars[i].DOColor(targetColor, duration).SetEase(Ease.OutQuad);
                _chargeBars[i].DOFillAmount(shouldBeFilled ? 1f : 0f, duration).SetEase(Ease.OutQuad);
            }
        }

        private void UpdateMultiplierText(ComboState state, bool animate)
        {
            if (_multiplierText == null) return;

            string newText = $"x{state.Multiplier:0.0}";

            if (newText == _multiplierText.text) return;

            _multiplierText.text = newText;

            if (!animate) return;

            // Multiplier arttıysa punch, düştüyse shake
            if (state.Multiplier > _lastMultiplier)
            {
                _multiplierText.transform
                    .DOPunchScale(_multiplierBaseScale * (_textPunchScale - 1f), 0.2f, 1, 0.5f)
                    .OnComplete(() => _multiplierText.transform.localScale = _multiplierBaseScale);
            }
        }

        /// <summary>
        /// Sahnede yükseltmenin en üst seviyesi kadar bar var (5). Yalnızca
        /// MaxCharge kadarı gösterilir; fazlası "kilitli aşama" gibi duruyordu.
        /// </summary>
        private void SetVisibleBarCount(int count)
        {
            if (count == _visibleBars) return;
            _visibleBars = count;

            for (int i = 0; i < _chargeBars.Length; i++)
            {
                if (_chargeBars[i] == null) continue;
                GetBarRoot(_chargeBars[i]).SetActive(i < count);
            }
        }

        /// <summary>
        /// Bar Image'ı bir "Charge_N" çerçevesinin içindeki Fill'dir. Çerçeveyi
        /// gizlemek gerekir; çerçeve layout group'un doğrudan çocuğudur.
        /// </summary>
        private static GameObject GetBarRoot(Image bar)
        {
            var parent = bar.transform.parent;
            bool parentIsContainer = parent == null || parent.GetComponent<LayoutGroup>() != null;
            return parentIsContainer ? bar.gameObject : parent.gameObject;
        }

        // ── Max glow ─────────────────────────────────────────────────────────
        private void PlayMaxGlow(int index)
        {
            if (_chargeBars == null || index < 0 || index >= _chargeBars.Length) return;
            var bar = _chargeBars[index];
            if (bar == null) return;

            StopMaxGlow();
            bar.DOKill();
            bar.fillAmount = 1f;   // DOKill fill tween'ini de durdurdu
            bar.color      = _colorFilled;
            _glowBarIndex  = index;

            var root = GetBarRoot(bar).transform;
            _glowRoot      = root;
            _glowRootScale = root.localScale;
            root.DOKill();

            // Tint beyazın üstüne çıkamıyor; "parlama" dolu renk ile biraz
            // kısılmış hâli arasında gidip gelerek, büyüme ile birlikte okunuyor.
            Color dim = Color.Lerp(_colorFilled, Color.black, 0.3f);
            dim.a = _colorFilled.a;
            float pulse = Mathf.Max(1.05f, _maxPulseScale);

            // Girişte bir kez güçlü vuruş, sonra sürekli nabız.
            // Sahnede 2 kayıtlı; o kadar büyüyen bar komşularının üstüne taşıyor.
            root.localScale = _glowRootScale * Mathf.Clamp(_glowPunchScale, 1f, 1.35f);
            var intro = DOTween.Sequence()
                .Append(root.DOScale(_glowRootScale, 0.25f).SetEase(Ease.OutBack));

            var loop = DOTween.Sequence()
                .Append(root.DOScale(_glowRootScale * pulse, 0.45f).SetEase(Ease.InOutSine))
                .Join(bar.DOColor(_colorFilled, 0.45f).SetEase(Ease.InOutSine))
                .Append(root.DOScale(_glowRootScale, 0.45f).SetEase(Ease.InOutSine))
                .Join(bar.DOColor(dim, 0.45f).SetEase(Ease.InOutSine))
                .SetLoops(-1, LoopType.Restart);

            _glowSequence = DOTween.Sequence()
                .Append(intro)
                .Append(loop)
                .SetAutoKill(false);
        }

        /// <summary>Max charge pulse'u durdurur (charge düşünce çağır).</summary>
        /// <remarks>Rengi UpdateBars toparlar; burada yalnızca sequence bırakılır.</remarks>
        private void StopMaxGlow()
        {
            if (_glowBarIndex < 0) return;
            _glowSequence?.Kill();
            _glowSequence = null;
            _glowBarIndex = -1;

            if (_glowRoot != null)
            {
                _glowRoot.DOKill();
                _glowRoot.localScale = _glowRootScale;
                _glowRoot = null;
            }
        }
    }
}