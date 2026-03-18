using System.Collections.Generic;

namespace RogueBlockBlast.Core
{
    public static class LineClearSystem
    {
        /// <summary>
        /// Dolu satır ve sütunları temizler.
        /// FX sistemi için hangi satır/sütunların temizlendiğini de döndürür.
        /// </summary>
        /// <returns>
        /// totalCleared  : toplam temizlenen satır + sütun sayısı
        /// clearedRows   : [y] = true ise o satır temizlendi
        /// clearedCols   : [x] = true ise o sütun temizlendi
        /// </returns>
        public static (int totalCleared, bool[] clearedRows, bool[] clearedCols)
            ClearLines(BoardModel board)
        {
            List<int> rows = board.GetFullRows();
            List<int> cols = board.GetFullColumns();

            // bool array'leri oluştur
            bool[] clearedRows = new bool[board.Height];
            bool[] clearedCols = new bool[board.Width];

            for (int i = 0; i < rows.Count; i++)
            {
                clearedRows[rows[i]] = true;
                board.ClearRow(rows[i]);
            }

            for (int i = 0; i < cols.Count; i++)
            {
                clearedCols[cols[i]] = true;
                board.ClearColumn(cols[i]);
            }

            return (rows.Count + cols.Count, clearedRows, clearedCols);
        }
    }
}