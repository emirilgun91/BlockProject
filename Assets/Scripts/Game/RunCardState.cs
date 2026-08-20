using System.Collections.Generic;
using RogueBlockBlast.Content;
using UnityEngine;

namespace RogueBlockBlast.Game
{
    public sealed class RunCardState
    {
        // ── Stack totals ──────────────────────────────────────────────────────
        // Her kart seçiminde efekt tipine göre birikir. UI (kart slotlarındaki
        // canlı değer satırı) buradan okur — skorlama ile aynı kaynak.
        private readonly Dictionary<CardEffectType, float> _accumulated = new();

        public void Accumulate(CardEffectType type, float value)
        {
            _accumulated.TryGetValue(type, out float current);
            _accumulated[type] = current + value;
        }

        public float GetAccumulated(CardEffectType type)
            => _accumulated.TryGetValue(type, out float v) ? v : 0f;

        public int GetPickCount(CardEffectType type)
            => _pickCounts.TryGetValue(type, out int c) ? c : 0;

        private readonly Dictionary<CardEffectType, int> _pickCounts = new();

        public void CountPick(CardEffectType type)
        {
            _pickCounts.TryGetValue(type, out int c);
            _pickCounts[type] = c + 1;
        }

        // ── Diet Plan ─────────────────────────────────────────────────────────
        public bool  HasDietPlan;
        public int   DietPlanMaxSize     = 4;
        public float DietPlanScoreFactor = 1f;

        // ── Ghost Drop ────────────────────────────────────────────────────────
        public bool HasGhostDrop;
        public int  GhostDropMaxUses;
        public int  GhostDropUsesThisMilestone;

        // ── Slow Burn ─────────────────────────────────────────────────────────
        public bool  HasSlowBurn;
        public int   SlowBurnEarlyCount;
        public float SlowBurnEarlyFactor = 1f;
        public int   SlowBurnLateCount;
        public float SlowBurnLateFactor  = 1f;

        // ── Hyperfocus ────────────────────────────────────────────────────────
        public bool HasHyperfocus;
        public int  HyperfocusPenaltyShapes;

        // ── Tunnel Vision ─────────────────────────────────────────────────────
        public bool  HasTunnelVision;
        public float TunnelVisionMultiplier = 2.5f;

        // ── Line Master ───────────────────────────────────────────────────────
        public bool  HasLineMaster;
        public float LineMasterMultiplier = 2.5f;

        // ── Bounty Hunter ─────────────────────────────────────────────────────
        public bool HasBountyHunter;
        public int  BountyHunterCoinPerClear;

        // ── Future Investment ─────────────────────────────────────────────────
        public bool  HasFutureInvestment;
        public float FutureInvestmentPendingDiscount;

        // ── First Picks ───────────────────────────────────────────────────────
        public bool HasFirstPicks;
        public int  FirstPicksFreeCount;
        public int  FirstPicksUsedThisMilestone;

        // ── Momentum Shield ───────────────────────────────────────────────────
        public bool  HasMomentumShield;
        public float MomentumShieldMinMultiplier = 3f;

        // ── Hoarder ───────────────────────────────────────────────────────────
        public bool HasHoarder;
        public int  HoarderMinRemaining;
        public int  HoarderPoolBonus;

        // ── Perfect Clear ─────────────────────────────────────────────────────
        public bool  HasPerfectClear;
        public int   PerfectClearCoinReward;
        public float PerfectClearComboBoost;

        // ── Selective Blindness ───────────────────────────────────────────────
        public bool  HasSelectiveBlindness;
        public float SelectiveBlindnessSingleFactor = 2f;
        public int   SelectiveBlindnessRemoveCount  = 2;

        // ── Neon Cable ────────────────────────────────────────────────────────
        public bool        HasNeonCable;
        public float       NeonCableExplosionScore = 1f;
        public Vector2Int  NeonCablePositionA      = new Vector2Int(-1, -1);
        public Vector2Int  NeonCablePositionB      = new Vector2Int(-1, -1);
        public bool NeonCableAValid => NeonCablePositionA.x >= 0;
        public bool NeonCableBValid => NeonCablePositionB.x >= 0;

        // ── Safe Zone ─────────────────────────────────────────────────────────
        public bool       HasSafeZone;
        public float      SafeZoneComboFloor = 2f;
        public int        SafeZonePenalty    = 8;
        public Vector2Int SafeZonePosition   = new Vector2Int(-1, -1);
        public bool SafeZoneActive => SafeZonePosition.x >= 0;

        // ── Decaying Rift ─────────────────────────────────────────────────────
        public bool       HasDecayingRift;
        public int        RiftSpawnInterval  = 7;
        public int        RiftCountdownStart = 5;
        public int        RiftBonusShapes    = 2;
        public int        RiftPlacementCounter;           // counts toward next spawn
        public Vector2Int RiftTilePosition = new Vector2Int(-1, -1);
        public int        RiftCurrentCount;               // current countdown value
        public bool RiftTileActive => RiftTilePosition.x >= 0;

        // ── Phantom Cell ──────────────────────────────────────────────────────
        public bool       HasPhantomCell;
        public Vector2Int PhantomCellPosition = new Vector2Int(-1, -1);
        public bool PhantomCellActive => PhantomCellPosition.x >= 0;

        // ── Corner Stone ──────────────────────────────────────────────────────
        // Stack'lenir: iki kart seçilirse tile başına +6. Default 0 — değer
        // yalnızca kart seçildiğinde applier tarafından eklenir.
        public bool  HasCornerStone;
        public float CornerStoneBonus;

        // ── Center Base ───────────────────────────────────────────────────────
        public bool  HasCenterBase;
        public float CenterBaseBonus;

        // ── Double Strike ─────────────────────────────────────────────────────
        public bool  HasDoubleStrike;
        public int   DoubleStrikeMinLines = 2;
        public float DoubleStrikeFactor   = 1.5f;

        // ── Gambler ───────────────────────────────────────────────────────────
        public bool  HasGambler;
        public float GamblerChance    = 0.2f;
        public float GamblerWinFactor = 2f;
        public float GamblerLoseFactor = 0.5f;

        // ── Patient ───────────────────────────────────────────────────────────
        public bool  HasPatient;
        public int   PatientMinPlacements = 3;
        public float PatientFactor        = 2f;
        public int   PlacementsWithoutClear;

        // ── Card Collector ────────────────────────────────────────────────────
        public bool  HasCardCollector;
        public float CardCollectorPerCard;   // stack'lenir

        public void Reset()
        {
            _accumulated.Clear();
            _pickCounts.Clear();

            HasCornerStone   = false; CornerStoneBonus = 0f;
            HasCenterBase    = false; CenterBaseBonus  = 0f;
            HasDoubleStrike  = false; DoubleStrikeMinLines = 2;  DoubleStrikeFactor = 1.5f;
            HasGambler       = false; GamblerChance = 0.2f;
                                      GamblerWinFactor = 2f;     GamblerLoseFactor = 0.5f;
            HasPatient       = false; PatientMinPlacements = 3;  PatientFactor = 2f;
                                      PlacementsWithoutClear = 0;
            HasCardCollector = false; CardCollectorPerCard = 0f;

            HasDietPlan      = false; DietPlanMaxSize = 4;         DietPlanScoreFactor = 1f;
            HasGhostDrop     = false; GhostDropMaxUses = 0;        GhostDropUsesThisMilestone = 0;
            HasSlowBurn      = false; SlowBurnEarlyCount = 0;      SlowBurnEarlyFactor = 1f;
                                      SlowBurnLateCount = 0;       SlowBurnLateFactor = 1f;
            HasHyperfocus    = false; HyperfocusPenaltyShapes = 0;
            HasTunnelVision  = false; TunnelVisionMultiplier = 2.5f;
            HasLineMaster    = false; LineMasterMultiplier = 2.5f;
            HasBountyHunter  = false; BountyHunterCoinPerClear = 0;
            HasFutureInvestment = false; FutureInvestmentPendingDiscount = 0f;
            HasFirstPicks    = false; FirstPicksFreeCount = 0;     FirstPicksUsedThisMilestone = 0;
            HasMomentumShield = false; MomentumShieldMinMultiplier = 3f;
            HasHoarder       = false; HoarderMinRemaining = 0;     HoarderPoolBonus = 0;

            HasPerfectClear        = false; PerfectClearCoinReward = 0;   PerfectClearComboBoost = 0f;
            HasSelectiveBlindness  = false; SelectiveBlindnessSingleFactor = 2f; SelectiveBlindnessRemoveCount = 2;
            HasNeonCable           = false; NeonCableExplosionScore = 1f;
                                            NeonCablePositionA = new Vector2Int(-1,-1);
                                            NeonCablePositionB = new Vector2Int(-1,-1);
            HasSafeZone            = false; SafeZoneComboFloor = 2f;  SafeZonePenalty = 8;
                                            SafeZonePosition   = new Vector2Int(-1,-1);
            HasDecayingRift        = false; RiftSpawnInterval = 7;    RiftCountdownStart = 5;
                                            RiftBonusShapes = 2;      RiftPlacementCounter = 0;
                                            RiftTilePosition = new Vector2Int(-1,-1); RiftCurrentCount = 0;
            HasPhantomCell         = false; PhantomCellPosition = new Vector2Int(-1,-1);
        }

        // Resets per-milestone counters; called from RunController.HandleMilestoneReached.
        public void OnMilestoneReached()
        {
            GhostDropUsesThisMilestone  = 0;

            // "During this Milestone" diyen kartlar burada sona erer.
            // Kart seçimi bu çağrıdan SONRA yapıldığı için, burada kapatmak kartın tam olarak
            // bir milestone yaşamasını sağlar; tekrar seçilirse yeniden açılır.

            // First Picks — ücretsiz yerleştirmeler + döndürme kilidi
            HasFirstPicks               = false;
            FirstPicksFreeCount         = 0;
            FirstPicksUsedThisMilestone = 0;

            // Diet Plan — şekil boyutu kısıtı + puan cezası
            HasDietPlan         = false;
            DietPlanMaxSize     = 4;
            DietPlanScoreFactor = 1f;
        }
    }
}
