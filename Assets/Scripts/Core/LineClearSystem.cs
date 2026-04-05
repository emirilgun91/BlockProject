using System.Collections.Generic;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    public static class LineClearSystem
    {
        /// <summary>
        /// Dolu satır/sütunları temizler.
        /// TileSnapshot'ları temizlemeden ÖNCE toplar — VFX için.
        /// </summary>
        public static (int totalCleared, float tileValueSum, bool[] clearedRows, bool[] clearedCols, List<TileSnapshot> snapshots)
            ClearLines(BoardModel board)
        {
            List<int> rows = board.GetFullRows();
            List<int> cols = board.GetFullColumns();

            bool[] clearedRows = new bool[board.Height];
            bool[] clearedCols = new bool[board.Width];
            float  tileValueSum = 0f;

            // Hangi tile'lar temizlenecek — HashSet ile duplicate engelle
            var clearedCoords = new HashSet<Vector2Int>();
            foreach (int y in rows) for (int x = 0; x < board.Width;  x++) clearedCoords.Add(new Vector2Int(x, y));
            foreach (int x in cols) for (int y = 0; y < board.Height; y++) clearedCoords.Add(new Vector2Int(x, y));

            // Snapshot — temizlemeden ÖNCE al
            var snapshots = new List<TileSnapshot>(clearedCoords.Count);
            foreach (var coord in clearedCoords)
            {
                snapshots.Add(new TileSnapshot(
                    coord.x, coord.y,
                    board.GetCellColor(coord.x, coord.y),
                    board.GetTileValue(coord.x, coord.y)
                ));
                tileValueSum += board.GetTileValue(coord.x, coord.y);
            }

            // Şimdi temizle
            foreach (int y in rows) { clearedRows[y] = true; board.ClearRow(y); }
            foreach (int x in cols) { clearedCols[x] = true; board.ClearColumn(x); }

            return (rows.Count + cols.Count, tileValueSum, clearedRows, clearedCols, snapshots);
        }
    }

    /// <summary>Temizlenmeden önceki tile verisi — VFX'e geçilir.</summary>
    public readonly struct TileSnapshot
    {
        public readonly int   X, Y;
        public readonly Color Color;
        public readonly float Value;

        public TileSnapshot(int x, int y, Color color, float value)
        {
            X     = x;
            Y     = y;
            Color = color;
            Value = value;
        }
    }
}