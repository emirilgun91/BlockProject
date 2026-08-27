using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.Game.Prototype
{
    /// <summary>
    /// Dünya-uzayında duran tek bir kartın fiziksel davranışı.
    ///
    /// Görsel içerik bu sınıfın işi değil — kart yüzü mevcut <c>CardView_0</c>
    /// prefabıdır ve bir World Space Canvas içinde çocuk olarak durur. Burada
    /// yalnızca <b>kök transform</b> sürülür: uçuş, yerine yaylanma, hover,
    /// çıkış. İkisi çakışmaz çünkü CardView kendi yerel konumunu oynatır, biz
    /// kökü oynatırız.
    ///
    /// <b>Yönler kameraya göredir.</b> "Yukarı" dünya +Y değil, kameranın
    /// yukarısıdır; "yükselmek" dünya −Z değil, kameraya doğrudur. Eğimli
    /// kamerada dünya eksenleriyle çalışmak kartları ekranda yukarı kaçırıp
    /// devleştiriyordu — bkz. <see cref="SetBasis"/>.
    ///
    /// Her şey <see cref="Time.unscaledDeltaTime"/> ile çalışır: kart seçimi
    /// sırasında <c>Time.timeScale = 0</c>.
    /// </summary>
    public sealed class PhysicalCard : MonoBehaviour
    {
        private enum Phase { Dealing, Settled, Leaving }

        // ── Hedef poz ────────────────────────────────────────────────────────
        private Vector3    _slotPosition;
        private Quaternion _slotRotation;
        private float      _slotScale = 1f;

        // ── Kamera bazlı yönler ──────────────────────────────────────────────
        private Vector3 _up       = Vector3.up;
        private Vector3 _toCamera = Vector3.back;

        // ── Uçuş ─────────────────────────────────────────────────────────────
        private Vector3    _dealFrom;
        private Quaternion _dealFromRotation;
        private float      _dealDuration;
        private float      _dealDelay;
        private float      _dealElapsed;
        private float      _arcHeight;

        // ── Çıkış ────────────────────────────────────────────────────────────
        private Vector3 _leaveTarget;
        private float   _leaveDuration = 0.45f;
        private float   _leaveElapsed;
        private bool    _leaveShrink;

        // ── Durum ────────────────────────────────────────────────────────────
        private Phase _phase = Phase.Dealing;
        private float _hover;
        private float _hoverTarget;
        private float _swaySeed;

        // ── Ayarlar ──────────────────────────────────────────────────────────
        private float _hoverLift     = 0.35f;
        private float _hoverScale    = 1.12f;
        private float _hoverSpeed    = 9f;
        private float _settleDamping = 12f;
        private float _swayAmplitude = 0.02f;

        private CanvasGroup   _canvasGroup;
        private RectTransform _shadow;
        private Graphic       _shadowGraphic;
        private Vector2       _shadowRestOffset = new Vector2(10f, -12f);

        /// <summary>Kart yerine oturdu ve tıklanabilir durumda mı.</summary>
        public bool IsInteractive => _phase == Phase.Settled;

        // ── Kurulum ──────────────────────────────────────────────────────────

        public void Configure(
            CanvasGroup   canvasGroup,
            RectTransform shadow,
            Graphic       shadowGraphic,
            float         hoverLift,
            float         hoverScale,
            float         swayAmplitude)
        {
            _canvasGroup   = canvasGroup;
            _shadow        = shadow;
            _shadowGraphic = shadowGraphic;
            _hoverLift     = hoverLift;
            _hoverScale    = hoverScale;
            _swayAmplitude = swayAmplitude;
            _swaySeed      = Random.value * 100f;
        }

        /// <summary>
        /// Kartın hareket edeceği eksenler. Kamera eğimli olduğu için dünya
        /// eksenleri kullanılamaz: kameranın "yukarı"sı ve kameraya doğru olan
        /// yön dışarıdan verilir.
        /// </summary>
        public void SetBasis(Vector3 up, Vector3 toCamera)
        {
            _up       = up.normalized;
            _toCamera = toCamera.normalized;
        }

        public void SetSlot(Vector3 position, Quaternion rotation, float scale)
        {
            _slotPosition = position;
            _slotRotation = rotation;
            _slotScale    = scale;
        }

        public void Deal(Vector3 from, float delay, float duration, float arcHeight, float spinDegrees)
        {
            _dealFrom         = from;
            _dealFromRotation = _slotRotation * Quaternion.Euler(0f, 0f, spinDegrees);
            _dealDelay        = delay;
            _dealDuration     = Mathf.Max(0.01f, duration);
            _arcHeight        = arcHeight;
            _dealElapsed      = 0f;
            _phase            = Phase.Dealing;

            transform.SetPositionAndRotation(from, _dealFromRotation);
            transform.localScale = Vector3.one * (_slotScale * 0.6f);

            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        public void Leave(Vector3 target, bool shrink, float duration = 0.45f)
        {
            _leaveTarget   = target;
            _leaveShrink   = shrink;
            _leaveDuration = Mathf.Max(0.01f, duration);
            _leaveElapsed  = 0f;
            _phase         = Phase.Leaving;
            _hoverTarget   = 0f;
        }

        public void SetHovered(bool hovered)
        {
            if (_phase != Phase.Settled) return;
            _hoverTarget = hovered ? 1f : 0f;
        }

        // ── Unity ────────────────────────────────────────────────────────────

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            switch (_phase)
            {
                case Phase.Dealing: TickDealing(dt); break;
                case Phase.Settled: TickSettled(dt); break;
                case Phase.Leaving: TickLeaving(dt); break;
            }

            UpdateShadow();
        }

        // ── Fazlar ───────────────────────────────────────────────────────────

        private void TickDealing(float dt)
        {
            if (_dealDelay > 0f) { _dealDelay -= dt; return; }

            _dealElapsed += dt;
            float t    = Mathf.Clamp01(_dealElapsed / _dealDuration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);   // yavaşlayarak varış

            Vector3 p = Vector3.Lerp(_dealFrom, _slotPosition, ease);
            // Parabol — kameranın yukarısına doğru, dünya +Y'ye değil.
            p += _up * (Mathf.Sin(ease * Mathf.PI) * _arcHeight);

            transform.position   = p;
            transform.rotation   = Quaternion.Slerp(_dealFromRotation, _slotRotation, ease);
            transform.localScale = Vector3.one * Mathf.Lerp(_slotScale * 0.6f, _slotScale, ease);

            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Clamp01(t * 3f);

            if (t >= 1f) _phase = Phase.Settled;
        }

        private void TickSettled(float dt)
        {
            _hover = Mathf.MoveTowards(_hover, _hoverTarget, dt * _hoverSpeed);

            // Nefes alma — hover'dayken susar, kart "tutulmuş" gibi sabitlenir.
            float noise = (Mathf.PerlinNoise(_swaySeed + Time.unscaledTime * 0.4f, 0f) - 0.5f) * 2f;
            float sway  = noise * _swayAmplitude * (1f - _hover);

            Vector3 target = _slotPosition
                             + _up       * (_hoverLift * _hover + sway)
                             + _toCamera * (_hoverLift * 0.6f * _hover);

            // Hover'da kart yelpaze eğiminden kurtulup düzelir.
            Quaternion targetRot = Quaternion.Slerp(
                _slotRotation,
                Quaternion.LookRotation(-_toCamera, _up),
                _hover * 0.8f);

            float targetScale = Mathf.Lerp(_slotScale, _slotScale * _hoverScale, _hover);
            float k           = 1f - Mathf.Exp(-_settleDamping * dt);

            transform.position   = Vector3.Lerp(transform.position, target, k);
            transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, k);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, k);

            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        }

        private void TickLeaving(float dt)
        {
            _leaveElapsed += dt;
            float t    = Mathf.Clamp01(_leaveElapsed / _leaveDuration);
            float ease = t * t;   // hızlanarak — çekilip alınmış gibi

            transform.position = Vector3.Lerp(transform.position, _leaveTarget, ease);

            if (_leaveShrink)
                transform.localScale = Vector3.Lerp(
                    transform.localScale, Vector3.one * (_slotScale * 0.25f), ease);

            if (_canvasGroup != null) _canvasGroup.alpha = 1f - ease;

            if (t >= 1f) Destroy(gameObject);
        }

        // ── Gölge ────────────────────────────────────────────────────────────

        /// <summary>
        /// Gölge kartla <b>aynı canvas'ın içinde</b>, kartın arkasındaki
        /// kardeş olarak duruyor. Ayrı bir SpriteRenderer olsaydı sorting
        /// layer'ları elle yönetmek gerekirdi; canvas içinde sıra otomatik.
        ///
        /// Kart yükseldikçe gölge uzaklaşır, büyür ve soluklaşır — fiziksellik
        /// hissi bu ayrışmadan geliyor.
        /// </summary>
        private void UpdateShadow()
        {
            if (_shadow == null) return;

            float spread = 1f + _hover * 2.2f;

            _shadow.anchoredPosition = _shadowRestOffset * spread;
            _shadow.localScale       = Vector3.one * (1f + _hover * 0.06f);

            if (_shadowGraphic != null)
            {
                float alpha = Mathf.Lerp(0.55f, 0.32f, _hover);
                var   c     = _shadowGraphic.color;
                _shadowGraphic.color = new Color(c.r, c.g, c.b, alpha);
            }
        }
    }
}
