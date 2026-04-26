using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RogueBlockBlast.UI
{
    public sealed class BoardView : MonoBehaviour
    {
        [Header("Grid")]
        public Vector2 OriginWorld = Vector2.zero;
        public float   CellSize    = 1f;

        [Header("Prefabs")]
        public TileView TilePrefab;

        [Header("Intro Animation")]
        [SerializeField] private bool  _playIntroOnBuild  = true;
        [SerializeField] private float _introStagger      = 0.008f;
        [SerializeField] private float _introTileDuration = 0.25f;

        private TileView[,] _tiles;

        // ── Build ────────────────────────────────────────────────────────────
        public void Build(BoardModel board)
        {
            if (TilePrefab == null) { Debug.LogError("[BoardView] TilePrefab null"); return; }

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _tiles = new TileView[board.Width, board.Height];
            float targetScale = CellSize * 0.95f;

            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                var go = Instantiate(TilePrefab.gameObject, transform);
                go.name               = $"Tile_{x}_{y}";
                go.transform.position = GridToWorldCenter(x, y);

                // 1. Önce asıl hedef scale değerini ata
                go.transform.localScale = Vector3.one * targetScale;

                var tv = go.GetComponent<TileView>();
                if (tv == null) { Destroy(go); continue; }

                // 2. Init() çağırarak bu doğru scale değerinin _baseScale olarak kaydedilmesini sağla
                tv.Init();

                // 3. Eğer intro animasyonu oynayacaksa, şimdi sıfırla
                if (_playIntroOnBuild)
                {
                    go.transform.localScale = Vector3.zero;
                }

                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = 10;

                _tiles[x, y] = tv;
            }

            if (_playIntroOnBuild)
                StartCoroutine(PlayIntro(board.Width, board.Height, targetScale));
        }

        // ── Intro ────────────────────────────────────────────────────────────
        private IEnumerator PlayIntro(int width, int height, float targetScale)
        {
            var wait      = new WaitForSeconds(_introStagger);
            int diagonals = width + height - 1;

            for (int d = 0; d < diagonals; d++)
            {
                for (int x = 0; x < width; x++)
                {
                    int y = d - x;
                    if (y < 0 || y >= height) continue;

                    // y=0 board'da alt — görsel olarak üst = height-1-y
                    int vy   = height - 1 - y;
                    var tile = _tiles[x, vy];
                    if (tile == null) continue;

                    tile.transform
                        .DOScale(Vector3.one * targetScale, _introTileDuration)
                        .SetEase(Ease.OutBack, 1.5f);
                }
                yield return wait;
            }
        }

        // ── Mouse Helpers ────────────────────────────────────────────────────
        public bool IsMouseOverBoard(Camera cam)
        {
            if (_tiles == null || cam == null || Mouse.current == null) return false;

            Vector2 mp    = Mouse.current.position.ReadValue();
            Vector3 w3    = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, 0f));
            Vector2 local = new Vector2(w3.x, w3.y) - OriginWorld;

            float bx = local.x / CellSize;
            float by = local.y / CellSize;

            return bx >= 0 && bx < _tiles.GetLength(0) &&
                   by >= 0 && by < _tiles.GetLength(1);
        }

        public Vector2Int? TryGetCellUnderMouse(Camera cam)
        {
            var raw = GetRawCell(cam);
            if (raw == null) return null;

            int w = _tiles.GetLength(0), h = _tiles.GetLength(1);
            int x = raw.Value.x,         y = raw.Value.y;

            return (x >= 0 && y >= 0 && x < w && y < h)
                ? new Vector2Int(x, y)
                : (Vector2Int?)null;
        }

        public Vector2Int? TryGetClampedCellUnderMouse(Camera cam, PieceDefinition piece, Rotation rot)
        {
            if (_tiles == null || piece == null) return null;

            var raw = GetRawCell(cam);
            if (raw == null) return null;

            int w = _tiles.GetLength(0), h = _tiles.GetLength(1);
            var cells = piece.GetCells(rot);

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x; if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y; if (c.y > maxY) maxY = c.y;
            }

            int cx = Mathf.Clamp(raw.Value.x, -minX, w - 1 - maxX);
            int cy = Mathf.Clamp(raw.Value.y, -minY, h - 1 - maxY);

            return new Vector2Int(cx, cy);
        }

        // ── Render ───────────────────────────────────────────────────────────
        public void Render(BoardModel board, ISet<Vector2Int> ghostCells)
        {
            if (_tiles == null) return;

            Color emptyCell = new Color32(0x1c, 0x21, 0x32, 0xff);
            Color ghostOk   = BlockColorPalette.GhostValid;
            Color ghostBad  = BlockColorPalette.GhostInvalid;

            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                bool  filled    = board.IsFilled(x, y);
                Color baseColor = filled ? board.GetCellColor(x, y) : emptyCell;

                if (ghostCells != null && ghostCells.Contains(new Vector2Int(x, y)))
                    baseColor = filled ? ghostBad : ghostOk;

                _tiles[x, y].SetColor(baseColor);

                if (filled)
                    _tiles[x, y].SetTileValue(board.GetTileValue(x, y));
            }
        }

        // ── Accessors ────────────────────────────────────────────────────────
        public Vector3 GetTileWorldPosition(int x, int y) => GridToWorldCenter(x, y);

        public TileView GetTile(int x, int y)
        {
            if (_tiles == null || x < 0 || y < 0 ||
                x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1)) return null;
            return _tiles[x, y];
        }

        // ── Private ──────────────────────────────────────────────────────────
        private Vector2Int? GetRawCell(Camera cam)
        {
            if (_tiles == null || cam == null || Mouse.current == null) return null;

            Vector2 mp    = Mouse.current.position.ReadValue();
            Vector3 w3    = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, 0f));
            Vector2 local = new Vector2(w3.x, w3.y) - OriginWorld;

            return new Vector2Int(
                Mathf.FloorToInt(local.x / CellSize),
                Mathf.FloorToInt(local.y / CellSize)
            );
        }

        private Vector3 GridToWorldCenter(int x, int y) => new Vector3(
            OriginWorld.x + (x + 0.5f) * CellSize,
            OriginWorld.y + (y + 0.5f) * CellSize,
            0f
        );
    }
}