using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using UnityEngine;

namespace RogueBlockBlast.Game
{
    public static class CardEffectApplier
    {
        public static void Apply(
            Content.CardSO  card,
            ComboSystem     comboSystem,
            MilestoneSystem milestoneSystem,
            ref int         deadPoolRerolls,
            ref float       globalScoreMultiplier,
            ref int         coinBonusPerMilestone,
            RunCardState    cardState)
        {
            if (card == null) return;

            if (card.IsShapeCard)
            {
                ShapeCardEffectRegistry.Instance?.Register(card);
                return;
            }

            foreach (var effect in card.Effects)
            {
                switch (effect.Type)
                {
                    // ── Combo ────────────────────────────────────────────────
                    case Content.CardEffectType.ComboDecayImmunity:
                        Debug.Log("[Card] ComboDecayImmunity aktif");
                        break;

                    case Content.CardEffectType.ComboBonusPerClear:
                        comboSystem?.SetBonusPerClear(comboSystem.BonusPerClear + effect.Value);
                        break;

                    // ── Score ────────────────────────────────────────────────
                    case Content.CardEffectType.ScoreMultiplierBonus:
                    case Content.CardEffectType.LineScoreBonus:
                        globalScoreMultiplier += effect.Value;
                        break;

                    // ── Pool / Survival ──────────────────────────────────────
                    case Content.CardEffectType.ExtraDeadPoolReroll:
                        deadPoolRerolls += Mathf.RoundToInt(effect.Value);
                        break;

                    case Content.CardEffectType.PoolLimitBonus:
                        Debug.Log($"[Card] PoolLimitBonus +{effect.Value}");
                        break;

                    // ── Coin ─────────────────────────────────────────────────
                    case Content.CardEffectType.CoinBonusOnMilestone:
                        coinBonusPerMilestone += Mathf.RoundToInt(effect.Value);
                        break;

                    // ── Diet Plan ────────────────────────────────────────────
                    case Content.CardEffectType.DietPlanMaxSize:
                        cardState.HasDietPlan    = true;
                        cardState.DietPlanMaxSize = Mathf.RoundToInt(effect.Value);
                        break;

                    case Content.CardEffectType.DietPlanScoreFactor:
                        cardState.HasDietPlan         = true;
                        cardState.DietPlanScoreFactor  = effect.Value;
                        break;

                    // ── Ghost Drop ───────────────────────────────────────────
                    case Content.CardEffectType.GhostDropMaxUses:
                        cardState.HasGhostDrop    = true;
                        cardState.GhostDropMaxUses = Mathf.RoundToInt(effect.Value);
                        break;

                    // ── Soft Landing ─────────────────────────────────────────
                    case Content.CardEffectType.SoftLandingFactor:
                        comboSystem?.SetSoftLanding(effect.Value);
                        break;

                    // ── Slow Burn ─────────────────────────────────────────────
                    case Content.CardEffectType.SlowBurnEarlyCount:
                        cardState.HasSlowBurn        = true;
                        cardState.SlowBurnEarlyCount  = Mathf.RoundToInt(effect.Value);
                        break;

                    case Content.CardEffectType.SlowBurnEarlyFactor:
                        cardState.HasSlowBurn         = true;
                        cardState.SlowBurnEarlyFactor  = effect.Value;
                        break;

                    case Content.CardEffectType.SlowBurnLateCount:
                        cardState.HasSlowBurn       = true;
                        cardState.SlowBurnLateCount  = Mathf.RoundToInt(effect.Value);
                        break;

                    case Content.CardEffectType.SlowBurnLateFactor:
                        cardState.HasSlowBurn        = true;
                        cardState.SlowBurnLateFactor  = effect.Value;
                        break;

                    // ── Hyperfocus ────────────────────────────────────────────
                    case Content.CardEffectType.HyperfocusComboMultiplier:
                        cardState.HasHyperfocus = true;
                        if (comboSystem != null)
                        {
                            comboSystem.SetBonusPerClear(comboSystem.BonusPerClear * effect.Value);
                            comboSystem.SetBonusAtMaxCharge(comboSystem.BonusAtMaxCharge * effect.Value);
                        }
                        break;

                    case Content.CardEffectType.HyperfocusPenaltyShapes:
                        cardState.HasHyperfocus           = true;
                        cardState.HyperfocusPenaltyShapes  = Mathf.RoundToInt(effect.Value);
                        break;

                    // ── Tunnel Vision ─────────────────────────────────────────
                    case Content.CardEffectType.TunnelVisionMultiplier:
                        cardState.HasTunnelVision        = true;
                        cardState.TunnelVisionMultiplier  = effect.Value;
                        break;

                    // ── Line Master ───────────────────────────────────────────
                    case Content.CardEffectType.LineMasterMultiplier:
                        cardState.HasLineMaster       = true;
                        cardState.LineMasterMultiplier = effect.Value;
                        break;

                    // ── Bounty Hunter ─────────────────────────────────────────
                    case Content.CardEffectType.BountyHunterCoinPerClear:
                        cardState.HasBountyHunter          = true;
                        cardState.BountyHunterCoinPerClear  = Mathf.RoundToInt(effect.Value);
                        break;

                    case Content.CardEffectType.BountyHunterThresholdScale:
                        cardState.HasBountyHunter = true;
                        milestoneSystem?.ScalePermanent(effect.Value);
                        break;

                    // ── Future Investment ─────────────────────────────────────
                    case Content.CardEffectType.FutureInvestmentCurrentScale:
                        cardState.HasFutureInvestment = true;
                        milestoneSystem?.ScaleCurrentWindow(effect.Value);
                        break;

                    case Content.CardEffectType.FutureInvestmentNextDiscount:
                        cardState.HasFutureInvestment             = true;
                        cardState.FutureInvestmentPendingDiscount += effect.Value;
                        break;

                    // ── First Picks ───────────────────────────────────────────
                    case Content.CardEffectType.FirstPicksFreeCount:
                        cardState.HasFirstPicks       = true;
                        cardState.FirstPicksFreeCount  = Mathf.RoundToInt(effect.Value);
                        break;

                    // ── Momentum Shield ───────────────────────────────────────
                    case Content.CardEffectType.MomentumShieldMinMultiplier:
                        cardState.HasMomentumShield            = true;
                        cardState.MomentumShieldMinMultiplier   = effect.Value;
                        break;

                    // ── Hoarder ───────────────────────────────────────────────
                    case Content.CardEffectType.HoarderMinRemaining:
                        cardState.HasHoarder          = true;
                        cardState.HoarderMinRemaining  = Mathf.RoundToInt(effect.Value);
                        break;

                    case Content.CardEffectType.HoarderPoolBonus:
                        cardState.HasHoarder       = true;
                        cardState.HoarderPoolBonus  = Mathf.RoundToInt(effect.Value);
                        break;

                    default:
                        Debug.LogWarning($"[Card] Bilinmeyen efekt tipi: {effect.Type}");
                        break;
                }
            }
        }
    }
}
