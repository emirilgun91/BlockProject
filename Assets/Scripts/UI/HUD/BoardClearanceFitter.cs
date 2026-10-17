using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Bir HUD panelini tahtanın ekran alanının DIŞINDA tutar.
    ///
    /// Neden gerekiyor: tahta dünya uzayında (ortografik kamera), panel ise
    /// Canvas'ta duruyor. İkisi ekran genişliğine bambaşka tepki veriyor —
    /// tahtanın ekrandaki sol kenarı 1/aspect ile hareket ederken, ScaleWithScreenSize
    /// + match 0.5 olan Canvas 1/sqrt(aspect) ile ölçekleniyor. 16:9'da kart envanteri
    /// tahtaya sadece birkaç piksel kala bitiyordu; en oranı biraz değişince panel
    /// tahtanın üstüne biniyor.
    ///
    /// Sabit bir kenar boşluğu bu yüzden işe yaramaz: panelin genişliği tahtanın
    /// GERÇEK ekran kenarından hesaplanmalı. Bu bileşen her ekran boyutu / kamera
    /// değişiminde paneli daraltıp (gerekirse küçültüp) boşluğu koruyor.
    ///
    /// Panelin sol kenarı sabit kalsın diye pivot ve anchor'ın sola (x = 0)
    /// yaslanmış olması beklenir — sahnedeki CardInventoryUI zaten öyle.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardClearanceFitter : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Boş bırakılırsa sahnedeki ilk BoardView bulunur.")]
        [SerializeField] private BoardView _board;

        [Tooltip("Boş bırakılırsa parent zincirindeki Canvas kullanılır.")]
        [SerializeField] private Canvas _canvas;

        [Tooltip("Boş bırakılırsa Camera.main kullanılır.")]
        [SerializeField] private Camera _camera;

        [Header("Layout")]
        [Tooltip("Panel ile tahtanın görünen sol kenarı arasındaki boşluk, 1080p ekran pikseli " +
                 "cinsinden (ekran yüksekliğiyle orantılı büyür/küçülür).")]
        [SerializeField] private float _gapPixels = 32f;

        [Tooltip("Panelin daralabileceği en küçük genişlik (panelin kendi birimi). Tek sütun " +
                 "kart + kenar payı sığmalı. Bunun da sığmadığı dar ekranlarda panel küçültülür.")]
        [SerializeField] private float _minWidth = 150f;

        [Tooltip("Daralmak yetmezse panel ekranın sol kenarına bu kadar (Canvas birimi) " +
                 "yaklaşabilir. 5:4 gibi dar ekranlarda tahtanın solunda yer açar.")]
        [SerializeField] private float _minLeftMargin = 24f;

        [Tooltip("Son çare küçültmenin alt sınırı. Altına inilirse ikonlar okunmaz olur, " +
                 "bunun yerine boşluğa taşmayı kabul ederiz.")]
        [SerializeField] private float _minScale = 0.24f;

        [Tooltip("Panelin ölçeği. 0 ise sahnedeki başlangıç ölçeği kullanılır.")]
        [SerializeField] private float _maxScale = 0f;

        private RectTransform _rt;
        private float _baseScale;
        private float _baseWidth;
        private float _baseLeft;
        private readonly Vector3[] _corners = new Vector3[4];

        // Son hesabın girdileri — hiçbiri değişmediyse yeniden hesaplamıyoruz.
        private Vector2    _lastScreen;
        private Vector3    _lastCamPos;
        private Quaternion _lastCamRot;
        private float      _lastFov;

        private void Awake()
        {
            _rt        = (RectTransform)transform;
            _baseScale = _maxScale > 0f ? _maxScale : Mathf.Max(0.01f, _rt.localScale.x);
            _baseWidth = Mathf.Max(_minWidth, _rt.sizeDelta.x);
            _baseLeft  = _rt.anchoredPosition.x;

            if (_board  == null) _board  = FindObjectOfType<BoardView>();
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_camera == null) _camera = Camera.main;
        }

        private void OnEnable() => Invalidate();

        /// <summary>Bir sonraki karede yeniden hesaplamaya zorlar.</summary>
        public void Invalidate() => _lastScreen = Vector2.zero;

        private void LateUpdate()
        {
            if (_board == null || _canvas == null) return;
            if (_camera == null) { _camera = Camera.main; if (_camera == null) return; }

            // Kamera rig'i (parallax, sarsıntı) her karede oynatabiliyor; projeksiyon
            // ucuz, ama değişmeyen karede hiç yazmıyoruz.
            var screen = new Vector2(Screen.width, Screen.height);
            var ct     = _camera.transform;
            if (screen == _lastScreen && ct.position == _lastCamPos &&
                ct.rotation == _lastCamRot && Mathf.Approximately(_camera.fieldOfView, _lastFov))
                return;

            _lastScreen = screen;
            _lastCamPos = ct.position;
            _lastCamRot = ct.rotation;
            _lastFov    = _camera.fieldOfView;

            Apply();
        }

        /// <summary>
        /// Önce paneli DARALTIR: genişlik azalınca CardInventoryUI sütun sayısını
        /// düşürüyor ve kartlar okunur boyutta kalıyor. Eskiden ölçek küçültülüyordu;
        /// 16:10'da kartlar 0.30 ölçeğe, 4:3'te alt sınıra iniyor ve panel tahtanın
        /// üstüne taşıyordu. Tek sütun bile sığmazsa panel ekranın soluna doğru kayar
        /// (5:4 gibi dar ekranlar); o da yetmezse son çare olarak küçültür.
        /// </summary>
        private void Apply()
        {
            float boardLeftScreenX = BoardVisibleLeftScreenX();
            float scaleFactor      = Mathf.Max(0.0001f, _canvas.scaleFactor);

            // Panelin sol kenarının ekran x'i. Pivot/anchor sola yaslı olduğu için
            // genişlik/ölçek değişse de bu kenar yerinde kalır — geri besleme döngüsü olmaz.
            // anchoredPosition.x = 0 olsaydı kenarın nerede olacağı da buradan çıkar.
            _rt.GetWorldCorners(_corners);
            var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            float panelLeftScreenX = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]).x;
            float zeroLeftScreenX  = panelLeftScreenX - _rt.anchoredPosition.x * scaleFactor;

            float gap   = _gapPixels * Screen.height / 1080f;
            float right = boardLeftScreenX - gap;          // panelin sağ kenarı en fazla buraya

            // 1) Normal konumda, ölçek sabit: sığan genişlik (panel birimi)
            float left  = zeroLeftScreenX + _baseLeft * scaleFactor;
            float width = (right - left) / (scaleFactor * _baseScale);
            if (width >= _minWidth)
            {
                SetLeft(_baseLeft);
                SetWidth(Mathf.Min(width, _baseWidth));
                SetScale(_baseScale);
                return;
            }

            // 2) Tek sütun genişliğinde, ekranın soluna doğru kayarak
            SetWidth(_minWidth);
            float neededLeft = (right - _minWidth * scaleFactor * _baseScale - zeroLeftScreenX) / scaleFactor;
            float leftUnits  = Mathf.Clamp(neededLeft, Mathf.Min(_minLeftMargin, _baseLeft), _baseLeft);
            SetLeft(leftUnits);

            // 3) Hâlâ sığmıyorsa küçült
            float availablePx = right - (zeroLeftScreenX + leftUnits * scaleFactor);
            float fitScale    = availablePx > 0f ? availablePx / (_minWidth * scaleFactor) : 0f;
            SetScale(Mathf.Clamp(fitScale, _minScale, _baseScale));
        }

        private void SetLeft(float x)
        {
            var p = _rt.anchoredPosition;
            if (Mathf.Abs(p.x - x) < 0.5f) return;
            _rt.anchoredPosition = new Vector2(x, p.y);
        }

        /// <summary>
        /// Tahtanın ekrandaki en sol noktası. Origin'in izdüşümü yetmiyor: 2.5D
        /// kamerada karolar TileSkirt ile kameraya doğru kalkık, kameraya yakın alt
        /// kenar origin'den sola taşıyor. Tahta kutusunun 8 köşesi izdüşürülür.
        /// Renderer sınırları kullanılmaz: açılışta karolar küçükten büyüyerek
        /// geliyor ve o anda okunan sınır tahtayı olduğundan dar gösteriyordu.
        /// </summary>
        private float BoardVisibleLeftScreenX()
        {
            if (!_board.TryGetWorldBox(out var mn, out var mx))
                return _camera.WorldToScreenPoint(new Vector3(_board.OriginWorld.x, _board.OriginWorld.y, 0f)).x;

            float left = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3((i & 1) == 0 ? mn.x : mx.x, (i & 2) == 0 ? mn.y : mx.y, (i & 4) == 0 ? mn.z : mx.z);
                left = Mathf.Min(left, _camera.WorldToScreenPoint(p).x);
            }
            return left;
        }

        private void SetWidth(float w)
        {
            // 4 birimlik adımlar: kamera parallax'ı her karede kıpırdatırken layout'u
            // (ve CardInventoryUI'ın sütun hesabını) sürekli tetiklemesin.
            w = Mathf.Floor(w / 4f) * 4f;
            var size = _rt.sizeDelta;
            if (Mathf.Abs(size.x - w) < 0.5f) return;
            _rt.sizeDelta = new Vector2(w, size.y);
        }

        private void SetScale(float s)
        {
            var current = _rt.localScale;
            if (Mathf.Approximately(current.x, s)) return;
            _rt.localScale = new Vector3(s, s, 1f);
        }
    }
}
