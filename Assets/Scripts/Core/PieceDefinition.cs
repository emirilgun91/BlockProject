using UnityEngine;
using System;
using System.Collections.Generic;


namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Şeklin local hücreleri (0,0 referanslı). Rotasyon için cache içerir.
    /// </summary>
    public sealed class PieceDefinition
    {
        public string Id { get; }
        public Color BlockColor  { get; }
        public IReadOnlyList<Vector2Int> CellsR0 => _cellsR0;

        private readonly Vector2Int[] _cellsR0;
        private readonly Dictionary<Rotation, Vector2Int[]> _rotCache = new();

        public PieceDefinition(string id, IEnumerable<Vector2Int> cellsR0)
            : this(id, cellsR0, Color.white)
        {
        }

        public PieceDefinition(string id, IEnumerable<Vector2Int> cellsR0, Color blockColor)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id required", nameof(id));
            Id = id;

            var list = new List<Vector2Int>(cellsR0 ?? throw new ArgumentNullException(nameof(cellsR0)));
            if (list.Count == 0) throw new ArgumentException("Piece must have at least 1 cell.");

            _cellsR0 = list.ToArray();
            _rotCache[Rotation.R0] = _cellsR0;
            BlockColor = blockColor;
        }
        public PieceDefinition(string id, List<Vector2Int> cells, Color blockColor)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Id required", nameof(id)) : id;

            var list = new List<Vector2Int>(cells);
            if (list.Count == 0) throw new ArgumentException("Piece must have at least 1 cell.");

            _cellsR0 = list.ToArray();
            _rotCache[Rotation.R0] = _cellsR0;
            BlockColor = blockColor;
        }

        public IReadOnlyList<Vector2Int> GetCells(Rotation rotation)
        {
            if (_rotCache.TryGetValue(rotation, out var cached)) return cached;

            // R0 -> rotate N times 90deg around origin, then normalize to non-negative.
            var rotated = new List<Vector2Int>(_cellsR0.Length);
            foreach (var c in _cellsR0)
            {
                var v = c;
                int times = (int)rotation;
                for (int i = 0; i < times; i++)
                    v = new Vector2Int(v.y, -v.x); // 90 deg

                rotated.Add(v);
            }

            // normalize to min x,y = 0
            int minX = int.MaxValue, minY = int.MaxValue;
            for (int i = 0; i < rotated.Count; i++)
            {
                minX = Math.Min(minX, rotated[i].x);
                minY = Math.Min(minY, rotated[i].y);
            }
            for (int i = 0; i < rotated.Count; i++)
                rotated[i] = new Vector2Int(rotated[i].x - minX, rotated[i].y - minY);

            var arr = rotated.ToArray();
            _rotCache[rotation] = arr;
            return arr;
        }
    }
}