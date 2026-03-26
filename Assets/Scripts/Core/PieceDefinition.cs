using System.Collections.Generic;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    public sealed class PieceDefinition
    {
        public string                    Id         { get; }
        public IReadOnlyList<Vector2Int> Cells      { get; }
        public Color                     BlockColor { get; }
        public float                     TileValue  { get; }  // upgrade dahil, board'a yazılır

        public PieceDefinition(
            string           id,
            List<Vector2Int> cells,
            Color            blockColor,
            float            tileValue)
        {
            Id         = id;
            Cells      = cells.AsReadOnly();
            BlockColor = blockColor;
            TileValue  = tileValue;
        }

        public IReadOnlyList<Vector2Int> GetCells(Rotation rot)
        {
            if (rot == Rotation.R0) return Cells;

            var rotated = new List<Vector2Int>(Cells.Count);
            foreach (var c in Cells)
                rotated.Add(RotateCell(c, rot));
            return rotated;
        }

        private static Vector2Int RotateCell(Vector2Int c, Rotation rot) => rot switch
        {
            Rotation.R90  => new Vector2Int( c.y, -c.x),
            Rotation.R180 => new Vector2Int(-c.x, -c.y),
            Rotation.R270 => new Vector2Int(-c.y,  c.x),
            _             => c
        };
    }
}