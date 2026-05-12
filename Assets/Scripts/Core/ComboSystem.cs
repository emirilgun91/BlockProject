using System;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Combo mantığını yönetir — UI veya Unity bağımlılığı yok.
    ///
    /// KURALLAR:
    /// - Line clear → charge +1 (max 3)
    ///   · charge < 3  → multiplier += 0.1
    ///   · charge == 3 → multiplier += 0.2 (max charge bonusu)
    /// - Placement, clear yok → charge -1 (min 0)
    ///   · charge == 0 → multiplier sıfırlanır (x1.0)
    ///
    /// Kartlar ileride ChargePerClear ve BonusPerCharge değerlerini değiştirebilir.
    /// </summary>
    public sealed class ComboSystem
    {
        // ── Config (kartlarla değiştirilebilir) ──────────────────────────────
        public int   MaxCharge         { get; private set; } = 3;
        public float BonusPerClear     { get; private set; } = 0.1f;  // stage 1-2
        public float BonusAtMaxCharge  { get; private set; } = 0.2f;  // stage 3

        // ── Soft Landing ─────────────────────────────────────────────────────
        private bool  _softLandingActive;
        private float _softLandingFactor = 0.5f;

        // ── Combo Floor (Safe Zone card) ──────────────────────────────────────
        private float _comboFloor = 0f;

        // ── State ────────────────────────────────────────────────────────────
        public int   Charges    { get; private set; } = 0;
        public float Multiplier { get; private set; } = 1f;
        public bool  IsMaxCharge => Charges >= MaxCharge;

        // ── Events ───────────────────────────────────────────────────────────
        /// <summary>Herhangi bir state değişikliğinde tetiklenir.</summary>
        public event Action<ComboState> OnStateChanged;

        /// <summary>Multiplier sıfırlandığında tetiklenir.</summary>
        public event Action OnComboReset;

        /// <summary>Max charge'a ulaşıldığında tetiklenir.</summary>
        public event Action OnMaxCharge;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Line clear sonrası çağrılır.
        /// lineCount kadar charge ve multiplier bonus eklenir.
        /// </summary>
        public void OnLineClear(int lineCount)
        {
            if (lineCount <= 0) return;

            for (int i = 0; i < lineCount; i++)
            {
                if (Charges < MaxCharge)
                    Charges++;

                float bonus = Charges >= MaxCharge ? BonusAtMaxCharge : BonusPerClear;
                Multiplier += bonus;
            }

            if (IsMaxCharge)
                OnMaxCharge?.Invoke();

            FireStateChanged();
        }

        /// <summary>
        /// Her placement sonrası çağrılır.
        /// hadClear = true ise OnLineClear zaten çağrıldı, burada sadece no-clear durumu.
        /// </summary>
        public void OnPlacement(bool hadClear)
        {
            if (hadClear) return; // clear varsa OnLineClear halletti

            if (Charges > 0)
            {
                Charges--;

                if (Charges == 0)
                {
                    float floor = Mathf.Max(BaseMultiplier, _comboFloor);
                    Multiplier = _softLandingActive
                        ? Mathf.Max(floor, Multiplier * _softLandingFactor)
                        : floor;
                    OnComboReset?.Invoke();
                }
            }

            FireStateChanged();
        }

        /// <summary>Safe Zone kartı: combo reset'te düşülecek minimum çarpanı ayarla. 0 = devre dışı.</summary>
        public void SetComboFloor(float value)
        {
            _comboFloor = Mathf.Max(0f, value);
        }

        /// <summary>Perfect Clear kartı: çarpana doğrudan ekleme.</summary>
        public void AddMultiplier(float delta)
        {
            if (delta <= 0f) return;
            Multiplier += delta;
            FireStateChanged();
        }

        /// <summary>Perfect Clear kartı: charge'ı zorla max'a çek.</summary>
        public void ForceMaxCharge()
        {
            if (Charges >= MaxCharge) return;
            Charges = MaxCharge;
            OnMaxCharge?.Invoke();
            FireStateChanged();
        }

        /// <summary>Run başında sıfırlar.</summary>
        public void Reset()
        {
            Charges             = 0;
            Multiplier          = BaseMultiplier;
            _softLandingActive  = false;
            _comboFloor         = 0f;
            FireStateChanged();
        }

        /// <summary>
        /// Momentum Shield gibi zorla sıfırlamalar için — OnComboReset tetiklenmez,
        /// bu sayede Hyperfocus cezası yanlışlıkla uygulanmaz.
        /// </summary>
        public void ForceResetToBase()
        {
            Charges    = 0;
            Multiplier = BaseMultiplier;
            FireStateChanged();
        }

        /// <summary>Kart efekti: max charge bonus'unu değiştir.</summary>
        public void SetBonusAtMaxCharge(float value)
        {
            BonusAtMaxCharge = value;
        }

        /// <summary>Kart efekti: her clear başına bonus'u değiştir.</summary>
        public void SetBonusPerClear(float value)
        {
            BonusPerClear = value;
        }

        /// <summary>Soft Landing kartı: reset yerine mevcut combo'yu yarıya indir.</summary>
        public void SetSoftLanding(float factor)
        {
            _softLandingActive = true;
            _softLandingFactor = Mathf.Clamp01(factor);
        }

        /// <summary>
        /// Upgrade: ComboGainBoost — BonusPerClear'a kalıcı ekleme.
        /// Her çağrıda kümülatif — Reset'te kaybolmaz.
        /// </summary>
        public void AddBonusPerClear(float delta)
        {
            BonusPerClear += delta;
        }

        /// <summary>
        /// Upgrade: ComboBarExpansion — MaxCharge'ı artır (3→4→5).
        /// </summary>
        public void SetMaxCharge(int value)
        {
            MaxCharge = Mathf.Max(1, value);
        }

        /// <summary>
        /// Upgrade: StartingCombo — Run başında ve her reset'te
        /// 1.0 yerine bu değere döner.
        /// </summary>
        public float BaseMultiplier { get; private set; } = 1f;

        public void SetBaseMultiplier(float value)
        {
            BaseMultiplier = Mathf.Max(1f, value);
        }

        // ── Private ──────────────────────────────────────────────────────────
        private void FireStateChanged()
        {
            OnStateChanged?.Invoke(new ComboState(Charges, MaxCharge, Multiplier, IsMaxCharge));
        }
    }

    /// <summary>UI'ya gönderilen snapshot — mutable state geçmiyoruz.</summary>
    public readonly struct ComboState
    {
        public readonly int   Charges;
        public readonly int   MaxCharge;
        public readonly float Multiplier;
        public readonly bool  IsMaxCharge;

        public ComboState(int charges, int maxCharge, float multiplier, bool isMaxCharge)
        {
            Charges    = charges;
            MaxCharge  = maxCharge;
            Multiplier = multiplier;
            IsMaxCharge = isMaxCharge;
        }
    }
}