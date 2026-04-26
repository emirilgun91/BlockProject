using System.Collections.Generic;
using RogueBlockBlast.Core;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// BoardView üzerindeki FX tetikleyici.
    /// RunController'dan çağrılır — BoardView referansını paylaşır.
    /// </summary>
    public static class BoardFX
    {
        // Dalga efekti için her hücre arasındaki gecikme
        private const float WaveDelay = 0.03f;

        // Clear flash rengi — teal (palette ile uyumlu)
        private static readonly Color ClearFlash = new Color(0.08f, 0.72f, 0.60f, 1f);

        /// <summary>
        /// Yerleştirilen hücrelerde punch-scale + flash, komşularda ripple.
        /// </summary>
        public static void PlayPlaceFX(
            BoardView boardView,
            PieceDefinition piece,
            Vector2Int anchor,
            Rotation rot)
        {
            if (boardView == null || piece == null) return;

            var cells = piece.GetCells(rot);

            // Yerleştirilen hücreler
            var placedSet = new HashSet<Vector2Int>();
            foreach (var c in cells)
            {
                var pos  = new Vector2Int(anchor.x + c.x, anchor.y + c.y);
                var tile = boardView.GetTile(pos.x, pos.y);
                tile?.PlayPlaceFX();
                placedSet.Add(pos);
            }

            // Komşu hücreler — placed set'te olmayanlar
            var neighborDirs = new Vector2Int[]
            {
                new( 1, 0), new(-1, 0),
                new( 0, 1), new( 0,-1),
            };

            var visited = new HashSet<Vector2Int>(placedSet);

            foreach (var c in cells)
            {
                var pos = new Vector2Int(anchor.x + c.x, anchor.y + c.y);
                foreach (var dir in neighborDirs)
                {
                    var neighbor = pos + dir;
                    if (!visited.Add(neighbor)) continue; // zaten işlendi

                    var tile = boardView.GetTile(neighbor.x, neighbor.y);
                    tile?.PlayRippleFX(strength: 0.07f, duration: 0.18f);
                }
            }
        }

        /// <summary>
        /// Temizlenen satırlarda dalga efekti çalıştırır.
        /// clearedRows / clearedCols BoardView'dan alınır.
        /// </summary>
        public static void PlayLineClearFX(
            BoardView boardView,
            int boardWidth,
            int boardHeight,
            bool[] clearedRows,
            bool[] clearedCols)
        {
            if (boardView == null) return;

            // Board'un boş hücre rengi — TileView ile tutarlı
            Color emptyColor = new Color(0x1c / 255f, 0x21 / 255f, 0x32 / 255f, 1f);

            // Satırlar — soldan sağa dalga
            for (int y = 0; y < boardHeight; y++)
            {
                if (clearedRows != null && y < clearedRows.Length && clearedRows[y])
                {
                    for (int x = 0; x < boardWidth; x++)
                    {
                        float delay = x * WaveDelay;
                        boardView.GetTile(x, y)?.PlayClearFX(delay, ClearFlash, emptyColor);
                    }
                }
            }

            // Sütunlar — aşağıdan yukarı dalga
            for (int x = 0; x < boardWidth; x++)
            {
                if (clearedCols != null && x < clearedCols.Length && clearedCols[x])
                {
                    for (int y = 0; y < boardHeight; y++)
                    {
                        float delay = y * WaveDelay;
                        boardView.GetTile(x, y)?.PlayClearFX(delay, ClearFlash, emptyColor);
                    }
                }
            }
        }
    }
}