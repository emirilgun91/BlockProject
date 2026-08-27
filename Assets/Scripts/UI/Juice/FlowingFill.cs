using RogueBlockBlast.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.Juice
{
    /// <summary>
    /// Dolan barın düz renk yerine akan su gibi görünmesi.
    ///
    /// Yaklaşım: barın <see cref="Image"/> dolgusuna çocuk olarak bir
    /// <see cref="RawImage"/> eklenir ve dokusu yatayda kaydırılır.
    /// <c>RawImage.uvRect</c> kaydırmayı yerel olarak desteklediği için ek
    /// shader ya da materyal gerekmez.
    ///
    /// Neden dolgunun çocuğu: dolgu <c>Filled</c> tipinde olduğunda rect'i
    /// barın doluluğuyla birlikte değişir; çocuk da onunla birlikte kırpılır,
    /// yani dalga hep dolu kısımda kalır. Ayrı bir maske kurmaya gerek kalmaz.
    ///
    /// İki farklı hızda iki katman üst üste bindirilir — tek katman "kayan
    /// doku" gibi okunur, iki katman "akan sıvı" gibi.
    /// </summary>
    public sealed class FlowingFill : MonoBehaviour
    {
        [Header("Hedef")]
        [Tooltip("Barın dolgu Image'ı. Boşsa bu nesnedeki Image kullanılır.")]
        [SerializeField] private Image _fill;

        [Header("Akış")]
        // Sakin tutulur: bar sürekli göz önünde, güçlü akış hızla gürültüye dönüşüyor.
        [SerializeField] private float _speedA = 0.17f;
        [SerializeField] private float _speedB = -0.08f;

        [Tooltip("Dokunun yatayda kaç kez tekrarlanacağı. Yüksek = sık dalga.")]
        [SerializeField] private float _tilingA = 2.2f;
        [SerializeField] private float _tilingB = 1.3f;

        [Header("Görünüm")]
        [SerializeField] private float _alphaA = 0.11f;
        [SerializeField] private float _alphaB = 0.07f;

        [Tooltip("Dalganın rengi. Boş bırakılırsa dolgunun rengi kullanılır.")]
        [SerializeField] private bool  _useFillColor = true;
        [SerializeField] private Color _waveColor    = Color.white;

        private RawImage _layerA;
        private RawImage _layerB;
        private float    _offsetA;
        private float    _offsetB;

        private void Awake()
        {
            if (_fill == null) _fill = GetComponent<Image>();
            if (_fill == null)
            {
                Debug.LogWarning("[FlowingFill] Dolgu Image'ı yok — akış kurulmadı.");
                enabled = false;
                return;
            }

            _layerA = BuildLayer("FlowA", _tilingA, _alphaA);
            _layerB = BuildLayer("FlowB", _tilingB, _alphaB);
        }

        private void Update()
        {
            if (_layerA == null || _layerB == null) return;

            float vfx = Mathf.Clamp01(GameSettings.VfxIntensity);

            // ReduceMotion: akış durur ama katmanlar kalır — bar düz renk
            // yerine yine dokulu görünür, sadece hareket etmez.
            float dt = GameSettings.ReduceMotion ? 0f : Time.unscaledDeltaTime;

            _offsetA += dt * _speedA;
            _offsetB += dt * _speedB;

            float filled = FilledRatio();

            Apply(_layerA, _offsetA, _tilingA, _alphaA * vfx, filled);
            Apply(_layerB, _offsetB, _tilingB, _alphaB * vfx, filled);
        }

        /// <summary>
        /// Barın dolu oranı. <c>Image.type = Filled</c> RectTransform'u
        /// <b>değiştirmez</b> — kırpmayı shader yapar. Bu yüzden çocuk
        /// katmanlar kendiliğinden dolu kısımda kalmaz; oranı elle uygulamak
        /// gerekir, yoksa dalga boş barda da akar.
        /// </summary>
        private float FilledRatio()
        {
            if (_fill == null) return 1f;
            if (_fill.type != Image.Type.Filled) return 1f;
            return Mathf.Clamp01(_fill.fillAmount);
        }

        private void Apply(RawImage layer, float offset, float tiling, float alpha, float filled)
        {
            var rect = layer.rectTransform;

            // Katmanı dolu kısma daralt. Tiling de oranla ölçeklenir ki
            // dalganın sıklığı bar dolarken değişmesin.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(Mathf.Max(0.0001f, filled), 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            layer.uvRect = new Rect(offset, 0f, tiling * Mathf.Max(0.0001f, filled), 1f);

            var c = _useFillColor && _fill != null ? _fill.color : _waveColor;
            layer.color = new Color(c.r, c.g, c.b, alpha * filled);
        }

        private RawImage BuildLayer(string name, float tiling, float alpha)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_fill.rectTransform, worldPositionStays: false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin        = Vector2.zero;
            rect.anchorMax        = Vector2.one;
            rect.pivot            = new Vector2(0.5f, 0.5f);
            rect.offsetMin        = Vector2.zero;
            rect.offsetMax        = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            var raw = go.AddComponent<RawImage>();
            raw.texture       = JuiceGraphics.Wave.texture;
            raw.uvRect        = new Rect(0f, 0f, tiling, 1f);
            raw.raycastTarget = false;
            raw.color         = new Color(1f, 1f, 1f, alpha);

            return raw;
        }
    }
}
