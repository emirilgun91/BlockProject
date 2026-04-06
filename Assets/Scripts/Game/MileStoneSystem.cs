using System;
using RogueBlockBlast.Content;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Milestone ve pool limit mantığını yönetir.
    /// Unity bağımlılığı yok — pure C#.
    ///
    /// AKIŞ:
    /// 1. Her placement → OnPiecePlaced()
    /// 2. Her score değişimi → OnScoreChanged()
    /// 3. Pool limit bitince → OnPoolLimitExhausted event
    /// 4. Milestone geçilince → OnMilestoneReached event
    ///    · Coin verilir
    ///    · Pool limit sıfırlanır
    ///    · Kart seçim ekranı açılır (event dinleyicisi halleder)
    /// </summary>
    public sealed class MilestoneSystem
    {
        // ── Config ───────────────────────────────────────────────────────────
        private readonly MilestoneConfigSO _config;

        // ── State ────────────────────────────────────────────────────────────
        public int  CurrentMilestoneIndex { get; private set; } = 0;
        public int  PiecesPlacedInWindow  { get; private set; } = 0;
        public int  PiecesRemaining       => Mathf.Max(0, _config.PoolLimit - PiecesPlacedInWindow);
        public bool AllMilestonesCleared  => CurrentMilestoneIndex >= _config.TotalMilestones;

        // Sonraki milestone — null ise tüm milestone'lar geçildi
        public MilestoneData? NextMilestone => _config.GetMilestone(CurrentMilestoneIndex);

        // ── Events ───────────────────────────────────────────────────────────
        /// <summary>Milestone'a ulaşıldı. int=kazanılan coin, MilestoneData=milestone bilgisi.</summary>
        public event Action<int, MilestoneData> OnMilestoneReached;

        /// <summary>Pool limit bitti, milestone'a ulaşılamadı → game over tetiklenebilir.</summary>
        public event Action OnPoolLimitExhausted;

        /// <summary>UI güncellemesi için — her state değişiminde ateşlenir.</summary>
        public event Action<MilestoneProgressState> OnProgressChanged;

        // ── Constructor ──────────────────────────────────────────────────────
        public MilestoneSystem(MilestoneConfigSO config)
        {
            _config = config;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Her piece yerleştirmesinde çağır.</summary>
        public void OnPiecePlaced()
        {
          
            PiecesPlacedInWindow++;
            FireProgressChanged();
            
            if (PiecesRemaining <= 0)
                OnPoolLimitExhausted?.Invoke();
        }

        /// <summary>Her score güncellemesinde çağır.</summary>
        public void OnScoreChanged(int newScore)
        {
            if (AllMilestonesCleared) return;

            var milestone = NextMilestone;
            if (!milestone.HasValue) return;

            if (newScore >= milestone.Value.ScoreThreshold)
                TriggerMilestone(milestone.Value);
        }

        /// <summary>Run başında sıfırlar.</summary>
        public void Reset()
        {
            CurrentMilestoneIndex = 0;
            PiecesPlacedInWindow  = 0;
            FireProgressChanged();
        }

        /// <summary>
        /// Sadece piece counter'ı sıfırlar — milestone index değişmez.
        /// Dead pool reroll kullanılınca çağır.
        /// </summary>
        public void ResetPieceCounter()
        {
            PiecesPlacedInWindow = 0;
            FireProgressChanged();
        }

        // ── Private ──────────────────────────────────────────────────────────
        private void TriggerMilestone(MilestoneData data)
        {
            CurrentMilestoneIndex++;
            PiecesPlacedInWindow = 0; // pool limit sıfırla

            OnMilestoneReached?.Invoke(data.CoinReward, data);
            FireProgressChanged();
        }

        private void FireProgressChanged()
        {
            var next = NextMilestone;
            OnProgressChanged?.Invoke(new MilestoneProgressState(
                currentMilestone : CurrentMilestoneIndex,
                totalMilestones  : _config.TotalMilestones,
                piecesRemaining  : PiecesRemaining,
                poolLimit        : _config.PoolLimit,
                nextThreshold    : next?.ScoreThreshold ?? 0,
                nextLabel        : next?.Label ?? "MAX",
                allCleared       : AllMilestonesCleared
            ));
        }
        public void EnsureMinimumRemaining(int minimum = 6)
        {
            int current = PiecesRemaining;
            if (current < minimum)
                PiecesPlacedInWindow = Mathf.Max(0, _config.PoolLimit - minimum);
            // current >= minimum ise hiç dokunmuyoruz
            FireProgressChanged();
        }
    }

    /// <summary>UI'ya gönderilen snapshot.</summary>
    public readonly struct MilestoneProgressState
    {
        public readonly int    CurrentMilestone;
        public readonly int    TotalMilestones;
        public readonly int    PiecesRemaining;
        public readonly int    PoolLimit;
        public readonly int    NextThreshold;
        public readonly string NextLabel;
        public readonly bool   AllCleared;

        public MilestoneProgressState(
            int currentMilestone, int totalMilestones,
            int piecesRemaining,  int poolLimit,
            int nextThreshold,    string nextLabel,
            bool allCleared)
        {
            CurrentMilestone = currentMilestone;
            TotalMilestones  = totalMilestones;
            PiecesRemaining  = piecesRemaining;
            PoolLimit        = poolLimit;
            NextThreshold    = nextThreshold;
            NextLabel        = nextLabel;
            AllCleared       = allCleared;
        }

        /// <summary>Pool limit progress — 0..1 arası, UI progress bar için.</summary>
        public float PoolFillRatio => PoolLimit > 0
            ? 1f - (float)PiecesRemaining / PoolLimit
            : 1f;
    }
   
}