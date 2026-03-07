using System.Collections.Generic;

namespace RogueBlockBlast.Core
{
    public static class LineClearSystem
    {
        public static int ClearLines(BoardModel board)
        {
            List<int> rows = board.GetFullRows();
            List<int> cols = board.GetFullColumns();

            for (int i = 0; i < rows.Count; i++)
                board.ClearRow(rows[i]);

            for (int i = 0; i < cols.Count; i++)
                board.ClearColumn(cols[i]);

            return rows.Count + cols.Count;
        }
    }
}