using System;

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
        public int   MaxCharge         { get; private set; } = 5;
        public float BonusPerClear     { get; private set; } = 0.1f;  // stage 1-2
        public float BonusAtMaxCharge  { get; private set; } = 0.2f;  // stage 3

        // ── State ────────────────────────────────────────────────────────────
        public int   Charges    { get; private set; } = 5;
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
                Charges--;

            if (Charges == 0)
            {
                Multiplier = 1f;
                OnComboReset?.Invoke();
            }

            FireStateChanged();
        }

        /// <summary>Run başında sıfırlar.</summary>
        public void Reset()
        {
            Charges    = 0;
            Multiplier = 1f;
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