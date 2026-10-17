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
    /// </summary>
    public static class RunEvents
    {
        public static event Action                  OnRunStarted;
        public static event Action<PlacementReport> OnPiecePlaced;
        /// <summary>(etiket — "Stage 10", hepsi bitti mi, pencerede kalan şekil — mevcut dahil, pencere boyu)</summary>
        public static event Action<string, bool, int, int> OnMilestoneReached;
        public static event Action<CardSO, int>     OnCardPicked;       // (kart, envanterdeki kart sayısı)
        public static event Action                  OnDeadPoolSurvived;
        public static event Action<bool>            OnGambleResolved;   // kazandı mı
        public static event Action<int, int>        OnRunEnded;         // (skor, yerleştirilen şekil)

        public static void RunStarted()                    => OnRunStarted?.Invoke();
        public static void PiecePlaced(PlacementReport r)  => OnPiecePlaced?.Invoke(r);
        public static void MilestoneReached(string label, bool allCleared, int piecesRemaining, int windowSize)
            => OnMilestoneReached?.Invoke(label, allCleared, piecesRemaining, windowSize);
        public static void CardPicked(CardSO card, int inventoryCount) => OnCardPicked?.Invoke(card, inventoryCount);
        public static void DeadPoolSurvived()              => OnDeadPoolSurvived?.Invoke();
        public static void GambleResolved(bool won)        => OnGambleResolved?.Invoke(won);
        public static void RunEnded(int score, int pieces) => OnRunEnded?.Invoke(score, pieces);
    }
}
