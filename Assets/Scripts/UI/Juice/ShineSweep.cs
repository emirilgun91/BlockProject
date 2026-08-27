using RogueBlockBlast.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.Juice
{
    /// <summary>
    /// Bir panelin üstünden ara sıra geçen parlama süpürmesi.
    ///
    /// Sürekli değil: iki süpürme arasında belirgin bir sessizlik var. Sürekli
    /// parlayan bir panel gürültüye dönüşür; ara verilince göz her seferinde
    /// tekrar yakalar. Aralığa küçük bir rastgelelik eklenir ki ritim
    /// makineleşmesin.
    ///
    /// Maskeleme gerektirmez — şerit uçlarda zaten saydam olduğu için panel
    /// sınırında sert kesim oluşmaz; ayrıca giriş/çıkışta alfa ile solar.
    /// </summary>
    public sealed class ShineSweep : MonoBehaviour
    {
        [Header("Hedef")]
        [Tooltip("Üzerinden geçilecek alan. Boşsa bu nesnenin RectTransform'u.")]
        [SerializeField] private RectTransform _area;

        [Header("Zamanlama")]
        [Tooltip("İki süpürme arası bekleme (sn).")]
        [SerializeField] private float _interval       = 5.5f;
        [Tooltip("Beklemeye eklenen rastgelelik — ritim makineleşmesin.")]
        [SerializeField] private float _intervalJitter = 2.5f;
        [SerializeField] private float _sweepDuration  = 0.85f;
        [Tooltip("İlk süpürmeden önceki gecikme.")]
        [SerializeField] private float _initialDelay   = 2f;

        [Header("Görünüm")]
        [SerializeField] private Color _color      = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private float _peakAlpha  = 0.16f;
        [Tooltip("Şeridin genişliği, alanın genişliğine oran olarak.")]
        [SerializeField] private float _widthRatio = 0.14f;
        [Tooltip("Şeridin yüksekliği, alanın yüksekliğine oran olarak. " +
                 "Eğim verildiği için 1'den biraz büyük olmalı ki köşeler boş kalmasın.")]
        [SerializeField] private float _heightRatio = 1.25f;
        [Tooltip("Şeridin eğimi (derece). 0 = dik.")]
        [SerializeField] private float _tilt       = 14f;

        private Image _streak;
        private float _timer;
        private float _sweepTime = -1f;   // <0 = süpürme yok
        private float _nextWait;

        private void Awake()
        {
            if (_area == null) _area = transform as RectTransform;
            BuildStreak();

            _timer    = 0f;
            _nextWait = _initialDelay;
        }

        private void Update()
        {
            if (_streak == null || _area == null) return;

            float dt  = Time.unscaledDeltaTime;
            float vfx = Mathf.Clamp01(GameSettings.VfxIntensity);

            if (GameSettings.ReduceMotion || vfx <= 0.001f)
            {
                _streak.enabled = false;
                return;
            }

            if (_sweepTime < 0f)
            {
                _streak.enabled = false;
                _timer += dt;

                if (_timer >= _nextWait)
                {
                    _timer     = 0f;
                    _sweepTime = 0f;
                    _nextWait  = _interval + Random.Range(0f, _intervalJitter);

                    // Boyut her süpürmede güncel rect'ten hesaplanır. Awake'te
                    // hesaplamak güvenilmez: layout henüz çalışmamış olabilir
                    // ve rect 0 ya da yanlış ölçüde okunur.
                    ResizeStreak();
                }
                return;
            }

            _sweepTime += dt;
            float t = _sweepTime / Mathf.Max(0.01f, _sweepDuration);

            if (t >= 1f)
            {
                _sweepTime      = -1f;
                _streak.enabled = false;
                return;
            }

            _streak.enabled = true;

            float width = _area.rect.width;
            float travel = width * 1.6f;

            var rect = _streak.rectTransform;
            rect.anchoredPosition = new Vector2(Mathf.Lerp(-travel * 0.5f, travel * 0.5f, t), 0f);

            // Uçlarda solarak gir/çık — panel kenarında ani beliriş olmasın.
            float edgeFade = Mathf.Sin(t * Mathf.PI);
            _streak.color = new Color(_color.r, _color.g, _color.b, _peakAlpha * edgeFade * vfx);
        }

        /// <summary>Şeridi alanın güncel ölçüsüne göre boyutlandırır.</summary>
        private void ResizeStreak()
        {
            if (_streak == null || _area == null) return;

            float w = Mathf.Max(6f, _area.rect.width  * _widthRatio);
            float h = Mathf.Max(6f, _area.rect.height * _heightRatio);

            _streak.rectTransform.sizeDelta = new Vector2(w, h);
        }

        private void BuildStreak()
        {
            if (_area == null) return;

            var go = new GameObject("ShineStreak", typeof(RectTransform));
            go.transform.SetParent(_area, worldPositionStays: false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin     = new Vector2(0.5f, 0.5f);
            rect.anchorMax     = new Vector2(0.5f, 0.5f);
            rect.pivot         = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.Euler(0f, 0f, _tilt);

            // Panelde bir Layout Group varsa şerit onu bozmasın.
            go.AddComponent<LayoutElement>().ignoreLayout = true;

            _streak = go.AddComponent<Image>();
            _streak.sprite        = JuiceGraphics.Streak;
            _streak.raycastTarget = false;
            _streak.enabled       = false;
        }
    }
}
