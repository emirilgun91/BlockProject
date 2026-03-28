using UnityEngine;
using UnityEngine.UI;
using RogueBlockBlast.Core;
using RogueBlockBlast.Content;

namespace RogueBlockBlast.UI
{
    public class PoolSlotView : MonoBehaviour
    {
        [SerializeField] private RectTransform _container;
        [SerializeField] private GameObject _cellPrefab;
        [SerializeField] private GameObject _selectionFrame;
        [SerializeField] private float _fitPadding = 0.80f;
        float _targetScale = 1f;
        void Update()
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                Vector3.one * _targetScale,
                Time.deltaTime * 12f
            );
        }
        
        public void SetHighlight(bool value)
        {
            transform.localScale = value ? Vector3.one * 1.15f : Vector3.one;
        }
        public void Render(PieceDefinition piece, bool selected)
        {
            
            foreach (Transform child in _container)
                Destroy(child.gameObject);

            if (piece == null)
            {
                _selectionFrame.SetActive(false);
                return;
            }

            var cells = piece.GetCells(Rotation.R0);

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.y > maxY) maxY = c.y;
            }

            int width  = maxX - minX + 1;
            int height = maxY - minY + 1;

// Her ekseni ayrı kısıtla — en küçük olanı al
            float maxCellByWidth  = _container.rect.width  / width;
            float maxCellByHeight = _container.rect.height / height;
            float cellSize = Mathf.Min(maxCellByWidth, maxCellByHeight) * _fitPadding;

// Tile'ları tam bitişik yerleştir
            float totalW  = width  * cellSize;
            float totalH  = height * cellSize;
            float offsetX = totalW * 0.5f - cellSize * 0.5f;
            float offsetY = totalH * 0.5f - cellSize * 0.5f;
            
            foreach (var c in cells)
            {
                var go = Instantiate(_cellPrefab, _container);

                RectTransform rect = go.GetComponent<RectTransform>();

                rect.sizeDelta = new Vector2(cellSize, cellSize);

                rect.anchoredPosition = new Vector2(
                    (c.x - minX) * cellSize - offsetX,
                    (c.y - minY) * cellSize - offsetY
                );

                // Hem BlockCellView hem de olası SpriteRenderer/UI Image'ı renklendir
                var color = piece.BlockColor;

                var cellView = go.GetComponentInChildren<BlockCellView>(true);
                if (cellView != null)
                {
                    cellView.SetColor(color);
                }

                var sr = go.GetComponentInChildren<SpriteRenderer>(true);
                if (sr != null)
                    sr.color = color;

                var img = go.GetComponentInChildren<Image>(true);
                if (img != null)
                    img.color = color;
            }

            _selectionFrame.SetActive(selected);

            transform.localScale = selected ? Vector3.one * 1.1f : Vector3.one;
            
        }
        
    }
}