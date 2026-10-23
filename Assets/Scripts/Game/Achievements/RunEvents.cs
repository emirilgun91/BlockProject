using System;
using System.Collections.Generic;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;

namespace RogueBlockBlast.Game.Achievements
{
    /// <summary>Bir yerleştirmenin sonucu — achievement'ların ihtiyaç duyduğu her şey tek pakette.</summary>
    public readonly struct PlacementReport
    {
        public readonly int            LinesCleared;
        public readonly bool[]         ClearedRows;
        public readonly bool[]         ClearedCols;
        public readonly IReadOnlyList<TileSnapshot> Snapshots;
        public readonly bool           BoardEmptied;   // temizlikten hemen sonra tahta boş muydu
        public readonly float          ComboMultiplier;
        public readonly bool           ComboMaxCharge;
        public readonly int            Score;

        public PlacementReport(int linesCleared, bool[] clearedRows, bool[] clearedCols,
                               IReadOnlyList<TileSnapshot> snapshots, bool boardEmptied,
                               float comboMultiplier, bool comboMaxCharge, int score)
        {
            LinesCleared    = linesCleared;
            ClearedRows     = clearedRows;
            ClearedCols     = clearedCols;
            Snapshots       = snapshots;
            BoardEmptied    = boardEmptied;
            ComboMultiplier = comboMultiplier;
            ComboMaxCharge  = comboMaxCharge;
            Score           = score;
        }
    }

    /// <summary>
    /// RunController'ın "şu oldu" diye haber verdiği hub. <c>TutorialEvents</c> ile
    /// aynı fikir: RunController achievement sistemini tanımaz, abone yoksa bedava.
    ///
    /// Classic Mode'da hiçbir olay yayılmaz: roguelike başarımları ve ömür boyu
    /// sayaçlar Classic'le dolmasın. Classic'in kendi başarımları <see cref="ClassicEvents"/>'ten beslenir.
    /// </summary>
    public static class RunEvents
    {
        private static bool Silent => GameMode.IsClassic;

        public static event Action                  OnRunStarted;
        public static event Action<PlacementReport> OnPiecePlaced;
        /// <summary>(etiket — "Stage 10", hepsi bitti mi, pencerede kalan şekil — mevcut dahil, pencere boyu)</summary>
        public static event Action<string, bool, int, int> OnMilestoneReached;
        public static event Action<CardSO, int>     OnCardPicked;       // (kart, envanterdeki kart sayısı)
        public static event Action                  OnDeadPoolSurvived;
        public static event Action<bool>            OnGambleResolved;   // kazandı mı
        public static event Action<int, int>        OnRunEnded;         // (skor, yerleştirilen şekil)

        public static void RunStarted()                    { if (!Silent) OnRunStarted?.Invoke(); }
        public static void PiecePlaced(PlacementReport r)  { if (!Silent) OnPiecePlaced?.Invoke(r); }
        public static void MilestoneReached(string label, bool allCleared, int piecesRemaining, int windowSize)
            { if (!Silent) OnMilestoneReached?.Invoke(label, allCleared, piecesRemaining, windowSize); }
        public static void CardPicked(CardSO card, int inventoryCount) { if (!Silent) OnCardPicked?.Invoke(card, inventoryCount); }
        public static void DeadPoolSurvived()              { if (!Silent) OnDeadPoolSurvived?.Invoke(); }
        public static void GambleResolved(bool won)        { if (!Silent) OnGambleResolved?.Invoke(won); }
        public static void RunEnded(int score, int pieces) { if (!Silent) OnRunEnded?.Invoke(score, pieces); }
    }

    /// <summary>
    /// Classic Mode'un tek olayı: her yerleştirmeden sonra o oyunun skoru, temizlenen
    /// satır sayısı ve aktif oyun süresi (sn). <see cref="RunEvents"/> Classic'te sustuğu
    /// için Classic başarımları yalnızca buradan beslenir.
    /// </summary>
    public static class ClassicEvents
    {
        public static event Action<int, int, float> OnProgress;   // (skor, satır, aktif saniye)

        public static void Progress(int score, int lines, float activeSeconds)
            => OnProgress?.Invoke(score, lines, activeSeconds);
    }
}
