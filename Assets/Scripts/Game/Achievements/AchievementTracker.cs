using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using RogueBlockBlast.Core.Achievements;
using RogueBlockBlast.Game.Tutorial;
using RogueBlockBlast.Core.Save;
using UnityEngine;

namespace RogueBlockBlast.Game.Achievements
{
    /// <summary>
    /// Run içi olayları dinleyip achievement koşullarını kontrol eder. Bütün
    /// "ne zaman açılır" kuralları burada; RunController yalnızca
    /// <see cref="RunEvents"/> üzerinden haber verir.
    ///
    /// Run dışı (meta) olanlar — dükkân, yükseltme, coin — kendi
    /// registry'lerinde doğrudan <see cref="AchievementService"/>'i çağırır,
    /// çünkü ana menüde bu bileşenin dinleyeceği bir run yok.
    ///
    /// Sahne kurulumu gerekmez: ilk sahneden önce kendini yaratır.
    /// </summary>
    public sealed class AchievementTracker : MonoBehaviour
    {
        private const int   QuickMilestonePieces = 5;
        private const int   DizzyRotations       = 20;
        private const int   ZeroHeroMinPieces    = 10;
        private const int   LuckyWins            = 3;
        private const int   BlackCatLossStreak   = 5;
        private const float MonochromeTolerance  = 0.02f;

        public static AchievementTracker Instance { get; private set; }

        // ── Run durumu ───────────────────────────────────────────────────────
        private int  _rotationsThisRun;
        private int  _rotationsThisPiece;
        private bool _savedFromDeadPool;     // Kıl Payı: kurtarıldı, sonraki milestone bekleniyor
        private int  _gambleWins;
        private int  _gambleLossStreak;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[AchievementTracker]");
            DontDestroyOnLoad(go);
            go.AddComponent<AchievementTracker>();
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            RunEvents.OnRunStarted       += HandleRunStarted;
            RunEvents.OnPiecePlaced      += HandlePiecePlaced;
            RunEvents.OnMilestoneReached += HandleMilestoneReached;
            RunEvents.OnCardPicked       += HandleCardPicked;
            RunEvents.OnDeadPoolSurvived += HandleDeadPoolSurvived;
            RunEvents.OnGambleResolved   += HandleGamble;
            RunEvents.OnRunEnded         += HandleRunEnded;
            TutorialEvents.OnRotated     += HandleRotated;
        }

        private void OnDisable()
        {
            RunEvents.OnRunStarted       -= HandleRunStarted;
            RunEvents.OnPiecePlaced      -= HandlePiecePlaced;
            RunEvents.OnMilestoneReached -= HandleMilestoneReached;
            RunEvents.OnCardPicked       -= HandleCardPicked;
            RunEvents.OnDeadPoolSurvived -= HandleDeadPoolSurvived;
            RunEvents.OnGambleResolved   -= HandleGamble;
            RunEvents.OnRunEnded         -= HandleRunEnded;
            TutorialEvents.OnRotated     -= HandleRotated;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        private void HandleRunStarted()
        {
            _rotationsThisRun   = 0;
            _rotationsThisPiece = 0;
            _savedFromDeadPool  = false;
            _gambleWins         = 0;
            _gambleLossStreak   = 0;
        }

        private void HandleRotated()
        {
            _rotationsThisRun++;
            _rotationsThisPiece++;
            if (_rotationsThisPiece >= DizzyRotations)
                AchievementService.Unlock(AchievementIds.Dizzy);
        }

        private void HandlePiecePlaced(PlacementReport r)
        {
            _rotationsThisPiece = 0;
            AchievementService.AddStat(AchievementStats.PiecesPlaced, 1);

            // ── Skor ─────────────────────────────────────────────────────────
            if (r.Score >= 10_000)     AchievementService.Unlock(AchievementIds.Score10K);
            if (r.Score >= 100_000)    AchievementService.Unlock(AchievementIds.Score100K);
            if (r.Score >= 1_000_000)  AchievementService.Unlock(AchievementIds.Score1M);
            if (r.Score >= 10_000_000) AchievementService.Unlock(AchievementIds.Score10M);

            // ── Combo (çarpan temizliksiz yerleştirmede de değişebilir) ───────
            if (r.ComboMultiplier >= 2f  - 0.001f) AchievementService.Unlock(AchievementIds.ComboX2);
            if (r.ComboMultiplier >= 5f  - 0.001f) AchievementService.Unlock(AchievementIds.ComboX5);
            if (r.ComboMultiplier >= 10f - 0.001f) AchievementService.Unlock(AchievementIds.ComboX10);

            if (r.LinesCleared <= 0) return;

            // ── Temizlik ─────────────────────────────────────────────────────
            AchievementService.AddStat(AchievementStats.LinesCleared, r.LinesCleared);
            AchievementService.Unlock(AchievementIds.FirstClear);
            if (r.LinesCleared >= 2) AchievementService.Unlock(AchievementIds.DoubleClear);
            if (r.LinesCleared >= 3) AchievementService.Unlock(AchievementIds.TripleClear);
            if (r.LinesCleared >= 4) AchievementService.Unlock(AchievementIds.QuadClear);
            if (r.ComboMaxCharge)    AchievementService.Unlock(AchievementIds.MaxCharge);
            if (r.BoardEmptied)      AchievementService.Unlock(AchievementIds.PerfectClear);

            if (Any(r.ClearedRows) && Any(r.ClearedCols))
                AchievementService.Unlock(AchievementIds.Crossfire);

            if (HasMonochromeLine(r))
                AchievementService.Unlock(AchievementIds.Monochrome);
        }

        private void HandleMilestoneReached(string label, bool allCleared, int piecesRemaining, int windowSize)
        {
            AchievementService.Unlock(AchievementIds.FirstMilestone);

            int stage = ParseStageNumber(label);
            if (stage >= 5)  AchievementService.Unlock(AchievementIds.Stage5);
            if (stage >= 10) AchievementService.Unlock(AchievementIds.Stage10);
            if (stage >= 20) AchievementService.Unlock(AchievementIds.Stage20);
            if (allCleared)  AchievementService.Unlock(AchievementIds.AllStages);

            if (stage >= 5 && _rotationsThisRun == 0)
                AchievementService.Unlock(AchievementIds.Stiff);

            // Milestone, şeklin pencereden düşülmesinden ÖNCE tetikleniyor: kalan
            // sayı o anki şekli de içeriyor. 1 = pencerenin son şekliyle ulaşıldı.
            if (piecesRemaining == 1)
                AchievementService.Unlock(AchievementIds.LastGasp);

            int piecesUsed = windowSize - piecesRemaining + 1;
            // İlk aşamalar birkaç temizlikle geçilebiliyor; anlamlı olması için Stage 5+.
            if (stage >= 5 && piecesUsed > 0 && piecesUsed <= QuickMilestonePieces)
                AchievementService.Unlock(AchievementIds.QuickMilestone);

            if (_savedFromDeadPool)
                AchievementService.Unlock(AchievementIds.CloseCall);
            _savedFromDeadPool = false;
        }

        private void HandleCardPicked(CardSO card, int inventoryCount)
        {
            AchievementService.Unlock(AchievementIds.FirstCard);
            if (inventoryCount >= 10) AchievementService.Unlock(AchievementIds.Cards10);
            if (card != null && card.Rarity == CardRarity.Epic)
                AchievementService.Unlock(AchievementIds.EpicCard);
        }

        private void HandleDeadPoolSurvived() => _savedFromDeadPool = true;

        private void HandleGamble(bool won)
        {
            if (won)
            {
                _gambleWins++;
                _gambleLossStreak = 0;
                if (_gambleWins >= LuckyWins) AchievementService.Unlock(AchievementIds.Lucky);
            }
            else
            {
                _gambleLossStreak++;
                if (_gambleLossStreak >= BlackCatLossStreak) AchievementService.Unlock(AchievementIds.BlackCat);
            }
        }

        private void HandleRunEnded(int score, int piecesPlaced)
        {
            AchievementService.AddStat(AchievementStats.RunsPlayed, 1);
            if (score <= 0 && piecesPlaced >= ZeroHeroMinPieces)
                AchievementService.Unlock(AchievementIds.ZeroHero);
            SaveStore.Save();   // run sonu: debounce bekleyen sayaçları hemen yaz
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private static bool Any(bool[] flags)
        {
            if (flags == null) return false;
            foreach (bool f in flags) if (f) return true;
            return false;
        }

        /// <summary>"Stage 10" → 10. Etiketler sırayla artmıyor (8'den 10'a atlıyor), o yüzden sayıyla değil etiketle bakılır.</summary>
        private static int ParseStageNumber(string label)
        {
            if (string.IsNullOrEmpty(label)) return 0;
            int end = label.Length, start = end;
            while (start > 0 && char.IsDigit(label[start - 1])) start--;
            return start < end && int.TryParse(label.Substring(start, end - start), out int n) ? n : 0;
        }

        /// <summary>Temizlenen satır/sütunlardan biri baştan sona aynı renk mi?</summary>
        private static bool HasMonochromeLine(PlacementReport r)
        {
            if (r.Snapshots == null || r.Snapshots.Count == 0) return false;

            if (r.ClearedRows != null)
                for (int y = 0; y < r.ClearedRows.Length; y++)
                    if (r.ClearedRows[y] && IsLineMonochrome(r.Snapshots, row: y, col: -1, length: r.ClearedCols?.Length ?? 0)) return true;

            if (r.ClearedCols != null)
                for (int x = 0; x < r.ClearedCols.Length; x++)
                    if (r.ClearedCols[x] && IsLineMonochrome(r.Snapshots, row: -1, col: x, length: r.ClearedRows.Length)) return true;

            return false;
        }

        private static bool IsLineMonochrome(System.Collections.Generic.IReadOnlyList<TileSnapshot> snaps, int row, int col, int length)
        {
            bool  first = true;
            Color c0    = default;
            int   count = 0;

            foreach (var s in snaps)
            {
                if (row >= 0 && s.Y != row) continue;
                if (col >= 0 && s.X != col) continue;

                if (first) { c0 = s.Color; first = false; }
                else if (Mathf.Abs(s.Color.r - c0.r) > MonochromeTolerance ||
                         Mathf.Abs(s.Color.g - c0.g) > MonochromeTolerance ||
                         Mathf.Abs(s.Color.b - c0.b) > MonochromeTolerance)
                    return false;
                count++;
            }

            // Kesişim hücresi iki kez gelebilir; en az bir tam hat kadar hücre olmalı.
            return length > 0 && count >= length;
        }
    }
}
