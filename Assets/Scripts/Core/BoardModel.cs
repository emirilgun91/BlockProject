using System.Collections.Generic;
using UnityEngine;

namespace RogueBlockBlast.Core
{
    public class BoardModel
    {
        public int Width  { get; private set; }
        public int Height { get; private set; }

        private readonly bool[,]  _cells;
        private readonly Color[,] _colors;
        private readonly float[,] _tileValues;

        // Phantom cells: count as filled for line logic, block placement, score 0, not in _cells
        private readonly HashSet<Vector2Int> _phantomCells = new HashSet<Vector2Int>();

        // Dead zones: permanently filled, not cleared by ClearRow/ClearColumn
        private readonly HashSet<Vector2Int> _deadZones = new HashSet<Vector2Int>();

        public BoardModel(int width, int height)
        {
            Width  = width;
            Height = height;
            _cells      = new bool[width, height];
            _colors     = new Color[width, height];
            _tileValues = new float[width, height];
        }

        // ── Inside / Filled ──────────────────────────────────────────────────
        public bool IsInside(int x, int y) =>
            x >= 0 && y >= 0 && x < Width && y < Height;

        public bool IsFilled(int x, int y)
        {
            if (!IsInside(x, y)) return false;
            if (_cells[x, y]) return true;
            var v = new Vector2Int(x, y);
            return _phantomCells.Contains(v) || _deadZones.Contains(v);
        }

        public bool IsPhantom(int x, int y) => IsInside(x, y) && _phantomCells.Contains(new Vector2Int(x, y));
        public bool IsDeadZone(int x, int y) => IsInside(x, y) && _deadZones.Contains(new Vector2Int(x, y));

        // ── Set ──────────────────────────────────────────────────────────────
        public void SetFilled(int x, int y, bool value) =>
            SetFilled(x, y, value, Color.white, 0f);

        public void SetFilled(int x, int y, bool value, Color color) =>
            SetFilled(x, y, value, color, 0f);

        public void SetFilled(int x, int y, bool value, Color color, float tileValue)
        {
            if (!IsInside(x, y)) return;
            _cells[x, y]      = value;
            _colors[x, y]     = value ? color     : default;
            _tileValues[x, y] = value ? tileValue : 0f;
        }

        // ── Phantom ──────────────────────────────────────────────────────────
        public void AddPhantom(int x, int y)   => _phantomCells.Add(new Vector2Int(x, y));
        public void RemovePhantom(int x, int y) => _phantomCells.Remove(new Vector2Int(x, y));
        public void ClearAllPhantoms()          => _phantomCells.Clear();

        // ── Dead Zone ────────────────────────────────────────────────────────
        public void AddDeadZone(int x, int y)   => _deadZones.Add(new Vector2Int(x, y));
        public void RemoveDeadZone(int x, int y) => _deadZones.Remove(new Vector2Int(x, y));
        public void ClearAllDeadZones()          => _deadZones.Clear();
        public IReadOnlyCollection<Vector2Int> DeadZones => _deadZones;

        // ── Get ──────────────────────────────────────────────────────────────
        public Color GetCellColor(int x, int y)
        {
            if (!IsInside(x, y)) return default;
            return _colors[x, y];
        }

        public float GetTileValue(int x, int y)
        {
            if (!IsInside(x, y)) return 0f;
            return _tileValues[x, y];
        }

        // ── Full row / col ───────────────────────────────────────────────────
        public List<int> GetFullRows()
        {
            var full = new List<int>();
            for (int y = 0; y < Height; y++)
            {
                bool all = true;
                for (int x = 0; x < Width; x++)
                    if (!IsFilled(x, y)) { all = false; break; }
                if (all) full.Add(y);
            }
            return full;
        }

        public List<int> GetFullColumns()
        {
            var full = new List<int>();
            for (int x = 0; x < Width; x++)
            {
                bool all = true;
                for (int y = 0; y < Height; y++)
                    if (!IsFilled(x, y)) { all = false; break; }
                if (all) full.Add(x);
            }
            return full;
        }

        // ── Clear ────────────────────────────────────────────────────────────
        public void ClearRow(int row)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_deadZones.Contains(new Vector2Int(x, row))) continue;
                _cells[x, row]      = false;
                _colors[x, row]     = default;
                _tileValues[x, row] = 0f;
            }
        }

        public void ClearColumn(int col)
        {
            for (int y = 0; y < Height; y++)
            {
                if (_deadZones.Contains(new Vector2Int(col, y))) continue;
                _cells[col, y]      = false;
                _colors[col, y]     = default;
                _tileValues[col, y] = 0f;
            }
        }

        // ── Tile value sum ───────────────────────────────────────────────────
        public float SumRowValues(int row)
        {
            if (row < 0 || row >= Height) return 0f;
            float sum = 0f;
            for (int x = 0; x < Width; x++)
                sum += _tileValues[x, row];
            return sum;
        }

        public float SumColumnValues(int col)
        {
            if (col < 0 || col >= Width) return 0f;
            float sum = 0f;
            for (int y = 0; y < Height; y++)
                sum += _tileValues[col, y];
            return sum;
        }

        // ── Cell queries ─────────────────────────────────────────────────────
        public List<Vector2Int> GetEmptyCells(HashSet<Vector2Int> exclude = null)
        {
            var result = new List<Vector2Int>();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (IsFilled(x, y)) continue;
                var v = new Vector2Int(x, y);
                if (exclude != null && exclude.Contains(v)) continue;
                result.Add(v);
            }
            return result;
        }

        public List<Vector2Int> GetFilledCells()
        {
            var result = new List<Vector2Int>();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (_cells[x, y]) result.Add(new Vector2Int(x, y));
            return result;
        }

        // All placed pieces cleared (phantom and dead zones don't count against perfect clear)
        public bool IsAllCellsCleared()
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (_cells[x, y]) return false;
            return true;
        }
    }
}
