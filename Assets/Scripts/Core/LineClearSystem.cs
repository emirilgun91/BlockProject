using System.Collections.Generic;

namespace RogueBlockBlast.Core
{
    public static class LineClearSystem
    {
        /// <summary>
        /// Dolu satır/sütunları temizler.
        /// </summary>
        /// <returns>
        /// totalCleared : temizlenen satır + sütun sayısı
        /// tileValueSum : temizlenen tüm tile'ların puan toplamı
        /// clearedRows  : hangi satırların temizlendiği (FX için)
        /// clearedCols  : hangi sütunların temizlendiği (FX için)
        /// </returns>
        public static (int totalCleared, float tileValueSum, bool[] clearedRows, bool[] clearedCols)
            ClearLines(BoardModel board)
        {
            List<int> rows = board.GetFullRows();
            List<int> cols = board.GetFullColumns();

            bool[] clearedRows = new bool[board.Height];
            bool[] clearedCols = new bool[board.Width];

            float tileValueSum = 0f;

            // Önce değerleri topla, sonra temizle
            foreach (int y in rows)
            {
                tileValueSum    += board.SumRowValues(y);
                clearedRows[y]   = true;
                board.ClearRow(y);
            }

            foreach (int x in cols)
            {
                tileValueSum    += board.SumColumnValues(x);
                clearedCols[x]   = true;
                board.ClearColumn(x);
            }

            return (rows.Count + cols.Count, tileValueSum, clearedRows, clearedCols);
        }
    }
}