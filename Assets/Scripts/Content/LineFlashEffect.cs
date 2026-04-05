using DG.Tweening;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Temizlenen satır/sütunun kenarında beyaz emissive bir şerit çakar.
    /// Her BoardView'a bir tane yerleştir.
    ///
    /// Hierarchy:
    ///  BoardView
    ///   └── LineFlashEffect  ← bu script + SpriteRenderer
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LineFlashEffect : MonoBehaviour
    {
        [Header("Flash Settings")]
        [SerializeField] private float _flashDuration  = 0.12f;
        [SerializeField] private float _holdDuration   = 0.06f;
        [SerializeField] private float _fadeDuration   = 0.18f;
        [SerializeField] private Color _flashColor     = new Color(1f, 1f, 1f, 0.85f);

        private SpriteRenderer _sr;

        private void Awake()
        {
            _sr       = GetComponent<SpriteRenderer>();
            _sr.color = Color.clear;
            _sr.sortingOrder = 20;  // tile'ların üstünde
        }

        /// <summary>
        /// Yatay satır flash'ı.
        /// boardOrigin: BoardView.OriginWorld
        /// row: hangi satır
        /// cellSize: tile boyutu
        /// boardWidth: kaç sütun
        /// </summary>
        public void FlashRow(Vector2 boardOrigin, int row, float cellSize, int boardWidth, float delay = 0f)
        {
            float w = boardWidth * cellSize;
            float h = cellSize * 0.18f;  // ince şerit
            float x = boardOrigin.x + w * 0.5f;
            float y = boardOrigin.y + (row + 0.5f) * cellSize;

            Flash(new Vector3(x, y, 0f), new Vector2(w, h), delay);
        }

        /// <summary>Dikey sütun flash'ı.</summary>
        public void FlashColumn(Vector2 boardOrigin, int col, float cellSize, int boardHeight, float delay = 0f)
        {
            float w = cellSize * 0.18f;
            float h = boardHeight * cellSize;
            float x = boardOrigin.x + (col + 0.5f) * cellSize;
            float y = boardOrigin.y + h * 0.5f;

            Flash(new Vector3(x, y, 0f), new Vector2(w, h), delay);
        }

        // ── Private ──────────────────────────────────────────────────────────
        private void Flash(Vector3 worldPos, Vector2 size, float delay)
        {
            // Birden fazla flash üst üste gelebilir — her biri ayrı obje ister
            // Basit çözüm: yeni bir instance oluştur (pool ileride eklenebilir)
            var go = Instantiate(gameObject, worldPos, Quaternion.identity, transform.parent);
            var fx = go.GetComponent<LineFlashEffect>();
            var sr = go.GetComponent<SpriteRenderer>();

            sr.color = Color.clear;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            DOTween.Sequence()
                .SetUpdate(false)
                .AppendInterval(delay)
                .Append(sr.DOColor(_flashColor, _flashDuration).SetEase(Ease.OutQuad).SetUpdate(false))
                .AppendInterval(_holdDuration)
                .Append(sr.DOColor(Color.clear, _fadeDuration).SetEase(Ease.InQuad).SetUpdate(false))
                .AppendCallback(() => Destroy(go));
        }
    }
}