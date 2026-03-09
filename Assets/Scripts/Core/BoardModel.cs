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

        public BoardModel(int width, int height)
        {
            Width  = width;
            Height = height;

            _cells  = new bool[width, height];
            _colors = new Color[width, height];
        }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public bool IsFilled(int x, int y)
        {
            if (!IsInside(x, y)) return false;
            return _cells[x, y];
        }

        public void SetFilled(int x, int y, bool value)
        {
            SetFilled(x, y, value, Color.white);
        }

        public void SetFilled(int x, int y, bool value, Color color)
        {
            if (!IsInside(x, y)) return;

            _cells[x, y]  = value;
            _colors[x, y] = value ? color : default;
        }

        public Color GetCellColor(int x, int y)
        {
            if (!IsInside(x, y)) return default;
            return _colors[x, y];
        }

        public List<int> GetFullRows()
        {
            var full = new List<int>();

            for (int y = 0; y < Height; y++)
            {
                bool all = true;
                for (int x = 0; x < Width; x++)
                {
                    if (!_cells[x, y])
                    {
                        all = false;
                        break;
                    }
                }
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
                {
                    if (!_cells[x, y])
                    {
                        all = false;
                        break;
                    }
                }
                if (all) full.Add(x);
            }

            return full;
        }

        public void ClearRow(int row)
        {
            for (int x = 0; x < Width; x++)
            {
                _cells[x, row]  = false;
                _colors[x, row] = default;
            }
        }

        public void ClearColumn(int col)
        {
            for (int y = 0; y < Height; y++)
            {
                _cells[col, y]  = false;
                _colors[col, y] = default;
            }
        }
    }
}