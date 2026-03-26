using RogueBlockBlast.Core;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    public static class PlacementSystem
    {
        public static bool CanPlace(
            BoardModel      board,
            PieceDefinition piece,
            Vector2Int      anchor,
            Rotation        rot)
        {
            var cells = piece.GetCells(rot);
            foreach (var c in cells)
            {
                int x = anchor.x + c.x;
                int y = anchor.y + c.y;
                if (!board.IsInside(x, y)) return false;
                if (board.IsFilled(x, y))  return false;
            }
            return true;
        }

        public static void Place(
            BoardModel      board,
            PieceDefinition piece,
            Vector2Int      anchor,
            Rotation        rot)
        {
            var cells = piece.GetCells(rot);
            foreach (var c in cells)
            {
                int x = anchor.x + c.x;
                int y = anchor.y + c.y;

                // Renk + tile değeri birlikte yazılır
                board.SetFilled(x, y, true, piece.BlockColor, piece.TileValue);
            }
        }
    }
}