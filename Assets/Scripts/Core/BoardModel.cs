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
        private readonly float[,] _tileValues;   // her hücrenin puan değeri

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
            return _cells[x, y];
        }

        // ── Set ──────────────────────────────────────────────────────────────
        public void SetFilled(int x, int y, bool value) =>
            SetFilled(x, y, value, Color.white, 0f);

        public void SetFilled(int x, int y, bool value, Color color) =>
            SetFilled(x, y, value, color, 0f);

        /// <summary>Ana setter — renk ve tile değeri birlikte yazılır.</summary>
        public void SetFilled(int x, int y, bool value, Color color, float tileValue)
        {
            if (!IsInside(x, y)) return;

            _cells[x, y]      = value;
            _colors[x, y]     = value ? color      : default;
            _tileValues[x, y] = value ? tileValue  : 0f;
        }

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
                    if (!_cells[x, y]) { all = false; break; }
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
                    if (!_cells[x, y]) { all = false; break; }
                if (all) full.Add(x);
            }
            return full;
        }

        // ── Clear ────────────────────────────────────────────────────────────
        public void ClearRow(int row)
        {
            for (int x = 0; x < Width; x++)
            {
                _cells[x, row]      = false;
                _colors[x, row]     = default;
                _tileValues[x, row] = 0f;
            }
        }

        public void ClearColumn(int col)
        {
            for (int y = 0; y < Height; y++)
            {
                _cells[col, y]      = false;
                _colors[col, y]     = default;
                _tileValues[col, y] = 0f;
            }
        }

        // ── Tile value sum ───────────────────────────────────────────────────
        /// <summary>
        /// Bir satırdaki tüm tile değerlerini toplar.
        /// LineClearSystem tarafından temizlemeden önce çağrılır.
        /// </summary>
        public float SumRowValues(int row)
        {
            if (row < 0 || row >= Height) return 0f;
            float sum = 0f;
            for (int x = 0; x < Width; x++)
                sum += _tileValues[x, row];
            return sum;
        }

        /// <summary>Bir sütundaki tüm tile değerlerini toplar.</summary>
        public float SumColumnValues(int col)
        {
            if (col < 0 || col >= Width) return 0f;
            float sum = 0f;
            for (int y = 0; y < Height; y++)
                sum += _tileValues[col, y];
            return sum;
        }
    }
}