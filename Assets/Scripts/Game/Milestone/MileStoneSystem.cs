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
        private int _poolLimitOverride; // 0 = config'den oku — mutable

        // ── Threshold scaling ────────────────────────────────────────────────
        private float _currentWindowScale = 1f;  // Future Investment — milestone başında sıfırlanır
        private float _permanentScale     = 1f;  // Bounty Hunter — run boyunca kalıcı

        // ── State ────────────────────────────────────────────────────────────
        public int  CurrentMilestoneIndex        { get; private set; } = 0;
        public int  PiecesPlacedInWindow         { get; private set; } = 0;
        public int  CurrentScore                 { get; private set; } = 0;
        public int  LastCompletedPiecesRemaining { get; private set; } = 0;

        public int EffectivePoolLimit =>
            _poolLimitOverride > 0 ? _poolLimitOverride : _config.PoolLimit;

        public int  PiecesRemaining       => Mathf.Max(0, EffectivePoolLimit - PiecesPlacedInWindow);
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
        public MilestoneSystem(MilestoneConfigSO config, int poolLimitOverride = 0)
        {
            _config            = config;
            _poolLimitOverride = poolLimitOverride;
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
            CurrentScore = newScore;
            if (AllMilestonesCleared) return;

            var milestone = NextMilestone;
            if (!milestone.HasValue) return;

            if (newScore >= milestone.Value.ScoreThreshold * _currentWindowScale * _permanentScale)
                TriggerMilestone(milestone.Value);
            else
                FireProgressChanged();
        }

        /// <summary>Run başında sıfırlar — pool limit korunur.</summary>
        public void Reset()
        {
            CurrentMilestoneIndex        = 0;
            PiecesPlacedInWindow         = 0;
            CurrentScore                 = 0;
            LastCompletedPiecesRemaining = 0;
            _currentWindowScale          = 1f;
            _permanentScale              = 1f;
            FireProgressChanged();
        }

        /// <summary>Future Investment: mevcut milestone eşiğini ölçekle.</summary>
        public void ScaleCurrentWindow(float scale) { _currentWindowScale *= scale; FireProgressChanged(); }

        /// <summary>Bounty Hunter: tüm milestone eşiklerine kalıcı çarpan ekle.</summary>
        public void ScalePermanent(float scale) { _permanentScale *= scale; FireProgressChanged(); }

        /// <summary>Hyperfocus cezası: pool counter'ı artır, gerekirse exhausted tetikle.</summary>
        public void DeductPieces(int count)
        {
            PiecesPlacedInWindow = Mathf.Min(PiecesPlacedInWindow + count, EffectivePoolLimit);
            FireProgressChanged();
            if (PiecesRemaining <= 0)
                OnPoolLimitExhausted?.Invoke();
        }

        /// <summary>Pool limit'i güncelle — upgrade değişince NewRun'da çağır.</summary>
        public void SetPoolLimit(int limit) => _poolLimitOverride = limit;

        /// <summary>Kart seçimi sonrası UI'ı zorla güncelle.</summary>
        public void RefreshProgress() => FireProgressChanged();

        /// <summary>Decaying Rift erken temizleme: pool counter'ı geri al (remaining artar).</summary>
        public void AddPieces(int count)
        {
            PiecesPlacedInWindow = Mathf.Max(0, PiecesPlacedInWindow - count);
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
            LastCompletedPiecesRemaining = PiecesRemaining; // Hoarder için kaydet
            CurrentMilestoneIndex++;
            PiecesPlacedInWindow = 0;
            _currentWindowScale  = 1f; // sonraki pencere için sıfırla

            OnMilestoneReached?.Invoke(data.CoinReward, data);
            FireProgressChanged();
        }

        public void EnsureMinimumRemaining(int minimum = 6)
        {
            if (PiecesRemaining < minimum)
                PiecesPlacedInWindow = Mathf.Max(0, EffectivePoolLimit - minimum);
            FireProgressChanged();
        }

        private void FireProgressChanged()
        {
            var next = NextMilestone;

            // Önceki milestone eşiği — score progress başlangıç noktası
            int prevThreshold = CurrentMilestoneIndex > 0
                ? (_config.GetMilestone(CurrentMilestoneIndex - 1)?.ScoreThreshold ?? 0)
                : 0;

            // UI'ya etkili (ölçeklenmiş) eşiği gönder — progress bar doğru görünsün
            int effectiveNext = next.HasValue
                ? Mathf.RoundToInt(next.Value.ScoreThreshold * _currentWindowScale * _permanentScale)
                : 0;

            OnProgressChanged?.Invoke(new MilestoneProgressState(
                currentMilestone : CurrentMilestoneIndex,
                totalMilestones  : _config.TotalMilestones,
                piecesRemaining  : PiecesRemaining,
                poolLimit        : EffectivePoolLimit,
                nextThreshold    : effectiveNext,
                nextLabel        : next?.Label ?? "MAX",
                allCleared       : AllMilestonesCleared,
                currentScore     : CurrentScore,
                prevThreshold    : prevThreshold
            ));
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
        public readonly int    PrevThreshold;   // önceki milestone eşiği
        public readonly int    CurrentScore;
        public readonly string NextLabel;
        public readonly bool   AllCleared;

        public MilestoneProgressState(
            int currentMilestone, int totalMilestones,
            int piecesRemaining,  int poolLimit,
            int nextThreshold,    string nextLabel,
            bool allCleared,      int currentScore,
            int prevThreshold)
        {
            CurrentMilestone = currentMilestone;
            TotalMilestones  = totalMilestones;
            PiecesRemaining  = piecesRemaining;
            PoolLimit        = poolLimit;
            NextThreshold    = nextThreshold;
            PrevThreshold    = prevThreshold;
            CurrentScore     = currentScore;
            NextLabel        = nextLabel;
            AllCleared       = allCleared;
        }

        /// <summary>
        /// Skor tabanlı progress — 0..1 arası.
        /// Önceki milestone'dan sonraki milestone'a olan mesafedeki ilerleme.
        /// Örnek: prev=0, next=2500, score=250 → 0.10
        /// </summary>
        public float ScoreFillRatio
        {
            get
            {
                if (AllCleared) return 1f;
                int range = NextThreshold - PrevThreshold;
                if (range <= 0) return 1f;
                return Mathf.Clamp01((float)(CurrentScore - PrevThreshold) / range);
            }
        }

        /// <summary>Eski PoolFillRatio — geriye dönük uyumluluk için tutuldu.</summary>
        public float PoolFillRatio => PoolLimit > 0
            ? 1f - (float)PiecesRemaining / PoolLimit
            : 1f;
    }
}