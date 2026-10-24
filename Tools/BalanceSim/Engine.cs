// RunController / ComboSystem / MilestoneSystem / CardEffectApplier'ın birebir portu.
// Sıra ve yan etkiler oyundaki DoPlace ile aynı tutuldu — "düzeltilmiş" davranış
// yalnızca Config bayraklarıyla açılır, varsayılan oyundaki hâlidir.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BalanceSim
{
    public sealed class Rng
    {
        ulong s0, s1;
        public Rng(ulong seed) { s0 = Mix(ref seed); s1 = Mix(ref seed); }
        static ulong Mix(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL; ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
        public ulong Next() { ulong a = s0, b = s1; s0 = b; a ^= a << 23; s1 = a ^ b ^ (a >> 17) ^ (b >> 26); return s1 + b; }
        public int Range(int lo, int hi) => hi <= lo ? lo : lo + (int)(Next() % (ulong)(hi - lo));
        public float Value() => (float)((Next() >> 40) / (double)(1UL << 24));
    }

    public sealed class Config
    {
        public bool Demo;                 // RunController._demoBuild (sahnede AÇIK)
        public int DemoThreshold = 20000;
        /// <summary>23 Ekim düzeltmelerinden önceki davranış: yeni havuz ölüyken revive atlanır,
        /// card reroll kullanılamaz, Shape_4gen/L kartları çıkmaz.</summary>
        public bool Legacy;
        public int MaxPlacements = 6000;

        // ── Denge senaryoları (varsayılan = oyundaki hâl) ──
        public float ComboCap;            // >0: çarpan tavanı
        public float ComboDimK;           // >0: artış / (1 + (mult-base)/K)
        public int   ChainMode = 2;       // 0 sınırsız (24 Ekim öncesi), 1 zincir kırılınca sıfırla, 2 toplamı ChainCap ile sınırla (oyundaki)
        public float ChainCap = 1.0f;       // ComboSystem.ChainMasterCap
        public bool  MaxChargeFollowsBpc = true; // dolu barda artış = max(BonusAtMax, BonusPerClear + 0.1) (ComboSystem.MaxChargeBonus)
        public int[] CoinOverride;        // aşama başına coin ödülü
        public float GainScale = 1f;      // şekil skor yükseltmesinin etkisi
        public bool  StopAtVictory = true;
        public float[] TileOverride;      // >0: şeklin hücre değeri (seviyeyi yok sayar) — şekil deneyleri
        public int[]   WeightOverride;    // şeklin ağırlığı; 0 = hiç gelmez — şekil deneyleri
        public float   BountyPct;         // >0: Bounty Hunter satır başı coin = mevcut aşama ödülünün bu oranı — öneri deneyi
        public int     MilestonePasses;   // öneri: run başına milestone geçiş hakkı
        public float   PassMinPct = 0.75f; // hak, skor hedefin bu oranındaysa kullanılır
        public bool    PassGivesCoins;
        public bool    MilestoneGapScale = true; // Future Investment hedefin farkını ölçekler (MilestoneSystem.EffectiveThreshold)
        public bool    FutureInvestmentGift = true; // sonraki "indirim" aşama farkının o kadarı hediye skor (RunController)

        /// <summary>Classic Mode (GameMode.Classic): milestone/kart/upgrade yok, döndürme kapalı,
        /// her parça rastgele bir rotasyonla gelir, yalnızca başta açık şekiller çıkar ve
        /// tahtaya sığmayan parça ClassicFitRetries kez yeniden çekilir.</summary>
        public bool  Classic;
        public int   ClassicFitRetries = 2;   // ShapeSpawnService.ClassicFitRetries
        public bool  ClassicRotate;           // karşılaştırma: döndürme açık
        public bool  ClassicAllShapes;        // karşılaştırma: kilitli şekiller de çıkar
    }

    public sealed class Profile
    {
        public long Coins;
        public int[] Upg;
        public bool[] ShapeUnlocked;   // UnlockRegistry._unlockedShapes (yalnızca satın alınanlar)
        public int[] ScoreLvl, WeightLvl;
        public int MaxMilestoneReached;
        public HashSet<string> Reached = new();
        public HashSet<string> UnlockedCards = new();

        public Profile(GameData g)
        {
            Upg = new int[g.Upgrades.Count];
            ShapeUnlocked = new bool[g.Shapes.Count];
            ScoreLvl = new int[g.Shapes.Count];
            WeightLvl = new int[g.Shapes.Count];
        }

        public bool ShapeAvailable(ShapeDef s) => !s.Locked || ShapeUnlocked[s.Idx];
        public float Effect(GameData g, string id) { var u = g.Upgrades[g.U(id)]; return Upg[u.Idx] * u.PerLevel; }
        public bool UpgradeOpen(UpgradeDef u) => u.UnlockStage <= 0 || MaxMilestoneReached >= u.UnlockStage;
        public int EffectiveWeight(ShapeDef s) => Math.Max(2, s.BaseWeight + WeightLvl[s.Idx] * 5);
    }

    public sealed class Piece { public ShapeDef S; public float Tile; public int Rot; }

    public sealed class Combo
    {
        public int MaxCharge = 3;
        public float BonusPerClear = 0.1f, BonusAtMax = 0.2f;
        bool soft; float softF = 0.5f; float floor;
        public bool HasShield; bool shieldUsed;
        public bool HasChain; public float ChainBonus, ChainAcc; int consec;
        public int Charges; public float Mult = 1f, Base = 1f;
        public Action OnReset;
        public bool IsMax => Charges >= MaxCharge;

        // Senaryo ayarları (Config'ten); varsayılanları oyundaki davranış
        public float Cap, DimK, ChainCap = 0.5f;
        public int ChainMode;
        public bool MaxFollowsBpc;
        float chainThisStreak;
        public float MaxChainAcc;

        public void OnLineClear(int n)
        {
            if (n <= 0) return;
            for (int i = 0; i < n; i++)
            {
                if (Charges < MaxCharge) Charges++;
                float bam = MaxFollowsBpc ? MathF.Max(BonusAtMax, BonusPerClear + 0.1f) : BonusAtMax;
                float inc = Charges >= MaxCharge ? bam : BonusPerClear;
                if (DimK > 0f) inc /= 1f + (Mult - Base) / DimK;
                Mult += inc;
                if (Cap > 0f && Mult > Cap) Mult = Cap;
            }
            consec++;
            if (HasChain && ChainBonus > 0f)
            {
                float add = ChainBonus;
                if (ChainMode == 2) add = MathF.Max(0f, MathF.Min(add, ChainCap - ChainAcc));
                BonusPerClear += add; ChainAcc += add; chainThisStreak += add;
                if (ChainAcc > MaxChainAcc) MaxChainAcc = ChainAcc;
            }
        }

        public void OnPlacement(bool hadClear)
        {
            if (hadClear) return;
            consec = 0;
            if (ChainMode == 1 && chainThisStreak > 0f)
            {
                // açıklamadaki gibi: zincir kırılınca biriken bonus geri alınır
                BonusPerClear -= chainThisStreak; ChainAcc -= chainThisStreak; chainThisStreak = 0f;
            }
            if (Charges > 0)
            {
                if (Charges == 1 && HasShield && !shieldUsed) { shieldUsed = true; return; }
                Charges--;
                if (Charges == 0)
                {
                    float fl = MathF.Max(Base, floor);
                    Mult = soft ? MathF.Max(fl, Mult * softF) : fl;
                    if (!(soft && Mult > fl)) OnReset?.Invoke();
                }
            }
        }

        public void SetComboFloor(float v) => floor = MathF.Max(0f, v);
        public void AddMultiplier(float d) { if (d > 0f) Mult += d; }
        public void ForceMaxCharge() { if (Charges < MaxCharge) Charges = MaxCharge; }
        public void ForceResetToBase() { Charges = 0; Mult = Base; }
        public void SetSoftLanding(float f) { soft = true; softF = Math.Clamp(f, 0f, 1f); }
        public void Reset()
        {
            Charges = 0; Mult = Base; soft = false; floor = 0f; HasShield = false; shieldUsed = false;
            HasChain = false; ChainBonus = 0f; ChainAcc = 0f; consec = 0;
        }
        public bool SoftActive => soft;
        public float SoftFactor => softF;
        public float Floor => floor;
    }

    public sealed class MilestoneSys
    {
        readonly GameData g; int poolOverride;
        float curScale = 1f, permScale = 1f;
        public int Index, Placed, Score, LastRemaining;
        public Action<int, int> OnReached;
        public Action OnExhausted;
        public MilestoneSys(GameData g, int limit) { this.g = g; poolOverride = limit; }
        public int Limit => poolOverride > 0 ? poolOverride : g.PoolLimit;
        public int Remaining => Math.Max(0, Limit - Placed);
        public bool AllCleared => Index >= g.Milestones.Count;
        public bool GapScale;   // öneri: pencere ölçeği toplam hedefi değil, bu aşamanın gereken farkını ölçekler
        float Prev => Index == 0 ? 0f : g.Milestones[Index - 1].Threshold * permScale;
        float Eff(int i) => GapScale ? Prev + (g.Milestones[i].Threshold * permScale - Prev) * curScale : g.Milestones[i].Threshold * curScale * permScale;
        public float NextThreshold => AllCleared ? float.MaxValue : Eff(Index);
        public float PermScale => permScale;

        public void OnPiecePlaced() { Placed++; if (Remaining <= 0) OnExhausted?.Invoke(); }
        public void OnScoreChanged(int s)
        {
            Score = s;
            if (AllCleared) return;
            var m = g.Milestones[Index];
            if (s >= Eff(Index))
            {
                LastRemaining = Remaining;
                int idx = Index;
                Index++; Placed = 0; curScale = 1f;
                OnReached?.Invoke(m.Coin, idx);
            }
        }
        public void ScaleCurrentWindow(float s) => curScale *= s;
        public void ScalePermanent(float s) => permScale *= s;
        /// <summary>Milestone geçiş hakkı (öneri): hedefe ulaşılmadan aşamayı geçilmiş say.</summary>
        public void ForceReach(bool coins) { var m = g.Milestones[Index]; LastRemaining = 0; int idx = Index; Index++; Placed = 0; curScale = 1f; OnReached?.Invoke(coins ? m.Coin : 0, idx); }
        public void DeductPieces(int c) { Placed = Math.Min(Placed + c, Limit); if (Remaining <= 0) OnExhausted?.Invoke(); }
        public void SetPoolLimit(int l) => poolOverride = l;
        public void AddPieces(int c) => Placed = Math.Max(0, Placed - c);
        public void EnsureMinimumRemaining(int min = 6) { if (Remaining < min) Placed = Math.Max(0, Limit - min); }
    }

    public sealed class CardState
    {
        public bool HasDietPlan; public int DietPlanMaxSize = 4; public float DietPlanScoreFactor = 1f;
        public bool HasGhostDrop; public int GhostDropMaxUses, GhostDropUses;
        public bool HasSlowBurn; public int SlowBurnEarlyCount; public float SlowBurnEarlyFactor = 1f; public int SlowBurnLateCount; public float SlowBurnLateFactor = 1f;
        public bool HasHyperfocus; public int HyperfocusPenaltyShapes;
        public bool HasTunnelVision; public float TunnelVisionMultiplier = 2.5f;
        public bool HasLineMaster; public float LineMasterMultiplier = 2.5f;
        public bool HasBountyHunter; public int BountyHunterCoinPerClear, BountyCoins; public float BountyShare;
        public bool HasFutureInvestment; public float FutureInvestmentPendingDiscount;
        public bool HasFirstPicks; public int FirstPicksFreeCount, FirstPicksUsed;
        public bool HasMomentumShield; public float MomentumShieldMin = 3f;
        public bool HasHoarder; public int HoarderMinRemaining, HoarderPoolBonus;
        public bool HasPerfectClear; public int PerfectClearCoin; public float PerfectClearCombo;
        public bool HasSelectiveBlindness; public float SelBlindFactor = 2f; public int SelBlindRemove = 2;
        public bool HasNeonCable; public float NeonScore = 1f; public int NeonA = -1, NeonB = -1;
        public bool HasSafeZone; public float SafeFloor = 2f; public int SafePenalty = 8; public int SafePos = -1;
        public bool HasDecayingRift; public int RiftInterval = 7, RiftCountdown = 5, RiftBonus = 2, RiftCounter, RiftPos = -1, RiftCount;
        public bool HasPhantomCell; public int PhantomPos = -1;
        public bool HasCornerStone; public float CornerStoneBonus;
        public bool HasCenterBase; public float CenterBaseBonus;
        public bool HasDoubleStrike; public int DoubleStrikeMinLines = 2; public float DoubleStrikeFactor = 1.5f;
        public bool HasGambler; public float GamblerChance = 0.2f, GamblerWin = 2f, GamblerLose = 0.5f;
        public bool HasPatient; public int PatientMin = 3; public float PatientFactor = 2f; public int NoClearRun;
        public bool HasCardCollector; public float CardCollectorPerCard;

        public bool RotationLocked => HasFirstPicks && FirstPicksUsed < FirstPicksFreeCount;

        public void OnMilestoneReached()
        {
            GhostDropUses = 0;
            HasFirstPicks = false; FirstPicksFreeCount = 0; FirstPicksUsed = 0;
            HasDietPlan = false; DietPlanMaxSize = 4; DietPlanScoreFactor = 1f;
        }
    }

    public enum Reason { None, NoMoves, CannotPlace, PoolExhausted, DemoEnd, Cap, Victory }

    /// <summary>Tek run'ın istatistikleri.</summary>
    public sealed class RunStats
    {
        public int Score, Stage, Placements, Clears, Lines, CardPicks, Coins;
        public float MaxCombo = 1f, ChainAcc;
        public Reason Reason;
        public bool DiedOnFreshPool;       // yeni gelen 3'lü havuz hiç sığmadı
        public bool RevivesUnused;         // ölümde kullanılmamış revive/kalkan hakkı vardı
        public int RevivesUsed;
        public List<int> StagePlacements = new(); // her stage'i geçmek için harcanan hamle
        public List<string> Picked = new();
        public int PoolRerollsUsed, PassesUsed;
        public int Offers, OffersNoop, AllNoopOffers, PicksNoop;
    }

    public sealed class Run
    {
        public readonly GameData G; public readonly Profile P; public readonly Config C; public readonly Rng R;

        public ulong Cells, Phantom, Dead;
        public readonly float[] Val = new float[64];
        public ulong Occ => Cells | Phantom | Dead;

        public readonly Combo Combo = new();
        public MilestoneSys MS;
        public readonly CardState CS = new();
        public readonly List<Piece> Pool = new(3);
        public int Score, Coins;
        public float Global = 1f;
        int coinBonus, cardDeadReroll, upgRevivesUsed, freeDead, passesLeft;
        bool passReach;
        public int PoolRerolls, CardRerolls;
        public readonly Dictionary<int, int> Inventory = new();
        public readonly float[] ShapeScoreBonus, ShapeWeightDelta;
        public readonly bool[] ShapeHasEffect;
        public bool GameOver; public Reason Why;
        bool pendingCard;
        public RunStats Stats = new();
        int windowStart;

        public Run(GameData g, Profile p, Config c, Rng r)
        {
            G = g; P = p; C = c; R = r;
            ShapeScoreBonus = new float[g.Shapes.Count];
            ShapeWeightDelta = new float[g.Shapes.Count];
            ShapeHasEffect = new bool[g.Shapes.Count];
        }

        public const ulong Col0 = 0x0101010101010101UL;
        public static ulong ColMask(int x) => Col0 << x;
        public static ulong Neighbors(ulong m) =>
            ((m << 1) & ~Col0) | ((m >> 1) & ~(Col0 << 7)) | (m << 8) | (m >> 8);

        static readonly ulong Corners = (1UL << 0) | (1UL << 7) | (1UL << 56) | (1UL << 63);
        static readonly ulong Center = (1UL << 27) | (1UL << 28) | (1UL << 35) | (1UL << 36);

        // ── NewRun ──────────────────────────────────────────────────────────
        public void NewRun()
        {
            int poolBonus = (int)Math.Round(P.Effect(G, "upgrade_pool_capacity"));
            MS = new MilestoneSys(G, G.PoolLimit + poolBonus) { GapScale = C.MilestoneGapScale };
            MS.OnReached = HandleMilestoneReached;
            passesLeft = C.MilestonePasses + (int)Math.Round(P.Effect(G, "upgrade_milestone_pass"));
            MS.OnExhausted = () =>
            {
                if (passesLeft > 0 && !MS.AllCleared && MS.Score >= C.PassMinPct * MS.NextThreshold) { passesLeft--; Stats.PassesUsed++; passReach = !C.PassGivesCoins; MS.ForceReach(C.PassGivesCoins); passReach = false; return; }
                OnGameOver(Reason.PoolExhausted);
            };
            Combo.OnReset = () => { if (CS.HasHyperfocus && CS.HyperfocusPenaltyShapes > 0) MS.DeductPieces(CS.HyperfocusPenaltyShapes); };
            if (C.Classic)
            {
                // Classic: pencere sayacı yok, run yalnızca hamle kalmayınca biter
                MS = new MilestoneSys(G, int.MaxValue / 2);
                MS.OnExhausted = null;
            }

            Combo.MaxCharge = Math.Max(1, 3 + (int)Math.Round(P.Effect(G, "upgrade_combo_bar")));
            Combo.Base = MathF.Max(1f, 1f + P.Effect(G, "upgrade_starting_combo"));
            Combo.BonusPerClear = 0.1f + P.Effect(G, "upgrade_combo_gain");
            Combo.Reset();
            Combo.Cap = C.ComboCap; Combo.DimK = C.ComboDimK; Combo.ChainMode = C.ChainMode;
            Combo.ChainCap = C.ChainCap; Combo.MaxFollowsBpc = C.MaxChargeFollowsBpc;

            PoolRerolls = (int)Math.Round(P.Effect(G, "upgrade_pool_reroll"));
            CardRerolls = (int)Math.Round(P.Effect(G, "upgrade_card_reroll"));
            GenerateNewPool();
        }

        // ── Spawn ───────────────────────────────────────────────────────────
        int ShapeWeight(ShapeDef s)
        {
            if (C.Classic) return s.BaseWeight;
            if (C.WeightOverride != null) return C.WeightOverride[s.Idx];
            int w = P.EffectiveWeight(s);
            if (ShapeHasEffect[s.Idx]) w = Math.Max(1, w + (int)Math.Round(ShapeWeightDelta[s.Idx]));
            return w;
        }

        bool SpawnAllowed(ShapeDef s) => C.Classic
            ? C.ClassicAllShapes || !s.Locked
            : P.ShapeAvailable(s) && (C.WeightOverride == null || C.WeightOverride[s.Idx] > 0) && (!CS.HasDietPlan || s.Size < CS.DietPlanMaxSize);

        Piece RandomPiece()
        {
            int total = 0;
            foreach (var s in G.Shapes) if (SpawnAllowed(s)) total += ShapeWeight(s);
            if (total <= 0) return null;
            int roll = R.Range(0, total), cum = 0;
            foreach (var s in G.Shapes)
            {
                if (!SpawnAllowed(s)) continue;
                cum += ShapeWeight(s);
                if (roll < cum && C.Classic) return new Piece { S = s, Tile = s.BaseTile, Rot = R.Range(0, 4) };
                if (roll < cum && C.TileOverride != null && C.TileOverride[s.Idx] > 0) return new Piece { S = s, Tile = C.TileOverride[s.Idx] };
                if (roll < cum) return new Piece { S = s, Tile = s.BaseTile + (s.TileAt(P.ScoreLvl[s.Idx]) - s.BaseTile) * C.GainScale };
            }
            return null;
        }

        /// <summary>Bot'un "sonraki parça sığar mı" tahmini için spawn olasılıkları.</summary>
        public void SpawnDistribution(List<(ShapeDef s, float p)> outList)
        {
            outList.Clear();
            int total = 0;
            foreach (var s in G.Shapes) if (SpawnAllowed(s)) total += ShapeWeight(s);
            foreach (var s in G.Shapes) if (SpawnAllowed(s)) outList.Add((s, ShapeWeight(s) / (float)total));
        }

        public void GenerateNewPool(bool skipValidCheck = false)
        {
            Pool.Clear();
            for (int i = 0; i < 3; i++)
            {
                var p = RandomPiece();
                for (int t = 0; t < C.ClassicFitRetries && p != null && !BoardEval.AnyFits(Occ, MasksFor(p)); t++) p = RandomPiece();
                if (p != null) Pool.Add(p);
            }
            if (CS.HasNeonCable) ResetNeonCablePositions();
            if (!skipValidCheck && !HasAnyValidMove())
                OnGameOver(CS.RotationLocked ? Reason.CannotPlace : Reason.NoMoves);
        }

        public ulong[] MasksFor(Piece p) => C.Classic && !C.ClassicRotate ? p.S.Masks[p.Rot] : CS.RotationLocked ? p.S.Masks[0] : p.S.Distinct;

        public bool HasAnyValidMove()
        {
            ulong occ = Occ;
            foreach (var p in Pool)
                foreach (var m in MasksFor(p))
                    if ((m & occ) == 0) return true;
            return false;
        }

        void OnGameOver(Reason reason)
        {
            if (GameOver) return;
            if (reason != Reason.PoolExhausted && Pool.Count > 0 && HasAnyValidMove()) return;
            GameOver = true; Why = reason;
        }

        // ── Placement (RunController.DoPlace) ───────────────────────────────
        public void DoPlace(int pi, ulong mask)
        {
            var piece = Pool[pi];
            if ((mask & Occ) != 0) throw new InvalidOperationException("invalid placement");

            bool isGhostDrop = CS.HasGhostDrop && CS.GhostDropUses < CS.GhostDropMaxUses && (Neighbors(mask) & ~mask & Occ) == 0;
            bool isFirstPick = CS.HasFirstPicks && CS.FirstPicksUsed < CS.FirstPicksFreeCount;
            bool skip = isGhostDrop || isFirstPick;

            if (CS.HasSafeZone && CS.SafePos >= 0 && (mask & (1UL << CS.SafePos)) != 0)
                Combo.SetComboFloor(CS.SafeFloor);

            float eff = piece.Tile + ShapeScoreBonus[piece.S.Idx];
            Cells |= mask;
            for (ulong m = mask; m != 0; m &= m - 1)
            {
                int b = BitOperations.TrailingZeroCount(m);
                float bonus = 0f;
                if (CS.HasCornerStone && (Corners & (1UL << b)) != 0) bonus += CS.CornerStoneBonus;
                if (CS.HasCenterBase && (Center & (1UL << b)) != 0) bonus += CS.CenterBaseBonus;
                Val[b] = eff + bonus;
            }
            Stats.Placements++;

            // LineClearSystem.ClearLines
            ulong occ = Occ, rowsMask = 0, colsMask = 0;
            int rows = 0, cols = 0;
            for (int y = 0; y < 8; y++) if (((occ >> (8 * y)) & 0xFF) == 0xFF) { rows |= 1 << y; rowsMask |= 0xFFUL << (8 * y); }
            for (int x = 0; x < 8; x++) if ((occ & ColMask(x)) == ColMask(x)) { cols |= 1 << x; colsMask |= ColMask(x); }
            int cleared = BitOperations.PopCount((uint)rows) + BitOperations.PopCount((uint)cols);
            ulong U = rowsMask | colsMask;
            float tileSum = 0f, rowSum = 0f, colSum = 0f;
            for (ulong m = U; m != 0; m &= m - 1)
            {
                int b = BitOperations.TrailingZeroCount(m);
                tileSum += Val[b];
                if ((rowsMask & (1UL << b)) != 0) rowSum += Val[b];
                if ((colsMask & (1UL << b)) != 0) colSum += Val[b];
            }
            ulong clr = U & ~Dead;
            Cells &= ~clr;
            for (ulong m = clr; m != 0; m &= m - 1) Val[BitOperations.TrailingZeroCount(m)] = 0f;

            if (cleared > 0)
            {
                Stats.Clears++; Stats.Lines += cleared;
                Combo.OnLineClear(cleared);
                if (CS.HasBountyHunter && CS.BountyHunterCoinPerClear > 0)
                {
                    float share = C.BountyPct > 0f ? C.BountyPct : CS.BountyShare;
                    int per = share > 0f && !MS.AllCleared
                        ? Math.Max(1, (int)Math.Round(share * G.Milestones[MS.Index].Coin))
                        : CS.BountyHunterCoinPerClear;
                    int bc = cleared * per;
                    Coins += bc; CS.BountyCoins += bc;
                }
                if (CS.HasPhantomCell && CS.PhantomPos >= 0) CheckPhantomRelocate(rows, cols);
                if (CS.HasSafeZone && CS.SafePos >= 0) CheckSafeZoneCleared(rows, cols);
                if (CS.HasDecayingRift && CS.RiftPos >= 0) CheckRiftCleared(rows, cols);
                if (CS.HasPerfectClear && Cells == 0) HandlePerfectClear();
            }

            Combo.OnPlacement(cleared > 0);
            if (Combo.Mult > Stats.MaxCombo) Stats.MaxCombo = Combo.Mult;

            if (isGhostDrop) CS.GhostDropUses++;
            if (isFirstPick) CS.FirstPicksUsed++;

            float effGlobal = Global;
            if (CS.HasCardCollector) effGlobal += Inventory.Count * CS.CardCollectorPerCard;

            float effSum = tileSum;
            if (cleared > 0)
            {
                if (CS.HasTunnelVision) effSum = colSum * CS.TunnelVisionMultiplier;
                else if (CS.HasLineMaster) effSum = rowSum * CS.LineMasterMultiplier;
                if (CS.HasDietPlan) effSum *= CS.DietPlanScoreFactor;
                if (CS.HasSlowBurn)
                {
                    if (MS.Placed < CS.SlowBurnEarlyCount) effSum *= CS.SlowBurnEarlyFactor;
                    else if (MS.Remaining <= CS.SlowBurnLateCount) effSum *= CS.SlowBurnLateFactor;
                }
                if (CS.HasSelectiveBlindness)
                {
                    if (cleared >= 2) effSum = 0f; else effSum *= CS.SelBlindFactor;
                }
            }

            int gained = effSum <= 0f ? 0 : (int)Math.Round(effSum * Combo.Mult * effGlobal);
            if (CS.HasNeonCable && cleared > 0) gained += HandleNeonCable(rows, cols);
            if (CS.HasSelectiveBlindness && cleared == 1) RemoveRandomFilledBlocks(CS.SelBlindRemove);
            if (CS.HasDoubleStrike && cleared >= CS.DoubleStrikeMinLines) gained = (int)Math.Round(gained * CS.DoubleStrikeFactor);
            if (CS.HasGambler && R.Value() < CS.GamblerChance)
            {
                bool won = R.Value() < 0.5f;
                gained = (int)Math.Round(gained * (won ? CS.GamblerWin : CS.GamblerLose));
            }
            if (cleared == 0) CS.NoClearRun++;
            else
            {
                if (CS.HasPatient && CS.NoClearRun >= CS.PatientMin) gained = (int)Math.Round(gained * CS.PatientFactor);
                CS.NoClearRun = 0;
            }

            Score += gained;
            if (!C.Classic) MS.OnScoreChanged(Score);

            Pool.RemoveAt(pi);
            if (Pool.Count == 0)
            {
                bool wasOver = GameOver;
                GenerateNewPool(skipValidCheck: !C.Legacy);
                if (!wasOver && GameOver) Stats.DiedOnFreshPool = true;
            }

            if (CS.HasDecayingRift) UpdateDecayingRift();

            if (!HasAnyValidMove()) { HandleDeadPool(); return; }
            if (!skip) MS.OnPiecePlaced();
        }

        public void PoolReroll()
        {
            if (PoolRerolls <= 0) return;
            PoolRerolls--; Stats.PoolRerollsUsed++;
            MS.EnsureMinimumRemaining(6);
            if (C.Legacy) { GenerateNewPool(); return; }
            GenerateNewPool(skipValidCheck: true);
            if (!HasAnyValidMove()) HandleDeadPool();
        }

        bool HasReviveLeft()
        {
            int reviveLevel = (int)Math.Round(P.Effect(G, "upgrade_dead_pool_revive"));
            return (reviveLevel > 0 && upgRevivesUsed < reviveLevel) || freeDead > 0 ||
                   (cardDeadReroll > 0 && Score >= 300) ||
                   (CS.HasMomentumShield && Combo.Mult >= CS.MomentumShieldMin);
        }

        public void HandleDeadPool()
        {
            if (GameOver)
            {
                if (HasReviveLeft()) Stats.RevivesUnused = true;
                if (!C.Legacy) return;   // yeni kod: bitmiş run'da hak harcanmaz
            }
            if (CS.RotationLocked) { OnGameOver(Reason.CannotPlace); return; }
            int reviveLevel = (int)Math.Round(P.Effect(G, "upgrade_dead_pool_revive"));
            if (reviveLevel > 0 && upgRevivesUsed < reviveLevel)
            {
                upgRevivesUsed++; Stats.RevivesUsed++;
                Rescue();
                return;
            }
            if (freeDead > 0)
            {
                freeDead--;
                Rescue();
                return;
            }
            if (cardDeadReroll > 0 && Score >= 300)
            {
                cardDeadReroll--; Score -= 300; Stats.RevivesUsed++;
                Rescue();
                return;
            }
            if (CS.HasMomentumShield && Combo.Mult >= CS.MomentumShieldMin)
            {
                Stats.RevivesUsed++;
                Combo.ForceResetToBase();
                Rescue();
                return;
            }
            OnGameOver(Reason.NoMoves);
        }

        /// <summary>RunController.TryRescue (eski kod: kontrollü üretim, tek deneme).</summary>
        void Rescue()
        {
            MS.EnsureMinimumRemaining(6);
            if (C.Legacy)
            {
                GenerateNewPool();
                if (!HasAnyValidMove()) OnGameOver(Reason.NoMoves);
                return;
            }
            GenerateNewPool(skipValidCheck: true);
            if (!HasAnyValidMove()) HandleDeadPool();
        }

        // ── Milestone ───────────────────────────────────────────────────────
        void HandleMilestoneReached(int coinReward, int idx)
        {
            var data = G.Milestones[idx];
            CS.OnMilestoneReached();
            Dead = 0;
            if (CS.HasFutureInvestment && CS.FutureInvestmentPendingDiscount > 0f)
            {
                if (C.FutureInvestmentGift && !MS.AllCleared)
                {
                    // öneri: indirim borç değil hediye — yeni aşamanın gereken farkının o kadarı skor olarak verilir
                    float gap = (G.Milestones[MS.Index].Threshold - data.Threshold) * MS.PermScale;
                    Score += (int)Math.Round(gap * CS.FutureInvestmentPendingDiscount);
                }
                else MS.ScaleCurrentWindow(1f - CS.FutureInvestmentPendingDiscount);
                CS.FutureInvestmentPendingDiscount = 0f;
            }
            if (CS.HasHoarder && MS.LastRemaining >= CS.HoarderMinRemaining)
                MS.SetPoolLimit(MS.Limit + CS.HoarderPoolBonus);

            float coinMult = 1f + P.Effect(G, "upgrade_coin_gain");
            if (C.CoinOverride != null && idx < C.CoinOverride.Length) coinReward = C.CoinOverride[idx];
            int total = passReach ? 0 : (int)Math.Round((coinReward + coinBonus) * coinMult);   // Lifeline ile geçilen aşama coin vermez
            Coins += total;

            Stats.Stage = idx + 1;
            Stats.StagePlacements.Add(Stats.Placements - windowStart);
            windowStart = Stats.Placements;

            if (C.Demo && data.Threshold >= C.DemoThreshold)
            {
                GameOver = true; Why = Reason.DemoEnd;
                return;
            }

            // RunController: sabit aşamalı kilitli kartlar bu aşamada (ya da öncesinde) açılır
            foreach (var lc in G.Cards)
                if (lc.Locked && lc.UnlockAtStage > 0 && lc.UnlockAtStage <= MS.Index) P.UnlockedCards.Add(lc.Id);
            if (P.Reached.Add(data.Label))
            {
                if (MS.Index > P.MaxMilestoneReached) P.MaxMilestoneReached = MS.Index;
            }
            // RunController.OnVictory: son aşamada kart yok, run zaferle biter
            if (MS.AllCleared && C.StopAtVictory) { GameOver = true; Why = Reason.Victory; return; }
            pendingCard = true;
        }

        public bool PendingCard => pendingCard;

        // ── Kart seçimi (CardSelectionUI) ───────────────────────────────────
        public bool CardAvailable(CardDef c)
        {
            if (c.Locked && !P.UnlockedCards.Contains(c.Id)) return false;
            if (!string.IsNullOrEmpty(c.RequiredShapeId))
            {
                var s = G.Shapes[G.ShapeIndex[c.RequiredShapeId]];
                bool ok = P.ShapeUnlocked[s.Idx] || (!C.Legacy && !s.Locked);
                if (!ok) return false;
            }
            if (c.IsShape && !string.IsNullOrEmpty(c.TargetShapeId))
            {
                var s = G.Shapes[G.ShapeIndex[c.TargetShapeId]];
                if (!P.ShapeUnlocked[s.Idx] && s.Locked) return false;
            }
            if (c.Unique && Inventory.ContainsKey(c.Idx)) return false;
            foreach (var (t, _) in c.Effects)
            {
                if (t == E.TunnelVisionMultiplier && CS.HasLineMaster) return false;
                if (t == E.LineMasterMultiplier && CS.HasTunnelVision) return false;
            }
            return true;
        }

        // Bu efektler değeri biriktirmez, yalnızca bayrak/sabit atar: kart
        // ikinci kez alınınca hiçbir şey değişmez (IsUnique = false olsa da).
        static readonly HashSet<E> FlagEffects = new()
        {
            E.DoubleStrikeBonus, E.PatientBonus, E.LineMasterMultiplier, E.TunnelVisionMultiplier,
            E.MomentumShieldMinMultiplier, E.PerfectClearCoinReward, E.PhantomCellEnabled, E.RiftSpawnInterval,
            E.SoftLandingFactor, E.ComboShield, E.GamblerRoll
        };

        public static bool IsNoopDuplicate(Run r, CardDef c) =>
            !c.IsShape && r.Inventory.ContainsKey(c.Idx) && c.Effects.Count > 0 && c.Effects.All(e => FlagEffects.Contains(e.type) ||
                e.type == E.PerfectClearComboBoost || e.type == E.RiftCountdownStart || e.type == E.RiftBonusShapes);

        public List<CardDef> OfferCards(HashSet<int> excludeShown)
        {
            var pool = G.Cards.Where(c => CardAvailable(c) && (excludeShown == null || !excludeShown.Contains(c.Idx))).ToList();
            var res = new List<CardDef>(3);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int total = pool.Sum(c => Math.Max(1, c.BaseWeight));
                int roll = R.Range(0, total), cum = 0;
                for (int j = 0; j < pool.Count; j++)
                {
                    cum += Math.Max(1, pool[j].BaseWeight);
                    if (roll < cum) { res.Add(pool[j]); pool.RemoveAt(j); break; }
                }
            }
            return res;
        }

        public void ResolveCardSelection(IBot bot)
        {
            pendingCard = false;
            var shown = new HashSet<int>();
            var offer = OfferCards(null);
            foreach (var c in offer) shown.Add(c.Idx);
            Stats.Offers += offer.Count;
            Stats.OffersNoop += offer.Count(c => IsNoopDuplicate(this, c));
            if (offer.Count > 0 && offer.All(c => IsNoopDuplicate(this, c))) Stats.AllNoopOffers++;
            int rerolls = C.Legacy ? 0 : CardRerolls;
            while (true)
            {
                if (offer.Count == 0) return;
                int pick = bot.PickCard(this, offer, rerolls > 0);
                if (pick < 0 && rerolls > 0)
                {
                    rerolls--; CardRerolls--;
                    offer = OfferCards(shown);
                    foreach (var c in offer) shown.Add(c.Idx);
                    continue;
                }
                if (pick < 0) pick = 0;
                ApplyCard(offer[pick]);
                return;
            }
        }

        public void ApplyCard(CardDef card)
        {
            Stats.CardPicks++;
            if (IsNoopDuplicate(this, card)) Stats.PicksNoop++;
            Stats.Picked.Add(card.Id);
            bool hadNeon = CS.HasNeonCable, hadSafe = CS.HasSafeZone, hadPhantom = CS.HasPhantomCell;
            if (card.IsShape)
            {
                var si = G.ShapeIndex[card.TargetShapeId];
                ShapeHasEffect[si] = true;
                ShapeScoreBonus[si] += card.ShapeScore;
                ShapeWeightDelta[si] += card.ShapeWeight;
            }
            else
            {
                foreach (var (t, v) in card.Effects)
                {
                    switch (t)
                    {
                        case E.ComboBonusPerClear: Combo.BonusPerClear += v; break;
                        case E.ScoreMultiplierBonus:
                        case E.LineScoreBonus: Global += v; break;
                        case E.ExtraDeadPoolReroll: cardDeadReroll += (int)Math.Round(v); break;
                        case E.PoolLimitBonus: MS.SetPoolLimit(MS.Limit + (int)Math.Round(v)); break;
                        case E.HeavyLoad: MS.SetPoolLimit(MS.Limit + (int)Math.Round(v)); Global -= 0.10f; break;
                        case E.CoinBonusOnMilestone: coinBonus += (int)Math.Round(v); break;
                        case E.DietPlanMaxSize: CS.HasDietPlan = true; CS.DietPlanMaxSize = (int)Math.Round(v); break;
                        case E.DietPlanScoreFactor: CS.HasDietPlan = true; CS.DietPlanScoreFactor = v; break;
                        case E.GhostDropMaxUses: CS.HasGhostDrop = true; CS.GhostDropMaxUses = (int)Math.Round(v); break;
                        case E.SoftLandingFactor: Combo.SetSoftLanding(v); break;
                        case E.SlowBurnEarlyCount: CS.HasSlowBurn = true; CS.SlowBurnEarlyCount = (int)Math.Round(v); break;
                        case E.SlowBurnEarlyFactor: CS.HasSlowBurn = true; CS.SlowBurnEarlyFactor = v; break;
                        case E.SlowBurnLateCount: CS.HasSlowBurn = true; CS.SlowBurnLateCount = (int)Math.Round(v); break;
                        case E.SlowBurnLateFactor: CS.HasSlowBurn = true; CS.SlowBurnLateFactor = v; break;
                        case E.HyperfocusComboMultiplier:
                            CS.HasHyperfocus = true; Combo.BonusPerClear *= v; Combo.BonusAtMax *= v; break;
                        case E.HyperfocusPenaltyShapes: CS.HasHyperfocus = true; CS.HyperfocusPenaltyShapes = (int)Math.Round(v); break;
                        case E.TunnelVisionMultiplier: CS.HasTunnelVision = true; CS.TunnelVisionMultiplier = v; break;
                        case E.LineMasterMultiplier: CS.HasLineMaster = true; CS.LineMasterMultiplier = v; break;
                        case E.BountyHunterCoinPerClear: CS.HasBountyHunter = true; if (v >= 1f) CS.BountyHunterCoinPerClear = (int)Math.Round(v); else { CS.BountyHunterCoinPerClear = 1; CS.BountyShare = v; } break;
                        case E.BountyHunterThresholdScale: CS.HasBountyHunter = true; MS.ScalePermanent(v); break;
                        case E.FutureInvestmentCurrentScale: CS.HasFutureInvestment = true; MS.ScaleCurrentWindow(v); break;
                        case E.FutureInvestmentNextDiscount: CS.HasFutureInvestment = true; CS.FutureInvestmentPendingDiscount += v; break;
                        case E.FirstPicksFreeCount: CS.HasFirstPicks = true; CS.FirstPicksFreeCount = (int)Math.Round(v); break;
                        case E.MomentumShieldMinMultiplier: CS.HasMomentumShield = true; CS.MomentumShieldMin = v; break;
                        case E.HoarderMinRemaining: CS.HasHoarder = true; CS.HoarderMinRemaining = (int)Math.Round(v); break;
                        case E.HoarderPoolBonus: CS.HasHoarder = true; CS.HoarderPoolBonus = (int)Math.Round(v); break;
                        case E.PerfectClearCoinReward: CS.HasPerfectClear = true; CS.PerfectClearCoin = (int)Math.Round(v); break;
                        case E.PerfectClearComboBoost: CS.HasPerfectClear = true; CS.PerfectClearCombo = v; break;
                        case E.SelectiveBlindnessSingleFactor: CS.HasSelectiveBlindness = true; CS.SelBlindFactor = v; break;
                        case E.SelectiveBlindnessRemoveCount: CS.HasSelectiveBlindness = true; CS.SelBlindRemove = (int)Math.Round(v); break;
                        case E.NeonCableExplosionScore: CS.HasNeonCable = true; CS.NeonScore = v; break;
                        case E.SafeZoneComboFloor: CS.HasSafeZone = true; CS.SafeFloor = v; break;
                        case E.SafeZonePenalty: CS.HasSafeZone = true; CS.SafePenalty = (int)Math.Round(v); break;
                        case E.RiftSpawnInterval: CS.HasDecayingRift = true; CS.RiftInterval = Math.Max(1, (int)Math.Round(v)); break;
                        case E.RiftCountdownStart: CS.HasDecayingRift = true; CS.RiftCountdown = Math.Max(1, (int)Math.Round(v)); break;
                        case E.RiftBonusShapes: CS.HasDecayingRift = true; CS.RiftBonus = (int)Math.Round(v); break;
                        case E.PhantomCellEnabled: CS.HasPhantomCell = true; break;
                        case E.CornerStoneBonus: CS.HasCornerStone = true; CS.CornerStoneBonus += v; break;
                        case E.CenterBaseBonus: CS.HasCenterBase = true; CS.CenterBaseBonus += v; break;
                        case E.DoubleStrikeBonus: CS.HasDoubleStrike = true; break;
                        case E.GamblerRoll: CS.HasGambler = true; break;
                        case E.PatientBonus: CS.HasPatient = true; break;
                        case E.CardCollector: CS.HasCardCollector = true; CS.CardCollectorPerCard += v; break;
                        case E.ComboShield: Combo.HasShield = true; break;
                        case E.ChainMaster: Combo.HasChain = true; Combo.ChainBonus += v; break;
                    }
                }
            }
            if (CS.HasNeonCable && !hadNeon) ResetNeonCablePositions();
            if (CS.HasSafeZone && !hadSafe) CS.SafePos = PickRandomEmptyCell(Reserved());
            if (CS.HasPhantomCell && !hadPhantom)
            {
                int pos = PickRandomEmptyCell(Reserved());
                if (pos >= 0) { CS.PhantomPos = pos; Phantom |= 1UL << pos; }
            }
            Inventory.TryGetValue(card.Idx, out int n);
            Inventory[card.Idx] = n + 1;
        }

        // ── Overlay kartları ────────────────────────────────────────────────
        ulong Reserved()
        {
            ulong r = 0;
            if (CS.NeonA >= 0) r |= 1UL << CS.NeonA;
            if (CS.NeonB >= 0) r |= 1UL << CS.NeonB;
            if (CS.SafePos >= 0) r |= 1UL << CS.SafePos;
            if (CS.PhantomPos >= 0) r |= 1UL << CS.PhantomPos;
            if (CS.RiftPos >= 0) r |= 1UL << CS.RiftPos;
            return r;
        }

        readonly List<int> tmp = new(64);
        List<int> Empties(ulong exclude)
        {
            tmp.Clear();
            ulong occ = Occ;
            for (int b = 0; b < 64; b++)
                if (((occ | exclude) & (1UL << b)) == 0) tmp.Add(b);
            return tmp;
        }

        int PickRandomEmptyCell(ulong exclude)
        {
            var e = Empties(exclude);
            return e.Count == 0 ? -1 : e[R.Range(0, e.Count)];
        }

        void ResetNeonCablePositions()
        {
            ulong res = Reserved();
            if (CS.NeonA >= 0) res &= ~(1UL << CS.NeonA);
            if (CS.NeonB >= 0) res &= ~(1UL << CS.NeonB);
            var e = Empties(res);
            if (e.Count < 2) { CS.NeonA = -1; CS.NeonB = -1; return; }
            int iA = R.Range(0, e.Count);
            CS.NeonA = e[iA]; e.RemoveAt(iA);
            CS.NeonB = e[R.Range(0, e.Count)];
        }

        static bool Hit(int pos, int rows, int cols) => ((rows >> (pos / 8)) & 1) != 0 || ((cols >> (pos % 8)) & 1) != 0;

        int HandleNeonCable(int rows, int cols)
        {
            if (CS.NeonA < 0 || CS.NeonB < 0) return 0;
            bool a = Hit(CS.NeonA, rows, cols), b = Hit(CS.NeonB, rows, cols);
            if (!a && !b) return 0;
            int bonus = 0;
            if (a && !b) bonus = Explode(CS.NeonB);
            else if (b && !a) bonus = Explode(CS.NeonA);
            ResetNeonCablePositions();
            return bonus;
        }

        int Explode(int center)
        {
            int cx = center % 8, cy = center / 8;
            float sum = 0f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= 8 || y >= 8) continue;
                int b = y * 8 + x;
                if ((Dead & (1UL << b)) != 0) continue;
                sum += Val[b];
                Cells &= ~(1UL << b); Val[b] = 0f;
            }
            return (int)Math.Round(sum * CS.NeonScore * Combo.Mult * Global);
        }

        void CheckSafeZoneCleared(int rows, int cols)
        {
            if (!Hit(CS.SafePos, rows, cols)) return;
            CS.SafePos = -1;
            Combo.SetComboFloor(0f);
            MS.DeductPieces(CS.SafePenalty);
        }

        void CheckRiftCleared(int rows, int cols)
        {
            if (!Hit(CS.RiftPos, rows, cols)) return;
            CS.RiftPos = -1; CS.RiftCount = 0;
            MS.AddPieces(CS.RiftBonus);
        }

        void UpdateDecayingRift()
        {
            if (CS.RiftPos >= 0)
            {
                CS.RiftCount--;
                if (CS.RiftCount <= 0)
                {
                    int rp = CS.RiftPos;
                    Dead |= 1UL << rp;
                    if (!HasAnyValidMove())
                    {
                        Dead &= ~(1UL << rp);
                        CS.RiftCount = 1;
                        return;
                    }
                    CS.RiftPos = -1; CS.RiftCount = 0;
                }
            }
            CS.RiftCounter++;
            if (CS.RiftCounter >= CS.RiftInterval && CS.RiftPos < 0)
            {
                CS.RiftCounter = 0;
                int pos = PickRandomEmptyCell(Reserved());
                if (pos >= 0) { CS.RiftPos = pos; CS.RiftCount = CS.RiftCountdown; }
            }
        }

        void CheckPhantomRelocate(int rows, int cols)
        {
            int pp = CS.PhantomPos;
            if (!Hit(pp, rows, cols)) return;
            Phantom &= ~(1UL << pp);
            ulong res = Reserved() & ~(1UL << pp);
            int pos = PickRandomEmptyCell(res);
            if (pos >= 0) { CS.PhantomPos = pos; Phantom |= 1UL << pos; }
            else CS.PhantomPos = -1;
        }

        void HandlePerfectClear()
        {
            if (CS.PerfectClearCoin > 0) Coins += CS.PerfectClearCoin;
            Combo.ForceMaxCharge();
            if (CS.PerfectClearCombo > 0f) Combo.AddMultiplier(CS.PerfectClearCombo);
        }

        void RemoveRandomFilledBlocks(int count)
        {
            tmp.Clear();
            for (int b = 0; b < 64; b++) if ((Cells & (1UL << b)) != 0) tmp.Add(b);
            for (int i = 0; i < count && tmp.Count > 0; i++)
            {
                int idx = R.Range(0, tmp.Count);
                int b = tmp[idx]; tmp.RemoveAt(idx);
                Cells &= ~(1UL << b); Val[b] = 0f;
            }
        }

        // ── Sürücü ──────────────────────────────────────────────────────────
        public RunStats Play(IBot bot)
        {
            NewRun();
            while (!GameOver)
            {
                if (Stats.Placements >= C.MaxPlacements) { GameOver = true; Why = Reason.Cap; break; }
                if (PoolRerolls > 0 && bot.WantPoolReroll(this)) { PoolReroll(); continue; }
                var mv = bot.Choose(this);
                if (mv.Piece < 0) { HandleDeadPool(); continue; }
                DoPlace(mv.Piece, mv.Mask);
                if (pendingCard && !GameOver) ResolveCardSelection(bot);
                pendingCard = false;
                // CheckForSoftLock
                if (!GameOver && Pool.Count > 0 && !HasAnyValidMove()) HandleDeadPool();
            }
            Stats.Score = Score;
            Stats.Coins = Coins;
            Stats.Reason = Why;
            Stats.Stage = MS.Index;
            Stats.ChainAcc = Combo.MaxChainAcc;
            return Stats;
        }
    }

    public struct Move { public int Piece; public ulong Mask; }

    public interface IBot
    {
        Move Choose(Run r);
        int PickCard(Run r, List<CardDef> offer, bool canReroll);
        bool WantPoolReroll(Run r);
    }
}
