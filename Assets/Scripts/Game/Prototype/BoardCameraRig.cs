using RogueBlockBlast.Core.Settings;
using RogueBlockBlast.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RogueBlockBlast.Game.Prototype
{
    /// <summary>
    /// 2.5D ana sahnesi için perspektif kamera rig'i.
    ///
    /// Tasarım kuralı: <b>Tilt = 0 iken kadraj mevcut ortografik sahneyle birebir
    /// aynı olmalı.</b> Böylece prototipte tek değişken eğimin kendisi olur —
    /// "his doğru mu" sorusu başka hiçbir farkın gölgesinde kalmaz.
    ///
    /// Bunu sağlamak için kamera, <see cref="AimPoint"/> etrafında
    /// <see cref="_tiltDegrees"/> kadar döndürülür ve öyle bir mesafeye konur ki
    /// o noktadaki dikey görüş yarı-genişliği <see cref="_matchOrthographicSize"/>
    /// değerine eşit çıkar:
    ///
    ///     distance = orthoSize / tan(fov / 2)
    ///
    /// Tahta dünya z = 0 düzleminde durduğu için hücre seçimi
    /// <see cref="BoardView"/> içindeki ışın-düzlem kesişimiyle çalışır —
    /// bu rig'in seçimle ilgili hiçbir şey yapması gerekmez.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class BoardCameraRig : MonoBehaviour
    {
        [Header("Framing")]
        [Tooltip("Kameranın etrafında döndüğü ve odaklandığı dünya noktası. " +
                 "Mevcut sahnedeki ortografik kamera konumu: (4, 4).")]
        public Vector2 AimPoint = new Vector2(4f, 4f);

        [Tooltip("Eşleşilecek ortografik boyut. Mevcut sahnede 5.")]
        [SerializeField] private float _matchOrthographicSize = 5f;

        [Tooltip("Dikey görüş açısı. Mevcut kameranın kayıtlı FOV değeri 34.")]
        [Range(10f, 80f)]
        [SerializeField] private float _fieldOfView = 34f;

        [Header("Tilt")]
        [Tooltip("Tahtanın geriye yatma miktarı. 0 = mevcut sahneyle aynı kadraj.\n\n" +
                 "Pozitif: üst kenar geriye kaçar (masa hissi) — istenen yön.\n" +
                 "Negatif: üst kenar öne gelir (yukarıdan bakış).")]
        [Range(-25f, 55f)]
        [SerializeField] private float _tiltDegrees = 26f;

        [Header("Idle Sway")]
        [Tooltip("Kameranın hafif nefes alması. GameSettings.ReduceMotion açıkken devre dışı kalır.")]
        [SerializeField] private bool  _idleSway         = true;
        [SerializeField] private float _swayAmplitude    = 0.06f;
        [SerializeField] private float _swaySpeed        = 0.35f;

        [Header("Mouse Parallax")]
        [Tooltip("Fareye göre kameranın çok hafif kayması — sahneyi canlı tutar.")]
        [SerializeField] private bool  _mouseParallax    = true;
        [SerializeField] private float _parallaxStrength = 0.22f;
        [SerializeField] private float _parallaxDamping  = 6f;

        private Camera  _cam;
        private Vector2 _parallaxOffset;
        private float   _noiseSeed;

        // ── Public ───────────────────────────────────────────────────────────

        /// <summary>Eğim açısı — runtime'da (Inspector'dan veya koddan) değiştirilebilir.</summary>
        public float TiltDegrees
        {
            get => _tiltDegrees;
            set => _tiltDegrees = Mathf.Clamp(value, -25f, 55f);
        }

        /// <summary>Odak noktasının dünya konumu (z = 0 düzleminde).</summary>
        public Vector3 AimWorld => new Vector3(AimPoint.x, AimPoint.y, 0f);

        /// <summary>
        /// Tahtanın merkezine göre otomatik hizalama — Editor tool'u bunu
        /// kullanarak AimPoint'i doldurabilir. Mevcut sahnede kamera tahtanın
        /// tam merkezinde değil (kadrajın altında pool UI'ı var), o yüzden
        /// bu opsiyoneldir.
        /// </summary>
        public void AimAtBoardCenter(BoardView board, int width, int height)
        {
            if (board == null) return;
            AimPoint = board.OriginWorld + new Vector2(
                width  * board.CellSize * 0.5f,
                height * board.CellSize * 0.5f);
        }

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            _cam       = GetComponent<Camera>();
            _noiseSeed = Random.value * 100f;
        }

        private void OnEnable()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            Apply();
        }

        private void LateUpdate()
        {
            // Edit mode'da her repaint'te transform yazmak sahneyi sürekli kirletir.
            // Editörde kadraj OnEnable/OnValidate ile güncellenir, bu yeterli.
            if (!Application.isPlaying) return;

            UpdateParallax();
            Apply();
        }

        /// <summary>
        /// Kadrajı hemen yeniden hesaplar. Editor tool'ları alanları yazdıktan
        /// sonra bunu çağırır — bir sonraki OnValidate'i beklemeye gerek kalmaz.
        /// </summary>
        public void Refresh()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            _matchOrthographicSize = Mathf.Max(0.01f, _matchOrthographicSize);
            if (!Application.isPlaying) Apply();
        }
#endif

        // ── Private ──────────────────────────────────────────────────────────

        /// <summary>
        /// Kameranın projeksiyonunu, mesafesini ve dönüşünü tek yerden yazar.
        /// Her frame çağrılır; hesap birkaç trigonometrik işlemden ibaret.
        /// </summary>
        private void Apply()
        {
            if (_cam == null) return;

            _cam.orthographic = false;
            _cam.fieldOfView  = _fieldOfView;

            float distance = DistanceForOrthoMatch();

            // İşaret neden negatif: tahta XY düzleminde duran dikey bir yüzey.
            // Kamerayı yukarı alıp aşağı baktırmak, duvarın ÜST kenarını kameraya
            // yaklaştırır — üst sıra alt sıradan geniş görünür, istediğimizin tersi.
            // Kamerayı odak noktasının ALTINA alıp yukarı baktırınca üst kenar
            // uzaklaşır ve tahta geriye yatmış bir masa gibi okunur.
            var rotation = Quaternion.Euler(-_tiltDegrees, 0f, 0f);
            var offset   = rotation * (Vector3.back * distance);

            Vector3 aim = AimWorld + SwayOffset() + (Vector3)_parallaxOffset;

            transform.SetPositionAndRotation(aim + offset, rotation);

            // Eğim arttıkça tahtanın uzak kenarı geriye gider — near/far clip
            // güvenli aralıkta kalsın diye mesafeye göre ayarlanır.
            _cam.nearClipPlane = Mathf.Max(0.1f, distance * 0.05f);
            _cam.farClipPlane  = distance * 4f + 100f;
        }

        /// <summary>
        /// Odak düzleminde <see cref="_matchOrthographicSize"/> kadar dikey
        /// yarı-genişlik veren kamera mesafesi.
        /// </summary>
        private float DistanceForOrthoMatch()
        {
            float halfFovRad = _fieldOfView * 0.5f * Mathf.Deg2Rad;
            float tan        = Mathf.Tan(halfFovRad);
            if (tan < 0.0001f) return 10f;
            return _matchOrthographicSize / tan;
        }

        private Vector3 SwayOffset()
        {
            if (!_idleSway || !Application.isPlaying) return Vector3.zero;
            if (GameSettings.ReduceMotion)             return Vector3.zero;

            float t = Time.unscaledTime * _swaySpeed;
            float x = (Mathf.PerlinNoise(_noiseSeed + t, 0f)          - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0f, _noiseSeed + t * 0.83f)  - 0.5f) * 2f;

            return new Vector3(x, y, 0f) * _swayAmplitude;
        }

        private void UpdateParallax()
        {
            if (!_mouseParallax || !Application.isPlaying || GameSettings.ReduceMotion)
            {
                _parallaxOffset = Vector2.Lerp(
                    _parallaxOffset, Vector2.zero, Time.unscaledDeltaTime * _parallaxDamping);
                return;
            }

            if (Mouse.current == null) return;

            Vector2 mp = Mouse.current.position.ReadValue();
            if (Screen.width <= 0 || Screen.height <= 0) return;

            // Ekran merkezine göre -1..1 aralığında normalize
            var normalized = new Vector2(
                Mathf.Clamp(mp.x / Screen.width  * 2f - 1f, -1f, 1f),
                Mathf.Clamp(mp.y / Screen.height * 2f - 1f, -1f, 1f));

            Vector2 target = normalized * _parallaxStrength;

            _parallaxOffset = Vector2.Lerp(
                _parallaxOffset, target, Time.unscaledDeltaTime * _parallaxDamping);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.94f, 0.73f, 0.30f, 0.9f);
            Gizmos.DrawWireSphere(AimWorld, 0.2f);
            Gizmos.DrawLine(transform.position, AimWorld);
        }
#endif
    }
}
