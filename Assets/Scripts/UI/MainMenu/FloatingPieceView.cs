using System.Collections.Generic;
using DG.Tweening;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Tek bir süzülen blok parçası.
    /// FloatingPieceSpawner tarafından yönetilir.
    ///
    /// Prefab Hierarchy:
    ///  FloatingPiece (bu script)
    ///   └── Cells (boş parent — hücreler buraya instantiate edilir)
    /// </summary>
    public sealed class FloatingPieceView : MonoBehaviour
    {
        [Header("Cell Prefab")]
        [SerializeField] private GameObject _cellPrefab; // SpriteRenderer içeren basit kare prefab

        [Header("Cell Size")]
        [Tooltip("0 bırakılırsa ekran boyutuna göre otomatik hesaplanır")]
        [SerializeField] private float _cellSize = 0f;
        [Tooltip("Ekranın kaçta biri genişliğinde bir hücre olsun (0=manuel)")]
        [SerializeField] private float _cellScreenFraction = 0.04f; // ekranın %4'ü

        // ── Runtime state ─────────────────────────────────────────────────────
        private List<SpriteRenderer> _renderers = new();
        private Transform            _cells;

        // Hareket
        private Vector2 _direction;       // normalize drift yönü
        private float   _speed;           // birim/sn
        private float   _sineAmplitude;   // sine dalgası genliği
        private float   _sineFrequency;   // sine dalgası frekansı
        private float   _sinePhase;       // rastgele başlangıç fazı
        private Vector2 _sineAxis;        // sine'nin uygulandığı dik eksen

        // Katman
        private int   _layer;             // 1=yakın 2=orta 3=uzak
        private float _targetAlpha;

        // Bounds
        private FloatingPieceSpawner _spawner;
        private bool _alive;

        // Tween referansları
        private Tween _breatheTween;
        private Tween _fadeTween;

        // ── Init ─────────────────────────────────────────────────────────────

        public void Init(FloatingPieceSpawner spawner)
        {
            _spawner = spawner;

            // Cells parent
            if (_cells == null)
            {
                var go = new GameObject("Cells");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                _cells = go.transform;
            }
        }

        /// <summary>
        /// Parçayı verilen shape ile başlat.
        /// </summary>
        public void Spawn(
            PieceDefinition piece,
            Vector3 startPos,
            Vector2 direction,
            int layer,
            float speed,
            float sineAmplitude,
            float sineFrequency,
            float rotation)
        {
            _alive     = true;
            _direction = direction.normalized;
            _speed     = speed;
            _layer     = layer;
            _sinePhase = Random.Range(0f, Mathf.PI * 2f);
            _sineAmplitude = sineAmplitude;
            _sineFrequency = sineFrequency;

            // Drift yönüne dik eksen (sine wave için)
            _sineAxis = new Vector2(-_direction.y, _direction.x);

            // Layer'a göre alpha hedefi
            _targetAlpha = layer switch
            {
                1 => Random.Range(0.50f, 0.65f),
                2 => Random.Range(0.30f, 0.45f),
                _ => Random.Range(0.15f, 0.25f),
            };

            // Layer'a göre scale
            float scaleBase = layer switch
            {
                1 => Random.Range(1.00f, 1.30f),
                2 => Random.Range(0.70f, 0.90f),
                _ => Random.Range(0.40f, 0.60f),
            };

            transform.position   = startPos;
            transform.localScale = Vector3.one * scaleBase;
            transform.rotation   = Quaternion.Euler(0f, 0f, rotation);

            BuildCells(piece);
            SetAlpha(0f);

            // Fade in
            _fadeTween?.Kill();
            _fadeTween = DOTween.To(
                () => GetAlpha(),
                a  => SetAlpha(a),
                _targetAlpha,
                0.4f).SetEase(Ease.OutQuad);

            // Breathe
            _breatheTween?.Kill();
            float breatheTime = Random.Range(2f, 4f);
            _breatheTween = transform
                .DOScale(transform.localScale * 1.06f, breatheTime)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);

            // Rotation (sürekli)
            float rotDir = Random.value > 0.5f ? 1f : -1f;
            float rotSpd = Random.Range(8f, 22f) * rotDir;
            transform.DORotate(
                new Vector3(0f, 0f, transform.eulerAngles.z + rotSpd * 100f),
                100f,
                RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetRelative(false)
                .SetLoops(-1);
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_alive) return;

            float t = Time.time;

            // Sine wave offset
            float sine = Mathf.Sin(t * _sineFrequency + _sinePhase) * _sineAmplitude;

            // Toplam hareket
            Vector3 move = (Vector3)(_direction * _speed + _sineAxis * sine) * Time.deltaTime;
            transform.position += move;

            // Ekran dışına çıktı mı?
            if (_spawner != null && !_spawner.IsInsideKillBounds(transform.position))
            {
                Despawn();
            }
        }

        // ── Despawn ───────────────────────────────────────────────────────────

        private void Despawn()
        {
            if (!_alive) return;
            _alive = false;

            _breatheTween?.Kill();
            DOTween.Kill(transform);

            _fadeTween?.Kill();
            _fadeTween = DOTween.To(
                () => GetAlpha(),
                a  => SetAlpha(a),
                0f,
                0.35f)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    ClearCells();
                    _spawner?.OnPieceDespawned(this);
                });
        }

        // ── Cell building ─────────────────────────────────────────────────────

        private void BuildCells(PieceDefinition piece)
        {
            ClearCells();

            if (piece == null || _cellPrefab == null) return;

            var cells = piece.GetCells(Rotation.R0);
            if (cells == null || cells.Count == 0) return;

            // Cell size: manuel ayarlandıysa onu kullan, yoksa ekrana göre hesapla
            float effectiveCellSize = _cellSize > 0f
                ? _cellSize
                : Camera.main != null
                    ? (Camera.main.orthographicSize * 2f * Camera.main.aspect) * _cellScreenFraction
                    : 0.5f;

            // Merkez hesapla
            float cx = 0f, cy = 0f;
            foreach (var c in cells) { cx += c.x; cy += c.y; }
            cx /= cells.Count;
            cy /= cells.Count;

            Color col = piece.BlockColor;

            foreach (var c in cells)
            {
                var go = Instantiate(_cellPrefab, _cells);
                go.transform.localPosition = new Vector3(
                    (c.x - cx) * effectiveCellSize,
                    (c.y - cy) * effectiveCellSize,
                    0f);
                go.transform.localScale = Vector3.one * effectiveCellSize;

                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(col.r, col.g, col.b, 0f);
                    _renderers.Add(sr);
                }
            }
        }

        private void ClearCells()
        {
            _renderers.Clear();
            if (_cells == null) return;
            foreach (Transform child in _cells)
                Destroy(child.gameObject);
        }

        // ── Alpha helpers ─────────────────────────────────────────────────────

        private void SetAlpha(float a)
        {
            foreach (var sr in _renderers)
            {
                if (sr == null) continue;
                var c = sr.color;
                c.a    = a;
                sr.color = c;
            }
        }

        private float GetAlpha()
        {
            if (_renderers.Count == 0) return 0f;
            return _renderers[0] != null ? _renderers[0].color.a : 0f;
        }
    }
}