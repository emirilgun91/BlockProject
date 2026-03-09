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
        public float CellSize = 1f;

        [Header("Prefabs")]
        public TileView TilePrefab;

        private TileView[,] _tiles;

        public void Build(BoardModel board)
        {
            Debug.Log($"[BoardView.Build] board: {board.Width}x{board.Height} | TilePrefab null? {TilePrefab == null}");

            if (TilePrefab == null)
            {
                Debug.LogError("[BoardView.Build] TilePrefab is NOT assigned.");
                return;
            }

            // Eski çocukları temizle
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _tiles = new TileView[board.Width, board.Height];

            int created = 0;

            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                // Component instantiate yerine GameObject instantiate (daha stabil)
                GameObject go = Instantiate(TilePrefab.gameObject, transform);
                go.name = $"Tile_{x}_{y}";
                go.transform.position = GridToWorldCenter(x, y);
                go.transform.localScale = Vector3.one * (CellSize * 0.95f);

                var tv = go.GetComponent<TileView>();
                if (tv == null)
                {
                    Debug.LogError("[BoardView.Build] TilePrefab has NO TileView component!");
                    Destroy(go);
                    continue;
                }

                // Sorting garanti (isteğe bağlı ama faydalı)
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = 10;

                _tiles[x, y] = tv;
                created++;
            }

            Debug.Log($"[BoardView.Build] created={created} | children={transform.childCount}");
        }

        public Vector2Int? TryGetCellUnderMouse(Camera cam)
        {
            if (_tiles == null || cam == null) return null;
            if (Mouse.current == null) return null;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 mouseWorld3 = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            Vector2 mouseWorld = new Vector2(mouseWorld3.x, mouseWorld3.y);

            Vector2 local = mouseWorld - OriginWorld;

            int x = Mathf.FloorToInt(local.x / CellSize);
            int y = Mathf.FloorToInt(local.y / CellSize);

            int w = _tiles.GetLength(0);
            int h = _tiles.GetLength(1);

            if (x < 0 || y < 0 || x >= w || y >= h)
                return null;

            return new Vector2Int(x, y);
        }

        public void Render(BoardModel board, ISet<Vector2Int> ghostCells)
        {
            if (_tiles == null) return;

            // Board colors
            // Arkaplan:rgb(27, 42, 102) (camera)
            // Boş grid hücresi:#1c2132(TilePrefab rengi)
            Color emptyCell = new Color32(0x1c, 0x21, 0x32, 0xff);
            Color ghostOk   = BlockColorPalette.GhostValid;
            Color ghostBad  = BlockColorPalette.GhostInvalid;

            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                bool isFilled = board.IsFilled(x, y);
                Color baseColor = isFilled ? board.GetCellColor(x, y) : emptyCell;

                if (ghostCells != null && ghostCells.Contains(new Vector2Int(x, y)))
                {
                    // Overlays ghost tint on top of background / piece color
                    baseColor = isFilled ? ghostBad : ghostOk;
                }

                _tiles[x, y].SetColor(baseColor);
            }
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