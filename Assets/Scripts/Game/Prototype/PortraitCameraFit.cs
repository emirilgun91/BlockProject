using RogueBlockBlast.UI;
using UnityEngine;

namespace RogueBlockBlast.Game.Prototype
{
    /// <summary>
    /// Portrait sahnede kadrajı ekranın en-boy oranına göre kurar.
    ///
    /// Landscape'te kadrajı yükseklik belirler (<c>orthographicSize 5</c>); dikey
    /// ekranda ise sınır GENİŞLİK: tahta (8 × 0.85 = 6.8) + kenar payı ekrana
    /// sığmalı. Buradan gereken ortografik yarı-yüksekliği hesaplar ve
    /// <see cref="BoardCameraRig"/>'e yazar.
    ///
    /// Dikey yerleşim: HUD üstte, havuz ve kart şeridi altta. Tahtanın merkezi
    /// ekranın <see cref="_boardViewportY"/> yüksekliğine gelecek şekilde odak
    /// noktası kaydırılır.
    ///
    /// Yalnızca en-boy oranı değişince yeniden hesaplar (her karede değil).
    /// </summary>
    [RequireComponent(typeof(BoardCameraRig))]
    public sealed class PortraitCameraFit : MonoBehaviour
    {
        [SerializeField] private BoardView _board;
        [SerializeField] private int _boardWidth  = 8;
        [SerializeField] private int _boardHeight = 8;

        [Tooltip("Tahtanın iki yanında bırakılacak pay (hücre biriminde).")]
        [SerializeField] private float _sideMarginCells = 0.55f;

        [Tooltip("Tahta merkezinin ekrandaki dikey konumu (0 = alt, 1 = üst). " +
                 "Üstte HUD, altta havuz + kart şeridi olduğu için merkezin biraz üstü.")]
        [Range(0.3f, 0.7f)]
        [SerializeField] private float _boardViewportY = 0.555f;

        [Tooltip("Eğimli kamerada tahtanın üst kenarı küçük görünür; genişlik alt kenardan " +
                 "ölçülür. Bu çarpan o farkı telafi eder.")]
        [SerializeField] private float _tiltWidthCompensation = 1.06f;

        private BoardCameraRig _rig;
        private Camera         _cam;
        private float          _lastAspect = -1f;

        private void Awake()
        {
            _rig = GetComponent<BoardCameraRig>();
            _cam = GetComponent<Camera>();
            if (_board == null) _board = FindAnyObjectByType<BoardView>();
        }

        private void OnEnable() => _lastAspect = -1f;

        private void Update()
        {
            if (_cam == null || _board == null) return;
            float aspect = _cam.aspect;
            if (Mathf.Abs(aspect - _lastAspect) < 0.0005f) return;
            _lastAspect = aspect;
            Apply(aspect);
        }

        /// <summary>Editör kurucusu da çağırır — sahne kaydedilmeden önce kadraj doğru olsun.</summary>
        public void Apply(float aspect)
        {
            if (_rig == null) _rig = GetComponent<BoardCameraRig>();
            if (_board == null) _board = FindAnyObjectByType<BoardView>();
            if (_rig == null || _board == null || aspect <= 0f) return;

            float cell      = _board.CellSize;
            float halfWidth = (_boardWidth * cell * 0.5f + _sideMarginCells * cell) * _tiltWidthCompensation;

            // Genişliğe sığdırmak için gereken yarı-yükseklik; ekran yatay ise
            // yükseklik sınırı (tahta + pay) daha büyük olabilir — büyüğü alınır.
            float byWidth  = halfWidth / aspect;
            float byHeight = _boardHeight * cell * 0.5f + _sideMarginCells * cell;
            float ortho    = Mathf.Max(byWidth, byHeight);

            _rig.MatchOrthographicSize = ortho;

            // Tahta merkezini ekranın _boardViewportY yüksekliğine getir:
            // odak noktasını tersine kaydır (odak ekran ortasında görünür).
            Vector2 center = _board.OriginWorld + new Vector2(_boardWidth * cell * 0.5f, _boardHeight * cell * 0.5f);
            float shift = (_boardViewportY - 0.5f) * 2f * ortho;
            _rig.AimPoint = new Vector2(center.x, center.y - shift);
            _rig.Refresh();
        }
    }
}
