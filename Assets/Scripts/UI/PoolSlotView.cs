using UnityEngine;
using RogueBlockBlast.Core;

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

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            float slotSize = Mathf.Min(_container.rect.width, _container.rect.height);
            float cellSize = slotSize / Mathf.Max(width, height) * 0.6f;

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
                
            }

            _selectionFrame.SetActive(selected);

            transform.localScale = selected ? Vector3.one * 1.1f : Vector3.one;
            
        }
        
    }
}