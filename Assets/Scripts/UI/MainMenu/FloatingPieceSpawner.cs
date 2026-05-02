using System.Collections.Generic;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Ana menü arka planındaki süzülen blok parçalarını yönetir.
    ///
    /// Sahneye tek bir obje olarak koy — FloatingPieceView prefabını bağla.
    /// ShapeLibrary'den rastgele şekil seçer, kenardan kenardan gönderir.
    ///
    /// 3 Katman:
    ///   Layer 1 — Yakın : büyük, hızlı, opak
    ///   Layer 2 — Orta  : orta
    ///   Layer 3 — Uzak  : küçük, yavaş, saydam
    ///
    /// Sahne Kurulumu:
    ///   FloatingPieceSpawner objesini MainMenu sahnesine ekle.
    ///   Inspector'da ShapeLibrary ve FloatingPiece prefabını bağla.
    ///   KillMargin: kameranın dışına ne kadar uzak çıkınca yok edilsin (3-5 önerilir)
    /// </summary>
    public sealed class FloatingPieceSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ShapeLibrarySO   _shapeLibrary;
        [SerializeField] private FloatingPieceView _piecePrefab;
        [SerializeField] private Camera            _camera;
        [Tooltip("Parçalar bu transform'un child'ı olarak spawn edilir. Boş bırakılırsa scene root'u kullanılır.")]
        [SerializeField] private Transform         _pieceRoot;

        [Header("Pool")]
        [SerializeField] private int _poolSize = 10;

        [Header("Spawn")]
        [Tooltip("Kaç saniyede bir yeni parça spawn edilsin")]
        [SerializeField] private float _spawnInterval = 1.2f;
        [Tooltip("Aynı anda maksimum aktif parça")]
        [SerializeField] private int   _maxActive      = 10;

        [Header("Layer Dağılımı")]
        [SerializeField] private int _layerCount1 = 3; // Yakın
        [SerializeField] private int _layerCount2 = 3; // Orta
        [SerializeField] private int _layerCount3 = 4; // Uzak

        [Header("Speed Ranges (birim/sn)")]
        [SerializeField] private float _speedMin1 = 1.2f, _speedMax1 = 2.0f;
        [SerializeField] private float _speedMin2 = 0.7f, _speedMax2 = 1.2f;
        [SerializeField] private float _speedMin3 = 0.3f, _speedMax3 = 0.7f;

        [Header("Sine Wave")]
        [SerializeField] private float _sineAmpMin  = 0.05f, _sineAmpMax  = 0.20f;
        [SerializeField] private float _sineFreqMin = 0.3f,  _sineFreqMax = 0.8f;

        [Header("Bounds")]
        [Tooltip("Kill bölgesi kamera dışına bu kadar ekstra (world unit)")]
        [SerializeField] private float _killMargin = 5f;

        // ── Pool ─────────────────────────────────────────────────────────────
        private readonly List<FloatingPieceView> _pool   = new();
        private readonly List<FloatingPieceView> _active = new();
        private float _timer;

        // ── Layer queue ───────────────────────────────────────────────────────
        // Spawn sırasında hangi layer'ın kullanılacağını dengeli dağıt
        private readonly Queue<int> _layerQueue = new();

        // ── Bounds cache ──────────────────────────────────────────────────────
        private float   _camHalfH, _camHalfW;
        private Vector3 _camPos;
        private Vector3 _worldBL, _worldTR; // bottom-left, top-right world corners

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;

            // Pool oluştur — scale kirliliğini önlemek için scene root'a veya _pieceRoot'a spawn et
            Transform root = _pieceRoot != null ? _pieceRoot : null;
            for (int i = 0; i < _poolSize; i++)
            {
                var obj = root != null
                    ? Instantiate(_piecePrefab, root)
                    : Instantiate(_piecePrefab);
                obj.Init(this);
                obj.gameObject.SetActive(false);
                _pool.Add(obj);
            }

            // Layer queue'yu doldur
            RebuildLayerQueue();
        }

        private void Start()
        {
            RefreshBounds();
            Debug.Log($"[FloatingPieceSpawner] Bounds → BL:{_worldBL} TR:{_worldTR} | CamPos:{_camPos}");

            // Başlangıçta sahneyi biraz dolduralım
            int initialCount = Mathf.Min(6, _maxActive);
            for (int i = 0; i < initialCount; i++)
                SpawnOne(randomScreenPos: true);
        }

        private void Update()
        {
            RefreshBounds();

            _timer += Time.deltaTime;
            if (_timer >= _spawnInterval)
            {
                _timer = 0f;
                if (_active.Count < _maxActive)
                    SpawnOne(randomScreenPos: false);
            }
        }

        // ── Spawn ─────────────────────────────────────────────────────────────

        private void SpawnOne(bool randomScreenPos)
        {
            var piece = GetFromPool();
            if (piece == null) return;

            int layer = NextLayer();

            // Hız
            float speed = layer switch
            {
                1 => Random.Range(_speedMin1, _speedMax1),
                2 => Random.Range(_speedMin2, _speedMax2),
                _ => Random.Range(_speedMin3, _speedMax3),
            };

            // Sine
            float amp  = Random.Range(_sineAmpMin, _sineAmpMax);
            float freq = Random.Range(_sineFreqMin, _sineFreqMax);

            // Spawn pozisyon + yön
            Vector3 startPos;
            Vector2 dir;
            if (randomScreenPos)
            {
                // Ekran içinde rastgele nokta
                startPos = RandomInsideScreen();
                dir = Random.insideUnitCircle.normalized;
                if (dir == Vector2.zero) dir = Vector2.right;
            }
            else
            {
                GetEdgeSpawn(out startPos, out dir);
            }

            // Rotation başlangıcı
            float rot = Random.Range(0f, 360f);

            // Shape seç — PieceDefinition olarak (GetCells destekliyor)
            PieceDefinition pieceDef = ShapeSpawnService.GetRandomWeighted(_shapeLibrary);
            if (pieceDef == null) return;

            piece.gameObject.SetActive(true);
            piece.Spawn(pieceDef, startPos, dir, layer, speed, amp, freq, rot);
            _active.Add(piece);
        }

        // ── Pool callbacks ────────────────────────────────────────────────────

        public void OnPieceDespawned(FloatingPieceView piece)
        {
            _active.Remove(piece);
            piece.gameObject.SetActive(false);
            _pool.Add(piece);
        }

        // ── Bounds ───────────────────────────────────────────────────────────

        public bool IsInsideKillBounds(Vector3 worldPos)
        {
            float m = _killMargin;
            return
                worldPos.x > _worldBL.x - m &&
                worldPos.x < _worldTR.x + m &&
                worldPos.y > _worldBL.y - m &&
                worldPos.y < _worldTR.y + m;
        }

        private void RefreshBounds()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            _camPos = _camera.transform.position;

            // URP ve Orthographic kamera için Viewport buglarını ezip geçen saf matematik:
            if (_camera.orthographic)
            {
                _camHalfH = _camera.orthographicSize;
                _camHalfW = _camHalfH * _camera.aspect;
            }
            else
            {
                // Kazara Perspective kamera kullanılıyorsa fallback
                float distToPlane = Mathf.Abs(_camPos.z);
                if (distToPlane < 0.01f) distToPlane = 10f;
                _camHalfH = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distToPlane;
                _camHalfW = _camHalfH * _camera.aspect;
            }

            // Z eksenini 0'a sabitleyerek sol-alt ve sağ-üst sınırları belirliyoruz
            _worldBL = new Vector3(_camPos.x - _camHalfW, _camPos.y - _camHalfH, 0f);
            _worldTR = new Vector3(_camPos.x + _camHalfW, _camPos.y + _camHalfH, 0f);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private FloatingPieceView GetFromPool()
        {
            if (_pool.Count == 0) return null;
            var p = _pool[_pool.Count - 1];
            _pool.RemoveAt(_pool.Count - 1);
            return p;
        }

        private void RebuildLayerQueue()
        {
            _layerQueue.Clear();
            // Her layer sayısı kadar queue'ya ekle, shuffle
            var list = new List<int>();
            for (int i = 0; i < _layerCount1; i++) list.Add(1);
            for (int i = 0; i < _layerCount2; i++) list.Add(2);
            for (int i = 0; i < _layerCount3; i++) list.Add(3);

            // Fisher-Yates shuffle
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            foreach (var l in list) _layerQueue.Enqueue(l);
        }

        private int NextLayer()
        {
            if (_layerQueue.Count == 0) RebuildLayerQueue();
            return _layerQueue.Dequeue();
        }

        /// <summary>
        /// Ekran kenarından rastgele bir spawn noktası ve karşı tarafa doğru yön döndürür.
        /// </summary>
        private void GetEdgeSpawn(out Vector3 pos, out Vector2 dir)
        {
            RefreshBounds();

            float left   = _worldBL.x;
            float right  = _worldTR.x;
            float bottom = _worldBL.y;
            float top    = _worldTR.y;
            float margin = _camHalfW * 0.15f; // ekran genişliğinin %15'i kadar dışarıda başla

            int edge = Random.Range(0, 4);

            switch (edge)
            {
                case 0: // Sol → sağa
                    pos = new Vector3(left - margin, Random.Range(bottom, top), 0f);
                    dir = new Vector2(1f, Random.Range(-0.35f, 0.35f));
                    break;
                case 1: // Sağ → sola
                    pos = new Vector3(right + margin, Random.Range(bottom, top), 0f);
                    dir = new Vector2(-1f, Random.Range(-0.35f, 0.35f));
                    break;
                case 2: // Alt → yukarı
                    pos = new Vector3(Random.Range(left, right), bottom - margin, 0f);
                    dir = new Vector2(Random.Range(-0.35f, 0.35f), 1f);
                    break;
                default: // Üst → aşağı
                    pos = new Vector3(Random.Range(left, right), top + margin, 0f);
                    dir = new Vector2(Random.Range(-0.35f, 0.35f), -1f);
                    break;
            }

            dir = dir.normalized;
        }

        private Vector3 RandomInsideScreen()
        {
            RefreshBounds();
            return new Vector3(
                Mathf.Lerp(_worldBL.x, _worldTR.x, Random.Range(0.1f, 0.9f)),
                Mathf.Lerp(_worldBL.y, _worldTR.y, Random.Range(0.1f, 0.9f)),
                0f);
        }
    }
}