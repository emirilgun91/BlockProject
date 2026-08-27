using DG.Tweening;
using RogueBlockBlast.Core;
using RogueBlockBlast.Core.Settings;
using RogueBlockBlast.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.Juice
{
    /// <summary>
    /// Çarpan yazısının ("x1,0") kalp atışı gibi nabzı ve arkasındaki glow halesi.
    /// Kombo yükseldikçe hem hale güçlenir hem atış hızlanır — ikisi de çok küçük
    /// oranlarda, HUD gürültüye dönüşmesin diye.
    ///
    /// <b>DOTween çakışması:</b> <see cref="ComboView"/> kombo değiştiğinde aynı
    /// yazının <c>localScale</c>'ini punch'lıyor. İkinci bir yazıcı o efekti
    /// ezerdi. Bu yüzden nabız yalnızca yazıda aktif bir tween <i>yokken</i>
    /// uygulanır; punch sırasında susar, bitince kaldığı yerden devam eder.
    /// Hale ise her zaman çalışır, onu kimse sürmüyor.
    ///
    /// Atış eğrisi düz sinüs değil: gerçek kalp gibi güçlü bir vuruş, hemen
    /// ardından küçük bir ikinci vuruş, sonra sessizlik.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ComboHeartbeat : MonoBehaviour
    {
        [Header("Hedef")]
        [Tooltip("Nabzı uygulanacak çarpan yazısı. Boşsa bu nesnedeki TMP_Text.")]
        [SerializeField] private TMP_Text _text;

        [Header("Nabız")]
        [SerializeField] private float _amplitude      = 0.055f;
        [Tooltip("Sakin haldeki atış sayısı (vuruş/sn).")]
        [SerializeField] private float _rateBase       = 0.75f;
        [Tooltip("En yüksek komboda hızın katı. Küçük tutun — HUD titremesin.")]
        [SerializeField] private float _rateMaxFactor  = 1.45f;

        [Header("Glow")]
        [SerializeField] private bool  _createGlow      = true;
        [SerializeField] private Color _glowColor       = new Color(0.95f, 0.72f, 0.24f, 1f);
        [Tooltip("Halenin yazıya göre boyutu.")]
        [SerializeField] private float _glowSizeFactor  = 2.6f;
        [SerializeField] private float _glowAlphaBase   = 0.16f;
        [Tooltip("En yüksek komboda halenin alfa katı.")]
        [SerializeField] private float _glowAlphaMax    = 0.42f;

        [Header("Kombo tepkisi")]
        [Tooltip("Çarpanın yoğunluğa katkısının yumuşaklığı — tavanı olmadığı " +
                 "için doyuma ulaşmayan üstel eğri kullanılır.")]
        [SerializeField] private float _multiplierSoftness = 2.2f;

        [SerializeField] private float _intensityDamping = 3f;

        private RunController _run;
        private bool          _bound;

        private Image   _glow;
        private Vector3 _baseScale = Vector3.one;

        private float _intensity;       // 0..1 yumuşatılmış
        private float _intensityTarget;
        private float _phase;

        private void Awake()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text != null) _baseScale = _text.transform.localScale;

            if (_createGlow) BuildGlow();
        }

        private void OnDestroy() => Unbind();

        private void Update()
        {
            if (!_bound) TryBind();

            float dt = Time.unscaledDeltaTime;
            _intensity = Mathf.Lerp(
                _intensity, _intensityTarget, 1f - Mathf.Exp(-_intensityDamping * dt));

            float vfx  = Mathf.Clamp01(GameSettings.VfxIntensity);
            float beat = Advance(dt, vfx);

            ApplyScale(beat, vfx);
            ApplyGlow(beat, vfx);
        }

        // ── Bağlanma ─────────────────────────────────────────────────────────

        private void TryBind()
        {
            if (_run == null) _run = FindFirstObjectByType<RunController>();

            var combo = _run != null ? _run.Combo : null;
            if (combo == null) return;

            combo.OnStateChanged += HandleCombo;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound || _run == null || _run.Combo == null) return;
            _run.Combo.OnStateChanged -= HandleCombo;
            _bound = false;
        }

        private void HandleCombo(ComboState state)
        {
            // Çarpanın tavanı yok (ComboSystem sınırsız büyütür), o yüzden
            // doyuma ulaşmayan bir eğri. Şarj oranı da katılır — combo barı
            // 3 de olsa 5 de olsa kendini ayarlar.
            float over = Mathf.Max(0f, state.Multiplier - 1f);
            float mult = 1f - Mathf.Exp(-over / Mathf.Max(0.05f, _multiplierSoftness));

            float charge = state.MaxCharge > 0
                ? Mathf.Clamp01((float)state.Charges / state.MaxCharge)
                : 0f;

            _intensityTarget = Mathf.Clamp01(Mathf.Max(mult, charge * 0.75f));
        }

        // ── Atış ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Fazı ilerletir ve 0..1 arası atış değeri döndürür.
        /// Eğri: güçlü vuruş → küçük ikinci vuruş → sessizlik.
        /// </summary>
        private float Advance(float dt, float vfx)
        {
            if (GameSettings.ReduceMotion || vfx <= 0.001f) return 0f;

            float rate = _rateBase * Mathf.Lerp(1f, _rateMaxFactor, _intensity);
            _phase += dt * rate;
            _phase -= Mathf.Floor(_phase);       // 0..1 arasında tut

            float t = _phase;

            // İlk vuruş: 0.00–0.18. İkinci vuruş: 0.22–0.36. Kalanı sessizlik.
            if (t < 0.18f)  return Mathf.Sin(t / 0.18f * Mathf.PI);
            if (t < 0.22f)  return 0f;
            if (t < 0.36f)  return Mathf.Sin((t - 0.22f) / 0.14f * Mathf.PI) * 0.55f;
            return 0f;
        }

        private void ApplyScale(float beat, float vfx)
        {
            if (_text == null) return;

            var tr = _text.transform;

            // ComboView punch'ı sürerken karışma — bitince devral.
            if (DOTween.IsTweening(tr)) return;

            if (GameSettings.ReduceMotion || vfx <= 0.001f)
            {
                tr.localScale = _baseScale;
                return;
            }

            tr.localScale = _baseScale * (1f + beat * _amplitude * vfx);
        }

        private void ApplyGlow(float beat, float vfx)
        {
            if (_glow == null) return;

            float alpha = Mathf.Lerp(_glowAlphaBase, _glowAlphaMax, _intensity);
            alpha *= 0.65f + beat * 0.35f;     // atışla birlikte hafifçe kabarır
            alpha *= vfx;

            _glow.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, alpha);

            float scale = 1f + beat * 0.10f * vfx + _intensity * 0.08f;
            _glow.rectTransform.localScale = Vector3.one * scale;
        }

        // ── Hale ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Haleyi yazının arkasına, kardeş sırasında ilk çocuk olarak koyar.
        /// Yazının kendi transform'una dokunulmaz — ComboView'ın punch'ıyla
        /// çakışmasın diye.
        /// </summary>
        private void BuildGlow()
        {
            if (_text == null) return;

            var parent = _text.rectTransform.parent as RectTransform;
            if (parent == null) return;

            var go = new GameObject("ComboGlow", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.SetSiblingIndex(_text.rectTransform.GetSiblingIndex());

            var rect = go.GetComponent<RectTransform>();
            var src  = _text.rectTransform;

            rect.anchorMin        = src.anchorMin;
            rect.anchorMax        = src.anchorMax;
            rect.pivot            = src.pivot;
            rect.anchoredPosition = src.anchoredPosition;
            rect.sizeDelta        = src.sizeDelta * _glowSizeFactor;

            // Combo panelinde bir Layout Group varsa hale onu bozmasın.
            go.AddComponent<LayoutElement>().ignoreLayout = true;

            _glow = go.AddComponent<Image>();
            _glow.sprite        = JuiceGraphics.RadialGlow;
            _glow.raycastTarget = false;
            _glow.color         = new Color(_glowColor.r, _glowColor.g, _glowColor.b, _glowAlphaBase);
        }
    }
}
