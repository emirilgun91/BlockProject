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

        /// <summary>
        /// Parçayı tahtaya yazar.
        ///
        /// tileValueOverride: hücrelere yazılacak puan. null ise parçanın kendi
        /// TileValue'su kullanılır. Shape kartı bonusu gibi çalışma zamanında
        /// eklenen değerler buradan geçirilir — böylece tahtadaki hücre, ghost
        /// önizlemesinde gösterilen değerin AYNISINI saklar ve line clear'da
        /// ekrana uçan sayı ile kazanılan puan ayrışmaz.
        /// </summary>
        public static void Place(
            BoardModel      board,
            PieceDefinition piece,
            Vector2Int      anchor,
            Rotation        rot,
            float?          tileValueOverride = null)
        {
            float value = tileValueOverride ?? piece.TileValue;
            var cells = piece.GetCells(rot);
            foreach (var c in cells)
            {
                int x = anchor.x + c.x;
                int y = anchor.y + c.y;

                // Renk + tile değeri birlikte yazılır
                board.SetFilled(x, y, true, piece.BlockColor, value);
            }
        }
    }
}
