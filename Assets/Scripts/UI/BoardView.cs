using System.Collections.Generic;
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

        private TileView[,] _tiles;

        // ── Build ────────────────────────────────────────────────────────────
        public void Build(BoardModel board)
        {
            Debug.Log($"[BoardView.Build] board: {board.Width}x{board.Height} | TilePrefab null? {TilePrefab == null}");

            if (TilePrefab == null)
            {
                Debug.LogError("[BoardView.Build] TilePrefab is NOT assigned.");
                return;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _tiles = new TileView[board.Width, board.Height];

            int created = 0;

            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                GameObject go = Instantiate(TilePrefab.gameObject, transform);
                go.name            = $"Tile_{x}_{y}";
                go.transform.position   = GridToWorldCenter(x, y);
                go.transform.localScale = Vector3.one * (CellSize * 0.95f);

                var tv = go.GetComponent<TileView>();
                if (tv == null)
                {
                    Debug.LogError("[BoardView.Build] TilePrefab has NO TileView component!");
                    Destroy(go);
                    continue;
                }

                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = 10;

                _tiles[x, y] = tv;
                created++;
            }

            Debug.Log($"[BoardView.Build] created={created} | children={transform.childCount}");
        }

        // ── Mouse → Cell (sınır dışında null döner — orijinal davranış) ──────
        public Vector2Int? TryGetCellUnderMouse(Camera cam)
        {
            var raw = GetRawCellUnderMouse(cam);
            if (raw == null) return null;

            int w = _tiles.GetLength(0);
            int h = _tiles.GetLength(1);

            int x = raw.Value.x;
            int y = raw.Value.y;

            if (x < 0 || y < 0 || x >= w || y >= h)
                return null;

            return new Vector2Int(x, y);
        }

        // ── Mouse → Cell — shape'e göre clamp'li (sınırda kalar, null dönmez) 
        /// <summary>
        /// Mouse pozisyonunu board içinde tutar.
        /// Shape'in tüm hücreleri board sınırı içinde kalacak şekilde anchor clamp'lenir.
        /// Board tamamen dışındaysa null döner.
        /// </summary>
        public Vector2Int? TryGetClampedCellUnderMouse(Camera cam, PieceDefinition piece, Rotation rot)
        {
            if (_tiles == null || piece == null) return null;

            var raw = GetRawCellUnderMouse(cam);
            if (raw == null) return null;

            int w = _tiles.GetLength(0);
            int h = _tiles.GetLength(1);

            // Shape'in bounding box'ını hesapla
            var cells = piece.GetCells(rot);

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.y > maxY) maxY = c.y;
            }

            // Anchor'u clamp'le:
            // anchor + minX >= 0          → anchor >= -minX
            // anchor + maxX <= width - 1  → anchor <= width - 1 - maxX
            int clampedX = Mathf.Clamp(raw.Value.x, -minX, w - 1 - maxX);
            int clampedY = Mathf.Clamp(raw.Value.y, -minY, h - 1 - maxY);

            return new Vector2Int(clampedX, clampedY);
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
                bool  isFilled  = board.IsFilled(x, y);
                Color baseColor = isFilled ? board.GetCellColor(x, y) : emptyCell;

                if (ghostCells != null && ghostCells.Contains(new Vector2Int(x, y)))
                    baseColor = isFilled ? ghostBad : ghostOk;

                _tiles[x, y].SetColor(baseColor);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private Vector2Int? GetRawCellUnderMouse(Camera cam)
        {
            if (_tiles == null || cam == null)    return null;
            if (Mouse.current == null)             return null;

            Vector2 mousePos    = Mouse.current.position.ReadValue();
            Vector3 mouseWorld3 = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            Vector2 mouseWorld  = new Vector2(mouseWorld3.x, mouseWorld3.y);

            Vector2 local = mouseWorld - OriginWorld;

            int x = Mathf.FloorToInt(local.x / CellSize);
            int y = Mathf.FloorToInt(local.y / CellSize);

            return new Vector2Int(x, y);
        }

        private Vector3 GridToWorldCenter(int x, int y)
        {
            return new Vector3(
                OriginWorld.x + (x + 0.5f) * CellSize,
                OriginWorld.y + (y + 0.5f) * CellSize,
                0f
            );
        }
    }
}