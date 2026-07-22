using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using UnityEngine;

namespace RogueBlockBlast.Game
{
    public static class CardEffectApplier
    {
        /// <summary>Heavy Load: ek pool kapasitesinin karşılığı olan global çarpan cezası.</summary>
        private const float HeavyLoadMultiplierPenalty = 0.10f;


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
                // Stack takibi — kart slotlarındaki canlı değer satırı buradan okur.
                // Skorlamanın kullandığı değerlerle aynı kaynaktan beslenir.
                cardState?.Accumulate(effect.Type, effect.Value);
                cardState?.CountPick(effect.Type);

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
                        if (milestoneSystem != null)
                            milestoneSystem.SetPoolLimit(
                                milestoneSystem.CurrentPoolLimit + Mathf.RoundToInt(effect.Value));
                        Debug.Log($"[Card] Wide Pool: pool limit +{effect.Value} → {milestoneSystem?.CurrentPoolLimit}");
                        break;

                    // ── Heavy Load (pool +Value, karşılığında global çarpan -0.10) ──
                    case Content.CardEffectType.HeavyLoad:
                        if (milestoneSystem != null)
                            milestoneSystem.SetPoolLimit(
                                milestoneSystem.CurrentPoolLimit + Mathf.RoundToInt(effect.Value));
                        globalScoreMultiplier -= HeavyLoadMultiplierPenalty;
                        Debug.Log($"[Card] Heavy Load: pool limit +{effect.Value} → {milestoneSystem?.CurrentPoolLimit}, " +
                                  $"global çarpan -{HeavyLoadMultiplierPenalty} → {globalScoreMultiplier}");
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

                    // ── Perfect Clear ────────────────────────────────────────
                    case Content.CardEffectType.PerfectClearCoinReward:
                        cardState.HasPerfectClear     = true;
                        cardState.PerfectClearCoinReward = Mathf.RoundToInt(effect.Value);
                        break;

                    case Content.CardEffectType.PerfectClearComboBoost:
                        cardState.HasPerfectClear     = true;
                        cardState.PerfectClearComboBoost = effect.Value;
                        break;

                    // ── Selective Blindness ───────────────────────────────────
                    case Content.CardEffectType.SelectiveBlindnessSingleFactor:
                        cardState.HasSelectiveBlindness        = true;
                        cardState.SelectiveBlindnessSingleFactor = effect.Value;
                        break;

                    case Content.CardEffectType.SelectiveBlindnessRemoveCount:
                        cardState.HasSelectiveBlindness       = true;
                        cardState.SelectiveBlindnessRemoveCount = Mathf.RoundToInt(effect.Value);
                        break;

                    // ── Neon Cable ────────────────────────────────────────────
                    case Content.CardEffectType.NeonCableExplosionScore:
                        cardState.HasNeonCable          = true;
                        cardState.NeonCableExplosionScore = effect.Value;
                        break;

                    // ── Safe Zone ─────────────────────────────────────────────
                    case Content.CardEffectType.SafeZoneComboFloor:
                        cardState.HasSafeZone        = true;
                        cardState.SafeZoneComboFloor  = effect.Value;
                        break;

                    case Content.CardEffectType.SafeZonePenalty:
                        cardState.HasSafeZone    = true;
                        cardState.SafeZonePenalty = Mathf.RoundToInt(effect.Value);
                        break;

                    // ── Decaying Rift ─────────────────────────────────────────
                    case Content.CardEffectType.RiftSpawnInterval:
                        cardState.HasDecayingRift   = true;
                        cardState.RiftSpawnInterval  = Mathf.Max(1, Mathf.RoundToInt(effect.Value));
                        break;

                    case Content.CardEffectType.RiftCountdownStart:
                        cardState.HasDecayingRift    = true;
                        cardState.RiftCountdownStart  = Mathf.Max(1, Mathf.RoundToInt(effect.Value));
                        break;

                    case Content.CardEffectType.RiftBonusShapes:
                        cardState.HasDecayingRift = true;
                        cardState.RiftBonusShapes  = Mathf.RoundToInt(effect.Value);
                        break;

                    // ── Phantom Cell ──────────────────────────────────────────
                    case Content.CardEffectType.PhantomCellEnabled:
                        cardState.HasPhantomCell = true;
                        break;

                    // ── Corner Stone ──────────────────────────────────────────
                    case Content.CardEffectType.CornerStoneBonus:
                        cardState.HasCornerStone   = true;
                        cardState.CornerStoneBonus += effect.Value;   // stack'lenir
                        Debug.Log($"[Card] Corner Stone — köşe tile başına toplam +{cardState.CornerStoneBonus}");
                        break;

                    // ── Center Base ───────────────────────────────────────────
                    case Content.CardEffectType.CenterBaseBonus:
                        cardState.HasCenterBase   = true;
                        cardState.CenterBaseBonus += effect.Value;    // stack'lenir
                        Debug.Log($"[Card] Center Base — merkez tile başına toplam +{cardState.CenterBaseBonus}");
                        break;

                    // ── Double Strike ─────────────────────────────────────────
                    case Content.CardEffectType.DoubleStrikeBonus:
                        cardState.HasDoubleStrike = true;
                        Debug.Log($"[Card] Double Strike aktif — {cardState.DoubleStrikeMinLines}+ line clear'da " +
                                  $"skor ×{cardState.DoubleStrikeFactor}");
                        break;

                    // ── Gambler ───────────────────────────────────────────────
                    case Content.CardEffectType.GamblerRoll:
                        cardState.HasGambler = true;
                        Debug.Log($"[Card] Gambler aktif — %{cardState.GamblerChance * 100f} ihtimalle " +
                                  $"×{cardState.GamblerWinFactor} / ×{cardState.GamblerLoseFactor}");
                        break;

                    // ── Patient ───────────────────────────────────────────────
                    case Content.CardEffectType.PatientBonus:
                        cardState.HasPatient = true;
                        Debug.Log($"[Card] Patient aktif — {cardState.PatientMinPlacements}+ clear'sız " +
                                  $"yerleştirme sonrası skor ×{cardState.PatientFactor}");
                        break;

                    // ── Card Collector ────────────────────────────────────────
                    case Content.CardEffectType.CardCollector:
                        cardState.HasCardCollector     = true;
                        cardState.CardCollectorPerCard += effect.Value;   // stack'lenir
                        Debug.Log($"[Card] Card Collector — kart başına global çarpan toplam +{cardState.CardCollectorPerCard}");
                        break;

                    // ── Combo Shield ──────────────────────────────────────────
                    case Content.CardEffectType.ComboShield:
                        if (comboSystem != null) comboSystem.HasComboShield = true;
                        Debug.Log("[Card] Combo Shield aktif — run başına bir combo reset engellenecek");
                        break;

                    // ── Chain Master ──────────────────────────────────────────
                    case Content.CardEffectType.ChainMaster:
                        if (comboSystem != null)
                        {
                            comboSystem.HasChainMaster    = true;
                            comboSystem.ChainMasterBonus += effect.Value;   // stack'lenir
                        }
                        Debug.Log($"[Card] Chain Master — ardışık her clear BonusPerClear'a +{comboSystem?.ChainMasterBonus}");
                        break;

                    default:
                        Debug.LogWarning($"[Card] Bilinmeyen efekt tipi: {effect.Type}");
                        break;
                }
            }
        }
    }
}
