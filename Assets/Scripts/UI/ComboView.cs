using DG.Tweening;
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
        [SerializeField] private Image[] _chargeBars;   // 3 eleman — Charge_1/2/3

        [Header("Text")]
        [SerializeField] private TMP_Text _multiplierText;

        [Header("Colors")]
        [SerializeField] private Color _colorEmpty   = new Color(0.25f, 0.29f, 0.40f, 1f); // #404966
        [SerializeField] private Color _colorFilled  = new Color(0.91f, 0.64f, 0.19f, 1f); // #E8A230
        [SerializeField] private Color _colorMaxGlow = new Color(1.00f, 0.80f, 0.20f, 1f); // parlak amber

        [Header("Animation")]
        [SerializeField] private float _fillDuration  = 0.18f;
        [SerializeField] private float _emptyDuration = 0.14f;
        [SerializeField] private float _glowPunchScale = 1.22f;
        [SerializeField] private float _textPunchScale = 1.15f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private ComboSystem _system;
        private int         _lastCharges    = -1;
        private float       _lastMultiplier = -1f;
        private Sequence    _glowSequence;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>ComboSystem'e abone olur. RunController.Start()'ta çağır.</summary>
        private Vector3 _multiplierBaseScale;

        public void Bind(ComboSystem system)
        {
            if (_system != null)
            {
                _system.OnStateChanged -= HandleStateChanged;
                _system.OnMaxCharge    -= HandleMaxCharge;
                _system.OnComboReset   -= HandleReset;
            }

            _system = system;
            _system.OnStateChanged += HandleStateChanged;
            _system.OnMaxCharge    += HandleMaxCharge;
            _system.OnComboReset   += HandleReset;

            // Orijinal scale'leri kaydet — world space canvas'ta Vector3.one değil
            if (_multiplierText != null)
                _multiplierBaseScale = _multiplierText.transform.localScale;

            ApplyState(new ComboState(0, 3, 1f, false), animate: false);
        }

        // ── Handlers ─────────────────────────────────────────────────────────
        private void HandleStateChanged(ComboState state)
        {
            // Max charge düştüyse glow'u durdur
            if (_lastCharges >= state.MaxCharge && state.Charges < state.MaxCharge)
                StopMaxGlow();

            ApplyState(state, animate: true);
        }

        private void HandleMaxCharge()
        {
            // 3. bar'a özel glow animasyonu
            if (_chargeBars != null && _chargeBars.Length >= 3)
                PlayMaxGlow(_chargeBars[2]);
        }

        private void HandleReset()
        {
            if (_multiplierText != null)
            {
                _multiplierText.transform
                    .DOShakeScale(0.25f, 0.2f, 5, 45f)
                    .OnComplete(() => _multiplierText.transform.localScale = _multiplierBaseScale);
            }
        }

        private void OnDestroy()
        {
            if (_system != null)
            {
                _system.OnStateChanged -= HandleStateChanged;
                _system.OnMaxCharge    -= HandleMaxCharge;
                _system.OnComboReset   -= HandleReset;
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

            for (int i = 0; i < _chargeBars.Length; i++)
            {
                if (_chargeBars[i] == null) continue;

                bool shouldBeFilled = i < state.Charges;
                bool wasFilled      = i < _lastCharges;

                Color targetColor = shouldBeFilled
                    ? (state.IsMaxCharge ? _colorMaxGlow : _colorFilled)
                    : _colorEmpty;

                if (!animate)
                {
                    _chargeBars[i].color = targetColor;
                    continue;
                }

                float duration = shouldBeFilled ? _fillDuration : _emptyDuration;

                _chargeBars[i].DOColor(targetColor, duration)
                    .SetEase(Ease.OutQuad);
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

        // ── Max glow ─────────────────────────────────────────────────────────
        private void PlayMaxGlow(Image bar)
        {
            _glowSequence?.Kill();
            _glowSequence = DOTween.Sequence()
                // Sadece renk — scale yok, ekranı kaplamasın
                .Append(bar.DOColor(_colorMaxGlow, 0.15f).SetEase(Ease.OutQuad))
                // Sürekli pulse
                .Append(bar.DOColor(_colorFilled,  0.5f).SetEase(Ease.InOutSine))
                .Append(bar.DOColor(_colorMaxGlow,  0.5f).SetEase(Ease.InOutSine))
                .SetLoops(-1, LoopType.Restart)
                .SetAutoKill(false);
        }

        /// <summary>Max charge pulse'u durdurur (charge düşünce çağır).</summary>
        private void StopMaxGlow()
        {
            _glowSequence?.Kill();

            if (_chargeBars != null && _chargeBars.Length >= 3 && _chargeBars[2] != null)
                _chargeBars[2].DOColor(_colorEmpty, _emptyDuration);
        }
    }
}