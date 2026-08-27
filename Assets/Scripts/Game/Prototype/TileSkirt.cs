using UnityEngine;

namespace RogueBlockBlast.Game.Prototype
{
    /// <summary>
    /// 2.5D prototipinde tile'lara kalınlık hissi veren "etek" — gerçek mesh
    /// değil, ikinci bir sprite.
    ///
    /// Geometri: tahta dünya z = 0 düzleminde. Bu bileşen tile kökünü
    /// <see cref="_depth"/> kadar kameraya doğru (−z) kaldırır ve aynı sprite'ın
    /// koyu bir kopyasını tam z = 0'da bırakır. Eğimli kamerada üst yüz ile taban
    /// arasındaki bu boşluk blok yan yüzü gibi okunur.
    ///
    /// Neden LateUpdate'te bir kez: <see cref="UI.BoardView.Build"/> tile'ı
    /// Instantiate edip pozisyonunu hemen yazıyor. Awake/Start sırası yerine
    /// ilk LateUpdate'te uygulamak, Build'in yazdığı konumun üzerine güvenle
    /// eklenmeyi garanti eder. Board yeniden Build edilirse tile'lar yok edilip
    /// yeniden yaratıldığı için bileşen de sıfırdan uygulanır.
    ///
    /// Prototip notu: bu geçici bir çözüm. Kalıcı 3D geçişinde tile'lar gerçek
    /// mesh olacağı için bu bileşen tamamen kalkar.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TileSkirt : MonoBehaviour
    {
        [Header("Depth")]
        [Tooltip("Blok kalınlığı (dünya birimi). CellSize 0.85 için 0.24–0.34 arası iyi okunuyor.\n\n" +
                 "Ekranda görünen yükseklik = depth × sin(tilt). Tile'lar arası boşluk " +
                 "sadece 0.04 birim olduğu için 0.22'nin altında yan yüz neredeyse hiç okunmuyor.")]
        [SerializeField] private float _depth = 0.28f;

        [Header("Shading")]
        [Tooltip("Yan yüzün üst yüze göre koyulaşma oranı.")]
        [Range(0f, 1f)]
        [SerializeField] private float _darken = 0.55f;

        [Tooltip("Bu parlaklığın altındaki tile'lar boş kabul edilir ve etek gizlenir. " +
                 "Boş hücre rengi (#1c2132) ~0.13, dolu palet renkleri ~0.45+.")]
        [Range(0f, 1f)]
        [SerializeField] private float _emptyLuminanceThreshold = 0.22f;

        [Tooltip("Yarı saydam hücrelerde (ghost önizleme, alpha 0.10) etek gizlenir.")]
        [Range(0f, 1f)]
        [SerializeField] private float _minAlpha = 0.5f;

        private SpriteRenderer _top;
        private SpriteRenderer _side;
        private bool           _lifted;

        private void Awake()
        {
            _top = GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (_top == null) return;

            if (!_lifted)
            {
                ApplyLift();
                _lifted = true;
            }

            SyncSide();
        }

        private void OnDisable()
        {
            if (_side != null) _side.enabled = false;
        }

        // ── Private ──────────────────────────────────────────────────────────

        /// <summary>
        /// Üst yüzü kameraya doğru kaldırır ve tabanı z = 0'da bırakan yan yüz
        /// sprite'ını oluşturur.
        /// </summary>
        private void ApplyLift()
        {
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, p.y, p.z - _depth);

            if (_side != null) return;

            var go = new GameObject("Skirt");
            go.transform.SetParent(transform, worldPositionStays: false);
            // Yerel +z, tile kaldırıldığı için dünyada tam z = 0'a denk gelir.
            go.transform.localPosition = new Vector3(0f, 0f, _depth);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;

            _side = go.AddComponent<SpriteRenderer>();
            _side.sprite       = _top.sprite;
            _side.sortingLayerID = _top.sortingLayerID;
            // Üst yüz her zaman kazanır — tile'lar çakışmadığı için tek kademe yeter.
            _side.sortingOrder  = _top.sortingOrder - 1;
        }

        private void SyncSide()
        {
            if (_side == null) return;

            Color c = _top.color;

            bool visible = _top.enabled
                           && c.a >= _minAlpha
                           && Luminance(c) >= _emptyLuminanceThreshold;

            _side.enabled = visible;
            if (!visible) return;

            if (_side.sprite != _top.sprite) _side.sprite = _top.sprite;

            _side.color = new Color(
                c.r * (1f - _darken),
                c.g * (1f - _darken),
                c.b * (1f - _darken),
                c.a);
        }

        private static float Luminance(Color c)
            => c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
    }
}
