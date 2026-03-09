using UnityEngine;

namespace RogueBlockBlast.Core
{
    public static class PlacementSystem
    {
        public static bool CanPlace(
            BoardModel board,
            PieceDefinition piece,
            Vector2Int anchor,
            Rotation rot)
        {
            var cells = piece.GetCells(rot);

            for (int i = 0; i < cells.Count; i++)
            {
                int x = anchor.x + cells[i].x;
                int y = anchor.y + cells[i].y;

                if (!board.IsInside(x, y))
                    return false;

                if (board.IsFilled(x, y))
                    return false;
            }

            return true;
        }

        public static void Place(
            BoardModel board,
            PieceDefinition piece,
            Vector2Int anchor,
            Rotation rot)
        {
            var cells = piece.GetCells(rot);

            for (int i = 0; i < cells.Count; i++)
            {
                int x = anchor.x + cells[i].x;
                int y = anchor.y + cells[i].y;

                board.SetFilled(x, y, true, piece.BlockColor);
            }
        }
    }
}