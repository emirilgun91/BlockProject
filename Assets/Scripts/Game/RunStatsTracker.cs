using UnityEngine;

namespace RogueBlockBlast.Game
{
    /// <summary>
    /// Tracks per-run stats. Call the Record* methods from your existing systems.
    /// Feed the result into GameOverUI at run end.
    /// </summary>
    public sealed class RunStatsTracker : MonoBehaviour
    {
        // ── Singleton (lives on RunController's GameObject) ─────────────────
        public static RunStatsTracker Instance { get; private set; }

        // ── Stats ────────────────────────────────────────────────────────────
        public int LinesCleared  { get; private set; }
        public int RowsCleared   { get; private set; }
        public int ColsCleared   { get; private set; }
        public int PiecesPlaced  { get; private set; }
        public int MaxCombo      { get; private set; }  // highest multiplier * 10 reached
        public int CardsSelected { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Public API — call these from your existing systems ───────────────

        /// <summary>Call from LineClearSystem after clearing lines.</summary>
        public void RecordClear(int rowsCleared, int colsCleared)
        {
            RowsCleared  += rowsCleared;
            ColsCleared  += colsCleared;
            LinesCleared += rowsCleared + colsCleared;
        }

        /// <summary>Call from PlacementSystem after each placement.</summary>
        public void RecordPlacement() => PiecesPlaced++;

        /// <summary>Call from ScoreSystem when combo multiplier updates.</summary>
        public void RecordCombo(float multiplier)
        {
            int intVal = Mathf.RoundToInt(multiplier * 10);
            if (intVal > MaxCombo) MaxCombo = intVal;
        }

        /// <summary>Call from CardSelectionUI when a card is picked.</summary>
        public void RecordCardSelected() => CardsSelected++;

        /// <summary>Resets all stats — call at Run Start.</summary>
        public void Reset()
        {
            LinesCleared  = 0;
            RowsCleared   = 0;
            ColsCleared   = 0;
            PiecesPlaced  = 0;
            MaxCombo      = 0;
            CardsSelected = 0;
        }
    }
}