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
            _targetScale = value ? 1.15f : 1f;
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

           
            float maxCellByWidth  = _container.rect.width  / width;
            float maxCellByHeight = _container.rect.height / height;
            float cellSize = Mathf.Min(maxCellByWidth, maxCellByHeight) * 0.55f;

         
            float maxAllowed = Mathf.Min(_container.rect.width, _container.rect.height) / 1.5f;
            cellSize = Mathf.Min(cellSize, maxAllowed);
            float offsetX = (width - 1) * cellSize * 0.5f;
            float offsetY = (height - 1) * cellSize * 0.5f;
            
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
            _targetScale = selected ? 1.1f : 1f;
            
        }
        
    }
}