using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Toggle'ı kayan bir anahtar gibi çizer: açıkken iz vurgu rengine döner ve
    /// topuz sağa kayar.
    ///
    /// Değeri <c>onValueChanged</c>'den değil her karede <see cref="Toggle.isOn"/>'dan
    /// okur: <c>SettingsController</c> değerleri <c>SetIsOnWithoutNotify</c> ile
    /// yazıyor, olay hiç gelmiyor. Okuma ucuz, ve panel açıkken anahtar her zaman
    /// gerçek değeri gösteriyor.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public sealed class KitSwitch : MonoBehaviour
    {
        [SerializeField] private Image         _track;
        [SerializeField] private Image         _trackRim;
        [SerializeField] private RectTransform _knob;
        [SerializeField] private Image         _knobImage;
        [SerializeField] private float         _travel = 15f;     // topuzun merkezden sağa/sola kayması
        [SerializeField] private float         _speed  = 14f;

        private Toggle _toggle;
        private float  _t;                      // 0 = kapalı, 1 = açık

        public void Configure(Image track, Image trackRim, RectTransform knob, Image knobImage, float travel)
        {
            _track = track; _trackRim = trackRim; _knob = knob; _knobImage = knobImage; _travel = travel;
        }

        private void OnEnable()
        {
            _toggle = GetComponent<Toggle>();
            _t = _toggle.isOn ? 1f : 0f;
            Apply();
        }

        private void Update()
        {
            float target = _toggle.isOn ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime * _speed * 0.5f);
            Apply();
        }

        private void Apply()
        {
            float e = _t * _t * (3f - 2f * _t);   // smoothstep
            bool live = _toggle == null || _toggle.IsInteractable();
            float a = live ? 1f : 0.4f;

            if (_track != null)
                _track.color = Alpha(Color.Lerp(UiTheme.Sunken, UiTheme.WithAlpha(UiTheme.Accent, 0.35f), e), a);
            if (_trackRim != null)
                _trackRim.color = Alpha(Color.Lerp(UiTheme.RimSoft, UiTheme.Accent, e), a);
            if (_knobImage != null)
                _knobImage.color = Alpha(Color.Lerp(UiTheme.TextDim, Color.white, e), a);
            if (_knob != null)
                _knob.anchoredPosition = new Vector2(Mathf.Lerp(-_travel, _travel, e), 0f);
        }

        private static Color Alpha(Color c, float k) { c.a *= k; return c; }
    }
}
