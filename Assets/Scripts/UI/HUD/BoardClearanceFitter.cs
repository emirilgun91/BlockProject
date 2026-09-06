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
    /// değişiminde paneli ölçekleyip boşluğu koruyor.
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
        [Tooltip("Panel ile tahtanın sol kenarı arasında bırakılacak boşluk (ekran pikseli).")]
        [SerializeField] private float _gapPixels = 24f;

        [Tooltip("Panelin küçülebileceği en düşük ölçek. Altına inilirse ikonlar okunmaz olur, " +
                 "bunun yerine boşluğa taşmayı kabul ederiz.")]
        [SerializeField] private float _minScale = 0.24f;

        [Tooltip("Panelin büyüyebileceği en yüksek ölçek. 0 ise sahnedeki başlangıç ölçeği tavan olur.")]
        [SerializeField] private float _maxScale = 0f;

        private RectTransform _rt;
        private float _baseScale;
        private readonly Vector3[] _corners = new Vector3[4];

        // Son hesabın girdileri — hiçbiri değişmediyse yeniden hesaplamıyoruz.
        private Vector2 _lastScreen;
        private float   _lastOrthoSize;
        private float   _lastCamX;
        private float   _lastRectWidth;

        private void Awake()
        {
            _rt        = (RectTransform)transform;
            _baseScale = _maxScale > 0f ? _maxScale : Mathf.Max(0.01f, _rt.localScale.x);

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

            var screen = new Vector2(Screen.width, Screen.height);
            float ortho = _camera.orthographicSize;
            float camX  = _camera.transform.position.x;
            float rectW = _rt.rect.width;

            if (screen == _lastScreen &&
                Mathf.Approximately(ortho, _lastOrthoSize) &&
                Mathf.Approximately(camX,  _lastCamX) &&
                Mathf.Approximately(rectW, _lastRectWidth))
                return;

            _lastScreen    = screen;
            _lastOrthoSize = ortho;
            _lastCamX      = camX;
            _lastRectWidth = rectW;

            Apply(rectW);
        }

        private void Apply(float rectWidth)
        {
            if (rectWidth <= 1f) return;

            // Tahtanın sol kenarının ekran x'i. Origin sol-alt köşe, y'nin önemi yok.
            float boardLeftScreenX = _camera
                .WorldToScreenPoint(new Vector3(_board.OriginWorld.x, _board.OriginWorld.y, 0f)).x;

            // Panelin sol kenarının ekran x'i. Pivot/anchor sola yaslı olduğu için
            // ölçek değişse de bu kenar yerinde kalır — geri besleme döngüsü olmaz.
            _rt.GetWorldCorners(_corners);
            var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            float panelLeftScreenX = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]).x;

            float availablePx = boardLeftScreenX - _gapPixels - panelLeftScreenX;
            if (availablePx <= 0f) { SetScale(_minScale); return; }

            // rectWidth Canvas birimi; ekran pikseline çevirmek için scaleFactor gerekir.
            float scaleFactor = Mathf.Max(0.0001f, _canvas.scaleFactor);
            float fitScale    = availablePx / (rectWidth * scaleFactor);

            SetScale(Mathf.Clamp(fitScale, _minScale, _baseScale));
        }

        private void SetScale(float s)
        {
            var current = _rt.localScale;
            if (Mathf.Approximately(current.x, s)) return;
            _rt.localScale = new Vector3(s, s, 1f);
        }
    }
}
