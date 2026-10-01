using System.Collections.Generic;
using RogueBlockBlast.Core.Settings;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Satır tamamlama ipucunun görseli: tamamlanacak her satır / sütunun etrafında
    /// ince, parçanın renginde parlayan bir çerçeve.
    ///
    /// Eskiden ipucu hattaki blokları parça rengine / beyaza doğru kaydırıyordu;
    /// renkler soluyordu ve "başarı" değil "sönme" gibi okunuyordu. Artık bloklar
    /// kendi renginde kalır, ipucu yalnızca hattın ÇEVRESİNE çizilir.
    ///
    /// His: çerçeve belirirken hafifçe büyükten oturur (ease-out-back) ve üzerinde
    /// hat boyunca bir ışık akar. ReduceMotion açıkken ikisi de kapanır, çerçeve
    /// sabit kalır.
    ///
    /// Kurulum gerekmez — <see cref="Instance"/> ilk çağrıda kendini yaratır,
    /// shader'ı <c>Resources/FX/LineHintOutline</c>'dan yükler. RunController her
    /// karede <see cref="Show"/> çağırır; boş liste = hepsi gizli.
    /// </summary>
    public sealed class LineHintOutline : MonoBehaviour
    {
        private static LineHintOutline _instance;

        public static LineHintOutline Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindAnyObjectByType<LineHintOutline>();
                if (_instance == null)
                    _instance = new GameObject("LineHintOutline").AddComponent<LineHintOutline>();
                return _instance;
            }
        }

        [Header("Render")]
        [SerializeField] private string _sortingLayer = "Board";
        [Tooltip("Tile'ların üstünde, line clear efektlerinin (400) altında.")]
        [SerializeField] private int    _sortingOrder = 350;
        [Tooltip("Tile yüzeyinin biraz önünde (TileSkirt tile'ları ~0.22 kaldırıyor).")]
        [SerializeField] private float  _zOffset      = -0.02f;

        [Header("Shape (hücre biriminde)")]
        [Tooltip("Çerçevenin hattın dışına taşma payı.")]
        [SerializeField] private float _outset    = 0.10f;
        [SerializeField] private float _radius    = 0.16f;
        [SerializeField] private float _thickness = 0.028f;
        [SerializeField] private float _glow      = 0.10f;

        [Header("Look")]
        [Tooltip("HDR çarpanı — bloom'u besler.")]
        [SerializeField] private float _intensity  = 1.25f;
        [Tooltip("Akan ışığın bir turu (sn).")]
        [SerializeField] private float _sweepPeriod = 1.6f;
        [Tooltip("Belirme animasyonu (sn): büyükten oturma.")]
        [SerializeField] private float _popTime     = 0.32f;
        [SerializeField] private float _popScale    = 0.10f;

        private Shader   _shader;
        private Material _mat;
        private Mesh     _quad;
        private bool     _broken;
        private MaterialPropertyBlock _mpb;

        private readonly List<MeshRenderer>       _pool      = new List<MeshRenderer>();
        private readonly Dictionary<int, float>   _appearAt  = new Dictionary<int, float>();
        private readonly HashSet<int>             _liveKeys  = new HashSet<int>();
        private readonly List<int>                _staleKeys = new List<int>();

        private static readonly int ID_Color     = Shader.PropertyToID("_Color");
        private static readonly int ID_Size      = Shader.PropertyToID("_Size");
        private static readonly int ID_Inset     = Shader.PropertyToID("_Inset");
        private static readonly int ID_Radius    = Shader.PropertyToID("_Radius");
        private static readonly int ID_Thickness = Shader.PropertyToID("_Thickness");
        private static readonly int ID_Glow      = Shader.PropertyToID("_Glow");
        private static readonly int ID_Sweep     = Shader.PropertyToID("_Sweep");
        private static readonly int ID_Fade      = Shader.PropertyToID("_Fade");

        /// <summary>
        /// Verilen satır / sütunların çevresine çerçeve çizer; listede olmayanları gizler.
        /// <paramref name="strength"/> 0..1 genel görünürlük (RunController'ın belirme / sönme eğrisi).
        /// </summary>
        public void Show(BoardView board, IReadOnlyList<int> rows, IReadOnlyList<int> cols,
                         int width, int height, Color color, float strength)
        {
            _lastShowFrame = Time.frameCount;
            int count = (rows?.Count ?? 0) + (cols?.Count ?? 0);
            if (board == null || count == 0 || strength <= 0.001f || !EnsureBuilt())
            {
                HideFrom(0);
                _appearAt.Clear();
                return;
            }

            float cell   = board.CellSize;
            Vector2 org  = board.OriginWorld;
            float z      = SurfaceZ(board) + _zOffset;
            bool  motion = !GameSettings.ReduceMotion;
            float now    = Time.unscaledTime;

            color.a = 1f;
            Color hdr = Saturate(color) * _intensity;

            // Belirme zamanları: yeni gelen hat kendi pop'unu oynar, sabit kalanlar oynamaz
            _liveKeys.Clear();
            if (rows != null) foreach (int y in rows) _liveKeys.Add(y);
            if (cols != null) foreach (int x in cols) _liveKeys.Add(1000 + x);
            _staleKeys.Clear();
            foreach (var k in _appearAt.Keys) if (!_liveKeys.Contains(k)) _staleKeys.Add(k);
            foreach (var k in _staleKeys) _appearAt.Remove(k);
            foreach (var k in _liveKeys) if (!_appearAt.ContainsKey(k)) _appearAt[k] = now;

            float sweep = motion ? Mathf.Repeat(now / Mathf.Max(0.1f, _sweepPeriod), 1f) * 1.3f - 0.15f : -1f;

            int used = 0;
            if (rows != null)
                foreach (int y in rows)
                {
                    var center = new Vector3(org.x + width * cell * 0.5f, org.y + (y + 0.5f) * cell, z);
                    Draw(used++, center, new Vector2(width * cell, cell), cell, hdr, strength, _appearAt[y], now, motion, sweep);
                }
            if (cols != null)
                foreach (int x in cols)
                {
                    var center = new Vector3(org.x + (x + 0.5f) * cell, org.y + height * cell * 0.5f, z);
                    Draw(used++, center, new Vector2(cell, height * cell), cell, hdr, strength, _appearAt[1000 + x], now, motion, sweep);
                }

            HideFrom(used);
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void Draw(int index, Vector3 center, Vector2 lineSize, float cell, Color hdr,
                          float strength, float appearAt, float now, bool motion, float sweep)
        {
            var mr = Get(index);

            // Büyükten oturma (ease-out-back) + aynı sürede belirme
            float k    = Mathf.Clamp01((now - appearAt) / Mathf.Max(0.01f, _popTime));
            float pop  = motion ? 1f + _popScale * (1f - EaseOutBack(k)) : 1f;
            float fade = strength * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.6f));

            float pad  = (_outset + _glow * 1.4f) * cell;
            var   size = new Vector2(lineSize.x + pad * 2f, lineSize.y + pad * 2f) * pop;

            var tr = mr.transform;
            tr.position   = center;
            tr.rotation   = Quaternion.identity;
            tr.localScale = new Vector3(size.x, size.y, 1f);

            _mpb.Clear();
            _mpb.SetColor (ID_Color,     hdr);
            _mpb.SetVector(ID_Size,      new Vector4(size.x, size.y, 0f, 0f));
            _mpb.SetFloat (ID_Inset,     (_glow * 1.4f) * cell * pop);
            _mpb.SetFloat (ID_Radius,    _radius * cell);
            _mpb.SetFloat (ID_Thickness, _thickness * cell);
            _mpb.SetFloat (ID_Glow,      _glow * cell);
            _mpb.SetFloat (ID_Sweep,     sweep);
            _mpb.SetFloat (ID_Fade,      fade);
            mr.SetPropertyBlock(_mpb);

            if (!mr.gameObject.activeSelf) mr.gameObject.SetActive(true);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private static Color Saturate(Color c)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return m < 0.001f ? Color.white : new Color(c.r / m, c.g / m, c.b / m, 1f);
        }

        private static float SurfaceZ(BoardView board)
        {
            var t = board.GetTile(0, 0);
            return t != null ? t.transform.position.z : 0f;
        }

        private MeshRenderer Get(int index)
        {
            while (_pool.Count <= index)
            {
                var go = new GameObject("LineHint");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial    = _mat;
                mr.sortingLayerName  = _sortingLayer;
                mr.sortingOrder      = _sortingOrder;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows    = false;
                go.SetActive(false);
                _pool.Add(mr);
            }
            return _pool[index];
        }

        private void HideFrom(int index)
        {
            for (int i = index; i < _pool.Count; i++)
                if (_pool[i] != null && _pool[i].gameObject.activeSelf)
                    _pool[i].gameObject.SetActive(false);
        }

        private bool EnsureBuilt()
        {
            if (_mat != null) return true;
            if (_broken) return false;

            _shader = Resources.Load<Shader>("FX/LineHintOutline");
            if (_shader == null)
            {
                Debug.LogWarning("[LineHintOutline] Resources/FX/LineHintOutline shader'ı bulunamadı — ipucu çerçevesi kapalı.");
                _broken = true;
                return false;
            }

            _mat  = new Material(_shader) { name = "LineHintOutline (runtime)" };
            _mpb  = new MaterialPropertyBlock();
            _quad = new Mesh { name = "LineHintQuad" };
            _quad.vertices  = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
            _quad.uv        = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.bounds    = new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
            return true;
        }

        private int _lastShowFrame = -1;

        /// <summary>
        /// Show bu karede çağrılmadıysa (pause, kart seçimi, game over — RunController
        /// Update'i erken döner) çerçeveler ekranda asılı kalmasın.
        /// </summary>
        private void LateUpdate()
        {
            if (_lastShowFrame == Time.frameCount) return;
            HideFrom(0);
            _appearAt.Clear();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_mat  != null) Destroy(_mat);
            if (_quad != null) Destroy(_quad);
        }
    }
}
