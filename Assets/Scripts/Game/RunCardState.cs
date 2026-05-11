namespace RogueBlockBlast.Game
{
    public sealed class RunCardState
    {
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

        public void Reset()
        {
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
        }

        // Resets per-milestone counters; called from RunController.HandleMilestoneReached.
        public void OnMilestoneReached()
        {
            GhostDropUsesThisMilestone  = 0;
            FirstPicksUsedThisMilestone = 0;
        }
    }
}
