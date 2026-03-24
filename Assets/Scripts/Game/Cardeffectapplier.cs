using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using UnityEngine;

namespace RogueBlockBlast.Game
{
    /// <summary>
    /// Seçilen kartın efektlerini ilgili sistemlere uygular.
    /// RunController'dan çağrılır: CardEffectApplier.Apply(card, ...)
    ///
    /// Yeni efekt tipi eklemek için sadece switch'e yeni case ekle.
    /// </summary>
    public static class CardEffectApplier
    {
        /// <summary>
        /// Kartın tüm efektlerini sistemlere uygular.
        /// </summary>
        public static void Apply(
            Content.CardSO      card,
            ComboSystem         comboSystem,
            MilestoneSystem     milestoneSystem,
            ref int             deadPoolRerolls,
            ref float           globalScoreMultiplier,
            ref int             coinBonusPerMilestone)
        {
            if (card == null) return;

            foreach (var effect in card.Effects)
            {
                switch (effect.Type)
                {
                    // ── Combo ────────────────────────────────────────────────
                    case Content.CardEffectType.ComboDecayImmunity:
                        // ComboSystem'e ileride eklenecek flag
                        Debug.Log($"[Card] ComboDecayImmunity aktif");
                        break;

                    case Content.CardEffectType.ComboBonusPerClear:
                        comboSystem?.SetBonusPerClear(
                            comboSystem.BonusPerClear + effect.Value);
                        Debug.Log($"[Card] ComboBonusPerClear +{effect.Value}");
                        break;

                    // ── Score ────────────────────────────────────────────────
                    case Content.CardEffectType.ScoreMultiplierBonus:
                        globalScoreMultiplier += effect.Value;
                        Debug.Log($"[Card] ScoreMultiplierBonus +{effect.Value}");
                        break;

                    case Content.CardEffectType.LineScoreBonus:
                        // RunController'da DoPlace içinde kullanılır
                        // globalScoreMultiplier yerine ayrı field gerekebilir
                        globalScoreMultiplier += effect.Value;
                        Debug.Log($"[Card] LineScoreBonus +{effect.Value}");
                        break;

                    // ── Pool / Survival ──────────────────────────────────────
                    case Content.CardEffectType.ExtraDeadPoolReroll:
                        deadPoolRerolls += Mathf.RoundToInt(effect.Value);
                        Debug.Log($"[Card] ExtraDeadPoolReroll +{effect.Value}");
                        break;

                    case Content.CardEffectType.PoolLimitBonus:
                        // MilestoneSystem'e ileride eklenecek
                        Debug.Log($"[Card] PoolLimitBonus +{effect.Value}");
                        break;

                    // ── Coin ─────────────────────────────────────────────────
                    case Content.CardEffectType.CoinBonusOnMilestone:
                        coinBonusPerMilestone += Mathf.RoundToInt(effect.Value);
                        Debug.Log($"[Card] CoinBonusOnMilestone +{effect.Value}");
                        break;

                    default:
                        Debug.LogWarning($"[Card] Bilinmeyen efekt tipi: {effect.Type}");
                        break;
                }
            }
        }
    }
}