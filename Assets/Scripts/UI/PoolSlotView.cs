using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RogueBlockBlast.Core;
using RogueBlockBlast.Content;

namespace RogueBlockBlast.UI
{
    public class PoolSlotView : MonoBehaviour, IPointerClickHandler
    {
        public Action OnClicked;
        [SerializeField] private RectTransform _container;
        [SerializeField] private GameObject    _cellPrefab;
        [SerializeField] private GameObject    _selectionFrame;

        [Tooltip("Hücrelerin container'a oranı. Küçültmek padding ekler.")]
        [SerializeField] [Range(0.5f, 1f)] private float _fitPadding = 0.80f;

        [Header("Entrance Animation")]
        [SerializeField] private float _entranceDuration = 0.3f;

        // ── Scale animation ──────────────────────────────────────────────────
        float _targetScale = 1f;
        private Tween _entranceTween;

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

        public void PlayEntranceAnim(float delay = 0f)
        {
            _entranceTween?.Kill();

            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

            cg.alpha     = 0f;
            _targetScale = 0.75f;

            _entranceTween = DOTween.Sequence()
                .SetDelay(delay)
                .AppendCallback(() => _targetScale = 1f)
                .Append(cg.DOFade(1f, _entranceDuration * 0.6f).SetEase(Ease.OutQuad))
                .SetAutoKill(true);
        }

        public void OnPointerClick(PointerEventData _) => OnClicked?.Invoke();

        // ── Render ───────────────────────────────────────────────────────────
        public void Render(PieceDefinition piece, bool selected)
        {
            foreach (Transform child in _container)
                Destroy(child.gameObject);

            if (piece == null)
            {
                if (_selectionFrame != null) _selectionFrame.SetActive(false);
                return;
            }

            var cells = piece.GetCells(Rotation.R0);

            // ── Bounding box ─────────────────────────────────────────────────
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.y > maxY) maxY = c.y;
            }

            int shapeW = maxX - minX + 1;
            int shapeH = maxY - minY + 1;

            // ── Cell size — her iki ekseni ayrı kısıtla, küçük olanı al ──────
            // Böylece 4x1 yatay çubuk da 1x4 dikey çubuk da container'a sığar
            float maxCellByWidth  = _container.rect.width  / shapeW;
            float maxCellByHeight = _container.rect.height / shapeH;
            float cellSize = Mathf.Min(maxCellByWidth, maxCellByHeight) * _fitPadding;

            // ── Merkeze hizala ───────────────────────────────────────────────
            float offsetX = (shapeW - 1) * cellSize * 0.5f;
            float offsetY = (shapeH - 1) * cellSize * 0.5f;

            // ── Hücreleri oluştur ────────────────────────────────────────────
            foreach (var c in cells)
            {
                var go   = Instantiate(_cellPrefab, _container);
                var rect = go.GetComponent<RectTransform>();

                rect.sizeDelta       = new Vector2(cellSize, cellSize);
                rect.anchoredPosition = new Vector2(
                    (c.x - minX) * cellSize - offsetX,
                    (c.y - minY) * cellSize - offsetY
                );

                var color = piece.BlockColor;

                var cellView = go.GetComponentInChildren<BlockCellView>(true);
                if (cellView != null) cellView.SetColor(color);

                var sr = go.GetComponentInChildren<SpriteRenderer>(true);
                if (sr  != null) sr.color  = color;

                var img = go.GetComponentInChildren<Image>(true);
                if (img != null) img.color = color;
            }

            if (_selectionFrame != null)
                _selectionFrame.SetActive(selected);

            _targetScale = selected ? 1.1f : 1f;
        }
    }
}